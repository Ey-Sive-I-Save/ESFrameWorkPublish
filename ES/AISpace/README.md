# ES AI Space

> **唯一权威入口**：本文件是 AISpace 的唯一正文；机器可读身份见
> `ES/AISpace/AISPACE_AUTHORITY.json`。其他发现标记、Public 索引和 Unity 出口只做指针、派生投影或历史记录，不能与本入口竞争。

AI 协作空间的统一入口。这里保存归 AISpace 所有的 AI 生成内容、协作索引、任务说明、
状态和迁移标记。内容先按稳定分类，再按日期归档；实际代码、Unity 资产、AIWarnings、
AICommands、Automation 合同/回执和正式证据仍由各自项目权威目录负责，AISpace 只保存其
索引或明确归属的协作内容。

## 唯一正文、发现与竞争态

- `ES/AISpace/README.md` 是放置、发现和权威边界的唯一 AISpace 正文；不要在
  `ES/AISpace/Public`、`Assets/ES/AISpace/Public` 或其他目录复制本文件的规则正文。
- `MUSTREAD_PROJECT_INSTRUCTIONS` 文件只负责把 AI 引导回本文件；`Public/Skills/registry.json`
  和各主题 README 只保存索引/指针；`MIGRATION_MANIFEST.md` 保存迁移记录与分类决策；
  `AISPACE_CONTENT_CLASSIFICATION.json` 保存分类、日期和生命周期的机器规则。
- `Assets/ES/AISpace/Public` 是 Unity 导入出口，不是第二个 AISpace 根；真实业务权威仍由
  `Documentation`、`ES/Automation`、`Assets`、`AIWarnings`、`AICommands` 和 `.agents/skills`
  各自承担。
- AISpace 没有运行时租约或常驻竞争态。修改根入口或身份描述前必须重读当前内容；发现
  哈希/身份冲突时拒绝覆盖并回到本入口复核，不使用 last-write-wins。

## 固化规则：临时任务与正式交接分离

以下规则是 AISpace 的长期不变量，后续 Skill、窗口和任务文档不得回退或另建同义分类：

- 临时职责说明、窗口准备材料和短期任务文档统一使用
  `ES/AISpace/Local/CodexSessionTasks/<YYYYMMDD>/<responsibility>/`，并标记
  `temporary-task`。
- 该目录不是 Handoff、历史归档、会话权威或长期状态源；不得把它命名为或描述为
  `handoff`、`history`、`archive`，也不得在 AISpace 下新增领域专用交接目录。
- 正式交接和 Codex 历史只由 `ES/AI协作历程（Codex）/` 及外部不可变会话快照承载，
  其协议由 `es-codex-session-bootstrap` 唯一管理。
- 邮箱消息只有出现 `acceptedByRecordId` 与 `contextAccepted` 证据才算接收；仅
  `queued` 不得声称窗口已接取。
- `sourceAbsolutePath` 不是迁移或消费依据；涉及快照时只能使用信封中的
  `handoffFiles.absolutePath`。

```text
Local/                         # 私有 AI 内容
  <category>/<YYYYMMDD>/<agent-or-task>/
Public/                        # 可协作、可长期保留的 AISpace 内容
  <category>/<YYYYMMDD>/<topic-or-task>/
```

## 内容分类与日期约束

所有明确归 AISpace 的 AI 内容都先选择稳定分类，再进入日期目录；生命周期由元数据标记
`temporary`、`stable` 或 `archived`，不因内容稳定而搬离分类目录。私有内容使用
`Local/<category>/<YYYYMMDD>/<agent-or-task>/`，协作内容使用
`Public/<category>/<YYYYMMDD>/<topic-or-task>/`。截图、录屏、缓存、导出中间文件和失败重试
产物仍可归入 `temporary`，但不再是 AISpace 唯一允许的内容类型。

截图的归属按用途判断：私有诊断截图进入 `Local/Screenshots/<YYYYMMDD>/<agent-or-task>/`；
需要协作审阅的截图正文进入现有 `ES/UIEvidence` 或对应测试证据目录，Public 只登记索引；
只有 Unity 必须导入或引用的正式参考图、测试夹具截图才留在 `Assets` 下的既有目录（例如
`Assets/UI/References`、`Assets/ESTestAssets/**/Screenshots`）。`Assets/Screenshots` 不再
作为 MCP 或 AI 临时截图的默认位置。具体机器可读规则见
`Public/LOCAL_TEMP_POLICY.json`。

Unity 必须导入或引用的内容才进入 `Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/`。
不要创建 `Assets/ES/AISpace/Local`。AITalk、Codex 历史、Automation、AIWarnings
和 Skills 保留在各自既有权威目录，不复制到这里。

不确定归属时使用对应根下的 `Quarantine/<task>/`，记录来源和待确认原因；
禁止未经明确授权删除、重命名、覆盖或批量迁移。

Skill 本体仍只存在于 `.agents/skills/<skill-name>/`；公共空间可保存 AISpace 所有的稳定
候选、关系索引和协作内容，但不复制 Skill 文件夹；见 `Public/Skills/INDEX.md`。全量 Public/Private 扫描
与保留/迁移决策见 `Public/MIGRATION_MANIFEST.md`。

## Skill 生成/缓存绑定

需要生成候选、截图、缓存、稳定协作内容或协作索引的 Skill，必须在注册时登记到
`.agents/SKILL_AISPACE_BINDINGS.json`。该文件是 Skill 注册契约：每条记录包含稳定
`bindingId`、用途、存储类别、项目相对 `pathTemplate`、生命周期和写入策略，并回指对应
`.agents/skills/<skill-name>/governance.json`。

AISpace 的 `Public/Skills/registry.json` 是由注册契约生成的反向投影。它同时记录
`skillContractPath`、`registryPath` 和绑定 ID，因此可以从 Skill 找到 AISpace 落点，
也可以从 AISpace 找回 Skill 合同，形成双向可发现关系。没有生成/缓存需求的 Skill 不必
伪造绑定；新增或升级 Skill 时若产生这类内容，必须先补登记再生成 Catalog、Registry
Manifest 和关系投影。

绑定不会扩大 Skill 权限，也不会夺取其他目录的权威。归 AISpace 所有的稳定内容可以进入
分类/日期目录；外部权威产物仍保留在原目录，并由 AISpace 保存索引。实际 Unity
资产现在使用统一的 `Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/` 出口。旧物理路径
`Assets/ES/Space/Public` 已迁移完成，不得再作为新内容落点；发现旧路径时应标记为
迁移残留并回溯引用。
