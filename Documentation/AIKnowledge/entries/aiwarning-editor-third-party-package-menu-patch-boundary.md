# ES 第三方编辑器包嵌入与菜单补丁：保真 Knowledge

`KnowledgeId`: `es.aiwarning.editor.third-party-package-menu-patch-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Packages/Editor 合同  
`RouteKeys`: `aiwarnings`, `editor`, `package`, `menu`, `patch`, `rollback`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `03cca5801526d6b6e6f28ac769f647089ee787546b98c7365917d6f956c7c81a`  
`SourceSetHash`: `03cca5801526d6b6e6f28ac769f647089ee787546b98c7365917d6f956c7c81a`  
`EntryBodyHash`: `cf134bf30b2594bc2410039673e210deff66a27339c75d8859f321a0880e763f`  
`StaleWhen`: 嵌入包快照、manifest/package.json、菜单补丁、门禁脚本或 Unity 版本变化。

## 迁移范围

Warning 保留包来源、最小补丁、菜单 API 限制、升级回滚和证据边界；本条目保存完整流程、静态/运行时区分与原文快照。Knowledge 不替代 Warning、包源码、许可证或 Unity 证据。

## 嵌入与完整性

将经审阅包放在 `Packages/com.*`，目录名、`package.json.name` 与 `manifest.json` 依赖名一致；本地有效嵌入包优先于 PackageCache/Git。远程 Git 声明仅是备用来源/升级线索，不能据此声称已拉取或同步。保留许可证、版权、NOTICE 与上游版本。长期补丁只作用于项目快照并保持最小差异，PackageCache 改动会在重导入、清缓存或换机后丢失。静态门禁检查目录、包名、嵌套 `.git`、菜单补丁和 manifest 备用声明。

## 菜单边界

Unity 2022.3 的 `UnityEditor.Menu` 没有可用 `RemoveMenuItem`。`[MenuItem(path, true)]` 是验证/禁用回调，只能置灰；需要隐藏时，在嵌入包源码注释或条件编译掉原始 `HybridCLR/...`、`Luban/...` 注册并保留补丁。ES 替代入口使用 `【ES】/` 根路径，不复制第三方业务逻辑；空兼容抑制器不承担运行时删除菜单。

## 升级、回滚与证据

升级记录上游版本、提交号和 release notes；对比 API、程序集、菜单路径、许可证变化；在新快照重放最小补丁，不直接覆盖后假设仍存在。依次运行来源/完整性、UTF-8、静态编译检查，失败即停；随后才在目标 Unity 版本验证 Package Manager、导入、ReloadDomain、菜单显示/禁用、快捷键、排序和代表入口。保留旧快照与补丁差异。目录存在、包名匹配、无活动注册仅是静态证据；`packages-lock.json source: git` 是 evidence gap；静态编译不替代 Unity 证据。缺失源码造成的构建阻断需报告范围，不修复无关问题。每次修改同步上游记录、补丁说明、门禁脚本和 Warning 状态；删除本地包、恢复远程源或改变许可证需重新审阅。

## 原文快照

迁移前原始文件为 51 行、4541 UTF-8 字节，原始 SHA-256 为 `5b992cef4884f73b1c9efa8e5b5ba377fa27e9501299b012f462afc666708a28`。本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/第三方编辑器包嵌入与菜单补丁_AI协作警告.md` (`f0103c753440abc81acf3d722d5f2f5e9772faa555dbd578292a5359f0495fae`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`cc7bf9440d88c5740443122e3c2cb1775079ec8f584882a93f16b0090c084509`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-editor-third-party-package-menu-patch-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/第三方编辑器包嵌入与菜单补丁_AI协作警告.md`
