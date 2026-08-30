#!/usr/bin/env python3
"""Read-only validator for the project Skill↔AISpace registration contract."""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path


BINDINGS_REL = Path(".agents/SKILL_AISPACE_BINDINGS.json")
REGISTRY_REL = Path("ES/AISpace/Public/Skills/registry.json")
ABSOLUTE_RE = re.compile(r"^(?:[A-Za-z]:[\\/]|/|\\\\)")


def fail(message: str) -> int:
    print(f"FAIL: {message}")
    return 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    args = parser.parse_args()
    root = Path(args.project_root).resolve(strict=True)
    bindings_path = root / BINDINGS_REL
    registry_path = root / REGISTRY_REL
    if not bindings_path.is_file():
        return fail("AISpace Skill binding registry is missing")
    try:
        bindings = json.loads(bindings_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return fail(f"AISpace Skill binding registry is unreadable: {exc}")
    if bindings.get("canonicalConcept") != "AISpace":
        return fail("canonicalConcept must be AISpace")
    if bindings.get("relationProjection") != REGISTRY_REL.as_posix():
        return fail("relationProjection must point to the public Skill registry")
    if not isinstance(bindings.get("canonicalRoots"), dict):
        return fail("canonicalRoots must be an object")
    if bindings["canonicalRoots"].get("privateTemp") != "ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/":
        return fail("privateTemp root is not the classification-first date template")

    builder_path = root / ".agents/skills/es-skill-governance/scripts/Build-ESSkillRelationRegistry.py"
    spec = importlib.util.spec_from_file_location("es_skill_relation_builder", builder_path)
    if spec is None or spec.loader is None:
        return fail("cannot load relation builder")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    direct = module.skill_names(root)
    try:
        declared = module.collect_aispace_bindings(bindings, direct)
    except ValueError as exc:
        return fail(str(exc))
    ids = [binding_id for entry in declared.values() for binding_id in (str(item.get("bindingId")) for item in entry["bindings"])]
    if len(ids) != len(set(ids)):
        return fail("AISpace bindingId values are not unique")
    for entry in bindings.get("skills", []):
        if ABSOLUTE_RE.match(str(entry.get("skillContractRef", ""))):
            return fail("absolute Skill governance path leaked into the binding registry")
    if not registry_path.is_file():
        return fail("public Skill relation registry is missing")
    try:
        registry = json.loads(registry_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return fail(f"public Skill relation registry is unreadable: {exc}")
    by_name = {record.get("skillName"): record for record in registry.get("skills", []) if isinstance(record, dict)}
    for name, entry in declared.items():
        if not (root / entry["skillContractRef"]).is_file():
            return fail(f"Skill governance contract is missing for {name}")
        relation = by_name.get(name, {}).get("relations", {}).get("aispace")
        if relation is None or relation.get("status") != "bound":
            return fail(f"AISpace reverse relation is missing for {name}")
        if sorted(relation.get("bindingIds", [])) != sorted(str(item.get("bindingId")) for item in entry["bindings"]):
            return fail(f"AISpace binding projection drift for {name}")
    print(f"PASS: {len(declared)} Skills have stable AISpace bindings and reverse registry references ({len(ids)} bindings)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
