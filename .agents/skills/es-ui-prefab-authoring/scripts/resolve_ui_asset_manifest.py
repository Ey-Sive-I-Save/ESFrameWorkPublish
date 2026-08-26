#!/usr/bin/env python3
"""Resolve ScreenSpec asset slots to project-local Unity assets.

The resolver is deliberately read-only with respect to the ScreenSpec. It emits a
manifest receipt that records the selected file, GUID, dimensions, hash and
provenance decision. A caller must explicitly copy the receipt into the next
ScreenSpec revision; the resolver never promotes a placeholder by mutation.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any


ASSET_SOURCES = {"project-sprite", "ai-generated", "generated-procedural", "generated-placeholder"}
IMAGE_EXTENSIONS = {".asset", ".png", ".jpg", ".jpeg", ".tga", ".psd", ".svg"}
VERSION_RE = re.compile(r"-v(?P<version>\d+)(?=\.[^.]+$)", re.IGNORECASE)
GUID_RE = re.compile(r"^guid:\s*(?P<guid>[0-9a-f]{32})\s*$", re.IGNORECASE | re.MULTILINE)
WIDTH_RE = re.compile(r"^\s*m_Width:\s*(?P<value>\d+)\s*$", re.MULTILINE)
HEIGHT_RE = re.compile(r"^\s*m_Height:\s*(?P<value>\d+)\s*$", re.MULTILINE)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_guid(meta_path: Path) -> str:
    if not meta_path.is_file():
        return ""
    match = GUID_RE.search(meta_path.read_text(encoding="utf-8", errors="strict"))
    return match.group("guid").lower() if match else ""


def read_dimensions(path: Path) -> list[int] | None:
    if path.suffix.lower() != ".asset":
        return None
    text = path.read_text(encoding="utf-8", errors="strict")
    width = WIDTH_RE.search(text)
    height = HEIGHT_RE.search(text)
    if not width or not height:
        return None
    return [int(width.group("value")), int(height.group("value"))]


def project_file(project_root: Path, relative: str) -> Path | None:
    if not isinstance(relative, str) or not relative.strip():
        return None
    candidate = (project_root / relative.replace("\\", "/")).resolve()
    try:
        candidate.relative_to(project_root.resolve())
    except ValueError:
        return None
    return candidate


def find_generated_candidate(project_root: Path, asset_id: str) -> Path | None:
    root = project_root / "Assets" / "UI" / "Generated" / "AIUI"
    if not root.is_dir():
        return None
    candidates: list[tuple[int, str, Path]] = []
    for path in root.iterdir():
        if not path.is_file() or path.suffix.lower() not in IMAGE_EXTENSIONS or path.name.endswith(".meta"):
            continue
        stem = path.stem.lower()
        if asset_id.lower() not in stem:
            continue
        version_match = VERSION_RE.search(path.name)
        version = int(version_match.group("version")) if version_match else 0
        candidates.append((version, path.name.lower(), path))
    if not candidates:
        return None
    return sorted(candidates, key=lambda item: (item[0], item[1]), reverse=True)[0][2]


def relative_unix(project_root: Path, path: Path) -> str:
    return path.resolve().relative_to(project_root.resolve()).as_posix()


def resolve_asset(project_root: Path, asset: dict[str, Any], index: int) -> tuple[dict[str, Any], list[dict[str, str]]]:
    issues: list[dict[str, str]] = []
    asset_id = str(asset.get("id", "")).strip()
    path_value = asset.get("path", "")
    resolved = project_file(project_root, path_value)
    resolution = "declared"
    if resolved is None or not resolved.is_file():
        candidate = find_generated_candidate(project_root, asset_id)
        if candidate is not None:
            resolved = candidate
            resolution = "auto-discovered-generated-candidate"
    record: dict[str, Any] = {
        "index": index,
        "assetId": asset_id,
        "role": asset.get("role", ""),
        "source": asset.get("source", ""),
        "declaredPath": path_value,
        "resolvedPath": relative_unix(project_root, resolved) if resolved and resolved.is_file() else "",
        "resolution": resolution,
        "fallback": asset.get("fallback", ""),
        "declaredHash": str(asset.get("hash", "")).lower(),
        "actualHash": "",
        "guid": "",
        "dimensions": None,
        "status": "unresolved",
        "commercialAcceptance": "deferred",
    }
    if not asset_id:
        issues.append({"code": "asset-id", "path": f"assets[{index}].id", "message": "asset id is required"})
    if asset.get("source") not in ASSET_SOURCES:
        issues.append({"code": "asset-source", "path": f"assets[{index}].source", "message": "unsupported asset source"})
    if not asset.get("fallback"):
        issues.append({"code": "asset-fallback", "path": f"assets[{index}].fallback", "message": "fallback is required"})
    if resolved is None or not resolved.is_file():
        issues.append({"code": "asset-missing", "path": f"assets[{index}].path", "message": "asset path does not resolve inside the project"})
        return record, issues
    record["actualHash"] = sha256(resolved)
    record["guid"] = read_guid(resolved.with_suffix(resolved.suffix + ".meta"))
    record["dimensions"] = read_dimensions(resolved)
    if record["declaredHash"] and record["declaredHash"] != record["actualHash"]:
        issues.append({"code": "asset-hash", "path": f"assets[{index}].hash", "message": "declared hash does not match resolved file"})
    if not record["guid"]:
        issues.append({"code": "asset-guid", "path": f"assets[{index}]", "message": "Unity .meta GUID is missing or invalid"})
    required = ("provenance", "license", "importPolicy", "aspectPolicy", "cropPolicy", "nineSlice", "atlasOwner", "resolutionSet")
    if asset.get("source") in {"project-sprite", "ai-generated"}:
        for field in required:
            value = asset.get(field)
            if value is None or value == "" or value == []:
                issues.append({"code": "asset-provenance", "path": f"assets[{index}].{field}", "message": "resolved asset requires provenance/import metadata"})
    record["status"] = "verified" if not any(issue["path"].startswith(f"assets[{index}]") for issue in issues) else "resolved-with-issues"
    record["commercialAcceptance"] = "deferred" if asset.get("source") in {"generated-procedural", "generated-placeholder"} else "candidate"
    return record, issues


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path, help="ScreenSpec v3 JSON path")
    parser.add_argument("--project-root", type=Path, default=None)
    parser.add_argument("--out", type=Path, required=True, help="manifest receipt JSON path")
    parser.add_argument("--require-resolved", action="store_true", help="fail when any asset is unresolved or has an issue")
    args = parser.parse_args()
    spec_path = args.spec.resolve()
    project_root = (args.project_root or next((parent for parent in [spec_path, *spec_path.parents] if (parent / ".agents").is_dir() and (parent / "Assets").is_dir()), None))
    if project_root is None:
        print("cannot discover project root; pass --project-root", file=sys.stderr)
        return 2
    project_root = project_root.resolve()
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    assets_value = spec.get("assets", [])
    assets = list(assets_value.values()) if isinstance(assets_value, dict) else assets_value
    if not isinstance(assets, list):
        print("ScreenSpec assets must be an array or object", file=sys.stderr)
        return 2
    records: list[dict[str, Any]] = []
    issues: list[dict[str, str]] = []
    for index, asset in enumerate(assets):
        if not isinstance(asset, dict):
            issues.append({"code": "asset-type", "path": f"assets[{index}]", "message": "asset must be an object"})
            continue
        record, asset_issues = resolve_asset(project_root, asset, index)
        records.append(record)
        issues.extend(asset_issues)
    receipt = {
        "schemaVersion": 1,
        "resolver": "es-ui-prefab-authoring/resolve_ui_asset_manifest",
        "resolverVersion": 1,
        "projectRoot": project_root.as_posix(),
        "specPath": relative_unix(project_root, spec_path),
        "specSha256": sha256(spec_path),
        "status": "passed" if not issues else "blocked",
        "assetCount": len(records),
        "verifiedCount": sum(record["status"] == "verified" for record in records),
        "commercialAcceptance": "deferred" if any(record["commercialAcceptance"] == "deferred" for record in records) else "candidate",
        "assets": records,
        "issues": issues,
        "nonClaims": ["Unity import success", "GPU visual quality", "commercial license acceptance", "runtime input behavior"],
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 2 if args.require_resolved and issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
