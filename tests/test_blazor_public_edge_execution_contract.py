from pathlib import Path


def test_public_edge_execution_runner_defaults_to_smoke_scope_and_keeps_full_lane_available() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "const playwrightScope = (process.env.CHUMMER_PUBLIC_EDGE_PLAYWRIGHT_SCOPE || 'smoke').trim().toLowerCase();" in script
    assert "const availableWorkflowFamilyIds = [" in script
    assert "const smokeRequiredWorkflowFamilyIds = availableWorkflowFamilyIds.filter(id => id !== 'promoted_advanced_committed_actions');" in script
    assert "function normalizePlaywrightScope()" in script
    assert "return playwrightScope === 'full' ? 'full' : 'smoke';" in script
    assert "console.log(`public-edge playwright scope: ${normalizedScope}`);" in script
    assert "if (normalizedScope === 'full') {" in script


def test_public_edge_execution_verifier_accepts_scope_specific_required_workflow_sets() -> None:
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
