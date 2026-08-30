# GitHub

- 来源类别：代码/工具
- 动态优先级：B
- 预算：10M
- 质量评分 Q：5/5
- URP 适配预期 U：4/5（需 Unity 2022.3 URP 实测）
- 可搜索：是
- 支持部分下载：是
- 推荐搜索/名称解析词：GitHub Unity URP ShaderGraph package

## 能做什么

代表性内容：Unity Shader、UPM 工具仓库和 release 包；必须读取 LICENSE 与 Unity 版本。

## 标志性内容

本轮已取得代表性页面/小样本引用：ES/AISpace/Local/ResourceSources/20260830/samples/code-tools_GitHub.png。该文件属于 Local 研究快照，需完成许可证、哈希、依赖和 Unity 导入审查后才能进入正式资产链。

## 使用边界

批次证据：ES/AISpace/Local/ResourceSources/20260830/agent-batch-2/github/（含中文 site.md 与 provenance.json；若存在则含公开页面/样本文件）。

名称只用于候选检索，不能作为唯一身份；必须结合页面标签、格式、版本、许可证和内容哈希确认。未验证 Unity 导入、运行时表现或商业再分发权。

## 探索技巧（2026-08-30）

- 公开仓库元数据可用 `https://api.github.com/repos/<owner>/<repo>` 获取；请求需带 User-Agent，单站点预算内顺序执行。
- 该端点用于候选筛选，不等于许可证或代码可再分发证明；必须逐仓库读取 LICENSE。
- 不存在的仓库返回 HTTP 404（本轮 3 个候选拒绝），不应继续盲目重试；API 限流返回 403/429 时记录 `rate-limited` 并延后。
