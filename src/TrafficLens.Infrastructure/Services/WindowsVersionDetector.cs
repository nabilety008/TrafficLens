using System.Runtime.Versioning;

namespace TrafficLens.Infrastructure.Services;

/// <summary>
/// The Windows product and feature release TrafficLens detected on this machine,
/// e.g. "Windows 11" + "24H2". <see cref="Release"/> is null when Windows does
/// not expose usable release metadata, in which case the feature-update hold
/// must not be applied.
/// </summary>
public sealed class WindowsVersionInfo
{
    /// <summary>"Windows 10" or "Windows 11"; null when the OS could not be identified.</summary>
    public string? ProductVersion { get; init; }

    /// <summary>Feature release display version such as "22H2" or "24H2"; null when unknown.</summary>
    public string? Release { get; init; }

    /// <summary>Build number (e.g. 26100); null when unavailable.</summary>
    public int? BuildNumber { get; init; }

    public bool IsUsable => ProductVersion is not null && Release is not null;
}

/// <summary>Why a Windows release could not be resolved for the feature-update hold.</summary>
public enum WindowsVersionDetectionFailure
{
    None,

    /// <summary>The OS version metadata could not be read at all.</summary>
    Unreadable,

    /// <summary>The OS was identified but the feature release (DisplayVersion/ReleaseId) is missing or unusable.</summary>
    UnknownRelease,

    /// <summary>The OS build does not report a product TrafficLens can express in the policy.</summary>
    UnknownProduct
}

[SupportedOSPlatform("windows")]
public interface IWindowsVersionDetector
{
    WindowsVersionDetectionResult Detect();
}

public sealed class WindowsVersionDetectionResult
{
    private WindowsVersionDetectionResult(WindowsVersionInfo info, WindowsVersionDetectionFailure failure, string? error)
    {
        Info = info;
        Failure = failure;
        Error = error;
    }

    public WindowsVersionInfo Info { get; }

    public WindowsVersionDetectionFailure Failure { get; }

    public string? Error { get; }

    public bool IsUsable => Failure == WindowsVersionDetectionFailure.None && Info.IsUsable;

    public static WindowsVersionDetectionResult Success(WindowsVersionInfo info) =>
        new(info, WindowsVersionDetectionFailure.None, null);

    public static WindowsVersionDetectionResult Unsupported(WindowsVersionDetectionFailure failure, string error) =>
        new(new WindowsVersionInfo(), failure, error);
}

[SupportedOSPlatform("windows")]
internal interface IWindowsVersionKey : IDisposable
{
    object? GetValue(string name);
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsVersionRegistryKey : IWindowsVersionKey
{
    private readonly Microsoft.Win32.RegistryKey _key;

    public WindowsVersionRegistryKey(Microsoft.Win32.RegistryKey key)
    {
        _key = key;
    }

    public object? GetValue(string name) => _key.GetValue(name);

    public void Dispose() => _key.Dispose();
}

/// <summary>
/// Resolves the installed Windows product ("Windows 10"/"Windows 11") and the
/// current feature release ("22H2"/"24H2"/...) from stable local OS metadata:
/// the display version (DisplayVersion, formerly ReleaseId) and the build
/// number under HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion. Windows 11
/// still reports NT 10.0, so the product is derived from the build number
/// (build 22000 and above is Windows 11), never from marketing strings.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsVersionDetector : IWindowsVersionDetector
{
    public const string CurrentVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private readonly Func<IWindowsVersionKey?> _openCurrentVersionKey;

    public WindowsVersionDetector()
        : this(() =>
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(CurrentVersionKeyPath, false);
            return key is null ? null : new WindowsVersionRegistryKey(key);
        })
    {
    }

    internal WindowsVersionDetector(Func<IWindowsVersionKey?> openCurrentVersionKey)
    {
        _openCurrentVersionKey = openCurrentVersionKey;
    }

    public WindowsVersionDetectionResult Detect()
    {
        IWindowsVersionKey? key = null;
        try
        {
            key = _openCurrentVersionKey();
            if (key is null)
            {
                return WindowsVersionDetectionResult.Unsupported(
                    WindowsVersionDetectionFailure.Unreadable,
                    "The Windows version information could not be read.");
            }

            var build = key.GetValue("CurrentBuildNumber") as string;
            int? buildNumber = int.TryParse(build, out var parsedBuild) ? parsedBuild : null;
            var product = ResolveProduct(buildNumber);

            var release = ResolveRelease(key);

            if (product is null)
            {
                return WindowsVersionDetectionResult.Unsupported(
                    WindowsVersionDetectionFailure.UnknownProduct,
                    "The installed Windows product could not be identified.");
            }

            if (release is null)
            {
                return WindowsVersionDetectionResult.Unsupported(
                    WindowsVersionDetectionFailure.UnknownRelease,
                    "The current Windows feature release could not be determined.");
            }

            return WindowsVersionDetectionResult.Success(new WindowsVersionInfo
            {
                ProductVersion = product,
                Release = release,
                BuildNumber = buildNumber
            });
        }
        catch (Exception ex)
        {
            return WindowsVersionDetectionResult.Unsupported(
                WindowsVersionDetectionFailure.Unreadable,
                ex.Message);
        }
        finally
        {
            key?.Dispose();
        }
    }

    internal static string? ResolveProduct(int? buildNumber)
    {
        if (buildNumber is null || buildNumber < 0)
        {
            return null;
        }

        // Windows 11 kept the NT 10.0 version banner; the build number is the
        // only stable local signal (22000 is the first Windows 11 build).
        return buildNumber.Value >= 22000 ? "Windows 11" : "Windows 10";
    }

    private static string? ResolveRelease(IWindowsVersionKey key)
    {
        // DisplayVersion (20H2+) is the supported release identifier; older
        // builds exposed ReleaseId instead. Only well-formed YYH1/YYH2 values
        // are accepted so a malformed value can never become the policy target.
        var release = NormalizeRelease(key.GetValue("DisplayVersion") as string)
            ?? NormalizeRelease(key.GetValue("ReleaseId") as string);
        return release;
    }

    internal static string? NormalizeRelease(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var trimmed = candidate.Trim();
        if (trimmed.Length != 4 || !char.IsDigit(trimmed[0]) || !char.IsDigit(trimmed[1])
            || (trimmed[2] != 'H' && trimmed[2] != 'h')
            || trimmed[3] != '1' && trimmed[3] != '2')
        {
            return null;
        }

        return trimmed[..2] + "H" + trimmed[3];
    }
}
