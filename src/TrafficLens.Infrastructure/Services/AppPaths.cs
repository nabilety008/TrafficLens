namespace TrafficLens.Infrastructure.Services;

public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TrafficLens");

    public static string LogsDirectory { get; } = Path.Combine(RootDirectory, "logs");

    public static string DataDirectory { get; } = Path.Combine(RootDirectory, "data");

    public static string DatabaseFile { get; } = Path.Combine(DataDirectory, "trafficlens.db");

    public static string SettingsFile { get; } = Path.Combine(RootDirectory, "settings.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DataDirectory);
    }
}