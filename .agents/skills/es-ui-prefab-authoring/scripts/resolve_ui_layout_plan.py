#!/usr/bin/env python3
"""Resolve ScreenSpec normalized geometry into deterministic profile layout plans."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def bounds(value: Any) -> tuple[float, float, float, float] | None:
    if not isinstance(value, list) or len(value) != 4:
        return None
    try:
        result = tuple(float(item) for item in value)
    except (TypeError, ValueError):
        return None
    if result[0] < 0 or result[1] < 0 or result[2] > 1 or result[3] > 1 or result[0] > result[2] or result[1] > result[3]:
        return None
    return result


def overlap(first: tuple[float, float, float, float], second: tuple[float, float, float, float]) -> bool:
    return min(first[2], second[2]) - max(first[0], second[0]) > 1e-6 and min(first[3], second[3]) - max(first[1], second[1]) > 1e-6


def active(node: dict[str, Any], profile_id: str) -> bool:
    layout = node.get("layout") if isinstance(node.get("layout"), dict) else {}
    mode = str(layout.get("responsiveMode", node.get("responsiveMode", "both"))).lower()
    return mode in {"", "both", "all", profile_id.lower()}


def pixel_rect(local: tuple[float, float, float, float], origin: tuple[float, float], size: tuple[float, float]) -> tuple[float, float, float, float]:
    return (origin[0] + local[0] * size[0], origin[1] + local[1] * size[1], origin[0] + local[2] * size[0], origin[1] + local[3] * size[1])


def rect_dict(rect: tuple[float, float, float, float]) -> dict[str, float]:
    return {"x": round(rect[0], 3), "y": round(rect[1], 3), "width": round(rect[2] - rect[0], 3), "height": round(rect[3] - rect[1], 3)}


STATE_GEOMETRY_MUTATION_FIELDS = {
    "bounds", "anchor", "pivot", "layout", "layoutmode", "mode", "minsize",
    "targetsize", "siblingorder", "childgeometryowner", "safearea", "canvas",
    "parent", "position", "size", "width", "height",
}
STATE_GEOMETRY_MUTATION_LABELS = {
    "bounds", "anchor", "pivot", "layout", "layout-mode", "min-size",
    "target-size", "sibling-order", "child-geometry-owner", "safe-area",
    "canvas", "parent", "position", "size",
}


def state_geometry_contract(spec: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Expose the state geometry invariant even when the resolver runs alone."""
    issues: list[dict[str, Any]] = []
    states: list[dict[str, Any]] = []

    def reject_mutations(value: Any, path: str, state_id: str) -> None:
        if isinstance(value, dict):
            for key, nested in value.items():
                normalized = str(key).replace("_", "").replace("-", "").lower()
                if normalized in STATE_GEOMETRY_MUTATION_FIELDS:
                    issues.append({"code": "state-geometry", "stateId": state_id, "path": f"{path}.{key}", "message": "state-local geometry is forbidden; revise the base LayoutPlan"})
                reject_mutations(nested, f"{path}.{key}", state_id)
        elif isinstance(value, list):
            for index, nested in enumerate(value):
                reject_mutations(nested, f"{path}[{index}]", state_id)

    semantics = spec.get("stateSemantics")
    if isinstance(semantics, dict):
        for state_id, semantics_entry in semantics.items():
            if not isinstance(semantics_entry, dict):
                continue
            policy = semantics_entry.get("geometryPolicy")
            allowed = policy.get("allowedChanges") if isinstance(policy, dict) else None
            states.append({"stateId": state_id, "preserveBounds": policy.get("preserveBounds") if isinstance(policy, dict) else None, "allowedChanges": allowed if isinstance(allowed, list) else []})
            if not isinstance(policy, dict) or policy.get("preserveBounds") is not True:
                issues.append({"code": "state-geometry", "stateId": state_id, "path": f"stateSemantics.{state_id}.geometryPolicy.preserveBounds", "message": "resolved state geometry requires preserveBounds: true"})
            elif isinstance(allowed, list):
                forbidden = sorted({str(item).strip().lower() for item in allowed} & STATE_GEOMETRY_MUTATION_LABELS)
                if forbidden:
                    issues.append({"code": "state-geometry", "stateId": state_id, "path": f"stateSemantics.{state_id}.geometryPolicy.allowedChanges", "message": f"preserveBounds cannot allow geometry changes: {forbidden}"})
            effects = semantics_entry.get("effects")
            if isinstance(effects, list):
                for index, effect in enumerate(effects):
                    if isinstance(effect, dict):
                        reject_mutations(effect.get("changes"), f"stateSemantics.{state_id}.effects[{index}].changes", state_id)

    def visit(nodes: list[Any], path: str) -> None:
        for index, node in enumerate(nodes if isinstance(nodes, list) else []):
            if not isinstance(node, dict):
                continue
            variants = node.get("stateVariants")
            if isinstance(variants, dict):
                for state_id, variant in variants.items():
                    reject_mutations(variant, f"{path}[{index}].stateVariants.{state_id}", str(state_id))
            elif isinstance(variants, list):
                for variant_index, variant in enumerate(variants):
                    if isinstance(variant, dict):
                        state_id = str(variant.get("stateId", ""))
                        reject_mutations({key: value for key, value in variant.items() if key != "stateId"}, f"{path}[{index}].stateVariants[{variant_index}]", state_id)
            visit(node.get("children", []), f"{path}[{index}].children")

    visit(spec.get("components", []), "components")
    return states, issues


def min_size(node: dict[str, Any]) -> tuple[float, float]:
    value = (node.get("layout") or {}).get("minSize", [0, 0])
    if isinstance(value, list) and len(value) == 2:
        try:
            return max(0.0, float(value[0])), max(0.0, float(value[1]))
        except (TypeError, ValueError):
            pass
    return 0.0, 0.0


def layout_group_children(node: dict[str, Any], parent_origin: tuple[float, float], parent_size: tuple[float, float], parent_rect: tuple[float, float, float, float], profile_id: str, issues: list[dict[str, Any]], suggestions: list[dict[str, Any]]) -> list[dict[str, Any]]:
    layout = node.get("layout") or {}
    children = [child for child in node.get("children", []) if isinstance(child, dict) and active(child, profile_id)]
    mode = str(layout.get("mode", "absolute")).lower()
    default_owner = "parent-layout-group" if children and mode in {"list", "flow", "grid"} else "child-bounds"
    owner = str(layout.get("childGeometryOwner", default_owner))
    if owner != "parent-layout-group" or mode not in {"list", "flow", "grid"}:
        return []
    padding = layout.get("padding", [0, 0, 0, 0])
    if not isinstance(padding, list) or len(padding) != 4:
        padding = [0, 0, 0, 0]
    left, right, top, bottom = (max(0.0, float(value)) for value in padding)
    gap = max(0.0, float(layout.get("gap", 0) or 0))
    inner_origin = (parent_rect[0] + left, parent_rect[1] + bottom)
    inner_width = max(0.0, parent_rect[2] - parent_rect[0] - left - right)
    inner_height = max(0.0, parent_rect[3] - parent_rect[1] - top - bottom)
    resolved: list[dict[str, Any]] = []
    if mode == "grid":
        columns = max(1, int(layout.get("columns", 1) or 1))
        cell_width = float(layout.get("cellSize", [0, 0])[0] or 0) if isinstance(layout.get("cellSize"), list) else 0
        cell_height = float(layout.get("cellSize", [0, 0])[1] or 0) if isinstance(layout.get("cellSize"), list) else 0
        cell_width = cell_width or max(0.0, (inner_width - gap * (columns - 1)) / columns)
        rows = max(1, (len(children) + columns - 1) // columns)
        cell_height = cell_height or max(0.0, (inner_height - gap * (rows - 1)) / rows)
        for index, child in enumerate(children):
            col, row = index % columns, index // columns
            local_rect = (col * (cell_width + gap), inner_height - (row + 1) * cell_height - row * gap, (col + 1) * cell_width + col * gap, inner_height - row * (cell_height + gap))
            resolved.append({"node": child, "rect": (inner_origin[0] + local_rect[0], inner_origin[1] + local_rect[1], inner_origin[0] + local_rect[2], inner_origin[1] + local_rect[3])})
    else:
        horizontal = mode == "flow" and str(layout.get("axis", "horizontal")).lower() != "vertical"
        if mode == "list":
            horizontal = str(layout.get("axis", "vertical")).lower() == "horizontal"
        if horizontal:
            width = max(0.0, (inner_width - gap * max(0, len(children) - 1)) / max(1, len(children)))
            cursor = inner_origin[0]
            for child in children:
                resolved.append({"node": child, "rect": (cursor, inner_origin[1], cursor + width, inner_origin[1] + inner_height)})
                cursor += width + gap
        else:
            height = max(0.0, (inner_height - gap * max(0, len(children) - 1)) / max(1, len(children)))
            cursor = inner_origin[1] + inner_height
            for child in children:
                resolved.append({"node": child, "rect": (inner_origin[0], cursor - height, inner_origin[0] + inner_width, cursor)})
                cursor -= height + gap
    for item in resolved:
        child = item["node"]
        child_rect = item["rect"]
        required = min_size(child)
        if child_rect[2] - child_rect[0] + 0.5 < required[0] or child_rect[3] - child_rect[1] + 0.5 < required[1]:
            issues.append({"code": "min-size", "profileId": profile_id, "componentId": child.get("id", ""), "message": "layout group resolved child is smaller than minSize"})
            suggestions.append({"code": "layout-group-reflow", "profileId": profile_id, "componentId": node.get("id", ""), "action": "increase parent bounds or choose a profile-specific stack/grid variant"})
    return resolved


def resolve_children(nodes: list[Any], profile_id: str, origin: tuple[float, float], size: tuple[float, float], issues: list[dict[str, Any]], suggestions: list[dict[str, Any]], path: str = "components", inherited: list[dict[str, Any]] | None = None, rect_overrides: list[tuple[float, float, float, float]] | None = None, screen_origin: tuple[float, float] | None = None, screen_size: tuple[float, float] | None = None) -> list[dict[str, Any]]:
    active_nodes = [node for node in nodes if isinstance(node, dict) and active(node, profile_id)]
    resolved_nodes: list[dict[str, Any]] = []
    for index, node in enumerate(active_nodes):
        local = bounds((node.get("layout") or {}).get("bounds"))
        if local is None:
            issues.append({"code": "bounds", "profileId": profile_id, "componentId": node.get("id", ""), "message": "component has no valid normalized bounds"})
            continue
        # Layout groups already resolve child geometry in pixels. Reapplying a
        # child's normalized bounds here would shrink the child a second time.
        anchor = (node.get("layout") or {}).get("anchor") if isinstance((node.get("layout") or {}).get("anchor"), dict) else {}
        safe_policy = str(anchor.get("safeArea", "inherit")).lower()
        # Only the validated top-level background may intentionally cover notches
        # and system insets. Every other node resolves inside its parent/safe area.
        use_screen_rect = path == "components" and safe_policy == "ignore" and screen_origin is not None and screen_size is not None
        rect = rect_overrides[index] if rect_overrides is not None and index < len(rect_overrides) else pixel_rect(local, screen_origin if use_screen_rect else origin, screen_size if use_screen_rect else size)
        required = min_size(node)
        if rect[2] - rect[0] + 0.5 < required[0] or rect[3] - rect[1] + 0.5 < required[1]:
            issues.append({"code": "min-size", "profileId": profile_id, "componentId": node.get("id", ""), "message": "resolved bounds cannot satisfy minSize"})
            suggestions.append({"code": "profile-reflow", "profileId": profile_id, "componentId": node.get("id", ""), "action": "reflow or omit this component for the profile; do not uniformly scale"})
        if not use_screen_rect and (rect[0] < origin[0] - 0.5 or rect[1] < origin[1] - 0.5 or rect[2] > origin[0] + size[0] + 0.5 or rect[3] > origin[1] + size[1] + 0.5):
            issues.append({"code": "out-of-parent", "profileId": profile_id, "componentId": node.get("id", ""), "message": "resolved bounds leave the parent/safe-area rectangle"})
        layout_mode = str((node.get("layout") or {}).get("mode", "absolute")).lower()
        layout_owner = "parent-layout-group" if node.get("children") and layout_mode in {"list", "flow", "grid"} else "child-bounds"
        record = {"id": node.get("id", ""), "type": node.get("type", ""), "safeAreaPolicy": safe_policy, "geometryOwner": str((node.get("layout") or {}).get("childGeometryOwner", layout_owner)), "authoredBounds": list(local), "resolvedRect": rect_dict(rect), "children": []}
        grouped = layout_group_children(node, origin, size, rect, profile_id, issues, suggestions)
        if grouped:
            for item in grouped:
                child_record = resolve_children([item["node"]], profile_id, (item["rect"][0], item["rect"][1]), (item["rect"][2] - item["rect"][0], item["rect"][3] - item["rect"][1]), issues, suggestions, f"{path}[{index}].children", rect_overrides=[item["rect"]], screen_origin=screen_origin, screen_size=screen_size)
                record["children"].extend(child_record)
        else:
            record["children"] = resolve_children(node.get("children", []), profile_id, (rect[0], rect[1]), (rect[2] - rect[0], rect[3] - rect[1]), issues, suggestions, f"{path}[{index}].children", screen_origin=screen_origin, screen_size=screen_size)
        resolved_nodes.append({"record": record, "rect": rect, "node": node})
    for left_index, left in enumerate(resolved_nodes):
        left_layout = left["node"].get("layout") or {}
        if str(left_layout.get("mode", "")).lower() == "overlay" or str(left["node"].get("visualVariant", "")).lower() == "background":
            continue
        for right in resolved_nodes[left_index + 1:]:
            right_layout = right["node"].get("layout") or {}
            if str(right_layout.get("mode", "")).lower() == "overlay" or str(right["node"].get("visualVariant", "")).lower() == "background":
                continue
            if left["record"]["geometryOwner"] == "parent-layout-group" or right["record"]["geometryOwner"] == "parent-layout-group":
                continue
            if overlap(left["rect"], right["rect"]):
                issues.append({"code": "overlap", "profileId": profile_id, "componentId": left["record"]["id"], "with": right["record"]["id"], "message": "resolved sibling rectangles overlap"})
                suggestions.append({"code": "overlap-repair", "profileId": profile_id, "componentId": left["record"]["id"], "with": right["record"]["id"], "action": "choose one axis owner and reflow; changing color or z-order is not a layout repair"})
    return [item["record"] for item in resolved_nodes]


def component_index(nodes: list[Any]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}

    def visit(items: list[Any]) -> None:
        for node in items if isinstance(items, list) else []:
            if not isinstance(node, dict):
                continue
            component_id = node.get("id")
            if isinstance(component_id, str):
                result[component_id] = node
            visit(node.get("children", []))

    visit(nodes)
    return result


def resolved_record_index(records: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}

    def visit(items: list[dict[str, Any]]) -> None:
        for record in items:
            if not isinstance(record, dict):
                continue
            component_id = record.get("id")
            if isinstance(component_id, str):
                result[component_id] = record
            visit(record.get("children", []))

    visit(records)
    return result


def typography_font_size(spec: dict[str, Any], node: dict[str, Any]) -> float:
    role = str(node.get("typographyRole", "body"))
    roles = ((spec.get("designContract") or {}).get("typographyRoles") or {})
    role_contract = roles.get(role) if isinstance(roles, dict) else None
    token = role_contract.get("token") if isinstance(role_contract, dict) else None
    fallback_tokens = {
        "title": "titleSize", "body": "bodySize", "label": "labelSize",
        "caption": "captionSize", "numeric": "numericSize",
    }
    token = token or fallback_tokens.get(role, "bodySize")
    value = (spec.get("tokens") or {}).get(token, 18)
    try:
        return max(1.0, float(value))
    except (TypeError, ValueError):
        return 18.0


def estimated_line_count(text: str, width: float, font_size: float) -> int:
    """Use conservative mixed-script advances; this is not a TMP render claim."""
    if width <= 0 or font_size <= 0:
        return 10**6
    capacity = width
    lines = 0
    for paragraph in text.replace("\r", "").split("\n"):
        advance = 0.0
        for char in paragraph:
            code = ord(char)
            if char.isspace():
                advance += font_size * 0.35
            elif 0x2E80 <= code <= 0x9FFF or 0xAC00 <= code <= 0xD7AF or 0xF900 <= code <= 0xFAFF:
                advance += font_size
            else:
                advance += font_size * 0.60
        lines += max(1, int((advance + capacity - 1e-6) // capacity) if advance > 0 else 1)
    return max(1, lines)


def rect_gap(first: dict[str, float], second: dict[str, float]) -> float:
    first_right, first_top = first["x"] + first["width"], first["y"] + first["height"]
    second_right, second_top = second["x"] + second["width"], second["y"] + second["height"]
    horizontal = max(second["x"] - first_right, first["x"] - second_right, 0.0)
    vertical = max(second["y"] - first_top, first["y"] - second_top, 0.0)
    return max(horizontal, vertical) if horizontal == 0 or vertical == 0 else (horizontal * horizontal + vertical * vertical) ** 0.5


def resolve_fixture_text_fit(spec: dict[str, Any], plans: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Produce profile-pixel text capacity and action-clearance evidence."""
    issues: list[dict[str, Any]] = []
    evidence: list[dict[str, Any]] = []
    nodes = component_index(spec.get("components", []))
    semantics = spec.get("stateSemantics") if isinstance(spec.get("stateSemantics"), dict) else {}
    for plan in plans:
        profile_id = plan.get("profileId")
        records = resolved_record_index(plan.get("nodes", []))
        action_records = [
            records[component_id]["resolvedRect"]
            for component_id, node in nodes.items()
            if node.get("layerRole") == "action" and component_id in records
        ]
        for state_id, state in semantics.items():
            if not isinstance(state, dict):
                continue
            fixture_data = state.get("fixtureData") if isinstance(state.get("fixtureData"), dict) else {}
            bindings = state.get("fixtureTextBindings") if isinstance(state.get("fixtureTextBindings"), list) else []
            for binding_index, binding in enumerate(bindings):
                if not isinstance(binding, dict):
                    continue
                component_id = binding.get("componentId")
                key = binding.get("fixtureDataKey")
                node = nodes.get(component_id) if isinstance(component_id, str) else None
                record = records.get(component_id) if isinstance(component_id, str) else None
                text = fixture_data.get(key) if isinstance(key, str) else None
                path = f"stateSemantics.{state_id}.fixtureTextBindings[{binding_index}]"
                if node is None or not isinstance(text, str):
                    issues.append({"code": "fixture-text", "profileId": profile_id, "path": path, "message": "binding cannot resolve a declared component and fixture string"})
                    continue
                # A ScreenSpec may bind one fixture key to its wide and narrow
                # counterparts. The inactive counterpart has no rectangle in
                # this profile and must be evaluated by its own profile pass.
                if record is None:
                    continue
                insets = binding.get("contentInsetsPx")
                if not isinstance(insets, list) or len(insets) != 4:
                    issues.append({"code": "fixture-text", "profileId": profile_id, "componentId": component_id, "path": path + ".contentInsetsPx", "message": "binding needs four content inset values for pixel capacity estimation"})
                    continue
                left, top, right, bottom = (float(value) for value in insets)
                rect = record["resolvedRect"]
                available_width = rect["width"] - left - right
                available_height = rect["height"] - top - bottom
                font_size = typography_font_size(spec, node)
                line_height = font_size * 1.20
                estimated = estimated_line_count(text, available_width, font_size)
                available_lines = max(0, int(available_height // line_height))
                policy = str(binding.get("overflowPolicy", ""))
                max_lines = binding.get("maxLines")
                allowed_lines = min(available_lines, int(max_lines)) if isinstance(max_lines, int) else 0
                overflow = estimated > allowed_lines
                truncated = policy == "ellipsis" and overflow
                item = {
                    "profileId": profile_id, "stateId": state_id, "componentId": component_id,
                    "fixtureDataKey": key, "textHash": hashlib.sha256(text.encode("utf-8")).hexdigest(),
                    "fontSize": round(font_size, 3), "lineHeight": round(line_height, 3),
                    "rect": rect, "contentInsetsPx": [left, top, right, bottom],
                    "estimatedLines": estimated, "availableLines": available_lines,
                    "maxLines": max_lines, "overflowPolicy": policy, "overflow": overflow,
                    "truncated": truncated,
                }
                evidence.append(item)
                if available_width <= 0 or available_lines < 1:
                    issues.append({"code": "text-capacity", "profileId": profile_id, "componentId": component_id, "message": "text rectangle has no usable pixel capacity after declared insets"})
                elif policy == "wrap" and overflow:
                    issues.append({"code": "text-overflow", "profileId": profile_id, "stateId": state_id, "componentId": component_id, "message": "wrapped fixture text exceeds the declared line and pixel capacity"})
                elif policy == "scroll":
                    issues.append({"code": "text-overflow-policy", "profileId": profile_id, "stateId": state_id, "componentId": component_id, "message": "scroll policy has no registered Materializer scroll-container recipe"})
                clearance = float(binding.get("reserveActionClearancePx", 0) or 0)
                for action_rect in action_records:
                    if action_rect == rect:
                        continue
                    gap = rect_gap(rect, action_rect)
                    if gap + 1e-6 < clearance:
                        issues.append({"code": "action-clearance", "profileId": profile_id, "stateId": state_id, "componentId": component_id, "message": f"fixture text is only {round(gap, 3)}px from an action rectangle; requires {clearance}px"})
                        break
    return evidence, issues


def resolve_interaction_density(spec: dict[str, Any], plans: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Measure touch/action density from final static LayoutGroup rectangles."""
    issues: list[dict[str, Any]] = []
    evidence: list[dict[str, Any]] = []
    advanced = ((spec.get("designContract") or {}).get("advancedComposition") or {})
    density = advanced.get("interactionDensity") if isinstance(advanced, dict) else None
    groups = density.get("groups") if isinstance(density, dict) else []
    if not isinstance(groups, list):
        return evidence, issues
    nodes = component_index(spec.get("components", []))
    records_by_profile = {
        str(plan.get("profileId")): resolved_record_index(plan.get("nodes", []))
        for plan in plans if isinstance(plan, dict)
    }
    for group in groups:
        if not isinstance(group, dict):
            continue
        group_id = str(group.get("id", ""))
        profile_id = str(group.get("profileId", ""))
        component_ids = group.get("componentIds") if isinstance(group.get("componentIds"), list) else []
        required_gap = float(group.get("minGapPx", 0) or 0)
        max_targets = group.get("maxTargets")
        records = records_by_profile.get(profile_id, {})
        rects: list[tuple[str, dict[str, float], list[float]]] = []
        for component_id in component_ids:
            node = nodes.get(component_id)
            record = records.get(component_id)
            interaction = node.get("interaction") if isinstance(node, dict) and isinstance(node.get("interaction"), dict) else {}
            target = interaction.get("targetSize") if isinstance(interaction.get("targetSize"), list) else []
            if not isinstance(component_id, str) or record is None or len(target) != 2:
                continue
            rect = record.get("resolvedRect")
            if isinstance(rect, dict):
                rects.append((component_id, rect, [float(target[0]), float(target[1])]))
        if len(rects) != len(component_ids):
            issues.append({"code": "interaction-density", "profileId": profile_id, "groupId": group_id, "message": "one or more declared controls did not resolve to an active interaction rectangle"})
        target_failures: list[str] = []
        for component_id, rect, target in rects:
            if rect["width"] + 1e-6 < target[0] or rect["height"] + 1e-6 < target[1]:
                target_failures.append(component_id)
                issues.append({"code": "interaction-target", "profileId": profile_id, "groupId": group_id, "componentId": component_id, "message": f"resolved target is {rect['width']:.3f}x{rect['height']:.3f}px; requires {target[0]:.3f}x{target[1]:.3f}px"})
        observed_gaps = [rect_gap(first[1], second[1]) for index, first in enumerate(rects) for second in rects[index + 1:]]
        minimum_observed = min(observed_gaps) if observed_gaps else None
        if isinstance(max_targets, int) and len(rects) > max_targets:
            issues.append({"code": "interaction-density", "profileId": profile_id, "groupId": group_id, "message": "resolved target count exceeds declared maxTargets"})
        if minimum_observed is not None and minimum_observed + 1e-6 < required_gap:
            issues.append({"code": "interaction-gap", "profileId": profile_id, "groupId": group_id, "message": f"minimum resolved gap is {minimum_observed:.3f}px; requires {required_gap:.3f}px"})
        evidence.append({
            "profileId": profile_id,
            "groupId": group_id,
            "targetCount": len(rects),
            "maxTargets": max_targets,
            "minObservedGapPx": round(minimum_observed, 3) if minimum_observed is not None else None,
            "requiredGapPx": required_gap,
            "targetFailures": target_failures,
            "status": "passed" if len(rects) == len(component_ids) and not target_failures and (minimum_observed is None or minimum_observed + 1e-6 >= required_gap) else "blocked",
        })
    return evidence, issues


def _clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(value, maximum))


def _shift_crop_to_contain(start: float, size: float, protected_start: float, protected_end: float) -> float:
    if size + 1e-6 < protected_end - protected_start:
        return start
    minimum_start = max(0.0, protected_end - size)
    maximum_start = min(1.0 - size, protected_start)
    return _clamp(start, minimum_start, maximum_start) if minimum_start <= maximum_start else start


def projected_focal_cover(target_aspect: float, source_aspect: float, focal_point: list[Any], insets: list[Any]) -> tuple[dict[str, float], bool]:
    """Mirror the focal-cover geometry without claiming Sprite/UGUI runtime behavior."""
    focal_x, focal_y = float(focal_point[0]), float(focal_point[1])
    left, bottom, right, top = (float(value) for value in insets)
    width, height = 1.0, 1.0
    x, y = 0.0, 0.0
    if target_aspect > source_aspect:
        height = source_aspect / target_aspect
        y = _clamp(focal_y - height * 0.5, 0.0, 1.0 - height)
    elif target_aspect < source_aspect:
        width = target_aspect / source_aspect
        x = _clamp(focal_x - width * 0.5, 0.0, 1.0 - width)
    protected_left, protected_bottom = left, bottom
    protected_right, protected_top = 1.0 - right, 1.0 - top
    x = _shift_crop_to_contain(x, width, protected_left, protected_right)
    y = _shift_crop_to_contain(y, height, protected_bottom, protected_top)
    safe = x <= protected_left + 1e-6 and y <= protected_bottom + 1e-6 and x + width >= protected_right - 1e-6 and y + height >= protected_top - 1e-6
    return {"x": round(x, 6), "y": round(y, 6), "width": round(width, 6), "height": round(height, 6)}, safe


def resolve_focal_crop_feasibility(spec: dict[str, Any], plans: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Reject a focal-cover policy whose protected subject cannot fit a resolved profile rectangle."""
    evidence: list[dict[str, Any]] = []
    issues: list[dict[str, Any]] = []
    advanced = ((spec.get("designContract") or {}).get("advancedComposition") or {})
    if not isinstance(advanced, dict) or advanced.get("focalTreatment") != "subject":
        return evidence, issues
    assets_value = spec.get("assets", [])
    assets = assets_value if isinstance(assets_value, dict) else {item.get("id"): item for item in assets_value if isinstance(item, dict) and isinstance(item.get("id"), str)}
    policies = {item.get("logicalId"): item for item in advanced.get("focalAssetPolicies", []) if isinstance(item, dict) and isinstance(item.get("logicalId"), str)}
    nodes = component_index(spec.get("components", []))
    records_by_profile = {str(plan.get("profileId")): resolved_record_index(plan.get("nodes", [])) for plan in plans if isinstance(plan, dict)}
    for subject in advanced.get("focalSubjects", []):
        if not isinstance(subject, dict):
            continue
        logical_id = subject.get("logicalId")
        mapping = subject.get("componentIdsByProfile")
        policy = policies.get(logical_id)
        if not isinstance(mapping, dict) or not isinstance(policy, dict) or policy.get("cropPolicy") != "focal-cover":
            continue
        focal = policy.get("focalPoint")
        insets = policy.get("safeCropInsetsNormalized")
        if not isinstance(focal, list) or len(focal) != 2 or not isinstance(insets, list) or len(insets) != 4:
            continue
        policy_assets = [asset_id for asset_id in policy.get("assetIds", []) if isinstance(asset_id, str)]
        for profile_id, component_id in mapping.items():
            record = records_by_profile.get(str(profile_id), {}).get(component_id)
            node = nodes.get(component_id)
            asset_id = next((candidate for candidate in policy_assets if isinstance(node, dict) and candidate in node.get("assetSlots", [])), None)
            asset = assets.get(asset_id) if asset_id else None
            rect = record.get("resolvedRect") if isinstance(record, dict) else None
            source_aspect = asset.get("sourceAspectRatio") if isinstance(asset, dict) else None
            if not isinstance(rect, dict) or not isinstance(source_aspect, (int, float)) or isinstance(source_aspect, bool) or not math.isfinite(float(source_aspect)) or float(source_aspect) <= 0 or float(rect.get("height", 0)) <= 0:
                issues.append({"code": "focal-crop-feasibility", "profileId": str(profile_id), "componentId": str(component_id), "message": "focal-cover needs a resolved component rectangle and positive finite AssetManifest sourceAspectRatio"})
                continue
            target_aspect = float(rect["width"]) / float(rect["height"])
            applied_uv, safe = projected_focal_cover(target_aspect, float(source_aspect), focal, insets)
            evidence.append({"logicalId": logical_id, "profileId": str(profile_id), "componentId": component_id, "assetId": asset_id, "targetAspectRatio": round(target_aspect, 6), "sourceAspectRatio": float(source_aspect), "projectedAppliedUvNormalized": applied_uv, "safeCropSatisfied": safe, "status": "passed" if safe else "blocked"})
            if not safe:
                issues.append({"code": "focal-crop-feasibility", "profileId": str(profile_id), "componentId": component_id, "message": "resolved focal-cover crop cannot preserve safeCropInsetsNormalized; revise the protected region, asset ratio or profile layout"})
    return evidence, issues


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--strict", action="store_true", help="return non-zero when any diagnostic exists")
    args = parser.parse_args()
    spec_path = args.spec.resolve()
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    profiles = spec.get("profiles", [])
    plans: list[dict[str, Any]] = []
    state_geometry, issues = state_geometry_contract(spec)
    suggestions: list[dict[str, Any]] = []
    for profile in profiles:
        if not isinstance(profile, dict):
            continue
        profile_id = str(profile.get("id", ""))
        width, height = int(profile.get("width", 0)), int(profile.get("height", 0))
        safe = profile.get("safeArea", [0, 0, 1, 1])
        if not isinstance(safe, list) or len(safe) != 4:
            safe = [0, 0, 1, 1]
        safe_rect = (float(safe[0]) * width, float(safe[1]) * height, float(safe[2]) * width, float(safe[3]) * height)
        plan_issues: list[dict[str, Any]] = []
        plan_suggestions: list[dict[str, Any]] = []
        nodes = resolve_children(spec.get("components", []), profile_id, (safe_rect[0], safe_rect[1]), (safe_rect[2] - safe_rect[0], safe_rect[3] - safe_rect[1]), plan_issues, plan_suggestions, screen_origin=(0.0, 0.0), screen_size=(float(width), float(height)))
        plans.append({"profileId": profile_id, "resolution": [width, height], "safeArea": rect_dict(safe_rect), "nodes": nodes, "issues": plan_issues, "suggestions": plan_suggestions})
        issues.extend(plan_issues)
        suggestions.extend(plan_suggestions)
    text_fit, text_issues = resolve_fixture_text_fit(spec, plans)
    issues.extend(text_issues)
    interaction_density, density_issues = resolve_interaction_density(spec, plans)
    issues.extend(density_issues)
    focal_crop, focal_crop_issues = resolve_focal_crop_feasibility(spec, plans)
    issues.extend(focal_crop_issues)
    receipt = {"schemaVersion": 1, "resolver": "es-ui-prefab-authoring/resolve_ui_layout_plan", "resolverVersion": 5, "specPath": spec_path.as_posix(), "specSha256": sha256(spec_path), "status": "passed" if not issues else "blocked", "stateGeometryPolicy": {"preserveBounds": True, "states": state_geometry}, "profiles": plans, "textFit": text_fit, "interactionDensity": interaction_density, "focalCropFeasibility": focal_crop, "issues": issues, "suggestions": suggestions, "nonClaims": ["Unity Sprite import/atlas UV values", "Unity RectTransform values", "TMP line breaking and overflow rendering", "Canvas layout rebuild order", "GPU visual quality", "runtime input behavior"]}
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 2 if args.strict and issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
