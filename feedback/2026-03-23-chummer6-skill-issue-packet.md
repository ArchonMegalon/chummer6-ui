# Issue Packet: Chummer6 Public-Output Lane Is Overconstrained

Target repos:
- `ArchonMegalon/executive-assistant`
- `ArchonMegalon/Chummer6`

Target area:
- Chummer6 public-guide skill stack
- Chummer6 generated public repo outputs

Suggested labels:
- `chummer6`
- `public-guide`
- `media`
- `copy`
- `process`

Suggested title:

`Chummer6 skill: replace stacked public-output veto loops with severity-gated repair and first-contact canon`

## Paste-ready issue body

### Summary

The current Chummer6 skill stack is still aligned with product vision, but it is overconstraining its own public-output lane. The result is not vision drift. The result is iteration drag, unnecessary rerender churn, and public-facing artifacts that sound less real and less deliberate than the underlying work already is.

The current structure is directionally correct:
- explicit Chummer6 skills exist for public writing, visual direction, scene audit, visual audit, and whole-pack audit
- publishable lanes target `ArchonMegalon/Chummer6`
- the public writer is receipt-bounded
- the visual system uses style epochs, a scene ledger, and targeted rerenders rather than a monolithic pass

The failure mode is that too many defensible constraints are acting like hard vetoes at the same time. In practice, the system is better at rejecting output than at shipping the best available candidate.

### Problem

Several controls that make sense in isolation are combining into one long rejection funnel:
- every image should be a real moment
- every image should hide a troll easter egg
- style epoch memory and scene-ledger variation rules are enforced
- first-contact and canon-locked targets add stricter visual rules
- provider choreography and repetition checks add another layer of pressure
- the public writer aggressively removes architecture-speak and overclaim risk

This produces three visible problems:

1. Visual churn is too high.
   Strong images can be rejected for issues that should be repairable rather than fatal, especially around troll motif strength, composition-family policy, and first-contact interpretation.

2. First-contact page images are not reliably doing the page's storytelling job.
   Current outputs often land in atmospheric cyberpunk mood when the page needs a concrete scene contract with visible narrative consequences.

3. Public copy is overshooting honesty into self-negation.
   Current language repeatedly frames visible work as accidental, lucky, or hypothetical even when the repo already contains deliberate structure and inspectable artifacts.

### Why this matters

- It slows iteration by turning quality control into creative veto control.
- It teaches the outer agent to keep rejecting and rewriting instead of converging.
- It weakens public confidence because the repo visibly contains work that the copy keeps rhetorically minimizing.
- It makes provider instability look like prompt-direction failure because those classes are not surfaced distinctly.

### Working diagnosis

This does not currently look like "Codex as image renderer" failure. Based on the repo layout and worker flow, the likelier problem is a veto-heavy outer execution contract around the renderers. Treat this as an inference unless contradicted by run logs.

### Requested changes

#### 1. Replace stacked veto behavior with a severity ladder

Split audit findings into:
- `blocker`: wrong file/aspect, obvious readable gibberish or logos, broken crop, wrong page role, forbidden mechanics claims
- `repairable`: weak or missing troll motif, composition too close to recent asset, weak page-role fit, placeholder vibe
- `advisory`: taste-level concerns

Post-render audit should not behave like another all-or-nothing rejection pass.

#### 2. Make troll motif compliance a repair pass, not a first-pass veto

Generate the strongest scene first. If the troll cue is weak or absent, apply the deterministic troll postpass automatically. Do not reject otherwise strong images solely because the motif was not perfect on the first render.

#### 3. Hard-lock first-contact page scene contracts

For hero, `WHAT_CHUMMER6_IS`, `HORIZONS`, and `PARTS`, use explicit first-contact shot templates instead of broad mood guidance.

Required intent:
- hero: trust decision with visible proof props
- `WHAT_CHUMMER6_IS`: receipt-trail reasoning, not just contemplative mood
- `HORIZONS`: future street or district use-case with human stakes
- `PARTS`: boundaries and handoffs, not a prop gallery

#### 4. Move diversity policing earlier

Use the scene auditor and scene ledger to settle composition diversity before render spend. Post-render audit should mostly answer publishability for the page, not rediscover pack-level composition policy after the asset already exists.

#### 5. Put an explicit ceiling on rerender loops

Add hard limits such as:
- one plan revision
- two render attempts
- one motif repair pass
- then forced best-candidate acceptance or human review

Persist the best failed candidate instead of letting it disappear behind repeated retries.

#### 6. Separate provider failure from direction failure

Surface distinct outcomes such as:
- `provider_failed`
- `direction_failed`
- `repair_failed`
- `human_override`

Provider-side muddy text artifacts, quota trouble, or unstable lighting should not automatically train the system to rewrite prompts forever.

#### 7. Keep the anti-maintainer writer rules, but stop universal downshifting

Keep the public-writer bans on topology lectures, unsupported mechanics claims, and maintainer-memo language. Relax the blanket transformation policy that turns grounded public statements into accidental or self-negating phrasing everywhere.

#### 8. Introduce a readiness taxonomy

Replace one-note `concept/luck/accident` language with typed labels such as:
- `guide-stable`
- `inspectable-preview`
- `operator-only`
- `canon-locked`
- `not-publicly-promoted`
- `experimental-horizon`

This preserves honesty without flattening every page into the lowest-confidence dialect.

#### 9. Stop hypotheticalizing the parts map

`PARTS` should not describe itself as a speculative split if the page immediately links to concrete part surfaces. Better posture: the parts exist, but their readiness and polish differ.

#### 10. Treat canon-locked first-contact assets as low-variance surfaces

For hero, `parts-index`, and `horizons-index`, prefer one stable provider family and one style-epoch family unless a human explicitly rotates them. The most important public pages should be the least experimental.

#### 11. Escalate to human review earlier

After one failed correction on a canon-locked first-contact asset, produce a human-review packet instead of running another autonomous veto loop. The operator decision should be:
- ship this
- repair this one thing
- rerender with a new scene family

#### 12. Make debug artifacts first-class

For each important asset, keep a compact packet with:
- scene contract
- provider
- prompt hash
- composition family
- audit results
- rejection reasons
- candidate thumbnails

Bad runs should be inspectable after the fact without reverse-engineering invisible internal criteria.

### Acceptance criteria

- The audit system distinguishes `blocker`, `repairable`, and `advisory` findings and does not rerender on advisory-only output.
- Troll motif misses on otherwise strong images route through a repair step before rejection.
- Hero, `WHAT_CHUMMER6_IS`, `HORIZONS`, and `PARTS` use explicit scene contracts that encode page-role intent.
- Scene diversity policy is enforced at planning time, not mainly after render spend.
- The autonomous image loop has hard caps and preserves best candidates for review.
- Output artifacts and logs clearly distinguish provider instability from direction failure.
- Public-writer rules preserve honesty without defaulting every page to accidental or hypothetical language.
- Public copy can express different readiness levels without universal self-negation.
- `PARTS` copy acknowledges concrete parts while staying honest about uneven readiness.
- Canon-locked first-contact assets use lower-variance provider and style choices than secondary pages.
- Failed first-contact correction loops escalate to human review earlier.
- Asset debug packets are available for first-contact and canon-locked pages.

### Out of scope

- weakening receipt discipline
- allowing unsupported mechanics claims
- removing scene-ledger or style-epoch memory entirely
- replacing the public-safe writer with architecture-heavy copy

### Evidence

- Skill catalog and Chummer6-specific lanes: <https://raw.githubusercontent.com/ArchonMegalon/executive-assistant/main/SKILLS.md>
- Visual contract and prompt guidance: <https://raw.githubusercontent.com/ArchonMegalon/executive-assistant/main/chummer6_guide/VISUAL_PROMPTS.md>
- Current generated public repo output: <https://raw.githubusercontent.com/ArchonMegalon/Chummer6/main/README.md>
- EA repo context: <https://github.com/ArchonMegalon/executive-assistant>
- Bootstrap policy and attempt budget: <https://raw.githubusercontent.com/ArchonMegalon/executive-assistant/main/scripts/bootstrap_chummer6_guide_skill.py>
- Media worker flow and provider handling: <https://raw.githubusercontent.com/ArchonMegalon/executive-assistant/main/scripts/chummer6_guide_media_worker.py>
- Public writer worker: <https://github.com/ArchonMegalon/executive-assistant/blob/main/scripts/chummer6_guide_worker.py>
- Current `PARTS` landing posture: <https://raw.githubusercontent.com/ArchonMegalon/Chummer6/main/PARTS/README.md>
- Recent downstream repo history: <https://github.com/ArchonMegalon/Chummer6/commits/main/>

## Suggested follow-up split

If this should be broken into smaller issues, split it this way:

1. `chummer6-media`: severity ladder, rerender caps, provider-vs-direction failure taxonomy
2. `chummer6-visual-canon`: first-contact scene contracts, troll motif postpass, canon-locked provider stability
3. `chummer6-public-writer`: readiness taxonomy, relaxed blanket self-negation, `PARTS` posture cleanup
4. `chummer6-debugging`: candidate preservation and debug packet publication

## Operator notes

- The main change is architectural: stop treating every quality issue like a ship-stopper.
- The first fix to land should be the rejection architecture.
- The second fix should be first-contact image canon.
- The third fix should be public-copy posture.
