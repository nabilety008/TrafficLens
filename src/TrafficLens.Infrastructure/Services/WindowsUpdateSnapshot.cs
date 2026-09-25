namespace TrafficLens.Infrastructure.Services;

public sealed class WindowsUpdateSnapshot
{
    public bool ReadError { get; init; }

    public string? ErrorMessage { get; init; }

    public bool NoAutoUpdatePresent { get; init; }

    public int? NoAutoUpdateValue { get; init; }

    public bool NoAutoUpdateInvalid { get; init; }

    public string? NoAutoUpdateRawString { get; init; }

    public bool OtherPolicyPresent { get; init; }

    public int? ServiceStart { get; init; }

    public WindowsUpdateChangeRecord? Record { get; init; }
}
