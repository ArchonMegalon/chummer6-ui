"""Offline intake checks. No SDK, sibling repository, live network or product build."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import stat
import tempfile
import unittest
from unittest import mock
import urllib.request
import warnings
import zipfile


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("windows_package_feed", ROOT / "scripts/fetch-windows-package-feed.py")
feed = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(feed)


def zip_bytes(entries):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            for name, content in entries:
                archive.writestr(name, content)
    return buffer.getvalue()


def package_bytes(package_id, version, extra=()):
    xml = (f'<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
           f'<metadata><id>{package_id}</id><version>{version}</version></metadata></package>')
    return zip_bytes([(f"{package_id}.nuspec", xml), ("lib/net10.0/fixture.dll", b"fixture"), *extra])


def pin(content):
    return {"sha256": hashlib.sha256(content).hexdigest(), "sizeBytes": len(content)}


class IntakeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.output = self.root / "feed"
        self.cache = self.root / "cache"
        self.cache.mkdir()
        self.lock_path = self.root / "lock.json"
        self.manifest_path = self.root / "sources.json"
        self.lock, self.packages = {}, {}
        for key, count in feed.FEEDS.items():
            rows = []
            for index in range(count):
                package_id, version = f"Chummer.{key}{index}", "0.1.0-preview"
                filename = f"{package_id}.{version}.nupkg"
                content = package_bytes(package_id, version)
                rows.append({"fileName": filename, "packageId": package_id, "version": version, **pin(content)})
                self.packages[filename] = content
            self.lock[key] = {"packages": rows}
        self.bundle = zip_bytes([(f"packages/{n}", c) for n, c in self.packages.items()])
        self.manifest = {"schemaVersion": 1, "unavailablePackages": [], "assets": [{
            "fileName": "fixture.zip", "kind": "zip",
            "url": "https://github.com/ArchonMegalon/chummer6-core/releases/download/exact/fixture.zip",
            **pin(self.bundle), "members": {n: f"packages/{n}" for n in self.packages}}]}
        (self.cache / "fixture.zip").write_bytes(self.bundle)
        self.save()

    def save(self):
        self.lock_path.write_text(json.dumps(self.lock), encoding="utf-8")
        self.manifest_path.write_text(json.dumps(self.manifest), encoding="utf-8")

    def run_intake(self, **kwargs):
        self.save()
        return feed.materialize(self.lock_path, self.manifest_path, self.output, self.cache, **kwargs)

    def replace_bundle(self, entries):
        self.bundle = zip_bytes(entries)
        self.manifest["assets"][0].update(pin(self.bundle))
        (self.cache / "fixture.zip").write_bytes(self.bundle)

    def assert_no_partial_feed(self):
        self.assertFalse(self.output.exists())
        self.assertFalse(list(self.root.glob(".feed.stage-*")))
        self.assertFalse((self.root / ".feed.intake-lock").exists())

    def test_cold_offline_cached_bundle_and_resume_reverify_every_package(self):
        with mock.patch.object(feed, "download", side_effect=AssertionError("unexpected network")):
            self.assertEqual(self.run_intake(), 14)
            self.assertEqual({p.name for p in self.output.iterdir()}, set(self.packages))
            (self.cache / "fixture.zip").unlink()
            self.assertEqual(self.run_intake(), 14)
            name = next(iter(self.packages))
            (self.output / name).write_bytes(b"corrupt")
            with self.assertRaises(feed.IntakeError):
                self.run_intake(allow_download=True)
            self.assertEqual((self.output / name).read_bytes(), b"corrupt")

    def test_missing_cache_is_offline_by_default(self):
        (self.cache / "fixture.zip").unlink()
        with mock.patch.object(feed, "download", side_effect=AssertionError("unexpected network")):
            with self.assertRaisesRegex(feed.IntakeError, "offline"):
                self.run_intake()
        self.assert_no_partial_feed()

    def test_download_requires_opt_in_and_verifies_bytes_before_publishing(self):
        (self.cache / "fixture.zip").unlink()
        with mock.patch.object(feed, "download", return_value=self.bundle) as download:
            self.assertEqual(self.run_intake(allow_download=True), 14)
            download.assert_called_once()
        self.assertEqual((self.cache / "fixture.zip").read_bytes(), self.bundle)

    def test_corrupt_cache_is_preserved_never_redownloaded(self):
        bad = b"x" * len(self.bundle)
        (self.cache / "fixture.zip").write_bytes(bad)
        with mock.patch.object(feed, "download", side_effect=AssertionError("unexpected network")):
            with self.assertRaisesRegex(feed.IntakeError, "SHA-256"):
                self.run_intake(allow_download=True)
        self.assertEqual((self.cache / "fixture.zip").read_bytes(), bad)
        self.assert_no_partial_feed()

    def test_wrong_nested_package_after_earlier_writes_cleans_staging(self):
        entries = [(f"packages/{n}", c) for n, c in self.packages.items()]
        entries[-1] = (entries[-1][0], b"wrong bytes")
        self.replace_bundle(entries)
        with self.assertRaises(feed.IntakeError):
            self.run_intake()
        self.assert_no_partial_feed()

    def test_missing_selected_member_cleans_staging(self):
        self.replace_bundle([(f"packages/{n}", c) for n, c in list(self.packages.items())[:-1]])
        with self.assertRaises(KeyError):
            self.run_intake()
        self.assert_no_partial_feed()

    def test_existing_incomplete_or_extra_output_is_never_repaired(self):
        self.output.mkdir()
        sentinel = self.output / "unrelated.txt"
        sentinel.write_text("preserve", encoding="utf-8")
        with self.assertRaisesRegex(feed.IntakeError, "incomplete"):
            self.run_intake()
        self.assertEqual(list(self.output.iterdir()), [sentinel])

    def test_missing_owner_sources_fail_before_network_then_verified_local_input_unblocks(self):
        owners = self.lock["uiOwnerFeed"]["packages"] + self.lock["canonicalOwnerFeed"]["packages"]
        for row in owners:
            del self.manifest["assets"][0]["members"][row["fileName"]]
        self.manifest["unavailablePackages"] = [row["fileName"] for row in owners]
        with mock.patch.object(feed, "download", side_effect=AssertionError("unexpected network")):
            with self.assertRaisesRegex(feed.IntakeError, owners[0]["packageId"]):
                self.run_intake(allow_download=True)
        self.assert_no_partial_feed()
        inputs = self.root / "inputs"
        inputs.mkdir()
        for row in owners:
            (inputs / row["fileName"]).write_bytes(self.packages[row["fileName"]])
        self.assertEqual(self.run_intake(input_dir=inputs), 14)

    def test_all_direct_package_release_sources_supported(self):
        assets = []
        for name, row in feed.load_packages(self.lock).items():
            assets.append({"fileName": name, "kind": "nupkg", "url":
                           f"https://github.com/ArchonMegalon/chummer6-ui/releases/download/exact/{name}",
                           **pin(self.packages[name]), "members": {name: name}})
            (self.cache / name).write_bytes(self.packages[name])
        self.manifest["assets"] = assets
        self.assertEqual(self.run_intake(), 14)

    def test_manifest_rejects_unaccounted_unknown_duplicate_and_unpinned_sources(self):
        original = copy.deepcopy(self.manifest)
        cases = []
        missing = copy.deepcopy(original)
        missing["assets"][0]["members"].pop(next(iter(self.packages)))
        cases.append(missing)
        unknown = copy.deepcopy(original)
        unknown["assets"][0]["members"]["Chummer.Other.1.nupkg"] = "packages/other.nupkg"
        cases.append(unknown)
        duplicate = copy.deepcopy(original)
        duplicate["unavailablePackages"] = [next(iter(self.packages))]
        cases.append(duplicate)
        for field, value in (("url", "http://github.com/asset"), ("sha256", "latest"), ("sizeBytes", True)):
            changed = copy.deepcopy(original)
            changed["assets"][0][field] = value
            cases.append(changed)
        for manifest in cases:
            with self.subTest(manifest=manifest), self.assertRaises(feed.IntakeError):
                feed.load_manifest(manifest, feed.load_packages(self.lock))

    def test_nuspec_identity_version_duplicate_and_entities_are_rejected_even_with_matching_hash(self):
        row = next(iter(feed.load_packages(self.lock).values())).copy()
        for content in (package_bytes("Chummer.Wrong", row["version"]),
                        package_bytes(row["packageId"], "9.9.9"),
                        package_bytes(row["packageId"], row["version"], [("second.nuspec", "<package/>")]),
                        zip_bytes([("a.nuspec", '<!DOCTYPE package [<!ENTITY e "bad">]><package/>')])):
            with self.subTest(size=len(content)):
                row.update(pin(content))
                with self.assertRaises(feed.IntakeError):
                    feed.verify_package(content, row)

    def test_unsafe_members_are_rejected_including_unselected_and_nested_package_members(self):
        entries = [(f"packages/{n}", c) for n, c in self.packages.items()]
        link = zipfile.ZipInfo("link")
        link.create_system = 3
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        cases = [[("../escape", b"x")], [("/absolute", b"x")], [("C:/escape", b"x")],
                 [("nested\\escape", b"x")], [("NUL.txt", b"x")], [("bad. ", b"x")],
                 [("duplicate", b"1"), ("DUPLICATE", b"2")], [(link, b"destination")],
                 [("parent", b"x"), ("parent/child", b"x")]]
        for extras in cases:
            with self.subTest(extras=extras):
                self.replace_bundle(entries + extras)
                with self.assertRaises(feed.IntakeError):
                    self.run_intake()
                self.assert_no_partial_feed()
        row = next(iter(feed.load_packages(self.lock).values())).copy()
        content = package_bytes(row["packageId"], row["version"], [("../escape", b"x")])
        row.update(pin(content))
        with self.assertRaises(feed.IntakeError):
            feed.verify_package(content, row)

    def test_decompression_bound_and_bad_zip(self):
        content = zip_bytes([("bomb", b"0" * 4096)])
        with mock.patch.object(feed, "MAX_EXPANDED", 2048):
            with self.assertRaisesRegex(feed.IntakeError, "decompression"):
                with feed.checked_zip(content):
                    pass
        with self.assertRaises(zipfile.BadZipFile):
            with feed.checked_zip(b"not a zip"):
                pass

    def test_redirects_reject_downgrade_credentials_and_untrusted_host(self):
        redirect = feed.PublicRedirect()
        req = urllib.request.Request("https://github.com/asset", headers={"Authorization": "example", "Cookie": "example"})
        for url in ("http://release-assets.githubusercontent.com/a", "https://evil.example/a",
                    "https://user:pass@github.com/a", "https://github.com:444/a"):
            with self.subTest(url=url), self.assertRaises(feed.IntakeError):
                redirect.redirect_request(req, None, 302, "", {}, url)
        result = redirect.redirect_request(req, None, 302, "", {},
                                           "https://release-assets.githubusercontent.com/a?signature=public")
        self.assertNotIn("Authorization", result.headers)
        self.assertNotIn("Cookie", result.headers)

    def test_download_rejects_overlong_or_wrong_response(self):
        response = mock.MagicMock()
        response.__enter__.return_value = response
        response.geturl.return_value = self.manifest["assets"][0]["url"]
        opener = mock.Mock()
        opener.open.return_value = response
        for content in (self.bundle + b"x", b"x" * len(self.bundle)):
            response.read.return_value = content
            with mock.patch.object(feed.urllib.request, "build_opener", return_value=opener):
                with self.assertRaises(feed.IntakeError):
                    feed.download(self.manifest["assets"][0])
            response.read.assert_called_with(len(self.bundle) + 1)

    def test_duplicate_json_key_rejected(self):
        self.manifest_path.write_text('{"schemaVersion":1,"schemaVersion":1}', encoding="utf-8")
        with self.assertRaisesRegex(feed.IntakeError, "duplicate JSON"):
            feed.read_json(self.manifest_path)

    def test_linked_output_rejected(self):
        real = self.root / "real"
        real.mkdir()
        try:
            self.output.symlink_to(real, target_is_directory=True)
        except (NotImplementedError, OSError):
            self.skipTest("host does not permit symlinks")
        with self.assertRaisesRegex(feed.IntakeError, "linked/reparse"):
            self.run_intake()
        self.assertEqual(list(real.iterdir()), [])

    def test_committed_manifest_sources_every_locked_package(self):
        packages = feed.load_packages(feed.read_json(ROOT / "config/package-plane.lock.json"))
        manifest = feed.read_json(ROOT / "config/windows-package-downloads.json")
        assets = feed.load_manifest(manifest, packages)
        self.assertEqual(len(packages), 14)
        self.assertFalse(manifest["unavailablePackages"])
        sourced = [name for asset in assets for name in asset["members"]]
        self.assertCountEqual(sourced, packages)


if __name__ == "__main__":
    unittest.main()
