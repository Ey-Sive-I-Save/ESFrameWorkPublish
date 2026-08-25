#!/usr/bin/env python3
"""Validate an ES UI IntentSpec v1 without touching Unity or project assets."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
REGISTRY = Path(__file__).resolve().parents[1] / "references" / "player-intent-registry.json"
ALLOWED_FIELDS = {
    "schemaVersion", "status", "intentId", "primaryAction", "secondaryActions",
    "screenFamilies", "informationPriority", "requiredStates", "layoutPreferences",
    "inputModalities", "businessBridge", "visualOnly", "confidence", "missingInputs",
    "blockedWhen",
}
STATUSES = {"confirmed", "needs-clarification", "blocked"}


def fail(message: str) -> None:
    raise ValueError(message)


def load(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"invalid JSON: {exc}")
    if not isinstance(value, dict):
        fail("IntentSpec must be an object")
    return value


def validate(spec: dict, registry: dict) -> dict:
    unknown = sorted(set(spec) - ALLOWED_FIELDS)
    if unknown:
        fail(f"unknown fields: {', '.join(unknown)}")
    if spec.get("schemaVersion") != 1:
        fail("schemaVersion must be 1")
    if spec.get("status") not in STATUSES:
        fail("status must be confirmed, needs-clarification or blocked")
    intent_id = spec.get("intentId")
    if not isinstance(intent_id, str) or not intent_id or intent_id != intent_id.lower():
        fail("intentId must be a non-empty lowercase string")
    action = spec.get("primaryAction")
    actions = registry["actions"]
    if action not in actions:
        fail(f"unknown primaryAction: {action}")
    secondary = spec.get("secondaryActions")
    if not isinstance(secondary, list) or any(item not in actions for item in secondary):
        fail("secondaryActions must contain registered actions")
    if action in secondary or len(secondary) != len(set(secondary)):
        fail("primaryAction cannot be secondary and actions cannot repeat")
    families = spec.get("screenFamilies")
    allowed_families = set(registry["screenFamilies"])
    if not isinstance(families, list) or not families or any(item not in allowed_families for item in families):
        fail("screenFamilies must contain registered families")
    if not set(families) & set(actions[action]["screenFamilies"]):
        fail("screenFamilies is incompatible with primaryAction")
    for field in ("informationPriority", "requiredStates", "inputModalities", "missingInputs", "blockedWhen"):
        if not isinstance(spec.get(field), list) or any(not isinstance(item, str) or not item for item in spec[field]):
            fail(f"{field} must be a list of non-empty strings")
    if any(item not in registry["states"] for item in spec["requiredStates"]):
        fail("requiredStates contains an unknown state")
    if any(item not in registry["inputModalities"] for item in spec["inputModalities"]):
        fail("inputModalities contains an unknown modality")
    layout = spec.get("layoutPreferences")
    if not isinstance(layout, dict) or not {"wide", "narrow"} <= set(layout):
        fail("layoutPreferences must declare wide and narrow variants")
    if not isinstance(spec.get("businessBridge"), str) or not spec["businessBridge"]:
        fail("businessBridge must be a stable non-empty ID")
    if spec.get("visualOnly") is not True:
        fail("visualOnly must be true; runtime behavior is out of scope")
    confidence = spec.get("confidence")
    if not isinstance(confidence, (int, float)) or isinstance(confidence, bool) or not 0 <= confidence <= 1:
        fail("confidence must be a number from 0 to 1")
    def key_names(value):
        if isinstance(value, dict):
            for key, child in value.items():
                yield str(key).lower()
                yield from key_names(child)
        elif isinstance(value, list):
            for child in value:
                yield from key_names(child)
    forbidden = set(registry["forbiddenBusinessFields"])
    for field in sorted(set(key_names(spec)) & forbidden):
        fail(f"business payload field is forbidden: {field}")
    serialized = json.dumps(spec, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    if spec["status"] == "confirmed" and (confidence < 0.75 or spec["missingInputs"] or spec["blockedWhen"]):
        fail("confirmed intent requires confidence >= 0.75 and no missing or blocked conditions")
    if spec["status"] != "confirmed" and not (spec["missingInputs"] or spec["blockedWhen"]):
        fail("blocked or clarification intent must explain its condition")
    return {"intentId": intent_id, "primaryAction": action, "screenFamilies": families,
            "status": spec["status"], "specHash": hashlib.sha256(serialized.encode("utf-8")).hexdigest()}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("spec", type=Path)
    args = parser.parse_args()
    try:
        result = validate(load(args.spec), load(REGISTRY))
    except ValueError as exc:
        print(f"INVALID: {exc}", file=sys.stderr)
        return 2
    print(json.dumps({"status": "passed", **result}, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
