#!/usr/bin/env python3
"""Bounded, read-only probes for SQLite, TOML/INI and ZIP/TAR archives."""
import configparser
import hashlib
import json
import os
import sqlite3
import sys
import tarfile
import zipfile

MAX_ENTRIES = 200
MAX_TABLES = 128
MAX_COLUMNS = 128


def fail(message):
    print(json.dumps({"status": "error", "error": message}, ensure_ascii=False))
    return 1


def archive_probe(path):
    entries = []
    unsafe = []
    kind = "zip"
    try:
        if zipfile.is_zipfile(path):
            with zipfile.ZipFile(path) as zf:
                infos = zf.infolist()
                for info in infos[:MAX_ENTRIES]:
                    name = info.filename
                    entries.append({"path": name, "sizeBytes": info.file_size, "compressedBytes": info.compress_size})
                    norm = name.replace("\\", "/")
                    if norm.startswith("/") or "/../" in f"/{norm}" or norm == ".." or norm.startswith("../"):
                        unsafe.append(name)
        else:
            kind = "tar"
            with tarfile.open(path, "r:*") as tf:
                members = tf.getmembers()
                for member in members[:MAX_ENTRIES]:
                    name = member.name
                    entries.append({"path": name, "sizeBytes": member.size, "type": member.type.decode("ascii", "ignore") if isinstance(member.type, bytes) else str(member.type)})
                    norm = name.replace("\\", "/")
                    if norm.startswith("/") or "/../" in f"/{norm}" or norm == ".." or norm.startswith("../"):
                        unsafe.append(name)
    except (OSError, zipfile.BadZipFile, tarfile.TarError) as exc:
        return {"status": "error", "error": str(exc)}
    if unsafe:
        return {"status": "error", "error": "archive contains unsafe path", "unsafePaths": unsafe[:20]}
    return {"status": "passed", "parserId": f"{kind}.probe.v1", "summary": {"entryCount": len(entries), "truncated": len(entries) >= MAX_ENTRIES, "kind": kind}, "entries": entries}


def sqlite_probe(path):
    tables = []
    try:
        uri = "file:" + os.path.abspath(path).replace("\\", "/") + "?mode=ro"
        db = sqlite3.connect(uri, uri=True)
        try:
            rows = db.execute("SELECT name, type FROM sqlite_master WHERE type IN ('table','view') ORDER BY name LIMIT ?", (MAX_TABLES,)).fetchall()
            for name, kind in rows:
                cols = db.execute(f'PRAGMA table_info("{name.replace(chr(34), chr(34) * 2)}")').fetchall()
                tables.append({"name": name, "type": kind, "columnCount": len(cols), "columns": [c[1] for c in cols[:MAX_COLUMNS]]})
        finally:
            db.close()
    except (sqlite3.Error, OSError) as exc:
        return {"status": "error", "error": str(exc)}
    return {"status": "passed", "parserId": "sqlite.probe.v1", "summary": {"tableCount": len(tables), "truncated": len(tables) >= MAX_TABLES}, "entries": tables}


def text_probe(path, kind):
    try:
        with open(path, "r", encoding="utf-8-sig", errors="strict") as handle:
            text = handle.read(2 * 1024 * 1024)
    except (OSError, UnicodeError) as exc:
        return {"status": "error", "error": str(exc)}
    if kind == "ini":
        parser = configparser.ConfigParser(interpolation=None)
        try:
            parser.read_string(text)
        except configparser.Error as exc:
            return {"status": "error", "error": str(exc)}
        sections = [{"name": s, "keyCount": len(parser[s]), "keys": list(parser[s].keys())[:MAX_COLUMNS]} for s in parser.sections()[:MAX_TABLES]]
        return {"status": "passed", "parserId": "ini.probe.v1", "summary": {"sectionCount": len(sections)}, "entries": sections}
    try:
        import tomllib
        parsed = tomllib.loads(text)
        keys = list(parsed.keys())[:MAX_COLUMNS]
        return {"status": "passed", "parserId": "toml.probe.v1", "summary": {"rootKeyCount": len(parsed), "rootKeys": keys}}
    except (ValueError, UnicodeError) as exc:
        return {"status": "error", "error": str(exc)}


def main():
    if len(sys.argv) != 3:
        return fail("usage: Probe-ESStructuredPackage.py <path> <kind>")
    path, kind = sys.argv[1], sys.argv[2].lower()
    if not os.path.isfile(path):
        return fail("source file not found")
    if kind in ("zip", "archive"):
        result = archive_probe(path)
    elif kind == "sqlite":
        result = sqlite_probe(path)
    elif kind in ("toml", "ini"):
        result = text_probe(path, kind)
    else:
        return fail("unsupported kind")
    print(json.dumps(result, ensure_ascii=False))
    return 0 if result.get("status") == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
