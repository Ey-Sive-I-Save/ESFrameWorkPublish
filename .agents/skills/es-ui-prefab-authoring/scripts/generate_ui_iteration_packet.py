#!/usr/bin/env python3
"""Generate deterministic rebuild and incremental-iteration ScreenSpec packets."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
from typing import Any


ASSET_IDS = [
    "lobby-background",
    "lobby-frame",
    "player-avatar",
    "hero-banner",
    "rank-emblem",
    "mode-ranked",
    "mode-match",
    "mode-training",
    "friend-avatar",
    "mail-icon",
    "settings-icon",
]


def asset_manifest() -> list[dict[str, str]]:
    return [
        {
            "id": asset_id,
            "role": asset_id,
            "source": "generated-procedural",
            "path": f"Assets/UI/Generated/AIUI/{asset_id}.asset",
            "fallback": f"{asset_id}-missing",
        }
        for asset_id in ASSET_IDS
    ]


def layout(mode: str, bounds: list[float], minimum: list[float], responsive: str = "both", **extra: Any) -> dict[str, Any]:
    value: dict[str, Any] = {"mode": mode, "bounds": bounds, "minSize": minimum}
    if responsive != "both":
        value["responsiveMode"] = responsive
    value.update(extra)
    return value


def component(
    item_id: str,
    item_type: str,
    zone: str,
    bounds: list[float],
    minimum: list[float],
    *,
    text: str | None = None,
    visual: str = "surface",
    assets: list[str] | None = None,
    children: list[dict[str, Any]] | None = None,
    responsive: str = "both",
    interaction: str | None = None,
    layout_mode: str = "absolute",
    **extra: Any,
) -> dict[str, Any]:
    content: dict[str, Any] = {} if text is None else {"text": text}
    if "value" in extra:
        content["value"] = extra.pop("value")
    value: dict[str, Any] = {
        "id": item_id,
        "type": item_type,
        "zone": zone,
        "layout": layout(layout_mode, bounds, minimum, responsive, **extra.pop("layoutExtra", {})),
        "content": content,
        "visualVariant": visual,
        "stateVariants": {"default": {}},
    }
    if assets:
        value["assetSlots"] = assets
    if children:
        value["children"] = children
    if interaction:
        value["interaction"] = {"intent": interaction, "targetSize": minimum}
    value.update(extra)
    return value


def button(item_id: str, zone: str, bounds: list[float], text: str, intent: str, visual: str = "accent", responsive: str = "both", assets: list[str] | None = None) -> dict[str, Any]:
    value = component(item_id, "button", zone, bounds, [96, 52], text=text, visual=visual, interaction=intent, responsive=responsive)
    if assets:
        value["assetSlots"] = assets
    return value


def rebuild_spec(screen_id: str) -> dict[str, Any]:
    wide_topbar = component(
        "wide-topbar", "frame", "header", [0.025, 0.035, 0.975, 0.155], [960, 92], responsive="wide", visual="surface",
        children=[
            component("wide-avatar", "icon", "header", [0.018, 0.12, 0.095, 0.88], [72, 72], assets=["player-avatar"], visual="accent", responsive="wide"),
            component("wide-player-name", "text", "header", [0.115, 0.16, 0.31, 0.48], [180, 28], text="曜石旅者", visual="text", responsive="wide"),
            component("wide-player-level", "text", "header", [0.115, 0.52, 0.32, 0.82], [180, 22], text="Lv. 38  ·  巅峰之路", visual="mutedText", responsive="wide"),
            component("wide-rank", "status-badge", "header", [0.35, 0.20, 0.49, 0.80], [150, 46], text="星耀 III  ·  赛季 18", assets=["rank-emblem"], visual="accent", responsive="wide"),
            component("wide-gold", "counter", "header", [0.62, 0.20, 0.74, 0.80], [140, 46], text="12,840  金币", value=12840, visual="surface", responsive="wide"),
            component("wide-gems", "counter", "header", [0.75, 0.20, 0.85, 0.80], [120, 46], text="680  星石", value=680, visual="surface", responsive="wide"),
            button("wide-mail", "header", [0.875, 0.18, 0.925, 0.82], "", "open-mail", "accent", "wide", ["mail-icon"]),
            button("wide-settings", "header", [0.935, 0.18, 0.985, 0.82], "", "open-settings", "surface", "wide", ["settings-icon"]),
        ],
    )
    wide_hero = component(
        "wide-hero", "frame", "content", [0.025, 0.19, 0.685, 0.775], [820, 520], responsive="wide", visual="surface",
        children=[
            component("wide-hero-art", "image", "content", [0.02, 0.04, 0.46, 0.96], [380, 400], assets=["hero-banner"], visual="accent", responsive="wide"),
            component("wide-season", "status-badge", "content", [0.51, 0.10, 0.94, 0.19], [240, 38], text="S18  星海远征季", visual="accent", responsive="wide"),
            component("wide-hero-title", "text", "content", [0.51, 0.25, 0.95, 0.42], [340, 52], text="向星海发起挑战", visual="text", responsive="wide"),
            component("wide-hero-copy", "text", "content", [0.51, 0.44, 0.93, 0.56], [330, 42], text="组建你的队伍，赢取赛季限定奖励", visual="mutedText", responsive="wide"),
            button("wide-ranked", "content", [0.51, 0.63, 0.94, 0.76], "排位赛   开始匹配", "open-ranked", "accent", "wide"),
            component("wide-mode-strip", "frame", "content", [0.51, 0.80, 0.94, 0.93], [340, 58], responsive="wide", visual="surface", layout_mode="flow", layoutExtra={"gap": 10, "padding": [8, 8, 8, 8], "columns": 3}, children=[
                button("wide-match", "content", [0, 0, 0.32, 1], "匹配", "open-match", "surface", "wide", ["mode-match"]),
                button("wide-training", "content", [0.34, 0, 0.66, 1], "训练", "open-training", "surface", "wide", ["mode-training"]),
                component("wide-reward", "status-badge", "content", [0.68, 0, 1, 1], [100, 46], text="奖励 +20%", visual="accent", responsive="wide"),
            ]),
        ],
    )
    wide_party = component(
        "wide-party", "frame", "content", [0.72, 0.19, 0.975, 0.775], [310, 520], responsive="wide", visual="surface",
        children=[
            component("wide-party-title", "text", "content", [0.08, 0.88, 0.92, 0.97], [230, 32], text="好友与组队", visual="text", responsive="wide"),
            component("wide-party-status", "stat-row", "content", [0.08, 0.77, 0.92, 0.86], [230, 52], text="当前队伍  1 / 5", visual="accent", responsive="wide"),
            component("wide-friend-list", "list", "content", [0.06, 0.25, 0.94, 0.74], [250, 230], responsive="wide", visual="surface", layout_mode="list", layoutExtra={"gap": 8, "padding": [8, 8, 8, 8]}, children=[
                component("wide-friend-one", "stat-row", "content", [0, 0, 1, 1], [230, 58], text="青焰  ·  在线", assets=["friend-avatar"], visual="surface", responsive="wide"),
                component("wide-friend-two", "stat-row", "content", [0, 0, 1, 1], [230, 58], text="霜叶  ·  在线", assets=["friend-avatar"], visual="surface", responsive="wide"),
                component("wide-friend-three", "stat-row", "content", [0, 0, 1, 1], [230, 58], text="北辰  ·  训练中", assets=["friend-avatar"], visual="surface", responsive="wide"),
            ]),
            button("wide-invite", "content", [0.08, 0.08, 0.92, 0.19], "邀请好友组队", "invite-friend", "accent", "wide"),
        ],
    )
    wide_nav = component(
        "wide-navigation", "tab-bar", "navigation", [0.025, 0.825, 0.975, 0.965], [960, 76], responsive="wide", visual="surface", interaction="navigate-main-tab", layout_mode="flow", layoutExtra={"gap": 10, "padding": [8, 8, 8, 8]},
        children=[
            button("wide-home", "navigation", [0, 0, 1, 1], "大厅", "open-home", "accent", "wide"),
            button("wide-heroes", "navigation", [0, 0, 1, 1], "英雄", "open-heroes", "surface", "wide"),
            button("wide-inventory", "navigation", [0, 0, 1, 1], "背包", "open-inventory", "surface", "wide"),
            button("wide-clan", "navigation", [0, 0, 1, 1], "战队", "open-clan", "surface", "wide"),
            button("wide-events", "navigation", [0, 0, 1, 1], "活动", "open-events", "surface", "wide"),
        ],
    )

    narrow_topbar = component(
        "narrow-topbar", "frame", "header", [0.03, 0.03, 0.97, 0.14], [720, 104], responsive="narrow", visual="surface",
        children=[
            component("narrow-avatar", "icon", "header", [0.03, 0.14, 0.13, 0.86], [72, 72], assets=["player-avatar"], visual="accent", responsive="narrow"),
            component("narrow-player-name", "text", "header", [0.16, 0.25, 0.47, 0.55], [190, 26], text="曜石旅者", visual="text", responsive="narrow"),
            component("narrow-rank", "status-badge", "header", [0.16, 0.58, 0.48, 0.86], [190, 28], text="星耀 III", assets=["rank-emblem"], visual="accent", responsive="narrow"),
            component("narrow-gold", "counter", "header", [0.56, 0.25, 0.77, 0.75], [150, 42], text="12,840", value=12840, visual="surface", responsive="narrow"),
            button("narrow-settings", "header", [0.83, 0.18, 0.96, 0.82], "", "open-settings", "surface", "narrow", ["settings-icon"]),
        ],
    )
    narrow_hero = component(
        "narrow-hero", "frame", "content", [0.03, 0.16, 0.97, 0.57], [720, 620], responsive="narrow", visual="surface",
        children=[
            component("narrow-hero-art", "image", "content", [0.04, 0.06, 0.42, 0.94], [260, 450], assets=["hero-banner"], visual="accent", responsive="narrow"),
            component("narrow-season", "status-badge", "content", [0.47, 0.12, 0.94, 0.22], [240, 36], text="S18  星海远征季", visual="accent", responsive="narrow"),
            component("narrow-hero-title", "text", "content", [0.47, 0.28, 0.95, 0.45], [310, 48], text="向星海发起挑战", visual="text", responsive="narrow"),
            button("narrow-ranked", "content", [0.47, 0.58, 0.95, 0.72], "开始匹配", "open-ranked", "accent", "narrow", ["mode-ranked"]),
            component("narrow-reward", "status-badge", "content", [0.47, 0.78, 0.95, 0.90], [200, 36], text="赛季奖励已刷新", visual="mutedText", responsive="narrow"),
        ],
    )
    narrow_party = component(
        "narrow-party", "frame", "content", [0.03, 0.60, 0.97, 0.835], [720, 320], responsive="narrow", visual="surface",
        children=[
            component("narrow-party-title", "text", "content", [0.05, 0.74, 0.42, 0.90], [250, 30], text="好友与组队", visual="text", responsive="narrow"),
            component("narrow-friend-list", "list", "content", [0.04, 0.22, 0.96, 0.68], [660, 130], responsive="narrow", visual="surface", layout_mode="flow", layoutExtra={"gap": 8, "padding": [8, 8, 8, 8]}, children=[
                component("narrow-friend-one", "stat-row", "content", [0, 0, 0.32, 1], [190, 74], text="青焰  在线", assets=["friend-avatar"], visual="surface", responsive="narrow"),
                component("narrow-friend-two", "stat-row", "content", [0.34, 0, 0.66, 1], [190, 74], text="霜叶  在线", assets=["friend-avatar"], visual="surface", responsive="narrow"),
                component("narrow-friend-three", "stat-row", "content", [0.68, 0, 1, 1], [190, 74], text="北辰  训练中", assets=["friend-avatar"], visual="surface", responsive="narrow"),
            ]),
            button("narrow-invite", "content", [0.72, 0.74, 0.95, 0.91], "邀请", "invite-friend", "accent", "narrow"),
        ],
    )
    narrow_nav = component(
        "narrow-navigation", "tab-bar", "navigation", [0.03, 0.86, 0.97, 0.965], [720, 80], responsive="narrow", visual="surface", interaction="navigate-main-tab", layout_mode="flow", layoutExtra={"gap": 6, "padding": [6, 6, 6, 6]},
        children=[
            button("narrow-home", "navigation", [0, 0, 1, 1], "大厅", "open-home", "accent", "narrow"),
            button("narrow-heroes", "navigation", [0, 0, 1, 1], "英雄", "open-heroes", "surface", "narrow"),
            button("narrow-inventory", "navigation", [0, 0, 1, 1], "背包", "open-inventory", "surface", "narrow"),
            button("narrow-clan", "navigation", [0, 0, 1, 1], "战队", "open-clan", "surface", "narrow"),
            button("narrow-events", "navigation", [0, 0, 1, 1], "活动", "open-events", "surface", "narrow"),
        ],
    )

    return {
        "schemaVersion": 3,
        "screenId": screen_id,
        "screenType": "navigation",
        "template": "navigation",
        "prefabPath": f"Assets/UI/Prefabs/Generated/{screen_id}.prefab",
        "fixtureScenePath": f"Assets/UI/Scenes/Generated/{screen_id}Fixture.unity",
        "tokens": {"background": "#060A16", "surface": "#10243A", "accent": "#4DE1FF", "text": "#F5FAFF", "mutedText": "#9DB5CC", "danger": "#EF6D78", "titleSize": 34, "bodySize": 20, "buttonSize": 21, "spacing": 14, "padding": 24},
        "profiles": [{"id": "wide", "width": 1920, "height": 1080, "orientation": "landscape"}, {"id": "narrow", "width": 1080, "height": 1920, "orientation": "portrait"}],
        "states": [{"id": state, "fixture": f"{screen_id}/{state}"} for state in ["default", "selected", "disabled", "loading", "error", "long-content"]],
        "assets": asset_manifest(),
        "components": [component("lobby-background", "frame", "safe-area", [0, 0, 1, 1], [720, 520], assets=["lobby-background"], visual="background"), wide_topbar, wide_hero, wide_party, wide_nav, narrow_topbar, narrow_hero, narrow_party, narrow_nav],
        "behaviors": [{"id": f"{screen_id}.navigation", "inputs": ["open-home", "open-ranked", "open-match", "open-training", "open-heroes", "open-inventory", "open-clan", "open-events", "invite-friend", "open-settings"], "transitions": []}],
        "bindings": [],
        "designEvidence": {"schemaVersion": 2, "sourceType": "generated-design-brief", "brief": "原创星海竞技大厅，主动作突出，玩家身份、赛季目标、组队和主导航形成清晰阅读层级。", "referenceImages": [], "assetDecisions": [{"role": "all declared image slots", "status": "generated-procedural", "reason": "原创可重复视觉资源，由 Materializer 按资源槽生成并记录。"}], "responsiveDecisions": [{"profileId": "narrow", "strategy": "hero-first-stack", "layoutPolicy": "preserve-primary-action", "changes": ["hero remains dominant", "party compresses into horizontal list", "secondary topbar actions collapse"], "reason": "保留主动作和触控目标。"}], "assumptions": [{"statement": "这是原创竞技大厅，不使用任何商业游戏商标、角色或素材。", "confidence": 1.0}]},
    }


def iterate_spec(source: dict[str, Any], screen_id: str) -> dict[str, Any]:
    result = copy.deepcopy(source)
    result["screenId"] = screen_id
    result["prefabPath"] = f"Assets/UI/Prefabs/Generated/{screen_id}.prefab"
    result["fixtureScenePath"] = f"Assets/UI/Scenes/Generated/{screen_id}Fixture.unity"
    result["tokens"] = {"background": "#060A16", "surface": "#10243A", "accent": "#4DE1FF", "text": "#F5FAFF", "mutedText": "#9DB5CC", "danger": "#EF6D78", "titleSize": 34, "bodySize": 20, "buttonSize": 21, "spacing": 14, "padding": 24}
    result["assets"] = asset_manifest()

    def visit(node: dict[str, Any]) -> None:
        item_type = str(node.get("type", "frame"))
        node["visualVariant"] = "accent" if item_type in {"button", "progress", "status-badge"} else "text" if item_type in {"text", "subtitle"} else "surface"
        if item_type in {"image", "icon", "portrait"}:
            node["visualVariant"] = "accent"
        if node.get("id", "").endswith("-mail"):
            node["assetSlots"] = ["mail-icon"]
            node.setdefault("content", {})["text"] = ""
        elif node.get("id", "").endswith("-settings"):
            node["assetSlots"] = ["settings-icon"]
            node.setdefault("content", {})["text"] = ""
        node.setdefault("stateVariants", {"default": {}})
        for child in node.get("children", []) or []:
            visit(child)

    for node in result.get("components", []) or []:
        visit(node)
    evidence = result.setdefault("designEvidence", {})
    evidence["sourceType"] = "iterated-from-existing-spec"
    evidence["assetDecisions"] = [{"role": "all declared image slots", "status": "generated-procedural", "reason": "增量迭代将原有 fallback 替换为可重复的原创视觉资源。"}]
    evidence.setdefault("assumptions", []).append({"statement": "本版本保留原 ScreenSpec 语义树，仅修订视觉资源、颜色角色和呈现层级。", "confidence": 1.0})
    return result


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value: dict[str, Any]) -> str:
    encoded = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encoded)
    return sha256_bytes(encoded)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--screen-id", default="arena-moba-lobby")
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8"))
    rebuild = rebuild_spec(args.screen_id + "-rebuild")
    iteration = iterate_spec(source, args.screen_id + "-iteration")
    rebuild_path = args.out_dir / f"{args.screen_id}-rebuild.screen-spec.v3.json"
    iteration_path = args.out_dir / f"{args.screen_id}-iteration.screen-spec.v3.json"
    rebuild_hash = write_json(rebuild_path, rebuild)
    iteration_hash = write_json(iteration_path, iteration)
    receipt = {
        "schemaVersion": 1,
        "sourceSpec": str(args.source).replace("\\", "/"),
        "modes": {
            "rebuild": {"path": str(rebuild_path).replace("\\", "/"), "specSha256": rebuild_hash, "source": "design-blueprint"},
            "iterate": {"path": str(iteration_path).replace("\\", "/"), "specSha256": iteration_hash, "source": "existing-screen-spec"},
        },
        "deterministic": rebuild_hash == write_json(args.out_dir / ".rebuild-check.json", rebuild),
    }
    (args.out_dir / ".rebuild-check.json").unlink(missing_ok=True)
    write_json(args.out_dir / "iteration-packet.receipt.json", receipt)
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
