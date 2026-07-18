'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const {
  MAX_POLL_ATTEMPTS,
  POLL_INTERVAL_MS,
  waitForExactSuccessfulWorkflowRun,
} = require('../scripts/github_wait_for_workflow_run.js');

const RUN_ID = '987654';
const RUN_ATTEMPT = '1';

function clientFor(states) {
  const calls = [];
  let index = 0;
  const github = {
    rest: {
      actions: {
        async getWorkflowRun(args) {
          calls.push(args);
          if (index >= states.length) throw new Error('test supplied too few workflow-run states');
          const state = states[index];
          index += 1;
          return {
            data: {
              id: Number(RUN_ID),
              run_attempt: Number(RUN_ATTEMPT),
              conclusion: null,
              ...state,
            },
          };
        },
      },
    },
  };
  return { calls, github };
}

function waitArgs(github, wait) {
  return {
    github,
    owner: 'ArchonMegalon',
    repo: 'chummer6-ui',
    runId: RUN_ID,
    runAttempt: RUN_ATTEMPT,
    wait,
  };
}

test('queued and in-progress samples reach the exact completed successful run', async () => {
  const { calls, github } = clientFor([
    { status: 'queued' },
    { status: 'in_progress' },
    { status: 'completed', conclusion: 'success' },
  ]);
  const waits = [];
  const response = await waitForExactSuccessfulWorkflowRun(
    waitArgs(github, async milliseconds => waits.push(milliseconds))
  );

  assert.equal(response.data.status, 'completed');
  assert.equal(response.data.conclusion, 'success');
  assert.deepEqual(waits, [POLL_INTERVAL_MS, POLL_INTERVAL_MS]);
  assert.deepEqual(calls, [
    { owner: 'ArchonMegalon', repo: 'chummer6-ui', run_id: Number(RUN_ID) },
    { owner: 'ArchonMegalon', repo: 'chummer6-ui', run_id: Number(RUN_ID) },
    { owner: 'ArchonMegalon', repo: 'chummer6-ui', run_id: Number(RUN_ID) },
  ]);
});

test('a terminal producer failure blocks capture immediately', async () => {
  const { github } = clientFor([{ status: 'completed', conclusion: 'failure' }]);
  let waited = false;
  await assert.rejects(
    waitForExactSuccessfulWorkflowRun(waitArgs(github, async () => { waited = true; })),
    /completed without success/
  );
  assert.equal(waited, false);
});

test('the fixed sixty-second bound blocks a producer that never completes', async () => {
  const { calls, github } = clientFor(
    Array.from({ length: MAX_POLL_ATTEMPTS }, () => ({ status: 'in_progress' }))
  );
  const waits = [];
  await assert.rejects(
    waitForExactSuccessfulWorkflowRun(
      waitArgs(github, async milliseconds => waits.push(milliseconds))
    ),
    /fixed 60 second wait/
  );
  assert.equal(calls.length, MAX_POLL_ATTEMPTS);
  assert.equal(waits.length, MAX_POLL_ATTEMPTS - 1);
  assert.ok(waits.every(milliseconds => milliseconds === POLL_INTERVAL_MS));
});

test('run-attempt drift blocks before another poll or artifact authorization', async () => {
  const { calls, github } = clientFor([
    { status: 'in_progress', run_attempt: Number(RUN_ATTEMPT) + 1 },
  ]);
  let waited = false;
  await assert.rejects(
    waitForExactSuccessfulWorkflowRun(waitArgs(github, async () => { waited = true; })),
    /attempt drifted/
  );
  assert.equal(calls.length, 1);
  assert.equal(waited, false);
});
