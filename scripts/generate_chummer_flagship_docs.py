#!/usr/bin/env python3
from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
SPEC_PATH = DOCS / "TABLE_PULSE_FLAGSHIP_DOCS_SPEC.json"
SHOWCASE_PATH = DOCS / "TABLE_PULSE_FLAGSHIP_SHOWCASE.md"
MINIGAMES_PATH = DOCS / "TABLE_PULSE_REMOTE_REACTION_MINIGAMES.md"
INDEX_PATH = DOCS / "index.html"

INDEX_START = "<!-- GENERATED:TABLE_PULSE_DOCS_START -->"
INDEX_END = "<!-- GENERATED:TABLE_PULSE_DOCS_END -->"


def bullet_lines(items: list[str]) -> list[str]:
    return [f"- {item}" for item in items]


def numbered_lines(items: list[str]) -> list[str]:
    return [f"{index}. {item}" for index, item in enumerate(items, start=1)]


def sentence_join(items: list[str]) -> str:
    cleaned = [str(item).strip() for item in items if str(item).strip()]
    if not cleaned:
        return ""
    if len(cleaned) == 1:
        return cleaned[0]
    if len(cleaned) == 2:
        return f"{cleaned[0]} and {cleaned[1]}"
    return ", ".join(cleaned[:-1]) + f", and {cleaned[-1]}"


def image_markdown(image_output: str, alt: str) -> str:
    return f"![{alt}]({image_output})"


def video_lines(items: list[dict[str, Any]]) -> list[str]:
    rendered: list[str] = []
    for item in items:
        title = str(item.get("title") or "").strip()
        url = str(item.get("url") or "").strip()
        local_path_text = str(item.get("local_path") or "").strip()
        if local_path_text and not video_has_audio(Path(local_path_text)):
            continue
        note = str(item.get("note") or "Video with narration.").strip()
        caption_url = str(item.get("caption_url") or "").strip()
        if not title or not url:
            continue
        line = f"- [{title}]({url})"
        if note:
            line += f" - {note}"
        if caption_url:
            line += f" [Captions]({caption_url})."
        rendered.append(line)
    return rendered


def video_has_audio(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        result = subprocess.run(
            [
                "ffprobe",
                "-v",
                "error",
                "-select_streams",
                "a",
                "-show_entries",
                "stream=codec_name",
                "-of",
                "csv=p=0",
                str(path),
            ],
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=15,
        )
    except Exception:
        return False
    return bool(result.stdout.strip())


def sync_visual_gallery(spec: dict[str, Any]) -> list[dict[str, str]]:
    synced: list[dict[str, str]] = []
    for item in list(spec.get("visual_gallery") or []):
        row = dict(item or {})
        source = Path(str(row.get("image_source") or "").strip())
        output_rel = str(row.get("image_output") or "").strip()
        if not source.is_file() or not output_rel:
            continue
        destination = DOCS / output_rel
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        synced.append(
            {
                "title": str(row.get("title") or "").strip(),
                "image_output": output_rel,
                "alt": str(row.get("alt") or "").strip(),
                "caption": str(row.get("caption") or "").strip(),
            }
        )
    return synced


def render_showcase(spec: dict[str, Any]) -> str:
    overview = spec["overview"]
    editorial = dict(spec.get("editorial_posture") or {})
    taxonomy = dict(spec.get("product_taxonomy") or {})
    user_first = dict(spec.get("user_first_story") or {})
    origin = dict(spec.get("origin_dossier_spotlight") or {})
    gm_cockpit = dict(spec.get("gm_cockpit_spotlight") or {})
    release_posture = dict(spec.get("release_and_support_posture") or {})
    media_posture = dict(spec.get("media_posture") or {})
    related_surfaces = [
        dict(item)
        for item in list(spec.get("related_surfaces") or [])
        if isinstance(item, dict)
    ]
    visual_gallery = [dict(item) for item in list(spec.get("_synced_visual_gallery") or []) if isinstance(item, dict)]
    videos = [dict(item) for item in list(spec.get("public_videos") or []) if isinstance(item, dict)]
    lines: list[str] = [
        f"# {overview['title']}",
        "",
        str(editorial.get("opening") or overview["purpose"]).strip(),
        "",
        "## In One Sentence",
        "",
        overview["one_line_pitch"],
        "",
        "## Where It Fits",
        "",
        overview["delivery_posture"],
        "",
    ]
    if taxonomy:
        base_names = [str(dict(item).get("name") or "").strip() for item in list(taxonomy.get("base_features") or [])]
        future_names = [str(dict(item).get("name") or "").strip() for item in list(taxonomy.get("future_horizons") or [])]
        quiet_names = [str(dict(item).get("name") or "").strip() for item in list(taxonomy.get("throw_out") or [])]
        base_names = [name for name in base_names if name]
        future_names = [name for name in future_names if name]
        quiet_names = [name for name in quiet_names if name]
        lines.extend(
            [
                f"## {taxonomy['title']}",
                "",
                str(taxonomy.get("summary") or "").strip(),
                "",
                "The core story stays anchored in "
                + sentence_join(base_names)
                + ". Those are the surfaces that make Chummer feel useful before it feels ambitious.",
                "",
            ]
        )
        if future_names:
            lines.extend(
                [
                    "Keep "
                    + sentence_join(future_names)
                    + " on the future shelf for now. They matter, but they should not be the first burden on a new visitor.",
                    "",
                ]
            )
        if taxonomy.get("throw_out"):
            lines.extend(
                [
                    "A few labels should stay quiet: "
                    + sentence_join(quiet_names)
                    + ". They are starter help, navigation polish, or infrastructure, so the app should absorb them instead of selling them as separate ideas.",
                    "",
                ]
            )
        lines.append("")
    lines.extend(
        [
        "## Why You Would Open This",
        "",
        "Table Pulse is the live pressure-and-response system for Chummer6.",
        "",
        "It turns high-heat moments at the table into bounded reaction packets, governed player",
        "follow-up, faction movement, and public-safe world fallout. The important rule is that the",
        "system is dramatic without becoming fake authority:",
        "",
        *bullet_lines(overview["system_truths"]),
        "",
        "## How It Changes The Table",
        "",
        "The best version reads as one connected play loop, not as separate modules:",
        "",
        ]
    )
    for index, layer in enumerate(overview["layers"], start=1):
        joined = ", ".join(layer["items"])
        lines.append(f"{index}. **{layer['name']}**: {joined}.")
    if visual_gallery:
        lines.extend(["", "## Product Scenes", ""])
        for item in visual_gallery:
            lines.extend(
                [
                    f"### {item['title']}",
                    "",
                    image_markdown(item["image_output"], item["alt"]),
                    "",
                    item["caption"],
                    "",
                ]
            )
    rendered_videos = video_lines(videos)
    if rendered_videos:
        lines.extend(["", "## Watch The Scenes", "", *rendered_videos, ""])
    if media_posture:
        lines.extend(
            [
                f"## {media_posture['title']}",
                "",
                str(media_posture.get("summary") or "").strip(),
                "",
                "What stays visible:",
                "",
                *bullet_lines(list(media_posture.get("must_do") or [])),
                "",
                "What stays out of the pitch:",
                "",
                *bullet_lines(list(media_posture.get("must_not_do") or [])),
                "",
            ]
        )
    if release_posture:
        lines.extend(
            [
                f"## {release_posture['title']}",
                "",
                str(release_posture.get("summary") or "").strip(),
                "",
                "What a user should be able to rely on:",
                "",
                *bullet_lines(list(release_posture.get("user_promises") or [])),
                "",
                "Keep these as maintenance lanes:",
                "",
                *bullet_lines(list(release_posture.get("maintenance_lanes") or [])),
                "",
                "Do not present them as:",
                "",
                *bullet_lines(list(release_posture.get("must_not_present_as") or [])),
                "",
            ]
        )
    if user_first:
        lines.extend(
            [
                "",
                f"## {user_first['title']}",
                "",
                str(user_first.get("summary") or "").strip(),
                "",
                *numbered_lines(list(user_first.get("beats") or [])),
                "",
                "What stays trustworthy:",
                "",
                *bullet_lines(list(user_first.get("truths") or [])),
                "",
            ]
        )
    if origin:
        lines.extend(
            [
                f"## {origin['title']}",
                "",
                str(origin.get("summary") or "").strip(),
                "",
                "A player should leave this lane with:",
                "",
                *bullet_lines(list(origin.get("must_show") or [])),
                "",
                "ALICE keeps the line tight by doing the following:",
                "",
                *bullet_lines(list(origin.get("alice_connection") or [])),
                "",
                "That still does not mean:",
                "",
                *bullet_lines(list(origin.get("must_not_imply") or [])),
                "",
            ]
        )
    if gm_cockpit:
        lines.extend(
            [
                f"## {gm_cockpit['title']}",
                "",
                str(gm_cockpit.get("summary") or "").strip(),
                "",
                "A GM should be able to trust these rails immediately:",
                "",
                *bullet_lines(list(gm_cockpit.get("must_show") or [])),
                "",
                "From origin story to table call, the intended flow is:",
                "",
                *numbered_lines(list(gm_cockpit.get("origin_gimmick_flow") or [])),
                "",
                "The visual language should read as:",
                "",
                *bullet_lines(list(gm_cockpit.get("visual_language") or [])),
                "",
                "That still does not permit:",
                "",
                *bullet_lines(list(gm_cockpit.get("must_not_imply") or [])),
                "",
            ]
        )
    lines.extend(["", "## Hero Surfaces", ""])
    for surface in overview["surfaces"]:
        descriptor = surface.get("descriptor", "the surface")
        feels = sentence_join(list(surface.get("feels") or []))
        must_show = sentence_join(list(surface.get("must_show") or []))
        lines.extend(
            [
                f"### {surface['name']}",
                "",
                f"{surface['name']} is {descriptor}. It should feel {feels}. A reader should immediately grasp {must_show}.",
            ]
        )
        if surface.get("must_never_imply"):
            lines.extend(["", "It should never imply:", "", *bullet_lines(surface["must_never_imply"])])
        lines.append("")
    lines.extend(["## Core Play Loop", ""])
    for index, beat in enumerate(overview["play_loop"], start=1):
        lines.extend([f"### {index}. {beat['name']}", "", beat["description"]])
        if beat.get("items"):
            lines.extend(["", *bullet_lines(beat["items"])])
        lines.append("")
    lines.extend(
        [
            "## The Moment It Should Create",
            "",
            "A visitor should be able to picture an action board, not an admin form.",
            "",
            "The effect comes from:",
            "",
            *bullet_lines(overview["wow_effect"]["comes_from"]),
            "",
            "What would make it feel cheap:",
            "",
            *bullet_lines(overview["wow_effect"]["must_not_come_from"]),
            "",
            "## Hero Moment",
            "",
            "The flagship version should let a reader immediately picture the best-case loop:",
            "",
            *numbered_lines(overview["hero_moment"]),
            "",
            "## Promises We Should Not Break",
            "",
            *bullet_lines(overview["hard_truths"]),
            "",
            "## What Good Looks Like",
            "",
            "Call the stack ready only when all of the following are true:",
            "",
            *numbered_lines(overview["flagship_bar"]),
            "",
            "## Related Surface",
            "",
        ]
    )
    for row in related_surfaces:
        lines.extend(
            [
                f"### {row['name']}",
                "",
                f"Current status: {row['status']}",
                "",
                f"Why a reader should care: {row['reader_why']}",
                "",
                f"Connection here: {row['connection']}",
                "",
            ]
        )
    lines.extend(
        [
            "## Where This Comes From",
            "",
            str(editorial.get("source_note") or "This page is maintained from the Chummer6 design canon and public-guide source set.").strip(),
            "",
            *bullet_lines(spec["published_design_sources"]),
        ]
    )
    return "\n".join(lines) + "\n"


def render_minigames(spec: dict[str, Any]) -> str:
    mg = spec["minigames"]
    mobile_feel = sentence_join(list(mg["surface_requirements"]["mobile_pwa"]["feel"]))
    mobile_must_show = sentence_join(list(mg["surface_requirements"]["mobile_pwa"]["must_show"]))
    player_action_card_shape = sentence_join(list(mg["surface_requirements"]["player_action_cards"]))
    gm_cockpit_shape = sentence_join(list(mg["surface_requirements"]["gm_cockpit"]))
    lines: list[str] = [
        f"# {mg['title']}",
        "",
        mg["purpose"],
        "",
        "## Why It Exists",
        "",
        mg["goal"],
        "",
        "## What They Are",
        "",
        "Remote reaction mini-games are short governed follow-up encounters that occur after a",
        "Table Pulse packet is emitted.",
        "",
        "They stay:",
        "",
        "- opt-in or policy-allowed",
        "- receipt-backed",
        "- bounded in consequence",
        "- safe to adjudicate outside the main table session",
        "",
        "They are not:",
        "",
        "- direct mutation of the table state",
        "- autonomous side campaigns",
        "- public scoreboards",
        "- a replacement for the GM",
        "",
        "## Core Mini-Game Families",
        "",
    ]
    for family in mg["families"]:
        lines.extend(
            [
                f"### {family['name']}",
                "",
                family["description"],
                "",
                "The payoff can be " + sentence_join(list(family["payoff"])) + ".",
                "",
            ]
        )
    lines.extend(
        [
            "## Table Rules",
            "",
            "Every mini-game follows:",
            "",
            *bullet_lines(mg["policy_rules"]["inherit"]),
            "",
            "They can:",
            "",
            *bullet_lines(mg["policy_rules"]["may"]),
            "",
            "They cannot:",
            "",
            *bullet_lines(mg["policy_rules"]["may_not"]),
            "",
            "## Where It Shows Up",
            "",
            "### On Mobile / PWA",
            "",
            "A phone card should feel " + mobile_feel + ". A player should see " + mobile_must_show + ".",
            "",
            "### In Player Action Cards",
            "",
            "The mini-game should appear as " + player_action_card_shape + ".",
            "",
            "### In GM Cockpit",
            "",
            "The GM should see " + gm_cockpit_shape + ".",
            "",
            "## Best-Case Pattern",
            "",
            "A good one feels like this:",
            "",
            *numbered_lines(mg["wow_pattern"]),
            "",
            "That is the loop worth building toward.",
            "",
            "What to avoid:",
            "",
            *bullet_lines(mg["anti_pattern"]),
            "",
            "## Example Loop",
            "",
            mg["example_loop"],
            "",
            "## What Good Looks Like",
            "",
            "Call it ready only when:",
            "",
            *numbered_lines(mg["flagship_bar"]),
        ]
    )
    return "\n".join(lines) + "\n"


def render_index_section(spec: dict[str, Any]) -> str:
    visual_gallery = [dict(item) for item in list(spec.get("_synced_visual_gallery") or []) if isinstance(item, dict)]
    related_surfaces = [
        dict(item)
        for item in list(spec.get("related_surfaces") or [])
        if isinstance(item, dict)
    ]
    gm_cockpit = dict(spec.get("gm_cockpit_spotlight") or {})
    taxonomy = dict(spec.get("product_taxonomy") or {})
    release_posture = dict(spec.get("release_and_support_posture") or {})
    media_posture = dict(spec.get("media_posture") or {})
    videos = [dict(item) for item in list(spec.get("public_videos") or []) if isinstance(item, dict)]
    gallery_lines: list[str] = []
    if visual_gallery:
        gallery_lines.extend(["<div>", "  <p><strong>Visual notes</strong></p>"])
        for item in visual_gallery:
            gallery_lines.extend(
                [
                    "  <div>",
                    f"    <p><img src=\"{item['image_output']}\" alt=\"{item['alt']}\" style=\"max-width:100%; height:auto;\"></p>",
                    f"    <p><strong>{item['title']}</strong>: {item['caption']}</p>",
                    "  </div>",
                ]
            )
        gallery_lines.append("</div>")
    video_items = video_lines(videos)
    video_html: list[str] = []
    if video_items:
        video_html.extend(["<p><strong>Videos</strong></p>", "<ul>"])
        for item in video_items:
            video_html.append(f"  <li>{item[2:]}</li>")
        video_html.append("</ul>")
    surface_lines: list[str] = []
    if related_surfaces:
        surface_lines.extend(["<ul>"])
        for row in related_surfaces:
            surface_lines.append(f"  <li><strong>{row['name']}</strong>: {row['connection']}</li>")
        surface_lines.append("</ul>")
    return "\n".join(
        [
            INDEX_START,
            "<h3>",
            "<a id=\"flagship-table-pulse-and-living-world\" class=\"anchor\" href=\"#flagship-table-pulse-and-living-world\" aria-hidden=\"true\"><span class=\"octicon octicon-link\"></span></a>Flagship Table Pulse and Living World</h3>",
            "",
            f"<p>{spec['site_summary']}</p>",
            "",
            "<ul>",
            "  <li><a href=\"TABLE_PULSE_FLAGSHIP_SHOWCASE.md\">Table Pulse Flagship Showcase</a></li>",
            "  <li><a href=\"TABLE_PULSE_REMOTE_REACTION_MINIGAMES.md\">Remote Reaction Minigames</a></li>",
            "</ul>",
            "",
            "<p>Use these notes when you want the larger table picture: player action cards, runner identity, GM steering, public-safe fallout, Origin Dossier, ALICE, and short reaction moments that keep the city moving without taking authority away from the GM.</p>",
            f"<p><strong>{taxonomy.get('title', 'Product taxonomy')}</strong>: {taxonomy.get('summary', 'Base features and future expansion bets are separated so the public story stays readable.')}</p>",
            *gallery_lines,
            *video_html,
            f"<p><strong>{media_posture.get('title', 'Video and narration')}</strong>: {media_posture.get('summary', 'Linked media should be authored, captioned, and grounded in product truth.')}</p>",
            f"<p><strong>{release_posture.get('title', 'Downloads and support')}</strong>: {release_posture.get('summary', 'Distribution and support stay quiet, reliable, and separate from product horizons.')}</p>",
            "<p><strong>Origin dossier and ALICE</strong> are now part of this public explanation layer because the flagship story no longer starts only with table heat.</p>",
            f"<p><strong>{gm_cockpit.get('title', 'GM Cockpit')}</strong> keeps GM steering, allowances, mini-game adjudication, and public-safe fallout on one calm control surface.</p>",
            *surface_lines,
            INDEX_END,
        ]
    )


def replace_between(text: str, start: str, end: str, replacement: str) -> str:
    start_index = text.find(start)
    end_index = text.find(end)
    if start_index == -1 or end_index == -1 or end_index < start_index:
        raise RuntimeError("index.html is missing generator markers for Table Pulse docs section")
    end_index += len(end)
    return text[:start_index] + replacement + text[end_index:]


def main() -> None:
    spec = json.loads(SPEC_PATH.read_text(encoding="utf-8"))
    spec["_synced_visual_gallery"] = sync_visual_gallery(spec)
    SHOWCASE_PATH.write_text(render_showcase(spec), encoding="utf-8")
    MINIGAMES_PATH.write_text(render_minigames(spec), encoding="utf-8")

    index_text = INDEX_PATH.read_text(encoding="utf-8")
    replacement = render_index_section(spec)
    INDEX_PATH.write_text(replace_between(index_text, INDEX_START, INDEX_END, replacement), encoding="utf-8")

    print(f"wrote {SHOWCASE_PATH}")
    print(f"wrote {MINIGAMES_PATH}")
    print(f"updated {INDEX_PATH}")


if __name__ == "__main__":
    main()
