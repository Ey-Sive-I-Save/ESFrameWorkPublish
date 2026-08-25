# 游戏 UI 设计官方来源锁

本文件为游戏 UI canonical 知识条目保存有界、可重读的外部一手资料快照。它只记录来源身份、
响应哈希、目标版本和本次使用的最小合同，不复制整页文档，不替代当前项目源码，也不
证明 Unity、GPU、视觉质量或 Player 已运行。

`RetrievedAtUtc`: `2026-08-24T00:51:41.8882393Z`
`AdditionalRetrievedAtUtc`: `2026-08-24T01:47:34.1769115Z`

## Online sources

SHA-256 对 HTTP 响应正文的原始字节计算。下列页面在读取时均返回 HTTP 200，且请求 URL
与重定向后的最终 URL 相同。

| 产品/版本 | 官方 URL | Raw response SHA-256 | 本批使用范围 |
|---|---|---|---|
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/UICanvas.html | `c805db510b21c57c4c37dd5fd701be1d7b4e8477dde7bf5ee0dd0335ab27d11c` | UI 元素属于 Canvas 子层级；Canvas render mode 决定 screen/world space |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/script-CanvasScaler.html | `f234ec0d3ae3f8f527d43954ff3397aa876fa4fc482acce4efac4cc02bd7c2b8` | Scale With Screen Size、Reference Resolution 与宽高匹配是显式输入，不能从单张截图猜测 |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/UIAutoLayout.html | `09e44e94964ccb7c2bd342c45048ad855c32bd125335eba96cdf4e4ea75879b0` | Auto Layout 由 layout element 提供尺寸信息、layout controller 计算布局；父子职责不能混写 |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/EventSystem.html | `2c6b5536e9a873626a4fd65639ab6f23b1bd3782d8707e7023884332b280cdda` | EventSystem 管理选择、Input Module 与 Raycaster；静态层级不证明输入可用 |
| WCAG 2.2, SC 1.4.3 | https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html | `d19b7752115ab1647d46b2958cd2c1f269e9d3d069f09130c13211c61f6af2ed` | 普通文本 4.5:1，大文本 3:1；作为设计校准，不冒充 Unity 视觉测量回执 |
| WCAG 2.2, SC 2.4.13 | https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance.html | `fdcf316a1f0e3cc758eda9618322d132862124ff0a3f3b1a3acbf20daf804ff0` | 焦点指示面积至少等价于 2 CSS px 周长，聚焦/未聚焦像素对比至少 3:1 |
| WCAG 2.2, SC 2.5.8 | https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html | `f006ee8815f8e2a87ebd83fa538bb71d3d2dc43d921e9cd2761b77a590830f7f` | 指针目标至少 24x24 CSS px，或满足规范列出的间距/等价等例外 |
| DTCG Format 2025.10 | https://www.designtokens.org/tr/2025.10/format/ | `af60e88e744bed470e5420a930637d0c926df9f1d1a9f7860b010b579cd92420` | Token 的 `$value`、`$type`、组继承与引用解析属于交换格式；格式存在不等于 Unity 已有消费者 |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Screen-safeArea.html | `8de3c6209f1ac5e26c91114d33bcb9c5831b859b854376d96642683f4fa01069` | `Screen.safeArea` 是以像素表达的可见安全区域；需要项目消费者投影到 Canvas/RectTransform，API 存在不证明已适配 |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/script-SelectableNavigation.html | `b48cf231c7af7dfc374772cfa08b1fcb134ca6a377df66945797350c08803ce0` | Selectable Navigation 可为 Automatic、Explicit、Horizontal、Vertical 或 None；静态组件存在不证明焦点图正确 |
| Input System 1.11 | https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html | `c4b211588d395b05dfecc7f8627ec20a25fc48dfd34207980003ea8ccc7212dc` | Input System UI 需要 EventSystem 与兼容 UI Input Module/action 配置；包已安装不证明 Fixture 或 Player 已绑定该输入路径 |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/script-Image.html | `283c8da21a50aab33c8ee6fb0345d8dc46a594c507e0a0a5475cdc955360d229` | UGUI Image 消费 Sprite，并有 Simple/Sliced/Tiled/Filled 等类型；素材角色必须显式约束 import、切片与 fallback |
| Unity 2022.3 | https://docs.unity3d.com/2022.3/Documentation/Manual/SpriteAtlasWorkflow.html | `a5f413f361d05eaf605abac7b84b827cd812d8ac72ff2d85d4d2f78e2c60205a` | Sprite Atlas 负责将纹理打包为合并纹理；Atlas owner、变体、include-in-build 与资源发布仍是项目事实 |
| WCAG 2.2, SC 1.4.10 | https://www.w3.org/WAI/WCAG22/Understanding/reflow.html | `81b64fd88820df4d02821b76a451b97aedd54a62f34861c1acdedf4663a51c0e` | Reflow 约束用于校准窄视口和放大后的内容可达性；CSS 尺寸与 Unity profile 不自动换算 |
| WCAG 2.2, SC 1.4.12 | https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html | `01e89ec8a9ac83566070bb1029e040c6cabc0e89fac36a605445aaf589d6ee1e` | 文本间距变化不应造成内容或功能丢失；需用项目字体与 profile/state Fixture 验证，不可据规范直接签收 |
| WCAG 2.2, SC 1.4.1 | https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html | `1e963c78f47368c4b3e7da85dc4242ae0bfd5350b1790c68e1dd8a50f21683b3` | 颜色不能作为提示动作、状态或区分视觉元素的唯一手段 |
| Unicode UAX #9 | https://www.unicode.org/reports/tr9/ | `25ebbc5bc05ec302677ace0e3118e454e12da5e1e7ebec386aabd6e585c8cebe` | 双向文本需要 Unicode Bidirectional Algorithm；标准存在不证明 TMP、布局与输入已正确实现 RTL/Bidi |
| Unicode UAX #14 | https://www.unicode.org/reports/tr14/ | `dfa75adac235aaaf49320c955fbf48ef96acb16823ed4fb7ee8079b15b058582` | 换行机会由 Unicode Line Breaking Algorithm 定义；实际断行仍取决于引擎、语言数据、字体和项目策略 |

WCAG 的 CSS px 与项目 ScreenSpec 数值不是自动等价单位。项目当前 Validator 要求交互目标
至少 44x44，这是项目合同；本来源锁不把两者换算，也不据此宣称任一 Prefab 已达标。

## TextMeshPro 3.0.9 package source

项目 `Packages/manifest.json` 固定 `com.unity.textmeshpro` 3.0.9。已安装版本化包源码
`Library/PackageCache/com.unity.textmeshpro@3.0.9/Scripts/Runtime/TMP_FontAsset.cs` 的读取哈希为
`4944761e316e6c3e6053507283f4c1b310dd47312830346ef7efc4715ac403af`。源码公开
`fallbackFontAssetTable`；`HasCharacter`/`HasCharacters` 只有在 `searchFallbacks` 开启时才搜索
局部 Fallback，并使用已搜索实例集合避免重复搜索。它证明包能力，不证明项目已创建字体资产、
覆盖目标字形、配置全局或局部 Fallback，或拥有相应字体许可证。

预设 URL
`https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/FontAssetsFallback.html`
在本次读取中返回 HTTP 404，因此未被当作有效在线来源，也没有以模型记忆补写其内容。

## Locked decisions

- Screen family 先决定信息架构和主动作，再映射到当前注册模板；官方 Canvas/Layout 文档不提供 ES 的屏幕族或业务系统。
- 响应式设计必须显式记录 profile、Reference Resolution、宽高匹配、安全区和长内容策略；不能整体缩放一张参考图后宣称适配。
- Token 先定义语义角色，再由明确消费者映射到 Unity 字体、颜色、Sprite 或材质。DTCG 文件格式不是 Runtime 依赖授权。
- 颜色不能是 selected、focused、disabled、success/error 的唯一信号；焦点、对比与目标尺寸需要对应 profile/state 的新鲜视觉或 Runtime 证据。
- 字体必须绑定具体 TMP Font Asset、字形覆盖、Fallback 链、来源与许可证；默认字体或静默 Fallback 不能作为商业素材完成证据。
- 参考图必须记录不可变内容哈希、来源/许可证、裁剪与区域、观察、推导、假设和 review 身份；零哈希、空路径或 `placeholder` 不能标记为 `complete`。
- AssetManifest 必须把资源角色、内容哈希、provenance、许可证、import/crop/9-slice、Atlas owner 和 fallback 分开记录；`assetSlots` 或白图 fallback 不证明解析器和正式资源存在。
- BehaviorSpec 必须保留 intent、binding、状态转换、焦点/导航图和输入 modality；只投影为 `interactable` 不能证明行为闭环。
- 文本必须针对 locale、最长内容、换行、字体字形/Fallback、Bidi/RTL 和窄 profile 建立 Fixture；规范或包能力不能替代项目消费者与运行证据。

## StaleWhen

Unity、UGUI、Input System、TextMeshPro、WCAG、Unicode UAX 或 DTCG 目标版本变化；任一官方页面的最终 URL 或响应哈希变化；
本地 TMP 包版本或上述源码哈希变化；项目 ScreenSpec 单位、Validator 目标尺寸、字体/Token 消费者
、参考证据、AssetManifest、BehaviorSpec、文本/本地化消费者或证据合同变化时，本来源锁及依赖结论均标记为 stale，并重新读取一手来源。

## Non-claims

HTTP 200、页面哈希与包源码读取只证明当次静态来源。没有启动 Unity、创建或导入字体/Sprite/
SpriteAtlas/Localization 资产，没有配置或执行 EventSystem、Input Module、Selectable Navigation、
RTL/Bidi、本地化或读屏消费者，没有渲染截图、测量对比度/焦点/目标尺寸，也没有运行 PlayMode、
Profiler、Player、IL2CPP 或发布验收；这些状态统一为 `runtime-not-run`。
