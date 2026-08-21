using System.Globalization;
using System.Text.Json.Nodes;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public static class WorkspaceCollectionEditorProjector
{
    private const int MaximumNameLength = 512;
    private const int MaximumSelectTextLength = 32_767;
    private const int MaximumTextLength = 65_536;
    private const int MaximumRichTextLength = int.MaxValue;
    private const int MaximumNotesColorLength = 32;
    private const int MaximumVehicleLocationCount = 4_096;
    private const int MaximumLocationNameLength = 32_767;

    public static WorkspaceCollectionEditorState? TryProject(string? sectionId, JsonNode? section)
    {
        if (section is not JsonObject root
            || !TryResolveSchema(sectionId, out SectionSchema schema)
            || !TryGetPropertyValueIgnoreCase(root, schema.CollectionProperty, out JsonNode? collectionNode)
            || collectionNode is not JsonArray collection)
        {
            return null;
        }

        List<WorkspaceCollectionItemEditorState> items = [];
        HashSet<string> identities = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < collection.Count; index++)
        {
            if (collection[index] is not JsonObject item
                || !TryCreateTarget(schema, item, out WorkspaceCollectionItemTarget? target))
            {
                return null;
            }

            string identity = BuildIdentityKey(target!);
            if (!identities.Add(identity))
            {
                return null;
            }

            SectionSchema itemSchema = target!.NestedKind is { } nestedKind
                ? schema with { NestedKind = nestedKind }
                : schema;
            items.Add(ProjectItem(itemSchema, root, item, target, index));
        }

        return new WorkspaceCollectionEditorState(
            SectionId: schema.SectionId,
            Kind: schema.Kind,
            NestedKind: schema.NestedKind,
            Items: items);
    }

    private static WorkspaceCollectionItemEditorState ProjectItem(
        SectionSchema schema,
        JsonObject section,
        JsonObject item,
        WorkspaceCollectionItemTarget target,
        int index)
    {
        IReadOnlyList<WorkspaceCollectionTextField> textFields = ResolveTextFields(schema, item);
        WorkspaceCollectionTextValueState[] textValues = textFields
            .Select(field => new WorkspaceCollectionTextValueState(
                Field: field,
                Value: ReadText(item, ResolveJsonProperty(field)),
                IsRequired: field == WorkspaceCollectionTextField.Name,
                MaximumLength: field switch
                {
                    WorkspaceCollectionTextField.Name => MaximumNameLength,
                    WorkspaceCollectionTextField.GearName => MaximumSelectTextLength,
                    WorkspaceCollectionTextField.CustomName
                        when schema.Kind == WorkspaceCollectionKind.Lifestyle => MaximumSelectTextLength,
                    WorkspaceCollectionTextField.NotesColor => MaximumNotesColorLength,
                    WorkspaceCollectionTextField.Notes
                        when schema.Kind == WorkspaceCollectionKind.Lifestyle => MaximumRichTextLength,
                    _ => MaximumTextLength
                },
                IsEnabled: IsTextFieldEnabled(schema, item, field)))
            .ToArray();

        WorkspaceCollectionRatingState? rating = SupportsRating(schema)
            ? new WorkspaceCollectionRatingState(ReadInt(item, "rating"))
            : null;
        WorkspaceCollectionQuantityState? quantity = SupportsQuantity(schema)
            ? new WorkspaceCollectionQuantityState(ReadDecimal(item, "quantity", fallback: 1m))
            : null;
        WorkspaceCollectionIntegerValueState[] integerValues = ResolveIntegerFields(schema, item)
            .Select(field => new WorkspaceCollectionIntegerValueState(
                Field: field,
                Value: ReadInt(item, ResolveJsonProperty(field)),
                Maximum: ResolveIntegerMaximum(item, field),
                IsEnabled: IsIntegerFieldEnabled(schema, item, field))
            {
                Label = ResolveIntegerFieldLabel(schema, item, field)
            })
            .ToArray();
        WorkspaceCollectionToggleValueState[] toggles = ResolveToggleFields(schema)
            .Select(field => new WorkspaceCollectionToggleValueState(
                Field: field,
                Value: ReadBool(item, ResolveJsonProperty(schema, field)),
                IsEnabled: IsToggleEnabled(schema, section, item, field))
            {
                Label = ResolveToggleFieldLabel(schema, item, field)
            })
            .ToArray();
        WorkspaceItemConditionMonitorState? physicalConditionMonitor = schema.Kind == WorkspaceCollectionKind.Vehicle
            && schema.NestedKind is null
                ? ProjectVehiclePhysicalConditionMonitor(item)
                : null;
        WorkspaceItemConditionMonitorState? matrixConditionMonitor = schema.Kind is WorkspaceCollectionKind.Vehicle
                or WorkspaceCollectionKind.Gear
            || schema.Kind == WorkspaceCollectionKind.Armor && schema.NestedKind is null
            || schema.Kind == WorkspaceCollectionKind.Weapon && schema.NestedKind is null
            || schema.Kind == WorkspaceCollectionKind.Cyberware
                ? ProjectMatrixConditionMonitor(item)
                : null;
        WorkspaceContactEditorState? contact = schema.Kind == WorkspaceCollectionKind.Contact
            && schema.NestedKind is null
                ? ProjectContact(item)
                : null;
        WorkspaceLinkedCharacterState? linkedCharacter = schema.Kind is WorkspaceCollectionKind.Contact or WorkspaceCollectionKind.Pet
            && schema.NestedKind is null
                ? ProjectLinkedCharacter(item)
                : null;
        IReadOnlyList<WorkspaceLocationItemState>? vehicleLocations = schema.Kind == WorkspaceCollectionKind.Vehicle
            && schema.NestedKind is null
            && Guid.TryParseExact(target.ItemId, "D", out Guid vehicleId)
            && vehicleId != Guid.Empty
                ? TryProjectVehicleLocations(item)
                : null;
        bool? vehicleHomeNode = schema.Kind == WorkspaceCollectionKind.Vehicle
            && schema.NestedKind is null
            && Guid.TryParseExact(target.ItemId, "D", out Guid homeNodeVehicleId)
            && homeNodeVehicleId != Guid.Empty
            && TryReadStrictBool(item, "homeNode", out bool homeNode)
                ? homeNode
                : null;
        bool? armorHomeNode = schema.Kind == WorkspaceCollectionKind.Armor
            && schema.NestedKind is null
            && Guid.TryParseExact(target.ItemId, "D", out Guid homeNodeArmorId)
            && homeNodeArmorId != Guid.Empty
            && TryReadStrictBool(item, "homeNode", out bool armorHomeNodeValue)
                ? armorHomeNodeValue
                : null;
        CharacterWeaponHomeNodeSemantics? weaponHomeNode = ProjectWeaponHomeNode(
            schema,
            item,
            target);
        CharacterWeaponActiveCommlinkSemantics? weaponActiveCommlink =
            ProjectWeaponActiveCommlink(schema, item, target);
        bool? armorActiveCommlink = schema.Kind == WorkspaceCollectionKind.Armor
            && schema.NestedKind is null
            && Guid.TryParseExact(target.ItemId, "D", out Guid activeCommlinkArmorId)
            && activeCommlinkArmorId != Guid.Empty
            && TryReadStrictBool(item, "isCommlink", out bool armorIsCommlink)
            && armorIsCommlink
            && TryReadStrictBool(item, "activeCommlink", out bool armorActiveCommlinkValue)
                ? armorActiveCommlinkValue
                : null;
        CharacterGearActiveCommlinkSemantics? gearActiveCommlink =
            ProjectGearActiveCommlink(schema, item, target);
        CharacterPrototypeTranshumanSemantics? prototypeTranshuman =
            ProjectPrototypeTranshuman(schema, item, target);
        WorkspaceArmorDamageAdjustmentState? armorDamageAdjustment =
            ProjectArmorDamageAdjustment(schema, item, target);
        CharacterArmorEquipmentState? armorEquipment =
            ProjectArmorEquipment(schema, section, target);
        CharacterLifestyleIncrementState? lifestyleIncrement =
            ProjectLifestyleIncrement(schema, item, target);
        bool? weaponAccessoryIncludedInWeapon = schema.Kind == WorkspaceCollectionKind.Weapon
            && schema.NestedKind == WorkspaceNestedCollectionKind.WeaponAccessory
            && Guid.TryParseExact(target.ItemId, "D", out Guid accessoryParentWeaponId)
            && accessoryParentWeaponId != Guid.Empty
            && Guid.TryParseExact(target.NestedItemId, "D", out Guid accessoryId)
            && accessoryId != Guid.Empty
            && TryReadStrictBool(item, "includedInWeapon", out bool includedInWeapon)
                ? includedInWeapon
                : null;
        CharacterCritterPowerCountState? critterPowerCount = ProjectCritterPowerCount(
            schema,
            item,
            target);
        CharacterSpiritFetteringState? spiritFettering = ProjectSpiritFettering(
            schema,
            item,
            target);
        CharacterSpiritNameChoiceState? spiritNameChoice = ProjectSpiritNameChoice(
            schema,
            item,
            target);
        WorkspaceGearQuantityLifecycleState? gearQuantityLifecycle = ProjectGearQuantityLifecycle(
            schema,
            section,
            item,
            target);
        WorkspaceQualityLevelState? qualityLevel = ProjectQualityLevel(schema, item, target);

        string label = FirstNonBlank(
            schema.Kind == WorkspaceCollectionKind.Lifestyle
                ? ReadText(item, "customName")
                : null,
            ReadText(item, "name"),
            ReadText(item, "reward"),
            schema.Kind == WorkspaceCollectionKind.InitiationGrade
                ? $"Grade {ReadInt(item, "grade").ToString(CultureInfo.InvariantCulture)}"
                : null,
            ReadText(item, "suid"),
            target.NestedItemId,
            target.ItemId);

        return new WorkspaceCollectionItemEditorState(
            Target: target,
            Index: index,
            Label: label,
            TextValues: textValues,
            Rating: rating,
            Quantity: quantity,
            ToggleValues: toggles,
            AddableNestedKinds: ResolveAddableNestedKinds(schema),
            CanDelete: schema.Kind != WorkspaceCollectionKind.Lifestyle
                && (schema.Kind is not (WorkspaceCollectionKind.Contact or WorkspaceCollectionKind.Pet)
                    || ReadBool(item, "canDelete")),
            CanMove: schema.Kind != WorkspaceCollectionKind.Lifestyle,
            PhysicalConditionMonitor: physicalConditionMonitor,
            MatrixConditionMonitor: matrixConditionMonitor,
            Contact: contact,
            LinkedCharacter: linkedCharacter)
        {
            IntegerValues = integerValues,
            VehicleLocations = vehicleLocations,
            VehicleHomeNode = vehicleHomeNode,
            ArmorHomeNode = armorHomeNode,
            WeaponHomeNode = weaponHomeNode,
            WeaponActiveCommlink = weaponActiveCommlink,
            ArmorActiveCommlink = armorActiveCommlink,
            GearActiveCommlink = gearActiveCommlink,
            PrototypeTranshuman = prototypeTranshuman,
            ArmorDamageAdjustment = armorDamageAdjustment,
            ArmorEquipment = armorEquipment,
            LifestyleIncrement = lifestyleIncrement,
            WeaponAccessoryIncludedInWeapon = weaponAccessoryIncludedInWeapon,
            CritterPowerCount = critterPowerCount,
            SpiritFettering = spiritFettering,
            SpiritNameChoice = spiritNameChoice,
            GearQuantityLifecycle = gearQuantityLifecycle,
            GearQuantityLifecycleRequired = schema.Kind == WorkspaceCollectionKind.Gear
                && schema.NestedKind is null
                && ReadBool(item, "careerEditable"),
            QualityLevel = qualityLevel,
            CyberwareCommerceRequired = schema.Kind == WorkspaceCollectionKind.Cyberware
                && ReadBool(item, "careerEditable")
        };
    }

    private static CharacterLifestyleIncrementState? ProjectLifestyleIncrement(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Lifestyle
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid lifestyleId)
            || lifestyleId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "incrementState", out JsonNode? stateNode)
            || stateNode is not JsonObject state
            || !TryReadStrictString(state, "lifestyleId", out string projectedIdText, 36)
            || !Guid.TryParseExact(projectedIdText, "D", out Guid projectedId)
            || projectedId != lifestyleId
            || !TryReadStrictInt(state, "increments", out int increments)
            || !TryReadLifestyleIncrementUnit(state, out CharacterLifestyleIncrementUnit unit)
            || !TryReadStrictBool(state, "careerMode", out bool careerMode)
            || !TryReadStrictDecimal(state, "nuyen", out decimal nuyen)
            || !TryReadStrictBool(state, "nuyenExact", out bool nuyenExact)
            || !TryReadStrictDecimal(state, "totalIncrementCost", out decimal totalIncrementCost)
            || !TryReadStrictBool(state, "totalIncrementCostExact", out bool totalIncrementCostExact)
            || !TryReadStrictString(state, "displayName", out string displayName, MaximumNameLength)
            || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var projected = new CharacterLifestyleIncrementState(
            lifestyleId,
            increments,
            unit,
            careerMode,
            nuyen,
            nuyenExact,
            totalIncrementCost,
            totalIncrementCostExact,
            displayName);
        CharacterLifestyleIncrementAction probeAction = careerMode
            ? CharacterLifestyleIncrementAction.DecreaseCareer
            : CharacterLifestyleIncrementAction.SetCreation;
        int? requested = careerMode ? null : Math.Clamp(
            increments,
            CharacterLifestyleIncrementRules.CreationMinimum,
            CharacterLifestyleIncrementRules.CreationMaximum);
        return CharacterLifestyleIncrementRules.Quote(projected, probeAction, requested).Exact
            ? projected
            : null;
    }

    private static bool TryReadLifestyleIncrementUnit(
        JsonObject state,
        out CharacterLifestyleIncrementUnit unit)
    {
        unit = CharacterLifestyleIncrementUnit.Month;
        if (!TryGetPropertyValueIgnoreCase(state, "unit", out JsonNode? node)
            || node is not JsonValue value)
        {
            return false;
        }
        if (value.TryGetValue(out int numeric)
            && Enum.IsDefined(typeof(CharacterLifestyleIncrementUnit), numeric))
        {
            unit = (CharacterLifestyleIncrementUnit)numeric;
            return true;
        }
        return value.TryGetValue(out string? text)
            && Enum.TryParse(text, ignoreCase: false, out unit)
            && Enum.IsDefined(unit);
    }

    private static WorkspaceQualityLevelState? ProjectQualityLevel(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Quality
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid qualityId)
            || qualityId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "levelSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "anchorQualityId", out string projectedIdText, 36)
            || !Guid.TryParseExact(projectedIdText, "D", out Guid projectedId)
            || projectedId != qualityId
            || !TryReadStrictInt(semantics, "level", out int level)
            || !TryReadStrictInt(semantics, "maximumLevel", out int maximumLevel)
            || !TryReadStrictBool(semantics, "careerMode", out bool careerMode)
            || !TryReadStrictString(semantics, "qualityType", out string qualityType, 32)
            || qualityType is not ("Positive" or "Negative")
            || level < 1
            || maximumLevel < level)
        {
            return null;
        }

        return new WorkspaceQualityLevelState(
            qualityId,
            level,
            maximumLevel,
            careerMode,
            qualityType);
    }

    private static CharacterCritterPowerCountState? ProjectCritterPowerCount(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.CritterPower
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid critterPowerId)
            || critterPowerId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "countTowardsLimitSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "critterPowerId", out string projectedIdText, 36)
            || !Guid.TryParseExact(projectedIdText, "D", out Guid projectedId)
            || projectedId != critterPowerId
            || !TryReadStrictBool(semantics, "countsTowardsLimit", out bool countsTowardsLimit))
        {
            return null;
        }

        return new CharacterCritterPowerCountState(critterPowerId, countsTowardsLimit);
    }

    private static CharacterSpiritFetteringState? ProjectSpiritFettering(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Spirit
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid spiritId)
            || spiritId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "fetteringSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "spiritId", out string projectedIdText, 36)
            || !Guid.TryParseExact(projectedIdText, "D", out Guid projectedId)
            || projectedId != spiritId
            || !TryReadStrictString(semantics, "entityType", out string entityType, 16)
            || entityType is not ("Spirit" or "Sprite")
            || !TryReadStrictBool(semantics, "created", out bool created)
            || !TryReadStrictBool(semantics, "fettered", out bool fettered)
            || !TryReadStrictInt(semantics, "force", out int force)
            || !TryReadStrictInt(semantics, "services", out int services)
            || !TryReadStrictBool(semantics, "bound", out bool bound)
            || !TryReadStrictBool(semantics, "spriteFetteringAllowed", out bool spriteAllowed)
            || !TryReadStrictBool(semantics, "activationCostExact", out bool costExact)
            || !TryReadStrictInt(semantics, "activationKarmaCost", out int karmaCost)
            || !TryReadStrictInt(semantics, "availableKarma", out int availableKarma)
            || !TryReadStrictBool(semantics, "canFetter", out bool canFetter)
            || !TryReadStrictBool(semantics, "canUnfetter", out bool canUnfetter)
            || force < 0
            || services < 0
            || karmaCost < 0)
        {
            return null;
        }

        return new CharacterSpiritFetteringState(
            spiritId,
            entityType,
            created,
            fettered,
            force,
            services,
            bound,
            spriteAllowed,
            costExact,
            karmaCost,
            availableKarma,
            canFetter,
            canUnfetter);
    }

    private static CharacterSpiritNameChoiceState? ProjectSpiritNameChoice(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Spirit
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid spiritId)
            || spiritId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "nameChoiceSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "spiritId", out string projectedIdText, 36)
            || !Guid.TryParseExact(projectedIdText, "D", out Guid projectedId)
            || projectedId != spiritId
            || !TryReadStrictString(semantics, "entityType", out string entityType, 16)
            || !TryReadStrictString(
                semantics,
                "currentName",
                out string currentName,
                CharacterSpiritNameChoiceRules.MaximumNameLength)
            || !TryGetPropertyValueIgnoreCase(semantics, "allowedNames", out JsonNode? allowedNode)
            || allowedNode is not JsonArray allowedArray)
        {
            return null;
        }

        var allowed = new List<string>(allowedArray.Count);
        foreach (JsonNode? node in allowedArray)
        {
            if (node is not JsonValue value
                || !value.TryGetValue(out string? name)
                || name is null
                || name.Length > CharacterSpiritNameChoiceRules.MaximumNameLength)
            {
                return null;
            }
            allowed.Add(name);
        }

        var state = new CharacterSpiritNameChoiceState(
            spiritId,
            entityType,
            currentName,
            allowed.ToArray());
        return CharacterSpiritNameChoiceRules.IsValidState(state) ? state : null;
    }

    private static CharacterWeaponHomeNodeSemantics? ProjectWeaponHomeNode(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Weapon
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid weaponId)
            || weaponId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "homeNodeSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "weaponId", out string projectedWeaponIdText, 36)
            || !Guid.TryParseExact(projectedWeaponIdText, "D", out Guid projectedWeaponId)
            || projectedWeaponId != weaponId
            || !TryReadStrictString(semantics, "matrixOwnerId", out string ownerIdText, 36)
            || !Guid.TryParseExact(ownerIdText, "D", out Guid ownerId)
            || !TryReadStrictString(semantics, "matrixOwnerKind", out string ownerKind, 32)
            || !TryReadStrictBool(semantics, "visible", out bool visible)
            || !TryReadStrictBool(semantics, "enabled", out bool enabled)
            || !TryReadStrictBool(semantics, "homeNode", out bool homeNode)
            || !TryReadStrictBool(semantics, "isCommlink", out bool isCommlink)
            || !TryReadStrictInt(semantics, "deviceRating", out int deviceRating)
            || !TryReadStrictInt(semantics, "programLimit", out int programLimit)
            || !TryReadStrictInt(semantics, "depTotal", out int depTotal)
            || deviceRating < 0
            || programLimit < 0
            || depTotal < 0)
        {
            return null;
        }

        if (!visible)
        {
            return ownerId == Guid.Empty
                && string.IsNullOrEmpty(ownerKind)
                && !enabled
                && !isCommlink
                && deviceRating == 0
                && programLimit == 0
                && depTotal == 0
                    ? new CharacterWeaponHomeNodeSemantics(
                        weaponId,
                        Guid.Empty,
                        string.Empty,
                        Visible: false,
                        Enabled: false,
                        HomeNode: homeNode,
                        IsCommlink: false,
                        DeviceRating: 0,
                        ProgramLimit: 0,
                        DepTotal: 0)
                    : null;
        }

        if (ownerId == Guid.Empty
            || ownerKind is not ("Gear" or "Armor" or "Cyberware" or "Vehicle")
            || enabled && !isCommlink)
        {
            return null;
        }
        return new CharacterWeaponHomeNodeSemantics(
            weaponId,
            ownerId,
            ownerKind,
            visible,
            enabled,
            homeNode,
            isCommlink,
            deviceRating,
            programLimit,
            depTotal);
    }

    private static CharacterWeaponActiveCommlinkSemantics? ProjectWeaponActiveCommlink(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Weapon
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid weaponId)
            || weaponId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "activeCommlinkSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "weaponId", out string projectedWeaponIdText, 36)
            || !Guid.TryParseExact(projectedWeaponIdText, "D", out Guid projectedWeaponId)
            || projectedWeaponId != weaponId
            || !TryReadStrictString(semantics, "matrixOwnerId", out string ownerIdText, 36)
            || !Guid.TryParseExact(ownerIdText, "D", out Guid ownerId)
            || ownerId == Guid.Empty
            || !TryReadStrictString(semantics, "matrixOwnerKind", out string ownerKind, 32)
            || ownerKind is not ("Gear" or "Armor" or "Cyberware" or "Vehicle")
            || !TryReadStrictBool(semantics, "activeCommlink", out bool activeCommlink)
            || !TryReadStrictBool(semantics, "isCommlink", out bool isCommlink))
        {
            return null;
        }

        return new CharacterWeaponActiveCommlinkSemantics(
            weaponId,
            ownerId,
            ownerKind,
            activeCommlink,
            isCommlink);
    }

    private static CharacterGearActiveCommlinkSemantics? ProjectGearActiveCommlink(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Gear
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid gearId)
            || gearId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "activeCommlinkSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "gearId", out string projectedGearIdText, 36)
            || !Guid.TryParseExact(projectedGearIdText, "D", out Guid projectedGearId)
            || projectedGearId != gearId
            || !TryReadStrictBool(semantics, "activeCommlink", out bool activeCommlink)
            || !TryReadStrictBool(semantics, "isCommlink", out bool isCommlink))
        {
            return null;
        }

        return new CharacterGearActiveCommlinkSemantics(
            gearId,
            activeCommlink,
            isCommlink);
    }

    private static CharacterPrototypeTranshumanSemantics? ProjectPrototypeTranshuman(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Cyberware
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid cyberwareId)
            || cyberwareId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "prototypeTranshumanSemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictString(semantics, "cyberwareId", out string projectedIdText, 36)
            || !Guid.TryParseExact(projectedIdText, "D", out Guid projectedId)
            || projectedId != cyberwareId
            || !TryReadStrictBool(semantics, "prototypeTranshuman", out bool selected)
            || !TryReadStrictDecimal(semantics, "essenceAllowance", out decimal essenceAllowance)
            || essenceAllowance <= 0m
            || !TryGetPropertyValueIgnoreCase(semantics, "hierarchy", out JsonNode? hierarchyNode)
            || hierarchyNode is not JsonArray hierarchyArray
            || hierarchyArray.Count == 0)
        {
            return null;
        }

        var hierarchy = new List<CharacterPrototypeTranshumanNodeState>(hierarchyArray.Count);
        var identities = new HashSet<Guid>();
        foreach (JsonNode? node in hierarchyArray)
        {
            if (node is not JsonObject entry
                || !TryReadStrictString(entry, "cyberwareId", out string idText, 36)
                || !Guid.TryParseExact(idText, "D", out Guid id)
                || id == Guid.Empty
                || !identities.Add(id)
                || !TryReadStrictBool(entry, "prototypeTranshuman", out bool value))
            {
                return null;
            }
            hierarchy.Add(new CharacterPrototypeTranshumanNodeState(id, value));
        }
        if (hierarchy[0].CyberwareId != cyberwareId
            || hierarchy[0].PrototypeTranshuman != selected)
        {
            return null;
        }

        return new CharacterPrototypeTranshumanSemantics(
            cyberwareId,
            selected,
            essenceAllowance,
            hierarchy.ToArray());
    }

    private static CharacterArmorEquipmentState? ProjectArmorEquipment(
        SectionSchema schema,
        JsonObject section,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Armor
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid selectedArmorId)
            || selectedArmorId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(section, schema.CollectionProperty, out JsonNode? collectionNode)
            || collectionNode is not JsonArray collection)
        {
            return null;
        }

        List<CharacterArmorEquipmentBasis> armors = [];
        foreach (JsonNode? node in collection)
        {
            if (node is not JsonObject armor
                || !Guid.TryParseExact(ReadText(armor, "guid"), "D", out Guid armorId)
                || armorId == Guid.Empty
                || !TryReadStrictBool(armor, "equipped", out bool equipped)
                || !TryReadStrictBool(armor, "equippedExact", out bool equippedExact))
            {
                return null;
            }
            armors.Add(new CharacterArmorEquipmentBasis(armorId, equipped, equippedExact));
        }

        return CharacterArmorEquipmentRules.TryProject(selectedArmorId, armors, out CharacterArmorEquipmentState? state)
            ? state
            : null;
    }

    private static WorkspaceArmorDamageAdjustmentState? ProjectArmorDamageAdjustment(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Armor
            || schema.NestedKind is not null
            || !Guid.TryParseExact(target.ItemId, "D", out Guid armorId)
            || armorId == Guid.Empty
            || !TryReadStrictBool(item, "careerEditable", out bool careerEditable)
            || !careerEditable
            || !TryReadStrictBool(item, "armorDamageMaximumExact", out bool maximumExact)
            || !maximumExact
            || !TryReadStrictInt(item, "armorDamage", out int damage)
            || !TryReadStrictInt(item, "armorDamageMaximum", out int maximum)
            || damage < 0
            || maximum < 0)
        {
            return null;
        }

        return new WorkspaceArmorDamageAdjustmentState(
            armorId,
            damage,
            maximum,
            CharacterArmorDamageRules.CanRepair(damage),
            CharacterArmorDamageRules.CanDegrade(damage, maximum));
    }

    private static WorkspaceGearQuantityLifecycleState? ProjectGearQuantityLifecycle(
        SectionSchema schema,
        JsonObject section,
        JsonObject item,
        WorkspaceCollectionItemTarget target)
    {
        if (schema.Kind != WorkspaceCollectionKind.Gear
            || schema.NestedKind is not null
            || !ReadBool(item, "careerEditable")
            || !Guid.TryParseExact(target.ItemId, "D", out Guid gearId)
            || gearId == Guid.Empty
            || !TryGetPropertyValueIgnoreCase(item, "quantitySemantics", out JsonNode? semanticsNode)
            || semanticsNode is not JsonObject semantics
            || !TryReadStrictDecimal(semantics, "quantity", out decimal quantity)
            || !TryReadStrictInt(semantics, "decimalPlaces", out int decimalPlaces)
            || !TryReadStrictDecimal(semantics, "minimumIncrement", out decimal minimumIncrement)
            || !TryReadStrictDecimal(semantics, "purchaseUnitCost", out decimal purchaseUnitCost)
            || !TryReadStrictBool(semantics, "purchaseUnitCostExact", out bool purchaseUnitCostExact)
            || decimalPlaces is < 0 or > 28
            || !CharacterGearQuantityRules.IsValidAmount(quantity, minimumIncrement)
            || !TryGetPropertyValueIgnoreCase(semantics, "mergeCandidateGuids", out JsonNode? candidatesNode)
            || candidatesNode is not JsonArray candidateIds
            || !TryGetPropertyValueIgnoreCase(section, schema.CollectionProperty, out JsonNode? collectionNode)
            || collectionNode is not JsonArray collection)
        {
            return null;
        }

        List<WorkspaceGearMergeCandidateState> candidates = [];
        HashSet<Guid> seen = [];
        foreach (JsonNode? candidateIdNode in candidateIds)
        {
            if (candidateIdNode is not JsonValue candidateIdValue
                || !candidateIdValue.TryGetValue(out string? candidateIdText)
                || !Guid.TryParseExact(candidateIdText, "D", out Guid candidateId)
                || candidateId == Guid.Empty
                || candidateId == gearId
                || !seen.Add(candidateId))
            {
                return null;
            }

            JsonObject[] matches = collection
                .OfType<JsonObject>()
                .Where(candidate => string.Equals(
                    ReadText(candidate, schema.ItemIdProperty),
                    candidateId.ToString("D"),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1
                || !TryGetPropertyValueIgnoreCase(matches[0], "quantitySemantics", out JsonNode? candidateSemanticsNode)
                || candidateSemanticsNode is not JsonObject candidateSemantics
                || !TryReadStrictDecimal(candidateSemantics, "quantity", out decimal candidateQuantity))
            {
                return null;
            }

            candidates.Add(new WorkspaceGearMergeCandidateState(
                candidateId,
                FirstNonBlank(ReadText(matches[0], "name"), candidateId.ToString("D")),
                candidateQuantity));
        }

        return new WorkspaceGearQuantityLifecycleState(
            GearId: gearId,
            Quantity: quantity,
            DecimalPlaces: decimalPlaces,
            MinimumIncrement: minimumIncrement,
            PurchaseUnitCost: purchaseUnitCost,
            PurchaseUnitCostExact: purchaseUnitCostExact,
            MergeCandidates: candidates);
    }

    private static bool TryReadStrictBool(JsonObject source, string propertyName, out bool value)
    {
        value = false;
        return TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            && node is JsonValue jsonValue
            && jsonValue.TryGetValue(out value);
    }

    private static bool TryReadStrictInt(JsonObject source, string propertyName, out int value)
    {
        value = 0;
        return TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            && node is JsonValue jsonValue
            && jsonValue.TryGetValue(out value);
    }

    private static bool TryReadStrictDecimal(JsonObject source, string propertyName, out decimal value)
    {
        value = 0m;
        return TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            && node is JsonValue jsonValue
            && jsonValue.TryGetValue(out value);
    }

    private static IReadOnlyList<WorkspaceLocationItemState>? TryProjectVehicleLocations(JsonObject vehicle)
    {
        if (!TryGetPropertyValueIgnoreCase(vehicle, "locationCount", out JsonNode? countNode)
            || countNode is not JsonValue countValue
            || !countValue.TryGetValue(out int count)
            || count is < 0 or > MaximumVehicleLocationCount
            || !TryGetPropertyValueIgnoreCase(vehicle, "locations", out JsonNode? locationsNode)
            || locationsNode is not JsonArray locations
            || locations.Count != count)
        {
            return null;
        }

        List<WorkspaceLocationItemState> result = new(count);
        HashSet<Guid> identities = [];
        foreach (JsonNode? node in locations)
        {
            if (node is not JsonObject location
                || !TryReadStrictString(location, "guid", out string guidText, 36)
                || !Guid.TryParseExact(guidText, "D", out Guid id)
                || id == Guid.Empty
                || !identities.Add(id)
                || !TryReadStrictString(location, "name", out string name, MaximumLocationNameLength)
                || !TryReadStrictString(location, "notes", out string notes, MaximumTextLength))
            {
                return null;
            }

            result.Add(new WorkspaceLocationItemState(id, name, notes));
        }

        return result;
    }

    private static bool TryReadStrictString(
        JsonObject source,
        string propertyName,
        out string value,
        int maximumLength)
    {
        value = string.Empty;
        if (!TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            || node is not JsonValue jsonValue
            || !jsonValue.TryGetValue(out string? candidate)
            || candidate is null
            || candidate.Length > maximumLength)
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static WorkspaceLinkedCharacterState ProjectLinkedCharacter(JsonObject item)
    {
        JsonObject? linked = TryGetPropertyValueIgnoreCase(item, "linkedCharacter", out JsonNode? node)
            ? node as JsonObject
            : null;
        bool isLinked = linked is not null && ReadBool(linked, "isLinked");
        return new WorkspaceLinkedCharacterState(
            IsLinked: isLinked,
            IdentityResolved: linked is not null && ReadBool(linked, "identityResolved"),
            FileName: linked is null ? string.Empty : ReadText(linked, "fileName"),
            RelativeFileName: linked is null ? string.Empty : ReadText(linked, "relativeFileName"),
            DisplayName: linked is null ? string.Empty : ReadText(linked, "displayName"),
            CanAttach: ReadBool(item, "editSemanticsExact"),
            CanRemove: isLinked);
    }

    private static WorkspaceContactEditorState ProjectContact(JsonObject item)
    {
        bool exact = ReadBool(item, "editSemanticsExact");
        int maximum = ReadInt(item, "connectionMaximum");
        return new WorkspaceContactEditorState(
            Connection: ReadInt(item, "connection"),
            ConnectionMaximum: exact && maximum is >= 1 and <= 12 ? maximum : 0,
            ConnectionEditable: exact && ReadBool(item, "connectionEditable"),
            Loyalty: ReadInt(item, "loyalty"),
            LoyaltyMaximum: 6,
            LoyaltyEditable: exact && ReadBool(item, "loyaltyEditable"),
            Exact: exact);
    }

    private static WorkspaceItemConditionMonitorState ProjectVehiclePhysicalConditionMonitor(JsonObject item)
    {
        int filled = ReadInt(item, "physicalDamage");
        int maximum = ReadInt(item, "physicalConditionMaximum");
        bool maximumExact = ReadBool(item, "physicalConditionMaximumExact") && maximum > 0;
        bool filledValid = filled >= 0 && maximumExact && filled <= maximum;
        return new WorkspaceItemConditionMonitorState(
            Label: "Physical damage",
            Filled: filled,
            Maximum: maximumExact ? maximum : 0,
            MaximumExact: maximumExact,
            Editable: filledValid && ReadBool(item, "careerEditable"));
    }

    private static WorkspaceItemConditionMonitorState ProjectMatrixConditionMonitor(JsonObject item)
    {
        int filled = ReadInt(item, "matrixDamage");
        int maximum = ReadInt(item, "matrixConditionMaximum");
        bool maximumExact = ReadBool(item, "matrixConditionMaximumExact") && maximum > 0;
        bool filledValid = filled >= 0 && maximumExact && filled <= maximum;
        return new WorkspaceItemConditionMonitorState(
            Label: "Matrix damage",
            Filled: filled,
            Maximum: maximumExact ? maximum : 0,
            MaximumExact: maximumExact,
            Editable: filledValid && ReadBool(item, "careerEditable"));
    }

    private static bool TryCreateTarget(
        SectionSchema schema,
        JsonObject item,
        out WorkspaceCollectionItemTarget? target)
    {
        string itemId = ReadText(item, schema.ItemIdProperty).Trim();
        if (schema.Kind == WorkspaceCollectionKind.Gear
            && schema.NestedKind is null
            && (ReadInt(item, "depth") > 0 || !string.IsNullOrWhiteSpace(ReadText(item, "parentGuid"))))
        {
            string nestedParentId = ReadText(item, "parentGuid").Trim();
            target = string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(nestedParentId)
                ? null
                : new WorkspaceCollectionItemTarget(
                    WorkspaceCollectionKind.Gear,
                    nestedParentId,
                    WorkspaceNestedCollectionKind.Gear,
                    itemId);
            return target is not null;
        }
        if (schema.Kind == WorkspaceCollectionKind.Cyberware
            && schema.NestedKind is null
            && !string.IsNullOrWhiteSpace(ReadText(item, "parentGuid")))
        {
            string nestedParentId = ReadText(item, "parentGuid").Trim();
            target = string.IsNullOrWhiteSpace(itemId)
                ? null
                : new WorkspaceCollectionItemTarget(
                    WorkspaceCollectionKind.Cyberware,
                    nestedParentId,
                    WorkspaceNestedCollectionKind.CyberwarePlugin,
                    itemId);
            return target is not null;
        }

        if (schema.NestedKind is null)
        {
            target = string.IsNullOrWhiteSpace(itemId)
                ? null
                : new WorkspaceCollectionItemTarget(schema.Kind, itemId);
            return target is not null;
        }

        string parentId = ReadText(item, schema.ParentIdProperty!).Trim();
        target = string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(itemId)
            ? null
            : new WorkspaceCollectionItemTarget(schema.Kind, parentId, schema.NestedKind, itemId);
        return target is not null;
    }

    private static IReadOnlyList<WorkspaceCollectionTextField> ResolveTextFields(
        SectionSchema schema,
        JsonObject item)
    {
        if (schema.NestedKind is not null)
        {
            List<WorkspaceCollectionTextField> nestedFields =
            [
                WorkspaceCollectionTextField.Name,
                WorkspaceCollectionTextField.Category,
                WorkspaceCollectionTextField.Source,
                WorkspaceCollectionTextField.Notes,
                WorkspaceCollectionTextField.CustomName,
                WorkspaceCollectionTextField.Location
            ];
            if (schema.Kind == WorkspaceCollectionKind.Gear
                && schema.NestedKind == WorkspaceNestedCollectionKind.Gear)
            {
                nestedFields.Insert(1, WorkspaceCollectionTextField.GearName);
            }
            return nestedFields;
        }

        if (schema.Kind == WorkspaceCollectionKind.Pet)
        {
            return
            [
                WorkspaceCollectionTextField.Name,
                WorkspaceCollectionTextField.Metatype,
                WorkspaceCollectionTextField.Notes
            ];
        }

        if (schema.Kind == WorkspaceCollectionKind.Lifestyle)
        {
            return
            [
                WorkspaceCollectionTextField.CustomName,
                WorkspaceCollectionTextField.Notes,
                WorkspaceCollectionTextField.NotesColor
            ];
        }

        List<WorkspaceCollectionTextField> fields = [];
        if (schema.Kind != WorkspaceCollectionKind.InitiationGrade)
        {
            fields.Add(WorkspaceCollectionTextField.Name);
        }

        fields.Add(WorkspaceCollectionTextField.Notes);
        if (schema.Kind != WorkspaceCollectionKind.InitiationGrade)
        {
            fields.Add(WorkspaceCollectionTextField.CustomName);
        }

        switch (schema.Kind)
        {
            case WorkspaceCollectionKind.Gear:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.GearName,
                    WorkspaceCollectionTextField.Category,
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Location
                ]);
                break;
            case WorkspaceCollectionKind.Weapon:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Category,
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Damage,
                    WorkspaceCollectionTextField.Accuracy,
                    WorkspaceCollectionTextField.Mode,
                    WorkspaceCollectionTextField.ArmorPenetration
                ]);
                break;
            case WorkspaceCollectionKind.Armor:
                fields.AddRange([WorkspaceCollectionTextField.Category, WorkspaceCollectionTextField.Source, WorkspaceCollectionTextField.ArmorValue]);
                break;
            case WorkspaceCollectionKind.Skill:
                fields.Add(WorkspaceCollectionTextField.Category);
                break;
            case WorkspaceCollectionKind.Contact:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Role,
                    WorkspaceCollectionTextField.Location,
                    WorkspaceCollectionTextField.Metatype,
                    WorkspaceCollectionTextField.Gender,
                    WorkspaceCollectionTextField.Age,
                    WorkspaceCollectionTextField.ContactType,
                    WorkspaceCollectionTextField.PreferredPayment,
                    WorkspaceCollectionTextField.HobbiesVice,
                    WorkspaceCollectionTextField.PersonalLife,
                    WorkspaceCollectionTextField.GroupName
                ]);
                break;
            case WorkspaceCollectionKind.Vehicle:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Category,
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Handling,
                    WorkspaceCollectionTextField.Speed,
                    WorkspaceCollectionTextField.Body,
                    WorkspaceCollectionTextField.Sensor,
                    WorkspaceCollectionTextField.Seats
                ]);
                break;
            case WorkspaceCollectionKind.Quality:
                fields.Add(WorkspaceCollectionTextField.Source);
                break;
            case WorkspaceCollectionKind.Drug:
                fields.AddRange([WorkspaceCollectionTextField.Category, WorkspaceCollectionTextField.Source]);
                break;
            case WorkspaceCollectionKind.Cyberware:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Category,
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Location,
                    WorkspaceCollectionTextField.Grade,
                    WorkspaceCollectionTextField.Capacity
                ]);
                break;
            case WorkspaceCollectionKind.Spell:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Category,
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Type,
                    WorkspaceCollectionTextField.Range,
                    WorkspaceCollectionTextField.Duration,
                    WorkspaceCollectionTextField.DrainValue
                ]);
                break;
            case WorkspaceCollectionKind.Power:
                fields.Add(WorkspaceCollectionTextField.Source);
                break;
            case WorkspaceCollectionKind.ComplexForm:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Target,
                    WorkspaceCollectionTextField.Duration,
                    WorkspaceCollectionTextField.FadingValue
                ]);
                break;
            case WorkspaceCollectionKind.MatrixProgram:
                fields.AddRange([WorkspaceCollectionTextField.Source, WorkspaceCollectionTextField.Slot]);
                break;
            case WorkspaceCollectionKind.InitiationGrade:
                fields.Add(WorkspaceCollectionTextField.Reward);
                break;
            case WorkspaceCollectionKind.Spirit:
                if (ReadBool(item, "critterNameEditableExact"))
                {
                    fields.Add(WorkspaceCollectionTextField.CritterName);
                }
                break;
            case WorkspaceCollectionKind.CritterPower:
                fields.AddRange(
                [
                    WorkspaceCollectionTextField.Category,
                    WorkspaceCollectionTextField.Source,
                    WorkspaceCollectionTextField.Type,
                    WorkspaceCollectionTextField.Mode,
                    WorkspaceCollectionTextField.Range,
                    WorkspaceCollectionTextField.Duration
                ]);
                break;
            default:
                throw new InvalidOperationException($"Unsupported collection editor kind '{schema.Kind}'.");
        }

        return fields;
    }

    private static bool SupportsRating(SectionSchema schema)
        => schema.NestedKind is not null
            || schema.Kind is WorkspaceCollectionKind.Gear
                or WorkspaceCollectionKind.Armor
                or WorkspaceCollectionKind.Drug
                or WorkspaceCollectionKind.Cyberware
                or WorkspaceCollectionKind.Power
                or WorkspaceCollectionKind.CritterPower;

    private static bool SupportsQuantity(SectionSchema schema)
        => schema.NestedKind == WorkspaceNestedCollectionKind.Gear
            || schema.NestedKind is null
                && schema.Kind is WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Drug;

    private static IReadOnlyList<WorkspaceCollectionIntegerField> ResolveIntegerFields(
        SectionSchema schema,
        JsonObject item)
    {
        if (schema.NestedKind is not null || schema.Kind != WorkspaceCollectionKind.Spirit)
        {
            return [];
        }

        List<WorkspaceCollectionIntegerField> fields = [WorkspaceCollectionIntegerField.Services];
        if (ReadBool(item, "forceMaximumExact"))
        {
            fields.Add(WorkspaceCollectionIntegerField.Force);
        }
        return fields;
    }

    private static int ResolveIntegerMaximum(JsonObject item, WorkspaceCollectionIntegerField field)
        => field == WorkspaceCollectionIntegerField.Force
            ? ReadInt(item, "forceMaximum")
            : int.MaxValue;

    private static bool IsIntegerFieldEnabled(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionIntegerField field)
        => schema.Kind != WorkspaceCollectionKind.Spirit
            || field != WorkspaceCollectionIntegerField.Force
            || ReadBool(item, "forceEditable");

    private static string? ResolveIntegerFieldLabel(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionIntegerField field)
    {
        if (schema.Kind != WorkspaceCollectionKind.Spirit)
        {
            return null;
        }

        bool sprite = IsSprite(item);
        return field switch
        {
            WorkspaceCollectionIntegerField.Services => sprite ? "Tasks owed" : "Services owed",
            WorkspaceCollectionIntegerField.Force => sprite ? "Rating" : "Force",
            _ => null
        };
    }

    private static IReadOnlyList<WorkspaceCollectionToggleField> ResolveToggleFields(SectionSchema schema)
    {
        if (schema.NestedKind is not null)
        {
            List<WorkspaceCollectionToggleField> nestedFields =
            [
                WorkspaceCollectionToggleField.Equipped,
                WorkspaceCollectionToggleField.WirelessEnabled
            ];
            if (schema.NestedKind is WorkspaceNestedCollectionKind.Gear or WorkspaceNestedCollectionKind.CyberwarePlugin)
            {
                nestedFields.Add(WorkspaceCollectionToggleField.HomeNode);
            }

            return nestedFields;
        }

        return schema.Kind switch
        {
            WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Cyberware =>
            [
                WorkspaceCollectionToggleField.Equipped,
                WorkspaceCollectionToggleField.WirelessEnabled,
                WorkspaceCollectionToggleField.HomeNode
            ],
            WorkspaceCollectionKind.Weapon =>
            [
                WorkspaceCollectionToggleField.Equipped,
                WorkspaceCollectionToggleField.WirelessEnabled
            ],
            WorkspaceCollectionKind.Armor => [WorkspaceCollectionToggleField.WirelessEnabled],
            WorkspaceCollectionKind.Spirit => [WorkspaceCollectionToggleField.Bound],
            WorkspaceCollectionKind.InitiationGrade =>
            [
                WorkspaceCollectionToggleField.Resonance,
                WorkspaceCollectionToggleField.Group,
                WorkspaceCollectionToggleField.Ordeal,
                WorkspaceCollectionToggleField.Schooling
            ],
            WorkspaceCollectionKind.Contact =>
            [
                WorkspaceCollectionToggleField.Group,
                WorkspaceCollectionToggleField.Free,
                WorkspaceCollectionToggleField.Family,
                WorkspaceCollectionToggleField.Blackmail
            ],
            _ => []
        };
    }

    private static bool IsTextFieldEnabled(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionTextField field)
        => schema.Kind switch
        {
            WorkspaceCollectionKind.Contact
                when field is WorkspaceCollectionTextField.Name
                    or WorkspaceCollectionTextField.Metatype
                    or WorkspaceCollectionTextField.Gender
                    or WorkspaceCollectionTextField.Age => ReadBool(item, "identityEditable"),
            WorkspaceCollectionKind.Pet
                when field is WorkspaceCollectionTextField.Name
                    or WorkspaceCollectionTextField.Metatype => ReadBool(item, "identityEditable"),
            WorkspaceCollectionKind.Spirit
                when field == WorkspaceCollectionTextField.CritterName => ReadBool(item, "critterNameEditableExact"),
            _ => true
        };

    private static bool IsToggleEnabled(
        SectionSchema schema,
        JsonObject section,
        JsonObject item,
        WorkspaceCollectionToggleField field)
    {
        if (schema.Kind == WorkspaceCollectionKind.Spirit
            && field == WorkspaceCollectionToggleField.Bound)
        {
            return ReadBool(section, "created");
        }
        if (schema.Kind != WorkspaceCollectionKind.Contact)
        {
            return true;
        }

        return field switch
        {
            WorkspaceCollectionToggleField.Group => ReadBool(item, "groupEditable"),
            WorkspaceCollectionToggleField.Free => ReadBool(item, "freeEditable"),
            WorkspaceCollectionToggleField.Family => ReadBool(item, "familyEditable"),
            WorkspaceCollectionToggleField.Blackmail => ReadBool(item, "blackmailEditable"),
            _ => false
        };
    }

    private static string? ResolveToggleFieldLabel(
        SectionSchema schema,
        JsonObject item,
        WorkspaceCollectionToggleField field)
        => schema.Kind == WorkspaceCollectionKind.Spirit
            && field == WorkspaceCollectionToggleField.Bound
            ? IsSprite(item) ? "Registered" : "Bound"
            : null;

    private static bool IsSprite(JsonObject item)
        => string.Equals(ReadText(item, "entityType"), "Sprite", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<WorkspaceNestedCollectionKind> ResolveAddableNestedKinds(SectionSchema schema)
    {
        if (schema.NestedKind is not null)
        {
            return [];
        }

        return schema.Kind switch
        {
            WorkspaceCollectionKind.Gear => [WorkspaceNestedCollectionKind.Gear],
            WorkspaceCollectionKind.Cyberware => [WorkspaceNestedCollectionKind.CyberwarePlugin],
            WorkspaceCollectionKind.Weapon => [WorkspaceNestedCollectionKind.WeaponAccessory],
            WorkspaceCollectionKind.Armor => [WorkspaceNestedCollectionKind.ArmorMod],
            WorkspaceCollectionKind.Vehicle => [WorkspaceNestedCollectionKind.VehicleMod],
            _ => []
        };
    }

    private static string ResolveJsonProperty(WorkspaceCollectionTextField field)
        => field switch
        {
            WorkspaceCollectionTextField.Name => "name",
            WorkspaceCollectionTextField.Category => "category",
            WorkspaceCollectionTextField.Source => "source",
            WorkspaceCollectionTextField.Notes => "notes",
            WorkspaceCollectionTextField.CustomName => "customName",
            WorkspaceCollectionTextField.GearName => "gearName",
            WorkspaceCollectionTextField.Location => "location",
            WorkspaceCollectionTextField.Role => "role",
            WorkspaceCollectionTextField.Grade => "grade",
            WorkspaceCollectionTextField.Damage => "damage",
            WorkspaceCollectionTextField.ArmorValue => "armorValue",
            WorkspaceCollectionTextField.Accuracy => "accuracy",
            WorkspaceCollectionTextField.Mode => "mode",
            WorkspaceCollectionTextField.ArmorPenetration => "ap",
            WorkspaceCollectionTextField.Handling => "handling",
            WorkspaceCollectionTextField.Speed => "speed",
            WorkspaceCollectionTextField.Body => "body",
            WorkspaceCollectionTextField.Sensor => "sensor",
            WorkspaceCollectionTextField.Seats => "seats",
            WorkspaceCollectionTextField.Type => "type",
            WorkspaceCollectionTextField.Range => "range",
            WorkspaceCollectionTextField.Duration => "duration",
            WorkspaceCollectionTextField.DrainValue => "drainValue",
            WorkspaceCollectionTextField.Target => "target",
            WorkspaceCollectionTextField.FadingValue => "fadingValue",
            WorkspaceCollectionTextField.Capacity => "capacity",
            WorkspaceCollectionTextField.Slot => "rating",
            WorkspaceCollectionTextField.Reward => "reward",
            WorkspaceCollectionTextField.Metatype => "metatype",
            WorkspaceCollectionTextField.Gender => "gender",
            WorkspaceCollectionTextField.Age => "age",
            WorkspaceCollectionTextField.ContactType => "contactType",
            WorkspaceCollectionTextField.PreferredPayment => "preferredPayment",
            WorkspaceCollectionTextField.HobbiesVice => "hobbiesVice",
            WorkspaceCollectionTextField.PersonalLife => "personalLife",
            WorkspaceCollectionTextField.GroupName => "groupName",
            WorkspaceCollectionTextField.CritterName => "critterName",
            WorkspaceCollectionTextField.NotesColor => "notesColor",
            _ => throw new InvalidOperationException($"Unsupported collection text field '{field}'.")
        };

    private static string ResolveJsonProperty(WorkspaceCollectionToggleField field)
        => field switch
        {
            WorkspaceCollectionToggleField.Equipped => "equipped",
            WorkspaceCollectionToggleField.WirelessEnabled => "wirelessEnabled",
            WorkspaceCollectionToggleField.HomeNode => "homeNode",
            WorkspaceCollectionToggleField.Bound => "bound",
            WorkspaceCollectionToggleField.Resonance => "res",
            WorkspaceCollectionToggleField.Group => "group",
            WorkspaceCollectionToggleField.Ordeal => "ordeal",
            WorkspaceCollectionToggleField.Schooling => "schooling",
            WorkspaceCollectionToggleField.Free => "free",
            WorkspaceCollectionToggleField.Family => "family",
            WorkspaceCollectionToggleField.Blackmail => "blackmail",
            _ => throw new InvalidOperationException($"Unsupported collection toggle field '{field}'.")
        };

    private static string ResolveJsonProperty(WorkspaceCollectionIntegerField field)
        => field switch
        {
            WorkspaceCollectionIntegerField.Services => "services",
            WorkspaceCollectionIntegerField.Force => "force",
            _ => throw new InvalidOperationException($"Unsupported collection integer field '{field}'.")
        };

    private static string ResolveJsonProperty(
        SectionSchema schema,
        WorkspaceCollectionToggleField field)
        => schema.Kind == WorkspaceCollectionKind.Contact && field == WorkspaceCollectionToggleField.Group
            ? "isGroup"
            : ResolveJsonProperty(field);

    private static bool TryResolveSchema(string? sectionId, out SectionSchema schema)
    {
        string key = Normalize(sectionId);
        schema = key switch
        {
            "gear" => new(key, "gear", WorkspaceCollectionKind.Gear),
            "weapons" => new(key, "weapons", WorkspaceCollectionKind.Weapon),
            "armors" => new(key, "armors", WorkspaceCollectionKind.Armor),
            "skills" => new(key, "skills", WorkspaceCollectionKind.Skill),
            "contacts" => new(key, "contacts", WorkspaceCollectionKind.Contact),
            "pets" => new(key, "contacts", WorkspaceCollectionKind.Pet),
            "vehicles" => new(key, "vehicles", WorkspaceCollectionKind.Vehicle),
            "qualities" => new(key, "qualities", WorkspaceCollectionKind.Quality),
            "drugs" => new(key, "drugs", WorkspaceCollectionKind.Drug),
            "cyberwares" => new(key, "cyberwares", WorkspaceCollectionKind.Cyberware),
            "spells" => new(key, "spells", WorkspaceCollectionKind.Spell),
            "powers" => new(key, "powers", WorkspaceCollectionKind.Power),
            "complexforms" => new(key, "complexForms", WorkspaceCollectionKind.ComplexForm),
            "aiprograms" => new(key, "aiPrograms", WorkspaceCollectionKind.MatrixProgram),
            "initiationgrades" => new(key, "initiationGrades", WorkspaceCollectionKind.InitiationGrade),
            "spirits" => new(key, "spirits", WorkspaceCollectionKind.Spirit),
            "critterpowers" => new(key, "critterPowers", WorkspaceCollectionKind.CritterPower),
            "lifestyles" => new(key, "lifestyles", WorkspaceCollectionKind.Lifestyle),
            "weaponaccessories" => new(
                key,
                "accessories",
                WorkspaceCollectionKind.Weapon,
                WorkspaceNestedCollectionKind.WeaponAccessory,
                "accessoryGuid",
                "weaponGuid"),
            "armormods" => new(
                key,
                "armorMods",
                WorkspaceCollectionKind.Armor,
                WorkspaceNestedCollectionKind.ArmorMod,
                "modGuid",
                "armorGuid"),
            "vehiclemods" => new(
                key,
                "vehicleMods",
                WorkspaceCollectionKind.Vehicle,
                WorkspaceNestedCollectionKind.VehicleMod,
                "modGuid",
                "vehicleGuid"),
            _ => default
        };
        return !string.IsNullOrWhiteSpace(schema.SectionId);
    }

    private static bool TryGetPropertyValueIgnoreCase(
        JsonObject source,
        string propertyName,
        out JsonNode? value)
    {
        foreach ((string candidateName, JsonNode? candidateValue) in source)
        {
            if (string.Equals(candidateName, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidateValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string ReadText(JsonObject source, string propertyName)
    {
        if (!TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            || node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value && value.TryGetValue(out string? text))
        {
            return text ?? string.Empty;
        }

        return node.ToJsonString().Trim('"');
    }

    private static int ReadInt(JsonObject source, string propertyName)
    {
        if (!TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            || node is null)
        {
            return 0;
        }

        if (node is JsonValue value && value.TryGetValue(out int integer))
        {
            return integer;
        }

        return int.TryParse(ReadText(source, propertyName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private static decimal ReadDecimal(JsonObject source, string propertyName, decimal fallback)
    {
        if (TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            && node is JsonValue value
            && value.TryGetValue(out decimal number))
        {
            return number;
        }

        return decimal.TryParse(
            ReadText(source, propertyName),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : fallback;
    }

    private static bool ReadBool(JsonObject source, string propertyName)
    {
        if (TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            && node is JsonValue value
            && value.TryGetValue(out bool boolean))
        {
            return boolean;
        }

        string text = ReadText(source, propertyName);
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "1", StringComparison.Ordinal);
    }

    private static string BuildIdentityKey(WorkspaceCollectionItemTarget target)
        => target.NestedKind is null
            ? target.ItemId.Trim()
            : $"{target.ItemId.Trim()}\u001f{target.NestedKind}\u001f{target.NestedItemId?.Trim()}";

    private static string Normalize(string? value)
        => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private readonly record struct SectionSchema(
        string SectionId,
        string CollectionProperty,
        WorkspaceCollectionKind Kind,
        WorkspaceNestedCollectionKind? NestedKind = null,
        string ItemIdProperty = "guid",
        string? ParentIdProperty = null);
}
