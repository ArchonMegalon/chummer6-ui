#!/usr/bin/env node
'use strict';

const { chromium } = require(process.env.CHUMMER_PLAYWRIGHT_MODULE || 'playwright');

const baseUrl = (process.env.CHUMMER_BLAZOR_BASE_URL || 'http://127.0.0.1:8089').replace(/\/$/, '');
const buildUrl = process.env.CHUMMER_BUILD_PWA_URL || `${baseUrl}/app?fixture=blue&tab=tab-create`;

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function waitForLayout(page, expected) {
  await page.waitForFunction(
    (layout) => document.querySelector('.build-pwa-workspace')?.dataset.buildPwaLayoutEffective === layout,
    expected,
    { timeout: 15000 });
}

function boxesOverlap(first, second) {
  return first.x < second.x + second.width
    && first.x + first.width > second.x
    && first.y < second.y + second.height
    && first.y + first.height > second.y;
}

async function assertCompactInstallClearance(page, width) {
  const install = page.locator('[data-build-pwa-install-help]');
  const installBox = await install.boundingBox();
  assert(installBox, `Install launcher must be visible at ${width}px.`);
  assert(await install.evaluate((element) => getComputedStyle(element).position) !== 'fixed',
    `Install launcher must reserve normal-flow space at ${width}px.`);

  for (const [selector, label] of [
    ['.build-pwa-compact-context', 'compact header'],
    ['.build-pwa-compact-context progress', 'progress rail'],
    ['.build-pwa-step-rail', 'step rail'],
    ['.build-pwa-layout-picker', 'layout picker'],
    ['[data-nav-tab][aria-current="step"]', 'active step'],
    ['.build-pwa-mobile-command-menu > summary', 'Actions']
  ]) {
    const targetBox = await page.locator(selector).boundingBox();
    assert(targetBox, `${label} must be visible at ${width}px.`);
    assert(!boxesOverlap(installBox, targetBox),
      `Install launcher overlaps ${label} at ${width}px.`);
  }
}

async function assertNoOuterHorizontalOverflow(page, label) {
  const measurements = await page.evaluate(() => {
    const shell = document.querySelector('.desktop-shell--responsive-build');
    const workspace = document.querySelector('.build-pwa-workspace');
    return [
      ['page', document.documentElement],
      ['body', document.body],
      ['shell', shell],
      ['workspace', workspace]
    ].map(([name, element]) => ({
      clientWidth: element instanceof HTMLElement ? element.clientWidth : -1,
      children: (name === 'shell' || name === 'workspace') && element instanceof HTMLElement
        ? Array.from(element.children).map((child) => ({
            className: child.className,
            clientWidth: child instanceof HTMLElement ? child.clientWidth : -1,
            scrollWidth: child instanceof HTMLElement ? child.scrollWidth : -1
          }))
        : [],
      effective: workspace instanceof HTMLElement ? workspace.dataset.buildPwaLayoutEffective : '',
      minimum: workspace instanceof HTMLElement ? workspace.dataset.buildPwaLayoutMinimumInlineSize : '',
      name,
      rootFontSize: getComputedStyle(document.documentElement).fontSize,
      scrollWidth: element instanceof HTMLElement ? element.scrollWidth : -1,
      viewportWidth: window.innerWidth
    }));
  });

  const unavailable = measurements.filter((measurement) => measurement.clientWidth < 0);
  assert(unavailable.length === 0,
    `${label}: ${unavailable.map((measurement) => measurement.name).join(', ')} unavailable for overflow measurement.`);
  const overflow = measurements.filter(
    (measurement) => measurement.scrollWidth > measurement.clientWidth + 1);
  assert(overflow.length === 0,
    `${label}: outer horizontal overflow ${JSON.stringify(overflow)}.`);
}

async function assertForcedWorkspaceClamp(page, width) {
  await page.setViewportSize({ width, height: 900 });
  await waitForLayout(page, 'compact');
  assert(await page.locator('.build-pwa-workspace').getAttribute('data-build-pwa-layout-reason') === 'workspace-minimum-width',
    `Forced Workspace was not clamped when the three-rail minimum could not fit at ${width}px.`);
  assert(await page.locator('.build-pwa-workspace').getAttribute('data-build-pwa-layout-preference') === 'workspace',
    `The three-rail fit clamp overwrote the saved Workspace preference at ${width}px.`);
  assert(await page.locator('[data-build-pwa-layout-choice="workspace"]').isChecked(),
    `The saved Workspace choice was not retained at ${width}px.`);
  assert(await page.locator('#build-pwa-layout-status').textContent().then((text) => text.includes('cannot fit the three-column Workspace layout')),
    `The Workspace clamp did not expose its fit reason at ${width}px.`);
  await assertNoOuterHorizontalOverflow(page, `${width}px compact clamp`);
}

async function assertMeasuredWorkspaceFit(page, width, legacyMediaWouldClamp) {
  await page.setViewportSize({ width, height: 900 });
  await waitForLayout(page, 'workspace');
  await page.waitForFunction(() => {
    const shell = document.querySelector('.desktop-shell--responsive-build');
    const workspace = document.querySelector('.build-pwa-workspace');
    return shell instanceof HTMLElement
      && workspace instanceof HTMLElement
      && Math.abs(Number(workspace.dataset.buildPwaLayoutAvailableInlineSize) - shell.clientWidth) <= 0.25;
  }, null, { timeout: 15000 });
  const fit = await page.locator('.build-pwa-workspace').evaluate((workspace) => ({
    available: Number(workspace.dataset.buildPwaLayoutAvailableInlineSize),
    legacyCompact: window.matchMedia(window.chummerBuildPwaLayout.compactQuery).matches,
    minimum: Number(workspace.dataset.buildPwaLayoutMinimumInlineSize),
    preference: workspace.dataset.buildPwaLayoutPreference
  }));
  assert(fit.preference === 'workspace', `Measured fit overwrote Workspace preference at ${width}px.`);
  assert(fit.available + 0.25 >= fit.minimum,
    `Workspace was selected without fitting its measured geometry at ${width}px (${fit.available} < ${fit.minimum}).`);
  assert(fit.legacyCompact === legacyMediaWouldClamp,
    `Legacy media-query boundary was not exercised at ${width}px.`);
  await assertNoOuterHorizontalOverflow(page, `${width}px measured workspace fit`);
}

async function readMeasuredGeometry(page) {
  return page.locator('.build-pwa-workspace').evaluate((workspace) => {
    const shell = document.querySelector('.desktop-shell--responsive-build');
    const ancestors = [];
    let ancestor = shell instanceof HTMLElement ? shell.parentElement : null;
    while (ancestor instanceof HTMLElement && ancestors.length < 6) {
      ancestors.push({
        className: ancestor.className,
        clientWidth: ancestor.clientWidth,
        display: getComputedStyle(ancestor).display,
        tagName: ancestor.tagName.toLowerCase()
      });
      ancestor = ancestor.parentElement;
    }
    return {
      ancestors,
      available: Number(workspace.dataset.buildPwaLayoutAvailableInlineSize),
      effective: workspace.dataset.buildPwaLayoutEffective,
      minimum: Number(workspace.dataset.buildPwaLayoutMinimumInlineSize),
      rootFontSize: Number.parseFloat(getComputedStyle(document.documentElement).fontSize),
      shellClientWidth: shell instanceof HTMLElement ? shell.clientWidth : -1,
      viewportWidth: window.innerWidth
    };
  });
}

async function applyLayoutNow(page) {
  await page.evaluate(() => window.chummerBuildPwaLayout.applyAll());
  await page.waitForFunction(() => {
    const shell = document.querySelector('.desktop-shell--responsive-build');
    const workspace = document.querySelector('.build-pwa-workspace');
    if (shell instanceof HTMLElement && workspace instanceof HTMLElement) {
      window.chummerBuildPwaLayout.applyAll();
    }
    return shell instanceof HTMLElement
      && workspace instanceof HTMLElement
      && Math.abs(Number(workspace.dataset.buildPwaLayoutAvailableInlineSize) - shell.clientWidth) <= 0.25;
  }, null, { timeout: 15000 });
}

async function moveToMeasuredWorkspaceOffset(page, offset, expectedLayout, label) {
  await page.setViewportSize({ width: 1600, height: 900 });
  await page.evaluate(() => {
    const shell = document.querySelector('.desktop-shell--responsive-build');
    if (shell instanceof HTMLElement) {
      shell.style.removeProperty('inline-size');
    }
  });
  await applyLayoutNow(page);

  let geometry = await readMeasuredGeometry(page);
  assert(Number.isFinite(geometry.minimum) && geometry.minimum > 0,
    `${label} could not measure the Workspace probe (${JSON.stringify(geometry)}).`);
  for (let attempt = 0; attempt < 4; attempt += 1) {
    const desiredAvailable = geometry.minimum + offset;
    await page.evaluate((inlineSize) => {
      const shell = document.querySelector('.desktop-shell--responsive-build');
      if (shell instanceof HTMLElement) {
        shell.style.inlineSize = `${inlineSize}px`;
      }
    }, Math.max(320, desiredAvailable));
    await applyLayoutNow(page);
    geometry = await readMeasuredGeometry(page);
    if (Math.abs((geometry.available - geometry.minimum) - offset) <= 0.75) {
      break;
    }
  }

  geometry = await readMeasuredGeometry(page);
  assert(Math.abs((geometry.available - geometry.minimum) - offset) <= 0.75,
    `${label} did not land next to the measured boundary (${geometry.available} vs ${geometry.minimum}, offset ${offset}).`);
  assert(geometry.effective === expectedLayout,
    `${label} selected ${geometry.effective} instead of ${expectedLayout}.`);
  if (expectedLayout === 'compact') {
    assert(geometry.available + 0.25 < geometry.minimum,
      `${label} did not stay below the measured Workspace fit tolerance.`);
  } else {
    assert(geometry.available + 0.25 >= geometry.minimum,
      `${label} did not clear the measured Workspace fit tolerance.`);
  }
  await assertNoOuterHorizontalOverflow(page, label);
  return geometry;
}

async function assertMeasuredWorkspaceBoundary(page, rootFontSize, label) {
  await page.evaluate((fontSize) => {
    if (fontSize === null) {
      document.documentElement.style.removeProperty('font-size');
    } else {
      document.documentElement.style.fontSize = fontSize;
    }
    window.chummerBuildPwaLayout.setPreference('workspace');
  }, rootFontSize);

  const below = await moveToMeasuredWorkspaceOffset(page, -1, 'compact', `${label} just below`);
  const above = await moveToMeasuredWorkspaceOffset(page, 1, 'workspace', `${label} just above`);
  const expectedMinimum = 60.7 * above.rootFontSize;
  assert(Math.abs(above.minimum - expectedMinimum) <= 0.5,
    `${label} measured ${above.minimum}px instead of 60.7rem at a ${above.rootFontSize}px root (${expectedMinimum}px).`);
  assert(Math.abs(below.minimum - above.minimum) <= 0.25,
    `${label} changed the measured minimum while crossing the same root-size boundary.`);

  await page.evaluate(() => {
    document.documentElement.style.removeProperty('font-size');
    const shell = document.querySelector('.desktop-shell--responsive-build');
    if (shell instanceof HTMLElement) {
      shell.style.removeProperty('inline-size');
    }
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await applyLayoutNow(page);
  const restored = await readMeasuredGeometry(page);
  assert(restored.effective === 'workspace',
    `${label} did not restore the default-root Workspace baseline (${JSON.stringify(restored)}).`);
}

async function assertRootTextClamp(page, fontSize, label) {
  await page.evaluate(() => document.documentElement.style.removeProperty('font-size'));
  await page.setViewportSize({ width: 960, height: 900 });
  await applyLayoutNow(page);
  await waitForLayout(page, 'workspace');
  await page.evaluate((value) => {
    document.documentElement.style.fontSize = value;
  }, fontSize);
  await applyLayoutNow(page);
  await waitForLayout(page, 'compact');
  const fit = await page.locator('.build-pwa-workspace').evaluate((workspace) => ({
    available: Number(workspace.dataset.buildPwaLayoutAvailableInlineSize),
    minimum: Number(workspace.dataset.buildPwaLayoutMinimumInlineSize),
    preference: workspace.dataset.buildPwaLayoutPreference,
    reason: workspace.dataset.buildPwaLayoutReason,
    rootFontSize: getComputedStyle(document.documentElement).fontSize
  }));
  assert(fit.minimum > fit.available + 0.25,
    `${label} did not expand the measured three-column minimum (${fit.minimum} <= ${fit.available}).`);
  assert(fit.preference === 'workspace' && fit.reason === 'workspace-minimum-width',
    `${label} clamp did not preserve the Workspace preference and measured-fit reason.`);

  await page.evaluate(() => document.documentElement.style.removeProperty('font-size'));
  await applyLayoutNow(page);
  await waitForLayout(page, 'workspace');
}

async function assertFocusRepairRace(page) {
  await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
  await waitForLayout(page, 'workspace');
  const chromeTarget = page.locator('.tool-strip button:not([disabled]):visible').first();
  await chromeTarget.waitFor({ state: 'visible', timeout: 15000 });
  await chromeTarget.focus();
  const activeAfterRace = await page.evaluate(async () => {
    const chrome = document.activeElement;
    if (!(chrome instanceof HTMLElement) || !chrome.closest('.tool-strip')) {
      return null;
    }

    window.chummerBuildPwaLayout.setPreference('compact');
    const editor = document.querySelector('#chummer-workspace-main');
    if (!(editor instanceof HTMLElement)) {
      return null;
    }
    editor.focus();
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    const active = document.activeElement;
    return active instanceof HTMLElement
      ? `${active.tagName.toLowerCase()}#${active.id}.${active.className}`
      : null;
  });
  assert(activeAfterRace?.startsWith('main#chummer-workspace-main.'),
    `Deferred compact focus repair stole focus from a newer user target (${activeAfterRace}).`);
  await page.locator('[data-build-pwa-layout-choice="workspace"]').check();
  await waitForLayout(page, 'workspace');
}

async function assertFocusRepairAfterCssHide(page) {
  await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
  await waitForLayout(page, 'workspace');
  const chromeTarget = page.locator('.tool-strip button:not([disabled]):visible').first();
  await chromeTarget.waitFor({ state: 'visible', timeout: 15000 });
  await chromeTarget.focus();
  const result = await page.evaluate(async () => {
    const chrome = document.activeElement;
    if (!(chrome instanceof HTMLElement) || !chrome.closest('.tool-strip')) {
      return { ready: false };
    }

    const focusedBefore = document.activeElement === chrome;
    window.chummerBuildPwaLayout.setPreference('compact');
    // Chromium normally moves focus to body when display:none hides the tool
    // strip. Blur explicitly models that browser step so the race stays
    // deterministic if a rendering engine defers the focus transfer.
    chrome.blur();
    const neutralAfterHide = document.activeElement === document.body
      || document.activeElement === document.documentElement;
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    const active = document.activeElement;
    const accessibleFallback = active instanceof HTMLElement
      && active.getClientRects().length > 0
      && (active.matches('.build-pwa-mobile-command-menu > summary')
        || active.id === 'chummer-workspace-main');
    const accessibleName = active instanceof HTMLElement
      ? (active.getAttribute('aria-label') || active.textContent || '').trim()
      : '';
    return {
      accessibleFallback,
      accessibleName,
      focusedBefore,
      neutralAfterHide,
      ready: true
    };
  });

  assert(result.ready && result.focusedBefore,
    'CSS-hide focus repair probe could not focus the visible Workspace chrome.');
  assert(result.neutralAfterHide,
    'CSS-hide focus repair probe did not model the browser body-focus transition.');
  assert(result.accessibleFallback && result.accessibleName.length > 0,
    'CSS-hide focus repair did not move focus to a visible, named compact target.');
  await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
  await waitForLayout(page, 'workspace');
}

async function assertFocusRepairAfterUserGesture(page) {
  for (const gesture of ['pointer', 'keyboard']) {
    await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
    await waitForLayout(page, 'workspace');
    const chromeTarget = page.locator('.tool-strip button:not([disabled]):visible').first();
    await chromeTarget.waitFor({ state: 'visible', timeout: 15000 });

    const result = await chromeTarget.evaluate(async (chrome, kind) => {
      const editor = document.querySelector('#chummer-workspace-main');
      if (!(editor instanceof HTMLElement)) {
        return { ready: false };
      }
      editor.focus();

      if (kind === 'pointer') {
        chrome.dispatchEvent(new PointerEvent('pointerdown', {
          bubbles: true,
          pointerType: 'mouse'
        }));
        chrome.focus();
        chrome.dispatchEvent(new PointerEvent('pointerup', {
          bubbles: true,
          pointerType: 'mouse'
        }));
      } else {
        editor.dispatchEvent(new KeyboardEvent('keydown', {
          bubbles: true,
          key: 'Tab'
        }));
        chrome.focus();
        chrome.dispatchEvent(new KeyboardEvent('keyup', {
          bubbles: true,
          key: 'Tab'
        }));
      }
      if (document.activeElement !== chrome) {
        return { ready: false };
      }

      const chromeStrip = chrome.closest('.tool-strip');
      if (!(chromeStrip instanceof HTMLElement)) {
        return { ready: false };
      }

      // Model the media query winning before the resize callback. This is the
      // path that must recover from body focus via the remembered user target.
      chromeStrip.style.display = 'none';
      chrome.blur();
      const neutralBeforeLayout = document.activeElement === document.body
        || document.activeElement === document.documentElement;
      window.chummerBuildPwaLayout.setPreference('compact');
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));

      const active = document.activeElement;
      const repaired = active instanceof HTMLElement
        && active.getClientRects().length > 0
        && (active.matches('.build-pwa-mobile-command-menu > summary')
          || active.id === 'chummer-workspace-main');
      chromeStrip.style.removeProperty('display');
      return {
        neutralBeforeLayout,
        ready: true,
        repaired
      };
    }, gesture);

    assert(result.ready && result.neutralBeforeLayout && result.repaired,
      `CSS-hide focus repair lost a ${gesture}-focused Workspace control.`);
    await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
    await waitForLayout(page, 'workspace');
  }
}

async function assertFocusRepairInteractionGuards(page) {
  for (const interaction of ['pointer', 'keyboard']) {
    await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
    await waitForLayout(page, 'workspace');
    await page.locator('.tool-strip button:not([disabled]):visible').first()
      .waitFor({ state: 'visible', timeout: 15000 });
    await page.locator('.tool-strip button:not([disabled]):visible').first().focus();
    const result = await page.evaluate(async (kind) => {
      const chrome = document.activeElement;
      if (!(chrome instanceof HTMLElement) || !chrome.closest('.tool-strip')) {
        return { ready: false };
      }

      window.chummerBuildPwaLayout.setPreference('compact');
      chrome.blur();
      if (kind === 'pointer') {
        document.body.dispatchEvent(new PointerEvent('pointerdown', {
          bubbles: true,
          pointerType: 'mouse'
        }));
      } else {
        document.body.dispatchEvent(new KeyboardEvent('keydown', {
          bubbles: true,
          key: 'Tab'
        }));
      }
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      const activeAfterInteraction = document.activeElement;
      const neutralAfterInteraction = activeAfterInteraction === document.body
        || document.activeElement === document.documentElement;

      // A later geometry pass must not revive the stale pre-interaction target.
      window.chummerBuildPwaLayout.applyAll();
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      const activeAfterReapply = document.activeElement;
      const neutralAfterReapply = activeAfterReapply === document.body
        || document.activeElement === document.documentElement;
      return {
        activeAfterInteraction: activeAfterInteraction instanceof HTMLElement
          ? `${activeAfterInteraction.tagName.toLowerCase()}#${activeAfterInteraction.id}.${activeAfterInteraction.className}`
          : String(activeAfterInteraction),
        activeAfterReapply: activeAfterReapply instanceof HTMLElement
          ? `${activeAfterReapply.tagName.toLowerCase()}#${activeAfterReapply.id}.${activeAfterReapply.className}`
          : String(activeAfterReapply),
        neutralAfterInteraction,
        neutralAfterReapply,
        ready: true
      };
    }, interaction);

    assert(result.ready,
      `Focus-repair ${interaction} guard could not find stable visible Workspace chrome.`);
    assert(result.neutralAfterInteraction,
      `Deferred focus repair stole focus after a newer ${interaction} interaction (${result.activeAfterInteraction}).`);
    assert(result.neutralAfterReapply,
      `A later layout pass revived stale focus after a ${interaction} interaction (${result.activeAfterReapply}).`);
    await page.evaluate(() => window.chummerBuildPwaLayout.setPreference('workspace'));
    await waitForLayout(page, 'workspace');
  }
}

async function assertStableLayoutStatus(page) {
  const mutationCount = await page.evaluate(async () => {
    const status = document.querySelector('#build-pwa-layout-status');
    if (!(status instanceof HTMLElement)) {
      return -1;
    }

    let count = 0;
    const observer = new MutationObserver((records) => { count += records.length; });
    observer.observe(status, { characterData: true, childList: true, subtree: true });
    window.chummerBuildPwaLayout.applyAll();
    window.chummerBuildPwaLayout.applyAll();
    await new Promise((resolve) => requestAnimationFrame(resolve));
    observer.disconnect();
    return count;
  });
  assert(mutationCount === 0, 'Stable layout re-announced an unchanged live status.');
}

async function assertActiveStepReveal(page) {
  const probe = await page.evaluate(() => {
    const list = document.querySelector('.build-pwa-step-list');
    const active = list?.querySelector('[data-nav-tab][aria-current="step"]');
    const candidates = list
      ? Array.from(list.querySelectorAll('[data-nav-tab]:not([disabled])')).filter((step) => step !== active)
      : [];
    if (!(list instanceof HTMLElement) || !(active instanceof HTMLElement) || candidates.length === 0) {
      return { ready: false };
    }

    const target = candidates.reduce((furthest, candidate) => (
      Math.abs(candidate.offsetLeft - active.offsetLeft) > Math.abs(furthest.offsetLeft - active.offsetLeft)
        ? candidate
        : furthest
    ));
    list.scrollLeft = target.offsetLeft < list.scrollWidth / 2 ? list.scrollWidth : 0;
    const listBox = list.getBoundingClientRect();
    const targetBox = target.getBoundingClientRect();
    const wasOutside = targetBox.right < listBox.left || targetBox.left > listBox.right;
    target.click();
    return {
      ready: true,
      targetId: target.getAttribute('data-nav-tab'),
      wasOutside
    };
  });

  assert(probe.ready, 'Active-step reveal probe needs another enabled builder step.');
  assert(probe.wasOutside, 'Active-step reveal probe did not begin with its target outside the step rail.');
  await page.waitForFunction((targetId) => {
    const active = document.querySelector('[data-nav-tab][aria-current="step"]');
    return active?.getAttribute('data-nav-tab') === targetId;
  }, probe.targetId, { timeout: 15000 });
  await page.waitForFunction(() => {
    const list = document.querySelector('.build-pwa-step-list');
    const active = list?.querySelector('[data-nav-tab][aria-current="step"]');
    if (!(list instanceof HTMLElement) || !(active instanceof HTMLElement)) {
      return false;
    }

    const listBox = list.getBoundingClientRect();
    const activeBox = active.getBoundingClientRect();
    return activeBox.left >= listBox.left - 1 && activeBox.right <= listBox.right + 1;
  }, null, { timeout: 15000 });
  const activeRevealed = await page.evaluate(() => {
    const list = document.querySelector('.build-pwa-step-list');
    const active = list?.querySelector('[data-nav-tab][aria-current="step"]');
    if (!(list instanceof HTMLElement) || !(active instanceof HTMLElement)) {
      return false;
    }

    const listBox = list.getBoundingClientRect();
    const activeBox = active.getBoundingClientRect();
    return activeBox.left >= listBox.left - 1 && activeBox.right <= listBox.right + 1;
  });
  assert(activeRevealed, 'Active navigation did not reveal the newly selected step.');
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();

  try {
    await page.goto(`${baseUrl}/health`, { waitUntil: 'domcontentloaded', timeout: 15000 });
    await page.evaluate(async ({ cacheName, assetUrl, offlineUrl }) => {
      localStorage.removeItem('chummer.build-pwa.layout.v1');
      const cache = await caches.open(cacheName);
      await cache.put(assetUrl, new Response('cross-app-cache-sentinel', {
        status: 200,
        headers: {
          'Cache-Control': 'public, max-age=3600',
          'Content-Type': 'text/css'
        }
      }));
      await cache.put(offlineUrl, new Response('cross-app-offline-sentinel', {
        status: 200,
        headers: {
          'Cache-Control': 'public, max-age=3600',
          'Content-Type': 'text/html'
        }
      }));
    }, {
      cacheName: 'chummer-public-root-static-cross-app-proof',
      assetUrl: `${baseUrl}/app.css`,
      offlineUrl: `${baseUrl}/offline.html`
    });
    await page.goto(buildUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('.build-pwa-workspace').waitFor({ state: 'visible', timeout: 30000 });
    await waitForLayout(page, 'workspace');

    await page.waitForFunction(() => {
      const pwa = window.chummerPwa;
      const registration = pwa?.registration;
      const worker = registration?.active ?? registration?.waiting ?? registration?.installing;
      return registration?.scope === pwa?.expectedAuthority?.scope
        && worker?.scriptURL === pwa?.expectedAuthority?.scriptUrl;
    }, null, { timeout: 30000 });
    // Passive workers never claim an already-open page. Re-enter the Build URL
    // after the first install so the browser can attach the verified controller.
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('.build-pwa-workspace').waitFor({ state: 'visible', timeout: 30000 });
    await waitForLayout(page, 'workspace');
    await page.waitForFunction(() => {
      return navigator.serviceWorker.controller?.scriptURL
        === window.chummerPwa?.expectedAuthority?.scriptUrl;
    }, null, { timeout: 30000 });

    const registrationIdentity = await page.evaluate(() => {
      const pwa = window.chummerPwa;
      const registration = pwa.registration;
      const worker = registration.active ?? registration.waiting ?? registration.installing;
      return {
        registrationMatches: pwa.registration.scope === pwa.expectedAuthority.scope,
        workerMatches: worker?.scriptURL === pwa.expectedAuthority.scriptUrl,
        scope: registration.scope,
        scriptUrl: worker?.scriptURL ?? ''
      };
    });
    assert(registrationIdentity.registrationMatches,
      `Build install/update UI bound the wrong scope: ${registrationIdentity.scope}`);
    assert(registrationIdentity.workerMatches,
      `Build install/update UI bound the wrong worker: ${registrationIdentity.scriptUrl}`);

    const cacheIsolation = await page.evaluate(async ({ cacheName, assetUrl }) => {
      const revisionedAssetUrl = new URL(assetUrl);
      revisionedAssetUrl.searchParams.set(
        'build',
        window.chummerPwa.expectedAuthority.contentRevision);
      const unrelatedCache = await caches.open(cacheName);
      await unrelatedCache.put(revisionedAssetUrl.href, new Response('cross-app-cache-sentinel', {
        status: 200,
        headers: {
          'Cache-Control': 'public, max-age=3600',
          'Content-Type': 'text/css'
        }
      }));
      const response = await fetch(revisionedAssetUrl.href, { cache: 'reload' });
      const body = await response.text();
      const unrelatedResponse = await unrelatedCache.match(revisionedAssetUrl.href);
      return {
        body,
        unrelatedBody: unrelatedResponse ? await unrelatedResponse.text() : null
      };
    }, {
      cacheName: 'chummer-public-root-static-cross-app-proof',
      assetUrl: `${baseUrl}/app.css`
    });
    assert(cacheIsolation.body !== 'cross-app-cache-sentinel',
      'Build must not serve a matching URL from the public root cache.');
    assert(cacheIsolation.unrelatedBody === 'cross-app-cache-sentinel',
      'Build cache migration must preserve unrelated public root caches.');

    assert(!await page.locator('.desktop-shell--responsive-build').evaluate((element) => element === document.activeElement),
      'The responsive shell stole initial focus.');

    const editor = page.locator('#chummer-workspace-main');
    await editor.evaluate((element) => {
      element.dataset.resizeStateSentinel = 'preserved';
      const field = element.querySelector('input, textarea, select');
      if (field) {
        field.dataset.resizeFieldSentinel = 'preserved';
        field.dataset.resizeFieldValue = String(field.value || '');
      }
    });

    const activeSection = await page.locator('[data-nav-tab][aria-current="step"]').getAttribute('data-nav-tab');
    await page.locator('[data-build-pwa-layout-choice="workspace"]').check();
    await waitForLayout(page, 'workspace');

    await assertStableLayoutStatus(page);
    await assertFocusRepairAfterCssHide(page);
    await assertFocusRepairAfterUserGesture(page);
    await assertFocusRepairRace(page);
    await assertFocusRepairInteractionGuards(page);
    await assertMeasuredWorkspaceBoundary(page, null, 'app root size');
    await assertMeasuredWorkspaceFit(page, 959, true);
    await assertMeasuredWorkspaceFit(page, 960, false);
    await assertMeasuredWorkspaceBoundary(page, '20px', '20px root font');
    await assertRootTextClamp(page, '200%', '200% root text');

    const focusedChrome = page.locator('.tool-strip button:not([disabled]):visible').first();
    await focusedChrome.focus();
    await assertForcedWorkspaceClamp(page, 430);
    await assertForcedWorkspaceClamp(page, 390);
    await assertForcedWorkspaceClamp(page, 320);
    await page.waitForFunction(() => {
      const active = document.activeElement;
      return active instanceof HTMLElement
        && active.getClientRects().length > 0
        && (active.matches('.build-pwa-mobile-command-menu > summary') || active.id === 'chummer-workspace-main');
    }, null, { timeout: 15000 });

    assert(await editor.getAttribute('data-resize-state-sentinel') === 'preserved',
      'Viewport resize replaced the shared editor DOM instead of reflowing it.');
    assert(await page.locator('[data-nav-tab][aria-current="step"]').getAttribute('data-nav-tab') === activeSection,
      'Viewport resize changed the active builder section.');

    assert(await page.locator('.build-pwa-workspace').getAttribute('data-build-pwa-layout-preference') === 'workspace',
      'The accessibility clamp overwrote the saved Workspace preference.');
    assert(await page.locator('[data-build-pwa-layout-choice="workspace"]').isChecked(),
      'The saved Workspace choice was not retained while Compact provided accessible reflow.');
    assert(await page.locator('#build-pwa-layout-status').textContent().then((text) => text.includes('Workspace remains saved')),
      'The accessibility clamp did not explain that Workspace remains saved.');

    await assertNoOuterHorizontalOverflow(page, '320px accessibility clamp');

    const compactTitle = page.locator('#build-pwa-compact-title');
    assert(await compactTitle.evaluate((element) => element.tagName === 'H1' && getComputedStyle(element).display !== 'none'),
      'The compact title must be a visible h1.');

    for (const selector of [
      '[data-build-pwa-install-help]',
      '.build-pwa-mobile-command-menu > summary',
      '[data-build-pwa-next]',
      '[data-build-pwa-review]',
      '[data-nav-tab][aria-current="step"]'
    ]) {
      const box = await page.locator(selector).boundingBox();
      assert(box && box.height >= 44 && box.width >= 44,
        `${selector} must remain at least 44 by 44 CSS pixels in compact mode (got ${box ? `${box.width} by ${box.height}` : 'hidden'}).`);
    }

    await assertCompactInstallClearance(page, 320);
    await assertActiveStepReveal(page);

    await page.setViewportSize({ width: 1440, height: 1000 });
    await waitForLayout(page, 'workspace');
    assert(await page.locator('.build-pwa-compact-context').evaluate((element) => getComputedStyle(element).display) === 'none',
      'Saved Workspace preference did not return after widening the viewport.');

    await page.reload({ waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('.build-pwa-workspace').waitFor({ state: 'visible', timeout: 30000 });
    await waitForLayout(page, 'workspace');
    assert(await page.locator('[data-build-pwa-layout-choice="workspace"]').isChecked(),
      'Workspace override was not restored from local storage.');

    await page.locator('[data-build-pwa-layout-choice="auto"]').check();
    await waitForLayout(page, 'workspace');
    await page.setViewportSize({ width: 430, height: 900 });
    await waitForLayout(page, 'compact');
    await assertCompactInstallClearance(page, 430);
    await page.setViewportSize({ width: 1440, height: 1000 });
    await waitForLayout(page, 'workspace');

    await context.setOffline(true);
    const offlineResponse = await page.goto(
      `${baseUrl}/app?offline-isolation-proof=${Date.now()}`,
      { waitUntil: 'domcontentloaded', timeout: 15000 });
    const offlineBody = await offlineResponse.text();
    assert(offlineResponse.status() === 200, 'Build offline navigation must use its owned fallback.');
    assert(offlineBody.includes('Chummer Build PWA'),
      'Build offline navigation did not return its owned fallback document.');
    assert(!offlineBody.includes('cross-app-offline-sentinel'),
      'Build offline navigation must not read the public root cache fallback.');
    await context.setOffline(false);
  } finally {
    await context.setOffline(false).catch(() => undefined);
    await context.close();
    await browser.close();
  }
}

run()
  .then(() => console.log('Build PWA responsive layout checks passed.'))
  .catch((error) => {
    console.error(error && error.stack ? error.stack : error);
    process.exitCode = 1;
  });
