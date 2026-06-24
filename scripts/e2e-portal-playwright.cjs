#!/usr/bin/env node
'use strict';

const { chromium } = require('playwright');

const baseUrl = (process.env.CHUMMER_PORTAL_BASE_URL || 'http://127.0.0.1:8091').replace(/\/$/, '');
const expectedImplicitOwner = process.env.CHUMMER_PORTAL_EXPECTED_IMPLICIT_OWNER || 'local@self-host';
const navWaitUntil = process.env.CHUMMER_UI_NAV_WAIT_UNTIL || 'commit';
const navTimeoutMs = Number(process.env.CHUMMER_UI_NAV_TIMEOUT_MS || '15000');

function expectTextIncludes(actual, expected, context) {
  const haystack = (actual || '').toLowerCase();
  const needle = expected.toLowerCase();
  if (!haystack.includes(needle)) {
    throw new Error(`Expected ${context} to include '${expected}', got '${actual}'.`);
  }
}

async function openPortalPreview(page) {
  await page.goto(`${baseUrl}/blazor/preview`, { waitUntil: navWaitUntil, timeout: navTimeoutMs });
  await page.waitForSelector('[data-testid="startup-workbench"]', { timeout: 15000 });
  if (!page.url().includes('/blazor/preview')) {
    throw new Error(`Expected portal preview route to stay on /blazor/preview, got '${page.url()}'.`);
  }
}

async function openPortalWorkbench(page) {
  await page.goto(`${baseUrl}/blazor/workbench`, { waitUntil: navWaitUntil, timeout: navTimeoutMs });
  await page.waitForSelector('[data-testid="startup-workbench"]', { timeout: 15000 });
  if (!page.url().includes('/blazor/workbench')) {
    throw new Error(`Expected portal workbench route to stay on /blazor/workbench, got '${page.url()}'.`);
  }
}

async function openPortalBlazorRoot(page) {
  await page.goto(`${baseUrl}/blazor/`, { waitUntil: navWaitUntil, timeout: navTimeoutMs });
  await page.waitForSelector('[data-testid="startup-workbench"]', { timeout: 15000 });
  if (!page.url().includes('/blazor/workbench')) {
    throw new Error(`Expected portal /blazor/ root to resolve to /blazor/workbench, got '${page.url()}'.`);
  }
}

async function openPortalPreviewPath(page, relativePath, readySelector) {
  await page.goto(`${baseUrl}${relativePath}`, { waitUntil: navWaitUntil, timeout: navTimeoutMs });
  await page.waitForSelector(readySelector, { timeout: 30000 });
  if (!page.url().includes(relativePath.split('?')[0])) {
    throw new Error(`Expected portal preview route to stay on '${relativePath}', got '${page.url()}'.`);
  }
}

async function auditPortalHome(page) {
  await page.goto(`${baseUrl}/`, { waitUntil: navWaitUntil, timeout: navTimeoutMs });
  await page.waitForSelector('.hero, .panel', { timeout: 15000 });

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'implicit self-host sign-in', 'portal home');
  expectTextIncludes(bodyText, expectedImplicitOwner, 'portal home');
  expectTextIncludes(bodyText, 'signed owner propagation enabled', 'portal home');
  await expectVisibleSelector(page, 'a.cta[href="/blazor/workbench"]', 'portal home workbench CTA');
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

async function expectDialogFits(page, expectedTitle) {
  await page.waitForFunction((expected) => {
    const title = document.querySelector('#dialogTitle');
    return title && title.textContent && title.textContent.toLowerCase().includes(expected);
  }, expectedTitle.toLowerCase(), { timeout: 20000 });

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

async function auditPortalWorkbenchDesktop(page) {
  await openPortalPreview(page);
  await expectVisibleSelector(page, '.browser-preview-boundary', 'portal preview boundary');
  await expectVisibleSelector(page, '[data-startup-command="new_character"]', 'portal new character command');
  await expectVisibleSelector(page, '[data-startup-command="new_character_origin"]', 'portal origin dossier command');
  await expectVisibleSelector(page, '[data-startup-command="auto_alice"]', 'portal Auto ALICE command');
  await expectNoVisibleClipping(page, '[data-testid="startup-workbench"]', 'portal desktop startup workbench');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'shared workbench shell, running in the browser', 'portal desktop preview');
  expectTextIncludes(bodyText, 'Live browser workbench', 'portal desktop preview');
  expectTextIncludes(bodyText, 'Workbench route', 'portal desktop preview');
  expectTextIncludes(bodyText, 'implicit owner session posture', 'portal desktop preview');
  expectTextIncludes(bodyText, 'Origin Dossier', 'portal desktop preview');
}

async function auditPortalWorkbenchRoute(page) {
  await openPortalWorkbench(page);
  await expectVisibleSelector(page, '.browser-preview-boundary', 'portal workbench boundary');
  await expectVisibleSelector(page, '[data-testid="startup-workbench"]', 'portal workbench startup shell');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'shared workbench shell, running in the browser', 'portal workbench route');
  expectTextIncludes(bodyText, 'product-shaped browser workbench entrypoint', 'portal workbench route');
  expectTextIncludes(bodyText, 'Open preview proof shelf', 'portal workbench route');
  expectTextIncludes(bodyText, 'Start a new runner', 'portal workbench route');
  expectTextIncludes(bodyText, 'Import an existing runner', 'portal workbench route');
  expectTextIncludes(bodyText, 'Open Seeded Build Lab', 'portal workbench route');
  expectTextIncludes(bodyText, 'Continue Seeded Dossier', 'portal workbench route');
  expectTextIncludes(bodyText, 'No recent dossiers yet', 'portal workbench route');
  expectTextIncludes(bodyText, 'Continue a recent dossier', 'portal workbench route');
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

async function auditPortalBlazorRootResolvesToWorkbench(page) {
  await openPortalBlazorRoot(page);
  await expectVisibleSelector(page, '.browser-preview-boundary', 'portal blazor root workbench boundary');

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'product-shaped browser workbench entrypoint', 'portal blazor root route');
  expectTextIncludes(bodyText, 'Start a new runner', 'portal blazor root route');
}

async function auditPortalOriginDossier(page) {
  await openPortalPreview(page);
  await openStartupCommandDialog(page, 'new_character_origin', 'origin dossier');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Advanced story controls', 'portal origin dossier dialog');
  expectTextIncludes(dialogText, 'Story Preview', 'portal origin dossier dialog');
  expectTextIncludes(dialogText, 'Pick only the basics, then build the story', 'portal origin dossier dialog');
}

async function auditPortalNewCharacter(page) {
  await openPortalPreview(page);
  await openStartupCommandDialog(page, 'new_character', 'select build method');

  const dialog = page.locator('.desktop-dialog').first();
  const dialogText = await dialog.innerText();
  expectTextIncludes(dialogText, 'Character Name', 'portal new character dialog');
  expectTextIncludes(dialogText, 'Ruleset', 'portal new character dialog');
  expectTextIncludes(dialogText, 'Build Method', 'portal new character dialog');
  await expectVisibleSelector(page, '#dialogBackdrop [aria-label="Ruleset"]', 'portal ruleset field');
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
}

async function auditPortalOpenCharacterDeepLink(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?command=open_character',
    '.desktop-dialog'
  );

  await expectDialogFits(page, 'open character');

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

  await expectDialogFits(page, 'open for printing');

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

  await expectDialogFits(page, 'open for export');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Import Ruleset', 'portal open for export deep link');
  expectTextIncludes(dialogText, 'Import Source', 'portal open for export deep link');
  expectTextIncludes(dialogText, 'Review imported summary', 'portal open for export deep link');
}

async function auditPortalSeededPrintResult(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&command=print_character',
    '[data-result-dispatch="print"]'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Print preview prepared:', 'portal seeded print result');
  expectTextIncludes(bodyText, 'Browser print dispatch', 'portal seeded print result');
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
  expectTextIncludes(bodyText, 'Browser export dispatch', 'portal seeded export result');
  expectTextIncludes(bodyText, 'Last portable export', 'portal seeded export result');
}

async function auditPortalSeededSaveResult(page) {
  await openPortalPreviewPath(
    page,
    '/blazor/preview?fixture=blue&command=save_character',
    '[data-result-dispatch="save"]'
  );

  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Workspace saved.', 'portal seeded save result');
  expectTextIncludes(bodyText, 'Browser save dispatch', 'portal seeded save result');
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
  expectTextIncludes(bodyText, 'Browser download dispatch', 'portal seeded save-as result');
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
  expectTextIncludes(bodyText, 'Continue BLUE on profile', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE on rules', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE on gear', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on career log', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit career entry for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Remove career entry for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Save runner notes for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Edit runner notes for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Move career entry up for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Move career entry down for BLUE', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE on advanced', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE for download', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE for export', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Continue BLUE for print', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Resume BLUE on profile', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'Troy Simmons', 'portal workspace resume route');
  expectTextIncludes(bodyText, 'saved', 'portal workspace resume route');
  await expectVisibleSelector(page, '[data-workbench-recent-workspace]', 'portal workspace resume link');
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

  await expectDialogFits(page, 'add career entry');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Entry', 'portal restored career entry action route');
  expectTextIncludes(dialogText, 'Command Posture', 'portal restored career entry action route');
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

  await expectDialogFits(page, 'edit career entry');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Entry', 'portal restored career entry edit route');
  expectTextIncludes(dialogText, 'Command Posture', 'portal restored career entry edit route');
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

  await expectDialogFits(page, 'remove career entry');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove Current Entry', 'portal restored career entry delete route');
  expectTextIncludes(dialogText, 'Removal Scope', 'portal restored career entry delete route');
  expectTextIncludes(dialogText, 'Recovery', 'portal restored career entry delete route');
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

  await expectDialogFits(page, 'edit runner notes');

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Notes', 'portal restored runner notes route');
  expectTextIncludes(dialogText, 'Save target', 'portal restored runner notes route');
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

  await expectDialogFits(page, expectedTitle.toLowerCase());

  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `portal restored career entry reorder route ${controlId}`);
  expectTextIncludes(dialogText, 'The reordered list stays visible in the same utility pane.', `portal restored career entry reorder route ${controlId}`);
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

async function run() {
  const browser = await chromium.launch({ headless: true });

  try {
    const homePage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalHome(homePage);
    await homePage.close();

    const desktopWorkbenchPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalWorkbenchDesktop(desktopWorkbenchPage);
    await desktopWorkbenchPage.close();

    const desktopWorkbenchRoutePage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalWorkbenchRoute(desktopWorkbenchRoutePage);
    await desktopWorkbenchRoutePage.close();

    const desktopBlazorRootPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalBlazorRootResolvesToWorkbench(desktopBlazorRootPage);
    await desktopBlazorRootPage.close();

    const desktopOriginPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalOriginDossier(desktopOriginPage);
    await desktopOriginPage.close();

    const desktopNewCharacterPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalNewCharacter(desktopNewCharacterPage);
    await desktopNewCharacterPage.close();

    const desktopNewCharacterDeepLinkPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalNewCharacterDeepLink(desktopNewCharacterDeepLinkPage);
    await desktopNewCharacterDeepLinkPage.close();

    const desktopOriginDeepLinkPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalOriginDossierDeepLink(desktopOriginDeepLinkPage);
    await desktopOriginDeepLinkPage.close();

    const desktopOpenCharacterDeepLinkPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalOpenCharacterDeepLink(desktopOpenCharacterDeepLinkPage);
    await desktopOpenCharacterDeepLinkPage.close();

    const desktopOpenForPrintingDeepLinkPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalOpenForPrintingDeepLink(desktopOpenForPrintingDeepLinkPage);
    await desktopOpenForPrintingDeepLinkPage.close();

    const desktopOpenForExportDeepLinkPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalOpenForExportDeepLink(desktopOpenForExportDeepLinkPage);
    await desktopOpenForExportDeepLinkPage.close();

    const desktopSeededPrintResultPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalSeededPrintResult(desktopSeededPrintResultPage);
    await desktopSeededPrintResultPage.close();

    const desktopSeededExportResultPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalSeededExportResult(desktopSeededExportResultPage);
    await desktopSeededExportResultPage.close();

    const desktopSeededSaveResultPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalSeededSaveResult(desktopSeededSaveResultPage);
    await desktopSeededSaveResultPage.close();

    const desktopSeededSaveAsResultPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalSeededSaveAsResult(desktopSeededSaveAsResultPage);
    await desktopSeededSaveAsResultPage.close();

    const mobileWorkbenchPage = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await auditPortalWorkbenchMobile(mobileWorkbenchPage);
    await mobileWorkbenchPage.close();

    const seededBuildLabPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalSeededBuildLab(seededBuildLabPage);
    await seededBuildLabPage.close();

    const advancedWorkflowPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalAdvancedComplexForms(advancedWorkflowPage);
    await advancedWorkflowPage.close();

    const workspaceResumePage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalWorkspaceResumeRoute(workspaceResumePage);
    await workspaceResumePage.close();

    const restoredContactCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredContactAddCommitRoute(restoredContactCommitPage);
    await restoredContactCommitPage.close();

    const restoredContactActionPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredContactActionRoute(restoredContactActionPage);
    await restoredContactActionPage.close();

    const restoredCareerLogPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerLogRoute(restoredCareerLogPage);
    await restoredCareerLogPage.close();

    const restoredCareerEntryCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryAddCommitRoute(restoredCareerEntryCommitPage);
    await restoredCareerEntryCommitPage.close();

    const restoredCareerEntryActionPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryActionRoute(restoredCareerEntryActionPage);
    await restoredCareerEntryActionPage.close();

    const restoredCareerEntryEditPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryEditRoute(restoredCareerEntryEditPage);
    await restoredCareerEntryEditPage.close();

    const restoredCareerEntryDeletePage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryDeleteRoute(restoredCareerEntryDeletePage);
    await restoredCareerEntryDeletePage.close();

    const restoredCareerEntryEditCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryEditCommitRoute(restoredCareerEntryEditCommitPage);
    await restoredCareerEntryEditCommitPage.close();

    const restoredCareerEntryDeleteCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryDeleteCommitRoute(restoredCareerEntryDeleteCommitPage);
    await restoredCareerEntryDeleteCommitPage.close();

    const restoredRunnerNotesCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredRunnerNotesCommitRoute(restoredRunnerNotesCommitPage);
    await restoredRunnerNotesCommitPage.close();

    const restoredRunnerNotesPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredRunnerNotesRoute(restoredRunnerNotesPage);
    await restoredRunnerNotesPage.close();

    const restoredCareerEntryMoveUpPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryReorderRoute(restoredCareerEntryMoveUpPage, 'move_up', 'Move Entry Up');
    await restoredCareerEntryMoveUpPage.close();

    const restoredCareerEntryMoveDownPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCareerEntryReorderRoute(restoredCareerEntryMoveDownPage, 'move_down', 'Move Entry Down');
    await restoredCareerEntryMoveDownPage.close();

    const restoredComplexFormActionPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredComplexFormActionRoute(restoredComplexFormActionPage);
    await restoredComplexFormActionPage.close();

    const restoredInitiationCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredInitiationAddCommitRoute(restoredInitiationCommitPage);
    await restoredInitiationCommitPage.close();

    const restoredInitiationActionPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredInitiationActionRoute(restoredInitiationActionPage);
    await restoredInitiationActionPage.close();

    const restoredCyberwareCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCyberwareAddCommitRoute(restoredCyberwareCommitPage);
    await restoredCyberwareCommitPage.close();

    const restoredCyberwareActionPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredCyberwareActionRoute(restoredCyberwareActionPage);
    await restoredCyberwareActionPage.close();

    const restoredSpellCommitPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredSpellAddCommitRoute(restoredSpellCommitPage);
    await restoredSpellCommitPage.close();

    const restoredSpellActionPage = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await auditPortalRestoredSpellActionRoute(restoredSpellActionPage);
    await restoredSpellActionPage.close();

    console.log('portal playwright e2e completed');
  } finally {
    await browser.close();
  }
}

run().catch((error) => {
  console.error('portal playwright e2e failed:', error instanceof Error ? error.stack || error.message : error);
  process.exitCode = 1;
});
