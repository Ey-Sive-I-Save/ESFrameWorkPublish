# 全球免费游戏资源来源与下载探针（2026-08-30）

`KnowledgeId`: `es.resource.free-sources.v1`  
`Authority`: `bounded external-source probe + local provenance snapshot`  
`RouteKeys`: `resource`, `asset`, `free-resource`, `open-source`, `texture`, `audio`, `3d`, `2d`, `font`, `tool`, `provenance`, `license`, `download`, `resource-pipeline`  
`HashSchema`: `v2`  
`ContentHash`: `fe55196b431805cb6c8a80235cbe13ade395cbfb96eeb2e659a295bcc6a72d4f`
`SourceSetHash`: `fe55196b431805cb6c8a80235cbe13ade395cbfb96eeb2e659a295bcc6a72d4f`  
`EntryBodyHash`: `f1f13d2380ade03226b96fc50af2f24974026e64601459bcd76d91b82d55f405`  
`EvidenceLevel`: `S2`（网页可达性与本地快照；非 Unity 导入验收）  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 来源页面、许可证、下载地址、内容快照或资源收集合同变化时必须重新探测。  

## 作用

为 ES 资源收集和 AssetPackage 前置阶段提供均衡的全球公开来源导航。当前分为 5 类，每类 5 个来源，共 25 个来源；任何一类都没有超过平均来源数的 3 倍。探针只下载公开页面/元数据样本（单源最多 2 MiB），成功快照放在 `ES/AISpace/Local/ResourceSources/20260830/`，不自动导入 Unity、不进入正式 Assets。

本轮实测汇总：25 个来源均已尝试；20 个页面/元数据快照成功，5 个页面请求失败；在成功页面中另下载 14 个可公开取得的小样本。失败或找不到小样本的来源均保留原因，不伪称为完整资源包下载。

## 来源矩阵

| 类别 | 来源 | 能做什么 | 许可证/风险提示 | 探针结果 |
|---|---|---|---|---|
| 3D | Poly Haven | HDRI、PBR 材质、模型 | 站点与资产页声明 CC0；仍保存资产页证据 | downloaded |
| 3D | Kenney | 低多边形模型、原型包、道具 | 按资产页核对 CC0 | downloaded |
| 3D | Quaternius | FBX/OBJ/glTF 角色、场景包 | 多数包 CC0；逐包确认 | downloaded |
| 3D | OpenGameArt | 社区模型和游戏对象 | 每个条目许可证不同 | downloaded |
| 3D | itch.io CC0 3D | 社区 3D 包检索 | 每个包独立核对许可证 | failed（页面请求失败） |
| 纹理/HDRI | Poly Haven Textures | 高分辨率 PBR、HDRI | CC0 | downloaded |
| 纹理/HDRI | ambientCG | PBR 材质、HDRI | 站点声明 CC0 | downloaded |
| 纹理/HDRI | ShareTextures | 纹理和材质参考 | 逐资产确认 | downloaded |
| 纹理/HDRI | 3DTextures.me | 免费 PBR 纹理 | 逐资产确认 | downloaded |
| 纹理/HDRI | TextureCan | 纹理与材质浏览 | 逐资产确认 | downloaded |
| 音频 | Kenney Audio | UI、环境、游戏音效 | 按资产页核对 CC0 | downloaded |
| 音频 | Freesound | 音效采样和环境声 | 只选 CC0/CC-BY；禁止默认认为全站同一许可证 | downloaded |
| 音频 | OpenGameArt Audio | 音效和音乐 | 每个条目许可证不同 | downloaded |
| 音频 | Pixabay Sound Effects | 音效检索 | Pixabay Content License，逐项核对 | failed（页面请求失败） |
| 音频 | Mixkit Sound Effects | 音效素材 | Mixkit 许可条款，逐项核对 | downloaded |
| 2D/UI/字体 | Kenney 2D | UI、图标、精灵、原型图 | 按资产页核对 CC0 | downloaded |
| 2D/UI/字体 | OpenGameArt 2D | 精灵、瓦片、UI | 每个条目许可证不同 | downloaded |
| 2D/UI/字体 | itch.io CC0 2D | CC0 2D 包检索 | 每个包独立核对许可证 | failed（页面请求失败） |
| 2D/UI/字体 | Google Fonts | 可嵌入字体与字体元数据 | OFL/Apache，按字体核对 | downloaded |
| 2D/UI/字体 | Font Awesome Free | 图标与字体 | CC BY 4.0/SIL OFL/MIT 按组件区分 | downloaded |
| 代码/工具 | GitHub | 开源代码、Shader、工具仓库 | 必须读取仓库 LICENSE | downloaded |
| 代码/工具 | OpenUPM | Unity 包索引和安装元数据 | 按包和上游仓库核对 | downloaded |
| 代码/工具 | Unity Asset Store 免费工具 | Unity 编辑器/工具包检索 | Asset Store 条款和具体包许可 | failed（页面请求失败） |
| 代码/工具 | Godot Asset Library | 开源工具和插件参考 | 按资产许可证核对，不能直接当 Unity 包 | downloaded |
| 代码/工具 | npm 公共包 | 前端/工具依赖和源码参考 | 必须读取 package license 与依赖许可 | failed（页面请求失败） |

## 下载与放置规则

- `downloaded` 只表示公开页面/元数据快照成功，不表示二进制资产已获得再分发权。
- 通过探针的文件位于 `ES/AISpace/Local/ResourceSources/20260830/<category>/`，属于本地研究/待筛选内容，不是正式 Unity 资产。
- 真正下载 ZIP、FBX、PNG、WAV、字体或 UnityPackage 前，必须定位具体资产页、保存许可证和作者信息，再进入 `ES/AISpace/Local` 的待审目录；许可证不清晰的来源进入 Quarantine。
- 只有完成来源、哈希、许可证、依赖和目标路径审查并获得导入授权，才能交给 `es-resource-collection` 和 AssetPackage；本条目不授予导入、移动、发布或运行时权限。

## 失败面与恢复

- 页面可达但下载按钮需要登录、验证码或动态令牌：标记 `needs-manual-download`，不绕过认证。
- 来源许可混杂：按单项许可证拆分，禁止把“免费站点”升级为“全部可商用”。
- 下载内容为二进制或未知格式：只保留哈希和元数据，不执行、不导入。
- URL、页面内容或许可证漂移：旧快照标记 `stale`，重新探测后再更新条目。
- 重复来源跨类别出现（例如 Poly Haven、Kenney、OpenGameArt）：来源实体可复用，但每类用途和资产许可证仍独立记录。

## 质量、URP 与动态取源注册表

评分口径：`Q` 为公开内容质量/整理度（1–5），`U` 为 URP 适配预期（1–5；最终仍需在 Unity 2022.3 URP 中导入验证），`S` 表示能否按名称、标签或 API 搜索，`P` 表示是否支持单项/分辨率/格式等部分下载。`Priority` 是后续动态取源顺序：`A` 优先、`B` 次选、`C` 仅作补充。排名是资源收集路由权重，不是对许可证或商业可用性的承诺。

|排名|来源|类别|Q|U|S|P|Priority|用途与名称分析提示|
|---:|---|---|---:|---:|:--:|:--:|:--:|---|
|1|Poly Haven|3D/纹理|5|5|是|是|A|优先解析 `assetType + subject + biome + quality`，API/标签清晰|
|2|ambientCG|纹理/HDRI|5|5|是|是|A|解析 `material + surface + mapSet + resolution`，优先 PBR 集合|
|2.5|Poly Haven Textures|纹理/HDRI|5|5|是|是|A|解析 `material + surface + mapSet + resolution`，与 Poly Haven 模型源分开记账|
|3|Kenney|3D/2D/UI/音频|5|5|是|部分|A|解析 `pack + genre + platform + assetType`，适合原型和 UI|
|4|Quaternius|3D|4|4|是|部分|A|解析 `character/prop/environment + style + format`，检查材质管线|
|5|Google Fonts|字体|5|4|是|是|A|解析字体族、字重、语言子集；导入前核对 OFL/Apache|
|6|Font Awesome Free|2D/UI|4|4|是|是|B|解析 `icon name + style + package`，区分字体、SVG、PNG|
|7|Freesound|音频|4|3|是|是|B|解析 `foley/ambience/ui + duration + sampleRate`，逐条核对许可证|
|8|OpenGameArt|3D/2D/音频|3|3|是|部分|B|解析 `category + engine + format + license`，社区条目必须逐项审查|
|9|GitHub|代码/工具|5|4|是|是|B|解析仓库主题、Unity 版本、UPM 路径、LICENSE 和 release|
|10|OpenUPM|代码/工具|4|5|是|是|B|解析包名、版本、Unity 兼容范围和依赖闭包|
|11|Mixkit Sound Effects|音频|4|3|是|部分|B|解析 `effect + category + format`，先看下载条款|
|12|3DTextures.me|纹理|3|4|是|部分|B|解析 `surface + maps + resolution`，确认法线/粗糙度通道|
|13|ShareTextures|纹理|3|4|是|部分|B|解析材质名称与 map 集合，注意下载粒度和许可证|
|14|TextureCan|纹理|3|3|是|部分|B|解析 `material + seamless + map type`，先保存页面证据|
|15|Kenney Audio|音频|5|4|是|部分|B|解析包名、音效类别和单文件索引|
|16|Kenney 2D|2D/UI|5|5|是|部分|B|解析 pack、tile、UI、icon 和分辨率|
|17|Godot Asset Library|代码/工具|3|2|是|部分|C|可搜索插件，但必须把 Godot API 依赖标为迁移风险|
|18|itch.io CC0 3D|3D|3|3|是|部分|C|可按标签搜索；页面或下载失败时不得推断许可证|
|19|itch.io CC0 2D|2D/UI|3|3|是|部分|C|可按标签搜索；逐包保存作者和许可证|
|20|Pixabay Sound Effects|音频|3|2|是|部分|C|可搜索；下载条款需逐项确认|
|21|Unity Asset Store 免费工具|代码/工具|4|5|是|部分|B|可按 Unity 版本/URP/Editor 标签搜索，受商店条款约束|
|22|npm 公共包|代码/工具|4|2|是|是|C|解析包名、版本、exports、license 与依赖树，不视为 Unity 原生包|
|23|OpenGameArt Audio|音频|3|3|是|部分|B|解析音频类别、格式、时长、许可证|
|24|OpenGameArt 2D|2D/UI|3|3|是|部分|B|解析 sprite/tile/UI、尺寸、引擎标签和许可证|

### 动态取源决策

1. 先按 `Priority`，再按 `Q`、`U`、许可证清晰度、可搜索性和最近成功率排序；同分时优先已有本地快照且哈希未漂移的来源。
2. URP 资源优先顺序：Poly Haven/ambientCG 材质与 HDRI → Kenney/Quaternius 模型包 → Unity Asset Store URP 标签工具；Godot、npm 只能作为迁移参考。
3. 资源名称不得直接当作唯一身份。先拆分名称中的 `类型、主题、风格、平台/引擎、渲染管线、格式、分辨率、许可证`，再与页面标签、文件扩展名和包元数据交叉验证。
4. `S=是` 只代表站点具备页面搜索、标签搜索、索引或 API；不代表搜索结果一定可下载。`P=是/部分` 代表可尝试单项、格式、分辨率或文件级下载；`部分` 不代表整包可重组或许可证自动继承。
5. 每次动态取源都记录 `sourceId、排名快照、queryTokens、scanUtc、pageStatus、assetAttempt、licenseEvidence、sha256`；失败来源进入下一轮重试队列，不提升为已验证来源。

## SourceRefs

- `ES/AISpace/Local/ResourceSources/20260830/resource-source-probe.json` (`d9ced93234d69a6e3ea2c6e38bb0a3caefa5d353c20013d68300670e1bac1920`)
- `.agents/skills/es-resource-collection/SKILL.md` (`bd996d232a407511e2c5262ba6ff9ae8b0baa26624cd460ebd40f64abdd11bf1`)
- `.agents/skills/es-knowledge-creator/SKILL.md` (`bb2d2869573f9468db36afa74b8d86ee928987ae0e297dc46b858f71f8876ad7`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`4f490eba9b41f2d513a6a2bfde359d73a1182c16208bd0a01d276aefe447dfe1`)
- `ES/AISpace/README.md` (`278b6f0597bb09ee2335b376b13805ebb420f6d4204fd9772fffc3671b2d4f51`)

## EvidenceRefs

- `resource-source-probe.json`: 25 个来源的 URL、检索时间、状态、快照路径、字节数和 SHA-256。
- `runtime-not-run`: 未执行 Unity 导入、AssetDatabase、Player 或发布验证。

## Non-claims

本条目不声称任何来源的全部内容均为 CC0、不声称下载成功等于可商用、不声称网页快照等于 Unity 兼容、不声称已完成资源导入或发布验收。
