#!/usr/bin/env python3
"""Validate the identity and focal-crop safety of Unity UI semantic snapshots.

This validates serialized snapshot payloads only. It deliberately does not accept
PNGs or claim that Unity, GPU rendering, or the source process is proven.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path, issues: list[dict[str, Any]], code: str) -> dict[str, Any] | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        issues.append({"code": code, "path": path.as_posix(), "message": str(error)})
        return None
    if not isinstance(value, dict):
        issues.append({"code": code, "path": path.as_posix(), "message": "snapshot root must be an object"})
        return None
    return value


def declared_ids(items: Any) -> list[str]:
    if not isinstance(items, list):
        return []
    return [str(item.get("id")) for item in items if isinstance(item, dict) and isinstance(item.get("id"), str) and item["id"]]


def declared_focal_ids(spec: dict[str, Any]) -> set[str]:
    advanced = ((spec.get("designContract") or {}).get("advancedComposition") or {})
    policies = advanced.get("focalAssetPolicies") if isinstance(advanced, dict) else None
    if not isinstance(policies, list):
        return set()
    return {str(policy.get("logicalId")) for policy in policies if isinstance(policy, dict) and isinstance(policy.get("logicalId"), str) and policy["logicalId"]}


def snapshot_elements(snapshot: dict[str, Any], kind: str) -> list[dict[str, Any]]:
    raw = snapshot.get("elements" if kind == "editor" else "uiElements")
    return [item for item in raw if isinstance(item, dict)] if isinstance(raw, list) else []


def logical_id(element: dict[str, Any]) -> str:
    path = element.get("path")
    return path.rsplit("/", 1)[-1] if isinstance(path, str) else ""


def expected_viewport(spec: dict[str, Any], profile_id: str) -> tuple[int, int] | None:
    profiles = spec.get("profiles")
    if not isinstance(profiles, list):
        return None
    for profile in profiles:
        if not isinstance(profile, dict) or profile.get("id") != profile_id:
            continue
        width, height = profile.get("width"), profile.get("height")
        if isinstance(width, int) and not isinstance(width, bool) and isinstance(height, int) and not isinstance(height, bool) and width > 0 and height > 0:
            return width, height
    return None


def finite_rect(value: Any, fields: tuple[str, str, str, str]) -> tuple[float, float, float, float] | None:
    if not isinstance(value, dict):
        return None
    result: list[float] = []
    for field in fields:
        raw = value.get(field)
        if isinstance(raw, bool) or not isinstance(raw, (int, float)) or not math.isfinite(float(raw)):
            return None
        result.append(float(raw))
    return tuple(result)  # type: ignore[return-value]


def finite_pair(value: Any) -> tuple[float, float] | None:
    if not isinstance(value, list) or len(value) != 2:
        return None
    result: list[float] = []
    for raw in value:
        if isinstance(raw, bool) or not isinstance(raw, (int, float)) or not math.isfinite(float(raw)):
            return None
        result.append(float(raw))
    return tuple(result)  # type: ignore[return-value]


def unique_paths(elements: list[dict[str, Any]]) -> tuple[dict[str, dict[str, Any]], list[str]]:
    result: dict[str, dict[str, Any]] = {}
    duplicates: list[str] = []
    for element in elements:
        path = element.get("path")
        if not isinstance(path, str) or not path:
            duplicates.append("<missing>")
        elif path in result:
            duplicates.append(path)
        else:
            result[path] = element
    return result, sorted(set(duplicates))


def valid_canvas_metadata(value: Any) -> bool:
    if not isinstance(value, dict) or not isinstance(value.get("renderMode"), str) or not value["renderMode"]:
        return False
    scaler = value.get("scaler")
    if not isinstance(scaler, dict) or not isinstance(scaler.get("uiScaleMode"), str) or not scaler["uiScaleMode"] or not isinstance(scaler.get("screenMatchMode"), str) or not scaler["screenMatchMode"]:
        return False
    resolution = scaler.get("referenceResolution")
    if not isinstance(resolution, list) or len(resolution) != 2 or any(isinstance(item, bool) or not isinstance(item, (int, float)) or not math.isfinite(float(item)) or item <= 0 for item in resolution):
        return False
    match = scaler.get("match")
    return not isinstance(match, bool) and isinstance(match, (int, float)) and math.isfinite(float(match)) and 0.0 <= float(match) <= 1.0


def validate_snapshot_structure_pair(source: dict[str, Any], target: dict[str, Any], path: str, root_path: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> bool:
    valid = True
    for field in ("parentPath",):
        source_value, target_value = source.get(field), target.get(field)
        if not isinstance(source_value, str) or not source_value or not isinstance(target_value, str) or not target_value:
            issues.append({"code": "snapshot-structure-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": field})
            valid = False
        elif source_value != target_value:
            issues.append({"code": "snapshot-structure-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "field": field, "editor": source_value, "ui": target_value})
            valid = False
    for field in ("siblingIndex",):
        source_value, target_value = source.get(field), target.get(field)
        if isinstance(source_value, bool) or not isinstance(source_value, int) or source_value < 0 or isinstance(target_value, bool) or not isinstance(target_value, int) or target_value < 0:
            issues.append({"code": "snapshot-structure-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": field})
            valid = False
        elif source_value != target_value:
            issues.append({"code": "snapshot-structure-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "field": field, "editor": source_value, "ui": target_value})
            valid = False
    for field in ("anchorMin", "anchorMax", "pivot"):
        source_value, target_value = finite_pair(source.get(field)), finite_pair(target.get(field))
        if source_value is None or target_value is None:
            issues.append({"code": "snapshot-structure-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": field})
            valid = False
        elif any(abs(left - right) > 0.0001 for left, right in zip(source_value, target_value)):
            issues.append({"code": "snapshot-structure-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "field": field, "editor": source_value, "ui": target_value})
            valid = False
    parent_path = source.get("parentPath")
    if isinstance(parent_path, str) and parent_path and path != root_path and parent_path != root_path and not parent_path.startswith(root_path + "/"):
        issues.append({"code": "snapshot-parent-path-root", "profileId": profile_id, "stateId": state_id, "path": path, "parentPath": parent_path, "rootPath": root_path})
        valid = False
    return valid


def validate_runtime_viewport_and_interaction(target: dict[str, Any], target_rect: tuple[float, float, float, float], path: str, profile_id: str, state_id: str, expected: tuple[int, int], issues: list[dict[str, Any]]) -> bool:
    valid = True
    x, y, width, height = target_rect
    if x < -0.01 or y < -0.01 or x + width > expected[0] + 0.01 or y + height > expected[1] + 0.01:
        issues.append({"code": "snapshot-runtime-viewport-containment", "profileId": profile_id, "stateId": state_id, "path": path, "rect": target_rect, "viewport": expected})
        valid = False
    has_button = target.get("hasButton")
    if not isinstance(has_button, bool):
        issues.append({"code": "snapshot-button-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "hasButton": has_button})
        return False
    if target.get("active") is True and has_button and target.get("interactionTarget") is not None:
        minimum = finite_pair(target.get("interactionTarget"))
        if minimum is None or minimum[0] <= 0 or minimum[1] <= 0:
            issues.append({"code": "snapshot-interaction-target-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "interactionTarget": target.get("interactionTarget")})
            valid = False
        elif width + 0.01 < minimum[0] or height + 0.01 < minimum[1]:
            issues.append({"code": "snapshot-interaction-target-size", "profileId": profile_id, "stateId": state_id, "path": path, "actual": [width, height], "minimum": minimum})
            valid = False
    return valid


def intersect_rects(left: tuple[float, float, float, float], right: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
    min_x, min_y = max(left[0], right[0]), max(left[1], right[1])
    max_x, max_y = min(left[0] + left[2], right[0] + right[2]), min(left[1] + left[3], right[1] + right[3])
    return min_x, min_y, max(0.0, max_x - min_x), max(0.0, max_y - min_y)


def rects_match(left: tuple[float, float, float, float], right: tuple[float, float, float, float], tolerance: float = 0.01) -> bool:
    return all(abs(source - target) <= tolerance for source, target in zip(left, right))


def validate_visibility_snapshot(element: dict[str, Any], target_rect: tuple[float, float, float, float], path: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> dict[str, Any] | None:
    if "visibility" not in element or not isinstance(element.get("visibility"), dict):
        issues.append({"code": "snapshot-visibility-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "visibility"})
        return None
    visibility = element["visibility"]
    clips = visibility.get("clipAncestors")
    visible_rect = finite_rect(visibility.get("visibleRect"), ("x", "y", "width", "height"))
    fraction = visibility.get("visibleFraction")
    has_non_rect_mask = visibility.get("hasNonRectMaskAncestor")
    if not isinstance(clips, list) or visible_rect is None or visible_rect[2] < 0 or visible_rect[3] < 0 or isinstance(fraction, bool) or not isinstance(fraction, (int, float)) or not math.isfinite(float(fraction)) or float(fraction) < 0.0 or float(fraction) > 1.0 or not isinstance(has_non_rect_mask, bool):
        issues.append({"code": "snapshot-visibility-invalid", "profileId": profile_id, "stateId": state_id, "path": path})
        return None
    expected_visible = target_rect
    active_non_rect_mask = False
    clip_paths: set[tuple[str, str]] = set()
    for index, clip in enumerate(clips):
        clip_path = f"visibility.clipAncestors[{index}]"
        if not isinstance(clip, dict) or not isinstance(clip.get("path"), str) or not clip["path"] or clip.get("type") not in {"Mask", "RectMask2D"} or not isinstance(clip.get("enabled"), bool):
            issues.append({"code": "snapshot-visibility-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": clip_path})
            continue
        key = (clip["path"], clip["type"])
        if key in clip_paths:
            issues.append({"code": "snapshot-visibility-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": clip_path, "message": "duplicate clipping ancestor"})
        clip_paths.add(key)
        clip_rect = finite_rect(clip.get("screenRect"), ("x", "y", "width", "height"))
        if clip_rect is None or clip_rect[2] < 0 or clip_rect[3] < 0:
            issues.append({"code": "snapshot-visibility-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": clip_path + ".screenRect"})
            continue
        if clip["type"] == "RectMask2D" and clip["enabled"] is True:
            expected_visible = intersect_rects(expected_visible, clip_rect)
        if clip["type"] == "Mask" and clip["enabled"] is True:
            active_non_rect_mask = True
    if active_non_rect_mask != has_non_rect_mask:
        issues.append({"code": "snapshot-visibility-mask-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "declared": has_non_rect_mask, "expected": active_non_rect_mask})
    if visible_rect[0] < target_rect[0] - 0.01 or visible_rect[1] < target_rect[1] - 0.01 or visible_rect[0] + visible_rect[2] > target_rect[0] + target_rect[2] + 0.01 or visible_rect[1] + visible_rect[3] > target_rect[1] + target_rect[3] + 0.01 or not rects_match(visible_rect, expected_visible):
        issues.append({"code": "snapshot-visible-rect-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "declared": visible_rect, "expected": expected_visible})
    target_area = max(0.0, target_rect[2]) * max(0.0, target_rect[3])
    expected_fraction = 0.0 if target_area <= 0.0001 else max(0.0, expected_visible[2]) * max(0.0, expected_visible[3]) / target_area
    if abs(float(fraction) - expected_fraction) > 0.0001:
        issues.append({"code": "snapshot-visible-fraction-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "declared": float(fraction), "expected": expected_fraction})
    return {"visibleRect": visible_rect, "visibleFraction": float(fraction), "hasNonRectMaskAncestor": has_non_rect_mask}


def validate_input_reachability_snapshot(element: dict[str, Any], visibility: dict[str, Any] | None, path: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> dict[str, Any] | None:
    reachability = element.get("inputReachability")
    if not isinstance(reachability, dict):
        issues.append({"code": "snapshot-input-reachability-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "inputReachability"})
        return None
    groups = reachability.get("canvasGroupChain")
    required_bools = ("inputAllowedByCanvasGroups", "visibleByCanvasGroups", "reachable")
    if not isinstance(groups, list) or any(not isinstance(reachability.get(field), bool) for field in required_bools) or (reachability.get("raycastBlocker") is not None and not isinstance(reachability.get("raycastBlocker"), dict)):
        issues.append({"code": "snapshot-input-reachability-invalid", "profileId": profile_id, "stateId": state_id, "path": path})
        return None
    input_allowed, visible_by_groups = True, True
    stop_ancestors = False
    for index, group in enumerate(groups):
        group_path = f"inputReachability.canvasGroupChain[{index}]"
        if not isinstance(group, dict) or not isinstance(group.get("path"), str) or not group["path"] or any(not isinstance(group.get(field), bool) for field in ("enabled", "interactable", "blocksRaycasts", "ignoreParentGroups")):
            issues.append({"code": "snapshot-input-reachability-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": group_path})
            continue
        alpha = group.get("alpha")
        if isinstance(alpha, bool) or not isinstance(alpha, (int, float)) or not math.isfinite(float(alpha)) or float(alpha) < 0.0 or float(alpha) > 1.0:
            issues.append({"code": "snapshot-input-reachability-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": group_path + ".alpha"})
            continue
        if stop_ancestors:
            issues.append({"code": "snapshot-input-reachability-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": group_path, "message": "CanvasGroup chain continues after ignoreParentGroups"})
        if group["enabled"]:
            input_allowed &= group["interactable"] and group["blocksRaycasts"]
            visible_by_groups &= float(alpha) > 0.001
            stop_ancestors = group["ignoreParentGroups"]
    blocker = reachability.get("raycastBlocker")
    if isinstance(blocker, dict):
        blocker_rect = finite_rect(blocker.get("screenRect"), ("x", "y", "width", "height"))
        if not isinstance(blocker.get("path"), str) or not blocker["path"] or isinstance(blocker.get("siblingIndex"), bool) or not isinstance(blocker.get("siblingIndex"), int) or blocker["siblingIndex"] < 0 or blocker.get("reason") != "same-parent-opaque-graphic" or blocker_rect is None or blocker_rect[2] < 0 or blocker_rect[3] < 0:
            issues.append({"code": "snapshot-input-reachability-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "inputReachability.raycastBlocker"})
    expected_reachable = input_allowed and visible_by_groups and visibility is not None and visibility["visibleFraction"] > 0.001 and blocker is None
    if reachability["inputAllowedByCanvasGroups"] != input_allowed or reachability["visibleByCanvasGroups"] != visible_by_groups or reachability["reachable"] != expected_reachable:
        issues.append({"code": "snapshot-input-reachability-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "declared": {field: reachability[field] for field in required_bools}, "expected": {"inputAllowedByCanvasGroups": input_allowed, "visibleByCanvasGroups": visible_by_groups, "reachable": expected_reachable}})
    return reachability


def validate_interactive_visibility(target: dict[str, Any], visibility: dict[str, Any] | None, reachability: dict[str, Any] | None, path: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> bool:
    if target.get("active") is not True or target.get("hasButton") is not True or target.get("interactable") is not True or target.get("interactionTarget") is None:
        return True
    minimum = finite_pair(target.get("interactionTarget"))
    if minimum is None or visibility is None or reachability is None:
        return False
    visible_rect = visibility["visibleRect"]
    valid = True
    if visible_rect[2] + 0.01 < minimum[0] or visible_rect[3] + 0.01 < minimum[1]:
        issues.append({"code": "snapshot-interaction-visible-size", "profileId": profile_id, "stateId": state_id, "path": path, "actualVisible": [visible_rect[2], visible_rect[3]], "minimum": minimum})
        valid = False
    if visibility["hasNonRectMaskAncestor"]:
        issues.append({"code": "snapshot-interaction-nonrect-mask-unproven", "profileId": profile_id, "stateId": state_id, "path": path, "message": "an interactive target under Mask needs runtime raycast evidence"})
        valid = False
    if reachability.get("raycastBlocker") is not None:
        issues.append({"code": "snapshot-interaction-raycast-blocked", "profileId": profile_id, "stateId": state_id, "path": path, "blocker": reachability.get("raycastBlocker")})
        valid = False
    if reachability.get("reachable") is not True:
        issues.append({"code": "snapshot-interaction-unreachable", "profileId": profile_id, "stateId": state_id, "path": path})
        valid = False
    return valid


def validate_snapshot_tree_integrity(elements_by_path: dict[str, dict[str, Any]], root_path: str, kind: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> None:
    sibling_paths: dict[tuple[str, int], list[str]] = {}
    for path, element in elements_by_path.items():
        parent_path, sibling_index = element.get("parentPath"), element.get("siblingIndex")
        if not isinstance(parent_path, str) or not parent_path or isinstance(sibling_index, bool) or not isinstance(sibling_index, int) or sibling_index < 0:
            continue
        if path != root_path and parent_path != root_path and parent_path not in elements_by_path:
            issues.append({"code": "snapshot-parent-path-missing", "profileId": profile_id, "stateId": state_id, "snapshot": kind, "path": path, "parentPath": parent_path})
        sibling_paths.setdefault((parent_path, sibling_index), []).append(path)
    for (parent_path, sibling_index), paths in sorted(sibling_paths.items()):
        if len(paths) > 1:
            issues.append({"code": "snapshot-sibling-index-duplicate", "profileId": profile_id, "stateId": state_id, "snapshot": kind, "parentPath": parent_path, "siblingIndex": sibling_index, "paths": sorted(paths)})


def layout_axis_control(value: Any, path: str, field: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> tuple[bool, bool] | None:
    if not isinstance(value, dict) or not isinstance(value.get("x"), bool) or not isinstance(value.get("y"), bool):
        issues.append({"code": "snapshot-layout-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": field})
        return None
    return value["x"], value["y"]


def validate_layout_snapshot(element: dict[str, Any], path: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> tuple[bool, bool] | None:
    """Return the axes actively sized by this node's ContentSizeFitter."""
    if "layout" not in element:
        issues.append({"code": "snapshot-layout-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "layout", "message": "controller evidence is required for every snapshot element"})
        return None
    layout = element.get("layout")
    if layout is None:
        return False, False
    if not isinstance(layout, dict) or set(("layoutGroup", "contentSizeFitter")) - set(layout):
        issues.append({"code": "snapshot-layout-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "layout"})
        return None
    group = layout.get("layoutGroup")
    if group is not None:
        if not isinstance(group, dict) or not isinstance(group.get("type"), str) or not group["type"] or not isinstance(group.get("enabled"), bool):
            issues.append({"code": "snapshot-layout-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "layout.layoutGroup"})
        else:
            child_axes = layout_axis_control(group.get("childAxisControl"), path, "layout.layoutGroup.childAxisControl", profile_id, state_id, issues)
            if child_axes is not None and not group["enabled"] and any(child_axes):
                issues.append({"code": "snapshot-layout-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "layout.layoutGroup.childAxisControl", "message": "disabled LayoutGroup cannot control a child axis"})
    fitter = layout.get("contentSizeFitter")
    if fitter is None:
        return False, False
    if not isinstance(fitter, dict) or not isinstance(fitter.get("enabled"), bool) or fitter.get("horizontalFit") not in {"Unconstrained", "MinSize", "PreferredSize"} or fitter.get("verticalFit") not in {"Unconstrained", "MinSize", "PreferredSize"}:
        issues.append({"code": "snapshot-layout-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "field": "layout.contentSizeFitter"})
        return None
    self_axes = layout_axis_control(fitter.get("selfAxisControl"), path, "layout.contentSizeFitter.selfAxisControl", profile_id, state_id, issues)
    if self_axes is None:
        return None
    expected_axes = (
        fitter["enabled"] and fitter["horizontalFit"] != "Unconstrained",
        fitter["enabled"] and fitter["verticalFit"] != "Unconstrained",
    )
    if self_axes != expected_axes:
        issues.append({"code": "snapshot-layout-fitter-axis-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "declared": self_axes, "expected": expected_axes})
    return self_axes


def validate_snapshot_layout_ownership(elements_by_path: dict[str, dict[str, Any]], kind: str, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> int:
    """Reject two active UGUI layout controllers for the same child RectTransform axis."""
    fitter_axes: dict[str, tuple[bool, bool]] = {}
    for path, element in elements_by_path.items():
        axes = validate_layout_snapshot(element, path, profile_id, state_id, issues)
        if axes is not None:
            fitter_axes[path] = axes
    checked = 0
    for path, (fitter_x, fitter_y) in fitter_axes.items():
        parent_path = elements_by_path[path].get("parentPath")
        parent = elements_by_path.get(parent_path) if isinstance(parent_path, str) else None
        parent_layout = parent.get("layout") if isinstance(parent, dict) else None
        group = parent_layout.get("layoutGroup") if isinstance(parent_layout, dict) else None
        if not isinstance(group, dict) or group.get("enabled") is not True:
            continue
        child_axes = layout_axis_control(group.get("childAxisControl"), parent_path, "layout.layoutGroup.childAxisControl", profile_id, state_id, issues)
        if child_axes is None:
            continue
        checked += 1
        for axis, parent_controls, fitter_controls in (("x", child_axes[0], fitter_x), ("y", child_axes[1], fitter_y)):
            if parent_controls and fitter_controls:
                issues.append({"code": "snapshot-layout-axis-conflict", "profileId": profile_id, "stateId": state_id, "snapshot": kind, "path": path, "parentPath": parent_path, "axis": axis, "controllers": ["parent-layout-group", "self-content-size-fitter"]})
    return checked


def validate_snapshot_geometry_pair(editor: dict[str, Any], runtime: dict[str, Any], profile_id: str, state_id: str, expected: tuple[int, int] | None, issues: list[dict[str, Any]]) -> dict[str, Any]:
    evidence: dict[str, Any] = {"checkedElementCount": 0, "checkedStructuralElementCount": 0, "checkedInteractionTargetCount": 0, "checkedVisibleInteractionTargetCount": 0, "checkedLayoutOwnershipCount": 0, "checkedVisibilityCount": 0, "checkedInputReachabilityCount": 0, "status": "passed"}
    issue_count_before_geometry = len(issues)
    if expected is None:
        issues.append({"code": "snapshot-profile-viewport", "profileId": profile_id, "stateId": state_id, "message": "profile must provide positive integer width and height"})
        evidence["status"] = "blocked"
        return evidence
    expected_payload = {"width": expected[0], "height": expected[1], "orientation": "landscape" if expected[0] >= expected[1] else "portrait"}
    for snapshot, kind in ((editor, "editor"), (runtime, "ui")):
        if snapshot.get("viewport") != expected_payload:
            issues.append({"code": "snapshot-viewport-mismatch", "profileId": profile_id, "stateId": state_id, "snapshot": kind, "expected": expected_payload, "actual": snapshot.get("viewport")})
        if kind == "ui" and (snapshot.get("screenWidth") != expected[0] or snapshot.get("screenHeight") != expected[1]):
            issues.append({"code": "snapshot-screen-dimensions", "profileId": profile_id, "stateId": state_id, "expected": [expected[0], expected[1]], "actual": [snapshot.get("screenWidth"), snapshot.get("screenHeight")]})
    editor_root_path, runtime_root_path = editor.get("rootPath"), runtime.get("rootPath")
    if not isinstance(editor_root_path, str) or not editor_root_path or not isinstance(runtime_root_path, str) or not runtime_root_path:
        issues.append({"code": "snapshot-canvas-metadata", "profileId": profile_id, "stateId": state_id, "message": "editor and UI snapshots must declare non-empty rootPath values"})
    if not valid_canvas_metadata(editor.get("canvas")) or not valid_canvas_metadata(runtime.get("canvas")):
        issues.append({"code": "snapshot-canvas-metadata", "profileId": profile_id, "stateId": state_id, "message": "editor and UI snapshots must declare Canvas render mode and complete CanvasScaler metadata"})
    if editor_root_path != runtime_root_path or editor.get("canvas") != runtime.get("canvas"):
        issues.append({"code": "snapshot-canvas-mismatch", "profileId": profile_id, "stateId": state_id, "editorRootPath": editor.get("rootPath"), "uiRootPath": runtime.get("rootPath")})
    editor_by_path, editor_duplicates = unique_paths(snapshot_elements(editor, "editor"))
    runtime_by_path, runtime_duplicates = unique_paths(snapshot_elements(runtime, "ui"))
    if editor_duplicates or runtime_duplicates:
        issues.append({"code": "snapshot-element-path-duplicate", "profileId": profile_id, "stateId": state_id, "editor": editor_duplicates, "ui": runtime_duplicates})
    editor_paths, runtime_paths = set(editor_by_path), set(runtime_by_path)
    if editor_paths != runtime_paths:
        issues.append({"code": "snapshot-element-path-set", "profileId": profile_id, "stateId": state_id, "missingFromUi": sorted(editor_paths - runtime_paths), "missingFromEditor": sorted(runtime_paths - editor_paths)})
    if isinstance(editor_root_path, str) and editor_root_path:
        validate_snapshot_tree_integrity(editor_by_path, editor_root_path, "editor", profile_id, state_id, issues)
    if isinstance(runtime_root_path, str) and runtime_root_path:
        validate_snapshot_tree_integrity(runtime_by_path, runtime_root_path, "ui", profile_id, state_id, issues)
    evidence["checkedLayoutOwnershipCount"] = validate_snapshot_layout_ownership(runtime_by_path, "ui", profile_id, state_id, issues)
    for path in sorted(editor_paths & runtime_paths):
        source, target = editor_by_path[path], runtime_by_path[path]
        if not isinstance(editor_root_path, str) or not editor_root_path or path != editor_root_path and not path.startswith(editor_root_path + "/"):
            issues.append({"code": "snapshot-element-path-root", "profileId": profile_id, "stateId": state_id, "path": path, "rootPath": editor_root_path})
        source_rect = finite_rect(source.get("screenRect"), ("x", "y", "width", "height"))
        target_rect = finite_rect(target, ("screenX", "screenY", "screenWidth", "screenHeight"))
        if source_rect is None or target_rect is None:
            issues.append({"code": "snapshot-geometry-invalid", "profileId": profile_id, "stateId": state_id, "path": path})
            continue
        if any(value < 0 for value in (source_rect[2], source_rect[3], target_rect[2], target_rect[3])) or any(abs(left - right) > 0.01 for left, right in zip(source_rect, target_rect)):
            issues.append({"code": "snapshot-geometry-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source_rect, "ui": target_rect})
        if not isinstance(source.get("active"), bool) or not isinstance(target.get("active"), bool):
            issues.append({"code": "snapshot-active-invalid", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source.get("active"), "ui": target.get("active")})
        elif source.get("active") != target.get("active"):
            issues.append({"code": "snapshot-active-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source.get("active"), "ui": target.get("active")})
        if source.get("interactionTarget") != target.get("interactionTarget"):
            issues.append({"code": "snapshot-interaction-target-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source.get("interactionTarget"), "ui": target.get("interactionTarget")})
        if source.get("layout") != target.get("layout"):
            issues.append({"code": "snapshot-layout-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source.get("layout"), "ui": target.get("layout")})
        validate_layout_snapshot(source, path, profile_id, state_id, issues)
        if source.get("visibility") != target.get("visibility"):
            issues.append({"code": "snapshot-visibility-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source.get("visibility"), "ui": target.get("visibility")})
        source_visibility = validate_visibility_snapshot(source, source_rect, path, profile_id, state_id, issues)
        target_visibility = validate_visibility_snapshot(target, target_rect, path, profile_id, state_id, issues)
        if source_visibility is not None and target_visibility is not None:
            evidence["checkedVisibilityCount"] += 1
        if source.get("inputReachability") != target.get("inputReachability"):
            issues.append({"code": "snapshot-input-reachability-mismatch", "profileId": profile_id, "stateId": state_id, "path": path, "editor": source.get("inputReachability"), "ui": target.get("inputReachability")})
        validate_input_reachability_snapshot(source, source_visibility, path, profile_id, state_id, issues)
        target_reachability = validate_input_reachability_snapshot(target, target_visibility, path, profile_id, state_id, issues)
        if target_reachability is not None:
            evidence["checkedInputReachabilityCount"] += 1
        if isinstance(editor_root_path, str) and editor_root_path and validate_snapshot_structure_pair(source, target, path, editor_root_path, profile_id, state_id, issues):
            evidence["checkedStructuralElementCount"] += 1
        if expected is not None and validate_runtime_viewport_and_interaction(target, target_rect, path, profile_id, state_id, expected, issues):
            if target.get("active") is True and target.get("hasButton") is True and target.get("interactionTarget") is not None:
                evidence["checkedInteractionTargetCount"] += 1
        if validate_interactive_visibility(target, target_visibility, target_reachability, path, profile_id, state_id, issues):
            if target.get("active") is True and target.get("hasButton") is True and target.get("interactable") is True and target.get("interactionTarget") is not None:
                evidence["checkedVisibleInteractionTargetCount"] += 1
        evidence["checkedElementCount"] += 1
    if len(issues) > issue_count_before_geometry:
        evidence["status"] = "blocked"
    return evidence


def validate_focal_crop(elements: list[dict[str, Any]], required_ids: set[str], snapshot_path: Path, issues: list[dict[str, Any]]) -> list[dict[str, Any]]:
    seen: set[str] = set()
    evidence: list[dict[str, Any]] = []
    for element in elements:
        crop = element.get("focalCrop")
        if crop is None:
            continue
        component_id = logical_id(element)
        if not isinstance(crop, dict):
            issues.append({"code": "invalid-focal-crop", "path": snapshot_path.as_posix(), "componentId": component_id, "message": "focalCrop must be an object or null"})
            continue
        seen.add(component_id)
        satisfied = crop.get("safeCropSatisfied")
        evidence.append({"componentId": component_id, "safeCropSatisfied": satisfied})
        if satisfied is not True:
            issues.append({"code": "focal-crop-unsafe", "path": snapshot_path.as_posix(), "componentId": component_id, "message": "serialized focal crop does not preserve its declared safe region"})
    for component_id in sorted(required_ids - seen):
        issues.append({"code": "missing-focal-crop", "path": snapshot_path.as_posix(), "componentId": component_id, "message": "declared focal-cover component has no serialized focalCrop evidence"})
    return evidence


def validate_snapshot_evidence(spec_path: Path, evidence_root: Path) -> dict[str, Any]:
    issues: list[dict[str, Any]] = []
    spec_path = spec_path.resolve()
    evidence_root = evidence_root.resolve()
    spec = load_json(spec_path, issues, "invalid-spec")
    if spec is None:
        return {"schemaVersion": 1, "validator": "es-ui-prefab-authoring/validate_ui_snapshot_evidence", "status": "blocked", "issues": issues}

    expected_hash = sha256(spec_path)
    profiles = declared_ids(spec.get("profiles"))
    states = declared_ids(spec.get("states"))
    if not profiles:
        issues.append({"code": "missing-profile-matrix", "path": spec_path.as_posix(), "message": "ScreenSpec must declare at least one profile for snapshot evidence"})
    if not states:
        issues.append({"code": "missing-state-matrix", "path": spec_path.as_posix(), "message": "ScreenSpec must declare at least one state for snapshot evidence"})
    required_focal_ids = declared_focal_ids(spec)
    pairs: list[dict[str, Any]] = []
    for profile_id in profiles:
        for state_id in states:
            issue_count_before_pair = len(issues)
            prefix = f"{profile_id}__{state_id}"
            editor_path = evidence_root / f"{prefix}.editor.json"
            ui_path = evidence_root / f"{prefix}.ui.json"
            pair = {"profileId": profile_id, "stateId": state_id, "editorPath": editor_path.as_posix(), "uiPath": ui_path.as_posix(), "status": "passed", "focalCrop": [], "geometry": {}}
            if not editor_path.is_file() or not ui_path.is_file():
                missing = [path.as_posix() for path in (editor_path, ui_path) if not path.is_file()]
                issues.append({"code": "missing-snapshot", "profileId": profile_id, "stateId": state_id, "paths": missing, "message": "each declared profile/state needs paired editor and UI snapshots"})
                pair["status"] = "blocked"
                pairs.append(pair)
                continue
            editor = load_json(editor_path, issues, "invalid-editor-snapshot")
            runtime = load_json(ui_path, issues, "invalid-ui-snapshot")
            if editor is None or runtime is None:
                pair["status"] = "blocked"
                pairs.append(pair)
                continue
            for snapshot, command, kind, path in ((editor, "editor.snapshot", "editor", editor_path), (runtime, "ui.snapshot", "ui", ui_path)):
                for field, expected in (("command", command), ("profileId", profile_id), ("stateId", state_id), ("specHash", expected_hash)):
                    if snapshot.get(field) != expected:
                        issues.append({"code": "snapshot-identity", "path": path.as_posix(), "field": field, "expected": expected, "actual": snapshot.get(field)})
                elements = snapshot_elements(snapshot, kind)
                if not elements:
                    issues.append({"code": "empty-snapshot-elements", "path": path.as_posix(), "message": "semantic snapshot must contain at least one UI element"})
                pair["focalCrop"].extend(validate_focal_crop(elements, required_focal_ids, path, issues))
            for field in ("panelId", "profileId", "stateId", "runId", "specHash", "sceneGeneration"):
                if editor.get(field) != runtime.get(field):
                    issues.append({"code": "snapshot-pair-mismatch", "profileId": profile_id, "stateId": state_id, "field": field, "editor": editor.get(field), "ui": runtime.get(field)})
            capture_key = f"{editor.get('panelId')}.{profile_id}.{state_id}"
            if editor.get("captureKey") != capture_key:
                issues.append({"code": "snapshot-capture-key", "path": editor_path.as_posix(), "expected": capture_key, "actual": editor.get("captureKey")})
            pair["geometry"] = validate_snapshot_geometry_pair(editor, runtime, profile_id, state_id, expected_viewport(spec, profile_id), issues)
            if len(issues) > issue_count_before_pair:
                pair["status"] = "blocked"
            pairs.append(pair)

    return {
        "schemaVersion": 1,
        "validator": "es-ui-prefab-authoring/validate_ui_snapshot_evidence",
        "validatorVersion": 1,
        "specPath": spec_path.as_posix(),
        "specSha256": expected_hash,
        "evidenceRoot": evidence_root.as_posix(),
        "requiredPairs": len(profiles) * len(states),
        "snapshotPairs": pairs,
        "status": "passed" if not issues else "blocked",
        "issues": issues,
        "nonClaims": ["Unity process execution", "Prefab or Fixture Scene persistence", "GPU PNG pixels", "rendered visual quality", "runtime input behavior"],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path, help="ScreenSpec v3 used to create the snapshots")
    parser.add_argument("--evidence-root", type=Path, required=True, help="directory containing <profile>__<state>.editor/ui.json")
    parser.add_argument("--out", type=Path, required=True, help="receipt output path")
    parser.add_argument("--strict", action="store_true", help="return non-zero when any identity or crop issue exists")
    args = parser.parse_args()
    receipt = validate_snapshot_evidence(args.spec, args.evidence_root)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 2 if args.strict and receipt["issues"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
