# AI 内容迁移记录 · 2026-08-27

状态：已执行；本记录用于后续回流检测，不代表所有项目文件已迁移。

## 迁移规则

- 仅迁移根目录 AI 诊断快照、Skill 生成缓存和无引用 MCP 截图。
- 不迁移 `Assets/UI` 正式资产、`ES/Output`/`ES/UIEvidence` 证据、`ES/Automation` 受管运行内容、HybridCLR/Mono/第三方 Hash 文件。
- 本次不删除、不覆盖、不执行 Git/Unity/Runtime/发布操作。
- `source` 路径必须不存在，`destination` 路径必须存在且 SHA-256/字节数一致。

## 明细

| source | destination | bytes | sha256 |
|---|---|---:|---|
| `.Tempasset-bake-state-undo-replay.json` | `ES/AISpace/Local/Recovered/RootArtifacts/.Tempasset-bake-state-undo-replay.json` | 11963 | `6c3242d9590b2f5c879a09f2df8dcbd6054b86390c22107c7c3ce0deeda7b06b` |
| `mcp-headers.txt` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-headers.txt` | 270 | `a9d53e3941811748b55c751c00227196c90fae10263d96339194dd62ecb05e35` |
| `mcp-init.json` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-init.json` | 153 | `69a3294a6be6d32b48e16c6625084cca95632b2f14738f6dfab32f8035ec6cf1` |
| `mcp-list-tools.json` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-list-tools.json` | 59 | `d059fc1a28537e4788d50b9fbb56654fc9612b090e780f0b85e8071601dfa24e` |
| `mcp-read-instances.json` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-read-instances.json` | 94 | `fb49d489bbd935fa9e51977ecc9e2e32a79e43a554a48aef154a22783136deba` |
| `mcp-read-state.json` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-read-state.json` | 97 | `73d088941ffd06c464e1715fcb09c0b2bb3446584e60b6eeb8d3206fec0651c1` |
| `mcp-resources-list.json` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-resources-list.json` | 63 | `c26ed5df68a6f472f36855f60fb4852231da521ea4b4b428288cacb2043349e3` |
| `mcp-resources-list.out` | `ES/AISpace/Local/Recovered/RootArtifacts/mcp-resources-list.out` | 4898 | `ca347bdb0c12287739fe27e49069720a2afea03c089edd89a3ddb636b0e8dbd7` |
| `Assets/Screenshots/arena-moba-lobby-mcp.png` | `ES/UIEvidence/reference-ingest/arena-moba-lobby-mcp.png` | 140996 | `ce8c546a8d7bc3b466ed4fe070c8c24d9021796155d178b84a80bd821bcb5afe` |
| `Assets/Screenshots/arena-moba-lobby-mcp.png.meta` | `ES/UIEvidence/reference-ingest/arena-moba-lobby-mcp.png.meta` | 2948 | `c2c21bac8af8cec49538ec3ec1594582c8a5068603f6afbb3e59c7ba1a212781` |

### Skill 缓存

以下 14 个 `.pyc` 文件全部从 `.agents/skills/**/__pycache__/` 迁移到 `ES/AISpace/Local/Recovered/SkillCaches/`，路径层级完整保留；其 SHA-256 与迁移前一致：

- `es-skill-creator`: 3 个（2 scripts、1 tests）
- `es-ui-intent-authoring`: 1 个
- `es-ui-prefab-authoring`: 10 个

精确源路径：

- `.agents/skills/es-skill-creator/scripts/__pycache__/Build-ESSkillCatalog.cpython-312.pyc`
- `.agents/skills/es-skill-creator/scripts/__pycache__/generate_openai_yaml.cpython-312.pyc`
- `.agents/skills/es-skill-creator/tests/__pycache__/test_build_es_skill_catalog.cpython-312.pyc`
- `.agents/skills/es-ui-intent-authoring/scripts/__pycache__/validate_intent_spec.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/evaluate_ui_tokens.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/evaluate_ui_typography.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/generate_ui_iteration_packet.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/ingest_ui_reference.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/resolve_ui_layout_plan.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/screen_spec_adapter.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/self_test_game_ui_platform.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/validate_game_ui_screen_spec.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/validate_ui_gpu_evidence.cpython-312.pyc`
- `.agents/skills/es-ui-prefab-authoring/scripts/__pycache__/validate_ui_snapshot_evidence.cpython-312.pyc`

## 后续回流检测

复扫以下模式即可检测违规回流：

```powershell
Get-ChildItem .agents/skills -Recurse -Directory -Filter __pycache__
Get-ChildItem .agents/skills -Recurse -File -Filter *.pyc
Get-ChildItem . -Force -File | Where-Object Name -match '^(mcp-|\.Temp)'
Test-Path Assets/Screenshots/arena-moba-lobby-mcp.png
```

若命中源路径，先记录时间、路径和 SHA-256，再按本记录修复；不要把新的命中自动视为已授权删除。
