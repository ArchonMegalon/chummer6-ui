const CHUMMER_PWA_CACHE = 'chummer-online-static-v1';
const STATIC_ASSETS = [
  './offline.html',
  './app.css',
  './Chummer.Blazor.styles.css',
  './manifest.webmanifest',
  './icons/chummer-pwa.svg',
  './icons/chummer-pwa-maskable.svg',
  './media/chummer6/chummer6-hero-baseline.png',
  './media/chummer6/karma-forge-baseline.png'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CHUMMER_PWA_CACHE)
      .then(cache => cache.addAll(STATIC_ASSETS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys
          .filter(key => key.startsWith('chummer-online-') && key !== CHUMMER_PWA_CACHE)
          .map(key => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => caches.match('./offline.html')));
    return;
  }

  if (!isStaticAssetRequest(url)) {
    return;
  }

  event.respondWith(
    caches.match(request).then(cached => {
      if (cached) {
        return cached;
      }

      return fetch(request).then(response => {
        if (!response || response.status !== 200 || response.type !== 'basic') {
          return response;
        }

        const copy = response.clone();
        caches.open(CHUMMER_PWA_CACHE).then(cache => cache.put(request, copy));
        return response;
      });
    })
  );
});

function isStaticAssetRequest(url) {
  if (url.origin !== self.location.origin || url.search) {
    return false;
  }

  const path = url.pathname.toLowerCase();
  if (path.includes('/api/') || path.includes('/workspaces/') || path.includes('/session/')) {
    return false;
  }

  return path.endsWith('/app.css')
    || path.endsWith('/chummer.blazor.styles.css')
    || path.endsWith('/manifest.webmanifest')
    || path.endsWith('/offline.html')
    || path.endsWith('/icons/chummer-pwa.svg')
    || path.endsWith('/icons/chummer-pwa-maskable.svg')
    || path.endsWith('/media/chummer6/chummer6-hero-baseline.png')
    || path.endsWith('/media/chummer6/karma-forge-baseline.png');
}
