#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const baseUrl = (process.env.CHUMMER_PORTAL_BASE_URL || 'https://chummer.run').replace(/\/$/, '');
const outputPath = process.env.CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH
  || path.join(process.cwd(), '.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json');
const promotedRouteBase = '/blazor/workbench';
const traceRoutes = process.env.CHUMMER_PUBLIC_EDGE_E2E_TRACE === '1';
const routeNavigationRetryAttempts = Number(process.env.CHUMMER_PUBLIC_EDGE_ROUTE_RETRY_ATTEMPTS || '3');
const routeNavigationRetryDelayMs = Number(process.env.CHUMMER_PUBLIC_EDGE_ROUTE_RETRY_DELAY_MS || '1500');
const playwrightScope = (process.env.CHUMMER_PUBLIC_EDGE_PLAYWRIGHT_SCOPE || 'smoke').trim().toLowerCase();
let promotedContinuationQuery = 'runner=blue';
const availableWorkflowFamilyIds = [
  'promoted_startup_command_executions',
  'promoted_dense_tool_surfaces',
  'promoted_origin_rules_continuity',
  'promoted_build_lab_continuity',
  'promoted_weapon_selection_execution',
  'promoted_combat_support_execution',
  'promoted_skill_selection_execution',
  'promoted_skill_maintenance_execution',
  'promoted_vehicle_selection_execution',
  'promoted_vehicle_mod_selection_execution',
  'promoted_quality_selection_execution',
  'promoted_quality_delete_execution',
  'promoted_spell_selection_execution',
  'promoted_magic_support_execution',
  'promoted_magic_delete_execution',
  'promoted_cyberware_selection_execution',
  'promoted_cyberware_edit_execution',
  'promoted_cyberware_delete_execution',
  'promoted_drug_selection_execution',
  'promoted_gear_maintenance_execution',
  'promoted_source_gear_utility_execution',
  'promoted_magic_cleanup_utility_execution',
  'promoted_contact_connection_execution',
  'promoted_vehicle_edit_execution',
  'promoted_vehicle_delete_execution',
  'promoted_contact_delete_execution',
  'promoted_contact_edit_execution',
  'promoted_career_entry_execution',
  'promoted_career_entry_committed_execution',
  'promoted_career_log_continuity',
  'promoted_resumed_workspace',
  'promoted_career_entry_edit_execution',
  'promoted_career_entry_delete_execution',
  'promoted_career_entry_edit_committed_execution',
  'promoted_career_entry_delete_committed_execution',
  'promoted_runner_notes_execution',
  'promoted_runner_notes_committed_execution',
  'promoted_career_entry_reorder_execution',
  'promoted_identity_license_execution',
  'promoted_recent_work_affordances',
  'promoted_restored_section_continuations',
  'promoted_restored_tab_landings',
  'promoted_restored_section_content',
  'promoted_result_continuations',
  'promoted_action_continuations',
  'promoted_advanced_action_affordances',
  'promoted_advanced_action_executions',
  'promoted_committed_actions',
  'promoted_advanced_committed_actions',
];
const smokeRequiredWorkflowFamilyIds = [
  'promoted_startup_command_executions',
  'promoted_dense_tool_surfaces',
  'promoted_origin_rules_continuity',
  'promoted_build_lab_continuity',
  'promoted_resumed_workspace',
  'promoted_result_continuations',
  'promoted_action_continuations',
  'promoted_committed_actions',
  'promoted_advanced_action_executions',
];

function normalizePlaywrightScope() {
  return playwrightScope === 'full' ? 'full' : 'smoke';
}

function requiredWorkflowFamilyIdsForScope(scope) {
  return scope === 'full'
    ? [...availableWorkflowFamilyIds]
    : [...smokeRequiredWorkflowFamilyIds];
}

function expectTextIncludes(text, expected, label) {
  if (!text.includes(expected)) {
    throw new Error(`${label}: expected text to include '${expected}'`);
  }
}

async function currentBodyExcerpt(page) {
  try {
    const bodyText = await page.locator('body').innerText({ timeout: 5000 });
    return bodyText.replace(/\s+/g, ' ').trim().slice(0, 400);
  } catch (_error) {
    return '';
  }
}

async function enrichRouteError(page, route, label, error) {
  const details = [error.message];
  try {
    details.push(`route=${route}`);
    details.push(`page_url=${page.url()}`);
    const title = await page.title();
    if (title) {
      details.push(`title=${title}`);
    }
    const excerpt = await currentBodyExcerpt(page);
    if (excerpt) {
      details.push(`body_excerpt=${excerpt}`);
    }
  } catch (_innerError) {
  }
  error.message = `${label}: ${details.join('\n')}`;
  return error;
}

function shouldRetryRouteNavigation(error) {
  const message = String(error && error.message || '');
  return message.includes('ERR_ABORTED') || message.includes('Timeout');
}

async function openPath(page, route, waitSelector) {
  let lastError = null;
  for (let attempt = 1; attempt <= routeNavigationRetryAttempts; attempt += 1) {
    try {
      if (traceRoutes) {
        process.stderr.write(`[chummer-public-edge-e2e] ${route} (attempt ${attempt}/${routeNavigationRetryAttempts})\n`);
      }
      await page.goto(`${baseUrl}${route}`, { waitUntil: 'commit', timeout: 45000 });
      if (waitSelector) {
        await page.locator(waitSelector).waitFor({ state: 'visible', timeout: 45000 });
      }
      return;
    } catch (error) {
      lastError = error;
      if (attempt >= routeNavigationRetryAttempts || !shouldRetryRouteNavigation(error)) {
        break;
      }

      try {
        await page.goto('about:blank', { waitUntil: 'load', timeout: 5000 });
      } catch (_ignored) {
      }

      await page.waitForTimeout(routeNavigationRetryDelayMs);
    }
  }

  throw await enrichRouteError(page, route, 'openPath failure', lastError || new Error(`Unable to open ${route}`));
}

function continuationQueryFromUrl(urlString) {
  const url = new URL(urlString, `${baseUrl}${promotedRouteBase}`);
  const runnerId = url.searchParams.get('runner');
  if (runnerId) {
    return `runner=${runnerId}`;
  }

  const workspaceId = url.searchParams.get('workspace');
  if (workspaceId) {
    return `workspace=${workspaceId}`;
  }

  const fixtureId = url.searchParams.get('fixture');
  if (fixtureId) {
    return `fixture=${fixtureId}`;
  }

  return null;
}

function isReusableContinuationQuery(query) {
  return query.startsWith('workspace=') || (query.startsWith('runner=') && query !== 'runner=blue');
}

async function resolvePromotedContinuationQuery(page) {
  if (isReusableContinuationQuery(promotedContinuationQuery)) {
    return promotedContinuationQuery;
  }

  await openPath(page, promotedRouteBase, 'body');
  const findContinuationHref = async () => page.evaluate(() => {
    const anchors = Array.from(document.querySelectorAll('a[href]'));
    const hrefs = anchors
      .map(anchor => anchor.getAttribute('href'))
      .filter(href => typeof href === 'string');
    return hrefs.find(href => href.includes('workbench?runner='))
      || hrefs.find(href => href.includes('workbench?workspace='))
      || hrefs.find(href => href.includes('workbench?fixture='))
      || null;
  });
  const continuationHref = await findContinuationHref();
  if (!continuationHref) {
    throw new Error('unable to resolve promoted continuation query from visible runner, workspace, or fixture links');
  }
  const continuationQuery = continuationQueryFromUrl(continuationHref);
  if (continuationQuery && isReusableContinuationQuery(continuationQuery)) {
    promotedContinuationQuery = continuationQuery;
    return promotedContinuationQuery;
  }
  const continuationUrl = new URL(continuationHref, `${baseUrl}${promotedRouteBase}`);
  const fixtureId = continuationUrl.searchParams.get('fixture');
  if (fixtureId) {
    await openPath(page, `${promotedRouteBase}?fixture=${encodeURIComponent(fixtureId)}`, 'section.classic-chummer-shell');
    await page.waitForFunction(
      () => new URL(window.location.href).searchParams.has('workspace'),
      null,
      { timeout: 45000 },
    ).catch(() => {});

    const rewrittenQuery = continuationQueryFromUrl(page.url());
    if (rewrittenQuery && isReusableContinuationQuery(rewrittenQuery)) {
      promotedContinuationQuery = rewrittenQuery;
      return promotedContinuationQuery;
    }

    const resolvedHref = await findContinuationHref();
    if (resolvedHref) {
      const resolvedQuery = continuationQueryFromUrl(resolvedHref);
      if (resolvedQuery && isReusableContinuationQuery(resolvedQuery)) {
        promotedContinuationQuery = resolvedQuery;
        return promotedContinuationQuery;
      }
    }
    promotedContinuationQuery = `fixture=${fixtureId}`;
    return promotedContinuationQuery;
  }
  throw new Error('unable to resolve promoted continuation query from visible runner, workspace, or fixture links');
}

async function readClassicShellState(page) {
  return page.locator('section.classic-chummer-shell').first().evaluate(el => ({
    tab: el.getAttribute('data-tab') || '',
    ruleset: el.getAttribute('data-ruleset') || '',
    workflow: el.getAttribute('data-active-workflow') || '',
    routeSegment: el.getAttribute('data-route-segment') || '',
    runner: el.getAttribute('data-legacy-runner') || el.getAttribute('data-active-runner') || '',
  }));
}

async function readRecoveryActions(page) {
  return page.locator('[data-workbench-recovery-action]').evaluateAll(nodes => nodes.map(node => ({
    action: node.getAttribute('data-workbench-recovery-action') || '',
    href: node.getAttribute('href') || '',
    text: (node.textContent || '').replace(/\s+/g, ' ').trim(),
  })));
}

async function auditResumedWorkspace(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}`;
  await openPath(page, route, 'section.classic-chummer-shell');
  const shellState = await readClassicShellState(page);
  const recoveryActions = await readRecoveryActions(page);
  const restoredRunnerText = await page.locator('[data-workbench-recovery-workspace]').first().textContent().catch(() => '');
  const buildLabAction = recoveryActions.find(action => action.action === 'build-lab');
  const continueRecentAction = recoveryActions.find(action => action.action === 'continue-recent');
  const restoreWorkspaceAction = recoveryActions.find(action => action.action === 'restore-workspace');
  if (shellState.routeSegment !== 'workbench') {
    throw new Error(`hosted resumed workspace: expected data-route-segment=workbench, got '${shellState.routeSegment}'`);
  }
  if (shellState.ruleset !== 'sr5') {
    throw new Error(`hosted resumed workspace: expected data-ruleset=sr5, got '${shellState.ruleset}'`);
  }
  if (!buildLabAction) {
    throw new Error('hosted resumed workspace: missing build-lab recovery action');
  }
  if (!continueRecentAction) {
    throw new Error('hosted resumed workspace: missing continue-recent recovery action');
  }
  if (!restoreWorkspaceAction) {
    throw new Error('hosted resumed workspace: missing restore-workspace recovery action');
  }
  if (!/(^|\/)workbench\?(runner|workspace|fixture)=/.test(continueRecentAction.href || '')) {
    throw new Error(`hosted resumed workspace: expected continue-recent href to preserve continuation query, got '${continueRecentAction.href || ''}'`);
  }
  if (!/(^|\/)workbench\?workspace=/.test(buildLabAction.href || '') || !/tab=tab-create/.test(buildLabAction.href || '')) {
    throw new Error(`hosted resumed workspace: expected build-lab href to target restored workspace build-lab lane, got '${buildLabAction.href || ''}'`);
  }
  if (!/(^|\/)workbench\?workspace=/.test(restoreWorkspaceAction.href || '')) {
    throw new Error(`hosted resumed workspace: expected restore-workspace href to preserve workspace route, got '${restoreWorkspaceAction.href || ''}'`);
  }
  expectTextIncludes(restoredRunnerText || '', 'BLUE', 'hosted resumed workspace');
  return {
    route,
    assertion: 'restored workspace recovery actions and hosted shell state rendered with canonical continuation hrefs',
    status: 'pass',
  };
}

async function auditStartupCommandExecution(page, route, expectedText) {
  await openPath(page, route, '.desktop-dialog');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, expectedText, `hosted startup command route ${route}`);
  return {
    route,
    assertion: `startup command surface '${expectedText}' rendered from canonical proof-compatible route`,
    status: 'pass',
  };
}

async function auditDenseToolSurface(page, route, expectedTitle, expectedSummary, expectedMarker, expectedSecondaryMarker) {
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `hosted dense tool surface route ${route}`);
  expectTextIncludes(dialogText, expectedSummary, `hosted dense tool surface route ${route}`);
  expectTextIncludes(dialogText, expectedMarker, `hosted dense tool surface route ${route}`);
  if (expectedSecondaryMarker) {
    expectTextIncludes(dialogText, expectedSecondaryMarker, `hosted dense tool surface route ${route}`);
  }
  return {
    route,
    assertion: `dense tool surface '${expectedTitle}' rendered with compact browser-visible utility markers`,
    status: 'pass',
  };
}

async function auditOriginWizardSurface(page) {
  const route = `${promotedRouteBase}?command=new_character_origin`;
  await openPath(page, route, '[data-origin-wizard]');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Origin Dossier', 'hosted origin wizard route');
  expectTextIncludes(dialogText, 'Create the story first. Review it, then continue to a guided build if you want mechanics.', 'hosted origin wizard route');
  expectTextIncludes(dialogText, 'Advanced story controls', 'hosted origin wizard route');
  expectTextIncludes(dialogText, 'Story Preview', 'hosted origin wizard route');
  return {
    route,
    assertion: 'origin dossier wizard rendered with story, advanced controls, and preview surfaces',
    status: 'pass',
  };
}

async function auditRulesContinuitySurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-rules`;
  await openPath(page, route, 'section.classic-chummer-shell');
  const shellState = await readClassicShellState(page);
  if (shellState.tab !== 'tab-rules') {
    throw new Error(`hosted rules continuity route: expected data-tab=tab-rules, got '${shellState.tab}'`);
  }
  if (shellState.ruleset !== 'sr5') {
    throw new Error(`hosted rules continuity route: expected data-ruleset=sr5, got '${shellState.ruleset}'`);
  }
  return {
    route,
    assertion: 'rules continuity route preserved the rules tab, runner continuation, and visible ruleset posture on the hosted shell',
    status: 'pass',
  };
}

async function auditBuildLabContinuitySurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-create`;
  await openPath(page, route, 'section.classic-chummer-shell');
  const shellState = await readClassicShellState(page);
  const bodyText = await page.locator('body').innerText();
  if (shellState.tab !== 'tab-create') {
    throw new Error(`hosted build lab continuity route: expected data-tab=tab-create, got '${shellState.tab}'`);
  }
  if (shellState.workflow !== 'build-lab') {
    throw new Error(`hosted build lab continuity route: expected data-active-workflow=build-lab, got '${shellState.workflow}'`);
  }
  expectTextIncludes(bodyText, 'Build Lab', 'hosted build lab continuity route');
  expectTextIncludes(bodyText, 'Continue Build Lab', 'hosted build lab continuity route');
  return {
    route,
    assertion: 'build-lab continuity route preserved the seeded build-lab workflow and continuation posture on the hosted shell',
    status: 'pass',
  };
}

async function auditVehicleSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=vehicle_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Vehicle / Drone', 'hosted vehicle selection route');
  expectTextIncludes(dialogText, 'Browse vehicles and drones, inspect stats and source, then confirm the selected entry.', 'hosted vehicle selection route');
  expectTextIncludes(dialogText, 'Available Vehicles', 'hosted vehicle selection route');
  expectTextIncludes(dialogText, 'Catalog Grid', 'hosted vehicle selection route');
  expectTextIncludes(dialogText, 'Used Vehicle Discount %', 'hosted vehicle selection route');
  return {
    route,
    assertion: 'vehicle selection dialog rendered dense gear-lane catalog, filters, and selection details',
    status: 'pass',
  };
}

async function auditWeaponSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-combat&control=combat_add_weapon`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Weapon', 'hosted weapon selection route');
  expectTextIncludes(dialogText, 'Available Weapons', 'hosted weapon selection route');
  expectTextIncludes(dialogText, 'Catalog Grid', 'hosted weapon selection route');
  expectTextIncludes(dialogText, 'Included Accessories', 'hosted weapon selection route');
  expectTextIncludes(dialogText, 'Filter Summary', 'hosted weapon selection route');
  return {
    route,
    assertion: 'weapon selection dialog rendered dense combat catalog with visible accessories, filters, and source context',
    status: 'pass',
  };
}

async function auditSkillSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-skills&control=skill_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Skill', 'hosted skill selection route');
  expectTextIncludes(dialogText, 'Browse skills, inspect category and linked attribute, then confirm the selected skill.', 'hosted skill selection route');
  expectTextIncludes(dialogText, 'Available Skills', 'hosted skill selection route');
  expectTextIncludes(dialogText, 'Linked Attribute', 'hosted skill selection route');
  expectTextIncludes(dialogText, 'Filter', 'hosted skill selection route');
  return {
    route,
    assertion: 'skill selection dialog rendered dense skill catalog with visible linked-attribute and filter controls',
    status: 'pass',
  };
}

async function auditVehicleModSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=vehicle_mod_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Vehicle Mod', 'hosted vehicle mod selection route');
  expectTextIncludes(dialogText, 'Browse modifications, inspect slot, availability, and source, then confirm the selected mod.', 'hosted vehicle mod selection route');
  expectTextIncludes(dialogText, 'Available Mods', 'hosted vehicle mod selection route');
  expectTextIncludes(dialogText, 'Selection Details', 'hosted vehicle mod selection route');
  expectTextIncludes(dialogText, 'Slot', 'hosted vehicle mod selection route');
  return {
    route,
    assertion: 'vehicle mod selection dialog rendered dense catalog, slot, availability, and source context',
    status: 'pass',
  };
}

async function auditQualitySelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-qualities&control=quality_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Quality', 'hosted quality selection route');
  expectTextIncludes(dialogText, 'Browse qualities, inspect karma cost and source, then confirm the selected quality.', 'hosted quality selection route');
  expectTextIncludes(dialogText, 'Available Qualities', 'hosted quality selection route');
  expectTextIncludes(dialogText, 'Filter Summary', 'hosted quality selection route');
  expectTextIncludes(dialogText, 'Karma', 'hosted quality selection route');
  return {
    route,
    assertion: 'quality selection dialog rendered dense category, karma, source, and filter context',
    status: 'pass',
  };
}

async function auditQualityDeleteSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-qualities&control=quality_delete`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove First Impression', 'hosted quality delete route');
  expectTextIncludes(dialogText, 'Remove First Impression from the current quality list?', 'hosted quality delete route');
  expectTextIncludes(dialogText, 'Removal Scope', 'hosted quality delete route');
  expectTextIncludes(dialogText, 'Karma totals and tags', 'hosted quality delete route');
  expectTextIncludes(dialogText, 'The selected quality will be removed while karma, source, and surrounding list context remain visible.', 'hosted quality delete route');
  return {
    route,
    assertion: 'quality delete dialog rendered karma-impact and recovery context with visible surrounding quality state',
    status: 'pass',
  };
}

async function auditSpellSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=spell_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Spell', 'hosted spell selection route');
  expectTextIncludes(dialogText, 'Search the spell list, inspect source and drain, then confirm the learned spell.', 'hosted spell selection route');
  expectTextIncludes(dialogText, 'Available Spells', 'hosted spell selection route');
  expectTextIncludes(dialogText, 'Selection Details', 'hosted spell selection route');
  expectTextIncludes(dialogText, 'Drain', 'hosted spell selection route');
  return {
    route,
    assertion: 'spell selection dialog rendered dense magic catalog with visible drain, source, and category context',
    status: 'pass',
  };
}

async function auditMagicDeleteSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=magic_delete`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove Stunbolt', 'hosted magic delete route');
  expectTextIncludes(dialogText, 'Remove Stunbolt from the current magic list?', 'hosted magic delete route');
  expectTextIncludes(dialogText, 'Removal Scope', 'hosted magic delete route');
  expectTextIncludes(dialogText, 'Drain and category', 'hosted magic delete route');
  expectTextIncludes(dialogText, 'current drain options stay visible', 'hosted magic delete route');
  return {
    route,
    assertion: 'magic delete dialog rendered drain-impact and recovery context with visible surrounding magic state',
    status: 'pass',
  };
}

async function auditCyberwareSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-cyberware&control=cyberware_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Cyberware', 'hosted cyberware selection route');
  expectTextIncludes(dialogText, 'Search, filter, keep source/cost/essence details visible, and confirm the selected implant.', 'hosted cyberware selection route');
  expectTextIncludes(dialogText, 'Available Cyberware', 'hosted cyberware selection route');
  expectTextIncludes(dialogText, 'Catalog Grid', 'hosted cyberware selection route');
  expectTextIncludes(dialogText, 'Essence', 'hosted cyberware selection route');
  expectTextIncludes(dialogText, 'Filter Summary', 'hosted cyberware selection route');
  return {
    route,
    assertion: 'cyberware selection dialog rendered dense catalog with visible essence, cost, source, and filter context',
    status: 'pass',
  };
}

async function auditCyberwareEditSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-cyberware&control=cyberware_edit`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Cyberware', 'hosted cyberware edit route');
  expectTextIncludes(dialogText, 'Edit the selected implant while keeping source, cost, essence, and notes visible.', 'hosted cyberware edit route');
  expectTextIncludes(dialogText, 'Installed Ware', 'hosted cyberware edit route');
  expectTextIncludes(dialogText, 'Live Summary', 'hosted cyberware edit route');
  expectTextIncludes(dialogText, 'Recalculated Essence', 'hosted cyberware edit route');
  return {
    route,
    assertion: 'cyberware edit dialog rendered installed-ware context with live recalculation and follow-through posture',
    status: 'pass',
  };
}

async function auditCyberwareDeleteSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-cyberware&control=cyberware_delete`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove Cybereyes Rating 4', 'hosted cyberware delete route');
  expectTextIncludes(dialogText, 'Remove Cybereyes Rating 4 from installed ware?', 'hosted cyberware delete route');
  expectTextIncludes(dialogText, 'Installed Ware', 'hosted cyberware delete route');
  expectTextIncludes(dialogText, 'Recovery', 'hosted cyberware delete route');
  expectTextIncludes(dialogText, 'Essence and capacity totals', 'hosted cyberware delete route');
  return {
    route,
    assertion: 'cyberware delete dialog rendered installed-ware removal impact and recovery context',
    status: 'pass',
  };
}

async function auditDrugSelectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=drug_add`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Drug', 'hosted drug selection route');
  expectTextIncludes(dialogText, 'Browse drugs, inspect speed and crash state, then confirm the selected dose.', 'hosted drug selection route');
  expectTextIncludes(dialogText, 'Available Drugs', 'hosted drug selection route');
  expectTextIncludes(dialogText, 'Selection Details', 'hosted drug selection route');
  expectTextIncludes(dialogText, 'Crash', 'hosted drug selection route');
  return {
    route,
    assertion: 'drug selection dialog rendered dense gear-lane catalog and dose-specific crash/source details',
    status: 'pass',
  };
}

async function auditContactConnectionSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-contacts&control=contact_connection`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Connection / Loyalty', 'hosted contact connection route');
  expectTextIncludes(dialogText, 'Adjust the selected contact while keeping the contact summary visible.', 'hosted contact connection route');
  expectTextIncludes(dialogText, 'Current Connection/Loyalty', 'hosted contact connection route');
  expectTextIncludes(dialogText, 'Adjusting connection and loyalty keeps the selected contact summary visible.', 'hosted contact connection route');
  return {
    route,
    assertion: 'contact connection dialog rendered compact edit controls with visible selected-contact summary context',
    status: 'pass',
  };
}

async function auditVehicleEditSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=vehicle_edit`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Vehicle / Drone', 'hosted vehicle edit route');
  expectTextIncludes(dialogText, 'Edit the selected vehicle or drone while keeping stats, source, and notes visible.', 'hosted vehicle edit route');
  expectTextIncludes(dialogText, 'Vehicle Details', 'hosted vehicle edit route');
  expectTextIncludes(dialogText, 'Live Summary', 'hosted vehicle edit route');
  expectTextIncludes(dialogText, 'Current Garage', 'hosted vehicle edit route');
  return {
    route,
    assertion: 'vehicle edit dialog rendered selected-item context with live summary and visible garage navigation state',
    status: 'pass',
  };
}

async function auditVehicleDeleteSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=vehicle_delete`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove GMC Roadmaster', 'hosted vehicle delete route');
  expectTextIncludes(dialogText, 'Remove GMC Roadmaster from the current garage?', 'hosted vehicle delete route');
  expectTextIncludes(dialogText, 'Current Garage', 'hosted vehicle delete route');
  expectTextIncludes(dialogText, 'Recovery', 'hosted vehicle delete route');
  expectTextIncludes(dialogText, 'Mods, mounts, and seats', 'hosted vehicle delete route');
  return {
    route,
    assertion: 'vehicle delete dialog rendered removal impact and recovery context with visible garage navigation state',
    status: 'pass',
  };
}

async function auditContactDeleteSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-contacts&control=contact_remove`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove Mr. Johnson', 'hosted contact delete route');
  expectTextIncludes(dialogText, 'Remove Mr. Johnson from the current contact roster?', 'hosted contact delete route');
  expectTextIncludes(dialogText, 'Current Roster', 'hosted contact delete route');
  expectTextIncludes(dialogText, 'Removal Scope', 'hosted contact delete route');
  expectTextIncludes(dialogText, 'Nearby contact notes', 'hosted contact delete route');
  return {
    route,
    assertion: 'contact delete dialog rendered roster impact and recovery context with visible surrounding contact state',
    status: 'pass',
  };
}

async function auditContactEditSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-contacts&control=contact_edit`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Contact', 'hosted contact edit route');
  expectTextIncludes(dialogText, 'Edit the selected contact while keeping role and connection visible.', 'hosted contact edit route');
  expectTextIncludes(dialogText, 'Contact Details', 'hosted contact edit route');
  expectTextIncludes(dialogText, 'Connection/Loyalty', 'hosted contact edit route');
  expectTextIncludes(dialogText, 'Connection, loyalty, and contact role remain visible while editing.', 'hosted contact edit route');
  return {
    route,
    assertion: 'contact edit dialog rendered selected-contact details with visible role and connection editing context',
    status: 'pass',
  };
}

async function auditCareerEntrySurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=create_entry`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Entry', 'hosted career entry route');
  expectTextIncludes(dialogText, 'Add a new entry while keeping the compact list/detail editor visible.', 'hosted career entry route');
  expectTextIncludes(dialogText, 'Command status', 'hosted career entry route');
  expectTextIncludes(dialogText, 'Entry Title', 'hosted career entry route');
  expectTextIncludes(dialogText, 'Entry creation and editing stay compact and preserve list context.', 'hosted career entry route');
  return {
    route,
    assertion: 'career calendar entry dialog rendered compact list/detail editor with visible command posture and list-context continuity',
    status: 'pass',
  };
}

async function auditCareerLogContinuitySurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar`;
  await openPath(page, route, '.section-preview > h2');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Calendar', 'hosted career log continuity route');
  expectTextIncludes(bodyText, 'Add Entry', 'hosted career log continuity route');
  return {
    route,
    assertion: 'career log continuation route landed on the calendar/support section with visible add-entry utility posture',
    status: 'pass',
  };
}

async function auditCareerEntryEditSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=edit_entry`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Entry', 'hosted career entry edit route');
  expectTextIncludes(dialogText, 'Edit the selected entry in the same compact list/detail editor.', 'hosted career entry edit route');
  expectTextIncludes(dialogText, 'Command status', 'hosted career entry edit route');
  expectTextIncludes(dialogText, 'Entry Title', 'hosted career entry edit route');
  return {
    route,
    assertion: 'career calendar edit dialog rendered compact list/detail editor with visible current-entry context',
    status: 'pass',
  };
}

async function auditCareerEntryDeleteSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=delete_entry`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Remove Current Entry', 'hosted career entry delete route');
  expectTextIncludes(dialogText, 'Remove Current Entry from the active list?', 'hosted career entry delete route');
  expectTextIncludes(dialogText, 'Removal Scope', 'hosted career entry delete route');
  expectTextIncludes(dialogText, 'Recovery', 'hosted career entry delete route');
  return {
    route,
    assertion: 'career calendar delete dialog rendered removal scope and recovery posture with current-list context',
    status: 'pass',
  };
}

async function auditRunnerNotesSurface(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-info&control=open_notes`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Edit Notes', 'hosted runner notes route');
  expectTextIncludes(dialogText, 'Edit runner notes in a compact text utility pane.', 'hosted runner notes route');
  expectTextIncludes(dialogText, 'Notes', 'hosted runner notes route');
  expectTextIncludes(dialogText, 'Save target', 'hosted runner notes route');
  return {
    route,
    assertion: 'runner notes dialog rendered compact notes editor with visible metadata/save context',
    status: 'pass',
  };
}

async function auditCareerEntryReorderSurface(page, controlId, expectedTitle) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=${controlId}`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `hosted career entry reorder route ${route}`);
  expectTextIncludes(dialogText, 'The reordered list stays visible in the same utility pane.', `hosted career entry reorder route ${route}`);
  return {
    route,
    assertion: `${expectedTitle} dialog rendered compact list-reorder utility posture`,
    status: 'pass',
  };
}

async function auditIdentityLicenseSurface(page, controlId, expectedTitle, expectedSummary, expectedMarker) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-info&control=${controlId}`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `hosted identity/license route ${route}`);
  expectTextIncludes(dialogText, expectedSummary, `hosted identity/license route ${route}`);
  expectTextIncludes(dialogText, expectedMarker, `hosted identity/license route ${route}`);
  expectTextIncludes(dialogText, 'lifestyle', `hosted identity/license route ${route}`);
  return {
    route,
    assertion: `${expectedTitle} dialog rendered identity, SIN/license, source, and lifestyle-cover context`,
    status: 'pass',
  };
}

async function auditResumedResultContinuation(page, route, expectedText) {
  await openPath(page, route, 'body');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, expectedText, `hosted resumed result route ${route}`);
  return {
    route,
    assertion: `visible result continuation text '${expectedText}' rendered`,
    status: 'pass',
  };
}

async function auditRecentWorkAffordances(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}`;
  await openPath(page, route, '[data-workbench-recent-workspace]');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Resume BLUE', 'hosted recent work affordances');
  const recentWorkspaceLink = page.locator('[data-workbench-recent-workspace]').first();
  const recentHref = await recentWorkspaceLink.getAttribute('href');
  const recentRouteQuery = await recentWorkspaceLink.getAttribute('data-workbench-route-query');
  if (!/(^|\/)workbench\?workspace=/.test(recentHref || '')) {
    throw new Error(`hosted recent work affordances: expected workspace continuation href, got '${recentHref || ''}'`);
  }
  if (recentRouteQuery !== 'workspace') {
    throw new Error(`hosted recent work route query marker: expected workspace route query, got '${recentRouteQuery || ''}'`);
  }
  return {
    route,
    assertion: 'recent restored workspace affordance remains visible with canonical workspace route-query marker',
    status: 'pass',
  };
}

async function expectWorkspaceRouteQueryMarker(page, selector, context) {
  const routeQuery = await page.locator(selector).first().getAttribute('data-workbench-route-query');
  expectTextIncludes(routeQuery || '', 'workspace', context);
}

async function auditRestoredSectionContinuations(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}`;
  const restoredContinuationsSelector = '[data-workbench-entry-card="restored-continuations"]';
  await openPath(page, route, restoredContinuationsSelector);
  await expectWorkspaceRouteQueryMarker(page, restoredContinuationsSelector, 'hosted restored section route query marker');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Resume BLUE on profile', 'hosted restored section continuations');
  expectTextIncludes(bodyText, 'Resume BLUE on rules', 'hosted restored section continuations');
  expectTextIncludes(bodyText, 'Resume BLUE on gear', 'hosted restored section continuations');
  expectTextIncludes(bodyText, 'Resume BLUE on career log', 'hosted restored section continuations');
  expectTextIncludes(bodyText, 'Resume BLUE on advanced', 'hosted restored section continuations');
  return {
    route,
    assertion: 'restored section continuation affordances remain visible with canonical workspace route-query marker',
    status: 'pass',
  };
}

async function auditRestoredTabLanding(page, route, tabId) {
  const expectedHeadings = {
    'tab-info': 'Profile',
    'tab-rules': 'Rules',
    'tab-gear': 'Gear',
    'tab-technomancer': 'Complex Forms',
  };
  await openPath(page, route, '.section-preview > h2');
  const sectionHeading = await page.locator('.section-preview > h2').first().innerText();
  expectTextIncludes(sectionHeading || '', expectedHeadings[tabId] || tabId, `hosted restored tab landing ${route}`);
  return {
    route,
    assertion: `restored route landed on '${expectedHeadings[tabId] || tabId}' section for canonical proof-compatible route`,
    status: 'pass',
  };
}

async function auditRestoredSectionContent(page, route, selector, expectedText) {
  await openPath(page, route, selector);
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, expectedText, `hosted restored section content ${route}`);
  return {
    route,
    assertion: `restored route rendered section-specific marker '${expectedText}'`,
    status: 'pass',
  };
}

async function auditResumedAction(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=create_entry`;
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, 'Add Entry', 'hosted resumed action route');
  expectTextIncludes(dialogText, 'Entry Title', 'hosted resumed action route');
  return {
    route,
    assertion: 'calendar entry dialog opened with expected browser-visible fields',
    status: 'pass',
  };
}

async function auditAdvancedActionAffordances(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}`;
  const restoredActionsSelector = '[data-workbench-entry-card="restored-actions"]';
  await openPath(page, route, restoredActionsSelector);
  await expectWorkspaceRouteQueryMarker(page, restoredActionsSelector, 'hosted restored action route query marker');
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, 'Add a complex form for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add initiation for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add cyberware for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add a spell for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add and keep career entry for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add career entry for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Apply career entry edit for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Edit career entry for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Remove and keep career entry result for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Remove career entry for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Save dossier notes for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Edit dossier notes for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Move career entry up for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Move career entry down for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add SIN/license for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Edit SIN/license for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Remove SIN/license for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add armor for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Reload weapon for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Review damage track for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Specialize skill for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Remove skill for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Edit skill group for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add adept power for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add spirit for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add critter power for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add Matrix program for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add gear for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Edit gear for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Remove gear for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Show source for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Show gear source for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Mount gear for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Toggle gear free/paid for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Add general magic item for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Bind spirit for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Show magic source for BLUE', 'hosted advanced action affordances');
  expectTextIncludes(bodyText, 'Remove drug for BLUE', 'hosted advanced action affordances');
  return {
    route,
    assertion: 'advanced and career/support restored action affordances remain visible with canonical workspace route-query marker',
    status: 'pass',
  };
}

async function auditAdvancedActionExecution(page, route, expectedTitle, expectedText) {
  await openPath(page, route, '.desktop-dialog');
  const dialogText = await page.locator('.desktop-dialog').innerText();
  expectTextIncludes(dialogText, expectedTitle, `hosted advanced action route ${route}`);
  expectTextIncludes(dialogText, expectedText, `hosted advanced action route ${route}`);
  return {
    route,
    assertion: `advanced dialog '${expectedTitle}' rendered with expected browser-visible details`,
    status: 'pass',
  };
}

async function auditCommittedAction(page) {
  const route = `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=create_entry&dialog_action=add`;
  await openPath(page, route, '#summaryName');
  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, "Entry 'New entry' added.", 'hosted committed action route');
  return {
    route,
    assertion: 'committed calendar entry result remains visible after dialog action',
    status: 'pass',
  };
}

async function auditAdvancedCommittedAction(page, route, expectedText) {
  await openPath(page, route, '#summaryName');
  await page.waitForFunction(() => !document.querySelector('#dialogBackdrop'), { timeout: 15000 });
  const bodyText = await page.locator('body').innerText();
  expectTextIncludes(bodyText, expectedText, `hosted advanced committed action route ${route}`);
  return {
    route,
    assertion: `advanced committed action published visible notice '${expectedText}'`,
    status: 'pass',
  };
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  const normalizedScope = normalizePlaywrightScope();
  console.log(`public-edge playwright scope: ${normalizedScope}`);
  const receipt = {
    contract_name: 'chummer6-ui.blazor_public_edge_execution_proof',
    generated_at: new Date().toISOString(),
    status: 'failed',
    base_url: baseUrl,
    playwright_scope: normalizedScope,
    proof_tier: 'hosted_promoted_route_execution',
    route_lane: 'promoted_blazor_workbench',
    promoted_route_base: promotedRouteBase,
    required_workflow_family_ids: requiredWorkflowFamilyIdsForScope(normalizedScope),
    available_workflow_family_ids: availableWorkflowFamilyIds,
    workflow_families: [],
    notes: [
      'This receipt is for hosted browser execution proof against the public edge.',
      'Do not treat route-entry proof as equivalent to this execution receipt.',
      'Only promoted /blazor/workbench workflow execution counts for this proof tier.',
      'Default hosted public-edge scope is smoke; set CHUMMER_PUBLIC_EDGE_PLAYWRIGHT_SCOPE=full for the broader live-edge mutation matrix.',
    ],
  };

  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    page.setDefaultTimeout(45000);
    page.setDefaultNavigationTimeout(45000);
    await resolvePromotedContinuationQuery(page);
    if (normalizedScope === 'smoke') {
      receipt.workflow_families.push({
        id: 'promoted_startup_command_executions',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'startup_command_execution',
        checks: [
          await auditStartupCommandExecution(page, `${promotedRouteBase}?command=new_character`, 'New runner'),
          await auditStartupCommandExecution(page, `${promotedRouteBase}?command=new_character_origin`, 'Origin Dossier'),
          await auditStartupCommandExecution(page, `${promotedRouteBase}?command=open_for_export`, 'Open for Export'),
        ],
      });
      receipt.workflow_families.push({
        id: 'promoted_dense_tool_surfaces',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'dense_tool_surface_execution',
        checks: [
          await auditDenseToolSurface(
            page,
            `${promotedRouteBase}?command=character_roster`,
            'Character Roster',
            'Group runners into your own folders',
            'Runner Status',
            'Bio / Concept / Notes',
          ),
        ],
      });
      receipt.workflow_families.push({
        id: 'promoted_origin_rules_continuity',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'origin_and_rules_surface_continuity',
        checks: [
          await auditOriginWizardSurface(page),
          await auditRulesContinuitySurface(page),
        ],
      });
      await resolvePromotedContinuationQuery(page);
      receipt.workflow_families.push({
        id: 'promoted_build_lab_continuity',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'build_lab_runner_continuity',
        checks: [await auditBuildLabContinuitySurface(page)],
      });
      receipt.workflow_families.push({
        id: 'promoted_resumed_workspace',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'workspace_resume_continuity',
        checks: [await auditResumedWorkspace(page)],
      });
      receipt.workflow_families.push({
        id: 'promoted_result_continuations',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'browser_result_continuations',
        checks: [
          await auditResumedResultContinuation(page, `${promotedRouteBase}?${promotedContinuationQuery}&command=save_character_as`, 'Download prepared:'),
          await auditResumedResultContinuation(page, `${promotedRouteBase}?${promotedContinuationQuery}&command=print_character`, 'Print preview prepared:'),
        ],
      });
      receipt.workflow_families.push({
        id: 'promoted_action_continuations',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'dialog_action_continuity',
        checks: [await auditResumedAction(page)],
      });
      receipt.workflow_families.push({
        id: 'promoted_committed_actions',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'committed_visible_state',
        checks: [await auditCommittedAction(page)],
      });
      receipt.workflow_families.push({
        id: 'promoted_advanced_action_executions',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'advanced_dialog_action_execution',
        checks: [
          await auditAdvancedActionExecution(
            page,
            `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-technomancer&control=complex_form_add`,
            'Add Complex Form',
            'Browse complex forms, inspect target and source',
          ),
        ],
      });
    } else {
    receipt.workflow_families.push({
      id: 'promoted_startup_command_executions',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'startup_command_execution',
      checks: [
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=new_character`, 'New runner'),
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=new_character_origin`, 'Origin Dossier'),
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=character_roster`, 'Character Roster'),
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=master_index`, 'Master Index'),
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=open_character`, 'Open Dossier'),
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=open_for_printing`, 'Open for Printing'),
        await auditStartupCommandExecution(page, `${promotedRouteBase}?command=open_for_export`, 'Open for Export'),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_dense_tool_surfaces',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'dense_tool_surface_execution',
      checks: [
        await auditDenseToolSurface(
          page,
          `${promotedRouteBase}?command=character_roster`,
          'Character Roster',
          'Group runners into your own folders',
          'Runner Status',
          'Bio / Concept / Notes',
        ),
        await auditDenseToolSurface(
          page,
          `${promotedRouteBase}?command=master_index`,
          'Master Index',
          'Search the catalog, inspect the selected reference, and keep the current source visible',
          'Linked PDF / URL',
          'Use Setting',
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_origin_rules_continuity',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'origin_and_rules_surface_continuity',
      checks: [
        await auditOriginWizardSurface(page),
        await auditRulesContinuitySurface(page),
      ],
    });
    await resolvePromotedContinuationQuery(page);
    receipt.workflow_families.push({
      id: 'promoted_build_lab_continuity',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'build_lab_runner_continuity',
      checks: [await auditBuildLabContinuitySurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_weapon_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'combat_lane_weapon_selection_execution',
      checks: [await auditWeaponSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_skill_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'skills_lane_skill_selection_execution',
      checks: [await auditSkillSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_vehicle_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'gear_lane_vehicle_selection_execution',
      checks: [await auditVehicleSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_vehicle_mod_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'gear_lane_vehicle_mod_selection_execution',
      checks: [await auditVehicleModSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_quality_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'qualities_lane_quality_selection_execution',
      checks: [await auditQualitySelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_quality_delete_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'qualities_lane_quality_delete_execution',
      checks: [await auditQualityDeleteSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_spell_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'magic_lane_spell_selection_execution',
      checks: [await auditSpellSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_magic_delete_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'magic_lane_delete_execution',
      checks: [await auditMagicDeleteSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_cyberware_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'cyberware_lane_selection_execution',
      checks: [await auditCyberwareSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_cyberware_edit_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'cyberware_lane_edit_execution',
      checks: [await auditCyberwareEditSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_cyberware_delete_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'cyberware_lane_delete_execution',
      checks: [await auditCyberwareDeleteSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_drug_selection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'gear_lane_drug_selection_execution',
      checks: [await auditDrugSelectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_contact_connection_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'contact_lane_connection_edit_execution',
      checks: [await auditContactConnectionSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_vehicle_edit_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'gear_lane_vehicle_edit_execution',
      checks: [await auditVehicleEditSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_vehicle_delete_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'gear_lane_vehicle_delete_execution',
      checks: [await auditVehicleDeleteSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_contact_delete_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'contacts_lane_contact_delete_execution',
      checks: [await auditContactDeleteSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_contact_edit_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'contacts_lane_contact_edit_execution',
      checks: [await auditContactEditSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_entry_execution',
      checks: [await auditCareerEntrySurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_committed_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_committed_visible_state',
      checks: [
        await auditAdvancedCommittedAction(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=create_entry&dialog_action=add`,
          "Entry 'New entry' added.",
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_log_continuity',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_log_section_continuity',
      checks: [await auditCareerLogContinuitySurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_edit_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_edit_execution',
      checks: [await auditCareerEntryEditSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_delete_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_delete_execution',
      checks: [await auditCareerEntryDeleteSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_edit_committed_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_edit_committed_visible_state',
      checks: [
        await auditAdvancedCommittedAction(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=edit_entry&dialog_action=apply`,
          "Entry renamed to 'Current Entry'.",
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_delete_committed_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_delete_committed_visible_state',
      checks: [
        await auditAdvancedCommittedAction(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-calendar&control=delete_entry&dialog_action=delete`,
          "Entry 'Current Entry' removed.",
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_runner_notes_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'runner_notes_edit_execution',
      checks: [await auditRunnerNotesSurface(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_runner_notes_committed_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'runner_notes_committed_visible_state',
      checks: [
        await auditAdvancedCommittedAction(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-info&control=open_notes&dialog_action=save`,
          "Notes saved.",
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_career_entry_reorder_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'career_calendar_reorder_execution',
      checks: [
        await auditCareerEntryReorderSurface(page, 'move_up', 'Move Entry Up'),
        await auditCareerEntryReorderSurface(page, 'move_down', 'Move Entry Down'),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_resumed_workspace',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'workspace_resume_continuity',
      checks: [await auditResumedWorkspace(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_magic_cleanup_utility_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'magic_cleanup_utility_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=magic_add`,
          'Magic',
          'Magic'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=magic_bind`,
          'Bind',
          'Bind'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=magic_source`,
          'Source',
          'Source'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=drug_delete`,
          'Remove',
          'Remove'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_source_gear_utility_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'source_gear_utility_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-info&control=show_source`,
          'Source',
          'Source'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=gear_source`,
          'Source',
          'Source'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=gear_mount`,
          'Mount',
          'Mount'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=toggle_free_paid`,
          'Pricing status',
          'Selected item pricing was toggled between free and paid.'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_gear_maintenance_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'gear_maintenance_utility_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=gear_add`,
          'Add Gear',
          'Browse the catalog'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=gear_edit`,
          'Edit Gear',
          'Edit'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear&control=gear_delete`,
          'Remove Armor Jacket',
          'Removal Scope'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_magic_support_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'magic_resonance_support_utility_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-adept&control=adept_power_add`,
          'Power',
          'Power'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=spirit_add`,
          'Spirit',
          'Spirit'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-critter&control=critter_power_add`,
          'Power',
          'Critter'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-technomancer&control=matrix_program_add`,
          'Program',
          'Program'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_skill_maintenance_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'skill_maintenance_utility_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-skills&control=skill_specialize`,
          'Specialization',
          'Specialization'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-skills&control=skill_remove`,
          'Remove Perception',
          'Remove Perception'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-skills&control=skill_group`,
          'Skill Group',
          'Group composition and current rating remain visible while editing.'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_combat_support_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'combat_support_utility_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-combat&control=combat_add_armor`,
          'Add Armor',
          'Armor'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-combat&control=combat_reload`,
          'Reload',
          'Weapon and ammo selection remain visible while reloading.'
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-combat&control=combat_damage_track`,
          'Damage Track',
          'Current track state remains visible before applying the damage step.'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_identity_license_execution',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'identity_sin_license_utility_execution',
      checks: [
        await auditIdentityLicenseSurface(
          page,
          'identity_license_add',
          'Add SIN / License',
          'Create a browser-safe identity, SIN, or license record while keeping rating, source, and legal status visible.',
          'Legal status'
        ),
        await auditIdentityLicenseSurface(
          page,
          'identity_license_edit',
          'Edit SIN / License',
          'Review the selected identity record while keeping attached licenses and source status visible.',
          'Attached Context'
        ),
        await auditIdentityLicenseSurface(
          page,
          'identity_license_delete',
          'Remove SIN / License',
          'Remove the selected identity record only after the attached license and recovery context stays visible.',
          'Removal Impact'
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_recent_work_affordances',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'recent_work_resume_affordances',
      checks: [await auditRecentWorkAffordances(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_restored_section_continuations',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'section_continuation_affordances',
      checks: [await auditRestoredSectionContinuations(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_restored_tab_landings',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'restored_tab_landing_execution',
      checks: [
        await auditRestoredTabLanding(page, `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-info`, 'tab-info'),
        await auditRestoredTabLanding(page, `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-rules`, 'tab-rules'),
        await auditRestoredTabLanding(page, `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear`, 'tab-gear'),
        await auditRestoredTabLanding(page, `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-technomancer`, 'tab-technomancer'),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_restored_section_content',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'restored_section_surface_content',
      checks: [
        await auditRestoredSectionContent(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-info`,
          '.section-preview > h2',
          'Profile',
        ),
        await auditRestoredSectionContent(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-rules`,
          '.section-preview > h2',
          'Rules',
        ),
        await auditRestoredSectionContent(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-gear`,
          '.section-preview > h2',
          'Gear',
        ),
        await auditRestoredSectionContent(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-technomancer`,
          '.section-preview > h2',
          'Complex Forms',
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_result_continuations',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'browser_result_continuations',
      checks: [
        await auditResumedResultContinuation(page, `${promotedRouteBase}?${promotedContinuationQuery}&command=save_character_as`, 'Download prepared:'),
        await auditResumedResultContinuation(page, `${promotedRouteBase}?${promotedContinuationQuery}&command=export_character`, 'Export Dossier'),
        await auditResumedResultContinuation(page, `${promotedRouteBase}?${promotedContinuationQuery}&command=print_character`, 'Print preview prepared:'),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_action_continuations',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'dialog_action_continuity',
      checks: [await auditResumedAction(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_advanced_action_affordances',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'advanced_action_continuation_affordances',
      checks: [await auditAdvancedActionAffordances(page)],
    });
    receipt.workflow_families.push({
      id: 'promoted_advanced_action_executions',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'advanced_dialog_action_execution',
      checks: [
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-technomancer&control=complex_form_add`,
          'Add Complex Form',
          'Browse complex forms, inspect target and source',
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-adept&control=initiation_add`,
          'Add Initiation / Submersion',
          'Choose the reward, keep grade and track visible',
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-cyberware&control=cyberware_add`,
          'Add Cyberware',
          'Search, filter, keep source/cost/essence details visible',
        ),
        await auditAdvancedActionExecution(
          page,
          `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=spell_add`,
          'Add Spell',
          'Search the spell list, inspect source and drain',
        ),
      ],
    });
    receipt.workflow_families.push({
      id: 'promoted_committed_actions',
      route_lane: 'promoted_blazor_workbench',
      workflow_contract: 'committed_visible_state',
      checks: [await auditCommittedAction(page)],
    });
    if (normalizedScope === 'full') {
      receipt.workflow_families.push({
        id: 'promoted_advanced_committed_actions',
        route_lane: 'promoted_blazor_workbench',
        workflow_contract: 'advanced_committed_visible_state',
        checks: [
          await auditAdvancedCommittedAction(
            page,
            `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-technomancer&control=complex_form_add&dialog_action=add`,
            "Complex form 'Cleaner' added.",
          ),
          await auditAdvancedCommittedAction(
            page,
            `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-adept&control=initiation_add&dialog_action=add`,
            "Initiation/submersion reward 'Masking' added.",
          ),
          await auditAdvancedCommittedAction(
            page,
            `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-cyberware&control=cyberware_add&dialog_action=add`,
            "Cyberware 'Wired Reflexes 2' added.",
          ),
          await auditAdvancedCommittedAction(
            page,
            `${promotedRouteBase}?${promotedContinuationQuery}&tab=tab-magician&control=spell_add&dialog_action=add`,
            "Spell 'Stunbolt' added.",
          ),
        ],
      });
    }
    }
    await page.close();
    receipt.status = 'passed';
  } catch (error) {
    receipt.error = error.message;
  } finally {
    await browser.close();
  }

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(receipt, null, 2)}\n`);

  if (receipt.status !== 'passed') {
    process.exitCode = 1;
  }
}

run().catch(error => {
  console.error(error);
  process.exit(1);
});
