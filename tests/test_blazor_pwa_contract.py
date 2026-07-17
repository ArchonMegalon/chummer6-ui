from __future__ import annotations

import json
import re
import struct
from pathlib import Path
from urllib.parse import urljoin


REPO_ROOT = Path(__file__).resolve().parents[1]
APP_RAZOR = REPO_ROOT / "Chummer.Blazor" / "Components" / "App.razor"
WWWROOT = REPO_ROOT / "Chummer.Blazor" / "wwwroot"
INSTALL_PANEL = REPO_ROOT / "Chummer.Blazor" / "Components" / "Shell" / "BuildPwaInstallPanel.razor"
INTEGRITY_E2E = REPO_ROOT / "scripts" / "e2e-build-pwa-integrity-playwright.cjs"
CACHE_LEASE_TEST = REPO_ROOT / "scripts" / "test-build-pwa-cache-leases.cjs"
RECOVERY_RUNTIME = WWWROOT / "js" / "build-pwa-recovery.js"


def test_blazor_app_advertises_installable_pwa_surface() -> None:
    app = APP_RAZOR.read_text(encoding="utf-8")

    assert '<base href="@BuildBaseHref()" />' in app
    assert '<link rel="manifest" href="@BuildStaticAssetHref("manifest.webmanifest")" />' in app
    assert '<link rel="icon" type="image/svg+xml" href="@BuildStaticAssetHref("icons/chummer-pwa.svg")" />' in app
    assert '<link rel="apple-touch-icon" href="@BuildStaticAssetHref("media/chummer6/chummer6-hero-baseline.png")" />' in app
    assert '<meta name="theme-color" content="#0f3b3e" />' in app
    assert "navigator.serviceWorker.register(serviceWorkerScript, {" in app
    assert "scope: serviceWorkerScope" in app
    assert "updateViaCache: 'none'" in app
    assert "window.chummerPwa" in app
    assert '<meta name="application-name" content="Chummer Build" />' in app
    assert '<link rel="apple-touch-icon" sizes="180x180"' in app
    assert '<BuildPwaInstallPanel />' in app
    assert 'BuildStaticAssetHref("build-pwa-install.css")' in app
    assert 'BuildStaticAssetHref("js/build-pwa-install.js")' in app


def test_pwa_manifest_has_a_distinct_builder_identity_and_roster_start_url() -> None:
    manifest = json.loads((WWWROOT / "manifest.webmanifest").read_text(encoding="utf-8"))

    assert manifest["name"] == "Chummer Runner Builder"
    assert manifest["short_name"] == "Chummer Build"
    assert manifest["id"] == "./app"
    assert manifest["id"] not in {"/mobile", "/mobile/player", "/mobile/gm"}
    assert manifest["start_url"] == "./app?command=character_roster&source=pwa"
    assert manifest["scope"] == "./"
    assert manifest["display"] == "standalone"
    assert "window-controls-overlay" not in manifest.get("display_override", [])
    assert any(icon["purpose"] == "maskable" for icon in manifest["icons"])
    assert {shortcut["short_name"] for shortcut in manifest["shortcuts"]} >= {"New", "Roster"}
    assert all(shortcut["short_name"] != "Play" for shortcut in manifest["shortcuts"])
    png_sizes = {
        (icon["sizes"], icon["purpose"])
        for icon in manifest["icons"]
        if icon["type"] == "image/png"
    }
    assert ("180x180", "any") in png_sizes
    assert ("192x192", "any") in png_sizes
    assert ("512x512", "any") in png_sizes
    assert ("512x512", "maskable") in png_sizes

    hosted_manifest_url = "https://chummer.run/blazor/manifest.webmanifest"
    assert urljoin(hosted_manifest_url, manifest["id"]) == "https://chummer.run/blazor/app"
    assert urljoin(hosted_manifest_url, manifest["start_url"]) == (
        "https://chummer.run/blazor/app?command=character_roster&source=pwa"
    )
    assert urljoin(hosted_manifest_url, manifest["scope"]) == "https://chummer.run/blazor/"


def test_service_worker_caches_only_static_shell_assets_not_runner_data() -> None:
    worker = (WWWROOT / "service-worker.js").read_text(encoding="utf-8")

    assert "CHUMMER_PWA_CACHE" in worker
    assert "CHUMMER_BUILD_PWA_CACHE_PREFIX = 'chummer-build-static-'" in worker
    assert re.search(r"CHUMMER_BUILD_PWA_CACHE_VERSION = 'v\d+';", worker)
    assert "const CHUMMER_PWA_CACHE = buildRevisionCacheName(" in worker
    assert "CHUMMER_BUILD_PWA_CACHE_GENERATION," in worker
    assert "CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION);" in worker
    assert "chummer-shell-play-shell-" not in worker
    assert "caches.open(CHUMMER_PWA_CACHE)" in worker
    assert "const cached = await cache.match(request)" in worker
    assert "caches.match(request)" not in worker
    assert "request.mode === 'navigate'" in worker
    assert "const cached = await cache.match(OFFLINE_URL)" in worker
    assert "caches.match(" not in worker
    assert "const publicPath = exactReleasePublicPath(url)" in worker
    assert "RELEASE_CONTENT_PATHNAMES.get(url.pathname) || null" in worker
    assert "workspace=ws-1" not in worker
    assert ".then(() => self.skipWaiting())" not in worker
    assert "self.skipWaiting(" not in worker
    assert "chummer-build-update-activated" in worker
    assert "notifyBuildClientsOfActivation" in worker
    assert "Promise.allSettled" in worker
    assert "cache.addAll" not in worker
    assert "chummer6-hero-baseline.png" not in worker
    assert "karma-forge-baseline.png" not in worker
    assert "cacheControl.includes('private')" in worker
    assert "cacheControl.includes('no-store')" in worker
    assert "expectedMimeTypesForPath" in worker
    assert "response.headers.get('Content-Type')" in worker
    assert "new URL(self.registration.scope)" in worker


def test_build_worker_has_no_forced_activation_path_and_gc_is_post_activation() -> None:
    worker = (WWWROOT / "service-worker.js").read_text(encoding="utf-8")
    activate = worker.split("self.addEventListener('activate'", 1)[1].split(
        "self.addEventListener('fetch'", 1
    )[0]

    assert "caches.delete" not in activate
    assert "self.clients.claim(" not in activate
    assert "self.skipWaiting(" not in worker
    assert "activateBuildWorker" in activate
    assert "await requestCacheLeaseSweep();" in worker
    assert "await notifyBuildClientsOfActivation();" in worker
    assert "self.clients.matchAll({ type: 'window', includeUncontrolled: true })" in worker
    assert "client.postMessage({ type: CHUMMER_PWA_ACTIVATED_MESSAGE })" in worker
    assert "chummer-shell-play-shell" not in worker
    assert "chummer-media-play-shell" not in worker


def test_offline_shell_states_living_world_data_is_not_cached() -> None:
    offline = (WWWROOT / "offline.html").read_text(encoding="utf-8")

    assert "Your runner data is not cached" in offline
    assert "Chummer Build PWA" in offline
    assert "Retry Chummer Build" in offline
    assert "Chummer Online PWA" not in offline
    assert "Black Ledger" in offline
    assert "heat" in offline
    assert "opt-in living-world data stays server-bound" in offline


def test_build_install_lifecycle_has_prompt_manual_install_and_update_actions() -> None:
    app = APP_RAZOR.read_text(encoding="utf-8")
    panel = INSTALL_PANEL.read_text(encoding="utf-8")
    script = (WWWROOT / "js" / "build-pwa-install.js").read_text(encoding="utf-8")
    css = (WWWROOT / "build-pwa-install.css").read_text(encoding="utf-8")

    assert "data-build-pwa-install-help" in panel
    assert 'aria-controls="build-pwa-install-panel"' in panel
    assert 'data-build-pwa-install\n         aria-labelledby="build-pwa-install-heading"\n         hidden' in panel
    assert "data-build-pwa-install-status" in panel
    assert "data-build-pwa-install-action" in panel
    assert "data-build-pwa-update-action" in panel
    assert "data-build-pwa-update-guidance" in panel
    assert "Close every Chummer Build browser tab and installed-app window" in panel
    assert "The browser will start the waiting version" in panel
    assert "data-build-pwa-dismiss-action" in panel
    assert "Not now" in panel
    assert "iPhone or iPad" in panel
    assert "Add to Home Screen" in panel
    assert "Android" in panel
    assert "Desktop" in panel
    assert 'listen(window, "beforeinstallprompt"' in script
    assert 'listen(window, "appinstalled"' in script
    assert 'listen(navigator.serviceWorker, "controllerchange"' in script
    assert 'chummer-build:service-worker-registration' in app
    assert 'chummer-build:service-worker-registration-failed' in app
    assert 'chummer-build:service-worker-registration' in script
    assert 'chummer-build:service-worker-registration-failed' in script
    assert "pwa.registration = registration" in app
    assert "pwa.expectedAuthority" in app
    assert "Object.freeze(expectedAuthority)" in app
    assert "writable: false" in app
    assert "configurable: false" in app
    assert "registrationMatchesBuild" in script
    assert "registration.scope !== expectedScope" in script
    assert "worker?.scriptURL === expectedScriptUrl" in script
    assert "buildRegistrationAuthority" in script
    assert "registrationStillMatchesAuthority" in script
    assert script.count("navigator.serviceWorker.ready") == 0
    assert "document.baseURI" not in script
    assert "A frozen Build registration authority is required for install handoff." in script
    assert 'scope: authorityScope.href' in script
    assert 'authorityScope.origin !== window.location.origin' in script
    assert 'authorityScript.pathname !== expectedWorker.pathname' in script
    assert 'scriptQueryKeys[0] !== "build"' in script
    assert 'qrContainer.hidden = true' in script
    assert 'qrContainer.removeAttribute("data-build-pwa-qr-signature")' in script
    lease_sweep = script.split("const postCacheLeaseSweep", 1)[1].split(
        "const scheduleCacheLeaseSweep", 1
    )[0]
    assert "registrationStillMatchesAuthority(authority)" in lease_sweep
    assert "authority.registration.active" in lease_sweep
    assert "active.scriptURL !== authority.scriptUrl" in lease_sweep
    assert "skip-waiting" not in script
    assert "waiting.postMessage" not in script
    assert "close every Chummer Build" in script
    assert 'window.sessionStorage.setItem(dismissalKey, "1")' in script
    assert "const hideInstalledControls" in script
    assert "preserveLauncherAcrossHydration" in script
    assert 'launcherHydrationObserver.observe(helpButton, { attributes: true, attributeFilter: ["hidden"] })' in script
    assert "installControlsSuppressed = appInstallConfirmed || isStandalone()" in script
    assert 'listen(window, "appinstalled"' in script
    assert "appInstallConfirmed || isStandalone()" in script
    assert "renderInstallLauncherState();" in script
    waiting_refresh = script.split("const refreshWaitingUpdate", 1)[1].split(
        "const integrityApi", 1
    )[0]
    assert "announcedWaitingWorker !== waiting" in waiting_refresh
    assert "if (!guidanceDismissed) setPanelVisible(true);" in waiting_refresh
    assert "refreshPassiveWorkerState" in script
    assert 'listen(window, "focus", refreshPassiveWorkerState)' in script
    assert 'listen(window, "pageshow", refreshPassiveWorkerState)' in script
    assert "chummerBuildPwaInstallController" in script
    assert 'window.matchMedia("(display-mode: standalone)")' in script
    assert 'listen(standaloneMediaQuery, "change", handleDisplayModeChange)' in script
    assert "if (!isStandalone()) appInstallConfirmed = false;" in script
    assert "listenerRemovers.splice(0).reverse()" in script
    assert "launcher rendered outside the Build worker's scope stays manual-only" in script
    assert re.search(r"\b(?:window\.)?navigator\.userAgent(?!Data\b)", script) is None
    assert "window.navigator.userAgentData" in script
    assert "window.localStorage.getItem(devicePreferenceKey)" in script
    assert "window.localStorage.setItem(devicePreferenceKey, memoryDevicePreference)" in script
    assert "bottom:" not in css
    assert "top: max(3.9rem" in css
    assert "min-width: 44px" in css
    assert "min-height: 44px" in css
    assert "@media (forced-colors: active)" in css
    assert "body:has(.build-pwa-layout--compact)" in css
    assert ".build-pwa-install-launcher:not([hidden])" in css
    assert "position: relative;" in css
    assert "inset: auto;" in css


def test_build_png_icons_have_the_declared_square_dimensions() -> None:
    expected = {
        "chummer-build-180.png": (180, 180),
        "chummer-build-192.png": (192, 192),
        "chummer-build-512.png": (512, 512),
        "chummer-build-maskable-512.png": (512, 512),
    }

    for name, dimensions in expected.items():
        payload = (WWWROOT / "icons" / name).read_bytes()
        assert payload[:8] == b"\x89PNG\r\n\x1a\n"
        assert struct.unpack(">II", payload[16:24]) == dimensions


def test_build_pwa_focus_and_reduced_motion_contract_is_accessibility_safe() -> None:
    layout_script = (WWWROOT / "js" / "build-pwa-layout.js").read_text(encoding="utf-8")
    install_script = (WWWROOT / "js" / "build-pwa-install.js").read_text(encoding="utf-8")
    styles = "\n".join(
        [
            (WWWROOT / "app.css").read_text(encoding="utf-8"),
            (WWWROOT / "build-pwa-install.css").read_text(encoding="utf-8"),
        ]
    )

    assert "target.focus({ preventScroll: true })" in layout_script
    assert "const isVisibleStableFocusTarget" in install_script
    assert 'target.closest("[hidden]")' in install_script
    assert 'style.display !== "none" && style.visibility !== "hidden"' in install_script
    assert "document.activeElement === target" in install_script
    suppressed_block = install_script.split("if (installControlsSuppressed) {", 1)[1].split(
        "return;", 1
    )[0]
    assert suppressed_block.index("focusStableTargetBeforeHiding(false)") < suppressed_block.index(
        "setPanelVisible(false)"
    )
    dismiss_block = install_script.split(
        "if (dismissButton instanceof HTMLButtonElement) {", 1
    )[1].split("if (installButton instanceof HTMLButtonElement) {", 1)[0]
    assert dismiss_block.index("focusStableTargetBeforeHiding(true)") < dismiss_block.index(
        "setPanelVisible(false)"
    )
    assert "@media (prefers-reduced-motion: reduce)" in styles

    reduced_motion_blocks = re.findall(
        r"@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{(.*?)\n\}",
        styles,
        flags=re.DOTALL,
    )
    assert reduced_motion_blocks
    assert any(".build-pwa" in block for block in reduced_motion_blocks)


def test_build_integrity_runtime_has_a_fixed_private_cross_page_contract() -> None:
    app = APP_RAZOR.read_text(encoding="utf-8")
    worker = (WWWROOT / "service-worker.js").read_text(encoding="utf-8")
    script = (WWWROOT / "js" / "build-pwa-integrity.js").read_text(encoding="utf-8")
    e2e = INTEGRITY_E2E.read_text(encoding="utf-8")
    owner_boundary = (
        REPO_ROOT / "Chummer.Blazor" / "Services" / "HostedBuildOwnerBoundary.cs"
    ).read_text(encoding="utf-8")

    assert "window.chummerBuildPwaIntegrity" in script
    assert "chummer-build-workspace-integrity-v1" in script
    assert "ownerInvalidationTokens" in script
    assert "/^[0-9a-f]{64}$/" in script
    assert "ownerInvalidationTokens" in app
    assert "HostedBuildOwnerInvalidationTokenService" in app
    assert "Object.freeze(expectedAuthority.ownerInvalidationTokens)" in app
    assert "channelNames" in script
    assert "recentlyHandledExternalMessages" in script
    assert "CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64" in owner_boundary
    assert "CHUMMER_BUILD_OWNER_CHANNEL_PREVIOUS_HMAC_KEY_BASE64" in owner_boundary
    assert "CHUMMER_BUILD_OWNER_CHANNEL_ALLOW_EPHEMERAL" in owner_boundary
    assert "must decode to exactly 32 bytes" in owner_boundary
    assert "externally provisioned 32-byte Base64 key shared by every replica" in owner_boundary
    assert "environment.IsDevelopment()" in owner_boundary
    assert 'environment.IsEnvironment("Test")' in owner_boundary
    assert "chummer:build-integrity-changed" in script
    assert "BroadcastChannel" in script
    assert "beforeunload" in script
    for method in (
        "registerBridge",
        "unregisterBridge",
        "updateState",
        "publishDelete",
        "markBridgeUnavailable",
        "getSnapshot",
        "canReload",
    ):
        assert method in script
        assert method in e2e

    for field in (
        "workspaceId",
        "contentRevision",
        "savedRevision",
        "isDirty",
        "hasConflict",
        "updateDeferred",
        "bridgeAvailable",
    ):
        assert field in script
        assert field in e2e

    assert "mutationKind" in script
    assert "revision" in script
    assert "Number(value)" not in script
    assert "Number.isSafeInteger(value) && value > 0" in script
    assert "wireKeys = ['mutationKind', 'revision', 'workspaceId']" in e2e
    assert 'BuildStaticAssetHref("js/build-pwa-integrity.js")' in app
    assert app.index('BuildStaticAssetHref("js/build-pwa-integrity.js")') < app.index(
        'BuildStaticAssetHref("js/build-pwa-install.js")'
    )
    assert "'js/build-pwa-integrity.js'" in worker
    assert "RELEASE_CONTENT_PATHNAMES.get(url.pathname) || null" in worker
    assert "runPassiveWaitingWorkerDoesNotDisplaceSibling" in e2e
    assert "runSameRevisionDeleteTombstoneContract" in e2e
    assert "RequestBuildPwaIntegrityBridgeRecoveryAsync" in e2e
    assert "runRecoveryStreamOutcomes" in e2e
    assert "chummer-build-skip-waiting" not in e2e
    for forbidden_wire_field in (
        "runnerName",
        "runnerAlias",
        "freeText",
        "nativeXml",
        "routeQuery",
    ):
        assert forbidden_wire_field not in script


def test_build_cache_gc_requires_exact_complete_live_client_leases() -> None:
    worker = (WWWROOT / "service-worker.js").read_text(encoding="utf-8")
    page = (WWWROOT / "js" / "build-pwa-install.js").read_text(encoding="utf-8")
    lease_test = CACHE_LEASE_TEST.read_text(encoding="utf-8")

    worker_version = re.search(
        r"const CHUMMER_BUILD_PWA_CACHE_VERSION = '([^']+)';",
        worker,
    )
    page_version = re.search(
        r'const CHUMMER_BUILD_PWA_CACHE_VERSION = "([^"]+)";',
        page,
    )
    assert worker_version is not None
    assert page_version is not None
    assert worker_version.group(1) == page_version.group(1)

    for message_type in (
        "chummer-build-pwa-cache-lease-request",
        "chummer-build-pwa-cache-lease-response",
        "chummer-build-pwa-cache-lease-sweep",
    ):
        assert message_type in worker
        assert message_type in page

    assert "isPlainExactMessage(data, ['type', 'requestId', 'cacheVersion'])" in worker
    assert "client.postMessage({ type: CHUMMER_BUILD_PWA_CACHE_LEASE_REQUEST, requestId })" in worker
    assert "self.clients.matchAll({ type: 'window', includeUncontrolled: true })" in worker
    assert "haveSameClientIds(firstSnapshot, secondSnapshot)" in worker
    assert "url.origin !== self.location.origin" in worker
    assert "CHUMMER_BUILD_PWA_SCOPE_URL = new URL(self.registration.scope)" in worker
    assert "CHUMMER_BUILD_PWA_SCOPE_PATH = CHUMMER_BUILD_PWA_SCOPE_URL.pathname" in worker
    assert "cacheLeaseSweepPromise" in worker
    assert "pendingCacheLeaseRequest = null" in worker
    assert "self.clients.claim(" not in worker
    assert "self.skipWaiting(" not in worker
    assert "skip-waiting" not in page

    assert 'isPlainExactMessage(event.data, ["type", "requestId"])' in page
    assert "isValidCacheLeaseRequestId(event.data.requestId)" in page
    assert "event.source.postMessage({" in page
    assert "cacheVersion: CHUMMER_BUILD_PWA_CACHE_VERSION" in page
    assert "source.scriptURL === authority.scriptUrl" in page
    assert 'listen(window, "focus", refreshPassiveWorkerState)' in page
    assert 'listen(window, "pageshow", refreshPassiveWorkerState)' in page
    assert "visibilitychange" in page

    for scenario in (
        "frozenLegacyMigration",
        "singleByteMismatchFailsBeforeCacheOpen",
        "orphanWaitingCacheGetsGraceThenReclaims",
        "threeReleaseLifecycleAndRestart",
        "leaseTopologyAndRootScopeBoundary",
    ):
        assert scenario in lease_test


def test_deleted_dirty_runner_recovery_is_exact_memory_only_bounded_and_dispatch_confirmed() -> None:
    store = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "WorkspaceRecoveryPayloadStore.cs"
    ).read_text(encoding="utf-8")
    presenter = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "CharacterOverviewPresenter.cs"
    ).read_text(encoding="utf-8")
    workspace = (
        REPO_ROOT
        / "Chummer.Blazor"
        / "Components"
        / "Shell"
        / "BuildPwaWorkspace.razor"
    ).read_text(encoding="utf-8")
    downloads = (
        REPO_ROOT
        / "Chummer.Blazor"
        / "Components"
        / "Layout"
        / "DesktopShell.Downloads.cs"
    ).read_text(encoding="utf-8")
    loader = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "WorkspaceOverviewLoader.cs"
    ).read_text(encoding="utf-8")
    state = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "CharacterOverviewState.cs"
    ).read_text(encoding="utf-8")
    recovery_runtime = RECOVERY_RUNTIME.read_text(encoding="utf-8")

    assert "MaxPayloadBytes = 8 * 1024 * 1024" in store
    assert "MaxRetainedEntries = 4" in store
    assert "MaxRetainedBytes = 16L * 1024 * 1024" in store
    assert "ProtectedFromEviction" in store
    assert "Recovery vault capacity is occupied by protected dirty or conflicted payloads" in store
    assert "CryptographicOperations.ZeroMemory" in store
    assert "TryBeginCaptureIntent" in store
    assert "CanonicalValidationCapability validationCapability" in store
    assert "validationCapability.Matches(workspaceId, sourceRevision, document, digest)" in store
    assert "CapabilityIssuer" in loader
    assert "Canonical validation authority is loader-owned." in loader
    assert "validationTask.Result.IsValid" in loader
    assert "CreateAfterCanonicalValidation" not in loader
    assert 'WorkspaceDocumentFormat.NativeXml => "application/xml"' in store
    assert 'WorkspaceDocumentFormat.Json => "application/json"' in store
    assert "localStorage" not in store
    assert "BroadcastChannel" not in store

    prepare = presenter.split("PrepareRecoveryCopyAsync", 1)[1].split(
        "TryAcquireRecoveryCopyExportLease", 1
    )[0]
    assert "_client" not in prepare
    assert "WorkspaceRecoveryExportRequest" in prepare
    assert "PendingRecoveryExportVersion" in prepare
    assert "Convert.ToBase64String" not in prepare
    assert "WorkspaceDownloadReceipt" not in prepare
    assert "MarkExported" not in prepare
    assert "CompleteRecoveryCopyExport" in presenter
    assert "AcknowledgeRecoveryCopySaved" in presenter
    assert "CloseDeletedRecoveryAtomicallyAsync" in presenter
    assert "TryCommitExplicitClose" in presenter
    assert "WorkspaceRecoveryExportRequest? PendingRecoveryExport" in state

    assert "Document: verifiedWorkspace.Document" in loader
    assert "Save exact recovery copy" in workspace
    assert "Recovery copy unavailable" in workspace
    assert "data-build-pwa-integrity-close-recovery" in workspace
    assert "data-build-pwa-integrity-confirm-recovery" in workspace
    assert "data-build-pwa-recovery-readiness" in workspace
    assert "recovery.ExportConfirmed" in workspace
    assert "DotNetStreamReference" in downloads
    assert '"chummerDownloads.saveRecoveryStream"' in downloads
    assert "ConfirmRecoveryCopyDownloadDispatched" not in downloads
    assert "RejectRecoveryCopyDownloadDispatch" not in downloads
    assert "window.navigator.userActivation?.isActive !== true" in recovery_runtime
    assert "showSaveFilePicker" in recovery_runtime
    assert "await writable.close()" in recovery_runtime
    assert "durable_saved" in recovery_runtime
    assert "dispatched_requires_explicit_user_ack" in recovery_runtime
    assert "URL.revokeObjectURL" in recovery_runtime
    assert "bytes.fill(0)" in recovery_runtime
    assert 'BuildStaticAssetHref("js/build-pwa-recovery.js")' in APP_RAZOR.read_text(encoding="utf-8")
    assert "'js/build-pwa-recovery.js'" in (WWWROOT / "service-worker.js").read_text(encoding="utf-8")
    assert "http://" not in prepare
    assert "https://" not in prepare


def test_hardened_recovery_authority_commit_and_key_repository_boundaries_are_sealed() -> None:
    loader = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "WorkspaceOverviewLoader.cs"
    ).read_text(encoding="utf-8")
    store = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "WorkspaceRecoveryPayloadStore.cs"
    ).read_text(encoding="utf-8")
    mutations = (
        REPO_ROOT
        / "Chummer.Presentation"
        / "Overview"
        / "CharacterOverviewPresenter.WorkspaceMutations.cs"
    ).read_text(encoding="utf-8")
    owner_boundary = (
        REPO_ROOT
        / "Chummer.Blazor"
        / "Services"
        / "HostedBuildOwnerBoundary.cs"
    ).read_text(encoding="utf-8")
    presenter_tests = (
        REPO_ROOT
        / "Chummer.Tests"
        / "Presentation"
        / "CharacterOverviewPresenterTests.cs"
    ).read_text(encoding="utf-8")
    component_tests = (
        REPO_ROOT
        / "Chummer.Tests"
        / "Presentation"
        / "BuildPwaWorkspaceTests.cs"
    ).read_text(encoding="utf-8")

    # Caller-owned ValidateAsync remains an early signal, but cannot issue the
    # opaque receipt without the loader's concrete, non-overridable codecs.
    assert "private sealed class CanonicalDocumentAuthority" in loader
    assert "CanonicalAuthority.Validate(workspaceId, document);" in loader
    assert "new Sr4WorkspaceCodec(" in loader
    assert "new Sr5WorkspaceCodec(" in loader
    assert "new Sr6WorkspaceCodec(" in loader
    assert "codec.ParseSummary(envelope)" in loader
    assert "codec.Validate(envelope)" in loader
    assert "codec.BuildDownload(workspaceId, envelope, document.Format)" in loader
    assert "public static CanonicalValidationCapability" not in loader

    # Dispose may cancel validation, but cannot erase a capture that has
    # crossed the committing linearization point or let close pass it.
    assert "TryBeginCommitLocked" in store
    assert "CaptureIntentState.Committing" in store
    assert "TryCancelBeforeCommitLocked" in store
    assert "_captureIntents.Values.Any" in store
    assert "FinalizeCaptureIntent(ownedIntent)" in store
    assert "Disposing_a_committing_capture_intent_cannot_remove_the_close_barrier" in presenter_tests

    # XML mutation success captures the exact committed bytes before the
    # fallible projection reload and keeps the runner review-gated on failure.
    assert "TryCaptureRecoveryPayloadAsync(" in mutations
    assert "postCommitCaptureIntent" in mutations
    assert "committedDocument" in mutations
    assert '"postcommit XML refresh"' in mutations
    assert "reloaded is null || !reloaded.CanPublish" in mutations
    assert "Committed_xml_mutation_reload_failure_keeps_runner_review_gated_with_exact_recovery" in presenter_tests

    # Production cannot infer repository identity from a mutable path or accept
    # caller-substitutable repository/encryptor objects. One typed capability,
    # constructed from an inherited directory descriptor and pinned certificate,
    # owns both authorities and configures them together.
    assert "private HostedBuildDataProtectionMaterial(" in owner_boundary
    assert "FromInheritedUnixDirectoryDescriptor(" in owner_boundary
    assert (
        "Hosted Build production requires typed, host-owned pinned repository "
        "and certificate-encryptor material."
    ) in owner_boundary
    assert (
        "is not accepted in production because a mutable filesystem path "
        "cannot pin repository identity."
    ) in owner_boundary
    assert "Configure<HostedBuildDataProtectionMaterial>" in owner_boundary
    assert "options.XmlRepository = material.Repository" in owner_boundary
    assert "options.XmlEncryptor = material.Protector" in owner_boundary

    # The retry regression is exercised on an actual rendered bUnit component,
    # not only by a source-text assertion.
    assert "Retry_release_renders_reenters_and_stale_finally_cannot_clear_the_new_timer_owner" in component_tests
    assert "RenderWorkspace(context, \"tab-info\")" in component_tests
    assert '"RetryIntegrityBridgeRegistrationAsync"' in component_tests
