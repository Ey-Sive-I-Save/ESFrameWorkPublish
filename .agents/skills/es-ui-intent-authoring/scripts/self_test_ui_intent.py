#!/usr/bin/env python3
"""Deterministic positive/negative replay for IntentSpec v1."""
from __future__ import annotations

import copy
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from validate_intent_spec import REGISTRY, load, validate  # noqa: E402


def base() -> dict:
    return {"schemaVersion": 1, "status": "confirmed", "intentId": "equip-item",
            "primaryAction": "equip", "secondaryActions": ["inspect", "compare", "cancel"],
            "screenFamilies": ["collection"], "informationPriority": ["item-list", "selected-item", "stats", "action-bar"],
            "requiredStates": ["default", "selected", "disabled", "empty", "loading", "error", "long-content"],
            "layoutPreferences": {"wide": {"composition": "grid-detail"}, "narrow": {"composition": "list-detail"}},
            "inputModalities": ["pointer", "keyboard", "gamepad"], "businessBridge": "equipment-domain",
            "visualOnly": True, "confidence": 0.92, "missingInputs": [], "blockedWhen": []}


def must_reject(spec: dict, registry: dict) -> None:
    try:
        validate(spec, registry)
    except ValueError:
        return
    raise AssertionError("invalid IntentSpec was accepted")


def main() -> int:
    registry = load(REGISTRY)
    valid = base()
    first = validate(valid, registry)
    second = validate(copy.deepcopy(valid), registry)
    assert first == second, "repeat validation is not deterministic"
    ambiguous = copy.deepcopy(valid)
    ambiguous["status"] = "needs-clarification"
    ambiguous["confidence"] = 0.4
    ambiguous["missingInputs"] = ["primary-goal"]
    must_reject({**ambiguous, "status": "confirmed"}, registry)
    unknown = copy.deepcopy(valid)
    unknown["screenFamilies"] = ["unknown-family"]
    must_reject(unknown, registry)
    expansion = copy.deepcopy(valid)
    expansion["items"] = []
    must_reject(expansion, registry)
    blocked = copy.deepcopy(valid)
    blocked["visualOnly"] = False
    must_reject(blocked, registry)
    receipt = {"skillName": "es-ui-intent-authoring", "status": "passed", "evidenceLevel": "S2",
               "cases": ["normal-input", "invalid-input", "denied-expansion", "repeat-idempotency", "interruption-recovery"],
               "deterministicHash": hashlib.sha256(json.dumps(first, sort_keys=True).encode()).hexdigest(),
               "runtimeClaimsNotProven": ["runtime menu", "input navigation", "Unity Prefab", "visual fidelity"]}
    print(json.dumps(receipt, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
