from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = REPO_ROOT / "scripts" / "audit-compliance.sh"


def test_audit_compliance_uses_mtp_aware_test_helper_for_chummer_tests() -> None:
    text = SCRIPT_PATH.read_text(encoding="utf-8")

    assert 'bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj' in text
    assert 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj' not in text


def test_audit_compliance_fail_closes_when_filtered_test_sets_disappear() -> None:
    text = SCRIPT_PATH.read_text(encoding="utf-8")

    assert '--filter "FullyQualifiedName~MigrationComplianceTests" --minimum-expected-tests 1 --output Normal' in text
    assert '--filter "FullyQualifiedName~LifeModulesEndToEndTests" --minimum-expected-tests 1 --output Normal' in text
