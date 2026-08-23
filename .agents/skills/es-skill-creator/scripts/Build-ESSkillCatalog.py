#!/usr/bin/env python3
"""Build the project Skill classification/lifecycle catalog.

The catalog is derived navigation metadata. It never grants permission; the
governance.json and AIWarnings/AICommands remain authoritative for execution.
"""
from __future__ import annotations

import argparse
import hashlib
import re
from datetime import datetime, timezone
from pathlib import Path

import yaml


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def parse_resource_index(path: Path) -> dict[str, dict[str, object]]:
    data: dict[str, dict[str, object]] = {}
    current = None
    raw = path.read_text(encoding="utf-8")
    for line in raw.splitlines():
        if line.startswith("currentSkills:"):
            current = "current"
            continue
        if line.startswith("newSkills:"):
            current = "new"
            continue
        m = re.match(r"^\s*-\s*\{name:\s*([^,]+),\s*family:\s*([^,]+),\s*routeKeys:\s*\[([^]]*)\],\s*mcp:\s*\[([^]]*)\]\}", line)
        if m:
            name, family, routes, mcp = m.groups()
            data[name.strip()] = {
                "family": family.strip(),
                "routeKeys": [x.strip() for x in routes.split(",") if x.strip()],
                "mcp": [x.strip() for x in mcp.split(",") if x.strip()],
            }
            continue
        if current == "new":
            m = re.match(r"^\s{2}([A-Za-z0-9_-]+):\s*\[([^]]*)\]", line)
            if m:
                family, names = m.groups()
                for name in names.split(","):
                    name = name.strip()
                    if name:
                        data.setdefault(name, {"family": family, "routeKeys": [], "mcp": ["none-required"]})
    return data


def display_name(skill_dir: Path, name: str) -> str:
    ui = skill_dir / "agents" / "openai.yaml"
    if ui.exists():
        for line in ui.read_text(encoding="utf-8").splitlines():
            m = re.match(r"^display_name:\s*[\"']?(.*?)[\"']?\s*$", line)
            if m and m.group(1):
                return m.group(1)
    return name


def discovery_eligibility(policy: dict[str, object], maturity: str, delivery: str,
                          registration_state: str) -> dict[str, object]:
    states = policy.get("states", {})
    state = states.get(maturity)
    if not isinstance(state, dict):
        raise ValueError(f"maturity is not registered in SKILL_DISCOVERY_POLICY.json: {maturity}")
    result = {
        "discoveryState": state.get("discoveryState", ""),
        "planEligibility": state.get("planEligibility", ""),
        "runtimeEligibility": state.get("runtimeEligibility", ""),
    }
    override = (policy.get("deliveryOverrides", {}) or {}).get(delivery)
    if isinstance(override, dict):
        for field in ("discoveryState", "planEligibility", "runtimeEligibility"):
            if override.get(field):
                result[field] = override[field]
    registration = (policy.get("registrationOverrides", {}) or {}).get(registration_state)
    result["reviewRequired"] = True if not isinstance(registration, dict) else bool(registration.get("reviewRequired", True))
    if any(not result[field] for field in ("discoveryState", "planEligibility", "runtimeEligibility")):
        raise ValueError(f"incomplete discovery policy result for {maturity}/{delivery}")
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--catalog", default=".agents/SKILL_CATALOG.yaml")
    parser.add_argument("--write", action="store_true", help="required to mutate the catalog")
    args = parser.parse_args()
    root = Path(args.project_root).resolve()
    if not args.write:
        parser.error("--write is required; catalog registration is an explicit write")
    skills_root = root / ".agents" / "skills"
    resource_index = root / ".agents" / "SKILL_RESOURCE_INDEX.yaml"
    discovery_policy_path = root / ".agents" / "SKILL_DISCOVERY_POLICY.json"
    if not discovery_policy_path.exists():
        raise ValueError(f"Missing discovery policy: {discovery_policy_path}")
    discovery_policy = yaml.safe_load(discovery_policy_path.read_text(encoding="utf-8")) or {}
    catalog_path = (root / args.catalog).resolve()
    mappings = parse_resource_index(resource_index)
    old = {}
    if catalog_path.exists():
        old = (yaml.safe_load(catalog_path.read_text(encoding="utf-8")) or {}).get("skills", {})
    now = utc_now()
    records = []
    for skill_dir in sorted(p for p in skills_root.iterdir() if p.is_dir()):
        name = skill_dir.name
        skill_md = skill_dir / "SKILL.md"
        gov_path = skill_dir / "governance.json"
        if not skill_md.exists():
            continue
        is_draft = not gov_path.exists()
        gov = (yaml.safe_load(gov_path.read_text(encoding="utf-8")) or {}) if not is_draft else {}
        mapping = mappings.get(name, {})
        previous = old.get(name, {}) if isinstance(old, dict) else {}
        mtimes = [p.stat().st_mtime for p in skill_dir.rglob("*") if p.is_file()]
        latest = datetime.fromtimestamp(max(mtimes), timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
        maturity = str(gov.get("maturity", "Draft" if is_draft else "Proposed"))
        delivery = str(gov.get("delivery", "NotReady" if is_draft else "Designed"))
        state = "Draft" if is_draft else ("Archived" if maturity == "Archived" else ("NeedsReview" if delivery != "Accepted" and delivery != "Released" else "Registered"))
        registration_state = previous.get("registrationState", state)
        eligibility = discovery_eligibility(discovery_policy, maturity, delivery, registration_state) if not is_draft else {
            "discoveryState": "candidate", "planEligibility": "advisory-only",
            "runtimeEligibility": "blocked", "reviewRequired": True,
        }
        records.append({
            "name": name,
            "displayName": display_name(skill_dir, name),
            "skillPath": f".agents/skills/{name}",
            "family": mapping.get("family", previous.get("family", "unclassified")),
            "routeKeys": gov.get("routeKeys", mapping.get("routeKeys", [])),
            "mcp": mapping.get("mcp", ["none-required"]),
            "tier": gov.get("tier", "Workflow"),
            "maturity": maturity,
            "delivery": delivery,
            "registrationState": registration_state,
            **eligibility,
            "evidenceLevel": gov.get("evidenceLevel", "S0"),
            "riskClass": gov.get("riskClass", "unspecified"),
            "owner": gov.get("owner", "ESFramework Skill maintainers"),
            "acceptanceOwner": gov.get("acceptanceOwner", "designated ESFramework maintainer"),
            "firstRegisteredUtc": previous.get("firstRegisteredUtc", now),
            "lastModifiedUtc": latest,
            "lastReviewedUtc": previous.get("lastReviewedUtc", now),
            "skillHash": sha256(skill_md),
            "governanceHash": sha256(gov_path) if not is_draft else None,
            "statusNote": previous.get("statusNote", "Draft registration: governance.json required before execution or acceptance" if is_draft else "Initial catalog registration"),
        })
    catalog = {
        "schemaVersion": 1,
        "catalogId": "esframework-skill-catalog",
        "status": "active",
        "authority": "derived-navigation",
        "purpose": "Classify and track lifecycle of direct-child project Skills; never grants execution permission.",
        "sourceRoot": ".agents/skills",
        "resourceIndex": ".agents/SKILL_RESOURCE_INDEX.yaml",
        "discoveryPolicy": ".agents/SKILL_DISCOVERY_POLICY.json",
        "registryManifest": ".agents/SKILL_REGISTRY.manifest.json",
        "registrationRule": "Every direct Skill root with SKILL.md must have exactly one catalog record before acceptance.",
        "hashRule": "skillHash and governanceHash detect stale registration; refresh the record after every Skill change.",
        "generatedAtUtc": now,
        "skills": {r["name"]: r for r in records},
    }
    catalog_path.parent.mkdir(parents=True, exist_ok=True)
    catalog_path.write_text(yaml.safe_dump(catalog, sort_keys=False, allow_unicode=True, width=120), encoding="utf-8", newline="\n")
    print(f"REGISTERED: {len(records)} Skills -> {catalog_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
