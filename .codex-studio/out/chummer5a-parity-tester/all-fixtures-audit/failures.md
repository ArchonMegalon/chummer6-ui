# Failures

## Failure: (live user journey) / user_journey_tester_trace

- Fixture: (live user journey)
- Step: 1
- Action: user_journey_tester_trace
- Expected: User-journey trace should prove observable Linux desktop flows with the required assertions and screenshots.
- Actual: user journey workflow file_new_character_visible_workspace is missing required assertion(s): starter_attributes_match_seeded_workspace, section_preview_omits_review_copy
- Severity: blocking
- Category: behavioral
- Reference screenshot: /docker/chummercomplete/chummer-presentation/.codex-studio/out/chummer5a-parity-tester/all-fixtures-audit/screenshots/reference/reference-screenshots-not-captured.txt
- Actual screenshot: 
- Diff screenshot: /docker/chummercomplete/chummer-presentation/.codex-studio/out/chummer5a-parity-tester/all-fixtures-audit/screenshots/diff/diff-images-not-generated.txt

### Remediation Target

Refresh the user-journey tester trace until the required Linux desktop workflows, screenshots, and assertions all pass.

## Failure: Apex Predator.chum5, BLUE.chum5, Barrett.chum5, Bastion.chum5, Blindfire.chum5, Davis Jones.chum5, Draught.chum5, Fuzzy-chargen.chum5, Gangerbean.chum5, Gentle Earthquake.chum5, Ghile Mear.chum5, Glessner.chum5, Harmony.chum5, Miko.chum5, Mittens Chargen.chum5, Monomax (approved) 3.chum5, Munin.chum5, Munin_Career.chum5, Ocelot2.0.chum5, Pañcama.chum5, Popstar.chum5, Rez0luti0n2.0.chum5, SCSi.chum5, Serpent.chum5, Skink.chum5, Soma (Career).chum5, Soma.chum5, Spirit_Warden.chum5, Tenshi.chum5, Ushi Resub.chum5, Wesson.chum5, Yeti-#ffffff2.chum5, prime.chum5, resub.chum5 / fixture_ui_reconstruction_receipts

- Fixture: Apex Predator.chum5, BLUE.chum5, Barrett.chum5, Bastion.chum5, Blindfire.chum5, Davis Jones.chum5, Draught.chum5, Fuzzy-chargen.chum5, Gangerbean.chum5, Gentle Earthquake.chum5, Ghile Mear.chum5, Glessner.chum5, Harmony.chum5, Miko.chum5, Mittens Chargen.chum5, Monomax (approved) 3.chum5, Munin.chum5, Munin_Career.chum5, Ocelot2.0.chum5, Pañcama.chum5, Popstar.chum5, Rez0luti0n2.0.chum5, SCSi.chum5, Serpent.chum5, Skink.chum5, Soma (Career).chum5, Soma.chum5, Spirit_Warden.chum5, Tenshi.chum5, Ushi Resub.chum5, Wesson.chum5, Yeti-#ffffff2.chum5, prime.chum5, resub.chum5
- Step: 2
- Action: fixture_ui_reconstruction_receipts
- Expected: Explicit fixture sets should carry per-fixture UI reconstruction receipts.
- Actual: 34 explicit fixture(s) were selected under all_available_fixtures_explicit, but --reconstruction-receipts-dir was not provided.
- Severity: blocking
- Category: behavioral
- Reference screenshot: /docker/chummercomplete/chummer-presentation/.codex-studio/out/chummer5a-parity-tester/all-fixtures-audit/screenshots/reference/reference-screenshots-not-captured.txt
- Actual screenshot: 
- Diff screenshot: /docker/chummercomplete/chummer-presentation/.codex-studio/out/chummer5a-parity-tester/all-fixtures-audit/screenshots/diff/diff-images-not-generated.txt

### Remediation Target

Generate per-fixture UI reconstruction receipts proving open, save, reload, and identity-preserving roundtrips for the explicit fixture set.
