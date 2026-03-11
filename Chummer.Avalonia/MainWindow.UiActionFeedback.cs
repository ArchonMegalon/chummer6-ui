using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private void ApplyUiActionFailure(string operationName, Exception ex)
    {
        CharacterOverviewState state = _adapter.State;
        MainWindowShellFrame shellFrame = MainWindowShellFrameProjector.Project(
            state,
            _shellSurfaceResolver.Resolve(state, _shellPresenter.State),
            _commandAvailabilityEvaluator);

        MainWindowFeedbackCoordinator.ApplyUiActionFailure(
            _controls.ToolStrip,
            _controls.SectionHost,
            _controls.StatusStrip,
            shellFrame,
            operationName,
            ex);
    }
}
