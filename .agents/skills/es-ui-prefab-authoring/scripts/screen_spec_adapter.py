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
    "damage-pop": "text", "map": "panel", "marker": "image", "slider": "panel", "toggle": "button",
    "dropdown": "button", "reward-track": "list", "tooltip": "panel",
}


def layout_intent(layout: dict[str, Any]) -> dict[str, Any]:
    bounds = layout.get("bounds", [0, 0, 1, 1])
    mode = str(layout.get("mode", "content"))
    return {
        "mode": "centered" if mode == "center" else mode,
        "anchorMinX": bounds[0], "anchorMinY": bounds[1],
        "anchorMaxX": bounds[2], "anchorMaxY": bounds[3],
        "pivotX": 0.5, "pivotY": 0.5,
        "sizeWidth": (layout.get("preferredSize") or [0, 0])[0],
        "sizeHeight": (layout.get("preferredSize") or [0, 0])[1],
    }


def layout_spec(component: dict[str, Any]) -> dict[str, Any] | None:
    mode = str((component.get("layout") or {}).get("mode", ""))
    if mode not in {"grid", "list", "flow"} and not component.get("children"):
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
    }


def normalize_component(component: dict[str, Any]) -> dict[str, Any]:
    component_type = str(component.get("type", "frame"))
    content = component.get("content") if isinstance(component.get("content"), dict) else {}
    component_type = str(component.get("type", "frame"))
    has_value = isinstance(content.get("value"), (int, float)) and not isinstance(content.get("value"), bool)
    result: dict[str, Any] = {
        "id": component.get("id"),
        "kind": TYPE_TO_KIND.get(component_type, "panel"),
        "componentType": component_type,
        "visualVariant": component.get("visualVariant", "default"),
        "assetSlots": list(component.get("assetSlots", [])) if isinstance(component.get("assetSlots"), list) else [],
        "value": content.get("value", 0),
        "hasValue": has_value,
        "text": content.get("text", ""),
        "colorToken": component.get("visualVariant", "surface"),
        "layout": "both",
        "width": ((component.get("layout") or {}).get("preferredSize") or [0, 0])[0],
        "height": ((component.get("layout") or {}).get("preferredSize") or [0, 0])[1],
        "minWidth": ((component.get("layout") or {}).get("minSize") or [0, 0])[0],
        "minHeight": ((component.get("layout") or {}).get("minSize") or [0, 0])[1],
        "fillWidth": True,
        "interactable": bool(component.get("interaction")),
        "layoutIntent": layout_intent(component.get("layout") or {}),
        "children": [normalize_component(child) for child in component.get("children", []) if isinstance(child, dict)],
    }
    component_layout = str((component.get("layout") or {}).get("responsiveMode", "both"))
    if component_layout in {"wide", "narrow"}:
        result["layout"] = component_layout
    spec = layout_spec(component)
    if spec:
        result["layoutSpec"] = spec
    return result


def normalize(spec: dict[str, Any]) -> dict[str, Any]:
    if spec.get("schemaVersion") != 3 or "components" not in spec:
        return spec
    result = {
        "panelId": spec.get("screenId", "ui-panel"),
        "prefabPath": spec.get("prefabPath", f"Assets/UI/Prefabs/Generated/{spec.get('screenId', 'ui-panel')}.prefab"),
        "fixtureScenePath": spec.get("fixtureScenePath", f"Assets/UI/Scenes/Generated/{spec.get('screenId', 'ui-panel')}.unity"),
        "tokens": spec.get("tokens", {}),
        "rootLayoutIntent": {"mode": "stretch", "anchorMinX": 0, "anchorMinY": 0, "anchorMaxX": 1, "anchorMaxY": 1, "pivotX": 0.5, "pivotY": 0.5},
        "elements": [normalize_component(component) for component in spec.get("components", []) if isinstance(component, dict)],
        "designEvidence": spec.get("designEvidence", {"schemaVersion": 2, "sourceType": "brief-only", "brief": f"Generic {spec.get('screenType', 'game')} screen", "analysisArtifact": {"path": "", "sha256": "0" * 64, "method": "screen-spec-v3", "status": "complete"}, "visionReview": {"provider": "screen-spec-v3", "model": "deterministic-adapter", "reviewMethod": "semantic-component-contract", "reviewedAt": "1970-01-01T00:00:00Z", "imageHashes": ["0" * 64], "semanticCoverage": 1.0, "status": "complete"}, "referenceImages": [{"path": "", "role": "generated-spec", "status": "placeholder"}], "sourceRegions": [{"id": "screen", "role": "screen", "bounds": [0, 0, 1, 1], "evidence": "ScreenSpec v3 component contract", "confidence": 1.0, "major": True}], "decisions": [], "responsiveDecisions": [], "assetDecisions": [{"role": "component-assets", "status": "placeholder", "reason": "ScreenSpec v3 adapter fallback; production AssetManifest should replace this."}], "assumptions": []}),
    }
    return result
