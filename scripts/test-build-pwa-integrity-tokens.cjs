#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const integritySource = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'js', 'build-pwa-integrity.js'),
  'utf8');
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

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

function flushAsyncWork() {
  return new Promise((resolve) => setImmediate(resolve));
}

function createBroadcastBus() {
  const channels = new Set();
  return {
    add(channel) { channels.add(channel); },
    remove(channel) { channels.delete(channel); },
    post(sender, message) {
      for (const candidate of channels) {
        if (candidate !== sender && !candidate.closed && candidate.name === sender.name) {
          setImmediate(() => candidate.emit(message, true));
        }
      }
    }
  };
}

function createHarness({
  cryptoMode = 'available',
  throwOnBroadcast = false,
  ownerInvalidationTokens = ['b'.repeat(64)],
  broadcastBus = null
} = {}) {
  const operations = [];
  const changedEvents = [];
  const storageWrites = [];
  const documentTouches = [];
  const windowListeners = new Map();
  const channels = [];
  let tokenGeneration = 0;
  let context = null;

  class HarnessCustomEvent {
    constructor(type, init = {}) {
      this.type = type;
      this.detail = init.detail;
    }
  }

  class HarnessBroadcastChannel {
    constructor(name) {
      this.name = name;
      this.listeners = new Map();
      this.messages = [];
      this.closed = false;
      channels.push(this);
      broadcastBus?.add(this);
    }

    addEventListener(type, handler) {
      this.listeners.set(type, handler);
    }

    postMessage(message) {
      operations.push('broadcast-post');
      if (throwOnBroadcast) throw new Error('simulated closed channel');
      this.messages.push(message);
      broadcastBus?.post(this, message);
    }

    close() {
      operations.push('broadcast-close');
      this.closed = true;
      broadcastBus?.remove(this);
    }

    emit(message, structuredClone = false) {
      const payload = structuredClone && context
        ? vm.runInContext(`(${JSON.stringify(message)})`, context)
        : message;
      this.listeners.get('message')?.({ data: payload });
    }
  }

  const crypto = cryptoMode === 'missing'
    ? undefined
    : {
        getRandomValues(bytes) {
          if (cryptoMode === 'throws') throw new Error('simulated crypto failure');
          tokenGeneration += 1;
          for (let index = 0; index < bytes.length; index += 1) {
            bytes[index] = (tokenGeneration * 31 + index) & 0xff;
          }
          return bytes;
        }
      };
  const storage = {
    setItem(key, value) {
      storageWrites.push([key, value]);
    }
  };
  const documentListeners = new Map();
  const document = new Proxy({
    visibilityState: 'visible',
    addEventListener(type, handler) {
      if (!documentListeners.has(type)) documentListeners.set(type, new Set());
      documentListeners.get(type).add(handler);
    },
    removeEventListener(type, handler) {
      documentListeners.get(type)?.delete(handler);
    }
  }, {
    get(target, property, receiver) {
      documentTouches.push(String(property));
      return Reflect.get(target, property, receiver);
    },
    set(target, property, value, receiver) {
      documentTouches.push(String(property));
      return Reflect.set(target, property, value, receiver);
    }
  });
  const window = {
    crypto,
    BroadcastChannel: HarnessBroadcastChannel,
    setTimeout,
    localStorage: storage,
    sessionStorage: storage,
    addEventListener(type, handler) {
      if (!windowListeners.has(type)) windowListeners.set(type, new Set());
      windowListeners.get(type).add(handler);
    },
    removeEventListener(type, handler) {
      windowListeners.get(type)?.delete(handler);
    },
    dispatchEvent(event) {
      operations.push(`window-event:${event.type}`);
      if (event.type === 'chummer:build-integrity-changed') {
        changedEvents.push(event.detail);
      }
      for (const handler of windowListeners.get(event.type) || []) handler(event);
      return event.defaultPrevented !== true;
    }
  };
  Object.defineProperty(window, 'chummerPwa', {
    value: Object.freeze({
      expectedAuthority: Object.freeze({
        scriptUrl: 'https://chummer.test/blazor/service-worker.js',
        scope: 'https://chummer.test/blazor/',
        ownerInvalidationTokens: Object.freeze([...ownerInvalidationTokens])
      })
    }),
    writable: false,
    configurable: false
  });
  context = vm.createContext({
    window,
    document,
    CustomEvent: HarnessCustomEvent,
    Uint8Array,
    console
  });
  vm.runInContext(integritySource, context, { filename: 'build-pwa-integrity.js' });
  vm.runInContext(`
    globalThis.makeSnapshot = (overrides = {}) => Object.assign({
      workspaceId: 'token-test-workspace',
      contentRevision: 1,
      savedRevision: 1,
      isDirty: false,
      hasConflict: false,
      updateDeferred: false,
      bridgeAvailable: true
    }, overrides);
    globalThis.makeWire = (overrides = {}) => Object.assign({
      workspaceId: 'token-test-workspace',
      revision: 2,
      mutationKind: 'checkpoint'
    }, overrides);
    globalThis.makeMalformedSnapshot = (kind) => {
      const value = globalThis.makeSnapshot();
      if (kind === 'missing-key') delete value.savedRevision;
      if (kind === 'extra-key') value.runnerName = 'must-not-cross';
      if (kind === 'string-revision') value.contentRevision = '9';
      if (kind === 'numeric-dirty') value.isDirty = 0;
      if (kind === 'bridge-false') value.bridgeAvailable = false;
      return value;
    };
  `, context);

  return {
    api: window.chummerBuildPwaIntegrity,
    channel: (index = 0) => channels[index],
    channels,
    changedEvents,
    context,
    documentTouches,
    operations,
    storageWrites,
    window
  };
}

function makeSnapshot(harness, overrides = {}) {
  return harness.context.makeSnapshot(overrides);
}

function makeWire(harness, overrides = {}) {
  return harness.context.makeWire(overrides);
}

function bridgeReturning(harness, snapshot = makeSnapshot(harness)) {
  return { invokeMethodAsync: async () => snapshot };
}

async function secureOpaqueTokenStaysOutsidePublicPayloads() {
  const harness = createHarness();
  const token = harness.api.registerBridge(bridgeReturning(harness));
  assert(/^[0-9a-f]{32}$/.test(token || ''),
    `Registration token is not 128-bit lowercase hexadecimal: ${token}`);

  harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 7,
    savedRevision: 6,
    isDirty: true
  }), 'workspace-update', token);

  const snapshot = harness.api.getSnapshot();
  const messages = harness.channel().messages;
  assert(JSON.stringify(Object.keys(snapshot).sort()) === JSON.stringify(snapshotKeys),
    `Snapshot escaped its exact schema: ${JSON.stringify(Object.keys(snapshot))}`);
  assert(messages.length === 1
    && JSON.stringify(Object.keys(messages[0]).sort()) === JSON.stringify(wireKeys),
  `Broadcast escaped its exact schema: ${JSON.stringify(messages)}`);
  assert(!JSON.stringify({ snapshot, messages, events: harness.changedEvents }).includes(token),
    'Registration token escaped through a snapshot, event, or BroadcastChannel payload.');
  assert(!Object.keys(harness.api).some((key) => /token/i.test(key)),
    'Registration token became an enumerable public API property.');
  assert(harness.storageWrites.length === 0, 'Registration wrote an opaque token to browser storage.');
  assert(harness.documentTouches.every(property =>
    property === 'addEventListener' || property === 'visibilityState'),
  `Registration touched an unexpected DOM surface: ${JSON.stringify(harness.documentTouches)}`);
}

async function secureGenerationFailureFailsClosed() {
  for (const cryptoMode of ['missing', 'throws']) {
    const harness = createHarness({ cryptoMode });
    const token = harness.api.registerBridge(bridgeReturning(harness));
    assert(token === null, `${cryptoMode} crypto unexpectedly registered a bridge.`);
    assert(harness.api.getSnapshot().bridgeAvailable === false,
      `${cryptoMode} crypto left the bridge available.`);
    assert(await harness.api.canReload() === false,
      `${cryptoMode} crypto allowed a reload without a registered bridge.`);
  }
}

async function staleTokenCannotMutateOrUnregisterNewBridge() {
  const harness = createHarness();
  const firstToken = harness.api.registerBridge(bridgeReturning(harness));
  const secondToken = harness.api.registerBridge(bridgeReturning(harness));
  assert(firstToken !== secondToken, 'Consecutive bridge registrations reused a token.');

  harness.api.updateState(makeSnapshot(harness, { contentRevision: 5, savedRevision: 5 }),
    'workspace-update', secondToken);
  harness.api.updateState(makeSnapshot(harness, { contentRevision: 99, savedRevision: 99 }),
    'checkpoint', firstToken);
  assert(harness.api.getSnapshot().contentRevision === 5,
    'A stale token overwrote the newer bridge snapshot.');
  assert(harness.api.unregisterBridge(firstToken) === false,
    'A stale token unregistered the newer bridge.');
  assert(harness.api.markBridgeUnavailable(firstToken) === false,
    'A stale token marked the newer bridge unavailable.');
  assert(harness.api.getSnapshot().bridgeAvailable === true,
    'Stale token operations changed newer bridge availability.');

  harness.api.updateState(makeSnapshot(harness, { contentRevision: 6, savedRevision: 6 }),
    'checkpoint', secondToken);
  assert(harness.api.getSnapshot().contentRevision === 6,
    'The active token stopped updating after stale-token attempts.');
}

async function oldAsyncRejectionCannotClearNewBridge() {
  const harness = createHarness();
  const oldInvocation = deferred();
  const oldBridge = {
    invokeMethodAsync(method) {
      if (method === 'HandleExternalWorkspaceRevisionAsync') return oldInvocation.promise;
      return Promise.resolve(makeSnapshot(harness));
    }
  };
  const oldToken = harness.api.registerBridge(oldBridge);
  harness.api.updateState(makeSnapshot(harness), 'workspace-update', oldToken);
  harness.channel().emit(makeWire(harness));

  const newToken = harness.api.registerBridge(bridgeReturning(harness));
  harness.api.updateState(makeSnapshot(harness, { contentRevision: 8, savedRevision: 8 }),
    'checkpoint', newToken);
  oldInvocation.reject(new Error('old circuit failed'));
  await flushAsyncWork();

  assert(harness.api.getSnapshot().bridgeAvailable === true
    && harness.api.getSnapshot().contentRevision === 8,
  'An old async rejection cleared or overwrote the newer bridge.');
  harness.api.updateState(makeSnapshot(harness, { contentRevision: 9, savedRevision: 9 }),
    'checkpoint', newToken);
  assert(harness.api.getSnapshot().contentRevision === 9,
    'The newer bridge token was unusable after an old rejection.');
}

async function oldAsyncResultCannotOverwriteNewBridge() {
  const harness = createHarness();
  const oldReload = deferred();
  const oldToken = harness.api.registerBridge({
    invokeMethodAsync: () => oldReload.promise
  });
  harness.api.updateState(makeSnapshot(harness, { contentRevision: 2, savedRevision: 2 }),
    'checkpoint', oldToken);
  const reloadResult = harness.api.canReload();

  const newToken = harness.api.registerBridge(bridgeReturning(harness));
  harness.api.updateState(makeSnapshot(harness, { contentRevision: 12, savedRevision: 12 }),
    'checkpoint', newToken);
  oldReload.resolve(makeSnapshot(harness, { contentRevision: 3, savedRevision: 3 }));

  assert(await reloadResult === false, 'An old canReload result authorized a newer bridge reload.');
  assert(harness.api.getSnapshot().contentRevision === 12
    && harness.api.getSnapshot().bridgeAvailable === true,
  'An old canReload result overwrote the newer bridge snapshot.');
}

async function malformedSnapshotsInvalidateOnlyTheMatchingBridge() {
  const malformedKinds = [
    'missing-key',
    'extra-key',
    'string-revision',
    'numeric-dirty',
    'bridge-false'
  ];
  for (const kind of malformedKinds) {
    const harness = createHarness();
    const malformed = harness.context.makeMalformedSnapshot(kind);
    const token = harness.api.registerBridge(bridgeReturning(harness, malformed));
    harness.api.updateState(makeSnapshot(harness, {
      contentRevision: 4,
      savedRevision: 3,
      isDirty: true
    }), 'workspace-update', token);

    assert(await harness.api.canReload() === false,
      `${kind} live state was normalized into a reload authorization.`);
    const snapshot = harness.api.getSnapshot();
    assert(snapshot.bridgeAvailable === false && snapshot.isDirty === true,
      `${kind} live state did not fail closed while preserving dirty state.`);
  }

  const directHarness = createHarness();
  const directToken = directHarness.api.registerBridge(bridgeReturning(directHarness));
  directHarness.api.updateState(makeSnapshot(directHarness, {
    contentRevision: 6,
    savedRevision: 5,
    isDirty: true
  }), 'workspace-update', directToken);
  directHarness.api.updateState(
    directHarness.context.makeMalformedSnapshot('string-revision'),
    'checkpoint',
    directToken);
  const directSnapshot = directHarness.api.getSnapshot();
  assert(directSnapshot.bridgeAvailable === false
    && directSnapshot.isDirty === true
    && directSnapshot.contentRevision === 6,
  'Malformed updateState input was normalized into clean/newer state.');
}

async function wireRevisionsAreStrictPositiveSafeIntegersAndDeleteAllowsSameRevision() {
  const harness = createHarness();
  const received = [];
  const token = harness.api.registerBridge({
    invokeMethodAsync(method, workspaceId, revision, mutationKind) {
      if (method === 'HandleExternalWorkspaceRevisionAsync') {
        received.push({ workspaceId, revision, mutationKind });
      }
      return Promise.resolve(makeSnapshot(harness));
    }
  });
  harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 5,
    savedRevision: 5
  }), 'snapshot', token);

  const invalidRevisions = [
    true,
    false,
    '6',
    NaN,
    Infinity,
    -Infinity,
    Number.MAX_SAFE_INTEGER + 1,
    0,
    -1,
    1.5,
    null
  ];
  for (const revision of invalidRevisions) {
    harness.channel().emit(makeWire(harness, { revision }));
  }
  harness.channel().emit(makeWire(harness, { revision: 5, mutationKind: 'checkpoint' }));
  await flushAsyncWork();
  assert(received.length === 0,
    `Malformed or non-advancing wire revisions crossed the bridge: ${JSON.stringify(received)}`);

  harness.channel().emit(makeWire(harness, { revision: 5, mutationKind: 'delete' }));
  await flushAsyncWork();
  assert(received.length === 1
    && received[0].revision === 5
    && received[0].mutationKind === 'delete',
  `A same-revision delete tombstone was rejected: ${JSON.stringify(received)}`);
}

async function committedDeletePublishingRequiresTheCurrentOpaqueToken() {
  const harness = createHarness();
  const firstToken = harness.api.registerBridge(bridgeReturning(harness));
  const activeToken = harness.api.registerBridge(bridgeReturning(harness));
  harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 7,
    savedRevision: 7
  }), 'snapshot', activeToken);

  assert(harness.api.publishDelete('token-test-workspace', 7, firstToken) === false,
    'A stale generation published a delete tombstone.');
  for (const revision of [true, '7', NaN, Infinity, Number.MAX_SAFE_INTEGER + 1, 0, -1]) {
    assert(harness.api.publishDelete('token-test-workspace', revision, activeToken) === false,
      `Malformed committed delete revision was accepted: ${String(revision)}`);
  }
  assert(harness.channel().messages.length === 0,
    'Rejected delete publications leaked a wire message.');

  assert(harness.api.publishDelete('token-test-workspace', 7, activeToken) === true,
    'The active generation could not publish a same-revision committed delete.');
  const messages = harness.channel().messages;
  assert(messages.length === 1
    && messages[0].workspaceId === 'token-test-workspace'
    && messages[0].revision === 7
    && messages[0].mutationKind === 'delete',
  `Committed delete did not use the exact wire schema: ${JSON.stringify(messages)}`);
}

async function rollingOwnerKeyChannelsPublishOnBothAndDedupeInboundDelivery() {
  const currentOwnerToken = 'b'.repeat(64);
  const previousOwnerToken = 'c'.repeat(64);
  const harness = createHarness({
    ownerInvalidationTokens: [currentOwnerToken, previousOwnerToken]
  });
  const received = [];
  const token = harness.api.registerBridge({
    invokeMethodAsync(method, workspaceId, revision, mutationKind) {
      if (method === 'HandleExternalWorkspaceRevisionAsync') {
        received.push({ workspaceId, revision, mutationKind });
      }
      return Promise.resolve(makeSnapshot(harness));
    }
  });
  harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 5,
    savedRevision: 5
  }), 'snapshot', token);

  assert(harness.channels.length === 2,
    `Rolling key page did not join both owner channels: ${harness.channels.length}`);
  assert(JSON.stringify(harness.api.channelNames) === JSON.stringify([
    `chummer-build-workspace-integrity-v1-${currentOwnerToken}`,
    `chummer-build-workspace-integrity-v1-${previousOwnerToken}`
  ]), 'Rolling owner channel order was not current then previous.');

  harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 6,
    savedRevision: 5
  }), 'workspace-update', token);
  assert(harness.channels.every(channel => channel.messages.length === 1
    && channel.messages[0].revision === 6),
  'A rolling-key publication did not reach both opaque owner channels.');

  const remote = makeWire(harness, { revision: 7, mutationKind: 'checkpoint' });
  harness.channel(0).emit(remote);
  harness.channel(1).emit(remote);
  await flushAsyncWork();
  assert(received.length === 1
    && received[0].revision === 7
    && received[0].mutationKind === 'checkpoint',
  `Dual-channel delivery re-entered the bridge: ${JSON.stringify(received)}`);
}

async function oldOnlyAndRollingPagesExchangeOnTheSharedPreviousKeyBus() {
  const previousOwnerToken = 'd'.repeat(64);
  const currentOwnerToken = 'e'.repeat(64);
  const foreignOwnerToken = 'f'.repeat(64);
  const bus = createBroadcastBus();
  const oldPage = createHarness({
    ownerInvalidationTokens: [previousOwnerToken],
    broadcastBus: bus
  });
  const rollingPage = createHarness({
    ownerInvalidationTokens: [currentOwnerToken, previousOwnerToken],
    broadcastBus: bus
  });
  const foreignPage = createHarness({
    ownerInvalidationTokens: [foreignOwnerToken],
    broadcastBus: bus
  });
  const receivedByOld = [];
  const receivedByRolling = [];
  const receivedByForeign = [];
  const oldToken = oldPage.api.registerBridge({
    invokeMethodAsync(method, workspaceId, revision, mutationKind) {
      if (method === 'HandleExternalWorkspaceRevisionAsync') {
        receivedByOld.push({ workspaceId, revision, mutationKind });
      }
      return Promise.resolve(makeSnapshot(oldPage));
    }
  });
  const rollingToken = rollingPage.api.registerBridge({
    invokeMethodAsync(method, workspaceId, revision, mutationKind) {
      if (method === 'HandleExternalWorkspaceRevisionAsync') {
        receivedByRolling.push({ workspaceId, revision, mutationKind });
      }
      return Promise.resolve(makeSnapshot(rollingPage));
    }
  });
  const foreignToken = foreignPage.api.registerBridge({
    invokeMethodAsync(method, workspaceId, revision, mutationKind) {
      if (method === 'HandleExternalWorkspaceRevisionAsync') {
        receivedByForeign.push({ workspaceId, revision, mutationKind });
      }
      return Promise.resolve(makeSnapshot(foreignPage));
    }
  });
  oldPage.api.updateState(makeSnapshot(oldPage), 'snapshot', oldToken);
  rollingPage.api.updateState(makeSnapshot(rollingPage), 'snapshot', rollingToken);
  foreignPage.api.updateState(makeSnapshot(foreignPage), 'snapshot', foreignToken);

  rollingPage.api.updateState(makeSnapshot(rollingPage, {
    contentRevision: 2,
    savedRevision: 1
  }), 'workspace-update', rollingToken);
  await flushAsyncWork();
  assert(receivedByOld.length === 1 && receivedByOld[0].revision === 2,
    `Old-only page missed the rolling page on the previous-key bus: ${JSON.stringify(receivedByOld)}`);

  oldPage.api.updateState(makeSnapshot(oldPage, {
    contentRevision: 3,
    savedRevision: 2
  }), 'checkpoint', oldToken);
  await flushAsyncWork();
  assert(receivedByRolling.length === 1 && receivedByRolling[0].revision === 3,
    `Rolling page missed the old-only page on the previous-key bus: ${JSON.stringify(receivedByRolling)}`);
  assert(receivedByForeign.length === 0,
    `A foreign owner received a rolling-key message: ${JSON.stringify(receivedByForeign)}`);
}

async function broadcastFailureClosesChannelButKeepsLocalProtection() {
  const harness = createHarness({ throwOnBroadcast: true });
  const token = harness.api.registerBridge(bridgeReturning(harness));
  const operationStart = harness.operations.length;
  harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 2,
    savedRevision: 1,
    isDirty: true
  }), 'workspace-update', token);

  const operations = harness.operations.slice(operationStart);
  assert(operations.indexOf('broadcast-post') >= 0
    && operations.indexOf('broadcast-close') > operations.indexOf('broadcast-post'),
  `Broadcast failure did not close the channel: ${JSON.stringify(operations)}`);
  assert(operations.indexOf('window-event:chummer:build-integrity-changed')
    > operations.indexOf('broadcast-close'),
  'Local integrity dispatch ran before the failed BroadcastChannel was closed.');

  const beforeUnload = {
    type: 'beforeunload',
    defaultPrevented: false,
    preventDefault() { this.defaultPrevented = true; },
    returnValue: undefined
  };
  harness.window.dispatchEvent(beforeUnload);
  assert(beforeUnload.defaultPrevented === true && beforeUnload.returnValue === '',
    'Broadcast failure disabled local dirty-state unload protection.');
  assert(harness.api.getSnapshot().isDirty === true,
    'Broadcast failure discarded the local dirty snapshot.');
}

async function bridgeRecoveryRemainsAvailableAfterRepeatedFailuresAndFocus() {
  const harness = createHarness();
  let recoveryRequests = 0;
  const bridge = {
    invokeMethodAsync(method) {
      if (method === 'RequestBuildPwaIntegrityBridgeRecoveryAsync') {
        recoveryRequests += 1;
      }
      return Promise.resolve(makeSnapshot(harness));
    }
  };

  for (let attempt = 0; attempt < 5; attempt += 1) {
    const token = harness.api.registerBridge(bridge);
    assert(harness.api.markBridgeUnavailable(token) === true,
      `Bridge failure ${attempt + 1} was not recognized.`);
    await new Promise(resolve => setTimeout(resolve, 0));
  }
  harness.window.dispatchEvent({ type: 'focus' });
  await new Promise(resolve => setTimeout(resolve, 0));
  assert(recoveryRequests >= 6,
    `Recovery stopped after repeated failures/focus: ${recoveryRequests}`);

  const recoveredToken = harness.api.registerBridge(bridge);
  const recovered = harness.api.updateState(makeSnapshot(harness, {
    contentRevision: 9,
    savedRevision: 9
  }), 'checkpoint', recoveredToken);
  assert(recovered.bridgeAvailable === true && recovered.contentRevision === 9,
    'A post-cap bridge registration could not recover.');
}

async function run() {
  await secureOpaqueTokenStaysOutsidePublicPayloads();
  await secureGenerationFailureFailsClosed();
  await staleTokenCannotMutateOrUnregisterNewBridge();
  await oldAsyncRejectionCannotClearNewBridge();
  await oldAsyncResultCannotOverwriteNewBridge();
  await malformedSnapshotsInvalidateOnlyTheMatchingBridge();
  await wireRevisionsAreStrictPositiveSafeIntegersAndDeleteAllowsSameRevision();
  await committedDeletePublishingRequiresTheCurrentOpaqueToken();
  await rollingOwnerKeyChannelsPublishOnBothAndDedupeInboundDelivery();
  await oldOnlyAndRollingPagesExchangeOnTheSharedPreviousKeyBus();
  await broadcastFailureClosesChannelButKeepsLocalProtection();
  await bridgeRecoveryRemainsAvailableAfterRepeatedFailuresAndFocus();
}

run()
  .then(() => console.log('Build PWA bridge token and exact snapshot checks passed.'))
  .catch((error) => {
    console.error(error && error.stack ? error.stack : error);
    process.exitCode = 1;
  });
