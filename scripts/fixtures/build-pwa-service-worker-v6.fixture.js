// Frozen predecessor fixture captured before the revision-addressed v7 worker.
// It intentionally models the v6 cache ownership/install/fetch behavior and
// must not be rewritten from the current worker during tests.
const CHUMMER_PWA_CACHE = 'chummer-build-static-v6';
const SCOPE = new URL(self.registration.scope);
const STATIC_PATHS = [
  'offline.html',
  'app.css',
  'build-pwa-install.css',
  'Chummer.Blazor.styles.css',
  'manifest.webmanifest',
  'js/build-pwa-recovery.js',
  'js/build-pwa-integrity.js',
  'js/build-pwa-install.js',
  'js/build-pwa-layout.js',
  'icons/chummer-build-180.png',
  'icons/chummer-build-192.png',
  'icons/chummer-build-512.png',
  'icons/chummer-build-maskable-512.png',
  'icons/chummer-pwa.svg',
  'icons/chummer-pwa-maskable.svg'
];
const STATIC_URLS = new Set(STATIC_PATHS.map(path => new URL(path, SCOPE).href));
const OFFLINE_URL = new URL('offline.html', SCOPE).href;

self.addEventListener('install', event => {
  event.waitUntil((async () => {
    const cache = await caches.open(CHUMMER_PWA_CACHE);
    for (const url of STATIC_URLS) {
      const request = new Request(url, { cache: 'reload' });
      const response = await fetch(request);
      if (!response || response.status !== 200) throw new Error(`v6 precache rejected ${url}`);
      await cache.put(request, response);
    }
  })());
});

self.addEventListener('activate', event => {
  // The captured predecessor was already passive: no skipWaiting/clients.claim.
  event.waitUntil(Promise.resolve());
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  if (event.request.mode === 'navigate') {
    event.respondWith(fetch(event.request).catch(async () => {
      const cache = await caches.open(CHUMMER_PWA_CACHE);
      return (await cache.match(OFFLINE_URL)) || Response.error();
    }));
    return;
  }
  if (!STATIC_URLS.has(event.request.url)) return;
  event.respondWith((async () => {
    const cache = await caches.open(CHUMMER_PWA_CACHE);
    return (await cache.match(event.request)) || fetch(event.request);
  })());
});
