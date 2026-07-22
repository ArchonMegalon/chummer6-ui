#!/usr/bin/env python3
"""Launch the governed JIT runner for the unsigned Windows preview exporter.

This is a narrow compatibility wrapper around the reviewed disposable-runner
controller.  It changes only the committed exporter/composition snapshots, workflow
identity, candidate boundary, and the absence of a native-capture relay.
"""

from __future__ import annotations

import hashlib
import importlib.util
import sys
from pathlib import Path
from types import ModuleType


LEGACY_PATH = Path(__file__).resolve().with_name("preview_nightly_jit_launcher.py")
LEGACY_SPEC = importlib.util.spec_from_file_location(
    "preview_nightly_jit_launcher_unsigned_compat", LEGACY_PATH
)
if LEGACY_SPEC is None or LEGACY_SPEC.loader is None:
    raise RuntimeError("could not load governed JIT launcher")
legacy = importlib.util.module_from_spec(LEGACY_SPEC)
sys.modules[LEGACY_SPEC.name] = legacy
LEGACY_SPEC.loader.exec_module(legacy)

SCOPE_MODULE_NAME = "chummer6_ui_preview_nightly_unsigned_scope_contract"
COMPOSITION_MODULE_NAME = (
    "chummer6_ui_preview_nightly_unsigned_composition_contract"
)
WORKFLOW_PATH = (
    ".github/workflows/unsigned-windows-preview-nightly-candidate-export.yml"
)
WORKFLOW_FILE = "unsigned-windows-preview-nightly-candidate-export.yml"
EXPORTER_PATH = "scripts/preview_nightly_unsigned_candidate_export.py"
SCOPE_PATH = "scripts/preview_nightly_unsigned_scope.py"
COMPOSITION_PATH = "scripts/preview_nightly_unsigned_composition.py"
EXPECTED_DIRECTORIES = (
    "publication",
    "provenance",
    "publication/files",
    "provenance/config",
    "provenance/retained-windows-publish-closure",
)

legacy.WORKFLOW_PATH = WORKFLOW_PATH
legacy.WORKFLOW_FILE = WORKFLOW_FILE
legacy.EXPORT_JOB_NAME = "Export exact unsigned Windows candidate bytes"
legacy.RUNNER_LABEL_PREFIX = "chummer-unsigned-windows-preview-export-"
legacy.RUNNER_NAME_PREFIX = "chummer-unsigned-windows-preview-jit-"
legacy.CONTAINER_PREFIX = "chummer-unsigned-windows-preview-jit-"
legacy.CONFIG_HOLDER_PREFIX = "chummer-unsigned-windows-preview-jit-config-holder-"
legacy.CONFIG_VERIFY_PREFIX = "chummer-unsigned-windows-preview-jit-config-verify-"
legacy.OWNER_LABEL = "run.chummer.unsigned-windows-preview-jit"
legacy.NONCE_LABEL = "run.chummer.unsigned-windows-preview-jit.nonce"
legacy.RECEIPT_CONTRACT = "chummer6-ui.preview-nightly-unsigned-jit-launch"
legacy.EXPECTED_CONTENT_DIRECTORIES = EXPECTED_DIRECTORIES
legacy.UNSIGNED_WINDOWS_PREVIEW_LANE = True

_load_governed_exporter = legacy.load_trusted_exporter


def verify_committed_unsigned_authority(repo_root: Path):
    repo_root = legacy.require_absolute_directory_no_links(
        repo_root, "launcher repository"
    )
    shown_root = Path(
        legacy.run_checked(
            ("git", "-C", str(repo_root), "rev-parse", "--show-toplevel"),
            kind="local",
        ).strip()
    )
    if shown_root != repo_root:
        legacy.fail("launcher repository root differs from git authority")
    origin = legacy.run_checked(
        ("git", "-C", str(repo_root), "remote", "get-url", "origin"),
        kind="local",
    ).strip()
    if origin != legacy.ORIGIN_URL:
        legacy.fail("launcher origin differs from the fixed repository")
    commit = legacy.require_match(
        legacy.run_checked(
            ("git", "-C", str(repo_root), "rev-parse", "HEAD"), kind="local"
        ).strip(),
        legacy.COMMIT_RE,
        "local trusted commit",
    )
    legacy.require_local_head(
        repo_root, commit, "before unsigned trusted snapshot construction"
    )
    for relative in (
        "scripts/preview_nightly_jit_launcher.py",
        "scripts/preview_nightly_unsigned_jit_launcher.py",
    ):
        legacy.committed_file_snapshot(repo_root, commit, relative)
    exporter_source = legacy.committed_file_snapshot(
        repo_root, commit, EXPORTER_PATH
    )
    scope_source = legacy.committed_file_snapshot(repo_root, commit, SCOPE_PATH)
    composition_source = legacy.committed_file_snapshot(
        repo_root, commit, COMPOSITION_PATH
    )
    legacy.require_local_head(
        repo_root, commit, "after unsigned trusted snapshot construction"
    )
    return legacy.LocalAuthority(
        commit, exporter_source, composition_source, (("scope", scope_source),)
    )


def load_unsigned_exporter(
    source: bytes,
    composition_source: bytes | None = None,
    authorities: tuple[tuple[str, bytes], ...] = (),
) -> ModuleType:
    if (
        not isinstance(authorities, tuple)
        or len(authorities) != 1
        or authorities[0][0] != "scope"
        or not isinstance(authorities[0][1], bytes)
        or not authorities[0][1]
    ):
        legacy.fail("trusted unsigned scope snapshot is missing")
    scope_source = authorities[0][1]
    if not isinstance(composition_source, bytes) or not composition_source:
        legacy.fail("trusted unsigned composition snapshot is missing")
    scope = ModuleType(SCOPE_MODULE_NAME)
    scope.__file__ = "<committed-preview-nightly-unsigned-scope-snapshot>"
    scope.__dict__["_TRUSTED_SOURCE_SHA256"] = hashlib.sha256(
        scope_source
    ).hexdigest()
    try:
        code = compile(scope_source, scope.__file__, "exec", dont_inherit=True)
        exec(code, scope.__dict__)
    except Exception as exc:
        legacy.fail(
            "trusted unsigned scope snapshot could not be loaded: "
            f"{type(exc).__name__}"
        )
    sys.modules[SCOPE_MODULE_NAME] = scope
    composition = ModuleType(COMPOSITION_MODULE_NAME)
    composition.__file__ = "<committed-preview-nightly-unsigned-composition-snapshot>"
    composition.__dict__["_TRUSTED_SOURCE_SHA256"] = hashlib.sha256(
        composition_source
    ).hexdigest()
    try:
        code = compile(
            composition_source, composition.__file__, "exec", dont_inherit=True
        )
        exec(code, composition.__dict__)
    except Exception as exc:
        legacy.fail(
            "trusted unsigned composition snapshot could not be loaded: "
            f"{type(exc).__name__}"
        )
    sys.modules[COMPOSITION_MODULE_NAME] = composition
    return _load_governed_exporter(source, None, ())


legacy.verify_committed_local_authority = verify_committed_unsigned_authority
legacy.load_trusted_exporter = load_unsigned_exporter


def main(argv: list[str] | None = None) -> int:
    return legacy.main(argv)


if __name__ == "__main__":
    raise SystemExit(main())
