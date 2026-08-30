# Google Fonts

- 来源类别：字体
- 动态优先级：A
- 预算：10M
- 质量评分 Q：5/5
- URP 适配预期 U：4/5（需 Unity 2022.3 URP 实测）
- 可搜索：是
- 支持部分下载：是
- 推荐搜索/名称解析词：Google Fonts Chinese UI variable font

## 能做什么

代表性内容：可嵌入字体族、字重和语言子集元数据。

## 标志性内容

原始 fonts.google.com 下载端点返回非 ZIP 内容（拒绝原因：下载端点需要页面会话）；随后改用官方 `google/fonts` GitHub raw 端点，实测下载 11 个 TTF 到 `字体/`，本轮探查 2,110,480 bytes，临时压缩包为 0。

## 探索技巧（2026-08-30）

- 直接文件端点：`https://raw.githubusercontent.com/google/fonts/main/ofl/<family>/<file>`；仓库目录名使用小写 family。
- 变量字体文件名包含方括号，URL 中必须编码为 `%5B`、`%5D`；缺失文件返回 HTTP 404，应记录为候选拒绝而不是重试放大。
- 许可证需回读对应 OFL 目录的 `OFL.txt`；本轮仅验证下载可达性，未宣称许可证闭环。

## 使用边界

批次证据：ES/AISpace/Local/CodexSessionTasks/20260830/google-fonts-probe/；可见资源位于本目录 `字体/`。

名称只用于候选检索，不能作为唯一身份；必须结合页面标签、格式、版本、许可证和内容哈希确认。未验证 Unity 导入、运行时表现或商业再分发权。
