#!/usr/bin/env bash
set -euo pipefail

echo "[B1] checking explain-everywhere localization renderer requirements..."

if [[ ! -f Chummer.Presentation/Explain/RulesetExplainRenderer.cs ]]; then
  echo "[B1] FAIL: missing explain renderer contract in Chummer.Presentation/Explain/RulesetExplainRenderer.cs"
  exit 2
fi

if [[ ! -f Chummer.Blazor/Components/Shared/ExplainTracePanel.razor ]]; then
  echo "[B1] FAIL: missing ExplainTracePanel component."
  exit 3
fi

if ! rg -q "RulesetExplainRenderer|LocalizedRulesetExplainTrace|LocalizedRulesetExplainProvider" Chummer.Blazor/Components/Shared/ExplainTracePanel.razor Chummer.Presentation/Explain/RulesetExplainRenderer.cs Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B1] FAIL: explain renderer not wired into Blazor components."
  exit 4
fi

if rg -q "Explainability:|\bfrom active traits\b|(^|[^A-Za-z])Pack:" Chummer.Blazor/Components/Shared/ExplainTracePanel.razor Chummer.Presentation/Explain/RulesetExplainRenderer.cs; then
  echo "[B1] FAIL: hardcoded English explain prose still present in explain renderer path."
  exit 5
fi

echo "[B1] PASS: explain localization renderer components and localization-key payloads are present."
