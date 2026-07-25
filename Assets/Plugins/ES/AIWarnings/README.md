# ES AIWarnings 协作入口

本目录存放后续 AI 参与 ESFramework 时必须优先读取的高密度上下文。这里不是最终产品文档，改代码前仍然必须回读本地源码、确认当前路径、必要时编译验证。

## 必读最高警告

1. `项目最高警告/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md`
   - 适用：资源加载、AssetLibrary、AssetRegistry、RuntimeKey、Manifest、GameManager 启动资源流程。
   - 重点：`ESAssetLibrary` 只是编辑器资产组织工具；运行时不依赖 Library，只依赖烘焙后的 Manifest/Table；RuntimeKey 只在当前 Manifest/Table 内稳定，不是配置、存档或跨进程身份。

2. `项目最高警告/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md`
   - 适用：程序集流、编辑器自动注册、特性注册、资源扫描、ReloadDomain 后自动执行。
   - 重点：AssemblyStream 只做 Editor 元数据发现和注册解耦；禁止恢复 Runtime 程序集流；禁止在注册器里全量扫盘、扫资产、加载大资源。
   - 例外：`-SoEditorLoader.cs` 的 SO 编辑器索引、`ESEditorToolBar.cs` 的工具栏入口已标记为核心底层白名单，不要反复误判，但也不要照抄扩散。

3. `项目最高警告/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md`
   - 适用：任何编辑器初始化、域重载自动执行、编译后自动注册、`delayCall/update` 常驻逻辑。
   - 重点：普通工具和业务初始化不要随手用 `[InitializeOnLoad]`；优先使用 AssemblyStream 的 `EditorInvoker_*` / `EditorRegister_FOR_*`。

4. `项目最高警告/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md`
   - 适用：运行时核心热路径、Update/KCC/IK/StateMachine/AI/对象池等高频链路。
   - 重点：初始化阶段严格验证，热路径信任初始化结果；不要每帧用大量判空掩盖初始化错误。

5. `项目最高警告/项目最高警告_配置双键与Inspector分层_AI协作警告.md`
   - 适用：Buff、Tag、State、Skill、Item、Camera、Mode 等可配置运行对象。
   - 重点：配置层允许“枚举键 + 字符串键”，运行时热路径优先使用已烘焙、已缓存的强类型键。

6. `项目最高警告/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`
   - 适用：Skill、Buff、Actor、Item 的启动核心包、GameCoreTable、Consumer 收集与注入。
   - 重点：仅独立根定义 SO 实现 `IGameCoreSO`；Key、RuntimeData、Shared/Variable 嵌套数据严禁实现。

## 常用协作资料

- `InputRuntime/输入与交互入口_AI协作警告.md`
  - 输入运行时、改键、虚拟输入、RuntimeMode 对输入过滤、玩家输入与 AI 输入边界。

- `PlayerArchitecture/`
  - 玩家对象、Entity、Player facade、运动、KCC、控制源边界。

- `GameManager_SaveSystem/`
  - GameManager、Domain/Module、保存系统、静态高速入口。

- `CodexNotes/`
  - Codex 已参与过的工具重写、编辑器窗口、RuntimeWatch、程序集流稳定性等协作上下文。

- `AIPersonas与AI顶级目录边界_AI协作警告.md`
  - AIWarnings、AICommands、AITalk、AIPersonas 四类 AI 顶级目录边界；Persona 只改变口吻，不授权改代码，不覆盖项目规则。

## 当前强制结论

- 旧输入模块 `EntityAIInputSystemModule`、`EntityInputStateModule` 不再补兼容壳。旧场景/Prefab 的 Missing Type 必须清理序列化坏引用，不允许为了旧垃圾恢复类型。
- Unity 管理员权限警告不是项目代码问题，但开发时不建议用管理员权限启动 Unity。
- 看到乱码不要复制扩散，优先按 UTF-8 修复。
- 不要回滚无关工作区改动。
- 不要根据旧文档恢复已经废弃的系统。
- 任何全量扫描、全局初始化、运行时热路径改动，都必须先确认职责边界和性能影响。
