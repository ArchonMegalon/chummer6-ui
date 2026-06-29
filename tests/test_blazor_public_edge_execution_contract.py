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


def test_promoted_workbench_surfaces_startup_command_display_labels() -> None:
    preview = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")

    assert 'data-chummer-app-startup-command="@StartupCommandLabel"' in preview
    assert "<strong>@StartupCommandDisplayLabel</strong>" in preview
    assert "private string StartupCommandDisplayLabel" in preview
    assert 'NewCharacterCommand => "New runner"' in preview
    assert 'NewCharacterOriginCommand => "Origin Dossier"' in preview
    assert 'OpenForPrintingCommand => "Open for Printing"' in preview
    assert 'OpenForExportCommand => "Open for Export"' in preview
