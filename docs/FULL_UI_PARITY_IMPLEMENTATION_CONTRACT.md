# Absolute UI Parity Implementation Contract

This is the generic fail-closing, release-blocking parity contract for Chummer6 successor UI work.

This contract is ruleset-generic.
It is not an SR5-only contract.
It is not allowed to hold SR4, SR5, and SR6 to different parity dimensions on overlapping user-facing work.
The same rule extends to any future promoted ruleset lane.

The priority-build regression is only an example of the real rule:

- parity is not "the screen exists"
- parity is not "the route opens"
- parity is not "the labels roughly match"
- parity is not "the gate says covered"

Parity means the full legacy user-facing surface is implemented recursively:

- every UI element
- every element type
- every visible content string
- every event
- every state transition
- every dependent update
- every popup, tooltip, and flyout
- every editable affordance
- every commit, validation, and recovery path

Anything less is partial parity.
Partial parity is failure.
Failure must not close the route.
Failure must not pass the gate.
Failure must not ship.

## Core Rule

If the declared baseline already had the same user-facing function, Chummer6 must preserve the same mouse-driven result with the least possible relearning.

That rule applies to the entire route, not just the top-level dialog or tab.

There is no "close enough" allowance here.
There is no summary-only substitute allowance here.
There is no inventory-only allowance here.
There is no screenshot-only allowance here.

If the user-facing route is not fully implemented, the parity claim is false.

Zero missing event handlers is a hard rule.
After a parity pass, it must not be possible to discover that a legacy control exists on screen but its required event path was never wired.

## Baseline Rule

Every parity claim must declare its canonical baseline per promoted ruleset lane and per route family.

That baseline must be named directly in the route-local parity inventory and in the gate receipt.
It must not be implied.
It must not be guessed from the code.
It must not silently default to SR5.

Examples of valid baseline posture:

- SR4 overlapping legacy desktop surfaces: Chummer4-backed oracle
- SR5 overlapping legacy desktop surfaces: Chummer5A-backed oracle
- SR6 shared desktop surfaces with no separate legacy client: promoted shared desktop baseline or named SR6-native oracle where one exists

The same parity dimensions apply regardless of which baseline is declared:

- recursive element inventory
- widget class
- content
- option sets
- events
- dependent updates
- editable affordances
- validation and commit
- runtime materialization
- keyboard and pointer routes
- command surfaces
- branch matrix
- lifecycle behavior

SR4 is not allowed a weaker event standard than SR5.
SR6 is not allowed a weaker widget-class standard than SR5.
SR5 is not allowed to hide behind a generic shared shell when SR4 or SR6 still expose a different branch truth.

If a route is shared across SR4, SR5, and SR6, the same shared surface must be audited on each promoted lane against its declared baseline.
If a route is ruleset-specific, parity must be measured against that ruleset's declared baseline, not against a different edition just because it is convenient.
If a future promoted ruleset lane exposes the same shared route, it inherits the same burden automatically.

## Scope

This contract applies to every parity route:

- menu routes
- toolbar routes
- dialogs
- utility forms
- tabs
- panels
- grids
- lists
- trees
- popups
- context menus
- tooltips
- detail drawers
- edit panes
- selection workflows
- continuation workflows
- validation workflows
- create/edit/delete flows

No route is allowed to escape parity by omission, summary replacement, approximate rendering, or generic fallback rendering.
No route is allowed to escape parity because the missing controls were "created at runtime in the baseline client."
Runtime-created baseline controls are first-class parity scope.
Parity is branch-complete, not hero-path-complete.

That means every legacy mode, ruleset, feature flag, build method, optional-rule branch, and data-conditioned branch that materially changes the route is in scope.
One working branch does not close parity for the route.

Unsupported, preview, or intentionally absent routes are still in scope for honesty parity.
If a promoted ruleset lane does not support a route, the surface must say so explicitly and consistently.
Blank panes, fake parity shells, and silent fall-through are forbidden substitutes for unsupported-state honesty.

## What must match

### 1. Recursive element inventory

For every legacy surface, parity work must inventory the full UI tree recursively:

- root window or dialog
- all containers
- all child controls
- all dynamic children
- all controls created at runtime by legacy code
- all controls created from runtime loops, data binding, or conditional branches
- all controls inserted after selection changes, collection changes, or lifecycle events
- all hidden-but-activatable controls
- all popup menus
- all flyouts
- all tooltips that carry functional information
- all secondary inspectors
- all action strips
- all validation and status regions

The implementation target is not a screenshot.
The implementation target is not a story about the route.
The implementation target is not a field list.
The implementation target is the complete interactive control tree and its full behavior graph.

Designer-file parity is not enough.
Static markup parity is not enough.
If the declared baseline materialized controls at runtime, those runtime-created controls are part of the canonical surface and must be implemented, wired, and tested in Chummer6.

### 1A. Ruleset-lane and baseline-oracle parity

For every promoted route, parity work must record the baseline and support posture for each promoted ruleset lane separately.

This includes:

- which ruleset lanes expose the route
- which baseline oracle governs each exposed lane
- which parts of the surface are shared across lanes
- which parts are ruleset-specific
- which parts are intentionally unsupported, preview-only, or hidden
- which ruleset warnings, badges, disabled reasons, and unsupported-capability messages are required

The contract is not generic enough unless it covers SR4, SR5, and SR6 the same way:

- same fail-closing behavior
- same recursive inventory burden
- same event burden
- same runtime-control burden
- same proof burden

What may differ by ruleset is the declared baseline and the honest supported surface.
What may not differ by ruleset is the strictness of the parity audit.

### 2. Element type parity

Each legacy element must preserve its control class unless there is an explicit approved exception.

Examples:

- `ComboBox` must not silently degrade to `TextBox`
- `ListBox` must not silently degrade to summary chips
- `TreeView` must not silently degrade to flat prose
- `NumericUpDown` must not silently degrade to text entry
- `CheckBox` must not silently degrade to descriptive text
- editable table/grid cells must not silently degrade to read-only rows
- context menus must not silently degrade to helper text

If the legacy route had an actual interactive control, the successor route must expose an equivalent interactive control.
Replacing an interactive control with explanation text, summary text, or preview text is a hard parity failure.

### 2A. Grid, table, list, and tree structure parity

Structured collection surfaces must preserve their legacy shape, not just their container shell.

This includes:

- generated grid columns
- column headers
- column order
- column visibility
- column resize posture where legacy exposed it
- sortable vs non-sortable columns
- row-action columns
- inline cell editor classes
- list item templates where the template carries workflow meaning
- tree node hierarchy
- tree expanders
- per-row and per-node checkboxes, icons, and action affordances

If legacy used a grid, table, list template, or tree structure to carry workflow meaning, the successor route must preserve that structure recursively.
Implementing the outer container while simplifying the generated columns, cells, rows, or nodes is still parity failure.

### 3. Content parity

The content of each UI element is part of parity.

This includes:

- label text
- button captions
- tab names
- menu labels
- grid headers
- group titles
- placeholder text
- watermark text
- tooltip text
- context-menu text
- helper text
- error text
- empty-state text
- summary text
- status text
- default values
- selected values
- displayed totals
- displayed limits
- displayed sources
- displayed counts
- displayed abbreviations
- displayed units
- displayed punctuation and separators where those shape recognition
- warning text
- confirmation text
- instructional text
- visible icon labels or icon-backed meaning where the icon is functional

New synthetic helper copy is forbidden when the legacy route already named the same function.
Content drift is not cosmetic drift. It is workflow drift.

### 3A. Option-set parity

The contents of interactive option sets are part of parity.

This includes:

- exact available options
- option ordering
- option grouping
- blank or sentinel entries
- translated display labels
- backing values
- enabled vs disabled options
- one-item disabled posture
- selected-index behavior
- behavior when a previously selected option becomes invalid

If legacy offered a specific option set under a specific state, the successor route must offer the same option set under the same state.
Not just "a similar list." The actual list.

### 3B. Visual-semantics parity

Functional visual semantics are part of parity.

This includes:

- icon meaning where the icon is not decorative
- warning/error/success emphasis
- bold/italic/all-caps posture where legacy used it to signal state
- badges, markers, and attention chrome that carry workflow meaning
- truncation vs wrapping posture where that affects recognition
- dense vs expanded row posture where density carried workflow value
- inline-vs-secondary help posture where users relied on visual proximity

If the legacy route used visual emphasis to signal a condition, the successor route must preserve that signal.
Parity is not satisfied by keeping the same words while dropping the visual meaning.

### 4. Event parity

Every legacy event that changes user-visible behavior must be implemented.

This includes, at minimum:

- `SelectionChanged`
- `SelectedIndexChanged`
- `TextChanged`
- `ValueChanged`
- `CheckedChanged`
- `Click`
- `DoubleClick`
- right-click / context-menu open
- middle-click if legacy used it
- mouse wheel
- focus gained
- focus lost
- open/close expand/collapse
- row selection
- tab selection
- menu-open side effects
- drag/drop if legacy used it

If an event in legacy caused dependent controls, values, visibility, enablement, validation, or commit state to change, the successor route must do the same.
Missing event behavior is not a minor bug. Missing event behavior means the route is not implemented.

This is a zero-tolerance requirement:

- no missing `SelectionChanged`
- no missing `SelectedIndexChanged`
- no missing `ValueChanged`
- no missing `CheckedChanged`
- no missing click or double-click routes
- no missing dependent rebuild logic
- no dead controls that look real but do nothing
- no controls whose handler exists for one branch but not for another branch
- no handler that updates summary text while failing to update the actual dependent controls

If a control is present and interactive in legacy, the successor route must prove that the full required handler chain exists and produces the full required side effects.
This applies equally to controls declared in designers and to controls instantiated at runtime.
Runtime-created controls must not become dead controls, partially wired controls, or untracked controls.

### 4A. Keyboard and command-route parity

Absolutely full parity also includes keyboard and command posture wherever legacy exposed it.

This includes:

- tab order
- focus order
- default button behavior
- Enter-to-confirm behavior
- Escape-to-cancel behavior
- arrow-key selection behavior
- keyboard shortcuts
- mnemonics / accelerator keys
- delete key behavior
- spacebar toggle behavior

Mouse-first parity is not an excuse to regress keyboard routes that legacy users already relied on.

### 4B. Command-surface parity

If legacy exposed the same action through multiple command surfaces, the successor route must preserve that surface contract.

This includes:

- menu-item invocation
- toolbar-button invocation
- context-menu invocation
- hyperlink-style invocation
- double-click invocation
- default-button invocation
- keyboard-shortcut invocation
- cut/copy/paste behavior
- delete/remove behavior
- undo/redo behavior
- select-all behavior

The same command must preserve the same availability, enablement, confirmation, side effects, and dirty-state behavior across each exposed surface.
It is a parity failure if one surface works and another surface that existed in legacy is missing, disabled incorrectly, or wired to a weaker side effect chain.

### 5. State parity

Each element must preserve its legacy state contract.

This includes:

- visible vs hidden
- enabled vs disabled
- editable vs read-only
- selected vs unselected
- expanded vs collapsed
- default value
- minimum value
- maximum value
- increment step
- placeholder or empty selection posture
- valid vs invalid state
- primary vs secondary action posture
- focus order
- selected-row retention
- scroll position where legacy preserved it across safe refreshes
- warning/highlight/attention posture where the visual state carries meaning

### 5A. Ordering and geography parity

Parity includes exact ordering and spatial ownership, not just element presence.

This includes:

- left-to-right order
- top-to-bottom order
- parent container ownership
- row/column placement
- browse-lane vs inspect-lane ownership
- action-strip ordering
- inline-vs-secondary placement
- scroll-region ownership
- whether details appear inline, in a side pane, in a popup, or in a separate dialog

If the same job moved to a different place in the workflow, parity is broken even if all the controls technically exist somewhere.

### 5B. Selection-model, search, and filter parity

Parity includes how collections are searched, filtered, and selected.

This includes:

- single-select vs multi-select behavior
- selected-item vs selected-index semantics
- selection-anchor behavior where legacy used range selection
- type-ahead / incremental keyboard search
- live filter behavior
- explicit filter controls
- sort order and sort toggles
- select-all / clear-selection behavior
- what happens to selection after refresh or filter change

If legacy let the user find or retain an item through a specific selection or filter posture, the successor route must preserve that posture.
Finding the same item through a slower or different interaction is not full parity.

### 5C. Window, dialog, and container-posture parity

Parity includes the hosting posture of the surface itself wherever legacy behavior made it workflow-visible.

This includes:

- modal vs modeless behavior
- owner/child dialog relationship
- initial focused control
- initial active tab, page, or pane
- fixed-size vs resizable posture
- minimum and maximum size where legacy enforced it
- splitter presence and default splitter ratio
- docked vs floating posture
- default expanded or collapsed container state
- reopen posture where legacy restored it

If legacy required a route to block, float, focus, or size in a specific way for the workflow to make sense, the successor route must preserve that posture.

### 5D. Input-semantics and formatting parity

Parity includes how editable controls accept, reject, normalize, and display user input.

This includes:

- input masks
- allowed characters
- trimming behavior
- casing normalization
- numeric parsing rules
- date/time parsing rules
- decimal and separator handling
- blank-input handling
- null vs zero posture
- immediate formatting vs deferred formatting
- caret retention where legacy preserved it through safe edits
- text selection retention where legacy preserved it through safe edits

If legacy accepted, rejected, normalized, or reformatted input in a specific way, the successor route must do the same.
Visible parity with different parsing or normalization rules is still parity failure.

### 6. Dependent-update parity

Parity includes the full dependency graph.

If changing one control in the declared baseline caused any of the following, Chummer6 must do it too:

- repopulate another control
- change available options
- change selected option
- change visible details
- change totals
- change summary facts
- change validation state
- enable or disable commit
- show or hide continuation controls
- change source links
- change tooltips
- change limits
- change editable ranges
- clear invalid child state
- preserve still-valid child state
- rebuild runtime-created descendants
- add or remove collection-driven controls
- refresh default button enabled state

This must happen at the same point in the workflow, not only on final submit.
Any stale child selection or orphaned child state after a parent change is a parity failure.

### 6A. Mode, ruleset, and configuration-branch parity

Parity must hold across the full branch matrix that legacy exposed, not just a single happy path.

This includes:

- creation-mode vs career-mode behavior
- alternative build methods
- ruleset-conditioned branches
- optional-rule branches
- setting-gated branches
- data-conditioned branches
- one-item vs many-item branches
- zero-result and empty-collection branches

If a legacy route materially changed under a different mode or rules setting, the successor route must preserve that branch too.
Do not close parity by implementing one branch and silently degrading the others.

### 7. Editable-surface parity

A legacy editable route is not parity-complete if Chummer6 replaces it with:

- a snippet
- a summary
- a review expander
- a facts card
- a prose recap
- a debug payload
- a read-only preview

If the user could edit it in the declared baseline, the user must be able to edit it in Chummer6 with equivalent control classes and equivalent commit behavior.
Read-only imitation of an editable legacy route is a hard fail.

Parity also requires edit-density parity where density carried workflow value:

- compact multi-row editors must stay compact
- side-by-side editor/value/limit posture must stay side-by-side when that is how legacy users reasoned about the data
- do not force users through extra clicks, extra expanders, or extra detail drawers just to reach controls that used to be directly editable

### 8. Pointer-route parity

Parity includes how the user reaches a function:

- left click
- double click
- right click
- mouse wheel
- hover when hover reveals functional information
- click target geography
- action-strip ordering

If a popup menu or secondary action required right click in legacy, moving it behind a different gesture is a parity failure.
Changing the pointer route changes the workflow. Changing the workflow breaks parity.

### 9. Validation and commit parity

Parity includes:

- when validation runs
- how invalid input is rejected
- when values bounce back
- when commit is blocked
- when commit becomes enabled
- how dependent invalid states are repaired
- whether the route auto-corrects conflicting selections
- whether edits apply immediately, delayed, or on explicit confirm
- what confirmation prompts appear
- what happens on cancel
- what happens on close with dirty state
- what state is reverted and what state is preserved

Legacy hidden side effects are part of parity too.
If legacy commit performed cleanup, normalization, reallocation, stale-child removal, or dependent recalculation, the successor route must do the same.

### 10. Async and lifecycle parity

Parity also includes lifecycle behavior:

- initial default selections
- deferred population
- rebuild after field change
- refresh after selection change
- close behavior
- reopen behavior
- cancel behavior
- return-to-route behavior
- persistence of valid in-progress choices
- collection add/remove/replace behavior
- async cancellation behavior
- race safety between overlapping refreshes
- stale-update suppression
- behavior after reopening the same route
- behavior after changing state in an upstream route and returning

If the route only works in a happy-path demo and falls apart during rebuild, refresh, reopen, cancel, or continuation, parity is still broken.
If out-of-order async work can overwrite the newer state and legacy did not behave that way, parity is broken.

### 10A. Cross-head exposure rule

If a route is exposed on multiple active heads, parity must hold on each exposed head.

- Avalonia cannot claim parity that Blazor does not have if the route is visible in both.
- Blazor cannot surface a reduced substitute route while Avalonia carries the real route and still call that parity.
- If one head cannot yet preserve the route honestly, the route must stay hidden or explicitly unsupported on that head until parity is real.

## Generic implementation instructions

For any parity route, implement the work in this order.

### 1. Capture the legacy route recursively

Produce a route-local parity inventory that records:

- the declared baseline oracle for each promoted ruleset lane
- the supported vs unsupported posture for each promoted ruleset lane
- every visible element
- every dynamic element
- every runtime-created legacy element
- each element type
- each generated column, row template, and tree-node template where applicable
- each content string
- each option set and option order
- each event hook
- each dependent side effect
- each state transition
- each popup/flyout/tooltip host
- each action-strip and control ordering rule
- each default/invalid/empty/one-item edge posture
- each mode/ruleset/configuration branch
- each search/filter/sort posture
- each modal/modeless and focus posture
- each command surface for each action
- each input mask, parse rule, and formatting rule

Do not stop at the first obvious bug.
Do not stop at the reported symptom.
Do not stop at the first visible pane.
Capture the full route.

### 2. Build the event graph

For every interactive element, record:

- triggering event
- affected target elements
- target state changes
- data mutation side effects
- validation side effects
- commit side effects

The implementation is incomplete until the whole event graph exists in the successor route.

### 3. Implement real successor controls

Do not use generic fallback renderers where they erase control class or behavior.

If the generic renderer cannot preserve parity, carve the route off the generic renderer and build a dedicated parity surface.
Generic fallback convenience is never a justification for parity loss.

### 4. Preserve recursive child behavior

Dynamic branches are part of parity.

If a selection in legacy revealed more controls, more details, or more edit affordances, the successor route must reveal those same branches and make them interactive.

### 5. Implement the real commit path

Do not stop at UI cosmetics.

The route is incomplete unless the real selected values drive:

- the actual model state
- the actual follow-on surface
- the actual validation result
- the actual persisted outcome

Placeholder writes, summary-only persistence, and fake continuation state do not count.

### 6. Add fail-closing proof

The route must gain automated proof for:

- declared baseline correctness per promoted ruleset lane
- supported vs unsupported posture honesty per promoted ruleset lane
- cross-ruleset shared-surface parity where the same route is exposed on multiple promoted ruleset lanes
- element presence
- element type
- generated column, cell-editor, and tree/list structure parity where legacy exposed them
- content
- option-set contents
- option ordering
- event behavior
- dependent updates
- editable affordances
- popup/tooltip/context-menu posture
- visual emphasis semantics where functional
- validation behavior
- commit behavior
- keyboard/default-button behavior where legacy exposed it
- command-surface parity where legacy exposed the same action from multiple affordances
- search/filter/selection-model behavior where legacy exposed it
- input parsing, masking, and formatting behavior where legacy exposed it
- mode/ruleset/configuration branches
- modal/modeless and focus posture where legacy exposed it
- async rebuild/race safety for dynamic routes

That proof must include runtime-created controls.
If a control only exists after the user changes state, the proof must drive the route into that state and verify the created control, its type, its content, and its handler behavior.

## Explicitly forbidden shortcuts

The following must fail parity closeout immediately:

- route exists but child controls are missing
- labels match but behavior does not
- read-only summary replaces editable UI
- generic dialog fallback replaces dedicated legacy workflow
- generic section preview replaces a real editor
- text box replaces numeric up/down
- flat prose replaces list/tree/grid
- grid or tree container exists but generated columns, row actions, node structure, or inline editors drift from legacy
- route IDs and action IDs match but widget classes do not
- screenshot resemblance without event parity
- partial implementation of only the reported symptom
- any route that is only "visually familiar" but not behaviorally equivalent
- any route that only proves top-level controls while child behavior is still missing
- any route whose dynamic branches are unimplemented
- any route whose editor is replaced by a review surface
- any route whose runtime-created legacy controls were ignored because they do not appear in a static designer file or top-level markup file
- any route whose SR4, SR5, and SR6 overlapping surfaces are audited to different parity dimensions
- any route that silently defaults its baseline to SR5
- any route that measures an SR4 surface against SR5 just because the SR4 oracle is harder to honor
- any route that measures an SR6 shared surface against nothing at all
- any route whose shared SR4/SR5/SR6 surface drifts by lane while separate receipts still claim closure
- any route that exposes an unsupported ruleset surface through blank panes, generic filler, or fake parity controls instead of explicit unsupported-state honesty
- any route whose option contents or option order drift from legacy
- any route that preserves controls but loses default-button, Enter/Escape, or keyboard posture that legacy exposed
- any route whose legacy command surfaces do not all invoke the same real action semantics
- any route that leaves stale child selections alive after parent-state changes
- any route that allows async stale updates to overwrite newer user choices
- any route that implements only one legacy mode/ruleset branch and silently degrades the others
- any route that preserves words but drops functional visual emphasis
- any route whose search/filter/selection posture differs from legacy
- any route whose modal/modeless, focus, or container posture differs from legacy where that posture shaped the workflow
- any route whose input masks, parsing, normalization, or formatting drift from legacy

## Required fail-closing gate contract

Every parity gate for a route must explicitly report:

- `baseline_oracle_parity`
- `ruleset_posture_honesty_parity`
- `cross_ruleset_shared_surface_parity`
- `visual_parity`
- `behavioral_parity`
- `widget_class_parity`
- `structured_collection_parity`
- `content_parity`
- `visual_semantics_parity`
- `option_set_parity`
- `ordering_parity`
- `event_parity`
- `command_surface_parity`
- `state_transition_parity`
- `selection_model_parity`
- `branch_matrix_parity`
- `window_posture_parity`
- `input_semantics_parity`
- `editable_surface_parity`
- `popup_route_parity`
- `keyboard_route_parity`
- `validation_parity`
- `commit_parity`
- `runtime_materialization_parity`
- `async_lifecycle_parity`

Any blank, inferred, partial, approximate, "covered", or "good enough" answer must fail.
If the receipt cannot prove it, the route is not closed.

## Required tests and proof

### Runtime route tests

Each parity route must have tests that exercise the route as a user would:

- load each promoted ruleset lane that exposes the route
- verify the declared baseline and supported/unsupported posture for that lane
- compare overlapping shared surfaces across promoted ruleset lanes where the route is shared
- open route
- change selections
- trigger dependent updates
- verify option contents and option order
- edit values
- hit invalid states
- verify repair or rejection behavior
- commit valid state
- verify resulting model and next surface
- reopen and re-enter the route after dependent state changes
- verify runtime-created controls appear, wire correctly, and disappear correctly
- verify search/filter/selection behavior where legacy exposed it
- verify every legacy command surface for the same action reaches the same side effects
- verify input masks, parsing, normalization, and formatting where legacy exposed them
- verify alternate ruleset/mode/configuration branches that materially change the route
- verify modal/modeless and default-focus posture where legacy exposed it
- verify newer user actions cannot be overwritten by stale async rebuilds

These tests must be strong enough to fail when any required event handler is absent, partially wired, or wired only to cosmetic updates.

### Control-inventory proof

The runtime receipt must record, for each visible element:

- stable id or path
- control class
- visible text
- tooltip text
- enabled/disabled state
- visible/hidden state
- read-only/editable state
- selection/value state
- child elements
- option list where applicable
- selected index where applicable
- runtime materialization path where applicable

This includes controls that only exist after runtime materialization.
If the receipt only records static top-level controls and misses runtime-created descendants, the receipt is invalid and the gate must fail.

### Mouse-route proof

At minimum, seeded replay or equivalent deterministic proof must cover:

- primary click path
- double-click path where legacy used it
- right-click path where legacy used it
- spinner/wheel interactions where legacy used them
- commit and cancel routes

The proof burden is on the implementation, not on the reviewer to guess that it probably works.

## Acceptance rule

A route is only parity-complete when a veteran of the declared baseline can perform the full route without having to reinterpret the workflow because:

- the same kinds of controls exist
- the same content exists
- the same option sets and ordering exist
- the same search, filter, and selection posture exists
- the same events fire
- the same command surfaces exist and invoke the same real actions
- the same overlapping shared surface does not drift by promoted ruleset lane
- the same dependent surfaces update
- the same editing affordances exist
- the same input parsing and formatting behavior exists
- the same commit and validation behavior exists
- the same keyboard/default-button posture exists where legacy exposed it
- the same mode/ruleset/configuration branches behave correctly
- the same per-ruleset support and unsupported-state posture is honest
- the same modal/modeless and focus posture exists where legacy exposed it
- the same dynamic runtime-created controls appear and behave correctly
- the same lifecycle and refresh behavior survives real use instead of only ideal demos

Parity is recursive.
Parity is behavioral.
Parity includes everything on the route.
Anything less is a release blocker.
