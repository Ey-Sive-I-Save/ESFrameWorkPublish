# 项目最高警告：资源工具链四阶段严格隔离

> 级别：P0。阶段职责、产物权威和调用顺序不得合并；编辑器菜单必须遵守 `【ES】/` 统一根。

资源构建必须保持四个独立阶段，禁止重新合并为一个“构建资源”入口：

1. `ESAssetReferenceBaker`：只分析并输出 `ESAssetLibraryCatalog.json`、`ESAssetReferenceGraph.json`。禁止改 AB 标签、调用 `BuildPipeline`、复制发布文件。
2. `ESAssetBundleBuildPlanner`：读取烘焙结果，输出 `ESAssetBundleBuildPlan.json`、`ESAssetBundleAssetList.json`。这是唯一允许修改 ES 管理范围内 `AssetImporter.assetBundleName` 的阶段，禁止真正构建。
3. `ESAssetBundleBuilder`：校验标签与计划一致，唯一允许调用 `BuildPipeline.BuildAssetBundles`，只写 `BuildStaging`。
4. `ESAssetBundlePublisher`：只校验和分发已有 staging 产物，禁止分析、改标签或重新构建；`ESAssetReleaseManifest.json` 必须最后原子写入。

身份权威固定为：主资源 `GUID + LocalFileId(0)`；独立子资源 `GUID + LocalFileId`。`EnumKey/StringKey` 只在资产类型内作为业务寻址权威，不能进入全局物理加载映射。

## 唯一用户入口

开发者统一从以下窗口进入：

```text
【ES】/资源与发布/资源管理/资源管理窗口
```

窗口内部按顺序提供四个明确动作：

```text
1. 烘焙资产引用
2. 规划并标记 AB
3. 构建资源包
4. 发布资源包
```

四个阶段是独立操作和独立产物，不要求重新创建四个顶层菜单。自动化或测试可以直接调用对应强类型阶段 API，但不得绕过前置产物验证。

## P0 阶段边界

- `ESAssetReferenceBaker` 不能修改任何 AB 标签，也不能调用 Builder/Publisher。
- `ESAssetBundleBuildPlanner` 是唯一允许修改 ES 管理范围内 AB 标签的阶段；必须只消费已烘焙输入。
- `ESAssetBundleBuilder` 是唯一允许调用 `BuildPipeline.BuildAssetBundles` 的阶段；必须只写 staging。
- `ESAssetBundlePublisher` 不能重新分析、规划、标记或构建；根发布清单必须最后提交。
- 任一阶段失败不得伪造下阶段产物，也不得用旧产物冒充本次成功。
- 资产身份、业务 Key、BundleKey 和发布文件身份必须保持各自边界，禁止互相代替。

## 禁止恢复的旧菜单

以下入口已经废弃，任何源码、有效警告或 AI Command 都不得把它们写成正式入口：

```text
ES/Resource/1. Bake Asset References
ES/Resource/2. Plan And Mark AssetBundles
ES/Resource/3. Build AssetBundles
ES/Resource/4. Publish AssetBundles
Window/ES/...
Tools/ES/...
```

磁盘路径中的 `Assets/Plugins/ES/` 不属于菜单，不得机械替换。

## 修改后验收

1. 扫描 `MenuItem`、`CreateAssetMenu`、`AddComponentMenu`，确认没有新增旧菜单根。
2. 从资源管理窗口分别执行四阶段，确认每一步只产生自己的产物和副作用。
3. 故意移除或损坏上一阶段产物，下一阶段必须明确拒绝执行。
4. Builder 前验证当前 AB 标签与 BuildPlan 一致。
5. Publisher 前验证 staging、Bundle 索引和依赖闭包，根发布清单最后写入。
6. 检查 UTF-8、乱码与 `git diff --check`。
