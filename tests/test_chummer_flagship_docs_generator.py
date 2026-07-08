from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_flagship_docs_spec_promotes_origin_dossier_and_alice() -> None:
    spec = json.loads(
        (REPO_ROOT / "docs" / "TABLE_PULSE_FLAGSHIP_DOCS_SPEC.json").read_text(
            encoding="utf-8"
        )
    )

    assert spec["user_first_story"]["title"] == "User-First Entry Arc"
    assert "origin dossier" in spec["user_first_story"]["summary"].lower()
    assert spec["origin_dossier_spotlight"]["title"] == "Origin Dossier And ALICE"
    assert any("ALICE owns the native desktop workbench" in item for item in spec["origin_dossier_spotlight"]["alice_connection"])
    assert any(row["name"] == "ALICE" for row in spec["related_surfaces"])
    assert any("alice.png" in row["image_output"] for row in spec["visual_gallery"])
    assert spec["editorial_posture"]["opening"].startswith("Start with the person at the table")
    assert any(row["title"] == "TABLE PULSE 90-second deep dive" for row in spec["public_videos"])
    assert spec["release_and_support_posture"]["title"] == "Downloads And Support Should Feel Boring"
    assert any("Arch and CachyOS" in item for item in spec["release_and_support_posture"]["user_promises"])
    assert "an internal audit trail" in spec["release_and_support_posture"]["must_not_present_as"]
    assert "Only narrated, captioned clips" in spec["media_posture"]["summary"]


def test_flagship_docs_generator_syncs_visual_gallery_and_user_first_story() -> None:
    source = (REPO_ROOT / "scripts" / "generate_chummer_flagship_docs.py").read_text(
        encoding="utf-8"
    )

    assert "sentence_join" in source
    assert "sync_visual_gallery" in source
    assert "## Product Scenes" in source
    assert "## Watch The Scenes" in source
    assert "release_and_support_posture" in source
    assert "media_posture" in source
    assert "## Related Surface" in source
    assert "ALICE keeps the line tight by doing the following:" in source
    assert "Origin dossier and ALICE" in source
    assert "flagship story no longer starts only with table heat" in source
    assert "It should feel {feels}. A reader should immediately grasp {must_show}." in source
    assert "The payoff can be " in source
    assert "GENERATED FILE" not in source
