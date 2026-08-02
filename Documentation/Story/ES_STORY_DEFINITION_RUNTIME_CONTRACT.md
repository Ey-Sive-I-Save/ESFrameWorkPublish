# ES Story Definition Runtime Contract

状态：切片 A 冻结契约。

## 1. 身份

- `DefinitionId` 是稳定内容身份，由 `ESStoryConfigKey.StringKey` 表达。
- `StoryInstanceId` 是一次运行实例身份；重复或并发执行同一 Definition 时必须不同。
- `SessionId + SessionGeneration` 是一次前台对话会话身份，只用于当前进程的 UI、Interaction 与 RuntimeMode 协作。
- `NodeId` 在 Definition 内稳定；`OptionId` 在 Definition + Node 内稳定；`ActionId` 在 Definition + Node 内稳定。
- `SoDataInfo.KeyName` 只用于 Group、表格和编辑器定位，不得参与运行、存档、网络或迁移。

## 2. 作者数据、发布数据与运行数据

```text
ESStoryDefinitionDataInfo + ESStoryDefinitionGroup
→ 校验与烘焙
→ DefinitionId + ContentVersion + ContentSignature
→ 不可变 ESStoryDefinitionSnapshot
```

- 切片 A 可从作者 SO 同步烘焙 Snapshot，但必须标记为“不支持运行时热更新”。
- StoryInstance 只能持有不可变 Snapshot，不得长期持有作者 SO。
- `ESStoryDefinitionSnapshot` 是不可变内容快照；保存分区中的 StorySnapshot/Section 仅是序列化 DTO，两者均不得成为 Quest 进度的第二权威。
- Snapshot 必须复制全部运行字段，并建立 NodeId 索引；不得通过作者对象的可变列表继续取值。
- 活动实例固定 `DefinitionId + ContentVersion + ContentSignature`。Provider 或 Catalog 重建后只能重新解析完全相同版本；缺失时安全挂起或失败，禁止静默切换到最新版。
- Payload 不保存 CLR 类型名，不依赖任意反射、ScriptableObject 子节点或列表下标。

## 3. 切片 A 节点

只允许：`Start`、`Dialogue`、`Choice`、`Condition`、`Action`、`Complete`、`Fail`。

- Start：确定性跳转。
- Dialogue：发布只读显示数据，等待显式继续或进入 Choice。
- Choice：发布稳定 OptionId，并等待带 SessionGeneration 与 ViewRevision 的提交。
- Condition：只读判断，不产生副作用。
- Action：仅允许同步白名单 `Tags.SetTag(stableTag, absoluteState)`。
- Complete/Fail：结束实例并释放所有临时绑定。

## 4. 图校验

- 重复或空 NodeId、OptionId、ActionId 是发布错误。
- 缺失 EntryNode、NextNode 或 Option 目标是发布错误。
- 不可达节点是发布错误，除非显式标记为保留迁移节点。
- 图循环不是天然错误。
- 运行时每次推进必须有最大同步步数；超过上限视为“无进展循环”，实例失败并记录有界诊断。
- 校验器应区分：允许循环、无进展循环风险、不可达节点。

## 5. 版本与签名

- `ContentVersion` 标识正式内容版本。
- `ContentSignature` 由稳定身份、节点类型、稳定出口和 Payload 规范化内容确定性计算。
- 相同 ContentVersion 出现不同签名必须阻断发布。
- 已发布稳定 ID 的重命名属于迁移；不得通过显示名或节点顺序猜测旧身份。
- 切片 A 不实现跨 ContentVersion 迁移；版本不匹配时安全拒绝恢复。
- Load 只恢复 QuestRecord。下一次合法交互必须按 Record 固定的 DefinitionId、ContentVersion 与 ContentSignature 精确解析 Snapshot；解析失败时拒绝水合。

## 6. 禁止事项

- 不以 KeyName、GUID、路径、RuntimeKey、InstanceID 或列表下标作为 DefinitionId。
- 不让 Group、Pack、ResourcePlan 或 Manifest 成为第二份 Story 内容权威。
- 不在 Snapshot 内保存 Unity Object、Lease、Token、Provider、Scope 或委托。
- 不建立 Story 私有资源下载、缓存或引用计数系统。

## 7. 验收

- 源码：作者数据、Group、校验器、不可变 Snapshot 与精确版本解析入口存在。
- 静态编译：不得替代 Unity 导入。
- Unity Editor：验证中文 Inspector、Picker、重复 ID、缺失出口、循环分类和签名阻断。
- Unity Test Runner：验证 Snapshot 与作者 SO 脱离、版本固定和确定性签名。
- PlayMode：验证活动实例不会因内容源重建切换版本。
- IL2CPP：验证不依赖反射或 CLR 类型名恢复 Payload。
