# Codex 工具重写历史上下文

`KnowledgeId`: `es.aiwarning.editor.codex-tool-rewrite-context.v1`  
`Authority`: `AIWarnings historical context + current SimpleTools source`  
`RouteKeys`: `aiwarnings`, `editor`, `simpletools`, `codex`, `runtimewatch`, `objectpool`, `validation`, `history`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `7ff71775887317e7ecec36fd76e2520f6bd1a7c1ee36e249b4986b9f508b9abe`  
`SourceSetHash`: `7ff71775887317e7ecec36fd76e2520f6bd1a7c1ee36e249b4986b9f508b9abe`  
`EntryBodyHash`: `5a19aea4dc3c5e891422438887edbee01270def89a76cd3842ffe6a87ae2f9db`  
`StaleWhen`: `SimpleTools 源码、菜单/程序集边界、工具安全合同或验证证据变化。`

## 保真迁移

原历史上下文 790 行、50,133 UTF-8 字节；现 Warning 保留 historical 身份、工具职责范围、核心安全边界、RuntimeWatch/ObjectPool 关键门禁和未验证声明。详细入口、阶段记录、性能复盘、风险纠偏和人工验收清单迁移至本条目。历史内容不等于当前实现或交付承诺。

## 工具职责与安全边界

- 目标是 ES Editor SimpleTools 的商业级验证与小工具重写，不重定义玩家运行时架构。修改前检查脏工作树、菜单、asmdef 和源码；PowerShell/项目文本按严格 UTF-8 处理。
- 资产写入必须预览、确认、Undo/可恢复、Dirty/保存、失败摘要和路径边界；疑似未使用资源只能进入隔离区，不能永久删除。批处理必须报告成功、失败和跳过，UI 文案不得超过真实行为。
- 扫描和执行口径必须一致：未激活场景从 root 递归收集；ESSO 类型走 SOS 正确入口；页面打开、切页、搜索和重绘不得隐式扫描或把空缓存误报为无配置。

## RuntimeWatch 与 ObjectPool 门禁

RuntimeWatch 通过 `ESRuntimeWatchRegistry` 和宿主链路按需解析注册字段，普通 Mono、Domain、Module 与有限嵌套对象均可纳入；仅当前台聚焦页面自动采样，采样列表与渲染快照分离，禁止全场景递归反射。ObjectPool 页面只负责运行时统计、PrefabPrewarmDataInfo 审计、GameManager 接入和 PlayMode 池组状态；配置关系写入需确认/Undo/场景脏标记，诊断页只读，选中 Prefab 不等于已接入池化体系。

## 当前证据边界

已有 ES_Editor 局部编译、统一入口、焦点采样和 Layout/Repaint 快照的静态记录；完整依赖构建可能受其他脏文件影响。Unity 视觉、窄窗口、连续布局、Editor GC、PlayMode 和发布行为仍未由本条目证明，不能把局部静态通过升级为项目级验收。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/简单工具（SimpleTools）/Codex_工具重写_商业级验证协作上下文.md`
- `ProjectSettings/ProjectVersion.txt`
- `Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/MenuItemPathDefine.cs`
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/SimpleToolsWindow.cs`
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/Simple_ESTool_Page_RuntimeWatch.cs`
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/Simple_ESTool_Page_ObjectPool.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/简单工具（SimpleTools）/Codex_工具重写_商业级验证协作上下文.md` (`255af45a5c8a7275636c6e273b253328859b7e63b1b1cdcbe22c44cae9a4b84f`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/MenuItemPathDefine.cs` (`d83e91ef8456727b554c854d59405492202f342d841a4b3dde265ef7c6c06560`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/SimpleToolsWindow.cs` (`cfb38064030ec11a322f084ab8ee701f0f1f1040f653af4ad4b2b51e3d706ddc`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/Simple_ESTool_Page_RuntimeWatch.cs` (`2b2f0d5cc0489fdf2a5446bead3ab077982afd0eb00940e6ab4e5bea1cc5a3e3`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/ESTools/Simple_ESTool_Page_ObjectPool.cs` (`b0a07c05ee57d23e2f2260642b45b53ad343a678ad02cb09964b078cbf0d31e9`)
