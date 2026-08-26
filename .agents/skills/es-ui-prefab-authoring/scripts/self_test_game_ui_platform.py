#!/usr/bin/env python3
"""Exercise the generic ScreenSpec v3 registry with four game UI slices."""

from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile

from PIL import Image

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
from validate_game_ui_screen_spec import validate, validate_design_contract  # noqa: E402
from screen_spec_adapter import normalize  # noqa: E402
from ingest_ui_reference import analyze  # noqa: E402
from generate_ui_iteration_packet import bind_state_variants  # noqa: E402
from resolve_ui_layout_plan import resolve_children, resolve_fixture_text_fit, resolve_interaction_density, resolve_focal_crop_feasibility, state_geometry_contract  # noqa: E402
from validate_ui_snapshot_evidence import validate_snapshot_evidence  # noqa: E402
import validate_ui_gpu_evidence as gpu_evidence  # noqa: E402
from validate_ui_gpu_evidence import collect_png_stats, validate_gpu_evidence  # noqa: E402


def component(component_id: str, component_type: str, zone: str, *, text: str | None = None, value: float | None = None, asset: str | None = None, children: list[dict] | None = None, interactive: str | None = None) -> dict:
    node = {
        "id": component_id,
        "type": component_type,
        "zone": zone,
        "layout": {"mode": "stretch", "bounds": [0.02, 0.02, 0.98, 0.98], "minSize": [44, 44]},
        "content": {},
        "children": children or [],
        "stateVariants": {"default": {}, "disabled": {}}
    }
    if text is not None:
        node["content"]["text"] = text
    if value is not None:
        node["content"]["value"] = value
    if asset is not None:
        node["assetSlots"] = [asset]
    if interactive:
        node["interaction"] = {"intent": interactive, "targetSize": [44, 44]}
    return node


def spec(screen_id: str, screen_type: str, zones: list[dict]) -> dict:
    return {
        "schemaVersion": 3,
        "screenId": screen_id,
        "screenType": screen_type,
        "template": screen_type,
        "prefabPath": f"Assets/UI/Prefabs/Generated/{screen_id}.prefab",
        "fixtureScenePath": f"Assets/UI/Scenes/Generated/{screen_id}.unity",
        "tokens": {"surface": "#182230", "text": "#F4F7FB", "accent": "#53B8FF"},
        "profiles": [
            {"id": "wide", "width": 1920, "height": 1080, "orientation": "landscape"},
            {"id": "narrow", "width": 1080, "height": 1920, "orientation": "portrait"}
        ],
        "states": [{"id": state} for state in ("default", "selected", "empty", "loading", "disabled", "error")],
        "assets": [
            {"id": "frame", "source": "generated-placeholder", "path": "ES/UIAssets/common/frame.png"},
            {"id": "icon", "source": "generated-placeholder", "path": "ES/UIAssets/common/icon.png"},
            {"id": "portrait", "source": "generated-placeholder", "path": "ES/UIAssets/common/portrait.png"}
        ],
        "components": zones,
        "behaviors": [{"id": screen_id + ".input", "inputs": ["confirm", "cancel"], "transitions": []}],
        "bindings": []
    }


def build_samples() -> dict[str, dict]:
    return {
        "inventory": spec("inventory", "collection", [
            component("frame", "frame", "header", asset="frame", children=[component("title", "text", "header", text="Inventory")]),
            component("tabs", "tab-bar", "navigation", children=[component("items-tab", "button", "navigation", text="Items", interactive="switch-tab")], interactive="switch-tab"),
            component("grid", "grid", "content", children=[component("slot-01", "item-slot", "content", asset="icon", interactive="select-item")]),
            component("detail", "detail-panel", "content", children=[component("name", "text", "content", text="Astra Blade")])
        ]),
        "combat-hud": spec("combat-hud", "combat", [
            component("hud-frame", "frame", "safe-area", asset="frame"),
            component("health", "bar", "safe-area", value=1.0),
            component("skill", "cooldown", "action-bar", value=1.0, interactive="use-skill"),
            component("target", "target-panel", "safe-area", children=[component("target-name", "text", "safe-area", text="Training Target")])
        ]),
        "dialogue": spec("dialogue", "conversation", [
            component("dialogue-frame", "frame", "dialogue", asset="frame"),
            component("portrait-view", "portrait", "dialogue", asset="portrait"),
            component("speaker", "nameplate", "dialogue", text="Captain"),
            component("body", "text", "dialogue", text="We move at dawn."),
            component("choices", "choice-list", "choices", children=[component("choice-01", "button", "choices", text="Continue", interactive="choose-dialogue")], interactive="choose-dialogue")
        ]),
        "main-menu": spec("main-menu", "navigation", [
            component("menu-frame", "frame", "header", asset="frame"),
            component("menu-title", "text", "header", text="Main Menu"),
            component("menu-list", "list", "navigation", children=[component("start", "button", "navigation", text="Start", interactive="navigate-start")]),
            component("tooltip", "tooltip", "content", text="Select an option")
        ])
    }


def assert_adapter_contract() -> None:
    """Exercise fields where the Python and Unity adapters must stay identical."""
    flow_without_children = component("empty-flow", "list", "content")
    flow_without_children["layout"] = {"mode": "flow", "bounds": [0, 0, 1, 1], "minSize": [44, 44]}
    explicit = component("slider", "slider", "content", value=0.5, interactive="adjust")
    explicit["colorToken"] = "focus"
    explicit["layout"] = {
        "mode": "fixed", "bounds": [0, 0, 1, 1], "minSize": [44, 44],
        "preferredSize": [320, 48]
    }
    adapter_spec = spec("adapter-contract", "navigation", [flow_without_children, explicit])
    adapter_spec["intentContract"] = {
        "requestedScreenFamily": "navigation",
        "requestedPrimaryIntent": "adjust",
        "visualTarget": "original",
        "fidelityMode": "original",
        "referencePolicy": "not-required",
        "referenceSources": [],
        "productBoundary": "original",
    }
    adapter_spec["stateSemantics"] = {"default": {"affectedComponentIds": ["slider"]}}
    normalized = normalize(adapter_spec)
    if normalized["intentContract"]["requestedPrimaryIntent"] != "adjust":
        raise AssertionError("intentContract must survive normalization")
    if normalized["stateSemantics"]["default"]["affectedComponentIds"] != ["slider"]:
        raise AssertionError("stateSemantics must survive normalization")
    elements = {element["id"]: element for element in normalized["elements"]}
    if "layoutSpec" in elements["empty-flow"]:
        raise AssertionError("empty layout groups must not emit layoutSpec")
    slider = elements["slider"]
    if slider["kind"] != "slider":
        raise AssertionError("slider must preserve its semantic kind")
    if slider["colorToken"] != "focus":
        raise AssertionError("explicit colorToken must survive normalization")
    if slider["width"] != 320 or slider["height"] != 48:
        raise AssertionError("preferredSize must map to width/height")
    if not slider["hasValue"] or not slider["interactable"]:
        raise AssertionError("scalar value and structured interaction must be preserved")
    if slider["interaction"]["intent"] != "adjust":
        raise AssertionError("interaction intent must survive normalization")


def assert_reference_ingest_contract() -> None:
    """Ensure reference measurement records identity and bounded candidates."""
    with tempfile.TemporaryDirectory(prefix="es-ui-reference-") as temp_dir:
        path = Path(temp_dir) / "reference.png"
        image = Image.new("RGBA", (320, 180), (16, 24, 32, 255))
        for x in range(32, 288):
            for y in range(24, 92):
                image.putpixel((x, y), (64, 180, 210, 255))
        image.save(path)
        receipt = analyze(path, 128)
    if receipt["status"] != "candidate" or not receipt["regions"]:
        raise AssertionError("reference ingest must produce bounded candidate regions")
    source = receipt["source"]
    if source["width"] != 320 or source["height"] != 180 or len(source["sha256"]) != 64:
        raise AssertionError("reference ingest must preserve source identity and dimensions")
    if any(not region["reviewRequired"] for region in receipt["regions"]):
        raise AssertionError("reference regions must remain review-required candidates")


def assert_reference_intent_requires_source(registry: dict) -> None:
    """Reject a product-style request when its visual evidence is absent."""
    candidate = build_samples()["main-menu"]
    candidate["intentContract"] = {
        "requestedScreenFamily": "navigation",
        "requestedPrimaryIntent": "navigate-start",
        "visualTarget": "wangzhe-inspired-lobby",
        "fidelityMode": "reference-guided",
        "referencePolicy": "required",
        "referenceSources": [],
        "productBoundary": "inspired-style-with-original-assets",
    }
    candidate["designEvidence"] = {"referenceImages": []}
    issues = validate(candidate, registry, require_quality_gates=True)
    if not any(issue.get("code") == "intent-drift" for issue in issues):
        raise AssertionError("reference-guided requests without sources must be blocked as intent-drift")


def assert_state_binding_requires_variant(registry: dict) -> None:
    """Reject state prose that cannot be materialized by an affected node."""
    candidate = build_samples()["main-menu"]
    candidate["states"] = [{"id": "default"}, {"id": "long-content"}]
    candidate["stateSemantics"] = {
        "default": {
            "fixtureData": {"title": "Main Menu"},
            "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is visible"],
            "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True},
        },
        "long-content": {
            "fixtureData": {"title": "A very long title"},
            "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title wraps"],
            "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True},
        },
    }
    issues = validate(candidate, registry, require_feedback=True)
    if not any(issue.get("code") == "state-binding" for issue in issues):
        raise AssertionError("affected state components without matching stateVariants must be blocked")


def assert_state_binding_requires_semantic_owner(registry: dict) -> None:
    """Reject a component-only state that Fixture Driver will never execute."""
    candidate = build_samples()["main-menu"]
    candidate["states"] = [{"id": "default"}, {"id": "selected"}]
    candidate["stateSemantics"] = {
        "default": {
            "fixtureData": {"title": "Main Menu"},
            "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is visible"],
            "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True},
        },
        "selected": {
            "fixtureData": {"title": "Main Menu"},
            "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title receives focus"],
            "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True},
        },
    }
    candidate["components"][2]["stateVariants"] = {"default": {}, "selected": {}}
    issues = validate(candidate, registry, require_feedback=True)
    if not any(issue.get("code") == "state-binding" and "non-default stateVariant" in issue.get("message", "") for issue in issues):
        raise AssertionError("component-only non-default stateVariants must be blocked")


def assert_state_effects_are_executable(registry: dict) -> None:
    """Strict packets reject prose-only states and effects outside the execution set."""
    candidate = build_samples()["main-menu"]
    candidate["states"] = [{"id": "default"}, {"id": "selected"}]
    candidate["stateSemantics"] = {
        "default": {
            "fixtureData": {"title": "Main Menu"}, "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is visible"], "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True}, "effects": [{"componentId": "menu-title", "changes": {"graphicAlpha": 1.0}}],
        },
        "selected": {
            "fixtureData": {"title": "Main Menu"}, "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is focused"], "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True}, "effects": [{"componentId": "start", "changes": {"outline": True}}],
        },
    }
    candidate["components"][1]["stateVariants"] = {"default": {}, "selected": {}}
    issues = validate(candidate, registry, require_feedback=True)
    if not any(issue.get("code") == "state-effects" for issue in issues):
        raise AssertionError("state effects outside affectedComponentIds must be blocked")


def assert_state_geometry_is_immutable(registry: dict) -> None:
    """State labels must not be a backdoor around the resolved LayoutPlan."""
    candidate = build_samples()["main-menu"]
    candidate["states"] = [{"id": "default"}, {"id": "selected"}]
    candidate["stateSemantics"] = {
        "default": {
            "fixtureData": {"title": "Main Menu"}, "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is visible"], "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True}, "effects": [{"componentId": "menu-title", "changes": {"graphicAlpha": 1.0}}],
        },
        "selected": {
            "fixtureData": {"title": "Main Menu"}, "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is focused"], "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True, "allowedChanges": ["text-line-count"]}, "effects": [{"componentId": "menu-title", "changes": {"outline": True}}],
        },
    }
    candidate["components"][1]["stateVariants"] = {"default": {}, "selected": {"layout": {"bounds": [0, 0, 1, 1]}}}
    issues = validate(candidate, registry, require_feedback=True)
    if not any(issue.get("code") == "state-geometry" for issue in issues):
        raise AssertionError("state variant geometry must be rejected")
    _, resolver_issues = state_geometry_contract(candidate)
    if not any(issue.get("code") == "state-geometry" for issue in resolver_issues):
        raise AssertionError("standalone layout resolver must reject state variant geometry")
    candidate["components"][1]["stateVariants"]["selected"] = {}
    candidate["stateSemantics"]["selected"]["geometryPolicy"]["allowedChanges"] = ["bounds"]
    issues = validate(candidate, registry, require_feedback=True)
    if not any(issue.get("code") == "state-geometry" for issue in issues):
        raise AssertionError("preserveBounds must reject geometry-policy escape hatches")


def assert_anchor_and_layer_contracts() -> None:
    """Reject decorative anchor labels, inverted render layers, and muted primary actions."""
    tokens = {"background": "#000000", "surface": "#111111", "accent": "#ffffff", "text": "#ffffff", "titleSize": 24, "bodySize": 18, "labelSize": 16, "captionSize": 14, "numericSize": 18}
    contract = {
        "coordinateSpace": "screen-top-left-normalized",
        "canvas": {"rootRole": "screen-root", "renderMode": "ScreenSpaceOverlay", "scalerMode": "ScaleWithScreenSize", "singleRoot": True, "nestedCanvasPolicy": "forbid"},
        "anchorPolicy": {"defaultPivot": [0.5, 0.5], "allowedStrategies": ["edge-docked", "center", "stretch"], "safeAreaTarget": "profile-safe-area-inset"},
        "colorRoles": {"primaryAction": "accent", "secondaryAction": "surface", "feedback": "surface", "foregroundOnAccent": "text", "foregroundOnDanger": "text"},
        "typographyRoles": {role: {"token": token} for role, token in {"title": "titleSize", "body": "bodySize", "label": "labelSize", "caption": "captionSize", "numeric": "numericSize"}.items()},
        "layerRoles": {"background": 0, "information": 100, "feedback": 200, "action": 300},
    }
    component = lambda component_id, bounds, strategy, edge, pivot, layer, order, color, intent=None: {
        "id": component_id, "layout": {"bounds": bounds, "anchor": {"strategy": strategy, "edge": edge, "pivot": pivot, "safeArea": "inside"}},
        "colorToken": color, "typographyRole": "none", "layerRole": layer, "siblingOrder": order,
        "interaction": {} if intent is None else {"intent": intent},
    }
    candidate = {"designContract": contract, "intentContract": {"requestedPrimaryIntent": "play"}}
    components = [
        component("background", [0, 0, 1, 1], "stretch", "none", [0.5, 0.5], "background", 0, "background"),
        component("play", [0.4, 0.84, 0.6, 0.96], "edge-docked", "bottom", [0.5, 0], "action", 1, "surface", "play"),
        component("bad-center", [0.1, 0.1, 0.3, 0.2], "center", "none", [0.5, 0.5], "information", 2, "surface"),
    ]
    issues: list[dict[str, str]] = []
    validate_design_contract(candidate, [], tokens, components, issues, True)
    codes = {issue.get("code") for issue in issues}
    if not {"anchor-geometry", "primary-action-color"}.issubset(codes):
        raise AssertionError("strict design contract must reject non-geometric anchors and muted primary actions")
    components[1]["colorToken"] = "accent"
    components[2]["layout"]["bounds"] = [0.4, 0.4, 0.6, 0.6]
    components[2]["layerRole"] = "feedback"
    components[2]["siblingOrder"] = 0
    issues = []
    validate_design_contract(candidate, [], tokens, components, issues, True)
    if not any(issue.get("code") == "layer-order" for issue in issues):
        raise AssertionError("strict design contract must reject duplicate or inverted sibling order")
    components[0]["layout"]["anchor"]["safeArea"] = "ignore"
    components[1]["layout"]["anchor"]["safeArea"] = "ignore"
    issues = []
    validate_design_contract(candidate, [], tokens, components, issues, True)
    if not any(issue.get("code") == "safe-area-policy" for issue in issues):
        raise AssertionError("only a top-level stretch background may ignore the safe area")
    resolver_issues: list[dict] = []
    resolver_suggestions: list[dict] = []
    resolved = resolve_children([components[0]], "wide", (8.0, 12.0), (84.0, 176.0), resolver_issues, resolver_suggestions, screen_origin=(0.0, 0.0), screen_size=(100.0, 200.0))
    if resolver_issues or resolved[0]["resolvedRect"] != {"x": 0.0, "y": 0.0, "width": 100.0, "height": 200.0}:
        raise AssertionError("safe-area-ignored background must resolve to the complete profile rectangle")


def assert_state_variant_normalization() -> None:
    """Generator must remove stale variants instead of reproducing fake states."""
    candidate = build_samples()["main-menu"]
    candidate["stateSemantics"] = {
        "default": {"affectedComponentIds": ["menu-title"]},
        "selected": {"affectedComponentIds": ["menu-title"]},
    }
    bind_state_variants(candidate)
    root_variants = candidate["components"][0]["stateVariants"]
    title_variants = candidate["components"][1]["stateVariants"]
    if set(root_variants) != {"default"}:
        raise AssertionError("generator must remove non-semantic variants")
    if set(title_variants) != {"default", "selected"}:
        raise AssertionError("generator must add semantic state variants")


def assert_fixture_text_bindings_are_explicit(registry: dict) -> None:
    """Fixture values must name their text consumer and fit policy."""
    candidate = build_samples()["main-menu"]
    candidate["states"] = [{"id": "default"}, {"id": "long-content"}]
    title = candidate["components"][1]
    title["layout"]["bounds"] = [0.0, 0.0, 0.2, 0.3]
    title["layout"]["minSize"] = [20, 20]
    candidate["stateSemantics"] = {
        "default": {
            "fixtureData": {"title": "Main Menu"}, "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title is visible"], "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True}, "effects": [{"componentId": "menu-title", "changes": {"graphicAlpha": 1.0}}],
        },
        "long-content": {
            "fixtureData": {"title": "A deliberately long fixture title for narrow space"}, "affectedComponentIds": ["menu-title"],
            "visualChanges": ["title uses a bounded overflow policy"], "interactionChanges": ["start remains available"],
            "geometryPolicy": {"preserveBounds": True}, "effects": [{"componentId": "menu-title", "changes": {"wrapText": True}}],
            "fixtureTextBindings": [{"componentId": "menu-title", "fixtureDataKey": "title", "overflowPolicy": "ellipsis", "maxLines": 1, "contentInsetsPx": [2, 2, 2, 2], "reserveActionClearancePx": 0}],
        },
    }
    bind_state_variants(candidate)
    issues = validate(candidate, registry)
    if any(issue.get("code") == "fixture-text" for issue in issues):
        raise AssertionError("valid fixture text binding must not be rejected")
    candidate["stateSemantics"]["long-content"]["fixtureTextBindings"][0]["fixtureDataKey"] = "missing"
    issues = validate(candidate, registry)
    if not any(issue.get("code") == "fixture-text" for issue in issues):
        raise AssertionError("missing fixture text key must be rejected")
    candidate["stateSemantics"]["long-content"]["fixtureTextBindings"][0]["fixtureDataKey"] = "title"
    resolver_issues: list[dict] = []
    resolver_suggestions: list[dict] = []
    nodes = resolve_children(candidate["components"], "wide", (0.0, 0.0), (100.0, 100.0), resolver_issues, resolver_suggestions)
    text_fit, text_issues = resolve_fixture_text_fit(candidate, [{"profileId": "wide", "nodes": nodes}])
    if text_issues or not text_fit[0]["truncated"]:
        raise AssertionError("ellipsis fixture text must record truncation without claiming a wrap fit")


def assert_advanced_composition_contract(registry: dict) -> None:
    """High-fidelity constraints must preserve hierarchy and profile semantics."""
    project_root = next(parent for parent in [SCRIPT_DIR, *SCRIPT_DIR.parents] if (parent / ".agents").is_dir() and (parent / "Assets").is_dir())
    source = project_root / "Assets/UI/Contracts/Generated/arena-combat-hud-proof-v1/arena-combat-hud-proof-v2-iteration.screen-spec.v3.json"
    candidate = json.loads(source.read_text(encoding="utf-8"))
    candidate["designContract"]["advancedComposition"] = {
        "primaryActions": [{"logicalId": "combat-primary", "intent": "cast-primary", "componentIdsByProfile": {"wide": "wide-skill-primary", "narrow": "narrow-skill-primary"}}],
        "focalTreatment": "none",
        "noFocalReason": "This original combat HUD prioritizes live status and action readability over decorative hero art.",
        "focalSubjects": [],
        "alignmentGroups": [
            {"profileId": "wide", "axis": "x", "edge": "start", "componentIds": ["wide-player-panel", "wide-quest-feed"], "tolerancePx": 1},
            {"profileId": "narrow", "axis": "x", "edge": "start", "componentIds": ["narrow-player-panel", "narrow-target-panel"], "tolerancePx": 1},
        ],
        "clearanceConstraints": [
            {"profileId": "wide", "axis": "y", "relation": "before", "firstComponentId": "wide-player-panel", "secondComponentId": "wide-quest-feed", "minGapPx": 16},
            {"profileId": "narrow", "axis": "y", "relation": "before", "firstComponentId": "narrow-player-panel", "secondComponentId": "narrow-target-panel", "minGapPx": 16},
        ],
        "responsiveEquivalences": [{"logicalId": "combat-primary", "intent": "cast-primary", "componentIdsByProfile": {"wide": "wide-skill-primary", "narrow": "narrow-skill-primary"}}],
        "interactionDensity": {"groups": [
            {"id": "combat-actions-wide", "profileId": "wide", "componentIds": ["wide-skill-primary", "wide-skill-secondary", "wide-skill-utility", "wide-settings"], "minGapPx": 8, "maxTargets": 4},
            {"id": "combat-actions-narrow", "profileId": "narrow", "componentIds": ["narrow-skill-primary", "narrow-skill-secondary", "narrow-skill-utility", "narrow-settings"], "minGapPx": 8, "maxTargets": 4},
        ]},
    }
    issues = validate(candidate, registry, require_advanced_composition=True)
    if issues:
        raise AssertionError("valid advanced composition contract must pass: " + json.dumps(issues, ensure_ascii=False))
    plans: list[dict] = []
    for profile in candidate["profiles"]:
        safe = profile.get("safeArea", [0, 0, 1, 1])
        width, height = float(profile["width"]), float(profile["height"])
        safe_origin = (float(safe[0]) * width, float(safe[1]) * height)
        safe_size = ((float(safe[2]) - float(safe[0])) * width, (float(safe[3]) - float(safe[1])) * height)
        nodes = resolve_children(candidate["components"], profile["id"], safe_origin, safe_size, [], [], screen_origin=(0.0, 0.0), screen_size=(width, height))
        plans.append({"profileId": profile["id"], "nodes": nodes})
    density, density_issues = resolve_interaction_density(candidate, plans)
    if density_issues or any(item.get("status") != "passed" for item in density):
        raise AssertionError("resolved action groups must meet their declared target and gap constraints")
    candidate["designContract"]["advancedComposition"]["interactionDensity"]["groups"][0]["minGapPx"] = 999
    _, density_issues = resolve_interaction_density(candidate, plans)
    if not any(issue.get("code") == "interaction-gap" for issue in density_issues):
        raise AssertionError("resolved action density gap drift must be rejected")
    candidate["designContract"]["advancedComposition"]["interactionDensity"]["groups"][0]["minGapPx"] = 8
    focal_candidate = json.loads(json.dumps(candidate))
    focal_advanced = focal_candidate["designContract"]["advancedComposition"]
    focal_advanced["focalTreatment"] = "subject"
    focal_advanced.pop("noFocalReason", None)
    focal_advanced["focalSubjects"] = [{
        "logicalId": "player-avatar",
        "componentIdsByProfile": {"wide": "wide-player-avatar", "narrow": "narrow-player-avatar"},
        "protectedFromPrimaryAction": False,
    }]
    for asset in focal_candidate["assets"]:
        if asset["id"] == "player-avatar":
            asset["cropPolicy"] = "focal-cover"
            asset["focalPoint"] = [0.5, 0.42]
            asset["sourceAspectRatio"] = 0.65
            asset["atlasRotationPolicy"] = "disallow-rotation"
    focal_advanced["focalAssetPolicies"] = [{
        "logicalId": "player-avatar",
        "assetIds": ["player-avatar"],
        "cropPolicy": "focal-cover",
        "focalPoint": [0.5, 0.42],
        "safeCropInsetsNormalized": [0.05, 0.05, 0.05, 0.05],
    }]
    issues = validate(focal_candidate, registry, require_advanced_composition=True)
    if issues:
        raise AssertionError("focal asset policy tied to AssetManifest must pass: " + json.dumps(issues, ensure_ascii=False))
    focal_asset = next(asset for asset in focal_candidate["assets"] if asset["id"] == "player-avatar")
    del focal_asset["sourceAspectRatio"]
    issues = validate(focal_candidate, registry, require_advanced_composition=True)
    if not any(issue.get("code") == "focal-asset-policy" for issue in issues):
        raise AssertionError("focal-cover must require an AssetManifest source aspect ratio")
    focal_asset["sourceAspectRatio"] = 0.65
    del focal_asset["atlasRotationPolicy"]
    issues = validate(focal_candidate, registry, require_advanced_composition=True)
    if not any(issue.get("code") == "focal-asset-policy" for issue in issues):
        raise AssertionError("focal-cover must reject rotated SpriteAtlas packing risk")
    focal_asset["atlasRotationPolicy"] = "disallow-rotation"
    focal_advanced["focalAssetPolicies"][0]["focalPoint"] = [0.5, 0.5]
    issues = validate(focal_candidate, registry, require_advanced_composition=True)
    if not any(issue.get("code") == "focal-asset-policy" for issue in issues):
        raise AssertionError("focal crop policy drift from AssetManifest must be rejected")
    focal_advanced["focalAssetPolicies"][0]["focalPoint"] = [0.5, 0.42]
    focal_evidence, focal_issues = resolve_focal_crop_feasibility(focal_candidate, plans)
    if focal_issues or len(focal_evidence) != 2 or any(item.get("status") != "passed" for item in focal_evidence):
        raise AssertionError("feasible focal-cover policy must resolve safe crop evidence for every profile")
    focal_asset["sourceAspectRatio"] = 3.0
    _, focal_issues = resolve_focal_crop_feasibility(focal_candidate, plans)
    if not any(issue.get("code") == "focal-crop-feasibility" for issue in focal_issues):
        raise AssertionError("impossible focal safe crop must block the resolved profile plan")
    candidate["components"][-1]["children"][0]["colorToken"] = "surface"
    issues = validate(candidate, registry, require_advanced_composition=True)
    if not any(issue.get("code") == "visual-hierarchy" for issue in issues):
        raise AssertionError("primary action hierarchy drift must be rejected")


def assert_focal_crop_materialization_contract() -> None:
    """Keep focal-cover policy, crop execution and snapshot evidence connected."""
    project_root = next(parent for parent in [SCRIPT_DIR, *SCRIPT_DIR.parents] if (parent / ".agents").is_dir() and (parent / "Assets").is_dir())
    materializer = (project_root / "Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs").read_text(encoding="utf-8")
    crop_graphic = (project_root / "Assets/Scripts/ESLogic/Runtime/UI/ESUIFocalCropRawImage.cs").read_text(encoding="utf-8")
    required_materializer_fragments = (
        "out JObject focalPolicy",
        "ConfigureFocalCoverGraphic(go, sprite, focalAsset, focalPolicy)",
        'focalPolicy?["safeCropInsetsNormalized"] as JArray',
        "sourceAspectRatio does not match the resolved Sprite UV aspect",
        "does not support a SpriteAtlas-rotated Sprite",
        "BuildFocalCropSnapshot",
        "BuildPngCaptureMetadata",
        '"pngSha256"',
        '"edgeTransitionCount"',
        '["safeCropSatisfied"] = focalImage.SafeCropSatisfied',
        '["enabled"] = IsComponentEnabled(component)',
        '["hasButton"] = button != null',
        '["hasDescendantGraphic"] = graphics.Length > 0',
        '["graphicAlpha"] = graphic == null ? null : (float?)graphic.color.a',
        '["descendantGraphicAlpha"] = descendantGraphicAlpha',
        '["descendantGraphicAlphas"] = descendantGraphics',
        '["graphicColor"] = graphic == null ? null : "#" + ColorUtility.ToHtmlStringRGBA(graphic.color)',
        '["wrapText"] = wrapText',
        '["descendantTextStates"] = descendantTextStates',
    )
    if any(fragment not in materializer for fragment in required_materializer_fragments):
        raise AssertionError("focal-cover policy must flow into materialization and both snapshot formats")
    required_crop_fragments = (
        "ShiftCropToContain",
        "public float SourceAspectRatio",
        "safeCropSatisfied = protectedRegion.width >= 0f",
        "return minimumStart <= maximumStart ? Mathf.Clamp",
    )
    if any(fragment not in crop_graphic for fragment in required_crop_fragments):
        raise AssertionError("focal crop renderer must preserve a feasible protected region and expose its result")


def assert_snapshot_evidence_gate() -> None:
    """Verify cross-channel geometry and unsafe focal crops are rejected."""
    with tempfile.TemporaryDirectory(prefix="es-ui-snapshot-") as temp_dir:
        root = Path(temp_dir)
        packet = {
            "schemaVersion": 3,
            "screenId": "snapshot-proof",
            "profiles": [{"id": "wide", "width": 1920, "height": 1080}, {"id": "narrow", "width": 1080, "height": 1920}],
            "states": [{"id": "default"}, {"id": "selected"}],
            "designContract": {"advancedComposition": {"focalAssetPolicies": [{"logicalId": "hero-art"}]}},
        }
        spec_path = root / "snapshot-proof.screen-spec.v3.json"
        spec_path.write_text(json.dumps(packet), encoding="utf-8")
        spec_hash = __import__("hashlib").sha256(spec_path.read_bytes()).hexdigest()
        evidence_root = root / "evidence"
        evidence_root.mkdir()
        for profile_id, width, height in (("wide", 1920, 1080), ("narrow", 1080, 1920)):
            for state_id in ("default", "selected"):
                common = {"schemaVersion": 1, "panelId": "snapshot-proof", "profileId": profile_id, "stateId": state_id, "runId": "run-1", "specHash": spec_hash, "sceneGeneration": 1}
                viewport = {"width": width, "height": height, "orientation": "landscape" if width >= height else "portrait"}
                canvas = {"renderMode": "ScreenSpaceCamera", "scaler": {"uiScaleMode": "ScaleWithScreenSize", "referenceResolution": [1920, 1080], "screenMatchMode": "MatchWidthOrHeight", "match": 0.5}}
                editor_element = {"path": "Canvas/snapshot-proof/hero-art", "active": True, "screenRect": {"x": 20, "y": 20, "width": width - 40, "height": height - 40}, "focalCrop": {"safeCropSatisfied": True}}
                runtime_element = {"path": "Canvas/snapshot-proof/hero-art", "active": True, "screenX": 20, "screenY": 20, "screenWidth": width - 40, "screenHeight": height - 40, "focalCrop": {"safeCropSatisfied": True}}
                editor = {**common, "command": "editor.snapshot", "captureKey": f"snapshot-proof.{profile_id}.{state_id}", "rootPath": "Canvas/snapshot-proof", "viewport": viewport, "canvas": canvas, "elements": [editor_element]}
                runtime = {**common, "command": "ui.snapshot", "rootPath": "Canvas/snapshot-proof", "viewport": viewport, "canvas": canvas, "screenWidth": width, "screenHeight": height, "uiElements": [runtime_element]}
                (evidence_root / f"{profile_id}__{state_id}.editor.json").write_text(json.dumps(editor), encoding="utf-8")
                (evidence_root / f"{profile_id}__{state_id}.ui.json").write_text(json.dumps(runtime), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if receipt["status"] != "passed" or receipt["issues"]:
            raise AssertionError("paired snapshot evidence with safe focal crops must pass")
        broken_path = evidence_root / "wide__selected.ui.json"
        broken = json.loads(broken_path.read_text(encoding="utf-8"))
        broken["uiElements"][0]["screenX"] = 21
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-geometry-mismatch" for issue in receipt["issues"]):
            raise AssertionError("editor and UI element geometry must agree")
        broken["uiElements"][0]["screenX"] = 20
        broken["uiElements"][0]["active"] = False
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-active-mismatch" for issue in receipt["issues"]):
            raise AssertionError("editor and UI element activation must agree")
        broken["uiElements"][0]["active"] = True
        broken["uiElements"][0]["path"] = "Canvas/other-root/hero-art"
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-element-path-set" for issue in receipt["issues"]):
            raise AssertionError("UI snapshot elements must remain under the same semantic root")
        broken["uiElements"][0]["path"] = "Canvas/snapshot-proof/hero-art"
        editor_path = evidence_root / "wide__selected.editor.json"
        broken_editor = json.loads(editor_path.read_text(encoding="utf-8"))
        broken["uiElements"][0]["path"] = "Canvas/other-root/hero-art"
        broken_editor["elements"][0]["path"] = "Canvas/other-root/hero-art"
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        editor_path.write_text(json.dumps(broken_editor), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-element-path-root" for issue in receipt["issues"]):
            raise AssertionError("matching snapshot paths outside the declared root must be rejected")
        broken["uiElements"][0]["path"] = "Canvas/snapshot-proof/hero-art"
        broken_editor["elements"][0]["path"] = "Canvas/snapshot-proof/hero-art"
        broken.pop("rootPath")
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        editor_path.write_text(json.dumps(broken_editor), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-canvas-metadata" for issue in receipt["issues"]):
            raise AssertionError("missing snapshot root metadata must be rejected")
        broken["rootPath"] = "Canvas/snapshot-proof"
        broken["canvas"] = {}
        broken_editor["canvas"] = {}
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        editor_path.write_text(json.dumps(broken_editor), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-canvas-metadata" for issue in receipt["issues"]):
            raise AssertionError("empty Canvas metadata must be rejected")
        canvas = {"renderMode": "ScreenSpaceCamera", "scaler": {"uiScaleMode": "ScaleWithScreenSize", "referenceResolution": [1920, 1080], "screenMatchMode": "MatchWidthOrHeight", "match": 0.5}}
        broken["canvas"] = canvas
        broken_editor["canvas"] = canvas
        broken["uiElements"][0].pop("active")
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        editor_path.write_text(json.dumps(broken_editor), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-active-invalid" for issue in receipt["issues"]):
            raise AssertionError("missing active state must be rejected")
        broken["uiElements"][0]["active"] = True
        broken["uiElements"][0]["focalCrop"]["safeCropSatisfied"] = False
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "focal-crop-unsafe" for issue in receipt["issues"]):
            raise AssertionError("unsafe serialized focal crops must block snapshot evidence")
        broken["uiElements"][0]["focalCrop"]["safeCropSatisfied"] = True
        broken["specHash"] = "0" * 64
        broken_path.write_text(json.dumps(broken), encoding="utf-8")
        receipt = validate_snapshot_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "snapshot-identity" for issue in receipt["issues"]):
            raise AssertionError("snapshot identity drift must block evidence")
        empty_packet = {"schemaVersion": 3, "screenId": "empty-snapshot-proof", "profiles": [], "states": []}
        empty_path = root / "empty-snapshot-proof.screen-spec.v3.json"
        empty_path.write_text(json.dumps(empty_packet), encoding="utf-8")
        receipt = validate_snapshot_evidence(empty_path, evidence_root)
        expected_codes = {"missing-profile-matrix", "missing-state-matrix"}
        if not expected_codes.issubset({str(issue.get("code")) for issue in receipt["issues"]}):
            raise AssertionError("empty profile/state matrices must block snapshot evidence")


def assert_gpu_evidence_gate() -> None:
    """Verify PNG bytes, metadata, dimensions and non-blank pixels stay bound together."""
    with tempfile.TemporaryDirectory(prefix="es-ui-gpu-evidence-") as temp_dir:
        root = Path(temp_dir)
        packet = {
            "schemaVersion": 3,
            "screenId": "gpu-proof",
            "profiles": [{"id": "wide", "width": 64, "height": 48}],
            "states": [{"id": "default"}, {"id": "selected"}],
            "stateSemantics": {
                "default": {"visualChanges": []},
                "selected": {"affectedComponentIds": ["target"], "visualChanges": ["selection outline is visible"], "effects": [{"componentId": "target", "changes": {"outline": True}}]},
            },
        }
        spec_path = root / "gpu-proof.screen-spec.v3.json"
        spec_path.write_text(json.dumps(packet), encoding="utf-8")
        spec_hash = __import__("hashlib").sha256(spec_path.read_bytes()).hexdigest()
        evidence_root = root / "evidence"
        evidence_root.mkdir()
        png_path = evidence_root / "wide__default.png"
        image = Image.new("RGBA", (64, 48), (15, 24, 36, 255))
        for x in range(12, 52):
            for y in range(10, 38):
                image.putpixel((x, y), (224, 174, 65, 255))
        image.save(png_path)
        capture = collect_png_stats(png_path)
        common = {
            "schemaVersion": 1, "panelId": "gpu-proof", "profileId": "wide", "stateId": "default", "runId": "run-2", "specHash": spec_hash, "sceneGeneration": 1, "capture": capture,
            "rootPath": "Canvas/gpu-proof", "viewport": {"width": 64, "height": 48, "orientation": "landscape"},
            "canvas": {"renderMode": "ScreenSpaceCamera", "scaler": {"uiScaleMode": "ScaleWithScreenSize", "referenceResolution": [1920, 1080], "screenMatchMode": "MatchWidthOrHeight", "match": 0.5}},
            "screenWidth": 64, "screenHeight": 48,
        }
        snapshot_elements = [
            {"path": "Canvas/gpu-proof/root", "active": True, "screenRect": {"x": 0, "y": 0, "width": 64, "height": 48}, "focalCrop": None},
            {"path": "Canvas/gpu-proof/root/target", "active": True, "screenRect": {"x": 12, "y": 10, "width": 40, "height": 28}, "focalCrop": None},
        ]
        def runtime_snapshot_elements(*, target_outline: bool, target_interactable: bool = True) -> list[dict]:
            return [
                {"path": "Canvas/gpu-proof/root", "active": True, "hasButton": False, "interactable": False, "hasGraphic": False, "hasDescendantGraphic": False, "graphicAlpha": None, "descendantGraphicAlpha": None, "descendantGraphicAlphas": [], "graphicColor": None, "outline": False, "hasText": False, "wrapText": False, "text": None, "descendantTextStates": [], "screenX": 0, "screenY": 0, "screenWidth": 64, "screenHeight": 48, "focalCrop": None},
                {"path": "Canvas/gpu-proof/root/target", "active": True, "hasButton": True, "interactable": target_interactable, "hasGraphic": True, "hasDescendantGraphic": True, "graphicAlpha": 1.0, "descendantGraphicAlpha": 1.0, "descendantGraphicAlphas": [{"path": "target", "alpha": 1.0}], "graphicColor": "#e0ae41ff", "outline": target_outline, "hasText": False, "wrapText": False, "text": None, "descendantTextStates": [], "screenX": 12, "screenY": 10, "screenWidth": 40, "screenHeight": 28, "focalCrop": None},
            ]
        editor = {**common, "command": "editor.snapshot", "captureKey": "gpu-proof.wide.default", "elements": snapshot_elements}
        runtime = {**common, "command": "ui.snapshot", "uiElements": runtime_snapshot_elements(target_outline=False)}
        (evidence_root / "wide__default.editor.json").write_text(json.dumps(editor), encoding="utf-8")
        (evidence_root / "wide__default.ui.json").write_text(json.dumps(runtime), encoding="utf-8")
        selected_path = evidence_root / "wide__selected.png"
        selected = Image.new("RGBA", (64, 48), (15, 24, 36, 255))
        for x in range(12, 52):
            for y in range(10, 38):
                selected.putpixel((x, y), (56, 159, 255, 255))
        selected.save(selected_path)
        selected_capture = collect_png_stats(selected_path)
        selected_common = {**common, "stateId": "selected", "capture": selected_capture}
        selected_editor = {**selected_common, "command": "editor.snapshot", "captureKey": "gpu-proof.wide.selected", "elements": snapshot_elements}
        selected_runtime = {**selected_common, "command": "ui.snapshot", "uiElements": runtime_snapshot_elements(target_outline=True)}
        (evidence_root / "wide__selected.editor.json").write_text(json.dumps(selected_editor), encoding="utf-8")
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        if receipt["status"] != "passed":
            raise AssertionError("matched PNG bytes and snapshot capture metadata must pass")
        selected_runtime["uiElements"] = runtime_snapshot_elements(target_outline=False)
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "state-effect-snapshot-mismatch" for issue in receipt["issues"]):
            raise AssertionError("a visually different state must still prove its declared effect in the semantic snapshot")
        selected_runtime["uiElements"] = runtime_snapshot_elements(target_outline=True)
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        hidden_rects, hidden_missing, hidden_ambiguous, hidden_invalid = gpu_evidence.resolve_affected_rects(
            {"elements": [{"path": "Canvas/gpu-proof/root/reveal", "active": False, "screenRect": {"x": 16, "y": 12, "width": 12, "height": 12}}]},
            ["reveal"], 64, 48)
        if hidden_missing or hidden_ambiguous or hidden_invalid or not hidden_rects or hidden_rects[0]["baselineActive"]:
            raise AssertionError("a unique default-hidden component must retain its profile-local locality rectangle")
        _, _, ambiguous_components, _ = gpu_evidence.resolve_affected_rects(
            {"elements": [
                {"path": "Canvas/gpu-proof/root/a/reveal", "active": False, "screenRect": {"x": 16, "y": 12, "width": 12, "height": 12}},
                {"path": "Canvas/gpu-proof/root/b/reveal", "active": False, "screenRect": {"x": 32, "y": 12, "width": 12, "height": 12}},
            ]}, ["reveal"], 64, 48)
        if ambiguous_components != ["reveal"]:
            raise AssertionError("ambiguous default snapshot paths must not choose an arbitrary locality rectangle")
        unrelated = image.copy()
        for x in range(2, 10):
            for y in range(2, 10):
                unrelated.putpixel((x, y), (220, 64, 64, 255))
        unrelated.save(selected_path)
        selected_capture = collect_png_stats(selected_path)
        selected_editor["capture"] = selected_capture
        selected_runtime["capture"] = selected_capture
        (evidence_root / "wide__selected.editor.json").write_text(json.dumps(selected_editor), encoding="utf-8")
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "state-pixel-outside-affected-components" for issue in receipt["issues"]):
            raise AssertionError("unrelated background changes must not satisfy a target component's visual state evidence")
        selected.save(selected_path)
        selected_capture = collect_png_stats(selected_path)
        selected_editor["capture"] = selected_capture
        selected_runtime["capture"] = selected_capture
        (evidence_root / "wide__selected.editor.json").write_text(json.dumps(selected_editor), encoding="utf-8")
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        selected.save(png_path)
        flat_capture = collect_png_stats(png_path)
        editor["capture"] = flat_capture
        runtime["capture"] = flat_capture
        (evidence_root / "wide__default.editor.json").write_text(json.dumps(editor), encoding="utf-8")
        (evidence_root / "wide__default.ui.json").write_text(json.dumps(runtime), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "state-pixel-undifferentiated" for issue in receipt["issues"]):
            raise AssertionError("declared visual states must differ from their default capture")
        packet["stateSemantics"]["selected"] = {"affectedComponentIds": ["target"], "visualChanges": [], "effects": [{"componentId": "target", "changes": {"interactable": False}}]}
        selected_runtime["uiElements"] = runtime_snapshot_elements(target_outline=False, target_interactable=False)
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        spec_path.write_text(json.dumps(packet), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        if any(issue.get("code") == "state-pixel-undifferentiated" for issue in receipt["issues"]):
            raise AssertionError("behavior-only state effects must not require a visual pixel delta")
        packet["stateSemantics"]["selected"] = {"affectedComponentIds": ["target"], "visualChanges": ["selection outline is visible"], "effects": [{"componentId": "target", "changes": {"outline": True}}]}
        selected_runtime["uiElements"] = runtime_snapshot_elements(target_outline=True)
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        spec_path.write_text(json.dumps(packet), encoding="utf-8")
        packet["stateSemantics"]["selected"] = {"affectedComponentIds": ["target"], "visualChanges": ["target fades"], "effects": [{"componentId": "target", "changes": {"graphicAlpha": 0.45}}]}
        descendant_alpha = runtime_snapshot_elements(target_outline=False)
        descendant_alpha[1].update({"hasGraphic": False, "hasDescendantGraphic": True, "graphicAlpha": None, "descendantGraphicAlpha": 0.45, "descendantGraphicAlphas": [{"path": "target/visual", "alpha": 0.45}]})
        selected_runtime["uiElements"] = descendant_alpha
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        effect_issues: list[dict] = []
        gpu_evidence.validate_state_effect_snapshots(packet, evidence_root, effect_issues)
        if effect_issues:
            raise AssertionError("descendant graphic alpha must be proven through its declared materializer scope")
        descendant_alpha[1]["hasDescendantGraphic"] = False
        selected_runtime["uiElements"] = descendant_alpha
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        effect_issues = []
        gpu_evidence.validate_state_effect_snapshots(packet, evidence_root, effect_issues)
        if not any(issue.get("code") == "state-effect-snapshot-mismatch" for issue in effect_issues):
            raise AssertionError("graphic alpha must not claim direct Graphic capability when only descendants are affected")
        descendant_alpha[1].update({"hasDescendantGraphic": True, "descendantGraphicAlphas": [{"path": "target/visual", "alpha": 1.0}]})
        selected_runtime["uiElements"] = descendant_alpha
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        effect_issues = []
        gpu_evidence.validate_state_effect_snapshots(packet, evidence_root, effect_issues)
        if not any(issue.get("code") == "state-effect-snapshot-mismatch" for issue in effect_issues):
            raise AssertionError("a summary alpha must not hide a divergent descendant Graphic")
        descendant_alpha[1].update({"descendantGraphicAlphas": [{"path": "other-component/visual", "alpha": 0.45}]})
        selected_runtime["uiElements"] = descendant_alpha
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        effect_issues = []
        gpu_evidence.validate_state_effect_snapshots(packet, evidence_root, effect_issues)
        if not any(issue.get("code") == "state-effect-snapshot-mismatch" for issue in effect_issues):
            raise AssertionError("effect traces must not borrow a descendant value from another semantic component")
        packet["stateSemantics"]["selected"] = {"affectedComponentIds": ["target"], "visualChanges": ["target label changes"], "effects": [{"componentId": "target", "changes": {"text": "READY", "wrapText": True}}]}
        text_state = runtime_snapshot_elements(target_outline=False)
        text_state[1].update({"hasText": True, "wrapText": True, "text": "READY", "descendantTextStates": [{"path": "target/label", "wrapText": True, "text": "READY"}]})
        selected_runtime["uiElements"] = text_state
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        effect_issues = []
        gpu_evidence.validate_state_effect_snapshots(packet, evidence_root, effect_issues)
        if effect_issues:
            raise AssertionError("descendant TMP states must prove text and wrapping effects")
        text_state[1]["descendantTextStates"][0]["text"] = "STALE"
        selected_runtime["uiElements"] = text_state
        (evidence_root / "wide__selected.ui.json").write_text(json.dumps(selected_runtime), encoding="utf-8")
        effect_issues = []
        gpu_evidence.validate_state_effect_snapshots(packet, evidence_root, effect_issues)
        if not any(issue.get("code") == "state-effect-snapshot-mismatch" for issue in effect_issues):
            raise AssertionError("a summary text value must not hide a divergent descendant TMP state")
        image.save(png_path)
        capture = collect_png_stats(png_path)
        editor["capture"] = capture
        runtime["capture"] = capture
        (evidence_root / "wide__default.editor.json").write_text(json.dumps(editor), encoding="utf-8")
        (evidence_root / "wide__default.ui.json").write_text(json.dumps(runtime), encoding="utf-8")
        original_limit = gpu_evidence.MAX_PNG_PIXELS
        gpu_evidence.MAX_PNG_PIXELS = 100
        try:
            try:
                collect_png_stats(png_path)
            except ValueError as error:
                if "exceeds pixel limit" not in str(error):
                    raise
            else:
                raise AssertionError("oversized decoded PNGs must be rejected before full pixel expansion")
        finally:
            gpu_evidence.MAX_PNG_PIXELS = original_limit
        image = Image.new("RGBA", (64, 48), (15, 24, 36, 255))
        image.save(png_path)
        flat_capture = collect_png_stats(png_path)
        editor["capture"] = flat_capture
        runtime["capture"] = flat_capture
        (evidence_root / "wide__default.editor.json").write_text(json.dumps(editor), encoding="utf-8")
        (evidence_root / "wide__default.ui.json").write_text(json.dumps(runtime), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        failure_codes = {str(issue.get("code")) for issue in receipt["issues"]}
        if not {"uniform-png", "zero-edge-png"}.issubset(failure_codes):
            raise AssertionError("uniform PNGs must not pass pixel-integrity evidence")
        editor["capture"] = capture
        runtime["capture"] = capture
        (evidence_root / "wide__default.editor.json").write_text(json.dumps(editor), encoding="utf-8")
        (evidence_root / "wide__default.ui.json").write_text(json.dumps(runtime), encoding="utf-8")
        receipt = validate_gpu_evidence(spec_path, evidence_root)
        if not any(issue.get("code") == "capture-metadata-mismatch" for issue in receipt["issues"]):
            raise AssertionError("PNG hash drift must block capture evidence")


def main() -> int:
    registry_path = SCRIPT_DIR.parent / "references" / "game-ui-component-registry.json"
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    failures: dict[str, list[dict[str, str]]] = {}
    for name, sample in build_samples().items():
        issues = validate(sample, registry)
        if issues:
            failures[name] = issues
    if failures:
        print(json.dumps({"valid": False, "failures": failures}, ensure_ascii=False, indent=2))
        return 2
    try:
        assert_adapter_contract()
        assert_reference_ingest_contract()
        assert_reference_intent_requires_source(registry)
        assert_state_binding_requires_variant(registry)
        assert_state_binding_requires_semantic_owner(registry)
        assert_state_effects_are_executable(registry)
        assert_state_geometry_is_immutable(registry)
        assert_anchor_and_layer_contracts()
        assert_state_variant_normalization()
        assert_fixture_text_bindings_are_explicit(registry)
        assert_advanced_composition_contract(registry)
        assert_focal_crop_materialization_contract()
        assert_snapshot_evidence_gate()
        assert_gpu_evidence_gate()
    except AssertionError as error:
        print(json.dumps({"valid": False, "adapterContract": str(error)}, ensure_ascii=False, indent=2))
        return 3
    print(json.dumps({"valid": True, "sliceCount": 4, "slices": ["inventory", "combat-hud", "dialogue", "main-menu"], "referenceMeasurement": "passed"}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
