# 常规资源导入与配置

`KnowledgeId`: `es.project.resource-import-configuration.v1`
`Authority`: `Current ES source + AIWarnings P0`
`RouteKeys`: `resource`, `asset`, `import`, `configuration`, `assetpackage`, `library`
`ContentHash`: `6ea9c2319e419b3244ab227ca923a13268770f4a80411e14b47f373471c81344`
`EvidenceLevel`: `S1`

## 已验证边界

`ESAssetLibrary`/`ESAssetBook` 是 Editor 作者层；资源注册应经过统一内容注册入口。物理身份使用 GUID + LocalFileId，显示名和路径不能替代稳定身份。`unitypackage` 通常进入 Assets 后再由 Unity 导入和 ES 注册；导入结果不能直接视为 ResourcePlan 或发布产物。

## 导入流程

1. 记录来源、许可证、类型、大小、SHA-256 和观察时间。
2. 进入 staging 或隔离区，检查路径、编码、Meta 配对和物理身份。
3. 解析依赖并建立资源组状态。
4. 通过 AssetPackage/统一注册入口生成目标路径和分类。
5. 仅在明确授权后执行 Unity 导入或迁移。

## 失败面

路径越界、缺失许可证、Hash 变化、重复 GUID、Meta 丢失、类型不匹配和依赖闭包不完整必须停在 NeedsReview/Quarantine；不得静默覆盖目标。

## Non-claims

本条目不证明 Unity 导入、AssetDatabase 事务、运行时加载或发布已通过；这些需要独立运行证据。

## SourceRefs

- `Assets/Plugins/ES/0_Stand/_Res/Master/Shared/SoSupport/ESAssetLibrary.cs` (`6a95f1ded2dd6084baeef58b449a823d9ef8ca7269e7f332b4fd17bcb5c4e820`)
- `Assets/Plugins/ES/0_Stand/_Res/Master/Shared/SoSupport/ESAssetBookAndPage.cs` (`90d14a64d1d386522e30c6380974d9ef34806a373debb3708c8b50a0a59a83b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`c1fc2f3dd03713d0bedf4c12c4e95190613033af55cc28eb79b075976501c31b`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md` (`6ee72697e24d9dc57a3e6bc8c644f72e9b26b979d4a32ef47bbc7c49a895615d`)

`StaleWhen`: 导入入口、Library/Book、身份注册或资源 P0 变化。
