#!/usr/bin/env python3
"""Bounded, single-process UnityPackage (tar/gzip) projection."""
import argparse, hashlib, json, os, re, sys, tarfile

GUID = re.compile(r"^[0-9a-f]{32}$", re.I)

def fail(message, code=1):
    print(json.dumps({"status": "failed", "error": message}, ensure_ascii=False))
    return code

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--max-entries", type=int, default=100000)
    ap.add_argument("--max-associations", type=int, default=100000)
    args = ap.parse_args()
    path = os.path.abspath(args.path)
    if not os.path.isfile(path): return fail("source file missing")
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    entries = []
    groups = {}
    try:
        with tarfile.open(path, mode="r:*") as archive:
            members = archive.getmembers()
            if len(members) > args.max_entries: return fail("archive entry limit exceeded")
            for member in members:
                name = member.name.replace("\\", "/")
                if name.startswith("/") or re.match(r"^[A-Za-z]:", name) or any(part == ".." for part in name.split("/")):
                    return fail("archive contains unsafe path")
                entries.append(name)
                parts = name.split("/")
                if len(parts) >= 2 and GUID.match(parts[0]):
                    group = groups.setdefault(parts[0], {"groupId": parts[0], "pathname": None, "metaHeader": None, "hasAsset": False})
                    leaf = parts[-1]
                    if leaf == "pathname":
                        source = archive.extractfile(member)
                        group["pathname"] = source.read(4096).decode("utf-8", "replace").strip() if source else None
                    elif leaf == "asset.meta":
                        source = archive.extractfile(member)
                        group["metaHeader"] = source.read(4096).decode("utf-8", "replace")[:4096] if source else None
                    elif leaf == "asset":
                        group["hasAsset"] = True
    except (tarfile.TarError, OSError, EOFError) as exc:
        return fail("archive parse failed: " + str(exc))
    associated = sorted(groups.values(), key=lambda x: x["groupId"])
    if len(associated) > args.max_associations:
        return fail("association limit exceeded")
    packet = {
        "status": "passed", "sourcePath": path, "sourceSha256": h.hexdigest(),
        "parserId": "unitypackage.python-tar.v1", "detectedFormat": "unitypackage",
        "summary": {"sizeBytes": os.path.getsize(path), "entryCount": len(entries), "guidCount": len(groups), "associationCount": len(associated)},
        "entries": associated, "warnings": [], "errors": [],
        "nonClaims": ["Unity import", "runtime behavior", "network behavior", "release behavior"]
    }
    print(json.dumps(packet, ensure_ascii=False, separators=(",", ":")))
    return 0

if __name__ == "__main__":
    sys.exit(main())
