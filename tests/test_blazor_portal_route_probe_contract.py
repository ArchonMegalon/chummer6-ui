from pathlib import Path


def test_portal_route_probe_uses_stable_handoff_markers_for_help_status_downloads_and_contact() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")

    assert "function hasPortalChrome(text)" in script
    assert "function assertNamedRequirements(context, requirements)" in script
    assert "assertNamedRequirements('Blazor root app redirect'" in script
    assert "assertNamedRequirements('Blazor interactive home bootstrap'" in script
    assert "response.url.includes('/blazor/app')" in script
    assert "response.url.includes('command=character_roster')" in script
    assert "'contact route marker': text.includes('data-portal-home-route=\"contact\"')" in script
    assert "text.includes('data-portal-help-context=')" in script
    assert "text.includes('data-portal-status-action=\"open-discord\"')" in script
    assert "text.includes('data-download-action=\"open-status\"')" in script
    assert "text.includes('data-install-route-action=\"open-proof-required-route\"')" in script
    assert "text.includes('data-portal-contact-public-route=')" in script
    assert "text.includes('data-portal-contact-action=\"open-discord\"')" in script


def test_portal_landing_page_exposes_first_class_contact_handoff() -> None:
    source = Path("Chummer.Portal/Program.cs").read_text(encoding="utf-8")

    assert 'href="/contact" data-portal-home-route="contact"' in source
    assert ">Contact support</a>" in source


def test_portal_blazor_root_is_owned_only_by_the_reverse_proxy() -> None:
    source = Path("Chummer.Portal/Program.cs").read_text(encoding="utf-8")

    assert "app.MapGet(blazorHomeRoute" not in source
    assert 'MapPassThroughProxy(app, "/blazor/{**catchall}", options.BlazorProxyUrl);' in source
    assert "app.MapGet(PortalRoutes.PublicApp" in source


def test_blazor_server_owns_root_redirect_and_home_remains_orientation_route() -> None:
    home = Path("Chummer.Blazor/Components/Pages/Home.razor").read_text(encoding="utf-8")
    program = Path("Chummer.Blazor/Program.cs").read_text(encoding="utf-8")

    assert '@page "/home"' in home
    assert '@page "/"' not in home
    assert "Navigation.NavigateTo" not in home
    assert 'string rootAppRoute = $"{pathBase.Value}/app?command={CharacterRosterCommand}";' in program
    assert 'MapGet("/", () => Results.Redirect(rootAppRoute))' in program


def test_portal_route_probe_no_longer_depends_on_stale_copy_for_minimal_portal_surfaces() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")

    assert "data-portal-help-context=\"self-host-first\"" not in script
    assert "text.includes('Current release')" not in script
    assert "text.includes('Desktop Downloads')" not in script
    assert "text.includes('Explore Chummer Online instead')" not in script
    assert "data-portal-contact-scenarios=\"installer-account-app\"" not in script


def test_portal_route_probe_rejects_new_character_app_route_roster_fallback() -> None:
    script = Path("scripts/e2e-portal.cjs").read_text(encoding="utf-8")

    assert "url: `${baseUrl}/blazor/app?command=new_character`," in script
    assert "text.includes('data-active-workflow=\"build-lab\"')" in script
    assert "text.includes('data-command=\"new-character\"')" in script
    assert "text.includes('data-chummer-app-startup-command=\"new_character\"')" in script
    assert "text.includes('data-app-route-shared-shell=\"true\"')" in script
    assert "text.includes('Build Lab shell')" in script
    assert "!text.includes('Your runners will appear here.')" in script


def test_portal_playwright_contract_uses_runner_shell_language_instead_of_stale_dossier_copy() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")
    preview = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")

    assert "expectTextIncludes(bodyText, 'Import runner XML', 'portal workbench route');" in script
    assert "expectTextIncludes(bodyText, 'Saved Runners', 'portal workbench route');" in script
    assert "expectTextIncludes(bodyText, 'Active Table', 'portal workbench route');" in script
    assert "Expected portal /blazor/ root to resolve to /blazor/app" in script
    assert "expectTextIncludes(bodyText, 'Character Roster', 'portal blazor root app route');" in script
    assert "async function auditPortalBlazorHome(page)" in script
    assert "expectTextIncludes(bodyText, 'Chummer Online for real runner work.', 'portal Blazor home route');" in script
    assert "{ fn: auditPortalBlazorHome }" in script
    assert 'data-preview-session-posture="implicit-owner"' in preview
    assert "expectVisibleSelector(page, '[data-preview-session-posture=\"implicit-owner\"]'" in script
    assert "implicit owner session posture', 'portal desktop preview" not in script
    assert "Browser preview is not ready right now." not in script
    assert "Import an existing dossier" not in script
    assert "No recent dossiers yet" not in script
    assert "Continue a recent dossier" not in script
    assert "Active Dossier" not in script


def test_portal_playwright_uses_current_build_pwa_workspace_readiness_contract() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert (
        'const seededBuildLabReadySelector = '
        '\'.build-pwa-workspace[data-active-builder-section="tab-create"]\';'
    ) in script
    assert "await expectVisibleSelector(page, seededBuildLabReadySelector, 'portal build lab workspace');" in script
    assert "'[data-nav-tab=\"tab-create\"][aria-current=\"step\"]'" in script
    assert "expectTextIncludes(bodyText, 'Build Lab', 'portal seeded build lab');" in script
    assert "[data-build-lab]" not in script
    assert "Build Lab Intake" not in script


def test_portal_playwright_career_reorder_route_no_longer_references_missing_marker_variable() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "async function auditPortalRestoredCareerEntryReorderRoute(page, controlId, expectedTitle)" in script
    function_block = script.split(
        "async function auditPortalRestoredCareerEntryReorderRoute(page, controlId, expectedTitle) {",
        1,
    )[1].split("async function auditPortalRestoredMagicCleanupUtilityRoute", 1)[0]
    assert "expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined" not in function_block


def test_portal_playwright_career_entry_route_waits_for_the_interactive_editor() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    function_block = script.split(
        "async function auditPortalRestoredCareerEntryActionRoute(page) {",
        1,
    )[1].split("async function auditPortalRestoredCareerEntryEditRoute", 1)[0]
    assert "await expectDialogFits(page, 'add entry', 'add a new entry');" in function_block


def test_portal_playwright_complex_form_route_waits_for_the_interactive_catalog() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    function_block = script.split(
        "async function auditPortalRestoredComplexFormActionRoute(page) {",
        1,
    )[1].split("async function auditPortalRestoredInitiationAddCommitRoute", 1)[0]
    assert "await expectDialogFits(page, 'add complex form', 'cleaner');" in function_block


def test_portal_playwright_remaining_catalog_routes_wait_for_interactive_content() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "await expectDialogFits(page, 'add initiation', 'masking');" in script
    assert "await expectDialogFits(page, 'add cyberware', 'wired reflexes 2');" in script
    assert "await expectDialogFits(page, 'add spell', 'stunbolt');" in script


def test_portal_playwright_retries_transient_navigation_abortions_on_self_host_routes() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "const routeNavigationRetryAttempts = Number(process.env.CHUMMER_PORTAL_ROUTE_RETRY_ATTEMPTS || '3');" in script
    assert "const routeNavigationRetryDelayMs = Number(process.env.CHUMMER_PORTAL_ROUTE_RETRY_DELAY_MS || '1500');" in script
    assert "function shouldRetryRouteNavigation(error)" in script
    assert "message.includes('ERR_ABORTED')" in script
    assert "message.includes('ERR_NETWORK_CHANGED')" in script
    assert "message.includes('Timeout')" in script
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


def test_portal_playwright_new_character_audit_reopens_dialog_from_file_menu() -> None:
    script = Path("scripts/e2e-portal-playwright.cjs").read_text(encoding="utf-8")

    assert "async function expectNewRunnerMenuReopensDialog(page, context)" in script
    assert "label[data-field-id=\"newCharacterBuildMethod\"] select" in script
    assert "button.menu-btn.classic-menu-button" in script
    assert "button.menu-item.classic-menu-item" in script
    assert "await page.locator('#dialogClose').click({ timeout: 15000 });" in script
    assert "await page.locator('#dialogBackdrop').waitFor({ state: 'detached', timeout: 15000 });" in script
    assert "await page.locator('.menu-dropdown.classic-menu-dropdown').waitFor({ state: 'visible', timeout: 15000 });" in script
    assert "Expected ${context} File menu to expand after closing the startup dialog" in script
    assert "Expected ${context} File -> New runner to reopen the startup dialog with Priority selected" in script
    assert "await expectNewRunnerMenuReopensDialog(page, 'portal new character dialog');" in script
    assert "await expectNewRunnerMenuReopensDialog(page, 'portal new character deep link');" in script


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
    assert "await expectDialogFits(page, 'origin dossier', 'build handoff');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-panel > header p', 4.5, 3, 'portal origin build helper copy');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-summary-label', 4.5, 3, 'portal origin build summary labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin build summary values');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-book-preview] .dialog-origin-readonly p', 4.5, 1, 'portal origin build book preview');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-preview .dialog-origin-narrative p', 4.5, 1, 'portal origin build story preview');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build-support] .dialog-visual-pre', 4.5, 2, 'portal origin build supporting previews');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 2, 'portal origin dossier labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 2, 'portal origin dossier inputs');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 3, 'portal new character labels');" in script
    assert "await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 3, 'portal new character inputs');" in script
    assert "Expected ${context} to keep text contrast >=" in script
