#!/usr/bin/env python3
"""Fast automatic pull-request controls separate from protected release workflows."""

from __future__ import annotations

import argparse
import os
import re
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
PORTABILITY_PATTERNS = (
    re.compile(r"(?<![A-Za-z0-9])/(?:tmp|var/tmp|docker|workspace|home)/"),
    re.compile(r"[A-Za-z]:\\Users\\", re.IGNORECASE),
)
SECRET_PATTERNS = (
    re.compile("-----BEGIN " + "(?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile("g" + "hp_[A-Za-z0-9]{30,}"),
    re.compile("github_pat_" + "[A-Za-z0-9_]{40,}"),
    re.compile("AKIA" + "[A-Z0-9]{16}"),
)
RELEASE_PROJECTS = (
    "Chummer.Presentation/Chummer.Presentation.csproj",
    "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj",
    "Chummer.Avalonia/Chummer.Avalonia.csproj",
)


class ControlError(ValueError):
    pass


def git_paths(command: list[str]) -> list[str]:
    completed = subprocess.run(
        ["git", *command, "-z"],
        cwd=REPO_ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        raise ControlError("git path inventory failed")
    return [token.decode("utf-8") for token in completed.stdout.split(b"\0") if token]


def tracked_paths() -> list[str]:
    return git_paths(["ls-files"])


def changed_paths(base: str | None, head: str | None) -> list[str]:
    if base is None and head is None:
        return tracked_paths()
    if not base or not head or not re.fullmatch(r"[0-9a-fA-F]{40}", base) or not re.fullmatch(
        r"[0-9a-fA-F]{40}", head
    ):
        raise ControlError("base/head must be paired exact commits")
    return git_paths(["diff", "--name-only", "--diff-filter=ACMRT", base, head, "--"])


def regular_text(relative: str) -> str | None:
    path = REPO_ROOT / relative
    try:
        metadata = path.lstat()
    except OSError:
        return None
    if not stat.S_ISREG(metadata.st_mode) or path.is_symlink() or metadata.st_size > 2 * 1024 * 1024:
        return None
    if b"\0" in path.read_bytes()[:8192]:
        return None
    try:
        return path.read_text(encoding="utf-8-sig")
    except UnicodeError:
        return None


def check_secret_scan(paths: list[str]) -> None:
    findings = []
    for relative in paths:
        if relative.startswith(("tests/", "docs/")):
            continue
        content = regular_text(relative)
        if content is None:
            continue
        for pattern in SECRET_PATTERNS:
            if pattern.search(content):
                findings.append(relative)
                break
    if findings:
        raise ControlError("high-confidence secret material in changed files: " + ", ".join(sorted(findings)))


def check_receipt_portability(paths: list[str]) -> None:
    findings = []
    for relative in paths:
        lowered = relative.casefold()
        if ".generated." not in lowered or not lowered.endswith((".json", ".md")):
            continue
        content = regular_text(relative)
        if content is not None and any(pattern.search(content) for pattern in PORTABILITY_PATTERNS):
            findings.append(relative)
    if findings:
        raise ControlError("changed generated receipt contains a machine-local path: " + ", ".join(findings))


def check_dependency_versions(paths: list[str]) -> None:
    failures = []
    for relative in paths:
        if not relative.endswith((".csproj", ".props", ".targets")):
            continue
        content = regular_text(relative)
        if content is None:
            continue
        try:
            root = ET.fromstring(content)
        except ET.ParseError as exc:
            raise ControlError(f"invalid MSBuild XML {relative}: {exc}") from exc
        for reference in root.iter("PackageReference"):
            version = reference.attrib.get("Version") or (reference.findtext("Version") or "").strip()
            if not version:
                failures.append(f"{relative}:{reference.attrib.get('Include', '?')}:missing")
                continue
            normalized = version.casefold()
            if "*" in version or normalized in {"latest", "lateststable", "prerelease"}:
                failures.append(f"{relative}:{reference.attrib.get('Include', '?')}:{version}")
    if failures:
        raise ControlError("floating or missing dependency versions: " + ", ".join(failures))


def check_package_boundary() -> None:
    props = (REPO_ROOT / "Directory.Build.props").read_text(encoding="utf-8-sig")
    if props.count("<ChummerUseLocalCompatibilityTree") != 1 or not re.search(
        r"<ChummerUseLocalCompatibilityTree Condition=\"'\$\(ChummerUseLocalCompatibilityTree\)' == ''\">false</ChummerUseLocalCompatibilityTree>",
        props,
    ):
        raise ControlError("ChummerUseLocalCompatibilityTree must have one false default and no ambient Exists-based enable")
    helper = (REPO_ROOT / "scripts/ai/with-package-plane.sh").read_text(encoding="utf-8")
    for token in (
        'use_local_compatibility_tree="${CHUMMER_USE_LOCAL_COMPATIBILITY_TREE:-0}"',
        'no package authority configured',
        'choose exactly one package authority',
        '-p:ChummerUseLocalCompatibilityTree=true',
    ):
        if token not in helper:
            raise ControlError(f"package-plane helper lacks explicit authority guard: {token}")
    for relative in RELEASE_PROJECTS:
        root = ET.parse(REPO_ROOT / relative).getroot()
        for group in root.findall("ItemGroup"):
            group_condition = group.attrib.get("Condition", "")
            for reference in group.findall("ProjectReference"):
                include = reference.attrib.get("Include", "")
                if "chummer-core-engine" not in include and "$(ChummerLocal" not in include:
                    continue
                condition = f"{group_condition} {reference.attrib.get('Condition', '')}"
                if "ChummerUseLocalCompatibilityTree" not in condition or "true" not in condition:
                    raise ControlError(f"ambient sibling ProjectReference remains in {relative}: {include}")
    for relative in tracked_paths():
        if not relative.endswith((".csproj", ".props", ".targets")):
            continue
        content = regular_text(relative)
        if content is not None and re.search(r"<HintPath>[^<]*(?:chummer-core|chummer-hub|chummer\.run)", content, re.IGNORECASE):
            raise ControlError(f"sibling DLL HintPath remains active: {relative}")


def check_no_stub_release_path() -> None:
    verify = (REPO_ROOT / "scripts/ai/verify.sh").read_text(encoding="utf-8")
    required = (
        'CHUMMER_ALLOW_STUB_PACKAGES:-0',
        'export CHUMMER_ALLOW_STUB_PACKAGES=0',
        'integration" || "$verify_mode" == "release',
        'requires an explicit pinned CHUMMER_PUBLISHED_FEED_SOURCES',
        'requires a new absolute CHUMMER_VERIFY_ISOLATED_CACHE_ROOT',
    )
    for token in required:
        if token not in verify:
            raise ControlError(f"strict no-stub/no-cache mode guard is missing: {token}")


def check_license() -> None:
    license_path = REPO_ROOT / "LICENSE"
    if not license_path.is_file() or license_path.is_symlink() or license_path.stat().st_size < 100:
        raise ControlError("root LICENSE is missing, linked, or empty")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base")
    parser.add_argument("--head")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        changed = changed_paths(args.base, args.head)
        check_secret_scan(changed)
        check_receipt_portability(changed)
        check_dependency_versions(tracked_paths())
        check_package_boundary()
        check_no_stub_release_path()
        check_license()
    except ControlError as exc:
        print(f"pull-request-controls:error: {exc}", file=sys.stderr)
        return 2
    print("pull-request-controls:ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
