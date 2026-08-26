#!/usr/bin/env python3
"""Validate the generic ScreenSpec v3 contract against the game UI registry."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import math
import re
from pathlib import Path
from typing import Any

ID_RE = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
SCREEN_TYPES = {"hud", "navigation", "modal", "conversation", "progression", "collection", "combat", "world", "system", "result"}
LAYOUT_MODES = {"stretch", "center", "edge-docked", "flow", "grid", "list", "overlay", "absolute", "content"}
LAYOUT_GROUP_MODES = {"grid", "list", "flow"}
STATE_IDS = {"default", "selected", "empty", "loading", "disabled", "error", "long-content"}
FEEDBACK_RULE_IDS = {"UI-FB-001", "UI-FB-002", "UI-FB-003", "UI-FB-004", "UI-FB-005", "UI-FB-006"}
HEX_RE = re.compile(r"^#[0-9a-fA-F]{6}$")
STATE_EFFECT_FIELDS = {"visible", "interactable", "graphicAlpha", "graphicColor", "wrapText", "text", "outline"}
FIXTURE_TEXT_OVERFLOW_POLICIES = {"wrap", "ellipsis", "scroll"}
# State variants declare Fixture participation only. Geometry has one authored source
# in the base LayoutPlan, so a state must never smuggle a second RectTransform plan.
STATE_GEOMETRY_MUTATION_FIELDS = {
    "bounds", "anchor", "pivot", "layout", "layoutmode", "mode", "minsize",
    "targetsize", "siblingorder", "childgeometryowner", "safearea", "canvas",
    "parent", "position", "size", "width", "height",
}
STATE_GEOMETRY_MUTATION_LABELS = {
    "bounds", "anchor", "pivot", "layout", "layout-mode", "min-size",
    "target-size", "sibling-order", "child-geometry-owner", "safe-area",
    "canvas", "parent", "position", "size",
}


def fail(issues: list[dict[str, str]], path: str, message: str, code: str = "invalid") -> None:
    issues.append({"code": code, "path": path, "message": message})


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def non_empty_string(value: Any, path: str, issues: list[dict[str, str]]) -> str | None:
    if not isinstance(value, str) or not value.strip():
        fail(issues, path, "must be a non-empty string", "required")
        return None
    return value.strip()


def validate_bounds(value: Any, path: str, issues: list[dict[str, str]]) -> None:
    if not isinstance(value, list) or len(value) != 4:
        fail(issues, path, "must be [xMin,yMin,xMax,yMax]", "layout")
        return
    if any(isinstance(item, bool) or not isinstance(item, (int, float)) or not math.isfinite(float(item)) for item in value):
        fail(issues, path, "must contain finite numbers", "layout")
    elif any(item < 0 or item > 1 for item in value) or value[0] > value[2] or value[1] > value[3]:
        fail(issues, path, "must be normalized and ordered", "layout")


def validate_no_state_geometry_mutation(value: Any, path: str, issues: list[dict[str, str]]) -> None:
    """Reject state-local geometry before it can diverge from the LayoutPlan."""
    if isinstance(value, dict):
        for key, nested in value.items():
            if str(key).replace("_", "").replace("-", "").lower() in STATE_GEOMETRY_MUTATION_FIELDS:
                fail(issues, f"{path}.{key}", "state variants and effects cannot modify authored geometry; revise the base LayoutPlan", "state-geometry")
            validate_no_state_geometry_mutation(nested, f"{path}.{key}", issues)
    elif isinstance(value, list):
        for index, nested in enumerate(value):
            validate_no_state_geometry_mutation(nested, f"{path}[{index}]", issues)


def validate_component(component: Any, path: str, registry: dict[str, Any], assets: dict[str, Any], states: set[str], seen: set[str], issues: list[dict[str, str]], require_layout_ownership: bool = False, require_declared_states: bool = False) -> None:
    if not isinstance(component, dict):
        fail(issues, path, "component must be an object", "type")
        return
    component_id = non_empty_string(component.get("id"), f"{path}.id", issues)
    if component_id and (not ID_RE.fullmatch(component_id) or component_id in seen):
        fail(issues, f"{path}.id", "must be unique and use a stable lowercase id", "identity")
    if component_id:
        seen.add(component_id)
    component_type = non_empty_string(component.get("type"), f"{path}.type", issues)
    definition = registry.get("components", {}).get(component_type or "")
    if definition is None:
        fail(issues, f"{path}.type", "component type is not registered", "registry")
        definition = {}
    layout = component.get("layout")
    if not isinstance(layout, dict):
        fail(issues, f"{path}.layout", "every component needs a layout contract", "layout")
    else:
        mode = layout.get("mode")
        if mode not in LAYOUT_MODES:
            fail(issues, f"{path}.layout.mode", f"must be one of {sorted(LAYOUT_MODES)}", "layout")
        validate_bounds(layout.get("bounds"), f"{path}.layout.bounds", issues)
        min_size = layout.get("minSize", [0, 0])
        if not isinstance(min_size, list) or len(min_size) != 2 or any(isinstance(item, bool) or not isinstance(item, (int, float)) or item < 0 for item in min_size):
            fail(issues, f"{path}.layout.minSize", "must be a non-negative numeric pair", "layout")
        if mode in LAYOUT_GROUP_MODES and component.get("children"):
            owner = layout.get("childGeometryOwner")
            if require_layout_ownership and owner not in {"parent-layout-group", "child-bounds"}:
                fail(issues, f"{path}.layout.childGeometryOwner", "layout groups must declare parent-layout-group or child-bounds ownership", "layout")
    asset_slots = component.get("assetSlots", [])
    if not isinstance(asset_slots, list):
        fail(issues, f"{path}.assetSlots", "must be an array", "asset")
        asset_slots = []
    for index, slot in enumerate(asset_slots):
        if not isinstance(slot, str) or not slot.strip():
            fail(issues, f"{path}.assetSlots[{index}]", "must be a non-empty asset id", "asset")
        elif slot not in assets:
            fail(issues, f"{path}.assetSlots[{index}]", "asset id is not declared in assets", "asset")
    if definition.get("requiresAsset") and not asset_slots:
        fail(issues, f"{path}.assetSlots", "registered component requires at least one asset", "asset")
    content = component.get("content", {})
    if not isinstance(content, dict):
        fail(issues, f"{path}.content", "must be an object", "content")
        content = {}
    if definition.get("requiresText") and not isinstance(content.get("text"), str):
        fail(issues, f"{path}.content.text", "registered component requires text content", "content")
    if definition.get("requiresTextOrAsset") and not isinstance(content.get("text"), str) and not asset_slots:
        fail(issues, f"{path}", "registered component requires text or asset content", "content")
    if definition.get("requiresNumericValue") and not isinstance(content.get("value"), (int, float)):
        fail(issues, f"{path}.content.value", "registered component requires a numeric value", "content")
    interaction = component.get("interaction", {})
    if definition.get("interactive"):
        if not isinstance(interaction, dict) or not isinstance(interaction.get("intent"), str) or not interaction.get("intent").strip():
            fail(issues, f"{path}.interaction.intent", "interactive component requires an input intent", "behavior")
        target = interaction.get("targetSize", [44, 44]) if isinstance(interaction, dict) else [0, 0]
        if not isinstance(target, list) or len(target) != 2 or target[0] < 44 or target[1] < 44:
            fail(issues, f"{path}.interaction.targetSize", "interactive target must be at least 44x44", "behavior")
    children = component.get("children", [])
    if not isinstance(children, list):
        fail(issues, f"{path}.children", "must be an array", "structure")
        children = []
    if definition.get("requiresChildren") and not children:
        fail(issues, f"{path}.children", "registered component requires children", "structure")
    for index, child in enumerate(children):
        # IDs are global semantic identities, not only sibling names. Reuse the
        # root set so duplicate controls in separate branches cannot slip through.
        validate_component(child, f"{path}.children[{index}]", registry, assets, states, seen, issues, require_layout_ownership, require_declared_states)
    variants = component.get("stateVariants", {})
    if isinstance(variants, list):
        for index, variant in enumerate(variants):
            state_id = variant.get("stateId") if isinstance(variant, dict) else None
            if state_id not in states and (require_declared_states or state_id not in STATE_IDS):
                fail(issues, f"{path}.stateVariants[{index}].stateId", "state is not declared", "state")
            if isinstance(variant, dict):
                validate_no_state_geometry_mutation({key: value for key, value in variant.items() if key != "stateId"}, f"{path}.stateVariants[{index}]", issues)
    elif isinstance(variants, dict):
        for state_id, variant in variants.items():
            if state_id not in states and (require_declared_states or state_id not in STATE_IDS):
                fail(issues, f"{path}.stateVariants.{state_id}", "state is not declared", "state")
            validate_no_state_geometry_mutation(variant, f"{path}.stateVariants.{state_id}", issues)
    else:
        fail(issues, f"{path}.stateVariants", "must be an object or array", "state")


def collect_component_ids(components: list[Any]) -> set[str]:
    ids: set[str] = set()
    def visit(nodes: list[Any]) -> None:
        for node in nodes:
            if isinstance(node, dict) and isinstance(node.get("id"), str):
                ids.add(node["id"])
                visit(node.get("children", []))
    visit(components)
    return ids


def collect_components_by_id(components: list[Any]) -> dict[str, dict[str, Any]]:
    """Return the semantic nodes that own fixture-visible text."""
    result: dict[str, dict[str, Any]] = {}

    def visit(nodes: list[Any]) -> None:
        for node in nodes if isinstance(nodes, list) else []:
            if not isinstance(node, dict):
                continue
            component_id = node.get("id")
            if isinstance(component_id, str):
                result[component_id] = node
            visit(node.get("children", []))

    visit(components)
    return result


def collect_interaction_intents(components: list[Any]) -> set[str]:
    intents: set[str] = set()
    def visit(nodes: list[Any]) -> None:
        for node in nodes:
            if not isinstance(node, dict):
                continue
            interaction = node.get("interaction")
            if isinstance(interaction, dict) and isinstance(interaction.get("intent"), str) and interaction["intent"].strip():
                intents.add(interaction["intent"])
            visit(node.get("children", []))
    visit(components)
    return intents


def validate_profile_availability(spec: dict[str, Any], profile_ids: set[str], intents: set[str], issues: list[dict[str, str]], required: bool) -> None:
    availability = spec.get("profileAvailability")
    if not isinstance(availability, dict):
        if required:
            fail(issues, "profileAvailability", "strict feedback validation requires responsive intent availability", "behavior")
        return
    for profile_id in profile_ids:
        entry = availability.get(profile_id)
        path = f"profileAvailability.{profile_id}"
        if not isinstance(entry, dict):
            fail(issues, path, "must declare availableIntents and omittedIntents", "behavior")
            continue
        available = entry.get("availableIntents")
        omitted = entry.get("omittedIntents")
        if not isinstance(available, list):
            fail(issues, f"{path}.availableIntents", "must be an array", "behavior")
        if not isinstance(omitted, list):
            fail(issues, f"{path}.omittedIntents", "must be an array of {intent,reason} objects", "behavior")
            omitted = []
        known = set(available or []) | {item.get("intent") for item in omitted if isinstance(item, dict)}
        for intent in known:
            if intent not in intents:
                fail(issues, f"{path}", f"intent '{intent}' is not declared by a component", "behavior")
        for index, item in enumerate(omitted):
            if not isinstance(item, dict) or not isinstance(item.get("intent"), str) or not item.get("intent").strip() or not isinstance(item.get("reason"), str) or not item.get("reason").strip():
                fail(issues, f"{path}.omittedIntents[{index}]", "must contain a non-empty intent and reason", "behavior")


def validate_intent_contract(spec: dict[str, Any], intents: set[str], issues: list[dict[str, str]], required: bool) -> None:
    """Prevent a requested screen/style target from drifting during authoring."""
    contract = spec.get("intentContract")
    if not isinstance(contract, dict):
        if required:
            fail(issues, "intentContract", "quality-gated authoring requires an explicit request/fidelity contract", "intent-drift")
        return
    family = contract.get("requestedScreenFamily")
    if not isinstance(family, str) or not family.strip():
        fail(issues, "intentContract.requestedScreenFamily", "must identify the requested screen family", "intent-drift")
    elif family != spec.get("screenType"):
        fail(issues, "intentContract.requestedScreenFamily", "does not match ScreenSpec.screenType", "intent-drift")
    primary = contract.get("requestedPrimaryIntent")
    if not isinstance(primary, str) or not primary.strip():
        fail(issues, "intentContract.requestedPrimaryIntent", "must identify the user's primary action", "intent-drift")
    elif primary not in intents:
        fail(issues, "intentContract.requestedPrimaryIntent", "is not declared by any component interaction", "intent-drift")
    target = contract.get("visualTarget")
    if not isinstance(target, str) or not target.strip():
        fail(issues, "intentContract.visualTarget", "must identify the requested visual target or style family", "intent-drift")
    fidelity = contract.get("fidelityMode")
    if fidelity not in {"original", "reference-guided", "reference-match"}:
        fail(issues, "intentContract.fidelityMode", "must be original, reference-guided or reference-match", "intent-drift")
    policy = contract.get("referencePolicy")
    if policy not in {"required", "optional", "not-required"}:
        fail(issues, "intentContract.referencePolicy", "must declare whether reference input is required", "intent-drift")
    references = contract.get("referenceSources", [])
    images = (spec.get("designEvidence") or {}).get("referenceImages", [])
    if not isinstance(references, list):
        fail(issues, "intentContract.referenceSources", "must be an array", "intent-drift")
        references = []
    if not isinstance(images, list):
        images = []
    if policy == "required" or fidelity in {"reference-guided", "reference-match"}:
        if not references:
            fail(issues, "intentContract.referenceSources", "reference-driven requests must declare at least one source", "intent-drift")
        if not images:
            fail(issues, "designEvidence.referenceImages", "reference-driven requests cannot use an empty reference image list", "intent-drift")
    if references and len(images) < len(references):
        fail(issues, "designEvidence.referenceImages", "must contain evidence entries for every declared reference source", "intent-drift")
    boundary = contract.get("productBoundary")
    if not isinstance(boundary, str) or not boundary.strip():
        fail(issues, "intentContract.productBoundary", "must state original/inspired/official asset boundary", "intent-drift")


def validate_state_semantics(spec: dict[str, Any], states: set[str], component_ids: set[str], issues: list[dict[str, str]], required: bool) -> None:
    semantics = spec.get("stateSemantics")
    if not isinstance(semantics, dict):
        if required:
            fail(issues, "stateSemantics", "strict feedback validation requires concrete semantics for every declared state", "state")
        return
    for state_id in states:
        entry = semantics.get(state_id)
        path = f"stateSemantics.{state_id}"
        if not isinstance(entry, dict):
            fail(issues, path, "must define fixtureData, affectedComponentIds, visualChanges, interactionChanges and geometryPolicy", "state")
            continue
        if not isinstance(entry.get("fixtureData"), dict):
            fail(issues, f"{path}.fixtureData", "must be an object containing concrete fixture values", "state")
        affected = entry.get("affectedComponentIds")
        if not isinstance(affected, list) or not affected:
            fail(issues, f"{path}.affectedComponentIds", "must name components whose state is observable", "state")
        else:
            for index, component_id in enumerate(affected):
                if component_id not in component_ids:
                    fail(issues, f"{path}.affectedComponentIds[{index}]", "component id is not declared", "state")
                elif not _component_declares_state(spec.get("components", []), component_id, state_id):
                    fail(issues, f"{path}.affectedComponentIds[{index}]", "affected component must declare the same state in stateVariants", "state-binding")
        for field in ("visualChanges", "interactionChanges"):
            if not isinstance(entry.get(field), list) or not entry[field] or any(not isinstance(item, str) or not item.strip() for item in entry[field]):
                fail(issues, f"{path}.{field}", "must be a non-empty string array", "state")
        policy = entry.get("geometryPolicy")
        if not isinstance(policy, dict) or policy.get("preserveBounds") is not True:
            fail(issues, f"{path}.geometryPolicy.preserveBounds", "must explicitly state whether authored geometry is preserved", "state")
        elif "allowedChanges" in policy:
            allowed_changes = policy.get("allowedChanges")
            if not isinstance(allowed_changes, list) or any(not isinstance(item, str) or not item.strip() for item in allowed_changes):
                fail(issues, f"{path}.geometryPolicy.allowedChanges", "must be a string array when declared", "state-geometry")
            else:
                forbidden = sorted({item.strip().lower() for item in allowed_changes} & STATE_GEOMETRY_MUTATION_LABELS)
                if forbidden:
                    fail(issues, f"{path}.geometryPolicy.allowedChanges", f"cannot allow geometry changes while preserveBounds is true: {forbidden}", "state-geometry")
        validate_state_effects(entry, path, state_id, affected, component_ids, issues, required)


def validate_state_effects(entry: dict[str, Any], path: str, state_id: str, affected: Any, component_ids: set[str], issues: list[dict[str, str]], required: bool) -> None:
    """Require state prose to resolve into deterministic per-component effects."""
    effects = entry.get("effects")
    if not isinstance(effects, list) or not effects:
        if required:
            fail(issues, f"{path}.effects", "strict state semantics require non-empty executable component effects", "state-effects")
        return
    affected_ids = set(affected) if isinstance(affected, list) else set()
    effect_ids: set[str] = set()
    for index, effect in enumerate(effects):
        effect_path = f"{path}.effects[{index}]"
        if not isinstance(effect, dict):
            fail(issues, effect_path, "must be an object with componentId and changes", "state-effects")
            continue
        component_id = effect.get("componentId")
        if not isinstance(component_id, str) or component_id not in component_ids:
            fail(issues, f"{effect_path}.componentId", "must reference a declared component", "state-effects")
            continue
        if component_id not in affected_ids:
            fail(issues, f"{effect_path}.componentId", "must belong to affectedComponentIds", "state-effects")
        if component_id in effect_ids:
            fail(issues, f"{effect_path}.componentId", "must appear at most once per state", "state-effects")
        effect_ids.add(component_id)
        changes = effect.get("changes")
        if not isinstance(changes, dict) or not changes:
            fail(issues, f"{effect_path}.changes", "must declare at least one executable effect", "state-effects")
            continue
        unknown = set(changes) - STATE_EFFECT_FIELDS
        if unknown:
            fail(issues, f"{effect_path}.changes", f"contains unsupported effect fields: {sorted(unknown)}", "state-effects")
        for field in ("visible", "interactable", "wrapText", "outline"):
            if field in changes and not isinstance(changes[field], bool):
                fail(issues, f"{effect_path}.changes.{field}", "must be boolean", "state-effects")
        if "graphicAlpha" in changes and (isinstance(changes["graphicAlpha"], bool) or not isinstance(changes["graphicAlpha"], (int, float)) or not 0 <= changes["graphicAlpha"] <= 1):
            fail(issues, f"{effect_path}.changes.graphicAlpha", "must be a normalized number from 0 to 1", "state-effects")
        if "graphicColor" in changes and (not isinstance(changes["graphicColor"], str) or not HEX_RE.fullmatch(changes["graphicColor"])):
            fail(issues, f"{effect_path}.changes.graphicColor", "must be a #RRGGBB color", "state-effects")
        if "text" in changes and (not isinstance(changes["text"], str) or not changes["text"].strip()):
            fail(issues, f"{effect_path}.changes.text", "must be a non-empty string", "state-effects")
    if required and affected_ids - effect_ids:
        fail(issues, f"{path}.effects", f"must cover every affected component: {sorted(affected_ids - effect_ids)}", "state-effects")
    if state_id == "selected" and not any(isinstance(effect, dict) and (effect.get("changes") or {}).get("outline") is True for effect in effects):
        fail(issues, f"{path}.effects", "selected state needs an explicit outline effect", "state-effects")
    if state_id == "disabled" and not any(isinstance(effect, dict) and ((effect.get("changes") or {}).get("interactable") is False or (effect.get("changes") or {}).get("graphicAlpha", 1) < 1) for effect in effects):
        fail(issues, f"{path}.effects", "disabled state needs an explicit disabled interaction or alpha effect", "state-effects")
    if state_id == "loading" and not any(isinstance(effect, dict) and ((effect.get("changes") or {}).get("graphicAlpha", 1) < 1 or "text" in (effect.get("changes") or {})) for effect in effects):
        fail(issues, f"{path}.effects", "loading state needs an explicit visible loading effect", "state-effects")
    if state_id == "error" and not any(isinstance(effect, dict) and ("graphicColor" in (effect.get("changes") or {}) or "text" in (effect.get("changes") or {})) for effect in effects):
        fail(issues, f"{path}.effects", "error state needs an explicit feedback color or message effect", "state-effects")
    if state_id == "long-content" and not any(isinstance(effect, dict) and (effect.get("changes") or {}).get("wrapText") is True for effect in effects):
        fail(issues, f"{path}.effects", "long-content state needs an explicit wrapping effect", "state-effects")


def validate_fixture_text_bindings(spec: dict[str, Any], states: set[str], components: list[Any], issues: list[dict[str, str]], required: bool) -> None:
    """Bind fixture strings to concrete text owners instead of guessing key names.

    A binding is deliberately state-local: it identifies both the test input and
    the component whose rendered text must consume it. This keeps fixture data
    from becoming untraceable metadata and prevents long-content tests from
    silently exercising the authored short copy instead.
    """
    semantics = spec.get("stateSemantics")
    if not isinstance(semantics, dict):
        return
    by_id = collect_components_by_id(components)
    for state_id in states:
        entry = semantics.get(state_id)
        if not isinstance(entry, dict):
            continue
        path = f"stateSemantics.{state_id}"
        fixture_data = entry.get("fixtureData") if isinstance(entry.get("fixtureData"), dict) else {}
        affected = set(entry.get("affectedComponentIds", [])) if isinstance(entry.get("affectedComponentIds"), list) else set()
        raw_bindings = entry.get("fixtureTextBindings")
        textual_affected = {
            component_id
            for component_id in affected
            if isinstance(by_id.get(component_id, {}).get("content"), dict)
            and isinstance(by_id[component_id]["content"].get("text"), str)
        }
        if raw_bindings is None:
            if required and state_id == "long-content" and textual_affected:
                fail(issues, f"{path}.fixtureTextBindings", "long-content must bind every affected textual component to fixtureData", "fixture-text")
            continue
        if not isinstance(raw_bindings, list):
            fail(issues, f"{path}.fixtureTextBindings", "must be an array of explicit text bindings", "fixture-text")
            continue
        binding_ids: set[str] = set()
        for index, binding in enumerate(raw_bindings):
            binding_path = f"{path}.fixtureTextBindings[{index}]"
            if not isinstance(binding, dict):
                fail(issues, binding_path, "must be an object", "fixture-text")
                continue
            component_id = binding.get("componentId")
            if not isinstance(component_id, str) or component_id not in by_id:
                fail(issues, f"{binding_path}.componentId", "must reference a declared component", "fixture-text")
                continue
            if component_id in binding_ids:
                fail(issues, f"{binding_path}.componentId", "must appear at most once per state", "fixture-text")
            binding_ids.add(component_id)
            if component_id not in affected:
                fail(issues, f"{binding_path}.componentId", "must belong to affectedComponentIds", "fixture-text")
            content = by_id[component_id].get("content") if isinstance(by_id[component_id].get("content"), dict) else {}
            if not isinstance(content.get("text"), str):
                fail(issues, f"{binding_path}.componentId", "must target a component with authored text content", "fixture-text")
            if isinstance(by_id[component_id].get("interaction"), dict):
                fail(issues, f"{binding_path}.componentId", "cannot bind fixture text to an interactive control", "fixture-text")
            key = binding.get("fixtureDataKey")
            if not isinstance(key, str) or not key.strip():
                fail(issues, f"{binding_path}.fixtureDataKey", "must name a non-empty fixtureData string", "fixture-text")
            elif not isinstance(fixture_data.get(key), str) or not fixture_data[key].strip():
                fail(issues, f"{binding_path}.fixtureDataKey", "must resolve to a non-empty fixtureData string", "fixture-text")
            policy = binding.get("overflowPolicy")
            if policy not in FIXTURE_TEXT_OVERFLOW_POLICIES:
                fail(issues, f"{binding_path}.overflowPolicy", f"must be one of {sorted(FIXTURE_TEXT_OVERFLOW_POLICIES)}", "fixture-text")
            elif policy == "scroll":
                # Scroll is not yet a Materializer feature. Failing closed is
                # safer than serializing a non-scrollable text node as one.
                fail(issues, f"{binding_path}.overflowPolicy", "scroll requires a registered scroll-container Materializer recipe and is not available in v3", "fixture-text")
            max_lines = binding.get("maxLines")
            if isinstance(max_lines, bool) or not isinstance(max_lines, int) or max_lines < 1:
                fail(issues, f"{binding_path}.maxLines", "must be a positive integer", "fixture-text")
            clearance = binding.get("reserveActionClearancePx", 0)
            if isinstance(clearance, bool) or not isinstance(clearance, (int, float)) or clearance < 0:
                fail(issues, f"{binding_path}.reserveActionClearancePx", "must be a non-negative number", "fixture-text")
            insets = binding.get("contentInsetsPx")
            if not isinstance(insets, list) or len(insets) != 4 or any(isinstance(value, bool) or not isinstance(value, (int, float)) or value < 0 for value in insets):
                fail(issues, f"{binding_path}.contentInsetsPx", "must be [left,top,right,bottom] non-negative pixel values", "fixture-text")
        if required and state_id == "long-content":
            missing = sorted(textual_affected - binding_ids)
            if missing:
                fail(issues, f"{path}.fixtureTextBindings", f"must bind every affected textual component: {missing}", "fixture-text")
        effect_text_targets = {
            effect.get("componentId")
            for effect in entry.get("effects", []) if isinstance(effect, dict)
            and isinstance(effect.get("changes"), dict) and "text" in effect["changes"]
        }
        ambiguous = sorted(binding_ids & effect_text_targets)
        if ambiguous:
            fail(issues, f"{path}.effects", f"fixtureTextBindings own fixture copy; do not also set effect text for: {ambiguous}", "fixture-text")


def _component_declares_state(components: list[Any], component_id: str, state_id: str) -> bool:
    """Keep state semantics and component state variants bidirectionally bound."""
    for component in components if isinstance(components, list) else []:
        if not isinstance(component, dict):
            continue
        if component.get("id") == component_id:
            variants = component.get("stateVariants", {})
            if isinstance(variants, dict):
                return state_id in variants
            if isinstance(variants, list):
                return any(isinstance(item, dict) and item.get("stateId") == state_id for item in variants)
            return False
        if _component_declares_state(component.get("children", []), component_id, state_id):
            return True
    return False


def validate_state_variant_bindings(spec: dict[str, Any], states: set[str], issues: list[dict[str, str]], required: bool) -> None:
    """In strict packets, every non-baseline variant must be fixture-executable.

    `default` is the authored baseline and may be declared on every component without
    making every component an affected target. Any other state is observable intent:
    it must be declared by the screen and explicitly owned by its state semantics.
    """
    if not required:
        return
    semantics = spec.get("stateSemantics")
    if not isinstance(semantics, dict):
        return

    def state_ids(variants: Any) -> list[tuple[str, str]]:
        if isinstance(variants, dict):
            return [(str(state_id), f"stateVariants.{state_id}") for state_id in variants]
        if isinstance(variants, list):
            return [
                (item.get("stateId"), f"stateVariants[{index}].stateId")
                for index, item in enumerate(variants)
                if isinstance(item, dict) and isinstance(item.get("stateId"), str)
            ]
        return []

    def visit(nodes: list[Any], path: str) -> None:
        for index, component in enumerate(nodes if isinstance(nodes, list) else []):
            if not isinstance(component, dict):
                continue
            component_id = component.get("id")
            for state_id, variant_path in state_ids(component.get("stateVariants", {})):
                if state_id == "default":
                    continue
                if state_id not in states:
                    # Declaration is reported by validate_component; avoid a
                    # duplicate binding diagnostic for malformed variants.
                    continue
                affected = (semantics.get(state_id) or {}).get("affectedComponentIds")
                if not isinstance(affected, list) or component_id not in affected:
                    fail(
                        issues,
                        f"{path}[{index}].{variant_path}",
                        "non-default stateVariant must be listed in stateSemantics.affectedComponentIds",
                        "state-binding",
                    )
            visit(component.get("children", []), f"{path}[{index}].children")

    visit(spec.get("components", []), "components")


def validate_feedback(spec: dict[str, Any], issues: list[dict[str, str]], required: bool, component_ids: set[str], profile_ids: set[str], state_ids: set[str]) -> None:
    evidence = spec.get("designEvidence")
    if not isinstance(evidence, dict):
        if required:
            fail(issues, "designEvidence", "feedback gate requires designEvidence", "feedback")
        return
    feedback = evidence.get("feedback")
    if feedback is None:
        if required:
            fail(issues, "designEvidence.feedback", "required when --require-feedback is used", "feedback")
        return
    if not isinstance(feedback, dict):
        fail(issues, "designEvidence.feedback", "must be an object", "feedback")
        return
    prior = feedback.get("priorEvidenceBatch")
    if not isinstance(prior, str) or not prior.strip():
        fail(issues, "designEvidence.feedback.priorEvidenceBatch", "must identify the prior evidence batch", "feedback")
    rule_ids = feedback.get("ruleIds")
    if not isinstance(rule_ids, list) or not rule_ids or any(rule not in FEEDBACK_RULE_IDS for rule in rule_ids):
        fail(issues, "designEvidence.feedback.ruleIds", f"must contain registered rules from {sorted(FEEDBACK_RULE_IDS)}", "feedback")
    for field in ("changedFields", "expectedEffects", "falsificationChecks"):
        value = feedback.get(field)
        if not isinstance(value, list) or not value or any(not isinstance(item, str) or not item.strip() for item in value):
            fail(issues, f"designEvidence.feedback.{field}", "must be a non-empty string array", "feedback")
    bindings = spec.get("bindings")
    if required and not isinstance(bindings, list):
        fail(issues, "bindings", "strict feedback validation requires explicit rule bindings", "feedback")
        return
    if not isinstance(bindings, list):
        return
    by_rule = {item.get("ruleId"): item for item in bindings if isinstance(item, dict)}
    for rule_id in rule_ids if isinstance(rule_ids, list) else []:
        binding = by_rule.get(rule_id)
        path = f"bindings[{rule_id}]"
        if not isinstance(binding, dict):
            fail(issues, path, "every feedback rule must bind to components, profiles, states and evidence checks", "feedback")
            continue
        for field in ("componentIds", "profileIds", "stateIds", "evidenceRequirements", "nextArtifactFields"):
            value = binding.get(field)
            if not isinstance(value, list) or not value:
                fail(issues, f"{path}.{field}", "must be a non-empty array", "feedback")
        for index, component_id in enumerate(binding.get("componentIds", [])):
            if component_id not in component_ids:
                fail(issues, f"{path}.componentIds[{index}]", "component id is not declared", "feedback")
        for index, profile_id in enumerate(binding.get("profileIds", [])):
            if profile_id != "all" and profile_id not in profile_ids:
                fail(issues, f"{path}.profileIds[{index}]", "profile id is not declared", "feedback")
        for index, state_id in enumerate(binding.get("stateIds", [])):
                if state_id != "all" and state_id not in state_ids:
                    fail(issues, f"{path}.stateIds[{index}]", "state id is not declared", "feedback")


def relative_luminance(hex_color: str) -> float:
    values = [int(hex_color[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    linear = [value / 12.92 if value <= 0.03928 else ((value + 0.055) / 1.055) ** 2.4 for value in values]
    return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]


def contrast_ratio(first: str, second: str) -> float:
    light = max(relative_luminance(first), relative_luminance(second))
    dark = min(relative_luminance(first), relative_luminance(second))
    return (light + 0.05) / (dark + 0.05)


def _project_file(base_dir: Path | None, relative: Any) -> Path | None:
    if base_dir is None or not isinstance(relative, str) or not relative.strip():
        return None
    candidate = (base_dir / relative.replace("\\", "/")).resolve()
    try:
        candidate.relative_to(base_dir.resolve())
    except ValueError:
        return None
    return candidate


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_quality_gates(spec: dict[str, Any], assets: dict[str, Any], profiles: list[Any], tokens: Any, issues: list[dict[str, str]], base_dir: Path | None = None) -> None:
    gates = spec.get("qualityGates")
    if not isinstance(gates, dict):
        fail(issues, "qualityGates", "production quality validation requires asset, responsive, color and typography gates", "quality")
        return
    asset_policy = gates.get("assetPolicy")
    if not isinstance(asset_policy, dict):
        fail(issues, "qualityGates.assetPolicy", "must declare asset resolution and required provenance fields", "quality")
    else:
        if asset_policy.get("productionReady") is not True and asset_policy.get("commercialAcceptance") != "deferred":
            fail(issues, "qualityGates.assetPolicy.commercialAcceptance", "non-production assets must explicitly remain deferred", "quality")
        required_asset_fields = asset_policy.get("requiredFields")
        if not isinstance(required_asset_fields, list) or not required_asset_fields:
            fail(issues, "qualityGates.assetPolicy.requiredFields", "must list hash/provenance/license/import/crop/atlas requirements", "quality")
        allowed_sources = asset_policy.get("acceptedSources")
        if not isinstance(allowed_sources, list) or not allowed_sources:
            fail(issues, "qualityGates.assetPolicy.acceptedSources", "must declare accepted non-placeholder source classes", "quality")
        if asset_policy.get("productionReady") is True:
            for asset_id, asset in assets.items():
                for field in required_asset_fields or []:
                    value = asset.get(field) if isinstance(asset, dict) else None
                    if not isinstance(value, str) or not value.strip():
                        fail(issues, f"assets.{asset_id}.{field}", "production-ready asset requires a non-empty resolved field", "quality")
                if asset.get("source") not in set(allowed_sources or []):
                    fail(issues, f"assets.{asset_id}.source", "production-ready asset cannot use a placeholder/procedural source", "quality")
                resolved = _project_file(base_dir, asset.get("path"))
                if resolved is None or not resolved.is_file():
                    fail(issues, f"assets.{asset_id}.path", "production-ready asset path must resolve to a project file", "quality")
                elif isinstance(asset.get("hash"), str) and asset["hash"].strip() and _file_sha256(resolved) != asset["hash"].lower():
                    fail(issues, f"assets.{asset_id}.hash", "production-ready asset hash does not match the project file", "quality")
    responsive = gates.get("responsivePolicy")
    if not isinstance(responsive, dict):
        fail(issues, "qualityGates.responsivePolicy", "must declare CanvasScaler, reference resolution, safe area and reflow", "quality")
    else:
        for field in ("canvasRenderMode", "canvasScalerMode", "referenceResolution", "matchWidthOrHeight", "safeAreaPolicy", "reflowPolicy", "longContentPolicy"):
            if field not in responsive:
                fail(issues, f"qualityGates.responsivePolicy.{field}", "required responsive decision is missing", "quality")
        if responsive.get("uniformScaleOnly") is True:
            fail(issues, "qualityGates.responsivePolicy.uniformScaleOnly", "uniform scaling alone is not an acceptable responsive strategy", "quality")
        declared_profiles = responsive.get("profileIds")
        expected_profiles = {item.get("id") for item in profiles if isinstance(item, dict)}
        if set(declared_profiles or []) != expected_profiles:
            fail(issues, "qualityGates.responsivePolicy.profileIds", "must exactly match ScreenSpec profiles", "quality")
        if responsive.get("safeAreaPolicy") == "profile-safe-area-inset":
            for index, profile in enumerate(profiles):
                safe_area = profile.get("safeArea") if isinstance(profile, dict) else None
                if not isinstance(safe_area, list) or len(safe_area) != 4 or any(not isinstance(value, (int, float)) or value < 0 or value > 1 for value in safe_area) or safe_area[0] > safe_area[2] or safe_area[1] > safe_area[3]:
                    fail(issues, f"profiles[{index}].safeArea", "profile-safe-area-inset requires normalized safeArea bounds", "quality")
    color_policy = gates.get("colorPolicy")
    if not isinstance(color_policy, dict):
        fail(issues, "qualityGates.colorPolicy", "must declare semantic roles and non-color state signals", "quality")
    else:
        roles = color_policy.get("tokenRoles")
        if not isinstance(roles, list) or not {"surface", "text", "action", "feedback", "focus"}.issubset(set(roles)):
            fail(issues, "qualityGates.colorPolicy.tokenRoles", "must include surface, text, action, feedback and focus roles", "quality")
        signals = color_policy.get("nonColorStateSignals")
        if not isinstance(signals, dict) or any(not isinstance(value, list) or not value for value in signals.values()):
            fail(issues, "qualityGates.colorPolicy.nonColorStateSignals", "each state must have a structural, textual or icon signal", "quality")
        if isinstance(tokens, dict):
            for token_name in ("background", "surface", "text", "accent", "danger", "onAccent", "onDanger"):
                value = tokens.get(token_name)
                if not isinstance(value, str) or not HEX_RE.fullmatch(value):
                    fail(issues, f"tokens.{token_name}", "quality gates require explicit six-digit hex colors", "quality")
            if all(isinstance(tokens.get(name), str) and HEX_RE.fullmatch(tokens[name]) for name in ("background", "text")):
                minimum = float(color_policy.get("minimumTextContrast", 4.5))
                if contrast_ratio(tokens["background"], tokens["text"]) < minimum:
                    fail(issues, "qualityGates.colorPolicy.minimumTextContrast", "background/text contrast is below the declared threshold", "quality")
            for foreground, background, check_id in (("onAccent", "accent", "accent-foreground"), ("onDanger", "danger", "danger-foreground")):
                if all(isinstance(tokens.get(name), str) and HEX_RE.fullmatch(tokens[name]) for name in (foreground, background)):
                    if contrast_ratio(tokens[foreground], tokens[background]) < minimum:
                        fail(issues, f"qualityGates.colorPolicy.{check_id}", f"{foreground}/{background} contrast is below the declared threshold", "quality")
    typography = gates.get("typographyPolicy")
    if not isinstance(typography, dict):
        fail(issues, "qualityGates.typographyPolicy", "must declare Font Asset identity, glyph coverage and fallback chain", "quality")
    else:
        for field in ("fontAssetId", "fontAssetPath", "fontAssetHash", "license", "fallbackFontAssetIds", "requiredCharacters", "localeFixtures", "overflowPolicy"):
            value = typography.get(field)
            if field in {"fallbackFontAssetIds", "requiredCharacters", "localeFixtures"}:
                if not isinstance(value, list) or not value:
                    fail(issues, f"qualityGates.typographyPolicy.{field}", "must be a non-empty array", "quality")
            elif not isinstance(value, str) or not value.strip():
                fail(issues, f"qualityGates.typographyPolicy.{field}", "must be a non-empty string", "quality")
        distinct_fallback_ids = [value for value in typography.get("fallbackFontAssetIds", []) if isinstance(value, str) and value != typography.get("fontAssetId")]
        if distinct_fallback_ids:
            fallback_assets = typography.get("fallbackFontAssets")
            if not isinstance(fallback_assets, list):
                fail(issues, "qualityGates.typographyPolicy.fallbackFontAssets", "distinct fallback IDs require path/hash/license metadata", "quality")
            else:
                by_id = {item.get("id"): item for item in fallback_assets if isinstance(item, dict)}
                for fallback_id in distinct_fallback_ids:
                    record = by_id.get(fallback_id)
                    if not isinstance(record, dict):
                        fail(issues, f"qualityGates.typographyPolicy.fallbackFontAssets[{fallback_id}]", "fallback asset metadata is missing", "quality")
                        continue
                    for field in ("id", "path", "hash", "license"):
                        if not isinstance(record.get(field), str) or not record[field].strip():
                            fail(issues, f"qualityGates.typographyPolicy.fallbackFontAssets[{fallback_id}].{field}", "fallback metadata field is required", "quality")
                    fallback_path = _project_file(base_dir, record.get("path"))
                    if fallback_path is None or not fallback_path.is_file():
                        fail(issues, f"qualityGates.typographyPolicy.fallbackFontAssets[{fallback_id}].path", "fallback path must resolve to a project file", "quality")
                    elif isinstance(record.get("hash"), str) and _file_sha256(fallback_path) != record["hash"].lower():
                        fail(issues, f"qualityGates.typographyPolicy.fallbackFontAssets[{fallback_id}].hash", "fallback hash does not match the project file", "quality")
        font_path = _project_file(base_dir, typography.get("fontAssetPath"))
        if font_path is None or not font_path.is_file():
            fail(issues, "qualityGates.typographyPolicy.fontAssetPath", "fontAssetPath must resolve to a project file", "quality")
        elif isinstance(typography.get("fontAssetHash"), str) and _file_sha256(font_path) != typography["fontAssetHash"].lower():
            fail(issues, "qualityGates.typographyPolicy.fontAssetHash", "fontAssetHash does not match the project file", "quality")


def validate_design_contract(spec: dict[str, Any], profiles: list[Any], tokens: Any, components: list[Any], issues: list[dict[str, str]], required: bool) -> None:
    """Validate the explicit design decisions that adapters must not synthesize silently."""
    contract = spec.get("designContract")
    if not isinstance(contract, dict):
        if required:
            fail(issues, "designContract", "strict quality validation requires explicit Canvas, anchor, color, typography and layer contracts", "design-contract")
        return
    if contract.get("coordinateSpace") != "screen-top-left-normalized":
        fail(issues, "designContract.coordinateSpace", "must declare screen-top-left-normalized coordinates", "design-contract")
    canvas = contract.get("canvas")
    if not isinstance(canvas, dict):
        fail(issues, "designContract.canvas", "must declare root Canvas ownership and nested Canvas policy", "design-contract")
    else:
        for field in ("rootRole", "renderMode", "scalerMode", "singleRoot", "nestedCanvasPolicy"):
            if field not in canvas:
                fail(issues, f"designContract.canvas.{field}", "Canvas contract field is required", "design-contract")
        if canvas.get("singleRoot") is not True:
            fail(issues, "designContract.canvas.singleRoot", "the default UI screen must declare one root Canvas", "design-contract")
    anchors = contract.get("anchorPolicy")
    if not isinstance(anchors, dict):
        fail(issues, "designContract.anchorPolicy", "must declare default pivot, allowed strategies and safe-area target", "design-contract")
    else:
        pivot = anchors.get("defaultPivot")
        if not isinstance(pivot, list) or len(pivot) != 2 or any(not isinstance(value, (int, float)) or value < 0 or value > 1 for value in pivot):
            fail(issues, "designContract.anchorPolicy.defaultPivot", "must be a normalized [x,y] pivot", "design-contract")
        strategies = anchors.get("allowedStrategies")
        if not isinstance(strategies, list) or not strategies:
            fail(issues, "designContract.anchorPolicy.allowedStrategies", "must list supported anchor strategies", "design-contract")
        if not isinstance(anchors.get("safeAreaTarget"), str) or not anchors["safeAreaTarget"].strip():
            fail(issues, "designContract.anchorPolicy.safeAreaTarget", "must identify the safe-area owner", "design-contract")
    allowed_strategies = set(anchors.get("allowedStrategies", [])) if isinstance(anchors, dict) and isinstance(anchors.get("allowedStrategies"), list) else set()
    color_roles = contract.get("colorRoles")
    if not isinstance(color_roles, dict):
        fail(issues, "designContract.colorRoles", "must map primary, feedback and foreground roles to declared tokens", "design-contract")
    else:
        for role in ("primaryAction", "secondaryAction", "feedback", "foregroundOnAccent", "foregroundOnDanger"):
            token = color_roles.get(role)
            if not isinstance(token, str) or token not in (tokens if isinstance(tokens, dict) else {}):
                fail(issues, f"designContract.colorRoles.{role}", "must reference a declared color token", "design-contract")
    typography = contract.get("typographyRoles")
    if not isinstance(typography, dict):
        fail(issues, "designContract.typographyRoles", "must declare title/body/label/caption/numeric roles", "design-contract")
    else:
        for role in ("title", "body", "label", "caption", "numeric"):
            entry = typography.get(role)
            if not isinstance(entry, dict) or not isinstance(entry.get("token"), str) or entry["token"] not in (tokens if isinstance(tokens, dict) else {}):
                fail(issues, f"designContract.typographyRoles.{role}", "must reference a declared size token", "design-contract")
    layers = contract.get("layerRoles")
    if not isinstance(layers, dict) or not {"background", "information", "feedback", "action"}.issubset(layers):
        fail(issues, "designContract.layerRoles", "must declare background, information, feedback and action ordering", "design-contract")
    elif any(not isinstance(layers[role], int) for role in ("background", "information", "feedback", "action")) or not (layers["background"] < layers["information"] < layers["feedback"] < layers["action"]):
        fail(issues, "designContract.layerRoles", "must use strictly increasing background < information < feedback < action order", "layer-order")
    allowed_typography = set(typography.keys()) if isinstance(typography, dict) else set()
    primary_intent = spec.get("intentContract", {}).get("requestedPrimaryIntent") if isinstance(spec.get("intentContract"), dict) else None
    primary_token = color_roles.get("primaryAction") if isinstance(color_roles, dict) else None
    profile_ids = {str(profile.get("id")).lower() for profile in profiles if isinstance(profile, dict) and profile.get("id")}
    if not profile_ids:
        profile_ids = {"__default"}

    def near(value: float, expected: float, tolerance: float = 0.08) -> bool:
        return abs(value - expected) <= tolerance

    def validate_anchor_geometry(component: dict[str, Any], component_path: str, layout: dict[str, Any], anchor: dict[str, Any], strategy: str, is_root: bool) -> None:
        bounds = layout.get("bounds")
        if not isinstance(bounds, list) or len(bounds) != 4 or any(not isinstance(value, (int, float)) for value in bounds):
            return
        left, top, right, bottom = (float(value) for value in bounds)
        pivot = anchor.get("pivot")
        edge = str(anchor.get("edge") or layout.get("anchorEdge") or "none").lower()
        if strategy not in allowed_strategies:
            fail(issues, f"{component_path}.layout.anchor.strategy", "must be registered by designContract.anchorPolicy.allowedStrategies", "anchor-geometry")
        if strategy == "edge-docked":
            edge_requirements = {
                "left": near(left, 0), "right": near(right, 1), "top": near(top, 0), "bottom": near(bottom, 1),
                "top-left": near(left, 0) and near(top, 0), "top-right": near(right, 1) and near(top, 0),
                "bottom-left": near(left, 0) and near(bottom, 1), "bottom-right": near(right, 1) and near(bottom, 1),
            }
            if edge not in edge_requirements or not edge_requirements[edge]:
                fail(issues, f"{component_path}.layout.bounds", "edge-docked bounds must remain near the declared edge", "anchor-geometry")
        if strategy == "center" and isinstance(pivot, list) and len(pivot) == 2:
            centered_x = near((left + right) * 0.5, 0.5)
            centered_y = near((top + bottom) * 0.5, 0.5)
            if not (centered_x or centered_y) or not near(float(pivot[0]), 0.5, 0.001) or not near(float(pivot[1]), 0.5, 0.001):
                fail(issues, f"{component_path}.layout.anchor", "center strategy requires a centered axis and [0.5,0.5] pivot", "anchor-geometry")
        if strategy == "stretch" and not (near(left, 0) and near(right, 1) or near(top, 0) and near(bottom, 1)):
            fail(issues, f"{component_path}.layout.bounds", "stretch strategy must span a complete local axis", "anchor-geometry")
        safe_policy = anchor.get("safeArea")
        if safe_policy not in {"inside", "inherit", "ignore"}:
            fail(issues, f"{component_path}.layout.anchor.safeArea", "must be inside, inherit, or ignore", "anchor-geometry")
        elif is_root and safe_policy == "inherit":
            fail(issues, f"{component_path}.layout.anchor.safeArea", "top-level components cannot inherit an unspecified safe-area owner", "anchor-geometry")
        elif safe_policy == "ignore" and (not is_root or str(component.get("visualVariant", "")).lower() != "background" or strategy != "stretch"):
            fail(issues, f"{component_path}.layout.anchor.safeArea", "safeArea ignore is reserved for a top-level stretch background; content must remain inside or inherit", "safe-area-policy")

    def visit(nodes: Any, path: str, is_root: bool = False) -> None:
        if not isinstance(nodes, list):
            return
        sibling_layers: dict[str, list[tuple[int, int, str, str]]] = {profile_id: [] for profile_id in profile_ids}
        sibling_orders: dict[str, set[int]] = {profile_id: set() for profile_id in profile_ids}
        for index, component in enumerate(nodes):
            if not isinstance(component, dict):
                continue
            component_path = f"{path}[{index}]"
            layout = component.get("layout") if isinstance(component.get("layout"), dict) else {}
            anchor = layout.get("anchor") if isinstance(layout.get("anchor"), dict) else {}
            strategy = anchor.get("strategy") or layout.get("anchorStrategy")
            if required and not isinstance(layout.get("anchor"), dict):
                fail(issues, f"{component_path}.layout.anchor", "quality-gated components must declare strategy, edge, pivot and safe-area policy", "design-contract")
            if not isinstance(strategy, str) or not strategy.strip():
                fail(issues, f"{component_path}.layout.anchorStrategy", "components must declare an anchor strategy", "design-contract")
            if strategy == "edge-docked" and not (anchor.get("edge") or layout.get("anchorEdge")):
                fail(issues, f"{component_path}.layout.anchorEdge", "edge-docked components must declare left/right/top/bottom edge", "design-contract")
            pivot = anchor.get("pivot")
            if required and (not isinstance(pivot, list) or len(pivot) != 2 or any(not isinstance(value, (int, float)) or value < 0 or value > 1 for value in pivot)):
                fail(issues, f"{component_path}.layout.anchor.pivot", "must be an explicit normalized [x,y] pivot", "design-contract")
            if required and not isinstance(anchor.get("safeArea"), str):
                fail(issues, f"{component_path}.layout.anchor.safeArea", "must explicitly declare inside/inherit safe-area policy", "design-contract")
            if isinstance(strategy, str) and strategy.strip():
                validate_anchor_geometry(component, component_path, layout, anchor, strategy, is_root)
            color_token = component.get("colorToken")
            if required and (not isinstance(color_token, str) or color_token not in (tokens if isinstance(tokens, dict) else {})):
                fail(issues, f"{component_path}.colorToken", "must reference a declared color token", "design-contract")
            typography_role = component.get("typographyRole")
            if required and (not isinstance(typography_role, str) or (typography_role != "none" and typography_role not in allowed_typography)):
                fail(issues, f"{component_path}.typographyRole", "must reference a declared typography role or none", "design-contract")
            if not isinstance(component.get("layerRole"), str) or component["layerRole"] not in (layers if isinstance(layers, dict) else {}):
                fail(issues, f"{component_path}.layerRole", "components must bind to a declared layer role", "design-contract")
            if not isinstance(component.get("siblingOrder"), int) or component["siblingOrder"] < 0:
                fail(issues, f"{component_path}.siblingOrder", "components must declare deterministic sibling order", "design-contract")
            else:
                order = component["siblingOrder"]
                layer_role = component.get("layerRole")
                responsive_mode = str(layout.get("responsiveMode", component.get("responsiveMode", "both"))).strip().lower()
                active_profiles = profile_ids if responsive_mode in {"", "both", "all"} else {responsive_mode}
                for profile_id in active_profiles & profile_ids:
                    if order in sibling_orders[profile_id]:
                        fail(issues, f"{component_path}.siblingOrder", f"active siblings must not share deterministic render order in profile '{profile_id}'", "layer-order")
                    sibling_orders[profile_id].add(order)
                    if isinstance(layer_role, str) and isinstance(layers, dict) and isinstance(layers.get(layer_role), int):
                        sibling_layers[profile_id].append((order, layers[layer_role], component_path, layer_role))
            interaction = component.get("interaction") if isinstance(component.get("interaction"), dict) else {}
            if isinstance(primary_intent, str) and interaction.get("intent") == primary_intent and component.get("colorToken") != primary_token:
                fail(issues, f"{component_path}.colorToken", "the requested primary action must consume designContract.colorRoles.primaryAction", "primary-action-color")
            visit(component.get("children"), f"{component_path}.children")
        for profile_id, active_siblings in sibling_layers.items():
            previous_layer: int | None = None
            for _, layer_value, component_path, _ in sorted(active_siblings):
                if previous_layer is not None and layer_value < previous_layer:
                    fail(issues, f"{component_path}.siblingOrder", f"siblingOrder must not place a lower visual layer above a higher one in profile '{profile_id}'", "layer-order")
                previous_layer = layer_value

    visit(components, "components", True)


def _profile_component_geometry(components: list[Any], profile_id: str) -> dict[str, dict[str, Any]]:
    """Resolve authored child-bounds into screen-normalized rectangles.

    This intentionally refuses to treat children of a layout group as static
    geometry truth. Their final rectangles belong to Unity's LayoutGroup and
    must be checked by the resolver/runtime evidence instead.
    """
    result: dict[str, dict[str, Any]] = {}

    def visit(nodes: Any, parent_rect: tuple[float, float, float, float], parent_owner: str) -> None:
        if not isinstance(nodes, list):
            return
        for component in nodes:
            if not isinstance(component, dict) or not _profile_active(component, profile_id):
                continue
            component_id = component.get("id")
            layout = component.get("layout") if isinstance(component.get("layout"), dict) else {}
            bounds = _bounds(component)
            if not isinstance(component_id, str) or bounds is None:
                continue
            left, top, right, bottom = bounds
            parent_left, parent_top, parent_right, parent_bottom = parent_rect
            parent_width = parent_right - parent_left
            parent_height = parent_bottom - parent_top
            rect = (
                parent_left + parent_width * left,
                parent_top + parent_height * top,
                parent_left + parent_width * right,
                parent_top + parent_height * bottom,
            )
            owner = str(layout.get("childGeometryOwner", "child-bounds"))
            result[component_id] = {
                "component": component,
                "rect": rect,
                "geometryReliable": parent_owner != "parent-layout-group",
            }
            visit(component.get("children"), rect, owner)

    visit(components, (0.0, 0.0, 1.0, 1.0), "child-bounds")
    return result


def _rect_edge(rect: tuple[float, float, float, float], axis: str, edge: str) -> float:
    if axis == "x":
        return {"start": rect[0], "end": rect[2], "center": (rect[0] + rect[2]) * 0.5}[edge]
    return {"start": rect[1], "end": rect[3], "center": (rect[1] + rect[3]) * 0.5}[edge]


def validate_advanced_composition(spec: dict[str, Any], profiles: list[Any], components: list[Any], tokens: Any, issues: list[dict[str, str]], required: bool) -> None:
    """Validate explicit high-fidelity composition decisions.

    These constraints do not score subjective beauty. They make the decisions
    that normally drift in AI-authored UI (hierarchy, focal protection, rhythm
    and responsive semantic equivalence) executable and falsifiable.
    """
    if not required:
        return
    contract = spec.get("designContract") if isinstance(spec.get("designContract"), dict) else {}
    advanced = contract.get("advancedComposition")
    if not isinstance(advanced, dict):
        fail(issues, "designContract.advancedComposition", "high-fidelity validation requires an explicit composition contract", "advanced-composition")
        return
    profile_ids = [str(profile.get("id")).lower() for profile in profiles if isinstance(profile, dict) and isinstance(profile.get("id"), str)]
    profile_sizes = {
        str(profile.get("id")).lower(): (float(profile.get("width", 0)), float(profile.get("height", 0)))
        for profile in profiles if isinstance(profile, dict) and isinstance(profile.get("id"), str)
    }
    by_id = collect_components_by_id(components)
    geometry = {profile_id: _profile_component_geometry(components, profile_id) for profile_id in profile_ids}
    color_roles = contract.get("colorRoles") if isinstance(contract.get("colorRoles"), dict) else {}
    primary_token = color_roles.get("primaryAction")
    primary_intent = spec.get("intentContract", {}).get("requestedPrimaryIntent") if isinstance(spec.get("intentContract"), dict) else None

    def profile_map(entry: dict[str, Any], path: str) -> dict[str, str]:
        mapping = entry.get("componentIdsByProfile")
        if not isinstance(mapping, dict):
            fail(issues, f"{path}.componentIdsByProfile", "must map every declared profile to one component id", "advanced-composition")
            return {}
        normalized: dict[str, str] = {}
        for profile_id in profile_ids:
            component_id = mapping.get(profile_id)
            if not isinstance(component_id, str) or not component_id:
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must identify a component for this profile", "advanced-composition")
                continue
            normalized[profile_id] = component_id
            if component_id not in by_id:
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must reference a declared component", "advanced-composition")
            elif component_id not in geometry.get(profile_id, {}):
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must be active in its declared profile", "advanced-composition")
        extras = sorted(set(mapping) - set(profile_ids))
        if extras:
            fail(issues, f"{path}.componentIdsByProfile", f"contains undeclared profile ids: {extras}", "advanced-composition")
        return normalized

    primary_actions = advanced.get("primaryActions")
    if not isinstance(primary_actions, list) or len(primary_actions) != 1:
        fail(issues, "designContract.advancedComposition.primaryActions", "must declare exactly one requested primary action", "visual-hierarchy")
        primary_actions = []
    primary_mappings: dict[str, dict[str, str]] = {}
    for index, action in enumerate(primary_actions):
        path = f"designContract.advancedComposition.primaryActions[{index}]"
        if not isinstance(action, dict):
            fail(issues, path, "must be an object", "visual-hierarchy")
            continue
        logical_id = action.get("logicalId")
        if not isinstance(logical_id, str) or not logical_id.strip():
            fail(issues, f"{path}.logicalId", "must be a stable logical action id", "visual-hierarchy")
            continue
        intent = action.get("intent")
        if intent != primary_intent:
            fail(issues, f"{path}.intent", "must equal intentContract.requestedPrimaryIntent", "visual-hierarchy")
        mapping = profile_map(action, path)
        primary_mappings[logical_id] = mapping
        for profile_id, component_id in mapping.items():
            component = by_id.get(component_id, {})
            interaction = component.get("interaction") if isinstance(component.get("interaction"), dict) else {}
            if interaction.get("intent") != primary_intent:
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must target the requested primary interaction", "visual-hierarchy")
            if component.get("colorToken") != primary_token:
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must consume the primary action color token", "visual-hierarchy")
            if component.get("layerRole") != "action":
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must be declared on the action layer", "visual-hierarchy")

    for profile_id, profile_geometry in geometry.items():
        permitted = {mapping.get(profile_id) for mapping in primary_mappings.values()}
        for component_id, record in profile_geometry.items():
            component = record["component"]
            interaction = component.get("interaction") if isinstance(component.get("interaction"), dict) else {}
            if component.get("colorToken") == primary_token and interaction.get("intent") and component_id not in permitted:
                fail(issues, f"components[{component_id}].colorToken", f"profile '{profile_id}' gives primary-action color to an undeclared competing action", "visual-hierarchy")

    focal_treatment = advanced.get("focalTreatment")
    focal_subjects = advanced.get("focalSubjects")
    focal_mappings: dict[str, dict[str, str]] = {}
    if focal_treatment not in {"subject", "none"}:
        fail(issues, "designContract.advancedComposition.focalTreatment", "must be 'subject' or 'none'", "focal-protection")
        focal_subjects = []
    if focal_treatment == "none":
        if not isinstance(advanced.get("noFocalReason"), str) or not advanced["noFocalReason"].strip():
            fail(issues, "designContract.advancedComposition.noFocalReason", "is required when the screen intentionally has no focal subject", "focal-protection")
        if focal_subjects not in ([], None):
            fail(issues, "designContract.advancedComposition.focalSubjects", "must be empty when focalTreatment is none", "focal-protection")
        focal_subjects = []
    elif not isinstance(focal_subjects, list) or not focal_subjects:
        fail(issues, "designContract.advancedComposition.focalSubjects", "must declare protected focal subjects", "focal-protection")
        focal_subjects = []
    for index, subject in enumerate(focal_subjects):
        path = f"designContract.advancedComposition.focalSubjects[{index}]"
        if not isinstance(subject, dict):
            fail(issues, path, "must be an object", "focal-protection")
            continue
        logical_id = subject.get("logicalId")
        if not isinstance(logical_id, str) or not logical_id.strip() or logical_id in focal_mappings:
            fail(issues, f"{path}.logicalId", "must be a unique stable focal-subject id", "focal-asset-policy")
            continue
        mapping = profile_map(subject, path)
        focal_mappings[logical_id] = mapping
        protected_from = subject.get("protectedFromPrimaryAction", True)
        if not isinstance(protected_from, bool):
            fail(issues, f"{path}.protectedFromPrimaryAction", "must be a boolean", "focal-protection")
            continue
        for profile_id, component_id in mapping.items():
            component = by_id.get(component_id, {})
            if not isinstance(component.get("assetSlots"), list) or not component["assetSlots"]:
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "focal subjects must bind an authored asset slot", "focal-protection")
            if component.get("type") not in {"image", "icon", "portrait"}:
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "focal subjects must use an image, icon or portrait component with a focal-cover renderer", "focal-materialization")
            if component.get("visualVariant") != "none":
                fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "focal subjects must use visualVariant 'none' so authored art is not token-tinted", "focal-materialization")
            if protected_from:
                action_id = next(iter(primary_mappings.values()), {}).get(profile_id)
                focal_record = geometry.get(profile_id, {}).get(component_id)
                action_record = geometry.get(profile_id, {}).get(action_id)
                if focal_record and action_record:
                    if not focal_record["geometryReliable"] or not action_record["geometryReliable"]:
                        fail(issues, path, f"profile '{profile_id}' focal/action protection cannot be asserted inside a parent layout group", "focal-protection")
                    elif _overlap(focal_record["rect"], action_record["rect"]):
                        fail(issues, path, f"profile '{profile_id}' primary action overlaps the protected focal subject", "focal-protection")

    # AssetManifest owns resource identity; this composition sub-contract binds
    # each focal subject to an explicit crop decision that must agree with it.
    focal_policies = advanced.get("focalAssetPolicies")
    if focal_treatment == "none":
        if focal_policies not in (None, []):
            fail(issues, "designContract.advancedComposition.focalAssetPolicies", "must be empty when focalTreatment is none", "focal-asset-policy")
    elif not isinstance(focal_policies, list) or not focal_policies:
        fail(issues, "designContract.advancedComposition.focalAssetPolicies", "must bind crop policy for every focal subject", "focal-asset-policy")
    else:
        assets = {
            asset.get("id"): asset
            for asset in spec.get("assets", [])
            if isinstance(asset, dict) and isinstance(asset.get("id"), str)
        }
        policies_by_logical_id: dict[str, dict[str, Any]] = {}
        for index, policy in enumerate(focal_policies):
            path = f"designContract.advancedComposition.focalAssetPolicies[{index}]"
            if not isinstance(policy, dict):
                fail(issues, path, "must be an object", "focal-asset-policy")
                continue
            logical_id = policy.get("logicalId")
            if not isinstance(logical_id, str) or logical_id not in focal_mappings or logical_id in policies_by_logical_id:
                fail(issues, f"{path}.logicalId", "must identify one declared focal subject exactly once", "focal-asset-policy")
                continue
            policies_by_logical_id[logical_id] = policy
            asset_ids = policy.get("assetIds")
            if not isinstance(asset_ids, list) or not asset_ids or any(not isinstance(asset_id, str) or not asset_id for asset_id in asset_ids) or len(set(asset_ids)) != len(asset_ids):
                fail(issues, f"{path}.assetIds", "must be a non-empty unique asset-id list", "focal-asset-policy")
                asset_ids = []
            crop_policy = policy.get("cropPolicy")
            if crop_policy not in {"focal-cover", "contain", "no-crop"}:
                fail(issues, f"{path}.cropPolicy", "must be focal-cover, contain or no-crop", "focal-asset-policy")
            focal_point = policy.get("focalPoint")
            insets = policy.get("safeCropInsetsNormalized")
            focal_valid = isinstance(focal_point, list) and len(focal_point) == 2 and all(isinstance(value, (int, float)) and not isinstance(value, bool) and 0 <= value <= 1 for value in focal_point)
            insets_valid = isinstance(insets, list) and len(insets) == 4 and all(isinstance(value, (int, float)) and not isinstance(value, bool) and 0 <= value < 1 for value in insets)
            if crop_policy == "focal-cover":
                if not focal_valid:
                    fail(issues, f"{path}.focalPoint", "focal-cover requires a normalized [x, y] focal point", "focal-asset-policy")
                if not insets_valid or float(insets[0]) + float(insets[2]) >= 1 or float(insets[1]) + float(insets[3]) >= 1:
                    fail(issues, f"{path}.safeCropInsetsNormalized", "focal-cover requires four normalized insets that leave a positive safe region", "focal-asset-policy")
            elif focal_point is not None and not focal_valid:
                fail(issues, f"{path}.focalPoint", "when present, focalPoint must be normalized [x, y]", "focal-asset-policy")
            for asset_id in asset_ids:
                asset = assets.get(asset_id)
                if asset is None:
                    fail(issues, f"{path}.assetIds", f"references undeclared asset '{asset_id}'", "focal-asset-policy")
                    continue
                if asset.get("cropPolicy") != crop_policy:
                    fail(issues, f"{path}.cropPolicy", f"must match AssetManifest cropPolicy for '{asset_id}'", "focal-asset-policy")
                if crop_policy == "focal-cover" and asset.get("focalPoint") != focal_point:
                    fail(issues, f"{path}.focalPoint", f"must match AssetManifest focalPoint for '{asset_id}'", "focal-asset-policy")
                if crop_policy == "focal-cover":
                    source_aspect = asset.get("sourceAspectRatio")
                    if not isinstance(source_aspect, (int, float)) or isinstance(source_aspect, bool) or not math.isfinite(float(source_aspect)) or float(source_aspect) <= 0:
                        fail(issues, f"assets.{asset_id}.sourceAspectRatio", "focal-cover requires a positive finite source aspect ratio for profile feasibility checks", "focal-asset-policy")
                    if asset.get("atlasRotationPolicy") != "disallow-rotation":
                        fail(issues, f"assets.{asset_id}.atlasRotationPolicy", "focal-cover requires disallow-rotation because RawImage cover UVs cannot represent rotated SpriteAtlas packing", "focal-asset-policy")
        for logical_id, mapping in focal_mappings.items():
            policy = policies_by_logical_id.get(logical_id)
            if policy is None:
                continue
            expected_assets = {
                asset_id
                for component_id in mapping.values()
                for asset_id in by_id.get(component_id, {}).get("assetSlots", [])
                if isinstance(asset_id, str)
            }
            actual_assets = set(policy.get("assetIds", [])) if isinstance(policy.get("assetIds"), list) else set()
            if actual_assets != expected_assets:
                fail(issues, f"designContract.advancedComposition.focalAssetPolicies[{logical_id}].assetIds", "must cover exactly the focal subject asset slots across profiles", "focal-asset-policy")

    alignment_groups = advanced.get("alignmentGroups")
    if not isinstance(alignment_groups, list) or not alignment_groups:
        fail(issues, "designContract.advancedComposition.alignmentGroups", "must declare at least one key alignment relationship", "layout-rhythm")
        alignment_groups = []
    for index, group in enumerate(alignment_groups):
        path = f"designContract.advancedComposition.alignmentGroups[{index}]"
        if not isinstance(group, dict):
            fail(issues, path, "must be an object", "layout-rhythm")
            continue
        profile_id, axis, edge = group.get("profileId"), group.get("axis"), group.get("edge")
        component_ids = group.get("componentIds")
        tolerance = group.get("tolerancePx")
        if profile_id not in geometry or axis not in {"x", "y"} or edge not in {"start", "end", "center"}:
            fail(issues, path, "must use a declared profile, x/y axis and start/end/center edge", "layout-rhythm")
            continue
        if not isinstance(component_ids, list) or len(component_ids) < 2 or any(not isinstance(item, str) for item in component_ids):
            fail(issues, f"{path}.componentIds", "must contain at least two component ids", "layout-rhythm")
            continue
        if isinstance(tolerance, bool) or not isinstance(tolerance, (int, float)) or tolerance < 0 or tolerance > 8:
            fail(issues, f"{path}.tolerancePx", "must be a 0..8 pixel tolerance", "layout-rhythm")
            continue
        records = [geometry[profile_id].get(component_id) for component_id in component_ids]
        if any(record is None for record in records):
            fail(issues, f"{path}.componentIds", "must reference active components in the selected profile", "layout-rhythm")
            continue
        if any(not record["geometryReliable"] for record in records if record):
            fail(issues, path, "cannot assert authored alignment inside a parent layout group", "layout-rhythm")
            continue
        dimension = profile_sizes[profile_id][0 if axis == "x" else 1]
        coordinates = [_rect_edge(record["rect"], axis, edge) * dimension for record in records if record]
        if max(coordinates) - min(coordinates) > float(tolerance) + 1e-4:
            fail(issues, path, f"declared {axis}/{edge} alignment exceeds {tolerance}px", "layout-rhythm")

    clearance_constraints = advanced.get("clearanceConstraints")
    if not isinstance(clearance_constraints, list) or not clearance_constraints:
        fail(issues, "designContract.advancedComposition.clearanceConstraints", "must declare at least one key clearance relationship", "layout-clearance")
        clearance_constraints = []
    for index, constraint in enumerate(clearance_constraints):
        path = f"designContract.advancedComposition.clearanceConstraints[{index}]"
        if not isinstance(constraint, dict):
            fail(issues, path, "must be an object", "layout-clearance")
            continue
        profile_id = constraint.get("profileId")
        axis, relation = constraint.get("axis"), constraint.get("relation")
        first_id, second_id = constraint.get("firstComponentId"), constraint.get("secondComponentId")
        minimum = constraint.get("minGapPx")
        if profile_id not in geometry or axis not in {"x", "y"} or relation not in {"before", "after"}:
            fail(issues, path, "must use a declared profile, x/y axis and before/after relation", "layout-clearance")
            continue
        if not isinstance(first_id, str) or not isinstance(second_id, str) or first_id == second_id:
            fail(issues, path, "must name two distinct components", "layout-clearance")
            continue
        if isinstance(minimum, bool) or not isinstance(minimum, (int, float)) or minimum < 0:
            fail(issues, f"{path}.minGapPx", "must be a non-negative pixel gap", "layout-clearance")
            continue
        first, second = geometry[profile_id].get(first_id), geometry[profile_id].get(second_id)
        if first is None or second is None:
            fail(issues, path, "must reference active components in the selected profile", "layout-clearance")
            continue
        if not first["geometryReliable"] or not second["geometryReliable"]:
            fail(issues, path, "cannot assert authored clearance inside a parent layout group", "layout-clearance")
            continue
        dimension = profile_sizes[profile_id][0 if axis == "x" else 1]
        first_edge = first["rect"][2 if axis == "x" else 3]
        second_edge = second["rect"][0 if axis == "x" else 1]
        gap = (second_edge - first_edge) * dimension if relation == "before" else (first["rect"][0 if axis == "x" else 1] - second["rect"][2 if axis == "x" else 3]) * dimension
        if gap + 1e-4 < float(minimum):
            fail(issues, path, f"declared clearance is {gap:.2f}px, below {minimum}px", "layout-clearance")

    equivalences = advanced.get("responsiveEquivalences")
    if not isinstance(equivalences, list) or not equivalences:
        fail(issues, "designContract.advancedComposition.responsiveEquivalences", "must map key semantics across every profile", "responsive-equivalence")
        equivalences = []
    seen_logical_ids: set[str] = set()
    equivalence_mappings: dict[str, dict[str, str]] = {}
    for index, equivalence in enumerate(equivalences):
        path = f"designContract.advancedComposition.responsiveEquivalences[{index}]"
        if not isinstance(equivalence, dict):
            fail(issues, path, "must be an object", "responsive-equivalence")
            continue
        logical_id = equivalence.get("logicalId")
        if not isinstance(logical_id, str) or not logical_id.strip() or logical_id in seen_logical_ids:
            fail(issues, f"{path}.logicalId", "must be a unique logical semantic id", "responsive-equivalence")
            continue
        seen_logical_ids.add(logical_id)
        mapping = profile_map(equivalence, path)
        equivalence_mappings[logical_id] = mapping
        intent = equivalence.get("intent")
        if intent is not None:
            for profile_id, component_id in mapping.items():
                interaction = by_id.get(component_id, {}).get("interaction")
                if not isinstance(interaction, dict) or interaction.get("intent") != intent:
                    fail(issues, f"{path}.componentIdsByProfile.{profile_id}", "must preserve the declared interaction intent", "responsive-equivalence")
    for logical_id, mapping in primary_mappings.items():
        if equivalence_mappings.get(logical_id) != mapping:
            fail(issues, "designContract.advancedComposition.responsiveEquivalences", "must include the primary action with the same per-profile mapping", "responsive-equivalence")

    density = advanced.get("interactionDensity")
    groups = density.get("groups") if isinstance(density, dict) else None
    if not isinstance(groups, list) or not groups:
        fail(issues, "designContract.advancedComposition.interactionDensity.groups", "must declare interaction-density groups resolved after LayoutGroup placement", "interaction-density")
        return
    seen_group_ids: set[str] = set()
    for index, group in enumerate(groups):
        path = f"designContract.advancedComposition.interactionDensity.groups[{index}]"
        if not isinstance(group, dict):
            fail(issues, path, "must be an object", "interaction-density")
            continue
        group_id, profile_id, component_ids = group.get("id"), group.get("profileId"), group.get("componentIds")
        if not isinstance(group_id, str) or not group_id.strip() or group_id in seen_group_ids:
            fail(issues, f"{path}.id", "must be a unique stable group id", "interaction-density")
        else:
            seen_group_ids.add(group_id)
        if profile_id not in geometry:
            fail(issues, f"{path}.profileId", "must name a declared profile", "interaction-density")
        if not isinstance(component_ids, list) or len(component_ids) < 2 or any(not isinstance(component_id, str) or not component_id for component_id in component_ids) or len(set(component_ids)) != len(component_ids):
            fail(issues, f"{path}.componentIds", "must contain at least two unique interactive component ids", "interaction-density")
            component_ids = []
        maximum = group.get("maxTargets")
        if isinstance(maximum, bool) or not isinstance(maximum, int) or maximum < 1:
            fail(issues, f"{path}.maxTargets", "must be a positive integer", "interaction-density")
        elif len(component_ids) > maximum:
            fail(issues, f"{path}.componentIds", "cannot exceed maxTargets", "interaction-density")
        minimum_gap = group.get("minGapPx")
        if isinstance(minimum_gap, bool) or not isinstance(minimum_gap, (int, float)) or minimum_gap < 0 or minimum_gap > 256:
            fail(issues, f"{path}.minGapPx", "must be a 0..256 pixel gap", "interaction-density")
        for component_id in component_ids:
            component = by_id.get(component_id)
            interaction = component.get("interaction") if isinstance(component, dict) and isinstance(component.get("interaction"), dict) else None
            target = interaction.get("targetSize") if isinstance(interaction, dict) else None
            if component is None or profile_id not in geometry or component_id not in geometry[profile_id]:
                fail(issues, f"{path}.componentIds", f"'{component_id}' must be active in profile '{profile_id}'", "interaction-density")
            elif not isinstance(target, list) or len(target) != 2 or any(isinstance(value, bool) or not isinstance(value, (int, float)) or value <= 0 for value in target):
                fail(issues, f"{path}.componentIds", f"'{component_id}' must declare an interaction targetSize", "interaction-density")


def _profile_active(component: dict[str, Any], profile_id: str) -> bool:
    layout = component.get("layout") if isinstance(component.get("layout"), dict) else {}
    authored = str(layout.get("responsiveMode", component.get("responsiveMode", "both"))).strip().lower()
    if authored in {"", "both", "all"}:
        return True
    return authored == profile_id.lower()


def _bounds(component: dict[str, Any]) -> tuple[float, float, float, float] | None:
    layout = component.get("layout") if isinstance(component.get("layout"), dict) else {}
    value = layout.get("bounds")
    if not isinstance(value, list) or len(value) != 4:
        return None
    try:
        return tuple(float(item) for item in value)  # type: ignore[return-value]
    except (TypeError, ValueError):
        return None


def _overlap(first: tuple[float, float, float, float], second: tuple[float, float, float, float], epsilon: float = 1e-6) -> bool:
    width = min(first[2], second[2]) - max(first[0], second[0])
    height = min(first[3], second[3]) - max(first[1], second[1])
    return width > epsilon and height > epsilon


def validate_layout_plan(components: list[Any], profiles: list[Any], issues: list[dict[str, str]], required: bool) -> None:
    """Reject geometry contradictions before Unity can materialize them.

    Bounds are local to the component parent. A parent-layout-group owns child
    geometry, so authored child rectangles are preserved as semantic hints but
    are not treated as collision truth.
    """
    if not required:
        return
    profile_dimensions = {
        str(item.get("id")): (int(item.get("width")), int(item.get("height")))
        for item in profiles
        if isinstance(item, dict) and item.get("id") and isinstance(item.get("width"), int) and isinstance(item.get("height"), int)
    }

    def visit(
        nodes: list[Any],
        path: str,
        parent_size: tuple[float, float] | None = None,
        profile_id: str | None = None,
        parent_geometry_owner: str = "",
    ) -> None:
        # Resolve one profile per traversal. Recursing from a wide parent into
        # narrow siblings was previously mixing separate responsive branches and
        # reporting false overlap conflicts.
        if profile_id is None:
            for current_profile in profile_dimensions:
                visit(nodes, path, parent_size, current_profile, parent_geometry_owner)
            return
        valid_nodes = [node for node in nodes if isinstance(node, dict)]
        profile_width, profile_height = profile_dimensions[profile_id]
        active = [node for node in valid_nodes if _profile_active(node, profile_id)]
        for index, node in enumerate(active):
            rect = _bounds(node)
            if rect is None:
                continue
            layout = node.get("layout") if isinstance(node.get("layout"), dict) else {}
            min_size = layout.get("minSize", [0, 0])
            if parent_size is None:
                available = (profile_width * (rect[2] - rect[0]), profile_height * (rect[3] - rect[1]))
            else:
                available = (parent_size[0] * (rect[2] - rect[0]), parent_size[1] * (rect[3] - rect[1]))
            if isinstance(min_size, list) and len(min_size) == 2:
                if float(min_size[0]) > available[0] + 0.5 or float(min_size[1]) > available[1] + 0.5:
                    fail(issues, f"{path}[{index}].layout.minSize", f"profile '{profile_id}' cannot satisfy minSize within authored bounds", "layout-conflict")
            children = node.get("children", []) if isinstance(node.get("children"), list) else []
            owner = str(layout.get("childGeometryOwner", "child-bounds"))
            if children:
                visit(children, f"{path}[{index}].children", available, profile_id, owner)
            if str(layout.get("mode", "")) == "overlay":
                continue
            for other in active[index + 1:]:
                other_layout = other.get("layout") if isinstance(other.get("layout"), dict) else {}
                if str(other_layout.get("mode", "")) == "overlay":
                    continue
                if str(node.get("visualVariant", "")).lower() == "background" or str(other.get("visualVariant", "")).lower() == "background":
                    continue
                # Sibling bounds are only collision truth when this parent
                # explicitly owns children through a layout group.
                if parent_geometry_owner == "parent-layout-group" or owner == "parent-layout-group" or str(other_layout.get("childGeometryOwner", "")) == "parent-layout-group":
                    continue
                other_rect = _bounds(other)
                if rect and other_rect and _overlap(rect, other_rect):
                    fail(issues, f"{path}[{index}].layout.bounds", f"profile '{profile_id}' overlaps sibling '{other.get('id', '?')}'", "layout-conflict")

    visit(components, "components")


def validate(spec: Any, registry: dict[str, Any], require_feedback: bool = False, require_quality_gates: bool = False, require_advanced_composition: bool = False, base_dir: Path | None = None) -> list[dict[str, str]]:
    issues: list[dict[str, str]] = []
    if not isinstance(spec, dict):
        return [{"code": "type", "path": "$", "message": "ScreenSpec root must be an object"}]
    if spec.get("schemaVersion") != 3:
        fail(issues, "schemaVersion", "must equal 3", "schema")
    screen_id = non_empty_string(spec.get("screenId"), "screenId", issues)
    if screen_id and not ID_RE.fullmatch(screen_id):
        fail(issues, "screenId", "must use a stable lowercase id", "identity")
    screen_type = spec.get("screenType")
    if screen_type not in SCREEN_TYPES:
        fail(issues, "screenType", f"must be one of {sorted(SCREEN_TYPES)}", "template")
    template = spec.get("template", screen_type)
    if template not in registry.get("templates", {}):
        fail(issues, "template", "template is not registered", "registry")
    profiles = spec.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        fail(issues, "profiles", "must declare at least one profile", "responsive")
        profiles = []
    profile_ids: set[str] = set()
    for index, profile in enumerate(profiles):
        path = f"profiles[{index}]"
        if not isinstance(profile, dict):
            fail(issues, path, "profile must be an object", "type")
            continue
        profile_id = non_empty_string(profile.get("id"), f"{path}.id", issues)
        if profile_id in profile_ids:
            fail(issues, f"{path}.id", "profile id must be unique", "identity")
        if profile_id:
            profile_ids.add(profile_id)
        if not isinstance(profile.get("width"), int) or not isinstance(profile.get("height"), int) or profile["width"] <= 0 or profile["height"] <= 0:
            fail(issues, f"{path}.width/height", "must be positive integers", "responsive")
    states = spec.get("states")
    if not isinstance(states, list) or not states:
        fail(issues, "states", "must declare at least one state", "state")
        states = []
    state_ids: set[str] = set()
    for index, state in enumerate(states):
        path = f"states[{index}]"
        if not isinstance(state, dict):
            fail(issues, path, "state must be an object", "type")
            continue
        state_id = non_empty_string(state.get("id"), f"{path}.id", issues)
        if state_id in state_ids:
            fail(issues, f"{path}.id", "state id must be unique", "identity")
        if state_id:
            state_ids.add(state_id)
    assets_value = spec.get("assets", [])
    assets = assets_value if isinstance(assets_value, dict) else {item.get("id"): item for item in assets_value if isinstance(item, dict) and isinstance(item.get("id"), str)} if isinstance(assets_value, list) else {}
    if not isinstance(assets_value, (dict, list)):
        fail(issues, "assets", "must be an object or array", "asset")
    for asset_id, asset in assets.items():
        if not ID_RE.fullmatch(str(asset_id)) or not isinstance(asset, dict):
            fail(issues, f"assets.{asset_id}", "asset id and entry must be valid", "asset")
        elif asset.get("source") not in {"project-sprite", "ai-generated", "generated-procedural", "generated-placeholder"}:
            fail(issues, f"assets.{asset_id}.source", "must classify asset source", "asset")
    components = spec.get("components")
    if not isinstance(components, list) or not components:
        fail(issues, "components", "must contain at least one component", "structure")
        components = []
    seen: set[str] = set()
    for index, component in enumerate(components):
        validate_component(
            component,
            f"components[{index}]",
            registry,
            assets,
            state_ids,
            seen,
            issues,
            require_feedback or require_quality_gates,
            require_feedback or require_quality_gates,
        )
    template_definition = registry.get("templates", {}).get(template, {})
    declared_zones = {component.get("zone") for component in components if isinstance(component, dict) and isinstance(component.get("zone"), str)}
    for zone in template_definition.get("requiredZones", []):
        if zone not in declared_zones:
            fail(issues, "components", f"template requires zone '{zone}'", "template")
    behaviors = spec.get("behaviors", [])
    if not isinstance(behaviors, list):
        fail(issues, "behaviors", "must be an array", "behavior")
    component_ids = collect_component_ids(components)
    interaction_intents = collect_interaction_intents(components)
    validate_intent_contract(spec, interaction_intents, issues, require_quality_gates)
    validate_state_semantics(spec, state_ids, component_ids, issues, require_feedback)
    validate_fixture_text_bindings(spec, state_ids, components, issues, require_feedback)
    validate_state_variant_bindings(spec, state_ids, issues, require_feedback)
    validate_feedback(spec, issues, require_feedback, component_ids, profile_ids, state_ids)
    validate_profile_availability(spec, profile_ids, interaction_intents, issues, require_feedback)
    validate_layout_plan(spec["components"], profiles, issues, require_feedback or require_quality_gates)
    validate_design_contract(spec, profiles, spec.get("tokens"), components, issues, require_quality_gates)
    validate_advanced_composition(spec, profiles, components, spec.get("tokens"), issues, require_advanced_composition)
    if require_quality_gates:
        validate_quality_gates(spec, assets, profiles, spec.get("tokens"), issues, base_dir)
    return issues


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spec", type=Path)
    parser.add_argument("--registry", type=Path, default=Path(__file__).resolve().parents[1] / "references" / "game-ui-component-registry.json")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--require-feedback", action="store_true", help="require a structured feedback record in designEvidence")
    parser.add_argument("--require-quality-gates", action="store_true", help="require production contracts for assets, responsive layout, color and typography")
    parser.add_argument("--require-advanced-composition", action="store_true", help="require executable visual hierarchy, focal protection, layout rhythm and responsive equivalence constraints")
    args = parser.parse_args()
    try:
        project_root = next((parent for parent in [args.spec.resolve(), *args.spec.resolve().parents] if (parent / ".agents").is_dir() and (parent / "Assets").is_dir()), None)
        issues = validate(load(args.spec), load(args.registry), args.require_feedback, args.require_quality_gates, args.require_advanced_composition, project_root)
    except (OSError, UnicodeError, json.JSONDecodeError, TypeError, ValueError) as exc:
        issues = [{"code": "load", "path": "$", "message": str(exc)}]
    result = {"valid": not issues, "issueCount": len(issues), "issues": issues}
    print(json.dumps(result, ensure_ascii=False, indent=2) if args.json else ("PASS: generic ScreenSpec v3" if not issues else "FAIL: generic ScreenSpec v3 (" + str(len(issues)) + " issues)"))
    return 0 if not issues else 2


if __name__ == "__main__":
    raise SystemExit(main())
