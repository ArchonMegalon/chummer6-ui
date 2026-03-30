using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WorkspacePortabilityActivity(
    string Title,
    WorkspacePortabilityReceipt Receipt);
