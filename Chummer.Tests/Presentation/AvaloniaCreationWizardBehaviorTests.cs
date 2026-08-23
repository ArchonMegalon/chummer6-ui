using Chummer.Avalonia;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AvaloniaCreationWizardBehaviorTests
{
    [TestMethod]
    public void Checkpoint_store_round_trips_exact_navigation_and_recovers_fail_closed_from_corruption()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"chummer-avalonia-wizard-checkpoint-{Guid.NewGuid():N}");
        try
        {
            AvaloniaCreationWizardCheckpointStore store = new(root);
            CharacterCreationWizardDesktopCheckpoint expected = new(
                Schema: CharacterCreationWizardDesktopSchemas.CheckpointV1,
                WorkspaceId: "workspace-a",
                WorkspaceRevision: 7,
                SnapshotDigest: "sha256:" + new string('a', 64),
                SelectedStepId: "foundation");

            store.Save(expected);
            AvaloniaCreationWizardCheckpointLoad loaded = store.Load(expected.WorkspaceId);
            Assert.AreEqual(expected, loaded.Checkpoint);
            Assert.IsNull(loaded.RecoveryReason);

            string storedPath = Directory.EnumerateFiles(root, "*.json").Single();
            File.WriteAllText(storedPath, "{not-json");
            AvaloniaCreationWizardCheckpointLoad corrupt = store.Load(expected.WorkspaceId);
            Assert.IsNull(corrupt.Checkpoint);
            Assert.AreEqual(
                CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint,
                corrupt.RecoveryReason);

            store.Save(expected with { SelectedStepId = "life-modules" });
            Assert.AreEqual(
                "life-modules",
                store.Load(expected.WorkspaceId).Checkpoint?.SelectedStepId);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Checkpoint_store_rejects_oversized_local_file_before_deserialization()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"chummer-avalonia-wizard-checkpoint-oversized-{Guid.NewGuid():N}");
        try
        {
            AvaloniaCreationWizardCheckpointStore store = new(root);
            CharacterCreationWizardDesktopCheckpoint checkpoint = new(
                Schema: CharacterCreationWizardDesktopSchemas.CheckpointV1,
                WorkspaceId: "workspace-oversized",
                WorkspaceRevision: 3,
                SnapshotDigest: "sha256:" + new string('c', 64),
                SelectedStepId: "foundation");
            store.Save(checkpoint);
            string storedPath = Directory.EnumerateFiles(root, "*.json").Single();
            File.WriteAllBytes(
                storedPath,
                new byte[AvaloniaCreationWizardCheckpointStore.MaximumCheckpointBytes + 1]);

            AvaloniaCreationWizardCheckpointLoad oversized = store.Load(checkpoint.WorkspaceId);

            Assert.IsNull(oversized.Checkpoint);
            Assert.AreEqual(
                CharacterCreationWizardCheckpointInvalidationReasons.InvalidCheckpoint,
                oversized.RecoveryReason);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Checkpoint_store_hashes_workspace_identity_instead_of_using_it_as_a_path()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"chummer-avalonia-wizard-checkpoint-path-{Guid.NewGuid():N}");
        try
        {
            AvaloniaCreationWizardCheckpointStore store = new(root);
            CharacterCreationWizardDesktopCheckpoint checkpoint = new(
                Schema: CharacterCreationWizardDesktopSchemas.CheckpointV1,
                WorkspaceId: "../../not-a-path",
                WorkspaceRevision: 1,
                SnapshotDigest: "sha256:" + new string('b', 64),
                SelectedStepId: "method");

            store.Save(checkpoint);

            string storedPath = Directory.EnumerateFiles(root, "*.json").Single();
            Assert.AreEqual(root, Path.GetDirectoryName(storedPath));
            Assert.AreEqual(64, Path.GetFileNameWithoutExtension(storedPath).Length);
            Assert.AreEqual(checkpoint, store.Load(checkpoint.WorkspaceId).Checkpoint);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
