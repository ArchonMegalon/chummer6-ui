#!/usr/bin/env python3
"""Materialize only the package-plane lock's 14 Chummer packages, using stdlib.

Offline by default. Existing targets are verified, never repaired or overwritten.
This feed does not contain the third-party NuGet dependency closure.
"""
import argparse
from contextlib import contextmanager
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import stat
import sys
import tempfile
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
import zlib


ROOT = Path(__file__).resolve().parents[1]
FEEDS = {"coreRuntimeFeed": 8, "uiOwnerFeed": 2, "canonicalOwnerFeed": 4}
MAX_BYTES = 64 * 1024 * 1024
MAX_EXPANDED = 256 * 1024 * 1024
MAX_MEMBERS = 16384
MAX_NUSPEC = 1024 * 1024
SHA256 = re.compile(r"[0-9a-f]{64}\Z")
IDENTITY = re.compile(r"[A-Za-z0-9][A-Za-z0-9._+-]*\Z")
REDIRECT_HOSTS = {"github.com", "release-assets.githubusercontent.com",
                  "objects.githubusercontent.com", "github-releases.githubusercontent.com"}


class IntakeError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise IntakeError(message)


def safe_name(name, *, flat=False):
    require(isinstance(name, str) and bool(name), "empty archive/file name")
    parts = (name[:-1] if name.endswith("/") else name).split("/")
    require(not flat or len(parts) == 1, f"expected flat file name: {name!r}")
    for part in parts:
        require(part not in ("", ".", "..") and not part.endswith((" ", "."))
                and not any(ord(c) < 32 or ord(c) == 127 or c in '<>:"\\|?*' for c in part),
                f"unsafe archive/file name: {name!r}")
        require(part.split(".")[0].upper() not in
                {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)),
                 *(f"LPT{i}" for i in range(1, 10))}, f"Windows device name: {name!r}")
    require(not flat or not name.endswith("/"), "expected a file, not directory")
    return name


def physical(path):
    """Reject links/junctions/reparse points in existing path components on Windows too."""
    path = Path(path).absolute()
    for component in (path, *path.parents):
        try:
            info = component.lstat()
        except FileNotFoundError:
            continue
        require(not stat.S_ISLNK(info.st_mode)
                and not getattr(info, "st_file_attributes", 0) & 0x400,
                f"linked/reparse path is not allowed: {component}")
    return path


def read_file(path, limit):
    path = physical(path)
    require(stat.S_ISREG(path.stat().st_mode), f"not a regular file: {path}")
    require(path.stat().st_size <= limit, f"file exceeds size bound: {path}")
    with path.open("rb") as source:
        content = source.read(limit + 1)
    require(len(content) <= limit, f"file exceeds size bound: {path}")
    return content


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f"duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path):
    return json.loads(read_file(path, 2 * 1024 * 1024), object_pairs_hook=unique_object)


def valid_bytes(content, row, label):
    require(len(content) == row["sizeBytes"], f"size differs: {label}")
    require(hashlib.sha256(content).hexdigest() == row["sha256"], f"SHA-256 differs: {label}")


def load_packages(lock):
    require(isinstance(lock, dict), "lock must be an object")
    packages, identities = {}, set()
    for feed, count in FEEDS.items():
        rows = lock.get(feed, {}).get("packages")
        require(isinstance(rows, list) and len(rows) == count, f"expected {count} packages in {feed}")
        for row in rows:
            require(isinstance(row, dict), "invalid package row")
            for key in ("packageId", "version"):
                require(isinstance(row.get(key), str) and IDENTITY.fullmatch(row[key]),
                        f"invalid package {key}")
            name = safe_name(row.get("fileName"), flat=True)
            require(name == f"{row['packageId']}.{row['version']}.nupkg", "package filename differs from identity")
            require(isinstance(row.get("sha256"), str) and SHA256.fullmatch(row["sha256"]), "invalid package hash")
            require(type(row.get("sizeBytes")) is int and 0 < row["sizeBytes"] <= MAX_BYTES,
                    "invalid package size")
            identity = (row["packageId"].casefold(), row["version"].casefold())
            require(name.casefold() not in {n.casefold() for n in packages} and identity not in identities,
                    "duplicate locked package")
            packages[name] = row
            identities.add(identity)
    return packages


def public_url(url, *, initial=False):
    require(isinstance(url, str) and not any(c.isspace() or ord(c) < 32 for c in url), "invalid asset URL")
    parsed = urllib.parse.urlsplit(url)
    require(parsed.scheme == "https" and parsed.hostname in REDIRECT_HOSTS
            and parsed.port in (None, 443) and not parsed.username and not parsed.password
            and not parsed.fragment, "only credential-free HTTPS GitHub assets are allowed")
    if initial:
        require(parsed.hostname == "github.com" and not parsed.query
                and re.fullmatch(r"/ArchonMegalon/[A-Za-z0-9_.-]+/releases/download/[^/]+/[^/]+", parsed.path),
                "expected an exact public GitHub release asset URL")
    return url


def load_manifest(manifest, packages):
    require(isinstance(manifest, dict) and set(manifest) == {"schemaVersion", "assets", "unavailablePackages"}
            and type(manifest["schemaVersion"]) is int and manifest["schemaVersion"] == 1,
            "invalid download manifest schema")
    require(isinstance(manifest["assets"], list) and isinstance(manifest["unavailablePackages"], list),
            "invalid source lists")
    covered, asset_names = set(), set()
    for asset in manifest["assets"]:
        require(isinstance(asset, dict) and set(asset) ==
                {"fileName", "kind", "url", "sha256", "sizeBytes", "members"}, "invalid asset fields")
        name = safe_name(asset["fileName"], flat=True)
        require(name.casefold() not in asset_names, "duplicate asset filename")
        asset_names.add(name.casefold())
        require(asset["kind"] in ("zip", "nupkg"), "invalid asset kind")
        public_url(asset["url"], initial=True)
        require(urllib.parse.unquote(urllib.parse.urlsplit(asset["url"]).path.split("/")[-1]) == name,
                "URL filename differs from asset")
        require(isinstance(asset["sha256"], str) and SHA256.fullmatch(asset["sha256"]), "invalid asset hash")
        require(type(asset["sizeBytes"]) is int and 0 < asset["sizeBytes"] <= MAX_BYTES, "invalid asset size")
        require(isinstance(asset["members"], dict) and bool(asset["members"]), "empty asset members")
        member_names = set()
        for filename, member in asset["members"].items():
            require(filename in packages and filename not in covered, "unknown or duplicate sourced package")
            safe_name(member)
            require(not member.endswith("/") and member.casefold() not in member_names,
                    "duplicate/directory package member")
            member_names.add(member.casefold())
            covered.add(filename)
        if asset["kind"] == "nupkg":
            require(asset["members"] == {name: name} and name in packages, "invalid direct package source")
            require(all(asset[k] == packages[name][k] for k in ("sha256", "sizeBytes")),
                    "direct package source differs from lock")
    unavailable = manifest["unavailablePackages"]
    require(all(isinstance(n, str) for n in unavailable), "invalid unavailable package list")
    require(len(unavailable) == len(set(unavailable)) and not covered.intersection(unavailable)
            and covered.union(unavailable) == set(packages), "sources must account for exactly all locked packages")
    return manifest["assets"]


@contextmanager
def checked_zip(content):
    with zipfile.ZipFile(io.BytesIO(content)) as archive:
        infos = archive.infolist()
        require(len(infos) <= MAX_MEMBERS, "archive has too many members")
        names, expanded = {}, 0
        for info in infos:
            safe_name(info.orig_filename)
            require(info.orig_filename == info.filename, "truncated archive member name")
            name = info.filename.rstrip("/").casefold()
            require(name not in names, f"duplicate archive member: {info.filename}")
            mode = stat.S_IFMT(info.external_attr >> 16)
            require(mode in (0, stat.S_IFREG, stat.S_IFDIR) and not info.external_attr & 0x400,
                    "symlink/special archive member")
            require(mode != stat.S_IFDIR or info.is_dir(), "directory mode differs from member name")
            require(not info.flag_bits & 1 and info.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED),
                    "encrypted/unsupported archive member")
            require(info.file_size <= MAX_EXPANDED, "archive member exceeds decompression bound")
            expanded += info.file_size
            require(expanded <= MAX_EXPANDED, "archive exceeds decompression bound")
            names[name] = info
        for name in names:
            parts = name.split("/")
            for end in range(1, len(parts)):
                parent = names.get("/".join(parts[:end]))
                require(parent is None or parent.is_dir(), "archive file/directory collision")
        yield archive


def read_member(archive, name, limit):
    info = archive.getinfo(name)
    require(not info.is_dir() and info.file_size <= limit, f"member exceeds size bound: {name}")
    with archive.open(info) as source:
        content = source.read(limit + 1)
    require(len(content) == info.file_size and len(content) <= limit, "member decompression size differs")
    return content


def verify_package(content, row):
    valid_bytes(content, row, row["fileName"])
    with checked_zip(content) as archive:
        specs = [i.filename for i in archive.infolist()
                 if "/" not in i.filename and i.filename.lower().endswith(".nuspec")]
        require(len(specs) == 1, "package must contain exactly one root nuspec")
        xml = read_member(archive, specs[0], MAX_NUSPEC).decode("utf-8-sig")
        require("<!DOCTYPE" not in xml.upper() and "<!ENTITY" not in xml.upper(), "nuspec DTD/entities forbidden")
        root = ET.fromstring(xml)
        local = lambda node: node.tag.rsplit("}", 1)[-1]
        require(local(root) == "package", "invalid nuspec root")
        metadata = [node for node in root if local(node) == "metadata"]
        require(len(metadata) == 1, "invalid nuspec metadata")
        for key, expected in (("id", row["packageId"]), ("version", row["version"])):
            values = [node.text for node in metadata[0] if local(node) == key]
            require(values == [expected], f"nuspec {key} differs: {row['fileName']}")


class PublicRedirect(urllib.request.HTTPRedirectHandler):
    max_redirections = 5

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        public_url(newurl)
        # Recreate headers; never propagate credentials, cookies or authorization.
        return urllib.request.Request(newurl, headers={"User-Agent": "chummer-windows-package-intake/1"})


def download(asset):
    public_url(asset["url"], initial=True)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), PublicRedirect())
    request = urllib.request.Request(asset["url"], headers={"User-Agent": "chummer-windows-package-intake/1"})
    with opener.open(request, timeout=30) as response:
        public_url(response.geturl())
        content = response.read(asset["sizeBytes"] + 1)
    valid_bytes(content, asset, asset["fileName"])
    return content


def verify_feed(output, packages):
    output = physical(output)
    require(output.is_dir(), f"output is not a directory: {output}")
    require({p.name for p in output.iterdir()} == set(packages),
            "existing output is incomplete or has extra files; choose a new output directory")
    for name, row in packages.items():
        verify_package(read_file(output / name, row["sizeBytes"]), row)


@contextmanager
def output_lock(output):
    path = physical(output.parent / f".{output.name}.intake-lock")
    try:
        handle = path.open("xb")
    except FileExistsError as exc:
        raise IntakeError(f"intake lock exists: {path}; reconcile any active/interrupted intake first") from exc
    try:
        with handle:
            yield
    finally:
        path.unlink()


def materialize(lock_path, manifest_path, output, cache, *, allow_download=False, input_dir=None):
    packages = load_packages(read_json(lock_path))
    assets = load_manifest(read_json(manifest_path), packages)
    output, cache = physical(output), physical(cache)
    if output.exists():
        verify_feed(output, packages)
        return len(packages)
    local = {}
    if input_dir is not None:
        input_dir = physical(input_dir)
        require(input_dir.is_dir(), "input directory does not exist")
        for name, row in packages.items():
            candidate = input_dir / name
            physical(candidate)
            if candidate.exists():
                content = read_file(candidate, row["sizeBytes"])
                verify_package(content, row)
                local[name] = content
    sourced = {name for asset in assets for name in asset["members"]}
    missing = set(packages) - sourced - local.keys()
    require(not missing, "no verified source for locked packages: " + ", ".join(sorted(missing)))
    require(output.parent.is_dir(), "output parent must already exist")
    with output_lock(output):
        if output.exists():
            verify_feed(output, packages)
            return len(packages)
        stage = Path(tempfile.mkdtemp(prefix=f".{output.name}.stage-", dir=output.parent))
        try:
            def put(name, content):
                verify_package(content, packages[name])
                with (stage / name).open("xb") as target:
                    target.write(content)

            for name, content in local.items():
                put(name, content)
            for asset in assets:
                wanted = {name: member for name, member in asset["members"].items() if name not in local}
                if not wanted:
                    continue
                path = physical(cache / asset["fileName"])
                if path.exists():
                    content = read_file(path, asset["sizeBytes"])
                    valid_bytes(content, asset, path.name)
                else:
                    require(allow_download, f"offline: cached asset missing: {path}; supply it or use --download")
                    content = download(asset)
                    cache.mkdir(parents=True, exist_ok=True)
                    # Exclusive creation preserves other runs' cache files. No unverified bytes are saved.
                    with path.open("xb") as target:
                        target.write(content)
                if asset["kind"] == "nupkg":
                    put(asset["fileName"], content)
                else:
                    with checked_zip(content) as archive:
                        for name, member in wanted.items():
                            put(name, read_member(archive, member, packages[name]["sizeBytes"]))
            verify_feed(stage, packages)
            require(not output.exists(), "output appeared during intake; refusing replacement")
            stage.rename(output)
        finally:
            if stage.exists():
                shutil.rmtree(stage)
    return len(packages)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock", type=Path, default=ROOT / "config/package-plane.lock.json")
    parser.add_argument("--manifest", type=Path, default=ROOT / "config/windows-package-downloads.json")
    parser.add_argument("--output", type=Path, required=True, help="new flat feed or existing complete feed to verify")
    parser.add_argument("--cache", type=Path, default=ROOT / ".tmp/windows-package-intake3338/downloads")
    parser.add_argument("--input-dir", type=Path, help="optional existing flat directory of exact locked nupkg bytes")
    parser.add_argument("--download", action="store_true", help="explicitly allow missing public assets to be downloaded")
    args = parser.parse_args(argv)
    try:
        count = materialize(args.lock, args.manifest, args.output, args.cache,
                            allow_download=args.download, input_dir=args.input_dir)
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RuntimeError, ET.ParseError,
            zipfile.BadZipFile, zlib.error, NotImplementedError) as exc:
        print(f"Windows package intake failed: {exc}", file=sys.stderr)
        return 1
    print(f"Verified complete Chummer-only feed: {count} locked packages at {args.output}")
    print("Third-party NuGet dependencies still require their assigned public restore source.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
