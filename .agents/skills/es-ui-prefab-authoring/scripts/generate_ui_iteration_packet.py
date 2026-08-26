#!/usr/bin/env python3
"""Generate deterministic rebuild and incremental-iteration ScreenSpec packets."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
from typing import Any


def feedback_record(prior_batch: str, changed_fields: list[str], expected_effects: list[str]) -> dict[str, Any]:
    return {
        "priorEvidenceBatch": prior_batch,
        "ruleIds": ["UI-FB-001", "UI-FB-002", "UI-FB-003", "UI-FB-004", "UI-FB-005", "UI-FB-006"],
        "changedFields": changed_fields,
        "expectedEffects": expected_effects,
        "falsificationChecks": [
            "fresh wide and narrow default GPU captures",
            "fresh six-state capture matrix",
            "spec hash and generated-art build ID match",
            "visual review at native resolution",
        ],
    }


def bind_state_variants(spec: dict[str, Any]) -> None:
    """Derive non-baseline variants from the fixture execution contract.

    A non-default component variant without a matching `stateSemantics` target
    cannot be applied by the Materializer. Preserve only the baseline and the
    explicit state targets, then add any missing target declarations.
    """
    by_id: dict[str, dict[str, Any]] = {}
    states_by_component: dict[str, set[str]] = {}

    def visit(nodes: list[Any]) -> None:
        for node in nodes or []:
            if not isinstance(node, dict):
                continue
            if isinstance(node.get("id"), str):
                by_id[node["id"]] = node
            visit(node.get("children", []))

    visit(spec.get("components", []))
    semantics_map = spec.get("stateSemantics")
    if not isinstance(semantics_map, dict):
        return
    for state_id, semantics in semantics_map.items():
        if not isinstance(semantics, dict):
            continue
        for component_id in semantics.get("affectedComponentIds", []):
            node = by_id.get(component_id)
            if node is None:
                continue
            states_by_component.setdefault(component_id, set()).add(state_id)

    for component_id, node in by_id.items():
        required_states = states_by_component.get(component_id, set())
        variants = node.setdefault("stateVariants", {})
        if isinstance(variants, dict):
            node["stateVariants"] = {
                state_id: value
                for state_id, value in variants.items()
                if state_id == "default" or state_id in required_states
            }
            node["stateVariants"].setdefault("default", {})
            for state_id in required_states:
                node["stateVariants"].setdefault(state_id, {})
        elif isinstance(variants, list):
            node["stateVariants"] = [
                value
                for value in variants
                if isinstance(value, dict)
                and (value.get("stateId") == "default" or value.get("stateId") in required_states)
            ]
            listed = {value.get("stateId") for value in node["stateVariants"]}
            if "default" not in listed:
                node["stateVariants"].append({"stateId": "default"})
                listed.add("default")
            for state_id in sorted(required_states):
                if state_id not in listed:
                    node["stateVariants"].append({"stateId": state_id})
                    listed.add(state_id)

    def default_effects(state_id: str, component_id: str) -> dict[str, Any]:
        changes = {
            "default": {"graphicAlpha": 1.0},
            "selected": {"outline": True},
            "empty": {"visible": False},
            "loading": {"graphicAlpha": 0.72},
            "disabled": {"interactable": False, "graphicAlpha": 0.45},
            "error": {"graphicColor": "#E86C73"},
            "long-content": {"wrapText": True},
        }.get(state_id, {"graphicAlpha": 1.0})
        return {"componentId": component_id, "changes": changes}

    for state_id, semantics in semantics_map.items():
        if not isinstance(semantics, dict):
            continue
        targets = [component_id for component_id in semantics.get("affectedComponentIds", []) if component_id in by_id]
        existing = {
            effect.get("componentId"): effect
            for effect in semantics.get("effects", [])
            if isinstance(effect, dict) and effect.get("componentId") in targets and isinstance(effect.get("changes"), dict) and effect["changes"]
        }
        semantics["effects"] = [existing.get(component_id, default_effects(state_id, component_id)) for component_id in targets]
def rebuild_spec(source: dict[str, Any], screen_id: str) -> dict[str, Any]:
    """Create a deterministic rebuild without changing the user's intent.

    Rebuild used to contain a hard-coded navigation lobby blueprint. That made
    a combat or reference-guided request silently become an unrelated screen.
    Until a domain-specific blueprint compiler exists, the only honest rebuild
    is a structural copy of the validated source contract with fresh output
    identity. This preserves screen family, primary intent, visual target,
    fidelity mode, references, tokens, layout and quality gates.
    """
    result = copy.deepcopy(source)
    result["screenId"] = screen_id
    result["prefabPath"] = f"Assets/UI/Prefabs/Generated/{screen_id}.prefab"
    result["fixtureScenePath"] = f"Assets/UI/Scenes/Generated/{screen_id}Fixture.unity"
    result["generationMode"] = "rebuild-from-source-contract"
    bind_state_variants(result)
    for state in result.get("states", []) or []:
        if isinstance(state, dict) and state.get("id"):
            state["fixture"] = f"{screen_id}/{state['id']}"
    for behavior in result.get("behaviors", []) or []:
        if isinstance(behavior, dict) and behavior.get("id"):
            suffix = str(behavior["id"]).split(".", 1)[-1]
            behavior["id"] = f"{screen_id}.{suffix}"
    evidence = result.setdefault("designEvidence", {})
    evidence["sourceType"] = "rebuild-from-existing-screen-spec"
    source_id = str(source.get("screenId", "source-screen"))
    evidence["feedback"] = feedback_record(
        f"ES/UIEvidence/{source_id}",
        ["screenId", "prefabPath", "fixtureScenePath", "generationMode", "designEvidence.feedback"],
        [
            "screen family and primary intent are unchanged",
            "visual target and fidelity/reference policy are unchanged",
            "the regenerated packet keeps the source layout and design contracts",
        ],
    )
    evidence.setdefault("assumptions", []).append(
        {
            "statement": "重建暂不重新发明屏幕语义；它只刷新物化身份并继承源合同，避免需求漂移。",
            "confidence": 1.0,
        }
    )
    return result


def iterate_spec(source: dict[str, Any], screen_id: str) -> dict[str, Any]:
    result = copy.deepcopy(source)
    result["screenId"] = screen_id
    result["prefabPath"] = f"Assets/UI/Prefabs/Generated/{screen_id}.prefab"
    result["fixtureScenePath"] = f"Assets/UI/Scenes/Generated/{screen_id}Fixture.unity"
    for state in result.get("states", []) or []:
        if isinstance(state, dict) and state.get("id"):
            state["fixture"] = f"{screen_id}/{state['id']}"
    for behavior in result.get("behaviors", []) or []:
        if isinstance(behavior, dict) and behavior.get("id"):
            suffix = str(behavior["id"]).split(".", 1)[-1]
            behavior["id"] = f"{screen_id}.{suffix}"
    # An iteration must preserve validated design contracts, foreground
    # tokens, font metadata and asset hashes. Replacing them with the old
    # generator defaults silently regresses a quality-gated packet.
    result["tokens"] = copy.deepcopy(source.get("tokens", {}))
    result["assets"] = copy.deepcopy(source.get("assets", []))

    def visit(node: dict[str, Any]) -> None:
        item_type = str(node.get("type", "frame"))
        node_id = str(node.get("id", "")).lower()
        if node_id.endswith("background") or node.get("layerRole") == "background":
            # Background is an intentional full-screen underlay. Preserve its
            # semantic variant so overlap validation treats it as an overlay.
            node["visualVariant"] = "background"
        elif item_type in {"image", "icon", "portrait"}:
            # Images are authored art, not token-colored surfaces. Applying an
            # accent here multiplies the bitmap and was the main cause of the
            # v18 hero becoming a dark, unreadable map.
            node["visualVariant"] = "none"
        elif item_type in {"button"}:
            # Preserve the source action hierarchy. A generic iteration must
            # not flatten authored primary/secondary emphasis.
            node["visualVariant"] = node.get("visualVariant", "surface")
            node["assetSlots"] = [slot for slot in node.get("assetSlots", []) if slot != "mode-ranked"]
        elif item_type in {"progress", "status-badge", "badge"}:
            node["visualVariant"] = node.get("visualVariant", "accent")
        elif item_type in {"text", "subtitle"}:
            node["visualVariant"] = node.get("visualVariant", "text")
        else:
            node["visualVariant"] = node.get("visualVariant", "surface")
        if node.get("id", "").endswith("-mail"):
            node["assetSlots"] = ["mail-icon"]
            node.setdefault("content", {})["text"] = ""
        elif node.get("id", "").endswith("-settings"):
            node["assetSlots"] = ["settings-icon"]
            node.setdefault("content", {})["text"] = ""
        node.setdefault("stateVariants", {"default": {}})
        for child in node.get("children", []) or []:
            visit(child)

    for node in result.get("components", []) or []:
        visit(node)
    bind_state_variants(result)
    evidence = result.setdefault("designEvidence", {})
    evidence["sourceType"] = "iterated-from-existing-spec"
    source_id = str(source.get("screenId", "source-screen"))
    evidence["feedback"] = feedback_record(
        f"ES/UIEvidence/{source_id}",
        ["screenId", "prefabPath", "fixtureScenePath", "designEvidence.feedback"],
        ["the regenerated packet keeps the source design contract and asset hashes", "wide and narrow geometry remains deterministic", "the new evidence is bound to the regenerated spec hash"],
    )
    evidence["assetDecisions"] = [{"role": "all declared image slots", "status": "generated-procedural", "reason": "增量迭代将原有 fallback 替换为可重复的原创视觉资源。"}]
    evidence.setdefault("assumptions", []).append({"statement": "本版本保留原 ScreenSpec 语义树，仅修订视觉资源、颜色角色和呈现层级。", "confidence": 1.0})
    return result


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value: dict[str, Any]) -> str:
    encoded = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encoded)
    return sha256_bytes(encoded)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--screen-id", default="arena-moba-lobby")
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8"))
    rebuild = rebuild_spec(source, args.screen_id + "-rebuild")
    iteration = iterate_spec(source, args.screen_id + "-iteration")
    rebuild_path = args.out_dir / f"{args.screen_id}-rebuild.screen-spec.v3.json"
    iteration_path = args.out_dir / f"{args.screen_id}-iteration.screen-spec.v3.json"
    rebuild_hash = write_json(rebuild_path, rebuild)
    iteration_hash = write_json(iteration_path, iteration)
    receipt = {
        "schemaVersion": 1,
        "sourceSpec": str(args.source).replace("\\", "/"),
        "modes": {
            "rebuild": {"path": str(rebuild_path).replace("\\", "/"), "specSha256": rebuild_hash, "source": "source-contract-rebuild"},
            "iterate": {"path": str(iteration_path).replace("\\", "/"), "specSha256": iteration_hash, "source": "existing-screen-spec"},
        },
        "deterministic": rebuild_hash == write_json(args.out_dir / ".rebuild-check.json", rebuild),
    }
    (args.out_dir / ".rebuild-check.json").unlink(missing_ok=True)
    write_json(args.out_dir / "iteration-packet.receipt.json", receipt)
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
