#!/usr/bin/env node
'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const playwrightModule = process.env.CHUMMER_PLAYWRIGHT_MODULE || 'playwright';
const { chromium } = require(playwrightModule);

const baseUrl = (process.env.CHUMMER_CLICK_AUDIT_BASE_URL || 'https://chummer.run').replace(/\/$/, '');
const outputPath = process.env.CHUMMER_CLICK_AUDIT_RECEIPT_PATH
  || path.join(process.cwd(), '.codex-studio/tmp/CLICKABLE_SURFACE_E2E.generated.json');
const concurrency = Math.max(1, Number(process.env.CHUMMER_CLICK_AUDIT_CONCURRENCY || '4'));
const clickSettleMs = Math.max(100, Number(process.env.CHUMMER_CLICK_AUDIT_SETTLE_MS || '500'));
const clickEffectTimeoutMs = Math.max(
  clickSettleMs,
  Number(process.env.CHUMMER_CLICK_AUDIT_EFFECT_TIMEOUT_MS || '5000'),
);
const navigationTimeoutMs = Math.max(5000, Number(process.env.CHUMMER_CLICK_AUDIT_NAVIGATION_TIMEOUT_MS || '45000'));
const browserExecutablePath = (process.env.CHUMMER_PLAYWRIGHT_EXECUTABLE_PATH || '').trim();
const retryRounds = Math.max(0, Number(process.env.CHUMMER_CLICK_AUDIT_RETRIES || '1'));
const routeOverride = (process.env.CHUMMER_CLICK_AUDIT_ROUTES || '')
  .split(',')
  .map(value => value.trim())
  .filter(Boolean);
const tagFilter = new Set(
  (process.env.CHUMMER_CLICK_AUDIT_TAGS || '')
    .split(',')
    .map(value => value.trim().toLowerCase())
    .filter(Boolean),
);
function parseLabelFilter() {
  const json = (process.env.CHUMMER_CLICK_AUDIT_LABELS_JSON || '').trim();
  if (json) {
    const values = JSON.parse(json);
    if (!Array.isArray(values) || values.some(value => typeof value !== 'string')) {
      throw new TypeError('CHUMMER_CLICK_AUDIT_LABELS_JSON must be a JSON array of strings');
    }
    return values.map(value => value.trim()).filter(Boolean);
  }

  return (process.env.CHUMMER_CLICK_AUDIT_LABELS || '')
    .split(',')
    .map(value => value.trim())
    .filter(Boolean);
}

const labelFilter = new Set(parseLabelFilter());
const routes = routeOverride.length > 0
  ? routeOverride
  : [
      '/blazor/app?command=character_roster',
      '/blazor/workbench?command=character_roster',
      '/blazor/workbench?command=new_character',
      '/blazor/workbench?command=open_character',
      '/blazor/workbench?fixture=blue&tab=tab-info',
      '/blazor/workbench?fixture=blue&tab=tab-create',
      '/blazor/workbench?fixture=blue&tab=tab-rules',
      '/blazor/workbench?fixture=blue&tab=tab-skills',
      '/blazor/workbench?fixture=blue&tab=tab-gear',
      '/blazor/workbench?fixture=blue&tab=tab-combat',
      '/blazor/workbench?fixture=blue&tab=tab-magician',
      '/blazor/workbench?fixture=blue&tab=tab-technomancer',
      '/blazor/workbench?fixture=blue&tab=tab-contacts',
      '/blazor/workbench?fixture=blue&tab=tab-calendar',
      '/blazor/workbench?fixture=blue&tab=tab-stats',
      '/blazor/workbench?fixture=blue&tab=tab-contacts&control=contact_add',
      '/blazor/workbench?fixture=blue&tab=tab-info&control=open_notes',
      '/blazor/workbench?fixture=blue&tab=tab-gear&control=gear_add',
    ];

function nowIso() {
  return new Date().toISOString();
}

function sha256(value) {
  return crypto.createHash('sha256').update(value).digest('hex');
}

function targetUrl(route) {
  return new URL(route, baseUrl + '/').toString();
}

async function openRoute(page, route) {
  await page.goto(targetUrl(route), {
    waitUntil: 'domcontentloaded',
    timeout: navigationTimeoutMs,
  });
  await page.locator('main, section.classic-chummer-shell, body').first().waitFor({
    state: 'visible',
    timeout: navigationTimeoutMs,
  });
  await page.waitForFunction(() => Boolean(document.querySelector(
    'section.classic-chummer-shell:not([data-ssr-workbench-fallback]), #chummer-online-app:not([data-ssr-workbench-fallback])',
  )), null, { timeout: navigationTimeoutMs });
  await page.waitForTimeout(clickSettleMs);
  await page.waitForFunction(() => {
    const signature = [
      document.body ? document.body.innerHTML.length : 0,
      document.querySelectorAll('a[href], button, summary, [role="button"], [role="menuitem"]').length,
      document.querySelectorAll('[role="dialog"], dialog, details[open]').length,
    ].join('|');
    const now = Date.now();
    if (window.__chummerClickAuditSignature !== signature) {
      window.__chummerClickAuditSignature = signature;
      window.__chummerClickAuditStableSince = now;
      return false;
    }
    return now - (window.__chummerClickAuditStableSince || now) >= 750;
  }, null, { timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(Math.max(250, clickSettleMs));
}

async function collectInteractiveContracts(page, route) {
  await openRoute(page, route);
  return page.evaluate(sourceRoute => {
    const interactiveSelector = [
      'a[href]',
      'button:not([disabled])',
      'summary',
      '[role="button"]:not([aria-disabled="true"])',
      '[role="menuitem"]:not([aria-disabled="true"])',
      '[onclick]',
    ].join(',');

    function selectorFor(element) {
      if (element.id) {
        return '#' + CSS.escape(element.id);
      }

      const parts = [];
      let current = element;
      while (current && current.nodeType === Node.ELEMENT_NODE && current !== document.body) {
        const tag = current.tagName.toLowerCase();
        const parent = current.parentElement;
        if (!parent) {
          break;
        }
        const siblings = Array.from(parent.children).filter(candidate => candidate.tagName === current.tagName);
        const suffix = siblings.length > 1
          ? ':nth-of-type(' + String(siblings.indexOf(current) + 1) + ')'
          : '';
        parts.unshift(tag + suffix);
        current = parent;
      }
      return 'body > ' + parts.join(' > ');
    }

    function surfaceFor(element) {
      let current = element;
      while (current && current !== document.body) {
        const surfaceAttribute = Array.from(current.attributes || [])
          .find(attribute => attribute.name.startsWith('data-') && (
            attribute.name.endsWith('-action')
            || attribute.name.endsWith('-command')
            || attribute.name.endsWith('-item')
            || attribute.name.endsWith('-root')
            || attribute.name.endsWith('-card')
          ));
        if (surfaceAttribute) {
          return surfaceAttribute.name + '=' + surfaceAttribute.value;
        }
        current = current.parentElement;
      }
      return '';
    }

    function dataContractFor(element) {
      return Array.from(element.attributes || [])
        .filter(attribute => attribute.name.startsWith('data-'))
        .map(attribute => attribute.name + '=' + (
          attribute.name === 'data-workbench-recent-workspace'
            ? '<workspace>'
            : attribute.value
        ))
        .sort()
        .join('|');
    }

    function stableHrefFor(tag, href) {
      if (tag !== 'a') {
        return '';
      }
      href = (href || '').trim();
      if (!href || href.startsWith('#')) {
        return href;
      }
      try {
        const url = new URL(href, window.location.href);
        url.searchParams.delete('workspace');
        return url.pathname + url.search + url.hash;
      } catch {
        return href;
      }
    }

    function isReachable(element) {
      if (element.closest('[inert], [aria-hidden="true"]')) {
        return false;
      }
      for (let current = element; current && current !== document.body; current = current.parentElement) {
        const isRouteMenuPanel = current.matches('[data-app-route-menu-panel]');
        if (current.hasAttribute('hidden') && !isRouteMenuPanel) {
          return false;
        }
        const style = window.getComputedStyle(current);
        if ((style.display === 'none' || style.visibility === 'hidden' || style.visibility === 'collapse')
          && !isRouteMenuPanel) {
          return false;
        }
      }
      return Boolean(element.getClientRects().length
        || element.closest('details:not([open]), [data-app-route-menu-panel]'));
    }

    return Array.from(document.querySelectorAll(interactiveSelector))
      .filter(isReachable)
      .map((element, index) => {
      const tag = element.tagName.toLowerCase();
      const href = tag === 'a' ? (element.getAttribute('href') || '').trim() : '';
      const label = (
        element.getAttribute('aria-label')
        || element.getAttribute('title')
        || element.textContent
        || ''
      ).replace(/\s+/g, ' ').trim().slice(0, 180);
      const role = (element.getAttribute('role') || '').trim();
      const type = (element.getAttribute('type') || '').trim();
      const ariaCurrent = (element.getAttribute('aria-current') || '').trim();
      const dataContract = dataContractFor(element);
      const surface = surfaceFor(element);
      const stableHref = stableHrefFor(tag, href);
      return {
        route: sourceRoute,
        selector: selectorFor(element),
        ordinal: index,
        tag,
        role,
        type,
        ariaCurrent,
        href,
        label,
        dataContract,
        surface,
        hiddenAtCollection: !element.getClientRects().length,
        contractKey: [tag, role, type, ariaCurrent, stableHref, label, dataContract, surface].join('||'),
      };
    });
  }, route);
}

async function resolveTargetLocator(page, contract) {
  const auditId = crypto.randomBytes(12).toString('hex');
  const found = await page.evaluate(({ expected, marker }) => {
    const interactiveSelector = [
      'a[href]',
      'button:not([disabled])',
      'summary',
      '[role="button"]:not([aria-disabled="true"])',
      '[role="menuitem"]:not([aria-disabled="true"])',
      '[onclick]',
    ].join(',');

    function labelFor(element) {
      return (
        element.getAttribute('aria-label')
        || element.getAttribute('title')
        || element.textContent
        || ''
      ).replace(/\s+/g, ' ').trim().slice(0, 180);
    }

    function dataContractFor(element) {
      return Array.from(element.attributes || [])
        .filter(attribute => attribute.name.startsWith('data-')
          && attribute.name !== 'data-chummer-e2e-target')
        .map(attribute => attribute.name + '=' + (
          attribute.name === 'data-workbench-recent-workspace'
            ? '<workspace>'
            : attribute.value
        ))
        .sort()
        .join('|');
    }

    function surfaceFor(element) {
      let current = element;
      while (current && current !== document.body) {
        const surfaceAttribute = Array.from(current.attributes || [])
          .find(attribute => attribute.name.startsWith('data-') && (
            attribute.name.endsWith('-action')
            || attribute.name.endsWith('-command')
            || attribute.name.endsWith('-item')
            || attribute.name.endsWith('-root')
            || attribute.name.endsWith('-card')
          ));
        if (surfaceAttribute) {
          return surfaceAttribute.name + '=' + surfaceAttribute.value;
        }
        current = current.parentElement;
      }
      return '';
    }

    function stableHrefFor(tag, href) {
      if (tag !== 'a') {
        return '';
      }
      href = (href || '').trim();
      if (!href || href.startsWith('#')) {
        return href;
      }
      try {
        const url = new URL(href, window.location.href);
        url.searchParams.delete('workspace');
        return url.pathname + url.search + url.hash;
      } catch {
        return href;
      }
    }

    const matches = Array.from(document.querySelectorAll(interactiveSelector)).filter(element => {
      const tag = element.tagName.toLowerCase();
      const href = tag === 'a' ? (element.getAttribute('href') || '').trim() : '';
      return tag === expected.tag
        && (element.getAttribute('role') || '').trim() === expected.role
        && (element.getAttribute('type') || '').trim() === expected.type
        && (element.getAttribute('aria-current') || '').trim() === expected.ariaCurrent
        && stableHrefFor(tag, href) === stableHrefFor(expected.tag, expected.href)
        && labelFor(element) === expected.label
        && dataContractFor(element) === expected.dataContract
        && surfaceFor(element) === expected.surface;
    });
    const target = matches[0] || null;
    if (!target) {
      return false;
    }
    target.setAttribute('data-chummer-e2e-target', marker);
    return true;
  }, { expected: contract, marker: auditId });

  return found ? page.locator('[data-chummer-e2e-target="' + auditId + '"]').first() : null;
}

async function snapshot(page, locator) {
  return page.evaluate(element => {
    function smallHash(value) {
      let hash = 2166136261;
      for (let index = 0; index < value.length; index += 1) {
        hash ^= value.charCodeAt(index);
        hash = Math.imul(hash, 16777619);
      }
      return (hash >>> 0).toString(16);
    }

    const bodyClone = document.body ? document.body.cloneNode(true) : null;
    if (bodyClone) {
      for (const marked of bodyClone.querySelectorAll('[data-chummer-e2e-target]')) {
        marked.removeAttribute('data-chummer-e2e-target');
      }
    }
    const targetClone = element ? element.cloneNode(true) : null;
    if (targetClone) {
      targetClone.removeAttribute('data-chummer-e2e-target');
    }
    const details = Array.from(document.querySelectorAll('details')).map(item => item.open);
    const dialogs = Array.from(document.querySelectorAll('dialog, [role="dialog"], .modal, .dialog-backdrop'))
      .filter(item => item.getClientRects().length)
      .map(item => item.id || item.getAttribute('aria-label') || item.className || item.tagName);
    const statuses = Array.from(document.querySelectorAll('[role="status"], [aria-live]'))
      .map(item => (item.textContent || '').replace(/\s+/g, ' ').trim())
      .filter(Boolean);
    return {
      url: window.location.href,
      bodyHash: smallHash(bodyClone ? bodyClone.innerHTML : ''),
      targetHash: smallHash(targetClone ? targetClone.outerHTML : ''),
      details,
      dialogs,
      statuses,
      expanded: element ? element.getAttribute('aria-expanded') : null,
      pressed: element ? element.getAttribute('aria-pressed') : null,
      checked: element && 'checked' in element ? Boolean(element.checked) : null,
      value: element && 'value' in element ? String(element.value) : null,
    };
  }, await locator.elementHandle());
}

function changed(before, after) {
  return before.url !== after.url
    || before.bodyHash !== after.bodyHash
    || before.targetHash !== after.targetHash
    || JSON.stringify(before.details) !== JSON.stringify(after.details)
    || JSON.stringify(before.dialogs) !== JSON.stringify(after.dialogs)
    || JSON.stringify(before.statuses) !== JSON.stringify(after.statuses)
    || before.expanded !== after.expanded
    || before.pressed !== after.pressed
    || before.checked !== after.checked
    || before.value !== after.value;
}

async function capturePostActivationSnapshot(page, contract, before) {
  const remainedOnSourceDocument = page.url() === before.url;
  const afterLocator = remainedOnSourceDocument
    ? await resolveTargetLocator(page, contract)
    : null;
  return afterLocator && await afterLocator.count() > 0
    ? snapshot(page, afterLocator)
    : {
        url: page.url(),
        bodyHash: 'target-navigated-away',
        targetHash: 'target-navigated-away',
        details: [],
        dialogs: [],
        statuses: [],
        expanded: null,
        pressed: null,
        checked: null,
        value: null,
      };
}

async function waitForPostActivationEffect(page, contract, before, eventObserved) {
  const deadline = Date.now() + clickEffectTimeoutMs;
  let after = before;
  do {
    await page.waitForTimeout(Math.min(250, clickSettleMs));
    after = await capturePostActivationSnapshot(page, contract, before);
    if (changed(before, after) || eventObserved()) {
      return after;
    }
  } while (Date.now() < deadline);

  return after;
}

async function prepareTarget(page, locator) {
  await locator.evaluate(element => {
    for (const details of Array.from(element.closest('details') ? document.querySelectorAll('details') : [])) {
      if (details.contains(element)) {
        details.open = true;
      }
    }

    const menuPanel = element.closest('[data-app-route-menu-panel]');
    if (menuPanel) {
      const menuId = menuPanel.getAttribute('data-app-route-menu-panel');
      const opener = document.querySelector('[data-app-route-menu-root="' + CSS.escape(menuId || '') + '"] > button');
      if (opener && !menuPanel.getClientRects().length) {
        opener.click();
      }
      if (!menuPanel.getClientRects().length) {
        menuPanel.hidden = false;
      }
    }
  });
}

async function activateTarget(page, locator) {
  try {
    await locator.click({ force: true, timeout: 10000 });
    return false;
  } catch (error) {
    if (!String(error && error.message || error).includes('outside of the viewport')) {
      throw error;
    }
    await locator.focus();
    await page.keyboard.press('Enter');
    return true;
  }
}

async function auditContract(browser, contract) {
  const context = await browser.newContext({
    acceptDownloads: true,
    ignoreHTTPSErrors: false,
  });
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  const httpErrors = [];
  const requestsAfterClick = [];
  let popupObserved = false;
  let downloadObserved = false;
  let keyboardFallback = false;

  page.on('console', message => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text().slice(0, 500));
    }
  });
  page.on('pageerror', error => pageErrors.push(String(error).slice(0, 500)));
  page.on('response', response => {
    if (response.status() >= 400) {
      httpErrors.push({
        status: response.status(),
        url: response.url().slice(0, 1000),
        resourceType: response.request().resourceType(),
      });
    }
  });
  page.on('popup', () => { popupObserved = true; });
  page.on('download', () => { downloadObserved = true; });

  try {
    await openRoute(page, contract.route);
    const locator = await resolveTargetLocator(page, contract);
    if (!locator || await locator.count() === 0) {
      throw new Error('interactive target disappeared before click');
    }
    await prepareTarget(page, locator);
    await page.waitForTimeout(50);
    const before = await snapshot(page, locator);
    const requestListener = request => {
      if (request.resourceType() === 'document' || request.isNavigationRequest()) {
        requestsAfterClick.push(request.url());
      }
    };
    page.on('request', requestListener);

    let externalDispatch = false;
    if (contract.tag === 'a' && contract.href) {
      const resolved = new URL(contract.href, before.url);
      if (resolved.origin !== new URL(baseUrl).origin) {
        externalDispatch = await locator.evaluate(element => {
          let dispatched = false;
          const guard = event => {
            dispatched = true;
            event.preventDefault();
          };
          element.addEventListener('click', guard, { capture: true, once: true });
          element.click();
          return dispatched;
        });
      } else {
        await locator.evaluate(element => element.click());
        await Promise.race([
          page.waitForFunction(
            previousUrl => window.location.href !== previousUrl,
            before.url,
            { timeout: 3000 },
          ).catch(() => {}),
          page.waitForTimeout(Math.max(clickSettleMs, 1200)),
        ]);
      }
    } else {
      keyboardFallback = await activateTarget(page, locator);
    }

    const after = await waitForPostActivationEffect(
      page,
      contract,
      before,
      () => requestsAfterClick.length > 0 || popupObserved || downloadObserved || externalDispatch,
    );
    page.removeListener('request', requestListener);
    const observableChange = changed(before, after);
    const eventObserved = requestsAfterClick.length > 0 || popupObserved || downloadObserved || externalDispatch;
    const hrefValid = contract.tag !== 'a' || Boolean(contract.href);
    const currentLocationAffordance = contract.tag === 'a'
      && (() => {
        const target = new URL(contract.href, before.url);
        const current = new URL(before.url);
        target.searchParams.delete('workspace');
        current.searchParams.delete('workspace');
        return target.toString() === current.toString();
      })();
    const sameDocumentFragmentAffordance = contract.tag === 'a'
      && contract.href.startsWith('#')
      && await page.evaluate(hash => {
        const target = document.getElementById(decodeURIComponent(hash.slice(1)));
        return Boolean(target && (target.matches('main') || target.hasAttribute('tabindex')));
      }, contract.href);
    const passed = hrefValid && (
      observableChange
      || eventObserved
      || currentLocationAffordance
      || sameDocumentFragmentAffordance);

    return {
      ...contract,
      status: passed ? 'passed' : 'failed',
      failureKind: passed ? '' : (hrefValid ? 'no_observable_effect' : 'missing_href'),
      observableChange,
      requestObserved: requestsAfterClick.length > 0,
      popupObserved,
      downloadObserved,
      externalDispatch,
      keyboardFallback,
      currentLocationAffordance,
      sameDocumentFragmentAffordance,
      before,
      after,
      consoleErrors,
      pageErrors,
      httpErrors,
    };
  } catch (error) {
    return {
      ...contract,
      status: 'failed',
      failureKind: 'click_error',
      error: String(error && error.stack || error).slice(0, 2000),
      consoleErrors,
      pageErrors,
      httpErrors,
    };
  } finally {
    await context.close();
  }
}

async function mapWithConcurrency(items, limit, worker) {
  const results = new Array(items.length);
  let cursor = 0;
  async function runWorker() {
    while (true) {
      const index = cursor;
      cursor += 1;
      if (index >= items.length) {
        return;
      }
      results[index] = await worker(items[index], index);
    }
  }
  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, runWorker));
  return results;
}

async function main() {
  const startedAt = nowIso();
  const browser = await chromium.launch({
    headless: true,
    ...(browserExecutablePath ? { executablePath: browserExecutablePath } : {}),
  });
  const collectionContext = await browser.newContext();
  const collectionPage = await collectionContext.newPage();
  const collected = [];
  const collectionFailures = [];

  for (const route of routes) {
    try {
      const routeContracts = await collectInteractiveContracts(collectionPage, route);
      collected.push(...routeContracts);
    } catch (error) {
      collectionFailures.push({
        route,
        error: String(error && error.stack || error).slice(0, 2000),
      });
    }
  }
  await collectionContext.close();

  const uniqueByContract = new Map();
  for (const contract of collected) {
    const existing = uniqueByContract.get(contract.contractKey);
    if (existing) {
      existing.coveredRoutes.push(contract.route);
    } else {
      uniqueByContract.set(contract.contractKey, {
        ...contract,
        coveredRoutes: [contract.route],
      });
    }
  }
  const allContracts = Array.from(uniqueByContract.values());
  const contracts = allContracts.filter(contract =>
    (tagFilter.size === 0 || tagFilter.has(contract.tag))
    && (labelFilter.size === 0 || labelFilter.has(contract.label)));
  const results = await mapWithConcurrency(contracts, concurrency, contract => auditContract(browser, contract));
  for (let retryRound = 1; retryRound <= retryRounds; retryRound += 1) {
    const failedIndexes = results
      .map((result, index) => result.status === 'passed' ? -1 : index)
      .filter(index => index >= 0);
    if (failedIndexes.length === 0) {
      break;
    }

    const retryResults = await mapWithConcurrency(
      failedIndexes,
      Math.min(2, concurrency),
      index => auditContract(browser, contracts[index]),
    );
    for (let retryIndex = 0; retryIndex < failedIndexes.length; retryIndex += 1) {
      const resultIndex = failedIndexes[retryIndex];
      results[resultIndex] = {
        ...retryResults[retryIndex],
        retryRound,
      };
    }
  }
  await browser.close();

  const failed = results.filter(result => result.status !== 'passed');
  const browserErrors = results.filter(result =>
    (result.consoleErrors && result.consoleErrors.length > 0)
    || (result.pageErrors && result.pageErrors.length > 0)
    || (result.httpErrors && result.httpErrors.length > 0));
  const receipt = {
    contractName: 'chummer6-ui.clickable-surface-e2e',
    contractVersion: 3,
    generatedAt: nowIso(),
    startedAt,
    baseUrl,
    routes,
    scope: tagFilter.size > 0 || labelFilter.size > 0 ? 'filtered' : 'full',
    tagFilter: Array.from(tagFilter),
    labelFilter: Array.from(labelFilter),
    retryRounds,
    timing: {
      clickSettleMs,
      clickEffectTimeoutMs,
    },
    routeSetSha256: sha256(JSON.stringify(routes)),
    status: collectionFailures.length === 0 && failed.length === 0 && browserErrors.length === 0
      ? 'passed'
      : 'failed',
    totals: {
      renderedOccurrences: collected.length,
      discoveredUniqueInteractiveContracts: allContracts.length,
      auditedUniqueInteractiveContracts: contracts.length,
      uniqueInteractiveContracts: contracts.length,
      passed: results.length - failed.length,
      failed: failed.length,
      browserErrorContracts: browserErrors.length,
      collectionFailures: collectionFailures.length,
    },
    collectionFailures,
    failures: failed,
    browserErrors,
    results,
  };

  fs.mkdirSync(path.dirname(outputPath), { recursive: true, mode: 0o700 });
  fs.writeFileSync(outputPath, JSON.stringify(receipt, null, 2) + '\n', { mode: 0o600 });
  process.stdout.write(JSON.stringify({
    status: receipt.status,
    outputPath,
    totals: receipt.totals,
  }) + '\n');
  if (receipt.status !== 'passed') {
    process.exitCode = 1;
  }
}

main().catch(error => {
  process.stderr.write(String(error && error.stack || error) + '\n');
  process.exitCode = 1;
});
