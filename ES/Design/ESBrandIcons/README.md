# ES Brand Icons · Review Batch 02

状态：待审阅视觉资产，不作为现行运行时或 Unity 导入资源。

这批图标统一采用 24×24 viewBox、`currentColor`、1.65px 圆角描边，并加入可独立调节透明度的主轮廓、结构层和强调面。目标尺寸为 16px 页签、24px 工具栏和 32px 菜单；在 16px 下仍保留一个可识别的主轮廓，不堆叠无法辨认的小细节。

视觉层级：

- `.p`（primary）：语义主轮廓，默认完整不透明。
- `.s`（secondary）：内部结构，默认 0.62 不透明度。
- `.a`（accent）：小面积强调面，默认 0.18 不透明度。

三层都以 `currentColor` 为默认来源，因此可以直接由 `GUI.color`、USS 或宿主的品牌 Token 染色；状态色（成功/警告/错误）仍由 ES Presentation Token 负责，图标文件不内置状态语义。

SVG 作为外部图片加载时，宿主负责提供最终颜色；作为内联 SVG 时，宿主也可以把三层映射到自己的颜色 Token。当前文件保持 `currentColor` 兼容，不绑定某一套运行时 CSS 变量命名。

文件：

- `workbench.svg` ES 工作台
- `content.svg` 内容制作
- `config.svg` 项目配置
- `assets.svg` 资源管理
- `build.svg` 构建与发布
- `diagnostics.svg` 验证与诊断
- `automation.svg` 自动化
- `agent.svg` Agent 协作
- `scene.svg` 场景与对象
- `data.svg` 数据与表格
- `font.svg` 字体与 UI
- `audio.svg` 音频与音效
- `graph.svg` 图与流程
- `inspector.svg` Inspector 与属性
- `settings.svg` 编辑器设置
- `package.svg` 依赖与集成

接入前必须通过深色/浅色皮肤、默认/悬停/选中三种状态、16px 小尺寸、窄页签、Unity SVG 导入和版权来源复核。
