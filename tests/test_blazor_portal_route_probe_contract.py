from pathlib import Path


def test_portal_route_probe_uses_stable_handoff_markers_for_help_status_downloads_and_contact() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")

    assert "function hasPortalChrome(text)" in script
    assert "text.includes('data-portal-help-context=')" in script
    assert "text.includes('data-portal-status-action=\"open-discord\"')" in script
    assert "text.includes('data-download-action=\"open-status\"')" in script
    assert "text.includes('data-install-route-action=\"open-proof-required-route\"')" in script
    assert "text.includes('data-portal-contact-public-route=')" in script
    assert "text.includes('data-portal-contact-action=\"open-discord\"')" in script


def test_portal_home_exposes_the_contact_handoff_required_by_the_route_probe() -> None:
    program = Path("Chummer.Portal/Program.cs").read_text(encoding="utf-8")
    home_renderer = program.split("static string BuildPortalHomeHtml", 1)[1].split(
        "static string BuildDownloadsHtml", 1
    )[0]

    assert '<a href="/contact" data-portal-home-route="contact">Contact support</a>' in home_renderer


def test_portal_route_probe_no_longer_depends_on_stale_copy_for_minimal_portal_surfaces() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")

    assert "data-portal-help-context=\"self-host-first\"" not in script
    assert "text.includes('Current release')" not in script
    assert "text.includes('Desktop Downloads')" not in script
    assert "text.includes('Explore Chummer Online instead')" not in script
    assert "data-portal-contact-scenarios=\"installer-account-app\"" not in script


def test_portal_route_probe_verifies_public_online_alias_redirects_into_hosted_app_contract() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")

    assert "url: `${baseUrl}/online?command=character_roster`," in script
    assert "/\\/blazor\\/app\\/?\\?command=character_roster$/.test(response.url)" in script
    assert "typeof payload?.paths?.['/online'] === 'object'" in script


def test_portal_route_probe_blazor_root_resolves_to_the_character_roster_app() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")
    program = Path("Chummer.Portal/Program.cs").read_text(encoding="utf-8")
    runbook = Path("docs/BLAZOR_SELF_HOST_RUNBOOK.md").read_text(encoding="utf-8")
    blazor_root_check = script.split("url: `${baseUrl}/blazor/`,", 1)[1].split("  },", 1)[0]

    assert "app.MapGet(blazorHomeRoute, () => Results.Redirect(BuildBlazorAppUrl(options)));" in program
    assert "`/blazor/` resolves into `/blazor/app`" in runbook
    assert "assert: (text, response) =>" in blazor_root_check
    assert "/\\/blazor\\/app\\/?$/.test(response.url)" in blazor_root_check
    assert "/<base href=\"[^\"]*\\/blazor\\/\"/i.test(text)" in blazor_root_check
    assert "text.includes('data-route-family=\"app\"')" in blazor_root_check
    assert "text.includes('data-route-surface=\"roster\"')" in blazor_root_check
    assert "text.includes('data-active-workflow=\"character-roster\"')" in blazor_root_check
    assert "text.includes('Character Roster')" in blazor_root_check
    assert "Browser preview is not ready right now." not in blazor_root_check
    assert "The downloadable Chummer client is the current stable path." not in blazor_root_check


def test_portal_playwright_contract_tracks_current_dossier_facing_workbench_markers() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "expectTextIncludes(bodyText, 'Import dossier XML', 'portal workbench route');" in script
    assert "expectTextIncludes(bodyText, 'Saved Dossiers', 'portal workbench route');" in script
    assert "expectTextIncludes(bodyText, 'Active Table', 'portal workbench route');" in script
    assert "'[data-route-family=\"app\"][data-route-surface=\"roster\"][data-active-workflow=\"character-roster\"]'" in script
    assert "/\\/blazor\\/app\\/?$/.test(page.url())" in script
    assert "'portal blazor root character roster surface'" in script
    assert "expectTextIncludes(bodyText, 'Character Roster', 'portal blazor root route');" in script
    assert "Browser preview is not ready right now." not in script
    assert "The downloadable Chummer client is the current stable path." not in script
    assert "Import an existing dossier" not in script
    assert "Import runner XML" not in script
    assert "No recent dossiers yet" not in script
    assert "Continue a recent dossier" not in script
    assert "Saved Runners" not in script
    assert "Active Dossier" not in script


def test_portal_playwright_career_reorder_route_no_longer_references_missing_marker_variable() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "async function auditPortalRestoredCareerEntryReorderRoute(page, controlId, expectedTitle)" in script
    function_block = script.split(
        "async function auditPortalRestoredCareerEntryReorderRoute(page, controlId, expectedTitle) {",
        1,
    )[1].split("async function auditPortalRestoredMagicCleanupUtilityRoute", 1)[0]
    assert "expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined" not in function_block


def test_portal_playwright_retries_transient_navigation_abortions_on_self_host_routes() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "const routeNavigationRetryAttempts = Number(process.env.CHUMMER_PORTAL_ROUTE_RETRY_ATTEMPTS || '3');" in script
    assert "const routeNavigationRetryDelayMs = Number(process.env.CHUMMER_PORTAL_ROUTE_RETRY_DELAY_MS || '1500');" in script
    assert "function shouldRetryRouteNavigation(error)" in script
    assert "message.includes('ERR_ABORTED') || message.includes('Timeout')" in script
    assert "async function openPortalRoute(page, route, readySelector, waitUntilOverride)" in script
    assert "await page.goto('about:blank', { waitUntil: 'load', timeout: 5000 }).catch(() => {});" in script
    assert "await page.waitForTimeout(routeNavigationRetryDelayMs);" in script


def test_portal_playwright_supports_smoke_and_full_scopes_for_self_host_gating() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "const playwrightScope = (process.env.CHUMMER_PORTAL_PLAYWRIGHT_SCOPE || 'smoke').trim().toLowerCase();" in script
    assert "const smokeAudits = [" in script
    assert "const fullOnlyAudits = [" in script
    assert "portal playwright scope: ${normalizedScope}" in script
    assert "if (normalizedScope === 'full')" in script


def test_portal_playwright_origin_dossier_audit_measures_story_preview_contrast() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "async function expectMinimumTextContrast(page, selector, minimumRatio, context)" in script
    assert "async function expectVisibleCollectionMinimumTextContrast(page, selector, minimumRatio, minimumMatches, context)" in script
    assert "async function expectDialogFits(page, expectedTitle, expectedFallback)" in script
    assert "const expected = String(payload?.expected || '');" in script
    assert "const fallback = String(payload?.fallback || '');" in script
    assert "expected: expectedTitle.toLowerCase()," in script
    assert "fallback: expectedFallback ? expectedFallback.toLowerCase() : ''" in script
    assert "}, { query: selector, requiredMatches: minimumMatches }, { timeout: 15000 });" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-story-preview] .dialog-origin-narrative p', 4.5, 1, 'portal origin dossier story preview');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-story-preview] .dialog-origin-narrative p', 4.5, 1, 'portal origin dossier deep-link story preview');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-panel > header p', 4.5, 2, 'portal origin dossier helper copy');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-label', 4.5, 3, 'portal origin dossier summary labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin dossier summary values');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-panel > header p', 4.5, 2, 'portal origin dossier deep-link helper copy');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-label', 4.5, 3, 'portal origin dossier deep-link summary labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin dossier deep-link summary values');" in script
    assert "async function auditPortalOriginBuildDeepLink(page)" in script
    assert "'/blazor/preview?command=new_character_origin&dialog_action=generate_fitting_build'" in script
    assert "'[data-origin-build]'" in script
    assert "await expectDialogFits(page, 'origin build handoff', 'build handoff');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-panel > header p', 4.5, 3, 'portal origin build helper copy');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-summary-label', 4.5, 3, 'portal origin build summary labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin build summary values');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-book-preview] .dialog-origin-readonly p', 4.5, 2, 'portal origin build book preview');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-preview .dialog-origin-narrative p', 4.5, 1, 'portal origin build story preview');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build-support] .dialog-visual-pre', 4.5, 2, 'portal origin build supporting previews');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 2, 'portal origin dossier labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 2, 'portal origin dossier inputs');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 3, 'portal new character labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 3, 'portal new character inputs');" in script
    assert "Expected ${context} to keep text contrast >=" in script
