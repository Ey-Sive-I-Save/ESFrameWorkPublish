# 场景构建器权威、覆盖审计与项目内备份分层

**状态：现行约束；2026-08-10 已完成本次玩家控制器场景刷新与静态场景门禁。**

## 结论

测试场景的构建器产物是当前场景布局、标题、玩家出生点和验收导视的唯一权威。场景文件若明显落后于构建器，必须先判断是未重新生成还是明确保留的历史样板；不能把旧场景的布局当成当前功能事实，也不能手工在旧场景上继续叠加修补来代替重建。

本次已将 `Assets/Scenes/Tests/ESPlayerControllerTest.unity` 按官方玩家控制器构建流程刷新为“ES 玩家控制器 · 24 区综合验收场”，玩家出生点为 `(-24, 0.02, -2)`。正式验证仍须区分“已生成/已加载”和“PlayMode 已操作通过”。

## Prefab 与场景覆盖审计

- 角色主体是 3D KCC Entity；`ModelOffset`、表现节点和挂点不得出现与当前运动后端无关的 2D 物理组件。
- 场景实例上的组件、字段或引用若不是构建器/Prefab 基线明确生成的，必须视为可疑 override，先记录来源、影响和清理结果，再进入正式验证。
- 本次已移除玩家 `ModelOffset` 子节点上的 `AreaEffector2D`。该组件确认是场景 Prefab override，不属于当前场景构建器生成内容；清理后由 MCP 场景诊断确认 `totalIssues: 0`。
- 不得以“当前没有报错”推断覆盖安全；需要比较 Prefab 基线、构建器规则和场景实例序列化结果。

## 场景刷新门禁

每次刷新玩家/载具验收场景必须按以下顺序执行：

1. 刷新或确认正式角色 Variant 与载具 Prefab 的作者基线。
2. 在写入场景前保存回滚副本，并记录任务标识、源路径、时间和 SHA-256。
3. 通过官方场景构建器重新生成场景；禁止直接改场景标题、出生点或区域布局伪造构建结果。
4. 对玩家实例执行 Prefab override 审计，重点检查 `ModelOffset`、KCC、Collider、输入与挂载节点。
5. 运行场景静态诊断；只有 `totalIssues: 0` 才能进入 PlayMode 操作验收。
6. 报告中分别标注：构建器已运行、Unity 已导入、静态门禁、PlayMode、Profiler 和 Player 证据。

## 项目内备份分层

备份不得放在 `C:\Users\asus` 或其他项目外临时目录。项目内统一使用 `ES/Bak/<层>/<TaskKey>/`：

| 层 | 路径 | Git 策略 | 用途 |
|---|---|---|---|
| Local | `ES/Bak/Local/<TaskKey>/` | `.gitignore` 忽略 | 机器本地、短期回滚；不作为验收证据 |
| Reviewed | `ES/Bak/Reviewed/<TaskKey>/` | 默认纳入 Git | 用户要求保留、需要审阅或可复现的变更前基线 |

两层内容必须来自同一份变更前源文件；不得把变更后的文件冒充 before 备份。每个 Reviewed 目录必须带 `BACKUP_MANIFEST.md`，列出源路径、备份时间、原因、文件大小和 SHA-256。Local 副本可在任务完成且确认不再需要回滚后清理；清理时只允许删除对应 `TaskKey` 目录。

## 本次备份记录

- `ES/Bak/Local/20260810_PlayerControllerRefresh/`：本地回滚副本，已加入 `.gitignore`。
- `ES/Bak/Reviewed/20260810_PlayerControllerRefresh/`：可审阅的变更前基线，保留在项目 Git 范围内。
- 两份副本均包含 `ESPlayerControllerTest.unity`、`大黑塔.prefab`、`BlockCar.prefab`、`BlockBicycle.prefab`、`BlockHelicopter.prefab`。

## 禁止事项

- 禁止把项目外用户目录作为正式备份位置。
- 禁止用手工场景修改替代构建器重建。
- 禁止为了“修复”可疑覆盖而向角色 Prefab 添加无关物理组件。
- 禁止把静态场景诊断通过写成运行时移动、攀爬、骑乘或载具功能已验收。

## AI 高频误操作预防表

以下错误应在 AI 交付前主动排查，不等出现 Console 报错后再补救：

| 高频错误 | 预防规则 | 最小证据 |
|---|---|---|
| 只改生成场景，不改构建器/Prefab 作者源 | 先定位官方生成入口和正式 Variant；重新运行构建器后再验收 | 构建器入口、源资产路径、生成前后 diff |
| 看到旧布局就手工拖对象、改标题或出生点 | 先判断场景是否过期；过期场景必须重建，手工改动只能用于明确的场景私有配置 | 构建器运行记录、场景标题/出生点核对 |
| 在角色实例上添加“看起来有用”的组件 | 所有非基线组件先标为可疑 override；3D KCC 角色禁止混入 2D 物理或第二运动后端 | Prefab 对比、组件来源、清理结果 |
| 直接写玩家根 `Transform`、Motor 或载具 Rigidbody | 玩家根只由 Entity KCC 执行；载具根只由 VehicleController 执行；状态/输入通过既有模块提交请求 | 调用链检查、PlayMode 运行证据 |
| 新建第二套输入/控制器绕开 `ESInputService` 与 AI Domain | 输入必须走 `Input System → ESInputService → EntityPlayerInputWriteModule → EntityAIDomain` | 真实绑定、运行态输入诊断 |
| 把 DataInfo 字段存在误报成运行时能力已启用 | 只有明确消费者接线并读取最终解析值，才能报告能力生效 | BindDefinition/Permit/Attribute 链、运行态值 |
| MCP 场景 clean 或静态编译通过就宣称可玩 | 必须分层报告 Unity 导入、静态门禁、PlayMode、Profiler、Player；缺失项写 `待验收` | 对应层级的原始输出 |
| 只修当前场景，不清理旧序列化引用/Prefab override | 修复后重新加载场景和 Prefab，检查 missing script、旧类型名、无效引用与 override 数量 | Unity 重载结果、序列化搜索、场景诊断 |
| 用默认代码页、脚本批量转码或 PowerShell 破坏中文 YAML | 所有文本保持 UTF-8；修改后检查乱码、行尾和 YAML 结构 | 编码检查、`git diff --check` |
| 把备份放到用户目录，或误以为 ignored 文件已经被 Git 记录 | 备份只放 `ES/Bak/Local` 或 `ES/Bak/Reviewed`；分别用 `git check-ignore` 和 `git status` 验证 | 规则命中输出、备份 manifest |
| 为验证方便污染正式角色/载具/相机 Prefab | 测试导视和断言只放测试场景的 `ESSceneValidationGuide`；禁止一次性 OnGUI 和测试 MonoBehaviour 渗透正式资产 | Prefab diff、Guide 组件位置 |
| 覆盖用户已有 dirty changes 或顺手清理无关文件 | 修改前记录工作树；只提交任务范围；不使用 reset/checkout 覆盖他人改动 | 修改前后 `git status`、路径清单 |

## PlayMode 生命周期安全门禁

PlayMode 是运行态实验，不是普通编辑态。任何会改变场景、Prefab、脚本、构建器、输入资产、序列化数据或项目设置的操作，开始前必须确认 Unity 已退出 PlayMode，并等待编译/导入完成。

### 必须先退出 PlayMode 的操作

- 修改场景构建器、正式角色/载具 Prefab、场景序列化内容或测试导视配置。
- 修改输入 Action、DataInfo、Attribute/Permit、KCC 参数或车辆后端配置。
- 执行批量生成、Prefab Apply/Revert、资源迁移、脚本重命名、程序集/asmdef 变更。
- 运行可能写入资产、清理对象、刷新场景或切换资源 Scope 的 MCP/Editor 自动化命令。
- 进行任何不可逆或难以回滚的删除、覆盖、批量替换和发布准备操作。

### PlayMode 中只允许的动作

PlayMode 中只做只读观察、输入操作、运行态诊断和明确的临时实验；禁止把运行时实例修改当成资产修复。需要改变配置时，先记录运行证据，退出 PlayMode，再回到权威源修改并重新生成/导入。

### 进入修改前的状态确认

```text
[ ] Unity PlayMode 已停止，Play 按钮未高亮。
[ ] Console 没有 Compilation Error，AssetDatabase/脚本导入已完成。
[ ] MCP/Editor 自动化没有仍在运行的写入任务。
[ ] 已记录当前场景、Prefab 和工作树状态，必要时已有 ES/Bak before 备份。
```

如果无法确认退出状态，默认按“仍在 PlayMode 或仍有写入任务”处理，不执行高危修改。

## AI 交付前五分钟检查表

```text
[ ] 权威源已定位：构建器、Prefab、DataInfo、输入绑定分别是谁？
[ ] 场景是否由当前构建器生成，而不是旧场景手工修补？
[ ] 玩家/载具实例是否存在非基线组件、Missing Script 或旧引用？
[ ] 是否仍沿用 Input System/ESInputService/Entity/KCC/VehicleController 唯一链路？
[ ] 是否有项目内 before 备份、TaskKey、SHA-256 与 Git 过滤验证？
[ ] 是否已退出 PlayMode，并确认没有编译、导入或 MCP 写入任务？
[ ] 报告是否区分静态、Unity 导入、PlayMode、Profiler、Player 证据？
[ ] 是否检查并保留用户已有 dirty changes？
```
