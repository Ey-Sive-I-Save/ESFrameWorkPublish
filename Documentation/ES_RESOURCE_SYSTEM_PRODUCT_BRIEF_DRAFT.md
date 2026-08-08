# ESFramework 资源系统：证据分级产品介绍草案

状态：宣传草案 / 待源码复验  
最后验证：2026-08-08  
适用源码入口：`Assets/Plugins/ES/Editor/ESResPipeline`、`Assets/Plugins/ES/0_Stand/_Res/Runtime`、`Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime`

本文用于整理可以对外表达的产品价值，不替代 AIWarnings、源码、Unity Console、Test Runner、Player 或真实发布证据。没有达到对应证据等级的能力，不得使用“稳定”“商业生产”“所见即所得”等结果性表述。

## 一句话定位

ESFramework 资源系统把 Unity 项目中的资产身份、编辑器 Catalog、资源计划、发布清单、运行时 Provider 和 Scope 生命周期拆成可检查的阶段，让资源从“项目里有文件”变成“知道它是谁、如何发布、由谁持有、失败后如何恢复”。

当前这句话描述的是系统设计和源码链路，不等同于所有平台已经完成运行验收。

## 现在可以宣传的价值

### 1. 每个资源阶段都有明确状态

资源工作流窗口不会因为“目录里存在文件”就显示成功，而是检查：

- 协议版本；
- Library、Catalog 和资产身份；
- 重复 Key 与依赖闭包；
- Bundle Manifest、Local Release 和索引关系；
- 文件大小与 SHA-256 完整性；
- 缺失、无效、部分成功和已通过状态。

用户能看到当前阻断点、影响范围和下一步动作，并可以复制阶段诊断。这样资源问题可以被定位和复现，而不是依赖“再试一次”。

证据等级：`source-present`。窗口的窄屏、高 DPI 和 Unity 实机交互仍需验收。

### 2. 运行模式不会静默偷换

系统明确区分：

- `EditorDirect`：编辑器直接寻址；
- `EditorSimulateBuild`：使用正式发布元数据进行编辑器模拟；
- `LocalBuild`：读取本地正式发布产物；
- `HotUpdate/Net`：读取远端清单并执行下载、校验和缓存流程。

错误模式不会自动改成另一个模式。缺少 LocalBuild Manifest、Player 仍配置 EditorDirect 或发布元数据不完整时，系统应明确失败并给出诊断。

证据等级：`source-present`。四模式 Unity PlayMode、Player 和远端发布证据尚未齐全。

### 3. 资源生命周期有明确所有权

运行时不把编辑器 Library 当成正式 Provider。正式寻址使用 Manifest/Table 和发布 Bundle Index；资源加载通过 Provider、AssetScope、ResourcePlan 和 Lease 维护所有权。

Provider 切换时，旧代 Scope、旧请求和迟到异步结果必须与新 Provider 隔离。Scope 释放遵守引用计数、延迟释放和安全点，避免一个业务流程误释放另一个流程持有的资源。

证据等级：`source-present`。Scope、Provider 重建、取消、迟到回调和 IL2CPP 仍需运行证据。

### 4. Catalog 异常可以被解释和恢复

EditorDirect 首次发现 Catalog 缺失、解析失败、协议过期、内容冲突或 ConfigKey 注入不完整时，编辑器可以明确询问：

- 继续运行：明确进入降级模式，ConfigKey/ConfigData 不伪装可用；
- 打开资源配置：退出 PlayMode 后打开配置资产；
- 烘焙并重试：退出 PlayMode，在 EditMode 执行 Bake，成功后重新进入 PlayMode。

恢复流程不在 PlayMode 内自动写资产，也不把 Bake 失败标记成成功。

证据等级：`source-present`。弹窗、Bake 生命周期、Domain Reload 和 PlayMode 操作仍待 Unity 实测。

## 推荐演示顺序

1. 打开资源工作流窗口，先展示当前阶段状态，而不是直接展示绿色成功。
2. 选择一个资源，确认其 Library、身份和业务 Key。
3. 展示 Catalog/Plan/Manifest/Release 的逐阶段检查。
4. 故意使用过期产物，展示系统明确显示“无效”及修复动作。
5. 在 EditorDirect 中触发 Catalog 恢复，展示“烘焙并重试”不会在 PlayMode 内写资产。
6. 打开运行时监视器，展示配置运行模式、会话有效模式、Provider 和 Scope 诊断。
7. 最后再展示 ESAssetRefer、ResourcePlan 和运行时加载路径。

演示重点是“问题可解释、边界可见、失败可恢复”，不是把所有状态都伪装成绿色。

## 当前禁止使用的宣传词

在完成对应证据前，不使用以下表述：

- “所见即所得”；
- “非程序员几分钟上手”；
- “Undo/自动保存保证数据安全”；
- “支持多人无冲突继续编辑”；
- “复杂技能维护成本显著降低”；
- “新增类型无需额外接入成本”；
- “场景加载可以随时取消”；
- “已具备商业生产准入”；
- “已完成全平台发布验收”。

可替换为：

- “提供编辑器预览和阶段诊断”；
- “提供面向资源身份和发布链路的工作流”；
- “源码已建立明确的 Provider、Scope 和恢复边界”；
- “支持继续推进 Unity Editor、Player 和发布验收”。

## 能力声明与证据台账

| 能力声明 | 必需证据 | 当前状态 | 当前允许口径 |
|---|---|---|---|
| 阶段诊断可解释 | 源码、窗口截图、错误恢复操作 | `source-present` | 已建立阶段诊断实现 |
| 运行模式不静默降级 | 四模式 Unity PlayMode 与错误配置矩阵 | `source-present` | 已建立显式模式边界 |
| Catalog 失败可恢复 | 缺失、损坏、过期、冲突、Bake 失败/取消实测 | `source-present` | 已建立恢复流程代码 |
| Scope/Provider 生命周期安全 | P1-P10、T1-T7、R1-R11 日志 | `source-present` | 已建立所有权与代际隔离设计 |
| 编辑器预览与运行结果一致 | 截图、ReloadDomain、PlayMode 对照 | `not-run` | 不宣传“所见即所得” |
| 数据编辑安全 | Undo/Redo、多目标、迁移、冲突和异常回滚 | `not-run` | 不宣传“数据绝对安全” |
| 新类型易扩展 | 一个新 Track/Clip 的完整垂直切片 | `not-run` | 不宣传“无需额外接入成本” |
| 商业生产可用 | L3 多平台、压力、升级和真实发布回滚 | `not-run` | 不宣传“已商业准入” |

## 高风险边界

### Single 场景取消

Unity 的 `SceneManager.LoadSceneAsync` 一旦启动，不能可靠中止。

- Additive 场景可以在完成后补偿卸载；
- Single 场景可能已经替换当前场景，不能安全回滚；
- 取消 Token 目前主要取消调用方等待，不代表底层 Unity 操作停止。

因此不能宣传“场景加载可随时取消”。必须经过 Unity PlayMode、Player 和切场景压力测试后，再决定是否增加迟到 Scene Handle 或调整 API 语义。

### 历史协议产物

旧 V1/V3 产物不能因为文件存在就被当成当前发布成功。清理旧产物必须是独立、显式、可确认的动作，不能成为 Bake 的隐式副作用。

### Legacy Graph/NodeRunner

GraphView/NodeRunner 当前不作为正式商业扩展能力宣传。稳定身份、Undo、迁移、运行时快照和新增类型垂直切片完成前，不能把它描述为稳定生产工具。

## 宣传升级门槛

| 等级 | 必须具备的证据 | 允许的口径 |
|---|---|---|
| L0 | 源码存在、严格 UTF-8、静态检查 | “已建立实现和设计边界” |
| L1 | Unity Editor 编译、ReloadDomain、EditMode/PlayMode | “编辑器内可运行” |
| L2 | Windows/IL2CPP、取消、Provider 重建、资源计划 | “具备测试服运行条件” |
| L3 | 多平台、Profiler、压力、升级回归、真实远端发布和回滚 | “可评估商业生产准入” |

只有 L3 完成后，才可以把“商业级资源系统”作为确定性结论使用。

## 当前最小验收包

宣传材料最终必须绑定以下产物：

- 窄屏、高 DPI、主题和中文字段截图；
- Unity Console 和 ReloadDomain 记录；
- Catalog 异常矩阵和用户操作结果；
- 四种运行模式的 PlayMode 结果；
- Provider 切换、取消、Scope 释放和迟到结果日志；
- Windows/IL2CPP Player 构建结果；
- Profiler 指标和压力测试报告；
- 发布文件大小、SHA-256、远端校验和回滚记录。

缺少任何必需行时，宣传材料应保持“待源码复验”或“Verifying”，不能提前升级为 Stable。
