using TrafficLens.Network.Process;

namespace TrafficLens.Network.Tests;

public sealed class WindowsProcessMetadataProviderTests
{
    private readonly WindowsProcessMetadataProvider _provider = new();

    [Fact]
    public void Resolve_CurrentProcess_ReturnsMetadata()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        var identity = new ProcessInstanceId(current.Id, 0);
        var result = _provider.Resolve(identity);

        Assert.True(result.ProcessExists);
        Assert.NotNull(result.Metadata);
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata.ProcessName));
    }

    [Fact]
    public void Resolve_NonexistentPid_ReturnsNotExists()
    {
        var identity = new ProcessInstanceId(int.MaxValue, 0);
        var result = _provider.Resolve(identity);

        Assert.False(result.ProcessExists);
        Assert.Null(result.Metadata);
    }

    [Fact]
    public void Resolve_WrongStartTime_ReportsDifferentInstance()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        var startTicks = current.StartTime.ToUniversalTime().Ticks;
        var identity = new ProcessInstanceId(current.Id, startTicks + 10_000 * TimeSpan.TicksPerSecond);
        var result = _provider.Resolve(identity);

        Assert.True(result.ProcessExists);
        Assert.Null(result.Metadata);
    }

    [Fact]
    public void Resolve_MatchingStartTime_ReturnsMetadataForInstance()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        var startTicks = current.StartTime.ToUniversalTime().Ticks;
        var identity = new ProcessInstanceId(current.Id, startTicks);
        var result = _provider.Resolve(identity);

        Assert.True(result.ProcessExists);
        Assert.NotNull(result.Metadata);
    }
}