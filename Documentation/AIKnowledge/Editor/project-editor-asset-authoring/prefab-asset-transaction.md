# Prefab 正式资产编辑与提交事务

`KnowledgeId`: `es.unity.editor-prefab-asset-transaction.v1`

`Topic`: Unity Prefab instance override、正式 Prefab Asset 与 ES 分阶段提交事务

`Summary`: 区分场景实例 override 与 Prefab Asset 写入，并记录稳定身份、预检、幂等、部分阶段完成和恢复语义。

`Authority`: `Unity 2022.3 official documentation + AIWarnings + current source`

`RouteKeys`: `editor`, `prefab`, `asset-authoring`, `prefab-override`, `save-as-prefab-asset`, `stable-identity`, `preflight`, `commit`, `rollback`, `idempotency`

`ContentHash`: `48a130df78225dc9179d5a5a74e4b77e9cb6220f1c6f58ab37dee43e2365bf44`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity Editor 版本、任一 UnityOfficialReferences 响应内容哈希、Unity PrefabUtility/AssetDatabase 合同、ES 内容注册事务、Item Prefab 作者工具、稳定身份规则、相关测试定义或任一 SourceRef 哈希变化。

`RuntimeAcceptance`: `runtime-not-run`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`、`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`

## Scope

本条目负责区分 Prefab instance override 与正式 Prefab Asset 写入，并约束资产路径、稳定身份、预检、分阶段提交、幂等和失败恢复。它不负责一般 SerializedProperty 编辑、字段迁移、Scene Builder 或任意实体 Prefab 的运行时集成。

- 普通属性写入和 Prefab instance override 记录归 `es.unity.editor-serialized-undo-dirty.v1`。
- GUID/local file id、字段迁移和嵌套 Prefab 身份归 `es.unity.serialization-prefab-identity.v1`。
- 测试 Scene Builder、Fixture 和备份归 `es.unity.editor.project-scene-builder-authority.v1`。

## Trigger and routing

- 自然语言触发：创建或覆盖 Prefab Asset、SaveAsPrefabAsset、Prefab Variant、正式 Item Prefab、GUID/local file id、内容注册、预检/提交、部分失败重试和幂等。
- 精确 routeKeys：`prefab`, `asset-authoring`, `prefab-override`, `save-as-prefab-asset`, `stable-identity`, `preflight`, `commit`, `rollback`, `idempotency`。
- 默认命中本条目；实例属性修改追加 Serialized/Undo 条目；身份迁移追加序列化身份条目；Scene Builder 任务改读场景权威条目。
- 误路由回退：若任务只修改 Scene instance、Inspector 字段、Draft 或运行时 Entity，不得沿用正式 Prefab Asset 提交流程。

## Decision rules

1. SourceRef/官方响应哈希漂移、目标路径或 Prefab 类型不明确时，标记 `stale` 并停止。
2. 写入前必须确定是 instance override 还是 Prefab Asset；两条路径的 Undo、保存和后置证据不能互换。
3. Key/身份冲突、目标 Dirty、编译/导入/Bake 占用、预检票据漂移或同名层级歧义时，必须 `Blocked`，不得自动覆盖。
4. 分阶段事务失败时必须报告已经完成的正式资产阶段；只有重新核对 Definition、Prefab、Library 和身份一致，才能称恢复成功。
5. 实际创建/覆盖 Prefab 或修改项目文件由当前用户明确目标授权；执行 Unity 必须由用户单独点名并声明 Runtime 证据预算。仅选用受管通道时要求匹配 AICommand/TaskContract；静态知识保持 S1。

## Core conclusion

Prefab 编辑必须先区分“修改场景中的 Prefab instance override”和“创建或覆盖正式 Prefab Asset”。前者写实例并记录 override；后者写资产路径并重新核对保存结果、GUID/local file id、领域注册和失败后的已完成阶段。两条路径不能用同一个 Dirty 或保存调用互相替代。

## 两类 Prefab 写入

### Prefab instance override

- 优先通过 `SerializedObject` / `SerializedProperty` 编辑，获得 Undo、Dirty 和 override 语义。
- 直接修改时先 `Undo.RecordObject`，修改后调用 `PrefabUtility.RecordPrefabInstancePropertyModifications`。只 SetDirty 或只 RecordObject 都不能完整替代 override 记录。
- 实例所在 Scene 是否落盘是另一个步骤；记录 override 不等于 Scene 已保存。

### Prefab Asset 创建或覆盖

- `PrefabUtility.SaveAsPrefabAsset(instanceRoot, assetPath)` 从给定根创建 Prefab Asset，不修改输入对象。输入必须是普通 GameObject 或 Prefab instance 最外层根。
- 如果输入是 Prefab instance 根，结果是 Prefab Variant；需要独立 Prefab 时必须先显式 unpack，不能隐式改变资产语义。
- 覆盖现有 Prefab 时 Unity 按 GameObject 名称尝试保持引用；层级中存在重名 GameObject 或同对象挂有多个同类型 Component 会导致匹配不可预测，正式工具应在保存前阻断这类歧义。
- 在 `AssetDatabase.StartAssetEditing` 批处理期间，保存可能成功但返回 `null`，因为资产尚未导入。调用方必须结合批处理上下文和 `out success`/后置核对解释结果，不能无条件把 `null` 当作同一种失败。

## ES Item Prefab 作者流程

当前 `ESItemPrefabAuthoring` 的一次请求按以下阶段推进：

```text
项目级 Key/身份/Dirty 预检
  -> 创建并保存 Definition（若缺失）
  -> 构建临时 GameObject 根并验证
  -> SaveAsPrefabAsset，finally 释放临时根
  -> 读取 Prefab GUID + local file id
  -> 将稳定身份绑定并保存回 Definition
  -> 内容注册 commit=false 预检
  -> 使用同一 requestId、fingerprint、GUID、local file id、revision 提交
  -> 读取 Prefab 身份并验证 Definition/Prefab/Library 后置条件
```

`ESContentRegistrationAuthoring` 把预检和提交绑定到当前 Editor 进程。Domain Reload、输入变化、身份或 revision 漂移会使旧预检失效；编译、AssetDatabase 更新/导入或资源 Bake 占用时拒绝写入。

## 失败与恢复语义

- 创建 Definition 后、构建 Prefab 前失败时，Definition 可能已经成为正式资产。当前测试定义要求重试能复用已完成阶段，而不是假装跨资产全原子回滚。
- 临时构建根必须在 `finally` 中释放；这只证明临时对象清理路径存在，不证明 Prefab 保存与导入成功。
- 同 Key 不同身份、同身份不同 Key、重复页面、目标 Library/Definition/Prefab Dirty 都应在写入或注册前阻断。
- 内容注册提交失败会尝试恢复旧字段并再次落盘；只有重新核对权威对象一致，才能称回滚成功。
- 幂等重入要求 Definition、Prefab 身份和 Library 注册全部一致。路径存在、同名对象或按钮重复执行本身不是幂等证据。

## Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作、恢复与缺失证据 |
|---|---|---|---|
| 混淆 instance override 与 Asset 写入 | Scene 值变化却以为源 Prefab 已保存 | 先分类目标和权威对象 | 分别走 override 或 SaveAsPrefabAsset；重新打开目标验证 |
| 仅凭返回对象/路径判断成功 | 导入延迟、批处理 `null` 或错误资产被误报成功 | 检查批处理上下文和后置身份 | 重读资产并核对 GUID/local file id；缺回执保持 Blocked |
| 忽略 Variant 与同名歧义 | 生成意外 Variant 或引用匹配不确定 | 检查输入根、嵌套关系、重名节点和同类组件 | 显式 unpack 或阻断保存；修正结构后重新预检 |
| 假装跨资产全原子 | Definition 已创建但错误报告“全部回滚” | 记录每个阶段的持久化边界 | 报告已完成阶段并幂等重试；逐一核对权威资产 |
| 复用失效预检票据 | Domain Reload/输入漂移后提交旧请求 | 核对 requestId、fingerprint、identity、revision | 丢弃旧票据并重新 preview；禁止猜测恢复 |
| 把测试源码当执行成功 | 交付声称幂等/回滚已通过 | 要求本次 Test Runner 和 Prefab reopen 证据 | 无回执保持 `definition-only`、S1 和 `runtime-not-run` |

## Execution checklist

```text
开始前：读 Start/CurrentStatus/RuleIndex -> 验证 SourceRef -> 分类 override/Asset -> 检查权限、路径、Dirty、锁、身份和 Editor 时机
实施中：全量预检 -> 建立或复用 Definition -> 构建临时根 -> 保存 Prefab -> 读取身份 -> preview/commit 注册
失败时：finally 清理临时根 -> 停止后续阶段 -> 报告已完成资产 -> 恢复可恢复字段 -> 重新核对后才声明恢复
完成后：重读 Definition/Prefab/Library -> 核对 GUID/local file id/revision -> 验证幂等重入和冲突拒绝
不可跳过：Unity Test Runner、Prefab reopen、Variant/嵌套/重名、导入失败、失败注入和 Domain Reload
禁止：自动覆盖冲突；把 Dirty/路径/按钮/测试定义当成保存成功；无权限修改 Prefab、Scene、Library 或源码
```

## Evidence boundary

### 已验证事实

- 当前项目版本为 Unity `2022.3.45f1`。
- Unity 官方文档明确区分 Prefab instance modifications 与 Prefab Asset 保存，并说明 SaveAsPrefabAsset 的输入、Variant、引用匹配和批处理返回值边界。
- 当前 ES 源码存在 Dirty 预检、稳定身份读取、Definition/Prefab 分阶段创建、preview/commit 注册与临时根 finally 清理。
- 测试源码定义了重入幂等、Dirty 阻断、跨 Library 冲突、重复注册、晚到冲突和部分阶段失败后重试案例。

### 推导

- 多资产作者工具应把“全原子事务”和“可恢复分阶段事务”明确二选一；当前 Item Prefab 流程属于后者，交付时必须报告已完成阶段。
- Prefab 保存成功必须绑定具体资产路径和身份后置条件，不能只依据非空内存对象、Dirty 状态或 Project 视图中出现文件。

### 非声明

- `runtime-not-run`：本次未创建、覆盖或保存任何 Prefab/Definition/Library，未运行 Unity Test Runner 或 Domain Reload。
- 未证明嵌套 Prefab、Variant、同名节点引用保持、AssetEditing 批处理、导入失败、只读文件、版本控制锁或并发作者会话行为。
- 测试定义和源码路径不能替代 Unity 实机、Prefab reopen、Undo/Redo、失败注入和发布验收。

## UnityOfficialReferences

以下响应在 2026-08-23 返回 HTTP 200；响应内容哈希不参与本地 `ContentHash`。

| Unity 2022.3 官方文档 | SHA-256 |
|---|---|
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.RecordPrefabInstancePropertyModifications.html | `f85e013cac748b171f1d2e6c86332cb31ae4ef637807605bddd5f83b094bb5f7` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html | `ee6fb284907e76a909a7253d29015c36eafc1db77e81979f7689e03ffc0016ab` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.html | `db76fae1f10d348c4bec39d964cb9f13aff5a5d524968cc17be4e9648486732d` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RecordObject.html | `64e22af38a58cc39f0ff2d8b1fe2723dad1a9cd147240ae772fd6b4cc721c107` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorUtility.SetDirty.html | `c36eeae8ad4e94915664e6c3df10a021de8ea93a6a78ec8777cd447dfc0d36f2` |

## EvidenceRefs

- `StaticReview`: 已交叉读取 Unity 2022.3 官方文档、AIWarnings、内容注册与 Item Prefab 作者工具当前源码。
- `TestDefinition`: `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESItemPrefabAuthoringTests.cs` 定义重入幂等、Dirty 阻断、冲突拒绝和部分阶段失败后重试案例；本次未运行这些测试。
- `Runtime`: `runtime-not-run`；没有 Prefab 创建/覆盖、重新打开、失败注入或 Unity Test Runner 回执。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)
- `Assets/Plugins/ES/Editor/ESContentRegistration/ESContentRegistrationAuthoring.cs` (`2184f8b6e14f4cb557e59cf813e34750105838c7155b9efbf973bb2abb9539ac`)
- `Assets/Scripts/ESLogic/Editor/WeaponTemplates/ESItemPrefabAuthoring.cs` (`886066fca3956601e89fab7658770f8d280d82b0f46ed177c66c3e97f7f0533d`)
- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESItemPrefabAuthoringTests.cs` (`2c87dcab0b3b22fa8b6493487c6a37761364cdc8bf6a90d0f71148d34ff4d9df`)
