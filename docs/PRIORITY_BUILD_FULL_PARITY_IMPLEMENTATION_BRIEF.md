# Priority Build Full Parity Implementation Brief

This document is the fail-closing implementation brief for restoring full Chummer5a parity for the priority-build route, with special focus on two broken surfaces:

1. the metatype/priority continuation dialog
2. the first editable attributes surface that follows it

Treat this as a must-match parity contract, not as a design sketch.

## Why this brief exists

The current promoted route is not parity-complete.

- `Chummer.Presentation/Overview/DesktopDialogFactory.cs` currently builds `dialog.new_character.priority_workflow` as a generic dialog with static priority-letter selects plus a read-only summary snippet.
- `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` does not special-case `dialog.new_character.priority_workflow` in `TryBuildLegacyParityDialog`, so the priority route falls through the generic dialog renderer.
- The generic renderer maps `snippet` fields to a bordered `TextBlock`, not to a parity editor surface.
- `Chummer.Avalonia/Controls/SectionHostControl.axaml` and `SectionHostControl.axaml.cs` currently expose a facts-card strip, rows list, and `SectionReviewExpander` preview posture. That is review chrome, not an editable Chummer5a-style attribute workbench.
- Legacy Chummer5a does have the missing interaction surface:
  - `Chummer/Forms/Character Creation Forms/SelectMetatypePriority.Designer.cs`
  - `Chummer/Forms/Character Creation Forms/SelectMetatypePriority.cs`
  - `Chummer/Controls/Attributes/AttributeControl.Designer.cs`
  - `Chummer/Controls/Attributes/AttributeControl.cs`
  - `Chummer/Forms/Character Forms/CharacterShared.cs`
  - `Chummer/Forms/Character Forms/CharacterCreate.Designer.cs`

## Why the current parity gates missed this

The current gates are too inventory-heavy and too behavior-light for this route.

- `scripts/ai/milestones/generated-dialog-element-parity-check.sh` mainly verifies command IDs, control IDs, rebuildable dialog IDs, and test-marker wiring. It does not prove widget-class parity, dependent-event parity, or mutability of the priority workflow.
- `scripts/ai/milestones/section-host-ruleset-parity-check.sh` mainly verifies tab IDs, workspace action IDs, and ruleset-conditioned inventory. It does not prove that `tab-info.attributes` is editable or that it uses Chummer5a-equivalent controls.
- Current receipts can therefore claim the route is "covered" while still allowing:
  - a summary snippet instead of a real priority pane
  - a review expander instead of numeric up/down attribute editors
  - missing `SelectionChanged` side effects on priority controls

That blind spot must be closed as part of this work.

## Source Of Truth

The successor implementation must match the behavior and visible control classes from these legacy sources first:

- Priority-selection dialog:
  - `Chummer/Forms/Character Creation Forms/SelectMetatypePriority.Designer.cs`
  - `Chummer/Forms/Character Creation Forms/SelectMetatypePriority.cs`
- Character creation attributes surface:
  - `Chummer/Forms/Character Forms/CharacterCreate.Designer.cs`
  - `Chummer/Forms/Character Forms/CharacterCreate.cs`
  - `Chummer/Forms/Character Forms/CharacterShared.cs`
  - `Chummer/Controls/Attributes/AttributeControl.Designer.cs`
  - `Chummer/Controls/Attributes/AttributeControl.cs`
- Parity policy:
  - `docs/CHUMMER5A_MUSCLE_MEMORY_EXIT_TESTS.md`
  - `docs/PARITY_AUDIT.md`
  - `docs/PARITY_CHECKLIST.md`

## Non-Negotiable Parity Rules

- If Chummer5a already had the function, Chummer6 must preserve the same mouse-driven result with the least possible relearning.
- Summary-only or review-only substitute UI is forbidden for an editable legacy route.
- If the legacy route used `ComboBox`, `ListBox`, `CheckBox`, `NumericUpDown`, button strip, tooltip, or popup posture, the successor route must use equivalent interactive control classes. A text summary is not equivalent.
- If the legacy route had dependent `SelectedIndexChanged` or `ValueChanged` behavior, the successor route must fire the equivalent event and update the same dependent surfaces.
- The attributes route is not parity-complete until the user can directly edit attributes with true numeric up/down controls.
- `SectionReviewExpander`, snippet panels, fact cards, and static summaries may exist only as subordinate diagnostics. They must never replace the primary editing surface.
- If one head cannot yet deliver this parity honestly, the route must stay hidden or explicitly incomplete on that head instead of faking parity.

## Required Implementation Shape

### 1. Do not keep this on the generic dialog fallback path

`dialog.new_character.priority_workflow` must no longer be allowed to fall through the generic field renderer.

Required outcome:

- Add a dedicated priority-build parity pane for the priority-workflow dialog.
- The pane must be explicitly wired in the Avalonia dialog host instead of reusing the generic snippet-based form.
- If Blazor exposes the same route, it must render the same control tree and event semantics, or the route must stay hidden there until parity is real.

### 2. Do not keep attributes on the section-review fallback path

The editable creation-time attributes route must not be satisfied by:

- `ClassicAttributeFactsPanel`
- section rows
- `SectionReviewExpander`
- raw payload preview
- any collapsible summary pane as the only visible attribute surface

Required outcome:

- The attributes section for a creation-time priority-build runner must render a real editable attribute panel with one row per attribute and true numeric editors.
- The legacy "facts" and "review" chrome may remain as diagnostics only if it does not displace the editor and is hidden by default on the parity path.

## Recursive UI Contract: Priority Build Dialog

The successor priority-build pane must preserve the legacy information architecture and pane ownership.

### Dialog root

```text
Dialog: "Select Metatype Priority"
└─ Main container (legacy tlpMain equivalent)
   ├─ Top priority matrix (legacy tlpTopHalf equivalent)
   ├─ Left browse lane
   ├─ Right inspect lane
   ├─ Optional dynamic talent-skill choice lane
   └─ Bottom action strip
```

### Top priority matrix

```text
Top priority matrix
├─ Label "Metatype:"
├─ ComboBox cboHeritage
├─ Label "Attributes:"
├─ ComboBox cboAttributes
├─ Label "Magic or Resonance:"
├─ ComboBox cboTalent
├─ ComboBox cboTalents
├─ Label "Skills:"
├─ ComboBox cboSkills
├─ Label "Resources:"
├─ ComboBox cboResources
└─ Label lblSumtoTen
```

Required control behavior:

- `cboHeritage`, `cboAttributes`, `cboTalent`, `cboSkills`, `cboResources`
  - must be real drop-down list controls
  - must not allow free-form text entry
  - must show the same priority-letter posture as legacy
- `cboTalents`
  - must be a real drop-down list control
  - content is populated from the selected talent priority plus current metatype/metavariant constraints
  - this is not optional flavor text; it is part of the priority continuation contract
- `lblSumtoTen`
  - hidden in normal Priority mode
  - visible in Sum-to-Ten mode
  - must show the live total in legacy posture

### Left browse lane

```text
Left browse lane
├─ ComboBox cboCategory
└─ ListBox lstMetatypes
```

Required control behavior:

- `cboCategory`
  - real drop-down list control
  - filters the metatype list
- `lstMetatypes`
  - real listbox, not a summary chip list
  - single selection
  - double click commits the same OK action route as legacy

### Right inspect lane

```text
Right inspect lane
├─ Metavariant row
│  ├─ Label "Metavariant:"
│  └─ ComboBox cboMetavariant
├─ Spirits/possession row
│  ├─ Label "FORCE"
│  ├─ NumericUpDown nudForce
│  ├─ CheckBox chkPossessionBased
│  └─ ComboBox cboPossessionMethod
├─ Summary facts row group
│  ├─ Label "Karma:"
│  ├─ Value lblMetavariantKarma
│  ├─ Label "Special Attributes:"
│  ├─ Value lblSpecialAttributes
│  ├─ Label "Source:"
│  └─ Value/source-link lblSource
├─ Attribute readout grid
│  ├─ BOD label + value
│  ├─ AGI label + value
│  ├─ REA label + value
│  ├─ STR label + value
│  ├─ CHA label + value
│  ├─ INT label + value
│  ├─ LOG label + value
│  └─ WIL label + value
└─ Scrollable qualities panel
   ├─ Label "Qualities:"
   └─ Scrollable text/list body
```

Required control behavior:

- `cboMetavariant`
  - real drop-down list control
  - enabled only when there is more than one metavariant choice
- `nudForce`
  - true numeric up/down control
  - hidden unless the legacy rule path exposes it
- `chkPossessionBased`
  - same visibility rule as legacy
- `cboPossessionMethod`
  - enabled only when `chkPossessionBased` is checked
- `lblSource`
  - must preserve source-open affordance if legacy surface allowed source navigation
- attribute readout grid
  - must stay visible as a live inspect surface tied to the selected metatype/metavariant
  - values must update when the metatype-related selection changes
- qualities panel
  - must be a scrollable inspect lane for the selected metatype/metavariant qualities
  - must not be collapsed into a generic multiline snippet if the list grows

### Dynamic talent-skill choice lane

```text
Dynamic talent-skill choice lane
├─ Label lblMetatypeSkillSelection
├─ ComboBox cboSkill1
├─ ComboBox cboSkill2
└─ ComboBox cboSkill3
```

Required control behavior:

- hidden by default
- shown only when the selected talent node grants one or more free skill or skill-group choices
- each control must be a real drop-down list
- duplicate skill selections are not allowed except where legacy explicitly allows exotic edge cases

### Bottom action strip

```text
Bottom action strip
├─ Button "Cancel"
└─ Button "OK"
```

Required control behavior:

- order must stay `Cancel`, then `OK`, matching legacy action-strip memory
- `OK` must be disabled whenever the route is not in a valid committed state

## Event Contract: Priority Build Dialog

These events are mandatory. Missing any of them is a parity failure.

### Priority selectors

- `cboHeritage.SelectionChanged`
  - In Priority mode: run duplicate-priority reconciliation equivalent to `ManagePriorityItems(cboHeritage)`.
  - In Sum-to-Ten mode: recompute the live total.
  - Then reload and repopulate the dependent metatype data:
    - metatype list
    - metavariant list
    - selected metatype summary
- `cboAttributes.SelectionChanged`
  - In Priority mode: run duplicate-priority reconciliation equivalent to `ManagePriorityItems(cboAttributes)`.
  - In Sum-to-Ten mode: recompute the live total.
- `cboTalent.SelectionChanged`
  - In Priority mode: run duplicate-priority reconciliation equivalent to `ManagePriorityItems(cboTalent)`.
  - In Sum-to-Ten mode: recompute the live total.
  - Repopulate `cboTalents`.
- `cboSkills.SelectionChanged`
  - In Priority mode: run duplicate-priority reconciliation equivalent to `ManagePriorityItems(cboSkills)`.
  - In Sum-to-Ten mode: recompute the live total.
- `cboResources.SelectionChanged`
  - In Priority mode: run duplicate-priority reconciliation equivalent to `ManagePriorityItems(cboResources)`.
  - In Sum-to-Ten mode: recompute the live total.

### Metatype selectors

- `cboCategory.SelectionChanged`
  - repopulate the metatype list for the chosen category
  - in Sum-to-Ten mode, recompute the live total
- `lstMetatypes.SelectionChanged`
  - in Sum-to-Ten mode, recompute the live total
  - repopulate metavariants
  - refresh the selected-metatype inspect lane
  - repopulate `cboTalents`
- `cboMetavariant.SelectionChanged`
  - refresh the selected-metatype inspect lane
  - repopulate `cboTalents`
  - in Sum-to-Ten mode, recompute the live total

### Talent continuation selectors

- `cboTalents.SelectionChanged`
  - rerun the full talent-choice continuation logic from the legacy `ProcessTalentsIndexChanged` flow
  - hide `cboSkill1`, `cboSkill2`, `cboSkill3`, and the explanatory label before recalculating
  - if the selected talent grants skill choices:
    - populate 1..3 real drop-down choice controls
    - preserve prior selection where still valid
    - repair illegal duplicate selections the same way legacy does
  - recompute the displayed special-attribute point total
  - in Sum-to-Ten mode, recompute the live total
- `chkPossessionBased.CheckedChanged`
  - enable or disable `cboPossessionMethod`
- `lstMetatypes.DoubleClick`
  - same commit route as clicking `OK`

### OK action

The `OK` route must still perform the legacy character mutation contract, not a thin starter-XML placeholder write.

Minimum required parity:

- commit the five priority letters
- commit the selected talent choice
- commit the selected free-skill choices
- recalculate metatype karma, special attributes, priority bonuses, and starting nuyen from the real priority tables
- preserve the legacy post-selection point-shift behavior when base/karma buckets must be normalized

## Priority Reconciliation Rules

### Priority mode

- Exactly one of each priority letter must exist across Heritage, Attributes, Talent, Skills, and Resources.
- If the user sets one combo to a letter already owned by another combo, the collided combo must be reassigned to the missing letter, equivalent to legacy `ManagePriorityItems`.
- This must happen immediately on selection change, not on dialog close.

### Sum-to-Ten mode

- Duplicate letters are allowed.
- The live total must update immediately after every relevant selection change.
- The final total must be validated against the settings-defined Sum-to-Ten target before commit.

## Recursive UI Contract: Editable Attributes Surface

This is the route the user reaches after the priority continuation. It must match the legacy editable creation-time attributes surface, not the current review expander.

### Attributes pane root

```text
Common/Attributes parity pane
└─ Middle attributes column
   ├─ Header grid (legacy tlpAlias attribute header equivalent)
   │  ├─ Label "Attribute"
   │  ├─ Label "Base"
   │  ├─ Label "Karma"
   │  ├─ Label "Total"
   │  └─ Label "Limits"
   └─ Scrollable top-down attribute rows panel
      ├─ Attribute row 1
      ├─ Attribute row 2
      ├─ ...
      └─ Attribute row N
```

Required control behavior:

- The header row must stay visible above the editable rows.
- The row area must be a scrollable, dense, top-down editor surface.
- Each attribute row must align to the same five-column rhythm as legacy:
  - name
  - base points editor
  - karma editor
  - live value display
  - metatype limits display

### Attribute row contract

Each row must match the legacy `AttributeControl` job.

```text
AttributeRow
└─ Grid/TableLayout equivalent
   ├─ Name label
   └─ Right-side right-aligned cluster
      ├─ Metatype limits text
      ├─ Live value text
      ├─ NumericUpDown karma editor
      └─ NumericUpDown base editor
```

Creation-time required control types:

- attribute name: label/text block
- base editor: true numeric up/down control
- karma editor: true numeric up/down control
- live value: label/text block
- metatype limits: label/text block

Career-mode follow-up parity, when the same control is reused later:

- replace creation-time numeric editors with the legacy career affordances only where legacy does so
- do not simplify the creation-time path to make the career path easier

### Required row content

For each visible active attribute row:

- the name text must match legacy naming and abbreviation posture
- the base editor must bind to the attribute base bucket
- the karma editor must bind to the attribute karma bucket
- the live value text must show the same display-value posture as legacy
- the metatype limits text must show the same augmented metatype limit posture as legacy

### Required attribute editor rules

- `nudBase`
  - visible in priority-table creation flows
  - enabled only when `BaseUnlocked`
  - maximum must track `PriorityMaximum`
- `nudKarma`
  - maximum must track `KarmaMaximum`
- both editors
  - must be true spinner/up-down controls, not text boxes
  - must support mouse-driven increment/decrement
  - must fire real value-change behavior
- editing must update the bound attribute model, not a detached summary cache

### Required attribute value-change semantics

- Base and karma edits must debounce and commit with the same operational posture as legacy `AttributeControl`:
  - `ValueChanged` schedules a short delayed commit
  - commit validates against metatype maximum rules
  - invalid edits bounce back to the last valid value
  - valid edits mutate the character model
  - successful commit raises the equivalent dirty/update event
- When increasing one bucket at the cap boundary, the other bucket must shift the same way legacy `BeforeValueIncrement` does.
- The "only limited number of physical/mental attributes at natural max during creation" rule must still be enforced.

## Explicitly Forbidden Successor Shortcuts

The following are parity failures and must not be used to close this work:

- leaving `dialog.new_character.priority_workflow` on the generic dialog fallback path
- static `A/B/C/D/E` selectors with no dependent rebuild logic
- a read-only workflow snippet instead of a dedicated priority pane
- `SectionReviewExpander` as the primary attributes surface
- facts cards as the only visible attribute affordance
- text summary of attributes instead of numeric editors
- plain `TextBox` inputs where legacy used `NumericUpDown`
- "covered" parity gates that only prove route IDs and labels

## Required File Touch Targets

At minimum, expect this work to require changes in these successor files:

- `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- `Chummer.Avalonia/DesktopDialogWindow.axaml.cs`
- `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- `Chummer.Avalonia/Controls/SectionHostControl.axaml.cs`
- `Chummer.Presentation/Overview/DialogCoordinator.cs`
- `Chummer.Presentation/Overview/StarterWorkspaceXmlFactory.cs`
- new or expanded parity-specific view/control files for:
  - priority-build pane
  - editable attribute-row control
  - creation-time attribute-panel host

If a shared presenter/state seam is missing for this behavior, add it. Do not bury the logic inside ad hoc UI-only local state.

## Acceptance Criteria

This work is not complete until all of the following are true.

### Behavioral acceptance

- Changing a priority letter fires the dependent update path immediately.
- Priority mode duplicate letters auto-reconcile exactly as legacy.
- Sum-to-Ten mode total updates live exactly as legacy.
- Changing category/metatype/metavariant updates the inspect lane and talent options.
- Choosing a magical/resonance talent exposes the same skill-choice continuation controls as legacy.
- Entering the attributes surface exposes real numeric editors and allows direct editing.
- Attribute edits change the actual model and update the live display.

### Widget-class acceptance

- Priority selectors are real combo boxes.
- Metatype browse lane is a real listbox plus real combo box.
- Talent skill-choice controls are real combo boxes.
- Attribute editors are real numeric up/down controls.
- No summary-only substitute remains on the parity path.

### Geography acceptance

- Browse-left / inspect-right / action-strip-bottom ownership matches legacy.
- Attribute header and rows keep the same dense column rhythm as legacy.

### Mouse-route acceptance

- single click, double click, checkbox toggle, and spinner interactions match the legacy route expectations
- `lstMetatypes` double click commits OK
- mouse-wheel increment/decrement on numeric controls remains supported if legacy supported it on the platform

## Required Tests And Proof

Add fail-closing proof that exercises behavior, not just inventory.

### Runtime/UI tests

Add route-specific automated tests that prove:

- priority-letter change causes dependent state change
- duplicate letter reconciliation occurs in Priority mode
- Sum-to-Ten live total changes on every relevant selection
- metatype/talent changes repopulate dependent controls
- attribute rows render numeric up/down editors
- editing attribute base/karma mutates the model and updates the displayed value

### Screenshot and control-inventory proof

Add parity receipts that compare the promoted surface to the legacy route for:

- widget class
- visible label text
- control order
- pane ownership
- presence of numeric up/down editors
- absence of summary-only fallback on the parity path

### Mouse-only replay proof

At minimum, add replay or equivalent deterministic route proof for:

1. open priority build dialog
2. change one priority letter to a duplicate and verify auto-reconciliation
3. switch metatype and verify dependent inspect/talent surfaces update
4. choose a talent that grants skill choices and verify the drop-downs appear
5. enter attributes surface and increment/decrement at least two attributes with numeric editors

## Required Gate Fixes

The parity program must be hardened so this exact regression cannot pass again.

### Generated dialog gate

Extend `scripts/ai/milestones/generated-dialog-element-parity-check.sh` so it fails when:

- the priority workflow stays on the generic renderer
- the priority workflow lacks a dedicated parity pane
- the priority workflow exposes snippet-only or text-only continuation UI

### Section-host gate

Extend `scripts/ai/milestones/section-host-ruleset-parity-check.sh` so it fails when:

- `tab-info.attributes` lacks editable controls for creation-time builds
- the only attributes surface is a summary/review/expander pane
- numeric up/down editor presence is missing from the interactive inventory

### Receipt semantics

Any parity receipt for this route must explicitly record:

- `visual_parity`
- `behavioral_parity`
- `widget_class_parity`
- `event_parity`
- `editable_attribute_surface`

Any blank, inferred, or partial value must fail closeout.

## Implementation Order

Do the work in this order.

1. Carve the priority workflow off the generic dialog fallback path.
2. Implement the dedicated priority-build pane with the full dependent event graph.
3. Implement the editable attribute panel and row control with true numeric editors.
4. Bind the pane to real shared state instead of placeholder summary state.
5. Add runtime behavior tests.
6. Add screenshot/control-class proof.
7. Tighten the parity gates so this route cannot claim completion early again.

## Final bar

This route is only parity-complete when a Chummer5a veteran can:

- change a priority selection and see the same dependent updates
- choose metatype/talent/skill continuations through the same control classes
- land on an attributes surface with numeric up/down editors
- finish the route without having to interpret helper summaries or review chrome

Anything less is still partial parity and must not be published as complete.
