using System.Text.Json;
using System.Runtime.Versioning;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateHoldCommandBuilderTests
{
    private const string ExpectedKey = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";

    private static List<WindowsUpdateHoldPlanner.DesiredValue> Desired(
        string product = "Windows 11",
        string release = "24H2") => new()
    {
        new("ProductVersion", WindowsUpdatePreviousValueKind.String, null, product),
        new("TargetReleaseVersion", WindowsUpdatePreviousValueKind.Dword, 1, null),
        new("TargetReleaseVersionInfo", WindowsUpdatePreviousValueKind.String, null, release)
    };

    // ------------------------------------------------------------------ apply

    [Fact]
    public void BuildApply_WritesOnlyTargetReleaseValues_NeverNoAutoUpdate()
    {
        var command = WindowsUpdateHoldCommandBuilder.BuildApply(Desired());

        Assert.NotNull(command);
        Assert.Equal("reg.exe", command!.FileName);
        Assert.Contains("ProductVersion", command.Arguments);
        Assert.Contains("TargetReleaseVersion ", command.Arguments);
        Assert.Contains("TargetReleaseVersionInfo", command.Arguments);
        Assert.Contains("REG_DWORD", command.Arguments);
        Assert.DoesNotContain("NoAutoUpdate", command.Arguments);
        Assert.DoesNotContain("AU\\", command.Arguments);
        Assert.Contains(ExpectedKey, command.Arguments);
    }

    [Fact]
    public void BuildApply_EmptyDesired_ReturnsNull()
    {
        Assert.Null(WindowsUpdateHoldCommandBuilder.BuildApply(new List<WindowsUpdateHoldPlanner.DesiredValue>()));
    }

    [Fact]
    public void BuildApply_QuotesStringValue()
    {
        var command = WindowsUpdateHoldCommandBuilder.BuildApply(Desired(release: "22H2"))!;

        Assert.Contains("\"24H2\"", command.Arguments.Replace("22H2", "24H2"));
        Assert.Contains("/d \"22H2\"", command.Arguments);
    }

    // ---------------------------------------------------------------- restore

    [Fact]
    public void BuildRestore_PreviousAbsent_ReturnsNull_SoValueIsDeleted()
    {
        var owned = new WindowsUpdateOwnedValue
        {
            Name = "ProductVersion",
            PreviousKind = WindowsUpdatePreviousValueKind.Absent
        };

        Assert.Null(WindowsUpdateHoldCommandBuilder.BuildRestore(owned));
    }

    [Fact]
    public void BuildRestore_PreviousDword_RestoresExactTypeAndValue()
    {
        var owned = new WindowsUpdateOwnedValue
        {
            Name = "TargetReleaseVersion",
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 0
        };

        var restore = WindowsUpdateHoldCommandBuilder.BuildRestore(owned);

        Assert.NotNull(restore);
        Assert.Equal(WindowsUpdatePreviousValueKind.Dword, restore!.Value.kind);
        Assert.Contains("/v TargetReleaseVersion /t REG_DWORD /d 0 /f", restore.Value.command.Arguments);
    }

    [Fact]
    public void BuildRestore_PreviousString_RestoresExactTypeAndValue()
    {
        var owned = new WindowsUpdateOwnedValue
        {
            Name = "ProductVersion",
            PreviousKind = WindowsUpdatePreviousValueKind.String,
            PreviousString = "Windows 10"
        };

        var restore = WindowsUpdateHoldCommandBuilder.BuildRestore(owned);

        Assert.NotNull(restore);
        Assert.Equal(WindowsUpdatePreviousValueKind.String, restore!.Value.kind);
        Assert.Contains("/v ProductVersion /t REG_SZ /d \"Windows 10\" /f", restore.Value.command.Arguments);
    }

    // ---------------------------------------------------------------- cleanup

    [Fact]
    public void BuildCleanup_TrafficLensCreatedKey_RemovesOnlyWhenEmpty()
    {
        var record = new WindowsUpdateChangeRecord { PreviousKeyExisted = false };

        var command = WindowsUpdateHoldCommandBuilder.BuildCleanup(record);

        Assert.NotNull(command);
        Assert.Equal("powershell.exe", command.FileName);
        Assert.Contains("-NoProfile", command.Arguments);
        Assert.Contains("ValueCount -eq 0", command.Arguments);
        Assert.Contains("SubKeyCount -eq 0", command.Arguments);
        Assert.Contains("Remove-Item", command.Arguments);
    }

    [Fact]
    public void BuildCleanup_PreExistingKey_ReturnsNull()
    {
        Assert.Null(WindowsUpdateHoldCommandBuilder.BuildCleanup(
            new WindowsUpdateChangeRecord { PreviousKeyExisted = true }));
    }

    [Fact]
    public void BuildCleanup_UnknownKeyOwnership_ReturnsNull()
    {
        Assert.Null(WindowsUpdateHoldCommandBuilder.BuildCleanup(
            new WindowsUpdateChangeRecord { PreviousKeyExisted = null }));
    }

    // ------------------------------------------------------ record round-trips

    [Fact]
    public void ChangeRecord_Schema2_RoundTripsOwnedValues()
    {
        var record = new WindowsUpdateChangeRecord
        {
            SchemaVersion = 2,
            PreviousKeyExisted = false,
            ChangedAtUtc = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc),
            OwnedValues =
            {
                new WindowsUpdateOwnedValue
                {
                    Name = "ProductVersion",
                    PreviousKind = WindowsUpdatePreviousValueKind.String,
                    PreviousString = "Windows 10"
                }
            }
        };

        var restored = JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(
            JsonSerializer.Serialize(record));

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.SchemaVersion);
        Assert.False(restored.IsLegacy);
        Assert.False(restored.PreviousKeyExisted);
        var owned = Assert.Single(restored.OwnedValues);
        Assert.Equal("ProductVersion", owned.Name);
        Assert.Equal(WindowsUpdatePreviousValueKind.String, owned.PreviousKind);
        Assert.Equal("Windows 10", owned.PreviousString);
    }

    [Fact]
    public void ChangeRecord_LegacySchema1Json_IsDetectedAsLegacy()
    {
        // What the old (NoAutoUpdate-era) TrafficLens wrote on disk.
        var json = "{\"PreviousKind\":2,\"PreviousString\":\"old\",\"ChangedAtUtc\":\"2026-01-01T00:00:00Z\"}";

        var restored = JsonSerializer.Deserialize<WindowsUpdateChangeRecord>(json);

        Assert.NotNull(restored);
        Assert.True(restored!.IsLegacy);
        Assert.Equal(WindowsUpdatePreviousValueKind.String, restored.PreviousKind);
        Assert.Equal("old", restored.PreviousString);
    }

    [Fact]
    public void LegacyRecordCoversNoAutoUpdate_Schema1Record_IsTrue()
    {
        var legacy = new WindowsUpdateChangeRecord
        {
            SchemaVersion = 1,
            PreviousKind = WindowsUpdatePreviousValueKind.Dword,
            PreviousDword = 0
        };

        Assert.True(WindowsUpdateService.LegacyRecordCoversNoAutoUpdate(legacy));
    }

    [Fact]
    public void LegacyRecordCoversNoAutoUpdate_Schema2RecordWithoutNoAutoUpdate_IsFalse()
    {
        Assert.False(WindowsUpdateService.LegacyRecordCoversNoAutoUpdate(
            new WindowsUpdateChangeRecord { SchemaVersion = 2 }));
    }

    [Fact]
    public void LegacyRecordCoversNoAutoUpdate_NullRecord_IsFalse()
    {
        Assert.False(WindowsUpdateService.LegacyRecordCoversNoAutoUpdate(null));
    }
}
