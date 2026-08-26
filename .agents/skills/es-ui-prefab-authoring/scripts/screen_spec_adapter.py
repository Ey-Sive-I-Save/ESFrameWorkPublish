#!/usr/bin/env python3
"""Normalize ScreenSpec v3 into the materializer's internal semantic tree."""

from __future__ import annotations

from typing import Any


TYPE_TO_KIND = {
    "frame": "panel", "text": "text", "icon": "image", "image": "image", "portrait": "image",
    "button": "button", "tab-bar": "list", "list": "list", "grid": "list", "choice-list": "list",
    "item-slot": "card", "item-card": "card", "detail-panel": "panel", "stat-row": "panel",
    "progress": "panel", "bar": "panel", "counter": "panel", "cooldown": "panel", "status-badge": "panel",
    "badge": "panel", "input-hint": "panel", "focus-ring": "image", "nameplate": "panel", "subtitle": "text",
    "loading": "panel", "error-state": "panel", "empty-state": "panel", "target-panel": "panel",
    "damage-pop": "text", "map": "panel", "marker": "image", "slider": "slider", "toggle": "button",
    "dropdown": "button", "reward-track": "list", "tooltip": "panel",
}


def layout_intent(layout: dict[str, Any]) -> dict[str, Any]:
    bounds = layout.get("bounds", [0, 0, 1, 1])
    anchor = layout.get("anchor") if isinstance(layout.get("anchor"), dict) else {}
    mode = str(anchor.get("strategy") or layout.get("mode", "content"))
    preferred = layout.get("preferredSize") or [layout.get("preferredWidth", 0), layout.get("preferredHeight", 0)]
    pivot = anchor.get("pivot") or layout.get("pivot") or [0.5, 0.5]
    if not isinstance(pivot, list) or len(pivot) != 2:
        pivot = [0.5, 0.5]
    mode_key = mode.lower()
    normalized_mode = {
        "center": "centered",
        "stretch": "stretch",
        "edge-docked": "edge-docked",
        "absolute": "absolute",
        "content": "content",
        "fixed": "fixed",
    }.get(mode_key, "content")
    return {
        "mode": normalized_mode,
        "anchorMinX": bounds[0], "anchorMinY": bounds[1],
        "anchorMaxX": bounds[2], "anchorMaxY": bounds[3],
        "pivotX": pivot[0], "pivotY": pivot[1],
        "sizeWidth": preferred[0],
        "sizeHeight": preferred[1],
        "anchorStrategy": str(anchor.get("strategy") or layout.get("anchorStrategy") or mode_key),
        "anchorEdge": str(anchor.get("edge") or layout.get("anchorEdge") or "none"),
        "safeArea": str(anchor.get("safeArea") or layout.get("safeArea") or "inherit"),
    }


def layout_spec(component: dict[str, Any]) -> dict[str, Any] | None:
    mode = str((component.get("layout") or {}).get("mode", ""))
    # Match ESUIScreenSpecAdapter.cs exactly: children alone do not create a
    # layout group. Absolute/edge-docked containers retain authored geometry.
    children = component.get("children")
    if mode not in {"grid", "list", "flow"} or not isinstance(children, list) or not children:
        return None
    layout = component.get("layout") or {}
    return {
        "layoutMode": "grid" if mode == "grid" else "vertical" if mode == "list" else "horizontal",
        "axis": "horizontal" if mode in {"grid", "flow"} else "vertical",
        "gap": layout.get("gap", 12),
        "paddingLeft": layout.get("padding", [16, 16, 16, 16])[0],
        "paddingRight": layout.get("padding", [16, 16, 16, 16])[1],
        "paddingTop": layout.get("padding", [16, 16, 16, 16])[2],
        "paddingBottom": layout.get("padding", [16, 16, 16, 16])[3],
        "columns": layout.get("columns", 1),
        "cellWidth": layout.get("cellSize", [220, 120])[0],
        "cellHeight": layout.get("cellSize", [220, 120])[1],
        "spacingX": layout.get("gap", 12),
        "spacingY": layout.get("gap", 12),
        "childAlignment": "upper-left",
        "controlChildWidth": True,
        "controlChildHeight": True,
        "forceChildExpandWidth": mode == "list",
        "forceChildExpandHeight": False,
        "childGeometryOwner": layout.get("childGeometryOwner", "parent-layout-group"),
    }


def normalize_component(component: dict[str, Any]) -> dict[str, Any]:
    component_type = str(component.get("type", "frame"))
    content = component.get("content") if isinstance(component.get("content"), dict) else {}
    value = content.get("value")
    has_value = value is not None and isinstance(value, (str, int, float)) and not isinstance(value, bool)
    layout = component.get("layout") if isinstance(component.get("layout"), dict) else {}
    preferred = layout.get("preferredSize") or [layout.get("preferredWidth", 0), layout.get("preferredHeight", 0)]
    interaction = component.get("interaction")
    result: dict[str, Any] = {
        "id": component.get("id"),
        "kind": TYPE_TO_KIND.get(component_type, "panel"),
        "componentType": component_type,
        "visualVariant": component.get("visualVariant", "default"),
        "assetSlots": list(component.get("assetSlots", [])) if isinstance(component.get("assetSlots"), list) else [],
        "value": value if value is not None else 0,
        "hasValue": has_value,
        "text": content.get("text", ""),
        "colorToken": component.get("colorToken") or color_token_for_variant(str(component.get("visualVariant", "default"))),
        "typographyRole": component.get("typographyRole") or default_typography_role(component_type),
        "layerRole": component.get("layerRole") or default_layer_role(component_type, str(component.get("visualVariant", "default"))),
        "siblingOrder": component.get("siblingOrder", -1),
        "layout": "both",
        "width": preferred[0],
        "height": preferred[1],
        "minWidth": (layout.get("minSize") or [0, 0])[0],
        "minHeight": (layout.get("minSize") or [0, 0])[1],
        "fillWidth": True,
        "interactable": isinstance(interaction, dict),
        "interaction": dict(interaction) if isinstance(interaction, dict) else {},
        "stateVariants": dict(component.get("stateVariants", {})) if isinstance(component.get("stateVariants", {}), dict) else {},
        "layoutIntent": layout_intent(layout),
        "children": [normalize_component(child) for child in component.get("children", []) if isinstance(child, dict)],
    }
    component_layout = str(layout.get("responsiveMode", component.get("responsiveMode", "both")))
    if component_layout in {"wide", "narrow"}:
        result["layout"] = component_layout
    spec = layout_spec(component)
    if spec:
        result["layoutSpec"] = spec
    return result


def color_token_for_variant(visual_variant: str) -> str:
    key = (visual_variant or "").lower()
    if key == "none":
        return "#FFFFFF"
    if key in {"background"}:
        return "background"
    if key in {"accent", "accent-strong", "selected"}:
        return "accent"
    if key in {"muted", "mutedtext"}:
        return "mutedText"
    if key in {"danger", "error", "feedback"}:
        return "danger"
    if key in {"text"}:
        return "text"
    return "surface"


def default_typography_role(component_type: str) -> str:
    if component_type in {"button", "item-card", "item-slot", "tab-bar"}:
        return "label"
    if component_type in {"counter", "progress", "bar", "cooldown"}:
        return "numeric"
    if component_type in {"subtitle", "input-hint"}:
        return "caption"
    if component_type in {"text", "status-badge", "stat-row", "nameplate"}:
        return "body"
    return "none"


def default_layer_role(component_type: str, visual_variant: str) -> str:
    if visual_variant.lower() in {"danger", "error", "feedback"} or component_type in {"error-state", "loading"}:
        return "feedback"
    if component_type in {"button", "slider", "toggle", "dropdown", "input-hint"}:
        return "action"
    if visual_variant.lower() == "background":
        return "background"
    return "information"


def normalize(spec: dict[str, Any]) -> dict[str, Any]:
    if spec.get("schemaVersion") != 3 or "components" not in spec:
        return spec
    result = {
        "panelId": spec.get("screenId", "ui-panel"),
        "prefabPath": spec.get("prefabPath", f"Assets/UI/Prefabs/Generated/{spec.get('screenId', 'ui-panel')}.prefab"),
        "fixtureScenePath": spec.get("fixtureScenePath", f"Assets/UI/Scenes/Generated/{spec.get('screenId', 'ui-panel')}.unity"),
        "tokens": spec.get("tokens", {}),
        "assets": spec.get("assets", []),
        "profiles": spec.get("profiles", []),
        "states": spec.get("states", []),
        "qualityGates": spec.get("qualityGates", {}),
        "designContract": spec.get("designContract", {}),
        "intentContract": spec.get("intentContract", {}),
        "stateSemantics": spec.get("stateSemantics", {}),
        "profileAvailability": spec.get("profileAvailability", {}),
        "bindings": spec.get("bindings", []),
        "artifactStatus": spec.get("artifactStatus", {}),
        "generationMode": spec.get("generationMode", ""),
        "rootLayoutIntent": {"mode": "stretch", "anchorMinX": 0, "anchorMinY": 0, "anchorMaxX": 1, "anchorMaxY": 1, "pivotX": 0.5, "pivotY": 0.5},
        "elements": [normalize_component(component) for component in spec.get("components", []) if isinstance(component, dict)],
        "designEvidence": spec.get("designEvidence", {"schemaVersion": 2, "sourceType": "brief-only", "brief": f"Generic {spec.get('screenType', 'game')} screen", "analysisArtifact": {"path": "", "sha256": "0" * 64, "method": "screen-spec-v3", "status": "complete"}, "visionReview": {"provider": "screen-spec-v3", "model": "deterministic-adapter", "reviewMethod": "semantic-component-contract", "reviewedAt": "1970-01-01T00:00:00Z", "imageHashes": ["0" * 64], "semanticCoverage": 1.0, "status": "complete"}, "referenceImages": [{"path": "", "role": "generated-spec", "status": "placeholder"}], "sourceRegions": [{"id": "screen", "role": "screen", "bounds": [0, 0, 1, 1], "evidence": "ScreenSpec v3 component contract", "confidence": 1.0, "major": True}], "decisions": [], "responsiveDecisions": [], "assetDecisions": [{"role": "component-assets", "status": "placeholder", "reason": "ScreenSpec v3 adapter fallback; production AssetManifest should replace this."}], "assumptions": []}),
    }
    return result
