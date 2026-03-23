# Issue Packet: Chummer6 Needs an Explicit Visual-Density Contract

Target repos:
- `ArchonMegalon/executive-assistant`
- `ArchonMegalon/chummer6-design`
- `ArchonMegalon/Chummer6`

Target area:
- Chummer6 guide skill docs and bootstrap parity
- Design media canon for first-contact public assets
- Generated first-contact README and image outputs

Suggested labels:
- `chummer6`
- `public-guide`
- `art-direction`
- `media`
- `docs`

Suggested title:

`Chummer6 guide: align skill docs with runtime and make "packed and flashy" an enforceable first-contact contract`

## Paste-ready issue body

### Summary

The Chummer6 guide pipeline is materially healthier than it was before. Information architecture is better, the canon loader now pulls from the public registries and export manifest, and the generated README structure has improved.

The remaining problem is narrower and more concrete:
- the generated output is still too visually quiet and semantically generic
- the EA skill docs are now stale relative to the real runtime
- the front-page CTA and download posture still do not reflect the intended public funnel cleanly

This is no longer mainly an architecture-cleanup problem. It is now an art-direction and contract-encoding problem.

### What improved

- The canon loader now reads public-guide registries and export truth rather than relying on older broad repo-scope inputs.
- The bootstrap contract uses `public_page_registry`, `public_part_registry`, `public_faq_registry`, and `public_status`.
- The generated README is structurally better:
  - quick nav is near the top
  - the update block is restrained
  - front-page sprawl is reduced

Those changes should be kept.

### Problem

The output pack is cleaner, but it is still too restrained for a first-contact public repo:

1. First-contact images are still underpowered.
   They read as tasteful moody cyberpunk stills rather than information-rich, high-energy, unmistakably-Chummer art.

2. Key assets are still semantically thin.
   The hero, `horizons-index`, and `karma-forge` do not communicate enough specific product meaning in-frame.

3. The design canon still encodes "premium still / grounded scene" more strongly than "dense, layered, graphic, and exciting."

4. `SKILLS.md` is stale again.
   It still documents older broad inputs while the actual bootstrap has already moved to public registries.

5. The generated README still has two public-output misses:
   - participation CTA points to a deep Hub route instead of a softer public entry
   - the download shelf is still zip-first preview archives

### Why this matters

- The current pipeline now avoids obvious failures, but it still settles too easily for art that is competent and quiet rather than persuasive and memorable.
- The docs/runtime drift will mislead the next person who touches the Chummer6 skill stack.
- The front page is structurally improved but still not art-directed strongly enough to feel like a deliberate front door.
- If "packed and flashy" stays implicit, the model will keep converging on tasteful underload.

### Requested changes

#### 1. Fix EA skill docs/runtime drift first

Update `SKILLS.md` so the Chummer6 guide skills reflect the current bootstrap contract rather than the older `repo_readmes` / `design_scope` framing.

Bring these catalog entries into parity with the real runtime:
- `chummer6_public_writer`
- `chummer6_public_auditor`
- `chummer6_visual_director`
- `chummer6_scene_auditor`
- `chummer6_visual_auditor`
- `chummer6_pack_auditor`

#### 2. Add explicit visual-density fields to design canon

The current media brief is still too posture-oriented and not output-specific enough for first-contact assets.

Add first-class fields such as:
- `density_target`
- `semantic_density`
- `overlay_density`
- `negative_space_cap`
- `flash_level`
- `must_show_semantic_anchors`
- `foreground_midground_background_separation`

Do this per relevant asset class, especially hero, page-index, and horizon-first-contact assets.

#### 3. Raise the hero bar and expand reject classes

The hero improved from the old table relapse, but it still reads too much like `solo_operator_quiet` beside vague evidence.

Raise the bar so the hero must immediately communicate inspectable Shadowrun character or rules truth. Prefer directions like:
- streetdoc intake bay with visible stat and provenance overlays
- runner build-prep station with receipts, modifiers, and augmentation traces
- GM or player trust moment with visible why/how proof
- dossier or clinic ops bench with obvious build and rules context

Add hard reject classes for:
- `solo_operator_quiet`
- `brooding_profile`
- `generic_alley_still`
- `vague_clueboard`

Blocking only `safehouse_table` and `group_table` is too narrow now.

#### 4. Shift first-contact art toward graphic, cover-like composition

Keep the world grounded, but stop equating grounded with sparse and moody.

Strengthen direction toward:
- diagonal composition
- harder rim light
- stronger accent-color separation
- layered props
- bolder silhouettes
- sharper focal contrast
- diegetic HUD or smartlink elements
- less empty darkness
- more deliberate graphic framing

The target should be grounded but visually assertive.

#### 5. Add semantic-fit audit for horizon art

`Karma Forge` should not pass on style alone. It should have to show the actual horizon idea.

For horizon-specific assets, audit questions should include:
- does this communicate approval, rollback, provenance, or diff logic?
- does this read as governed rules evolution rather than generic card handling?
- are overlays doing real semantic work?
- would a new viewer infer controlled experimentation rather than vague forge mood?

#### 6. Add no-central-gibberish and no-empty-signboard rules

Page-level images should not pass when the core composition is basically a corridor or road plus one symbol, one sign, or one marker.

For page assets:
- reject central signboards unless they carry verified, meaningful post-composited information
- reject compositions whose main idea is "one lane plus one marker"
- require index pages to communicate plurality, branching, or possibility

For `horizons-index`, prefer a denser crossroads or future-lanes composition with multiple differentiated clues.

#### 7. Add a real overlay compositor step for selected assets

Overlays are still too often just a prompt hint. That is not enough for a product promising visible reasoning.

Selected assets should be able to receive a deterministic post-render overlay pass for elements such as:
- receipt trails
- provenance markers
- diff strips
- approval stamps
- rollback arrows
- smartlink brackets
- subtle numeric or state clusters

This should be used where overlays materially improve semantic clarity rather than as decoration.

#### 8. Tighten first-contact copy and asset metadata

Keep README-first assets free of maintainer jokes, dev-meta tone, and playful sparse-easter-egg treatment.

Hero and `karma-forge` should not remain in any permissive sparse-easter-egg bucket. First-contact assets need to be direct and legible first.

#### 9. Lock the improved README top structure into canon

The current front-page shape is better and should be preserved. Encode it rather than relying on worker convention.

Desired top order:
- hero
- one-sentence pitch
- quick nav
- one honesty block
- one update teaser
- deeper reading

Do not let front-page updates sprawl again.

#### 10. Fix participation CTA and download posture

Route the first-contact participation CTA to a softer public entry point instead of the deep Hub route.

If only preview zip archives exist, keep them honest and clearly secondary:
- label them as advanced or manual previews
- do not let them read as the default normal-user install story
- reflect "not zip-first" as publishing-policy intent, not just taste

### Acceptance criteria

- `SKILLS.md` accurately describes the current registry-driven Chummer6 guide runtime.
- Design media canon contains explicit density and spectacle fields for first-contact asset classes.
- The hero contract rejects quiet-profile or vague-board compositions and requires obvious Chummer-specific semantic anchors.
- First-contact art direction no longer defaults to sparse premium stills when denser graphic frames are required.
- `Karma Forge` and comparable horizon assets must pass semantic-fit checks, not style-only checks.
- Page-index assets cannot pass as empty-road or empty-signboard compositions.
- Selected first-contact assets support a deterministic overlay compositor or equivalent enforcement stage.
- README-first assets are stricter on humor, sparse easter eggs, and metadata tone.
- Front-page section order and update density are encoded in canon, not just implied.
- The participation CTA points to a softer public entry.
- Download artifacts are presented honestly as previews if that is all that currently exists.

### Out of scope

- undoing the recent registry-based canon-loader improvements
- reintroducing broad repo-scope skill inputs
- expanding front-page updates into a changelog slab again
- replacing grounded worldbuilding with random neon clutter

### Evidence

- Canon loader improvements: <https://raw.githubusercontent.com/ArchonMegalon/executive-assistant/main/scripts/chummer6_guide_canon.py>
- Stale EA skill docs: <https://github.com/ArchonMegalon/executive-assistant/blob/main/SKILLS.md>
- Current generated README: <https://raw.githubusercontent.com/ArchonMegalon/Chummer6/main/README.md>
- Current hero asset: <https://raw.githubusercontent.com/ArchonMegalon/Chummer6/main/assets/hero/chummer6-hero.png>
- Current design media brief: <https://raw.githubusercontent.com/ArchonMegalon/chummer6-design/main/products/chummer/PUBLIC_MEDIA_BRIEFS.yaml>
- Current page registry: <https://raw.githubusercontent.com/ArchonMegalon/chummer6-design/main/products/chummer/PUBLIC_GUIDE_PAGE_REGISTRY.yaml>
- Current `Karma Forge` art: <https://raw.githubusercontent.com/ArchonMegalon/Chummer6/main/assets/horizons/karma-forge.png>
- Current `horizons-index` art: <https://raw.githubusercontent.com/ArchonMegalon/Chummer6/main/assets/pages/horizons-index.png>

## Suggested follow-up split

If this should be broken into smaller issues, split it this way:

1. `docs-parity`: update `SKILLS.md` to match the registry-driven runtime
2. `design-canon`: add density, flash, and front-page layout fields to the design canon
3. `visual-pipeline`: enforce density, semantic-fit, reject classes, and overlay compositor support
4. `output-refresh`: replace hero, `horizons-index`, and `karma-forge`; fix participation CTA and download posture

## Operator notes

- This should be tracked separately from the earlier rejection-architecture issue.
- The main remaining risk is not broken structure. It is underpowered first-contact art.
- The next leap is making "packed and flashy" machine-enforceable rather than aspirational.
