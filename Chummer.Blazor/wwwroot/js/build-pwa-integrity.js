(function initializeBuildPwaIntegrity(window, document) {
    'use strict';

    const expectedAuthority = window.chummerPwa?.expectedAuthority;
    const configuredOwnerTokens = expectedAuthority?.ownerInvalidationTokens;
    const ownerInvalidationTokens = Object.isFrozen(expectedAuthority)
        && Array.isArray(configuredOwnerTokens)
        && Object.isFrozen(configuredOwnerTokens)
        && configuredOwnerTokens.length >= 1
        && configuredOwnerTokens.length <= 2
        && configuredOwnerTokens.every(token => typeof token === 'string' && /^[0-9a-f]{64}$/.test(token))
        && new Set(configuredOwnerTokens).size === configuredOwnerTokens.length
        ? Object.freeze(Array.from(configuredOwnerTokens))
        : Object.freeze([]);
    const channelNames = Object.freeze(ownerInvalidationTokens.map(
        token => `chummer-build-workspace-integrity-v1-${token}`));
    const channelName = channelNames[0] || null;
    const changedEventName = 'chummer:build-integrity-changed';
    const bridgeRecoveryEventName = 'chummer:build-integrity-bridge-recovery-required';
    const broadcastMutationKinds = new Set(['workspace-update', 'checkpoint', 'delete']);
    const wireKeys = ['mutationKind', 'revision', 'workspaceId'];
    const snapshotKeys = [
        'bridgeAvailable',
        'contentRevision',
        'hasConflict',
        'isDirty',
        'savedRevision',
        'updateDeferred',
        'workspaceId'
    ];
    let bridge = null;
    let lastRecoveryBridge = null;
    let bridgeRegistrationToken = null;
    const channels = [];
    const recentlyHandledExternalMessages = new Map();
    let beforeUnloadBound = false;
    let bridgeRecoveryScheduled = false;
    let snapshot = freezeSnapshot({ bridgeAvailable: false });

    function normalizeWorkspaceId(value) {
        if (typeof value !== 'string') return null;
        const normalized = value.trim();
        return normalized.length > 0 && normalized.length <= 256 ? normalized : null;
    }

    function normalizeRevision(value) {
        return Number.isSafeInteger(value) && value >= 0 ? value : 0;
    }

    function isPositiveSafeRevision(value) {
        return Number.isSafeInteger(value) && value > 0;
    }

    function freezeSnapshot(value) {
        return Object.freeze({
            workspaceId: normalizeWorkspaceId(value?.workspaceId),
            contentRevision: normalizeRevision(value?.contentRevision),
            savedRevision: normalizeRevision(value?.savedRevision),
            isDirty: value?.isDirty === true,
            hasConflict: value?.hasConflict === true,
            updateDeferred: value?.updateDeferred === true,
            bridgeAvailable: value?.bridgeAvailable === true
        });
    }

    function isPlainExactObject(value, expectedKeys) {
        if (!value
            || typeof value !== 'object'
            || Array.isArray(value)
            || Object.getPrototypeOf(value) !== Object.prototype) return false;
        const keys = Object.keys(value).sort();
        return keys.length === expectedKeys.length
            && keys.every((key, index) => key === expectedKeys[index]);
    }

    function isValidSnapshotPayload(value) {
        if (!isPlainExactObject(value, snapshotKeys)) return false;
        if (value.workspaceId !== null && normalizeWorkspaceId(value.workspaceId) === null) return false;
        return Number.isSafeInteger(value.contentRevision)
            && value.contentRevision >= 0
            && Number.isSafeInteger(value.savedRevision)
            && value.savedRevision >= 0
            && typeof value.isDirty === 'boolean'
            && typeof value.hasConflict === 'boolean'
            && typeof value.updateDeferred === 'boolean'
            && value.bridgeAvailable === true;
    }

    function copySnapshot() {
        return freezeSnapshot(snapshot);
    }

    function shouldWarnBeforeUnload() {
        return snapshot.isDirty || snapshot.hasConflict;
    }

    function beforeUnloadHandler(event) {
        if (!shouldWarnBeforeUnload()) return;
        event.preventDefault();
        event.returnValue = '';
    }

    function syncBeforeUnload() {
        const shouldBind = shouldWarnBeforeUnload();
        if (shouldBind === beforeUnloadBound) return;

        beforeUnloadBound = shouldBind;
        if (shouldBind) {
            window.addEventListener('beforeunload', beforeUnloadHandler);
        } else {
            window.removeEventListener('beforeunload', beforeUnloadHandler);
        }
    }

    function dispatchChanged() {
        syncBeforeUnload();
        window.dispatchEvent(new CustomEvent(changedEventName, { detail: copySnapshot() }));
    }

    function isValidRegistrationToken(value) {
        return typeof value === 'string' && /^[0-9a-f]{32}$/.test(value);
    }

    function createRegistrationToken() {
        if (!window.crypto || typeof window.crypto.getRandomValues !== 'function') return null;
        try {
            const bytes = new Uint8Array(16);
            window.crypto.getRandomValues(bytes);
            return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
        } catch {
            return null;
        }
    }

    function isActiveRegistration(registrationToken) {
        return bridge !== null
            && isValidRegistrationToken(registrationToken)
            && registrationToken === bridgeRegistrationToken;
    }

    function markBridgeUnavailable(registrationToken) {
        if (!isActiveRegistration(registrationToken)) return false;
        const disconnectedBridge = bridge;
        lastRecoveryBridge = disconnectedBridge;
        bridge = null;
        bridgeRegistrationToken = null;
        snapshot = freezeSnapshot({ ...snapshot, bridgeAvailable: false });
        dispatchChanged();
        scheduleBridgeRecovery(disconnectedBridge);
        return true;
    }

    function scheduleBridgeRecovery(disconnectedBridge) {
        if (bridgeRecoveryScheduled) return;
        bridgeRecoveryScheduled = true;
        window.setTimeout(() => {
            bridgeRecoveryScheduled = false;
            window.dispatchEvent(new CustomEvent(bridgeRecoveryEventName));
            const recoveryBridge = disconnectedBridge || lastRecoveryBridge;
            if (!recoveryBridge || typeof recoveryBridge.invokeMethodAsync !== 'function') return;
            try {
                void recoveryBridge.invokeMethodAsync('RequestBuildPwaIntegrityBridgeRecoveryAsync')
                    .catch(() => { });
            } catch {
                // A disconnected circuit will construct a fresh component and
                // registration episode when it reconnects.
            }
        }, 0);
    }

    function isExactWireMessage(value) {
        if (!isPlainExactObject(value, wireKeys)) return false;
        return normalizeWorkspaceId(value.workspaceId) !== null
            && isPositiveSafeRevision(value.revision)
            && broadcastMutationKinds.has(value.mutationKind);
    }

    function isDuplicateRotatedMessage(message) {
        const now = Date.now();
        const key = `${message.workspaceId}\n${message.revision}\n${message.mutationKind}`;
        const lastSeen = recentlyHandledExternalMessages.get(key);
        recentlyHandledExternalMessages.set(key, now);
        for (const [candidate, seenAt] of recentlyHandledExternalMessages) {
            if ((now - seenAt) > 1000) recentlyHandledExternalMessages.delete(candidate);
        }
        return Number.isFinite(lastSeen)
            && (now - lastSeen) >= 0
            && (now - lastSeen) <= 1000;
    }

    function postRevision(workspaceId, revision, mutationKind) {
        if (channels.length === 0
            || normalizeWorkspaceId(workspaceId) === null
            || !isPositiveSafeRevision(revision)
            || !broadcastMutationKinds.has(mutationKind)) return false;
        const message = Object.freeze({ workspaceId, revision, mutationKind });
        let posted = false;
        for (let index = channels.length - 1; index >= 0; index -= 1) {
            const channel = channels[index];
            try {
                channel.postMessage(message);
                posted = true;
            } catch {
                try {
                    channel.close();
                } catch {
                    // The channel is already unusable; local unload protection remains active.
                }
                channels.splice(index, 1);
            }
        }
        return posted;
    }

    async function handleExternalRevision(message) {
        if (!isExactWireMessage(message)) return;
        if (!snapshot.workspaceId || message.workspaceId !== snapshot.workspaceId) return;
        const isSameRevisionDelete = message.mutationKind === 'delete'
            && message.revision === snapshot.contentRevision;
        if (message.revision < snapshot.contentRevision
            || (message.revision === snapshot.contentRevision && !isSameRevisionDelete)) return;
        if (isDuplicateRotatedMessage(message)) return;
        const activeBridge = bridge;
        const activeToken = bridgeRegistrationToken;
        if (!activeBridge || !isActiveRegistration(activeToken)) {
            return;
        }

        try {
            await activeBridge.invokeMethodAsync(
                'HandleExternalWorkspaceRevisionAsync',
                message.workspaceId,
                message.revision,
                message.mutationKind);
        } catch {
            markBridgeUnavailable(activeToken);
        }
    }

    if (typeof window.BroadcastChannel === 'function') {
        for (const expectedChannelName of channelNames) {
            try {
                const channel = new window.BroadcastChannel(expectedChannelName);
                channel.addEventListener('message', (event) => {
                    void handleExternalRevision(event.data);
                });
                channels.push(channel);
            } catch {
                // Continue with the remaining rotation channel, if any.
            }
        }
    }

    function registerBridge(dotNetBridge) {
        if (!dotNetBridge || typeof dotNetBridge.invokeMethodAsync !== 'function') {
            return null;
        }

        lastRecoveryBridge = dotNetBridge;
        const registrationToken = createRegistrationToken();
        if (!registrationToken) {
            bridge = null;
            bridgeRegistrationToken = null;
            snapshot = freezeSnapshot({ ...snapshot, bridgeAvailable: false });
            dispatchChanged();
            return null;
        }

        bridge = dotNetBridge;
        bridgeRegistrationToken = registrationToken;
        bridgeRecoveryScheduled = false;
        snapshot = freezeSnapshot({ ...snapshot, bridgeAvailable: true });
        dispatchChanged();
        return registrationToken;
    }

    function unregisterBridge(registrationToken) {
        if (!isActiveRegistration(registrationToken)) return false;
        bridge = null;
        lastRecoveryBridge = null;
        bridgeRegistrationToken = null;
        snapshot = freezeSnapshot({ ...snapshot, bridgeAvailable: false });
        dispatchChanged();
        return true;
    }

    function requestBridgeRecoveryIfUnavailable() {
        if (bridge === null) scheduleBridgeRecovery();
    }

    window.addEventListener('focus', requestBridgeRecoveryIfUnavailable);
    window.addEventListener('pageshow', requestBridgeRecoveryIfUnavailable);
    window.addEventListener('online', requestBridgeRecoveryIfUnavailable);
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') requestBridgeRecoveryIfUnavailable();
    });

    function updateState(value, mutationKind, registrationToken) {
        if (!isActiveRegistration(registrationToken)) return copySnapshot();
        if (!isValidSnapshotPayload(value)) {
            markBridgeUnavailable(registrationToken);
            return copySnapshot();
        }

        const previous = snapshot;
        snapshot = freezeSnapshot({ ...value, bridgeAvailable: value?.bridgeAvailable !== false });

        const normalizedMutationKind = broadcastMutationKinds.has(mutationKind) ? mutationKind : null;
        const revisionAdvanced = snapshot.contentRevision > previous.contentRevision
            || (normalizedMutationKind === 'checkpoint' && snapshot.savedRevision > previous.savedRevision);
        if (normalizedMutationKind && revisionAdvanced) {
            postRevision(snapshot.workspaceId, snapshot.contentRevision, normalizedMutationKind);
        }

        dispatchChanged();
        return copySnapshot();
    }

    function publishDelete(workspaceId, revision, registrationToken) {
        if (!isActiveRegistration(registrationToken)) return false;
        const normalizedWorkspaceId = normalizeWorkspaceId(workspaceId);
        if (!normalizedWorkspaceId
            || normalizedWorkspaceId !== snapshot.workspaceId
            || !isPositiveSafeRevision(revision)
            || revision < snapshot.contentRevision) {
            return false;
        }

        return postRevision(normalizedWorkspaceId, revision, 'delete');
    }

    function setUpdateDeferred(deferred) {
        const activeBridge = bridge;
        const activeToken = bridgeRegistrationToken;
        if (!activeBridge || !isActiveRegistration(activeToken)) return false;

        snapshot = freezeSnapshot({ ...snapshot, updateDeferred: deferred === true });
        dispatchChanged();
        try {
            void activeBridge.invokeMethodAsync('SetUpdateDeferredAsync', deferred === true)
                .catch(() => markBridgeUnavailable(activeToken));
        } catch {
            markBridgeUnavailable(activeToken);
            return false;
        }
        return true;
    }

    async function canReload() {
        const activeBridge = bridge;
        const activeToken = bridgeRegistrationToken;
        if (!activeBridge || !isActiveRegistration(activeToken)) {
            return false;
        }

        try {
            const liveState = await activeBridge.invokeMethodAsync('GetBuildPwaIntegrityStateAsync');
            if (!isActiveRegistration(activeToken)) return false;
            if (!isValidSnapshotPayload(liveState)) {
                markBridgeUnavailable(activeToken);
                return false;
            }
            snapshot = freezeSnapshot({ ...liveState, bridgeAvailable: true });
            dispatchChanged();
            return !snapshot.isDirty && !snapshot.hasConflict;
        } catch {
            markBridgeUnavailable(activeToken);
            return false;
        }
    }

    window.chummerBuildPwaIntegrity = Object.freeze({
        registerBridge,
        unregisterBridge,
        updateState,
        publishDelete,
        markBridgeUnavailable,
        setUpdateDeferred,
        getSnapshot: copySnapshot,
        canReload,
        channelName,
        channelNames,
        changedEventName,
        bridgeRecoveryEventName
    });
})(window, document);
