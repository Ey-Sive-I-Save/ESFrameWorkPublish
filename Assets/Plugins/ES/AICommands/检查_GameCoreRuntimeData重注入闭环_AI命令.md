# 检查：GameCore RuntimeData 重注入闭环（P0）

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按“命令类型”和“默认改文件”决定是否允许改代码。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和相关入口文件，避免误改其他 AI 或用户的改动。
5. 只做本文允许的事情；如果用户需求超出本文范围，先说明需要换用哪个命令。
6. 结束时必须给出：已读规则、执行内容、改动文件、验证结果、剩余风险。
```

命令类型：P0 架构检查。
默认改文件：否。用户明确要求修复时，才允许修改 GameCore RuntimeData、强类型 Table、根 SO 注入和对应测试。
风险等级：L3。错误判断会造成稳定引用失效、资源强引用泄漏、重复 Key 污染或 RuntimeKey/Ready 不一致。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编码与文本（Encoding）/项目最高警告_P0_UTF8唯一编码_禁止AI默认代码页覆写与机械转码_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md
Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
Assets/Scripts/ESLogic/Data/GameCoreConfigKey/
Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/
Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs
```

## 检查范围

默认检查现有六类：

```text
Buff
Skill
Monster
NPC
Shot
Weapon
```

用户指定新类别时，将同一检查完整应用到该类别，不得降低标准。

## 强制检查项

### 1. 类型与表

```text
RuntimeData 是否继承非池化的 ESGameCoreRuntimeData。
Table 是否继承 ESGameCoreConfigKeyTable<TData>。
Table 是否使用唯一 GameCore.<Category> KeyScope，且没有 Rent、ResetRuntimeData、IPoolableAuto 或定义数据池。
是否仍存在普通 ESConfigKeyTable、Upsert 换实例、中央 switch、反射或万能工厂。
```

### 2. 稳定外壳与 Ready

```text
Clear/Remove 后旧引用是否保持同一对象。
旧引用是否 Ready=false。
同 Key 重建是否复用原对象。
已驻留对象是否始终保留，且没有任何回池入口或分配给其他 Key 的路径。
成功提交是否先同步实际 runtimeKey，最后 Ready=true。
缓存引用的业务读取是否检查 Ready。
```

### 3. 完整事务边界

逐个搜索所有 `AcquireRetained/TryAcquireRetained`，必须确认：

```text
Acquire 后立即进入 try。
CreateRuntimeData、SO 字段复制、默认值解析、filler、校验、Prefab/ExtraAsset 赋值全部在 try 内。
成功使用 CommitRetained/TryCommitRetained。
异常 catch 调用 AbandonRetained 后再 throw。
Try 入口准备失败、校验失败或提前 return false 前调用 AbandonRetained。
提交冲突或异常后 Ready=false 且不残留本次载荷。
```

禁止只检查 Commit 之后的 catch；提交前异常是本命令的 P0 检查重点。

### 4. 载荷释放与内存

逐类核对 `ReleaseRuntimePayload`：

```text
soSource
sharedData / class variableData
prefab / extraAsset
轨道、状态配置、集合、Operation、Tag 条件和其他领域重量级引用
```

值类型 VariableData 应恢复默认值。RuntimeData 只断开强引用，不直接重复释放 Loader Handle；底层 Lease/Handle 由 AssetScope 安全点释放。

### 5. RuntimeKey

```text
不得持久化到 SO、JSON、Catalog、Manifest、存档或网络。
不得由 Inspector 手工编辑或恢复。
不得把裸 int 跨表解释。
别名合并后返回实际活动槽位 RuntimeKey。
普通业务通过 EnumKey/StringKey 查询；RuntimeKey 仅用于当前表生命周期热路径。
```

### 6. 根 SO 与全局失败

```text
六个根 SO 注入点是否使用 CommitRetained，并在 catch 中 AbandonRetained。
重复 Key 是否明确失败而不替换。
Consumer 批量注入任一失败后是否清除半张表并释放 Consumer GameCore Scope。
ResetForResourceTransition 是否先清 GameCore Table，再释放 AssetScope/Provider。
```

### 7. 测试与验证

必须覆盖或明确报告缺失：

```text
提交失败清载荷
准备/filler 异常清载荷
失败后同 Key 仍复用同一实例
成功后 runtimeKey 与映射一致且 Ready=true
Clear/Remove 后载荷为空
别名合并返回实际槽位 RuntimeKey
误 Abandon 不破坏已提交记录
```

修复任务必须依次编译：

```text
dotnet build ES_Design.csproj --no-restore
dotnet build ES_Logic.csproj --no-restore
```

条件允许时运行 Unity EditMode Test Runner。Unity 已打开、许可证或外部状态阻断时必须明确说明，不得声称测试已执行。

## 禁止事项

```text
1. 禁止恢复 RegisterAndGetRuntimeKey + 手工写 runtimeKey 的旧注入模板。
2. 禁止用 Upsert 或新对象替换同 Key 稳定外壳。
3. 禁止只设置 Ready=false 而保留重量级载荷。
4. 禁止让 GameCore 定义外壳实现池接口、进入对象池，或用 Generation Handle 增加热路径成本。
5. 禁止用协程、反射、中央类别 switch 或每次注入分配事务 class 修复同步冷路径事务。
6. 禁止为了检查顺手修改无关工作区变化。
7. 禁止跳过 UTF-8、乱码、diff 和编译验证。
```

## ContractCompleteness

```yaml
commandId: gamecore.reinjection.review
writeMode: read-only
cancellation: N/A (read-only; no external effect; stop before analysis)
recovery: N/A (read-only; rerun from unchanged inputs; no rollback)
validation: read-only checks only; no writes, runtime, Git, release, or external effects
evidenceRef: source refs + SHA-256/content hash when available + read receipt; static evidence cannot claim Runtime
actionBoundary: AIBrain/ABCD selects intent and route; this command only reviews and reports; Automation/ABCC execution is out of scope
allowRoots: project files explicitly listed in 必须先读 and the contract's declared read-only targets only
denyPaths: source writes, undeclared paths, Git/history, release, Runtime/Unity, external services; deny-overrides
```
## 交付格式

```text
已读规则：
检查类别：
成功闭环：
发现问题（按严重度，带文件行号）：
改动文件（仅修复任务）：
测试与编译：
UTF-8/乱码/diff：
剩余风险：
```

## 需求

```text
<用户在这里补充：仅检查或同时修复、目标类别或全部六类、具体报错、是否允许运行 Unity EditMode 测试>
```
