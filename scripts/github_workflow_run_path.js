'use strict';

const BARE_WORKFLOW_PATH_RE = /^\.github\/workflows\/[A-Za-z0-9][A-Za-z0-9._-]*\.ya?ml$/;
const COMMIT_SHA_RE = /^[0-9a-f]{40}$/;

function workflowRunPathMatches(actualPath, barePath, source) {
  if (typeof actualPath !== 'string' || !BARE_WORKFLOW_PATH_RE.test(barePath)) return false;
  if (actualPath === barePath) return true;
  if (!actualPath.startsWith(`${barePath}@`)) return false;

  const branch = typeof source?.branch === 'string' ? source.branch.trim() : '';
  const ref = typeof source?.ref === 'string' ? source.ref.trim() : '';
  const sha = typeof source?.sha === 'string' ? source.sha.trim() : '';
  if (!branch || !COMMIT_SHA_RE.test(sha)) return false;

  const validRefs = new Set([branch, `refs/heads/${branch}`, `refs/tags/${branch}`]);
  if (!validRefs.has(ref)) return false;

  const suffix = actualPath.slice(barePath.length + 1);
  return validRefs.has(suffix) || suffix === sha;
}

module.exports = { workflowRunPathMatches };
