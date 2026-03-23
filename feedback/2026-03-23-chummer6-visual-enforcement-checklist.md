# Implementation Checklist: Harden Chummer6 Visual Enforcement Before Any Full Rerun

Decision:
- `no-go` on a full Chummer6 guide and art rerun right now
- `go` on a small code pass that hardens visual enforcement first
- after that, do a cheap targeted rerun of only:
  - `assets/hero/chummer6-hero.png`
  - `assets/pages/horizons-index.png`
  - `assets/horizons/karma-forge.png`

Reason:
- the text pipeline is now materially healthier
- the remaining weakness is concentrated in visual enforcement, not broad guide generation
- design canon improved faster than worker enforcement did

## Keep As-Is

Do not reopen these unless you are fixing a concrete bug:
- `executive-assistant/scripts/bootstrap_chummer6_guide_skill.py`
  - keep the registry-first guide inputs
- `executive-assistant/SKILLS.md`
  - keep the registry-first public-guide lane description in sync with runtime
- `Chummer6/README.md`
  - keep the current top-of-page structure
- `Chummer6/DOWNLOAD.md` and release output posture
  - keep archive-only manual-preview honesty
- participation CTA routing
  - keep the public `/participate` entry

## Likely Edit Points

Primary implementation files:
- `executive-assistant/scripts/chummer6_guide_worker.py`
- `executive-assistant/scripts/chummer6_guide_canon.py`
- `executive-assistant/scripts/chummer6_release_builder.py`

Design truth to read and normalize:
- `chummer6-design/products/chummer/PUBLIC_MEDIA_BRIEFS.yaml`
- `chummer6-design/products/chummer/PUBLIC_GUIDE_PAGE_REGISTRY.yaml`

Downstream outputs to refresh only after enforcement lands:
- `Chummer6/assets/hero/chummer6-hero.png`
- `Chummer6/assets/pages/horizons-index.png`
- `Chummer6/assets/horizons/karma-forge.png`

## Execution Order

### 1. Thread design visual contract into runtime

Implement a real `load_visual_contract()` seam in `executive-assistant/scripts/chummer6_guide_worker.py` or `executive-assistant/scripts/chummer6_guide_canon.py`.

It should read and normalize at least:
- media-brief asset-class rules from `PUBLIC_MEDIA_BRIEFS.yaml`
- page-type `visual_density_profile` and layout rules from `PUBLIC_GUIDE_PAGE_REGISTRY.yaml`
- stricter first-contact rules for hero, page index, and flagship horizons

Thread the normalized contract through:
- scene-plan generation
- visual-director prompt construction
- scene audit
- visual audit
- whole-pack audit

Done when:
- worker logs or debug artifacts show the loaded visual contract for each first-contact asset
- those values are used as runtime input rather than only design-side prose

### 2. Add hard-fail checks for the three critical assets

Add explicit fail conditions in `executive-assistant/scripts/chummer6_guide_worker.py`.

For `Chummer6/assets/hero/chummer6-hero.png`:
- fail if the frame is effectively one person plus vague wall, rack, or darkness
- fail if visible reasoning or proof cues are absent
- fail if prop density is below the first-contact threshold
- fail if the scene reads as quiet brooding instead of inspectable trust pressure

For `Chummer6/assets/pages/horizons-index.png`:
- fail if the scene is mostly empty roadway ambience
- fail if branching plurality is not obvious
- fail if only one central clue or symbol carries the concept
- fail if it reads as atmosphere instead of multiple future lanes

For `Chummer6/assets/horizons/karma-forge.png`:
- fail if it reads as generic operator or card tinkering
- fail if approval, provenance, or rollback cues are missing
- fail if overlay density is below the flagship-horizon threshold
- fail if it cannot be distinguished from a generic cyberpunk desktop moment

### 3. Remove sparse-humor and sparse-easter-egg allowances from flagship assets

In `executive-assistant/scripts/chummer6_guide_worker.py`:
- remove permissive sparse-humor treatment from `karma-forge`
- treat `karma-forge` as a strict flagship horizon asset

Desired policy:
- `humor_allowed = false`
- `semantic_anchor_required = true`
- `overlay_required = true`
- `flagship_horizon_strict = true`

Sparse humor should remain available only for explicitly secondary assets.

### 4. Expand banned and penalized composition families

The worker currently overfocuses on table-relapse prevention. Add stronger bans or penalties for first-contact and flagship assets:
- `brooding_profile`
- `vague_prop_wall`
- `low_density_solo_operator`
- `empty_roadway_ambience`
- `single_symbol_corridor`
- `generic_console_tinkering`
- `quiet_clueboard`
- `dark_negative_space_dominant`

Apply those rules most aggressively to:
- hero
- page index assets
- flagship horizon assets

Secondary part-page art can remain more permissive.

### 5. Add overlay compositor or overlay verifier

Implement one of:
- a lightweight deterministic post-render overlay compositor
- a stricter render-plan schema that requires overlay elements as first-class outputs

Minimum overlay intent by asset:

Hero:
- subtle provenance traces
- build-state or receipt cues
- stat-delta or reasoning hints

Horizons index:
- lane or branch differentiation by shape, color, or path logic
- avoid text-heavy signboards

Karma Forge:
- diff strips
- approval seals
- rollback arrows
- provenance glyphs
- consequence markers

The target is instrumented art, not just mood art.

### 6. Make "packed and flashy" measurable

Add measurable render-plan and audit fields in `executive-assistant/scripts/chummer6_guide_worker.py`:
- `density_score_target`
- `negative_space_cap`
- `overlay_density_target`
- `flash_level_target`
- `semantic_anchor_count_min`
- `foreground_prop_count_min`
- `distinct_clue_count_min`

Score against those in:
- scene audit
- visual audit
- pack audit where relevant

Do not leave visual density as a taste-only instruction.

### 7. Lock README layout to canon, not prompt drift

Use `PUBLIC_GUIDE_PAGE_REGISTRY.yaml` as a deterministic layout source for `Chummer6/README.md`.

Preserve this top order:
- hero
- short pitch
- quick nav
- current posture or honesty block
- one update teaser
- participation entry
- try-it-now entry
- deeper map

Guardrails:
- `nav_before_updates = true`
- `max_front_page_updates` enforced
- `section_order` treated as a hard layout rule

This work should be minimal. The README structure is already close to correct.

### 8. Preserve the two surfaces that are already right

Do not regress:
- participation CTA to the public `/participate` entry
- archive-only manual-preview labeling in the download shelf

If packaging improves later, update the release story then. Do not muddy it now.

### 9. Split `chummer6_guide_worker.py`

The current worker is too large for safe iteration. Do a pragmatic split, not a grand rewrite.

Suggested modules:
- `guide_copy.py`
- `guide_layout.py`
- `visual_contracts.py`
- `scene_planner.py`
- `visual_audit.py`
- `pack_audit.py`
- `publish_assets.py`

Minimum goal:
- isolate flagship-asset policy changes into small, local edits
- stop mixing canon loading, copy generation, visual planning, auditing, and publishing in one file

### 10. Add golden-image regression tests before any rerun

Add cheap local gates for:
- hero
- horizons index
- `karma-forge`

For each, define:
- must-pass visual checks
- must-fail anti-patterns
- a few human-reviewed positive examples
- reject-on-low-density condition

If those three do not pass locally, do not spend money on a full rerun.

## Rerun Gate

Only rerun the full pack after all of the following are true:
- visual contract is loaded from design canon at runtime
- the three critical asset classes have hard-fail checks
- sparse flagship humor allowance is removed
- banned composition families include the current failure basin
- overlay enforcement exists
- density and flash are scored explicitly
- README layout is deterministic
- golden-image checks pass locally for hero, horizons index, and `karma-forge`

## First Rerun Scope

After the code pass, rerun only:
- `Chummer6/assets/hero/chummer6-hero.png`
- `Chummer6/assets/pages/horizons-index.png`
- `Chummer6/assets/horizons/karma-forge.png`

Review those three manually.

Only after those three pass should you launch a full guide and art rerun.

## Deliverable Definition

The code pass is complete when:
- the worker consumes design visual contract data directly
- the three flagship assets fail fast on the current quiet and generic failure modes
- README layout remains stable
- participation and download posture do not regress
- a targeted three-asset rerun produces visibly denser, more product-specific, more instrumented art
