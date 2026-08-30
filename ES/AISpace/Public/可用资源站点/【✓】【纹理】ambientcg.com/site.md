# ambientCG

- 来源类别：纹理
- 动态优先级：A
- 预算：10M
- 质量评分 Q：5/5
- URP 适配预期 U：5/5（需 Unity 2022.3 URP 实测）
- 可搜索：是
- 支持部分下载：是
- 推荐搜索/名称解析词：ambientCG wood siding PBR 4K URP

## 能做什么

代表性内容：Bricks097/Bricks104 类 PBR 材质集合，含 BaseColor/Normal/Roughness 等贴图。HDRI 未纳入本批次，不在站点类型声明中。

## 标志性内容

本轮已取得代表性页面/小样本引用：ES/AISpace/Local/ResourceSources/20260830/samples/textures_ambientCG.png。该文件属于 Local 研究快照，需完成许可证、哈希、依赖和 Unity 导入审查后才能进入正式资产链。

## 使用边界

## 探索技巧（2026-08-30）

- 站点首页 `https://ambientcg.com` 可通过 HEAD 访问（HTTP 200，nginx）；本次未请求大体积目录接口。
- 资产下载优先使用站点公开 API 返回的固定 1K-JPG URL；按单个材质包下载并在本地解压，避免一次性拉取 4K/8K 包超过 10MB 预算。
- HDRI 与 PBR 纹理需分别检索；本批次只验证纹理，未将 HDRI 计入支持类型。
- 探查证据为短响应状态，不代表 Unity/URP 导入已通过。

批次证据：ES/AISpace/Local/ResourceSources/20260830/agent-batch-1/ambientcg/（含中文 site.md 与 provenance.json；若存在则含公开页面/样本文件）。

## 本轮真实下载（优先队列第 2 站）

通过 ambientCG API 获取 2 个公开 1K-JPG PBR 压缩包并解包，得到 14 张真实贴图，下载压缩总量 9,424,436 bytes，未超过本站 10MB 预算。资源可用于 URP Lit：Color 对应 Base Map，NormalGL 对应 Normal，Roughness/AmbientOcclusion/Displacement 用于后续材质配置。

传输用压缩包 `Bricks104_1K-JPG.zip`、`Bricks097_1K-JPG.zip` 已完成解包并清理，不在站点目录中长期保留。当前只保留解包后的 14 张 Color、NormalDX/NormalGL、Roughness、AmbientOcclusion、Displacement 等真实文件；具体文件名保留原站命名，便于回溯。

来源和解包过程已记录在同目录 `provenance.json`；仅使用公开下载接口，没有登录、验证码或动态令牌绕过。当前仍未执行 Unity 导入、贴图压缩设置或 URP 运行时验证。

名称只用于候选检索，不能作为唯一身份；必须结合页面标签、格式、版本、许可证和内容哈希确认。未验证 Unity 导入、运行时表现或商业再分发权。
