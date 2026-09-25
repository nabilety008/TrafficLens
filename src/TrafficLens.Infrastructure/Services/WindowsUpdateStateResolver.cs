using TrafficLens.Core.Abstractions;

namespace TrafficLens.Infrastructure.Services;

public static class WindowsUpdateStateResolver
{
    public static WindowsUpdateState Resolve(WindowsUpdateSnapshot snapshot)
    {
        if (snapshot.ReadError)
        {
            return new WindowsUpdateState
            {
                Status = WindowsUpdateStatus.Unknown,
                Error = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
                    ? "Windows Update state could not be read."
                    : snapshot.ErrorMessage
            };
        }

        if (snapshot.NoAutoUpdatePresent && snapshot.NoAutoUpdateValue == 1)
        {
            return snapshot.Record is not null
                ? new WindowsUpdateState
                {
                    Status = WindowsUpdateStatus.Disabled,
                    Reason = WindowsUpdateDisableReason.TrafficLens,
                    CanEnable = true
                }
                : new WindowsUpdateState
                {
                    Status = WindowsUpdateStatus.Disabled,
                    Reason = WindowsUpdateDisableReason.Policy
                };
        }

        if (snapshot.NoAutoUpdatePresent || snapshot.OtherPolicyPresent)
        {
            return new WindowsUpdateState
            {
                Status = WindowsUpdateStatus.ManagedByPolicy,
                OrganizationPolicyPresent = true,
                CanDisable = true
            };
        }

        if (snapshot.ServiceStart == 4)
        {
            return new WindowsUpdateState
            {
                Status = WindowsUpdateStatus.Disabled,
                Reason = WindowsUpdateDisableReason.Service
            };
        }

        return new WindowsUpdateState
        {
            Status = WindowsUpdateStatus.Enabled,
            CanDisable = true
        };
    }
}
