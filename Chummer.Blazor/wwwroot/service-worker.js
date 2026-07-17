const CHUMMER_BUILD_PWA_CACHE_PREFIX = 'chummer-build-static-';
// v6 is the compatibility lease marker emitted by already-loaded pages.
const CHUMMER_BUILD_PWA_CACHE_VERSION = 'v6';
const CHUMMER_BUILD_PWA_CACHE_GENERATION = 'v7';
const CHUMMER_BUILD_PWA_RELEASE_QUERY_KEY = 'build';
const CHUMMER_BUILD_PWA_RELEASE_REVISION_HEADER = 'X-Chummer-Build-Content-Revision';
const CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_WAITING = 'waiting';
const CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_ACTIVE = 'active';
const CHUMMER_BUILD_PWA_ORPHAN_WAITING_GRACE_MS = 24 * 60 * 60 * 1000;
const CHUMMER_PWA_ACTIVATED_MESSAGE = 'chummer-build-update-activated';
const CHUMMER_BUILD_PWA_CACHE_LEASE_REQUEST = 'chummer-build-pwa-cache-lease-request';
const CHUMMER_BUILD_PWA_CACHE_LEASE_RESPONSE = 'chummer-build-pwa-cache-lease-response';
const CHUMMER_BUILD_PWA_CACHE_LEASE_SWEEP = 'chummer-build-pwa-cache-lease-sweep';
const CHUMMER_BUILD_PWA_CACHE_LEASE_TIMEOUT_MS = 1500;
const CHUMMER_BUILD_PWA_WORKER_URL = new URL(self.location.href);
const CHUMMER_BUILD_PWA_SCOPE_URL = new URL(self.registration.scope);
const CHUMMER_BUILD_PWA_SCOPE_PATH = CHUMMER_BUILD_PWA_SCOPE_URL.pathname.endsWith('/')
  ? CHUMMER_BUILD_PWA_SCOPE_URL.pathname
  : `${CHUMMER_BUILD_PWA_SCOPE_URL.pathname}/`;
const CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION =
  exactReleaseContentRevision(CHUMMER_BUILD_PWA_WORKER_URL);
if (!CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION) {
  throw new Error('Build PWA worker requires one exact release-content revision.');
}
const CHUMMER_PWA_CACHE = buildRevisionCacheName(
  CHUMMER_BUILD_PWA_CACHE_GENERATION,
  CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION);
const CHUMMER_BUILD_PWA_CACHE_METADATA_URL = new URL(
  '__chummer-build-cache-metadata__',
  CHUMMER_BUILD_PWA_SCOPE_URL).href;
const RELEASE_CONTENT_PATHS = [
  'service-worker.js',
  'offline.html',
  'app.css',
  'build-pwa-install.css',
  'Chummer.Blazor.styles.css',
  'manifest.webmanifest',
  'js/build-pwa-recovery.js',
  'js/build-pwa-integrity.js',
  'js/build-pwa-install.js',
  'js/build-pwa-layout.js',
  'js/privacy-boundaries.js',
  '_framework/blazor.web.js',
  'icons/chummer-build-180.png',
  'icons/chummer-build-192.png',
  'icons/chummer-build-512.png',
  'icons/chummer-build-maskable-512.png',
  'icons/chummer-pwa.svg',
  'icons/chummer-pwa-maskable.svg'
];
const RELEASE_CONTENT_ASSETS = RELEASE_CONTENT_PATHS.map(publicPath => Object.freeze({
  publicPath,
  url: buildRevisionedAssetUrl(publicPath)
}));
const RELEASE_CONTENT_PATHNAMES = new Map(
  RELEASE_CONTENT_ASSETS.map(asset => [new URL(asset.url).pathname, asset.publicPath]));
const OFFLINE_URL = buildRevisionedAssetUrl('offline.html');
const BUILD_WINDOW_ROUTES = new Set(['', 'app', 'online', 'workbench']);
let cacheLeaseRequestSequence = 0;
let pendingCacheLeaseRequest = null;
let cacheLeaseSweepPromise = null;

self.addEventListener('install', event => {
  event.waitUntil(installVerifiedRelease());
});

self.addEventListener('message', event => {
  if (event.data?.type === CHUMMER_BUILD_PWA_CACHE_LEASE_RESPONSE) {
    recordCacheLeaseResponse(event);
    return;
  }

  if (event.data?.type === CHUMMER_BUILD_PWA_CACHE_LEASE_SWEEP
      && isPlainExactMessage(event.data, ['type'])
      && isBuildWindowClient(event.source)) {
    event.waitUntil(requestCacheLeaseSweep());
  }
});

self.addEventListener('activate', event => {
  // Activation remains passive: there is no skipWaiting or clients.claim path.
  // The browser may activate this worker only after incumbent clients release it.
  event.waitUntil(activateBuildWorker());
});

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => offlineFallback()));
    return;
  }

  const publicPath = exactReleasePublicPath(url);
  if (!publicPath) return;
  event.respondWith(serveExactReleaseAsset(request, url));
});

async function installVerifiedRelease() {
  // Fetch every byte before opening the revision cache. A failed response or
  // aggregate mismatch therefore cannot leave a partially staged namespace.
  const fetchedAssets = await Promise.all(RELEASE_CONTENT_ASSETS.map(fetchReleaseAssetForInstall));
  const derivedRevision = await deriveReleaseContentRevision(fetchedAssets);
  if (derivedRevision !== CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION) {
    throw new Error('Build PWA release byte contract did not match its requested revision.');
  }

  const cache = await caches.open(CHUMMER_PWA_CACHE);
  await Promise.all(fetchedAssets.map(asset => cache.put(asset.request, asset.response)));
  await writeOwnCacheMetadata(cache, CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_WAITING);

  const offline = await cache.match(OFFLINE_URL);
  if (!isVerifiedReleaseResponse(
      offline,
      new URL(OFFLINE_URL),
      CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION)) {
    throw new Error('Build PWA offline fallback could not be sealed.');
  }
}

async function fetchReleaseAssetForInstall(asset) {
  const request = new Request(asset.url, {
    cache: 'no-store',
    credentials: 'same-origin',
    redirect: 'error'
  });
  const response = await fetch(request);
  if (!isVerifiedReleaseResponse(
      response,
      new URL(asset.url),
      CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION)) {
    throw new Error(`Build PWA precache rejected ${asset.publicPath}`);
  }

  return Object.freeze({
    publicPath: asset.publicPath,
    request,
    response,
    bytes: new Uint8Array(await response.clone().arrayBuffer())
  });
}

async function deriveReleaseContentRevision(fetchedAssets) {
  const chunks = [];
  let totalLength = 0;
  const encoder = new TextEncoder();
  for (const asset of fetchedAssets) {
    const pathBytes = encoder.encode(asset.publicPath);
    const pathLength = encodeUint32(pathBytes.byteLength);
    const contentLength = encodeUint64(asset.bytes.byteLength);
    for (const chunk of [pathLength, pathBytes, contentLength, asset.bytes]) {
      chunks.push(chunk);
      totalLength += chunk.byteLength;
    }
  }

  const framed = new Uint8Array(totalLength);
  let offset = 0;
  for (const chunk of chunks) {
    framed.set(chunk, offset);
    offset += chunk.byteLength;
  }
  const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', framed));
  return [...digest].map(value => value.toString(16).padStart(2, '0')).join('');
}

function encodeUint32(value) {
  if (!Number.isSafeInteger(value) || value < 0 || value > 0xffffffff) {
    throw new Error('Build PWA release path length escaped uint32 framing.');
  }
  const result = new Uint8Array(4);
  new DataView(result.buffer).setUint32(0, value, false);
  return result;
}

function encodeUint64(value) {
  if (!Number.isSafeInteger(value) || value < 0) {
    throw new Error('Build PWA release content length escaped safe uint64 framing.');
  }
  const result = new Uint8Array(8);
  const view = new DataView(result.buffer);
  view.setUint32(0, Math.floor(value / 0x100000000), false);
  view.setUint32(4, value >>> 0, false);
  return result;
}

async function serveExactReleaseAsset(request, url) {
  const requestedRevision = exactReleaseContentRevision(url);
  if (!requestedRevision) return Response.error();

  if (requestedRevision !== CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION) {
    return fetchVerifiedReleaseAsset(request, url, requestedRevision);
  }

  const cache = await caches.open(CHUMMER_PWA_CACHE);
  const cached = await cache.match(request);
  if (isVerifiedReleaseResponse(cached, url, requestedRevision)) return cached;
  if (cached) await cache.delete(request);

  // The install cache is immutable after it is sealed. Cache eviction falls
  // back to a metadata-verified network response without repopulating it from
  // a potentially changing deployment edge.
  return fetchVerifiedReleaseAsset(request, url, requestedRevision);
}

async function fetchVerifiedReleaseAsset(request, url, requestedRevision) {
  try {
    const networkRequest = new Request(request, {
      cache: 'no-store',
      credentials: 'same-origin',
      redirect: 'error'
    });
    const response = await fetch(networkRequest);
    return isVerifiedReleaseResponse(response, url, requestedRevision)
      ? response
      : Response.error();
  } catch {
    return Response.error();
  }
}

async function offlineFallback() {
  const cache = await caches.open(CHUMMER_PWA_CACHE);
  const cached = await cache.match(OFFLINE_URL);
  if (isVerifiedReleaseResponse(
      cached,
      new URL(OFFLINE_URL),
      CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION)) {
    return cached;
  }
  if (cached) await cache.delete(OFFLINE_URL);
  return Response.error();
}

function exactReleasePublicPath(url) {
  if (!(url instanceof URL) || url.origin !== CHUMMER_BUILD_PWA_SCOPE_URL.origin) return null;
  return RELEASE_CONTENT_PATHNAMES.get(url.pathname) || null;
}

function exactReleaseContentRevision(url) {
  if (!(url instanceof URL)) return null;
  const keys = [...url.searchParams.keys()];
  if (keys.length !== 1 || keys[0] !== CHUMMER_BUILD_PWA_RELEASE_QUERY_KEY) return null;
  const revision = url.searchParams.get(CHUMMER_BUILD_PWA_RELEASE_QUERY_KEY);
  return isValidReleaseContentRevision(revision) ? revision : null;
}

function buildRevisionedAssetUrl(publicPath) {
  const url = new URL(publicPath, CHUMMER_BUILD_PWA_SCOPE_URL);
  url.searchParams.set(
    CHUMMER_BUILD_PWA_RELEASE_QUERY_KEY,
    CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION);
  return url.href;
}

function isVerifiedReleaseResponse(response, assetUrl, expectedRevision) {
  if (!response
      || response.status !== 200
      || response.type !== 'basic'
      || response.redirected === true) {
    return false;
  }
  if (!(assetUrl instanceof URL)
      || assetUrl.origin !== CHUMMER_BUILD_PWA_SCOPE_URL.origin
      || !exactReleasePublicPath(assetUrl)
      || exactReleaseContentRevision(assetUrl) !== expectedRevision
      || response.url !== assetUrl.href) {
    return false;
  }
  if (response.headers.get(CHUMMER_BUILD_PWA_RELEASE_REVISION_HEADER) !== expectedRevision) {
    return false;
  }

  const expectedMimeTypes = expectedMimeTypesForPath(assetUrl.pathname);
  const contentType = (response.headers.get('Content-Type') || '')
    .split(';', 1)[0]
    .trim()
    .toLowerCase();
  if (!expectedMimeTypes.has(contentType)) return false;

  const cacheControl = (response.headers.get('Cache-Control') || '').toLowerCase();
  return !cacheControl.includes('private') && !cacheControl.includes('no-store');
}

function expectedMimeTypesForPath(path) {
  const normalized = path.toLowerCase();
  if (normalized.endsWith('.css')) return new Set(['text/css']);
  if (normalized.endsWith('.js')) return new Set(['text/javascript', 'application/javascript']);
  if (normalized.endsWith('.webmanifest')) return new Set(['application/manifest+json', 'application/json']);
  if (normalized.endsWith('.html')) return new Set(['text/html']);
  if (normalized.endsWith('.png')) return new Set(['image/png']);
  if (normalized.endsWith('.svg')) return new Set(['image/svg+xml']);
  return new Set();
}

async function writeOwnCacheMetadata(cache, state) {
  const payload = JSON.stringify({
    cacheGeneration: CHUMMER_BUILD_PWA_CACHE_GENERATION,
    contentRevision: CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION,
    state,
    stateChangedAtEpochMs: Date.now()
  });
  await cache.put(CHUMMER_BUILD_PWA_CACHE_METADATA_URL, new Response(payload, {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  }));
}

async function readManagedCacheMetadata(cacheName) {
  try {
    const cache = await caches.open(cacheName);
    const response = await cache.match(CHUMMER_BUILD_PWA_CACHE_METADATA_URL);
    if (!response || response.status !== 200) return null;
    const value = await response.json();
    if (!isPlainExactMessage(
        value,
        ['cacheGeneration', 'contentRevision', 'state', 'stateChangedAtEpochMs'])
        || !isValidCacheVersion(value.cacheGeneration)
        || !isValidReleaseContentRevision(value.contentRevision)
        || ![CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_WAITING,
          CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_ACTIVE].includes(value.state)
        || !Number.isSafeInteger(value.stateChangedAtEpochMs)
        || value.stateChangedAtEpochMs <= 0
        || value.stateChangedAtEpochMs > Date.now()
        || buildRevisionCacheName(value.cacheGeneration, value.contentRevision) !== cacheName) {
      return null;
    }
    return value;
  } catch {
    return null;
  }
}

async function notifyBuildClientsOfActivation() {
  const clients = await snapshotBuildWindowClients();
  await Promise.allSettled(clients.map(async client => {
    client.postMessage({ type: CHUMMER_PWA_ACTIVATED_MESSAGE });
  }));
}

async function activateBuildWorker() {
  const cache = await caches.open(CHUMMER_PWA_CACHE);
  await writeOwnCacheMetadata(cache, CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_ACTIVE);
  await requestCacheLeaseSweep();
  await notifyBuildClientsOfActivation();
}

function requestCacheLeaseSweep() {
  if (cacheLeaseSweepPromise) return cacheLeaseSweepPromise;
  cacheLeaseSweepPromise = sweepUnusedBuildCaches()
    .catch(() => false)
    .finally(() => {
      pendingCacheLeaseRequest = null;
      cacheLeaseSweepPromise = null;
    });
  return cacheLeaseSweepPromise;
}

async function sweepUnusedBuildCaches() {
  const firstClients = await snapshotBuildWindowClients();
  const firstProtectedWorkers = snapshotProtectedRegistrationCaches();
  if (!firstProtectedWorkers) return false;
  const leases = await collectCacheLeases(firstClients);
  if (!leases) return false;

  const secondClients = await snapshotBuildWindowClients();
  const secondProtectedWorkers = snapshotProtectedRegistrationCaches();
  if (!secondProtectedWorkers
      || !haveSameClientIds(firstClients, secondClients)
      || !haveSameStrings(firstProtectedWorkers, secondProtectedWorkers)) {
    return false;
  }

  const retainedCaches = new Set([
    CHUMMER_PWA_CACHE,
    ...firstProtectedWorkers,
    ...leases.map(cacheVersion => `${CHUMMER_BUILD_PWA_CACHE_PREFIX}${cacheVersion}`)
  ]);
  const cacheNames = await caches.keys();
  const obsoleteCaches = [];
  for (const cacheName of cacheNames) {
    if (!isManagedBuildCache(cacheName) || retainedCaches.has(cacheName)) continue;
    const cacheGeneration = managedCacheGeneration(cacheName);
    const generationOrder = parseCacheVersion(cacheGeneration)
      - parseCacheVersion(CHUMMER_BUILD_PWA_CACHE_GENERATION);
    if (generationOrder > 0) continue;
    if (generationOrder < 0) {
      obsoleteCaches.push(cacheName);
      continue;
    }

    // Same-generation namespaces are deleted only with valid sealed metadata.
    // Unknown/partial caches remain fail-closed. An unreferenced waiting cache
    // receives a full-day registration grace period before it becomes reclaimable.
    const metadata = await readManagedCacheMetadata(cacheName);
    if (metadata?.state === CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_ACTIVE
        || (metadata?.state === CHUMMER_BUILD_PWA_CACHE_METADATA_STATE_WAITING
          && Date.now() - metadata.stateChangedAtEpochMs
            >= CHUMMER_BUILD_PWA_ORPHAN_WAITING_GRACE_MS)) {
      obsoleteCaches.push(cacheName);
    }
  }

  const finalProtectedWorkers = snapshotProtectedRegistrationCaches();
  if (!finalProtectedWorkers
      || !haveSameStrings(secondProtectedWorkers, finalProtectedWorkers)
      || obsoleteCaches.some(cacheName => finalProtectedWorkers.includes(cacheName))) {
    return false;
  }
  await Promise.all(obsoleteCaches.map(cacheName => caches.delete(cacheName)));
  return true;
}

function snapshotProtectedRegistrationCaches() {
  const protectedCaches = [];
  for (const worker of [self.registration.installing, self.registration.waiting]) {
    if (!worker || typeof worker.scriptURL !== 'string') continue;
    try {
      const url = new URL(worker.scriptURL);
      const revision = exactReleaseContentRevision(url);
      if (!revision
          || url.origin !== CHUMMER_BUILD_PWA_WORKER_URL.origin
          || url.pathname !== CHUMMER_BUILD_PWA_WORKER_URL.pathname) return null;
      protectedCaches.push(buildRevisionCacheName(
        CHUMMER_BUILD_PWA_CACHE_GENERATION,
        revision));
    } catch {
      return null;
    }
  }
  return [...new Set(protectedCaches)].sort();
}

function collectCacheLeases(clients) {
  const requestId = `build-cache-lease-${Date.now()}-${++cacheLeaseRequestSequence}`;
  const expectedClientIds = new Set(clients.map(client => client.id));
  return new Promise(resolve => {
    let completed = false;
    const finish = leases => {
      if (completed) return;
      completed = true;
      clearTimeout(pendingCacheLeaseRequest?.timeoutId);
      pendingCacheLeaseRequest = null;
      resolve(leases);
    };
    pendingCacheLeaseRequest = {
      requestId,
      expectedClientIds,
      leasesByClientId: new Map(),
      finish,
      timeoutId: setTimeout(() => finish(null), CHUMMER_BUILD_PWA_CACHE_LEASE_TIMEOUT_MS)
    };
    if (clients.length === 0) {
      finish([]);
      return;
    }
    for (const client of clients) {
      try {
        client.postMessage({ type: CHUMMER_BUILD_PWA_CACHE_LEASE_REQUEST, requestId });
      } catch {
        finish(null);
        return;
      }
    }
  });
}

function recordCacheLeaseResponse(event) {
  const pending = pendingCacheLeaseRequest;
  const data = event.data;
  const source = event.source;
  if (!pending || data?.requestId !== pending.requestId) return;
  if (!isPlainExactMessage(data, ['type', 'requestId', 'cacheVersion'])
      || !isValidCacheLeaseRequestId(data.requestId)
      || !isBuildWindowClient(source)
      || !pending.expectedClientIds.has(source.id)
      || pending.leasesByClientId.has(source.id)
      || !isValidCacheVersion(data.cacheVersion)) {
    pending.finish(null);
    return;
  }
  pending.leasesByClientId.set(source.id, data.cacheVersion);
  if (pending.leasesByClientId.size === pending.expectedClientIds.size) {
    pending.finish([...pending.leasesByClientId.values()]);
  }
}

async function snapshotBuildWindowClients() {
  const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  return clients.filter(isBuildWindowClient).sort((left, right) => left.id.localeCompare(right.id));
}

function isBuildWindowClient(client) {
  if (!client || client.type !== 'window' || typeof client.id !== 'string' || !client.id) return false;
  try {
    const url = new URL(client.url);
    if (url.origin !== self.location.origin) return false;
    const scopeRoot = CHUMMER_BUILD_PWA_SCOPE_PATH.slice(0, -1);
    if (url.pathname === scopeRoot || url.pathname === CHUMMER_BUILD_PWA_SCOPE_PATH) return true;
    if (!url.pathname.startsWith(CHUMMER_BUILD_PWA_SCOPE_PATH)) return false;
    const relativePath = url.pathname.slice(CHUMMER_BUILD_PWA_SCOPE_PATH.length).replace(/\/$/, '');
    return BUILD_WINDOW_ROUTES.has(relativePath);
  } catch {
    return false;
  }
}

function haveSameClientIds(firstSnapshot, secondSnapshot) {
  if (firstSnapshot.length !== secondSnapshot.length) return false;
  return firstSnapshot.every((client, index) => client.id === secondSnapshot[index].id);
}

function haveSameStrings(first, second) {
  return first.length === second.length && first.every((value, index) => value === second[index]);
}

function buildRevisionCacheName(generation, revision) {
  return `${CHUMMER_BUILD_PWA_CACHE_PREFIX}${generation}-${revision}`;
}

function isManagedBuildCache(cacheName) {
  if (typeof cacheName !== 'string' || !cacheName.startsWith(CHUMMER_BUILD_PWA_CACHE_PREFIX)) {
    return false;
  }
  return /^v[1-9][0-9]*(?:-[a-f0-9]{64})?$/.test(
    cacheName.slice(CHUMMER_BUILD_PWA_CACHE_PREFIX.length));
}

function managedCacheGeneration(cacheName) {
  return cacheName.slice(CHUMMER_BUILD_PWA_CACHE_PREFIX.length).split('-', 1)[0];
}

function isValidCacheVersion(cacheVersion) {
  return typeof cacheVersion === 'string'
    && cacheVersion.length <= 16
    && /^v[1-9][0-9]*$/.test(cacheVersion);
}

function parseCacheVersion(cacheVersion) {
  return Number(cacheVersion.slice(1));
}

function isValidReleaseContentRevision(revision) {
  return typeof revision === 'string' && /^[a-f0-9]{64}$/.test(revision);
}

function isValidCacheLeaseRequestId(requestId) {
  return typeof requestId === 'string'
    && requestId.length <= 128
    && /^build-cache-lease-[0-9]+-[1-9][0-9]*$/.test(requestId);
}

function isPlainExactMessage(message, expectedKeys) {
  if (!message
      || typeof message !== 'object'
      || Array.isArray(message)
      || Object.getPrototypeOf(message) !== Object.prototype) {
    return false;
  }
  const actualKeys = Object.keys(message).sort();
  const sortedExpectedKeys = [...expectedKeys].sort();
  return actualKeys.length === sortedExpectedKeys.length
    && actualKeys.every((key, index) => key === sortedExpectedKeys[index]);
}
