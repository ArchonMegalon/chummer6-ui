using System.Text.Json.Nodes;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ConditionMonitorEditorProjectorTests
{
    [TestMethod]
    public void TryProject_preserves_tracks_overflow_and_career_editability()
    {
        JsonObject section = new()
        {
            ["physicalTrack"] = 11,
            ["physicalFilled"] = 4,
            ["physicalOverflow"] = 3,
            ["physicalThresholdOffset"] = 1,
            ["physicalNaturalRecovery"] = "7",
            ["stunTrack"] = 10,
            ["stunFilled"] = 2,
            ["stunThresholdOffset"] = 0,
            ["stunNaturalRecovery"] = "6",
            ["physicalActsAsCore"] = false,
            ["stunActsAsMatrix"] = false,
            ["created"] = true
        };

        ConditionMonitorEditorState? result = ConditionMonitorEditorProjector.TryProject("conditionmonitor", section);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.CareerEditable);
        Assert.HasCount(2, result.Tracks);
        ConditionMonitorTrackState physical = result.Tracks.Single(track => track.Track == WorkspaceConditionMonitorTrack.Physical);
        Assert.AreEqual(4, physical.Filled);
        Assert.AreEqual(11, physical.TrackMaximum);
        Assert.AreEqual(3, physical.Overflow);
        Assert.AreEqual(14, physical.EditableMaximum);
    }

    [TestMethod]
    public void TryProject_labels_ai_alternate_tracks_without_changing_identity()
    {
        JsonObject section = new()
        {
            ["physicalTrack"] = 8,
            ["physicalFilled"] = 1,
            ["physicalOverflow"] = 0,
            ["stunTrack"] = 8,
            ["stunFilled"] = 3,
            ["physicalActsAsCore"] = true,
            ["stunActsAsMatrix"] = true,
            ["created"] = true
        };

        ConditionMonitorEditorState? result = ConditionMonitorEditorProjector.TryProject("conditionmonitor", section);

        Assert.IsNotNull(result);
        Assert.AreEqual("Core", result.Tracks.Single(track => track.Track == WorkspaceConditionMonitorTrack.Physical).Label);
        Assert.AreEqual("Matrix", result.Tracks.Single(track => track.Track == WorkspaceConditionMonitorTrack.Stun).Label);
    }

    [TestMethod]
    public void TryProject_fails_closed_for_invalid_or_unrelated_payloads()
    {
        JsonObject invalid = new()
        {
            ["physicalTrack"] = 10,
            ["physicalFilled"] = 11,
            ["stunTrack"] = 10,
            ["stunFilled"] = 0,
            ["created"] = true
        };

        Assert.IsNull(ConditionMonitorEditorProjector.TryProject("conditionmonitor", invalid));
        Assert.IsNull(ConditionMonitorEditorProjector.TryProject("attributes", invalid));
        Assert.IsNull(ConditionMonitorEditorProjector.TryProject("conditionmonitor", new JsonObject()));
    }
}
