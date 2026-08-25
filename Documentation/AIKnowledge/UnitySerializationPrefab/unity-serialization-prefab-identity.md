# Unity 序列化、Prefab 与持久身份（2022.3.45f1）

`KnowledgeId`: `es.unity.serialization-prefab-identity.v1`  
`Authority`: `Unity 2022.3 official documentation + UnityCsReference 2022.3 + project version`  
`RouteKeys`: `unity`, `serialization`, `prefab`, `asset-guid`, `local-file-id`, `instance-id`, `serialize-reference`, `field-migration`, `prefab-override`  
`ContentHash`: `697d242d955d9b0d6f61e32b4c9fc3340cc36b538ab99a8555ee33ca13e946a3`

## Scope

本条目整理 Unity `2022.3.45f1` 中最容易混淆的四类身份：序列化字段、资产 GUID、资产内 local file ID、会话内 Instance ID，并把它们映射到 Prefab 源对象、嵌套 Prefab 和 Override。目标是让 AI 在重命名、移动、复制、重导入或重开 Editor 前先判断哪一层身份必须保持。

本条目已在当前用户明确授权下登记到共享 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md`。AICommand 状态为 `NoMatchingCommand`；路由登记不授权修改任何序列化对象、Prefab 或资产。

## 已验证事实

### 1. 字段和值

- Unity 的脚本序列化直接处理符合规则的字段，不处理普通 C# 属性。
- `UnityEngine.Object` 派生对象可作为序列化引用；普通可序列化自定义类默认按值内联。
- `[SerializeReference]` 把普通托管对象作为宿主对象中的 managed reference 保存。这个身份属于宿主序列化数据，不应被当成跨资产 GUID。
- `FormerlySerializedAsAttribute` 只描述字段旧名迁移。它可以为字段重命名提供读取线索，但不替代类型迁移、资产 GUID 或 Prefab 对象身份。

### 2. 资产与子对象

- 资产 GUID 存在相邻 `.meta` 文件中。Unity 在 Project 窗口内移动或重命名资产时会同步处理 `.meta`。
- 在 Unity 外移动或重命名资产但没有同步 `.meta`，Unity 会把它视为新资产并生成新元数据，旧 GUID 引用随之断开。
- Unity 持久化资产引用使用 `(GUID, file ID)`：GUID 选择资产，file ID 选择该资产内的对象。
- local file ID 必须按 `long` 处理。Unity 官方 API 和 C# 参考源码都明确指出 Prefab 的 local ID 可能超过 32 位。
- 文本序列化文件中的 YAML 文档对象 ID 只在该文件内唯一，Unity 文档将其分配描述为 arbitrary；不能把它当成项目级稳定 Key。

### 3. 会话对象

- `Object.GetInstanceID()` 返回内存实例句柄。它会在 Editor 或 Player 会话之间变化，不能持久化到文件或用作重开后的对象身份。
- `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` 可以从当前实例解析持久化 `(GUID, local file ID)`；调用方必须检查布尔返回值，不能假设任意运行时对象都有资产身份。

### 4. Prefab 关系

- `PrefabUtility.GetCorrespondingObjectFromSource` 用来从 Prefab 实例对象解析对应的 Prefab Asset 对象；解析失败返回 `null`。
- Prefab 实例上的 Override 值优先于 Prefab Asset 值。因此修改源 Prefab 不保证覆盖已经存在 Override 的实例字段。
- 嵌套 Prefab 应保留对自身 Prefab Asset 的连接；嵌套关系不应被扁平化理解成只属于最外层 Prefab 的普通子节点。

### 版本校准决策卡（Unity 2022.3）

- `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` 的持久身份结果是 `(GUID, long localFileId)`；
  必须检查返回的 `bool`，不能把没有资产身份的运行时对象伪装成持久引用，也不能把 `long` 截成
  `int`。
- `.meta` 是资产 GUID 的一部分：Project 窗口内移动/改名应保持配对；外部移动、缺失或错配
  `.meta` 时按新资产/引用漂移处理，停止按旧 GUID 继续写入。
- `Object.GetInstanceID()` 只表示当前 Editor/Player 会话中的内存实例，不得写入长期配置、
  Knowledge、缓存或跨 Domain/进程恢复表。
- Prefab 实例到源对象必须使用 `PrefabUtility.GetCorrespondingObjectFromSource` 并处理 `null`；
  修改源值前先枚举实例 Override，不能用名称、Hierarchy 路径或最外层根对象猜测嵌套源。

## 身份选择矩阵

| 需求 | 应使用 | 禁止替代 |
|---|---|---|
| 跨 Editor 重开定位资产 | `.meta` GUID | Instance ID、绝对磁盘路径 |
| 定位资产内子对象 | GUID + `long` local file ID | 仅 GUID、对象名、Hierarchy 路径 |
| 当前会话临时查找对象 | Instance ID | 持久化存盘后跨会话复用 |
| 普通托管对象共享引用/多态 | `[SerializeReference]` managed reference | 把它当成 `UnityEngine.Object` 资产引用 |
| 字段重命名兼容 | `[FormerlySerializedAs]` 加迁移验证 | 仅改字段名并假设旧数据自动匹配 |
| Prefab 实例到源对象 | `PrefabUtility.GetCorrespondingObjectFromSource` | 通过名字或 Transform 路径猜测 |
| 判断源值是否会传播 | 先检查实例 Override | 只检查 Prefab Asset 当前值 |

## 从机制推导的制作规则

1. 资产移动、重命名和复制必须把 `.meta` 视为身份的一部分；版本控制必须成对保留资产和 `.meta`。
2. 编辑器工具记录长期引用时，优先存 Unity 支持的对象引用；只有需要显式诊断或外部表映射时才读取 GUID/local file ID，且 local ID 使用 `long`。
3. 不手工生成或全局替换 YAML `fileID`。文本 YAML 适合审查差异，不是绕过 `AssetDatabase`、`SerializedObject` 或 `PrefabUtility` 的默认写接口。
4. 字段重命名、字段类型变化、脚本移动、程序集调整和 Prefab 结构调整是不同迁移轴，必须分别验证；`FormerlySerializedAs` 只覆盖字段旧名这一轴。
5. 修改 Prefab Asset 前先枚举实例 Override 风险；处理嵌套 Prefab 时对每一层解析对应源对象，不能只操作最外层根对象。
6. 任何需要跨重开保持的自定义业务 ID 都应由业务稳定身份合同单独定义，不能复用 Instance ID，也不能把 Unity YAML 文件内 ID 宣称为业务全局 Key。

## 失败模式与停止条件

| 失败模式 | 可观察后果 | 最小恢复方向 |
|---|---|---|
| 资产与 `.meta` 分离 | GUID 改变，引用丢失或脚本 Missing | 恢复原 `.meta`；在确认引用图前停止继续保存 |
| 把 local file ID 截断为 `int` | Prefab 子对象解析错误或溢出 | 全链改用 `long`，重新读取官方 API 返回值 |
| 持久化 Instance ID | 重开后指向错误对象或无法解析 | 改存 Unity 对象引用或 GUID/local file ID |
| 字段直接重命名 | 旧序列化数据不再进入新字段 | 添加受控迁移并执行旧资产重开验证 |
| 忽略实例 Override | 源 Prefab 修改没有传播到目标实例 | 明确 Revert/Apply/保留 Override 决策 |
| 通过名字猜嵌套源对象 | 同名节点或结构变化后误修改 | 使用 Prefab 对应源对象 API，并处理 `null` |

任何 SourceRef 哈希变化、Unity 版本变化、GUID/local ID API 合同变化或 Prefab 序列化行为需要升级结论时，本条目立即 stale，停止把当前摘要作为新事实。

## 最小验证清单

以下为后续 Runtime/Editor 验收设计，本次未执行：

1. 创建含根对象、子对象、组件、嵌套 Prefab 和实例 Override 的隔离 fixture。
2. 记录资产 GUID 与所有相关对象的 `long` local file ID；保存、关闭 Editor、重开后重新解析并比较。
3. 在 Unity Project 窗口内移动资产，确认 GUID 保持；在隔离副本中模拟缺失 `.meta`，确认门禁能检测引用漂移并恢复。
4. 对字段重命名分别执行“带/不带 `FormerlySerializedAs`”的旧资产反序列化用例。
5. 对 Prefab Apply、Revert、嵌套源解析和已有 Override 执行正向、失败及恢复用例。
6. 检查 Console、Missing Script、丢失引用、Prefab Override 状态和重新序列化差异。

`runtime-not-run`：本次没有启动 Unity、没有执行 Domain Reload、Prefab 保存/重开、PlayMode、Player、IL2CPP 或发布验收。因此这里只能声明静态资料整理和条目合同验证，不能声明 Prefab 重开行为已通过。

## Assumptions and non-claims

- 该条目假设项目继续使用 `2022.3.45f1`；其他 Unity patch/minor 版本必须重新核对官方文档与 UnityCsReference。
- 没有检查项目内任何具体 Prefab 是否存在 GUID/file ID 漂移，也没有修改、导入或重保存资产。
- 没有证明 `fileID` 在任意导入器变化、脚本类型变化或结构重建后保持不变；需要跨此类变化时必须对目标资产做专门迁移验证。
- 没有证明编辑器工具、序列化回调或第三方 Odin 数据与 Unity 原生序列化具有相同身份合同。
- AIBrain 已能通过 routeKeys 发现本条目；这不证明任何具体 Prefab、序列化迁移或 Unity 行为。

## SourceRefs

- `Documentation/AIKnowledge/UnitySerializationPrefab/official-source-snapshot.md` (`bc7aea27f30ad1e7a5af08747f30519979fef0227d1557d7f9f2543c26cf611b`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)

`EvidenceLevel`: `S1`  
`StaleWhen`: Unity 版本、官方序列化/AssetDatabase/PrefabUtility 合同、官方资料快照、项目版本文件或本条目任一 SourceRef 哈希变化。
