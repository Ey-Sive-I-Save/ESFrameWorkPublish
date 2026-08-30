# unitypackage 解析与导入边界

`KnowledgeId`: `es.project.unitypackage-parser.v1`
`Authority`: `ES resource collection contract + Unity import boundary`
`RouteKeys`: `resource`, `unitypackage`, `parser`, `import`, `provenance`, `assetpackage`
`ContentHash`: `66c880a76d16b1509a4ae2bd981e2cf04a446d118416f9ec0f1ad4db6c0e99eb`
`EvidenceLevel`: `S1`

## 可解析内容

`.unitypackage` 是归档容器。收集阶段可在隔离区读取条目清单、路径、文件大小和归档 Hash，识别 `asset`、`path`、`asset.meta` 等成组文件，建立候选 GUID、依赖和许可证记录。

## 解析流程

1. 校验归档 Hash、大小、路径和压缩格式。
2. 禁止绝对路径、`..`、NUL 和目标根外写入。
3. 在 staging 解包并保持 `.meta` 配对；不得直接覆盖 Assets。
4. 解析 manifest/依赖候选，生成资源组 JSON。
5. 交由 AssetPackage/Unity 导入流程执行，导入后重新读取 GUID 和依赖。

## 失败面

恶意路径、重复 GUID、缺失 Meta、符号链接、压缩炸弹、许可证缺失、条目 Hash 变化和部分解包必须停止并保留 staging；不得自动删除原包或声明导入成功。

## Non-claims

本条目不声称已实现解析器，也不证明 Unity AssetDatabase 导入、脚本执行安全、运行时加载或发布成功。

## SourceRefs

- `Documentation/AIKnowledge/entries/resource-import-configuration.md` (`9506a66c018f284da71692ae4f20ae4c6e0277321597d51155fe0733cabc335a`)
- `.agents/skills/es-resource-collection/references/collection-contract.md` (`f752504741d7245e3348c7f63b09b63b8b100a2a97f61602b4ad0557cf2dc867`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESAssetPackageBakeData.cs` (`2ce2365230f6fef88489f6fa2095970bb47f4bf7d309e0e244fa5a8481010af0`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源工具链_四阶段严格隔离_AI协作警告.md` (`3ef18687efa69035b1952318581c0f4b4df7c08ac1f69bae8f32c2f3a0107251`)

`StaleWhen`: AssetPackage 导入链、归档格式处理或资源安全规则变化。
