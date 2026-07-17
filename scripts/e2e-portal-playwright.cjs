#!/usr/bin/env node
'use strict';

const { chromium } = require('playwright');

const baseUrl = (process.env.CHUMMER_PORTAL_BASE_URL || 'http://127.0.0.1:8091').replace(/\/$/, '');
const navWaitUntil = process.env.CHUMMER_UI_NAV_WAIT_UNTIL || 'commit';
const navTimeoutMs = Number(process.env.CHUMMER_UI_NAV_TIMEOUT_MS || '15000');
const routeNavigationRetryAttempts = Number(process.env.CHUMMER_PORTAL_ROUTE_RETRY_ATTEMPTS || '3');
const routeNavigationRetryDelayMs = Number(process.env.CHUMMER_PORTAL_ROUTE_RETRY_DELAY_MS || '1500');
const playwrightScope = (process.env.CHUMMER_PORTAL_PLAYWRIGHT_SCOPE || 'smoke').trim().toLowerCase();
const stagedCareerReorderRoutes = [
  '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_up',
  '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=move_down'
];

function expectTextIncludes(actual, expected, context) {
  const haystack = (actual || '').toLowerCase();
  const needle = expected.toLowerCase();
  if (!haystack.includes(needle)) {
    throw new Error(`Expected ${context} to include '${expected}', got '${actual}'.`);
  }
}

function expectAnyTextIncludes(actual, expectedValues, context) {
  for (const expected of expectedValues) {
    const haystack = (actual || '').toLowerCase();
    const needle = expected.toLowerCase();
    if (haystack.includes(needle)) {
      return;
    }
  }

  throw new Error(`Expected ${context} to include one of ${JSON.stringify(expectedValues)}, got '${actual}'.`);
}

function shouldRetryRouteNavigation(error) {
  const message = String(error && error.message || '');
  return message.includes('ERR_ABORTED') || message.includes('ERR_NETWORK_CHANGED') || message.includes('Timeout');
}

async function openPortalRoute(page, route, readySelector, waitUntilOverride) {
  let lastError = null;
  for (let attempt = 1; attempt <= routeNavigationRetryAttempts; attempt += 1) {
    try {
      await page.goto(`${baseUrl}${route}`, { waitUntil: waitUntilOverride || navWaitUntil, timeout: navTimeoutMs });
      if (readySelector) {
        await page.waitForSelector(readySelector, { timeout: 30000 });
      }
      return;
    } catch (error) {
      lastError = error;
      if (attempt >= routeNavigationRetryAttempts || !shouldRetryRouteNavigation(error)) {
        throw error;
      }

      await page.goto('about:blank', { waitUntil: 'load', timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(routeNavigationRetryDelayMs);
    }
  }

  throw lastError || new Error(`Failed to open portal route '${route}'.`);
}

async function openPortalPreview(page) {
  await openPortalRoute(page, '/blazor/preview', '[data-testid="startup-workbench"]');
  if (!page.url().includes('/blazor/preview')) {
    throw new Error(`Expected portal preview route to stay on /blazor/preview, got '${page.url()}'.`);
  }
}

async function openPortalWorkbench(page) {
  await openPortalRoute(page, '/blazor/workbench', '[data-testid="startup-workbench"]');
  if (!page.url().includes('/blazor/workbench')) {
    throw new Error(`Expected portal workbench route to stay on /blazor/workbench, got '${page.url()}'.`);
  }
}

async function openPortalBlazorRoot(page) {
  await openPortalRoute(page, '/blazor/', 'main');
  if (!page.url().includes('/blazor/')) {
    throw new Error(`Expected portal /blazor/ root to stay on /blazor/, got '${page.url()}'.`);
  }
}

async function openPortalPreviewPath(page, relativePath, readySelector, waitUntilOverride) {
  await openPortalRoute(page, relativePath, readySelector, waitUntilOverride);
  if (!page.url().includes(relativePath.split('?')[0])) {
    throw new Error(`Expected portal preview route to stay on '${relativePath}', got '${page.url()}'.`);
  }
}

async function auditPortalHome(page) {
  await openPortalRoute(page, '/', '[data-portal-home-action="explore-chummer-online"]');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Chummer', 'portal home');
  expectTextIncludes(bodyText, 'Explore Chummer Online, downloads, and support from one self-hosted edge.', 'portal home');
  expectTextIncludes(bodyText, 'Start in the Character Roster, continue into Chummer Online', 'portal home');
  expectTextIncludes(bodyText, 'Open Character Roster', 'portal home');
  expectTextIncludes(bodyText, 'Open Chummer Online overview', 'portal home');
  await expectVisibleSelector(page, 'a.cta[href="/app?command=character_roster"][data-portal-home-action="explore-chummer-online"]', 'portal home Chummer Online roster CTA');
  await expectVisibleSelector(page, '[data-portal-home-route="chummer-app"]', 'portal home Chummer Online route');
  await expectVisibleSelector(page, '[data-portal-home-route="chummer-home"]', 'portal home Chummer Online overview route');
  await expectVisibleSelector(page, '[data-portal-home-route="downloads"]', 'portal home desktop downloads route');
}

async function expectVisibleSelector(page, selector, context) {
  await page.waitForSelector(selector, { timeout: 15000, state: 'visible' });
  if (await page.locator(selector).count() < 1) {
    throw new Error(`Expected ${context} selector '${selector}' to be visible.`);
  }
}

async function expectNoVisibleClipping(page, rootSelector, context) {
  const failures = await page.locator(rootSelector).evaluate((root) => {
    function isVisible(node) {
      if (!(node instanceof Element)) {
        return false;
      }

      const style = getComputedStyle(node);
      const rect = node.getBoundingClientRect();
      return style.visibility !== 'hidden'
        && style.display !== 'none'
        && rect.width > 0
        && rect.height > 0
        && !node.closest('[hidden], [aria-hidden="true"]');
    }

    function textValue(node) {
      if (node instanceof HTMLInputElement || node instanceof HTMLTextAreaElement || node instanceof HTMLSelectElement) {
        return node.value || node.getAttribute('aria-label') || '';
      }

      return node.textContent || node.getAttribute('aria-label') || '';
    }

    return Array.from(root.querySelectorAll('button, a, h1, h2, h3, h4, h5, summary, .dialog-label, .mini-btn'))
      .filter((node) => isVisible(node) && textValue(node).trim().length > 0)
      .filter((node) => node.scrollWidth > node.clientWidth + 1 || node.scrollHeight > node.clientHeight + 1)
      .map((node) => ({
        text: textValue(node).trim().replace(/\s+/g, ' ').slice(0, 90),
        clientWidth: node.clientWidth,
        scrollWidth: node.scrollWidth,
        clientHeight: node.clientHeight,
        scrollHeight: node.scrollHeight
      }))
      .slice(0, 8);
  });

  if (failures.length > 0) {
    throw new Error(`Expected ${context} labels to fit without clipping. Samples: ${JSON.stringify(failures)}.`);
  }
}

async function expectMinimumTextContrast(page, selector, minimumRatio, context) {
  const locator = page.locator(selector).first();
  await locator.waitFor({ state: 'visible', timeout: 15000 });
  const contrast = await locator.evaluate((element) => {
    function textValue(node) {
      if (node instanceof HTMLInputElement || node instanceof HTMLTextAreaElement || node instanceof HTMLSelectElement) {
        return node.value || node.getAttribute('aria-label') || '';
      }

      return node.textContent || node.getAttribute('aria-label') || '';
    }

    function parseColor(value) {
      const match = String(value || '').match(/rgba?\(([^)]+)\)/i);
      if (!match) {
        return null;
      }

      const parts = match[1]
        .split(',')
        .map((part) => Number.parseFloat(part.trim()))
        .filter((part) => !Number.isNaN(part));
      if (parts.length < 3) {
        return null;
      }

      return {
        r: parts[0],
        g: parts[1],
        b: parts[2],
        a: parts.length >= 4 ? parts[3] : 1
      };
    }

    function composite(foreground, background) {
      const alpha = foreground.a;
      const inverse = 1 - alpha;
      const outAlpha = alpha + background.a * inverse;
      if (outAlpha <= 0) {
        return { r: 0, g: 0, b: 0, a: 0 };
      }

      return {
        r: (foreground.r * alpha + background.r * background.a * inverse) / outAlpha,
        g: (foreground.g * alpha + background.g * background.a * inverse) / outAlpha,
        b: (foreground.b * alpha + background.b * background.a * inverse) / outAlpha,
        a: outAlpha
      };
    }

    function luminance(channel) {
      const normalized = channel / 255;
      return normalized <= 0.03928
        ? normalized / 12.92
        : Math.pow((normalized + 0.055) / 1.055, 2.4);
    }

    function contrastRatio(foreground, background) {
      const foregroundLuminance = 0.2126 * luminance(foreground.r) + 0.7152 * luminance(foreground.g) + 0.0722 * luminance(foreground.b);
      const backgroundLuminance = 0.2126 * luminance(background.r) + 0.7152 * luminance(background.g) + 0.0722 * luminance(background.b);
      const lighter = Math.max(foregroundLuminance, backgroundLuminance);
      const darker = Math.min(foregroundLuminance, backgroundLuminance);
      return (lighter + 0.05) / (darker + 0.05);
    }

    function effectiveBackground(node) {
      const fallback = parseColor(getComputedStyle(document.body).backgroundColor) || { r: 255, g: 255, b: 255, a: 1 };
      let background = fallback;
      let current = node instanceof Element ? node : null;

      while (current) {
        const parsed = parseColor(getComputedStyle(current).backgroundColor);
        if (parsed && parsed.a > 0) {
          background = composite(parsed, background);
          if (background.a >= 0.999) {
            break;
          }
        }

        current = current.parentElement;
      }

      return background;
    }

    const foreground = parseColor(getComputedStyle(element).color);
    const background = effectiveBackground(element);
    if (!foreground) {
      return null;
    }

    return {
      ratio: contrastRatio(foreground, background),
      foreground: `rgba(${foreground.r}, ${foreground.g}, ${foreground.b}, ${foreground.a})`,
      background: `rgba(${Math.round(background.r)}, ${Math.round(background.g)}, ${Math.round(background.b)}, ${background.a.toFixed(3)})`,
      text: textValue(element).replace(/\s+/g, ' ').trim().slice(0, 120)
    };
  });

  if (!contrast) {
    throw new Error(`Expected ${context} to expose measurable foreground and background colors for '${selector}'.`);
  }

  if (contrast.ratio < minimumRatio) {
    throw new Error(
      `Expected ${context} to keep text contrast >= ${minimumRatio.toFixed(1)} for '${selector}', `
      + `got ${contrast.ratio.toFixed(2)} (fg ${contrast.foreground}, bg ${contrast.background}, sample '${contrast.text}').`);
  }
}

async function expectVisibleCollectionMinimumTextContrast(page, selector, minimumRatio, minimumMatches, context) {
  await page.waitForFunction((payload) => {
    const { query, requiredMatches } = payload;
    const nodes = Array.from(document.querySelectorAll(query));
    return nodes.filter((node) => {
      if (!(node instanceof Element)) {
        return false;
      }

      const style = getComputedStyle(node);
      const rect = node.getBoundingClientRect();
      return style.visibility !== 'hidden'
        && style.display !== 'none'
        && rect.width > 0
        && rect.height > 0
        && !node.closest('[hidden], [aria-hidden="true"]');
    }).length >= requiredMatches;
  }, { query: selector, requiredMatches: minimumMatches }, { timeout: 15000 });

  const evaluation = await page.locator('body').evaluate((root, payload) => {
    const { query, minimumRatioValue } = payload;

    function textValue(node) {
      if (node instanceof HTMLInputElement || node instanceof HTMLTextAreaElement || node instanceof HTMLSelectElement) {
        return node.value || node.getAttribute('aria-label') || '';
      }

      return node.textContent || node.getAttribute('aria-label') || '';
    }

    function isVisible(node) {
      if (!(node instanceof Element)) {
        return false;
      }

      const style = getComputedStyle(node);
      const rect = node.getBoundingClientRect();
      return style.visibility !== 'hidden'
        && style.display !== 'none'
        && rect.width > 0
        && rect.height > 0
        && !node.closest('[hidden], [aria-hidden="true"]');
    }

    function parseColor(value) {
      const match = String(value || '').match(/rgba?\(([^)]+)\)/i);
      if (!match) {
        return null;
      }

      const parts = match[1]
        .split(',')
        .map((part) => Number.parseFloat(part.trim()))
        .filter((part) => !Number.isNaN(part));
      if (parts.length < 3) {
        return null;
      }

      return {
        r: parts[0],
        g: parts[1],
        b: parts[2],
        a: parts.length >= 4 ? parts[3] : 1
      };
    }

    function composite(foreground, background) {
      const alpha = foreground.a;
      const inverse = 1 - alpha;
      const outAlpha = alpha + background.a * inverse;
      if (outAlpha <= 0) {
        return { r: 0, g: 0, b: 0, a: 0 };
      }

      return {
        r: (foreground.r * alpha + background.r * background.a * inverse) / outAlpha,
        g: (foreground.g * alpha + background.g * background.a * inverse) / outAlpha,
        b: (foreground.b * alpha + background.b * background.a * inverse) / outAlpha,
        a: outAlpha
      };
    }

    function luminance(channel) {
      const normalized = channel / 255;
      return normalized <= 0.03928
        ? normalized / 12.92
        : Math.pow((normalized + 0.055) / 1.055, 2.4);
    }

    function contrastRatio(foreground, background) {
      const foregroundLuminance = 0.2126 * luminance(foreground.r) + 0.7152 * luminance(foreground.g) + 0.0722 * luminance(foreground.b);
      const backgroundLuminance = 0.2126 * luminance(background.r) + 0.7152 * luminance(background.g) + 0.0722 * luminance(background.b);
      const lighter = Math.max(foregroundLuminance, backgroundLuminance);
      const darker = Math.min(foregroundLuminance, backgroundLuminance);
      return (lighter + 0.05) / (darker + 0.05);
    }

    function effectiveBackground(node) {
      const fallback = parseColor(getComputedStyle(document.body).backgroundColor) || { r: 255, g: 255, b: 255, a: 1 };
      let background = fallback;
      let current = node instanceof Element ? node : null;

      while (current) {
        const parsed = parseColor(getComputedStyle(current).backgroundColor);
        if (parsed && parsed.a > 0) {
          background = composite(parsed, background);
          if (background.a >= 0.999) {
            break;
          }
        }

        current = current.parentElement;
      }

      return background;
    }

    const nodes = Array.from(root.querySelectorAll(query))
      .filter((node) => isVisible(node) && textValue(node).trim().length > 0);

    const samples = nodes.map((node) => {
      const foreground = parseColor(getComputedStyle(node).color);
      const background = effectiveBackground(node);
      const ratio = foreground ? contrastRatio(foreground, background) : 0;
      return {
        ratio,
        text: textValue(node).replace(/\s+/g, ' ').trim().slice(0, 120),
        foreground: foreground ? `rgba(${foreground.r}, ${foreground.g}, ${foreground.b}, ${foreground.a})` : 'unparsed',
        background: `rgba(${Math.round(background.r)}, ${Math.round(background.g)}, ${Math.round(background.b)}, ${background.a.toFixed(3)})`
      };
    });

    return {
      count: samples.length,
      failing: samples.filter((sample) => sample.ratio < minimumRatioValue).slice(0, 8)
    };
  }, { query: selector, minimumRatioValue: minimumRatio });

  if (evaluation.count < minimumMatches) {
    throw new Error(`Expected ${context} to expose at least ${minimumMatches} visible matches for '${selector}', got ${evaluation.count}.`);
  }

  if (evaluation.failing.length > 0) {
    throw new Error(
      `Expected ${context} to keep every visible '${selector}' contrast >= ${minimumRatio.toFixed(1)}. `
      + `Failing samples: ${JSON.stringify(evaluation.failing)}.`);
  }
}

async function expectDialogFits(page, expectedTitle, expectedFallback) {
  await page.waitForFunction((payload) => {
    const expected = String(payload?.expected || '');
    const fallback = String(payload?.fallback || '');
    const title = document.querySelector('#dialogTitle');
    const dialogText = document.querySelector('.desktop-dialog');
    const candidate = (title && title.textContent || '').toLowerCase();
    const bodyText = (dialogText && dialogText.textContent || '').toLowerCase();

    return candidate.includes(expected) || (fallback && bodyText.includes(fallback)) || bodyText.includes(expected);
  }, {
    expected: expectedTitle.toLowerCase(),
    fallback: expectedFallback ? expectedFallback.toLowerCase() : ''
  }, { timeout: 20000 });

  const dialog = page.locator('.desktop-dialog').first();
  await dialog.waitFor({ state: 'visible', timeout: 15000 });
  await page.evaluate(() => window.chummerDialogs?.revealActiveDialog?.());
  await page.waitForTimeout(100);

  const box = await dialog.evaluate((element) => {
    if (!(element instanceof HTMLElement)) {
      return null;
    }

    const rect = element.getBoundingClientRect();
    return {
      x: rect.x,
      y: rect.y,
      width: rect.width,
      height: rect.height
    };
  });
  const viewport = page.viewportSize() || { width: 1280, height: 720 };
  if (!box) {
    throw new Error(`Expected '${expectedTitle}' dialog to have a measurable box.`);
  }

  const edgeSlack = 2;
  if (box.x < -edgeSlack
    || box.y < -edgeSlack
    || box.x + box.width > viewport.width + edgeSlack
    || box.y + box.height > viewport.height + edgeSlack) {
    throw new Error(`Expected '${expectedTitle}' dialog to fit inside ${viewport.width}x${viewport.height}, got ${JSON.stringify(box)}.`);
  }

  await expectNoVisibleClipping(page, '.desktop-dialog', `${expectedTitle} dialog`);
}

async function openStartupCommandDialog(page, commandId, expectedTitle) {
  const selector = `[data-startup-command="${commandId}"]`;
  await expectVisibleSelector(page, selector, `${commandId} startup command`);

  for (let attempt = 1; attempt <= 3; attempt += 1) {
    await page.locator(selector).evaluate((element) => {
      if (element instanceof HTMLElement) {
        element.click();
      }
    });
    try {
      await expectDialogFits(page, expectedTitle);
      return;
    } catch (error) {
      if (attempt >= 3) {
        throw error;
      }

      await page.waitForTimeout(1500);
    }
  }
}

async function expectNewRunnerMenuReopensDialog(page, context) {
  const buildMethod = page.locator('label[data-field-id="newCharacterBuildMethod"] select');
  await buildMethod.waitFor({ state: 'visible', timeout: 15000 });
  await buildMethod.selectOption('Karma');
  const buildMethodValue = await buildMethod.inputValue();
  if (buildMethodValue !== 'Karma') {
    throw new Error(`Expected ${context} Build Method to switch to Karma before using File -> New runner, got '${buildMethodValue}'.`);
  }

  const fileMenu = page.locator('button.menu-btn.classic-menu-button').filter({ hasText: 'File' }).first();
  await fileMenu.waitFor({ state: 'visible', timeout: 15000 });
  await fileMenu.click({ timeout: 15000 });
  const fileMenuExpandedState = await fileMenu.evaluate((element) => ({
    ariaExpanded: element.getAttribute('aria-expanded') || '',
    className: element.getAttribute('class') || ''
  }));
  const fileMenuExpanded = fileMenuExpandedState.ariaExpanded === 'true'
    || fileMenuExpandedState.className.split(/\s+/).includes('active');
  if (!fileMenuExpanded) {
    throw new Error(
      `Expected ${context} File menu to expand while the startup dialog is open, got `
      + `aria-expanded='${fileMenuExpandedState.ariaExpanded}' class='${fileMenuExpandedState.className}'.`);
  }

  const newRunner = page.locator('button.menu-item.classic-menu-item').filter({ hasText: 'New runner' }).first();
  await newRunner.waitFor({ state: 'visible', timeout: 15000 });
  await newRunner.click({ timeout: 15000 });

  const buildMethodReset = await buildMethod.inputValue();
  if (buildMethodReset !== 'Priority') {
    throw new Error(`Expected ${context} File -> New runner to reopen the startup dialog with Priority selected, got '${buildMethodReset}'.`);
  }

  const dialogVisible = await page.locator('#dialogBackdrop[data-dialog-id="dialog.new_character"]').isVisible();
  if (!dialogVisible) {
    throw new Error(`Expected ${context} startup dialog to remain visible after File -> New runner.`);
  }

  const fileMenuCollapsedState = await fileMenu.evaluate((element) => ({
    ariaExpanded: element.getAttribute('aria-expanded') || '',
    className: element.getAttribute('class') || ''
  }));
  const fileMenuCollapsed = fileMenuCollapsedState.ariaExpanded === 'false'
    || !fileMenuCollapsedState.className.split(/\s+/).includes('active');
  if (!fileMenuCollapsed) {
    throw new Error(
      `Expected ${context} File menu to collapse after selecting New runner, got `
      + `aria-expanded='${fileMenuCollapsedState.ariaExpanded}' class='${fileMenuCollapsedState.className}'.`);
  }
}

async function auditPortalWorkbenchDesktop(page) {
  await openPortalPreview(page);
  await expectVisibleSelector(page, '.browser-preview-boundary', 'portal preview boundary');
  await expectVisibleSelector(page, '[data-startup-command="new_character"]', 'portal new character command');
  await expectVisibleSelector(page, '[data-startup-command="new_character_origin"]', 'portal origin dossier command');
  await expectVisibleSelector(page, '[data-startup-command="auto_alice"]', 'portal Auto ALICE command');
  await expectNoVisibleClipping(page, '[data-testid="startup-workbench"]', 'portal desktop startup workbench');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Preview Chummer Online workflows without changing the public route.', 'portal desktop preview');
  expectTextIncludes(bodyText, 'Preview route tools', 'portal desktop preview');
  expectTextIncludes(bodyText, 'Compatibility route', 'portal desktop preview');
  expectTextIncludes(bodyText, 'implicit owner session posture', 'portal desktop preview');
  expectTextIncludes(bodyText, 'Origin Dossier', 'portal desktop preview');
}

async function auditPortalWorkbenchRoute(page) {
  await openPortalWorkbench(page);
  await expectVisibleSelector(page, '.browser-preview-boundary', 'portal workbench boundary');
  await expectVisibleSelector(page, '[data-testid="startup-workbench"]', 'portal workbench startup shell');
  await expectVisibleSelector(page, '.classic-compat-app [data-chummer-classic-shell][data-route-family="compatibility"]', 'portal workbench compatibility shell');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Chummer Online compatibility shell, running in the browser.', 'portal workbench route');
  expectTextIncludes(bodyText, 'older browser links alive', 'portal workbench route');
  expectTextIncludes(bodyText, 'Preview tools', 'portal workbench route');
  expectTextIncludes(bodyText, 'Start a new runner', 'portal workbench route');
  expectTextIncludes(bodyText, 'Import runner XML', 'portal workbench route');
  expectTextIncludes(bodyText, 'Open Seeded Build Lab', 'portal workbench route');
  expectTextIncludes(bodyText, 'Continue Seeded Dossier', 'portal workbench route');
  expectTextIncludes(bodyText, 'Saved Runners', 'portal workbench route');
  expectTextIncludes(bodyText, 'Active Table', 'portal workbench route');
  expectTextIncludes(bodyText, 'Review identity and profile', 'portal workbench route');
  expectTextIncludes(bodyText, 'Review rules and references', 'portal workbench route');
  expectTextIncludes(bodyText, 'Review loadout and gear', 'portal workbench route');
  expectTextIncludes(bodyText, 'Open advanced build lanes', 'portal workbench route');
  expectTextIncludes(bodyText, 'Prepare a browser download', 'portal workbench route');
  expectTextIncludes(bodyText, 'Prepare an export package', 'portal workbench route');
  expectTextIncludes(bodyText, 'Prepare a print preview', 'portal workbench route');
  await expectVisibleSelector(page, '[data-workbench-entry-card="new-character"]', 'portal workbench new character entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="open-character"]', 'portal workbench open character entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="continue-recent"]', 'portal workbench continue recent entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="profile"]', 'portal workbench profile entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="rules"]', 'portal workbench rules entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="gear"]', 'portal workbench gear entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="technomancer"]', 'portal workbench technomancer entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="save-as"]', 'portal workbench save-as entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="export"]', 'portal workbench export entry card');
  await expectVisibleSelector(page, '[data-workbench-entry-card="print"]', 'portal workbench print entry card');
  expectTextIncludes(bodyText, 'Published self-hosted Docker surface', 'portal workbench route');
}

async function auditPortalBlazorRootResolvesToApp(page) {
  await openPortalBlazorRoot(page);
  await expectVisibleSelector(page, 'main .button[href="/downloads"]', 'portal blazor root downloads CTA');
  await expectVisibleSelector(page, 'main .button.muted[href="/status"]', 'portal blazor root status CTA');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Browser preview is not ready right now.', 'portal blazor root route');
  expectTextIncludes(bodyText, 'The downloadable Chummer client is the current stable path.', 'portal blazor root route');
  expectTextIncludes(bodyText, 'Download Chummer', 'portal blazor root route');
  expectTextIncludes(bodyText, 'Status', 'portal blazor root route');
}

async function auditPortalOriginDossier(page) {
  await openPortalPreview(page);
  await openStartupCommandDialog(page, 'new_character_origin', 'origin dossier');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Advanced story controls', 'portal origin dossier dialog');
  expectTextIncludes(dialogText, 'Story Preview', 'portal origin dossier dialog');
  expectTextIncludes(dialogText, 'Pick only the basics, then build the story', 'portal origin dossier dialog');
  await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 2, 'portal origin dossier labels');
  await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 2, 'portal origin dossier inputs');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-panel > header p', 4.5, 2, 'portal origin dossier helper copy');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-label', 4.5, 3, 'portal origin dossier summary labels');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin dossier summary values');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-story-preview] .dialog-origin-narrative p', 4.5, 1, 'portal origin dossier story preview');
}

async function auditPortalNewCharacter(page) {
  await openPortalPreview(page);
  await openStartupCommandDialog(page, 'new_character', 'select build method');

  const dialog = page.locator('.desktop-dialog').first();
  const dialogText = await dialog.innerText();
  expectTextIncludes(dialogText, 'Character Name', 'portal new character dialog');
  expectTextIncludes(dialogText, 'Ruleset', 'portal new character dialog');
  expectTextIncludes(dialogText, 'Build Method', 'portal new character dialog');
  await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 3, 'portal new character labels');
  await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 3, 'portal new character inputs');
  await expectVisibleSelector(page, '#dialogBackdrop [aria-label="Ruleset"]', 'portal ruleset field');
  await expectNewRunnerMenuReopensDialog(page, 'portal new character dialog');
}

async function auditPortalNewCharacterDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=new_character',
    '#dialogBackdrop [aria-label="Ruleset"]'
  );

  await expectDialogFits(page, 'select build method');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Character Name', 'portal new character deep link');
  expectTextIncludes(dialogText, 'Build Method', 'portal new character deep link');
  await expectNewRunnerMenuReopensDialog(page, 'portal new character deep link');
}

async function auditPortalOriginDossierDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=new_character_origin',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'origin dossier');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Advanced story controls', 'portal origin dossier deep link');
  expectTextIncludes(dialogText, 'Story Preview', 'portal origin dossier deep link');
  await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-label', 4.5, 2, 'portal origin dossier deep-link labels');
  await expectVisibleCollectionMinimumTextContrast(page, '.desktop-dialog .dialog-input', 4.5, 2, 'portal origin dossier deep-link inputs');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-panel > header p', 4.5, 2, 'portal origin dossier deep-link helper copy');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-label', 4.5, 3, 'portal origin dossier deep-link summary labels');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-wizard] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin dossier deep-link summary values');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-story-preview] .dialog-origin-narrative p', 4.5, 1, 'portal origin dossier deep-link story preview');
}

async function auditPortalOriginBuildDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=new_character_origin&dialog_action=generate_fitting_build',
    '[data-origin-build]'
  );

  await expectDialogFits(page, 'origin build handoff', 'build handoff');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Build Handoff', 'portal origin build deep link');
  expectTextIncludes(dialogText, 'Book Preview', 'portal origin build deep link');
  expectTextIncludes(dialogText, 'Build Translation', 'portal origin build deep link');
  expectTextIncludes(dialogText, 'Start character creation', 'portal origin build deep link');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-panel > header p', 4.5, 3, 'portal origin build helper copy');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-summary-label', 4.5, 3, 'portal origin build summary labels');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-summary-card strong', 4.5, 3, 'portal origin build summary values');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-book-preview] .dialog-origin-readonly p', 4.5, 2, 'portal origin build book preview');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build] .dialog-origin-preview .dialog-origin-narrative p', 4.5, 1, 'portal origin build story preview');
  await expectVisibleCollectionMinimumTextContrast(page, '[data-origin-build-support] .dialog-visual-pre', 4.5, 2, 'portal origin build supporting previews');
}

async function auditPortalOpenCharacterDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=open_character',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'open runner');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Ruleset', 'portal open character deep link');
  expectTextIncludes(dialogText, 'Grounded explain receipt', 'portal open character deep link');
}

async function auditPortalOpenForPrintingDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=open_for_printing',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'open runner for printing');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Import Ruleset', 'portal open for printing deep link');
  expectTextIncludes(dialogText, 'Import Source', 'portal open for printing deep link');
  expectTextIncludes(dialogText, 'Review imported summary', 'portal open for printing deep link');
}

async function auditPortalOpenForExportDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=open_for_export',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'open runner for export');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Import Ruleset', 'portal open for export deep link');
  expectTextIncludes(dialogText, 'Import Source', 'portal open for export deep link');
  expectTextIncludes(dialogText, 'Review imported summary', 'portal open for export deep link');
}

async function auditPortalSeededPrintResult(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&command=print_character',
    '[data-result-dispatch="print"]',
    'domcontentloaded'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Print preview prepared:', 'portal seeded print result');
  expectTextIncludes(bodyText, 'Print file ready', 'portal seeded print result');
  expectTextIncludes(bodyText, 'Troy Simmons', 'portal seeded print result');
}

async function auditPortalSeededExportResult(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&command=export_character&dialog_action=download',
    '[data-result-dispatch="export"]'
  );

  await expectVisibleSelector(page, '[data-result-trust-receipt]', 'portal seeded export trust receipt');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Portable export ready:', 'portal seeded export result');
  expectTextIncludes(bodyText, 'Export ready', 'portal seeded export result');
  expectTextIncludes(bodyText, 'Last portable export', 'portal seeded export result');
}

async function auditPortalSeededSaveResult(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&command=save_character',
    '[data-result-dispatch="save"]'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Dossier saved.', 'portal seeded save result');
  expectTextIncludes(bodyText, 'Saved in this browser', 'portal seeded save result');
  expectTextIncludes(bodyText, 'save_character', 'portal seeded save result');
}

async function auditPortalSeededSaveAsResult(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&command=save_character_as',
    '[data-result-dispatch="download"]'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Download prepared:', 'portal seeded save-as result');
  expectTextIncludes(bodyText, 'Download ready', 'portal seeded save-as result');
}

async function auditPortalWorkbenchMobile(page) {
  await openPortalPreview(page);
  await expectVisibleSelector(page, '[data-testid="startup-workbench"]', 'portal mobile startup workbench');
  await expectVisibleSelector(page, '[data-startup-command="new_character_origin"]', 'portal mobile origin dossier command');
  await expectNoVisibleClipping(page, '[data-testid="startup-workbench"]', 'portal mobile startup workbench');

  await openStartupCommandDialog(page, 'new_character_origin', 'origin dossier');
}

async function auditPortalSeededBuildLab(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Build Lab Intake', 'portal seeded build lab');
  expectTextIncludes(bodyText, 'Troy Simmons', 'portal seeded build lab');
  expectTextIncludes(bodyText, 'BLUE', 'portal seeded build lab');
  await expectVisibleSelector(page, '[data-build-lab]', 'portal build lab workspace');
}

async function auditPortalAdvancedComplexForms(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-technomancer',
    '[data-section-quick-action="complex_form_add"]'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Complex Forms', 'portal complex forms workflow');
  expectTextIncludes(bodyText, 'Add Complex Form', 'portal complex forms workflow');
  const summaryName = await page.locator('#summaryName').inputValue();
  expectTextIncludes(summaryName, 'Troy Simmons', 'portal complex forms summary');
  await expectVisibleSelector(page, '[data-section-quick-action="complex_form_add"]', 'portal complex form add action');
}

async function auditPortalWorkspaceResumeRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1',
    '#summaryName'
  );

  const summaryName = await page.locator('#summaryName').inputValue();
  expectTextIncludes(summaryName, 'Troy Simmons', 'portal workspace resume summary');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Resume from restored session state', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE in build lab', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE on contacts', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on profile', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on rules', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on gear', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on career log', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit career entry for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Remove career entry for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Save dossier notes for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit dossier notes for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Move career entry up for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Move career entry down for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add SIN/license for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit SIN/license for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Remove SIN/license for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add armor for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Reload weapon for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Review damage track for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Specialize skill for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Remove skill for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit skill group for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add adept power for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add spirit for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add critter power for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add Matrix program for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add gear for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit gear for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Remove gear for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Show source for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Show gear source for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Mount gear for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Toggle gear free/paid for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Add general magic item for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Bind spirit for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Show magic source for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Remove drug for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on advanced', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Prepare BLUE browser download', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Download BLUE from browser', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Prepare BLUE export package', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Download BLUE export package', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Prepare BLUE print preview', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Troy Simmons', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'saved', 'portal workspace resume route');
  await expectVisibleSelector(page, '[data-workbench-recent-workspace]', 'portal dossier resume link');
}

async function auditPortalRestoredContactActionRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'add contact');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Street Doc', 'portal restored contact action route');
  expectTextIncludes(dialogText, 'Connection/Loyalty', 'portal restored contact action route');
}

async function auditPortalRestoredContactAddCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Dr. Mercy', 'portal restored contact commit route');
}

async function auditPortalRestoredCareerLogRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar',
    '.section-preview > h2'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Career Log', 'portal restored career log route');
  expectTextIncludes(bodyText, 'Add Entry', 'portal restored career log route');
}

async function auditPortalRestoredCareerEntryActionRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'add entry');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Entry', 'portal restored career entry action route');
  expectTextIncludes(dialogText, 'Add a new entry', 'portal restored career entry action route');
  expectTextIncludes(dialogText, 'Entry Title', 'portal restored career entry action route');
}

async function auditPortalRestoredCareerEntryAddCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=create_entry&dialog_action=add',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, "Entry 'New entry' added.", 'portal restored career entry commit route');
}

async function auditPortalRestoredCareerEntryEditRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'edit entry');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Entry', 'portal restored career entry edit route');
  expectTextIncludes(dialogText, 'Edit the selected entry', 'portal restored career entry edit route');
  expectTextIncludes(dialogText, 'Entry Title', 'portal restored career entry edit route');
}

async function auditPortalRestoredCareerEntryDeleteRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'remove current entry');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove Current Entry', 'portal restored career entry delete route');
  expectTextIncludes(dialogText, 'Remove Current Entry from the active list?', 'portal restored career entry delete route');
}

async function auditPortalRestoredCareerEntryEditCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=edit_entry&dialog_action=apply',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, "Entry renamed to 'Current Entry'.", 'portal restored career entry edit commit route');
}

async function auditPortalRestoredCareerEntryDeleteCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=delete_entry&dialog_action=delete',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, "Entry 'Current Entry' removed.", 'portal restored career entry delete commit route');
}

async function auditPortalRestoredRunnerNotesRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'edit notes');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Notes', 'portal restored runner notes route');
  expectTextIncludes(dialogText, 'Edit runner notes in a compact text utility pane.', 'portal restored runner notes route');
}

async function auditPortalRestoredRunnerNotesCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-info&control=open_notes&dialog_action=save',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Notes saved.', 'portal restored runner notes commit route');
}

async function auditPortalRestoredCareerEntryReorderRoute(page, controlId, expectedTitle) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=tab-calendar&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase()
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored career entry reorder route ${controlId}`);
  expectTextIncludes(dialogText, 'The reordered list stays visible in the same utility pane.', `portal restored career entry reorder route ${controlId}`);
}

async function auditPortalRestoredMagicCleanupUtilityRoute(page, tabId, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=${tabId}&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(page, expectedTitle.toLowerCase(), expectedMarker.toLowerCase());

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored magic cleanup utility route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored magic cleanup utility route ${controlId}`);
}

async function auditPortalRestoredSourceGearUtilityRoute(page, tabId, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=${tabId}&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase(),
    expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored source/gear utility route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored source/gear utility route ${controlId}`);
}

async function auditPortalRestoredGearMaintenanceRoute(page, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=tab-gear&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase(),
    expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored gear maintenance route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored gear maintenance route ${controlId}`);
}

async function auditPortalRestoredMagicSupportRoute(page, tabId, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=${tabId}&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase(),
    expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored magic support route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored magic support route ${controlId}`);
}

async function auditPortalRestoredSkillMaintenanceRoute(page, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=tab-skills&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase(),
    expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored skill maintenance route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored skill maintenance route ${controlId}`);
}

async function auditPortalRestoredCombatSupportRoute(page, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=tab-combat&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase(),
    expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored combat support route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored combat support route ${controlId}`);
}

async function auditPortalRestoredIdentityLicenseRoute(page, controlId, expectedTitle, expectedMarker) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    `/blazor/workbench?workspace=ws-1&tab=tab-info&control=${controlId}`,
    '.desktop-dialog'
  );

  await expectDialogFits(
    page,
    expectedTitle.toLowerCase(),
    expectedMarker?.toLowerCase ? expectedMarker.toLowerCase() : undefined
  );

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored identity/license route ${controlId}`);
  expectTextIncludes(dialogText, expectedMarker, `portal restored identity/license route ${controlId}`);
  expectTextIncludes(dialogText, 'lifestyle', `portal restored identity/license route ${controlId}`);
}

async function auditPortalRestoredComplexFormActionRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'add complex form');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Cleaner', 'portal restored complex form action route');
  expectTextIncludes(dialogText, 'Data Trails', 'portal restored complex form action route');
}

async function auditPortalRestoredInitiationActionRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-adept&control=initiation_add',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'add initiation');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Masking', 'portal restored initiation action route');
  expectTextIncludes(dialogText, 'Grade', 'portal restored initiation action route');
}

async function auditPortalRestoredInitiationAddCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-adept&control=initiation_add&dialog_action=add',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, "Initiation/submersion reward 'Masking' added", 'portal restored initiation commit route');
}

async function auditPortalRestoredCyberwareActionRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-cyberware&control=cyberware_add',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'add cyberware');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Wired Reflexes 2', 'portal restored cyberware action route');
  expectTextIncludes(dialogText, 'Essence', 'portal restored cyberware action route');
}

async function auditPortalRestoredCyberwareAddCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-cyberware&control=cyberware_add&dialog_action=add',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Wired Reflexes 2', 'portal restored cyberware commit route');
}

async function auditPortalRestoredSpellActionRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-magician&control=spell_add',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'add spell');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Stunbolt', 'portal restored spell action route');
  expectTextIncludes(dialogText, 'Drain', 'portal restored spell action route');
}

async function auditPortalRestoredSpellAddCommitRoute(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&tab=tab-create',
    '[data-build-lab]'
  );

  await openPortalPreviewPath(
    page,
    '/blazor/workbench?workspace=ws-1&tab=tab-magician&control=spell_add&dialog_action=add',
    '#summaryName'
  );

  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Stunbolt', 'portal restored spell commit route');
}

const desktopViewport = { width: 1440, height: 960 };
const mobileViewport = { width: 390, height: 844 };

async function runAuditSequence(browser, audits) {
  for (let index = 0; index < audits.length; index += 1) {
    const audit = audits[index];
    const auditLabel = audit.label
      || `${audit.fn.name}${audit.args && audit.args.length > 0 ? `(${audit.args.join(', ')})` : ''}`;
    console.log(`[${index + 1}/${audits.length}] start ${auditLabel}`);
    const page = await browser.newPage({ viewport: audit.viewport || desktopViewport });
    try {
      await audit.fn(page, ...(audit.args || []));
      console.log(`[${index + 1}/${audits.length}] done ${auditLabel}`);
    } finally {
      await page.close().catch(() => {});
    }
  }
}

const smokeAudits = [
  { fn: auditPortalHome },
  { fn: auditPortalWorkbenchDesktop },
  { fn: auditPortalWorkbenchRoute },
  { fn: auditPortalBlazorRootResolvesToApp },
  { fn: auditPortalOriginDossier },
  { fn: auditPortalNewCharacter },
  { fn: auditPortalOpenCharacterDeepLink },
  { fn: auditPortalOpenForPrintingDeepLink },
  { fn: auditPortalOpenForExportDeepLink },
  { fn: auditPortalWorkbenchMobile, viewport: mobileViewport },
  { fn: auditPortalSeededBuildLab },
  { fn: auditPortalAdvancedComplexForms },
  { fn: auditPortalWorkspaceResumeRoute },
  { fn: auditPortalRestoredContactAddCommitRoute },
  { fn: auditPortalRestoredCareerEntryAddCommitRoute },
  { fn: auditPortalRestoredRunnerNotesCommitRoute },
  { fn: auditPortalRestoredSourceGearUtilityRoute, args: ['tab-info', 'show_source', 'Source', 'Source'] },
  { fn: auditPortalRestoredGearMaintenanceRoute, args: ['gear_add', 'Add Gear', 'Browse the catalog'] },
  { fn: auditPortalRestoredSkillMaintenanceRoute, args: ['skill_specialize', 'Specialization', 'Specialization'] },
  { fn: auditPortalRestoredCombatSupportRoute, args: ['combat_add_armor', 'Add Armor', 'Armor'] },
  { fn: auditPortalRestoredIdentityLicenseRoute, args: ['identity_license_add', 'Add SIN / License', 'Legal status'] },
  { fn: auditPortalRestoredSpellActionRoute },
];

const fullOnlyAudits = [
  { fn: auditPortalNewCharacterDeepLink },
  { fn: auditPortalOriginDossierDeepLink },
  { fn: auditPortalOriginBuildDeepLink },
  { fn: auditPortalSeededPrintResult },
  { fn: auditPortalSeededExportResult },
  { fn: auditPortalSeededSaveResult },
  { fn: auditPortalSeededSaveAsResult },
  { fn: auditPortalRestoredContactActionRoute },
  { fn: auditPortalRestoredCareerLogRoute },
  { fn: auditPortalRestoredCareerEntryActionRoute },
  { fn: auditPortalRestoredCareerEntryEditRoute },
  { fn: auditPortalRestoredCareerEntryDeleteRoute },
  { fn: auditPortalRestoredCareerEntryEditCommitRoute },
  { fn: auditPortalRestoredCareerEntryDeleteCommitRoute },
  { fn: auditPortalRestoredRunnerNotesRoute },
  { fn: auditPortalRestoredCareerEntryReorderRoute, args: ['move_up', 'Move Entry Up'] },
  { fn: auditPortalRestoredCareerEntryReorderRoute, args: ['move_down', 'Move Entry Down'] },
  { fn: auditPortalRestoredMagicCleanupUtilityRoute, args: ['tab-magician', 'magic_add', 'Magic', 'Magic'] },
  { fn: auditPortalRestoredMagicCleanupUtilityRoute, args: ['tab-magician', 'magic_bind', 'Bind', 'Bind'] },
  { fn: auditPortalRestoredMagicCleanupUtilityRoute, args: ['tab-magician', 'magic_source', 'Source', 'Source'] },
  { fn: auditPortalRestoredMagicCleanupUtilityRoute, args: ['tab-gear', 'drug_delete', 'Remove', 'Remove'] },
  { fn: auditPortalRestoredSourceGearUtilityRoute, args: ['tab-gear', 'gear_source', 'Source', 'Source'] },
  { fn: auditPortalRestoredSourceGearUtilityRoute, args: ['tab-gear', 'gear_mount', 'Mount', 'Mount'] },
  { fn: auditPortalRestoredSourceGearUtilityRoute, args: ['tab-gear', 'toggle_free_paid', 'Free', 'Free'] },
  { fn: auditPortalRestoredGearMaintenanceRoute, args: ['gear_edit', 'Edit Gear', 'Edit'] },
  { fn: auditPortalRestoredGearMaintenanceRoute, args: ['gear_delete', 'Remove Armor Jacket', 'Removal Scope'] },
  { fn: auditPortalRestoredMagicSupportRoute, args: ['tab-adept', 'adept_power_add', 'Power', 'Power'] },
  { fn: auditPortalRestoredMagicSupportRoute, args: ['tab-magician', 'spirit_add', 'Spirit', 'Spirit'] },
  { fn: auditPortalRestoredMagicSupportRoute, args: ['tab-critter', 'critter_power_add', 'Power', 'Critter'] },
  { fn: auditPortalRestoredMagicSupportRoute, args: ['tab-technomancer', 'matrix_program_add', 'Program', 'Program'] },
  { fn: auditPortalRestoredSkillMaintenanceRoute, args: ['skill_remove', 'Remove Perception', 'Removal Scope'] },
  { fn: auditPortalRestoredSkillMaintenanceRoute, args: ['skill_group', 'Skill Group', 'Group composition and current rating remain visible while editing.'] },
  { fn: auditPortalRestoredCombatSupportRoute, args: ['combat_reload', 'Reload', 'Weapon and ammo selection remain visible while reloading.'] },
  { fn: auditPortalRestoredCombatSupportRoute, args: ['combat_damage_track', 'Damage Track', 'Current track state remains visible before applying the damage step.'] },
  { fn: auditPortalRestoredIdentityLicenseRoute, args: ['identity_license_edit', 'Edit SIN / License', 'Attached Context'] },
  { fn: auditPortalRestoredIdentityLicenseRoute, args: ['identity_license_delete', 'Remove SIN / License', 'Removal Impact'] },
  { fn: auditPortalRestoredComplexFormActionRoute },
  { fn: auditPortalRestoredInitiationAddCommitRoute },
  { fn: auditPortalRestoredInitiationActionRoute },
  { fn: auditPortalRestoredCyberwareAddCommitRoute },
  { fn: auditPortalRestoredCyberwareActionRoute },
  { fn: auditPortalRestoredSpellAddCommitRoute },
];

async function runSelectedAuditScope(browser) {
  const normalizedScope = playwrightScope === 'full' ? 'full' : 'smoke';
  console.log(`portal playwright scope: ${normalizedScope}`);
  await runAuditSequence(browser, smokeAudits);
  if (normalizedScope === 'full') {
    await runAuditSequence(browser, fullOnlyAudits);
  }
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  try {
    await runSelectedAuditScope(browser);
    console.log('portal playwright e2e completed');
  } finally {
    await browser.close();
  }
}

run().catch((error) => {
  console.error('portal playwright e2e failed:', error instanceof Error ? error.stack || error.message : error);
  process.exitCode = 1;
});
