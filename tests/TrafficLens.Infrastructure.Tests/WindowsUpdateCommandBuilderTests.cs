using System.Text.Json;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

public sealed class WindowsUpdateCommandBuilderTests
{
    private const string ExpectedKey = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

    private static WindowsUpdateSnapshot Snapshot(
        bool keyExists,
        int otherValues = 0,
        int subKeys = 0) =>
        new()
        {
            AuKeyExists = keyExists,
            AuOtherValues = otherValues,
            AuSubKeys = subKeys
        };

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
    public void BuildEnable_PreviousAbsentWithPreExistingKey_DeletesValueAndKeepsKey()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = true
        };
        var command = WindowsUpdateCommandBuilder.BuildEnable(record, Snapshot(keyExists: true));

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, Snapshot(keyExists: true)));
        Assert.Equal("reg.exe", command.FileName);
        Assert.Equal(
            $@"delete ""{ExpectedKey}"" /v NoAutoUpdate /f",
            command.Arguments);
    }

    [Fact]
    public void BuildEnable_TrafficLensCreatedKey_EmptyKey_RemovesValueAndEmptyKey()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = false
        };
        var snapshot = Snapshot(keyExists: true);

        Assert.True(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, snapshot));

        var command = WindowsUpdateCommandBuilder.BuildEnable(record, snapshot);

        Assert.Equal("powershell.exe", command.FileName);
        Assert.Contains("-NoProfile", command.Arguments);
        Assert.Contains("Remove-ItemProperty", command.Arguments);
        Assert.Contains("NoAutoUpdate", command.Arguments);
        Assert.Contains("ValueCount -eq 0", command.Arguments);
        Assert.Contains("SubKeyCount -eq 0", command.Arguments);
        Assert.Contains("Remove-Item", command.Arguments);
    }

    [Fact]
    public void BuildEnable_TrafficLensCreatedKey_UnrelatedValuePresent_KeepsKey()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = false
        };
        var snapshot = Snapshot(keyExists: true, otherValues: 1);

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, snapshot));

        var command = WindowsUpdateCommandBuilder.BuildEnable(record, snapshot);

        Assert.Equal("reg.exe", command.FileName);
        Assert.Equal(
            $@"delete ""{ExpectedKey}"" /v NoAutoUpdate /f",
            command.Arguments);
    }

    [Fact]
    public void BuildEnable_TrafficLensCreatedKey_SubKeyPresent_KeepsKey()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = false
        };
        var snapshot = Snapshot(keyExists: true, subKeys: 1);

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, snapshot));

        var command = WindowsUpdateCommandBuilder.BuildEnable(record, snapshot);

        Assert.Equal("reg.exe", command.FileName);
        Assert.DoesNotContain("Remove-Item", command.Arguments);
    }

    [Fact]
    public void BuildEnable_LegacyRecordWithoutKeyOwnership_KeepsKey()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = null
        };
        var snapshot = Snapshot(keyExists: true);

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, snapshot));

        var command = WindowsUpdateCommandBuilder.BuildEnable(record, snapshot);

        Assert.Equal("reg.exe", command.FileName);
        Assert.DoesNotContain("Remove-Item", command.Arguments);
    }

    [Fact]
    public void BuildEnable_PreviousDword_RestoresDwordValueWithoutKeyRemoval()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 2,
            PreviousKeyExisted = true
        };
        var command = WindowsUpdateCommandBuilder.BuildEnable(record, Snapshot(keyExists: true));

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, Snapshot(keyExists: true)));
        Assert.Equal("reg.exe", command.FileName);
        Assert.Equal(
            $@"add ""{ExpectedKey}"" /v NoAutoUpdate /t REG_DWORD /d 2 /f",
            command.Arguments);
    }

    [Fact]
    public void BuildEnable_PreviousString_RestoresStringValueWithoutKeyRemoval()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.String,
            PreviousString = "custom",
            PreviousKeyExisted = true
        };
        var command = WindowsUpdateCommandBuilder.BuildEnable(record, Snapshot(keyExists: true));

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, Snapshot(keyExists: true)));
        Assert.Equal("reg.exe", command.FileName);
        Assert.Equal(
            $@"add ""{ExpectedKey}"" /v NoAutoUpdate /t REG_SZ /d ""custom"" /f",
            command.Arguments);
    }

    [Fact]
    public void ShouldRemoveCreatedEmptyKey_ReadError_IsFalse()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = false
        };
        var snapshot = new WindowsUpdateSnapshot { ReadError = true, AuKeyExists = true };

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, snapshot));
    }

    [Fact]
    public void ShouldRemoveCreatedEmptyKey_KeyAbsent_IsFalse()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Absent,
            PreviousKeyExisted = false
        };

        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(record, Snapshot(keyExists: false)));
    }

    [Fact]
    public void ChangeRecord_SerializesAndRoundTrips()
    {
        var record = new WindowsUpdateChangeRecord
        {
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 1,
            PreviousKeyExisted = false,
            ChangedAtUtc = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(record);
        var restored = JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(json);

        Assert.NotNull(restored);
        Assert.Equal(record.PreviousKind, restored!.PreviousKind);
        Assert.Equal(record.PreviousDword, restored.PreviousDword);
        Assert.Equal(record.PreviousKeyExisted, restored.PreviousKeyExisted);
        Assert.Equal(record.ChangedAtUtc, restored.ChangedAtUtc);
    }

    [Fact]
    public void ChangeRecord_LegacyJsonWithoutKeyExisted_DeserializesAsNull()
    {
        var json = "{\"PreviousKind\":0,\"ChangedAtUtc\":\"2026-09-25T12:00:00Z\"}";

        var restored = JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(json);

        Assert.NotNull(restored);
        Assert.Null(restored!.PreviousKeyExisted);
        Assert.False(WindowsUpdateCommandBuilder.ShouldRemoveCreatedEmptyKey(
            restored,
            Snapshot(keyExists: true)));
    }
}
