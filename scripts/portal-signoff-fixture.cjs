#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');

function getArg(name, fallback) {
  const index = process.argv.indexOf(name);
  if (index >= 0 && process.argv[index + 1]) {
    return process.argv[index + 1];
  }

  return fallback;
}

function parsePort(rawPort) {
  const port = Number(rawPort);
  return Number.isInteger(port) && port > 0 ? port : null;
}

function resolvePort(options = {}) {
  return parsePort(options.port ?? process.env.CHUMMER_B7_RUNTIME_FIXTURE_PORT ?? '38091');
}

function loadAvaloniaFixtureAssets(options = {}) {
  const repoRoot = path.resolve(options.repoRoot || path.join(__dirname, '..'));
  const avaloniaIndexPath = path.join(repoRoot, 'Chummer.Avalonia.Browser', 'wwwroot', 'index.html');
  const avaloniaServiceWorkerPath = path.join(repoRoot, 'Chummer.Avalonia.Browser', 'wwwroot', 'service-worker.js');
  return {
    avaloniaIndex: fs.readFileSync(avaloniaIndexPath, 'utf8'),
    avaloniaServiceWorker: fs.readFileSync(avaloniaServiceWorkerPath, 'utf8'),
  };
}

const portalChromeCss = `
  <style>
    :root {
      --portal-gold: #ffd46f;
      --portal-mint: #8ff0bc;
    }

    body::before {
      content: "";
      background-size: 4.25rem 4.25rem;
    }

    @keyframes portal-surface-reveal {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .portal-card {
      animation: portal-surface-reveal .38s cubic-bezier(.2,.78,.2,1) both;
    }
  </style>
`;

function renderPage(title, body, extraHead = '') {
  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>${title}</title>
    ${portalChromeCss}
    ${extraHead}
  </head>
  <body>
    ${body}
  </body>
</html>`;
}

function renderLandingPage() {
  return renderPage(
    'Chummer Portal',
    `
    <main class="portal-card">
      <section data-homepage-section="hero">
        <h1>Download Chummer</h1>
        <p>A Shadowrun character manager for clean sheets and faster tables.</p>
        <p>Current public installers: Windows and Linux.</p>
        <a href="/downloads">Download Chummer</a>
        <a href="/help">Help</a>
        <a href="/contact">Contact</a>
        <button type="button">Watch the Chummer promo video</button>
        <span>Watch 90 sec</span>
        <video src="/media/promo/every-wonder-horizon-promo.mp4"></video>
      </section>
      <section aria-label="Example runners">
        <article>Kestrel</article>
        <article>Brick</article>
        <article>Whisper</article>
      </section>
    </main>`);
}

function renderBlazorNoticePage(extra = '') {
  return renderPage(
    'Chummer Online',
    `
    <main class="portal-card" data-route-family="app">
      <h1>Explore Chummer Online.</h1>
      <p>This is the Chummer Online entrypoint</p>
      <p>Browser preview is not ready right now.</p>
      <p>The downloadable Chummer client is the current stable path.</p>
      <a href="/downloads">Download Chummer</a>
      <a href="/status">Status</a>
      <div data-chummer-classic-shell="true">Classic shell</div>
      ${extra}
    </main>`,
    '<base href="/blazor/" />');
}

function renderBlazorHomePage() {
  return renderPage(
    'Chummer Online Home',
    `
    <main class="portal-card">
      <h1>Chummer Online for real dossier work.</h1>
      <a data-home-hero-action="explore-chummer-online" href="/app">Explore Chummer Online</a>
      <p>Roster entry: quick resume and directed startup flows.</p>
    </main>`,
    '<base href="/blazor/" />');
}

function renderHubPage() {
  return renderPage(
    'ChummerHub Web',
    '<main class="portal-card"><h1>ChummerHub Web</h1></main>',
    '<base href="/hub/" />');
}

function renderDocsPage() {
  return renderPage(
    'API docs',
    `
    <main class="portal-card" data-docs-panel="operator-openapi-explorer">
      <h1>Self-hosted OpenAPI explorer</h1>
      <p data-docs-shortcuts="operator-recovery" id="docs-shortcuts-description" data-docs-shortcuts-description>
        Recovery shortcuts for the current portal operator lane.
      </p>
      <section data-docs-summary="openapi-load-state" role="status" aria-live="polite">
        OpenAPI load state
      </section>
      <section data-docs-endpoints="openapi-route-list" role="list" aria-label="Documented portal routes"></section>
      <nav aria-describedby="docs-shortcuts-description">
        <a data-docs-action="open-chummer-app" href="/app?command=character_roster">Open Chummer App</a>
        <a data-docs-action="open-chummer-home" href="/blazor/home">Open Chummer Home</a>
        <a data-docs-action="open-downloads" href="/downloads/">Open downloads</a>
        <a data-docs-action="open-status" href="/status">Open status</a>
        <a data-docs-action="open-help" href="/help">Open help</a>
        <a data-docs-action="open-contact" href="/contact">Open contact</a>
        <a data-docs-action="open-openapi-json" href="/openapi/v1.json">Open OpenAPI JSON</a>
      </nav>
      <script src="/docs/docs.js"></script>
    </main>`);
}

function renderHelpPage() {
  return renderPage(
    'Help',
    `
    <main class="portal-card" data-portal-help-panel="handoff-guide" data-portal-help-context="fixture" data-portal-help-boundary="source-guidance-only">
      <h1>Help</h1>
      <nav aria-label="Help recovery actions">
        <a data-portal-help-action="open-chummer-app" href="/app?command=character_roster">Open Chummer App</a>
        <a data-portal-help-action="open-downloads" href="/downloads/">Open downloads</a>
        <a data-portal-help-action="open-status" href="/status">Open status</a>
        <a data-portal-help-action="open-contact" href="/contact">Open contact</a>
        <a data-portal-help-action="open-docs" href="/docs/">Open docs</a>
      </nav>
    </main>`);
}

function renderStatusPage() {
  return renderPage(
    'Status',
    `
    <main class="portal-card" data-portal-status-panel="release-availability" data-portal-status-availability="published" data-portal-status-release-status="published" data-portal-status-version="fixture-20260707" data-portal-status-artifact-count="2" data-portal-status-install-route-count="2" data-portal-status-boundary="source-manifest-backed">
      <h1>Status</h1>
      <nav aria-label="Status recovery actions">
        <a data-portal-status-action="open-downloads" href="/downloads/">Open downloads</a>
        <a data-portal-status-action="open-help" href="/help">Open help</a>
        <a data-portal-status-action="open-discord" href="https://discord.example.invalid/chummer">Open Discord</a>
        <a data-portal-status-action="open-chummer-app" href="/app?command=character_roster">Open Chummer App</a>
      </nav>
    </main>`);
}

function renderContactPage() {
  return renderPage(
    'Contact',
    `
    <main class="portal-card" data-portal-contact-panel="support-handoff" data-portal-contact-context="fixture" data-portal-contact-public-route="/contact">
      <h1>Contact</h1>
      <nav aria-label="Contact recovery actions">
        <a data-portal-contact-action="open-discord" href="https://discord.example.invalid/chummer">Open Discord</a>
        <a data-portal-contact-action="open-status" href="/status">Open status</a>
        <a data-portal-contact-action="open-downloads" href="/downloads/">Open downloads</a>
        <a data-portal-contact-action="open-help" href="/help">Open help</a>
        <a data-portal-contact-action="open-chummer-app" href="/app?command=character_roster">Open Chummer App</a>
      </nav>
    </main>`);
}

function renderDownloadsPage(url) {
  const nextRoute = url.searchParams.get('next') || '';
  const installState = url.searchParams.get('installState') || '';
  const installStatePanel = installState
    ? `
      <section data-install-state="${installState}" data-install-next-route="${nextRoute}" role="status" aria-live="polite">
        <a data-install-state-action="open-browser-app" href="/app?command=character_roster">Open browser app</a>
      </section>`
    : '';

  return renderPage(
    'Desktop Downloads',
    `
    <main class="portal-card" data-download-panel="desktop-downloads">
      <h1 id="desktop-downloads-title">Desktop Downloads</h1>
      ${installStatePanel}
      <p id="published-download-description" data-download-description>Published desktop downloads for the current portal lane.</p>
      <p id="compatibility-handoff-description" data-install-route-description>Compatibility handoff description.</p>
      <p id="fallback-link" data-download-fallback-guidance>Fallback guidance for proof-required install routes.</p>
      <a data-download-action="open-chummer-app" href="/app?command=character_roster">Open Chummer App</a>
      <a data-download-action="open-status" href="/status">Open status</a>
      <a data-download-action="open-help" href="/help">Open help</a>
      <a data-download-manifest-link href="/downloads/releases.json">Manifest</a>
      <a class="fallback-link" id="fallback-link-anchor" href="/downloads/releases.json">fallback-link</a>
      <section data-download-list="published-artifacts" aria-labelledby="desktop-downloads-title" aria-describedby="fallback-link">
        <article
          data-download-action="download-artifact"
          data-download-status="published"
          data-download-version="fixture-20260707"
          data-download-artifact-summary="Avalonia Windows installer"
          data-download-install-route="/downloads/install/avalonia-win-x64-installer"
          data-download-raw-url="/downloads/files/avalonia-win-x64-installer.exe"
          data-download-dispatch-url="/downloads/get/avalonia-win-x64-installer"
          data-download-link-mode="self-host-dispatch"
          data-download-platform="windows"
          data-download-platform-label="Windows"
          aria-describedby="published-download-description">
          <span data-download-platform-label>Windows</span>
        </article>
        <article
          data-download-action="download-artifact"
          data-download-status="published"
          data-download-version="fixture-20260707"
          data-download-artifact-summary="Avalonia Linux installer"
          data-download-install-route="/downloads/install/avalonia-linux-x64-installer"
          data-download-raw-url="/downloads/files/avalonia-linux-x64-installer.deb"
          data-download-dispatch-url="/downloads/get/avalonia-linux-x64-installer"
          data-download-link-mode="self-host-dispatch"
          data-download-platform="linux"
          data-download-platform-label="Linux"
          aria-describedby="published-download-description">
          <span data-download-platform-label>Linux</span>
        </article>
      </section>
      <section
        data-install-route-public-route="/downloads/install/avalonia-win-x64-installer"
        data-install-route-link-mode="proof-required"
        data-install-route-action="open-proof-required-route"
        data-install-route-posture-label="Proof required"
        data-install-route-promotion-label="Public"
        data-install-route-artifact-label="Windows installer"
        aria-describedby="compatibility-handoff-description">
        <p>compatibility-handoff-description</p>
      </section>
      <section
        data-self-host-downloads-panel="docker-operator"
        data-self-host-docker-command="docker compose --profile portal up -d"
        data-self-host-release-manifest="/downloads/releases.json"
        data-self-host-browser-app="/app?command=character_roster"
        data-self-host-installer-boundary="proof-required">
        <h2 id="self-host-downloads-title">self-host-downloads-title</h2>
      </section>
    </main>`);
}

const docsScript = `const endpointTemplate = {
  card: 'data-docs-endpoint-card',
  route: 'data-docs-endpoint-route',
  family: 'data-docs-endpoint-family',
  methods: 'data-docs-endpoint-methods',
  summary: 'data-docs-endpoint-summary',
  download: 'data-openapi-download-route="true"',
  installer: 'data-openapi-installer-handoff-route="true"',
  release: 'data-openapi-release-status-route="true"',
  support: 'data-openapi-support-handoff-route="true"',
  help: 'data-openapi-help-handoff-route="true"',
  app: 'data-openapi-chummer-app-route="true"',
  home: 'data-openapi-chummer-home-route="true"',
  blazor: 'data-openapi-blazor-entry-route="true"'
};
const docsLabels = [
  'Chummer Online',
  'Chummer Online overview',
  'Hosted browser entry',
  'endpoint-summary',
  'role="listitem"',
  '/downloads/install/{artifactId}'
];
function escapeHtml(value) {
  return String(value);
}
`;

const downloadsManifest = {
  version: 'fixture-20260707',
  status: 'published',
  source: 'portal-signoff-fixture',
  downloads: [
    {
      id: 'avalonia-win-x64-installer',
      artifactId: 'avalonia-win-x64-installer',
      head: 'avalonia',
      platformId: 'windows',
      kind: 'installer',
      installAccessClass: 'open_public'
    },
    {
      id: 'avalonia-linux-x64-installer',
      artifactId: 'avalonia-linux-x64-installer',
      head: 'avalonia',
      platformId: 'linux',
      kind: 'installer',
      installAccessClass: 'open_public'
    }
  ]
};

function writeJson(response, payload, headers = {}) {
  response.writeHead(200, {
    'content-type': 'application/json; charset=utf-8',
    ...headers
  });
  response.end(JSON.stringify(payload));
}

function writeHtml(response, body, headers = {}) {
  response.writeHead(200, {
    'content-type': 'text/html; charset=utf-8',
    ...headers
  });
  response.end(body);
}

function writeJs(response, body, headers = {}) {
  response.writeHead(200, {
    'content-type': 'application/javascript; charset=utf-8',
    ...headers
  });
  response.end(body);
}

function withIsolationHeaders(headers = {}) {
  return {
    'cross-origin-opener-policy': 'same-origin',
    'cross-origin-embedder-policy': 'require-corp',
    ...headers
  };
}

function createFixtureResponder(options = {}) {
  const effectivePort = resolvePort(options);
  if (effectivePort === null) {
    throw new Error('Invalid port for portal signoff fixture.');
  }
  const defaultHost = options.host || `127.0.0.1:${effectivePort}`;
  const { avaloniaIndex, avaloniaServiceWorker } = loadAvaloniaFixtureAssets(options);
  return function fixtureResponder(request, response) {
    const url = new URL(request.url || '/', `http://${request.headers.host || defaultHost}`);
    const pathname = url.pathname;

    if (pathname === '/online' && url.searchParams.get('command') === 'character_roster') {
      response.writeHead(302, { location: '/blazor/app?command=character_roster' });
      response.end();
      return;
    }

    if (pathname.startsWith('/downloads/install/')) {
      const artifactId = pathname.slice('/downloads/install/'.length);
      response.writeHead(302, { location: `/downloads/get/${artifactId}` });
      response.end();
      return;
    }

    if (pathname.startsWith('/downloads/get/')) {
      writeHtml(response, renderDownloadsPage(url));
      return;
    }

    if (request.method === 'POST' && pathname === '/blazor/_blazor/negotiate') {
      writeJson(response, {
        connectionId: 'fixture-connection-id',
        negotiateVersion: 1
      });
      return;
    }

    switch (pathname) {
      case '/':
        writeHtml(response, renderLandingPage());
        return;
      case '/blazor/':
        writeHtml(response, renderBlazorNoticePage());
        return;
      case '/blazor/deep-link-check':
        writeHtml(response, renderBlazorNoticePage('<div>deep-link-check</div>'));
        return;
      case '/blazor/health':
        writeJson(response, { pathBase: '/blazor', ok: true });
        return;
      case '/app':
      case '/blazor/app':
      case '/blazor/workbench':
        writeHtml(
          response,
          renderBlazorNoticePage(
            url.searchParams.get('command') === 'character_roster'
              ? '<div data-command="character-roster">Character Roster</div>'
              : ''));
        return;
      case '/blazor/home':
        writeHtml(response, renderBlazorHomePage());
        return;
      case '/hub/':
        writeHtml(response, renderHubPage());
        return;
      case '/hub/health':
        writeJson(response, { head: 'hub-web', pathBase: '/hub', status: 'ok' });
        return;
      case '/avalonia/':
      case '/avalonia/deep-link-signoff':
        writeHtml(response, avaloniaIndex, withIsolationHeaders());
        return;
      case '/avalonia/service-worker.js':
        writeJs(response, avaloniaServiceWorker, withIsolationHeaders());
        return;
      case '/avalonia/health':
        writeJson(
          response,
          {
            head: 'avalonia-browser',
            pathBase: '/avalonia',
            ok: true,
            isolation: {
              crossOriginOpenerPolicy: 'same-origin',
              crossOriginEmbedderPolicy: 'require-corp',
              requiresCrossOriginIsolation: true
            },
            staticAssets: {
              wasmMimeType: 'application/wasm'
            }
          },
          withIsolationHeaders());
        return;
      case '/api/health':
        writeJson(response, { ok: true, source: 'portal-signoff-fixture' });
        return;
      case '/api/tools/master-index':
        writeJson(response, {
          ok: true,
          tools: []
        });
        return;
      case '/api/ai/status':
        writeJson(response, {
          status: 'scaffolded',
          routes: ['coach', 'spider', 'director'],
          providers: ['fixture']
        });
        return;
      case '/openapi/v1.json':
        writeJson(response, {
          openapi: '3.1.0',
          info: {
            title: 'Fixture API',
            version: 'v1'
          },
          paths: {
            '/help': {},
            '/app': {},
            '/online': {},
            '/contact': {},
            '/status': {},
            '/blazor/app': {},
            '/blazor/home': {},
            '/blazor/': {},
            '/downloads/': {},
            '/downloads/releases.json': {},
            '/downloads/install/{artifactId}': {},
            '/api/health': {},
            '/api/tools/master-index': {}
          }
        });
        return;
      case '/docs/':
        writeHtml(response, renderDocsPage());
        return;
      case '/docs/docs.js':
        writeJs(response, docsScript);
        return;
      case '/help':
        writeHtml(response, renderHelpPage());
        return;
      case '/downloads/':
        writeHtml(response, renderDownloadsPage(url));
        return;
      case '/downloads/releases.json':
        writeJson(response, downloadsManifest);
        return;
      case '/status':
        writeHtml(response, renderStatusPage());
        return;
      case '/contact':
        writeHtml(response, renderContactPage());
        return;
      default:
        response.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
        response.end('not found');
    }
  };
}

function startFixtureServer(options = {}) {
  const effectivePort = resolvePort(options);
  if (effectivePort === null) {
    throw new Error('Invalid port for portal signoff fixture.');
  }
  const host = options.host || '127.0.0.1';
  const responder = createFixtureResponder({ port: effectivePort, host: `${host}:${effectivePort}` });
  const server = http.createServer(responder);
  server.listen(effectivePort, host, () => {
    console.log(`portal-signoff-fixture listening on http://${host}:${effectivePort}`);
  });
  return server;
}

module.exports = {
  createFixtureResponder,
  startFixtureServer
};

if (require.main === module) {
  const port = parsePort(getArg('--port', process.env.CHUMMER_B7_RUNTIME_FIXTURE_PORT || '38091'));
  if (port === null) {
    console.error('Invalid port for portal signoff fixture.');
    process.exit(2);
  }
  const server = startFixtureServer({ port });
  for (const signal of ['SIGINT', 'SIGTERM']) {
    process.on(signal, () => {
      server.close(() => process.exit(0));
    });
  }
}
