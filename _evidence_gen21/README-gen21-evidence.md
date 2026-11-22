# WIN-009 gen21 scoped native evidence (single-read sidecar contract)

Bundle sha256: 0a0a49330219b911cb53b9014f07cc588c034fdbe0454e234e0c2dbe5b8fdfb3 (26,767 B)
Bytes under test: Chummer.Avalonia.exe sha256 bfe379117959f2c0f292871e638a970c82a4aba20bc25c7bf0205c0cc024e9e1, 60,491,272 B,
stamp 1.0.0-preview.20261122.gen21, UI head 1de3544340cfb878a88135ca05f48dad5288d49f, coreHead 0b4b372981298531f8099c88c1c017741de42a65.

Raw receipts inside the bundle (also reproduced here as JSON):
- journey_receipt_A.json  SR5 magic-editor phase A  status=pass (181 steps, save via file-menu, sidecar written)
- journey_receipt_B.json  SR6 mystic-adept phase B  status=fail  (Core spell-runtime-stats finalization refusal, finish refused 3x)
- journey_receipt_C.json  SR6 reopen-verify phase C status=fail  (fail-closed gate: saved receipt must record a passing first journey)
- journey_receipt_C5.json SR5 reopen-verify phase C5 status=pass (fresh process, alias + spells verified)
- workspace_draft_state_B.json  persisted SR6 draft, decision[1] carries spell+power rating2+tradition+pp in Command AND Preview
- MANIFEST.json  per-file sha256 + sizes
