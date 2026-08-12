# P0：统一内容注册唯一入口与事务边界

状态：现行强约束。

## 唯一作者态入口

普通资产、GameCore DataInfo、独立 GameCore Root、Consumer 同步和 Catalog/ReferenceGraph Bake 的请求合同统一为：

`ESContentRegistrationAuthoring.Execute(ESContentRegistrationRequest)`

人工窗口、Inspector、资源窗口、ConfigKey Drawer、MCP 与 C# 自动化只能组装同一请求并调用该入口。禁止直接追加 `ESAssetPage`、修改 Page Key、写 `ManualGameCoreAssets`、调用旧收集 API，或以 `AssetDatabase.SaveAssets()` 冒充事务提交。

`ESAssetLibrary.EditorOnly_DragAssetsToBooks` 与 `ESAssetBook.EditorOnly_DragAtArea` 对资源 Library 必须 fail-closed。旧 `CollectAssetToRecommendedLibrary(s)` 是编译期禁用入口，不得恢复兼容层。

## 提交合同

1. 先执行 `commit=false` 预检，再根据预检回执执行 `commit=true`。
2. 每次成功预检返回独立 `requestId`；commit 必须回传该同一 `requestId`，并携带预检返回的 GUID、LocalFileId 与目标 revision。相同语义的并行预检也不得共享或互相消费提交资格；一次真实提交尝试无论成功或 CAS 失败都会消费资格，重试必须重新预检。
3. Key 迁移还必须携带 `hasExpectedCurrentKey=true` 及预检返回的当前 Key，形成 CAS。
4. StringKey 按原值保存；禁止 Trim、大小写归一化、自动替换或静默生成。
5. 目标存在未保存编辑、revision 改变、身份不符、Key 冲突或 Bake 进行中时必须拒绝写入。
6. 提交只保存明确目标资产；失败必须恢复内存快照并尝试精确落盘回滚。

## GameCore 与普通资产分流

- GameCore ScriptableObject 禁止进入普通 AssetTable。
- DataInfo 接入正式 Group 与 Consumer 使用 `RegisterGameCore`。
- 不属于 DataInfo/Group 组织关系的正式 GameCore 根使用 `RegisterGameCoreRoot`。
- 尚未定义正式事务的移除、移动、复制、合并和批量清空操作必须禁用，不能退回直接写列表。
- GameCore 只保存类型化稳定资产引用；直接 Prefab/Object 字段仍按 GameCore P0 处理，注册成功不代表定义合规。

## 阶段隔离

注册、Bake、规划、构建、发布是独立阶段。注册成功不能声称已 Bake，Bake 入队不能声称完成，静态编译不能声称 PlayMode 可玩或发布可用。

Bake 读取作者源期间，本机所有正式注册提交必须冻结。Windows Mutex 只解决同机多 Editor/多进程竞争；跨机器协作仍依赖版本控制、revision/CAS 和合并后重新预检，不得宣称具备分布式锁。

## MCP 证据边界

Unity 中的 `HandleCommand`、`CommandRegistry` 与工具元数据存在，只能证明 Unity 适配器可用；不能证明 stdio/HTTP MCP Server 已把该工具暴露给客户端。

2026-08-11 已复现：`mcpforunityserver` 10.0.0、10.1.0、10.1.2 及 10.1.3 beta 在 FastMCP 3.x 下为带参数的动态自定义工具生成签名时，会因装饰器丢失 `__annotations__` 而报 `KeyError`（首个参数通常显示为 `'action'`）。这是通用 Server 动态注册缺陷，不得通过删除参数元数据、增加第二套启动 bootstrap、改名参数或绕过统一 Facade 来伪装修复。

正式 MCP 闭环必须同时满足：

1. MCP 客户端 `tools/list` 可见 `es_content_registration`，或项目作用域自定义工具资源能返回该精确工具定义；
2. 通过 MCP 客户端调用 `commit=false`，Unity 返回统一合同的 revision、GUID、LocalFileId 与稳定 Key；
3. 再按同一客户端完成一次带当前预检回执的 `commit=true` 与幂等重放；
4. Server 日志没有动态工具注册失败，且结果来自精确 Unity 实例。

在上游缺陷解除前，可报告 Unity Handler、C# API、人工窗口和静态/测试证据，但必须把 MCP 客户端层标为阻断，不能用 legacy TCP 直发命令冒充 MCP 客户端闭环。

## 稳定身份

主资产身份固定为 `GUID + LocalFileId(0)`；子资产必须保存真实 LocalFileId。不得用 raw importer FileId、路径、对象名或当前选中对象替代稳定身份。

GameCore ConfigKey 的编辑器字段必须明确区分定义与引用：定义字段使用 `ESConfigKeyUsage(Declaration)`，未标记字段按引用处理。引用字段从已有定义选择；定义字段在写回前检查同强类型域的 EnumKey/StringKey 占用。发现占用时必须阻止写入并让用户明确选择“定位占用资产、取消修改、使用未占用建议 Key”，禁止静默覆盖、自动合并或拖到 GameCore 重建阶段才首次报告。建议 Key 只能在用户明确确认后写入，且不得 Trim、改大小写或修改其他定义资产。

## 最低验收

- `ES_Stand`、`ES_Editor`、内容注册测试程序集与 MCP 适配程序集静态编译通过。
- Unity 完成导入与 ReloadDomain，目标 Console 无编译错误。
- EditMode 至少覆盖普通资产注册、Key 迁移 CAS、GameCore 注册、GameCore Root、Consumer 同步、MCP 合同、幂等与失败拒绝。
- MCP 发布可用性必须按“证据边界”完成真实客户端发现与调用；Unity Handler 单测不能替代该层。
- 多 Unity 实例下必须先锁定精确 `Name@hash`，不能把其他项目的 Console 或测试结果当作本项目证据。
