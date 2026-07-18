'use strict';

const POLL_INTERVAL_MS = 2000;
const MAX_POLL_ATTEMPTS = 31;
const TRANSIENT_STATUSES = new Set(['queued', 'in_progress']);
const POSITIVE_INTEGER_RE = /^[1-9][0-9]*$/;

function requireExactApiInteger(value, label) {
  if (typeof value !== 'string' || !POSITIVE_INTEGER_RE.test(value)) {
    throw new Error(`${label} must be an exact positive integer string`);
  }
  const numeric = Number(value);
  if (!Number.isSafeInteger(numeric) || String(numeric) !== value) {
    throw new Error(`${label} exceeds exact API integer authority`);
  }
  return numeric;
}

function sleep(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function waitForExactSuccessfulWorkflowRun({
  github,
  owner,
  repo,
  runId,
  runAttempt,
  wait = sleep,
}) {
  const numericRunId = requireExactApiInteger(runId, 'producer run ID');
  requireExactApiInteger(runAttempt, 'producer run attempt');
  if (!github?.rest?.actions || typeof github.rest.actions.getWorkflowRun !== 'function') {
    throw new Error('GitHub workflow-run client is unavailable');
  }
  if (typeof owner !== 'string' || !owner || typeof repo !== 'string' || !repo) {
    throw new Error('exact repository owner and name are required');
  }
  if (typeof wait !== 'function') throw new Error('workflow-run wait function is unavailable');

  for (let poll = 1; poll <= MAX_POLL_ATTEMPTS; poll += 1) {
    const response = await github.rest.actions.getWorkflowRun({
      owner,
      repo,
      run_id: numericRunId,
    });
    const run = response?.data;
    if (!run || String(run.id) !== runId) {
      throw new Error('producer workflow-run API returned a different run ID');
    }
    if (String(run.run_attempt) !== runAttempt) {
      throw new Error('producer workflow run attempt drifted while capture waited');
    }
    if (run.status === 'completed') {
      if (run.conclusion !== 'success') {
        throw new Error('candidate run completed without success');
      }
      return response;
    }
    if (!TRANSIENT_STATUSES.has(run.status)) {
      throw new Error(`candidate run has unexpected non-terminal status: ${String(run.status)}`);
    }
    if (poll === MAX_POLL_ATTEMPTS) {
      throw new Error('candidate run did not complete successfully within the fixed 60 second wait');
    }
    await wait(POLL_INTERVAL_MS);
  }

  throw new Error('candidate run wait exhausted unexpectedly');
}

module.exports = {
  MAX_POLL_ATTEMPTS,
  POLL_INTERVAL_MS,
  waitForExactSuccessfulWorkflowRun,
};
