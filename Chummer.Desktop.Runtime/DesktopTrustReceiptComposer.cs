using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

public readonly record struct DesktopTrustReceiptSectionData(string Title, IReadOnlyList<string> Lines);

public static class DesktopTrustReceiptComposer
{
    public static string BuildDialogReceipt(DesktopDialogState dialog)
        => string.Join("\n", FlattenSections(BuildDialogReceiptSections(dialog)));

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildImportReviewSections(
        string? rulesetId,
        string selectedFile,
        string rawImportXml,
        string importOracleReceipt,
        string? importError)
    {
        string ruleset = Normalize(rulesetId, "detected at import time");
        string stagedFile = Normalize(selectedFile, "staged import file");
        string oracleReceipt = Normalize(importOracleReceipt, "oracle coverage is reviewed before the workspace is changed");
        string blockerReceipt = Normalize(importError, "no grounded import blocker is present before acceptance");
        string rawXmlPosture = string.IsNullOrWhiteSpace(rawImportXml)
            ? "raw XML review is empty"
            : $"raw XML review has {rawImportXml.Length} character(s)";
        string stagedArtifactReceipt = BuildImportStagedArtifactReceipt(stagedFile, rawXmlPosture);
        string rulesetToken = NormalizeReceiptToken(ruleset);
        string receiptToken = NormalizeReceiptToken(stagedFile);

        List<string> explainReceiptLines =
        [
            $"Import rule-environment receipt: target ruleset {ruleset}; import remains review-only until the grounded receipt is emitted.",
            $"Import environment diff: before staged/{receiptToken}; after oracle-reviewed/{ruleset}; review stays copy-safe until the user accepts import.",
            $"Import receipt correlation key: import/{rulesetToken}/{receiptToken}; matches the blocker, oracle, and before/after environment diff lines below.",
            $"Receipt scope: import target {ruleset}; before/after diff is copy-safe and excludes raw character XML until the user accepts import.",
            $"Import support handoff receipt: support can cite import/{rulesetToken}/{receiptToken} with staged file name, oracle, and blocker text; raw XML stays excluded unless the user attaches it.",
            $"Grounded import explain receipt: target {ruleset}; oracle {oracleReceipt}; staged file {stagedFile}; blocker {blockerReceipt}.",
            $"Import staged artifact receipt: {stagedArtifactReceipt}.",
            $"Import diagnostics receipt: before staged/{receiptToken}; after oracle-reviewed/{ruleset}; blocker {blockerReceipt}; proof {oracleReceipt}.",
            $"Import diagnostics diff: before staged file {stagedFile} with {rawXmlPosture}; after oracle-reviewed {ruleset} with blocker {blockerReceipt}; no workspace, source-toggle, or support state changes before acceptance.",
            $"Import support diagnostics receipt: support can cite import/{rulesetToken}/{receiptToken} with before/after staged-file truth, blocker text, and oracle proof without changing local workspace state."
        ];

        List<string> beforeImportLines =
        [
            $"Import source-toggle diff receipt: before import current workspace source toggles stay active while {stagedFile} is staged; after acceptance only reviewed {ruleset} source toggles bind to the workspace.",
            $"Import artifact diff receipt: before staged artifact {stagedArtifactReceipt}; after acceptance raw payload stays excluded from the support receipt unless the user attaches it.",
            $"Import environment tuple diff: before workspace/current-source/support-local/{receiptToken}; after oracle-reviewed/{ruleset}/accepted-source-only; correlation import/{rulesetToken}/{receiptToken}.",
            $"Environment diff before import: current workspace, support posture, and source toggles stay unchanged while {stagedFile} is staged."
        ];

        List<string> afterReviewLines =
        [
            $"Environment diff after import: accepted content binds to {ruleset}; {oracleReceipt}.",
            $"Import oracle receipt: {oracleReceipt}",
            $"Import blocker receipt: {blockerReceipt}",
            $"Import blocker diff receipt: before blocker {blockerReceipt}; after acceptance keeps the blocker visible until oracle review clears it.",
            $"Raw import receipt: {rawXmlPosture}."
        ];

        return
        [
            new DesktopTrustReceiptSectionData("Grounded explain receipt", explainReceiptLines),
            new DesktopTrustReceiptSectionData("Before import environment diff", beforeImportLines),
            new DesktopTrustReceiptSectionData("After review environment diff", afterReviewLines)
        ];
    }

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildDialogReceiptSections(DesktopDialogState dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        DesktopDialogField? importRuleset = dialog.Fields.FirstOrDefault(static field =>
            string.Equals(field.Id, "importRulesetId", StringComparison.Ordinal));
        bool isImportDialog = importRuleset is not null
            || dialog.Actions.Any(static action => string.Equals(action.Id, "import", StringComparison.Ordinal));

        if (!isImportDialog)
        {
            return [];
        }

        string ruleset = Normalize(importRuleset?.Value, "detected at import time");
        string rulesetToken = NormalizeReceiptToken(ruleset);
        string sourceToggleReceipt = Normalize(
            ResolveFieldValue(dialog.Fields, "masterIndexSourceSelectionSummary")
            ?? ResolveFieldValue(dialog.Fields, "masterIndexSourceSelectionLaneReceipt"),
            "source toggles stay unchanged until import acceptance");
        string oracleReceipt = Normalize(
            ResolveFieldValue(dialog.Fields, "masterIndexImportOracleReceipt")
            ?? ResolveFieldValue(dialog.Fields, "masterIndexAdjacentSr6OracleReceipt"),
            "oracle coverage is reviewed before the workspace is changed");
        string blockerReceipt = Normalize(
            ResolveFieldValue(dialog.Fields, "importBlockerReceipt"),
            "no grounded import blocker is present before acceptance");
        string stagedArtifactReceipt = BuildImportStagedArtifactReceipt(dialog.Fields);

        List<string> explainReceiptLines =
        [
            $"Import rule-environment receipt: target ruleset {ruleset}; import remains review-only until the grounded receipt is emitted.",
            $"Import environment diff: before review-only/{ruleset}; after oracle-reviewed/{ruleset}; review stays copy-safe until the user accepts import.",
            $"Import receipt correlation key: import/{rulesetToken}/review-only; matches the blocker, oracle, and before/after environment diff lines below.",
            $"Receipt scope: import target {ruleset}; before/after diff is copy-safe and excludes raw character XML until the user accepts import.",
            $"Import support handoff receipt: support can cite import/{rulesetToken}/review-only with oracle, source-toggle, and blocker text; raw XML stays excluded unless the user attaches it.",
            $"Grounded import explain receipt: target {ruleset}; oracle {oracleReceipt}; source toggles {sourceToggleReceipt}; blocker {blockerReceipt}.",
            $"Import staged artifact receipt: {stagedArtifactReceipt}.",
            $"Import diagnostics receipt: before review-only/{ruleset}; after oracle-reviewed/{ruleset}; blocker {blockerReceipt}; proof {oracleReceipt}.",
            $"Import diagnostics diff: before review-only {ruleset} with {sourceToggleReceipt}; after oracle-reviewed {ruleset} with blocker {blockerReceipt}; no workspace, source-toggle, or support state changes before acceptance.",
            $"Import support diagnostics receipt: support can cite import/{rulesetToken}/review-only with before/after source-toggle truth, blocker text, and oracle proof without changing local workspace state."
        ];

        List<string> beforeImportLines =
        [
            $"Import source-toggle diff receipt: before import {sourceToggleReceipt}; after acceptance only reviewed {ruleset} source toggles bind to the workspace.",
            $"Import artifact diff receipt: before staged artifact {stagedArtifactReceipt}; after acceptance raw payload stays excluded from the support receipt unless the user attaches it.",
            $"Import environment tuple diff: before workspace/current-source/support-local/review-only; after oracle-reviewed/{ruleset}/accepted-source-only; correlation import/{rulesetToken}/review-only.",
            $"Environment diff before import: the current workspace and support posture stay unchanged. Source toggles, support posture, and saved character remain unchanged; {sourceToggleReceipt}."
        ];

        List<string> afterReviewLines =
        [
            $"Environment diff after import: imported content is expected to bind to {ruleset}; accepted content binds to {ruleset} only after oracle review; {oracleReceipt}.",
            $"Import blocker receipt: {blockerReceipt}.",
            $"Import blocker diff receipt: before blocker {blockerReceipt}; after acceptance keeps the blocker visible until oracle review clears it."
        ];

        AddFieldReceipt(afterReviewLines, dialog.Fields, "masterIndexImportOracleReceipt", "Import oracle receipt");
        AddFieldReceipt(afterReviewLines, dialog.Fields, "masterIndexAdjacentSr6OracleReceipt", "Adjacent SR6 oracle receipt");
        AddFieldReceipt(afterReviewLines, dialog.Fields, "masterIndexSourceSelectionSummary", "Source selection receipt");
        AddFieldReceipt(afterReviewLines, dialog.Fields, "masterIndexSourceSelectionLaneReceipt", "Source toggle receipt");
        AddRawImportReceipt(afterReviewLines, dialog.Fields, "openCharacterXml", "Raw import receipt");
        AddRawImportReceipt(afterReviewLines, dialog.Fields, "heroLabXml", "Hero Lab import receipt");

        return
        [
            new DesktopTrustReceiptSectionData("Grounded explain receipt", explainReceiptLines),
            new DesktopTrustReceiptSectionData("Before import environment diff", beforeImportLines),
            new DesktopTrustReceiptSectionData("After review environment diff", afterReviewLines)
        ];
    }

    public static IReadOnlyList<string> BuildDiagnosticsDiff(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus)
        => FlattenSections(BuildDiagnosticsSections(installState, updateStatus));

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildDiagnosticsSections(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus)
    {
        ArgumentNullException.ThrowIfNull(installState);
        ArgumentNullException.ThrowIfNull(updateStatus);

        string targetVersion = Normalize(updateStatus.LastManifestVersion, installState.ApplicationVersion);
        string supportability = Normalize(updateStatus.SupportabilityState, "support posture follows current install truth");
        string manifestLocation = Normalize(updateStatus.ManifestLocation, "no manifest location configured");
        string proofPosture = Normalize(updateStatus.ProofStatus, "proof status not published locally");
        string rolloutPosture = Normalize(updateStatus.RolloutState, "rollout state not published locally");
        string blockerReceipt = Normalize(updateStatus.LastError, "none recorded");

        List<string> explainReceiptLines =
        [
            $"Diagnostics environment diff: {installState.HeadId} on {installState.Platform}/{installState.Arch}, channel {installState.ChannelId}.",
            $"Diagnostics receipt correlation key: support/{installState.InstallationId}/{installState.HeadId}/{installState.ChannelId}; matches the support packet and before/after environment diff lines below.",
            $"Support diagnostics packet id: support/{Normalize(installState.InstallationId, "local-install")}/before-{Normalize(installState.ApplicationVersion, "installed-version")}/after-{targetVersion}; ties the copied packet, portal prefill, and visible diagnostics diff to the same install without changing local state.",
            $"Support diagnostics correlation: {BuildDiagnosticsCorrelation(installState, updateStatus)}; before/after diff is copy-safe and does not change local install state.",
            $"Release-channel receipt: installed {installState.ChannelId}/{installState.ApplicationVersion} is compared against manifest {Normalize(updateStatus.ChannelId, installState.ChannelId)}/{targetVersion}.",
            $"Grounded support receipt: installation {installState.InstallationId} carries the same head, channel, platform, architecture, manifest, and supportability context into diagnostics.",
            $"Support diagnostics receipt: before {installState.ApplicationVersion}/{Normalize(updateStatus.Status, "unknown")}; after {targetVersion}/{Normalize(updateStatus.RecommendedAction, "review support posture")}; blocker {blockerReceipt}; proof {proofPosture}.",
            $"Support diagnostics explain receipt: installed {installState.HeadId}/{installState.ApplicationVersion} stays the before state; support reviews recommended action '{Normalize(updateStatus.RecommendedAction, "review support posture")}' as the after state with blocker {blockerReceipt} and proof {proofPosture}.",
            $"Support handoff receipt: support can cite support/{installState.InstallationId}/{installState.HeadId}/{installState.ChannelId} with before/after tuple, blocker, proof, rollout, and supportability without changing local install state."
        ];

        List<string> beforeSupportLines =
        [
            $"Diagnostics environment diff before support: installed version {installState.ApplicationVersion}; update status {Normalize(updateStatus.Status, "unknown")}; supportability {supportability}; manifest {manifestLocation}; last blocker {blockerReceipt}.",
            $"Before: installed version {installState.ApplicationVersion}; update status {Normalize(updateStatus.Status, "unknown")}; supportability {supportability}; manifest {manifestLocation}; last blocker {blockerReceipt}.",
            $"Support blocker diff receipt: before blocker {blockerReceipt} is attached to {Normalize(installState.InstallationId, "local-install")}; after support keeps the blocker, rollout {rolloutPosture}, and manifest {manifestLocation} copy-safe until the user retries or updates.",
            $"Support identity diff: before {Normalize(installState.Status, "unclaimed")} install {installState.InstallationId}; after support keeps {Normalize(installState.UserId ?? installState.SubjectId, "the same local installation")} attached to the diagnostics packet."
        ];

        List<string> afterSupportLines =
        [
            $"Diagnostics environment diff after support: target version {targetVersion}; recommended action {Normalize(updateStatus.RecommendedAction, "review support posture")}; proof {proofPosture}; rollout {rolloutPosture}; support packet carries before/after environment truth without changing local install state.",
            $"After: target version {targetVersion}; recommended action {Normalize(updateStatus.RecommendedAction, "review support posture")}; proof {proofPosture}; rollout {rolloutPosture}.",
            $"Support proof diff receipt: before installed proof for {installState.HeadId}/{installState.ChannelId}/{installState.ApplicationVersion} remains the local truth; after support reviews manifest proof {proofPosture} and rollout {rolloutPosture} without mutating the install.",
            $"Support environment tuple diff: before {installState.HeadId}/{installState.Platform}/{installState.Arch}/{installState.ChannelId}/{installState.ApplicationVersion}; after {Normalize(updateStatus.HeadId, installState.HeadId)}/{Normalize(updateStatus.Platform, installState.Platform)}/{Normalize(updateStatus.Arch, installState.Arch)}/{Normalize(updateStatus.ChannelId, installState.ChannelId)}/{targetVersion}.",
            $"Support blocker receipt: current blocker {blockerReceipt}; support reviews it against manifest {manifestLocation} before any retry, reinstall, or escalation.",
            $"Support packet diff receipt: before/after diagnostics are attached before any user is asked to retry, reinstall, or contact support."
        ];

        if (!string.IsNullOrWhiteSpace(updateStatus.RolloutState)
            || !string.IsNullOrWhiteSpace(updateStatus.RolloutReason))
        {
            afterSupportLines.Add($"Rollout receipt: state {Normalize(updateStatus.RolloutState, "unknown")}; reason {Normalize(updateStatus.RolloutReason, "none published")}.");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.FixAvailabilitySummary))
        {
            afterSupportLines.Add($"Fix receipt: {updateStatus.FixAvailabilitySummary}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.KnownIssueSummary))
        {
            afterSupportLines.Add($"Known issue receipt: {updateStatus.KnownIssueSummary}");
        }

        if (!string.IsNullOrWhiteSpace(updateStatus.LastError))
        {
            afterSupportLines.Add($"Last diagnostics blocker: {updateStatus.LastError}");
        }

        return
        [
            new DesktopTrustReceiptSectionData("Grounded support explain receipt", explainReceiptLines),
            new DesktopTrustReceiptSectionData("Before support environment diff", beforeSupportLines),
            new DesktopTrustReceiptSectionData("After support environment diff", afterSupportLines)
        ];
    }

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildCrashDiagnosticsSections(DesktopCrashReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        string platform = ResolveCrashPlatform(report);
        string crashToken = NormalizeReceiptToken(report.CrashId);
        string headToken = NormalizeReceiptToken(report.HeadId);
        string versionToken = NormalizeReceiptToken(report.ApplicationVersion);
        string architecture = Normalize(report.ProcessArchitecture, "unknown");
        string version = Normalize(report.ApplicationVersion, "unknown");
        string headId = Normalize(report.HeadId, "unknown");
        string exceptionType = Normalize(report.ExceptionType, "unknown exception");
        string processName = Normalize(report.ProcessName, "unknown process");
        string runtimeVersion = Normalize(report.RuntimeVersion, "unknown runtime");

        return
        [
            new DesktopTrustReceiptSectionData(
                "Grounded crash explain receipt",
                [
                    $"Crash diagnostics receipt: crash/{crashToken}/{headToken} is grounded to the captured desktop runtime packet.",
                    $"Crash environment diff: before {headId}/{platform}/{architecture}/{version}; after support-review/{Normalize(report.CrashId, "unknown")}; recovery remains copy-safe until the user chooses the next action.",
                    $"Crash support receipt correlation key: crash/{crashToken}/{versionToken}/{NormalizeReceiptToken(platform)}; matches the support diagnostics packet and before/after environment diff lines below.",
                    $"Crash diagnostics packet id: crash/{crashToken}/before-{versionToken}/after-support-review; ties the copied packet, support handoff, and visible before/after diff to the same crash packet without mutating local state.",
                    $"Crash support explain receipt: exception {exceptionType} at {report.CapturedAtUtc:u}; process {processName}; runtime {runtimeVersion}.",
                    $"Crash support handoff receipt: support can cite crash/{crashToken}/{headToken} with before/after tuple, exception, and runtime proof without exposing raw character data.",
                    "Crash packet diff receipt: before/after crash diagnostics are copy-safe and can be attached to support without exposing raw character data."
                ]),
            new DesktopTrustReceiptSectionData(
                "Before recovery environment diff",
                [
                    $"Crash environment diff before recovery: head {headId} version {version} on {platform}/{architecture} stopped with {exceptionType}.",
                    $"Crash environment tuple diff: before {headId}/{platform}/{architecture}/{version}; after support-review/{Normalize(report.CrashId, "unknown")}."
                ]),
            new DesktopTrustReceiptSectionData(
                "After recovery environment diff",
                [
                    $"Crash environment diff after recovery: support reviews crash/{crashToken}/{headToken} before local restart, restore, or escalation.",
                    $"Crash blocker receipt: exception {exceptionType}; crash id {Normalize(report.CrashId, "unknown")}; restart or support action stays explicit."
                ])
        ];
    }

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildBuildLabSections(BuildLabConceptIntakeState buildLab)
    {
        ArgumentNullException.ThrowIfNull(buildLab);

        string variantSummary = buildLab.Variants.Count == 0
            ? "no variant is projected yet"
            : string.Join("; ", buildLab.Variants.Select(static variant =>
                $"{variant.Label} {variant.TableFit}"));
        string progressionSummary = buildLab.ProgressionTimelines.Count == 0
            ? "no progression timeline is projected yet"
            : string.Join(" -> ", buildLab.ProgressionTimelines.Select(static step => step.Title));
        string blockerSummary = buildLab.Watchouts is { Count: > 0 }
            ? string.Join("; ", buildLab.Watchouts)
            : buildLab.CanContinue
                ? "no blocker is active"
                : "build review still has a blocker";
        string disabledActions = buildLab.Actions.Count == 0
            ? "none"
            : string.Join(", ", buildLab.Actions.Where(static action => !action.Enabled).Select(static action => action.Label));
        if (string.IsNullOrWhiteSpace(disabledActions))
        {
            disabledActions = "none";
        }
        string runtimeFingerprint = Normalize(buildLab.WorkflowId, "runtime fingerprint pending");
        string leadRuleset = Normalize(buildLab.RulesetId, "ruleset pending");
        string leadOrigin = Normalize(buildLab.SourceDocumentId, Normalize(buildLab.Summary, "origin pending"));
        string coverageSummary = Normalize(buildLab.TeamCoverage?.CoverageSummary, "coverage pending");

        return
        [
            new DesktopTrustReceiptSectionData(
                "Grounded explain receipt",
                [
                    $"Build receipt correlation key: build/{NormalizeReceiptToken(leadRuleset)}/{NormalizeReceiptToken(runtimeFingerprint)}; ties the copied build blocker, support handoff, and visible before/after diff to the same candidate review.",
                    $"Build receipt scope: workspace {buildLab.WorkspaceId}; blocker and explain receipts are copy-safe and do not apply a variant.",
                    $"Grounded build explain receipt: build origin {leadOrigin}; runtime {runtimeFingerprint}; variants {variantSummary}; coverage {coverageSummary}.",
                    $"Grounded explain receipt: build origin {leadOrigin}; runtime {runtimeFingerprint}; variants {variantSummary}; coverage {coverageSummary}.",
                    $"Build support handoff receipt: support can cite build/{NormalizeReceiptToken(leadRuleset)}/{NormalizeReceiptToken(runtimeFingerprint)} with variant summary, coverage, blocker, and runtime proof.",
                    $"Build diagnostics packet id: build/{NormalizeReceiptToken(leadRuleset)}/before-intake/after-variant-review.",
                    $"Build diagnostics correlation: ties the copied build blocker, support handoff, and visible before/after diff to the same candidate review; before/after diff is copy-safe and does not apply a variant, export, or support action.",
                    $"Build support diagnostics receipt: build review for {leadRuleset} stays copy-safe; disabled build action(s): {disabledActions}; support closure not required.",
                    $"Build blocker diagnostics diff: before build environment diff and after build environment diff remain review-only; no variant, export, or campaign fit result is applied before review."
                ]),
            new DesktopTrustReceiptSectionData(
                "Before build environment diff",
                [
                    $"Environment diff before build: concept intake '{Normalize(buildLab.Summary, "pending")}' on {leadRuleset}; runtime {runtimeFingerprint}; coverage {coverageSummary}.",
                    $"Before build environment diff: variant summary {variantSummary}; progression {progressionSummary}; blocker {blockerSummary}."
                ]),
            new DesktopTrustReceiptSectionData(
                "After build environment diff",
                [
                    $"Environment diff after build: candidate variants remain review-only until explicit selection; runtime {runtimeFingerprint}; origin {leadOrigin}.",
                    $"After build environment diff: disabled build action(s): {disabledActions}; blocker {blockerSummary}; no variant, export, or campaign fit result is applied before review."
                ])
        ];
    }

    public static string BuildPortabilityDiagnosticsDiffText(WorkspacePortabilityReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return string.Join(
            " ",
            [
                $"Import diagnostics receipt: {BuildImportRuleEnvironmentSummary(receipt)}.",
                $"Import support diagnostics receipt: {BuildImportSupportReuse(receipt)}.",
                $"Grounded import explain receipt: {BuildImportExplainReceiptSummary(receipt)}.",
                $"Import blocker receipt: {BuildImportBlockerSummary(receipt)}.",
                $"Environment diff before import: {BuildEnvironmentBeforeImportSummary(receipt)}.",
                $"Environment diff after import: {BuildEnvironmentAfterImportSummary(receipt)}."
            ]);
    }

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildPortabilityReceiptSections(WorkspacePortabilityReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        string ruleset = Normalize(receipt.FormatId, "detected at import time");
        string token = NormalizeReceiptToken(ruleset);
        string correlation = $"import/{token}/{NormalizeReceiptToken(receipt.PayloadSha256)}";
        string explain = BuildImportExplainReceiptSummary(receipt);
        string before = BuildEnvironmentBeforeImportSummary(receipt);
        string after = BuildEnvironmentAfterImportSummary(receipt);
        string blocker = BuildImportBlockerSummary(receipt);
        string supportReuse = BuildImportSupportReuse(receipt);
        string tupleDiff = $"Import environment tuple diff: before workspace/current-source/support-local/{token}; after oracle-reviewed/{token}/accepted-source-only; correlation {correlation}.";

        return
        [
            new DesktopTrustReceiptSectionData(
                "Grounded explain receipt",
                [
                    $"Import receipt correlation key: {correlation}",
                    $"Receipt scope: import target {ruleset}; before/after diff is copy-safe and excludes raw character XML until the user accepts import.",
                    $"Import support handoff receipt: {supportReuse}",
                    $"Import diagnostics receipt: {BuildImportRuleEnvironmentSummary(receipt)}.",
                    $"Import support diagnostics receipt: {supportReuse}",
                    $"Grounded import explain receipt: {explain}",
                    $"Import blocker receipt: {blocker}",
                    $"Import diagnostics diff: {BuildPortabilityDiagnosticsDiffText(receipt)}"
                ]),
            new DesktopTrustReceiptSectionData(
                "Before import environment diff",
                [
                    tupleDiff,
                    $"Import environment before: {before}",
                    $"Environment diff before import: {before}"
                ]),
            new DesktopTrustReceiptSectionData(
                "After review environment diff",
                [
                    $"Import environment after: {after}",
                    $"Environment diff after import: {after}",
                    $"Import explain receipt: {explain}",
                    $"Import support reuse: {supportReuse}"
                ])
        ];
    }

    public static IReadOnlyList<DesktopTrustReceiptSectionData> BuildRuntimeInspectorSupportSections(RuntimeInspectorProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        string fingerprint = Normalize(projection.RuntimeLock.RuntimeFingerprint, "runtime fingerprint pending");
        string packetId = $"runtime/{NormalizeReceiptToken(fingerprint)}";
        string diagnostics = $"target {projection.TargetKind}/{projection.TargetId}; install {projection.Install.State}; ruleset {projection.RuntimeLock.RulesetId}";
        string environmentBefore = $"runtime {projection.RuntimeLock.RulesetId}/{fingerprint} remains pinned on {projection.Install.State}";
        string environmentAfter = $"support review remains evidence-only for {projection.ProfileSourceKind}/{Normalize(projection.Promotion?.CurrentStage, "review-required")}";
        string blocker = ResolveRuntimeInspectorBlocker(projection);

        return
        [
            new DesktopTrustReceiptSectionData(
                "Support diagnostics receipt",
                [
                    $"Diagnostics receipt correlation key: {packetId}",
                    $"Support diagnostics packet id: {packetId}/support-review",
                    $"Support diagnostics correlation: support reviews runtime inspector projection {fingerprint} without mutating the local install.",
                    $"Grounded support receipt: runtime inspector {fingerprint} remains the before state while support review stays copy-safe.",
                    $"Support diagnostics receipt: {diagnostics}",
                    $"Support diagnostics explain receipt: blocker {blocker}; compatibility review remains bounded to the runtime inspector projection.",
                    $"Support handoff receipt: support can cite {packetId} with compatibility, blocker, and environment diff receipts.",
                    $"Support proof diff receipt: before runtime proof {fingerprint}; after support review remains evidence-only.",
                    $"Support environment tuple diff: before runtime/{fingerprint}; after support-review/{NormalizeReceiptToken(blocker)}.",
                    $"Support packet diff receipt: before/after support diagnostics stay copy-safe and do not apply a runtime change."
                ]),
            new DesktopTrustReceiptSectionData(
                "Before support environment diff",
                [
                    $"Before support environment diff: {environmentBefore}",
                    $"Compatibility diagnostics packet id: compatibility/{NormalizeReceiptToken(fingerprint)}",
                    $"Compatibility blocker receipt: {blocker}",
                    $"Compatibility environment diff: runtime remains in-place while diagnostics are reviewed.",
                    $"Compatibility proof diff receipt: proof review stays evidence-only until operator action.",
                    $"Compatibility packet diff receipt: before/after compatibility diagnostics remain copy-safe.",
                    $"Compatibility support handoff receipt: support can cite compatibility/{NormalizeReceiptToken(fingerprint)} with blocker and environment summaries."
                ]),
            new DesktopTrustReceiptSectionData(
                "After support environment diff",
                [
                    $"After support environment diff: {environmentAfter}",
                    $"Grounded support explain receipt: runtime {fingerprint} keeps blocker {blocker} visible until explicit follow-through."
                ])
        ];
    }

    private static IReadOnlyList<string> FlattenSections(IReadOnlyList<DesktopTrustReceiptSectionData> sections)
        => sections.SelectMany(static section =>
        {
            List<string> lines = [];
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                lines.Add(section.Title);
            }

            lines.AddRange(section.Lines);
            return lines;
        }).ToArray();

    private static string? ResolveFieldValue(IReadOnlyList<DesktopDialogField> fields, string fieldId)
        => fields.FirstOrDefault(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal))?.Value;

    private static void AddFieldReceipt(
        List<string> lines,
        IReadOnlyList<DesktopDialogField> fields,
        string fieldId,
        string label)
    {
        string? value = ResolveFieldValue(fields, fieldId);
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    private static void AddRawImportReceipt(
        List<string> lines,
        IReadOnlyList<DesktopDialogField> fields,
        string fieldId,
        string label)
    {
        string? value = ResolveFieldValue(fields, fieldId);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        lines.Add($"{label}: raw XML review has {value.Length} character(s).");
    }

    private static string BuildImportStagedArtifactReceipt(IReadOnlyList<DesktopDialogField> fields)
    {
        string? selectedFile = ResolveFieldValue(fields, "selectedFile");
        if (string.IsNullOrWhiteSpace(selectedFile))
        {
            selectedFile = ResolveFieldValue(fields, "importFile");
        }

        string? openCharacterXml = ResolveFieldValue(fields, "openCharacterXml");
        if (string.IsNullOrWhiteSpace(openCharacterXml))
        {
            openCharacterXml = ResolveFieldValue(fields, "heroLabXml");
        }

        string rawXmlPosture = string.IsNullOrWhiteSpace(openCharacterXml)
            ? "raw XML review is empty"
            : $"raw XML review has {openCharacterXml.Length} character(s)";
        return BuildImportStagedArtifactReceipt(selectedFile, rawXmlPosture);
    }

    private static string BuildImportStagedArtifactReceipt(string? selectedFile, string rawXmlPosture)
        => $"staged file {Normalize(selectedFile, "selected import file")}; {rawXmlPosture}";

    private static string BuildDiagnosticsCorrelation(DesktopInstallLinkingState installState, DesktopUpdateClientStatus updateStatus)
        => $"{installState.HeadId}/{installState.Platform}/{installState.Arch}/{installState.ChannelId}/{installState.ApplicationVersion} -> {Normalize(updateStatus.HeadId, installState.HeadId)}/{Normalize(updateStatus.Platform, installState.Platform)}/{Normalize(updateStatus.Arch, installState.Arch)}/{Normalize(updateStatus.ChannelId, installState.ChannelId)}/{Normalize(updateStatus.LastManifestVersion, installState.ApplicationVersion)}";

    private static string BuildImportRuleEnvironmentSummary(WorkspacePortabilityReceipt receipt)
        => $"{Normalize(receipt.FormatId, "detected at import time")}; {Normalize(receipt.CompatibilityState, "compatibility pending")}";

    private static string BuildImportExplainReceiptSummary(WorkspacePortabilityReceipt receipt)
        => Normalize(receipt.ProvenanceSummary, Normalize(receipt.ReceiptSummary, "import explain receipt pending"));

    private static string BuildImportSupportReuse(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.PayloadSha256)
            ? Normalize(receipt.ProvenanceSummary, "support reuse pending")
            : $"Support can cite payload {receipt.PayloadSha256} with {Normalize(receipt.CompatibilityState, "compatibility pending")} compatibility.";

    private static string BuildImportBlockerSummary(WorkspacePortabilityReceipt receipt)
    {
        WorkspacePortabilityNote? note = receipt.Notes
            .FirstOrDefault(static item => !string.Equals(item.Severity, WorkspacePortabilityNoteSeverities.Info, StringComparison.OrdinalIgnoreCase));
        return note is null
            ? "no grounded import blocker is present before acceptance"
            : Normalize(note.Summary, "import blocker pending");
    }

    private static string BuildEnvironmentBeforeImportSummary(WorkspacePortabilityReceipt receipt)
        => string.IsNullOrWhiteSpace(receipt.ContextSummary)
            ? "current workspace, support posture, and source toggles stay unchanged while the import is staged"
            : receipt.ContextSummary;

    private static string BuildEnvironmentAfterImportSummary(WorkspacePortabilityReceipt receipt)
        => Normalize(receipt.NextSafeAction, "accepted content remains review-only until explicit confirmation");

    private static string ResolveCrashPlatform(DesktopCrashReport report)
        => Normalize(report.OperatingSystem, "unknown-platform");

    private static string ResolveRuntimeInspectorBlocker(RuntimeInspectorProjection projection)
    {
        RuntimeLockCompatibilityDiagnostic? diagnostic = projection.CompatibilityDiagnostics.FirstOrDefault(static item =>
            !string.Equals(item.State, RuntimeLockCompatibilityStates.Compatible, StringComparison.OrdinalIgnoreCase));
        if (diagnostic is not null && !string.IsNullOrWhiteSpace(diagnostic.Message))
        {
            return diagnostic.Message.Trim();
        }

        RuntimeInspectorWarning? warning = projection.Warnings.FirstOrDefault(static item =>
            string.Equals(item.Severity, RuntimeInspectorWarningSeverityLevels.Warning, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Severity, RuntimeInspectorWarningSeverityLevels.Error, StringComparison.OrdinalIgnoreCase));
        return warning is not null && !string.IsNullOrWhiteSpace(warning.Message)
            ? warning.Message.Trim()
            : "no blocker recorded";
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeReceiptToken(string? value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "pending" : value.Trim();
        char[] chars = normalized
            .ToLowerInvariant()
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        string token = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(token) ? "pending" : token;
    }
}
