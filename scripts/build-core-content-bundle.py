#!/usr/bin/env python3
"""Build runtime content from approved Git objects; installed apps need no Git or SDK."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

PINNED_COMMITS = {"8706f38a7b5c29308fe007baa101038f06f61906", "c29b27f3f6791189cade6c37621773ddd7339ecd"}
ROOTS = ("Chummer/data", "Chummer/lang", "Chummer/customdata")
MANIFEST = "core-content-manifest.json"

def git(source, *args):
    return subprocess.check_output(["git", "--no-replace-objects", "-C", str(source), *args])

def safe_path(name):
    return any(name.startswith(root + "/") for root in ROOTS) and "\\" not in name and ":" not in name and all(p and p not in (".", "..") and not p.endswith((".", " ")) for p in name.split("/"))

def build(source, destination, *, approved=PINNED_COMMITS):
    source, destination = Path(source).resolve(), Path(destination).absolute()
    commit = git(source, "rev-parse", "HEAD").decode().strip()
    if commit not in approved: raise ValueError("Core content must be at the application-approved commit")
    if destination.is_symlink(): raise ValueError("Bundle output must not be a symlink")
    inventory, seen = [], set()
    for row in git(source, "ls-tree", "-rz", "--full-tree", commit, "--", *ROOTS).split(b"\0"):
        if not row: continue
        header, rawname = row.split(b"\t", 1)
        mode, kind, oid = header.decode().split()
        name = rawname.decode("utf-8")
        if mode not in ("100644", "100755") or kind != "blob" or not safe_path(name) or name.casefold() in seen:
            raise ValueError("Unsupported or ambiguous content entry")
        seen.add(name.casefold()); inventory.append((name, oid))
    required = {"Chummer/data/settings.xml", "Chummer/data/settings-all-sources.xml", "Chummer/data/lifemodules.xml"}
    if not required.issubset({name for name, _ in inventory}): raise ValueError("Approved Core tree is missing required runtime rules")
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(prefix=".core-content-", dir=destination.parent))
    try:
        for root in ROOTS: (temporary / root).mkdir(parents=True)
        files = []
        # Never copy developer overrides or untracked files into the distributable.
        process = subprocess.Popen(["git", "--no-replace-objects", "-C", str(source), "cat-file", "--batch"], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        try:
            for name, oid in sorted(inventory):
                process.stdin.write((oid + "\n").encode()); process.stdin.flush()
                result_oid, kind, size = process.stdout.readline().decode().strip().split()
                if result_oid != oid or kind != "blob": raise ValueError("Git object identity changed")
                data = process.stdout.read(int(size))
                if len(data) != int(size) or process.stdout.read(1) != b"\n": raise ValueError("Truncated Git object")
                path = temporary / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
                files.append({"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
        finally:
            process.stdin.close(); process.stdout.close()
            if process.wait(timeout=10) != 0: raise ValueError("Git object extraction failed")
        # File-based installers must retain required empty directories too.
        for root in ROOTS:
            if not any((temporary / root).iterdir()):
                name = root + "/.bundle-directory"
                (temporary / name).write_bytes(b"")
                files.append({"path": name, "bytes": 0, "sha256": hashlib.sha256(b"").hexdigest()})
        files.sort(key=lambda item: item["path"])
        manifest = (json.dumps({"schemaVersion": 2, "contentCommit": commit, "files": files}, sort_keys=True, separators=(",", ":")) + "\n").encode()
        (temporary / MANIFEST).write_bytes(manifest)
        digest = hashlib.sha256(manifest).hexdigest()
        if destination.exists():
            expected = {x.relative_to(temporary).as_posix(): x.read_bytes() for x in temporary.rglob("*") if x.is_file()}
            actual = {}
            for entry in destination.rglob("*"):
                if entry.is_symlink(): raise ValueError("Existing bundle contains a symlink")
                if entry.is_file(): actual[entry.relative_to(destination).as_posix()] = entry.read_bytes()
            if expected != actual: raise ValueError("Existing bundle differs; use a fresh build output directory")
        else: os.rename(temporary, destination)
        return digest
    finally:
        if temporary.exists(): shutil.rmtree(temporary)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--digest-output", type=Path, required=True)
    args = parser.parse_args()
    digest = build(args.source, args.output)
    args.digest_output.parent.mkdir(parents=True, exist_ok=True)
    args.digest_output.write_text(digest + "\n")
    print("Verified Core content bundle: " + digest)

if __name__ == "__main__": main()
