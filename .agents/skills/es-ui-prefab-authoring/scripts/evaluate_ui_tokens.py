#!/usr/bin/env python3
"""Evaluate ScreenSpec color tokens, contrast and token consumer coverage."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


TOKEN_ALIASES = {
    "action": "accent",
    "focus": "accent",
    "feedback": "danger",
    "border": "surfaceRaised",
    "icon": "accent",
    "panel": "surface",
    "primary": "accent",
    "selected": "accent",
    "muted": "mutedText",
}
VARIANT_TO_TOKEN = {
    "background": "background",
    "surface": "surface",
    "surfaceraised": "surfaceRaised",
    "panel": "surface",
    "card": "surfaceRaised",
    "raised": "surfaceRaised",
    "accent": "accent",
    "primary": "accent",
    "selected": "accent",
    "text": "text",
    "mutedtext": "mutedText",
    "muted": "mutedText",
    "danger": "danger",
    "error": "danger",
    "feedback": "danger",
    "none": None,
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_color(value: Any) -> tuple[float, float, float, float] | None:
    if not isinstance(value, str):
        return None
    raw = value.strip().lstrip("#")
    if len(raw) == 3:
        raw = "".join(ch * 2 for ch in raw) + "ff"
    elif len(raw) == 6:
        raw += "ff"
    if len(raw) != 8:
        return None
    try:
        return tuple(int(raw[index : index + 2], 16) / 255.0 for index in range(0, 8, 2))  # type: ignore[return-value]
    except ValueError:
        return None


def relative_luminance(color: tuple[float, float, float, float]) -> float:
    channels = []
    for channel in color[:3]:
        channels.append(channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4)
    return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]


def contrast_ratio(first: tuple[float, float, float, float], second: tuple[float, float, float, float]) -> float:
    high, low = sorted((relative_luminance(first), relative_luminance(second)), reverse=True)
    return (high + 0.05) / (low + 0.05)


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


def token_for(component: dict[str, Any]) -> str | None:
    explicit = component.get("colorToken")
    if isinstance(explicit, str) and explicit.strip():
        return explicit.strip()
    variant = str(component.get("visualVariant", "")).replace("-", "").lower()
    return VARIANT_TO_TOKEN.get(variant)


def resolve_role(tokens: dict[str, Any], role: str) -> str | None:
    if role in tokens:
        return role
    alias = TOKEN_ALIASES.get(role)
    return alias if alias in tokens else None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--strict", action="store_true", help="return non-zero when any issue exists")
    args = parser.parse_args()

    spec_path = args.spec.resolve()
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    tokens = spec.get("tokens") if isinstance(spec.get("tokens"), dict) else {}
    policy = ((spec.get("qualityGates") or {}).get("colorPolicy") or {})
    minimum = float(policy.get("minimumTextContrast", 4.5))
    issues: list[dict[str, Any]] = []
    parsed: dict[str, tuple[float, float, float, float]] = {}
    for name, value in tokens.items():
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            continue
        color = parse_color(value)
        if color is None:
            issues.append({"code": "invalid-color", "token": name, "message": "token is not a valid #RGB/#RRGGBB/#RRGGBBAA color"})
        else:
            parsed[name] = color

    required_roles = [str(role) for role in policy.get("tokenRoles", []) if isinstance(role, str)]
    role_resolution = {role: resolve_role(tokens, role) for role in required_roles}
    for role, resolved in role_resolution.items():
        if resolved is None:
            issues.append({"code": "missing-role", "role": role, "message": "no token or declared alias satisfies this semantic role"})

    accent_label = "onAccent" if "onAccent" in parsed else "text"
    danger_label = "onDanger" if "onDanger" in parsed else "text"
    contrast_pairs = [
        ("text", "background", "background-text"),
        ("mutedText", "background", "background-muted-text"),
        ("text", "surface", "surface-text"),
        ("mutedText", "surface", "surface-muted-text"),
        ("text", "surfaceRaised", "raised-surface-text"),
        (accent_label, "accent", "accent-label"),
        ("background", "accentWarm", "warm-action-background"),
        (danger_label, "danger", "danger-label"),
    ]
    contrast_checks: list[dict[str, Any]] = []
    for foreground, background, check_id in contrast_pairs:
        if foreground not in parsed or background not in parsed:
            continue
        ratio = round(contrast_ratio(parsed[foreground], parsed[background]), 4)
        passed = ratio >= minimum
        contrast_checks.append({"id": check_id, "foreground": foreground, "background": background, "ratio": ratio, "minimum": minimum, "status": "passed" if passed else "blocked"})
        if not passed:
            issues.append({"code": "contrast", "checkId": check_id, "message": f"contrast ratio {ratio} is below {minimum}", "foreground": foreground, "background": background})
    if "onAccent" not in parsed:
        issues.append({"code": "missing-token", "token": "onAccent", "message": "accent surfaces need an explicit readable foreground token"})
    if "onDanger" not in parsed:
        issues.append({"code": "missing-token", "token": "onDanger", "message": "danger surfaces need an explicit readable foreground token"})

    consumers: dict[str, list[str]] = {str(name): [] for name in tokens}
    unknown_variants: list[dict[str, Any]] = []
    for component in flatten_components(spec.get("components")):
        token = token_for(component)
        component_id = str(component.get("id", ""))
        if token in consumers:
            consumers[token].append(component_id)
        elif token is not None:
            unknown_variants.append({"componentId": component_id, "token": token})
        # The materializer uses role-specific foregrounds for actionable
        # labels. Record that consumer edge explicitly instead of treating
        # onAccent/onDanger as merely declared contrast inputs.
        variant = str(component.get("visualVariant", "")).replace("-", "").lower()
        if component.get("type") in {"button", "toggle", "dropdown"} or component.get("interaction"):
            if variant in {"accent", "primary", "selected"} and "onAccent" in consumers:
                consumers["onAccent"].append(component_id)
            if variant in {"danger", "error", "feedback"} and "onDanger" in consumers:
                consumers["onDanger"].append(component_id)
    for item in unknown_variants:
        issues.append({"code": "unknown-token-consumer", **item, "message": "component resolves to a token not declared by ScreenSpec"})

    state_signal_policy = policy.get("nonColorStateSignals") if isinstance(policy.get("nonColorStateSignals"), dict) else {}
    declared_states = [str(state.get("id")) for state in spec.get("states", []) if isinstance(state, dict) and state.get("id")]
    state_signals: dict[str, list[str]] = {}
    for state in declared_states:
        signals = state_signal_policy.get(state)
        if signals is None:
            # The policy is keyed by visual state categories, not necessarily every fixture id.
            signals = state_signal_policy.get(state.lower(), [])
        state_signals[state] = [str(signal) for signal in signals] if isinstance(signals, list) else []
        if state not in {"default"} and not state_signals[state]:
            issues.append({"code": "state-signal", "state": state, "message": "non-color state signal policy is missing"})

    receipt = {
        "schemaVersion": 1,
        "evaluator": "es-ui-prefab-authoring/evaluate_ui_tokens",
        "evaluatorVersion": 1,
        "specPath": spec_path.as_posix(),
        "specSha256": sha256(spec_path),
        "status": "passed" if not issues else "blocked",
        "minimumTextContrast": minimum,
        "roleResolution": role_resolution,
        "contrastChecks": contrast_checks,
        "consumerTrace": consumers,
        "stateSignals": state_signals,
        "issues": issues,
        "nonClaims": ["Unity material assignment", "font rendering", "GPU color management", "commercial visual acceptance"],
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 2 if args.strict and issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
