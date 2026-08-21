using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerNuyenExpenseEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    decimal AvailableNuyen,
    IReadOnlyList<CharacterCareerNuyenExpenseEntry> Expenses);

public sealed record CareerNuyenExpenseEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    decimal ExpectedAvailableNuyen,
    CharacterCareerNuyenExpenseEntry ExpectedExpense,
    decimal Amount,
    string Reason,
    DateTime ExpenseDateLocal);

internal static class CareerNuyenExpenseEditorProjector
{
    private static readonly string[] KnownNuyenUndoTypes =
    [
        "AddCyberware", "IncreaseLifestyle", "AddArmor", "AddArmorMod", "AddWeapon", "AddWeaponMod",
        "AddWeaponAccessory", "AddGear", "AddVehicle", "AddVehicleMod", "AddVehicleGear",
        "AddVehicleWeapon", "AddVehicleWeaponMod", "AddVehicleWeaponAccessory", "AddVehicleWeaponMount",
        "ManualAdd", "ManualSubtract", "AddArmorGear", "AddVehicleModCyberware", "AddCyberwareGear",
        "AddWeaponGear", "ImproveInitiateGrade", "AddVehicleWeaponMountMod", "ModifyVehicleWeaponMount"
    ];

    public static CareerNuyenExpenseEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing Nuyen expenses.");
        }
        (decimal nuyen, IReadOnlyList<CharacterCareerNuyenExpenseEntry> expenses) = ProjectState(xml);
        if (expenses.Count == 0)
        {
            throw new InvalidOperationException("The saved career runner has no Nuyen expense to edit.");
        }
        return new CareerNuyenExpenseEditorState(workspaceId, contentRevision, nuyen, expenses);
    }

    internal static (decimal Nuyen, IReadOnlyList<CharacterCareerNuyenExpenseEntry> Expenses) ProjectState(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ReadRequiredBool(root, "created"))
        {
            throw new InvalidOperationException("Nuyen expense editing is available only for career runners.");
        }
        decimal nuyen = ReadOptionalDecimal(root, "nuyen");
        XElement[] expenseContainers = root.Elements("expenses").Take(2).ToArray();
        if (expenseContainers.Length > 1)
        {
            throw new InvalidOperationException("The saved runner has duplicate <expenses> containers.");
        }

        List<CharacterCareerNuyenExpenseEntry> entries = [];
        HashSet<Guid> identities = [];
        foreach (XElement expense in expenseContainers.SingleOrDefault()?.Elements("expense") ?? [])
        {
            if (!string.Equals(ReadOptionalText(expense, "type", "Karma"), "Nuyen", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Guid id = ReadRequiredGuid(expense, "guid");
            if (!identities.Add(id))
            {
                throw new InvalidOperationException("The saved runner has duplicate Nuyen expense GUIDs.");
            }
            DateTime date = ReadRequiredDate(expense, "date");
            decimal amount = ReadRequiredDecimal(expense, "amount");
            string reason = ReadOptionalText(expense, "reason", string.Empty);
            bool refund = ReadOptionalBool(expense, "refund");
            bool forceCareerVisible = ReadOptionalBool(expense, "forcecareervisible");
            string nuyenUndoType = ReadNuyenUndoType(expense);
            if (!CharacterCareerNuyenExpenseEditRules.TryCreateEntry(
                    id,
                    date,
                    amount,
                    reason,
                    refund,
                    forceCareerVisible,
                    nuyenUndoType,
                    out CharacterCareerNuyenExpenseEntry? entry)
                || entry is null)
            {
                throw new InvalidOperationException($"Nuyen expense {id:D} is outside Chummer5's editable bounds.");
            }
            entries.Add(entry);
        }
        return (nuyen, entries);
    }

    private static string ReadNuyenUndoType(XElement expense)
    {
        XElement[] undoNodes = expense.Elements("undo").Take(2).ToArray();
        if (undoNodes.Length == 0)
        {
            return "AddCyberware";
        }
        if (undoNodes.Length != 1)
        {
            throw new InvalidOperationException("A Nuyen expense has duplicate <undo> values.");
        }
        string raw = ReadOptionalText(undoNodes[0], "nuyentype", "AddCyberware");
        string? known = KnownNuyenUndoTypes.FirstOrDefault(value =>
            string.Equals(value, raw, StringComparison.OrdinalIgnoreCase));
        // ExpenseUndo.ConvertToNuyenExpenseType falls back to ManualAdd for unknown saved values.
        return known ?? "ManualAdd";
    }

    private static Guid ReadRequiredGuid(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !Guid.TryParse(values[0].Value.Trim(), out Guid value) || value == Guid.Empty)
        {
            throw new InvalidOperationException($"A Nuyen expense has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static DateTime ReadRequiredDate(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !DateTime.TryParse(
                values[0].Value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime value))
        {
            throw new InvalidOperationException($"A Nuyen expense has an invalid or duplicate <{name}> value.");
        }
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static decimal ReadRequiredDecimal(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !decimal.TryParse(values[0].Value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            throw new InvalidOperationException($"A Nuyen expense has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static decimal ReadOptionalDecimal(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return 0m;
        }
        if (values.Length != 1
            || !decimal.TryParse(values[0].Value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static bool ReadRequiredBool(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static bool ReadOptionalBool(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return false;
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"A Nuyen expense has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static string ReadOptionalText(XElement parent, string name, string fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return fallback;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException($"A Nuyen expense has duplicate <{name}> values.");
        }
        return values[0].Value;
    }
}
