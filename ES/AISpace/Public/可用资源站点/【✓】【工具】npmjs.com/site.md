# npm 公共包

- 来源类别：代码/工具
- 动态优先级：C
- 预算：10M
- 质量评分 Q：4/5
- URP 适配预期 U：2/5（需 Unity 2022.3 URP 实测）
- 可搜索：是
- 支持部分下载：是
- 推荐搜索/名称解析词：npm asset pipeline CLI package license

## 能做什么

代表性内容：包名、版本、exports、license 和依赖树；不视为 Unity 原生包。

## 标志性内容

本轮页面探针未取得可安全落盘的小样本；保留来源注册和检索策略，不声称资源包已下载。

## 使用边界

### 探索技巧（2026-08-30）

- 包元数据公开入口：`https://registry.npmjs.org/<package>`（以 `express` 探针 HTTP 200 验证）；无需登录即可读取元数据。
- 下载应优先使用包版本 `dist.tarball`，逐项流式写入并在解压后释放响应体；包许可证必须逐包复核。

批次证据：ES/AISpace/Local/ResourceSources/20260830/agent-batch-3/npm.md；统一回执：ES/AISpace/Local/ResourceSources/20260830/agent-batch-3/provenance.json。

名称只用于候选检索，不能作为唯一身份；必须结合页面标签、格式、版本、许可证和内容哈希确认。未验证 Unity 导入、运行时表现或商业再分发权。
