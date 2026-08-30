#!/usr/bin/env python3
"""Deterministic fixture and negative tests for the structured/package probe."""
import importlib.util
import json
import os
import sqlite3
import tempfile
import zipfile


def load_probe():
    path = os.path.join(os.path.dirname(__file__), "Probe-ESStructuredPackage.py")
    spec = importlib.util.spec_from_file_location("es_structured_probe", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    probe = load_probe()
    results = []
    with tempfile.TemporaryDirectory(prefix="es-reader-fixture-") as root:
        db_path = os.path.join(root, "sample.sqlite")
        db = sqlite3.connect(db_path)
        db.execute("CREATE TABLE assets (id INTEGER PRIMARY KEY, path TEXT NOT NULL)")
        db.execute("CREATE VIEW asset_view AS SELECT id, path FROM assets")
        db.commit()
        db.close()
        sqlite_result = probe.sqlite_probe(db_path)
        results.append({"id": "sqlite-schema", "passed": sqlite_result.get("status") == "passed" and sqlite_result["summary"]["tableCount"] == 2})

        toml_path = os.path.join(root, "sample.toml")
        with open(toml_path, "w", encoding="utf-8") as handle:
            handle.write("[reader]\nmax_entries = 10\n")
        toml_result = probe.text_probe(toml_path, "toml")
        results.append({"id": "toml-keys", "passed": toml_result.get("status") == "passed" and "reader" in toml_result["summary"]["rootKeys"]})

        ini_path = os.path.join(root, "sample.ini")
        with open(ini_path, "w", encoding="utf-8") as handle:
            handle.write("[reader]\nmax_entries=10\n")
        ini_result = probe.text_probe(ini_path, "ini")
        results.append({"id": "ini-sections", "passed": ini_result.get("status") == "passed" and ini_result["summary"]["sectionCount"] == 1})

        safe_zip = os.path.join(root, "safe.zip")
        with zipfile.ZipFile(safe_zip, "w") as archive:
            archive.writestr("Assets/readme.txt", "ok")
        safe_result = probe.archive_probe(safe_zip)
        results.append({"id": "zip-safe", "passed": safe_result.get("status") == "passed" and safe_result["summary"]["entryCount"] == 1})

        unsafe_zip = os.path.join(root, "unsafe.zip")
        with zipfile.ZipFile(unsafe_zip, "w") as archive:
            archive.writestr("../escape.txt", "blocked")
        unsafe_result = probe.archive_probe(unsafe_zip)
        results.append({"id": "zip-traversal-denied", "passed": unsafe_result.get("status") == "error" and "unsafe path" in unsafe_result.get("error", "")})

    passed = sum(1 for result in results if result["passed"])
    output = {"validator": "Test-ESStructuredPackage", "status": "passed" if passed == len(results) else "failed", "passed": passed, "total": len(results), "cases": results, "cleanup": "temporary fixture directory removed"}
    print(json.dumps(output, ensure_ascii=False))
    return 0 if passed == len(results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
