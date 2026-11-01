"""Light offline checks using real checkout pins and disposable Git repositories."""

import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("workspace", ROOT / "scripts/prepare-agent-workspace.py")
workspace = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(workspace)


class WorkspaceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="friday paths ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "checkout with spaces"
        self.root.mkdir()
        self.manifest = json.loads((ROOT / "config/agent-workspace.windows.json").read_text())
        for name in self.manifest["required_paths"]:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, target)
        shutil.copyfile(ROOT / ".gitignore", self.root / ".gitignore")
        self.git("init", "-q")
        self.git("config", "user.name", "Offline test")
        self.git("config", "user.email", "offline@example.invalid")
        self.git("config", "core.autocrlf", "false")
        self.git("remote", "add", "origin", self.manifest["repository"])
        self.git("add", ".")
        self.git("commit", "-qm", "test baseline")
        self.manifest["known_baseline"] = self.git("rev-parse", "HEAD")
        self.env = patch.dict(os.environ, {"TEABLE_BASE_URL": "https://teable.invalid",
                                         "TEABLE_BASE_ID": "base-test", "TEABLE_API_KEY": "test-token-never-log"})
        self.env.start()
        self.addCleanup(self.env.stop)
        # Never inspect/install a host SDK or exercise any network in unit checks.
        original_which = shutil.which
        self.tools = patch.object(workspace.shutil, "which", side_effect=lambda name: None if name == "dotnet" else original_which(name))
        self.tools.start()
        self.addCleanup(self.tools.stop)
        self.cache = Path(self.temp.name) / "private task cache"
        self.cache_default = patch.object(workspace, "default_cache_root", return_value=self.cache)
        self.cache_default.start()
        self.addCleanup(self.cache_default.stop)
        self.intake = workspace.package_intake(self.root)
        self.intake_patch = patch.object(workspace, "package_intake", return_value=self.intake)
        self.intake_patch.start()
        self.addCleanup(self.intake_patch.stop)

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.root, check=True, capture_output=True, text=True).stdout.strip()

    def check(self):
        return workspace.check_workspace(self.root, self.manifest)

    def task(self, **updates):
        task = {"task_id": "windows-1", "owner": "windows-worker", "ready": True,
                "title": "Check desktop", "owner_session_id": "session-1", "assignment_generation": 3,
                "base_sha": self.manifest["known_baseline"], "allowed_paths": ["Chummer.Avalonia/Views"],
                "acceptance": "Report actual Windows results."}
        task.update(updates)
        return task

    def prompt(self, tasks):
        with patch.object(workspace, "coordination", side_effect=[{"ok": True}, {"worker_id": "windows-worker", "tasks": tasks}]):
            return workspace.next_prompt(self.root, "windows-worker", self.check())

    def test_real_pins_and_clean_clone_are_checked_without_build_claim(self):
        report = self.check()
        self.assertTrue(report["workspace_valid"])
        self.assertFalse(report["worktree_dirty"])
        self.assertFalse(report["build_ready"])
        self.assertFalse(report["build_executed"])
        self.assertFalse(report["sdk_installed"])
        self.assertTrue(report["package_setup"]["available"])
        self.assertTrue(report["package_setup"]["download_manifest_complete"])
        # Production manifest is validated against actual repo files, not a synthetic copy of its pins.
        production = json.loads((ROOT / "config/agent-workspace.windows.json").read_text())
        packages = workspace.package_pins(ROOT, production)
        self.assertTrue(any(p["packageId"] == "Chummer.Ui.Kit" for p in packages))

    def test_baseline_remains_valid_after_local_commit_and_dirty_edits(self):
        (self.root / "local.txt").write_text("task commit")
        self.git("add", "local.txt")
        self.git("commit", "-qm", "task commit")
        self.assertFalse(self.check()["worktree_dirty"])
        (self.root / "local.txt").write_text("work in progress")
        self.assertTrue(self.check()["worktree_dirty"])
        self.assertIn("BEGIN UNTRUSTED ASSIGNMENT", self.prompt([self.task()]))

    def test_wrong_origin_and_missing_baseline_fail_without_echoing_url(self):
        self.git("remote", "set-url", "origin", "https://test-token-never-log@github.com/other/repo.git")
        with self.assertRaises(workspace.SetupError) as failure:
            self.check()
        self.assertNotIn("test-token", str(failure.exception))
        self.git("remote", "set-url", "origin", self.manifest["repository"])
        self.manifest["known_baseline"] = "0" * 40
        with self.assertRaises(workspace.SetupError):
            self.check()

    def test_portable_relative_paths_and_traversal(self):
        self.assertEqual(workspace.relative_path(self.root, "Chummer.Avalonia\\Views"), self.root / "Chummer.Avalonia/Views")
        for name in ("../outside", "C:\\outside", "\\\\server\\share", "/absolute", "foo/../../bar"):
            with self.subTest(name=name), self.assertRaises(workspace.SetupError):
                workspace.relative_path(self.root, name)
        for origin in ("git@github.com:ArchonMegalon/chummer6-ui.git", "ssh://git@github.com/ArchonMegalon/chummer6-ui.git"):
            self.git("remote", "set-url", "origin", origin)
            self.assertTrue(self.check()["workspace_valid"])

    def test_missing_required_file_is_not_a_successful_bootstrap(self):
        (self.root / "Chummer.Avalonia/Chummer.Avalonia.csproj").unlink()
        with self.assertRaises(workspace.SetupError):
            self.check()

    def test_actual_sdk_and_msbuild_pin_mutations_fail(self):
        global_json = self.root / "global.json"
        original = global_json.read_text()
        global_json.write_text(original.replace("10.0.103", "10.0.999"))
        with self.assertRaises(workspace.SetupError):
            self.check()
        global_json.write_text(original)
        props = self.root / "Directory.Build.props"
        props.write_text(props.read_text().replace("0.1.0-preview", "0.2.0-preview"))
        with self.assertRaises(workspace.SetupError):
            self.check()

    def test_lock_pin_mutation_and_sibling_fallback_fail(self):
        lock_path = self.root / "config/package-plane.lock.json"
        lock = json.loads(lock_path.read_text())
        lock["coreRuntimeFeed"]["packages"][0]["version"] = "0.0.0-unapproved"
        lock_path.write_text(json.dumps(lock))
        with self.assertRaises(workspace.SetupError):
            self.check()
        shutil.copyfile(ROOT / "config/package-plane.lock.json", lock_path)
        for name, value in (("CHUMMER_USE_LOCAL_COMPATIBILITY_TREE", "1"),
                            ("ChummerUseLocalCompatibilityTree", "true"),
                            ("CHUMMER_UI_KIT_PACKAGE_VERSION", "latest")):
            with self.subTest(name=name), patch.dict(os.environ, {name: value}), self.assertRaises(workspace.SetupError):
                self.check()
        props = self.root / "Directory.Build.props"
        props.write_text(props.read_text().replace(
            ">false</ChummerUseLocalCompatibilityTree>", ">true</ChummerUseLocalCompatibilityTree>"))
        with self.assertRaises(workspace.SetupError):
            self.check()

    def test_fake_shared_package_bytes_are_rejected_against_real_lock(self):
        packages = workspace.package_pins(self.root, self.manifest)
        feed = Path(self.temp.name) / "feed"
        feed.mkdir()
        (feed / packages[0]["fileName"]).write_bytes(b"wrong package, correct name")
        with self.assertRaisesRegex(workspace.SetupError, "does not match"):
            workspace.inspect_feed(self.root, packages, feed)

    def test_lock_formatting_is_portable_but_checksum_changes_fail(self):
        lock_path = self.root / "config/package-plane.lock.json"
        lock = json.loads(lock_path.read_text())
        lock_path.write_bytes(json.dumps(lock, indent=4).replace("\n", "\r\n").encode())
        self.assertTrue(self.check()["workspace_valid"])
        lock["coreRuntimeFeed"]["packages"][0]["sha256"] = "f" * 64
        lock_path.write_text(json.dumps(lock))
        with self.assertRaisesRegex(workspace.SetupError, "lock differs"):
            self.check()

    def test_no_ready_task_replaces_saved_prompt_and_controls_are_data(self):
        path = Path(self.temp.name) / "state/prompt.txt"
        workspace.save_prompt(self.root, path, "old assignment")
        workspace.save_prompt(self.root, path, self.prompt([]))
        self.assertIn("No ready assignment", path.read_text())
        prompt = self.prompt([self.task(title="$(touch should-not-exist) test-token-never-log\u001b[31m")])
        workspace.save_prompt(self.root, path, prompt)
        self.assertNotIn("test-token-never-log", path.read_text())
        self.assertIn("[REDACTED]", path.read_text())
        self.assertFalse((self.root / "should-not-exist").exists())
        with self.assertRaises(workspace.SetupError):
            workspace.save_prompt(self.root, self.root / "prompt.txt", prompt)

    def test_escaped_token_is_redacted_from_json_handoff(self):
        token = 'test-"unicode-\u2603-token'
        with patch.dict(os.environ, {"TEABLE_API_KEY": token}):
            prompt = self.prompt([self.task(title="Do not log " + token)])
        self.assertNotIn(token, prompt)
        self.assertNotIn(json.dumps(token)[1:-1], prompt)
        self.assertIn("[REDACTED]", prompt)

    def test_ambiguous_wrong_owner_or_wrong_task_base_do_not_emit_prompt(self):
        for tasks in ([self.task(), self.task(task_id="windows-2")],
                      [self.task(owner="different-worker")], [self.task(base_sha="0" * 40)]):
            with self.subTest(tasks=tasks), self.assertRaises(workspace.SetupError):
                self.prompt(tasks)

    def test_real_child_interface_only_reads_and_never_places_token_in_argv(self):
        child = self.root / "scripts/agent-coordination.py"
        payload = {"worker_id": "windows-worker", "tasks": [self.task()]}
        child.write_text("import json,sys\nfrom pathlib import Path\n"
                         "with Path('invocations.jsonl').open('a') as out: out.write(json.dumps(sys.argv[1:])+'\\n')\n"
                         "print(json.dumps({'ok':True} if sys.argv[1]=='doctor' else " + repr(payload) + "))\n")
        with patch.object(workspace.subprocess, "run", wraps=subprocess.run) as calls:
            prompt = workspace.next_prompt(self.root, "windows-worker", self.check())
        for call in calls.call_args_list:
            command = call.args[0]
            self.assertFalse(call.kwargs.get("shell", False))
            self.assertNotIn("test-token-never-log", json.dumps(command))
            if command[0] != "git":
                self.assertEqual(command[:2], [workspace.sys.executable, str(child)])
        invocations = [json.loads(line) for line in (self.root / "invocations.jsonl").read_text().splitlines()]
        self.assertEqual(invocations, [["doctor"], ["next", "--worker-id", "windows-worker"]])
        self.assertIn("windows-1", prompt)
        self.assertNotIn("test-token-never-log", json.dumps(invocations))

    def test_check_and_prepare_modes_are_offline_and_keep_restore_files_intact(self):
        manifest_path = self.root / "config/agent-workspace.windows.json"
        manifest_path.write_text(json.dumps(self.manifest))
        path = Path(self.temp.name) / "new state/prompt.txt"
        lock_path = self.root / "config/package-plane.lock.json"
        before = lock_path.read_bytes()
        for mode in ("check", "prepare"):
            output = io.StringIO()
            with contextlib.redirect_stdout(output), patch.object(workspace, "coordination", side_effect=AssertionError("unexpected network")):
                result = workspace.main(["--repo-root", str(self.root), "--mode", mode, "--prompt-path", str(path)])
            self.assertEqual(result, 0)
            self.assertTrue(json.loads(output.getvalue())["workspace_valid"])
            self.assertEqual(path.parent.exists(), mode == "prepare")
            self.assertFalse(path.exists())
            self.assertEqual(lock_path.read_bytes(), before)

    def test_cli_failure_suppresses_child_secret_output_and_expires_old_prompt(self):
        (self.root / "scripts/agent-coordination.py").write_text(
            "import sys\nprint('test-token-never-log',file=sys.stderr)\nsys.exit(2)\n")
        manifest_path = self.root / "config/agent-workspace.windows.json"
        manifest_path.write_text(json.dumps(self.manifest))
        path = Path(self.temp.name) / "state/prompt.txt"
        workspace.save_prompt(self.root, path, "stale assignment")
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            result = workspace.main(["--repo-root", str(self.root), "--mode", "next", "--worker-id", "windows-worker", "--prompt-path", str(path)])
        self.assertEqual(result, 2)
        self.assertNotIn("test-token-never-log", out.getvalue() + err.getvalue() + path.read_text())
        self.assertIn("must be refreshed", path.read_text())

    def seed_exact_fixture_assets(self):
        """Keep real IDs/versions, with byte pins for deliberately synthetic test ZIPs."""
        lock_path = self.root / "config/package-plane.lock.json"
        lock = json.loads(lock_path.read_text())
        members = {}
        bundle = io.BytesIO()
        with zipfile.ZipFile(bundle, "w") as archive:
            for group in self.manifest["package_groups"]:
                for row in lock[group]["packages"]:
                    package = io.BytesIO()
                    with zipfile.ZipFile(package, "w") as nupkg:
                        nupkg.writestr("fixture.nuspec", '<package><metadata><id>' + row["packageId"] +
                                      '</id><version>' + row["version"] + '</version></metadata></package>')
                    content = package.getvalue()
                    row.update(sha256=hashlib.sha256(content).hexdigest(), sizeBytes=len(content))
                    members[row["fileName"]] = "packages/" + row["fileName"]
                    archive.writestr(members[row["fileName"]], content)
        self.bundle = bundle.getvalue()
        lock_path.write_text(json.dumps(lock), encoding="utf-8")
        canonical = json.dumps(lock, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()
        self.manifest["package_lock_sha256"] = hashlib.sha256(canonical).hexdigest()
        downloads = {"schemaVersion": 1, "unavailablePackages": [], "assets": [{
            "fileName": "fixture.zip", "kind": "zip",
            "url": "https://github.com/ArchonMegalon/chummer6-ui/releases/download/exact/fixture.zip",
            "sha256": hashlib.sha256(self.bundle).hexdigest(), "sizeBytes": len(self.bundle), "members": members}]}
        (self.root / "config/windows-package-downloads.json").write_text(json.dumps(downloads), encoding="utf-8")
        (self.cache / "downloads").mkdir(parents=True)
        (self.cache / "downloads/fixture.zip").write_bytes(self.bundle)

    @contextlib.contextmanager
    def sdk(self, *, windows=True, version="10.0.103", broken=False):
        original_run = workspace.run
        original_which = shutil.which

        def run(command, root):
            if command[0] != "dotnet":
                return original_run(command, root)
            if broken:
                raise workspace.SetupError("SDK command failed")
            return version + " [fixture SDK path]" if command[1] == "--list-sdks" else version

        with patch.object(workspace, "native_windows", return_value=windows), \
                patch.object(workspace, "run", side_effect=run), \
                patch.object(workspace.shutil, "which", side_effect=lambda name: "dotnet" if name == "dotnet" else original_which(name)):
            yield

    def test_offline_setup_reuses_intake_and_creates_only_local_mapped_configuration(self):
        self.seed_exact_fixture_assets()
        before = self.git("status", "--porcelain")
        with patch.object(self.intake, "download", side_effect=AssertionError("unexpected network")):
            report = workspace.setup_workspace(self.root, self.manifest)
            resumed = workspace.setup_workspace(self.root, self.manifest)
        self.assertEqual(report["packages"]["verified_count"], 14)
        self.assertTrue(resumed["nuget_config_verified"])
        self.assertFalse(report["build_ready"])
        self.assertFalse(report["build_executed"])
        self.assertEqual(before, self.git("status", "--porcelain"))
        config = ET.parse(self.cache / "NuGet.Config").getroot()
        self.assertIsNotNone(config.find("./packageSources/clear"))
        self.assertEqual({p.get("key"): p.get("value") for p in config.findall("./packageSources/add")},
                         {"chummer-exact": str(self.cache / "feed"), "nuget.org": "https://api.nuget.org/v3/index.json"})
        self.assertEqual([(p.get("key"), p.find("package").get("pattern")) for p in config.findall("./packageSourceMapping/packageSource")],
                         [("chummer-exact", "Chummer.*"), ("nuget.org", "*")])
        self.assertIn("BEGIN UNTRUSTED ASSIGNMENT", self.prompt([self.task()]))

    def test_setup_download_requires_explicit_opt_in_and_preserves_pins(self):
        self.seed_exact_fixture_assets()
        (self.cache / "downloads/fixture.zip").unlink()
        before = (self.root / "config/package-plane.lock.json").read_bytes()
        with patch.object(self.intake, "download", return_value=self.bundle) as download:
            with self.assertRaisesRegex(workspace.SetupError, "offline"):
                workspace.setup_workspace(self.root, self.manifest)
            download.assert_not_called()
            self.assertFalse((self.cache / "NuGet.Config").exists())
            workspace.setup_workspace(self.root, self.manifest, download=True)
            download.assert_called_once()
        self.assertEqual(before, (self.root / "config/package-plane.lock.json").read_bytes())
        self.assertFalse((self.cache / "build.lock").exists())

    def test_setup_cli_accepts_new_explicit_feed_and_next_survives_damaged_feed(self):
        self.seed_exact_fixture_assets()
        (self.root / "config/agent-workspace.windows.json").write_text(json.dumps(self.manifest))
        feed = Path(self.temp.name) / "new explicit feed"
        common = ["--repo-root", str(self.root), "--cache-root", str(self.cache), "--package-feed", str(feed)]
        output = io.StringIO()
        with contextlib.redirect_stdout(output), patch.object(self.intake, "download", side_effect=AssertionError("unexpected network")):
            self.assertEqual(workspace.main([*common, "--mode", "setup"]), 0)
        self.assertEqual(json.loads(output.getvalue())["packages"]["verified_count"], 14)
        (feed / "extra.txt").write_text("preserve")
        prompt_path = Path(self.temp.name) / "assignment.txt"
        with contextlib.redirect_stdout(io.StringIO()), patch.object(workspace, "coordination", side_effect=[
                {"ok": True}, {"worker_id": "windows-worker", "tasks": [self.task()]}]):
            self.assertEqual(workspace.main([*common, "--mode", "next", "--worker-id", "windows-worker",
                                             "--prompt-path", str(prompt_path)]), 0)
        self.assertIn("BEGIN UNTRUSTED ASSIGNMENT", prompt_path.read_text())
        self.assertEqual((feed / "extra.txt").read_text(), "preserve")

    def test_readiness_becomes_true_only_with_verified_windows_sdk_feed_and_config(self):
        self.seed_exact_fixture_assets()
        workspace.setup_workspace(self.root, self.manifest)
        with self.sdk():
            report = self.check()
        self.assertTrue(report["build_ready"])
        self.assertTrue(report["prerequisites_ready"])
        self.assertEqual(report["build_blockers"], [])
        self.assertFalse(report["restore_executed"])
        self.assertFalse(report["build_executed"])
        self.assertFalse(report["native_runtime_validated"])
        for options in ({"windows": False}, {"version": "10.0.999"}, {"broken": True}):
            with self.sdk(**options):
                self.assertFalse(self.check()["build_ready"])
                with self.assertRaisesRegex(workspace.SetupError, "prerequisites"):
                    workspace.build_desktop(self.root, self.manifest)
                self.assertIn("BEGIN UNTRUSTED ASSIGNMENT", self.prompt([self.task()]))
        (self.cache / "NuGet.Config").write_text("changed source policy")
        with self.sdk():
            self.assertFalse(self.check()["build_ready"])

    def test_extra_or_corrupt_feed_fails_without_repair_and_cache_must_be_external(self):
        self.seed_exact_fixture_assets()
        workspace.setup_workspace(self.root, self.manifest)
        extra = self.cache / "feed/extra.txt"
        extra.write_text("preserve me")
        with self.assertRaisesRegex(workspace.SetupError, "extra"):
            workspace.setup_workspace(self.root, self.manifest, download=True)
        self.assertEqual(extra.read_text(), "preserve me")
        report = self.check()
        self.assertFalse(report["build_ready"])
        self.assertIn("extra", report["packages"]["verification_error"])
        with patch.object(workspace, "coordination", side_effect=[{"ok": True}, {"worker_id": "windows-worker", "tasks": [self.task()]}]):
            report = workspace.check_workspace(self.root, self.manifest, self.cache / "feed", strict_feed=False)
            self.assertIn("BEGIN UNTRUSTED ASSIGNMENT", workspace.next_prompt(self.root, "windows-worker", report))
        with self.assertRaisesRegex(workspace.SetupError, "outside"):
            workspace.setup_workspace(self.root, self.manifest, self.root / ".tmp/cache")

    def test_local_lock_refuses_concurrent_operation_and_preserves_existing_lock(self):
        self.seed_exact_fixture_assets()
        path = self.cache / "build.lock"
        path.write_text("active operation")
        with self.assertRaisesRegex(workspace.SetupError, "lock exists"):
            workspace.setup_workspace(self.root, self.manifest)
        self.assertEqual(path.read_text(), "active operation")
        self.assertFalse((self.cache / "feed").exists())

    def mock_dotnet(self, calls, *, failed_stage=None):
        original_run = subprocess.run

        def run(command, **kwargs):
            if command[0] != "dotnet":
                return original_run(command, **kwargs)
            calls.append((command, kwargs))
            # A second cache selection still uses this checkout's build lock.
            with self.assertRaisesRegex(workspace.SetupError, "lock exists"):
                with workspace.local_build_lock(self.root):
                    self.fail("concurrent build lock acquired")
            return subprocess.CompletedProcess(command, 1 if command[1] == failed_stage else 0,
                                               "fixture output test-token-never-log", "fixture stderr")
        return patch.object(workspace.subprocess, "run", side_effect=run)

    def test_mocked_native_commands_are_serial_primary_only_isolated_and_redacted(self):
        self.seed_exact_fixture_assets()
        workspace.setup_workspace(self.root, self.manifest)
        calls = []
        with self.sdk(), self.mock_dotnet(calls), patch.dict(os.environ, {"RestoreSources": "unapproved-source"}):
            report = workspace.build_desktop(self.root, self.manifest)
        self.assertTrue(report["build_succeeded"])
        self.assertTrue(report["restore_succeeded"])
        self.assertFalse(report["native_runtime_validated"])
        self.assertEqual([c[0][1] for c in calls], ["restore", "build"])
        for command, options in calls:
            self.assertEqual(command[2], "Chummer.Avalonia/Chummer.Avalonia.csproj")
            self.assertIn("-p:ChummerUseLocalCompatibilityTree=false", command)
            self.assertNotIn("test-token-never-log", json.dumps(command))
            self.assertNotIn("TEABLE_API_KEY", options["env"])
            self.assertNotIn("RestoreSources", options["env"])
            self.assertEqual(options["env"]["NUGET_PACKAGES"], str(self.cache / "nuget-packages"))
            self.assertFalse(options.get("shell", False))
        self.assertIn("--no-restore", calls[1][0])
        self.assertIn("-m:1", calls[1][0])
        self.assertIn(str(self.cache / "NuGet.Config"), calls[0][0])
        self.assertNotIn("test-token-never-log", (self.cache / "build.log").read_text())
        self.assertFalse((self.cache / "build.lock").exists())

    def test_mocked_restore_failure_skips_build_and_compatibility_is_explicit(self):
        self.seed_exact_fixture_assets()
        workspace.setup_workspace(self.root, self.manifest)
        calls = []
        with self.sdk(), self.mock_dotnet(calls, failed_stage="restore"):
            report = workspace.build_desktop(self.root, self.manifest, project="compatibility")
        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0][0][2], "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj")
        self.assertEqual(report["failed_stage"], "restore")
        self.assertTrue(report["restore_executed"])
        self.assertFalse(report["build_executed"])
        self.assertFalse(report["build_succeeded"])

    def test_mocked_build_failure_timeout_and_launch_failure_are_reported_truthfully(self):
        self.seed_exact_fixture_assets()
        workspace.setup_workspace(self.root, self.manifest)
        calls = []
        with self.sdk(), self.mock_dotnet(calls, failed_stage="build"):
            report = workspace.build_desktop(self.root, self.manifest)
        self.assertTrue(report["restore_succeeded"])
        self.assertTrue(report["build_executed"])
        self.assertFalse(report["build_succeeded"])
        self.assertEqual(report["build_exit_code"], 1)
        original_run = subprocess.run
        for failure in (OSError("not started"), subprocess.TimeoutExpired("dotnet", 900)):
            def run(command, **kwargs):
                if command[0] == "dotnet":
                    raise failure
                return original_run(command, **kwargs)
            with self.sdk(), patch.object(workspace.subprocess, "run", side_effect=run):
                report = workspace.build_desktop(self.root, self.manifest)
            self.assertFalse(report["build_executed"])
            self.assertFalse(report["restore_succeeded"])
            self.assertEqual(report["restore_executed"], isinstance(failure, subprocess.TimeoutExpired))
            self.assertEqual((self.cache / "build.lock").exists(),
                             isinstance(failure, subprocess.TimeoutExpired))
            if isinstance(failure, subprocess.TimeoutExpired):
                self.assertTrue(report["operation_requires_reconciliation"])
                with self.sdk(), self.assertRaisesRegex(workspace.SetupError, "lock exists"):
                    workspace.build_desktop(self.root, self.manifest)

    def test_interrupted_build_retains_lock_until_processes_are_reconciled(self):
        self.seed_exact_fixture_assets()
        workspace.setup_workspace(self.root, self.manifest)
        original_run = subprocess.run

        def run(command, **kwargs):
            if command[0] == "dotnet":
                raise KeyboardInterrupt()
            return original_run(command, **kwargs)

        with self.sdk(), patch.object(workspace.subprocess, "run", side_effect=run):
            with self.assertRaises(KeyboardInterrupt):
                workspace.build_desktop(self.root, self.manifest)
        self.assertTrue((self.cache / "build.lock").exists())


if __name__ == "__main__":
    unittest.main()
