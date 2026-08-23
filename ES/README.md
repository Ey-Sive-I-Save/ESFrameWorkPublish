# ES 项目级目录

`ES/` 只承载明确属于 ES、且由本项目直接控制的配置、工具、文档、测试与输出。Unity 固定目录、第三方包和 `Assets/Plugins/ES` 源码入口不在这里重复或搬迁。

`ES/` 内的 `Config`、`Tools`、`Contracts` 和可复现脚本属于输入/源码事实；`Automation/Temp`、`Processing`、`Output`、`ResourcePipeline` 构建结果、`Output`、`Releases` 和 `Archive` 属于运行产物、交付物或历史材料。`Automation/Runs` 中如果是受管 RunRecord 或控制动作审计，可作为审计证据保留。其他生成物不得作为当前源码或发布通过证据，除非带有对应的 Git 基线、生成时间和验证回执。

## 目录结构

| 目录 | 用途 | 是否属于源码事实 |
|---|---|---|
| `Config/Luban` | Luban 配置、表格和生成脚本 | 是 |
| `Config/SoTable` | SoTable 表格、计划、示例与导出配置 | 是 |
| `Documentation/StaticSite` | HTML 技术文档、同步规则和本地更新台账 | 是 |
| `Documentation/Output` | ES 文档生成输出 | 视生成流程而定 |
| `Tools` | ES 项目级确定性工具和验证脚本 | 是 |
| `Tests` | 项目级样例、夹具和非 Unity 测试输入 | 是 |
| `Archive` | 已退出活跃入口、但仍需保留的 ES 历史材料 | 否 |
| `Output` | 截图、UnityPackage 等人工交付输出 | 否 |
| `Releases` | 发布包和发布检出目录 | 否 |
| `ResourcePipeline` | 资源烘焙、规划、构建、测试发布与远端发布产物 | 生成物；仅保留输入、合同和必要的可审阅清单 |
| `AI协作历程（Codex）` | 用户明确授权后维护的逐窗口协作档案 | 是，且禁止机械改写历史事实 |

## 资源管线目录

| 目录 | 用途 | 自动清理策略 | 是否进入 Player |
|---|---|---|---|
| `ResourcePipeline/BuildStaging/<Platform>` | AssetBundle 构建后的发布暂存区 | 每次构建清理旧暂存 | 否 |
| `ResourcePipeline/Baked` | Library Catalog 与直接引用图烘焙结果 | 后续烘焙覆盖对应 Library | 否 |
| `ResourcePipeline/Planned/<Platform>` | Bundle 分配计划和资产索引 | 后续规划覆盖 | 否 |
| `ResourcePipeline/BuildCache/<Platform>` | Unity 原始 AssetBundle 输出与 Manifest | 只保留当前计划需要的 Bundle | 否 |
| `ResourcePipeline/InitialTarget/<Platform>` | 构建完成后的初始目标与校验输入 | 后续构建覆盖 | 否 |
| `ResourcePipeline/Published/LocalTest/<Platform>` | 本机测试发布镜像 | 每个平台只保留最新 1 个版本 | 否 |
| `ResourcePipeline/Published/ManualUploadPlans/<Platform>` | 手工上传清单、Hash 与目标 URL | 保留最新 10 份 | 否 |
| `ResourcePipeline/Releases/<Platform>` | CDN/对象存储的权威远端发布产物 | 保留历史版本 | 否 |

`LocalBuild` 可以把当前版本复制到 `Assets/StreamingAssets/Res/<Platform>` 并随 Player 发布；`HotUpdate` 不写入 ES 内置资源，并清理该平台以前生成的内置资源。`ES/ResourcePipeline/Published` 和 `ES/ResourcePipeline/Releases` 都位于 `Assets` 外，不会被 Unity 自动打进 Player。

资源管线的 `Baked`、`BuildCache`、`BuildStaging`、`InitialTarget`、`Planned`、`Published` 和 `Releases` 均是可再生产物或发布镜像，统一不纳入 Git；需要保留的发布版本应进入独立制品库或带基线信息的显式归档目录。一次性迁移旧仓库时，维护者还必须对历史已跟踪产物执行单独的索引清理，不能仅依赖 `.gitignore`。

## 边界

- 不手工修改 AssetBundle、Catalog、Library Identity、Consumer Manifest、Release Manifest 或 Bundle Index；这些文件由 Hash、Manifest 与 BundleKey 相互校验。
- `Output`、`Releases` 和资源管线生成物不等同于真实发布已通过；最终结论仍需 Unity 运行、测试和发布环境证据。
- `.agents/skills`、`Assets/Plugins/ES/AIWarnings`、`Assets/Plugins/ES/AICommands` 保持各自固定发现入口，不迁入 `ES/`。
