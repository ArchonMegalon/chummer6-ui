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

const port = Number(getArg('--port', process.env.CHUMMER_B7_RUNTIME_FIXTURE_PORT || '38091'));
if (!Number.isInteger(port) || port <= 0) {
  console.error('Invalid port for portal signoff fixture.');
  process.exit(2);
}

const repoRoot = path.resolve(__dirname, '..');
const avaloniaIndexPath = path.join(repoRoot, 'Chummer.Avalonia.Browser', 'wwwroot', 'index.html');
const avaloniaServiceWorkerPath = path.join(repoRoot, 'Chummer.Avalonia.Browser', 'wwwroot', 'service-worker.js');

const avaloniaIndex = fs.readFileSync(avaloniaIndexPath, 'utf8');
const avaloniaServiceWorker = fs.readFileSync(avaloniaServiceWorkerPath, 'utf8');

const landingPage = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>Chummer Portal</title>
  </head>
  <body>
    <h1>Chummer Portal</h1>
    <nav>
      <a href="/blazor/">Blazor</a>
      <a href="/hub/">Hub</a>
      <a href="/session/">Session</a>
      <a href="/coach/">Coach</a>
      <a href="/avalonia/">Avalonia</a>
      <a href="/downloads/">Downloads</a>
      <a href="/docs/">Docs</a>
      <a href="/api/health">API health</a>
    </nav>
  </body>
</html>`;

const blazorPage = `<!doctype html>
<html>
  <head>
    <base href="/blazor/" />
    <title>Chummer Blazor</title>
  </head>
  <body>Blazor shell</body>
</html>`;

const hubPage = `<!doctype html>
<html>
  <head>
    <base href="/hub/" />
    <title>ChummerHub Web</title>
  </head>
  <body>ChummerHub Web</body>
</html>`;

const docsPage = `<!doctype html>
<html>
  <head>
    <title>API docs</title>
  </head>
  <body>
    <h1>Self-hosted OpenAPI explorer</h1>
    <script src="/docs/docs.js"></script>
  </body>
</html>`;

const downloadsPage = `<!doctype html>
<html>
  <head>
    <title>Desktop Downloads</title>
  </head>
  <body>
    <h1>Desktop Downloads</h1>
    <p data-source="/downloads/releases.json">No published desktop builds yet</p>
    <a class="fallback-link" href="/downloads/releases.json">fallback-link</a>
  </body>
</html>`;

const downloadsManifest = {
  version: 'unpublished',
  status: 'staging',
  source: 'portal-signoff-fixture',
  downloads: []
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
  const effectivePort = Number(options.port || port);
  const defaultHost = options.host || `127.0.0.1:${effectivePort}`;
  return function fixtureResponder(request, response) {
    const url = new URL(request.url || '/', `http://${request.headers.host || defaultHost}`);
  const pathname = url.pathname;

  if (request.method === 'POST' && pathname === '/blazor/_blazor/negotiate') {
    writeJson(response, {
      connectionId: 'fixture-connection-id',
      negotiateVersion: 1
    });
    return;
  }

  switch (pathname) {
    case '/':
      writeHtml(response, landingPage);
      return;
    case '/blazor/':
    case '/blazor/deep-link-check':
      writeHtml(response, blazorPage);
      return;
    case '/blazor/health':
      writeJson(response, { pathBase: '/blazor', ok: true });
      return;
    case '/hub/':
      writeHtml(response, hubPage);
      return;
    case '/hub/health':
      writeJson(response, { head: 'hub-web', pathBase: '/hub', ok: true });
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
        paths: {}
      });
      return;
    case '/docs/':
      writeHtml(response, docsPage);
      return;
    case '/docs/docs.js':
      writeJs(response, 'window.__docsFixture=true;');
      return;
    case '/downloads/':
      writeHtml(response, downloadsPage);
      return;
    case '/downloads/releases.json':
      writeJson(response, downloadsManifest);
      return;
    default:
      response.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
      response.end('not found');
  }
  };
}

function startFixtureServer(options = {}) {
  const effectivePort = Number(options.port || port);
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
  const server = startFixtureServer({ port });
  for (const signal of ['SIGINT', 'SIGTERM']) {
    process.on(signal, () => {
      server.close(() => process.exit(0));
    });
  }
}
