import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


WORKER_PATH = Path(__file__).resolve().parents[1] / "es_scene_scan_worker.py"
SPEC = importlib.util.spec_from_file_location("es_scene_scan_worker", WORKER_PATH)
WORKER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(WORKER)


def sha256_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


class SceneScanWorkerTests(unittest.TestCase):
    def make_manifest(self, run_root, generation, response_path=""):
        snapshot_path = run_root / "scene-snapshot.json"
        snapshot = {
            "scene": {"name": "TestScene", "path": "Assets/Scenes/TestScene.unity"},
            "objects": [
                {
                    "hierarchyPath": "Root[0]", "activeSelf": True, "activeInHierarchy": True,
                    "layer": 0, "tag": "Untagged", "isStatic": False, "depth": 0,
                    "components": ["UnityEngine.Transform", "UnityEngine.MeshRenderer"],
                },
                {
                    "hierarchyPath": "Root[0]/Child[0]", "activeSelf": False, "activeInHierarchy": False,
                    "layer": 3, "tag": "Enemy", "isStatic": True, "depth": 1,
                    "components": ["UnityEngine.Transform", "UnityEngine.BoxCollider"],
                },
            ],
        }
        write_json(snapshot_path, snapshot)
        return {
            "protocolVersion": 1,
            "runId": "0123456789abcdef0123456789abcdef",
            "generation": generation,
            "taskId": WORKER.TASK_ID,
            "taskVersion": WORKER.TASK_VERSION,
            "workerType": WORKER.WORKER_TYPE,
            "workerId": WORKER.WORKER_ID,
            "workerVersion": WORKER.WORKER_VERSION,
            "entrypointHash": sha256_file(WORKER_PATH),
            "stepId": WORKER.STEP_ID,
            "optionsSchemaHash": WORKER.EXPECTED_OPTIONS_SCHEMA_HASH,
            "dryRun": False,
            "sceneSnapshotPath": str(snapshot_path),
            "sceneSnapshotHash": sha256_file(snapshot_path),
            "inputResponsePath": str(response_path) if response_path else "",
            "workerOutputDirectory": str(run_root / "WorkerOutput"),
        }

    def test_two_stage_report_excludes_inactive_object(self):
        with tempfile.TemporaryDirectory() as temporary:
            run_root = Path(temporary)
            input_path = run_root / "stage-input.json"
            stage_path = run_root / "stage-result.json"
            write_json(input_path, self.make_manifest(run_root, 0))

            self.assertEqual(WORKER.main(["--input", str(input_path), "--stage-result", str(stage_path)]), WORKER.EXIT_NEEDS_INPUT)
            first_stage = json.loads(stage_path.read_text(encoding="utf-8"))
            self.assertEqual(first_stage["status"], "NeedsInput")
            self.assertEqual(first_stage["generation"], 0)

            response_path = run_root / "input-response.json"
            write_json(response_path, {
                "protocolVersion": 1,
                "runId": "0123456789abcdef0123456789abcdef",
                "requestGeneration": 0,
                "stepId": WORKER.STEP_ID,
                "schemaHash": WORKER.EXPECTED_OPTIONS_SCHEMA_HASH,
                "accepted": True,
                "values": {"includeInactive": False, "detailMode": "detailed", "topComponentCount": 5},
            })
            write_json(input_path, self.make_manifest(run_root, 1, response_path))
            self.assertEqual(WORKER.main(["--input", str(input_path), "--stage-result", str(stage_path)]), WORKER.EXIT_COMPLETED)

            report = json.loads((run_root / "WorkerOutput" / "scene-scan.json").read_text(encoding="utf-8"))
            self.assertEqual(report["summary"]["includedObjectCount"], 1)
            self.assertEqual(report["summary"]["inactiveExcludedCount"], 1)
            self.assertEqual(
                report["componentCounts"],
                [
                    {"componentType": "UnityEngine.MeshRenderer", "count": 1},
                    {"componentType": "UnityEngine.Transform", "count": 1},
                ],
            )
            self.assertTrue((run_root / "WorkerOutput" / "scene-scan.md").is_file())

    def test_rejects_stale_input_generation(self):
        with tempfile.TemporaryDirectory() as temporary:
            run_root = Path(temporary)
            response_path = run_root / "input-response.json"
            write_json(response_path, {
                "protocolVersion": 1,
                "runId": "0123456789abcdef0123456789abcdef",
                "requestGeneration": 99,
                "stepId": WORKER.STEP_ID,
                "schemaHash": WORKER.EXPECTED_OPTIONS_SCHEMA_HASH,
                "accepted": True,
                "values": {"includeInactive": True, "detailMode": "summary", "topComponentCount": 1},
            })
            input_path = run_root / "stage-input.json"
            stage_path = run_root / "stage-result.json"
            write_json(input_path, self.make_manifest(run_root, 1, response_path))

            self.assertEqual(WORKER.main(["--input", str(input_path), "--stage-result", str(stage_path)]), WORKER.EXIT_FAILED)
            stage_result = json.loads(stage_path.read_text(encoding="utf-8"))
            self.assertEqual(stage_result["status"], "Failed")
            self.assertIn("代次", stage_result["errors"][0])

    def test_cli_completes_two_stage_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            run_root = Path(temporary)
            input_path = run_root / "stage-input.json"
            stage_path = run_root / "stage-result.json"
            write_json(input_path, self.make_manifest(run_root, 0))

            first = subprocess.run(
                [sys.executable, str(WORKER_PATH), "--input", str(input_path), "--stage-result", str(stage_path)],
                capture_output=True,
                text=True,
                timeout=5,
                check=False,
            )
            self.assertEqual(first.returncode, WORKER.EXIT_NEEDS_INPUT, first.stderr)

            response_path = run_root / "input-response.json"
            write_json(response_path, {
                "protocolVersion": 1,
                "runId": "0123456789abcdef0123456789abcdef",
                "requestGeneration": 0,
                "stepId": WORKER.STEP_ID,
                "schemaHash": WORKER.EXPECTED_OPTIONS_SCHEMA_HASH,
                "accepted": True,
                "values": {"includeInactive": True, "detailMode": "summary", "topComponentCount": 3},
            })
            write_json(input_path, self.make_manifest(run_root, 1, response_path))
            second = subprocess.run(
                [sys.executable, str(WORKER_PATH), "--input", str(input_path), "--stage-result", str(stage_path)],
                capture_output=True,
                text=True,
                timeout=5,
                check=False,
            )
            self.assertEqual(second.returncode, WORKER.EXIT_COMPLETED, second.stderr)
            self.assertTrue((run_root / "WorkerOutput" / "scene-scan.json").is_file())
            self.assertTrue((run_root / "WorkerOutput" / "scene-scan.md").is_file())


if __name__ == "__main__":
    unittest.main()
