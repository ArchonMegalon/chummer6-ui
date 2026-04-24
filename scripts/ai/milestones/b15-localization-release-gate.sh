#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
catalog_path="$repo_root/Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs"
signoff_project_path="$repo_root/Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj"
signoff_path="$repo_root/docs/WORKBENCH_RELEASE_SIGNOFF.md"
local_release_proof_path="$repo_root/.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json"
next90_m104_receipt_path="$repo_root/.codex-studio/published/NEXT90_M104_UI_EXPLAIN_RECEIPTS.generated.json"
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
signoff_retry_attempted=0
signoff_retry_reason=""

run_signoff_runner() {
  scripts/ai/with-package-plane.sh run --project "$signoff_project_path" --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false
}

set +e
run_signoff_runner >"$signoff_log" 2>&1
signoff_status=$?
set -e

if [[ $signoff_status -ne 0 ]]; then
  if rg -q "libhostpolicy\.so|Failed to run as a self-contained app|runtimeconfig\.json' was not found" "$signoff_log"; then
    signoff_retry_attempted=1
    signoff_retry_reason="runtimeconfig_bootstrap_repair"
  elif rg -q "CSC : error CS0006: Metadata file .* could not be found" "$signoff_log"; then
    signoff_retry_attempted=1
    signoff_retry_reason="metadata_reference_rebuild"
  elif rg -q "error NETSDK1064: Package .* was not found|Package .* version .* was not found" "$signoff_log"; then
    signoff_retry_attempted=1
    signoff_retry_reason="package_restore_repair"
  fi

  if [[ $signoff_retry_attempted -eq 1 ]]; then
    set +e
    scripts/ai/with-package-plane.sh build "$signoff_project_path" --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false >>"$signoff_log" 2>&1
    run_signoff_runner >>"$signoff_log" 2>&1
    signoff_status=$?
    set -e
  fi
fi

python3 - "$catalog_path" "$receipt_path" "$legacy_lang_root" "$local_release_proof_path" "$next90_m104_receipt_path" "$signoff_status" "$signoff_log" "$signoff_retry_attempted" "$signoff_retry_reason" <<'PY'
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
next90_m104_receipt_path = Path(sys.argv[5])
signoff_status = int(sys.argv[6])
signoff_log_path = Path(sys.argv[7])
signoff_retry_attempted = int(sys.argv[8])
signoff_retry_reason = str(sys.argv[9]).strip()

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
minimum_override_count_by_locale = {
    "de-de": 40,
    "fr-fr": 40,
    "ja-jp": 40,
    "pt-br": 40,
    "zh-cn": 40,
}
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
required_localization_domains = [
    "app_chrome",
    "install_update_support",
    "explain_receipts",
    "data_rules_names",
    "generated_artifacts",
]
localization_domain_config = {
    "app_chrome": {
        "key_prefixes": ["desktop.shell.", "desktop.home.", "desktop.dialog."],
        "required_keys": [
            "desktop.shell.menu.file",
            "desktop.home.title",
            "desktop.dialog.global_settings.title",
        ],
    },
    "install_update_support": {
        "key_prefixes": [
            "desktop.install_link.",
            "desktop.update.",
            "desktop.support.",
            "desktop.support_case.",
            "desktop.devices.",
            "desktop.report.",
            "desktop.crash.",
        ],
        "required_keys": [
            "desktop.shell.tool.update_status",
            "desktop.shell.tool.open_support",
            "desktop.shell.tool.report_issue",
            "desktop.home.section.install_support",
            "desktop.home.section.update_posture",
            "desktop.support.title",
        ],
    },
    "explain_receipts": {
        "required_keys": ["desktop.home.section.build_explain"],
        "required_receipt_statuses": {"next90_m104_explain_receipts": "pass"},
    },
    "data_rules_names": {
        "legacy_corpus_domain": True,
    },
    "generated_artifacts": {
        "required_keys": [
            "desktop.home.button.open_campaign_artifacts",
            "desktop.home.button.open_my_artifacts",
            "desktop.home.button.open_published_artifacts",
            "desktop.campaign.status.server_generated",
        ],
        "required_receipt_statuses": {"local_release_proof": "pass"},
    },
}


def status_pass(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def load_json_summary(path: Path) -> dict[str, object]:
    if path.is_file():
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            return {"status": "invalid", "path": str(path)}
        if isinstance(payload, dict):
            return payload
        return {"status": "invalid", "path": str(path)}
    return {"status": "missing", "path": str(path)}

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
for locale in shipping_locales:
    if locale == "en-us":
        locale_summary.append(
            {
                "locale": locale,
                "override_count": len(default_keys),
                "minimum_override_count": len(default_keys),
                "default_key_count": len(default_keys),
                "untranslated_key_count": 0,
                "missing_release_seed_keys": [],
                "legacy_xml_present": legacy_language_files[locale]["xml"],
                "legacy_data_xml_present": legacy_language_files[locale]["data_xml"],
            }
        )
        continue
    override_keys = locale_overrides[locale]
    missing_seed_keys = [key for key in release_seed_keys if key not in override_keys]
    untranslated_key_count = len(default_keys) - len(override_keys)
    locale_entry = {
        "locale": locale,
        "override_count": len(override_keys),
        "minimum_override_count": minimum_override_count_by_locale[locale],
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
    minimum_override_count = minimum_override_count_by_locale[locale]
    if len(override_keys) < minimum_override_count:
        status = "fail"
        blocking_findings.append(
            f"{locale}: localized trust-surface override count {len(override_keys)} is below required floor {minimum_override_count}"
        )
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
local_release_summary = load_json_summary(local_release_proof_path)
next90_m104_summary = load_json_summary(next90_m104_receipt_path)

receipt_summaries = {
    "local_release_proof": local_release_summary,
    "next90_m104_explain_receipts": next90_m104_summary,
}

locale_key_sets = {
    locale: set(default_keys) if locale == "en-us" else set(locale_overrides[locale])
    for locale in shipping_locales
}

domain_coverage: dict[str, str] = {}
locale_domain_coverage: dict[str, dict[str, str]] = {locale: {} for locale in shipping_locales}
domain_evidence: dict[str, dict[str, object]] = {}
for domain in required_localization_domains:
    config = localization_domain_config[domain]
    prefixes = config.get("key_prefixes", [])
    required_keys = set(config.get("required_keys", []))
    legacy_corpus_domain = bool(config.get("legacy_corpus_domain"))
    expected_keys = sorted(
        required_keys
        | {
            key
            for key in default_keys
            if any(key.startswith(prefix) for prefix in prefixes)
        }
    )

    locale_missing_keys: dict[str, list[str]] = {}
    for locale in shipping_locales:
        if legacy_corpus_domain:
            locale_ok = legacy_language_files[locale]["xml"] and legacy_language_files[locale]["data_xml"]
            locale_missing_keys[locale] = []
        else:
            locale_missing_keys[locale] = [key for key in expected_keys if key not in locale_key_sets[locale]]
            locale_ok = not locale_missing_keys[locale]
        locale_domain_coverage[locale][domain] = "pass" if locale_ok else "fail"

    receipt_checks = {
        receipt_name: status_pass(receipt_summaries[receipt_name].get("status"))
        for receipt_name in config.get("required_receipt_statuses", {})
    }
    domain_keys_ok = legacy_corpus_domain or bool(expected_keys)
    all_locales_ok = all(locale_domain_coverage[locale][domain] == "pass" for locale in shipping_locales)
    domain_status = "pass" if domain_keys_ok and all_locales_ok and all(receipt_checks.values()) else "fail"
    domain_coverage[domain] = domain_status

    domain_evidence[domain] = {
        "status": domain_status,
        "matched_default_key_count": len(expected_keys),
        "required_key_count": len(expected_keys),
        "required_keys": expected_keys,
        "locale_missing_keys": locale_missing_keys,
        "required_receipts": {
            receipt_name: {
                "status": receipt_summaries[receipt_name].get("status"),
                "path": receipt_summaries[receipt_name].get("path"),
            }
            for receipt_name in config.get("required_receipt_statuses", {})
        },
        "legacy_corpus_domain": legacy_corpus_domain,
    }

    if not domain_keys_ok:
        status = "fail"
        blocking_findings.append(f"{domain}: localization gate could not map the domain to any catalog keys or legacy corpus evidence")
    elif not all_locales_ok:
        status = "fail"
        failing_locales = [locale for locale in shipping_locales if locale_domain_coverage[locale][domain] != "pass"]
        blocking_findings.append(f"{domain}: localized coverage is incomplete for {', '.join(failing_locales)}")
    elif not all(receipt_checks.values()):
        status = "fail"
        failing_receipts = [name for name, passed in receipt_checks.items() if not passed]
        blocking_findings.append(f"{domain}: required receipt proof is not pass/ready ({', '.join(failing_receipts)})")

if signoff_status != 0:
    status = "fail"
    blocking_findings.append("localization_signoff: localization signoff smoke runner failed.")

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
        "retry_attempted": signoff_retry_attempted > 0,
        "retry_reason": signoff_retry_reason,
        "log_excerpt": signoff_log_path.read_text(encoding="utf-8").strip().splitlines()[-5:],
    },
    "locale_summary": locale_summary,
    "legacy_language_root": str(legacy_lang_root),
    "local_release_proof": local_release_summary,
    "blocking_findings": blocking_findings,
    "translation_backlog_findings": translation_backlog_findings,
    "domain_coverage": domain_coverage,
    "locale_domain_coverage": locale_domain_coverage,
    "domain_evidence": domain_evidence,
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
    "evidence": {
        "shipping_locales": shipping_locales,
        "required_localization_domains": required_localization_domains,
        "signoffStatus": signoff_status,
        "retryAttempted": signoff_retry_attempted > 0,
        "retryReason": signoff_retry_reason,
        "blockingFindingCount": len(blocking_findings),
        "translationBacklogCount": len(translation_backlog_findings),
        "reasonCount": len(blocking_findings),
        "failureCount": len(blocking_findings),
    },
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if signoff_status != 0:
    raise SystemExit("[b15] FAIL: localization signoff smoke runner failed; see receipt for details.")

if status != "pass":
    raise SystemExit("[b15] FAIL: localization release gate is not yet flagship-ready; see receipt for blocking findings.")
PY

rm -f "$signoff_log"
echo "[b15] PASS: localization release gate is green."
