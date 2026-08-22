using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerCreateExpenseEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterCareerCreateExpenseOperation Operation,
    CharacterCareerCreateExpenseState Expense);

public sealed record CareerCreateExpenseEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerCreateExpenseState ExpectedState,
    CharacterCareerCreateExpenseOperation Operation,
    int Amount,
    decimal Percent,
    string Reason,
    DateTime ExpenseDateLocal,
    bool Refund,
    bool KarmaNuyenExchange,
    bool ForceCareerVisible);

internal static class CareerCreateExpenseEditorProjector
{
    public static CareerCreateExpenseEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        CharacterCareerCreateExpenseOperation operation,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before creating an expense.");
        }
        return new CareerCreateExpenseEditorState(
            workspaceId,
            contentRevision,
            operation,
            ProjectState(xml, sourceDataResolver));
    }

    internal static CharacterCareerCreateExpenseState ProjectState(
        string xml,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        CharacterCareerManualKarmaState current = CareerManualKarmaEditorProjector.ProjectState(
            xml,
            sourceDataResolver);
        if (!CharacterCareerCreateExpenseRules.TryProject(
                true,
                current.AvailableKarma,
                current.AvailableNuyen,
                current.NuyenPerKarmaWorkingForPeople,
                current.NuyenPerKarmaWorkingForMan,
                out CharacterCareerCreateExpenseState? state)
            || state is null)
        {
            throw new InvalidOperationException(
                "The saved runner and settings profile do not prove the exact Chummer5 CreateExpense rules.");
        }
        return state;
    }
}
