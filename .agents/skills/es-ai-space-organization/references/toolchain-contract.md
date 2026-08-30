# AISpace 严谨工具链合同

`Invoke-ESAISpaceToolchain.ps1` 是 AISpace 相关 Skill 的统一静态入口。它只执行确定性、只读检查；可选地写入项目相对 JSON 回执，不启动 Unity/Runtime，不删除、重命名或修改 Git。

固定检查顺序：

1. AISpace 唯一权威与发现闭包（`Test-ESAISpaceAuthority.ps1`）。
2. Local/Public 临时内容策略（`Test-ESLocalTempPolicy.ps1`）。
3. Skill 输出绑定及反向投影（`Test-ESSkillAISpaceBindings.py`、`Test-ESSkillRelationRegistry.py`）。
4. 所有已绑定 Skill 的 `SKILL.md` 与 `governance.json` 入口存在性。

任一检查失败即整体 `failed` 并返回非零退出码。回执明确 `runtime-not-run` 与未证明项，不能把静态通过升级为 Unity、Runtime 或发布验收。
