#!/usr/bin/env python3
"""Regression fixtures for the Skill Catalog builder's write boundary."""
from __future__ import annotations

import importlib.util
import io
import os
import stat
import struct
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import yaml


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "scripts" / "Build-ESSkillCatalog.py"
SPEC = importlib.util.spec_from_file_location("build_es_skill_catalog", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Unable to load catalog builder: {SCRIPT_PATH}")
BUILDER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = BUILDER
SPEC.loader.exec_module(BUILDER)


def create_windows_junction(link: Path, target: Path) -> None:
    """Create an NTFS junction without shelling out or requiring symlink privilege."""
    import ctypes
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateDirectoryW.argtypes = [wintypes.LPCWSTR, wintypes.LPVOID]
    kernel32.CreateDirectoryW.restype = wintypes.BOOL
    kernel32.CreateFileW.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    kernel32.CreateFileW.restype = wintypes.HANDLE
    kernel32.DeviceIoControl.argtypes = [
        wintypes.HANDLE,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
        wintypes.LPVOID,
    ]
    kernel32.DeviceIoControl.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL

    if not kernel32.CreateDirectoryW(str(link), None):
        raise ctypes.WinError(ctypes.get_last_error())

    generic_write = 0x40000000
    share_all = 0x00000001 | 0x00000002 | 0x00000004
    open_existing = 3
    open_reparse_point = 0x00200000
    backup_semantics = 0x02000000
    handle = kernel32.CreateFileW(
        str(link),
        generic_write,
        share_all,
        None,
        open_existing,
        open_reparse_point | backup_semantics,
        None,
    )
    if handle == ctypes.c_void_p(-1).value:
        error = ctypes.get_last_error()
        os.rmdir(link)
        raise ctypes.WinError(error)

    target_name = str(target.resolve())
    substitute = ("\\??\\" + target_name).encode("utf-16-le")
    print_name = target_name.encode("utf-16-le")
    path_buffer = substitute + b"\0\0" + print_name + b"\0\0"
    mount_point_header_size = 8
    reparse_data = struct.pack(
        "<IHHHHHH",
        0xA0000003,
        mount_point_header_size + len(path_buffer),
        0,
        0,
        len(substitute),
        len(substitute) + 2,
        len(print_name),
    ) + path_buffer
    returned = wintypes.DWORD()
    try:
        buffer = ctypes.create_string_buffer(reparse_data)
        if not kernel32.DeviceIoControl(
            handle,
            0x000900A4,
            buffer,
            len(reparse_data),
            None,
            0,
            ctypes.byref(returned),
            None,
        ):
            raise ctypes.WinError(ctypes.get_last_error())
    except BaseException:
        kernel32.CloseHandle(handle)
        os.rmdir(link)
        raise
    kernel32.CloseHandle(handle)


def create_directory_link(link: Path, target: Path) -> None:
    try:
        os.symlink(target, link, target_is_directory=True)
    except OSError:
        if os.name != "nt":
            raise
        create_windows_junction(link, target)


class SkillCatalogBuilderTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="es-skill-catalog-test-")
        self.sandbox = Path(self.temporary.name)
        self.project = self.sandbox / "project"
        self.skill = self.project / ".agents" / "skills" / "es-fixture"
        (self.skill / "agents").mkdir(parents=True)
        self._write(
            self.skill / "SKILL.md",
            "---\nname: es-fixture\ndescription: Fixture Skill.\n---\n\n# Fixture\n",
        )
        self._write(
            self.skill / "governance.json",
            '{"maturity":"Proposed","delivery":"Designed","routeKeys":["fixture"]}\n',
        )
        self._write(self.skill / "agents" / "openai.yaml", 'display_name: "Fixture Skill"\n')
        self._write(
            self.project / ".agents" / "SKILL_RESOURCE_INDEX.yaml",
            "currentSkills:\n"
            "  - {name: es-fixture, family: test, routeKeys: [fixture], mcp: [none-required]}\n"
            "newSkills:\n",
        )
        self._write(
            self.project / ".agents" / "SKILL_DISCOVERY_POLICY.json",
            "{\n"
            '  "states": {"Proposed": {"discoveryState": "candidate", '
            '"planEligibility": "advisory-only", "runtimeEligibility": "blocked"}},\n'
            '  "deliveryOverrides": {},\n'
            '  "registrationOverrides": {"NeedsReview": {"reviewRequired": true}}\n'
            "}\n",
        )
        self.catalog_relative = ".agents/SKILL_CATALOG.yaml"
        self.catalog = self.project / self.catalog_relative

    def tearDown(self) -> None:
        lock_path = BUILDER.catalog_writer_lock_path(self.catalog)
        self.temporary.cleanup()
        lock_path.unlink(missing_ok=True)

    @staticmethod
    def _write(path: Path, content: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8", newline="\n")

    def _run(self, now: str, catalog: str | Path | None = None) -> int:
        arguments = [
            str(SCRIPT_PATH),
            "--project-root",
            str(self.project),
            "--catalog",
            str(catalog if catalog is not None else self.catalog_relative),
            "--write",
        ]
        with (
            mock.patch.object(sys, "argv", arguments),
            mock.patch.object(BUILDER, "utc_now", return_value=now),
            redirect_stdout(io.StringIO()),
        ):
            return BUILDER.main()

    def test_touch_is_stable_and_content_change_refreshes_times(self) -> None:
        first_time = "2026-08-24T01:00:00Z"
        second_time = "2026-08-24T02:00:00Z"
        third_time = "2026-08-24T03:00:00Z"
        fourth_time = "2026-08-24T04:00:00Z"

        self.assertEqual(self._run(first_time), 0)
        first_bytes = self.catalog.read_bytes()
        first_mtime = self.catalog.stat().st_mtime_ns
        first_catalog = yaml.safe_load(first_bytes)
        self.assertEqual(first_catalog["generatedAtUtc"], first_time)
        self.assertIn("lastModifiedUtc changes only", first_catalog["hashRule"])
        self.assertEqual(first_catalog["skills"]["es-fixture"]["lastModifiedUtc"], first_time)

        os.utime(self.skill / "SKILL.md", None)
        self.assertEqual(self._run(second_time), 0)
        self.assertEqual(self.catalog.read_bytes(), first_bytes)
        self.assertEqual(self.catalog.stat().st_mtime_ns, first_mtime)

        self._write(
            self.skill / "SKILL.md",
            "---\nname: es-fixture\ndescription: Changed Fixture Skill.\n---\n\n# Fixture\n",
        )
        self.assertEqual(self._run(third_time), 0)
        changed_bytes = self.catalog.read_bytes()
        changed_catalog = yaml.safe_load(changed_bytes)
        changed_record = changed_catalog["skills"]["es-fixture"]
        self.assertNotEqual(changed_bytes, first_bytes)
        self.assertEqual(changed_catalog["generatedAtUtc"], third_time)
        self.assertEqual(changed_record["lastModifiedUtc"], third_time)
        self.assertEqual(changed_record["firstRegisteredUtc"], first_time)
        self.assertEqual(changed_record["lastReviewedUtc"], first_time)

        changed_mtime = self.catalog.stat().st_mtime_ns
        self.assertEqual(self._run(fourth_time), 0)
        self.assertEqual(self.catalog.read_bytes(), changed_bytes)
        self.assertEqual(self.catalog.stat().st_mtime_ns, changed_mtime)

    def test_catalog_parent_escape_is_rejected_without_outside_write(self) -> None:
        outside = self.sandbox / "outside.yaml"
        self._write(outside, "sentinel\n")

        with self.assertRaisesRegex(ValueError, "escapes project root"):
            self._run("2026-08-24T01:00:00Z", "../outside.yaml")

        self.assertEqual(outside.read_text(encoding="utf-8"), "sentinel\n")
        self.assertEqual(list(self.sandbox.glob(".outside.yaml.*.tmp")), [])

    def test_arbitrary_project_targets_are_rejected_without_overwrite(self) -> None:
        targets = {
            "AGENTS.md": "portal: sentinel\n",
            ".git/HEAD": "ref: refs/heads/sentinel\n",
            "Packages/manifest.json": '{"dependencies":{"sentinel":"1.0.0"}}\n',
            "src/tool.py": "sentinel = True\n",
        }
        for relative, content in targets.items():
            with self.subTest(relative=relative):
                target = self.project / relative
                self._write(target, content)
                before = target.read_bytes()

                with self.assertRaisesRegex(ValueError, "Catalog path must be exactly"):
                    self._run("2026-08-24T01:00:00Z", relative)

                self.assertEqual(target.read_bytes(), before)

    def test_writer_lock_contention_fails_closed(self) -> None:
        with BUILDER.catalog_writer_lock(self.catalog, timeout_seconds=0.2):
            with self.assertRaisesRegex(TimeoutError, "Catalog writer lock"):
                with BUILDER.catalog_writer_lock(self.catalog, timeout_seconds=0.05):
                    self.fail("contending writer unexpectedly acquired the lock")

    def test_invalid_existing_catalog_is_rejected_without_overwrite(self) -> None:
        invalid_catalogs = {
            "invalid-utf8": b"\xff\xfe\x00",
            "not-a-mapping": b"- item\n",
            "wrong-schema": (
                f"schemaVersion: 2\ncatalogId: {BUILDER.CATALOG_ID}\nskills: {{}}\n".encode("utf-8")
            ),
            "wrong-id": b"schemaVersion: 1\ncatalogId: not-es\nskills: {}\n",
        }
        for case_name, payload in invalid_catalogs.items():
            with self.subTest(case=case_name):
                self.catalog.write_bytes(payload)

                with self.assertRaises(ValueError):
                    self._run("2026-08-24T01:00:00Z")

                self.assertEqual(self.catalog.read_bytes(), payload)

    def test_input_snapshot_drift_aborts_and_preserves_output(self) -> None:
        original_capture = BUILDER.capture_input_snapshot
        calls = 0

        def capture_with_drift(project_root: Path):
            nonlocal calls
            snapshot = original_capture(project_root)
            calls += 1
            if calls == 1:
                self._write(
                    self.skill / "SKILL.md",
                    "---\nname: es-fixture\ndescription: Concurrent change.\n---\n\n# Fixture\n",
                )
            return snapshot

        with (
            mock.patch.object(BUILDER, "capture_input_snapshot", side_effect=capture_with_drift),
            self.assertRaisesRegex(BUILDER.CatalogConflictError, "input snapshot changed"),
        ):
            self._run("2026-08-24T01:00:00Z")

        self.assertFalse(self.catalog.exists())
        self.assertEqual(list(self.catalog.parent.glob(f".{self.catalog.name}.*.tmp")), [])

    def test_snapshot_covers_inventory_and_every_consumed_file(self) -> None:
        paths = [
            self.skill / "SKILL.md",
            self.skill / "governance.json",
            self.skill / "agents" / "openai.yaml",
            self.project / ".agents" / "SKILL_RESOURCE_INDEX.yaml",
            self.project / ".agents" / "SKILL_DISCOVERY_POLICY.json",
        ]
        baseline = BUILDER.capture_input_snapshot(self.project).fingerprint
        for path in paths:
            with self.subTest(path=path.name):
                original = path.read_bytes()
                path.write_bytes(original + b" ")
                try:
                    self.assertNotEqual(BUILDER.capture_input_snapshot(self.project).fingerprint, baseline)
                finally:
                    path.write_bytes(original)

        added_skill = self.project / ".agents" / "skills" / "es-inventory-only"
        added_skill.mkdir()
        try:
            self.assertNotEqual(BUILDER.capture_input_snapshot(self.project).fingerprint, baseline)
        finally:
            added_skill.rmdir()

    def test_output_existence_drift_aborts_without_overwrite(self) -> None:
        original_capture = BUILDER.capture_output_state
        calls = 0
        concurrent = b"concurrent-output\n"

        def capture_with_drift(path: Path):
            nonlocal calls
            state = original_capture(path)
            calls += 1
            if calls == 1:
                path.write_bytes(concurrent)
            return state

        with (
            mock.patch.object(BUILDER, "capture_output_state", side_effect=capture_with_drift),
            self.assertRaisesRegex(BUILDER.CatalogConflictError, "existence/hash changed"),
        ):
            self._run("2026-08-24T01:00:00Z")

        self.assertEqual(self.catalog.read_bytes(), concurrent)
        self.assertEqual(list(self.catalog.parent.glob(f".{self.catalog.name}.*.tmp")), [])

    def test_output_hash_drift_aborts_without_overwrite(self) -> None:
        self.assertEqual(self._run("2026-08-24T01:00:00Z"), 0)
        original = self.catalog.read_bytes()
        self._write(
            self.skill / "SKILL.md",
            "---\nname: es-fixture\ndescription: New projection.\n---\n\n# Fixture\n",
        )
        original_capture = BUILDER.capture_output_state
        calls = 0
        concurrent = original + b"# concurrent-output-drift\n"

        def capture_with_drift(path: Path):
            nonlocal calls
            state = original_capture(path)
            calls += 1
            if calls == 1:
                path.write_bytes(concurrent)
            return state

        with (
            mock.patch.object(BUILDER, "capture_output_state", side_effect=capture_with_drift),
            self.assertRaisesRegex(BUILDER.CatalogConflictError, "existence/hash changed"),
        ):
            self._run("2026-08-24T02:00:00Z")

        self.assertEqual(self.catalog.read_bytes(), concurrent)
        self.assertEqual(list(self.catalog.parent.glob(f".{self.catalog.name}.*.tmp")), [])

    def test_atomic_replace_preserves_existing_mode(self) -> None:
        self._write(self.catalog, "original\n")
        if os.name != "nt":
            os.chmod(self.catalog, 0o640)
        expected_mode = stat.S_IMODE(self.catalog.stat().st_mode)

        self.assertTrue(BUILDER.atomic_write_text(self.catalog, "replacement\n"))

        self.assertEqual(self.catalog.read_text(encoding="utf-8"), "replacement\n")
        self.assertEqual(stat.S_IMODE(self.catalog.stat().st_mode), expected_mode)

    def test_catalog_link_or_reparse_escape_is_rejected(self) -> None:
        outside = self.sandbox / "outside-directory"
        outside.mkdir()
        link = self.project / ".agents" / "outside-link"
        try:
            create_directory_link(link, outside)
        except OSError as exc:
            self.skipTest(f"directory link unavailable: {exc}")

        try:
            with self.assertRaisesRegex(ValueError, "escapes project root"):
                self._run("2026-08-24T01:00:00Z", ".agents/outside-link/catalog.yaml")

            self.assertFalse((outside / "catalog.yaml").exists())
        finally:
            if link.is_symlink():
                link.unlink()
            elif link.exists():
                os.rmdir(link)

    def test_replace_failure_preserves_target_and_cleans_staged_file(self) -> None:
        self._write(self.catalog, "original\n")
        target_mode = self.catalog.stat().st_mode
        observed_source: list[Path] = []
        real_fsync = os.fsync

        def fail_replace(source: str | Path, destination: str | Path) -> None:
            source_path = Path(source)
            self.assertEqual(source_path.parent, self.catalog.parent)
            self.assertEqual(Path(destination), self.catalog)
            observed_source.append(source_path)
            raise OSError("injected replace failure")

        with (
            mock.patch.object(BUILDER.os, "fsync", wraps=real_fsync) as fsync_mock,
            mock.patch.object(BUILDER.os, "replace", side_effect=fail_replace),
            self.assertRaisesRegex(OSError, "injected replace failure"),
        ):
            BUILDER.atomic_write_text(self.catalog, "replacement\n")

        self.assertTrue(fsync_mock.called)
        self.assertEqual(len(observed_source), 1)
        self.assertFalse(observed_source[0].exists())
        self.assertEqual(self.catalog.read_text(encoding="utf-8"), "original\n")
        self.assertEqual(self.catalog.stat().st_mode, target_mode)
        self.assertEqual(list(self.catalog.parent.glob(f".{self.catalog.name}.*.tmp")), [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
