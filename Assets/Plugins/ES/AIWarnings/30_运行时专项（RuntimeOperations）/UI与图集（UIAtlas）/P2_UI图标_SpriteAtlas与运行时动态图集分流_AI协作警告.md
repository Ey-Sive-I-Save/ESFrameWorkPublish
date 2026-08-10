# P2：UI 图标的 SpriteAtlas 与运行时动态图集分流

> 状态：现行约束
>
> 级别：P2

## 结论

运行时根据 `IconKey` 选择图标，不等于运行时产生了新纹理。

- 候选 Sprite 能随构建或热更资源包管理时，即使运行时才知道要用哪个图标，也应使用 `Image + SpriteAtlas`。
- 只有远端头像、用户上传图片、临时 `Texture2D`、截图或 `RenderTexture` 等无法预先打包的纹理，才使用 `ESDynamicAtlasGraphic`。
- 禁止仅因技能图标由配置动态选择，就把常规 UI 图标全部接入运行时动态图集。

## 选择路径

```text
SkillId / IconKey -> 按需加载 SpriteAtlas -> 解析 Sprite -> Image.sprite
不可预打包 Texture -> ESDynamicAtlasGraphic
```
