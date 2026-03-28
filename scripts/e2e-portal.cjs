#!/usr/bin/env node
'use strict';

const baseUrl = (process.env.CHUMMER_PORTAL_BASE_URL || 'http://chummer-portal:8080').replace(/\/$/, '');

const requiredLandingLinks = [
  '/blazor/',
  '/hub/',
  '/session/',
  '/coach/',
  '/avalonia/',
  '/downloads/',
  '/docs/',
  '/api/health'
];

function hasIsolationHeaders(response) {
  return response.headers.get('cross-origin-opener-policy') === 'same-origin'
    && response.headers.get('cross-origin-embedder-policy') === 'require-corp';
}

const checks = [
  {
    url: `${baseUrl}/`,
    assert: text =>
      text.includes('Chummer Portal') &&
      requiredLandingLinks.every(link => text.includes(link))
  },
  {
    url: `${baseUrl}/blazor/health`,
    assert: text => {
      const payload = JSON.parse(text);
      return payload?.pathBase === '/blazor' && payload?.ok === true;
    }
  },
  {
    url: `${baseUrl}/blazor/`,
    assert: text => /<base href="[^"]*\/blazor\/"/i.test(text)
  },
  {
    url: `${baseUrl}/blazor/deep-link-check`,
    assert: text => /<base href="[^"]*\/blazor\/"/i.test(text)
  },
  {
    url: `${baseUrl}/hub/health`,
    assert: text => {
      const payload = JSON.parse(text);
      return payload?.head === 'hub-web' && payload?.pathBase === '/hub' && payload?.ok === true;
    }
  },
  {
    url: `${baseUrl}/hub/`,
    assert: text => /<base href="[^"]*\/hub\/"/i.test(text) && text.includes('ChummerHub Web')
  },
  {
    url: `${baseUrl}/avalonia/`,
    assert: (text, response) =>
      text.includes('Avalonia Browser Host')
      && text.includes('Degraded browser mode')
      && text.includes('Service worker')
      && hasIsolationHeaders(response)
  },
  {
    url: `${baseUrl}/avalonia/deep-link-signoff`,
    assert: (text, response) =>
      text.includes('Avalonia Browser Host')
      && text.includes('/avalonia/')
      && hasIsolationHeaders(response)
  },
  {
    url: `${baseUrl}/avalonia/service-worker.js`,
    assert: (text, response) =>
      response.headers.get('content-type')?.includes('javascript')
      && text.includes('chummer-avalonia-browser-host-v')
      && text.includes('caches.open')
      && text.includes('caches.keys')
      && text.includes('caches.delete')
      && text.includes('caches.match("./index.html")')
  },
  {
    url: `${baseUrl}/avalonia/health`,
    assert: (text, response) => {
      const payload = JSON.parse(text);
      return payload?.head === 'avalonia-browser'
        && payload?.pathBase === '/avalonia'
        && payload?.ok === true
        && payload?.isolation?.crossOriginOpenerPolicy === 'same-origin'
        && payload?.isolation?.crossOriginEmbedderPolicy === 'require-corp'
        && payload?.isolation?.requiresCrossOriginIsolation === true
        && payload?.staticAssets?.wasmMimeType === 'application/wasm'
        && hasIsolationHeaders(response);
    }
  },
  {
    method: 'POST',
    url: `${baseUrl}/blazor/_blazor/negotiate?negotiateVersion=1`,
    headers: {
      'Content-Type': 'text/plain;charset=UTF-8'
    },
    body: '',
    assert: text => {
      const payload = JSON.parse(text);
      return typeof payload?.connectionId === 'string' && payload.connectionId.length > 0;
    }
  },
  {
    url: `${baseUrl}/api/health`,
    assert: text => {
      const payload = JSON.parse(text);
      return payload?.ok === true;
    }
  },
  {
    url: `${baseUrl}/api/tools/master-index`,
    assert: text => !text.includes('missing_or_invalid_api_key')
  },
  {
    url: `${baseUrl}/api/ai/status`,
    assert: text => {
      const payload = JSON.parse(text);
      return payload?.status === 'scaffolded'
        && Array.isArray(payload?.routes)
        && payload.routes.includes('coach')
        && Array.isArray(payload?.providers)
        && !text.includes('missing_or_invalid_api_key');
    }
  },
  {
    url: `${baseUrl}/openapi/v1.json`,
    assert: text => {
      const payload = JSON.parse(text);
      return typeof payload?.openapi === 'string' && payload.openapi.length > 0;
    }
  },
  {
    url: `${baseUrl}/docs/`,
    assert: text =>
      text.includes('Self-hosted OpenAPI explorer') &&
      text.includes('/docs/docs.js') &&
      !text.toLowerCase().includes('jsdelivr')
  },
  {
    url: `${baseUrl}/downloads/releases.json`,
    assert: text => {
      const payload = JSON.parse(text);
      return typeof payload?.version === 'string'
        && typeof payload?.status === 'string'
        && typeof payload?.source === 'string'
        && Array.isArray(payload?.downloads);
    }
  },
  {
    url: `${baseUrl}/downloads/`,
    assert: text =>
      text.includes('Desktop Downloads') &&
      text.includes('/downloads/releases.json') &&
      text.includes('No published desktop builds yet') &&
      text.includes('fallback-link')
  }
];

(async () => {
  for (const check of checks) {
    const response = await fetch(check.url, {
      method: check.method ?? 'GET',
      headers: check.headers,
      body: check.body
    });
    const body = await response.text();
    if (!response.ok) {
      throw new Error(`Portal check failed: ${check.url} -> HTTP ${response.status}`);
    }

    let passed = false;
    try {
      passed = Boolean(check.assert(body, response));
    } catch (error) {
      throw new Error(`Portal check failed: ${check.url} -> assertion threw: ${error.message}`);
    }

    if (!passed) {
      throw new Error(`Portal check failed: ${check.url} -> assertion returned false`);
    }

    console.log(`ok: ${check.url}`);
  }

  console.log('portal E2E completed');
})().catch(error => {
  console.error(error.message);
  process.exit(1);
});
