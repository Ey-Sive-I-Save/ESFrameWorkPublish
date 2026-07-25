# 资源工具链四阶段严格隔离

资源构建必须保持四个独立阶段，禁止重新合并为一个“构建资源”入口：

1. `ESAssetReferenceBaker`：只分析并输出 `ESAssetLibraryCatalog.json`、`ESAssetReferenceGraph.json`。禁止改 AB 标签、调用 `BuildPipeline`、复制发布文件。
2. `ESAssetBundleBuildPlanner`：读取烘焙结果，输出 `ESAssetBundleBuildPlan.json`、`ESAssetBundleAssetList.json`。这是唯一允许修改 ES 管理范围内 `AssetImporter.assetBundleName` 的阶段，禁止真正构建。
3. `ESAssetBundleBuilder`：校验标签与计划一致，唯一允许调用 `BuildPipeline.BuildAssetBundles`，只写 `BuildStaging`。
4. `ESAssetBundlePublisher`：只校验和分发已有 staging 产物，禁止分析、改标签或重新构建；`ESAssetReleaseManifest.json` 必须最后原子写入。

身份权威固定为：主资源 `GUID + LocalFileId(0)`；独立子资源 `GUID + LocalFileId`。`EnumKey/StringKey` 只在资产类型内作为业务寻址权威，不能进入全局物理加载映射。

正式入口固定为：

- `ES/Resource/1. Bake Asset References`
- `ES/Resource/2. Plan And Mark AssetBundles`
- `ES/Resource/3. Build AssetBundles`
- `ES/Resource/4. Publish AssetBundles`
