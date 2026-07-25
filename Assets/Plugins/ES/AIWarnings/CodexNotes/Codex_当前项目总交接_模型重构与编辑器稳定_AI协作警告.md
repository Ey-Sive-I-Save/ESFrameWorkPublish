# Codex 当前项目总交接：模型重构与编辑器稳定 AI 协作警告

更新时间：2026-07-22  
职责定位：Codex 当前主要负责“玩家/生命体模型重构思路、资产包分离编辑器、编辑器预览底层、AICommands/AIWarnings 协作入口、编辑器生命周期与内存稳定性”。

## 0. 先读结论

1. 本项目不是小工具集合，核心目标是把 ESFramework 推到可长期协作、可商业化维护的 Unity 框架。
2. 不要只看单个脚本能不能跑，必须看：配置入口、生命周期、ReloadDomain、资源链路、预览清理、可回退、可复用。
3. 不要随意新增大量脚本。优先复用项目已有框架：ESMenuTreeWindowAB、ESSO/SOS、GameManager Module、EditorInvoker_Level、ESPreview 底层、AICommands、AIWarnings。
4. 写代码前必须先确认周边文件。项目工作区经常很脏，不要回滚别人改动。
5. 中文文件和中文路径很多，禁止用会破坏编码的写法批量重写文件。

## 1. 已明确过时的理解

- [过时] “小格子每个动画都实时 PreviewRenderUtility 播放”
  - 实测不稳定且性能差。资产包小格子动画预览应使用缓存帧，按可见页队列生成，长期复用。

- [过时] “AnimatorController 临时生成用于动画预览”
  - 已废弃。临时 AnimatorController 曾触发 UnityEditor.Graphs.Edge.WakeUp 空引用异常。不要再用临时 Controller 做编辑器动作预览。

- [过时] “重复导出自动生成唯一文件名就行”
  - 当前协议是默认不重复导出。必须建立源 GUID -> 目标路径的导出链路，防止资源污染和不可回退。

- [过时] “导出后只看目标文件是否存在”
  - 错。有效导出要看源 GUID -> 目标路径链路，目标文件存在且链路有效才算已导出。

- [过时] “Core 和 Domain 不具备逻辑能力，只能做壳”
  - 项目没有强制这个规则。Core/Domain 可以有逻辑，但要保持边界清晰。控制来源建议由 AI/状态/输入等域生成控制请求，实际执行可以进入模块或 Domain，不要把所有控制判断堆在外壳。

- [过时] “Transform 天然昂贵，应该立刻做 VirtualTransform”
  - 不成立。Transform 本身不是必然瓶颈，真正昂贵的是高频查找、深层级频繁遍历、跨系统重复同步、每帧大量 SetParent/Instantiate/Destroy。VirtualTransform 只在海量非 GameObject 逻辑对象或网络/预测同步层需要时再做。

## 2. 玩家/生命体模型重构共识

目标不是只做玩家，而是所有生命体兼容：玩家、NPC、怪物、召唤物、载具驾驶体、可切换角色、剧情控制对象、联网代理对象。

推荐层级思路：

1. Root/EntityRoot
   - 负责对象身份、生命周期、模块挂载、总开关、池化回收入口。

2. Logic/Core/Domain
   - 负责实体能力域：属性、战斗、状态、技能、交互、阵营、目标、Buff、移动意图。
   - 可以有逻辑能力，但不要直接塞输入设备读取和具体表现细节。

3. Control Request
   - 统一成“控制请求”更稳。来源可以是玩家输入、AI、剧情、网络同步、回放、调试工具。
   - 用户倾向：不要强行把“判断谁能控制”做成一堆外壳脚本；更适合写入 AI 域/状态域/模块内部，由模块处理控制效果来源。

4. View/Model/Animator/VFX/Audio
   - 表现层可替换。角色切换、换皮、LOD、剧情镜头都依赖这个边界。

5. Sensors/Interaction/Target
   - 目标搜索、交互检测、命中/受击代理不要混进输入或动画。

商业级要求：

1. 支持 MMO：网络权威、预测/回滚、远端表现、低频同步和本地高频表现分离。
2. 支持开放世界：流式加载、远近 LOD、对象池、远处低频 Tick。
3. 支持角色切换：控制源和被控实体必须解耦。
4. 支持剧情：剧情控制源可以临时接管控制请求，但不能破坏实体模块。
5. 支持 RPG 战斗：Buff、状态、技能、属性、命中结算、表现播放要分层。
6. 支持 MOBA/FPS：输入响应、技能前后摇、镜头/准星/命中检测需要独立扩展，不要绑死第三人称 RPG。

## 3. 资源分离与资产包烘焙窗口

模块定位：

- 独立编辑器窗口，和 SODataWindow 同级。
- 每个资源包拥有持久烘焙数据，建议继承 ESSO，方便走 SOS 查询。
- 每个烘焙配置可命名，有目标源文件夹、导出目标文件夹、分类规则、默认分类文件夹名、预览模型、Avatar、URP fallback 材质等配置。

核心链路：

1. 选择或新建 BakeData。
2. 指定资源包源文件夹。
3. 扫描资产并分类。
4. 预览和筛选实际使用资产。
5. 导出前通报依赖文件。
6. 复制到目标目录。
7. 按类型划分 Material/Texture/Prefab/Model/Animation/Audio/Shader/Font/Video 等文件夹。
8. 导出文件名前缀使用 `ES选用_`。
9. 写入源 GUID -> 目标路径链路。
10. 后续打开显示已选中/已导出状态，除非目标链接被删除。

必须补齐或保持的点：

1. 导出文件夹必须完整定义，不要只靠用户临时选择。
2. 导出前必须展示依赖摘要，尤其 Prefab/Model/Material/Texture/Animation 的依赖。
3. 重复导出默认不支持，除非用户明确要求重建或覆盖。
4. 完成链建议用可序列化 SO 保存，Odin 支持 Dictionary 时可直接用字典。
5. 分类系统建议枚举化，动画分类可以按名称近似匹配：Idle、Walk、Run、Jog、Jump、Fall、Land、Attack、Skill、Hit、Death、Dodge、Turn、RootMotion、Interact、Emote 等。

## 4. 编辑器预览系统

当前结论：

1. 大预览窗口：实时渲染，追求高清、自由视角、60 FPS 以上体验。
2. 小格子预览：缓存帧，不再每格实时播放，防止卡死和状态串扰。
3. 小格子帧缓存放项目外，不放 Assets/。按资源名/视角/preview_01 组织，便于复用和清理。
4. 小格子队列必须把当前可见页放最前面，用户切页后优先生成当前页。
5. 视角至少支持正面、侧面、背面，重建按钮要能按视角重建。

动画预览踩坑：

1. Humanoid 动画不能只看曲线路径匹配。Clip 可能只有 RootT/RootQ 和肌肉曲线，模型 Avatar 不匹配时会只位移不摆身体。
2. 临时 AnimatorController 风险大，已废弃。
3. HumanPoseHandler 可用于诊断，但不能作为唯一商业级预览方案。
4. AnimationMode/采样/截图顺序非常敏感。截图前必须明确采样指定时刻。
5. 多个预览对象、多个 RT、多个采样驱动共享时容易出现 T Pose 交叉帧。不能让多个小格子抢同一个 Player/RT/采样状态。
6. 曾出现“一帧正常一帧 T Pose”的规律性问题，根源高概率是采样状态或渲染目标复用错误，而不是动画资源本身。

预览生命周期硬规则：

1. 预览对象必须 `HideFlags.HideAndDontSave`，不能污染场景。
2. 预览对象必须接入 ES 编辑器预览清理标记。
3. PreviewScene 和普通隐藏对象方案都踩过坑，后续使用必须走统一底层，不要各窗口重写一套 Camera/RT/Light/Model/Animator。
4. 大量预览对象时，可采用“每组对象 + 相机间隔 100M，Camera far clip 80”的隔离策略，避免互相影响。
5. 预览资源释放必须覆盖：窗口关闭、ReloadDomain 前、切换配置、切换模型、重建帧、异常中断。

## 5. 编辑器生命周期与内存稳定

全局硬规则：

1. 长生命周期事件必须用命名方法，不要用匿名函数。
2. 注册前优先 `-=` 再 `+=`。
3. OnDisable/Dispose/Reload 前必须解绑事件、释放 RT、DestroyImmediate 预览对象、Dispose PlayableGraph/PreviewRenderUtility。
4. 普通编辑器初始化优先使用 `EditorInvoker_Level*` 或项目既有注册器，不要随手 `[InitializeOnLoad]`。
5. `InitializeOnLoad` 只允许在明确必须、成本极低、不会全盘扫描、不会持有重对象时使用。

已处理过的稳定点：

1. `ESCmdAgentWindow`
   - 进程 Output/Error/Exited 回调已从匿名函数改为命名方法。
   - 增加 Process -> Tab 映射。
   - Stop 和自然退出都会解绑事件。

2. `ESEditorToolBar`
   - 顶部工具栏延迟选择页面从匿名 delayCall 改为命名排队。
   - 使用 `-=` 再 `+=` 防止重复排队。

3. `ESSODataInfoWindow.MenuExpansion`
   - 刷新后展开菜单从匿名捕获 `this + menuPath` 改为实例命名方法。

4. `AssetPackageBakeWindow`
   - 已有 `QueuePreviewRepaint -> RepaintAssetPackageWindow` 命名去重模式，后续照这个风格写。

仍需谨慎检查：

1. `ESInstaller.cs` 仍有若干匿名 delayCall，多数是安装器一次性流程，风险中低。
2. `RuntimeWatch.cs` 有匿名 delayCall，需要看上下文后再决定。
3. `Selection.selectionChanged +=` 目前多是命名方法，重点检查是否有完整 `-= `，不要为了消灭匿名函数乱改。
4. Odin 弹窗 `OnClose += () => ...` 多数是短生命周期，但如果捕获窗口/大对象，也要改命名解绑。

## 6. AssemblyStream 认知

AssemblyStream 不能做全项目重扫描。它应该只做 Editor 特性注册解耦，不能成为 ReloadDomain 内存暴涨源。

已知优化方向：

1. Editor 侧有效程序集包括 `ES_Editor`。
2. 反射取类型应使用安全方法处理 `ReflectionTypeLoadException`。
3. 排序必须真正赋值回列表，不能只调用 OrderBy 不使用结果。
4. Editor 下默认不要跑 Runtime AssemblyStream 全量扫描，除非有明确开关。
5. ReloadDomain 编译曾出现高内存占用问题，排查时优先看 AssemblyStream、InitializeOnLoad、全盘 AssetDatabase 扫描、静态缓存持有大对象。

## 7. AICommands 与 AIWarnings

AICommands 目标：

1. 不是写很多花哨提示词，而是给 AI 可复制执行的稳定流程。
2. 应按常用性和有效性分级。P0 必须服务游戏核心搭建，不要把特定工具体检伪装成 P0。
3. 每个命令要声明：命令类型、默认是否改文件、风险等级、必须读取路径、验证方式、缺参数时是否先问用户。

AIWarnings 目标：

1. 存长期有效事实、项目约束、踩坑纠偏。
2. 不存人设、情绪、临时吐槽。
3. 内容要可执行：路径、禁止事项、推荐流程、已废弃方案都要明确。
4. 后续 AI 先读最高警告，再读 CodexNotes，再读和任务相关的专项文件。

已删除：

1. `Assets/Plugins/ES/AIPersonas` 已按用户要求删除。不要再把人设系统作为项目协作主链路。

## 8. 插件和工具边界

用户已有或倾向：

1. Input System：已有或计划使用。
2. Cinemachine：已有。
3. KCC：已有。
4. Odin Inspector：已有。
5. Addressables：用户暂不想直接接入，倾向自写资源加载/分离管理工具。

协作判断：

1. 可以自写商业级资源加载工具，但必须具备清晰链路：引用索引、导出链、依赖分析、异步加载、缓存、卸载、池化、错误恢复、编辑器验证。
2. 不要因为不用 Addressables 就忽略资源生命周期。自写系统更需要强约束和工具链。
3. Odin 可用于编辑器配置和可视化，但运行时核心不要依赖 Odin。

## 9. 与核心系统的关系

ESInput：

- 输入只产生控制意图或请求，不应直接操纵所有角色细节。

ESCommand：

- 适合沉淀 AI 协作命令、编辑器命令、运行时调试命令。
- 新增命令必须有风险分级和验证路径。

GameManager：

- 适合作为全局模块入口，如资源模块、LOD 模块、运行时数据模块、对象池模块。
- 不要把单个实体的内部状态塞进 GameManager。

RuntimeMode：

- 用于过滤编辑器/运行时/调试/剧情/联网等模式差异。
- 输入、控制请求、调试命令都应尊重 RuntimeMode。

ValueChange：

- 适合属性变化、状态变化、UI/表现响应。
- 注意订阅解绑，避免闭包持有实体。

Link：

- 适合源 GUID -> 目标路径、配置 -> 资源、实体 -> 表现对象等长期链路。
- 资源导出不能只看文件存在，必须看 Link。

State：

- 状态是动作、技能、控制请求的重要组织层。
- 预览配置应包括 previewModel、previewAvatar、previewIdleClip 等，便于编辑器技能/动作预览。

Interaction：

- 交互不应和输入绑定死。AI、剧情、玩家输入都可能触发交互请求。

## 10. 后续 AI 不要犯的错

1. 不要没读 AIWarnings 就直接改。
2. 不要看到中文乱码还继续复制传播。
3. 不要用 PowerShell `Set-Content` 乱写中文文件。
4. 不要全盘扫描 Assets 当作普通 OnGUI 或 InitializeOnLoad 逻辑。
5. 不要把编辑器预览对象留在真实场景。
6. 不要创建临时 AnimatorController 做动画预览。
7. 不要让多个动画小格子共享同一个采样对象、RT 或驱动状态。
8. 不要把导出状态简化成“文件存在”。
9. 不要为统一架构新增一堆空接口，除非它们真的减少重复或承接明确模块职责。
10. 不要为了“商业级”堆复杂度。商业级首先是边界清楚、生命周期完整、工具链可验证、失败可恢复。

