# 游戏 UI AssetManifest 与素材解析边界

`KnowledgeId`: `es.project.game-ui-asset-manifest.v1`  
`Authority`: `Current ScreenSpec/registry/validator/adapter/materializer source + official source snapshot`  
`RouteKeys`: `ui-automation`, `ui-asset-manifest`, `asset-manifest`, `asset-provenance`, `asset-license`, `asset-fallback`, `sprite-atlas`, `crop-policy`, `asset-resolver`  
`HashSchema`: `v2`  
`ContentHash`: `58e08515b70bb85d851cd9c3d8b6431ea9c8ad56b4a7a7734669c1a6e33c1004`  
`SourceSetHash`: `58e08515b70bb85d851cd9c3d8b6431ea9c8ad56b4a7a7734669c1a6e33c1004`  
`EntryBodyHash`: `5785c8af3988b9bb47da420dc6f45254552a62384e41145bdb89c7a0686c2c59`
`EvidenceLevel`: `S0`  
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目是 ScreenSpec v3 `assets`/AssetManifest 的 canonical owner。它负责素材稳定身份、角色、
来源、内容哈希、许可证、import/crop/9-slice、Atlas owner、fallback 与解析状态；不负责通用
ResourcePlan/Provider 发布，不把 `assetSlots`、Registry `requiresAsset` 或白图 fallback 当成正式素材。

## Trigger and routing

- 自然语言触发：UI AssetManifest、Sprite/Icon 来源、素材 hash/provenance/license、裁剪、9-slice、
  SpriteAtlas、fallback、Asset resolver、ScreenSpec 素材槽。
- 精确路由：`ui-asset-manifest`、`asset-manifest`、`asset-provenance`、`asset-license`、
  `asset-fallback`、`sprite-atlas`、`crop-policy`、`asset-resolver`。
- 误路由边界：通用包下载、Provider、Scope 或发布由资源管线 owner 负责；参考图输入身份由
  reference-design-evidence owner 负责；视觉风格与 Token 由 visual-design-system owner 负责。

## Canonical AssetManifest fields

| 字段组 | 最小合同 | 当前消费者状态 |
|---|---|---|
| Identity | stable asset id、role、content SHA-256、source kind、source identity | `resolve_ui_asset_manifest.py` 对声明路径、实际哈希、GUID 和尺寸做静态回执；商业来源仍需单独审核 |
| Rights | license id/text、author/owner、allowed use、review status | ScreenSpec 可声明 provenance/license；`generated-procedural` 当前保持 deferred，不能升级为商业授权 |
| Import | project path、asset GUID（存在时）、texture/sprite type、pixels per unit、filter/wrap、color space | ScreenSpec 的 importPolicy 被保留并进入回执；Unity AssetImporter 实际状态仍需 Runtime/Editor 证据 |
| Geometry | original size、crop rect、pivot、border/9-slice、preserveAspect | resolver 读取 aspect/crop/focal/nineSlice/resolutionSet 字段并绑定身份；最终裁切仍需 GPU 复核 |
| Atlas | atlas owner/id、variant、include/build policy、packing constraints | 项目未由本知识证明已创建或发布 SpriteAtlas |
| Fallback | fallback id、触发条件、视觉/布局影响、placeholder 状态 | 白图可保持结构可见，但必须报告 placeholder |
| Resolution | resolver id/version、resolved path/GUID/hash、resolution receipt | `resolve_ui_asset_manifest.py` 已是静态解析器；它不发布资源、不创建 Atlas，也不授予商业验收 |

## Decision rules

1. 每个 `assetSlots` 引用必须解析到同一 ScreenSpec 的稳定 asset id；存在 id 不等于文件存在。
2. `project-sprite` 必须绑定当前项目路径/GUID 与内容 hash；`ai-generated` 必须绑定生成来源、输入、
   输出 hash 和使用权；`generated-placeholder` 必须显式保持 placeholder。
3. crop、pivot、9-slice、preserveAspect 和 Atlas 归属是独立字段，不能从最终截图反推。
4. license 未知、hash 缺失、路径越界、资源导入类型不符或 resolver 输出不匹配时停止正式物化声明。
5. fallback 只能维持布局/状态 Fixture，可见白图不能升级为商业美术或素材完成证据。
6. AssetManifest 变化会使依赖 ScreenSpec、Prefab、快照、PNG 和发布证据 stale；必须以新 spec hash 重跑。

## Verified facts

- ScreenSpec 当前样例的根 `assets` 已包含 path/hash/provenance/license/import/crop/Atlas/resolution/fallback 字段。
- `resolve_ui_asset_manifest.py` 会在项目根下解析声明路径、重新计算 SHA-256、读取 `.meta` GUID 和尺寸；当前大厅回执为 10/10 verified，commercial acceptance 仍 deferred。
- Python/C# Adapter 保留根 `assets` 与组件 `assetSlots`，Materializer 先消费声明路径；按 semantic ID 生成 procedural art 只作为声明资源不可用时的 fallback。
- 当前生成资源是项目内 `generated-procedural` Sprite 候选，不是已批准的商业美术；Atlas 构建、Unity 导入和 GPU 辨识度仍未证明。
- 项目 manifest 未声明 Addressables；这不阻止未来资源方案，但当前不能声称 Addressables 解析/发布可用。

## Required reads

- 本条目、ScreenSpec 模板、Registry、Validator、两个 Adapter、Materializer contract/source、UI 工作流。
- 修改/发布真实资源时追加 `es-resource-pipeline`、`es-resource-publish-audit` 与当前资源源码/配置。
- 使用外部或 AI 生成素材时必须取得当前来源、许可证和内容 hash，不能由本条目代填。

## Common AI failure modes

| 错误行为 | 触发/症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 当前证据 | 缺失证据 | Source owner |
|---|---|---|---|---|---|---|---|---|
| `assetSlots` 被当成正式资源 | Validator 通过但 Prefab 仍是白图 | 混淆语义引用和解析结果 | 检查 root manifest 与 resolver receipt | 标记 placeholder/Blocked | 补 resolver 后以新 spec hash 重物化 | Validator/Adapter/Materializer + resolver 回执 | Unity 导入与 GPU 辨识度复核 | 本条目 + resolver owner |
| 只校验 source 枚举 | 无 hash/license 仍签收 | 误读 Validator 覆盖 | 逐项核对 identity/rights/import/geometry | 补齐 manifest 或停止 | 使旧 Prefab/证据 stale | Validator 静态源码 | 内容哈希与权利证据 | 本条目 |
| fallback 冒充商业美术 | 画面可见即写“素材完成” | 把可渲染性当资产真实性 | 快照/报告列出 placeholder | 只声明结构可见 | 替换素材后重采集所有状态 | 白图 fallback 源码 | 正式 Sprite 与视觉/发布证据 | Materializer + 资源 owner |
| 裁剪/9-slice 被截图反推 | 不同 profile 边框变形 | 缺失几何合同 | 核对原图、crop、border、PPU | 显式记录并验证 import | 恢复原资源，重导入和重物化 | Unity Image 官方来源锁 | 当前 AssetImporter/Prefab 回执 | 本条目 + Unity asset owner |
| Atlas 名称被当成已发布 | 引用 atlas id 但构建不含资源 | 混淆设计归属和构建事实 | 查当前 Atlas asset、GUID、include/build receipt | 转资源发布审计 | 回退为未打包资源并披露 | SpriteAtlas 官方来源锁 | Atlas/Player 构建证据 | 资源管线 owner |

## Execution checklist

- 开始前：列出每个 asset id、role、source identity、hash、license 与目标 profile/state。
- 物化前：验证文件/GUID、import/crop/9-slice、Atlas owner、fallback 和 resolver receipt。
- 证据中：快照与报告同时列出 resolved asset、hash 与 placeholder，不接受空 `assets` 冒充完成。
- 发布前：转资源发布 owner，取得 Player/包/Atlas 真实回执。

## Evidence boundary and non-claims

Static 可证明声明资源的路径、哈希、GUID、尺寸和 provenance 回执；不能证明 Unity 导入 Sprite、
创建 Atlas、构建 Addressables/AssetBundle、GPU 主视觉辨识度或 Player/发布验收。商业素材审核仍为 deferred。

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json` (`4aba3b950fef2b9c45dc6b4ba6abc3b6a59517ddeb566ab86ede106d5facf38d`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`4d60216d8d3c870d243f01577074b7b16b5e2234cb8eff02f9f26231521def74`)
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_asset_manifest.py` (`e435d150cc8f5a6928aa255a958c626af54ef977e02ddad01ace002badf36eb9`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`4688b2f94c887ffda48468492f39aad66a8a47cffb1a25f1ddd3e48e97e84158`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)

## StaleWhen

ScreenSpec `assets` schema、Registry、Validator、任一 Adapter、Materializer/resolver、Unity Image/
SpriteAtlas 版本、资源发布方案、官方来源锁或任一 SourceRef 哈希变化。
