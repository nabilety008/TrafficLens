using System.Runtime.Versioning;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

/// <summary>
/// Tests for version detection. All tests run against an injected key
/// abstraction — no test ever touches the real HKLM.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsVersionDetectorTests
{
    private const string Build11 = "26100";   // Windows 11 24H2-era build
    private const string Build10 = "19045";   // Windows 10 22H2-era build

    private static WindowsVersionDetectionResult Detect(Dictionary<string, string?> values) =>
        new WindowsVersionDetector(() => new FakeVersionKey(values)).Detect();

    [Fact]
    public void Detect_Windows11BuildWithDisplayVersion_ReturnsWindows11AndRelease()
    {
        var result = Detect(new Dictionary<string, string?>
        {
            ["CurrentBuildNumber"] = Build11,
            ["DisplayVersion"] = "24H2"
        });

        Assert.True(result.IsUsable);
        Assert.Equal("Windows 11", result.Info.ProductVersion);
        Assert.Equal("24H2", result.Info.Release);
        Assert.Equal(26100, result.Info.BuildNumber);
    }

    [Fact]
    public void Detect_Windows10BuildWithDisplayVersion_ReturnsWindows10AndRelease()
    {
        var result = Detect(new Dictionary<string, string?>
        {
            ["CurrentBuildNumber"] = Build10,
            ["DisplayVersion"] = "22H2"
        });

        Assert.True(result.IsUsable);
        Assert.Equal("Windows 10", result.Info.ProductVersion);
        Assert.Equal("22H2", result.Info.Release);
    }

    [Fact]
    public void Detect_Windows11DoesNotRelyOnMarketingVersionString()
    {
        // Windows 11 still reports NT 10.0; only the build distinguishes it.
        var result = Detect(new Dictionary<string, string?>
        {
            ["CurrentBuildNumber"] = "22000",
            ["DisplayVersion"] = "21H2",
            ["ProductName"] = "Windows 10 Pro"   // known stale value on Win 11
        });

        Assert.True(result.IsUsable);
        Assert.Equal("Windows 11", result.Info.ProductVersion);
    }

    [Fact]
    public void Detect_MissingDisplayVersion_FallsBackToReleaseId()
    {
        var result = Detect(new Dictionary<string, string?>
        {
            ["CurrentBuildNumber"] = Build11,
            ["ReleaseId"] = "23H2"
        });

        Assert.True(result.IsUsable);
        Assert.Equal("23H2", result.Info.Release);
    }

    [Fact]
    public void Detect_MissingReleaseMetadata_IsUnsupportedAndNotUsable()
    {
        var result = Detect(new Dictionary<string, string?>
        {
            ["CurrentBuildNumber"] = Build11
        });

        Assert.False(result.IsUsable);
        Assert.Equal(WindowsVersionDetectionFailure.UnknownRelease, result.Failure);
        Assert.Null(result.Info.Release);
    }

    [Fact]
    public void Detect_MalformedReleaseValue_IsRejectedNotUsed()
    {
        var result = Detect(new Dictionary<string, string?>
        {
            ["CurrentBuildNumber"] = Build11,
            ["DisplayVersion"] = "garbage"
        });

        Assert.False(result.IsUsable);
        Assert.Equal(WindowsVersionDetectionFailure.UnknownRelease, result.Failure);
    }

    [Fact]
    public void Detect_MissingBuildNumber_IsUnknownProduct()
    {
        var result = Detect(new Dictionary<string, string?>
        {
            ["DisplayVersion"] = "24H2"
        });

        Assert.False(result.IsUsable);
        Assert.Equal(WindowsVersionDetectionFailure.UnknownProduct, result.Failure);
        Assert.Null(result.Info.ProductVersion);
    }

    [Fact]
    public void Detect_MissingKey_IsUnreadable()
    {
        var detector = new WindowsVersionDetector(() => null);

        var result = detector.Detect();

        Assert.False(result.IsUsable);
        Assert.Equal(WindowsVersionDetectionFailure.Unreadable, result.Failure);
    }

    [Theory]
    [InlineData("21H1", "21H1")]
    [InlineData("21h2", "21H2")]
    [InlineData("20H2", "20H2")]
    public void NormalizeRelease_AcceptsWellFormedReleases(string candidate, string expected)
    {
        Assert.Equal(expected, WindowsVersionDetector.NormalizeRelease(candidate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("24")]
    [InlineData("24H")]
    [InlineData("24H3")]
    [InlineData("2QH2")]
    [InlineData("HH22")]
    [InlineData("24HH")]
    public void NormalizeRelease_RejectsUnusableValues(string? candidate)
    {
        Assert.Null(WindowsVersionDetector.NormalizeRelease(candidate));
    }

    private sealed class FakeVersionKey : IWindowsVersionKey
    {
        private readonly Dictionary<string, string?> _values;

        public FakeVersionKey(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public object? GetValue(string name) =>
            _values.TryGetValue(name, out var value) ? value : null;

        public void Dispose()
        {
        }
    }
}
