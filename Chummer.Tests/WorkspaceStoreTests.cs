using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Chummer.Contracts.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public class WorkspaceStoreTests
{
    [TestMethod]
    public void File_workspace_store_readiness_probe_is_owner_scoped_and_leaves_no_payload()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            IWorkspaceStoreReadinessProbe probe = store;
            OwnerScope owner = new("hosted-build-readiness-test-owner");

            probe.Probe(owner);

            Assert.AreEqual(0, store.List(owner).Count);
            Assert.AreEqual(
                0,
                Directory.EnumerateFiles(
                    stateDirectory,
                    "*.probe",
                    SearchOption.AllDirectories).Count());
            Assert.ThrowsExactly<ArgumentException>(() => probe.Probe(OwnerScope.LocalSingleUser));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_create_and_get_roundtrip()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            WorkspaceDocument expected = new("<character><name>Neo</name></character>", RulesetId: RulesetDefaults.Sr5);

            WorkspaceStoreEntry created = Create(store, expected);
            WorkspaceStoredDocument stored = Get(store, created.Id);
            WorkspaceDocument actual = stored.Document;

            Assert.AreEqual(expected.State, actual.State);
            Assert.AreEqual(expected.PayloadEnvelope.Payload, actual.PayloadEnvelope.Payload);
            Assert.AreEqual(expected.Format, actual.Format);
            Assert.AreEqual(RulesetDefaults.Sr5, actual.PayloadEnvelope.RulesetId);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_persists_across_instances()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            CharacterWorkspaceId id;
            {
                FileWorkspaceStore store = new(stateDirectory);
                id = Create(
                    store,
                    new WorkspaceDocument("<character><alias>BLUE</alias></character>", RulesetId: RulesetDefaults.Sr5)).Id;
            }

            {
                FileWorkspaceStore store = new(stateDirectory);
                WorkspaceStoredDocument loaded = Get(store, id);
                StringAssert.Contains(loaded.Document.PayloadEnvelope.Payload, "BLUE");
            }
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_persists_ruleset_id_across_instances()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            CharacterWorkspaceId id;
            {
                FileWorkspaceStore store = new(stateDirectory);
                id = Create(
                    store,
                    new WorkspaceDocument("<character><name>Ruleset</name></character>", RulesetId: "SR6")).Id;
            }

            {
                FileWorkspaceStore store = new(stateDirectory);
                WorkspaceStoredDocument loaded = Get(store, id);
                Assert.AreEqual("sr6", loaded.Document.PayloadEnvelope.RulesetId);
            }
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_persists_internal_payload_envelope()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            CharacterWorkspaceId id = Create(
                store,
                new WorkspaceDocument(
                    "<character><name>Envelope</name></character>",
                    RulesetId: "SR6")).Id;
            string persistedPath = Path.Combine(stateDirectory, "workspaces", $"{id.Value}.json");

            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(persistedPath));
            JsonElement root = json.RootElement;
            Assert.IsTrue(root.TryGetProperty("Envelope", out JsonElement envelope));
            Assert.IsFalse(root.TryGetProperty("Content", out _));
            Assert.IsFalse(root.TryGetProperty("RulesetId", out _));
            Assert.AreEqual("sr6", envelope.GetProperty("RulesetId").GetString());
            Assert.AreEqual(1, envelope.GetProperty("SchemaVersion").GetInt32());
            Assert.AreEqual("workspace", envelope.GetProperty("PayloadKind").GetString());
            StringAssert.Contains(envelope.GetProperty("Payload").GetString() ?? string.Empty, "<character>");
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_reads_legacy_payload_without_envelope()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            CharacterWorkspaceId id = new("legacypayload");
            string persistedPath = Path.Combine(stateDirectory, "workspaces", $"{id.Value}.json");
            File.WriteAllText(
                persistedPath,
                """
                {
                  "Content": "<character><name>Legacy</name></character>",
                  "Format": "Chum5Xml",
                  "RulesetId": "SR6"
                }
                """);

            WorkspaceStoredDocument loaded = Get(store, id);

            StringAssert.Contains(loaded.Document.PayloadEnvelope.Payload, "Legacy");
            StringAssert.Contains(loaded.Document.State.Payload, "Legacy");
            Assert.AreEqual(WorkspaceDocumentFormat.NativeXml, loaded.Document.Format);
            Assert.AreEqual("sr6", loaded.Document.PayloadEnvelope.RulesetId);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_detects_sr4_ruleset_from_legacy_payload_content()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            CharacterWorkspaceId id = new("legacysr4");
            string persistedPath = Path.Combine(stateDirectory, "workspaces", $"{id.Value}.json");
            File.WriteAllText(
                persistedPath,
                """
                {
                  "Content": "<character><name>Starter Shadow</name><gameedition>SR4</gameedition></character>",
                  "Format": "NativeXml"
                }
                """);

            WorkspaceStoredDocument loaded = Get(store, id);

            Assert.AreEqual(RulesetDefaults.Sr4, loaded.Document.PayloadEnvelope.RulesetId);
            StringAssert.Contains(loaded.Document.PayloadEnvelope.Payload, "Starter Shadow");
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_rejects_invalid_ids()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            WorkspaceStoreReadResult read = store.Get(new CharacterWorkspaceId("../bad"));
            Assert.AreEqual(WorkspaceOperationOutcome.Missing, read.Outcome);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_returns_false_for_corrupt_payload()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            CharacterWorkspaceId id = Create(
                store,
                new WorkspaceDocument("<character><name>Neo</name></character>", RulesetId: RulesetDefaults.Sr5)).Id;
            string persistedPath = Path.Combine(stateDirectory, "workspaces", $"{id.Value}.json");

            File.WriteAllText(persistedPath, "{invalid-json");

            WorkspaceStoreReadResult read = store.Get(id);
            Assert.AreEqual(WorkspaceOperationOutcome.Corrupt, read.Outcome);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_lists_created_workspaces()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            CharacterWorkspaceId first = Create(
                store,
                new WorkspaceDocument("<character><name>First</name></character>", RulesetId: RulesetDefaults.Sr5)).Id;
            CharacterWorkspaceId second = Create(
                store,
                new WorkspaceDocument("<character><name>Second</name></character>", RulesetId: RulesetDefaults.Sr5)).Id;

            IReadOnlyList<WorkspaceStoreEntry> listed = store.List();

            CollectionAssert.AreEquivalent(
                new[] { first.Value, second.Value },
                listed.Select(item => item.Id.Value).ToArray());
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_delete_removes_workspace()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            WorkspaceStoreEntry created = Create(
                store,
                new WorkspaceDocument("<character><name>DeleteMe</name></character>", RulesetId: RulesetDefaults.Sr5));

            WorkspaceStoreMutationResult deleted = store.Delete(created.Id, created.ContentRevision);
            WorkspaceStoreReadResult read = store.Get(created.Id);

            Assert.IsTrue(deleted.Success, deleted.Error);
            Assert.AreEqual(WorkspaceOperationOutcome.Missing, read.Outcome);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void File_workspace_store_isolates_owner_scopes_and_preserves_local_single_user_path()
    {
        string stateDirectory = CreateTempStateDirectory();
        try
        {
            FileWorkspaceStore store = new(stateDirectory);
            OwnerScope alice = new("Alice@example.com");

            CharacterWorkspaceId globalId = Create(
                store,
                new WorkspaceDocument("<character><name>Global</name></character>", RulesetId: RulesetDefaults.Sr5)).Id;
            CharacterWorkspaceId aliceId = Create(
                store,
                alice,
                new WorkspaceDocument("<character><name>Alice</name></character>", RulesetId: RulesetDefaults.Sr6)).Id;
            WorkspaceStoredDocument globalDocument = Get(store, globalId);
            WorkspaceStoreReadResult crossOwnerRead = store.Get(alice, globalId);
            WorkspaceStoredDocument aliceDocument = Get(store, alice, aliceId);

            Assert.AreEqual(WorkspaceOperationOutcome.Missing, crossOwnerRead.Outcome);
            Assert.AreEqual(RulesetDefaults.Sr5, globalDocument.Document.PayloadEnvelope.RulesetId);
            Assert.AreEqual(RulesetDefaults.Sr6, aliceDocument.Document.PayloadEnvelope.RulesetId);
            Assert.HasCount(1, store.List());
            Assert.HasCount(1, store.List(alice));
            Assert.IsTrue(File.Exists(Path.Combine(stateDirectory, "workspaces", $"{globalId.Value}.json")));
            Assert.IsTrue(Directory.Exists(Path.Combine(stateDirectory, "owners")));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static string CreateTempStateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "chummer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static WorkspaceStoreEntry Create(IWorkspaceStore store, WorkspaceDocument document)
    {
        WorkspaceStoreMutationResult result = store.CreateWorkspaceDocument(document);
        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNotNull(result.Entry);
        return result.Entry.Value;
    }

    private static WorkspaceStoreEntry Create(
        IWorkspaceStore store,
        OwnerScope owner,
        WorkspaceDocument document)
    {
        WorkspaceStoreMutationResult result = store.CreateWorkspaceDocument(owner, document);
        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNotNull(result.Entry);
        return result.Entry.Value;
    }

    private static WorkspaceStoredDocument Get(IWorkspaceStore store, CharacterWorkspaceId id)
    {
        WorkspaceStoreReadResult result = store.Get(id);
        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNotNull(result.Value);
        return result.Value;
    }

    private static WorkspaceStoredDocument Get(
        IWorkspaceStore store,
        OwnerScope owner,
        CharacterWorkspaceId id)
    {
        WorkspaceStoreReadResult result = store.Get(owner, id);
        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNotNull(result.Value);
        return result.Value;
    }
}
