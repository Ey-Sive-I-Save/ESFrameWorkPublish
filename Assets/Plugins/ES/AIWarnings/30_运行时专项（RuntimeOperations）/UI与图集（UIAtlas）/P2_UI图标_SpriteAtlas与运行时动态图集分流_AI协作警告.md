# P2：UI 图标图集分流
Status: current
StableId: es.aiwarning.p2.ui-icon-atlas-routing.v1
Authority: AIWarnings；Knowledge 详述
RouteKeys: aiwarnings, p2, ui, icon, atlas
Applicability: IconKey/SkillId 图标、动态纹理
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p2-ui-icon-atlas-routing.md
StaleWhen: 图标/图集实现或 SourceRef 变化。
- 可构建/热更 Sprite（运行时选图亦然）走 `Image + SpriteAtlas`。
- 远端、上传、临时 Texture、截图、RenderTexture 才走 `ESDynamicAtlasGraphic`；动态选图不改变此界线。
- 路径：`SkillId/IconKey → SpriteAtlas → Sprite → Image.sprite`；动态纹理 → `ESDynamicAtlasGraphic`。
Knowledge：`es.aiwarning.p2.ui-icon-atlas-routing.v1`
