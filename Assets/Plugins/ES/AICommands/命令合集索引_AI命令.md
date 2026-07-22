# AI Commands 合集索引

本目录不是“越多越好”的命令堆，而是 ESFramework 给 AI 使用的项目命令库。使用方式：把下面任意一个 `Assets/Plugins/ES/AICommands/xxx.md` 路径复制给 AI，AI 必须先读取该文件全文，再按文件内规则执行。

优先级原则：P0 必须服务游戏核心搭建，不把某个工具窗口、某次故障、某个临时问题误升为 P0。工具维护类命令即使常用，也只能算 P1/P2。

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

命令类型：信息补全。
默认改文件：否，除非用户要求整理命令目录。
风险等级：L1。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/README.md
Assets/Plugins/ES/AIWarnings/通用架构理解_跨系统纠偏_AI协作警告.md
Assets/Plugins/ES/AIWarnings/GameCoreGlobalData与AICommands_AI协作警告.md
```

## 分级规则

```text
P0：游戏核心搭建命令。直接服务玩家、生命体、输入、运行模式、命令、模块生命周期、对象池等主干。
P1：项目维护高频命令。常用于排错和工具链维护，但不属于游戏核心主干。
P2：明确执行命令。允许小范围改文件，必须有具体目标、路径或报错。
P3：低频或偏专题命令。适合方案讨论、交接沉淀、特定工具或特定系统判断。

有效性：是否能稳定产出可验证结果。
常用性：是否在 ESFramework 游戏核心搭建中高频出现。
风险：是否可能误改代码、污染资产、扩大影响面。
```

## P0 游戏核心搭建

```text
Assets/Plugins/ES/AICommands/信息_项目总入口_快速融入_AI命令.md
Assets/Plugins/ES/AICommands/信息_玩家架构上下文_AI命令.md
Assets/Plugins/ES/AICommands/玩家对象模板_层级验证_AI命令.md
Assets/Plugins/ES/AICommands/角色控制请求链路_检查_AI命令.md
Assets/Plugins/ES/AICommands/方案_角色层级商业级模板_AI命令.md
Assets/Plugins/ES/AICommands/方案_玩家控制请求架构_AI命令.md
Assets/Plugins/ES/AICommands/信息_输入运行模式上下文_AI命令.md
Assets/Plugins/ES/AICommands/RuntimeMode输入过滤_检查_AI命令.md
Assets/Plugins/ES/AICommands/信息_ESCommand上下文_AI命令.md
Assets/Plugins/ES/AICommands/GameManager模块接入_检查_AI命令.md
Assets/Plugins/ES/AICommands/对象池预热配置_检查_AI命令.md
```

适用：搭建或评审游戏主干。包括玩家对象模板、所有生命体通用结构、角色切换、控制请求、输入过滤、运行模式、运行时命令、GameManager 模块生命周期、对象池预热。

## P1 项目维护高频

```text
Assets/Plugins/ES/AICommands/检查_编译错误定位_AI命令.md
Assets/Plugins/ES/AICommands/检查_脏工作树影响面_AI命令.md
Assets/Plugins/ES/AICommands/编译与ReloadDomain内存_检查_AI命令.md
Assets/Plugins/ES/AICommands/信息_资源治理上下文_AI命令.md
Assets/Plugins/ES/AICommands/检查_中文编码风险_AI命令.md
Assets/Plugins/ES/AICommands/检查_Obsolete误用_AI命令.md
Assets/Plugins/ES/AICommands/检查_程序集引用边界_AI命令.md
Assets/Plugins/ES/AICommands/检查_编辑器窗口ReloadDomain_AI命令.md
Assets/Plugins/ES/AICommands/检查_静态缓存清理_AI命令.md
Assets/Plugins/ES/AICommands/检查_动画Avatar预览失败_AI命令.md
Assets/Plugins/ES/AICommands/检查_资源导出重复链路_AI命令.md
Assets/Plugins/ES/AICommands/State动画预览配置_检查_AI命令.md
Assets/Plugins/ES/AICommands/检查_输入动作绑定缺失_AI命令.md
Assets/Plugins/ES/AICommands/检查_RuntimeMode阻断规则_AI命令.md
Assets/Plugins/ES/AICommands/检查_GameManager模块生命周期_AI命令.md
Assets/Plugins/ES/AICommands/检查_对象池运行时GC_AI命令.md
Assets/Plugins/ES/AICommands/OpTargetPack与Item运动_检查_AI命令.md
Assets/Plugins/ES/AICommands/检查_Item每发变量污染_AI命令.md
Assets/Plugins/ES/AICommands/资源依赖与未使用资产_分析_AI命令.md
```

适用：编译、内存、编辑器窗口、静态缓存、编码、程序集、资产依赖、动画预览等维护问题。重要，但不是游戏核心主干。

## P2 明确执行

```text
Assets/Plugins/ES/AICommands/执行_修复单个编译错误_AI命令.md
Assets/Plugins/ES/AICommands/执行_资产包预览小修复_AI命令.md
Assets/Plugins/ES/AICommands/执行_资产包导出链路小修复_AI命令.md
Assets/Plugins/ES/AICommands/新增输入动作_AI命令.md
Assets/Plugins/ES/AICommands/执行_新增输入动作_强约束_AI命令.md
Assets/Plugins/ES/AICommands/ESCommand新增运行时命令_AI命令.md
Assets/Plugins/ES/AICommands/执行_新增ESCommand运行时命令_强约束_AI命令.md
Assets/Plugins/ES/AICommands/新增GameTag_AI命令.md
Assets/Plugins/ES/AICommands/新增物理层语义_AI命令.md
Assets/Plugins/ES/AICommands/新增飞行物类型_AI命令.md
```

适用：已有明确目标，允许 AI 小范围改文件。不要把 P2 当成默认入口。

## P3 方案与沉淀

```text
Assets/Plugins/ES/AICommands/方案_资源分离工作流_AI命令.md
Assets/Plugins/ES/AICommands/方案_性能预算与0GC_AI命令.md
Assets/Plugins/ES/AICommands/AIWarnings更新_交接总结_AI命令.md
Assets/Plugins/ES/AICommands/执行_新增AIWarning交接_AI命令.md
```

适用：做架构讨论、跨系统判断、阶段总结、纠正过时理解。一般不用于快速修 bug。

## 选择建议

```text
1. 做游戏核心架构：优先用 P0。
2. 新 AI 介入：先用 信息_项目总入口_快速融入_AI命令.md，再按任务选 P0。
3. 有报错或工具故障：用 P1，不要误当成核心 P0。
4. 有资产包预览/导出问题：用 P1 资源治理或资产包体检命令。
5. 要 AI 动手改：只发 P2，并补充具体目标、路径、报错或期望行为。
```

## 不要这样用

```text
1. 不要一次发很多命令让 AI 自由发挥。
2. 不要让执行类命令在没有目标路径和验证方式时改代码。
3. 不要把方案命令当修复命令。
4. 不要让 AI 根据文件名猜任务，必须读取命令全文。
5. 不要绕过 AIWarnings，旧理解可能已经过时。
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：推荐使用哪个命令路径，以及原因。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：无需编译。
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
