(function initializeBuildPwaLayout(window, document) {
    'use strict';

    const storageKey = 'chummer.build-pwa.layout.v1';
    const compactQuery = '(max-width: 59.999rem)';
    const allowedPreferences = new Set(['auto', 'compact', 'workspace']);
    const compactMedia = window.matchMedia(compactQuery);
    let memoryPreference = null;
    let applyScheduled = false;
    let lastFocusedElement = null;
    let lastFocusedInteractionGeneration = 0;
    let meaningfulFocusGeneration = 0;
    let interactionGeneration = 0;
    let focusRepairSequence = 0;
    let workspaceMeasurementProbe = null;
    const pendingActiveStepScrolls = new WeakSet();
    const pendingFocusRepairs = new WeakMap();
    const observedInlineSizes = new WeakMap();
    const observedShells = new WeakSet();

    function isNeutralDocumentFocus(element) {
        return !(element instanceof HTMLElement)
            || element === document.body
            || element === document.documentElement;
    }

    function noteInteraction(event) {
        interactionGeneration += 1;

        // A single pointer or keyboard gesture can focus an element before its
        // trailing pointerup/click/keyup event. Keep those later phases tied to
        // the same meaningful focus so a media-query reflow can still repair it
        // after the browser has moved focus to body. An interaction after focus
        // was lost (or a window blur) deliberately leaves the generations split
        // and cancels the deferred repair.
        const isWindowBlur = event?.type === 'blur' && event.currentTarget === window;
        const interactionBelongsToLastFocus = lastFocusedElement instanceof HTMLElement
            && lastFocusedElement.isConnected
            && (document.activeElement === lastFocusedElement
                || (event?.target instanceof Node && lastFocusedElement.contains(event.target)));
        if (!isWindowBlur && interactionBelongsToLastFocus) {
            lastFocusedInteractionGeneration = interactionGeneration;
        }
    }

    document.addEventListener('focusin', (event) => {
        if (event.target instanceof HTMLElement && !isNeutralDocumentFocus(event.target)) {
            meaningfulFocusGeneration += 1;
            lastFocusedElement = event.target;
            lastFocusedInteractionGeneration = interactionGeneration;
        }
    }, true);
    document.addEventListener('pointerdown', noteInteraction, true);
    document.addEventListener('pointerup', noteInteraction, true);
    document.addEventListener('click', noteInteraction, true);
    document.addEventListener('keydown', noteInteraction, true);
    document.addEventListener('keyup', noteInteraction, true);
    window.addEventListener('blur', noteInteraction);

    function normalizePreference(value) {
        const normalized = String(value || '').trim().toLowerCase();
        return allowedPreferences.has(normalized) ? normalized : 'auto';
    }

    function readStoredPreference() {
        if (memoryPreference !== null) {
            return memoryPreference;
        }

        try {
            memoryPreference = normalizePreference(window.localStorage.getItem(storageKey));
        } catch {
            memoryPreference = 'auto';
        }

        return memoryPreference;
    }

    function ensureWorkspaceMeasurementProbe(workspace) {
        if (workspaceMeasurementProbe instanceof HTMLElement && workspaceMeasurementProbe.isConnected) {
            return workspaceMeasurementProbe;
        }

        const minimumInlineSize = window.getComputedStyle(workspace)
            .getPropertyValue('--build-pwa-workspace-minimum-inline-size')
            .trim();
        if (!minimumInlineSize) {
            return null;
        }

        const probe = document.createElement('span');
        probe.setAttribute('aria-hidden', 'true');
        probe.dataset.buildPwaWorkspaceMeasurement = 'minimum-inline-size';
        probe.style.cssText = [
            'position:fixed',
            'inset:0 auto auto 0',
            'display:block',
            `inline-size:${minimumInlineSize}`,
            'block-size:1px',
            'visibility:hidden',
            'pointer-events:none',
            'contain:strict',
            'transform:translateX(-100%)'
        ].join(';');
        document.body.appendChild(probe);
        workspaceMeasurementProbe = probe;
        geometryObserver?.observe(probe);
        return probe;
    }

    function measureWorkspaceFit(shell, workspace) {
        const probe = ensureWorkspaceMeasurementProbe(workspace);
        const availableInlineSize = shell.clientWidth;
        const minimumInlineSize = probe?.getBoundingClientRect().width ?? Number.POSITIVE_INFINITY;
        return {
            availableInlineSize,
            fits: Number.isFinite(minimumInlineSize)
                && availableInlineSize + 0.25 >= minimumInlineSize,
            minimumInlineSize
        };
    }

    function effectiveLayout(preference, workspaceFits) {
        if (preference === 'compact') {
            return 'compact';
        }

        if (preference === 'workspace') {
            return workspaceFits ? 'workspace' : 'compact';
        }

        return workspaceFits ? 'workspace' : 'compact';
    }

    function statusText(preference, effective) {
        if (preference === 'workspace' && effective === 'compact') {
            return 'Workspace remains saved. Compact is temporarily used because this browser window cannot fit the three-column Workspace layout.';
        }

        if (preference === 'auto') {
            return `Auto is using the ${effective} layout for the current browser width.`;
        }

        return `${preference === 'compact' ? 'Compact' : 'Workspace'} layout selected.`;
    }

    function layoutReason(preference, effective) {
        if (preference === 'workspace' && effective === 'compact') {
            return 'workspace-minimum-width';
        }

        return preference === 'auto' ? 'browser-width' : 'preference';
    }

    function scrollActiveStepIntoView(workspace) {
        if (pendingActiveStepScrolls.has(workspace)) {
            return;
        }

        pendingActiveStepScrolls.add(workspace);
        window.requestAnimationFrame(() => {
            pendingActiveStepScrolls.delete(workspace);
            const activeStep = workspace.querySelector('[data-nav-tab][aria-current="step"]');
            if (!(activeStep instanceof HTMLElement) || !activeStep.isConnected) {
                return;
            }

            activeStep.scrollIntoView({
                block: 'nearest',
                inline: 'nearest'
            });
        });
    }

    function focusCandidateWillBeHidden(focusCandidate, effective) {
        const hiddenByCompact = effective === 'compact'
            && focusCandidate.closest('.menu-shell, .tool-strip, .desktop-shell-topline, .mdi-strip');
        const hiddenByWorkspace = effective === 'workspace'
            && focusCandidate.closest('.build-pwa-compact-context, .build-pwa-mobile-dock');
        return Boolean(hiddenByCompact || hiddenByWorkspace);
    }

    function planFocusRepair(shell, workspace, effective) {
        const repairToken = ++focusRepairSequence;
        pendingFocusRepairs.set(shell, repairToken);
        const activeBeforeLayout = document.activeElement;
        const focusCandidate = activeBeforeLayout instanceof HTMLElement
            && !isNeutralDocumentFocus(activeBeforeLayout)
            && shell.contains(activeBeforeLayout)
            ? activeBeforeLayout
            : isNeutralDocumentFocus(activeBeforeLayout)
                && lastFocusedElement instanceof HTMLElement
                && shell.contains(lastFocusedElement)
                && lastFocusedInteractionGeneration === interactionGeneration
                ? lastFocusedElement
                : null;
        if (!(focusCandidate instanceof HTMLElement)) {
            pendingFocusRepairs.delete(shell);
            return null;
        }

        if (!focusCandidateWillBeHidden(focusCandidate, effective)) {
            pendingFocusRepairs.delete(shell);
            return null;
        }

        return {
            effective,
            focusCandidate,
            focusGeneration: meaningfulFocusGeneration,
            interactionGeneration,
            repairToken,
            shell,
            workspace
        };
    }

    function findAccessibleFocusTarget(workspace, effective) {
        const selectors = effective === 'compact'
            ? [
                '.build-pwa-mobile-command-menu > summary',
                '#chummer-workspace-main',
                '[data-nav-tab][aria-current="step"]:not([disabled])'
            ]
            : [
                '#chummer-workspace-main',
                '[data-nav-tab][aria-current="step"]:not([disabled])'
            ];
        return selectors
            .map((selector) => workspace.querySelector(selector))
            .find((target) => target instanceof HTMLElement
                && target.isConnected
                && target.getClientRects().length > 0
                && !target.closest('[inert]')) ?? null;
    }

    function moveFocusIfLayoutHidesIt(plan) {
        if (plan === null) {
            return;
        }

        window.requestAnimationFrame(() => {
            if (pendingFocusRepairs.get(plan.shell) !== plan.repairToken) {
                return;
            }
            pendingFocusRepairs.delete(plan.shell);

            if (meaningfulFocusGeneration !== plan.focusGeneration
                || interactionGeneration !== plan.interactionGeneration) {
                return;
            }

            const activeNow = document.activeElement;
            if (activeNow !== plan.focusCandidate && !isNeutralDocumentFocus(activeNow)) {
                return;
            }

            if (plan.focusCandidate.isConnected && plan.focusCandidate.getClientRects().length > 0) {
                return;
            }

            const target = findAccessibleFocusTarget(plan.workspace, plan.effective);
            if (!(target instanceof HTMLElement)
                || meaningfulFocusGeneration !== plan.focusGeneration
                || interactionGeneration !== plan.interactionGeneration) {
                return;
            }

            target.focus({ preventScroll: true });
        });
    }

    function setDatasetValue(element, name, value) {
        if (element.dataset[name] !== value) {
            element.dataset[name] = value;
        }
    }

    function applyToShell(shell, workspace, preference, effective, fit) {
        const focusRepair = planFocusRepair(shell, workspace, effective);
        const reason = layoutReason(preference, effective);
        shell.classList.toggle('desktop-shell--build-layout-compact', effective === 'compact');
        shell.classList.toggle('desktop-shell--build-layout-workspace', effective === 'workspace');
        setDatasetValue(shell, 'buildPwaLayoutPreference', preference);
        setDatasetValue(shell, 'buildPwaLayoutEffective', effective);
        setDatasetValue(shell, 'buildPwaLayoutReason', reason);

        workspace.classList.toggle('build-pwa-layout--compact', effective === 'compact');
        workspace.classList.toggle('build-pwa-layout--workspace', effective === 'workspace');
        setDatasetValue(workspace, 'buildPwaLayoutPreference', preference);
        setDatasetValue(workspace, 'buildPwaLayoutEffective', effective);
        setDatasetValue(workspace, 'buildPwaLayoutReason', reason);
        setDatasetValue(workspace, 'buildPwaLayoutAvailableInlineSize', fit.availableInlineSize.toFixed(2));
        setDatasetValue(workspace, 'buildPwaLayoutMinimumInlineSize', fit.minimumInlineSize.toFixed(2));

        workspace.querySelectorAll('[data-build-pwa-layout-choice]').forEach((choice) => {
            const checked = choice.value === preference;
            if (choice.checked !== checked) {
                choice.checked = checked;
            }
        });

        const status = workspace.querySelector('#build-pwa-layout-status');
        if (status) {
            const nextStatus = statusText(preference, effective);
            if (status.textContent !== nextStatus) {
                status.textContent = nextStatus;
            }
        }

        moveFocusIfLayoutHidesIt(focusRepair);
    }

    function applyAll() {
        applyScheduled = false;
        const preference = readStoredPreference();
        const decisions = Array.from(document.querySelectorAll('.desktop-shell--responsive-build'))
            .map((shell) => {
                const workspace = shell.querySelector('.build-pwa-workspace');
                if (!(workspace instanceof HTMLElement)) {
                    return null;
                }

                observeShellGeometry(shell);
                const fit = measureWorkspaceFit(shell, workspace);
                return {
                    effective: effectiveLayout(preference, fit.fits),
                    fit,
                    shell,
                    workspace
                };
            })
            .filter(Boolean);

        decisions.forEach((decision) => {
            applyToShell(
                decision.shell,
                decision.workspace,
                preference,
                decision.effective,
                decision.fit);
        });
        return decisions[0]?.effective ?? (compactMedia.matches ? 'compact' : 'workspace');
    }

    function scheduleApply() {
        if (applyScheduled) {
            return;
        }

        applyScheduled = true;
        window.requestAnimationFrame(applyBootstrapLayout);
    }

    function setPreference(value) {
        const preference = normalizePreference(value);
        memoryPreference = preference;
        try {
            window.localStorage.setItem(storageKey, preference);
        } catch {
            // The in-memory preference still applies when storage is unavailable.
        }

        applyAll();
        return preference;
    }

    document.addEventListener('change', (event) => {
        const choice = event.target instanceof Element
            ? event.target.closest('[data-build-pwa-layout-choice]')
            : null;
        if (!choice) {
            return;
        }

        setPreference(choice.value);
    });

    const geometryObserver = typeof window.ResizeObserver === 'function'
        ? new window.ResizeObserver((entries) => {
            let inlineSizeChanged = false;
            entries.forEach((entry) => {
                const borderBox = Array.isArray(entry.borderBoxSize)
                    ? entry.borderBoxSize[0]
                    : entry.borderBoxSize;
                const inlineSize = borderBox?.inlineSize ?? entry.contentRect.width;
                const previousInlineSize = observedInlineSizes.get(entry.target);
                if (previousInlineSize === undefined || Math.abs(previousInlineSize - inlineSize) > 0.25) {
                    observedInlineSizes.set(entry.target, inlineSize);
                    inlineSizeChanged = true;
                }
            });
            if (inlineSizeChanged) {
                scheduleApply();
            }
        })
        : null;

    function observeShellGeometry(shell) {
        if (geometryObserver === null || observedShells.has(shell)) {
            return;
        }

        observedShells.add(shell);
        geometryObserver.observe(shell);
    }

    const onBrowserLayoutChange = scheduleApply;
    if (typeof compactMedia.addEventListener === 'function') {
        compactMedia.addEventListener('change', onBrowserLayoutChange);
    } else if (typeof compactMedia.addListener === 'function') {
        compactMedia.addListener(onBrowserLayoutChange);
    }
    window.addEventListener('resize', scheduleApply, { passive: true });

    const activeStepObserver = new MutationObserver((mutations) => {
        const changedWorkspaces = new Set();
        mutations.forEach((mutation) => {
            const target = mutation.target instanceof Element
                ? mutation.target.closest('[data-nav-tab]')
                : null;
            const workspace = target?.closest('.build-pwa-workspace');
            if (workspace) {
                changedWorkspaces.add(workspace);
            }
        });
        changedWorkspaces.forEach(scrollActiveStepIntoView);
    });
    activeStepObserver.observe(document.body, {
        attributes: true,
        attributeFilter: ['aria-current'],
        subtree: true
    });

    const observer = new MutationObserver(scheduleApply);
    observer.observe(document.body, { childList: true, subtree: true });
    const bootstrapObserverTimeout = window.setTimeout(() => observer.disconnect(), 15000);

    function stopBootstrapObserverWhenReady() {
        if (!document.querySelector('.desktop-shell--responsive-build')) {
            return;
        }

        observer.disconnect();
        window.clearTimeout(bootstrapObserverTimeout);
    }

    const applyBootstrapLayout = () => {
        const effective = applyAll();
        stopBootstrapObserverWhenReady();
        return effective;
    };

    window.chummerBuildPwaLayout = Object.freeze({
        applyAll,
        compactQuery,
        getEffectiveLayout: () => {
            const shell = document.querySelector('.desktop-shell--responsive-build');
            const workspace = shell?.querySelector('.build-pwa-workspace');
            if (!(shell instanceof HTMLElement) || !(workspace instanceof HTMLElement)) {
                return compactMedia.matches ? 'compact' : 'workspace';
            }

            return effectiveLayout(readStoredPreference(), measureWorkspaceFit(shell, workspace).fits);
        },
        getPreference: readStoredPreference,
        setPreference,
        storageKey
    });

    applyBootstrapLayout();
})(window, document);
