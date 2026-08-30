# 可用资源站点

本区是全球公开资源来源的协作注册区。每个站点一个目录，每站预算统一为 10M（预算只限制本批分析/证据规模，不代表购买额度或下载授权）。

站点目录统一命名为“无 www 前缀的完整域名【中文站名-资源类型】”，同一网站的不同资源类型保留独立目录，避免类型混账。

## 路由规则

- 动态取源先按 Priority（A → B → C），再按质量 Q、URP 预期 U、许可证清晰度、搜索能力和最近成功率排序。
- 每个站点的 site.md 同时记录用途、可搜索性、名称解析词、部分下载能力和本地标志性内容引用。
- Public 只保存索引与文本卡片；实际下载样本仍位于 ES/AISpace/Local/ResourceSources/20260830/，不自动导入 Unity。
- “可搜索”不等于一定可下载；“部分下载”不等于可重组整包。许可证必须按资产逐项确认。

## 注册总数

25 个来源，5 类各 5 个；平均每类 5 个，最大类规模不超过平均值的 3 倍。完整质量/URP/搜索/部分下载排名见 Documentation/AIKnowledge/entries/free-resource-sources.md。

## 目录

每个子目录包含一个 site.md，其中的“标志性内容”是该站点的代表性资源类型或本轮探针样本引用，不把网页快照冒充为完整资产包。

## 本轮真实网站采集

- 第 1 批：8 个站点，公开页面样本 8 份，全部小于 2 MiB，并有逐站 provenance。
- 第 2 批：8 个站点，公开页面样本 8 份，共 1,146,072 bytes；音频、纹理和 ZIP 二进制未强行下载，已标注部分下载。
- 第 3 批：9 个站点，6 个公开样本成功，3 个站点返回 403（itch.io 3D/2D、Pixabay），未绕过限制。
- 三批合计：25 个站点均有中文说明和 provenance；所有下载均记录 URL、UTC 时间、状态、SHA-256、许可证、搜索词、URP 预期和部分下载标记。

批次证据位于 `ES/AISpace/Local/ResourceSources/20260830/agent-batch-1/`、`agent-batch-2/`、`agent-batch-3/`。

## 真实资源下载批次

真实文件已按每站 10MB 上限写入以下目录：

- `real-batch-1/`：8 站检查，3 个真实文件，其他站点因直链、许可证或质量不足舍弃并记录原因。
- `real-batch-2/`：8 站各 1 个文件，共 8,258,229 bytes；最大单文件 7,797,804 bytes，未超过 10MB。
- `real-batch-3/`：9 站检查，取得 npm lodash 318,961 bytes；itch.io/Pixabay 的 403 和其他动态/不稳定来源均停止并记录。

真实批次目录：`ES/AISpace/Local/ResourceSources/20260830/real-batch-1/`、`real-batch-2/`、`real-batch-3/`。每个站点目录含中文 `site.md` 与 `provenance.json`；记录实际文件、URL、哈希、许可证、URP 判断、部分下载状态和舍弃原因。

当前下载文件只进入 Local 研究区，尚未执行 Unity 导入或正式 AssetPackage 聚合。
