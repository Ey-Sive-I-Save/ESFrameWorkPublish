#!/usr/bin/env python3
"""Validate TMP Font Asset identity and deterministic glyph/fixture coverage."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any


UNICODE_PATTERN = re.compile(r"^\s*m_Unicode:\s*(\d+)\s*$", re.MULTILINE)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def flatten_components(nodes: Any) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    if not isinstance(nodes, list):
        return result
    for node in nodes:
        if not isinstance(node, dict):
            continue
        result.append(node)
        result.extend(flatten_components(node.get("children")))
    return result


def collect_strings(value: Any, path: str = "fixture") -> list[tuple[str, str]]:
    result: list[tuple[str, str]] = []
    if isinstance(value, str) and value:
        result.append((path, value))
    elif isinstance(value, dict):
        for key, child in value.items():
            result.extend(collect_strings(child, f"{path}.{key}"))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            result.extend(collect_strings(child, f"{path}[{index}]"))
    return result


def collect_rendered_fixture_texts(spec: dict[str, Any]) -> list[tuple[str, str]]:
    """Collect text that can actually reach a rendered component, not IDs or prose metadata."""
    result: list[tuple[str, str]] = []
    for index, component in enumerate(flatten_components(spec.get("components"))):
        content = component.get("content") if isinstance(component.get("content"), dict) else {}
        text = content.get("text")
        if isinstance(text, str) and text:
            result.append((f"components[{index}].content.text", text))
    state_semantics = spec.get("stateSemantics") if isinstance(spec.get("stateSemantics"), dict) else {}
    for state_id, state in state_semantics.items():
        if not isinstance(state, dict):
            continue
        fixture_data = state.get("fixtureData") if isinstance(state.get("fixtureData"), dict) else {}
        bindings = state.get("fixtureTextBindings") if isinstance(state.get("fixtureTextBindings"), list) else []
        for index, binding in enumerate(bindings):
            if not isinstance(binding, dict):
                continue
            key = binding.get("fixtureDataKey")
            component_id = binding.get("componentId")
            value = fixture_data.get(key) if isinstance(key, str) else None
            if isinstance(value, str) and value:
                result.append((f"stateSemantics.{state_id}.fixtureTextBindings[{index}]({component_id}).fixtureData.{key}", value))
    return result


def project_relative(root: Path, raw: Any) -> tuple[Path | None, str | None]:
    if not isinstance(raw, str) or not raw.strip():
        return None, "path is missing"
    candidate = (root / raw.replace("/", "\\")).resolve()
    try:
        candidate.relative_to(root.resolve())
    except ValueError:
        return None, "font path escapes project root"
    return candidate, None


def read_font_asset(root: Path, record: dict[str, Any], label: str) -> tuple[str, set[int], dict[str, Any]]:
    """Resolve one fallback asset and return its serialized text and glyph table."""
    path, path_issue = project_relative(root, record.get("path"))
    result: dict[str, Any] = {
        "id": record.get("id", ""),
        "path": record.get("path", ""),
        "hash": "",
        "unicodeCount": 0,
        "status": "blocked",
        "issues": [],
    }
    if path_issue:
        result["issues"].append({"code": "font-path", "message": f"{label}: {path_issue}"})
        return "", set(), result
    if path is None or not path.is_file():
        result["issues"].append({"code": "font-missing", "message": f"{label}: fallback TMP Font Asset does not exist"})
        return "", set(), result
    text = path.read_text(encoding="utf-8")
    actual_hash = sha256(path)
    result["hash"] = actual_hash
    declared_hash = str(record.get("hash", ""))
    if not declared_hash or actual_hash.lower() != declared_hash.lower():
        result["issues"].append({"code": "font-hash", "message": f"{label}: fallback hash does not match file", "declared": declared_hash, "actual": actual_hash})
    if "m_CharacterTable:" not in text:
        result["issues"].append({"code": "font-format", "message": f"{label}: file does not look like a serialized TMP Font Asset"})
    unicode_values = {int(match) for match in UNICODE_PATTERN.findall(text)}
    result["unicodeCount"] = len(unicode_values)
    if result["issues"] == []:
        result["status"] = "verified"
    return text, unicode_values, result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path)
    parser.add_argument("--project-root", type=Path, default=None)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--strict", action="store_true", help="return non-zero when any issue exists")
    args = parser.parse_args()

    spec_path = args.spec.resolve()
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    if args.project_root:
        root = args.project_root.resolve()
    else:
        # Generated packets may live several levels below Assets/. Walk upward
        # until the Unity project markers are found instead of assuming a
        # fixed directory depth.
        root = None
        for candidate in (spec_path.parent, *spec_path.parents):
            if (candidate / "Assets").is_dir() and (candidate / "ProjectSettings").is_dir():
                root = candidate.resolve()
                break
        if root is None:
            root = spec_path.parents[3].resolve()
    policy = ((spec.get("qualityGates") or {}).get("typographyPolicy") or {})
    issues: list[dict[str, Any]] = []
    font_path, path_issue = project_relative(root, policy.get("fontAssetPath"))
    if path_issue:
        issues.append({"code": "font-path", "message": path_issue})
    if font_path is None or not font_path.is_file():
        issues.append({"code": "font-missing", "path": str(policy.get("fontAssetPath", "")), "message": "declared TMP Font Asset does not exist"})
        font_text = ""
        actual_hash = ""
    else:
        font_text = font_path.read_text(encoding="utf-8")
        actual_hash = sha256(font_path)
        declared_hash = str(policy.get("fontAssetHash", ""))
        if not declared_hash:
            issues.append({"code": "font-hash", "message": "fontAssetHash is required"})
        elif actual_hash.lower() != declared_hash.lower():
            issues.append({"code": "font-hash", "message": "declared fontAssetHash does not match file", "declared": declared_hash, "actual": actual_hash})

    unicode_values = {int(match) for match in UNICODE_PATTERN.findall(font_text)}
    required = [str(value) for value in policy.get("requiredCharacters", []) if isinstance(value, str)]
    fixtures = collect_rendered_fixture_texts(spec)
    required_by_source: dict[str, str] = {f"requiredCharacters[{index}]": value for index, value in enumerate(required)}
    missing_required: list[dict[str, Any]] = []
    for source, text in required_by_source.items():
        missing = sorted({character for character in text if not character.isspace() and ord(character) not in unicode_values})
        if missing:
            missing_required.append({"source": source, "text": text, "missing": missing})
    missing_fixture: list[dict[str, Any]] = []
    for source, text in fixtures:
        missing = sorted({character for character in text if not character.isspace() and ord(character) not in unicode_values})
        if missing:
            missing_fixture.append({"source": source, "text": text, "missing": missing})
    if missing_required:
        issues.append({"code": "glyph-coverage", "scope": "requiredCharacters", "items": missing_required, "message": "required characters are absent from the primary TMP Font Asset"})
    if missing_fixture:
        issues.append({"code": "glyph-coverage", "scope": "fixture-text", "items": missing_fixture, "message": "fixture text contains characters absent from the primary TMP Font Asset"})

    fallback_ids = [str(value) for value in policy.get("fallbackFontAssetIds", []) if isinstance(value, str)]
    if not fallback_ids:
        issues.append({"code": "fallback", "message": "typographyPolicy must declare at least one fallbackFontAssetId"})
    distinct_fallback_ids = [value for value in fallback_ids if value != policy.get("fontAssetId")]
    fallback_records = policy.get("fallbackFontAssets") if isinstance(policy.get("fallbackFontAssets"), list) else []
    fallback_receipts: list[dict[str, Any]] = []
    fallback_unicode: set[int] = set()
    records_by_id = {
        str(record.get("id")): record
        for record in fallback_records
        if isinstance(record, dict) and isinstance(record.get("id"), str)
    }
    for fallback_id in distinct_fallback_ids:
        record = records_by_id.get(fallback_id)
        if record is None:
            issues.append({"code": "fallback-metadata", "fallbackId": fallback_id, "message": "distinct fallback requires path, hash and license metadata"})
            continue
        _, values, receipt = read_font_asset(root, record, f"fallback {fallback_id}")
        fallback_receipts.append(receipt)
        fallback_unicode.update(values)
        if receipt["issues"]:
            issues.extend(receipt["issues"])
    missing_with_fallback: list[dict[str, Any]] = []
    for item in missing_required + missing_fixture:
        unresolved = [character for character in item["missing"] if ord(character) not in fallback_unicode]
        if unresolved:
            missing_with_fallback.append({**item, "missingAfterFallback": unresolved})
    if missing_with_fallback and distinct_fallback_ids:
        issues.append({"code": "fallback-coverage", "scope": "fixture-text", "items": missing_with_fallback, "message": "declared fallback assets do not cover all missing fixture glyphs"})
    if (missing_required or missing_fixture) and not distinct_fallback_ids:
        issues.append({"code": "fallback", "message": "missing glyphs have no distinct fallback asset declared"})

    locale_fixtures = [str(value) for value in policy.get("localeFixtures", []) if isinstance(value, str)]
    if not locale_fixtures:
        issues.append({"code": "locale-fixtures", "message": "at least one locale fixture is required"})
    if "m_CharacterTable:" not in font_text:
        issues.append({"code": "font-format", "message": "file does not look like a serialized TMP Font Asset"})

    receipt = {
        "schemaVersion": 1,
        "evaluator": "es-ui-prefab-authoring/evaluate_ui_typography",
        "evaluatorVersion": 2,
        "specPath": spec_path.as_posix(),
        "specSha256": sha256(spec_path),
        "fontAssetPath": str(policy.get("fontAssetPath", "")),
        "fontAssetHash": actual_hash,
        "fontAssetId": policy.get("fontAssetId", ""),
        "unicodeCount": len(unicode_values),
        "requiredCharacterCount": len(required),
        "fixtureStringCount": len(fixtures),
        "localeFixtures": locale_fixtures,
        "missingRequiredCharacters": missing_required,
        "missingFixtureCharacters": missing_fixture,
        "fallbackFontAssetIds": fallback_ids,
        "fallbackFontAssets": fallback_receipts,
        "missingCharactersAfterFallback": missing_with_fallback,
        "status": "passed" if not issues else "blocked",
        "issues": issues,
        "nonClaims": ["Unity font import", "font fallback load order", "GPU glyph rendering", "commercial license acceptance"],
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 2 if args.strict and issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
