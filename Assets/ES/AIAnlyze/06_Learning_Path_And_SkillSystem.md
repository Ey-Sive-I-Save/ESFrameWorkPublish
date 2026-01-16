# 框架学习顺序与技能系统开发路线（结合当前项目）

## 1. 推荐学习顺序（从下到上）

1. **基础语言与 Unity 引擎**  
   - 熟悉 C# 语法、委托/事件/泛型；
   - 掌握 Unity 场景、Prefab、基础组件、生命周期函数。

2. **ES 基础类型与约定**  
   - 阅读 `BaseDefine_ValueType` 与 `BaseDefine_Law`：
     - 如 `IDeepClone`, `ISharedAndVariable`, `ESTryResult`, `IEnumeratorTask` 等；
     - 理解“可更新对象/任务”的基本抽象。

3. **生命周期与托管体系（IESWithLife / IESHosting / IESModule）**  
   - 文件位置：
     - `IWithLife.cs`
     - `HostingPart_Define/IESHosting.cs`
     - `ModulePart_Define/IESModule.cs`
     - `ESHostMono.cs`
   - 目标：理解一个“Host 托管多个 Module”的运行模型，清楚每个接口的职责：
     - Host 负责整体 Update/Enable/Disable；
     - Module 负责自身逻辑与向 Host 注册/注销。

4. **Res 资源体系（ESResMaster / ESResLoader / ESResSource / ResLibrary）**  
   - 候选阅读顺序：
     1. `ResUse/ESResSource.cs`（单个资源源抽象）
     2. `ResUse/ESResLoader.cs`（Loader 的加载接口与队列添加接口）
     3. `Master/ESResMaster.cs` + `Master/Part/-ESRes_JsonData.cs`（总控与 Json 数据生成）
     4. `SoSupport/ResLibrary.cs`（Library/Book/Page 层次）
   - 目标：弄清楚“从 AssetKey -> AB -> 实例对象”的全链路。

5. **Link 消息体系**  
   - 关键文件：
     - `IReceiveLink.cs`
     - `Link-ActionSupport.cs`
     - `LinkReceiveList.cs` / `LinkFlagReceiveList.cs` / `LinkReceiveChannelList.cs` / `LinkReceiveChannelPool.cs` / `LinkReceivePool.cs`
   - 目标：掌握三种消息形态：简单 Link、Flag Link、Channel Link，以及它们的容器特性。

6. **Editor 工具与菜单系统（ESMenuTreeWindow / DevManagement 等）**  
   - 目的：理解如何用 Odin + 自定义框架搭建复杂的编辑器工具；
   - 虽然不直接影响运行时玩法，但对“如何组织大型工具 UI”很有参考价值。

7. **高层系统设计（技能、任务、Mod 等）**  
   - 在理解以上基础模块后，再阅读 AIAnlyze / AIPreview 中的设计文档与原型代码，理解如何把底层能力拼装成高层系统。

## 2. 技能系统开发路线（与框架结合）

1. **定义数据结构（ScriptableObject 层）**  
   - `SkillDefinition`：
     - Id, 名称, 图标, 描述；
     - 冷却时间、消耗、施法距离等基础属性；
     - 目标过滤规则（枚举 + 参数）；
     - 效果列表（引用若干 SkillEffect）。
   - `SkillEffect`：
     - 类型（伤害、治疗、状态、位移、召唤、脚本事件...）；
     - 数值、持续时间、叠加规则；
     - 触发时机（命中/结束/周期 Tick）。
   - 可选：使用 `ResLibrary/ResBook/ResPage` 将技能按职业/玩法分组管理。

2. **实现运行时执行器（SkillRunner）**  
   - 核心职责：
     - 校验施法条件（冷却、资源、距离、角度等）；
     - 按配置播放动画/特效/音效（可调用 Res 系统加载资源）；
     - 依次执行 SkillEffect。
   - 可以让 SkillRunner 实现 `IESModule`，挂在角色的 Hosting 上：
     - 利用 Host/Module 体系进行更新与生命周期管理；
     - 利用 `Signal_IsActiveAndEnable` 控制技能是否可用。

3. **事件与状态同步（Link 体系）**  
   - 使用 Link 发布技能相关事件：
     - `OnSkillCastStart (caster, skillId)`
     - `OnSkillHit (caster, target, skillId)`
     - `OnSkillFinish (caster, skillId)`
   - UI、任务、成就、日志系统可以订阅这些 Link：
     - UI：更新冷却条、显示提示；
     - 任务：统计击杀/技能使用次数；
     - 成就：触发“连续命中”这类成就条件。

4. **与 Res 系统的对接**  
   - 技能中所需的：图标、特效 Prefab、音效等均通过 Res 系统加载：
     - SkillDefinition 中只保存 ResKey / 逻辑名；
     - 运行时由 ESResMaster/ESResLoader 将其解析为具体资源。

5. **与 Mod 机制的结合**  
   - 允许 Mod 在自己的 ResLibrary 中新增 SkillDefinition 与 SkillEffect：
     - 主工程在启动时扫描所有启用 Mod 的技能定义并注册到全局表；
     - 角色/任务只用 SkillId，不关心来源是“本体还是 Mod”。

6. **渐进式开发建议**  
   - 先做最小可行版本：只有伤害/治疗两种 Effect，且只支持单体目标；
   - 再逐步扩展到：范围技能、状态类技能、复合效果；
   - 最后引入：脚本事件型效果，用于驱动复杂剧情或自定义逻辑。

## 3. 总结

- 学习路线建议：
  - 先吃透“生命周期 + 托管 + 资源 + 消息”四块基础；
  - 再看 Editor 工具和高层系统（技能/Mod/任务）如何复用这些基础。
- 技能系统的关键是：
  - 数据驱动、运行时执行器、消息分发三者分离；
  - 与已有的 ESRes / Link / Hosting 体系深度结合，而不是重新造一套平行系统。
