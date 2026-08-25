#!/usr/bin/env python3
"""Build the project Skill classification/lifecycle catalog.

The catalog is derived navigation metadata. It never grants permission; the
governance.json and AIWarnings/AICommands remain authoritative for execution.

Canonical parent rechecks detect observed path drift. They do not make the
path-based final replace immune to a malicious local actor swapping a parent
directory after the final check; that residual stays outside this tool's claim.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import tempfile
import time
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable, Iterator

import yaml


CATALOG_RELATIVE_PATH = Path(".agents/SKILL_CATALOG.yaml")
CATALOG_ID = "esframework-skill-catalog"
LOCK_TIMEOUT_SECONDS = 10.0
LOCK_POLL_SECONDS = 0.05


@dataclass(frozen=True)
class CapturedFile:
    resolved_path: str | None
    data: bytes | None


@dataclass(frozen=True)
class SkillInput:
    name: str
    resolved_path: str
    skill_md: CapturedFile
    governance: CapturedFile
    openai: CapturedFile


@dataclass(frozen=True)
class InputSnapshot:
    fingerprint: str
    resource_index: CapturedFile
    discovery_policy: CapturedFile
    skills: tuple[SkillInput, ...]


@dataclass(frozen=True)
class OutputState:
    exists: bool
    digest: str | None
    data: bytes | None


class CatalogConflictError(RuntimeError):
    """The input or output generation changed while a build was in progress."""


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def resolve_within_project(project_root: Path, candidate: str | Path, label: str) -> Path:
    """Resolve an input path and reject lexical or link-based project escapes."""
    path = Path(candidate)
    if not path.is_absolute():
        path = project_root / path
    try:
        resolved = path.resolve(strict=False)
        resolved.relative_to(project_root)
    except (OSError, RuntimeError, ValueError) as exc:
        raise ValueError(f"{label} escapes project root: {candidate}") from exc
    return resolved


def revalidate_catalog_target(project_root: Path, catalog_path: Path) -> None:
    try:
        current_root = project_root.resolve(strict=True)
        agents_parent = (project_root / ".agents").resolve(strict=True)
        agents_parent.relative_to(current_root)
    except (OSError, RuntimeError, ValueError) as exc:
        raise CatalogConflictError("Catalog parent canonical identity is no longer valid") from exc
    expected = agents_parent / CATALOG_RELATIVE_PATH.name
    if current_root != project_root or expected != catalog_path or not agents_parent.is_dir():
        raise CatalogConflictError("Catalog parent canonical identity changed")
    if catalog_path.exists():
        try:
            if catalog_path.resolve(strict=True) != catalog_path or not catalog_path.is_file():
                raise CatalogConflictError("Catalog target became a link, reparse point, or non-file")
        except OSError as exc:
            raise CatalogConflictError("Catalog target identity could not be revalidated") from exc


def resolve_catalog_target(project_root: Path, catalog_argument: str | Path) -> Path:
    agents_parent = resolve_within_project(project_root, ".agents", "Catalog parent")
    if not agents_parent.is_dir():
        raise ValueError(f"Catalog parent is not a directory: {agents_parent}")
    expected = agents_parent / CATALOG_RELATIVE_PATH.name
    candidate = resolve_within_project(project_root, catalog_argument, "Catalog path")
    if candidate != expected:
        raise ValueError(
            f"Catalog path must be exactly {CATALOG_RELATIVE_PATH.as_posix()}: {catalog_argument}"
        )
    revalidate_catalog_target(project_root, expected)
    return expected


def catalog_writer_lock_path(catalog_path: Path) -> Path:
    lock_root = Path(tempfile.gettempdir()).resolve(strict=True)
    identity = os.path.normcase(str(catalog_path)).encode("utf-8")
    return lock_root / f"es-skill-catalog-{hashlib.sha256(identity).hexdigest()}.lock"


def _try_lock(handle: object) -> bool:
    if os.name == "nt":
        import msvcrt

        handle.seek(0)
        try:
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            return True
        except OSError as exc:
            if exc.errno in (13, 11, 36):
                return False
            raise

    import fcntl

    try:
        fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        return True
    except BlockingIOError:
        return False


def _unlock(handle: object) -> None:
    if os.name == "nt":
        import msvcrt

        handle.seek(0)
        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
        return

    import fcntl

    fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


@contextmanager
def catalog_writer_lock(catalog_path: Path, timeout_seconds: float | None = None) -> Iterator[Path]:
    timeout = LOCK_TIMEOUT_SECONDS if timeout_seconds is None else timeout_seconds
    if timeout < 0:
        raise ValueError("Lock timeout must be non-negative")
    lock_path = catalog_writer_lock_path(catalog_path)
    flags = os.O_RDWR | os.O_CREAT
    if hasattr(os, "O_BINARY"):
        flags |= os.O_BINARY
    if hasattr(os, "O_NOINHERIT"):
        flags |= os.O_NOINHERIT
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(lock_path, flags, 0o600)
    handle = os.fdopen(descriptor, "r+b", buffering=0)
    acquired = False
    try:
        if not stat.S_ISREG(os.fstat(handle.fileno()).st_mode):
            raise ValueError(f"Catalog lock is not a regular file: {lock_path}")
        deadline = time.monotonic() + timeout
        while not _try_lock(handle):
            if time.monotonic() >= deadline:
                raise TimeoutError(f"Timed out waiting for Catalog writer lock: {lock_path}")
            time.sleep(min(LOCK_POLL_SECONDS, max(0.0, deadline - time.monotonic())))
        acquired = True
        yield lock_path
    finally:
        if acquired:
            _unlock(handle)
        handle.close()


def atomic_write_text(
    path: Path,
    text: str,
    before_replace: Callable[[], None] | None = None,
) -> bool:
    """Stage UTF-8 beside the target, fsync it, then atomically replace it."""
    payload = text.encode("utf-8")
    target_mode: int | None = None
    if path.exists():
        if not path.is_file():
            raise ValueError(f"Catalog target is not a file: {path}")
        if path.read_bytes() == payload:
            if before_replace is not None:
                before_replace()
            return False
        target_mode = stat.S_IMODE(path.stat().st_mode)

    if not path.parent.is_dir():
        raise ValueError(f"Catalog parent is not a directory: {path.parent}")
    descriptor: int | None = None
    temporary_path: Path | None = None
    try:
        descriptor, temporary_name = tempfile.mkstemp(
            dir=path.parent,
            prefix=f".{path.name}.",
            suffix=".tmp",
        )
        temporary_path = Path(temporary_name)
        stream = os.fdopen(descriptor, "w", encoding="utf-8", newline="\n")
        descriptor = None
        with stream:
            stream.write(text)
            stream.flush()
            os.fsync(stream.fileno())
        if target_mode is not None:
            os.chmod(temporary_path, target_mode)
        if before_replace is not None:
            before_replace()
        os.replace(temporary_path, path)
        temporary_path = None
        return True
    finally:
        if descriptor is not None:
            os.close(descriptor)
        if temporary_path is not None:
            try:
                temporary_path.unlink()
            except FileNotFoundError:
                pass


def decode_utf8(data: bytes, label: str) -> str:
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise ValueError(f"{label} is not valid UTF-8") from exc


def load_yaml_mapping(data: bytes, label: str, allow_empty: bool = False) -> dict[str, object]:
    text = decode_utf8(data, label)
    try:
        value = yaml.safe_load(text)
    except yaml.YAMLError as exc:
        raise ValueError(f"{label} is not valid YAML") from exc
    if value is None and allow_empty:
        return {}
    if not isinstance(value, dict):
        raise ValueError(f"{label} must contain a YAML mapping")
    return value


def _capture_optional_file(project_root: Path, path: Path, label: str) -> CapturedFile:
    if not path.exists():
        return CapturedFile(None, None)
    resolved = resolve_within_project(project_root, path, label)
    if not resolved.is_file():
        raise ValueError(f"{label} is not a file: {resolved}")
    relative = resolved.relative_to(project_root).as_posix()
    return CapturedFile(relative, resolved.read_bytes())


def _file_projection(captured: CapturedFile) -> dict[str, str | None]:
    return {
        "resolvedPath": captured.resolved_path,
        "sha256": sha256_bytes(captured.data) if captured.data is not None else None,
    }


def capture_input_snapshot(project_root: Path) -> InputSnapshot:
    skills_root = resolve_within_project(project_root, ".agents/skills", "Skills root")
    if not skills_root.is_dir():
        raise ValueError(f"Skills root is not a directory: {skills_root}")
    resource_index = _capture_optional_file(
        project_root,
        project_root / ".agents" / "SKILL_RESOURCE_INDEX.yaml",
        "Resource index",
    )
    discovery_policy = _capture_optional_file(
        project_root,
        project_root / ".agents" / "SKILL_DISCOVERY_POLICY.json",
        "Discovery policy",
    )
    if resource_index.data is None:
        raise ValueError("Resource index is missing")
    if discovery_policy.data is None:
        raise ValueError("Discovery policy is missing")

    inventory: list[dict[str, str]] = []
    skills: list[SkillInput] = []
    for discovered in sorted(skills_root.iterdir(), key=lambda item: item.name):
        if not discovered.is_dir():
            continue
        resolved_skill = resolve_within_project(skills_root, discovered, f"Skill path '{discovered.name}'")
        resolved_relative = resolved_skill.relative_to(project_root).as_posix()
        inventory.append({"name": discovered.name, "resolvedPath": resolved_relative})
        skill_md = _capture_optional_file(
            resolved_skill,
            resolved_skill / "SKILL.md",
            f"Skill source '{discovered.name}/SKILL.md'",
        )
        if skill_md.data is None:
            governance = CapturedFile(None, None)
            openai = CapturedFile(None, None)
        else:
            governance = _capture_optional_file(
                resolved_skill,
                resolved_skill / "governance.json",
                f"Skill governance '{discovered.name}/governance.json'",
            )
            openai = _capture_optional_file(
                resolved_skill,
                resolved_skill / "agents" / "openai.yaml",
                f"Skill UI metadata '{discovered.name}/agents/openai.yaml'",
            )
        skills.append(
            SkillInput(
                name=discovered.name,
                resolved_path=resolved_relative,
                skill_md=skill_md,
                governance=governance,
                openai=openai,
            )
        )

    projection = {
        "inventory": inventory,
        "files": {
            ".agents/SKILL_RESOURCE_INDEX.yaml": _file_projection(resource_index),
            ".agents/SKILL_DISCOVERY_POLICY.json": _file_projection(discovery_policy),
            **{
                f"{skill.resolved_path}/{suffix}": _file_projection(captured)
                for skill in skills
                for suffix, captured in (
                    ("SKILL.md", skill.skill_md),
                    ("governance.json", skill.governance),
                    ("agents/openai.yaml", skill.openai),
                )
            },
        },
    }
    fingerprint = sha256_bytes(
        json.dumps(projection, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    )
    return InputSnapshot(
        fingerprint=fingerprint,
        resource_index=resource_index,
        discovery_policy=discovery_policy,
        skills=tuple(skills),
    )


def capture_output_state(path: Path) -> OutputState:
    if not path.exists():
        return OutputState(False, None, None)
    if not path.is_file():
        raise ValueError(f"Catalog target is not a file: {path}")
    data = path.read_bytes()
    return OutputState(True, sha256_bytes(data), data)


def assert_output_cas(path: Path, expected: OutputState) -> OutputState:
    current = capture_output_state(path)
    if current.exists != expected.exists or current.digest != expected.digest:
        raise CatalogConflictError("Catalog output existence/hash changed during generation")
    return current


def load_old_catalog(state: OutputState) -> dict[str, object]:
    if not state.exists:
        return {}
    if state.data is None:
        raise ValueError("Existing Catalog bytes are unavailable")
    catalog = load_yaml_mapping(state.data, "Existing Catalog")
    if type(catalog.get("schemaVersion")) is not int or catalog.get("schemaVersion") != 1:
        raise ValueError("Existing Catalog schemaVersion must be integer 1")
    if catalog.get("catalogId") != CATALOG_ID:
        raise ValueError(f"Existing Catalog catalogId must be {CATALOG_ID}")
    skills = catalog.get("skills", {})
    if not isinstance(skills, dict):
        raise ValueError("Existing Catalog skills must be a mapping")
    if any(not isinstance(record, dict) for record in skills.values()):
        raise ValueError("Existing Catalog Skill records must be mappings")
    return catalog


def prior_timestamp(record: dict[str, object], field: str, fallback: str) -> str:
    value = record.get(field)
    return value if isinstance(value, str) and value else fallback


def without_field(record: dict[str, object], field: str) -> dict[str, object]:
    return {key: value for key, value in record.items() if key != field}


def parse_resource_index(raw: str) -> dict[str, dict[str, object]]:
    data: dict[str, dict[str, object]] = {}
    current = None
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


def display_name(openai: CapturedFile, name: str) -> str:
    if openai.data is not None:
        for line in decode_utf8(openai.data, f"Skill UI metadata '{name}'").splitlines():
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


def build_catalog(snapshot: InputSnapshot, old_catalog: dict[str, object], now: str) -> dict[str, object]:
    if snapshot.resource_index.data is None or snapshot.discovery_policy.data is None:
        raise ValueError("Catalog input snapshot is incomplete")
    mappings = parse_resource_index(decode_utf8(snapshot.resource_index.data, "Resource index"))
    discovery_policy = load_yaml_mapping(snapshot.discovery_policy.data, "Discovery policy")
    old_skills = old_catalog.get("skills", {})
    if not isinstance(old_skills, dict):
        raise ValueError("Existing Catalog skills must be a mapping")

    records: list[dict[str, object]] = []
    for skill in snapshot.skills:
        if skill.skill_md.data is None:
            continue
        decode_utf8(skill.skill_md.data, f"Skill source '{skill.name}/SKILL.md'")
        is_draft = skill.governance.data is None
        gov = (
            load_yaml_mapping(skill.governance.data, f"Skill governance '{skill.name}'", allow_empty=True)
            if skill.governance.data is not None
            else {}
        )
        mapping = mappings.get(skill.name, {})
        previous = old_skills.get(skill.name, {})
        if not isinstance(previous, dict):
            raise ValueError(f"Existing Catalog record must be a mapping: {skill.name}")
        maturity = str(gov.get("maturity", "Draft" if is_draft else "Proposed"))
        delivery = str(gov.get("delivery", "NotReady" if is_draft else "Designed"))
        state = "Draft" if is_draft else (
            "Archived" if maturity == "Archived" else (
                "NeedsReview" if delivery not in ("Accepted", "Released") else "Registered"
            )
        )
        registration_state = previous.get("registrationState", state)
        eligibility = discovery_eligibility(
            discovery_policy,
            maturity,
            delivery,
            str(registration_state),
        ) if not is_draft else {
            "discoveryState": "candidate",
            "planEligibility": "advisory-only",
            "runtimeEligibility": "blocked",
            "reviewRequired": True,
        }
        first_registered = prior_timestamp(previous, "firstRegisteredUtc", now)
        last_reviewed = prior_timestamp(previous, "lastReviewedUtc", now)
        record: dict[str, object] = {
            "name": skill.name,
            "displayName": display_name(skill.openai, skill.name),
            "skillPath": f".agents/skills/{skill.name}",
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
            "firstRegisteredUtc": first_registered,
            "lastModifiedUtc": None,
            "lastReviewedUtc": last_reviewed,
            "skillHash": sha256_bytes(skill.skill_md.data),
            "governanceHash": sha256_bytes(skill.governance.data) if not is_draft else None,
            "statusNote": previous.get(
                "statusNote",
                "Draft registration: governance.json required before execution or acceptance"
                if is_draft else "Initial catalog registration",
            ),
        }
        previous_projection = without_field(previous, "lastModifiedUtc")
        current_projection = without_field(record, "lastModifiedUtc")
        record["lastModifiedUtc"] = (
            prior_timestamp(previous, "lastModifiedUtc", now)
            if previous_projection == current_projection
            else now
        )
        records.append(record)

    catalog: dict[str, object] = {
        "schemaVersion": 1,
        "catalogId": CATALOG_ID,
        "status": "active",
        "authority": "derived-navigation",
        "purpose": "Classify and track lifecycle of direct-child project Skills; never grants execution permission.",
        "sourceRoot": ".agents/skills",
        "resourceIndex": ".agents/SKILL_RESOURCE_INDEX.yaml",
        "discoveryPolicy": ".agents/SKILL_DISCOVERY_POLICY.json",
        "registryManifest": ".agents/SKILL_REGISTRY.manifest.json",
        "registrationRule": "Every direct Skill root with SKILL.md must have exactly one catalog record before acceptance.",
        "hashRule": "skillHash covers SKILL.md and governanceHash covers governance.json; lastModifiedUtc changes only when the generated catalog record projection changes. Other Skill resources are outside this hash projection.",
        "generatedAtUtc": None,
        "skills": {record["name"]: record for record in records},
    }
    previous_generated = prior_timestamp(old_catalog, "generatedAtUtc", now)
    old_projection = without_field(old_catalog, "generatedAtUtc")
    current_projection = without_field(catalog, "generatedAtUtc")
    catalog["generatedAtUtc"] = previous_generated if old_projection == current_projection else now
    return catalog


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--catalog", default=CATALOG_RELATIVE_PATH.as_posix())
    parser.add_argument("--write", action="store_true", help="required to mutate the catalog")
    args = parser.parse_args()
    root = Path(args.project_root).resolve(strict=True)
    if not root.is_dir():
        raise ValueError(f"Project root is not a directory: {root}")
    if not args.write:
        parser.error("--write is required; catalog registration is an explicit write")
    catalog_path = resolve_catalog_target(root, args.catalog)

    with catalog_writer_lock(catalog_path):
        revalidate_catalog_target(root, catalog_path)
        initial_output = capture_output_state(catalog_path)
        old_catalog = load_old_catalog(initial_output)
        initial_inputs = capture_input_snapshot(root)
        catalog = build_catalog(initial_inputs, old_catalog, utc_now())
        serialized = yaml.safe_dump(catalog, sort_keys=False, allow_unicode=True, width=120)

        def verify_cas() -> None:
            revalidate_catalog_target(root, catalog_path)
            current_inputs = capture_input_snapshot(root)
            if current_inputs.fingerprint != initial_inputs.fingerprint:
                raise CatalogConflictError("Catalog input snapshot changed during generation")
            assert_output_cas(catalog_path, initial_output)

        changed = atomic_write_text(catalog_path, serialized, before_replace=verify_cas)
        revalidate_catalog_target(root, catalog_path)
        final_output = capture_output_state(catalog_path)
        expected_digest = sha256_bytes(serialized.encode("utf-8"))
        if not final_output.exists or final_output.digest != expected_digest:
            raise CatalogConflictError("Catalog output does not match the generated payload after apply")

    status = "REGISTERED" if changed else "UNCHANGED"
    print(f"{status}: {len(catalog['skills'])} Skills -> {catalog_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
