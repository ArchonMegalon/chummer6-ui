#!/usr/bin/env bash
set -euo pipefail

echo "[B9] checking campaign journal continuity surface..."

if [[ ! -f Chummer.Blazor/Components/Shared/CampaignJournalPanel.razor ]]; then
  echo "[B9] FAIL: missing CampaignJournalPanel component."
  exit 3
fi

if ! rg -q "JournalPanelProjection|Sections|Notes|LedgerEntries|TimelineEvents|JournalScopeKinds|TimelineEventKinds|LedgerEntryKinds" Chummer.Blazor/Components/Shared/CampaignJournalPanel.razor Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B9] FAIL: campaign journal path is not contract-driven."
  exit 4
fi

if ! rg -q "CampaignJournalPanel" Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B9] FAIL: campaign journal panel is not composed on the home surface."
  exit 5
fi

if ! rg -q "DowntimePlannerProjector|data-journal-downtime-planner|data-journal-calendar-view|data-journal-schedule-view" \
  Chummer.Blazor/Components/Shared/CampaignJournalPanel.razor \
  Chummer.Presentation/Overview/DowntimePlannerState.cs; then
  echo "[B9] FAIL: explicit downtime planner/calendar/schedule rails are missing from the shared journal seam."
  exit 6
fi

if ! rg -q "DowntimePlanner|BuildDowntimePlanner|SetDowntimePlanner|DowntimePlannerBorder|DowntimeScheduleList" \
  Chummer.Avalonia/MainWindow.ShellFrameProjector.cs \
  Chummer.Avalonia/Controls/SectionHostControl.axaml \
  Chummer.Avalonia/Controls/SectionHostControl.axaml.cs; then
  echo "[B9] FAIL: downtime planner/calendar/schedule rails are not projected in the Avalonia head."
  exit 7
fi

echo "[B9] PASS: campaign journal continuity surface is present and contract-driven."
