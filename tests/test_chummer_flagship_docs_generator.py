from __future__ import annotations

import json
from pathlib import Path


def test_flagship_docs_spec_promotes_origin_dossier_and_alice() -> None:
    spec = json.loads(
        Path("/docker/chummercomplete/chummer-presentation/docs/TABLE_PULSE_FLAGSHIP_DOCS_SPEC.json").read_text(
            encoding="utf-8"
        )
    )

    assert spec["user_first_story"]["title"] == "User-First Entry Arc"
    assert "origin dossier" in spec["user_first_story"]["summary"].lower()
    assert spec["origin_dossier_spotlight"]["title"] == "Origin Dossier And ALICE"
    assert any("shipped MVP supplies grounded desktop build assistance" in item for item in spec["origin_dossier_spotlight"]["alice_connection"])
    assert any(row["name"] == "ALICE" for row in spec["related_surfaces"])
    assert any("alice.png" in row["image_output"] for row in spec["visual_gallery"])
    assert spec["editorial_posture"]["opening"].startswith("A Chummer6 campaign begins with a runner")
    assert any(row["title"] == "TABLE PULSE 90-second deep dive" for row in spec["public_videos"])
    assert spec["release_and_support_posture"]["title"] == "Downloads Without Guesswork"
    assert any("Arch and CachyOS" in item for item in spec["release_and_support_posture"]["user_promises"])
    assert "an internal audit trail" in spec["release_and_support_posture"]["must_not_present_as"]
    assert "Only narrated, captioned clips" in spec["media_posture"]["summary"]


def test_flagship_docs_generator_syncs_visual_gallery_and_user_first_story() -> None:
    source = Path("/docker/chummercomplete/chummer-presentation/scripts/generate_chummer_flagship_docs.py").read_text(
        encoding="utf-8"
    )

    assert "sentence_join" in source
    assert "sync_visual_gallery" in source
    assert "## Product Scenes" in source
    assert "## Watch The Scenes" in source
    assert "release_and_support_posture" in source
    assert "media_posture" in source
    assert "## Related Surface" in source
    assert "ALICE stays grounded in the following ways:" in source
    assert "Origin Dossier and ALICE" in source
    assert 'str(editorial.get("opening") or overview["purpose"]).strip()' in source
    assert "{surface['name']} is {descriptor}: {feels}. It brings {must_show} into one legible view." in source
    assert "GENERATED FILE" not in source
