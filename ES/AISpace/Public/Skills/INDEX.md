# ESFramework Skills 索引

本文件只是导航索引，不是 Skill 本体，也不授予执行权限。唯一权威位置是：
`.agents/skills/<skill-name>/SKILL.md`。当前目录的直接 Skill 数量不在此文件维护；
具体数量、状态、哈希和治理字段以 `.agents/SKILL_CATALOG.yaml` 为准。

AISpace 正文唯一权威是 `ES/AISpace/README.md`；本文件只维护 Skill 导航，不复制其放置规则。

Skill 与 Catalog、Resource Index、Registry Manifest、AIBrain、Knowledge、AICommand、
Evidence、Authority 和中文路由别名的关系注册由 `registry.json` 统一投影；它是可重建的
导航索引，不授予执行权限。
使用 `.agents/skills/es-skill-governance/scripts/Build-ESSkillRelationRegistry.py --write`
重建，使用同一脚本的 `--check` 做只读漂移检查。

### AISpace 输出绑定

具备生成或缓存需求的 Skill 先在 `.agents/SKILL_AISPACE_BINDINGS.json` 注册稳定
`bindingId`，再重建关系投影。绑定记录的 `skillContractRef` 指向 Skill 的
`governance.json`；关系投影的 `aispace.registryPath`、`aispace.skillContractPath` 和
`aispace.bindingIds` 提供反向引用。验证命令：

```text
python .agents/skills/es-skill-governance/scripts/Test-ESSkillAISpaceBindings.py --project-root .
```

新增绑定可使用受限写入脚本（必须显式传 `--write`），例如：

```text
python .agents/skills/es-skill-governance/scripts/Register-ESSkillAISpaceBinding.py --project-root . --skill-name es-example --binding-id aispace.es-example.private-temp --purpose "任务临时缓存" --storage-class private-temp --path-template ES/AISpace/Local/Cache/<YYYYMMDD>/<agent-or-task>/ --content-authority skill-governance --lifecycle disposable --retention task-scoped --write-policy user-directed-only --artifact-kind cache --write
```

脚本只更新注册契约，不自动生成业务文件；写入后仍须按顺序重建 Catalog、Registry
Manifest 和关系投影。

`private-temp`/`private-content` 默认落到 `ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/`，
`public-index`/`public-content` 落到 `ES/AISpace/Public/<category>/<YYYYMMDD>/<topic-or-task>/`，
`unity-public` 落到 `Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/`。Skill 本体不移动；
没有生成、缓存、稳定协作内容或索引需求的 Skill 不需要创建空绑定。所有绑定均先分类、再日期，
生命周期可为 `temporary`、`stable` 或 `archived`。

## 基础协作与治理

`es-adversarial-review` · `es-ai-abc-core` · `es-ai-collaboration-menu` · `es-ai-interaction-governance` ·
`es-ai-space-organization` · `es-change-risk-register` · `es-codex-session-bootstrap` ·
`es-first-principles-analysis` · `es-repository-discovery` · `es-skill-creator` ·
`es-skill-governance` · `es-skill-session-refresh` · `es-skill-validator` ·
`es-static-deep-replay` · `es-task-read-snapshot` · `es-utf8-guard` · `es-worktree-audit`

## AI、Knowledge、Command 与 Automation

`es-agent-mechanism-replication` · `es-aibrain-route-authoring` · `es-aiwarning-authoring` ·
`es-aicommand-contract-authoring` · `es-ai-knowledge-curation` ·
`es-knowledge-creator` · `es-knowledge-validator` · `es-generate-agent-artifacts` ·
`es-automation-worker-authoring` · `es-task-context-runtime` · `es-use-ai-command` ·
`es-feishu-cli` · `es-publish-aitest-prompt`

## 工程、架构与运行时

`es-api-contract-review` · `es-dependency-boundary-audit` · `es-gamecore-config-authoring` ·
`es-gamecore-integration` · `es-input-action` · `es-command-authoring` ·
`es-entity-authoring` · `es-entity-prefab-validation` · `es-tag-config` ·
`es-fix-compile-error` · `es-migration-planning` · `es-module-lifecycle` · `es-open-source-migration` ·
`es-observability-evidence` · `es-performance-budgeting` · `es-resource-pipeline` ·
`es-resource-publish-audit` · `es-security-input-audit` · `es-stable-graph-authoring` · `es-weapon-abc-part`

## 编辑器、UI、测试与发布

`es-editor-availability-validator` · `es-editor-tooling` · `es-prompt-engineering` · `es-ui-intent-authoring` ·
`es-ui-prefab-authoring` · `es-test-fixture-authoring` · `es-start-estest` ·
`es-unity-compile` · `es-release-acceptance` · `es-release-notes-evidence`

## 放置规则

本索引不复制 AISpace 放置规则；统一读取 `ES/AISpace/README.md`，机器身份读取
`ES/AISpace/AISPACE_AUTHORITY.json`。Skill 注册和生成/缓存绑定的顺序与验证命令以
`.agents/README.md`、`.agents/SKILL_CATALOG.yaml` 和 `.agents/SKILL_AISPACE_BINDINGS.json`
为准；本文件只提供入口指针与关系导航。
