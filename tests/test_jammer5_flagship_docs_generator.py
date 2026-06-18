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
    assert any("ALICE owns the native desktop workbench" in item for item in spec["origin_dossier_spotlight"]["alice_connection"])
    assert any(row["name"] == "ALICE" for row in spec["related_horizons"])
    assert any("alice.png" in row["image_output"] for row in spec["visual_gallery"])
    assert spec["editorial_posture"]["opening"].startswith("This is the product guide")
    assert any(row["title"] == "TABLE PULSE 90-second deep dive" for row in spec["public_videos"])


def test_flagship_docs_generator_syncs_visual_gallery_and_user_first_story() -> None:
    source = Path("/docker/chummercomplete/chummer-presentation/scripts/generate_jammer5_flagship_docs.py").read_text(
        encoding="utf-8"
    )

    assert "sync_visual_gallery" in source
    assert "## Visual Notes" in source
    assert "## Watch" in source
    assert "## Related Horizon" in source
    assert "How ALICE connects:" in source
    assert "origin-dossier lane" in source
    assert "flagship story no longer starts only with table heat" in source
    assert "GENERATED FILE" not in source
