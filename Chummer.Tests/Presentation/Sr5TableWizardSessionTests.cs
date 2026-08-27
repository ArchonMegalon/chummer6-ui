using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

CareerEdgeUseEditorState edge = Edge(revision: 17);
Sr5TableWizardSnapshot beforeRun = Sr5TableWizardProjector.Project(
    Sr5TableWizardLane.BeforeRun,
    edge);
Assert(beforeRun.Actions.Count == 2, "Before Run must expose only exact Spend/Regain Edge leaves");
Assert(beforeRun.Weapons.Count == 0, "Before Run must not inherit Playtime Weapon authority");
Assert(beforeRun.Actions.All(action => action.Identity.Lane == Sr5TableWizardLane.BeforeRun),
    "Before Run action identity must carry the lane");

CareerWeaponFireEditorState weapon = Weapon(revision: 17);
ExpectInvalid(
    () => new Sr5TableWizardSession().Bind(beforeRun with { Weapons = [weapon] }),
    "Before Run snapshots must reject injected Playtime Weapon authority");
var catalog = new CareerWeaponFireCatalogEditorState(
    edge.WorkspaceId,
    edge.ContentRevision,
    [weapon]);
Sr5TableWizardSnapshot playtime = Sr5TableWizardProjector.Project(
    Sr5TableWizardLane.Playtime,
    edge,
    catalog);
Sr5TableWizardActionState fire = playtime.Actions.Single(action =>
    action.Identity.Kind == Sr5TableWizardActionKind.FireWeapon);
Assert(fire.Identity.WeaponId == weapon.Weapon.Identity.WeaponId,
    "Playtime Weapon identity must bind the exact direct Weapon Guid");
Assert(fire.Identity.AmmoSlot == 1 && fire.Identity.AmmoGearId == Guid.Empty,
    "Playtime Weapon identity must bind the exact active clip and ammo identity");
Assert(fire.Identity.FireMode == CharacterWeaponFireMode.ShortBurst,
    "only the shortened burst that Core can plan may be selected");
Assert(fire.WeaponPlan is { RoundsConsumed: 2, NewAmmoRemaining: 0, RequiresPartialConfirmation: true },
    "Core shortened-burst semantics must survive projection");

var session = new Sr5TableWizardSession();
session.Bind(playtime);
Assert(session.TrySelect(fire.Identity), "exact Playtime action must be selectable");
ExpectInvalid(() => session.CreateWeaponRequest(confirmed: false),
    "an unconfirmed table action must not produce a mutation request");
CareerWeaponFireRequest request = session.CreateWeaponRequest(confirmed: true);
Assert(request.Identity == weapon.Weapon.Identity
       && request.ExpectedNodeRevision == weapon.Weapon.Revision
       && request.Mode == CharacterWeaponFireMode.ShortBurst
       && request.ConfirmedPartial,
    "confirmed request must preserve exact identity, revision, mode, and shortened-burst consent");

Sr5TableWizardCheckpoint checkpoint = session.CreateCheckpoint();
byte[] payload = Sr5TableWizardSession.SerializeCheckpoint(checkpoint);
Assert(Sr5TableWizardSession.TryDeserializeCheckpoint(payload, out Sr5TableWizardCheckpoint? parsed)
       && parsed is not null,
    "review checkpoint must round-trip");
Sr5TableWizardState restored = new Sr5TableWizardSession().Bind(playtime, parsed);
Assert(restored.Resume.Restored && restored.SelectedAction?.Identity == fire.Identity,
    "exact reviewed action must resume");

CareerEdgeUseEditorState revisedEdge = Edge(revision: 18);
CareerWeaponFireEditorState revisedWeapon = Weapon(revision: 18);
Sr5TableWizardSnapshot revised = Sr5TableWizardProjector.Project(
    Sr5TableWizardLane.Playtime,
    revisedEdge,
    new CareerWeaponFireCatalogEditorState(
        revisedEdge.WorkspaceId,
        revisedEdge.ContentRevision,
        [revisedWeapon]));
Sr5TableWizardState stale = new Sr5TableWizardSession().Bind(revised, parsed);
Assert(!stale.Resume.Restored
       && stale.Resume.InvalidationReason
           == Sr5TableWizardCheckpointInvalidationReasons.WorkspaceRevisionChanged,
    "a saved review must fail closed after runner revision changes");

Assert(!session.TrySelect(fire.Identity with { WeaponId = Guid.NewGuid() }),
    "a tampered typed identity must not select by label or mode alone");
ExpectInvalid(
    () => Sr5TableWizardProjector.Project(
        Sr5TableWizardLane.Playtime,
        edge,
        catalog with { ContentRevision = edge.ContentRevision + 1 }),
    "cross-revision catalog composition must fail closed");

CareerWeaponFireCatalogEditorState projectedCatalog =
    CareerWeaponFireEditorProjector.ProjectCatalog(
        WeaponXml(weapon.Weapon.Identity.WeaponId),
        edge.WorkspaceId,
        edge.ContentRevision);
Assert(projectedCatalog.Weapons.Count == 1
       && projectedCatalog.Weapons[0].Weapon.Identity.WeaponId == weapon.Weapon.Identity.WeaponId,
    "catalog projector must preserve an exact eligible direct Weapon identity");
ExpectInvalid(
    () => CareerWeaponFireEditorProjector.ProjectCatalog(
        WeaponXml(weapon.Weapon.Identity.WeaponId).Replace(
            "</weapons>",
            $"<weapon><guid>{weapon.Weapon.Identity.WeaponId:D}</guid></weapon></weapons>",
            StringComparison.Ordinal),
        edge.WorkspaceId,
        edge.ContentRevision),
    "duplicate direct Weapon identities must fail the entire catalog closed");

Console.WriteLine("SR5 Before Run / Playtime presentation tests passed.");

static CareerEdgeUseEditorState Edge(long revision)
    => new(
        new CharacterWorkspaceId("workspace-table-1"),
        revision,
        new CharacterCareerEdgeUseState(
            EdgeUsed: 1,
            TotalEdge: 3,
            CanSpend: true,
            CanRegain: true));

static CareerWeaponFireEditorState Weapon(long revision)
{
    Guid weaponId = Guid.Parse("7a4a31ca-b18f-43d2-9cef-eaef117bf09d");
    CharacterWeaponFireIdentity identity = new(weaponId, AmmoSlot: 1, Guid.Empty);
    CharacterWeaponFireSource source = new(
        RangeType: "Ranged",
        Ammo: "30(c)",
        BaseModes: "BF/FA",
        AllowSingleShot: false,
        AllowShortBurst: true,
        AllowLongBurst: false,
        AllowFullBurst: true,
        AllowSuppressiveFire: false,
        SingleShot: 1,
        ShortBurst: 3,
        LongBurst: 6,
        FullBurst: 10,
        SuppressiveFire: 20,
        Accessories: []);
    Assert(CharacterWeaponFireRules.TryCreateState(
        identity,
        created: true,
        displayName: "Ares Alpha",
        ammoRemaining: 2,
        ammoGearQuantity: null,
        source,
        hasUnsupportedModeSemantics: false,
        out CharacterWeaponFireState state),
        "test Weapon state must be coherent");
    return new CareerWeaponFireEditorState(
        new CharacterWorkspaceId("workspace-table-1"),
        revision,
        state);
}

static string WeaponXml(Guid weaponId)
    => $"""
       <character>
         <created>true</created>
         <weapons>
           <weapon>
             <guid>{weaponId:D}</guid>
             <name>Ares Alpha</name>
             <customname></customname>
             <type>Ranged</type>
             <ammo>30(c)</ammo>
             <mode>BF/FA</mode>
             <allowsingleshot>false</allowsingleshot>
             <allowshortburst>true</allowshortburst>
             <allowlongburst>false</allowlongburst>
             <allowfullburst>true</allowfullburst>
             <allowsuppressive>false</allowsuppressive>
             <singleshot>1</singleshot>
             <shortburst>3</shortburst>
             <longburst>6</longburst>
             <fullburst>10</fullburst>
             <suppressive>20</suppressive>
             <activeammoslot>1</activeammoslot>
             <ammoslots>1</ammoslots>
             <clips><clip><count>2</count><id>00000000-0000-0000-0000-000000000000</id></clip></clips>
             <accessories></accessories>
             <wirelesson>false</wirelesson>
           </weapon>
         </weapons>
         <gears></gears>
       </character>
       """;

static void ExpectInvalid(Action action, string message)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
