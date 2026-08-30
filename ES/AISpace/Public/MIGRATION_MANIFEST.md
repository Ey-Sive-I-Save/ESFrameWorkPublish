# AISpace 全量内容分类与迁移清单

扫描范围：`ES`、`Assets`、`Documentation`、`.agents`、`Tools`（排除 `Library`、`Temp`、`Logs`）。
分类顺序固定为：**分类 → 日期（YYYYMMDD）→ 所有者/主题**。生命周期为
`temporary`、`stable`、`archived`；稳定内容不因日期而被清理。

## 归属矩阵

| 内容类别 | AISpace 落点 | 生命周期 | 外部权威是否保留 |
|---|---|---|---|
| Screenshots / Captures | `ES/AISpace/Local/Screenshots/<YYYYMMDD>/<owner>/` | temporary 或 stable | 正式测试证据仍在 `ES/UIEvidence` 或测试证据目录 |
| Cache / Scratch / Exports | `ES/AISpace/Local/<category>/<YYYYMMDD>/<owner>/` | temporary | 不进入 Unity 包 |
| EditorTooling / UIIntent / StaticReplay | `ES/AISpace/Local/<category>/<YYYYMMDD>/<owner>/` | temporary 或 stable | 源码与正式回执留在各自权威目录 |
| AgentCandidates / WebPageStudio / ABC 审计资源 | `ES/AISpace/Public/<category>/<YYYYMMDD>/<topic>/` | stable 或 archived | Automation 合同、RunRecord 与正式证据保持原位 |
| Skills 关系与注册索引 | `ES/AISpace/Public/Skills/<YYYYMMDD>/` | retained-index | Skill 本体保持 `.agents/skills` |
| 必须被 Unity 导入的 AISpace 公共资产 | `Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/` | stable | Unity `.meta` 与引用保持配对 |

## 外部权威边界

`Documentation/AIKnowledge`、`ES/Automation`、`ES/AI协作历程（Codex）`、`.agents/skills`、
`Assets/Plugins/ES/AIWarnings`、`Assets/Plugins/ES/AICommands`、`Assets/UI` 和正式测试证据目录
不因“AI 内容”标签而整体搬入 AISpace。AISpace 对这些对象只保存索引、来源和迁移指针，避免
出现第二个权威副本；已注册 Skill 的生成/缓存链路按 `.agents/SKILL_AISPACE_BINDINGS.json`
双向发现。

特别保留的外部生成目录：`ES/Output/WebPageStudio` 是 WebPageStudio 输出权威（其中浏览器
profile/cache 属于运行产物），`ES/Automation/AI` 是请求、运行和回执权威，`ES/Bak` 是备份
权威；它们不是 AISpace 临时落点，也不应被扫描器误迁移。

## 已执行迁移

- `Assets/ES/Space/Public` → `Assets/ES/AISpace/Public` 的物理重命名已完成，`.meta` 配对保持。
- 已发现的临时 `.pyc`、编辑器日志和历史截图已归档到 `ES/AISpace/Local/` 的分类/日期目录；
  未发现的外部权威文件不作猜测性移动。
- 空的旧缓存/截图目录已删除；无文件被未经授权删除。
- 2026-08-29 在用户明确授权后清理 `ES/AISpace/Local/Cache`，删除 21 个临时 Skill 运行缓存文件；
  稳定内容、截图和外部权威输出未删除。

后续迁移必须先更新本清单和绑定契约，再执行可回滚的有界移动；禁止按日期单独建立并列根目录。
