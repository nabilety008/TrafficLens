using System.Text.Json;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

public sealed class WindowsUpdateCommandBuilderTests
{
    private const string ExpectedKey = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

    [Fact]
    public void BuildDisable_ReturnsRegAddDwordOne()
    {
        var command = WindowsUpdateCommandBuilder.BuildDisable();

        Assert.Equal("reg.exe", command.FileName);
        Assert.Equal(
            $@"add ""{ExpectedKey}"" /v NoAutoUpdate /t REG_DWORD /d 1 /f",
            command.Arguments);
    }

    [Fact]
    public void BuildEnable_PreviousAbsent_DeletesValue()
    {
        var command = WindowsUpdateCommandBuilder.BuildEnable(new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent
        });

        Assert.Equal("reg.exe", command.FileName);
        Assert.Equal(
            $@"delete ""{ExpectedKey}"" /v NoAutoUpdate /f",
            command.Arguments);
    }

    [Fact]
    public void BuildEnable_PreviousDword_RestoresDwordValue()
    {
        var command = WindowsUpdateCommandBuilder.BuildEnable(new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 2
        });

        Assert.Equal(
            $@"add ""{ExpectedKey}"" /v NoAutoUpdate /t REG_DWORD /d 2 /f",
            command.Arguments);
    }

    [Fact]
    public void BuildEnable_PreviousString_RestoresStringValue()
    {
        var command = WindowsUpdateCommandBuilder.BuildEnable(new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.String,
            PreviousString = "custom"
        });

        Assert.Equal(
            $@"add ""{ExpectedKey}"" /v NoAutoUpdate /t REG_SZ /d ""custom"" /f",
            command.Arguments);
    }

    [Fact]
    public void ChangeRecord_SerializesAndRoundTrips()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 1,
            ChangedAtUtc = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(record);
        var restored = JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(json);

        Assert.NotNull(restored);
        Assert.Equal(record.PreviousKind, restored!.PreviousKind);
        Assert.Equal(record.PreviousDword, restored.PreviousDword);
        Assert.Equal(record.ChangedAtUtc, restored.ChangedAtUtc);
    }
}
