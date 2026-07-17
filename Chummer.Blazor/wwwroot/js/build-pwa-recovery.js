(function initializeBuildPwaRecoveryDownloads(window, document) {
    'use strict';

    const downloads = window.chummerDownloads = window.chummerDownloads || {};
    const maxPayloadBytes = 8 * 1024 * 1024;
    const pickerLifetimeMs = 60000;
    const supportedMimeTypes = new Set(['application/xml', 'application/json']);
    downloads._pendingRecoveryPicker = null;

    function releasePendingPicker(pendingPicker, expired) {
        if (!pendingPicker) return;
        pendingPicker.expired = expired === true;
        pendingPicker.handle = null;
        pendingPicker.error = null;
        pendingPicker.settledPromise = null;
        if (pendingPicker.expirationTimer !== null) {
            window.clearTimeout(pendingPicker.expirationTimer);
            pendingPicker.expirationTimer = null;
        }
        if (downloads._pendingRecoveryPicker === pendingPicker) {
            downloads._pendingRecoveryPicker = null;
        }
    }

    downloads.captureRecoverySaveGesture = function(event) {
        if (event?.isTrusted !== true) return;
        const target = event?.target instanceof Element ? event.target : null;
        const saveButton = target?.closest(
            '[data-build-pwa-integrity-save-copy][data-build-pwa-recovery-save="true"]');
        if (!saveButton) {
            return;
        }

        // Never let a handle from an earlier click authorize this save.
        releasePendingPicker(downloads._pendingRecoveryPicker, true);
        if (typeof window.showSaveFilePicker !== 'function'
            || window.navigator.userActivation?.isActive !== true) {
            return;
        }

        const pendingPicker = {
            createdAt: Date.now(),
            expired: false,
            handle: null,
            error: null,
            settledPromise: null,
            expirationTimer: null
        };
        pendingPicker.settledPromise = window.showSaveFilePicker({
            suggestedName: 'chummer-runner.recovery.chum5',
            types: [{
                description: 'Chummer recovery file',
                accept: {
                    'application/xml': ['.chum5'],
                    'application/json': ['.json']
                }
            }]
        }).then(handle => {
            if (!pendingPicker.expired) pendingPicker.handle = handle;
        }).catch(error => {
            if (!pendingPicker.expired) {
                pendingPicker.error = error || new Error('Recovery picker failed.');
            }
        });
        pendingPicker.expirationTimer = window.setTimeout(
            () => releasePendingPicker(pendingPicker, true),
            pickerLifetimeMs);
        downloads._pendingRecoveryPicker = pendingPicker;
    };

    if (!downloads._recoveryGestureBound) {
        downloads._recoveryGestureBound = true;
        document.addEventListener('click', downloads.captureRecoverySaveGesture, true);
    }

    downloads.saveRecoveryStream = async function(
        fileName,
        mimeType,
        documentLength,
        exportToken,
        streamReference) {
        const outcome = (status, error) => Object.freeze({ status, error: error || null });
        if (typeof fileName !== 'string'
            || fileName.length < 1
            || fileName.length > 128
            || !supportedMimeTypes.has(mimeType)
            || !Number.isSafeInteger(documentLength)
            || documentLength < 1
            || documentLength > maxPayloadBytes
            || typeof exportToken !== 'string'
            || !/^[0-9a-f]{64}$/.test(exportToken)
            || !streamReference
            || typeof streamReference.arrayBuffer !== 'function') {
            return outcome('stale', 'Recovery export metadata was invalid.');
        }

        let bytes = null;
        let objectUrl = null;
        let writable = null;
        try {
            const buffer = await streamReference.arrayBuffer();
            if (!(buffer instanceof ArrayBuffer)) {
                return outcome('stale', 'Recovery byte length changed before save.');
            }

            bytes = new Uint8Array(buffer);
            if (bytes.byteLength !== documentLength) {
                return outcome('stale', 'Recovery byte length changed before save.');
            }
            const pendingPicker = downloads._pendingRecoveryPicker;
            downloads._pendingRecoveryPicker = null;
            if (pendingPicker
                && pendingPicker.expired !== true
                && Number.isSafeInteger(pendingPicker.createdAt)
                && (Date.now() - pendingPicker.createdAt) >= 0
                && (Date.now() - pendingPicker.createdAt) <= pickerLifetimeMs
                && pendingPicker.settledPromise instanceof Promise) {
                await pendingPicker.settledPromise;
                if (pendingPicker.expired === true) {
                    releasePendingPicker(pendingPicker, true);
                    return outcome('blocked', 'The gesture-bound recovery picker expired.');
                }

                const pickerError = pendingPicker.error;
                const handle = pendingPicker.handle;
                releasePendingPicker(pendingPicker, false);
                if (pickerError) {
                    return outcome(pickerError.name === 'AbortError' ? 'cancelled' : 'blocked');
                }

                if (!handle || typeof handle.createWritable !== 'function') {
                    return outcome('blocked', 'The gesture-bound recovery picker became unavailable.');
                }

                try {
                    writable = await handle.createWritable();
                    await writable.write(bytes);
                    await writable.close();
                    writable = null;
                    return outcome('durable_saved');
                } catch {
                    if (writable && typeof writable.abort === 'function') {
                        try { await writable.abort(); } catch { }
                    }
                    writable = null;
                    return outcome('failed', 'The selected recovery file could not be written and closed.');
                }
            }
            else if (pendingPicker) {
                releasePendingPicker(pendingPicker, true);
            }

            const blob = new Blob([bytes], { type: mimeType });
            objectUrl = URL.createObjectURL(blob);
            const anchor = document.createElement('a');
            anchor.href = objectUrl;
            anchor.download = fileName;
            anchor.rel = 'noopener';
            document.body.appendChild(anchor);
            anchor.click();
            anchor.remove();
            return outcome('dispatched_requires_explicit_user_ack');
        } catch {
            return outcome('failed', 'Recovery bytes could not be transferred to the browser.');
        } finally {
            if (writable && typeof writable.abort === 'function') {
                try { await writable.abort(); } catch { }
            }
            if (objectUrl) {
                try { URL.revokeObjectURL(objectUrl); } catch { }
            }
            if (bytes) {
                try { bytes.fill(0); } catch { }
            }
        }
    };
})(window, document);
