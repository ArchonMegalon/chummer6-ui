#!/usr/bin/env python3
from __future__ import annotations

import json
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


def render_showcase(spec: dict[str, Any]) -> str:
    overview = spec["overview"]
    lines: list[str] = [
        f"# {overview['title']}",
        "",
        "<!-- GENERATED FILE: edit TABLE_PULSE_FLAGSHIP_DOCS_SPEC.json and rerun scripts/generate_jammer5_flagship_docs.py -->",
        "",
        f"Purpose: {overview['purpose']}",
        "",
        "## Delivery Posture",
        "",
        overview["delivery_posture"],
        "",
        "## One-Line Pitch",
        "",
        overview["one_line_pitch"],
        "",
        "## What this system is",
        "",
        "Table Pulse is the live pressure-and-response system for Chummer6.",
        "",
        "It turns high-heat moments at the table into bounded reaction packets, governed player",
        "follow-up, faction movement, and public-safe world fallout. The important rule is that the",
        "system is dramatic without becoming fake authority:",
        "",
        *bullet_lines(overview["system_truths"]),
        "",
        "## Why it feels flagship",
        "",
        "This is not a generic notification center.",
        "",
        "The flagship posture is a five-layer play loop:",
        "",
    ]
    for index, layer in enumerate(overview["layers"], start=1):
        lines.append(f"{index}. `{layer['name']}`")
        lines.extend([f"   - {item}" for item in layer["items"]])
    lines.extend(["", "## Hero Surfaces", ""])
    for surface in overview["surfaces"]:
        descriptor = surface.get("descriptor", "the surface")
        lines.extend(
            [
                f"### {surface['name']}",
                "",
                f"{surface['name']} is {descriptor}.",
                "",
                "It should feel:",
                "",
                *bullet_lines(surface["feels"]),
                "",
                "It must show:",
                "",
                *bullet_lines(surface["must_show"]),
            ]
        )
        if surface.get("must_never_imply"):
            lines.extend(["", "It must never imply:", "", *bullet_lines(surface["must_never_imply"])])
        lines.append("")
    lines.extend(["## Core Play Loop", ""])
    for index, beat in enumerate(overview["play_loop"], start=1):
        lines.extend([f"### {index}. {beat['name']}", "", beat["description"]])
        if beat.get("items"):
            lines.extend(["", *bullet_lines(beat["items"])])
        lines.append("")
    lines.extend(
        [
            "## Wow-Effect Requirements",
            "",
            "To feel flagship, the documentation and the product should present the system as an",
            "action board, not as an admin form.",
            "",
            "The wow effect should come from:",
            "",
            *bullet_lines(overview["wow_effect"]["comes_from"]),
            "",
            "The wow effect must not come from:",
            "",
            *bullet_lines(overview["wow_effect"]["must_not_come_from"]),
            "",
            "## Hero Moment",
            "",
            "The flagship version should let a reader immediately picture the best-case loop:",
            "",
            *numbered_lines(overview["hero_moment"]),
            "",
            "## Hard Product Truths",
            "",
            *bullet_lines(overview["hard_truths"]),
            "",
            "## Flagship Acceptance",
            "",
            "Call this flagship only when all of the following are true:",
            "",
            *numbered_lines(overview["flagship_bar"]),
            "",
            "## Design Source Anchors",
            "",
            "These docs are generated from the published Chummer6 design canon and dramatic briefing,",
            "then rewritten into Jammer5-style docs language for the public docs repo.",
            "",
            *bullet_lines(spec["published_design_sources"]),
        ]
    )
    return "\n".join(lines) + "\n"


def render_minigames(spec: dict[str, Any]) -> str:
    mg = spec["minigames"]
    lines: list[str] = [
        f"# {mg['title']}",
        "",
        "<!-- GENERATED FILE: edit TABLE_PULSE_FLAGSHIP_DOCS_SPEC.json and rerun scripts/generate_jammer5_flagship_docs.py -->",
        "",
        f"Purpose: {mg['purpose']}",
        "",
        "## Product Goal",
        "",
        mg["goal"],
        "",
        "## What these are",
        "",
        "Remote reaction mini-games are short governed follow-up encounters that occur after a",
        "Table Pulse packet is emitted.",
        "",
        "They are:",
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
                "Payoff:",
                "",
                *bullet_lines(family["payoff"]),
                "",
            ]
        )
    lines.extend(
        [
            "## GM And Policy Rules",
            "",
            "Every mini-game must inherit:",
            "",
            *bullet_lines(mg["policy_rules"]["inherit"]),
            "",
            "Mini-games may:",
            "",
            *bullet_lines(mg["policy_rules"]["may"]),
            "",
            "Mini-games may not:",
            "",
            *bullet_lines(mg["policy_rules"]["may_not"]),
            "",
            "## Surface Requirements",
            "",
            "### On Mobile / PWA",
            "",
            "The prompt should feel:",
            "",
            *bullet_lines(mg["surface_requirements"]["mobile_pwa"]["feel"]),
            "",
            "The user must see:",
            "",
            *bullet_lines(mg["surface_requirements"]["mobile_pwa"]["must_show"]),
            "",
            "### In Signal Deck",
            "",
            "The mini-game should appear as:",
            "",
            *bullet_lines(mg["surface_requirements"]["signal_deck"]),
            "",
            "### In GM Cockpit",
            "",
            "The GM should see:",
            "",
            *bullet_lines(mg["surface_requirements"]["gm_cockpit"]),
            "",
            "## Wow-Effect Pattern",
            "",
            "The best version of this feature looks like:",
            "",
            *numbered_lines(mg["wow_pattern"]),
            "",
            "That is the wow loop.",
            "",
            "Not this:",
            "",
            *bullet_lines(mg["anti_pattern"]),
            "",
            "## Example Loop",
            "",
            mg["example_loop"],
            "",
            "## Flagship Bar",
            "",
            "Call the mini-game lane flagship only when:",
            "",
            *numbered_lines(mg["flagship_bar"]),
        ]
    )
    return "\n".join(lines) + "\n"


def render_index_section(spec: dict[str, Any]) -> str:
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
            "<p>These generated docs cover Signal Deck, Runner Passport, the GM cockpit, living newsroom projection, consent and privacy gates, and the new reaction mini-games in a wow-forward but fail-closed product language.</p>",
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
