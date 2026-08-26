#!/usr/bin/env python3
"""Extract deterministic candidate geometry from a UI reference image.

This is an input measurement tool, not a semantic vision model. It records the
source hash, pixel dimensions, visible-content bounds and conservative connected
regions. Region labels and anchor hints are candidates with confidence; they never
grant Unity materialization authority and must be reviewed before entering a
ScreenSpec. Keeping this step deterministic makes AI revisions traceable.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import deque
from pathlib import Path
from statistics import median
from typing import Any

from PIL import Image


TOOL_VERSION = 1
DEFAULT_MAX_SIDE = 256


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def rgb_distance(left: tuple[int, int, int], right: tuple[int, int, int]) -> float:
    return math.sqrt(sum((float(a) - float(b)) ** 2 for a, b in zip(left, right)))


def border_background(image: Image.Image) -> tuple[int, int, int]:
    pixels = image.convert("RGB")
    width, height = pixels.size
    samples: list[tuple[int, int, int]] = []
    stride_x = max(1, width // 32)
    stride_y = max(1, height // 32)
    for x in range(0, width, stride_x):
        samples.extend((pixels.getpixel((x, 0)), pixels.getpixel((x, height - 1))))
    for y in range(0, height, stride_y):
        samples.extend((pixels.getpixel((0, y)), pixels.getpixel((width - 1, y))))
    return tuple(int(round(median(channel))) for channel in zip(*samples))  # type: ignore[arg-type]


def downsample_mask(image: Image.Image, max_side: int, background: tuple[int, int, int]) -> tuple[list[list[bool]], tuple[int, int]]:
    source = image.convert("RGBA")
    width, height = source.size
    scale = min(1.0, float(max_side) / max(width, height))
    sample_width = max(1, int(round(width * scale)))
    sample_height = max(1, int(round(height * scale)))
    sample = source.resize((sample_width, sample_height), Image.Resampling.BILINEAR)
    mask: list[list[bool]] = []
    for y in range(sample_height):
        row: list[bool] = []
        for x in range(sample_width):
            red, green, blue, alpha = sample.getpixel((x, y))
            visible = alpha >= 16 and rgb_distance((red, green, blue), background) >= 18.0
            row.append(visible)
        mask.append(row)
    return mask, (sample_width, sample_height)


def connected_regions(mask: list[list[bool]], min_area: int = 8) -> list[tuple[int, int, int, int, int]]:
    if not mask or not mask[0]:
        return []
    height, width = len(mask), len(mask[0])
    seen = [[False] * width for _ in range(height)]
    regions: list[tuple[int, int, int, int, int]] = []
    for start_y in range(height):
        for start_x in range(width):
            if seen[start_y][start_x] or not mask[start_y][start_x]:
                continue
            queue: deque[tuple[int, int]] = deque([(start_x, start_y)])
            seen[start_y][start_x] = True
            min_x = max_x = start_x
            min_y = max_y = start_y
            area = 0
            while queue:
                x, y = queue.popleft()
                area += 1
                min_x, max_x = min(min_x, x), max(max_x, x)
                min_y, max_y = min(min_y, y), max(max_y, y)
                for next_x, next_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= next_x < width and 0 <= next_y < height and not seen[next_y][next_x] and mask[next_y][next_x]:
                        seen[next_y][next_x] = True
                        queue.append((next_x, next_y))
            if area >= min_area:
                regions.append((min_x, min_y, max_x + 1, max_y + 1, area))
    return sorted(regions, key=lambda region: (region[1], region[0], -region[4]))


def region_role(bounds: tuple[int, int, int, int], image_size: tuple[int, int], area_ratio: float) -> tuple[str, str]:
    x0, y0, x1, y1 = bounds
    width, height = image_size
    region_width, region_height = x1 - x0, y1 - y0
    aspect = region_width / max(1, region_height)
    touches_edge = x0 <= 1 or y0 <= 1 or x1 >= width - 1 or y1 >= height - 1
    if area_ratio >= 0.42 and touches_edge:
        return "background-candidate", "low"
    if aspect >= 4.0 or aspect <= 0.25:
        return "strip-or-divider-candidate", "low"
    if area_ratio >= 0.12:
        return "panel-candidate", "medium"
    if max(region_width, region_height) <= min(width, height) * 0.28:
        return "icon-or-control-candidate", "low"
    return "content-region-candidate", "low"


def anchor_hint(bounds: tuple[int, int, int, int], image_size: tuple[int, int]) -> tuple[str, str]:
    x0, y0, x1, y1 = bounds
    width, height = image_size
    margin = 0.035
    left, right = x0 / width, x1 / width
    top, bottom = y0 / height, y1 / height
    if left <= margin and right >= 1.0 - margin:
        return "stretch-x", "candidate-from-edge-contact"
    if top <= margin and bottom >= 1.0 - margin:
        return "stretch-y", "candidate-from-edge-contact"
    if abs((left + right) * 0.5 - 0.5) <= 0.06 and abs((top + bottom) * 0.5 - 0.5) <= 0.06:
        return "centered", "candidate-from-image-center"
    if left <= margin:
        return "left-docked", "candidate-from-edge-contact"
    if right >= 1.0 - margin:
        return "right-docked", "candidate-from-edge-contact"
    if top <= margin:
        return "top-docked", "candidate-from-edge-contact"
    if bottom >= 1.0 - margin:
        return "bottom-docked", "candidate-from-edge-contact"
    return "content-relative", "candidate-only"


def analyze(path: Path, max_side: int) -> dict[str, Any]:
    source_hash = sha256(path)
    with Image.open(path) as opened:
        image = opened.convert("RGBA")
        width, height = image.size
        background = border_background(image)
        mask, sampled_size = downsample_mask(image, max_side, background)
        regions = connected_regions(mask, min_area=max(8, int(sampled_size[0] * sampled_size[1] * 0.00025)))

    scale_x, scale_y = width / sampled_size[0], height / sampled_size[1]
    candidates: list[dict[str, Any]] = []
    for index, (x0, y0, x1, y1, area) in enumerate(regions[:128]):
        pixel_bounds = [round(x0 * scale_x), round(y0 * scale_y), round(x1 * scale_x), round(y1 * scale_y)]
        normalized = [
            clamp01(pixel_bounds[0] / width), clamp01(pixel_bounds[1] / height),
            clamp01(pixel_bounds[2] / width), clamp01(pixel_bounds[3] / height),
        ]
        area_ratio = (pixel_bounds[2] - pixel_bounds[0]) * (pixel_bounds[3] - pixel_bounds[1]) / (width * height)
        role, confidence = region_role(tuple(pixel_bounds), (width, height), area_ratio)
        anchor, anchor_reason = anchor_hint(tuple(pixel_bounds), (width, height))
        candidates.append({
            "id": f"reference-region-{index + 1:03d}",
            "role": role,
            "pixelBounds": pixel_bounds,
            "normalizedBounds": normalized,
            "areaRatio": round(area_ratio, 6),
            "componentPixelCount": area,
            "confidence": confidence,
            "observations": ["connected non-background pixel region"],
            "assumptions": ["semantic type, parent and interaction are not recoverable from pixels alone"],
            "anchorHint": anchor,
            "anchorHintReason": anchor_reason,
            "reviewRequired": True,
        })
    return {
        "schemaVersion": 1,
        "analyzer": "es-ui-prefab-authoring/ingest_ui_reference",
        "analyzerVersion": TOOL_VERSION,
        "source": {"path": path.as_posix(), "sha256": source_hash, "width": width, "height": height, "mode": "RGBA"},
        "method": {"background": "border-median", "maskThresholdRgbDistance": 18.0, "maxAnalysisSide": max_side, "semanticModel": "none"},
        "backgroundEstimate": {"rgb": list(background), "hex": "#%02X%02X%02X" % background},
        "regions": candidates,
        "regionCount": len(candidates),
        "status": "candidate" if candidates else "blocked",
        "confidencePolicy": "candidate geometry only; semantic labels, anchors, parent ownership, OCR and responsive behavior require review or an explicit design IR",
        "nonClaims": ["OCR/text identity", "Unity RectTransform or Canvas ownership", "responsive layout", "interaction behavior", "visual acceptance", "commercial asset provenance"],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path, help="PNG/JPEG UI reference image")
    parser.add_argument("--out", type=Path, required=True, help="JSON evidence path")
    parser.add_argument("--max-analysis-side", type=int, default=DEFAULT_MAX_SIDE)
    args = parser.parse_args()
    path = args.reference.resolve()
    if not path.is_file():
        parser.error(f"reference does not exist: {path}")
    if args.max_analysis_side < 32:
        parser.error("--max-analysis-side must be at least 32")
    receipt = analyze(path, args.max_analysis_side)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 0 if receipt["status"] == "candidate" else 2


if __name__ == "__main__":
    raise SystemExit(main())
