# R5 创意资源与渲染证据最小闭环

`KnowledgeId`: `es.project.creative-resource-evidence.v1`
`Authority`: Current ES resource/UI/camera contracts + AIWarnings + bounded official-source snapshots
`RouteKeys`: `creative-resource`, `resource`, `provenance`, `license`, `rendering-evidence`, `frame-debugger`, `profiler`, `single-variable-test`, `evidence`
`EvidenceLevel`: `S1`
`RuntimeEvidence`: `runtime-not-run`
`ContentHash`: `dea244b1488d40ed474d597ad9b8e86e6feebc54605d827e2d859c1dd8eaebf1`
`StaleWhen`: 资源发布链、AssetPackage、Composite Shader、相机/UI/VFX 合同、Unity 版本、外部来源快照或任一 SourceRef 哈希变化。

## 结论

现有知识已覆盖资源分层、相机/UI 权威和渲染批次证据，但这些内容分散在多个条目；本条目只提供 R5 的最小路由，不建立第二套运行时或资源权威。

## 创意变量与投影

- 风格候选：写实电影、高对比科幻、二次元、暗黑战术；每次只改变一个风格变量，并绑定目标材质、VFX、Audio、Animation 或 ScreenSpec 意图。
- 镜头变量：构图、焦点、FOV、跟随距离、肩部偏移、遮挡恢复；只能经 `ESCameraRequest/Lease → ESCameraDirector → CM2 Adapter`。
- UI 变量：信息密度、层级、状态色与文本长度；只能作为 ScreenSpec v3 的视觉/意图输入，不能直接读写 HP、Stats 或业务状态。
- 资源投影：先冻结来源、作者、许可证原文、版本、获取 UTC、尺寸、SHA-256 与 GUID/LocalFileId 或源指纹，再投影到 `keep/stage/quarantine`；禁止把候选直接写入正式 Assets。

## 证据阶梯

1. Static：合同、SourceRef、哈希、依赖闭包、稳定身份、单变量矩阵。
2. Unity Editor：导入/材质/Prefab/ScreenSpec 生成回执；不能证明 Player 性能。
3. Frame Debugger：同一输入下记录事件序号、Pass/Shader、最终材质、纹理、Canvas/排序、Mask/Stencil 和断批原因。
4. Profiler：记录 CPU/GPU、Canvas rebuild、内存和稳态区间；目标设备采样优先于 Editor Play Mode。
5. Player/Release：以目标平台构建、Manifest/BundleIndex、下载/校验/回滚回执完成发布结论。

## 失败面与反驳

- “免费”不等于可商用；许可证缺失或页面漂移必须 `candidate/Quarantine`。
- Prefab/Shader/截图存在不等于已接入、可见或性能通过。
- Frame Debugger 解释单帧渲染事件，不替代目标平台稳态 Profiler。
- 旧代 Provider/Scope/Lease 或重复导出不得使用 last-write-wins；冲突、缺依赖、Hash 不匹配必须失败闭合。
- 生成占位图、空哈希、模型观察不能标记 `complete` 或视觉通过。

## 当前边界

本条目支持 R5 方案设计和证据规划；本轮未执行 Unity、PlayMode、Frame Debugger、Profiler、Player、下载或发布，均为 `runtime-not-run`。外部网页只做校准，不能替代项目权威。

## SourceRefs

- `Documentation/AIKnowledge/ExternalSources/unity-rendering-evidence-official-20260830.json` (`e737be40ab96bb8b27a738fae09a50d615ec91d8919106c96fbc982c17ebddf4`)
- `Documentation/AIKnowledge/ExternalSources/creative-resource-license-official-20260830.json` (`5d4067cff4bc2b47ca7a4854fadfbd37d9d048c9b747de19436020ea79f8ca2a`)
- `Documentation/AIKnowledge/entries/resource-management-architecture.md` (`dc734f497eecf776115d4415bcbf3c613976c5c3cabcd8d6dc70767cb127f030`)
- `Documentation/AIKnowledge/entries/aiwarning-p0-asset-pipeline-four-stage-boundary.md` (`84ea56c10177fc3b95a2b3a0e403bba6934d4381b962e59585e5cf626e0a829a`)
- `Documentation/AIKnowledge/Engineering/engineering-rendering-batching-evidence/rendering-batching-evidence-contract.md` (`10e4cdbbf97fc40b85cbe708a3c4d6f5dab48461d1ca780172d9ee47e8900bf8`)
