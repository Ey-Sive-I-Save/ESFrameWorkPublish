# ES Framework Architecture Contract v0.1

状态：草案，未冻结。不修改源码、asmdef 或正式审计状态。
版本：0.1

本契约只建立骨架和裁决项，不作为已验收事实。

## 1. 稳定身份

- Authority：`ESConfigKey`、`ESKeyCatalog`、GameCore Catalog
- Allowed：EnumKey/StringKey 作为稳定配置身份；RuntimeKey 作为当前进程强类型表内运行索引；SchemaHash 作为跨进程/跨版本校验
- Forbidden：持久化 RuntimeKey；用裸字符串代替强类型身份；把 GUID 当业务身份
- Evidence：SourceOnly；部分静态测试存在；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：是否需要统一 StableIdentity 类型，收敛 Tag/Attribute/Input/Camera 各自 SchemaHash

## 2. 数据归属

- Authority：DataInfo、DataGroup、DataPack 和 GameCore 归属规则
- Allowed：Info 对应 Group；Pack 只在有明确聚合契约时使用；GameCore 只承载核心配置
- Forbidden：模块自建第二套内容聚合；GameCore 反向引用 Prefab/Scene/内容对象
- Evidence：P0 规则存在；部分 InfoGroup 测试源码存在；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：是否需要一份统一 DataInfo/Group/Pack 归属表

## 3. Module / Service / Profile

- Authority：Domain/Module/Service/Profile 边界规则
- Allowed：Domain 作为大边界；Module 作为能力点；Service 只做受控注入；Profile 必须满足 P0 Profile 契约
- Forbidden：把普通配置命名为 Profile；Domain 无限膨胀；Service Locator 任意注册
- Evidence：P0 与专项规则存在；部分测试存在；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：是否需要正式注册表约束新增 Domain/Module/Service/Profile

## 4. 所有权和生命周期

- Authority：Entity、Pool、Scope、Context 的所有权语义
- Allowed：Entity 拥有定义绑定；Pool 拥有租期；Scope 拥有资源生命周期；Context 只做局部可变上下文
- Forbidden：跨所有权释放；裸引用跨代；用 Context 替代 Tag/Stat/Permit/Resource Scope
- Evidence：专项规则和源码存在；部分测试存在；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：是否需要统一 Ownership 图，把现有 Pool/Scope/Lease 边界串成单一契约

## 5. 请求仲裁

- Authority：`Request -> Lease -> Active Set -> Arbitration -> Commit -> Executor`
- Allowed：多来源申请，集中裁决，单点执行；控制权由 LocalControl/受信 Bridge 收口
- Forbidden：模块自建第二套 Runner、第二套请求队列、第二套控制权
- Evidence：P0 协议和 Camera Director 首切片存在；部分测试存在；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：外部 Bridge（回放/观战/剧情）归属和提权边界

## 6. asmdef 边界

- Authority：真实 `.asmdef` references
- Allowed：Runtime 只依赖 Runtime；Editor 只依赖 Editor/Runtime；Tests 可依赖被测程序集
- Forbidden：Runtime 引用 Editor；Test/Developer 包成为正式 Runtime 无条件依赖；Player 发布闭包包含未裁决原型
- Evidence：asmdef 事实已盘点；门禁未实现；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：`ES_Logic -> ESFramework.AITest` 已建议采用修正后的方案 B：通用诊断契约入 `ES_Stand` 程序集内的 `ESFramework.Diagnostics.*` 命名空间，AITest 专用契约留在可选 AITest 包；asmdef 已拆分，Unity 编译待验证

## 7. 错误、取消和恢复

- Authority：统一 Run/Lease/Journal/Recovery 语义
- Allowed：结构化错误、取消边界、失效生成、持久化恢复证据
- Forbidden：用普通日志当恢复权威；取消后伪报成功；把不可回滚步骤伪装成原子回滚
- Evidence：局部语义存在；`ESOperationRun/Journal/Lock` 未实现；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：是否将 Publisher/Installer 的 ESOperationRun 作为框架级错误/恢复核心

## 8. 版本迁移

- Authority：SchemaHash + 迁移契约
- Allowed：稳定身份变化提升 SchemaHash；旧资产/存档/配置有明确迁移路径
- Forbidden：无影响面分析直接改 Schema；用生成工程或源码存在冒充迁移验收
- Evidence：局部迁移契约存在；框架级迁移协议未实现；Unity/PlayMode/Release NotCollected
- ContractVersion：0.1
- OpenDecision：是否需要统一 Migration Contract 模板和兼容性测试矩阵

## 当前边界

- 本文件只是 v0.1 骨架。
- 不因 Unity 证据缺失而阻塞草案，但证据仍必须标记 NotCollected。
- 不修改源码、asmdef、MODULE_AUDIT_STATE.md。
