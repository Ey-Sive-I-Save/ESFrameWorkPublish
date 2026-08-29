# P2：第三方 Unity 编辑器包嵌入与菜单补丁边界

Status: current
StableId: es.aiwarning.editor.third-party-package-menu-patch-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, editor, package, menu, patch, rollback
Applicability: 第三方 Unity Editor 包嵌入、菜单补丁、升级与回滚
Owner: ESFramework EditorTooling 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-editor-third-party-package-menu-patch-boundary.md
StaleWhen: 嵌入包快照、manifest/package.json、菜单补丁、门禁脚本或 Unity 版本变化。

## 长期约束

- 项目定制包优先使用 `Packages/com.*` 的审阅快照；目录、`package.json.name`、`manifest.json` 依赖名必须一致，保留许可证/NOTICE/上游版本，禁止把 PackageCache 临时改动当长期方案。
- 菜单补丁必须是最小、可审阅、可回滚的项目内差异。Unity 2022.3 不提供可用 `UnityEditor.Menu.RemoveMenuItem`；`[MenuItem(path, true)]` 只能验证/置灰，真正隐藏需在嵌入包源码禁用原注册。
- ES 替代入口遵守 `【ES】/` 根路径，不复制第三方业务逻辑；兼容抑制器不得虚构运行时删除菜单。来源/完整性门禁是静态证据，不等同于 Unity 导入、ReloadDomain 或菜单实机验收。
- 升级必须记录上游版本/提交与 release notes，重放补丁并保留旧快照；失败先停止。未完成 Unity 包解析、菜单实机和代表入口验证前，状态只能是 `Implemented-Unverified`/`Integrating`，不得宣称 Stable/Ready。
- `packages-lock.json` 的 `source: git`、静态编译或菜单源码扫描不能单独证明 Embedded、导入、显示、快捷键或排序；许可证、删除本地包、恢复远程源等属于显式依赖变更。

## Knowledge 导航

完整适用范围、门禁、菜单处理、升级回滚步骤、证据等级和维护责任见 `es.aiwarning.editor.third-party-package-menu-patch-boundary.v1`。Knowledge 不授予依赖修改或运行权限。
