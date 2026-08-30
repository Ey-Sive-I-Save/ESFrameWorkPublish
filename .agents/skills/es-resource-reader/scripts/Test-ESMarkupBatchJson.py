import json, os, sys

def fail(msg):
    print(json.dumps({"valid": False, "error": msg}, ensure_ascii=False)); return 1

def main(path):
    try:
        with open(path, encoding="utf-8-sig") as f: doc = json.load(f)
    except Exception as exc: return fail(f"invalid-json: {exc}")
    if not isinstance(doc, dict): return fail("envelope must be object")
    if doc.get("status") != "passed" or doc.get("parserId") != "markup.batch.v1": return fail("invalid envelope status/parserId")
    if not isinstance(doc.get("elapsedMilliseconds"), (int, float)) or doc["elapsedMilliseconds"] < 0: return fail("invalid elapsedMilliseconds")
    items = doc.get("items")
    if not isinstance(items, list) or not items: return fail("items must be non-empty array")
    seen = set()
    for i, item in enumerate(items):
        if not isinstance(item, dict): return fail(f"item[{i}] must be object")
        p = item.get("path")
        if not isinstance(p, str) or not p: return fail(f"item[{i}] path missing")
        if p in seen: return fail(f"duplicate path: {p}")
        seen.add(p)
        status = item.get("status")
        if status == "passed":
            if item.get("parserId") not in ("yaml.batch.v1", "html.batch.v1", "markdown.batch.v1", "xml.batch.v1", "unity-yaml.batch.v1"): return fail(f"item[{i}] invalid parserId")
            if not isinstance(item.get("summary"), dict) or not isinstance(item.get("entries"), list) or len(item["entries"]) > 200: return fail(f"item[{i}] invalid projection")
        elif status == "failed":
            if not isinstance(item.get("error"), str) or not item["error"]: return fail(f"item[{i}] failed item missing error")
        else: return fail(f"item[{i}] invalid status")
    print(json.dumps({"valid": True, "itemCount": len(items), "paths": sorted(seen)}, ensure_ascii=False)); return 0

if __name__ == "__main__": sys.exit(main(sys.argv[1]))
