#!/usr/bin/env node
'use strict';

const { chromium } = require('playwright');

const UI_URL = process.env.CHUMMER_BLAZOR_BASE_URL || 'http://127.0.0.1:8089';
const SAMPLE_CHARACTER_FILE = process.env.CHUMMER_UI_SAMPLE_FILE || '/work/testdata/BLUE.chum5';
const NAVIGATION_WAIT_UNTIL = process.env.CHUMMER_UI_NAV_WAIT_UNTIL || 'commit';
const ROOT_NAV_TIMEOUT_MS = Number(process.env.CHUMMER_UI_NAV_TIMEOUT_MS || '15000');
const ROOT_NAV_RETRY_ATTEMPTS = Number(process.env.CHUMMER_UI_NAV_RETRY_ATTEMPTS || '4');
const ROOT_NAV_RETRY_DELAY_MS = Number(process.env.CHUMMER_UI_NAV_RETRY_DELAY_MS || '2000');

const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));

async function openRootWithRetry(page) {
  let lastError = null;
  for (let attempt = 1; attempt <= ROOT_NAV_RETRY_ATTEMPTS; attempt += 1) {
    try {
      await page.goto(`${UI_URL}/`, { waitUntil: NAVIGATION_WAIT_UNTIL, timeout: ROOT_NAV_TIMEOUT_MS });
      return;
    } catch (error) {
      lastError = error;
      if (attempt >= ROOT_NAV_RETRY_ATTEMPTS) {
        break;
      }

      // Service startup can lag briefly in containerized runs; retry before failing the suite.
      await delay(ROOT_NAV_RETRY_DELAY_MS);
    }
  }

  throw lastError || new Error(`Unable to open ${UI_URL}/`);
}

async function openNewCharacterDialog(page) {
  const newCharacterButton = page.locator('[data-startup-command="new_character"]').first();
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    await newCharacterButton.click();
    try {
      await page.waitForSelector('#dialogTitle', { timeout: 5000 });
      return;
    } catch (error) {
      if (attempt >= 3) {
        throw error;
      }

      await delay(1000);
    }
  }
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  try {
    await openRootWithRetry(page);
    await page.waitForSelector('[data-testid="startup-workbench"]', { timeout: 15000 });
    await openNewCharacterDialog(page);

    let dialogTitle = (await page.locator('#dialogTitle').textContent()) || '';
    if (!dialogTitle.toLowerCase().includes('select build method')) {
      throw new Error(`Expected build-method dialog, got '${dialogTitle}'.`);
    }

    const dialog = page.locator('#dialogBackdrop');
    await dialog.getByLabel('Character Name').fill('Playwright Runner');
    await dialog.getByLabel('Alias').fill('PW');

    const rulesetInput = dialog.locator('input[aria-label="Ruleset"]').first();
    const buildMethodInput = dialog.locator('input[aria-label="Build Method"]').first();
    if ((await rulesetInput.inputValue()).trim().toLowerCase() !== 'sr5') {
      throw new Error(`Expected default ruleset 'sr5', got '${await rulesetInput.inputValue()}'.`);
    }

    if ((await buildMethodInput.inputValue()).trim().toLowerCase() !== 'priority') {
      throw new Error(`Expected default build method 'priority', got '${await buildMethodInput.inputValue()}'.`);
    }

    await dialog.getByRole('button', { name: 'OK' }).click();
    await page.waitForFunction(() => {
      const title = document.querySelector('#dialogTitle');
      return title && title.textContent && title.textContent.toLowerCase().includes('select metatype priority');
    }, { timeout: 15000 });

    dialogTitle = (await page.locator('#dialogTitle').textContent()) || '';
    if (!dialogTitle.toLowerCase().includes('select metatype priority')) {
      throw new Error(`Expected metatype-priority dialog, got '${dialogTitle}'.`);
    }

    await page.locator('#dialogBackdrop').getByRole('button', { name: 'OK' }).click();
    await page.waitForFunction(() => {
      const summaryName = document.querySelector('#summaryName');
      const summaryAlias = document.querySelector('#summaryAlias');
      const charState = document.querySelector('#charState');
      return summaryName instanceof HTMLInputElement
        && summaryAlias instanceof HTMLInputElement
        && summaryName.value === 'Playwright Runner'
        && summaryAlias.value === 'PW'
        && charState
        && charState.textContent
        && charState.textContent.toLowerCase().includes('loaded');
    }, { timeout: 20000 });

    const saveButton = page.getByRole('button', { name: 'Save' }).first();
    if (await saveButton.isDisabled()) {
      throw new Error('Save stayed disabled after opening the new workspace.');
    }

    console.log('playwright UI flow completed');
  } finally {
    await browser.close();
  }
}

run().catch(error => {
  console.error('playwright UI flow failed:', error instanceof Error ? error.stack || error.message : error);
  process.exitCode = 1;
});
