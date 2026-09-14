namespace TrafficLens.Network.Process;

/// <summary>Resolved, cached metadata for one process instance.</summary>
public sealed record ProcessMetadata(
    string ProcessName,
    string? ExecutablePath,
    bool IconAvailable,
    long ActualStartTimeUtcTicks);

/// <summary>
/// Outcome of a metadata lookup. <see cref="ProcessExists"/> distinguishes "the
/// process is gone" from "the process exists but the requested identity is a
/// different instance (PID reuse)". When true and <see cref="Metadata"/> is
/// null the existing process's start time does not match the requested
/// instance identity.
/// </summary>
public readonly record struct ProcessMetadataResult(
    bool ProcessExists,
    ProcessMetadata? Metadata);

public interface IProcessMetadataProvider
{
    /// <summary>
    /// Never throws. Returns <see cref="ProcessMetadataResult"/> with
    /// ProcessExists=false when the PID is not present; with Metadata=null when
    /// the process exists but is a different instance than the requested
    /// identity (PID reuse); otherwise with Metadata populated (which may have
    /// a null executable path for protected processes).
    /// </summary>
    ProcessMetadataResult Resolve(ProcessInstanceId identity);
}