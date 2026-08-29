# UI 图标 SpriteAtlas 与运行时动态图集分流：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p2.ui-icon-atlas-routing.v1`  
`Authority`: `AIWarnings` 与当前 UI/资源运行时合同  
`RouteKeys`: `aiwarnings`, `p2`, `ui`, `icon`, `sprite-atlas`, `dynamic-atlas`, `resource-routing`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `22698141e680f829ce5f5f6c20a14933781afeb8f8d823f3bcbfd4266fab0e3a`  
`SourceSetHash`: `22698141e680f829ce5f5f6c20a14933781afeb8f8d823f3bcbfd4266fab0e3a`  
`EntryBodyHash`: `bcc5f522fa40537afb4acabe4d6f44fa38ce44c97c09f4ef92bf71bbad7a3763`  
`StaleWhen`: IconKey、SpriteAtlas、ESDynamicAtlasGraphic、资源构建或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留 SpriteAtlas 与动态图集的选择边界；本条目承载适用纹理类型、资源路径和误用判定。Knowledge 不授予资源构建或运行时修改权限。

## 选择规则

运行时根据 `IconKey` 选择图标，不代表运行时产生新纹理。候选 Sprite 能随构建或热更资源包管理时，即使运行时才知道具体图标，也应使用 `Image + SpriteAtlas`。只有远端头像、用户上传图片、临时 `Texture2D`、截图、`RenderTexture` 等无法预先打包的纹理才使用 `ESDynamicAtlasGraphic`。

禁止仅因技能图标由配置动态选择，就将常规 UI 图标全部接入运行时动态图集；这会把本可构建期管理的资源转入不必要的运行时纹理路径。标准路径为 `SkillId / IconKey → 按需加载 SpriteAtlas → 解析 Sprite → Image.sprite`，不可预打包 Texture 才走 `ESDynamicAtlasGraphic`。

## 原文快照与验收

迁移前台账快照：21 行、767 字节，原始 SHA-256 `207f74a74d0f5e9cdcf91c5dd23d4f5afb9f40e3899938460a6c159666d4b5c5`。验收检查 IconKey 选择、资源包管理、不可预打包分类及 UI 绑定路径；本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md` (`9215f8a2f89b0bcb8e9acda6c03e8a53d13f58a39d7e2c42f80d89cce9859196`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`6a6de42136473a2d48f4fff93b1df98abe76633fb76608027f864a624986af7b`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p2-ui-icon-atlas-routing.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md`
