#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
catalog_path="$repo_root/Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs"
signoff_project_path="$repo_root/Chummer.Tests/Presentation/Chummer.Presentation.Localization.Signoff.Tests.csproj"
signoff_path="$repo_root/docs/WORKBENCH_RELEASE_SIGNOFF.md"
local_release_proof_path="$repo_root/.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json"
legacy_lang_root="$repo_root/Chummer/lang"

mkdir -p "$(dirname "$receipt_path")"

echo "[b15] checking localization release gate prerequisites..."

if ! rg -q 'b15-localization-release-gate\.sh' "$signoff_path"; then
  echo "[b15] FAIL: workbench release signoff does not cite the localization release gate: $signoff_path" >&2
  exit 51
fi

if ! test -f "$catalog_path"; then
  echo "[b15] FAIL: desktop localization catalog is missing: $catalog_path" >&2
  exit 52
fi

echo "[b15] executing localization signoff smoke runner..."
signoff_log="$(mktemp "${TMPDIR:-/tmp}/chummer-b15-signoff.XXXXXX.log")"
set +e
bash -lc '
  set -euo pipefail
  scripts/ai/with-package-plane.sh build "'"$signoff_project_path"'" --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false
  scripts/ai/with-package-plane.sh run --project "'"$signoff_project_path"'" --no-build --nologo --verbosity quiet
' >"$signoff_log" 2>&1
signoff_status=$?
set -e

python3 - "$catalog_path" "$receipt_path" "$legacy_lang_root" "$local_release_proof_path" "$signoff_status" "$signoff_log" <<'PY'
from __future__ import annotations

import datetime as dt
import json
import re
import sys
from pathlib import Path

catalog_path = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
legacy_lang_root = Path(sys.argv[3])
local_release_proof_path = Path(sys.argv[4])
signoff_status = int(sys.argv[5])
signoff_log_path = Path(sys.argv[6])

text = catalog_path.read_text(encoding="utf-8")

default_match = re.search(
    r"DefaultTrustSurfaceStrings\s*=\s*new Dictionary<string, string>\(StringComparer\.Ordinal\)\s*\{(?P<body>.*?)\n\s*\};\n\n\s*private static IReadOnlyDictionary<string, string> BuildLocaleTrustSurfaceStrings",
    text,
    re.S,
)
if default_match is None:
    raise SystemExit("[b15] FAIL: could not parse DefaultTrustSurfaceStrings from DesktopLocalizationCatalog.cs")

default_keys = sorted(set(re.findall(r'\["([^"]+)"\]\s*=', default_match.group("body"))))
if not default_keys:
    raise SystemExit("[b15] FAIL: no default trust-surface localization keys were parsed.")

shipping_locales = ["en-us", "de-de", "fr-fr", "ja-jp", "pt-br", "zh-cn"]
non_default_locales = [locale for locale in shipping_locales if locale != "en-us"]
release_seed_keys = [
    "desktop.shell.menu.file",
    "desktop.shell.tool.update_status",
    "desktop.shell.tool.open_support",
    "desktop.shell.tool.report_issue",
    "desktop.home.title",
    "desktop.home.section.install_support",
    "desktop.home.section.update_posture",
    "desktop.support.title",
]

def extract_locale_block(locale: str) -> str:
    pattern = rf'if \(string\.Equals\(languageCode, "{re.escape(locale)}", StringComparison\.Ordinal\)\)\s*\{{(?P<body>.*?)\n\s*\}}'
    match = re.search(pattern, text, re.S)
    if match is None:
        raise SystemExit(f"[b15] FAIL: could not parse localization override block for {locale}.")
    return match.group("body")

locale_overrides: dict[str, list[str]] = {}
for locale in non_default_locales:
    block = extract_locale_block(locale)
    locale_overrides[locale] = sorted(set(re.findall(r'localized\["([^"]+)"\]\s*=', block)))

legacy_language_files: dict[str, dict[str, bool]] = {}
for locale in shipping_locales:
    legacy_language_files[locale] = {
        "xml": (legacy_lang_root / f"{locale}.xml").is_file(),
        "data_xml": locale == "en-us" or (legacy_lang_root / f"{locale}_data.xml").is_file(),
    }

silent_clone_guard = "localized[pair.Key] = pair.Value;" not in text

locale_summary = []
status = "pass"
blocking_findings: list[str] = []
translation_backlog_findings: list[str] = []
for locale in non_default_locales:
    override_keys = locale_overrides[locale]
    missing_seed_keys = [key for key in release_seed_keys if key not in override_keys]
    untranslated_key_count = len(default_keys) - len(override_keys)
    locale_entry = {
        "locale": locale,
        "override_count": len(override_keys),
        "default_key_count": len(default_keys),
        "untranslated_key_count": untranslated_key_count,
        "missing_release_seed_keys": missing_seed_keys,
        "legacy_xml_present": legacy_language_files[locale]["xml"],
        "legacy_data_xml_present": legacy_language_files[locale]["data_xml"],
    }
    locale_summary.append(locale_entry)

    if missing_seed_keys:
        status = "fail"
        blocking_findings.append(f"{locale}: missing release-critical localized seed keys ({', '.join(missing_seed_keys)})")
    if untranslated_key_count > 0:
        translation_backlog_findings.append(
            f"{locale}: {untranslated_key_count} trust-surface keys still rely on explicit en-US fallback"
        )
    if not legacy_language_files[locale]["xml"] or not legacy_language_files[locale]["data_xml"]:
        status = "fail"
        blocking_findings.append(f"{locale}: legacy language corpus files are incomplete under {legacy_lang_root}")

if not silent_clone_guard:
    status = "fail"
    blocking_findings.append("DesktopLocalizationCatalog still silently clones en-US strings into non-default locales")

local_release_summary: dict[str, object] | None = None
if local_release_proof_path.is_file():
    try:
        local_release_summary = json.loads(local_release_proof_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        local_release_summary = {"status": "invalid", "path": str(local_release_proof_path)}
else:
    local_release_summary = {"status": "missing", "path": str(local_release_proof_path)}

payload = {
    "contract_name": "chummer6-ui.localization_release_gate",
    "generated_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "status": status,
    "shipping_locales": shipping_locales,
    "default_key_count": len(default_keys),
    "release_seed_keys": release_seed_keys,
    "explicit_fallback_runtime": "pass" if silent_clone_guard else "fail",
    "signoff_smoke_runner": {
        "status": "pass" if signoff_status == 0 else "fail",
        "log_excerpt": signoff_log_path.read_text(encoding="utf-8").strip().splitlines()[-5:],
    },
    "locale_summary": locale_summary,
    "legacy_language_root": str(legacy_lang_root),
    "local_release_proof": local_release_summary,
    "blocking_findings": blocking_findings,
    "translation_backlog_findings": translation_backlog_findings,
    "acceptance_gates": [
        "pseudo_localization",
        "missing_key_fail_fast",
        "top_surface_overflow_checks",
        "locale_smoke_first_launch",
        "locale_smoke_settings",
        "locale_smoke_explain",
        "locale_smoke_updater",
        "locale_smoke_support",
        "non_english_generated_artifact_smoke",
    ],
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if signoff_status != 0:
    raise SystemExit("[b15] FAIL: localization signoff smoke runner failed; see receipt for details.")

if status != "pass":
    raise SystemExit("[b15] FAIL: localization release gate is not yet flagship-ready; see receipt for blocking findings.")
PY

rm -f "$signoff_log"
echo "[b15] PASS: localization release gate is green."
