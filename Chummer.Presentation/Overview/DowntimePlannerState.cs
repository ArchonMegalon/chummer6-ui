using Chummer.Contracts.Journal;
using Chummer.Contracts.Presentation;

namespace Chummer.Presentation.Overview;

public sealed record DowntimePlannerState(
    IReadOnlyList<DowntimePlannerLaneState> PlannerLanes,
    IReadOnlyList<DowntimeCalendarDayState> CalendarDays,
    IReadOnlyList<DowntimeScheduleItemState> ScheduleItems);

public sealed record DowntimePlannerLaneState(
    string LaneId,
    string Title,
    IReadOnlyList<DowntimeScheduleItemState> Items)
{
    public int ItemCount => Items.Count;
}

public sealed record DowntimeCalendarDayState(
    DateOnly Date,
    int ItemCount,
    string Summary);

public sealed record DowntimeScheduleItemState(
    string EventId,
    string Title,
    string Kind,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string LaneId,
    string LaneTitle)
{
    public string TimeWindow => FormatTimeWindow(StartsAtUtc, EndsAtUtc);

    private static string FormatTimeWindow(DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc)
    {
        if (endsAtUtc is null)
        {
            return startsAtUtc.ToLocalTime().ToString("u");
        }

        DateTimeOffset ends = endsAtUtc.Value.ToLocalTime();
        DateTimeOffset starts = startsAtUtc.ToLocalTime();
        if (starts.Date == ends.Date)
        {
            return $"{starts:u} -> {ends:HH:mm}";
        }

        return $"{starts:u} -> {ends:u}";
    }
}

public static class DowntimePlannerProjector
{
    public static DowntimePlannerState? FromJournal(JournalPanelProjection? projection)
    {
        if (projection is null)
        {
            return null;
        }

        DowntimeScheduleItemState[] schedule = projection.TimelineEvents
            .OrderBy(item => item.StartsAtUtc)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .Select(ToScheduleItem)
            .ToArray();

        DowntimePlannerLaneState[] lanes = schedule
            .GroupBy(item => item.LaneId, StringComparer.Ordinal)
            .Select(group => new DowntimePlannerLaneState(
                LaneId: group.Key,
                Title: group.First().LaneTitle,
                Items: group.ToArray()))
            .OrderByDescending(lane => lane.ItemCount)
            .ThenBy(lane => lane.Title, StringComparer.Ordinal)
            .ToArray();

        DowntimeCalendarDayState[] calendar = schedule
            .GroupBy(item => DateOnly.FromDateTime(item.StartsAtUtc.UtcDateTime.Date))
            .Select(group => new DowntimeCalendarDayState(
                Date: group.Key,
                ItemCount: group.Count(),
                Summary: BuildCalendarSummary(group)))
            .OrderBy(day => day.Date)
            .ToArray();

        return new DowntimePlannerState(
            PlannerLanes: lanes,
            CalendarDays: calendar,
            ScheduleItems: schedule);
    }

    private static DowntimeScheduleItemState ToScheduleItem(TimelineEventView timelineEvent)
    {
        (string laneId, string laneTitle) = ResolveLane(timelineEvent.Kind);
        return new DowntimeScheduleItemState(
            EventId: timelineEvent.EventId,
            Title: timelineEvent.Title,
            Kind: timelineEvent.Kind,
            StartsAtUtc: timelineEvent.StartsAtUtc,
            EndsAtUtc: timelineEvent.EndsAtUtc,
            LaneId: laneId,
            LaneTitle: laneTitle);
    }

    private static string BuildCalendarSummary(IGrouping<DateOnly, DowntimeScheduleItemState> day)
    {
        IReadOnlyList<string> kinds = day
            .GroupBy(item => item.LaneTitle, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {group.Key}")
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return string.Join(" · ", kinds);
    }

    private static (string LaneId, string LaneTitle) ResolveLane(string kind)
    {
        if (string.Equals(kind, TimelineEventKinds.Downtime, StringComparison.OrdinalIgnoreCase))
        {
            return ("downtime", "Downtime");
        }

        if (string.Equals(kind, TimelineEventKinds.Training, StringComparison.OrdinalIgnoreCase))
        {
            return ("training", "Training");
        }

        if (kind.Contains("heal", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("addiction", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("recovery", StringComparison.OrdinalIgnoreCase))
        {
            return ("recovery", "Recovery");
        }

        if (string.Equals(kind, TimelineEventKinds.Reminder, StringComparison.OrdinalIgnoreCase))
        {
            return ("ops", "Ops Reminder");
        }

        return ("schedule", "Scheduled");
    }
}
