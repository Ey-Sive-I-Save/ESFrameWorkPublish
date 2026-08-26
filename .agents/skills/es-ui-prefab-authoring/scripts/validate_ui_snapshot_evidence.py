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


def validate_snapshot_geometry_pair(editor: dict[str, Any], runtime: dict[str, Any], profile_id: str, state_id: str, expected: tuple[int, int] | None, issues: list[dict[str, Any]]) -> dict[str, Any]:
    evidence: dict[str, Any] = {"checkedElementCount": 0, "checkedStructuralElementCount": 0, "checkedInteractionTargetCount": 0, "status": "passed"}
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
        if isinstance(editor_root_path, str) and editor_root_path and validate_snapshot_structure_pair(source, target, path, editor_root_path, profile_id, state_id, issues):
            evidence["checkedStructuralElementCount"] += 1
        if expected is not None and validate_runtime_viewport_and_interaction(target, target_rect, path, profile_id, state_id, expected, issues):
            if target.get("active") is True and target.get("hasButton") is True and target.get("interactionTarget") is not None:
                evidence["checkedInteractionTargetCount"] += 1
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
