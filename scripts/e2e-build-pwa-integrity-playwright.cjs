#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.CHUMMER_PLAYWRIGHT_MODULE || 'playwright');

const integrityRuntimeScript = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'js', 'build-pwa-integrity.js'),
  'utf8');
const recoveryRuntimeScript = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'js', 'build-pwa-recovery.js'),
  'utf8');
const installScript = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'js', 'build-pwa-install.js'),
  'utf8');
const serviceWorkerScript = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'service-worker.js'),
  'utf8');
const installStyles = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'build-pwa-install.css'),
  'utf8');
const expectedCacheVersion = serviceWorkerScript.match(
  /const CHUMMER_BUILD_PWA_CACHE_VERSION = '([^']+)';/)?.[1];
const expectedReleaseRevision = 'a'.repeat(64);
assertBootstrapContract();
const harnessUrl = 'http://127.0.0.1:41789/app';
const harnessOrigin = new URL(harnessUrl).origin;
const ownerInvalidationToken = 'a'.repeat(64);
const channelName = `chummer-build-workspace-integrity-v1-${ownerInvalidationToken}`;
const eventName = 'chummer:build-integrity-changed';
const snapshotKeys = [
  'bridgeAvailable',
  'contentRevision',
  'hasConflict',
  'isDirty',
  'savedRevision',
  'updateDeferred',
  'workspaceId'
];
const wireKeys = ['mutationKind', 'revision', 'workspaceId'];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function assertBootstrapContract() {
  if (!expectedCacheVersion || !expectedReleaseRevision) {
    throw new Error('Build PWA cache lease/release revision source contract is missing.');
  }
}

function harnessDocument(scopePath = '/') {
  assert(/^\/[a-z/]*$/.test(scopePath), `Unsafe test scope path: ${scopePath}`);
  const safeIntegrityScript = integrityRuntimeScript.replaceAll('</script', '<\\/script');
  const safeInstallScript = installScript.replaceAll('</script', '<\\/script');
  return `<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>Build integrity harness</title></head>
<body>
  <button type="button"
          data-build-pwa-integrity-save-copy
          data-build-pwa-recovery-save="true">Save exact recovery copy</button>
  <button type="button"
          data-build-pwa-install-help
          aria-controls="build-pwa-install-panel"
          aria-expanded="false"
          hidden>Install app</button>
  <section id="build-pwa-install-panel"
           data-build-pwa-install
           aria-labelledby="build-pwa-install-heading"
           hidden>
    <h1 id="build-pwa-install-heading">Chummer Build install</h1>
    <p data-build-pwa-install-status role="status" aria-live="polite" aria-atomic="true">Ready.</p>
    <section data-build-pwa-install-handoff
             data-build-pwa-handoff-effective="pending"
             aria-labelledby="build-pwa-install-handoff-heading">
      <h2 id="build-pwa-install-handoff-heading">Choose where to install</h2>
      <fieldset>
        <legend>Installation handoff</legend>
        <label><input type="radio" name="build-pwa-install-device" value="auto" data-build-pwa-install-device-choice="auto" checked>Auto</label>
        <label><input type="radio" name="build-pwa-install-device" value="mobile" data-build-pwa-install-device-choice="mobile">Mobile</label>
        <label><input type="radio" name="build-pwa-install-device" value="desktop" data-build-pwa-install-device-choice="desktop">Desktop</label>
      </fieldset>
      <p id="build-pwa-install-device-status" data-build-pwa-install-device-status role="status" aria-live="polite"></p>
      <div data-build-pwa-desktop-handoff hidden>
        <div data-build-pwa-install-qr></div>
        <a data-build-pwa-install-link target="_blank" rel="noopener noreferrer">Open install page</a>
        <code data-build-pwa-install-link-text></code>
        <button type="button" data-build-pwa-copy-install-link>Copy mobile install link</button>
      </div>
      <div data-build-pwa-mobile-handoff hidden><p>Install on this device.</p></div>
    </section>
    <button type="button" data-build-pwa-install-action hidden>Install</button>
    <button type="button"
            data-build-pwa-update-action
            aria-controls="build-pwa-update-guidance"
            aria-expanded="false"
            hidden>Review update steps</button>
    <button type="button" data-build-pwa-dismiss-action>Not now</button>
    <section id="build-pwa-update-guidance"
             data-build-pwa-update-guidance
             tabindex="-1"
             hidden>
      <h2>Finish the update safely</h2>
      <p>Save or copy work, close every Chummer Build window, then reopen.</p>
    </section>
    <details data-build-pwa-manual><summary>Manual install steps</summary></details>
  </section>
  <main id="chummer-workspace-main" tabindex="-1">Build workspace</main>
  <script>
    window.chummerPwa = window.chummerPwa || {};
    Object.defineProperty(window.chummerPwa, 'expectedAuthority', {
      value: Object.freeze({
        scriptUrl: new URL('service-worker.js?build=${expectedReleaseRevision}',
          new URL('${scopePath}', document.baseURI)).href,
        scope: new URL('${scopePath}', document.baseURI).href,
        contentRevision: '${expectedReleaseRevision}',
        ownerInvalidationTokens: Object.freeze(['${ownerInvalidationToken}'])
      }),
      writable: false,
      configurable: false,
      enumerable: true
    });
    try {
      Object.defineProperty(navigator.serviceWorker, 'controller', {
        configurable: true,
        value: Object.freeze({
          scriptURL: new URL('/service-worker-incumbent.js', document.baseURI).href
        })
      });
    } catch (_error) {
    }
    window.__incumbentControllerAtStartup = navigator.serviceWorker?.controller?.scriptURL || null;
  </script>
  <script src="/js/build-pwa-recovery.js"></script>
  <script>${safeIntegrityScript}</script>
  <script>${safeInstallScript}</script>
</body>
</html>`;
}

async function createHarnessContext(browser, { scopePath = '/', contextOptions = {} } = {}) {
  const context = await browser.newContext(contextOptions);
  await context.addInitScript(({ expectedChannel, expectedEvent }) => {
    window.__integrityWireMessages = [];
    window.__integrityEvents = [];
    window.__bridgeCalls = [];
    window.__bridgeNextSnapshot = null;
    window.__waitingWorkerMessages = [];

    window.addEventListener(expectedEvent, (event) => {
      window.__integrityEvents.push(JSON.parse(JSON.stringify(event.detail)));
    });

    const NativeBroadcastChannel = window.BroadcastChannel;
    window.BroadcastChannel = class RecordingBroadcastChannel extends NativeBroadcastChannel {
      constructor(name) {
        super(name);
        this.__chummerName = name;
      }

      postMessage(payload) {
        if (this.__chummerName === expectedChannel) {
          window.__integrityWireMessages.push(JSON.parse(JSON.stringify(payload)));
        }
        return super.postMessage(payload);
      }
    };
  }, { expectedChannel: channelName, expectedEvent: eventName });
  await context.route(`${harnessOrigin}/**`, (route) => {
    const url = new URL(route.request().url());
    const response = {
      '/app': ['text/html; charset=utf-8', harnessDocument(scopePath)],
      '/js/build-pwa-recovery.js': ['text/javascript; charset=utf-8', recoveryRuntimeScript],
      '/js/build-pwa-integrity.js': ['text/javascript; charset=utf-8', integrityRuntimeScript],
      '/js/build-pwa-install.js': ['text/javascript; charset=utf-8', installScript],
      '/service-worker.js': ['text/javascript; charset=utf-8', serviceWorkerScript],
      '/build-pwa-install.css': ['text/css; charset=utf-8', installStyles]
    }[url.pathname];
    return response
      ? route.fulfill({ status: 200, contentType: response[0], body: response[1] })
      : route.fulfill({ status: 404, contentType: 'text/plain', body: 'not found' });
  });
  return context;
}

async function openHarnessPage(context) {
  const page = await context.newPage();
  await page.goto(harnessUrl, { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => Boolean(window.chummerBuildPwaIntegrity));
  return page;
}

async function registerTestBridge(page) {
  const registrationToken = await page.evaluate(() => {
    let activeToken = null;
    window.__testIntegrityBridge = {
      invokeMethodAsync: async (...args) => {
        window.__bridgeCalls.push(JSON.parse(JSON.stringify(args)));
        const method = args[0];
        let next = window.__bridgeNextSnapshot;
        if (!next && method === 'HandleExternalWorkspaceRevisionAsync') {
          const current = window.chummerBuildPwaIntegrity.getSnapshot();
          const remoteRevision = Number(args[2]);
          const remoteMutationKind = args[3];
          next = current.isDirty || current.hasConflict
            ? { ...current, hasConflict: true }
            : {
                ...current,
                contentRevision: remoteRevision,
                savedRevision: remoteMutationKind === 'checkpoint'
                  ? remoteRevision
                  : current.savedRevision
              };
        }
        if (next) {
          window.__bridgeNextSnapshot = null;
          window.setTimeout(() => {
            window.chummerBuildPwaIntegrity.updateState(next, 'refresh', activeToken);
          }, 0);
        }
        return next || window.chummerBuildPwaIntegrity.getSnapshot();
      }
    };
    activeToken = window.chummerBuildPwaIntegrity.registerBridge(window.__testIntegrityBridge);
    window.__integrityToken = activeToken;
    return activeToken;
  });
  assert(/^[0-9a-f]{32}$/.test(registrationToken || ''),
    `Bridge registration did not return a secure opaque token: ${registrationToken}`);
}

function cleanSnapshot(revision) {
  return {
    workspaceId: 'browser-integrity-workspace',
    contentRevision: revision,
    savedRevision: revision,
    isDirty: false,
    hasConflict: false,
    updateDeferred: false,
    bridgeAvailable: true
  };
}

async function setState(page, snapshot, mutationKind) {
  await page.evaluate(
    async ({ next, kind }) => window.chummerBuildPwaIntegrity.updateState(
      next,
      kind,
      window.__integrityToken),
    { next: snapshot, kind: mutationKind });
}

async function clearProbeReceipts(page) {
  await page.evaluate(() => {
    window.__integrityWireMessages.length = 0;
    window.__integrityEvents.length = 0;
    window.__bridgeCalls.length = 0;
  });
}

async function invokeRecoverySave(page, bytes, declaredLength = bytes.length) {
  return page.evaluate(async ({ sourceBytes, expectedLength, token }) => {
    const source = new Uint8Array(sourceBytes);
    const outcome = await window.chummerDownloads.saveRecoveryStream(
      'browser-integrity-workspace.recovery.chum5',
      'application/xml',
      expectedLength,
      token,
      { arrayBuffer: async () => source.buffer });
    return {
      outcome,
      sourceZeroed: source.every((value) => value === 0),
      pickerPending: window.chummerDownloads._pendingRecoveryPicker !== null
    };
  }, {
    sourceBytes: bytes,
    expectedLength: declaredLength,
    token: 'c'.repeat(64)
  });
}

async function runRecoveryStreamOutcomes(browser) {
  const context = await createHarnessContext(browser);
  const page = await openHarnessPage(context);
  const saveButton = page.locator(
    '[data-build-pwa-integrity-save-copy][data-build-pwa-recovery-save="true"]');

  try {
    await page.waitForFunction(() =>
      typeof window.chummerDownloads?.saveRecoveryStream === 'function');
    await page.evaluate(() => {
      window.__recoveryReceipts = {
        pickerCalls: 0,
        writes: [],
        closes: 0,
        aborts: 0
      };
      Object.defineProperty(window, 'showSaveFilePicker', {
        configurable: true,
        value: async () => {
          window.__recoveryReceipts.pickerCalls += 1;
          return {
            createWritable: async () => ({
              write: async (bytes) => {
                window.__recoveryReceipts.writes.push(Array.from(bytes));
              },
              close: async () => { window.__recoveryReceipts.closes += 1; },
              abort: async () => { window.__recoveryReceipts.aborts += 1; }
            })
          };
        }
      });
    });
    await saveButton.click();
    const durable = await invokeRecoverySave(page, [60, 99, 104, 97, 114, 97, 99, 116, 101, 114, 47, 62]);
    const durableReceipts = await page.evaluate(() => window.__recoveryReceipts);
    assert(durable.outcome.status === 'durable_saved'
      && durableReceipts.pickerCalls === 1
      && durableReceipts.closes === 1
      && durableReceipts.aborts === 0,
    `Gesture-bound File System Access save was not durably verified: ${JSON.stringify({ durable, durableReceipts })}`);
    assert(durableReceipts.writes.length === 1
      && durableReceipts.writes[0].some((value) => value !== 0),
    'The durable save did not write the streamed recovery bytes before cleanup.');
    assert(durable.sourceZeroed && !durable.pickerPending,
      'Durable recovery completion retained a byte buffer or reusable picker handle.');

    await page.evaluate(() => {
      window.__recoveryFallback = { clicks: 0, creates: 0, revokes: 0 };
      Object.defineProperty(window, 'showSaveFilePicker', {
        configurable: true,
        value: undefined
      });
      URL.createObjectURL = () => {
        window.__recoveryFallback.creates += 1;
        return 'blob:http://127.0.0.1/recovery-fallback';
      };
      URL.revokeObjectURL = () => { window.__recoveryFallback.revokes += 1; };
      HTMLAnchorElement.prototype.click = function() {
        window.__recoveryFallback.clicks += 1;
      };
    });
    await saveButton.click();
    const fallback = await invokeRecoverySave(page, [123, 34, 114, 117, 110, 110, 101, 114, 34, 58, 49, 125]);
    const fallbackReceipts = await page.evaluate(() => window.__recoveryFallback);
    assert(fallback.outcome.status === 'dispatched_requires_explicit_user_ack',
      `Blob fallback falsely claimed durable completion: ${JSON.stringify(fallback.outcome)}`);
    assert(fallbackReceipts.clicks === 1
      && fallbackReceipts.creates === 1
      && fallbackReceipts.revokes === 1
      && fallback.sourceZeroed
      && !fallback.pickerPending,
    `Blob fallback cleanup or dispatch receipt was incomplete: ${JSON.stringify({ fallback, fallbackReceipts })}`);

    await page.evaluate(() => {
      Object.defineProperty(window, 'showSaveFilePicker', {
        configurable: true,
        value: async () => { throw new DOMException('cancelled', 'AbortError'); }
      });
    });
    await saveButton.click();
    const cancelled = await invokeRecoverySave(page, [1, 2, 3, 4]);
    assert(cancelled.outcome.status === 'cancelled'
      && cancelled.sourceZeroed
      && !cancelled.pickerPending,
    `Picker cancellation did not retain a retry-safe outcome: ${JSON.stringify(cancelled)}`);

    await page.evaluate(() => {
      Object.defineProperty(window, 'showSaveFilePicker', {
        configurable: true,
        value: async () => { throw new DOMException('blocked', 'SecurityError'); }
      });
    });
    await saveButton.click();
    const blocked = await invokeRecoverySave(page, [5, 6, 7, 8]);
    assert(blocked.outcome.status === 'blocked'
      && blocked.sourceZeroed
      && !blocked.pickerPending,
    `Blocked picker falsely claimed durable completion: ${JSON.stringify(blocked)}`);

    await page.evaluate(() => {
      window.__recoveryWriteAborts = 0;
      Object.defineProperty(window, 'showSaveFilePicker', {
        configurable: true,
        value: async () => ({
          createWritable: async () => ({
            write: async () => { throw new Error('disk write failed'); },
            close: async () => undefined,
            abort: async () => { window.__recoveryWriteAborts += 1; }
          })
        })
      });
    });
    await saveButton.click();
    const failed = await invokeRecoverySave(page, [9, 10, 11, 12]);
    assert(failed.outcome.status === 'failed'
      && failed.sourceZeroed
      && !failed.pickerPending
      && (await page.evaluate(() => window.__recoveryWriteAborts)) === 1,
    `Failed file write was not aborted and retained for retry: ${JSON.stringify(failed)}`);

    await page.evaluate(() => {
      Object.defineProperty(window, 'showSaveFilePicker', {
        configurable: true,
        value: undefined
      });
    });
    const stale = await invokeRecoverySave(page, [13, 14, 15, 16], 5);
    assert(stale.outcome.status === 'stale'
      && stale.sourceZeroed
      && !stale.pickerPending,
    `Stale recovery length escaped cleanup or retry semantics: ${JSON.stringify(stale)}`);
  } finally {
    await context.close();
  }
}

async function runTwoPageIntegrityContract(browser) {
  const context = await createHarnessContext(browser);
  const first = await openHarnessPage(context);
  const second = await openHarnessPage(context);
  let secondNavigations = 0;
  second.on('framenavigated', (frame) => {
    if (frame === second.mainFrame()) secondNavigations += 1;
  });

  try {
    await registerTestBridge(first);
    await registerTestBridge(second);
    await setState(first, cleanSnapshot(7), 'workspace-update');
    await setState(second, cleanSnapshot(7), 'workspace-update');
    await second.waitForTimeout(100);
    await clearProbeReceipts(first);
    await clearProbeReceipts(second);

    await second.evaluate((next) => { window.__bridgeNextSnapshot = next; }, cleanSnapshot(8));
    await setState(first, cleanSnapshot(8), 'checkpoint');
    await second.waitForFunction(() =>
      window.chummerBuildPwaIntegrity.getSnapshot().contentRevision === 8);

    const cleanReceiver = await second.evaluate(() => ({
      snapshot: window.chummerBuildPwaIntegrity.getSnapshot(),
      bridgeCalls: window.__bridgeCalls,
      events: window.__integrityEvents
    }));
    assert(cleanReceiver.bridgeCalls.length > 0,
      'A clean second page did not request fresh state after the first page saved.');
    assert(cleanReceiver.snapshot.isDirty === false && cleanReceiver.snapshot.hasConflict === false,
      'Clean cross-page refresh changed the receiver to dirty or conflicted.');
    assert(JSON.stringify(Object.keys(cleanReceiver.snapshot).sort()) === JSON.stringify(snapshotKeys),
      `Snapshot escaped its privacy boundary: ${JSON.stringify(Object.keys(cleanReceiver.snapshot))}`);
    for (const event of cleanReceiver.events) {
      assert(JSON.stringify(Object.keys(event).sort()) === JSON.stringify(snapshotKeys),
        `Integrity event escaped its fixed snapshot contract: ${JSON.stringify(Object.keys(event))}`);
    }

    const senderWire = await first.evaluate(() => window.__integrityWireMessages);
    assert(senderWire.length === 1,
      `A single saved revision emitted ${senderWire.length} BroadcastChannel messages.`);
    assert(JSON.stringify(Object.keys(senderWire[0]).sort()) === JSON.stringify(wireKeys),
      `Broadcast payload keys escaped the fixed contract: ${JSON.stringify(Object.keys(senderWire[0]))}`);
    assert(senderWire[0].workspaceId === 'browser-integrity-workspace',
      'Broadcast payload did not identify the workspace being invalidated.');
    assert(senderWire[0].revision === 8 && senderWire[0].mutationKind === 'checkpoint',
      `Broadcast payload carried the wrong revision receipt: ${JSON.stringify(senderWire[0])}`);
    for (const secret of ['private-runner', 'free-text', '<character', 'fixture=', 'runner=']) {
      assert(!JSON.stringify(senderWire[0]).includes(secret),
        `Broadcast payload leaked private runner material: ${secret}`);
    }

    await clearProbeReceipts(first);
    await clearProbeReceipts(second);
    await setState(second, {
      ...cleanSnapshot(8),
      savedRevision: 7,
      isDirty: true
    }, 'workspace-update');
    await second.waitForTimeout(50);
    await clearProbeReceipts(first);
    await clearProbeReceipts(second);
    const navigationBaseline = secondNavigations;

    await setState(first, cleanSnapshot(9), 'checkpoint');
    await second.waitForFunction(() =>
      window.chummerBuildPwaIntegrity.getSnapshot().hasConflict === true);
    await second.waitForTimeout(150);

    const dirtyReceiver = await second.evaluate(() => ({
      snapshot: window.chummerBuildPwaIntegrity.getSnapshot(),
      bridgeCalls: window.__bridgeCalls
    }));
    assert(dirtyReceiver.snapshot.contentRevision === 8 && dirtyReceiver.snapshot.savedRevision === 7,
      'Remote save overwrote the dirty receiver revision checkpoint.');
    assert(dirtyReceiver.snapshot.isDirty === true && dirtyReceiver.snapshot.hasConflict === true,
      'Dirty receiver did not preserve edits and surface the remote revision conflict.');
    assert(secondNavigations === navigationBaseline,
      'Dirty/conflicted receiver reloaded after a cross-page save.');
  } finally {
    await context.close();
  }

}

async function beforeUnloadOutcome(page) {
  return page.evaluate(() => {
    const event = new Event('beforeunload', { cancelable: true });
    const dispatched = window.dispatchEvent(event);
    return { defaultPrevented: event.defaultPrevented, dispatched };
  });
}

async function assertExactPageCacheLeaseResponse(page) {
  const responses = await page.evaluate((releaseRevision) => {
    const received = [];
    const source = new EventTarget();
    Object.assign(source, {
      scriptURL: new URL(`/service-worker.js?build=${releaseRevision}`, document.baseURI).href,
      postMessage: (message) => received.push(JSON.parse(JSON.stringify(message)))
    });
    const scope = new URL('/', document.baseURI).href;
    const registration = new EventTarget();
    Object.assign(registration, {
      scope,
      active: source,
      waiting: null,
      installing: null
    });
    window.dispatchEvent(new CustomEvent('chummer-build:service-worker-registration', {
      detail: { registration, scriptUrl: source.scriptURL, scope }
    }));
    const dispatch = (data, eventSource = source) => {
      const event = new Event('message');
      Object.defineProperties(event, {
        data: { value: data },
        source: { value: eventSource }
      });
      navigator.serviceWorker.dispatchEvent(event);
    };
    dispatch({
      type: 'chummer-build-pwa-cache-lease-request',
      requestId: 'build-cache-lease-1700000000000-1'
    });
    dispatch({
      type: 'chummer-build-pwa-cache-lease-request',
      requestId: 'build-cache-lease-1700000000000-2',
      extra: 'reject'
    });
    dispatch({
      type: 'chummer-build-pwa-cache-lease-request',
      requestId: 'build-cache-lease-1700000000000-3'
    }, {
      scriptURL: new URL('/unrelated-worker.js', document.baseURI).href,
      postMessage: (message) => received.push(JSON.parse(JSON.stringify(message)))
    });
    return received;
  }, expectedReleaseRevision);

  assert(responses.length === 1,
    `Page answered ${responses.length} valid/invalid cache lease requests.`);
  assert(JSON.stringify(Object.keys(responses[0]).sort())
    === JSON.stringify(['cacheVersion', 'requestId', 'type']),
  `Page lease response escaped its exact payload: ${JSON.stringify(responses[0])}`);
  assert(responses[0].type === 'chummer-build-pwa-cache-lease-response'
    && responses[0].requestId === 'build-cache-lease-1700000000000-1'
    && responses[0].cacheVersion === expectedCacheVersion,
  `Page lease response did not bind the request and static cache version: ${JSON.stringify(responses[0])}`);
}

async function runSameRevisionDeleteTombstoneContract(browser) {
  const context = await createHarnessContext(browser);
  const sender = await openHarnessPage(context);
  const receiver = await openHarnessPage(context);

  try {
    await registerTestBridge(sender);
    await registerTestBridge(receiver);
    await setState(sender, cleanSnapshot(15), 'test-setup');
    await setState(receiver, {
      ...cleanSnapshot(15),
      savedRevision: 14,
      isDirty: true
    }, 'test-setup');
    await sender.waitForTimeout(75);
    await clearProbeReceipts(sender);
    await clearProbeReceipts(receiver);

    await receiver.evaluate(() => {
      window.chummerBuildPwaIntegrity.markBridgeUnavailable(window.__integrityToken);
    });
    await receiver.waitForFunction(() => window.__bridgeCalls.some((call) =>
      call[0] === 'RequestBuildPwaIntegrityBridgeRecoveryAsync'));
    await registerTestBridge(receiver);
    await setState(receiver, {
      ...cleanSnapshot(15),
      savedRevision: 14,
      isDirty: true
    }, 'reconnected-snapshot');
    await clearProbeReceipts(receiver);

    const published = await sender.evaluate(() =>
      window.chummerBuildPwaIntegrity.publishDelete(
        'browser-integrity-workspace',
        15,
        window.__integrityToken));
    assert(published === true, 'Committed same-revision delete was not published.');
    await receiver.waitForFunction(() => window.__bridgeCalls.some((call) =>
      call[0] === 'HandleExternalWorkspaceRevisionAsync'
      && call[2] === 15
      && call[3] === 'delete'));

    const wire = await sender.evaluate(() => window.__integrityWireMessages);
    assert(wire.length === 1
      && JSON.stringify(Object.keys(wire[0]).sort()) === JSON.stringify(wireKeys)
      && wire[0].mutationKind === 'delete'
      && wire[0].revision === 15,
    `Same-revision delete escaped its exact tombstone contract: ${JSON.stringify(wire)}`);
  } finally {
    await context.close();
  }
}

async function runBeforeUnloadAndBridgeLossContract(browser) {
  const context = await createHarnessContext(browser);
  const page = await openHarnessPage(context);

  try {
    await assertExactPageCacheLeaseResponse(page);
    await registerTestBridge(page);
    await setState(page, cleanSnapshot(12), 'workspace-update');
    assert((await beforeUnloadOutcome(page)).defaultPrevented === false,
      'Clean Build state installed an unnecessary beforeunload prompt.');

    await setState(page, {
      ...cleanSnapshot(12),
      contentRevision: 13,
      isDirty: true
    }, 'workspace-update');
    assert((await beforeUnloadOutcome(page)).defaultPrevented === true,
      'Dirty Build state did not guard document unload.');

    await setState(page, {
      ...cleanSnapshot(13),
      hasConflict: true
    }, 'remote-conflict');
    assert((await beforeUnloadOutcome(page)).defaultPrevented === true,
      'Conflicted Build state did not guard document unload.');

    await setState(page, cleanSnapshot(13), 'checkpoint');
    const unregistered = await page.evaluate(async () => {
      window.chummerBuildPwaIntegrity.unregisterBridge(window.__integrityToken);
      return {
        snapshot: window.chummerBuildPwaIntegrity.getSnapshot(),
        canReload: await window.chummerBuildPwaIntegrity.canReload()
      };
    });
    assert(unregistered.snapshot.bridgeAvailable === false && unregistered.canReload === false,
      'Unregistering the active bridge did not fail closed.');

    await page.evaluate(() => {
      window.__integrityToken = window.chummerBuildPwaIntegrity.registerBridge(
        window.__testIntegrityBridge);
      window.chummerBuildPwaIntegrity.markBridgeUnavailable(window.__integrityToken);
    });
    const unavailable = await page.evaluate(async () => ({
      snapshot: window.chummerBuildPwaIntegrity.getSnapshot(),
      canReload: await window.chummerBuildPwaIntegrity.canReload()
    }));
    assert(unavailable.snapshot.bridgeAvailable === false,
      'Circuit loss did not publish a fail-closed bridge state.');
    assert(unavailable.canReload === false,
      'Circuit loss allowed a service-worker or cross-page reload without fresh state.');
    assert((await beforeUnloadOutcome(page)).defaultPrevented === false,
      'Bridge loss alone broadened beforeunload beyond dirty/conflict state.');
  } finally {
    await context.close();
  }
}

async function bindWaitingUpdate(page) {
  await page.evaluate((releaseRevision) => {
    const scriptUrl = new URL(`/service-worker.js?build=${releaseRevision}`, document.baseURI).href;
    const scope = new URL('/', document.baseURI).href;
    const waiting = {
      scriptURL: scriptUrl,
      postMessage: (message) => window.__waitingWorkerMessages.push(
        JSON.parse(JSON.stringify(message)))
    };
    const registration = {
      scope,
      active: null,
      installing: null,
      waiting,
      addEventListener: () => undefined
    };
    window.dispatchEvent(new CustomEvent('chummer-build:service-worker-registration', {
      detail: { registration, scriptUrl, scope }
    }));
  }, expectedReleaseRevision);
  await page.locator('[data-build-pwa-update-action]').waitFor({ state: 'visible' });
}

async function runDeferredUpdateAndControllerRace(browser) {
  const context = await createHarnessContext(browser);
  const page = await openHarnessPage(context);
  let navigations = 0;
  page.on('framenavigated', (frame) => {
    if (frame === page.mainFrame()) navigations += 1;
  });

  try {
    await registerTestBridge(page);
    await bindWaitingUpdate(page);
    await setState(page, {
      ...cleanSnapshot(20),
      contentRevision: 21,
      isDirty: true
    }, 'workspace-update');
    await page.locator('[data-build-pwa-update-action]').click();
    await page.waitForFunction(() =>
      window.chummerBuildPwaIntegrity.getSnapshot().updateDeferred === true);

    const deferred = await page.evaluate(() => ({
      messages: window.__waitingWorkerMessages,
      status: document.querySelector('[data-build-pwa-install-status]')?.textContent || '',
      snapshot: window.chummerBuildPwaIntegrity.getSnapshot()
    }));
    assert(deferred.messages.length === 0,
      'Dirty Build state sent a command to the passive waiting worker.');
    assert(deferred.snapshot.isDirty === true && deferred.snapshot.updateDeferred === true,
      'Dirty update request did not preserve state and record deferral.');
    assert(/save|copy|conflict|close|reopen/i.test(deferred.status),
      `Live status did not explain the deferred update: ${deferred.status}`);

    await setState(page, cleanSnapshot(21), 'checkpoint');
    assert((await page.evaluate(() => window.__waitingWorkerMessages.length)) === 0,
      'Saving a runner sent a command to the passive waiting worker.');
    await page.locator('[data-build-pwa-update-action]').click();
    await page.locator('[data-build-pwa-update-guidance]').waitFor({ state: 'visible' });
    const reviewed = await page.evaluate(() => ({
      messages: window.__waitingWorkerMessages,
      status: document.querySelector('[data-build-pwa-install-status]')?.textContent || '',
      expanded: document.querySelector('[data-build-pwa-update-action]')?.getAttribute('aria-expanded'),
      guidanceFocused: document.activeElement === document.querySelector('[data-build-pwa-update-guidance]')
    }));
    assert(reviewed.messages.length === 0,
      `Clean review forced the waiting worker: ${JSON.stringify(reviewed.messages)}`);
    assert(reviewed.expanded === 'true' && reviewed.guidanceFocused,
      'Update guidance was not revealed and focused accessibly.');
    assert(/close every Chummer Build|reopen/i.test(reviewed.status),
      `Live status did not explain passive activation: ${reviewed.status}`);

    // Defensive only: a browser/devtools-originated controller change still
    // re-checks live state and coalesces duplicate reload signals.
    const baseline = navigations;
    const reloaded = page.waitForEvent('domcontentloaded', { timeout: 10000 });
    await page.evaluate(() => {
      navigator.serviceWorker.dispatchEvent(new Event('controllerchange'));
      navigator.serviceWorker.dispatchEvent(new Event('controllerchange'));
    });
    await reloaded;
    await page.waitForTimeout(150);
    assert(navigations - baseline === 1,
      `Duplicate controllerchange events caused ${navigations - baseline} reloads instead of one.`);
  } finally {
    await context.close();
  }
}

async function assertRequiredRuntimeAssetsRemainUsable(page) {
  const assets = await page.evaluate(async () => {
    const paths = [
      '/js/build-pwa-recovery.js',
      '/js/build-pwa-integrity.js',
      '/js/build-pwa-install.js',
      '/service-worker.js',
      '/build-pwa-install.css'
    ];
    return Promise.all(paths.map(async (assetPath) => {
      const response = await fetch(assetPath, { cache: 'reload' });
      return { assetPath, ok: response.ok, body: await response.text() };
    }));
  });
  assert(assets.every((asset) => asset.ok && asset.body.length > 0),
    `Dirty sibling lost a required Build asset: ${JSON.stringify(assets.map(({ assetPath, ok }) => ({ assetPath, ok })))}`);
  assert(assets.find((asset) => asset.assetPath.endsWith('build-pwa-integrity.js'))
    .body.includes('chummerBuildPwaIntegrity'),
  'Dirty sibling fetched an integrity asset without the runtime contract.');
  assert(await page.evaluate(() =>
    typeof window.chummerBuildPwaIntegrity?.getSnapshot === 'function'),
  'Dirty sibling lost its already-loaded integrity runtime.');
}

async function runPassiveWaitingWorkerDoesNotDisplaceSibling(browser) {
  const context = await createHarnessContext(browser);
  const cleanPage = await openHarnessPage(context);
  const dirtyPage = await openHarnessPage(context);
  let cleanNavigations = 0;
  let dirtyNavigations = 0;
  cleanPage.on('framenavigated', (frame) => {
    if (frame === cleanPage.mainFrame()) cleanNavigations += 1;
  });
  dirtyPage.on('framenavigated', (frame) => {
    if (frame === dirtyPage.mainFrame()) dirtyNavigations += 1;
  });

  try {
    await registerTestBridge(cleanPage);
    await registerTestBridge(dirtyPage);
    await setState(cleanPage, cleanSnapshot(30), 'test-setup');
    await setState(dirtyPage, {
      ...cleanSnapshot(31),
      savedRevision: 30,
      isDirty: true
    }, 'test-setup');
    await bindWaitingUpdate(cleanPage);
    await bindWaitingUpdate(dirtyPage);

    const cleanNavigationBaseline = cleanNavigations;
    const dirtyNavigationBaseline = dirtyNavigations;

    await cleanPage.locator('[data-build-pwa-update-action]').click();
    await cleanPage.locator('[data-build-pwa-update-guidance]').waitFor({ state: 'visible' });
    await cleanPage.waitForTimeout(100);
    assert((await cleanPage.evaluate(() => window.__waitingWorkerMessages.length)) === 0,
      'A clean tab sent a forced-activation command to the waiting worker.');
    assert((await dirtyPage.evaluate(() => window.__waitingWorkerMessages.length)) === 0,
      'A clean sibling caused the dirty tab to send a worker command.');
    assert(cleanNavigations === cleanNavigationBaseline,
      'Reviewing update steps reloaded the clean tab.');

    await dirtyPage.waitForFunction(() =>
      window.chummerBuildPwaIntegrity.getSnapshot().updateDeferred === true);
    await dirtyPage.waitForTimeout(100);
    const preserved = await dirtyPage.evaluate(() => ({
      snapshot: window.chummerBuildPwaIntegrity.getSnapshot(),
      status: document.querySelector('[data-build-pwa-install-status]')?.textContent || ''
    }));
    assert(dirtyNavigations === dirtyNavigationBaseline,
      'Clean sibling displaced the dirty tab while an update was waiting.');
    assert(preserved.snapshot.contentRevision === 31
      && preserved.snapshot.savedRevision === 30
      && preserved.snapshot.isDirty === true,
    `Dirty sibling lost its unsaved checkpoint: ${JSON.stringify(preserved.snapshot)}`);
    assert(preserved.snapshot.updateDeferred === true,
      'Dirty sibling did not retain the passive waiting-update state.');
    assert(await dirtyPage.evaluate(() =>
      navigator.serviceWorker.controller?.scriptURL === window.__incumbentControllerAtStartup),
    'Clean sibling replaced the dirty sibling incumbent controller.');
    assert(/save|copy|close|reopen/i.test(preserved.status),
      `Dirty sibling did not receive honest waiting-update guidance: ${preserved.status}`);
    await assertRequiredRuntimeAssetsRemainUsable(dirtyPage);

    await setState(dirtyPage, {
      ...cleanSnapshot(31),
      updateDeferred: true
    }, 'checkpoint');
    await dirtyPage.waitForFunction(() => {
      const button = document.querySelector('[data-build-pwa-update-action]');
      return button instanceof HTMLButtonElement && !button.disabled && !button.hidden;
    });
    assert(dirtyNavigations === dirtyNavigationBaseline,
      'Becoming clean reloaded the dirty sibling without an explicit action.');

    await dirtyPage.locator('[data-build-pwa-update-action]').click();
    await dirtyPage.waitForTimeout(100);
    assert(dirtyNavigations === dirtyNavigationBaseline,
      'Reviewing update steps forced a reload after the dirty sibling became clean.');
    assert((await dirtyPage.evaluate(() => window.__waitingWorkerMessages.length)) === 0,
      'Cleaned sibling sent a forced-activation command to the waiting worker.');
    await assertRequiredRuntimeAssetsRemainUsable(dirtyPage);
  } finally {
    await context.close();
  }
}

async function runInstallDeviceHandoffContract(browser) {
  const context = await createHarnessContext(browser);
  const page = await openHarnessPage(context);

  try {
    const contract = await page.evaluate(() => {
      const handoff = window.chummerBuildPwaHandoff;
      const origin = window.location.origin;
      const rootUrl = handoff.buildCanonicalInstallUrl({
        origin,
        scope: `${origin}/?workspace=secret-runner&token=secret#owner-token`
      });
      const pathBaseUrl = handoff.buildCanonicalInstallUrl({
        origin,
        scope: `${origin}/blazor/?workspace=secret-runner&token=secret#owner-token`
      });
      let rejectedExternalScope = false;
      try {
        handoff.buildCanonicalInstallUrl({ origin, scope: "https://example.invalid/blazor/" });
      } catch {
        rejectedExternalScope = true;
      }

      const firstMatrix = handoff.encodeQrMatrix(pathBaseUrl);
      const secondMatrix = handoff.encodeQrMatrix(pathBaseUrl);
      const finderMatches = (centerX, centerY) => {
        for (let y = centerY - 3; y <= centerY + 3; y += 1) {
          for (let x = centerX - 3; x <= centerX + 3; x += 1) {
            const distance = Math.max(Math.abs(x - centerX), Math.abs(y - centerY));
            const expected = distance !== 2;
            if (firstMatrix.modules[y][x] !== expected) return false;
          }
        }
        return true;
      };
      let capacityRejected = false;
      try {
        handoff.encodeQrMatrix("x".repeat(272));
      } catch {
        capacityRejected = true;
      }

      return {
        rootUrl,
        pathBaseUrl,
        rejectedExternalScope,
        deviceCases: {
          uaMobile: handoff.resolveEffectiveDevice("auto", {
            standalone: false,
            userAgentDataMobile: true,
            coarsePointer: false,
            maxTouchPoints: 0
          }),
          uaDesktop: handoff.resolveEffectiveDevice("auto", {
            standalone: false,
            userAgentDataMobile: false,
            coarsePointer: true,
            maxTouchPoints: 10
          }),
          coarseTouchFallback: handoff.resolveEffectiveDevice("auto", {
            standalone: false,
            userAgentDataMobile: null,
            coarsePointer: true,
            maxTouchPoints: 5
          }),
          noTouchFallback: handoff.resolveEffectiveDevice("auto", {
            standalone: false,
            userAgentDataMobile: null,
            coarsePointer: true,
            maxTouchPoints: 0
          }),
          explicitDesktop: handoff.resolveEffectiveDevice("desktop", {
            standalone: false,
            userAgentDataMobile: true,
            coarsePointer: true,
            maxTouchPoints: 5
          }),
          explicitMobile: handoff.resolveEffectiveDevice("mobile", {
            standalone: false,
            userAgentDataMobile: false,
            coarsePointer: false,
            maxTouchPoints: 0
          }),
          standalone: handoff.resolveEffectiveDevice("mobile", {
            standalone: true,
            userAgentDataMobile: true,
            coarsePointer: true,
            maxTouchPoints: 5
          })
        },
        matrix: {
          version: firstMatrix.version,
          size: firstMatrix.size,
          mask: firstMatrix.mask,
          signature: handoff.matrixSignature(firstMatrix),
          repeatedSignature: handoff.matrixSignature(secondMatrix),
          allBoolean: firstMatrix.modules.every((row) =>
            row.length === firstMatrix.size && row.every((cell) => typeof cell === "boolean")),
          finderMatches: finderMatches(3, 3)
            && finderMatches(firstMatrix.size - 4, 3)
            && finderMatches(3, firstMatrix.size - 4)
        },
        capacityRejected
      };
    });

    assert(contract.rootUrl === `${harnessOrigin}/app`,
      `Root install handoff retained private route context: ${contract.rootUrl}`);
    assert(contract.pathBaseUrl === `${harnessOrigin}/blazor/app`,
      `PathBase install handoff was not canonical: ${contract.pathBaseUrl}`);
    assert(contract.rejectedExternalScope,
      "A cross-origin Build install scope was accepted.");
    assert(JSON.stringify(contract.deviceCases) === JSON.stringify({
      uaMobile: "mobile",
      uaDesktop: "desktop",
      coarseTouchFallback: "mobile",
      noTouchFallback: "desktop",
      explicitDesktop: "desktop",
      explicitMobile: "mobile",
      standalone: "standalone"
    }), `Install device classification drifted: ${JSON.stringify(contract.deviceCases)}`);
    assert(contract.matrix.version === 10
      && contract.matrix.size === 57
      && contract.matrix.allBoolean
      && contract.matrix.finderMatches
      && contract.matrix.signature === contract.matrix.repeatedSignature,
    `Local QR matrix failed deterministic structural checks: ${JSON.stringify(contract.matrix)}`);
    assert(contract.matrix.signature === "08b160cc",
      `Local QR matrix no longer matches its independently generated standard matrix: ${contract.matrix.signature}`);
    assert(contract.capacityRejected,
      "An over-capacity QR payload emitted a partial matrix instead of failing closed.");

    await page.waitForFunction(() =>
      document.querySelector("[data-build-pwa-install-handoff]")?.dataset.buildPwaHandoffEffective === "desktop");
    const desktopUi = await page.evaluate(() => ({
      desktopHidden: document.querySelector("[data-build-pwa-desktop-handoff]")?.hidden,
      mobileHidden: document.querySelector("[data-build-pwa-mobile-handoff]")?.hidden,
      href: document.querySelector("[data-build-pwa-install-link]")?.href,
      linkText: document.querySelector("[data-build-pwa-install-link-text]")?.textContent,
      svgLabel: document.querySelector("[data-build-pwa-install-qr] svg")?.getAttribute("aria-label"),
      signature: document.querySelector("[data-build-pwa-install-qr]")?.dataset.buildPwaQrSignature
    }));
    assert(desktopUi.desktopHidden === false && desktopUi.mobileHidden === true,
      `Desktop browser did not receive desktop handoff: ${JSON.stringify(desktopUi)}`);
    assert(desktopUi.href === `${harnessOrigin}/app`
      && desktopUi.linkText === `${harnessOrigin}/app`
      && desktopUi.svgLabel?.includes("clean Chummer Build mobile install page")
      && /^[0-9a-f]{8}$/.test(desktopUi.signature || ""),
    `Desktop QR/link contract was incomplete: ${JSON.stringify(desktopUi)}`);

    await page.locator('[data-build-pwa-install-help]').click();
    await page.locator('[data-build-pwa-install-device-choice="mobile"]').check();
    await page.waitForFunction(() =>
      document.querySelector("[data-build-pwa-install-handoff]")?.dataset.buildPwaHandoffEffective === "mobile");
    const mobileUi = await page.evaluate(() => ({
      desktopHidden: document.querySelector("[data-build-pwa-desktop-handoff]")?.hidden,
      mobileHidden: document.querySelector("[data-build-pwa-mobile-handoff]")?.hidden,
      stored: localStorage.getItem("chummer.build-pwa.install-device.v1")
    }));
    assert(mobileUi.desktopHidden === true
      && mobileUi.mobileHidden === false
      && mobileUi.stored === "mobile",
    `Explicit mobile handoff was not applied and persisted: ${JSON.stringify(mobileUi)}`);

    await page.reload({ waitUntil: "domcontentloaded" });
    await page.waitForFunction(() =>
      document.querySelector("[data-build-pwa-install-handoff]")?.dataset.buildPwaHandoffEffective === "mobile");
    assert(await page.locator('[data-build-pwa-install-device-choice="mobile"]').isChecked(),
      "Persisted mobile handoff was not restored after reload.");
  } finally {
    await context.close();
  }

  const overCapacityScope = `/${"a".repeat(280)}/`;
  const overCapacityContext = await createHarnessContext(browser, { scopePath: overCapacityScope });
  const overCapacityPage = await openHarnessPage(overCapacityContext);
  try {
    await overCapacityPage.waitForFunction(() =>
      document.querySelector("[data-build-pwa-install-handoff]")?.dataset.buildPwaHandoffEffective === "desktop");
    const failureUi = await overCapacityPage.evaluate(() => ({
      qrChildren: document.querySelector("[data-build-pwa-install-qr]")?.childElementCount,
      qrSignature: document.querySelector("[data-build-pwa-install-qr]")?.dataset.buildPwaQrSignature,
      statusRole: document.querySelector("[data-build-pwa-install-device-status]")?.getAttribute("role"),
      status: document.querySelector("[data-build-pwa-install-device-status]")?.textContent || "",
      link: document.querySelector("[data-build-pwa-install-link]")?.href || ""
    }));
    assert(failureUi.qrChildren === 0
      && !failureUi.qrSignature
      && failureUi.statusRole === "status"
      && /QR code could not be generated/i.test(failureUi.status)
      && failureUi.link.startsWith(`${harnessOrigin}/${"a".repeat(280)}/app`),
    `Over-capacity QR failure emitted a partial code or hid its accessible fallback: ${JSON.stringify(failureUi)}`);
  } finally {
    await overCapacityContext.close();
  }
}

async function runInstallFocusContract(browser) {
  const context = await createHarnessContext(browser);
  const page = await openHarnessPage(context);

  try {
    await page.locator('[data-build-pwa-install-help]').click();
    assert(await page.evaluate(() =>
      document.activeElement === document.querySelector('[data-build-pwa-copy-install-link]')),
    'Desktop install launcher did not move focus to the QR copy fallback.');
    assert(await page.locator('[data-build-pwa-install-status]').getAttribute('aria-live') === 'polite',
      'Install/update status is not a polite live region.');
    assert(await page.locator('[data-build-pwa-install-device-status]').getAttribute('aria-live') === 'polite',
      'Device handoff status is not a polite live region.');
    await page.locator('[data-build-pwa-dismiss-action]').click();
    assert(await page.evaluate(() =>
      document.activeElement === document.querySelector('[data-build-pwa-install-help]')),
    'Dismissing install guidance did not restore focus to its launcher.');

    await page.locator('[data-build-pwa-install-help]').click();
    await page.evaluate(() => window.dispatchEvent(new Event('appinstalled')));
    await page.waitForFunction(() =>
      document.activeElement === document.querySelector('#chummer-workspace-main'));
    const installedFocus = await page.evaluate(() => ({
      panelHidden: document.querySelector('[data-build-pwa-install]')?.hidden,
      launcherHidden: document.querySelector('[data-build-pwa-install-help]')?.hidden,
      workspaceFocused: document.activeElement === document.querySelector('#chummer-workspace-main')
    }));
    assert(installedFocus.panelHidden
      && installedFocus.launcherHidden
      && installedFocus.workspaceFocused,
    `appinstalled hid the focused UI without relocating focus: ${JSON.stringify(installedFocus)}`);
  } finally {
    await context.close();
  }
}

async function runMobileNativeInstallContract(browser) {
  const context = await createHarnessContext(browser, {
    contextOptions: {
      hasTouch: true,
      viewport: { width: 390, height: 844 }
    }
  });
  await context.addInitScript(() => {
    Object.defineProperty(Navigator.prototype, 'userAgentData', {
      configurable: true,
      get: () => Object.freeze({ mobile: true })
    });
  });
  const page = await openHarnessPage(context);

  try {
    await page.waitForFunction(() =>
      document.querySelector('[data-build-pwa-install-handoff]')?.dataset.buildPwaHandoffEffective === 'mobile');

    const autoMobile = await page.evaluate(() => ({
      desktopHidden: document.querySelector('[data-build-pwa-desktop-handoff]')?.hidden,
      mobileHidden: document.querySelector('[data-build-pwa-mobile-handoff]')?.hidden,
      preference: document.querySelector('[data-build-pwa-install-handoff]')?.dataset.buildPwaHandoffPreference,
      status: document.querySelector('[data-build-pwa-install-device-status]')?.textContent || ''
    }));
    assert(autoMobile.desktopHidden === true
      && autoMobile.mobileHidden === false
      && autoMobile.preference === 'auto'
      && /mobile installation guidance/i.test(autoMobile.status),
    `Mobile browser setting did not select direct-device guidance: ${JSON.stringify(autoMobile)}`);

    await page.locator('[data-build-pwa-install-help]').click();
    const manualFallback = await page.evaluate(() => ({
      panelHidden: document.querySelector('[data-build-pwa-install]')?.hidden,
      manualOpen: document.querySelector('[data-build-pwa-manual]')?.open,
      summaryFocused: document.activeElement
        === document.querySelector('[data-build-pwa-manual] > summary')
    }));
    assert(manualFallback.panelHidden === false
      && manualFallback.manualOpen === true
      && manualFallback.summaryFocused === true,
    `Mobile browser without a native prompt did not expose accessible Add to Home Screen guidance: ${JSON.stringify(manualFallback)}`);

    await page.evaluate(() => {
      window.__nativeInstallPromptCalls = 0;
      const promptEvent = new Event('beforeinstallprompt', { cancelable: true });
      Object.defineProperties(promptEvent, {
        prompt: {
          value: async () => {
            window.__nativeInstallPromptCalls += 1;
          }
        },
        userChoice: {
          value: Promise.resolve({ outcome: 'accepted' })
        }
      });
      window.dispatchEvent(promptEvent);
      window.__nativeInstallPromptPrevented = promptEvent.defaultPrevented;
    });
    const installButton = page.locator('[data-build-pwa-install-action]');
    await installButton.waitFor({ state: 'visible' });
    assert(await installButton.textContent().then((text) => text.trim()) === 'Install Chummer Build',
      'Mobile native install prompt used desktop-specific button copy.');
    await installButton.click();
    await page.waitForFunction(() =>
      /Install accepted/i.test(document.querySelector('[data-build-pwa-install-status]')?.textContent || ''));
    const nativePrompt = await page.evaluate(() => ({
      buttonHidden: document.querySelector('[data-build-pwa-install-action]')?.hidden,
      prevented: window.__nativeInstallPromptPrevented,
      promptCalls: window.__nativeInstallPromptCalls,
      status: document.querySelector('[data-build-pwa-install-status]')?.textContent || ''
    }));
    assert(nativePrompt.buttonHidden === true
      && nativePrompt.prevented === true
      && nativePrompt.promptCalls === 1
      && /Install accepted/i.test(nativePrompt.status),
    `Native mobile install prompt was not consumed exactly once: ${JSON.stringify(nativePrompt)}`);
  } finally {
    await context.close();
  }
}

async function runDismissedWaitingWorkerRediscoveryContract(browser) {
  const context = await createHarnessContext(browser);
  const page = await openHarnessPage(context);

  try {
    await page.evaluate((releaseRevision) => {
      const scriptUrl = new URL(`/service-worker.js?build=${releaseRevision}`, document.baseURI).href;
      const scope = new URL('/', document.baseURI).href;
      const active = new EventTarget();
      Object.assign(active, { scriptURL: scriptUrl, postMessage: () => undefined });
      const registration = new EventTarget();
      Object.assign(registration, {
        scope,
        active,
        waiting: null,
        installing: null
      });
      window.__rediscoveryRegistration = registration;
      window.__rediscoveryScriptUrl = scriptUrl;
      window.dispatchEvent(new CustomEvent('chummer-build:service-worker-registration', {
        detail: { registration, scriptUrl, scope }
      }));
    }, expectedReleaseRevision);

    await page.locator('[data-build-pwa-install-help]').click();
    await page.locator('[data-build-pwa-dismiss-action]').click();
    await page.evaluate(() => {
      const status = document.querySelector('[data-build-pwa-install-status]');
      window.__waitingStatusMutations = 0;
      window.__waitingStatusObserver = new MutationObserver(() => {
        window.__waitingStatusMutations += 1;
      });
      window.__waitingStatusObserver.observe(status, {
        childList: true,
        characterData: true,
        subtree: true
      });
      const waiting = new EventTarget();
      Object.assign(waiting, {
        scriptURL: window.__rediscoveryScriptUrl,
        postMessage: () => undefined
      });
      window.__rediscoveryRegistration.waiting = waiting;
      window.dispatchEvent(new Event('focus'));
    });
    await page.waitForFunction(() => {
      const button = document.querySelector('[data-build-pwa-update-action]');
      return button instanceof HTMLButtonElement && button.hidden === false;
    });

    const discovered = await page.evaluate(() => ({
      panelHidden: document.querySelector('[data-build-pwa-install]')?.hidden,
      status: document.querySelector('[data-build-pwa-install-status]')?.textContent || '',
      mutations: window.__waitingStatusMutations
    }));
    assert(discovered.panelHidden === true,
      'Passive waiting-worker rediscovery reopened deliberately dismissed guidance.');
    assert(/downloaded and waiting/i.test(discovered.status),
      `Rediscovered waiting worker was not announced once: ${discovered.status}`);

    await page.evaluate(() => {
      window.dispatchEvent(new Event('focus'));
      window.dispatchEvent(new PageTransitionEvent('pageshow'));
    });
    await page.waitForTimeout(50);
    const repeated = await page.evaluate(() => ({
      panelHidden: document.querySelector('[data-build-pwa-install]')?.hidden,
      mutations: window.__waitingStatusMutations
    }));
    assert(repeated.panelHidden === true,
      'Repeated passive checks reopened deliberately dismissed guidance.');
    assert(repeated.mutations === discovered.mutations,
      `Repeated passive checks duplicated the waiting announcement (${discovered.mutations} -> ${repeated.mutations}).`);
  } finally {
    await context.close();
  }
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  try {
    if (process.env.CHUMMER_BUILD_PWA_E2E_CASE === 'mobile-install') {
      await runMobileNativeInstallContract(browser);
      return;
    }
    await runRecoveryStreamOutcomes(browser);
    await runTwoPageIntegrityContract(browser);
    await runSameRevisionDeleteTombstoneContract(browser);
    await runBeforeUnloadAndBridgeLossContract(browser);
    await runDeferredUpdateAndControllerRace(browser);
    await runPassiveWaitingWorkerDoesNotDisplaceSibling(browser);
    await runInstallDeviceHandoffContract(browser);
    await runMobileNativeInstallContract(browser);
    await runInstallFocusContract(browser);
    await runDismissedWaitingWorkerRediscoveryContract(browser);
  } finally {
    await browser.close();
  }
}

run()
  .then(() => console.log('Build PWA integrity, update, and focus checks passed.'))
  .catch((error) => {
    console.error(error && error.stack ? error.stack : error);
    process.exitCode = 1;
  });
