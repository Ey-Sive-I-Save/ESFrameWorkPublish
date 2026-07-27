# ES 资源管线输出目录

此目录由 ES 资源构建、测试和发布管线管理，不属于 Unity 的 `Assets` 目录，因此其中内容不会自动进入 Player。

## 目录说明

| 目录 | 用途 | 自动清理策略 | 是否进入 Player |
|---|---|---|---|
| `BuildStaging/<Platform>` | AB 构建完成后的发布暂存区，供“发布资源包”读取和校验 | 每次构建清理旧暂存，只保留当前构建 | 否 |
| `ResourcePipeline/Baked` | Library Catalog 与直接引用图烘焙结果 | 后续烘焙覆盖对应 Library | 否 |
| `ResourcePipeline/Planned/<Platform>` | 当前 Bundle 分配计划和资产文件索引 | 后续规划覆盖 | 否 |
| `ResourcePipeline/BuildCache/<Platform>` | Unity 原始 AssetBundle 构建输出与 Manifest，用于增量构建 | 只保留当前 BuildPlan 使用的 Bundle | 否 |
| `Published/LocalTest/<Platform>` | 当前发布版本的本机测试镜像，用于检查目录结构或模拟 CDN | 只保留最新 1 个版本 | 否 |
| `Published/ManualUploadPlans/<Platform>` | 手工上传文件清单、顺序、Hash 和目标 URL | 保留最新 10 份 | 否 |
| `Res/<Platform>` | 准备上传至 CDN/对象存储的权威远端发布产物 | 自动保留历史版本 | 否 |

## 与 StreamingAssets 的关系

- `LocalBuild`：当前版本还会复制到 `Assets/StreamingAssets/Res/<Platform>`，并随 Player 发布。
- `HotUpdate`：不会写入 ES 资源到 StreamingAssets，并会清理该平台以前生成的内置资源。
- `ES/Published/LocalTest` 和 `ES/Res` 均位于 `Assets` 外，不会被 Unity 自动打进 Player。

## 不要手工修改

以下文件由 Manifest、SHA-256 和 BundleKey 相互校验，手工修改后会导致发布或运行时完整性检查失败：

- AssetBundle 文件；
- Catalog；
- Library Identity；
- Consumer Manifest；
- Release Manifest；
- Bundle Index。

如需重新生成，请按顺序执行：

1. 烘焙资产引用；
2. 规划并标记 AB；
3. 构建资源包；
4. 发布资源包。

## 远端历史版本

`Res/<Platform>` 的历史版本不会被自动删除，因为已经发布的旧客户端和回滚版本可能仍在引用它们。远端版本应在确认没有活跃客户端引用后，通过专门的版本下线流程删除。
