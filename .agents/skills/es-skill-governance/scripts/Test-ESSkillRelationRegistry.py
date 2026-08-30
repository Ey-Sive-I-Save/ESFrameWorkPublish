#!/usr/bin/env python3
"""Read-only validator for the AISpace Public Skill relationship registry."""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path


OUTPUT_REL = Path("ES/AISpace/Public/Skills/registry.json")
ABSOLUTE_RE = re.compile(r"^(?:[A-Za-z]:[\\/]|/|\\\\)")


def fail(message: str) -> int:
    print(f"FAIL: {message}")
    return 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    args = parser.parse_args()
    root = Path(args.project_root).resolve(strict=True)
    builder = root / ".agents/skills/es-skill-governance/scripts/Build-ESSkillRelationRegistry.py"
    output = root / OUTPUT_REL
    if not builder.is_file() or not output.is_file():
        return fail("builder or public relation registry is missing")

    check = subprocess.run(
        [sys.executable, str(builder), "--project-root", str(root), "--check"],
        cwd=str(root),
        text=True,
        encoding="utf-8",
        capture_output=True,
    )
    if check.returncode != 0:
        print(check.stdout, end="")
        print(check.stderr, end="")
        return fail("builder drift check failed")

    spec = importlib.util.spec_from_file_location("es_skill_relation_builder", builder)
    if spec is None or spec.loader is None:
        return fail("cannot load relation builder for deterministic-output verification")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    expected_bytes = module.output_bytes(module.build_projection(root))
    if output.read_bytes() != expected_bytes:
        return fail("registry bytes are not the deterministic builder output")

    try:
        registry = json.loads(output.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return fail(f"registry JSON is invalid: {exc}")
    records = registry.get("skills")
    if not isinstance(records, list):
        return fail("skills must be an array")
    names = [record.get("skillName") for record in records if isinstance(record, dict)]
    direct = sorted(
        path.name
        for path in (root / ".agents/skills").iterdir()
        if path.is_dir() and (path / "SKILL.md").is_file()
    )
    if sorted(names) != direct or len(names) != len(set(names)):
        return fail("relationship records do not match the direct Skill inventory exactly")
    if registry.get("summary", {}).get("closedCount") != len(direct):
        return fail("not every direct Skill has a closed relationship")
    required_relations = {"catalog", "resourceIndex", "registryManifest", "aibrain", "knowledge", "aiCommand", "evidence", "authority", "chineseAliases", "aispace"}
    for record in records:
        if record.get("relationStatus") != "closed":
            return fail(f"Skill is not closed: {record.get('skillName')}")
        if set(record.get("relations", {})) != required_relations:
            return fail(f"relationship set is incomplete: {record.get('skillName')}")
        for value in (record.get("skillPath"),):
            if not isinstance(value, str) or ABSOLUTE_RE.match(value):
                return fail("absolute path leaked into a public relationship record")
        aispace = record.get("relations", {}).get("aispace")
        if not isinstance(aispace, dict) or aispace.get("registryPath") != OUTPUT_REL.as_posix():
            return fail(f"AISpace reverse relation is missing: {record.get('skillName')}")
        if aispace.get("status") == "bound":
            if not aispace.get("bidirectional") or not aispace.get("skillContractPath", "").endswith("/governance.json"):
                return fail(f"AISpace binding is not bidirectional: {record.get('skillName')}")
    print(f"PASS: {len(direct)} Skill relationships are registered, current, closed, and project-relative")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
