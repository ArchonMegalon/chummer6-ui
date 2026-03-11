#nullable enable annotations

using Chummer.Contracts.Journal;
using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DowntimePlannerProjectorTests
{
    [TestMethod]
    public void FromJournal_projects_explicit_planner_calendar_and_schedule_views()
    {
        JournalPanelProjection projection = new(
            ScopeKind: JournalScopeKinds.Campaign,
            ScopeId: "campaign-1",
            Sections:
            [
                new JournalPanelSection(JournalPanelSurfaceIds.TimelinePanel, JournalPanelSectionKinds.Timeline, "Timeline", 3)
            ],
            Notes: [],
            LedgerEntries: [],
            TimelineEvents:
            [
                new TimelineEventView(
                    EventId: "timeline-1",
                    Kind: TimelineEventKinds.Downtime,
                    Title: "Street doc follow-up",
                    StartsAtUtc: new DateTimeOffset(2026, 03, 12, 10, 0, 0, TimeSpan.Zero),
                    EndsAtUtc: new DateTimeOffset(2026, 03, 12, 12, 0, 0, TimeSpan.Zero)),
                new TimelineEventView(
                    EventId: "timeline-2",
                    Kind: TimelineEventKinds.Training,
                    Title: "Rigger drills",
                    StartsAtUtc: new DateTimeOffset(2026, 03, 13, 8, 0, 0, TimeSpan.Zero),
                    EndsAtUtc: new DateTimeOffset(2026, 03, 13, 10, 0, 0, TimeSpan.Zero)),
                new TimelineEventView(
                    EventId: "timeline-3",
                    Kind: "healing",
                    Title: "Clinic recovery check",
                    StartsAtUtc: new DateTimeOffset(2026, 03, 13, 12, 0, 0, TimeSpan.Zero))
            ]);

        DowntimePlannerState? state = DowntimePlannerProjector.FromJournal(projection);

        Assert.IsNotNull(state);
        Assert.AreEqual(3, state.ScheduleItems.Count);
        Assert.IsTrue(state.PlannerLanes.Any(lane => lane.LaneId == "downtime"));
        Assert.IsTrue(state.PlannerLanes.Any(lane => lane.LaneId == "training"));
        Assert.IsTrue(state.PlannerLanes.Any(lane => lane.LaneId == "recovery"));
        Assert.AreEqual(2, state.CalendarDays.Count);
        Assert.IsTrue(state.CalendarDays.All(day => day.ItemCount > 0));
    }

    [TestMethod]
    public void FromJournal_returns_null_when_projection_is_missing()
    {
        DowntimePlannerState? state = DowntimePlannerProjector.FromJournal(null);
        Assert.IsNull(state);
    }
}
