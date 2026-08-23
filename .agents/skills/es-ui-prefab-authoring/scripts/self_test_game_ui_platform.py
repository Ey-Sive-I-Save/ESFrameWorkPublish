#!/usr/bin/env python3
"""Exercise the generic ScreenSpec v3 registry with four game UI slices."""

from __future__ import annotations

import json
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
from validate_game_ui_screen_spec import validate  # noqa: E402


def component(component_id: str, component_type: str, zone: str, *, text: str | None = None, value: float | None = None, asset: str | None = None, children: list[dict] | None = None, interactive: str | None = None) -> dict:
    node = {
        "id": component_id,
        "type": component_type,
        "zone": zone,
        "layout": {"mode": "stretch", "bounds": [0.02, 0.02, 0.98, 0.98], "minSize": [44, 44]},
        "content": {},
        "children": children or [],
        "stateVariants": {"default": {}, "disabled": {}}
    }
    if text is not None:
        node["content"]["text"] = text
    if value is not None:
        node["content"]["value"] = value
    if asset is not None:
        node["assetSlots"] = [asset]
    if interactive:
        node["interaction"] = {"intent": interactive, "targetSize": [44, 44]}
    return node


def spec(screen_id: str, screen_type: str, zones: list[dict]) -> dict:
    return {
        "schemaVersion": 3,
        "screenId": screen_id,
        "screenType": screen_type,
        "template": screen_type,
        "prefabPath": f"Assets/UI/Prefabs/Generated/{screen_id}.prefab",
        "fixtureScenePath": f"Assets/UI/Scenes/Generated/{screen_id}.unity",
        "tokens": {"surface": "#182230", "text": "#F4F7FB", "accent": "#53B8FF"},
        "profiles": [
            {"id": "wide", "width": 1920, "height": 1080, "orientation": "landscape"},
            {"id": "narrow", "width": 1080, "height": 1920, "orientation": "portrait"}
        ],
        "states": [{"id": state} for state in ("default", "selected", "empty", "loading", "disabled", "error")],
        "assets": [
            {"id": "frame", "source": "generated-placeholder", "path": "ES/UIAssets/common/frame.png"},
            {"id": "icon", "source": "generated-placeholder", "path": "ES/UIAssets/common/icon.png"},
            {"id": "portrait", "source": "generated-placeholder", "path": "ES/UIAssets/common/portrait.png"}
        ],
        "components": zones,
        "behaviors": [{"id": screen_id + ".input", "inputs": ["confirm", "cancel"], "transitions": []}],
        "bindings": []
    }


def build_samples() -> dict[str, dict]:
    return {
        "inventory": spec("inventory", "collection", [
            component("frame", "frame", "header", asset="frame", children=[component("title", "text", "header", text="Inventory")]),
            component("tabs", "tab-bar", "navigation", children=[component("items-tab", "button", "navigation", text="Items", interactive="switch-tab")], interactive="switch-tab"),
            component("grid", "grid", "content", children=[component("slot-01", "item-slot", "content", asset="icon", interactive="select-item")]),
            component("detail", "detail-panel", "content", children=[component("name", "text", "content", text="Astra Blade")])
        ]),
        "combat-hud": spec("combat-hud", "combat", [
            component("hud-frame", "frame", "safe-area", asset="frame"),
            component("health", "bar", "safe-area", value=1.0),
            component("skill", "cooldown", "action-bar", value=1.0, interactive="use-skill"),
            component("target", "target-panel", "safe-area", children=[component("target-name", "text", "safe-area", text="Training Target")])
        ]),
        "dialogue": spec("dialogue", "conversation", [
            component("dialogue-frame", "frame", "dialogue", asset="frame"),
            component("portrait-view", "portrait", "dialogue", asset="portrait"),
            component("speaker", "nameplate", "dialogue", text="Captain"),
            component("body", "text", "dialogue", text="We move at dawn."),
            component("choices", "choice-list", "choices", children=[component("choice-01", "button", "choices", text="Continue", interactive="choose-dialogue")], interactive="choose-dialogue")
        ]),
        "main-menu": spec("main-menu", "navigation", [
            component("menu-frame", "frame", "header", asset="frame"),
            component("menu-title", "text", "header", text="Main Menu"),
            component("menu-list", "list", "navigation", children=[component("start", "button", "navigation", text="Start", interactive="navigate-start")]),
            component("tooltip", "tooltip", "content", text="Select an option")
        ])
    }


def main() -> int:
    registry_path = SCRIPT_DIR.parent / "references" / "game-ui-component-registry.json"
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    failures: dict[str, list[dict[str, str]]] = {}
    for name, sample in build_samples().items():
        issues = validate(sample, registry)
        if issues:
            failures[name] = issues
    if failures:
        print(json.dumps({"valid": False, "failures": failures}, ensure_ascii=False, indent=2))
        return 2
    print(json.dumps({"valid": True, "sliceCount": 4, "slices": ["inventory", "combat-hud", "dialogue", "main-menu"]}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
