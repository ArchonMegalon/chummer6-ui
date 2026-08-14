namespace Chummer.Presentation.Overview;

public enum WorkspaceConditionMonitorTrack
{
    Physical,
    Stun
}

public sealed record ConditionMonitorEditRequest(
    WorkspaceConditionMonitorTrack Track,
    int Filled);
