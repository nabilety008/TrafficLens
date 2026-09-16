using TrafficLens.Infrastructure.Logging;

namespace TrafficLens.Infrastructure.Tests;

public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly string _dir;

    public FileLoggerProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"tl_logtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Constructor_PrunesExpiredLogFiles()
    {
        var expiredPath = Path.Combine(_dir, "trafficlens-2019-06-15.log");
        var recentPath = Path.Combine(_dir, $"trafficlens-{DateTime.Now:yyyy-MM-dd}.log");
        var unrelatedPath = Path.Combine(_dir, "other.txt");

        File.WriteAllText(expiredPath, "expired");
        File.WriteAllText(recentPath, "recent");
        File.WriteAllText(unrelatedPath, "keep");

        new FileLoggerProvider(_dir).Dispose();

        Assert.False(File.Exists(expiredPath), "expired log should have been deleted");
        Assert.True(File.Exists(recentPath), "recent log should be kept");
        Assert.True(File.Exists(unrelatedPath), "unrelated file should be left alone");
    }

    [Fact]
    public void Constructor_DoesNotThrow_OnEmptyDirectory()
    {
        new FileLoggerProvider(_dir).Dispose();
    }
}