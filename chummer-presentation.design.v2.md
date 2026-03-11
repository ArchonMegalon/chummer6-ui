
# chummer-presentation.design.v2.md

Version: v2.0  
Status: authoritative design for Codex Instance B

## 1. Mission

`chummer-presentation` is the **desktop, browser, and mobile rendering repo** for Chummer.

It owns:

- Avalonia desktop workbench
- Blazor / WASM workbench surfaces
- shared UI-kit surfaces consumed by the dedicated `chummer-play` shell
- design system and design tokens
- localization rendering
- Explain trace visualization
- local-first UI state
- browse and Build Lab UX
- GM Board / Spider Feed rendering
- runtime inspector and Hub client UX

It does **not** own:

- rules implementation
- XML parsing
- RuntimeLock compilation
- Hub server workflows
- AI provider secrets
- media generation job execution

This repo is where **truth becomes understandable and usable**.

---

## 2. Product responsibilities

## 2.1 Base Chummer features owned here

1. **Explain Everywhere UI**
   - localized tooltips and drawers
   - trace-step drill-down
   - before/after comparison
   - “why unavailable?” rendering

2. **Build Lab UI**
   - concept input
   - variant compare
   - progression timeline
   - trap warning display
   - role overlap display

3. **Browse Workspaces**
   - item/spell/quality/ware selectors
   - facet filters
   - disable reasons
   - saved views
   - virtualized lists

4. **Contact & relationship graph UI**
   - contacts, factions, obligations, favors, heat
   - timeline and note rendering

5. **Calendar / ledger / downtime surfaces**
   - events, training, healing, addiction tests, expenses
   - visual schedules and logs

6. **Runtime Inspector**
   - active RuleProfile / RulePacks
   - migration preview
   - runtime diff
   - capability/provider diagnostics

7. **Dossier / handout viewers**
   - preview generated artifacts
   - attach, approve, export, archive

## 2.2 Companion-facing features rendered here

The Presentation repo also owns the shared user-facing UX building blocks for:

- Chummer Coach
- Johnson's Briefcase asset previews
- Spider Feed
- NPC Persona Studio screens
- Session Memory Engine views
- Shadowfeed
- Sixth World News cards/videos
- Route Cinema viewers
- Portrait Forge selection/re-roll UI

The actual heavy generation stays in `chummer.run-services`.

---

## 3. Architecture principles

1. **No hardcoded Shadowrun rules**
   - if you need math, ask the engine

2. **Localization happens here**
   - engine emits keys and parameters
   - presentation resolves final text using active language packs

3. **Offline-first for session**
   - local event log first
   - sync later
   - never overwrite canonical state with absolute values

4. **Virtualization is mandatory**
   - huge lists must be virtualized
   - no “render 4000 rows and pray” UI

5. **Browser constraints are first-class**
   - IndexedDB / OPFS, not raw file paths
   - async-heavy workloads
   - AOT build and cross-origin isolation requirements honored

6. **AI output is always visibly a draft unless approved**
   - no silent mutation in UI

---

## 4. Browser, WASM, and native constraints

## 4.1 Storage

In browser heads, local persistence must use browser-safe abstractions only:

- IndexedDB
- OPFS when needed
- browser file picker abstractions
- Avalonia `StorageProvider` in browser-safe mode

Do not use direct `System.IO` path assumptions in browser code.

## 4.2 Multithreading and cross-origin isolation

Threaded/shared-memory browser heads require:

- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Embedder-Policy: require-corp`
- explicit fallback behavior if `crossOriginIsolated` is false

The UI must expose a degraded-mode banner when isolation requirements are missing.

## 4.3 AOT and performance

Production WASM builds must use WebAssembly AOT unless there is an explicit waiver.

Large dataset screens must use virtualization:
- Avalonia `TreeDataGrid` or equivalent
- Blazor `<Virtualize>` or equivalent
- virtualized item cards for session lists and NPC vaults

## 4.4 Deep-link routing and static assets

Nginx / edge hosting must provide SPA fallback routing and serve `.wasm` with the correct MIME type.

This repo must ship deployment checks for:
- deep-link refresh success
- `.wasm` MIME correctness
- isolation header presence
- service-worker cache behavior

---

## 5. Concrete feature design

## 5.1 Explain Everywhere UI

Required UI affordances:

- inline value chip
- hover/click inspector
- localized trace lines
- provider origin badges
- pack badges
- “what changed?” diff panel

The UI must accept localization keys plus parameters and render them through pack language resources.

## 5.2 Build Lab UI

Required screens:

- concept intake
- compare 3–5 variant builds
- 25 / 50 / 100 Karma timeline
- “best for your table style” chips
- role overlap warning chips
- export to BuildIdeaCard / Character Template

## 5.3 Browse Workspaces

Each browse workspace must support:

- search
- facets
- source filters
- pack filters
- disabled reasons
- “best for current build”
- saved searches
- virtualization
- keyboard navigation
- controller-friendly tap targets on mobile where relevant

## 5.4 Contact and campaign continuity screens

UI screens:
- contact card
- relationship graph
- heat tracker
- faction status
- ledger
- calendar/timeline
- unresolved favors
- downtime planner

These must work without AI, but can accept AI-generated drafts from Run Services.

## 5.5 GM Board / Spider Feed

The GM Board must not be a chat log. It is a tactical control surface.

Use:
- ephemeral cards
- color-coded severity
- big action buttons
- expiry and stale-state banners
- pin / dismiss / snooze
- interruption budget slider
- panic button: “Mute Spider 15 min”

Autonomy levels:
- Off
- Low
- Tactical
- Narrative
- High

UI behavior changes with autonomy level.

## 5.6 Session app

The dedicated `chummer-play` repo owns the shipped session/mobile shell.
This repo only owns the reusable rendering contracts and shared UI-kit primitives that shell consumes.

The session app must remain narrow:
- dashboard
- trackers
- quick actions
- notes
- pins
- messages
- portraits / NPC cards
- runtime bundle status

Local state uses event logs, not absolute tracker snapshots.

Portraits and other heavy media must be cached aggressively client-side to avoid repeat downloads.

## 5.6.1 Post-split ownership map

After the `chummer-play` split, Presentation ownership for session/coach flows is limited to shared seams:

- `ISessionClient`, launch/deep-link contracts, and session-ready DTO rendering
- shared UI-kit primitives consumed by `chummer-play`
- workbench-side Coach sidecars embedded in the heads this repo still ships
- portal/proxy expectations for external `/session` and `/coach` hosts

Presentation does not own:

- the shipped `/session` host
- the shipped `/coach` host
- dedicated play/mobile deployment packaging

## 5.7 Portrait Forge and generated assets

The UI does not generate images/videos, but it must provide:

- prompt seed preview
- style selection
- reroll history
- canonical asset selection
- side-by-side compare
- “mark as canonical”
- “attach to NPC/contact/character”

## 5.8 Johnson's Briefcase and dossier UX

Required UI:
- artifact list
- artifact preview
- approve / reject / archive
- export / share
- attach to scene, NPC, campaign, or character
- print-friendly rendering

## 5.9 Shadowfeed and News Network

UI surfaces:
- feed viewer
- campaign-only filter
- approved canon filter
- “send to players”
- “keep GM-only”
- recap card / video card viewer

---

## 6. Contracts consumed

From engine:
- explain DTOs using localization keys
- RuntimeLock DTOs
- Build Lab DTOs
- semantic seeds for media features
- session projection DTOs

From run-services:
- Hub clients
- Coach chat DTOs
- Spider Feed cards
- asset job status DTOs
- session relay clients
- publication/review DTOs

No repo-specific secret handling in client code.

---

## 7. LTD integrations relevant to this repo

Direct client-side use of LTD APIs is forbidden.

But the UI must be designed to consume outputs from:
- 1min.AI / AI Magicx via Run Services
- Prompting Systems prompt packs via Run Services
- MarkupGo / PeekShot generated assets
- Mootion videos
- AvoMap videos
- Documentation.AI docs/help
- ApproveThis review state
- MetaSurvey feedback prompts
- Teable-backed review dashboards if embedded/admin-only

---

## 8. Accessibility and quality requirements

Mandatory:
- dark mode
- font scaling
- reduced-motion mode
- keyboard-first navigation on desktop/web
- touch-first affordances in session/mobile
- large target sizes for live-table use
- screen-reader-friendly action labels where practical

Generated assets must expose:
- source metadata
- created time
- approved state
- stale state if relevant

---

## 9. Forbidden shortcuts

Never:
- implement rules in UI code
- parse legacy XML directly
- call vendor AI APIs from the client
- use browser-incompatible file APIs
- render massive catalogs without virtualization
- overwrite session state with absolute values

---

## 10. First milestones for this repo

### Milestone B1 — Explain Everywhere UI
Deliver:
- localized explain panels
- provider and pack badges
Exit:
- no user-facing explain text is baked in English

### Milestone B2 — Browse + virtualization
Deliver:
- virtualized selectors and saved filters
Exit:
- 5000-item catalog loads without UI collapse

### Milestone B3 — Build Lab UI
Deliver:
- concept, variant compare, progression timeline
Exit:
- Build Lab works with engine DTOs only

### Milestone B4 — GM Board + Spider Feed
Deliver:
- tactical card feed
- interruption budget slider
- mute/pin/snooze
Exit:
- no chat-log design remains

### Milestone B5 — Session local-first shell
Deliver:
- event-log-based session state
- runtime bundle status
- portrait cache
Exit:
- offline updates sync without absolute-value overwrite

### Milestone B6 — Artifact viewer suite
Deliver:
- portrait compare
- dossier preview
- recap/news cards
- route video viewer
Exit:
- generated assets can be reviewed and attached without custom one-off UI

---

## 11. What Codex Instance B should do first

1. remove all hardcoded explain prose assumptions
2. build the localization-key explain renderer
3. implement browse virtualization everywhere
4. add the GM Board / Spider Feed card system
5. add session event-log local store and cache
6. build generic generated-asset preview and approval components
