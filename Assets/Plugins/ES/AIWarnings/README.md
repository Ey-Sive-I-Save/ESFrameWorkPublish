# ES AIWarnings 协作入口

本目录存放后续 AI 参与 ESFramework 时必须优先读取的高密度上下文。这里不是最终产品文档，改代码前仍然必须回读本地源码、确认当前路径、必要时编译验证。

## 必读最高警告

1. `项目最高警告/项目最高警告_P0_UTF8唯一编码_禁止AI默认代码页覆写与机械转码_AI协作警告.md`
   - 适用：所有源码、配置、文档及其他文本文件的读取、修改、生成和批处理。
   - 重点：全项目唯一使用 UTF-8；禁止 PowerShell 默认代码页覆写和未经验证的机械转码；修改后必须严格解码、乱码扫描并检查 Diff。

2. `项目最高警告/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md`
   - 适用：资源加载、AssetLibrary、AssetRegistry、RuntimeKey、Manifest、GameManager 启动资源流程。
   - 重点：`ESAssetLibrary` 只是编辑器资产组织工具；运行时不依赖 Library，只依赖烘焙后的 Manifest/Table；RuntimeKey 禁止进入 Library、Catalog、Manifest、JSON 或 ConfigKey，只是当前进程、当前强类型表生命周期内自动生成的临时索引。

3. `项目最高警告/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md`
   - 适用：程序集流、编辑器自动注册、特性注册、资源扫描、ReloadDomain 后自动执行。
   - 重点：AssemblyStream 只做 Editor 元数据发现和注册解耦；禁止恢复 Runtime 程序集流；禁止在注册器里全量扫盘、扫资产、加载大资源。
   - 例外：`-SoEditorLoader.cs` 的 SO 编辑器索引、`ESEditorToolBar.cs` 的工具栏入口已标记为核心底层白名单，不要反复误判，但也不要照抄扩散。

4. `项目最高警告/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md`
   - 适用：任何编辑器初始化、域重载自动执行、编译后自动注册、`delayCall/update` 常驻逻辑。
   - 重点：普通工具和业务初始化不要随手用 `[InitializeOnLoad]`；优先使用 AssemblyStream 的 `EditorInvoker_*` / `EditorRegister_FOR_*`。

5. `项目最高警告/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md`
   - 适用：运行时核心热路径、Update/KCC/IK/StateMachine/AI/对象池等高频链路。
   - 重点：初始化阶段严格验证，热路径信任初始化结果；不要每帧用大量判空掩盖初始化错误。

6. `项目最高警告/项目最高警告_配置双键与Inspector分层_AI协作警告.md`
   - 适用：Buff、Tag、State、Skill、Item、Camera、Mode 等可配置运行对象。
   - 重点：配置层允许“枚举键 + 字符串键”，运行时热路径优先使用已烘焙、已缓存的强类型键。

7. `项目最高警告/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md`
   - 适用：所有跨配置、存档、网络、版本、DLC、Mod 或外部数据的业务 Key。
   - 重点：稳定身份只用 EnumKey/StringKey；Catalog 必须确定性构建并产出 SchemaHash；RuntimeKey 仅当前进程、当前 Catalog 生命周期内权威，严禁持久化、联网或跨表解释。

8. `项目最高警告/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`
   - 适用：Skill、Buff、Actor、Item 的启动核心包、GameCoreTable、Consumer 收集与注入。
   - 重点：仅独立根定义 SO 实现 `IGameCoreSO`；Key、RuntimeData、Shared/Variable 嵌套数据严禁实现。
   - P0：`SoDataInfo.KeyName` 只用于数据组字典、策划命名、SO 表格与编辑器定位；禁止作为 ConfigKey fallback、RuntimeKey、运行时查表、存档或网络身份。

9. `项目最高警告/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md`
   - 适用：GameCore RuntimeData、强类型 Table、动态注入、根 SO 注入、Consumer 重载、Clear/Remove 与资源安全点。
   - 重点：RuntimeData 按业务 Key 稳定驻留；所有准备逻辑必须在事务 try 内；成功用 `CommitRetained/TryCommitRetained`，失败用 `AbandonRetained`；Table 先写实际 RuntimeKey 再置 `Ready=true`，清理时释放重量级载荷但保留稳定外壳。

10. `项目最高警告/项目最高警告_IL2CPP工具链注册_禁止以编译器文件存在代替Unity可检测_AI协作警告.md`
   - 适用：Windows IL2CPP、HybridCLR、AOT 生成、Consumer 发布和 Player 构建。
   - 重点：`cl.exe` 存在不等于 Unity 可用；必须通过带 C++ 组件要求的 `vswhere` 验证 Visual Studio 实例注册，检测失败先修复 Installer，禁止降级 Mono 或跳过 HybridCLR。

## 常用协作资料

- `InputRuntime/输入与交互入口_AI协作警告.md`
  - 输入运行时、改键、虚拟输入、RuntimeMode 对输入过滤、玩家输入与 AI 输入边界。

- `PlayerArchitecture/`
  - 玩家对象、Entity、Player facade、运动、KCC、控制源边界。

- `GameManager_SaveSystem/`
  - GameManager、Domain/Module、保存系统、静态高速入口。

- `CodexNotes/`
  - Codex 已参与过的工具重写、编辑器窗口、RuntimeWatch、程序集流稳定性等协作上下文。
  - `P2_编辑器菜单根必须使用【ES】_AI协作警告.md`：所有 `MenuItem`、`CreateAssetMenu`、`AddComponentMenu` 必须统一归入 `【ES】/`，并且只能使用规定的十个一级分类；`常用窗口`必须独立存在；禁止 `ES/`、`Window/ES/`、`Tools/ES/` 和实现名一级目录。

- `AIPersonas与AI顶级目录边界_AI协作警告.md`
  - AIWarnings、AICommands、AITalk、AIPersonas 四类 AI 顶级目录边界；Persona 只改变口吻，不授权改代码，不覆盖项目规则。

## 当前强制结论

- 旧输入模块 `EntityAIInputSystemModule`、`EntityInputStateModule` 不再补兼容壳。旧场景/Prefab 的 Missing Type 必须清理序列化坏引用，不允许为了旧垃圾恢复类型。
- Unity 管理员权限警告不是项目代码问题，但开发时不建议用管理员权限启动 Unity。
- 看到乱码不要复制扩散，优先按 UTF-8 修复。
- 不要回滚无关工作区改动。
- 不要根据旧文档恢复已经废弃的系统。
- 任何全量扫描、全局初始化、运行时热路径改动，都必须先确认职责边界和性能影响。
- ES 自有 Unity 菜单根统一为 `【ES】/`；不得新增 `ES/` 或 `Window/ES/` 等旧入口。
