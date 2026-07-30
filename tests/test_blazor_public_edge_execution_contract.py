import ast
import re
from pathlib import Path


def _extract_js_array(source: str, name: str) -> set[str]:
    match = re.search(rf"const {re.escape(name)} = \[(.*?)\];", source, re.DOTALL)
    assert match, f"missing JS array {name}"
    return set(re.findall(r"'([^']+)'", match.group(1)))


def _extract_python_set(source: str, name: str) -> set[str]:
    match = re.search(rf"{re.escape(name)} = (\{{.*?\}})", source, re.DOTALL)
    assert match, f"missing Python set {name}"
    parsed = ast.literal_eval(match.group(1))
    return set(parsed)


def test_public_edge_execution_runner_defaults_to_smoke_scope_and_keeps_full_lane_available() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "const playwrightScope = (process.env.CHUMMER_PUBLIC_EDGE_PLAYWRIGHT_SCOPE || 'smoke').trim().toLowerCase();" in script
    assert "const availableWorkflowFamilyIds = [" in script
    assert "const smokeRequiredWorkflowFamilyIds = [" in script
    assert "function normalizePlaywrightScope()" in script
    assert "return playwrightScope === 'full' ? 'full' : 'smoke';" in script
    assert "console.log(`public-edge playwright scope: ${normalizedScope}`);" in script
    assert "if (normalizedScope === 'full') {" in script

    available = _extract_js_array(script, "availableWorkflowFamilyIds")
    smoke = _extract_js_array(script, "smokeRequiredWorkflowFamilyIds")

    assert smoke < available
    assert "promoted_advanced_committed_actions" in available
    assert "promoted_advanced_committed_actions" not in smoke
    assert "promoted_combat_support_execution" in available
    assert "promoted_identity_license_execution" in available
    assert smoke == {
        "promoted_startup_command_executions",
        "promoted_dense_tool_surfaces",
        "promoted_origin_rules_continuity",
        "promoted_build_lab_continuity",
        "promoted_resumed_workspace",
        "promoted_result_continuations",
        "promoted_action_continuations",
        "promoted_committed_actions",
        "promoted_advanced_action_executions",
    }


def test_public_edge_workbench_refresh_wrapper_materializes_before_verification() -> None:
    wrapper = Path(
        "scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh"
    ).read_text(encoding="utf-8")
    flagship_gate = Path(
        "scripts/ai/milestones/b14-flagship-ui-release-gate.sh"
    ).read_text(encoding="utf-8")

    assert 'CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_REFRESH:-0' in wrapper
    assert "materialize-external-host-proof-blockers.py" in wrapper
    assert "--browser-proof-output" in wrapper
    assert "verify_blazor_public_edge_workbench_proof.py" in wrapper
    assert "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_REFRESH=1" in flagship_gate


def test_public_edge_execution_verifier_accepts_scope_specific_required_workflow_sets() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")
    verifier = Path("scripts/verify_blazor_public_edge_execution_proof.py").read_text(encoding="utf-8")

    assert 'SUPPORTED_PLAYWRIGHT_SCOPES = {"smoke", "full"}' in verifier
    assert 'AVAILABLE_WORKFLOW_FAMILY_IDS = {' in verifier
    assert 'FULL_ONLY_WORKFLOW_FAMILY_IDS = {' in verifier
    assert 'SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS = {' in verifier
    assert 'def required_workflow_family_ids_for_scope(scope: str) -> set[str]:' in verifier
    assert 'if scope == "full":' in verifier
    assert 'return set(SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS)' in verifier
    assert 'playwright_scope must be one of' in verifier
    assert 'required_workflow_family_ids mismatch for scope' in verifier
    assert 'missing required workflow families for ' in verifier

    assert _extract_python_set(verifier, "AVAILABLE_WORKFLOW_FAMILY_IDS") == _extract_js_array(
        script,
        "availableWorkflowFamilyIds",
    )
    assert _extract_python_set(verifier, "SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS") == _extract_js_array(
        script,
        "smokeRequiredWorkflowFamilyIds",
    )


def test_public_edge_execution_runner_reuses_rewritten_fixture_workspace() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "let promotedContinuationQuery = 'runner=blue';" in script
    assert "let promotedContinuationQuery = 'workspace=ws-1';" not in script
    assert "function continuationQueryFromUrl(urlString)" in script
    assert "function isReusableContinuationQuery(query)" in script
    assert "return query.startsWith('workspace=') || (query.startsWith('runner=') && query !== 'runner=blue');" in script
    assert "if (isReusableContinuationQuery(promotedContinuationQuery))" in script
    assert "return `workspace=${workspaceId}`;" in script
    assert "return `fixture=${fixtureId}`;" in script
    assert "await openPath(page, `${promotedRouteBase}?fixture=${encodeURIComponent(fixtureId)}`, 'section.classic-chummer-shell');" in script
    assert "new URL(window.location.href).searchParams.has('workspace')" in script
    assert "const rewrittenQuery = continuationQueryFromUrl(page.url());" in script
    assert "if (rewrittenQuery && isReusableContinuationQuery(rewrittenQuery))" in script
    assert "const resolvedQuery = continuationQueryFromUrl(resolvedHref);" in script


def test_public_edge_execution_runner_waits_for_result_continuation_text() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "async function waitForBodyTextIncludes(page, expected, label)" in script
    assert "document.body.innerText.includes(expectedText)" in script
    assert "await waitForBodyTextIncludes(page, expectedText, `hosted resumed result route ${route}`);" in script
    assert "await openPath(page, route, 'section.classic-chummer-shell');" in script
    assert "await waitForBodyTextIncludes(page, \"Entry 'New entry' added.\", 'hosted committed action route');" in script
    assert "await waitForBodyTextIncludes(page, expectedText, `hosted advanced committed action route ${route}`);" in script


def test_public_edge_execution_runner_preserves_published_receipt_on_live_failure() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "const failedOutputPath = process.env.CHUMMER_PUBLIC_EDGE_FAILED_EXECUTION_PROOF_PATH" in script
    assert "'.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json'" in script
    assert "'BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.failed.generated.json'" in script
    assert "const writeFailedProofToOutput = process.env.CHUMMER_PUBLIC_EDGE_WRITE_FAILED_PROOF_TO_OUTPUT === '1';" in script
    assert "receipt.status === 'passed' || writeFailedProofToOutput ? outputPath : failedOutputPath" in script
    assert "public-edge execution failed; wrote failure receipt" in script
    assert "left published receipt unchanged" in script


def test_public_edge_execution_runner_retries_network_changed_navigation() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "function shouldRetryRouteNavigation(error)" in script
    assert "message.includes('ERR_ABORTED')" in script
    assert "message.includes('ERR_NETWORK_CHANGED')" in script
    assert "message.includes('Timeout')" in script


def test_public_edge_execution_runner_defaults_to_managed_chromium() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "function browserLaunchOptions()" in script
    assert "process.env.CHUMMER_PLAYWRIGHT_EXECUTABLE_PATH" in script
    assert "process.env.CHUMMER_PLAYWRIGHT_CHANNEL || 'chromium'" in script
    assert "return { headless: true, executablePath };" in script
    assert "return channel ? { headless: true, channel } : { headless: true };" in script
    assert "chromium.launch(browserLaunchOptions())" in script


def test_blazor_route_host_publishes_immediate_public_shell_fallbacks() -> None:
    app = Path("Chummer.Blazor/Components/App.razor").read_text(encoding="utf-8")

    assert '<Routes @rendermode="new InteractiveServerRenderMode(prerender: false)" />' in app
    assert "BuildAppRouteFallback()" in app
    assert 'data-ssr-app-route-fallback="true"' in app
    assert 'data-active-workflow="@appRouteFallback.ActiveWorkflow"' in app
    assert 'data-route-family="@appRouteFallback.RouteFamily"' in app
    assert 'data-canonical-route="@appRouteFallback.CanonicalRoute"' in app
    assert 'data-route-alias="@appRouteFallback.RouteAlias"' in app
    assert 'data-command="@appRouteFallback.CommandDataKey"' in app
    assert 'data-chummer-app-startup-command="@appRouteFallback.StartupCommandLabel"' in app
    assert 'data-control="@appRouteFallback.ControlDataKey"' in app
    assert 'data-dialog-action="@appRouteFallback.DialogActionDataKey"' in app
    assert 'data-fixture="@appRouteFallback.FixtureDataKey"' in app
    assert 'data-legacy-runner="@appRouteFallback.LegacyRunnerDataKey"' in app
    assert 'data-app-route-shared-shell="true"' in app
    assert "private static string BuildAppHref(" in app
    assert "private static string BuildAppContinuationHref(" in app
    assert "private static string BuildAppSeedHref(" in app
    assert "private static string BuildAppNewRunnerHref(" in app
    assert "private static string BuildWorkbenchNewRunnerHref(WorkbenchFallback fallback)" in app
    assert 'HasExplicitWorkspace: !string.IsNullOrWhiteSpace(requestedWorkspace)' in app
    assert 'string control = NormalizeDataToken(GetQueryValue(query, "control"));' in app
    assert 'string dialogAction = NormalizeDataToken(GetQueryValue(query, "dialog_action"));' in app
    assert 'string runner = NormalizeDataToken(GetQueryValue(query, "runner"));' in app
    assert 'FixtureDataKey: NormalizeRouteDataKey(fixture)' in app
    assert 'LegacyRunnerDataKey: NormalizeRouteDataKey(runner)' in app
    assert 'ControlDataKey: NormalizeRouteDataKey(control)' in app
    assert 'DialogActionDataKey: NormalizeRouteDataKey(dialogAction)' in app
    assert 'fixture: fallback.Fixture' in app
    assert 'href="@BuildAppNewRunnerHref(appRouteFallback)"' in app
    assert 'href="@BuildAppContinuationHref(appRouteFallback, tab: "tab-create")"' in app
    assert 'href="@BuildAppSeedHref(appRouteFallback, tab: "tab-technomancer")"' in app
    assert 'string.Equals(fallback.Command, "new_character", StringComparison.OrdinalIgnoreCase)' in app
    assert "ShouldRenderAppFallbackStartupShell(query, command)" in app
    assert 'RouteFamily: isOnlineAliasRoute ? "online-alias" : "app"' in app
    assert 'CanonicalRoute: "app"' in app
    assert 'RouteAlias: isOnlineAliasRoute ? "online" : "none"' in app
    assert 'string.Equals(tab, "tab-contacts", StringComparison.OrdinalIgnoreCase)' in app
    assert 'string.Equals(tab, "tab-rules", StringComparison.OrdinalIgnoreCase)' in app
    assert '"contacts" => "Contacts"' in app
    assert '"rules" => "Rules"' in app
    assert '"master-index" => "Master Index"' in app
    assert '"Search the catalog, inspect the selected reference, and keep the current source visible."' in app
    assert '"Linked PDF / URL"' in app
    assert '"Use Setting"' in app
    assert '"Search the spell list, inspect source and drain, then confirm the learned spell."' in app
    assert '"Available Spells"' in app
    assert '"Selection Details"' in app
    assert '"Add a new entry while keeping the compact list/detail editor visible."' in app
    assert '"Entry creation and editing stay compact and preserve list context."' in app
    assert '"tab-calendar" => "Career Log"' in app
    assert 'aria-label="Career log actions"' in app
    assert 'tab: "tab-calendar", control: "create_entry")">Add Entry</a>' in app
    assert '"Search, filter, keep source/cost/essence details visible, and confirm the selected implant."' in app
    assert '"Available Cyberware"' in app
    assert '"Catalog Grid"' in app
    assert '"Filter Summary"' in app
    assert '"global-settings" => "Global Settings"' in app
    assert '"build-lab" => "Build Lab shell"' in app
    assert "section.browser-app-roster:not([data-ssr-app-route-fallback])" in app
    assert 'href="@BuildWorkbenchHref(appRouteFallback.Workspace, command: "new_character")"' not in app
    assert "Character Roster" in app
    assert "Open example" in app
    assert "BuildWorkbenchFallback()" in app
    assert 'data-ssr-workbench-fallback="true"' in app
    assert 'class="classic-chummer-menu browser-app-classic-menu-bar"' in app
    assert 'data-app-menu-root="file"' in app
    assert 'data-app-menu-summary="file"' in app
    assert 'href="@BuildWorkbenchNewRunnerHref(workbenchFallback)"' in app
    assert 'string.Equals(fallback.Command, "new_character", StringComparison.OrdinalIgnoreCase)' in app
    assert "section.classic-chummer-shell:not([data-ssr-workbench-fallback])" in app
    assert "Complex form 'Cleaner' added." in app


def test_promoted_workbench_surfaces_startup_command_display_labels() -> None:
    preview = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")

    assert 'data-chummer-app-startup-command="@StartupCommandLabel"' in preview
    assert "<strong>@StartupCommandDisplayLabel</strong>" in preview
    assert "private string StartupCommandDisplayLabel" in preview
    assert '"new-character" => "New runner"' in preview
    assert '"new-character-origin" => "Origin Dossier"' in preview
    assert '"open-for-printing" => "Open for Printing"' in preview
    assert '"open-for-export" => "Open for Export"' in preview


def test_workbench_fallback_uses_click_safe_menu_roots_and_non_self_new_runner_links() -> None:
    preview = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")

    assert "private string WorkbenchNewRunnerHref" in preview
    assert "if (!IsWorkbenchRoute)" in preview
    assert "return BuildPreviewHref(command: NewCharacterCommand, useWorkbenchRoute: true);" in preview
    assert 'string? tabId = workspaceId is not null || IsCommandAlias(Command, "new-character")' in preview
    assert "command: NewCharacterCommand" in preview
    assert 'class="classic-chummer-menu browser-app-classic-menu-bar"' in preview
    assert 'class="classic-menu-item browser-app-classic-menu-root" data-app-menu-root="file"' in preview
    assert '<summary role="button" tabindex="0" aria-expanded="false" data-app-menu-summary="file">File</summary>' in preview
    assert 'class="classic-menu-flyout browser-app-classic-menu-flyout" role="menu" data-app-menu-flyout="file"' in preview
    assert 'href="@WorkbenchNewRunnerHref"' in preview
    assert 'data-app-menu-item="new-runner"' in preview
    assert 'data-classic-toolstrip-action="new-runner"' in preview
    assert 'data-workbench-dock-action="start-new"' in preview
    assert 'data-workbench-command-palette-action="new-character"' in preview
