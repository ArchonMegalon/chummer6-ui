# WIN-009 gen11 — Account-Sync Contract Handoff (Priority-4 transfer wave)

**Worker:** friday-windows (WIN-009, gen 11) · **Date:** 2026-11-23 · **Branch:** `windows/009-dwarf-journey-drive` @ `945ba2ddd`
**Evidence branch:** `evidence/win009-p4-amend-wave` @ `cc5fe173c6107652444e6bf4c279a3075a662654` (zip `win009-p4-amend-wave.zip`, 199,812 B, sha256 `717f7619e6b0e06a294357e17c75df0e00d9906522879b2b1036c132de5045d5`, raw-URL byte-identity verified)
**Published bytes used:** gen21 `bfe3791179…` (60,491,272 B), gen22 `f05bf0df…`, gen23 `01e3cb23…`, gen25 `c84dd9e8…` (60,491,261 B) — all stamp `1.0.0-preview.20261122.genNN`, coreHead pinned `0b4b3729`.

---

## 1. What the Android account-sync work can rely on TODAY (native-verified)

### 1.1 Magician export leg — **PASS on gen21 bytes** (`journey_receipt_P4A.json`, status `pass`)
Route: bundled `Samples/Legacy/Soma-Career.chum5` → production coordinator import → save (SavedRevision 1) → **native `.chum5` Save-As** → **portable dossier export**.

| Field | Value |
|---|---|
| workspaceId | `27fde5e3d62c447684a69153efc425d9` |
| savedRevision | 1 |
| native save-as file | `27fde5e3d62c447684a69153efc425d9.chum5`, **156,725 B** |
| portable export file | `Soma-export.json`, **43,434 B** |
| rulesetId | `sr5` |
| spells | total 10, sustained 5 (spells live in the **native payload**, not the dossier) |
| tradition | Hermetic |
| persisted doc state | PayloadKind `sr5/chum5-xml`, ContentRevision == SavedRevision == 1 |

**Contract meaning for sync:** a portable dossier is a *small* (≈43 KB) envelope whose payload is the native character format; the native `.chum5` is the fidelity carrier. Both artifacts are byte-count and sha256 recordable per workspace revision.

### 1.2 Amend round trip — **diagnosed to the exact line; Core-gated**
Native execution on gen25 bytes (first honest execution ever):
- Runner pressed `AmendRoundTripButton`: **delivered=True; attached; raise:hit=True; realTarget=AccessText**.
- Governed **export succeeded** (receipt written by `WorkspacePersistenceService.ExportAsync` → in-process client → Core `WorkspaceService.Export`).
- Governed **import refused**: `Amend round trip failed: Workspace format 'Json' is not supported.`
- Runner recorded the literal line in `InteractionNotes` and **withheld the success receipt** (fail-closed assertion) — correct governance behavior.

**Core contract gap (request `recJ85gRNPpVELB6cJK`, status `requested`):**
- Export side: `Chummer.Application/Workspaces/WorkspaceService.cs` receipt build (~line 1005–1007) serializes `DataExportBundle` to **JSON** and stamps `Format: WorkspaceDocumentFormat.Json`.
- Import side: every ruleset codec accepts **only `NativeXml`** — `Sr5WorkspaceCodec.cs:98` (dispatch) and `:342` (throw in `ToXmlContent`); `Sr4WorkspaceCodec.cs:130/520`; `Sr6WorkspaceCodec.cs:164/277`.
- Core's own tests pin the Json export (`WorkspaceServiceTests.cs:395/845`) — fix direction is Core's call: (a) accept Json on import, or (b) export the importable format. Enum today: `NativeXml = 0, Chum5Xml = NativeXml, Json = 1`.

### 1.3 Portability receipt shape (from native recon of the contract types)
`WorkspaceExportReceipt.Portability` carries: `CompatibilityState`, `NextSafeAction`, `SupportedExchangeModes`, `PayloadSha256`. `WorkspaceImportResult` carries: `ImportReceiptId`, `Portability` (same record). These fields are what account-sync should persist alongside each exported bundle; the amend leg is the natural producer of both once the Core format gap closes.

---

## 2. Runner-honesty findings the controller should know (affects prior evidence)

1. **The amend leg had never truly run before this wave.** Prior receipts (incl. gen33 w4) recorded "amend round trip completed with both portability receipts" **vacuously**: the runner's wait predicate `AmendBody.Children.Count > 0` was satisfied instantly by the construction-time explainer `TextBlock`, and the runner re-clicked the studio **opener** instead of the amend button. Fixed in `b67658496` (press the real button via resolved route; gate on the receipt line texts `Amend completed:` / `Amend round trip failed:`; forbid replay; 90 s timeout).
2. **Validation leg failure (P4C/P4C2, gen21) was a runner defect, not product.** The single pre-resolved press landed on a container detached by the tab-info section load → no observed input event → 20 s timeout, twice identical. Fixed in `f6adbb597` (re-resolve + re-press, up to 3 attempts, per-press diagnostics). P4C3 then passed the leg with attempt-1 honestly recorded `delivered=False;detached`.
3. **Product crash found + fixed:** `RunAmendRoundTripAsync` called `this.FindControl<Button>("AmendRoundTripButton")` **before** its try block; the programmatic studio window has **no Avalonia name scope** → unhandled `InvalidOperationException: Could not find parent name scope` killed the app (P4C4, err preserved, no receipt). Fixed in `f6e4f79dd` + `a0ed641c5` (method receives its own Button instance; `_amendBody` field set in `BuildAmendSection`).
4. **Cosmetic:** doubled `Amend round trip failed:` prefix removed in `945ba2ddd` (renderer owns the prefix; catch stores bare message).

---

## 3. What account-sync should build against (recommendations)

1. **Per-revision artifact ledger:** for each saved revision record `{workspaceId, savedRevision, payloadKind, payloadSha256, payloadBytes}`. Native proof shows `ContentRevision == SavedRevision` convergence is assertable.
2. **Dossier + native pair:** treat `Soma-export.json`-style dossiers as the sync envelope and the `.chum5` as the fidelity carrier; do **not** expect spell data inside the dossier (native payload carries it).
3. **Portability fields as first-class sync metadata:** persist `CompatibilityState`, `NextSafeAction`, `SupportedExchangeModes`, `PayloadSha256` from `WorkspaceExportReceipt.Portability`, and `ImportReceiptId` from `WorkspaceImportResult`.
4. **Do not round-trip via amend until Core ships the format fix** (`recJ85gRNPpVELB6cJK`); the governed import will refuse with the exact literal above.
5. **Edition isolation:** SR5 legs use `sr5` ruleset id; SR6 equivalents remain Core-blocked (spell-runtime-stats `recLT9MR9GkxaQSbpc2`) — sync contracts should be edition-tagged from day one.

---

## 4. Standing limits (unchanged)

- No installer selected, no release upload, no full-creation claim.
- SR6 finalization → save → fresh-process reopen stays blocked on Core9857 (spell-runtime-stats).
- Installed preview app (PID 16044 reconciliation) remains the only claim state; stale Nov-13 browser-approval state unchanged.
