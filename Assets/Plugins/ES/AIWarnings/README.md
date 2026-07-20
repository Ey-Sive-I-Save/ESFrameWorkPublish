# ES AIWarnings 协作入口

本目录存放其他 AI 对 ES 框架、业务层和工具链的高密度理解与注意事项。它的用途是帮助后续 AI 快速建立上下文，减少重复误改。

这些文档不是最终产品文档，也不是绝对事实。改代码前必须回读本地源码、确认当前路径、必要时编译验证。

## 建议阅读顺序

1. `CodexNotes/Codex_工具重写_商业级验证协作上下文.md`
   - 适用：编辑器工具重写、商业级验证、小工具加固。
   - 重点：职责边界、插件结构、asmdef、菜单入口、脏工作区和验证优先级。

2. `CodexNotes/编辑器窗口迁移_ESMenuTreeWindowAB适配_AI协作警告.md`
   - 适用：普通 EditorWindow/OdinEditorWindow 迁移、ESMenuTreeWindowAB 适配、TrackView 临时弹窗统一外壳。
   - 重点：哪些窗口已迁移，哪些临时弹窗可以迁，TrackView/GraphView/弹出菜单不要硬迁，迁移后必须保留关闭保存逻辑。

3. `CodexNotes/SO表格工具_AI协作说明.md`
   - 适用：SO Table、命名空间迁移、表格导入导出、批处理计划。
   - 重点：高风险写入、计划只读、Group/Info 规则、CSV/XLSX 格式事实。

4. `InputRuntime/输入与交互入口_AI协作警告.md`
   - 适用：输入运行时、改键、虚拟输入、RuntimeMode 对输入的过滤。
   - 重点：`ESInputConfig`、`ESInputBindingProfile`、`ESInputRuntime`、`ESInputService` 的职责边界。

5. `GameManager_SaveSystem/架构体系_ESGameManager_SaveSystem_AI协作警告.md`
   - 适用：GameManager、三域结构、保存系统、模块 Inspector。
   - 重点：当前唯一 GameManager 位置、不要恢复旧 `ESGameCore`、保存系统静态门面。

6. `通用架构理解_跨系统纠偏_AI协作警告.md`
   - 适用：跨系统架构修改前的总览，尤其是 Entity、Item、Input、ValueChange、Buff、StateMachine、AITalk。
   - 重点：不要用大外壳、大 Domain、大模块接管一切；配置、运行态、表现、桥接、高频路径必须分清。

7. `PlayerArchitecture/玩家对象模型重构_AI协作说明.md`
   - 适用：玩家对象模型、Entity 与 Player facade 的边界。
   - 重点：玩家运行时主要在 `Assets/Scripts/ESLogic`，不要只看 `Assets/Plugins/ES`。

8. `PlayerArchitecture/模型重构_插件依赖边界_AI协作说明.md`
   - 适用：玩家/角色模型重构时判断插件依赖边界。
   - 重点：KCC、InputSystem、FinalIK、Cinemachine、EasySave3、DOTween、Odin、Luban/MemoryPack/UniTask 等插件在角色体系中的可依赖层级。

9. `PlayerArchitecture/模型重构_今日修正_CoreDomain与AI域控制_AI协作警告.md`
   - 适用：玩家/角色/所有生命体模型重构时纠正过时理解。
   - 重点：Core/Domain 具备逻辑能力；外壳只管结构和桥接；控制来源优先落在 AI 域/模块；不要一开始新增大量脚本。

10. `玩家运动_PlayerMotion_AI协作说明.md`
   - 适用：玩家运动、KCC、交互物、攀爬/飞行/游泳/骑乘。
   - 重点：`EntityKCCData`、输入逐帧写入、`StateSupportFlags`、交互模块装配。

11. `AI协作职责_状态机与IK上层_Buff边界说明.md`
   - 适用：StateMachine、FinalIK Driver、BuffDomain 边界。
   - 重点：StateMachine 不直接操作 FinalIK solver，BuffDomain 当前不要误判为完整 Buff 系统。

## 当前关键判断

- `Assets/Plugins/ES` 是框架、插件工具和编辑器工具重点区域。
- 玩家、GameManager、保存系统、Entity 运行时主线大量位于 `Assets/Scripts/ESLogic`。
- `Assets/Plugins/ES/2_Feature/ESGameCore` 当前不应恢复；旧 `GlobalDomain / GameRunDomain / ES_GameCore` 相关内容若出现在资产索引里，优先视为历史残留，必须用源码核对。
- 当前工作区可能长期存在未提交改动。任何 AI 都不得回滚、清理或覆盖无关变更。
- 中文文件和源码必须按 UTF-8 处理。看到乱码不要复制扩散，先确认原文件编码。

## 使用规则

- 先读相关说明，再读对应源码。
- 说明和源码冲突时，以当前源码为准。
- 涉及 Unity 序列化、Odin、`.meta`、asmdef、生成文件、批量资产写入时，小步修改并保留可验证结果。
- 修改工具时优先验证编译、菜单入口、资产写入安全、Undo/脏标记、异常提示和域重载行为。
- 不要把某份 AI 说明当成全框架总纲。每份说明只对它声明的职责范围有效。

## 新增或更新说明的要求

新增文档文件名必须包含职责关键词，建议同时包含中文，例如：

```text
职责范围_模块名_AI协作说明.md
Codex_工具重写_商业级验证协作上下文.md
```

正文至少包含：

- 适用范围。
- 最后核对日期或上下文时间。
- 已验证路径。
- 不要做的误操作。
- 需要源码复核的可能过时点。

优先写可验证事实，少写泛泛建议。
