# ES Editor 正式资产生产与事务边界

`KnowledgeId`: `es.project.editor-asset-authoring.v1`

`Topic`: ES Editor 正式资产生产、序列化修改与提交事务边界

`Summary`: 区分作者修改、Unity 正式资产落盘和 ES 领域 preview/commit，并限定 Undo、Dirty、稳定身份、失败恢复与证据声明。

`Authority`: `Unity 2022.3 official documentation + current source + AIWarnings`

`RouteKeys`: `editor`, `asset-authoring`, `asset-database`, `prefab`, `scene`, `serialized-object`, `undo`, `dirty`, `save`, `transaction`, `stable-identity`

`ContentHash`: `8b3a02c76bdbd591273da56d6abfebd28976012b964320905e77cdcab6c20568`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity Editor 版本、任一 UnityOfficialReferences 响应内容哈希、AssetDatabase/PrefabUtility/SerializedObject/Undo 合同、ES 内容注册事务、Prefab 作者工具、Workbench Draft/Source 提交协议、任一 SourceRef 哈希或正式知识索引路由发生变化。

`RuntimeAcceptance`: `runtime-not-run`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`

## Scope

本条目是 Editor 正式资产生产的路由枢纽，只负责先识别权威对象、修改类型和所需证据，再选择 canonical Knowledge。它不拥有 SerializedProperty、Prefab、Workbench、Scene Builder、EditorWindow 或稳定身份的详细 API/实现事实，也不授权任何资产写入。

## Trigger and routing

- 自然语言触发：Editor 中创建/修改/保存正式资产、Undo/Dirty/Save、Prefab、SerializedObject、Scene、Draft、内容注册、稳定身份或事务恢复。
- 当前 routeKeys 用于发现本路由枢纽；进入正文后必须按下表收敛到 1～3 个 canonical 条目，禁止把本条目当作万能实现指南。
- 误路由回退：无法判断权威对象时停止写入，回读当前源码、AIWarnings Start 链和 `KnowledgeIndex.yaml`；不得用最相似 API 猜测。

## Decision rules

1. 先回答“正在修改哪个权威对象”：Inspector/SerializedObject、Prefab instance、Prefab Asset、Scene、Draft/Source、稳定身份或 EditorWindow。
2. SourceRef、索引绑定、Unity 版本或官方响应哈希发生漂移时，本条目立即 `stale`，旧计划不得继续。
3. 任务同时命中多个对象时，先读取共同上游，再最多追加两个细分条目；超过三个条目时拆成具名批次。
4. 当前用户未明确要求修改或相应 Unity 动作时，只能分析或生成候选计划；Dirty、按钮、文件和测试定义都不授予 AI 自行执行。用户已经明确要求时可在其范围内直接实施；只有选用受管通道才要求匹配 AICommand 与 TaskContract。
5. 完成声明必须绑定具体对象的后置条件和真实证据；静态知识只能维持 S1。

## Canonical ownership and deduplication

| 决策对象 | canonical Knowledge | 本条目保留内容 | 不在本条目重复的内容 |
|---|---|---|---|
| EditorWindow、owner、ReloadDomain、菜单 | `es.unity.editor-window-lifecycle-menu.v1` | 路由条件 | 生命周期 API、菜单规则和检查清单 |
| SerializedProperty、多目标、Undo/Dirty | `es.unity.editor-serialized-undo-dirty.v1` | 修改类型分流 | 数据流、Undo 表和回滚算法 |
| Prefab Asset、预检/提交、幂等 | `es.unity.editor-prefab-asset-transaction.v1` | 正式资产分流 | SaveAsPrefabAsset、身份绑定和阶段恢复 |
| GUID/local file id、字段迁移、嵌套 Prefab | `es.unity.serialization-prefab-identity.v1` | 身份路由 | 持久身份和迁移事实 |
| Workbench Draft/Source、外部漂移 | `es.project.editor-workbench-authoring.v1` | 长会话分流 | Draft、Baseline、Commit 和会话恢复 |
| Scene Builder、Fixture、override 审计、备份 | `es.unity.editor.project-scene-builder-authority.v1` | Scene 分流 | Builder 权威、备份和 override 分类 |

## Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作与恢复 |
|---|---|---|---|
| 一次加载所有 Editor 条目 | 上下文被重复事实占满 | 先识别权威对象和动作 | 只选 canonical 1～3 条；歧义时停止并重路由 |
| 把本总览当 API 权威 | 使用过时或不适用的保存方式 | 检查是否已进入细分条目和 SourceRefs | 回读 canonical 条目及当前源码 |
| 混淆 Dirty、Undo、Save 和提交 | UI 有变化却无正式资产证据 | 为目标列出四类状态和后置条件 | 路由到 Serialized/Prefab/Workbench 条目分别闭环 |
| 把测试或按钮存在当成功 | EvidenceLevel 被夸大 | 要求当前运行回执与具体产物 | 无回执保持 `runtime-not-run` |
| 没有用户指令仍扩大范围 | AI 自行修改了资产、索引或外部状态 | 区分当前用户范围、受管协议和工具能力 | 停止写入；保留计划并请求用户明确目标或动作 |

## Execution checklist

```text
开始前：读 Start/CurrentStatus/RuleIndex -> 验证 SourceRef/ContentHash -> 审计工作树和权限
路由：识别权威对象 -> 选择 canonical 1～3 条 -> 读取其 RequiredReads 和当前源码
实施：按 canonical 条目的预检、写入、失败恢复和后置条件执行
完成后：重新核对权威对象、身份、Undo/Dirty/Save/Commit 和证据等级
禁止：从总览直接推导 API；跨对象复用成功证据；用静态检查冒充 Unity/发布通过
```

## Evidence boundary

### 已验证事实

- 当前项目版本文件和本条目 SourceRefs 可由静态验证器回读并重算哈希。
- `KnowledgeIndex.yaml` 已为上述 canonical 条目提供独立路由绑定。

### 推导

- 本条目的价值是减少误路由和重复读取；具体实现结论始终由 canonical 条目及其更高权威来源裁决。

### 非声明

- `runtime-not-run`：未启动 Unity、未修改任何资产、未执行 Undo/Redo、Prefab/Scene 保存、Test Runner、Profiler、Player、IL2CPP 或发布。
- 索引存在、源码存在、测试定义存在和静态验证通过均不证明 Editor 资产生产行为可用。

## UnityOfficialReferences

以下为读取时的响应内容哈希，用于识别本次外部依据；它们不参与本地 `ContentHash` 计算。

| Unity 2022.3 官方文档 | HTTP | 响应内容 SHA-256 |
|---|---:|---|
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.CreateAsset.html | 200 | `67ce60e534f990e0cd1c33e4b72de5db98b2ca51062592156d0b11dd8a5467b8` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.SaveAssetIfDirty.html | 200 | `bb6000ff90682f6cecacb073436edd13c10a5b0886efc00db975bdde710d36aa` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RecordObject.html | 200 | `64e22af38a58cc39f0ff2d8b1fe2723dad1a9cd147240ae772fd6b4cc721c107` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorUtility.SetDirty.html | 200 | `c36eeae8ad4e94915664e6c3df10a021de8ea93a6a78ec8777cd447dfc0d36f2` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html | 200 | `ee6fb284907e76a909a7253d29015c36eafc1db77e81979f7689e03ffc0016ab` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.SaveScene.html | 200 | `eb1d1abe9fc8cec8d0e59885c7f609dc84d34972431e4cb28d5d3eeb3cb9405e` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.ApplyModifiedProperties.html | 200 | `3d529a357c10585028a4bef767223142fe039817babf011be29f4b45a5f51665` |

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编码与文本（Encoding）/项目最高警告_P0_UTF8唯一编码_禁止AI默认代码页覆写与机械转码_AI协作警告.md` (`81969fa8ebb72586dc30d79ea6f20182cf31b801af930ad97ef355b2d3fc57eb`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`bda2011a12df8424e091a5e6d1cd9cbb8c8297dc0ea64c9ee49927df66f23177`)
- `Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSerializedMutation.cs` (`67f4e4077bb7cd504f4b22a2a926c72ce03bf3cd4370a9feeca1b5c0e8404091`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationAuthoring.cs` (`2184f8b6e14f4cb557e59cf813e34750105838c7155b9efbf973bb2abb9539ac`)
- `Assets/Scripts/ESLogic/Editor/WeaponTemplates/ESItemPrefabAuthoring.cs` (`886066fca3956601e89fab7658770f8d280d82b0f46ed177c66c3e97f7f0533d`)
- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESItemPrefabAuthoringTests.cs` (`2c87dcab0b3b22fa8b6493487c6a37761364cdc8bf6a90d0f71148d34ff4d9df`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldEditSession.cs` (`8300cd18fd60715d75b5f1f74c7e6d2b023b5e4c59dd36df177cd665a0913f0b`)
