#!/usr/bin/env python3
"""Validate the generic ScreenSpec v3 contract against the game UI registry."""

from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path
from typing import Any

ID_RE = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
SCREEN_TYPES = {"hud", "navigation", "modal", "conversation", "progression", "collection", "combat", "world", "system", "result"}
LAYOUT_MODES = {"stretch", "center", "edge-docked", "flow", "grid", "list", "overlay", "absolute", "content"}
STATE_IDS = {"default", "selected", "empty", "loading", "disabled", "error", "long-content"}


def fail(issues: list[dict[str, str]], path: str, message: str, code: str = "invalid") -> None:
    issues.append({"code": code, "path": path, "message": message})


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def non_empty_string(value: Any, path: str, issues: list[dict[str, str]]) -> str | None:
    if not isinstance(value, str) or not value.strip():
        fail(issues, path, "must be a non-empty string", "required")
        return None
    return value.strip()


def validate_bounds(value: Any, path: str, issues: list[dict[str, str]]) -> None:
    if not isinstance(value, list) or len(value) != 4:
        fail(issues, path, "must be [xMin,yMin,xMax,yMax]", "layout")
        return
    if any(isinstance(item, bool) or not isinstance(item, (int, float)) or not math.isfinite(float(item)) for item in value):
        fail(issues, path, "must contain finite numbers", "layout")
    elif any(item < 0 or item > 1 for item in value) or value[0] > value[2] or value[1] > value[3]:
        fail(issues, path, "must be normalized and ordered", "layout")


def validate_component(component: Any, path: str, registry: dict[str, Any], assets: dict[str, Any], states: set[str], seen: set[str], issues: list[dict[str, str]]) -> None:
    if not isinstance(component, dict):
        fail(issues, path, "component must be an object", "type")
        return
    component_id = non_empty_string(component.get("id"), f"{path}.id", issues)
    if component_id and (not ID_RE.fullmatch(component_id) or component_id in seen):
        fail(issues, f"{path}.id", "must be unique and use a stable lowercase id", "identity")
    if component_id:
        seen.add(component_id)
    component_type = non_empty_string(component.get("type"), f"{path}.type", issues)
    definition = registry.get("components", {}).get(component_type or "")
    if definition is None:
        fail(issues, f"{path}.type", "component type is not registered", "registry")
        definition = {}
    layout = component.get("layout")
    if not isinstance(layout, dict):
        fail(issues, f"{path}.layout", "every component needs a layout contract", "layout")
    else:
        mode = layout.get("mode")
        if mode not in LAYOUT_MODES:
            fail(issues, f"{path}.layout.mode", f"must be one of {sorted(LAYOUT_MODES)}", "layout")
        validate_bounds(layout.get("bounds"), f"{path}.layout.bounds", issues)
        min_size = layout.get("minSize", [0, 0])
        if not isinstance(min_size, list) or len(min_size) != 2 or any(isinstance(item, bool) or not isinstance(item, (int, float)) or item < 0 for item in min_size):
            fail(issues, f"{path}.layout.minSize", "must be a non-negative numeric pair", "layout")
    asset_slots = component.get("assetSlots", [])
    if not isinstance(asset_slots, list):
        fail(issues, f"{path}.assetSlots", "must be an array", "asset")
        asset_slots = []
    for index, slot in enumerate(asset_slots):
        if not isinstance(slot, str) or not slot.strip():
            fail(issues, f"{path}.assetSlots[{index}]", "must be a non-empty asset id", "asset")
        elif slot not in assets:
            fail(issues, f"{path}.assetSlots[{index}]", "asset id is not declared in assets", "asset")
    if definition.get("requiresAsset") and not asset_slots:
        fail(issues, f"{path}.assetSlots", "registered component requires at least one asset", "asset")
    content = component.get("content", {})
    if not isinstance(content, dict):
        fail(issues, f"{path}.content", "must be an object", "content")
        content = {}
    if definition.get("requiresText") and not isinstance(content.get("text"), str):
        fail(issues, f"{path}.content.text", "registered component requires text content", "content")
    if definition.get("requiresTextOrAsset") and not isinstance(content.get("text"), str) and not asset_slots:
        fail(issues, f"{path}", "registered component requires text or asset content", "content")
    if definition.get("requiresNumericValue") and not isinstance(content.get("value"), (int, float)):
        fail(issues, f"{path}.content.value", "registered component requires a numeric value", "content")
    interaction = component.get("interaction", {})
    if definition.get("interactive"):
        if not isinstance(interaction, dict) or not isinstance(interaction.get("intent"), str) or not interaction.get("intent").strip():
            fail(issues, f"{path}.interaction.intent", "interactive component requires an input intent", "behavior")
        target = interaction.get("targetSize", [44, 44]) if isinstance(interaction, dict) else [0, 0]
        if not isinstance(target, list) or len(target) != 2 or target[0] < 44 or target[1] < 44:
            fail(issues, f"{path}.interaction.targetSize", "interactive target must be at least 44x44", "behavior")
    children = component.get("children", [])
    if not isinstance(children, list):
        fail(issues, f"{path}.children", "must be an array", "structure")
        children = []
    if definition.get("requiresChildren") and not children:
        fail(issues, f"{path}.children", "registered component requires children", "structure")
    child_seen: set[str] = set()
    for index, child in enumerate(children):
        validate_component(child, f"{path}.children[{index}]", registry, assets, states, child_seen, issues)
    variants = component.get("stateVariants", {})
    if isinstance(variants, list):
        for index, variant in enumerate(variants):
            state_id = variant.get("stateId") if isinstance(variant, dict) else None
            if state_id not in states and state_id not in STATE_IDS:
                fail(issues, f"{path}.stateVariants[{index}].stateId", "state is not declared", "state")
    elif isinstance(variants, dict):
        for state_id in variants:
            if state_id not in states and state_id not in STATE_IDS:
                fail(issues, f"{path}.stateVariants.{state_id}", "state is not declared", "state")
    else:
        fail(issues, f"{path}.stateVariants", "must be an object or array", "state")


def validate(spec: Any, registry: dict[str, Any]) -> list[dict[str, str]]:
    issues: list[dict[str, str]] = []
    if not isinstance(spec, dict):
        return [{"code": "type", "path": "$", "message": "ScreenSpec root must be an object"}]
    if spec.get("schemaVersion") != 3:
        fail(issues, "schemaVersion", "must equal 3", "schema")
    screen_id = non_empty_string(spec.get("screenId"), "screenId", issues)
    if screen_id and not ID_RE.fullmatch(screen_id):
        fail(issues, "screenId", "must use a stable lowercase id", "identity")
    screen_type = spec.get("screenType")
    if screen_type not in SCREEN_TYPES:
        fail(issues, "screenType", f"must be one of {sorted(SCREEN_TYPES)}", "template")
    template = spec.get("template", screen_type)
    if template not in registry.get("templates", {}):
        fail(issues, "template", "template is not registered", "registry")
    profiles = spec.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        fail(issues, "profiles", "must declare at least one profile", "responsive")
        profiles = []
    profile_ids: set[str] = set()
    for index, profile in enumerate(profiles):
        path = f"profiles[{index}]"
        if not isinstance(profile, dict):
            fail(issues, path, "profile must be an object", "type")
            continue
        profile_id = non_empty_string(profile.get("id"), f"{path}.id", issues)
        if profile_id in profile_ids:
            fail(issues, f"{path}.id", "profile id must be unique", "identity")
        if profile_id:
            profile_ids.add(profile_id)
        if not isinstance(profile.get("width"), int) or not isinstance(profile.get("height"), int) or profile["width"] <= 0 or profile["height"] <= 0:
            fail(issues, f"{path}.width/height", "must be positive integers", "responsive")
    states = spec.get("states")
    if not isinstance(states, list) or not states:
        fail(issues, "states", "must declare at least one state", "state")
        states = []
    state_ids: set[str] = set()
    for index, state in enumerate(states):
        path = f"states[{index}]"
        if not isinstance(state, dict):
            fail(issues, path, "state must be an object", "type")
            continue
        state_id = non_empty_string(state.get("id"), f"{path}.id", issues)
        if state_id in state_ids:
            fail(issues, f"{path}.id", "state id must be unique", "identity")
        if state_id:
            state_ids.add(state_id)
    assets_value = spec.get("assets", [])
    assets = assets_value if isinstance(assets_value, dict) else {item.get("id"): item for item in assets_value if isinstance(item, dict) and isinstance(item.get("id"), str)} if isinstance(assets_value, list) else {}
    if not isinstance(assets_value, (dict, list)):
        fail(issues, "assets", "must be an object or array", "asset")
    for asset_id, asset in assets.items():
        if not ID_RE.fullmatch(str(asset_id)) or not isinstance(asset, dict):
            fail(issues, f"assets.{asset_id}", "asset id and entry must be valid", "asset")
        elif asset.get("source") not in {"project-sprite", "ai-generated", "generated-placeholder"}:
            fail(issues, f"assets.{asset_id}.source", "must classify asset source", "asset")
    components = spec.get("components")
    if not isinstance(components, list) or not components:
        fail(issues, "components", "must contain at least one component", "structure")
        components = []
    seen: set[str] = set()
    for index, component in enumerate(components):
        validate_component(component, f"components[{index}]", registry, assets, state_ids, seen, issues)
    template_definition = registry.get("templates", {}).get(template, {})
    declared_zones = {component.get("zone") for component in components if isinstance(component, dict) and isinstance(component.get("zone"), str)}
    for zone in template_definition.get("requiredZones", []):
        if zone not in declared_zones:
            fail(issues, "components", f"template requires zone '{zone}'", "template")
    behaviors = spec.get("behaviors", [])
    if not isinstance(behaviors, list):
        fail(issues, "behaviors", "must be an array", "behavior")
    return issues


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path)
    parser.add_argument("--registry", type=Path, default=Path(__file__).resolve().parents[1] / "references" / "game-ui-component-registry.json")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        issues = validate(load(args.spec), load(args.registry))
    except (OSError, UnicodeError, json.JSONDecodeError, TypeError, ValueError) as exc:
        issues = [{"code": "load", "path": "$", "message": str(exc)}]
    result = {"valid": not issues, "issueCount": len(issues), "issues": issues}
    print(json.dumps(result, ensure_ascii=False, indent=2) if args.json else ("PASS: generic ScreenSpec v3" if not issues else "FAIL: generic ScreenSpec v3 (" + str(len(issues)) + " issues)"))
    return 0 if not issues else 2


if __name__ == "__main__":
    raise SystemExit(main())
