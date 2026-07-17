from __future__ import annotations

from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[1]
COMPONENT = REPO_ROOT / "Chummer.Blazor" / "Components" / "Shell" / "BuildPwaWorkspace.razor"
DESKTOP_SHELL = REPO_ROOT / "Chummer.Blazor" / "Components" / "Layout" / "DesktopShell.razor"
DESKTOP_SHELL_CODE = REPO_ROOT / "Chummer.Blazor" / "Components" / "Layout" / "DesktopShell.razor.cs"
APP = REPO_ROOT / "Chummer.Blazor" / "Components" / "App.razor"
CSS = REPO_ROOT / "Chummer.Blazor" / "wwwroot" / "app.css"
LAYOUT_SCRIPT = REPO_ROOT / "Chummer.Blazor" / "wwwroot" / "js" / "build-pwa-layout.js"
BROWSER_TEST = REPO_ROOT / "scripts" / "e2e-build-pwa-responsive-playwright.cjs"
BROWSER_RUNNER = REPO_ROOT / "scripts" / "run-build-pwa-responsive-e2e.sh"
INSTALL_CSS = REPO_ROOT / "Chummer.Blazor" / "wwwroot" / "build-pwa-install.css"
INSTALL_PANEL = REPO_ROOT / "Chummer.Blazor" / "Components" / "Shell" / "BuildPwaInstallPanel.razor"
INSTALL_SCRIPT = REPO_ROOT / "Chummer.Blazor" / "wwwroot" / "js" / "build-pwa-install.js"
INTEGRITY_BROWSER_TEST = REPO_ROOT / "scripts" / "e2e-build-pwa-integrity-playwright.cjs"
RUNTIME_TEST = REPO_ROOT / "tests" / "test_blazor_pwa_runtime_surface.py"
INTEROP_DEADLINE = REPO_ROOT / "Chummer.Blazor" / "Services" / "RecoveryInteropDeadlineRuntime.cs"


def test_build_pwa_uses_one_shared_editor_and_shared_shell_callbacks() -> None:
    component = COMPONENT.read_text(encoding="utf-8")
    shell = DESKTOP_SHELL.read_text(encoding="utf-8")
    shell_code = DESKTOP_SHELL_CODE.read_text(encoding="utf-8")

    assert component.count("<SectionPane ") == 1
    assert 'data-build-pwa-layout-source="browser-measured-geometry"' in component
    assert component.count('id="chummer-workspace-main"') == 1
    assert '<SummaryHeader State="State"' in component
    assert 'SelectTabRequested.InvokeAsync(tab.Id)' in component
    assert 'ExecuteCommandRequested.InvokeAsync(command.Id)' in component
    assert 'ExecuteWorkspaceActionRequested.InvokeAsync(action)' in component
    assert '<h1 id="build-pwa-compact-title">' in component
    assert '<BuildPwaWorkspace State="@State"' in shell
    assert 'SelectTabRequested="@SelectTabAsync"' in shell
    assert 'ExecuteUiControlRequested="@HandleUiControlAsync"' in shell
    assert 'AttributeEditRequested="@HandleAttributeEditAsync"' in shell
    assert 'InspectRuntimeRequested="@OpenRuntimeInspectorAsync"' in shell
    assert 'MobileCommands="@HeadCommands.ToArray()"' in shell
    assert 'tabindex="0"' not in shell
    assert "_shellRoot.FocusAsync()" not in shell_code
    assert "UseResponsiveBuildWorkspace" in shell_code


def test_browser_width_drives_distinct_workspace_and_compact_layouts() -> None:
    css = CSS.read_text(encoding="utf-8")
    script = LAYOUT_SCRIPT.read_text(encoding="utf-8")

    assert '@media (max-width: 59.999rem)' in css
    assert 'grid-template-areas: "steps editor summary";' in css
    assert '"context"\n            "steps"\n            "editor"\n            "summary"' in css
    assert '.build-pwa-summary-rail {' in css
    assert 'position: sticky;' in css
    assert '.build-pwa-mobile-dock {' in css
    assert 'position: fixed;' in css
    assert 'min-height: 2.75rem;' in css
    assert '@media (prefers-reduced-motion: reduce)' in css
    assert '@media (prefers-contrast: more)' in css
    assert '@media (forced-colors: active)' in css
    assert '@media (any-pointer: coarse)' in css
    assert "const compactQuery = '(max-width: 59.999rem)';" in script
    assert "--build-pwa-workspace-minimum-inline-size: 60.7rem;" in css
    assert "min-width: var(--build-pwa-workspace-minimum-inline-size);" in css
    assert "function ensureWorkspaceMeasurementProbe(workspace)" in script
    assert ".getPropertyValue('--build-pwa-workspace-minimum-inline-size')" in script
    assert "const availableInlineSize = shell.clientWidth;" in script
    assert "probe?.getBoundingClientRect().width" in script
    assert "return workspaceFits ? 'workspace' : 'compact';" in script
    assert "compactMedia.addEventListener('change', onBrowserLayoutChange)" in script
    assert "const onBrowserLayoutChange = scheduleApply;" in script
    assert "new window.ResizeObserver" in script
    assert "window.addEventListener('resize', scheduleApply, { passive: true })" in script
    assert "Math.abs(previousInlineSize - inlineSize) > 0.25" in script
    assert "accessibilityCompactQuery" not in script
    assert "navigator.userAgent" not in script
    assert re.search(
        r"\.build-pwa-editor > \.section-preview \{.*?overflow-x: auto;.*?overscroll-behavior-inline: contain;",
        css,
        flags=re.DOTALL,
    )


def test_accessible_layout_override_is_persisted_without_forking_form_state() -> None:
    component = COMPONENT.read_text(encoding="utf-8")
    app = APP.read_text(encoding="utf-8")
    script = LAYOUT_SCRIPT.read_text(encoding="utf-8")

    for choice in ("auto", "compact", "workspace"):
        assert f'data-build-pwa-layout-choice="{choice}"' in component

    assert '<fieldset class="build-pwa-layout-picker"' in component
    assert 'role="status" aria-live="polite"' in component
    assert 'aria-current="@(isActive ? "step" : null)"' in component
    assert 'aria-controls="chummer-workspace-main"' in component
    assert 'js/build-pwa-layout.js' in app
    assert "window.localStorage.getItem(storageKey)" in script
    assert "window.localStorage.setItem(storageKey, preference)" in script
    assert "desktop-shell--build-layout-compact" in script
    assert "desktop-shell--build-layout-workspace" in script
    assert "build-pwa-layout--compact" in script
    assert "build-pwa-layout--workspace" in script
    assert "Workspace remains saved. Compact is temporarily used because this browser window cannot fit the three-column Workspace layout" in script
    assert "workspace-minimum-width" in script
    assert "setDatasetValue(workspace, 'buildPwaLayoutReason', reason)" in script
    assert "if (status.textContent !== nextStatus)" in script


def test_resize_and_override_only_reflow_the_existing_editor_dom() -> None:
    component = COMPONENT.read_text(encoding="utf-8")
    script = LAYOUT_SCRIPT.read_text(encoding="utf-8")

    assert component.count("<SectionPane ") == 1
    assert "compactMedia.addEventListener('change', onBrowserLayoutChange)" in script
    assert "applyToShell(" in script
    assert "const decisions = Array.from" in script
    assert "decisions.forEach((decision)" in script
    assert "classList.toggle" in script
    assert "setDatasetValue(workspace, 'buildPwaLayoutEffective', effective)" in script
    assert ".innerHTML" not in script
    assert "replaceChildren" not in script
    assert "cloneNode" not in script
    assert "location.reload" not in script
    assert "moveFocusIfLayoutHidesIt" in script
    assert "const focusRepair = planFocusRepair(shell, workspace, effective);" in script
    assert "let meaningfulFocusGeneration = 0;" in script
    assert "let interactionGeneration = 0;" in script
    assert "lastFocusedInteractionGeneration === interactionGeneration" in script
    assert "const interactionBelongsToLastFocus" in script
    assert "lastFocusedInteractionGeneration = interactionGeneration;" in script
    assert "const isWindowBlur = event?.type === 'blur' && event.currentTarget === window;" in script
    assert "pendingFocusRepairs.get(plan.shell) !== plan.repairToken" in script
    assert "document.hasFocus" not in script
    assert "meaningfulFocusGeneration !== plan.focusGeneration" in script
    assert "interactionGeneration !== plan.interactionGeneration" in script
    assert "activeNow !== plan.focusCandidate && !isNeutralDocumentFocus(activeNow)" in script
    assert "document.addEventListener('pointerdown', noteInteraction, true)" in script
    assert "document.addEventListener('click', noteInteraction, true)" in script
    assert "document.addEventListener('keydown', noteInteraction, true)" in script
    assert "window.addEventListener('blur', noteInteraction)" in script
    assert "findAccessibleFocusTarget(plan.workspace, plan.effective)" in script
    assert "target.focus({ preventScroll: true })" in script
    assert "scrollActiveStepIntoView" in script
    assert "attributeFilter: ['aria-current']" in script
    assert "activeStep.scrollIntoView" in script
    assert "block: 'nearest'" in script
    assert "inline: 'nearest'" in script
    assert "observer.disconnect()" in script
    assert "bootstrapObserverTimeout" in script
    assert 'InvokeVoidAsync("chummerBuildPwaLayout.applyAll")' in component
    assert "OnAfterRenderAsync(bool firstRender)" in component


def test_compact_controls_keep_thumb_sized_targets_and_no_focusable_css_clone() -> None:
    component = COMPONENT.read_text(encoding="utf-8")
    css = CSS.read_text(encoding="utf-8")

    assert 'data-build-pwa-previous' in component
    assert 'data-build-pwa-review' in component
    assert 'data-build-pwa-next' in component
    assert '.build-pwa-layout--compact button,' in css
    assert '.build-pwa-layout--compact a[href],' in css
    assert 'min-height: 44px;' in css
    assert 'min-width: 44px;' in css
    assert '.build-pwa-compact-context,\n.build-pwa-mobile-dock,\n.build-pwa-mobile-command-menu {\n    display: none;' in css
    assert '.build-pwa-layout--compact .build-pwa-compact-context {' in css
    assert component.count('id="summaryName"') == 0


def test_browser_probe_exercises_resize_preservation_and_persisted_override() -> None:
    probe = BROWSER_TEST.read_text(encoding="utf-8")
    runner = BROWSER_RUNNER.read_text(encoding="utf-8")

    assert "page.setViewportSize({ width: 430, height: 900 })" in probe
    assert "assertForcedWorkspaceClamp(page, 430)" in probe
    assert "assertForcedWorkspaceClamp(page, 390)" in probe
    assert "assertForcedWorkspaceClamp(page, 320)" in probe
    assert "assertMeasuredWorkspaceFit(page, 959, true)" in probe
    assert "assertMeasuredWorkspaceFit(page, 960, false)" in probe
    assert "assertMeasuredWorkspaceBoundary(page, null, 'app root size')" in probe
    assert "assertMeasuredWorkspaceBoundary(page, '20px', '20px root font')" in probe
    assert "moveToMeasuredWorkspaceOffset(page, -1, 'compact'" in probe
    assert "moveToMeasuredWorkspaceOffset(page, 1, 'workspace'" in probe
    assert "const expectedMinimum = 60.7 * above.rootFontSize" in probe
    assert "Math.abs(Number(workspace.dataset.buildPwaLayoutAvailableInlineSize) - shell.clientWidth) <= 0.25" in probe
    assert "assertRootTextClamp(page, '200%', '200% root text')" in probe
    assert "assertFocusRepairAfterCssHide(page)" in probe
    assert "assertFocusRepairAfterUserGesture(page)" in probe
    assert "assertFocusRepairRace(page)" in probe
    assert "assertFocusRepairInteractionGuards(page)" in probe
    assert "A later layout pass revived stale focus after a ${interaction} interaction" in probe
    assert "assertStableLayoutStatus(page)" in probe
    assert "page.setViewportSize({ width: 1440, height: 1000 })" in probe
    assert "data-resize-state-sentinel" in probe
    assert "active builder section" in probe
    assert "box.height >= 44 && box.width >= 44" in probe
    assert "[data-build-pwa-layout-choice=\"workspace\"]" in probe
    assert "page.reload" in probe
    assert "Workspace override was not restored from local storage" in probe
    assert "workspace-minimum-width" in probe
    assert "The saved Workspace choice was not retained at ${width}px" in probe
    assert "The Workspace clamp did not expose its fit reason at ${width}px" in probe
    assert "Deferred compact focus repair stole focus from a newer user target" in probe
    assert "CSS-hide focus repair lost a ${gesture}-focused Workspace control" in probe
    assert "Stable layout re-announced an unchanged live status" in probe
    assert "Active navigation did not reveal the newly selected step" in probe
    assert "assertNoOuterHorizontalOverflow(page, `${width}px compact clamp`)" in probe
    assert "['page', document.documentElement]" in probe
    assert "['shell', shell]" in probe
    assert "['workspace', workspace]" in probe
    assert "outer horizontal overflow" in probe
    assert "assertCompactInstallClearance(page, 320)" in probe
    assert "assertCompactInstallClearance(page, 430)" in probe
    assert "Install launcher overlaps ${label} at ${width}px" in probe
    assert "[data-nav-tab][aria-current=\"step\"]" in probe
    assert "compact title must be a visible h1" in probe
    assert "cross-app-cache-sentinel" in probe
    assert "pwa.registration.scope === pwa.expectedAuthority.scope" in probe
    assert "worker?.scriptURL === pwa.expectedAuthority.scriptUrl" in probe
    assert "must not serve a matching URL from the public root cache" in probe
    assert "scripts/ai/build.sh" in runner
    assert "CHUMMER_BUILD_OWNER_CHANNEL_ALLOW_EPHEMERAL=true" in runner
    assert 'state_dir="$(mktemp -d "$log_dir/build-pwa-responsive-e2e-state.XXXXXX")"' in runner
    assert 'CHUMMER_STATE_PATH="$state_dir"' in runner
    assert 'rm -rf -- "$state_dir"' in runner
    assert 'node "$PLAYWRIGHT_SCRIPT"' in runner


def test_compact_install_launcher_reserves_flow_and_runtime_always_builds_incrementally() -> None:
    install_css = INSTALL_CSS.read_text(encoding="utf-8")
    runtime_test = RUNTIME_TEST.read_text(encoding="utf-8")

    assert ".build-pwa-install-launcher:not([hidden])" in install_css
    assert "position: relative;" in install_css
    assert "inset: auto;" in install_css
    assert "body:has(.build-pwa-layout--compact)" in install_css
    assert '"build",' in runtime_test
    assert '"--no-restore",' in runtime_test
    assert '"--no-build",' in runtime_test
    assert "PWA_SURFACE_INPUTS" not in runtime_test
    assert "dll_mtime" not in runtime_test


def test_build_install_handoff_has_accessible_mobile_and_desktop_surfaces() -> None:
    panel = INSTALL_PANEL.read_text(encoding="utf-8")
    install_css = INSTALL_CSS.read_text(encoding="utf-8")

    for choice in ("auto", "mobile", "desktop"):
        assert f'data-build-pwa-install-device-choice="{choice}"' in panel
    assert 'data-build-pwa-install-device-status' in panel
    assert 'role="status"' in panel
    assert 'aria-live="polite"' in panel
    assert 'data-build-pwa-desktop-handoff' in panel
    assert 'data-build-pwa-mobile-handoff' in panel
    assert 'data-build-pwa-install-qr' in panel
    assert 'data-build-pwa-copy-install-link' in panel
    assert "never contains a runner, workspace, sign-in token, or account context" in panel

    assert ".build-pwa-install-panel__desktop-handoff" in install_css
    assert "grid-template-columns: minmax(9rem, 12rem) minmax(0, 1fr);" in install_css
    assert "@media (max-width: 46rem)" in install_css
    assert "grid-template-columns: 1fr;" in install_css
    assert "min-height: 44px;" in install_css
    assert "shape-rendering: crispEdges;" in install_css
    assert "@media (forced-colors: active)" in install_css


def test_build_install_handoff_uses_capability_signals_and_clean_scoped_urls() -> None:
    script = INSTALL_SCRIPT.read_text(encoding="utf-8")

    assert "window.navigator.userAgentData" in script
    assert re.search(r"\b(?:window\.)?navigator\.userAgent(?!Data\b)", script) is None
    assert 'window.matchMedia("(display-mode: standalone)")' in script
    assert 'window.matchMedia("(any-pointer: coarse)")' in script
    assert "window.navigator.maxTouchPoints" in script
    assert '"chummer.build-pwa.install-device.v1"' in script
    assert "window.localStorage.getItem(devicePreferenceKey)" in script
    assert "window.localStorage.setItem(devicePreferenceKey, memoryDevicePreference)" in script

    assert 'new URL("app", scopeUrl)' in script
    assert "document.baseURI" not in script
    assert 'scope: authorityScope.href' in script
    assert 'scriptQueryKeys[0] !== "build"' in script
    assert "A frozen Build registration authority is required" in script
    assert 'scopeUrl.origin !== expectedOrigin' in script
    assert 'scopeUrl.username = ""' in script
    assert 'scopeUrl.password = ""' in script
    assert 'scopeUrl.search = ""' in script
    assert 'scopeUrl.hash = ""' in script
    assert "encodeQrMatrix(canonicalInstallUrl)" in script
    assert '"QR code for the clean Chummer Build mobile install page"' in script
    assert "qrContainer.replaceChildren()" in script
    assert "The QR code could not be generated" in script
    assert "focusStableTargetBeforeHiding(false)" in script
    assert "focusStableTargetBeforeHiding(true)" in script


def test_install_browser_contract_covers_pathbase_overrides_qr_and_safe_failure() -> None:
    probe = INTEGRITY_BROWSER_TEST.read_text(encoding="utf-8")

    assert 'scope: `${origin}/?workspace=secret-runner&token=secret#owner-token`' in probe
    assert 'scope: `${origin}/blazor/?workspace=secret-runner&token=secret#owner-token`' in probe
    assert "uaMobile" in probe
    assert "coarseTouchFallback" in probe
    assert "explicitDesktop" in probe
    assert "explicitMobile" in probe
    assert 'standalone: "standalone"' in probe
    assert 'localStorage.getItem("chummer.build-pwa.install-device.v1")' in probe
    assert 'contract.matrix.signature === "08b160cc"' in probe
    assert 'handoff.encodeQrMatrix("x".repeat(272))' in probe
    assert "Over-capacity QR failure emitted a partial code" in probe
    assert "QR code could not be generated" in probe
    assert "document.activeElement === document.querySelector('#chummer-workspace-main')" in probe
    assert "runMobileNativeInstallContract" in probe
    assert "hasTouch: true" in probe
    assert "Object.defineProperty(Navigator.prototype, 'userAgentData'" in probe
    assert "new Event('beforeinstallprompt', { cancelable: true })" in probe
    assert "nativePrompt.promptCalls === 1" in probe
    assert "manualFallback.manualOpen === true" in probe


def test_integrity_bridge_retry_uses_generation_owner_cas_and_bounded_interop() -> None:
    component = COMPONENT.read_text(encoding="utf-8")
    deadline = INTEROP_DEADLINE.read_text(encoding="utf-8")

    assert component.count("RecoveryInteropDeadlineRuntime.RunAsync(") >= 2
    assert '"chummerBuildPwaIntegrity.registerBridge"' in component
    assert '"chummerBuildPwaIntegrity.updateState"' in component
    assert "IntegrityInteropDeadline" in component
    assert "Interlocked.Increment(ref _integrityRetryGeneration)" in component
    assert "Interlocked.CompareExchange(ref _integrityRetryOwner, owner, 0)" in component
    assert "Interlocked.CompareExchange(ref _integrityRetryOwner, 0, owner)" in component
    assert "Volatile.Write(ref _integrityRetryPending" not in component
    assert "operation.WaitAsync(timeout, lifetime)" in deadline
    assert "await invocation.CancelAsync()" in deadline
    assert "ObserveLateCompletionAsync(operation)" in deadline
    assert "ObserveLateCompletionAsync(operation)" in deadline
