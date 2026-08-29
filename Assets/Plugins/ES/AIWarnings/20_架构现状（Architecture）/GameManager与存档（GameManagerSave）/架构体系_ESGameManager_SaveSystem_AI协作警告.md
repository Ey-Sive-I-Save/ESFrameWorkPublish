# ESGameManager / SaveSystem / Module Inspector 边界

Status: current
StableId: es.aiwarning.esgamemanager-savesystem-boundary.v1
Authority: AIWarnings（当前 GameManager/SaveSystem 约束）；详细事实见 Knowledge
RouteKeys: aiwarnings, architecture, gamemanager, domain, save, load, module, inspector, command
Applicability: ESGameManager、三域、ESGameSave 门面、模块 Inspector、Command 目录
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-esgamemanager-savesystem-boundary.md
StaleWhen: ESGameManager 三域、ESGameSave 门面、SaveSystem、模块 Drawer、Command 目录或 SourceRef 哈希变化。

## 长期约束

- 唯一 GameManager 是 `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`；只保留 `ESSystemDomain`、`ESFlowDomain`、`ESWorldDomain`，禁止恢复旧四域、GlobalDomain/GameRunDomain 或 ES_GameCore 中转程序集。
- 系统域提供稳定能力，流程域决定阶段/开关，世界域描述世界实例；ESGameSave/Input 属系统域，ESCommand 属流程域，RuntimeMode 是 GameManager 流程状态服务。Feature 不拥有全局生命周期或主流程。
- SaveSystem 公开入口是 `ESGameSave.Set/Get/Save/Load/Has/Delete/Info`；查询类操作不得创建模块，只有 Set/Load 明确 EnsureModule。禁止恢复 `ESGameManager.SaveModule` 或 `GetModuleFast<T>`。
- Set/Get 使用内存 Json 快照，Save/Load 使用磁盘；写盘保持临时文件、写后校验、备份、替换和失败处理。Easy Save 3 只是底层能力，不得散写 ES3 key。
- GameManager 主 Inspector 只显示轻量模块摘要和弹窗入口，不递归绘制大模块或反射扫描字段；完整 Odin 编辑走独立弹窗。Mono 文件名必须与类名一致，Command 主干保持 `Assets/Scripts/ESLogic/Runtime/Command/`。
- 所有中文文本保持严格 UTF-8；静态编译、旧结构搜索和文档验证不得冒充 Unity、运行时或发布验收。

## Knowledge 导航

详细三域归属、Save/Load 创建语义、未来 Link 分层、Inspector 绘制和 Command 文件约束见 `es.aiwarning.esgamemanager-savesystem-boundary.v1`。本 Warning 不授权恢复旧架构、源码、Git、运行时或发布改造。
