#!/usr/bin/env python3
"""Validate GPU PNG integrity against paired Unity semantic snapshots.

This is a deterministic pixel-integrity gate. It rejects an incomplete matrix,
identity drift, wrong dimensions, hash drift, transparent/uniform frames and
zero-edge frames. It does not judge composition, style, accessibility or player
usability, so a passing receipt is not visual acceptance.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import re
from typing import Any
import warnings

from PIL import Image, ImageDraw

from validate_ui_snapshot_evidence import declared_ids, load_json, sha256, validate_snapshot_evidence


CAPTURE_FIELDS = (
    "pngFileName", "pngSha256", "pngByteLength", "width", "height", "pixelCount",
    "nonTransparentPixelCount", "opaquePixelCount", "sampleStride", "sampledColorBucketCount",
    "edgeTransitionCount", "edgeComparisonCount", "rgbaMin", "rgbaMax",
)
MAX_PNG_PIXELS = 16_777_216
MIN_STATE_CHANGED_PIXELS = 16
MIN_STATE_CHANGED_RATIO = 0.00001
MIN_STATE_LOCALITY_RATIO = 0.80
STATE_LOCALITY_PADDING_PX = 4
VISUAL_EFFECT_FIELDS = frozenset({"visible", "graphicAlpha", "graphicColor", "outline", "wrapText", "text"})
EXECUTABLE_EFFECT_FIELDS = frozenset({"visible", "interactable", "graphicAlpha", "graphicColor", "wrapText", "text", "outline"})
COLOR_PATTERN = re.compile(r"^#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?$")


def color_bucket(pixel: tuple[int, int, int, int]) -> int:
    return ((pixel[0] >> 4) << 12) | ((pixel[1] >> 4) << 8) | ((pixel[2] >> 4) << 4) | (pixel[3] >> 4)


def load_rgba_image(path: Path) -> Image.Image:
    # The evidence gate may inspect externally supplied PNGs. Bound decompression and
    # keep only the current pixel row instead of materializing a full Python tuple list.
    with warnings.catch_warnings():
        warnings.simplefilter("error", Image.DecompressionBombWarning)
        with Image.open(path) as source:
            width, height = source.size
            pixel_count = width * height
            if pixel_count == 0:
                raise ValueError("decoded PNG has no pixels")
            if pixel_count > MAX_PNG_PIXELS:
                raise ValueError(f"decoded PNG exceeds pixel limit: {pixel_count} > {MAX_PNG_PIXELS}")
            image = source.convert("RGBA")
            image.load()
    return image


def collect_png_stats(path: Path) -> dict[str, Any]:
    image = load_rgba_image(path)
    try:
        width, height = image.size
        pixel_count = width * height
        stride = max(1, math.ceil(pixel_count / 16384))
        sampled: set[int] = set()
        non_transparent = 0
        opaque = 0
        edge_transitions = 0
        edge_comparisons = 0
        min_channels = [255, 255, 255, 255]
        max_channels = [0, 0, 0, 0]
        pixels = image.load()
        for y in range(height):
            for x in range(width):
                index = y * width + x
                pixel = pixels[x, y]
                non_transparent += int(pixel[3] > 0)
                opaque += int(pixel[3] == 255)
                for channel, value in enumerate(pixel):
                    min_channels[channel] = min(min_channels[channel], value)
                    max_channels[channel] = max(max_channels[channel], value)
                if index % stride == 0:
                    sampled.add(color_bucket(pixel))
                if x > 0:
                    edge_comparisons += 1
                    edge_transitions += int(pixel != pixels[x - 1, y])
                if y > 0:
                    edge_comparisons += 1
                    edge_transitions += int(pixel != pixels[x, y - 1])
        return {
            "pngFileName": path.name,
            "pngSha256": sha256(path),
            "pngByteLength": path.stat().st_size,
            "width": width,
            "height": height,
            "pixelCount": pixel_count,
            "nonTransparentPixelCount": non_transparent,
            "opaquePixelCount": opaque,
            "sampleStride": stride,
            "sampledColorBucketCount": len(sampled),
            "edgeTransitionCount": edge_transitions,
            "edgeComparisonCount": edge_comparisons,
            "rgbaMin": min_channels,
            "rgbaMax": max_channels,
        }
    finally:
        image.close()

def capture_from(snapshot: dict[str, Any], path: Path, issues: list[dict[str, Any]], profile_id: str, state_id: str) -> dict[str, Any] | None:
    capture = snapshot.get("capture")
    if not isinstance(capture, dict):
        issues.append({"code": "missing-capture-metadata", "path": path.as_posix(), "profileId": profile_id, "stateId": state_id})
        return None
    missing = [field for field in CAPTURE_FIELDS if field not in capture]
    if missing:
        issues.append({"code": "incomplete-capture-metadata", "path": path.as_posix(), "profileId": profile_id, "stateId": state_id, "fields": missing})
        return None
    return capture


def validate_capture_metadata(capture: dict[str, Any], actual: dict[str, Any], png_path: Path, profile_id: str, state_id: str, issues: list[dict[str, Any]]) -> None:
    for field in CAPTURE_FIELDS:
        if capture.get(field) != actual.get(field):
            issues.append({"code": "capture-metadata-mismatch", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id, "field": field, "declared": capture.get(field), "actual": actual.get(field)})
    if capture.get("pngFileName") != png_path.name:
        issues.append({"code": "capture-path-mismatch", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id})
    if actual["nonTransparentPixelCount"] == 0:
        issues.append({"code": "transparent-png", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id})
    if actual["sampledColorBucketCount"] < 2:
        issues.append({"code": "uniform-png", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id})
    if actual["edgeTransitionCount"] == 0:
        issues.append({"code": "zero-edge-png", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id})


def state_requires_visual_delta(spec: dict[str, Any], state_id: str) -> bool:
    if state_id == "default":
        return False
    semantics = spec.get("stateSemantics")
    state = semantics.get(state_id) if isinstance(semantics, dict) else None
    if not isinstance(state, dict):
        return False
    if isinstance(state.get("visualChanges"), list) and state["visualChanges"]:
        return True
    return any(
        isinstance(effect, dict)
        and isinstance(effect.get("changes"), dict)
        and VISUAL_EFFECT_FIELDS.intersection(effect["changes"])
        for effect in state.get("effects", [])
    )


def build_locality_mask(width: int, height: int, affected_rects: list[dict[str, Any]]) -> Image.Image:
    # ImageDraw unions the rectangles in native code. This keeps locality checking
    # O(pixelCount + affectedComponentCount), not O(pixelCount * componentCount).
    mask = Image.new("1", (width, height), 0)
    draw = ImageDraw.Draw(mask)
    for rect in affected_rects:
        draw.rectangle((rect["x"], rect["y"], rect["right"] - 1, rect["bottom"] - 1), fill=1)
    del draw
    return mask


def state_pixel_delta(baseline_path: Path, state_path: Path, affected_rects: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    baseline = load_rgba_image(baseline_path)
    state = load_rgba_image(state_path)
    locality_mask: Image.Image | None = None
    try:
        if baseline.size != state.size:
            raise ValueError(f"state PNG dimensions differ: {baseline.size} != {state.size}")
        pixel_count = baseline.width * baseline.height
        changed = 0
        changed_inside = 0
        baseline_pixels = baseline.load()
        state_pixels = state.load()
        locality_mask = build_locality_mask(baseline.width, baseline.height, affected_rects) if affected_rects is not None else None
        locality_pixels = locality_mask.load() if locality_mask is not None else None
        for y in range(baseline.height):
            for x in range(baseline.width):
                if baseline_pixels[x, y] == state_pixels[x, y]:
                    continue
                changed += 1
                if locality_pixels is not None and locality_pixels[x, y]:
                    changed_inside += 1
        result = {
            "pixelCount": pixel_count,
            "changedPixelCount": changed,
            "changedPixelRatio": changed / pixel_count,
            "minimumChangedPixels": max(MIN_STATE_CHANGED_PIXELS, math.ceil(pixel_count * MIN_STATE_CHANGED_RATIO)),
        }
        if affected_rects is not None:
            result.update({
                "affectedRects": affected_rects,
                "changedInsideAffectedPixelCount": changed_inside,
                "changedOutsideAffectedPixelCount": changed - changed_inside,
                "localityRatio": changed_inside / changed if changed else 0.0,
                "minimumLocalityRatio": MIN_STATE_LOCALITY_RATIO,
            })
        return result
    finally:
        if locality_mask is not None:
            locality_mask.close()
        baseline.close()
        state.close()


def state_affected_component_ids(spec: dict[str, Any], state_id: str) -> list[str]:
    semantics = spec.get("stateSemantics")
    state = semantics.get(state_id) if isinstance(semantics, dict) else None
    raw = state.get("affectedComponentIds") if isinstance(state, dict) else None
    return [component_id for component_id in raw if isinstance(component_id, str) and component_id] if isinstance(raw, list) else []


def normalize_rgba_hex(value: Any) -> str | None:
    if not isinstance(value, str) or not COLOR_PATTERN.fullmatch(value):
        return None
    normalized = value.lower()
    return normalized if len(normalized) == 9 else normalized + "ff"


def resolve_snapshot_component(elements: list[dict[str, Any]], component_id: str) -> tuple[dict[str, Any] | None, str | None]:
    matches = [element for element in elements if logical_id_from_path(element.get("path")) == component_id]
    if not matches:
        return None, "missing"
    if len(matches) != 1:
        return None, "ambiguous"
    return matches[0], None


def logical_id_from_path(path: Any) -> str:
    return path.rsplit("/", 1)[-1] if isinstance(path, str) else ""


def has_unique_target_relative_paths(trace: Any, component_id: str) -> bool:
    if not isinstance(trace, list) or not trace:
        return False
    paths = [item.get("path") for item in trace if isinstance(item, dict)]
    return (
        len(paths) == len(trace)
        and all(isinstance(path, str) and (path == component_id or path.startswith(component_id + "/")) for path in paths)
        and len(set(paths)) == len(paths)
    )


def validate_state_effect_snapshots(spec: dict[str, Any], evidence_root: Path, issues: list[dict[str, Any]]) -> list[dict[str, Any]]:
    results: list[dict[str, Any]] = []
    semantics = spec.get("stateSemantics")
    if not isinstance(semantics, dict):
        return results
    for profile in spec.get("profiles", []):
        if not isinstance(profile, dict) or not isinstance(profile.get("id"), str):
            continue
        profile_id = profile["id"]
        for state_id in declared_ids(spec.get("states")):
            state = semantics.get(state_id)
            effects = state.get("effects") if isinstance(state, dict) else None
            if not isinstance(effects, list) or not effects:
                continue
            result = {"profileId": profile_id, "stateId": state_id, "status": "passed", "effects": []}
            snapshot_path = evidence_root / f"{profile_id}__{state_id}.ui.json"
            snapshot = load_json(snapshot_path, issues, "invalid-ui-snapshot") if snapshot_path.is_file() else None
            if snapshot is None:
                result["status"] = "blocked"
                results.append(result)
                continue
            raw_elements = snapshot.get("uiElements")
            elements = [element for element in raw_elements if isinstance(element, dict)] if isinstance(raw_elements, list) else []
            for effect in effects:
                if not isinstance(effect, dict) or not isinstance(effect.get("componentId"), str) or not isinstance(effect.get("changes"), dict):
                    continue
                component_id = effect["componentId"]
                changes = {field: value for field, value in effect["changes"].items() if field in EXECUTABLE_EFFECT_FIELDS}
                evidence = {"componentId": component_id, "status": "passed", "fields": []}
                component, resolution = resolve_snapshot_component(elements, component_id)
                if component is None:
                    evidence["status"] = "blocked"
                    result["status"] = "blocked"
                    issues.append({"code": "state-effect-evidence-" + str(resolution), "profileId": profile_id, "stateId": state_id, "componentId": component_id})
                    result["effects"].append(evidence)
                    continue
                for field, expected in changes.items():
                    actual_field = field
                    required_capability = None
                    if field == "visible":
                        actual_field = "active"
                    elif field == "interactable":
                        required_capability = "hasButton"
                    elif field == "graphicAlpha":
                        actual_field = "descendantGraphicAlpha"
                        required_capability = "hasDescendantGraphic"
                    elif field in {"graphicColor", "outline"}:
                        required_capability = "hasGraphic"
                    elif field in {"wrapText", "text"}:
                        required_capability = "hasText"
                    actual = component.get(actual_field)
                    field_evidence = {"field": field, "expected": expected, "actual": actual, "status": "passed"}
                    matches = True
                    if required_capability is not None and component.get(required_capability) is not True:
                        matches = False
                        field_evidence["reason"] = "missing-" + required_capability
                    elif field == "graphicAlpha":
                        trace = component.get("descendantGraphicAlphas")
                        field_evidence["descendantGraphicAlphas"] = trace
                        matches = (
                            isinstance(actual, (int, float)) and not isinstance(actual, bool)
                            and isinstance(expected, (int, float)) and not isinstance(expected, bool)
                            and abs(float(actual) - float(expected)) <= (1.0 / 255.0)
                            and has_unique_target_relative_paths(trace, component_id)
                            and all(
                                isinstance(item, dict)
                                and isinstance(item.get("alpha"), (int, float))
                                and not isinstance(item.get("alpha"), bool)
                                and abs(float(item["alpha"]) - float(expected)) <= (1.0 / 255.0)
                                for item in trace
                            )
                        )
                    elif field == "graphicColor":
                        matches = normalize_rgba_hex(actual) == normalize_rgba_hex(expected) and normalize_rgba_hex(expected) is not None
                    elif field in {"wrapText", "text"}:
                        trace = component.get("descendantTextStates")
                        field_evidence["descendantTextStates"] = trace
                        matches = (
                            actual == expected
                            and has_unique_target_relative_paths(trace, component_id)
                            and all(isinstance(item, dict) and item.get(field) == expected for item in trace)
                        )
                    else:
                        matches = actual == expected
                    if not matches:
                        field_evidence["status"] = "blocked"
                        evidence["status"] = "blocked"
                        result["status"] = "blocked"
                        issues.append({"code": "state-effect-snapshot-mismatch", "profileId": profile_id, "stateId": state_id, "componentId": component_id, "field": field, "expected": expected, "actual": actual})
                    evidence["fields"].append(field_evidence)
                result["effects"].append(evidence)
            results.append(result)
    return results


def parse_screen_rect(raw: Any, width: int, height: int) -> dict[str, int] | None:
    if not isinstance(raw, dict):
        return None
    try:
        x = float(raw["x"])
        y = float(raw["y"])
        rect_width = float(raw["width"])
        rect_height = float(raw["height"])
    except (KeyError, TypeError, ValueError):
        return None
    if not all(math.isfinite(value) for value in (x, y, rect_width, rect_height)) or rect_width <= 0 or rect_height <= 0:
        return None
    left = max(0, math.floor(x) - STATE_LOCALITY_PADDING_PX)
    top = max(0, math.floor(y) - STATE_LOCALITY_PADDING_PX)
    right = min(width, math.ceil(x + rect_width) + STATE_LOCALITY_PADDING_PX)
    bottom = min(height, math.ceil(y + rect_height) + STATE_LOCALITY_PADDING_PX)
    if right <= left or bottom <= top:
        return None
    return {"x": left, "y": top, "right": right, "bottom": bottom}


def resolve_affected_rects(snapshot: dict[str, Any], component_ids: list[str], width: int, height: int) -> tuple[list[dict[str, Any]], list[str], list[str], list[str]]:
    raw_elements = snapshot.get("elements")
    elements = raw_elements if isinstance(raw_elements, list) else []
    by_id: dict[str, list[dict[str, Any]]] = {}
    for element in elements:
        if not isinstance(element, dict):
            continue
        path = element.get("path")
        if not isinstance(path, str) or not path:
            continue
        by_id.setdefault(path.rsplit("/", 1)[-1], []).append(element)

    rects: list[dict[str, Any]] = []
    missing: list[str] = []
    ambiguous: list[str] = []
    invalid: list[str] = []
    for component_id in component_ids:
        matches = by_id.get(component_id, [])
        active_matches = [element for element in matches if element.get("active") is True]
        if len(active_matches) == 1:
            element = active_matches[0]
        elif len(active_matches) > 1 or len(matches) > 1:
            ambiguous.append(component_id)
            continue
        elif len(matches) == 1:
            # A default-hidden component can legitimately become visible in this state.
            # Its baseline RectTransform still provides the profile-local locality region.
            element = matches[0]
        else:
            missing.append(component_id)
            continue
        rect = parse_screen_rect(element.get("screenRect"), width, height)
        if rect is None:
            invalid.append(component_id)
            continue
        rects.append({"componentId": component_id, "baselineActive": element.get("active") is True, **rect})
    return rects, missing, ambiguous, invalid


def validate_state_pixel_deltas(spec: dict[str, Any], evidence_root: Path, issues: list[dict[str, Any]]) -> list[dict[str, Any]]:
    states = declared_ids(spec.get("states"))
    if "default" not in states:
        return []
    results: list[dict[str, Any]] = []
    for profile in spec.get("profiles", []):
        if not isinstance(profile, dict) or not isinstance(profile.get("id"), str):
            continue
        profile_id = profile["id"]
        baseline_path = evidence_root / f"{profile_id}__default.png"
        baseline_editor_path = evidence_root / f"{profile_id}__default.editor.json"
        for state_id in states:
            if not state_requires_visual_delta(spec, state_id):
                continue
            state_path = evidence_root / f"{profile_id}__{state_id}.png"
            result = {"profileId": profile_id, "baselineStateId": "default", "stateId": state_id, "status": "passed"}
            if not baseline_path.is_file() or not state_path.is_file() or not baseline_editor_path.is_file():
                result["status"] = "blocked"
                results.append(result)
                continue
            try:
                baseline = load_json(baseline_editor_path, issues, "invalid-editor-snapshot")
                if baseline is None:
                    result["status"] = "blocked"
                    results.append(result)
                    continue
                baseline_image = load_rgba_image(baseline_path)
                try:
                    component_ids = state_affected_component_ids(spec, state_id)
                    affected_rects, missing_components, ambiguous_components, invalid_components = resolve_affected_rects(
                        baseline, component_ids, baseline_image.width, baseline_image.height)
                finally:
                    baseline_image.close()
                result["affectedComponentIds"] = component_ids
                if not component_ids or missing_components or ambiguous_components or invalid_components:
                    result["status"] = "blocked"
                    if not component_ids:
                        issues.append({"code": "state-locality-missing-affected-components", "profileId": profile_id, "stateId": state_id})
                    if missing_components:
                        issues.append({"code": "state-locality-missing-component", "profileId": profile_id, "stateId": state_id, "componentIds": missing_components})
                    if ambiguous_components:
                        issues.append({"code": "state-locality-ambiguous-component", "profileId": profile_id, "stateId": state_id, "componentIds": ambiguous_components})
                    if invalid_components:
                        issues.append({"code": "state-locality-invalid-screen-rect", "profileId": profile_id, "stateId": state_id, "componentIds": invalid_components})
                    results.append(result)
                    continue
                delta = state_pixel_delta(baseline_path, state_path, affected_rects)
            except (Image.DecompressionBombWarning, OSError, ValueError) as error:
                result["status"] = "blocked"
                result["message"] = str(error)
                issues.append({"code": "invalid-state-pixel-delta", "profileId": profile_id, "stateId": state_id, "message": str(error)})
                results.append(result)
                continue
            result.update(delta)
            if delta["changedPixelCount"] < delta["minimumChangedPixels"]:
                result["status"] = "blocked"
                issues.append({"code": "state-pixel-undifferentiated", "profileId": profile_id, "stateId": state_id, **delta})
            elif delta["localityRatio"] < MIN_STATE_LOCALITY_RATIO:
                result["status"] = "blocked"
                issues.append({"code": "state-pixel-outside-affected-components", "profileId": profile_id, "stateId": state_id, **delta})
            results.append(result)
    return results


def validate_gpu_evidence(spec_path: Path, evidence_root: Path) -> dict[str, Any]:
    structural = validate_snapshot_evidence(spec_path, evidence_root)
    issues = [dict(item) for item in structural.get("issues", [])]
    spec_path = spec_path.resolve()
    evidence_root = evidence_root.resolve()
    spec = load_json(spec_path, issues, "invalid-spec")
    if spec is None:
        return {"schemaVersion": 1, "validator": "es-ui-prefab-authoring/validate_ui_gpu_evidence", "status": "blocked", "issues": issues}

    pairs: list[dict[str, Any]] = []
    for profile in spec.get("profiles", []):
        if not isinstance(profile, dict) or not isinstance(profile.get("id"), str):
            continue
        profile_id = profile["id"]
        expected_width, expected_height = profile.get("width"), profile.get("height")
        for state_id in declared_ids(spec.get("states")):
            pair_issue_count = len(issues)
            prefix = f"{profile_id}__{state_id}"
            editor_path = evidence_root / f"{prefix}.editor.json"
            ui_path = evidence_root / f"{prefix}.ui.json"
            png_path = evidence_root / f"{prefix}.png"
            pair = {"profileId": profile_id, "stateId": state_id, "pngPath": png_path.as_posix(), "status": "passed", "pixelIntegrity": None}
            if not editor_path.is_file() or not ui_path.is_file() or not png_path.is_file():
                missing = [path.as_posix() for path in (editor_path, ui_path, png_path) if not path.is_file()]
                issues.append({"code": "missing-gpu-evidence", "profileId": profile_id, "stateId": state_id, "paths": missing})
                pair["status"] = "blocked"
                pairs.append(pair)
                continue
            editor = load_json(editor_path, issues, "invalid-editor-snapshot")
            runtime = load_json(ui_path, issues, "invalid-ui-snapshot")
            if editor is None or runtime is None:
                pair["status"] = "blocked"
                pairs.append(pair)
                continue
            editor_capture = capture_from(editor, editor_path, issues, profile_id, state_id)
            runtime_capture = capture_from(runtime, ui_path, issues, profile_id, state_id)
            try:
                actual = collect_png_stats(png_path)
            except (Image.DecompressionBombWarning, OSError, ValueError) as error:
                issues.append({"code": "invalid-png", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id, "message": str(error)})
                actual = None
            if editor_capture is not None and runtime_capture is not None:
                if json.dumps(editor_capture, sort_keys=True) != json.dumps(runtime_capture, sort_keys=True):
                    issues.append({"code": "capture-pair-mismatch", "profileId": profile_id, "stateId": state_id})
            if actual is not None:
                if expected_width != actual["width"] or expected_height != actual["height"]:
                    issues.append({"code": "png-dimension-mismatch", "path": png_path.as_posix(), "profileId": profile_id, "stateId": state_id, "expected": [expected_width, expected_height], "actual": [actual["width"], actual["height"]]})
                if editor_capture is not None:
                    validate_capture_metadata(editor_capture, actual, png_path, profile_id, state_id, issues)
                if runtime_capture is not None:
                    validate_capture_metadata(runtime_capture, actual, png_path, profile_id, state_id, issues)
                pair["pixelIntegrity"] = actual
            if len(issues) > pair_issue_count:
                pair["status"] = "blocked"
            pairs.append(pair)

    state_effects = validate_state_effect_snapshots(spec, evidence_root, issues)
    state_deltas = validate_state_pixel_deltas(spec, evidence_root, issues)
    return {
        "schemaVersion": 1,
        "validator": "es-ui-prefab-authoring/validate_ui_gpu_evidence",
        "validatorVersion": 1,
        "specPath": spec_path.as_posix(),
        "specSha256": sha256(spec_path),
        "evidenceRoot": evidence_root.as_posix(),
        "structuralStatus": structural.get("status"),
        "pixelIntegrityStatus": "passed" if not issues else "blocked",
        "capturePairs": pairs,
        "stateEffectEvidence": state_effects,
        "statePixelDeltas": state_deltas,
        "status": "passed" if not issues else "blocked",
        "issues": issues,
        "nonClaims": ["Unity process provenance", "human or AI composition review", "brand/style fidelity", "accessibility", "runtime input behavior", "commercial visual acceptance"],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path, help="ScreenSpec v3 used to capture the PNGs")
    parser.add_argument("--evidence-root", type=Path, required=True, help="directory containing matched snapshot and PNG evidence")
    parser.add_argument("--out", type=Path, required=True, help="receipt output path")
    parser.add_argument("--strict", action="store_true", help="return non-zero when any GPU evidence issue exists")
    args = parser.parse_args()
    receipt = validate_gpu_evidence(args.spec, args.evidence_root)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 2 if args.strict and receipt["issues"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
