# 资源工具链四阶段隔离：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.asset-pipeline-four-stage-boundary.v1`  
`Authority`: `AIWarnings` 与当前资源流水线实现  
`RouteKeys`: `aiwarnings`, `p0`, `assets`, `resource-pipeline`, `bake`, `plan`, `build`, `publish`, `staging`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `bfd171c931abdb32682fc704cdc94e8c221b0a879d61df066c22e794bde51d89`  
`SourceSetHash`: `bfd171c931abdb32682fc704cdc94e8c221b0a879d61df066c22e794bde51d89`  
`EntryBodyHash`: `7791a75d66774b726b6053208e40106fdec74491dfd5ae173bca05bc698924de`  
`StaleWhen`: 阶段 API、产物 Schema、菜单入口、Provider 状态、发布协议或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留阶段隔离、身份边界、权限和禁止事项；本条目承载阶段职责、产物关系、Provider 状态、缓存/校验规则、废弃菜单和验收细节。Knowledge 仅导航与证据回溯，不授予构建、网络、发布或 Unity 权限。

## 阶段与产物

1. `ESAssetReferenceBaker` 只分析并输出 `ESAssetLibraryCatalog.json`、`ESAssetReferenceGraph.json`，不得改 AB 标签、调用 BuildPipeline 或复制发布文件。
2. `ESAssetBundleBuildPlanner` 只读取烘焙结果并输出 `ESAssetBundleBuildPlan.json`、`ESAssetBundleAssetList.json`；它是唯一可修改 ES 管理范围 `AssetImporter.assetBundleName` 的阶段，不得真正构建。
3. `ESAssetBundleBuilder` 校验标签与计划一致，是唯一可调用 `BuildPipeline.BuildAssetBundles` 的阶段，只写 `BuildStaging`。
4. `ESAssetBundlePublisher` 只校验 staging 并输出本地 Release/上传计划，禁止分析、规划、改标签、重建或远端网络；`ESAssetReleaseManifest.json` 最后原子写入。
5. 远端发布只消费第四步上传计划，执行预检、上传与验证，不得回头分析、规划、改标签或构建。

任一阶段失败不得伪造下阶段产物或以旧产物冒充本次成功；自动化/测试可直接调用强类型阶段 API，但仍需前置产物验证。唯一用户入口是 `【ES】/资源与发布/资源管理/资源管理窗口`，依次提供烘焙、规划标记、构建、发布、远端发布五个动作。

## 身份、Provider 与发布校验

物理身份为主资源 `GUID + LocalFileId(0)`、独立子资源 `GUID + LocalFileId`；EnumKey/StringKey 仅作资产类型内业务寻址，不能进入全局物理加载映射。ManualPlan 可用但不联网；Aliyun OSS 已实现但缺真实 Bucket 验收；S3Compatible、HttpPut、ExternalCommand 未实现。Root Manifest 必须 `no-cache` 并最后切换；版本 Bundle/索引可 immutable 缓存；上传后以 HEAD 校验长度和 `x-oss-meta-es-sha256`，ETag 不得充当内容 Hash。Unity 生成 `.csproj` 不得手工修改。

## 原文快照

迁移前台账快照：89 行、4652 字节，原始 SHA-256 `98e2336f8e9197051c93eaa3fe774f087463089cc931ffc85f459516e6d48563`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源工具链_四阶段严格隔离_AI协作警告.md` (`3ef18687efa69035b1952318581c0f4b4df7c08ac1f69bae8f32c2f3a0107251`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`dd8a518af1aa00886ec2bb4af146a7a2d74dff293a48faa1422c40403109cc8d`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-asset-pipeline-four-stage-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源工具链_四阶段严格隔离_AI协作警告.md`
