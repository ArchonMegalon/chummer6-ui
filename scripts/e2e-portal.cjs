#!/usr/bin/env node
'use strict';

const baseUrl = (process.env.CHUMMER_PORTAL_BASE_URL || 'http://chummer-portal:8080').replace(/\/$/, '');
const requiredLandingLinks = [
  '/downloads',
  '/help',
  '/contact'
];
const expectedProofRequiredInstallerRoutes = [
  '/downloads/install/avalonia-linux-x64-installer',
  '/downloads/install/avalonia-win-x64-installer',
  '/downloads/install/blazor-desktop-linux-x64-installer',
  '/downloads/install/blazor-desktop-win-x64-installer'
];

function hasIsolationHeaders(response) {
  return response.headers.get('cross-origin-opener-policy') === 'same-origin'
    && response.headers.get('cross-origin-embedder-policy') === 'require-corp';
}

function hasPortalChrome(text) {
  return text.includes('--portal-gold: #ffd46f')
    && text.includes('--portal-mint: #8ff0bc')
    && text.includes('body::before')
    && text.includes('background-size: 4.25rem 4.25rem')
    && text.includes('@keyframes portal-surface-reveal')
    && text.includes('animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both');
}

function normalizePlatformToken(download) {
  return String(download?.platformId || download?.platform || '').trim().toLowerCase();
}

function normalizeKindToken(download) {
  return String(download?.kind || download?.format || '').trim().toLowerCase();
}

function expectsDirectPublicInstallRedirect(download) {
  const installAccessClass = String(download?.installAccessClass || '').trim().toLowerCase();
  const platform = normalizePlatformToken(download);
  const kind = normalizeKindToken(download);
  const isDesktopPublicInstaller = installAccessClass === 'open_public'
    && (platform.includes('windows') || platform.includes('linux') || platform.startsWith('win-') || platform.startsWith('linux-'))
    && (kind === 'installer' || kind === 'msix' || kind === 'deb');
  return isDesktopPublicInstaller;
}

let observedReleaseManifest = null;

const checks = [
  {
    url: `${baseUrl}/`,
    assert: text =>
      text.includes('Explore Chummer Online, downloads, and support from one self-hosted edge.') &&
      text.includes('Start in the Character Roster, continue into Chummer Online') &&
      text.includes('data-portal-home-action="explore-chummer-online"') &&
      text.includes('/app?command=character_roster') &&
      text.includes('aria-label="Chummer Online routes"') &&
      text.includes('data-portal-home-route="chummer-app-roster"') &&
      text.includes('data-portal-home-route="chummer-app"') &&
      text.includes('data-portal-home-route="chummer-home"') &&
      text.includes('data-portal-home-route="downloads"') &&
      text.includes('Open Character Roster') &&
      text.includes('Open Chummer Online') &&
      text.includes('Open Chummer Online overview') &&
      text.includes('Get desktop client') &&
      requiredLandingLinks.every(link => text.includes(link)) &&
      hasPortalChrome(text)
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
    assert: text =>
      text.includes('Browser preview is not ready right now.')
      && text.includes('The downloadable Chummer client is the current stable path.')
      && text.includes('href="/downloads"')
      && text.includes('href="/status"')
  },
  {
    url: `${baseUrl}/app`,
    assert: (text, response) =>
      (/\/app\/?$/.test(response.url) || /\/blazor\/app\/?$/.test(response.url))
      && /<base href="[^"]*\/blazor\/"/i.test(text)
      && text.includes('data-route-family="app"')
  },
  {
    url: `${baseUrl}/online?command=character_roster`,
    assert: (text, response) =>
      /\/blazor\/app\/?\?command=character_roster$/.test(response.url)
      && /<base href="[^"]*\/blazor\/"/i.test(text)
  },
  {
    url: `${baseUrl}/blazor/app`,
    assert: text => /<base href="[^"]*\/blazor\/"/i.test(text)
  },
  {
    url: `${baseUrl}/blazor/app?command=new_character`,
    assert: text =>
      /<base href="[^"]*\/blazor\/"/i.test(text)
      && text.includes('data-route-family="app"')
      && text.includes('data-active-workflow="build-lab"')
      && text.includes('data-command="new-character"')
      && text.includes('data-chummer-app-startup-command="new_character"')
      && text.includes('data-app-route-shared-shell="true"')
      && text.includes('Build Lab shell')
      && !text.includes('Your runners will appear here.')
  },
  {
    url: `${baseUrl}/blazor/home`,
    assert: text =>
      /<base href="[^"]*\/blazor\/"/i.test(text)
      && text.includes('Chummer Online for real dossier work.')
  },
  {
    url: `${baseUrl}/blazor/workbench`,
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
      return payload?.head === 'hub-web' && payload?.pathBase === '/hub' && payload?.status === 'ok';
    }
  },
  {
    url: `${baseUrl}/hub/`,
    assert: text =>
      /<base href="[^"]*\/hub\/"/i.test(text)
      && (text.includes('ChummerHub Web') || text.includes('Chummer Hub Web'))
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
    url: `${baseUrl}/openapi/v1.json`,
    assert: text => {
      const payload = JSON.parse(text);
      return typeof payload?.openapi === 'string'
        && payload.openapi.length > 0
        && typeof payload?.paths?.['/help'] === 'object'
        && typeof payload?.paths?.['/app'] === 'object'
        && typeof payload?.paths?.['/online'] === 'object'
        && typeof payload?.paths?.['/contact'] === 'object'
        && typeof payload?.paths?.['/status'] === 'object'
        && typeof payload?.paths?.['/blazor/app'] === 'object'
        && typeof payload?.paths?.['/blazor/home'] === 'object'
        && typeof payload?.paths?.['/blazor/'] === 'object'
        && typeof payload?.paths?.['/downloads/'] === 'object'
        && typeof payload?.paths?.['/downloads/releases.json'] === 'object'
        && typeof payload?.paths?.['/downloads/install/{artifactId}'] === 'object'
        && typeof payload?.paths?.['/api/health'] === 'object'
        && typeof payload?.paths?.['/api/tools/master-index'] === 'object';
    }
  },
  {
    url: `${baseUrl}/docs/`,
    assert: text =>
      text.includes('Self-hosted OpenAPI explorer') &&
      text.includes('/docs/docs.js') &&
      text.includes('data-docs-panel="operator-openapi-explorer"') &&
      text.includes('data-docs-shortcuts="operator-recovery"') &&
      text.includes('aria-describedby="docs-shortcuts-description"') &&
      text.includes('data-docs-shortcuts-description') &&
      text.includes('data-docs-summary="openapi-load-state"') &&
      text.includes('role="status"') &&
      text.includes('aria-live="polite"') &&
      text.includes('data-docs-endpoints="openapi-route-list"') &&
      text.includes('role="list"') &&
      text.includes('aria-label="Documented portal routes"') &&
      text.includes('data-docs-action="open-chummer-app"') &&
      text.includes('/app?command=character_roster') &&
      text.includes('data-docs-action="open-chummer-home"') &&
      text.includes('data-docs-action="open-downloads"') &&
      text.includes('data-docs-action="open-status"') &&
      text.includes('data-docs-action="open-help"') &&
      text.includes('data-docs-action="open-contact"') &&
      text.includes('data-docs-action="open-openapi-json"') &&
      hasPortalChrome(text) &&
      !text.toLowerCase().includes('jsdelivr')
  },
  {
    url: `${baseUrl}/docs/docs.js`,
    assert: text =>
      text.includes('data-docs-endpoint-card') &&
      text.includes('data-docs-endpoint-route') &&
      text.includes('data-docs-endpoint-family') &&
      text.includes('data-docs-endpoint-methods') &&
      text.includes('data-docs-endpoint-summary') &&
      text.includes('role="listitem"') &&
      text.includes('data-openapi-download-route="true"') &&
      text.includes('data-openapi-installer-handoff-route="true"') &&
      text.includes('data-openapi-release-status-route="true"') &&
      text.includes('data-openapi-support-handoff-route="true"') &&
      text.includes('data-openapi-help-handoff-route="true"') &&
      text.includes('data-openapi-chummer-app-route="true"') &&
      text.includes('data-openapi-chummer-home-route="true"') &&
      text.includes('data-openapi-blazor-entry-route="true"') &&
      text.includes('Chummer Online') &&
      text.includes('Chummer Online overview') &&
      text.includes('Hosted browser entry') &&
      text.includes('endpoint-summary') &&
      text.includes('escapeHtml') &&
      text.includes('/downloads/install/{artifactId}')
  },
  {
    url: `${baseUrl}/help`,
    assert: text =>
      text.includes('data-portal-help-panel="handoff-guide"') &&
      text.includes('data-portal-help-context=') &&
      text.includes('aria-label="Help recovery actions"') &&
      text.includes('data-portal-help-action="open-chummer-app"') &&
      text.includes('/app?command=character_roster') &&
      text.includes('data-portal-help-action="open-downloads"') &&
      text.includes('data-portal-help-action="open-status"') &&
      text.includes('data-portal-help-action="open-discord"') &&
      text.includes('data-portal-help-action="open-contact"') &&
      text.includes('data-portal-help-action="open-docs"') &&
      hasPortalChrome(text) &&
      text.includes('data-portal-help-boundary="source-guidance-only"')
  },
  {
    url: `${baseUrl}/downloads/releases.json`,
    assert: text => {
      const payload = JSON.parse(text);
      observedReleaseManifest = payload;
      return typeof payload?.version === 'string'
        && typeof payload?.status === 'string'
        && typeof payload?.source === 'string'
        && Array.isArray(payload?.downloads);
    }
  },
  {
    url: `${baseUrl}/status`,
    assert: text =>
      text.includes('data-portal-status-panel="release-availability"') &&
      text.includes('aria-label="Status recovery actions"') &&
      text.includes('data-portal-status-availability=') &&
      text.includes('data-portal-status-release-status=') &&
      text.includes('data-portal-status-version=') &&
      text.includes('data-portal-status-artifact-count=') &&
      text.includes('data-portal-status-install-route-count=') &&
      text.includes('data-portal-status-boundary="source-manifest-backed"') &&
      text.includes('data-portal-status-action="open-downloads"') &&
      text.includes('data-portal-status-action="open-help"') &&
      text.includes('data-portal-status-action="open-discord"') &&
      text.includes('data-portal-status-action="open-chummer-app"') &&
      text.includes('/app?command=character_roster') &&
      hasPortalChrome(text)
  },
  {
    url: `${baseUrl}/downloads/`,
    assert: text =>
      text.includes('data-download-panel="desktop-downloads"') &&
      text.includes('desktop-downloads-title') &&
      text.includes('aria-labelledby="desktop-downloads-title"') &&
      text.includes('aria-describedby="fallback-link"') &&
      text.includes('data-download-action="open-chummer-app"') &&
      text.includes('data-download-action="open-status"') &&
      text.includes('data-download-action="open-help"') &&
      hasPortalChrome(text) &&
      text.includes('/app?command=character_roster') &&
      text.includes('/downloads/releases.json') &&
      text.includes('data-download-manifest-link') &&
      text.includes('fallback-link')
      && text.includes('data-download-fallback-guidance') &&
      text.includes('data-download-action="download-artifact"') &&
      text.includes('data-download-status=') &&
      text.includes('data-download-version=') &&
      text.includes('data-download-artifact-summary=') &&
      text.includes('data-download-install-route=') &&
      text.includes('data-download-raw-url=') &&
      text.includes('data-download-dispatch-url=') &&
      text.includes('data-download-link-mode="self-host-dispatch"') &&
      text.includes('data-download-platform=') &&
      text.includes('data-download-platform-label') &&
      text.includes('data-download-description') &&
      text.includes('aria-describedby="published-download-description"') &&
      text.includes('data-install-route-public-route=') &&
      text.includes('data-install-route-link-mode="proof-required"') &&
      text.includes('data-install-route-action="open-proof-required-route"') &&
      text.includes('data-install-route-posture-label') &&
      text.includes('data-install-route-promotion-label') &&
      text.includes('data-install-route-artifact-label') &&
      text.includes('compatibility-handoff-description') &&
      text.includes('data-install-route-description') &&
      text.includes('aria-describedby="compatibility-handoff-description"') &&
      text.includes('data-self-host-downloads-panel="docker-operator"') &&
      text.includes('self-host-downloads-title') &&
      text.includes('data-self-host-docker-command="docker compose --profile portal up -d"') &&
      text.includes('data-self-host-release-manifest=') &&
      text.includes('data-self-host-browser-app=') &&
      text.includes('data-self-host-installer-boundary="proof-required"') &&
      text.includes('data-download-list="published-artifacts"')
  },
  {
    url: `${baseUrl}/downloads/?next=%2Fdownloads%2Finstall%2Fblazor-desktop-linux-x64-installer&installState=proof_required`,
    assert: text =>
      text.includes('data-install-state="proof_required"')
      && text.includes('data-install-next-route=')
      && text.includes('/downloads/install/blazor-desktop-linux-x64-installer')
      && text.includes('role="status"')
      && text.includes('aria-live="polite"')
      && text.includes('data-install-state-action="open-browser-app"')
      && text.includes('/app?command=character_roster')
  },
  {
    url: `${baseUrl}/contact`,
    assert: text =>
      text.includes('data-portal-contact-panel="support-handoff"')
      && text.includes('data-portal-contact-context=')
      && text.includes('data-portal-contact-public-route=')
      && text.includes('aria-label="Contact recovery actions"')
      && text.includes('/app?command=character_roster')
      && text.includes('data-portal-contact-action="open-discord"')
      && text.includes('data-portal-contact-action="open-status"')
      && text.includes('data-portal-contact-action="open-downloads"')
      && text.includes('data-portal-contact-action="open-help"')
      && text.includes('data-portal-contact-action="open-chummer-app"')
      && hasPortalChrome(text)
  }
];

async function validateManifestInstallHandoffs(manifest) {
  const downloads = Array.isArray(manifest?.downloads)
    ? manifest.downloads.filter(download => typeof download?.id === 'string' && download.id.length > 0)
    : [];

  for (const download of downloads) {
    const route = `${baseUrl}/downloads/install/${encodeURIComponent(download.id)}`;
    const response = await fetch(route, { redirect: 'manual' });
    const _body = await response.text();
    const location = response.headers.get('location') || '';
    const decodedLocation = decodeURIComponent(location);
    const acceptedStatuses = [301, 302, 303, 307, 308];

    if (!acceptedStatuses.includes(response.status)) {
      throw new Error(`Portal check failed: ${route} -> HTTP ${response.status}`);
    }

    const installAccessClass = String(download.installAccessClass || '').toLowerCase();
    const expectedRoute = `/downloads/install/${download.id}`;
    const expectedDirectDownloadRoute = `/downloads/get/${download.id}`;
    const passed = expectsDirectPublicInstallRedirect(download)
      ? decodedLocation === expectedDirectDownloadRoute || decodedLocation.endsWith(expectedDirectDownloadRoute)
      : installAccessClass === 'open_public'
        ? location.length > 0
            && !decodedLocation.includes('/login?next=')
            && !decodedLocation.includes('installState=proof_required')
        : decodedLocation.includes(expectedRoute) && decodedLocation.includes('installState=');

    if (!passed) {
      throw new Error(`Portal check failed: ${route} -> unexpected redirect ${location || '<empty>'}`);
    }

    console.log(`ok: ${route}`);
  }
}

(async () => {
  for (const check of checks) {
    const response = await fetch(check.url, {
      method: check.method ?? 'GET',
      headers: check.headers,
      body: check.body,
      redirect: check.redirect
    });
    const body = await response.text();
    const acceptedStatuses = check.acceptedStatuses || [];
    const statusAccepted = response.ok || acceptedStatuses.includes(response.status);
    if (!statusAccepted) {
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

  await validateManifestInstallHandoffs(observedReleaseManifest);

  console.log('portal E2E completed');
})().catch(error => {
  console.error(error.message);
  process.exit(1);
});
