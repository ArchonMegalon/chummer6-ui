# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-ui/pull/20

Findings:
- [high] .codex-studio/published/QUEUE.generated.yaml [contracts] ui-milestone-coverage-queue-drift-not-enforced
Queue still contains `Finish milestone coverage modeling for ui so ETA and completion truth are no longer partial.` plus multiple other closure-era publications.; WORKLIST declares `WL-212` done and `Repo-local live queue: none`, so queue/worklist truth is currently divergent.; The updated `ui-milestone-coverage-check.sh` changed from unconditional failure on that queue publication to allowing it whenever `WL-212` exists and is `queued|done`, so verify no longer enforces queue normalization for this closed slice.
Expected fix: Reinstate strict queue/worklist closure enforcement for completed slices (at minimum WL-212) so stale closure publications fail verification, or clear the queue and update guards to require absence when the mapped WL row is done.
- [medium] scripts/ai/milestones/ui-milestone-coverage-check.sh [correctness] ui-milestone-guard-disallows-valid-active-statuses
WORKLIST status key includes `in_progress` and `blocked`.; Multiple WL mapping checks in `ui-milestone-coverage-check.sh` require row status `(queued|done)` only, which will fail valid active rows if a queue item is legitimately in progress.
Expected fix: Accept the full declared status set (`queued|in_progress|blocked|done`) where active queue mapping is valid, while still enforcing explicit closure conditions when a slice is marked done.
