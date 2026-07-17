#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createHash, webcrypto } = require('node:crypto');

const workerSource = fs.readFileSync(
  path.join(__dirname, '..', 'Chummer.Blazor', 'wwwroot', 'service-worker.js'),
  'utf8');
const legacyV6Source = fs.readFileSync(
  path.join(__dirname, 'fixtures', 'build-pwa-service-worker-v6.fixture.js'),
  'utf8');
const origin = 'https://chummer.test';
const releasePathsMatch = workerSource.match(/const RELEASE_CONTENT_PATHS = \[(.*?)\];/s);
const releasePaths = releasePathsMatch
  ? [...releasePathsMatch[1].matchAll(/'([^']+)'/g)].map(match => match[1])
  : [];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function contentTypeForPath(publicPath) {
  const normalized = publicPath.toLowerCase();
  if (normalized.endsWith('.css')) return 'text/css';
  if (normalized.endsWith('.js')) return 'text/javascript';
  if (normalized.endsWith('.webmanifest')) return 'application/manifest+json';
  if (normalized.endsWith('.html')) return 'text/html';
  if (normalized.endsWith('.png')) return 'image/png';
  if (normalized.endsWith('.svg')) return 'image/svg+xml';
  return 'application/octet-stream';
}

function tagBasicResponse(response, url) {
  Object.defineProperty(response, 'type', { configurable: true, value: 'basic' });
  Object.defineProperty(response, 'url', { configurable: true, value: url || '' });
  Object.defineProperty(response, 'redirected', { configurable: true, value: false });
  const nativeClone = response.clone.bind(response);
  Object.defineProperty(response, 'clone', {
    configurable: true,
    value: () => tagBasicResponse(nativeClone(), url)
  });
  return response;
}

function basicResponse(body, { url = '', revision = null, contentType = 'text/plain' } = {}) {
  const headers = {
    'Cache-Control': 'public,max-age=31536000,immutable',
    'Content-Type': contentType
  };
  if (revision) headers['X-Chummer-Build-Content-Revision'] = revision;
  return tagBasicResponse(new Response(body, { status: 200, headers }), url);
}

function canonicalRevision(contents) {
  const aggregate = createHash('sha256');
  for (const publicPath of releasePaths) {
    const encodedPath = Buffer.from(publicPath, 'utf8');
    const content = contents.get(publicPath);
    assert(content, `Synthetic release omitted ${publicPath}.`);
    const pathLength = Buffer.alloc(4);
    const contentLength = Buffer.alloc(8);
    pathLength.writeUInt32BE(encodedPath.length);
    contentLength.writeBigUInt64BE(BigInt(content.length));
    aggregate.update(pathLength);
    aggregate.update(encodedPath);
    aggregate.update(contentLength);
    aggregate.update(content);
  }
  return aggregate.digest('hex');
}

function buildRelease(label) {
  const contents = new Map(releasePaths.map(publicPath => [
    publicPath,
    Buffer.from(`${label}:${publicPath}`, 'utf8')
  ]));
  return { label, contents, revision: canonicalRevision(contents) };
}

function createReleaseNetwork(scopePath, releases) {
  const normalizedScope = scopePath.endsWith('/') ? scopePath : `${scopePath}/`;
  const releasesByRevision = new Map(releases.map(release => [release.revision, release]));
  const corruptions = new Map();
  return {
    deployedRevision: releases[0].revision,
    offline: false,
    corrupt(revision, publicPath) { corruptions.set(`${revision}:${publicPath}`, true); },
    clearCorruptions() { corruptions.clear(); },
    async fetch(request) {
      if (this.offline) throw new Error('simulated offline');
      const url = new URL(typeof request === 'string' ? request : request.url);
      if (url.pathname === `${normalizedScope}app`) {
        const body = `<!doctype html><script src="${normalizedScope}js/build-pwa-integrity.js?build=${this.deployedRevision}"></script>`;
        return basicResponse(body, { url: url.href, contentType: 'text/html' });
      }

      const relativePath = url.pathname.startsWith(normalizedScope)
        ? url.pathname.slice(normalizedScope.length)
        : null;
      assert(relativePath, `Network rejected out-of-scope request ${url.href}.`);
      const revision = url.searchParams.get('build');
      if (!revision) {
        return basicResponse(`legacy-v6:${relativePath}`, {
          url: url.href,
          contentType: contentTypeForPath(relativePath)
        });
      }
      const release = releasesByRevision.get(revision);
      assert(release, `Network has no immutable release for ${url.href}.`);
      let content = release.contents.get(relativePath);
      assert(content, `Network release omitted ${relativePath}.`);
      if (corruptions.has(`${revision}:${relativePath}`)) {
        content = Buffer.concat([content.subarray(0, Math.max(0, content.length - 1)), Buffer.from('!')]);
      }
      return basicResponse(content, {
        url: url.href,
        revision,
        contentType: contentTypeForPath(relativePath)
      });
    }
  };
}

function createSharedCaches() {
  const stores = new Map();
  const deleted = [];
  const keyFor = request => typeof request === 'string' ? request : request.url;

  function cacheFor(cacheName) {
    return {
      async match(request) {
        const entry = stores.get(cacheName)?.get(keyFor(request));
        if (!entry) return undefined;
        return basicResponse(entry.body.slice(), {
          url: entry.url,
          revision: entry.revision,
          contentType: entry.contentType
        });
      },
      async put(request, response) {
        const key = keyFor(request);
        const body = Buffer.from(await response.arrayBuffer());
        if (!stores.has(cacheName)) stores.set(cacheName, new Map());
        stores.get(cacheName).set(key, {
          body,
          url: response.url || '',
          revision: response.headers.get('X-Chummer-Build-Content-Revision'),
          contentType: response.headers.get('Content-Type') || 'application/octet-stream'
        });
      },
      async delete(request) {
        return stores.get(cacheName)?.delete(keyFor(request)) === true;
      }
    };
  }

  return {
    api: {
      async open(cacheName) {
        if (!stores.has(cacheName)) stores.set(cacheName, new Map());
        return cacheFor(cacheName);
      },
      async keys() { return [...stores.keys()]; },
      async delete(cacheName) {
        deleted.push(cacheName);
        return stores.delete(cacheName);
      }
    },
    deleted,
    snapshot(cacheName) {
      return [...(stores.get(cacheName) || new Map()).entries()]
        .map(([url, entry]) => [url, entry.body.toString('utf8')])
        .sort((left, right) => left[0].localeCompare(right[0]));
    }
  };
}

function createClient(id, pathname) {
  return {
    id,
    type: 'window',
    url: `${origin}${pathname}`,
    messages: [],
    onMessage: null,
    postMessage(message) {
      this.messages.push(JSON.parse(JSON.stringify(message)));
      this.onMessage?.(message);
    }
  };
}

function createWorkerHarness({
  source = workerSource,
  scopePath,
  revision = null,
  sharedCaches,
  network,
  clientSnapshots = [[]],
  clock = { now: 1700000000000 }
}) {
  const handlers = new Map();
  const normalizedScope = scopePath.endsWith('/') ? scopePath : `${scopePath}/`;
  const scopeUrl = `${origin}${normalizedScope}`;
  const workerUrl = new URL('service-worker.js', scopeUrl);
  if (revision) workerUrl.searchParams.set('build', revision);
  let clientSnapshotIndex = 0;
  class HarnessDate extends Date {
    static now() { return clock.now; }
  }
  const registration = { scope: scopeUrl, installing: null, waiting: null, active: null };
  const context = {
    URL,
    Request,
    Response,
    Promise,
    Set,
    Map,
    Date: HarnessDate,
    Object,
    Array,
    String,
    Number,
    RegExp,
    Uint8Array,
    DataView,
    TextEncoder,
    crypto: webcrypto,
    console,
    clearTimeout,
    setTimeout: (callback, timeout, ...args) =>
      setTimeout(callback, Math.min(Number(timeout) || 0, 20), ...args),
    fetch: request => network.fetch(request),
    caches: sharedCaches.api,
    self: {
      location: { origin, href: workerUrl.href },
      registration,
      clients: {
        matchAll: async () => {
          const index = Math.min(clientSnapshotIndex, clientSnapshots.length - 1);
          clientSnapshotIndex += 1;
          return clientSnapshots[index] || [];
        }
      },
      addEventListener: (type, handler) => handlers.set(type, handler)
    }
  };
  context.globalThis = context;
  vm.createContext(context);
  const exposure = source === workerSource
    ? `;globalThis.__test = Object.freeze({
        cacheName: CHUMMER_PWA_CACHE,
        metadataUrl: CHUMMER_BUILD_PWA_CACHE_METADATA_URL,
        offlineUrl: OFFLINE_URL,
        releaseAssets: Object.freeze(RELEASE_CONTENT_ASSETS.map(asset => asset.url)),
        requestSweep: requestCacheLeaseSweep,
        requestType: CHUMMER_BUILD_PWA_CACHE_LEASE_REQUEST,
        responseType: CHUMMER_BUILD_PWA_CACHE_LEASE_RESPONSE,
        sweepType: CHUMMER_BUILD_PWA_CACHE_LEASE_SWEEP
      });`
    : ';globalThis.__test = Object.freeze({ cacheName: CHUMMER_PWA_CACHE, offlineUrl: OFFLINE_URL });';
  vm.runInContext(`${source}\n${exposure}`, context, { filename: revision ? 'service-worker-v7.js' : 'service-worker-v6.fixture.js' });

  return {
    api: context.__test,
    handlers,
    registration,
    workerUrl: workerUrl.href,
    async lifecycle(type) {
      let lifetime = null;
      handlers.get(type)({ waitUntil: promise => { lifetime = Promise.resolve(promise); } });
      assert(lifetime, `${type} did not extend worker lifetime.`);
      await lifetime;
    },
    async fetch(request) {
      let responsePromise = null;
      const background = [];
      handlers.get('fetch')({
        request,
        respondWith: promise => { responsePromise = Promise.resolve(promise); },
        waitUntil: promise => background.push(Promise.resolve(promise))
      });
      const response = responsePromise ? await responsePromise : await network.fetch(request);
      await Promise.all(background);
      return response;
    },
    message(data, sourceClient) {
      let lifetime = null;
      handlers.get('message')?.({
        data,
        source: sourceClient,
        waitUntil: promise => { lifetime = Promise.resolve(promise); }
      });
      return lifetime;
    }
  };
}

function respondToLeases(harness, client, cacheVersion = 'v6') {
  client.onMessage = message => {
    if (message.type !== harness.api.requestType) return;
    setTimeout(() => harness.message({
      type: harness.api.responseType,
      requestId: message.requestId,
      cacheVersion
    }, client), 0);
  };
}

async function frozenLegacyMigration(scopePath) {
  const release = buildRelease(`new-${scopePath}`);
  const network = createReleaseNetwork(scopePath, [release]);
  const caches = createSharedCaches();
  const legacy = createWorkerHarness({
    source: legacyV6Source,
    scopePath,
    sharedCaches: caches,
    network
  });
  await legacy.lifecycle('install');
  const legacySnapshot = caches.snapshot(legacy.api.cacheName);
  assert(legacySnapshot.length > 0, `${scopePath} frozen v6 fixture did not install.`);

  const current = createWorkerHarness({
    scopePath,
    revision: release.revision,
    sharedCaches: caches,
    network
  });
  await current.lifecycle('install');
  assert(JSON.stringify(caches.snapshot(legacy.api.cacheName)) === JSON.stringify(legacySnapshot),
    `${scopePath} v7 waiting install mutated the frozen v6 cache.`);
  assert((await caches.api.keys()).includes(legacy.api.cacheName),
    `${scopePath} v7 waiting install reclaimed v6 before activation.`);
  await current.lifecycle('activate');
  assert(!(await caches.api.keys()).includes(legacy.api.cacheName),
    `${scopePath} safe v7 activation did not reclaim frozen v6.`);
}

async function singleByteMismatchFailsBeforeCacheOpen(scopePath) {
  const release = buildRelease(`mismatch-${scopePath}`);
  const network = createReleaseNetwork(scopePath, [release]);
  const caches = createSharedCaches();
  network.corrupt(release.revision, 'js/build-pwa-integrity.js');
  const worker = createWorkerHarness({
    scopePath,
    revision: release.revision,
    sharedCaches: caches,
    network
  });
  let rejected = false;
  try {
    await worker.lifecycle('install');
  } catch {
    rejected = true;
  }
  assert(rejected, `${scopePath} single-byte drift did not reject installation.`);
  assert(!(await caches.api.keys()).includes(worker.api.cacheName),
    `${scopePath} failed byte verification opened a partial cache.`);
}

async function orphanWaitingCacheGetsGraceThenReclaims(scopePath) {
  const [activeRelease, orphanRelease] = ['active', 'orphan'].map(label =>
    buildRelease(`${scopePath}:${label}`));
  const network = createReleaseNetwork(scopePath, [activeRelease, orphanRelease]);
  const caches = createSharedCaches();
  const clock = { now: 1700000000000 };
  const active = createWorkerHarness({
    scopePath,
    revision: activeRelease.revision,
    sharedCaches: caches,
    network,
    clock
  });
  await active.lifecycle('install');
  await active.lifecycle('activate');
  const orphan = createWorkerHarness({
    scopePath,
    revision: orphanRelease.revision,
    sharedCaches: caches,
    network,
    clock
  });
  await orphan.lifecycle('install');
  assert(await active.api.requestSweep() === true,
    `${scopePath} immediate orphan grace sweep failed.`);
  assert((await caches.api.keys()).includes(orphan.api.cacheName),
    `${scopePath} fresh waiting cache was deleted before registration grace.`);
  clock.now += (24 * 60 * 60 * 1000) + 1;
  assert(await active.api.requestSweep() === true,
    `${scopePath} aged orphan sweep failed.`);
  assert(!(await caches.api.keys()).includes(orphan.api.cacheName),
    `${scopePath} aged unreferenced waiting cache leaked.`);
}

async function threeReleaseLifecycleAndRestart(scopePath) {
  const releases = ['N', 'N+1', 'N+2'].map(label => buildRelease(`${scopePath}:${label}`));
  const [releaseN, releaseN1, releaseN2] = releases;
  const network = createReleaseNetwork(scopePath, releases);
  const caches = createSharedCaches();
  const incumbent = createWorkerHarness({
    scopePath,
    revision: releaseN.revision,
    sharedCaches: caches,
    network
  });
  await incumbent.lifecycle('install');
  await incumbent.lifecycle('activate');
  const incumbentSnapshot = caches.snapshot(incumbent.api.cacheName);

  const waiting = createWorkerHarness({
    scopePath,
    revision: releaseN1.revision,
    sharedCaches: caches,
    network
  });
  incumbent.registration.waiting = { scriptURL: waiting.workerUrl };
  await waiting.lifecycle('install');
  assert(JSON.stringify(caches.snapshot(incumbent.api.cacheName)) === JSON.stringify(incumbentSnapshot),
    `${scopePath} N+1 waiting worker mutated N.`);
  assert(await incumbent.api.requestSweep() === true,
    `${scopePath} stable waiting-cache sweep failed.`);
  assert((await caches.api.keys()).includes(waiting.api.cacheName),
    `${scopePath} active N deleted explicit waiting N+1.`);

  network.deployedRevision = releaseN1.revision;
  const normalizedScope = scopePath.endsWith('/') ? scopePath : `${scopePath}/`;
  const navigation = { method: 'GET', mode: 'navigate', url: `${origin}${normalizedScope}app` };
  const html = await (await incumbent.fetch(navigation)).text();
  const assetUrl = new URL(html.match(/src="([^"]+)"/)[1], origin).href;
  const n1Asset = await (await incumbent.fetch(new Request(assetUrl))).text();
  assert(n1Asset.startsWith(`${scopePath}:N+1:`),
    `${scopePath} incumbent mixed N bytes into N+1 HTML.`);

  network.offline = true;
  const incumbentOffline = await (await incumbent.fetch(navigation)).text();
  assert(incumbentOffline.startsWith(`${scopePath}:N:offline.html`),
    `${scopePath} waiting state did not keep the N offline fallback.`);
  network.offline = false;

  incumbent.registration.waiting = null;
  await waiting.lifecycle('activate');
  assert(!(await caches.api.keys()).includes(incumbent.api.cacheName),
    `${scopePath} N+1 activation leaked same-generation N.`);

  const restarted = createWorkerHarness({
    scopePath,
    revision: releaseN1.revision,
    sharedCaches: caches,
    network
  });
  assert(await restarted.api.requestSweep() === true,
    `${scopePath} active N+1 restart could not sweep its sealed cache set.`);
  assert((await caches.api.keys()).includes(restarted.api.cacheName),
    `${scopePath} restart reclaimed its own current cache.`);

  const nextWaiting = createWorkerHarness({
    scopePath,
    revision: releaseN2.revision,
    sharedCaches: caches,
    network
  });
  restarted.registration.installing = { scriptURL: nextWaiting.workerUrl };
  await nextWaiting.lifecycle('install');
  restarted.registration.installing = null;
  restarted.registration.waiting = { scriptURL: nextWaiting.workerUrl };
  assert(await restarted.api.requestSweep() === true,
    `${scopePath} N+1 could not prove stable N+2 waiting topology.`);
  assert((await caches.api.keys()).includes(nextWaiting.api.cacheName),
    `${scopePath} N+1 deleted explicit waiting N+2.`);

  restarted.registration.waiting = null;
  network.deployedRevision = releaseN2.revision;
  await nextWaiting.lifecycle('activate');
  const finalCaches = await caches.api.keys();
  assert(!finalCaches.includes(restarted.api.cacheName)
      && finalCaches.includes(nextWaiting.api.cacheName),
  `${scopePath} N+2 activation did not reclaim N+1 exactly: ${JSON.stringify(finalCaches)}`);
  network.offline = true;
  const nextOffline = await (await nextWaiting.fetch(navigation)).text();
  assert(nextOffline.startsWith(`${scopePath}:N+2:offline.html`),
    `${scopePath} N+2 offline fallback crossed release boundaries.`);
}

async function leaseTopologyAndRootScopeBoundary() {
  const release = buildRelease('lease-root');
  const network = createReleaseNetwork('/', [release]);
  const caches = createSharedCaches();
  const buildClient = createClient('build', '/app');
  const unrelated = createClient('unrelated', '/account/settings');
  const worker = createWorkerHarness({
    scopePath: '/',
    revision: release.revision,
    sharedCaches: caches,
    network,
    clientSnapshots: [[buildClient, unrelated], [buildClient, unrelated], [buildClient, unrelated]]
  });
  respondToLeases(worker, buildClient);
  await worker.lifecycle('install');
  await worker.lifecycle('activate');
  assert(unrelated.messages.length === 0,
    'Root-scope cache maintenance messaged an unrelated same-origin window.');

  const missing = createClient('missing', '/app');
  const failClosed = createWorkerHarness({
    scopePath: '/',
    revision: release.revision,
    sharedCaches: caches,
    network,
    clientSnapshots: [[missing], [missing]]
  });
  assert(await failClosed.api.requestSweep() === false,
    'Missing Build-client lease did not fail closed.');

  const arrived = createClient('arrived', '/workbench');
  const race = createWorkerHarness({
    scopePath: '/',
    revision: release.revision,
    sharedCaches: caches,
    network,
    clientSnapshots: [[buildClient], [buildClient, arrived]]
  });
  respondToLeases(race, buildClient);
  assert(await race.api.requestSweep() === false,
    'Build-client topology race did not fail closed.');
}

async function run() {
  assert(releasePaths.length >= 18, 'Worker release closure was not discovered exactly.');
  assert(!workerSource.includes('self.skipWaiting(') && !workerSource.includes('self.clients.claim('),
    'Current worker exposes forced activation.');
  assert(!legacyV6Source.includes('self.skipWaiting(') && !legacyV6Source.includes('self.clients.claim('),
    'Frozen predecessor fixture does not model the passive v6 predecessor.');
  for (const scopePath of ['/', '/blazor/']) {
    await frozenLegacyMigration(scopePath);
    await singleByteMismatchFailsBeforeCacheOpen(scopePath);
    await orphanWaitingCacheGetsGraceThenReclaims(scopePath);
    await threeReleaseLifecycleAndRestart(scopePath);
  }
  await leaseTopologyAndRootScopeBoundary();
}

run()
  .then(() => console.log('Build PWA immutable release, cache lifecycle, and fail-closed checks passed.'))
  .catch(error => {
    console.error(error && error.stack ? error.stack : error);
    process.exitCode = 1;
  });
