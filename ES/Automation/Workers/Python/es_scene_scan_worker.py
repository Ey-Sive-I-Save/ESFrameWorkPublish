#!/usr/bin/env python3
"""受管的 ES 场景扫描 Worker。

此 Worker 只读取 Unity C# 导出的规范化场景快照，只在 RunId 临时目录写入。
它不会解析 .unity YAML、启动 Unity、修改资产，且每个需要人类输入的阶段都会退出。
"""

import argparse
import hashlib
import json
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


PROTOCOL_VERSION = 1
TASK_ID = "es.scene.scan"
TASK_VERSION = 1
WORKER_TYPE = "Python"
WORKER_ID = "es.scene.scan.python"
WORKER_VERSION = "0.1.0"
STEP_ID = "scene-scan.report-options"
EXPECTED_OPTIONS_SCHEMA_HASH = "4bbaa61e9bf8a2e2664d3b9cf98944711aa26d5c714e911044298193f08a14cb"
EXIT_COMPLETED = 0
EXIT_FAILED = 10
EXIT_CANCELLED = 20
EXIT_NEEDS_INPUT = 30
MAX_DETAILED_OBJECTS = 5000
MAX_COMPONENTS_PER_DETAILED_OBJECT = 64


class ProtocolError(RuntimeError):
    """输入或检查点不符合已注册协议。"""


def utc_now():
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path):
    with Path(path).open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json_atomic(path, value):
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2, sort_keys=True)
        handle.write("\n")
    temporary.replace(target)


def write_text_atomic(path, value):
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(value)
    temporary.replace(target)


def require_exact_keys(value, required, context):
    if not isinstance(value, dict):
        raise ProtocolError(context + " 必须是对象。")
    actual = set(value.keys())
    expected = set(required)
    missing = expected - actual
    extra = actual - expected
    if missing:
        raise ProtocolError(context + " 缺少字段：" + ", ".join(sorted(missing)))
    if extra:
        raise ProtocolError(context + " 包含未注册字段：" + ", ".join(sorted(extra)))


def require_string(value, field, allow_empty=False):
    if not isinstance(value, str) or (not allow_empty and not value):
        raise ProtocolError(field + " 必须是" + ("可为空" if allow_empty else "非空") + "字符串。")
    return value


def require_int(value, field, minimum=None, maximum=None):
    if isinstance(value, bool) or not isinstance(value, int):
        raise ProtocolError(field + " 必须是整数。")
    if minimum is not None and value < minimum:
        raise ProtocolError(field + " 小于允许下限。")
    if maximum is not None and value > maximum:
        raise ProtocolError(field + " 超过允许上限。")
    return value


def require_hash(value, field):
    value = require_string(value, field)
    if len(value) != 64 or any(char not in "0123456789abcdefABCDEF" for char in value):
        raise ProtocolError(field + " 必须是 64 位 SHA-256。")
    return value.lower()


def require_run_id(value):
    value = require_string(value, "runId")
    if len(value) != 32 or any(char not in "0123456789abcdefABCDEF" for char in value):
        raise ProtocolError("runId 必须是 32 位十六进制 GUID。")
    return value.lower()


def ensure_inside(path, root, field):
    resolved_path = Path(path).resolve()
    resolved_root = Path(root).resolve()
    try:
        resolved_path.relative_to(resolved_root)
    except ValueError:
        raise ProtocolError(field + " 必须位于当前 RunId 临时目录内。")
    return resolved_path


def identity_from_manifest(manifest):
    if not isinstance(manifest, dict):
        return {}
    return {
        "runId": manifest.get("runId", ""),
        "generation": manifest.get("generation", 0),
        "taskId": manifest.get("taskId", TASK_ID),
        "taskVersion": manifest.get("taskVersion", TASK_VERSION),
        "workerType": manifest.get("workerType", WORKER_TYPE),
        "workerId": manifest.get("workerId", WORKER_ID),
        "workerVersion": manifest.get("workerVersion", WORKER_VERSION),
        "entrypointHash": manifest.get("entrypointHash", ""),
        "stepId": manifest.get("stepId", STEP_ID),
        "schemaHash": manifest.get("optionsSchemaHash", EXPECTED_OPTIONS_SCHEMA_HASH),
    }


def write_stage_result(stage_path, identity, status, exit_code, started_at, errors=None):
    payload = {
        "protocolVersion": PROTOCOL_VERSION,
        "runId": identity.get("runId", ""),
        "generation": identity.get("generation", 0),
        "taskId": identity.get("taskId", TASK_ID),
        "taskVersion": identity.get("taskVersion", TASK_VERSION),
        "workerType": identity.get("workerType", WORKER_TYPE),
        "workerId": identity.get("workerId", WORKER_ID),
        "workerVersion": identity.get("workerVersion", WORKER_VERSION),
        "entrypointHash": identity.get("entrypointHash", ""),
        "status": status,
        "exitCode": exit_code,
        "startedAtUtc": started_at,
        "finishedAtUtc": utc_now(),
        "stepId": identity.get("stepId", STEP_ID),
        "schemaHash": identity.get("schemaHash", EXPECTED_OPTIONS_SCHEMA_HASH),
        "errors": errors or [],
    }
    write_json_atomic(stage_path, payload)


def validate_manifest(manifest, stage_path):
    required = [
        "protocolVersion", "runId", "generation", "taskId", "taskVersion", "workerType", "workerId",
        "workerVersion", "entrypointHash", "stepId", "optionsSchemaHash", "dryRun", "sceneSnapshotPath",
        "sceneSnapshotHash", "inputResponsePath", "workerOutputDirectory",
    ]
    require_exact_keys(manifest, required, "阶段输入")
    if manifest["protocolVersion"] != PROTOCOL_VERSION:
        raise ProtocolError("不支持的阶段输入协议版本。")
    run_id = require_run_id(manifest["runId"])
    generation = require_int(manifest["generation"], "generation", minimum=0)
    if manifest["taskId"] != TASK_ID or manifest["taskVersion"] != TASK_VERSION:
        raise ProtocolError("阶段输入不属于此受信场景扫描任务。")
    if manifest["workerType"] != WORKER_TYPE or manifest["workerId"] != WORKER_ID or manifest["workerVersion"] != WORKER_VERSION:
        raise ProtocolError("阶段输入的 Worker 身份不匹配。")
    expected_entrypoint_hash = require_hash(manifest["entrypointHash"], "entrypointHash")
    if sha256_file(__file__) != expected_entrypoint_hash:
        raise ProtocolError("Worker 入口文件指纹与受信 TaskContract 不匹配。")
    if manifest["stepId"] != STEP_ID:
        raise ProtocolError("Worker 不支持该动态输入步骤。")
    if require_hash(manifest["optionsSchemaHash"], "optionsSchemaHash") != EXPECTED_OPTIONS_SCHEMA_HASH:
        raise ProtocolError("输入表单 SchemaHash 不匹配。")
    if not isinstance(manifest["dryRun"], bool):
        raise ProtocolError("dryRun 必须为布尔值。")

    run_root = Path(stage_path).resolve().parent
    snapshot_path = ensure_inside(require_string(manifest["sceneSnapshotPath"], "sceneSnapshotPath"), run_root, "sceneSnapshotPath")
    response_raw = require_string(manifest["inputResponsePath"], "inputResponsePath", allow_empty=True)
    response_path = ensure_inside(response_raw, run_root, "inputResponsePath") if response_raw else None
    output_directory = ensure_inside(require_string(manifest["workerOutputDirectory"], "workerOutputDirectory"), run_root, "workerOutputDirectory")
    if not snapshot_path.is_file():
        raise ProtocolError("场景快照不存在。")
    if sha256_file(snapshot_path) != require_hash(manifest["sceneSnapshotHash"], "sceneSnapshotHash"):
        raise ProtocolError("场景快照 Hash 不匹配。")
    if generation == 0 and response_path is not None:
        raise ProtocolError("首阶段不应携带输入响应。")
    if generation == 1 and response_path is None:
        raise ProtocolError("报告阶段必须携带规范化输入响应。")
    if generation > 1:
        raise ProtocolError("当前 Worker 只注册了两个阶段。")
    return {
        "runId": run_id,
        "generation": generation,
        "taskId": TASK_ID,
        "taskVersion": TASK_VERSION,
        "workerType": WORKER_TYPE,
        "workerId": WORKER_ID,
        "workerVersion": WORKER_VERSION,
        "entrypointHash": expected_entrypoint_hash,
        "stepId": STEP_ID,
        "schemaHash": EXPECTED_OPTIONS_SCHEMA_HASH,
        "dryRun": manifest["dryRun"],
        "snapshotPath": snapshot_path,
        "responsePath": response_path,
        "outputDirectory": output_directory,
    }


def validate_input_response(response, identity):
    required = ["protocolVersion", "runId", "requestGeneration", "stepId", "schemaHash", "accepted", "values"]
    require_exact_keys(response, required, "输入响应")
    if response["protocolVersion"] != PROTOCOL_VERSION:
        raise ProtocolError("输入响应协议版本不匹配。")
    if require_run_id(response["runId"]) != identity["runId"]:
        raise ProtocolError("输入响应 RunId 已过期。")
    if require_int(response["requestGeneration"], "requestGeneration", minimum=0) != 0:
        raise ProtocolError("输入响应代次已过期。")
    if response["stepId"] != STEP_ID or require_hash(response["schemaHash"], "schemaHash") != EXPECTED_OPTIONS_SCHEMA_HASH:
        raise ProtocolError("输入响应步骤或 SchemaHash 已过期。")
    if not isinstance(response["accepted"], bool):
        raise ProtocolError("输入响应 accepted 必须为布尔值。")
    if not isinstance(response["values"], dict):
        raise ProtocolError("输入响应 values 必须为对象。")
    if not response["accepted"]:
        return None

    values = response["values"]
    require_exact_keys(values, ["includeInactive", "detailMode", "topComponentCount"], "场景扫描选项")
    if not isinstance(values["includeInactive"], bool):
        raise ProtocolError("includeInactive 必须为布尔值。")
    if values["detailMode"] not in ("summary", "detailed"):
        raise ProtocolError("detailMode 必须是已注册稳定选项 ID。")
    top_component_count = require_int(values["topComponentCount"], "topComponentCount", minimum=1, maximum=50)
    return {
        "includeInactive": values["includeInactive"],
        "detailMode": values["detailMode"],
        "topComponentCount": top_component_count,
    }


def analyse_snapshot(snapshot, options, run_id):
    if not isinstance(snapshot, dict) or not isinstance(snapshot.get("scene"), dict) or not isinstance(snapshot.get("objects"), list):
        raise ProtocolError("场景快照结构无效。")

    scene = snapshot["scene"]
    scene_name = require_string(scene.get("name"), "scene.name", allow_empty=True)
    scene_path = require_string(scene.get("path"), "scene.path", allow_empty=True)
    all_objects = snapshot["objects"]
    included = []
    inactive_excluded = 0
    component_counts = Counter()
    layer_counts = Counter()
    tag_counts = Counter()
    max_depth = 0
    total_component_count = 0

    for index, item in enumerate(all_objects):
        if not isinstance(item, dict):
            raise ProtocolError("场景快照 objects[" + str(index) + "] 必须是对象。")
        required = ["hierarchyPath", "activeSelf", "activeInHierarchy", "layer", "tag", "isStatic", "depth", "components"]
        missing = set(required) - set(item.keys())
        if missing:
            raise ProtocolError("场景快照对象缺少字段：" + ", ".join(sorted(missing)))
        if not isinstance(item["activeSelf"], bool) or not isinstance(item["activeInHierarchy"], bool):
            raise ProtocolError("场景快照对象激活状态必须为布尔值。")
        if not isinstance(item["components"], list) or any(not isinstance(component, str) for component in item["components"]):
            raise ProtocolError("场景快照对象组件必须是字符串数组。")
        depth = require_int(item["depth"], "object.depth", minimum=0)
        if not options["includeInactive"] and not item["activeInHierarchy"]:
            inactive_excluded += 1
            continue
        included.append(item)
        max_depth = max(max_depth, depth)
        layer_counts[str(item["layer"])] += 1
        tag_counts[str(item["tag"])] += 1
        total_component_count += len(item["components"])
        component_counts.update(item["components"])

    top_count = options["topComponentCount"]
    component_rows = [
        {"componentType": name, "count": count}
        for name, count in sorted(component_counts.items(), key=lambda entry: (-entry[1], entry[0]))[:top_count]
    ]
    layer_rows = [{"layer": layer, "count": count} for layer, count in sorted(layer_counts.items(), key=lambda entry: (-entry[1], entry[0]))]
    tag_rows = [{"tag": tag, "count": count} for tag, count in sorted(tag_counts.items(), key=lambda entry: (-entry[1], entry[0]))]
    report = {
        "reportVersion": 1,
        "runId": run_id,
        "dryRun": options["dryRun"],
        "scene": {"name": scene_name, "path": scene_path},
        "options": options,
        "summary": {
            "sourceObjectCount": len(all_objects),
            "includedObjectCount": len(included),
            "inactiveExcludedCount": inactive_excluded,
            "rootObjectCount": sum(1 for item in included if item["depth"] == 0),
            "maxDepth": max_depth,
            "totalComponentCount": total_component_count,
            "uniqueComponentTypeCount": len(component_counts),
        },
        "componentCounts": component_rows,
        "layerCounts": layer_rows,
        "tagCounts": tag_rows,
    }
    if options["detailMode"] == "detailed":
        detail_rows = []
        for item in included[:MAX_DETAILED_OBJECTS]:
            components = item["components"]
            detail_rows.append({
                "hierarchyPath": item["hierarchyPath"],
                "activeSelf": item["activeSelf"],
                "activeInHierarchy": item["activeInHierarchy"],
                "layer": item["layer"],
                "tag": item["tag"],
                "isStatic": item["isStatic"],
                "depth": item["depth"],
                "components": components[:MAX_COMPONENTS_PER_DETAILED_OBJECT],
                "omittedComponentCount": max(0, len(components) - MAX_COMPONENTS_PER_DETAILED_OBJECT),
            })
        report["objects"] = detail_rows
        report["detailedObjectTruncatedCount"] = max(0, len(included) - len(detail_rows))
    return report


def build_markdown(report):
    scene = report["scene"]
    summary = report["summary"]
    lines = [
        "# 场景扫描报告",
        "",
        "- RunId：`" + report["runId"] + "`",
        "- 场景：`" + (scene["path"] or "<未保存场景:" + scene["name"] + ">") + "`",
        "- 模式：`" + report["options"]["detailMode"] + "`",
        "- 包含未激活对象：`" + str(report["options"]["includeInactive"]).lower() + "`",
        "",
        "## 汇总",
        "",
        "| 指标 | 数值 |",
        "| --- | ---: |",
        "| 源对象数 | " + str(summary["sourceObjectCount"]) + " |",
        "| 纳入对象数 | " + str(summary["includedObjectCount"]) + " |",
        "| 排除的未激活对象 | " + str(summary["inactiveExcludedCount"]) + " |",
        "| 根对象数 | " + str(summary["rootObjectCount"]) + " |",
        "| 最大层级深度 | " + str(summary["maxDepth"]) + " |",
        "| 组件实例总数 | " + str(summary["totalComponentCount"]) + " |",
        "| 组件类型数 | " + str(summary["uniqueComponentTypeCount"]) + " |",
        "",
        "## 高频组件",
        "",
        "| 组件 | 数量 |",
        "| --- | ---: |",
    ]
    lines.extend("| `" + row["componentType"] + "` | " + str(row["count"]) + " |" for row in report["componentCounts"])
    if not report["componentCounts"]:
        lines.append("| （无） | 0 |")
    lines.extend(["", "## Layer 分布", "", "| Layer | 对象数 |", "| --- | ---: |"])
    lines.extend("| `" + row["layer"] + "` | " + str(row["count"]) + " |" for row in report["layerCounts"])
    lines.extend(["", "## Tag 分布", "", "| Tag | 对象数 |", "| --- | ---: |"])
    lines.extend("| `" + row["tag"] + "` | " + str(row["count"]) + " |" for row in report["tagCounts"])
    if report["options"]["detailMode"] == "detailed":
        lines.extend(["", "## 详细对象", "", "详细对象清单位于 `scene-scan.json` 的 `objects` 字段；最多保留 " + str(MAX_DETAILED_OBJECTS) + " 项。"])
    lines.extend(["", "本报告只描述 Unity C# 导出的瞬时快照，不代表 PlayMode、构建结果或发布验收。", ""])
    return "\n".join(lines)


def run_stage(manifest, stage_path, started_at):
    identity = validate_manifest(manifest, stage_path)
    if identity["generation"] == 0:
        write_stage_result(stage_path, identity, "NeedsInput", EXIT_NEEDS_INPUT, started_at)
        return EXIT_NEEDS_INPUT

    response = load_json(identity["responsePath"])
    options = validate_input_response(response, identity)
    if options is None:
        write_stage_result(stage_path, identity, "Cancelled", EXIT_CANCELLED, started_at)
        return EXIT_CANCELLED

    snapshot = load_json(identity["snapshotPath"])
    options["dryRun"] = identity["dryRun"]
    report = analyse_snapshot(snapshot, options, identity["runId"])
    output_directory = identity["outputDirectory"]
    output_directory.mkdir(parents=True, exist_ok=True)
    write_json_atomic(output_directory / "scene-scan.json", report)
    write_text_atomic(output_directory / "scene-scan.md", build_markdown(report))
    write_stage_result(stage_path, identity, "Completed", EXIT_COMPLETED, started_at)
    return EXIT_COMPLETED


def main(argv=None):
    parser = argparse.ArgumentParser(description="ES 受管场景扫描 Worker")
    parser.add_argument("--input", required=True, help="C# 写入的阶段输入 JSON")
    parser.add_argument("--stage-result", required=True, help="本阶段唯一的结构化结果 JSON")
    arguments = parser.parse_args(argv)
    started_at = utc_now()
    manifest = None
    identity = {}
    try:
        manifest = load_json(arguments.input)
        identity = identity_from_manifest(manifest)
        return run_stage(manifest, Path(arguments.stage_result), started_at)
    except Exception as exception:  # 必须以结构化失败留痕；不把 stdout 作为协议。
        message = str(exception) or exception.__class__.__name__
        try:
            write_stage_result(Path(arguments.stage_result), identity, "Failed", EXIT_FAILED, started_at, [message])
        except Exception:
            pass
        return EXIT_FAILED


if __name__ == "__main__":
    sys.exit(main())
