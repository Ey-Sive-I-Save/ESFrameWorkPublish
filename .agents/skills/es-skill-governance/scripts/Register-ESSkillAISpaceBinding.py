#!/usr/bin/env python3
"""Register one Skill↔AISpace output binding with an atomic, bounded write."""
from __future__ import annotations

import argparse
import json
import os
import re
import tempfile
import time
from pathlib import Path
from typing import Any


BINDINGS_REL = Path(".agents/SKILL_AISPACE_BINDINGS.json")
ABSOLUTE_RE = re.compile(r"^(?:[A-Za-z]:[\\/]|/|\\\\)")
STORAGE_ROOTS = {
    "private-temp": "ES/AISpace/Local/",
    "private-content": "ES/AISpace/Local/",
    "public-index": "ES/AISpace/Public/",
    "public-content": "ES/AISpace/Public/",
    "unity-public": "Assets/ES/AISpace/Public/",
}


def fail(message: str) -> int:
    print(f"FAIL: {message}")
    return 1


def canonical(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def acquire_lock(lock_path: Path, timeout: float = 10.0) -> int:
    deadline = time.monotonic() + timeout
    while True:
        try:
            return os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        except FileExistsError:
            if time.monotonic() >= deadline:
                raise RuntimeError("AISpace binding writer lock is held by another process")
            time.sleep(0.05)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--skill-name", required=True)
    parser.add_argument("--binding-id", required=True)
    parser.add_argument("--purpose", required=True)
    parser.add_argument("--storage-class", required=True, choices=sorted(STORAGE_ROOTS))
    parser.add_argument("--path-template", required=True)
    parser.add_argument("--content-authority", required=True)
    parser.add_argument("--lifecycle", required=True)
    parser.add_argument("--retention", required=True)
    parser.add_argument("--write-policy", required=True)
    parser.add_argument("--artifact-kind", action="append", dest="artifact_kinds", required=True)
    parser.add_argument("--write", action="store_true", help="atomically update the project binding registry")
    args = parser.parse_args()
    if not args.write:
        return fail("registration is a project write; pass --write explicitly")
    root = Path(args.project_root).resolve(strict=True)
    binding_path = root / BINDINGS_REL
    skill_dir = root / ".agents/skills" / args.skill_name
    if not re.fullmatch(r"es-[a-z0-9-]+", args.skill_name):
        return fail("skill-name must use the direct lowercase es- form")
    if not (skill_dir / "SKILL.md").is_file() or not (skill_dir / "governance.json").is_file():
        return fail("Skill must have SKILL.md and governance.json before AISpace binding registration")
    path_template = args.path_template.replace("\\", "/")
    if ABSOLUTE_RE.match(path_template) or ".." in Path(path_template).parts:
        return fail("path-template must be project-relative and cannot contain '..'")
    if not path_template.startswith(STORAGE_ROOTS[args.storage_class]):
        return fail(f"path-template must remain under the {args.storage_class} canonical root")
    try:
        registry = json.loads(binding_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return fail(f"binding registry is unreadable: {exc}")
    if registry.get("schemaVersion") != 1 or registry.get("canonicalConcept") != "AISpace":
        return fail("binding registry schema or canonical concept is invalid")
    entries = registry.get("skills")
    if not isinstance(entries, list):
        return fail("binding registry skills must be an array")
    candidate = {
        "bindingId": args.binding_id,
        "purpose": args.purpose,
        "storageClass": args.storage_class,
        "pathTemplate": path_template,
        "contentAuthority": args.content_authority,
        "lifecycle": args.lifecycle,
        "artifactKinds": sorted(set(args.artifact_kinds)),
        "retention": args.retention,
        "writePolicy": args.write_policy,
    }
    all_ids = {
        str(binding.get("bindingId"))
        for entry in entries if isinstance(entry, dict)
        for binding in entry.get("bindings", []) if isinstance(entry.get("bindings", []), list)
        if isinstance(binding, dict) and binding.get("bindingId")
    }
    existing_entry = next((entry for entry in entries if isinstance(entry, dict) and entry.get("skillName") == args.skill_name), None)
    if args.binding_id in all_ids:
        if existing_entry and any(binding == candidate for binding in existing_entry.get("bindings", [])):
            print(f"UNCHANGED: {args.binding_id}")
            return 0
        return fail(f"bindingId collision: {args.binding_id}")
    if existing_entry is None:
        existing_entry = {
            "skillName": args.skill_name,
            "skillContractRef": f".agents/skills/{args.skill_name}/governance.json",
            "bindings": [],
        }
        entries.append(existing_entry)
    existing_entry.setdefault("bindings", []).append(candidate)
    registry["skills"] = sorted(entries, key=lambda entry: str(entry.get("skillName", "")))
    data = (json.dumps(registry, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    lock_path = Path(tempfile.gettempdir()) / "es-skill-aispace-bindings.lock"
    handle = acquire_lock(lock_path)
    try:
        baseline = binding_path.read_bytes()
        if binding_path.read_bytes() != baseline:
            raise RuntimeError("AISpace binding registry changed during registration")
        fd, temp_name = tempfile.mkstemp(prefix="SKILL_AISPACE_BINDINGS.json.tmp-", dir=str(binding_path.parent))
        try:
            with os.fdopen(fd, "wb") as stream:
                stream.write(data)
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temp_name, binding_path)
        finally:
            if os.path.exists(temp_name):
                os.unlink(temp_name)
    finally:
        os.close(handle)
        try:
            lock_path.unlink()
        except FileNotFoundError:
            pass
    print(f"REGISTERED: {args.binding_id} -> {BINDINGS_REL.as_posix()}")
    print("NEXT: rebuild Skill Catalog, Registry Manifest, AISpace relation registry, then run Test-ESSkillAISpaceBindings.py")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
