using TrafficLens.Core.Abstractions;
using TrafficLens.Infrastructure.Services;

namespace TrafficLens.Infrastructure.Tests;

public sealed class WindowsUpdateStateResolverTests
{
    [Fact]
    public void Resolve_ReadError_ReturnsUnknownWithError()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            ReadError = true,
            ErrorMessage = "access denied"
        });

        Assert.Equal(WindowsUpdateStatus.Unknown, state.Status);
        Assert.Equal("access denied", state.Error);
        Assert.False(state.CanDisable);
        Assert.False(state.CanEnable);
    }

    [Fact]
    public void Resolve_NoAutoUpdateOneWithRecord_ReturnsDisabledByTrafficLensAndCanEnable()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            NoAutoUpdatePresent = true,
            NoAutoUpdateValue = 1,
            Record = new WindowsUpdateChangeRecord()
        });

        Assert.Equal(WindowsUpdateStatus.Disabled, state.Status);
        Assert.Equal(WindowsUpdateDisableReason.TrafficLens, state.Reason);
        Assert.True(state.CanEnable);
        Assert.False(state.CanDisable);
        Assert.False(state.OrganizationPolicyPresent);
    }

    [Fact]
    public void Resolve_NoAutoUpdateOneWithoutRecord_ReturnsDisabledByPolicyAndBlocksEnable()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            NoAutoUpdatePresent = true,
            NoAutoUpdateValue = 1
        });

        Assert.Equal(WindowsUpdateStatus.Disabled, state.Status);
        Assert.Equal(WindowsUpdateDisableReason.Policy, state.Reason);
        Assert.False(state.CanEnable);
        Assert.False(state.CanDisable);
    }

    [Fact]
    public void Resolve_NoAutoUpdateZero_ReturnsManagedByPolicyWithOrganizationFlag()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            NoAutoUpdatePresent = true,
            NoAutoUpdateValue = 0
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, state.Status);
        Assert.True(state.OrganizationPolicyPresent);
        Assert.True(state.CanDisable);
        Assert.False(state.CanEnable);
    }

    [Fact]
    public void Resolve_NoAutoUpdateNonInteger_ReturnsManagedByPolicy()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            NoAutoUpdatePresent = true,
            NoAutoUpdateInvalid = true,
            NoAutoUpdateRawString = "not-a-number"
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, state.Status);
        Assert.True(state.OrganizationPolicyPresent);
    }

    [Fact]
    public void Resolve_OtherPolicyValueWithoutNoAutoUpdate_ReturnsManagedByPolicy()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            OtherPolicyPresent = true
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, state.Status);
        Assert.True(state.OrganizationPolicyPresent);
        Assert.True(state.CanDisable);
        Assert.False(state.CanEnable);
    }

    [Fact]
    public void Resolve_ServiceDisabledWithoutPolicy_ReturnsDisabledByServiceAndBlocksBothActions()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            ServiceStart = 4
        });

        Assert.Equal(WindowsUpdateStatus.Disabled, state.Status);
        Assert.Equal(WindowsUpdateDisableReason.Service, state.Reason);
        Assert.False(state.CanDisable);
        Assert.False(state.CanEnable);
    }

    [Fact]
    public void Resolve_NoPolicyAndServiceRunning_ReturnsEnabledAndCanDisable()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            ServiceStart = 2
        });

        Assert.Equal(WindowsUpdateStatus.Enabled, state.Status);
        Assert.Equal(WindowsUpdateDisableReason.None, state.Reason);
        Assert.True(state.CanDisable);
        Assert.False(state.CanEnable);
    }

    [Fact]
    public void Resolve_RecordWithoutMatchingPolicy_IsIgnoredAndReturnsEnabled()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            NoAutoUpdatePresent = false,
            ServiceStart = 3,
            Record = new WindowsUpdateChangeRecord()
        });

        Assert.Equal(WindowsUpdateStatus.Enabled, state.Status);
        Assert.False(state.CanEnable);
        Assert.True(state.CanDisable);
    }

    [Fact]
    public void Resolve_OtherPolicyPresentWithStaleRecord_StaysManagedAndBlocksEnable()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot
        {
            OtherPolicyPresent = true,
            Record = new WindowsUpdateChangeRecord
            {
                PreviousKind = WindowsUpdatePreviousValueKind.Absent,
                PreviousKeyExisted = false
            }
        });

        Assert.Equal(WindowsUpdateStatus.ManagedByPolicy, state.Status);
        Assert.True(state.OrganizationPolicyPresent);
        Assert.False(state.CanEnable);
    }

    [Fact]
    public void Resolve_EmptySnapshot_ReturnsEnabled()
    {
        var state = WindowsUpdateStateResolver.Resolve(new WindowsUpdateSnapshot());

        Assert.Equal(WindowsUpdateStatus.Enabled, state.Status);
        Assert.True(state.CanDisable);
        Assert.False(state.OrganizationPolicyPresent);
    }
}
