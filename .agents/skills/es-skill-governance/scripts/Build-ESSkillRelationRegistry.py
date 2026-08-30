#!/usr/bin/env python3
"""Build the AISpace Public Skill relationship projection.

The projection connects each direct Skill to its existing authoritative
Catalog, Resource Index, Registry Manifest, AIBrain route, Knowledge bindings,
AICommand binding (or an explicit non-execution exemption), evidence contract,
authority references, and Chinese route aliases. It is navigation metadata only
and never grants execution rights.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import tempfile
import time
from pathlib import Path
from typing import Any

import yaml


OUTPUT_REL = Path("ES/AISpace/Public/Skills/registry.json")
AISPACE_BINDINGS_REL = Path(".agents/SKILL_AISPACE_BINDINGS.json")
SOURCE_RELS = (
    Path(".agents/SKILL_DISCOVERY_POLICY.json"),
    Path(".agents/SKILL_RESOURCE_INDEX.yaml"),
    Path(".agents/SKILL_CATALOG.yaml"),
    Path(".agents/SKILL_REGISTRY.manifest.json"),
    Path(".agents/SKILL_ROUTE_ALIASES.zh-CN.json"),
    Path(".agents/skills/es-skill-governance/references/command-binding-registry.json"),
    Path("Documentation/AIKnowledge/AIBRAIN_ENTRY.md"),
    Path("Documentation/AIKnowledge/KnowledgeIndex.yaml"),
    Path("Assets/Plugins/ES/AICommands/AICommandCatalog.json"),
    AISPACE_BINDINGS_REL,
)
SKILL_NAME_RE = re.compile(r"es-[a-z0-9-]+")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def resolve_inside(root: Path, relative: Path, label: str) -> Path:
    if relative.is_absolute():
        raise ValueError(f"{label} must be project-relative: {relative}")
    root = root.resolve(strict=True)
    target = (root / relative).resolve(strict=False)
    try:
        target.relative_to(root)
    except ValueError as exc:
        raise ValueError(f"{label} escapes the project root: {relative}") from exc
    return target


def read_source(root: Path, relative: Path) -> tuple[str, str, bytes]:
    target = resolve_inside(root, relative, "source path")
    if not target.is_file():
        raise ValueError(f"Required source is missing: {relative.as_posix()}")
    data = target.read_bytes()
    try:
        data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise ValueError(f"Source is not strict UTF-8: {relative.as_posix()}") from exc
    return relative.as_posix(), sha256(data), data


def load_json(root: Path, relative: Path) -> Any:
    return json.loads(read_source(root, relative)[2].decode("utf-8"))


def load_yaml(root: Path, relative: Path) -> Any:
    return yaml.safe_load(read_source(root, relative)[2].decode("utf-8"))


def skill_names(root: Path) -> list[str]:
    skills_root = resolve_inside(root, Path(".agents/skills"), "Skills root")
    if not skills_root.is_dir():
        raise ValueError(".agents/skills is missing")
    return sorted(p.name for p in skills_root.iterdir() if p.is_dir() and (p / "SKILL.md").is_file())


def collect_aibrain_routes(text: str) -> dict[str, list[str]]:
    routes: dict[str, list[str]] = {}
    for line in text.splitlines():
        if "|" not in line or "es-" not in line:
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        area = cells[0] if cells else ""
        for name in sorted(set(SKILL_NAME_RE.findall(line))):
            routes.setdefault(name, []).append(area)
    return {name: sorted(set(areas)) for name, areas in routes.items()}


def collect_knowledge(index: Any) -> dict[str, list[dict[str, Any]]]:
    result: dict[str, list[dict[str, Any]]] = {}
    for entry in index.get("entries", []) if isinstance(index, dict) else []:
        if not isinstance(entry, dict):
            continue
        key_set = {str(key) for key in entry.get("routeKeys", []) if key}
        for name in entry.get("relatedSkills", []) or []:
            result.setdefault(str(name), []).append({
                "knowledgeId": str(entry.get("knowledgeId", "")),
                "file": str(entry.get("file", "")),
                "overlappingRouteKeys": sorted(key_set),
            })
    for name in result:
        result[name].sort(key=lambda item: item["knowledgeId"])
    return result


def collect_commands(registry: Any, catalog: Any) -> tuple[dict[str, list[dict[str, Any]]], dict[str, dict[str, Any]], set[str]]:
    bindings: dict[str, list[dict[str, Any]]] = {}
    for entry in registry.get("entries", []) if isinstance(registry, dict) else []:
        if not isinstance(entry, dict) or not entry.get("skillName"):
            continue
        bindings.setdefault(str(entry["skillName"]), []).append({
            "commandId": str(entry.get("commandId", "")),
            "role": str(entry.get("role", "")),
            "riskLevel": str(entry.get("riskLevel", "")),
            "writeMode": str(entry.get("writeMode", "")),
        })
    exemptions = {
        str(entry["skillName"]): {
            "reason": str(entry.get("reason", "")),
            "allowedOutputs": [str(value) for value in entry.get("allowedOutputs", [])],
        }
        for entry in registry.get("nonExecutionExemptions", []) if isinstance(registry, dict)
        if isinstance(entry, dict) and entry.get("skillName")
    }
    for name in bindings:
        bindings[name].sort(key=lambda item: item["commandId"])
    command_ids = {
        str(entry.get("id"))
        for entry in catalog.get("commands", []) if isinstance(entry, dict) and entry.get("id")
    }
    return bindings, exemptions, command_ids


def command_status(governance: dict[str, Any], bindings: list[dict[str, Any]], exemption: dict[str, Any] | None) -> str:
    if bindings:
        return "bound"
    if exemption is not None:
        return "non-execution-exempt"
    requirement = str(governance.get("commandRequirement", "none"))
    return "not-required" if requirement in ("", "none", "not-required") else "missing"


def collect_aispace_bindings(data: Any, direct_names: list[str]) -> dict[str, dict[str, Any]]:
    """Load the authoritative Skill↔AISpace output declarations.

    The binding file is intentionally a small registration contract.  It may
    mention only Skills that generate/cache content, but every mention must
    point back to that Skill's governance file and use a project-relative
    template.  The generated public registry then supplies the reverse edge.
    """
    if not isinstance(data, dict) or data.get("schemaVersion") != 1:
        raise ValueError("AISpace Skill binding registry must have schemaVersion 1")
    entries = data.get("skills")
    if not isinstance(entries, list):
        raise ValueError("AISpace Skill binding registry skills must be an array")
    direct = set(direct_names)
    result: dict[str, dict[str, Any]] = {}
    binding_ids: set[str] = set()
    allowed_storage = {"private-temp", "private-content", "public-index", "public-content", "unity-public"}
    absolute = re.compile(r"^(?:[A-Za-z]:[\\/]|/|\\\\)")
    for entry in entries:
        if not isinstance(entry, dict) or not entry.get("skillName"):
            raise ValueError("AISpace Skill binding entry must contain skillName")
        name = str(entry["skillName"])
        if name not in direct:
            raise ValueError(f"AISpace Skill binding references a non-direct Skill: {name}")
        if name in result:
            raise ValueError(f"duplicate AISpace Skill binding entry: {name}")
        expected_contract = f".agents/skills/{name}/governance.json"
        if str(entry.get("skillContractRef", "")) != expected_contract:
            raise ValueError(f"AISpace Skill binding has invalid skillContractRef: {name}")
        bindings = entry.get("bindings")
        if not isinstance(bindings, list) or not bindings:
            raise ValueError(f"AISpace Skill binding entry must declare at least one binding: {name}")
        normalized: list[dict[str, Any]] = []
        for binding in bindings:
            if not isinstance(binding, dict):
                raise ValueError(f"AISpace Skill binding must be an object: {name}")
            binding_id = str(binding.get("bindingId", ""))
            path_template = str(binding.get("pathTemplate", ""))
            storage_class = str(binding.get("storageClass", ""))
            if not binding_id or binding_id in binding_ids:
                raise ValueError(f"duplicate or empty AISpace bindingId: {binding_id or name}")
            if not path_template or absolute.match(path_template) or ".." in Path(path_template).parts:
                raise ValueError(f"AISpace binding pathTemplate must be project-relative: {name}/{binding_id}")
            if storage_class not in allowed_storage:
                raise ValueError(f"AISpace binding storageClass is invalid: {name}/{binding_id}")
            binding_ids.add(binding_id)
            normalized.append({str(key): value for key, value in binding.items()})
        result[name] = {
            "skillContractRef": expected_contract,
            "bindings": normalized,
        }
    return result


def build_projection(root: Path) -> dict[str, Any]:
    source_snapshot: dict[str, str] = {}
    source_bytes: dict[str, bytes] = {}
    for relative in SOURCE_RELS:
        path, digest, data = read_source(root, relative)
        source_snapshot[path] = digest
        source_bytes[path] = data

    catalog = yaml.safe_load(source_bytes[".agents/SKILL_CATALOG.yaml"].decode("utf-8")) or {}
    resource_index = yaml.safe_load(source_bytes[".agents/SKILL_RESOURCE_INDEX.yaml"].decode("utf-8")) or {}
    manifest = json.loads(source_bytes[".agents/SKILL_REGISTRY.manifest.json"].decode("utf-8"))
    aliases = json.loads(source_bytes[".agents/SKILL_ROUTE_ALIASES.zh-CN.json"].decode("utf-8"))
    command_registry = json.loads(source_bytes[".agents/skills/es-skill-governance/references/command-binding-registry.json"].decode("utf-8"))
    command_catalog = json.loads(source_bytes["Assets/Plugins/ES/AICommands/AICommandCatalog.json"].decode("utf-8"))
    aispace_binding_registry = json.loads(source_bytes[AISPACE_BINDINGS_REL.as_posix()].decode("utf-8"))
    knowledge_index = yaml.safe_load(source_bytes["Documentation/AIKnowledge/KnowledgeIndex.yaml"].decode("utf-8")) or {}
    aibrain_routes = collect_aibrain_routes(source_bytes["Documentation/AIKnowledge/AIBRAIN_ENTRY.md"].decode("utf-8"))
    knowledge = collect_knowledge(knowledge_index)
    bindings, exemptions, command_ids = collect_commands(command_registry, command_catalog)
    resource_by_name = {
        str(item.get("name")): item
        for item in resource_index.get("currentSkills", []) if isinstance(item, dict) and item.get("name")
    }
    resource_names = set(resource_by_name)
    for values in (resource_index.get("newSkills", {}) or {}).values() if isinstance(resource_index.get("newSkills", {}), dict) else []:
        resource_names.update(str(value) for value in values or [])
    manifest_by_name = {str(item.get("skillName")): item for item in manifest.get("skills", []) if isinstance(item, dict)}
    catalog_by_name = catalog.get("skills", {}) if isinstance(catalog, dict) else {}
    direct_names = skill_names(root)
    aispace_bindings = collect_aispace_bindings(aispace_binding_registry, direct_names)
    for name, declaration in aispace_bindings.items():
        contract_path = resolve_inside(root, Path(declaration["skillContractRef"]), "AISpace Skill contract path")
        if not contract_path.is_file():
            raise ValueError(f"AISpace Skill binding contract is missing: {declaration['skillContractRef']}")

    records: list[dict[str, Any]] = []
    for name in direct_names:
        governance = load_json(root, Path(".agents/skills") / name / "governance.json")
        skill_dir = Path(".agents/skills") / name
        skill_hash = sha256(resolve_inside(root, skill_dir / "SKILL.md", "Skill path").read_bytes())
        governance_hash = sha256(resolve_inside(root, skill_dir / "governance.json", "Governance path").read_bytes())
        evidence_binding_path = resolve_inside(root, skill_dir / "evidence-contract.binding.json", "Evidence binding path")
        evidence_binding_hash = sha256(evidence_binding_path.read_bytes()) if evidence_binding_path.is_file() else ""
        static_manifest_present = resolve_inside(root, skill_dir / "static-replay.manifest.json", "Static replay manifest path").is_file()
        adapter_present = resolve_inside(root, skill_dir / "references/static-replay-adapter.md", "Static replay adapter path").is_file()
        catalog_record = catalog_by_name.get(name) if isinstance(catalog_by_name, dict) else None
        manifest_record = manifest_by_name.get(name)
        route_keys = sorted({str(value) for value in governance.get("routeKeys", []) if value})
        knowledge_refs = []
        for item in knowledge.get(name, []):
            overlap = sorted(set(route_keys).intersection(item["overlappingRouteKeys"]))
            if overlap:
                knowledge_refs.append({**item, "overlappingRouteKeys": overlap})
        command_binding_status = command_status(governance, bindings.get(name, []), exemptions.get(name))
        command_catalog_match = all(binding["commandId"] in command_ids for binding in bindings.get(name, []))
        aispace = aispace_bindings.get(name)
        stale = (
            not isinstance(catalog_record, dict)
            or str(catalog_record.get("skillHash", "")) != skill_hash
            or str(catalog_record.get("governanceHash", "")) != governance_hash
            or not isinstance(manifest_record, dict)
            or str(manifest_record.get("skillHash", "")) != skill_hash
            or str(manifest_record.get("governanceHash", "")) != governance_hash
            or str(manifest_record.get("evidenceContractBindingHash", "")) != evidence_binding_hash
        )
        missing_required = (
            not isinstance(catalog_record, dict)
            or name not in resource_names
            or not isinstance(manifest_record, dict)
            or not aibrain_routes.get(name)
            or not knowledge_refs
            or not aliases.get("skills", {}).get(name)
            or command_binding_status == "missing"
            or not command_catalog_match
            or not evidence_binding_path.is_file()
            or not static_manifest_present
            or not adapter_present
        )
        relation_status = "stale" if stale else ("blocked" if missing_required and command_binding_status == "missing" else ("partial" if missing_required else "closed"))
        records.append({
            "skillName": name,
            "skillPath": f".agents/skills/{name}",
            "skillHash": skill_hash,
            "governanceHash": governance_hash,
            "evidenceContractBindingHash": evidence_binding_hash,
            "registrationState": str((catalog_record or {}).get("registrationState", "missing")),
            "discoveryState": str((catalog_record or {}).get("discoveryState", "missing")),
            "planEligibility": str((catalog_record or {}).get("planEligibility", "missing")),
            "runtimeEligibility": str((catalog_record or {}).get("runtimeEligibility", "missing")),
            "reviewRequired": bool((catalog_record or {}).get("reviewRequired", True)),
            "routeKeys": route_keys,
            "relations": {
                "catalog": {"path": ".agents/SKILL_CATALOG.yaml", "recordPresent": isinstance(catalog_record, dict), "hashMatch": isinstance(catalog_record, dict) and catalog_record.get("skillHash") == skill_hash and catalog_record.get("governanceHash") == governance_hash},
                "resourceIndex": {"path": ".agents/SKILL_RESOURCE_INDEX.yaml", "recordPresent": name in resource_names, "overlappingRouteKeys": sorted(set(route_keys).intersection({str(value) for value in resource_by_name.get(name, {}).get("routeKeys", []) if value}))},
                "registryManifest": {"path": ".agents/SKILL_REGISTRY.manifest.json", "recordPresent": isinstance(manifest_record, dict), "hashMatch": isinstance(manifest_record, dict) and manifest_record.get("skillHash") == skill_hash and manifest_record.get("governanceHash") == governance_hash},
                "aibrain": {"path": "Documentation/AIKnowledge/AIBRAIN_ENTRY.md", "discoverable": bool(aibrain_routes.get(name)), "routeAreas": aibrain_routes.get(name, [])},
                "knowledge": {"path": "Documentation/AIKnowledge/KnowledgeIndex.yaml", "refs": knowledge_refs},
                "aiCommand": {"path": ".agents/skills/es-skill-governance/references/command-binding-registry.json", "catalogPath": "Assets/Plugins/ES/AICommands/AICommandCatalog.json", "status": command_binding_status, "bindings": bindings.get(name, []), "catalogMatch": command_catalog_match, "exemption": exemptions.get(name)},
                "evidence": {"bindingPath": f".agents/skills/{name}/evidence-contract.binding.json", "bindingHash": evidence_binding_hash, "staticReplayManifest": static_manifest_present, "staticReplayAdapter": adapter_present},
                "authority": {"requiredAuthorityRefs": [str(value) for value in governance.get("requiredAuthorityRefs", []) if value], "aiWarningsRefs": [str(value) for value in governance.get("requiredAuthorityRefs", []) if "AIWarning" in str(value)]},
                "chineseAliases": {"path": ".agents/SKILL_ROUTE_ALIASES.zh-CN.json", "aliasCount": len(aliases.get("skills", {}).get(name, []))},
                "aispace": {
                    "path": AISPACE_BINDINGS_REL.as_posix(),
                    "registryPath": OUTPUT_REL.as_posix(),
                    "skillContractPath": (aispace or {}).get("skillContractRef", f".agents/skills/{name}/governance.json"),
                    "bindingCount": len((aispace or {}).get("bindings", [])),
                    "bindingIds": [str(binding.get("bindingId")) for binding in (aispace or {}).get("bindings", [])],
                    "bindings": (aispace or {}).get("bindings", []),
                    "status": "bound" if aispace else "not-bound",
                    "bidirectional": bool(aispace),
                },
            },
            "relationStatus": relation_status,
        })

    projection: dict[str, Any] = {
        "schemaVersion": 1,
        "registryId": "es-public-skills-relation-registry",
        "authority": "derived-navigation",
        "purpose": "Register stable relationships among direct Skills and their authoritative discovery, knowledge, command, evidence, and authority navigation sources; never grants execution permission.",
        "outputPath": OUTPUT_REL.as_posix(),
        "sourceOfTruth": [".agents/skills/<skill-name>/SKILL.md", ".agents/skills/<skill-name>/governance.json", ".agents/skills/<skill-name>/evidence-contract.binding.json", ".agents/SKILL_CATALOG.yaml", ".agents/SKILL_RESOURCE_INDEX.yaml", ".agents/SKILL_REGISTRY.manifest.json", "Documentation/AIKnowledge/AIBRAIN_ENTRY.md", "Documentation/AIKnowledge/KnowledgeIndex.yaml", ".agents/skills/es-skill-governance/references/command-binding-registry.json", "Assets/Plugins/ES/AICommands/AICommandCatalog.json", ".agents/SKILL_ROUTE_ALIASES.zh-CN.json", AISPACE_BINDINGS_REL.as_posix()],
        "relationTypes": ["catalog", "resourceIndex", "registryManifest", "aibrain", "knowledge", "aiCommand", "evidence", "authority", "chineseAliases", "aispace"],
        "sourceSnapshot": source_snapshot,
        "skills": records,
        "summary": {
            "skillCount": len(records),
            "closedCount": sum(record["relationStatus"] == "closed" for record in records),
            "partialCount": sum(record["relationStatus"] == "partial" for record in records),
            "staleCount": sum(record["relationStatus"] == "stale" for record in records),
            "blockedCount": sum(record["relationStatus"] == "blocked" for record in records),
        },
    }
    projection["registryHash"] = sha256(canonical(projection))
    return projection


def output_bytes(projection: dict[str, Any]) -> bytes:
    return (json.dumps(projection, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def validate_projection(root: Path, projection: dict[str, Any]) -> list[str]:
    expected = build_projection(root)
    errors: list[str] = []
    candidate = dict(projection)
    actual_hash = candidate.pop("registryHash", None)
    if actual_hash != sha256(canonical(candidate)):
        errors.append("registryHash does not match the canonical projection")
    if canonical(projection) != canonical(expected):
        errors.append("registry projection is stale; rebuild it with --write")
    if projection.get("summary", {}).get("closedCount") != len(projection.get("skills", [])):
        errors.append("not every Skill has a closed relationship set")
    names = [record.get("skillName") for record in projection.get("skills", [])]
    if len(names) != len(set(names)):
        errors.append("duplicate Skill relationship records")
    if any(Path(str(value)).is_absolute() for record in projection.get("skills", []) for value in [record.get("skillPath", "")]):
        errors.append("absolute Skill path leaked into the public registry")
    return errors


def acquire_lock(lock_path: Path, timeout: float = 10.0) -> int:
    deadline = time.monotonic() + timeout
    while True:
        try:
            return os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        except FileExistsError:
            if time.monotonic() >= deadline:
                raise RuntimeError("Skill relation registry writer lock is held by another process")
            time.sleep(0.05)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--write", action="store_true", help="atomically write the Public relation registry")
    parser.add_argument("--check", action="store_true", help="read-only drift and closure check")
    args = parser.parse_args()
    if args.write == args.check:
        parser.error("choose exactly one of --write or --check")
    root = Path(args.project_root).resolve(strict=True)
    output = resolve_inside(root, OUTPUT_REL, "Output path")
    projection = build_projection(root)
    if args.check:
        if not output.is_file():
            print("FAIL: relation registry is missing")
            return 1
        try:
            current = json.loads(output.read_text(encoding="utf-8"))
        except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
            print(f"FAIL: relation registry is unreadable: {exc}")
            return 1
        errors = validate_projection(root, current)
        if errors:
            for error in errors:
                print(f"FAIL: {error}")
            return 1
        print(f"PASS: {len(current.get('skills', []))} Skill relationships are current and closed")
        return 0

    output.parent.mkdir(parents=True, exist_ok=True)
    lock_path = Path(tempfile.gettempdir()) / f"es-skill-relation-{sha256(str(output).lower().encode('utf-8'))}.lock"
    handle = acquire_lock(lock_path)
    try:
        baseline = output.read_bytes() if output.is_file() else None
        data = output_bytes(projection)
        if output.is_file() and output.read_bytes() == data:
            print(f"UNCHANGED: {output}")
            return 0
        if (output.read_bytes() if output.is_file() else None) != baseline:
            raise RuntimeError("Public relation registry changed during the build")
        if build_projection(root)["registryHash"] != projection["registryHash"]:
            raise RuntimeError("Skill relationship inputs changed during the build")
        fd, temp_name = tempfile.mkstemp(prefix="registry.json.tmp-", dir=str(output.parent))
        try:
            with os.fdopen(fd, "wb") as stream:
                stream.write(data)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temp_name, output)
        finally:
            if os.path.exists(temp_name):
                os.unlink(temp_name)
        print(f"REGISTERED: {len(projection['skills'])} Skill relationships -> {output}")
        return 0
    finally:
        os.close(handle)
        try:
            lock_path.unlink()
        except FileNotFoundError:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
