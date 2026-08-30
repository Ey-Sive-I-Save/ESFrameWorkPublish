# ES·3A 个性化 Unity 渲染架构总纲（2022.3 / URP 14）

`KnowledgeId`: `es.engineering.aaa-rendering-architecture.v1`  
`Authority`: 当前 ES 渲染源码、质量/URP 配置、AIWarnings、渲染证据合同与 Unity 2022.3 官方校准快照  
`RouteKeys`: `unity`, `rendering`, `aaa-rendering`, `rendering-architecture`, `quality-tier`, `urp`, `shader-variant`, `srp-batcher`, `gpu-instancing`, `lighting`, `shadow`, `post-processing`, `material-property-block`, `rendering-evidence`, `performance-budget`  
`EvidenceLevel`: `S1`（静态架构知识；运行证据仍为 `runtime-not-run`）  
`ContentHash`: `bf819947671087cfa7f3dc248182b8dc6b9d24eb8904107ebf261655e5ff39a7`
`StaleWhen`: Unity/URP 版本、Quality/Graphics/Renderer 配置、Composite Shader 参数或 ShaderGUI、渲染 AIWarnings、官方校准快照、证据合同或任一 SourceRef 哈希变化。

## 一句话定位

ES 不是“堆特效的万能 Shader”，而是一条**可仲裁的表现管线**：内容身份 → 受管材质参数 → URP Renderer → 画面证据。3A 感来自稳定的光影层次、材质响应、镜头节奏和可读性；ES 个性来自“状态驱动的 Composite 表现 + 三档可解释预算 + 不旁路的所有权”。

风格模板由纯数据 `ESRenderStylePreset` 提供，首批覆盖 Natural PBR、Stylized Toon、Noir Contrast、Neon Sci-Fi、Fantasy Atmosphere 与 Mobile Flat；`ESRenderStyleCatalog` 负责稳定顺序、按质量档首选回退和重复/非法模板校验。`ESRenderSceneIntent` 再把战斗、探索、菜单、过场、社交、拍照等场景语义映射到首选风格与透明预算缩放，`ESRenderPlatformProfile` 提供桌面、主机、移动端与 WebGL 的质量上限及预算缩放，`ESRenderFeatureRecipe` 将 SSAO、Bloom、Decal、屏幕空间阴影、体积效果和阴影级别拆成可组合配方，`ESRenderMaterialRecipe` 覆盖 Opaque、AlphaClip、Transparent、Additive 表面模型及法线、金属度、粗糙度、发光和描边约束，`ESRenderLightingRecipe` 覆盖主光/附加光、实时/烘焙/混合阴影、级联、软阴影与反射探针，`ESRenderEffectsRecipe` 覆盖透明、粒子、Decal、后处理和 Shader Variant 预算，`ESRenderConfigurationResolver` 最终合并这些配方并输出可审计的降级标记与预算结果，`ESRenderTemplateBundle` 为全部配方提供版本化身份、URP/Unity 兼容范围和可发布目录。模板只绑定质量意图和受限数值，不依赖 MonoBehaviour，后端写入与 GameManager 注册保持解耦。

除首批六种风格外，目录现已扩展 Retro Pixel、Horror Grit、Cozy Pastel 与 Tactical Realism，覆盖复古像素、恐怖写实、温馨低对比和战术写实等商业内容类型；它们沿用同一套 URP 配方与平台降级合同。

## 内容类型到场景模板

`ESRenderContentTypeCatalog` 提供 Action、RolePlaying、Strategy、Horror、Cozy、Racing、Simulation、Stylized 八类内容入口。每类只声明首选风格、默认场景意图、透明/粒子预算缩放与读图优先级；`ESRenderSceneTemplateCatalog` 再把内容类型解析为稳定的 Renderer、Material、Volume、Shader 资源绑定，`ESRenderSceneTemplatePlanFactory` 将平台质量上限和内容预算合并为可审计计划。这样新项目只需选择内容类型，ES 即可生成一套可回退的 URP 场景起点，不要求学习 Unity 原生菜单，也不把内容逻辑耦合到 MonoBehaviour。

| 内容类型 | 首选风格 | 默认场景意图 | 重点控制 |
|---|---|---|---|
| Action | StylizedToon | Combat | 轮廓、受击反馈、粒子峰值 |
| RolePlaying | NaturalPbr | Exploration | 材质层次、昼夜与空间氛围 |
| Strategy | TacticalRealism | Exploration | 信息密度、对比度、低透明噪声 |
| Horror | HorrorGrit | Cinematic | 阴影、雾、暗部读图与峰值 Bloom |
| Cozy | CozyPastel | Social | 柔和对比、暖色曝光、低压迫感 |
| Racing | NeonSciFi | Combat | 速度感、发光边界、透明预算 |
| Simulation | NaturalPbr | Exploration | 稳定批处理、长时运行预算 |
| Stylized | StylizedToon | PhotoMode | 色块、描边、镜头展示与截图一致性 |

内容类型不是新的渲染后端；它只是 ES 的可组合输入。任何平台或质量档都必须经过同一 `ESRenderConfigurationResolver`，超出透明、粒子、Decal 或 Variant 上限时产生可解释降级，而不是静默改变风格身份。

## 权威数据流

```text
Entity/Skill/Weapon/VFX 状态
  -> ES Definition / Operation / ScreenSpec
  -> ESCompositeShaderParameters（属性 ID、质量 Keyword、MPB/材质实例）
  -> Material / Shader Variant
  -> URP Asset + RendererData + Volume
  -> Camera/Frame Debugger/Profiler 证据
```

- `GraphicsSettings.asset` 只决定项目级 SRP 入口；`QualitySettings.asset` 决定当前质量档和每档 URP Asset。
- URP RendererData 负责 RendererFeature 与渲染资源组合；不要在业务脚本中偷偷增加第二条 RenderFeature/后处理路径。
- Composite Shader 的 2D、3D Lit、3D VFX、UI 四类职责保持分离；连续参数走属性/受管 MPB，离散分支才使用有限 Keyword。
- Renderer 的 MPB 是对象参数所有权工具，不是性能保证；UGUI 使用受生命周期管理的运行材质实例。
- 相机仍由 `ESCameraModule → ESCameraDirector → ESCameraCinemachine2ViewAdapter` 仲裁；渲染架构不得直接写 VCam 或复制 Camera Controller。

后端质量切换必须遵循受证据约束的事务链：`ESRenderIntent → ESRenderQualityPolicy → ESRenderBackendChangePlan(dry-run) → ESRenderBackendApplyGate → ESRenderBackendApplySession → ESRenderBackendUnityWriter → ESRenderBackendReceipt → ESRenderBackendEvidenceReceipt`。Apply 与 Rollback 都只能在 Gate 已通过、幂等键匹配且状态前提满足时执行；门禁会保留并精确匹配该幂等键，禁止把其他请求的 Ready 状态复用到当前请求。ApplySession 每个实例只允许一次执行尝试，且无论成功、失败还是需要回滚都会消费会话。Editor-only Writer 只按已映射质量名调用 `QualitySettings.SetQualityLevel`，Apply 后必须重新捕获真实快照；`EvaluateApply` 未得到目标质量和管线状态时只能进入 `RollbackRequired`。Rollback 入口会先确认当前快照仍是已应用目标，再写回基线并由 `EvaluateRollback` 逐字段匹配 Apply 前快照；不能用调用方自报成功替代证据。`ESRenderBackendResourceSnapshot` 以只读方式记录 RenderPipelineAsset 与 RendererData 身份，并按当前项目 URP 包版本计算兼容状态；Unity 6 只输出 `ForwardCandidateUnverified`，Built-in/HDRP 输出拒绝。`ESRenderVolumeResourceSnapshot` 以只读方式记录 VolumeProfile 资产库存指纹，`ESRenderShaderResourceSnapshot` 记录 Shader 资产及 Keyword 空间指纹；这些库存/身份事实都不代表场景实际生效、编译 Variant 数量或视觉验收，身份未暴露时保持“未知”而不是伪造名称。`ESShaderVariantCompileLogParser` 仅接受调用方提供的受限日志文本，限制字符/行数并统计可识别的 Variant、Keyword、错误、警告与未解析行；它不执行编译器、不读取任意路径，`runtimeAcceptance` 永远为 false，未识别格式不得推断编译 Variant 数量。EvidenceReceipt 会绑定可用资源身份字段，仅记录操作、幂等键、计划状态和后端回执，`runtimeAcceptance` 固定为 false，不能冒充运行时验收；未来落盘必须先通过 `ESRenderEvidencePathPolicy`，仅允许项目根 `ES/Output/RenderingEvidence/*.json`。

编辑器用户入口固定为 `ESUrpRenderControlWindow`：只显示 ES 语义质量档、后端快照、dry-run 与受门禁的应用动作，不要求用户学习 Unity 原生 Quality/Graphics 菜单。

## ES 三档画质：不是“高/中/低开关”

| 档位 | ES 语义 | 适合的成本旋钮 | 必须保持的画面个性 |
|---|---|---|---|
| Performant | 轮廓优先、稳定响应 | 低阴影/低 AA、无额外 RendererFeature、较低 LOD/粒子预算 | 颜色分级、关键边缘/受击反馈、角色轮廓 |
| Balanced | 玩法与氛围平衡 | 有界 SSAO、标准阴影、有限透明/VFX、稳定动态分辨率策略 | 材质层次、空间接触、镜头节奏 |
| High Fidelity | 电影化局部峰值 | 高阴影/AA、SSAO 高采样、反射/体积/后处理按场景启用 | 高光、细节、体积感；不得牺牲读图 |

当前静态事实：项目存在 `Performant`、`Balanced`、`High Fidelity` 三档；`QualitySettings.m_CurrentQuality=0` 指向 Performant；Balanced/High Renderer 含 SSAO，High 还含更多高质量渲染资源。`ESRenderBackendSnapshot` 同时记录捕获时的质量档数量，旧构造函数无法提供该事实时保持 `0`（未知），不将未知误判为缺失。以上是配置事实，不是成本或帧率证明。

## 3A 画面拆解与 ES 个性化方案

1. **形体层**：角色/武器轮廓、法线与阴影先保证可读；不以 Bloom 或颜色掩盖几何错误。
2. **材质层**：Base/Normal/Mask/Emission 等连续参数由 Composite 参数表驱动；效果顺序固定、可回放。
3. **状态层**：冻结、灼烧、溶解、受击等由 Entity/Operation 状态映射到已有参数，不新增平行状态系统。
4. **空间层**：SSAO、阴影、雾、后处理只由 URP Asset/Renderer/Volume 组合控制；按档位和场景预算启用。
5. **镜头层**：FOV、肩部偏移、Shake、遮挡回缩走 Camera Request/Modifier，形成 ES 的“状态—镜头—材质”联动，而非脚本直写相机。
6. **读图层**：UI/战斗反馈保留对比度和遮挡预算；任何视觉增强都必须有无效果基线和单变量对照。

## 性能与批处理决策

- SRP Batcher 优先用于共享 Shader Variant 的大量 Mesh；自定义 Shader 需满足 `UnityPerDraw`/`UnityPerMaterial` 常量缓冲区约束。
- 使用 MPB 会退出 SRP Batcher；大量完全相同 Mesh/材质时才考虑 GPU Instancing，并用同输入 Profiler A/B 决策。
- Shader Variant 只保留真正改变编译路径的离散选项；质量档必须同步 `_QualityTier` 与互斥 Keyword，不能只改浮点。
- Draw Call、SetPass、Canvas rebuild、Render Thread、GPU、GC 和显存分别记录；不能以“同图集/同材质/Inspector 可见”替代 Frame Debugger/Profiler。
- 每档必须定义首次、预热、稳态、扩容四阶段；没有目标平台、输入规模、重复次数和基线时，结论为 `Deferred`。

## 关键失败面（最小矩阵）

| failureId | 触发/症状 | 预防与恢复 | 缺失证据 |
|---|---|---|---|
| `quality-keyword-drift` | 档位字段变了但 Keyword/Variant 未同步 | 统一参数 API；检查材质 Keyword 与构建日志 | Unity 导入/Player Variant 日志 |
| `mpb-srp-regression` | MPB 参数正确但 CPU Render Thread 变慢 | MPB、受管材质、Instancing 同输入 A/B；保留 Owner 语义 | Frame Debugger + 目标平台 Profiler |
| `renderer-feature-budget-overrun` | Balanced/High 的 SSAO/后处理造成峰值超预算 | 每档限制 Feature、分辨率、采样和启用场景；超预算回退上一档 | 目标平台 GPU/CPU 峰值 |
| `material-instance-leak` | UGUI 切换后材质实例持续增长 | 源材质/Shader 变更时重建，Disable/Destroy 释放 | 重复进出场景的内存趋势 |
| `evidence-overclaim` | 代码/测试存在被写成 3A 或性能已达标 | 交付按 S1/Runtime 分层；无运行证据保持 `runtime-not-run` | PlayMode、Profiler、Player 证据 |

## 最小验收矩阵

静态：Quality/Graphics/Renderer 引用闭合；Composite 参数、Shader 属性、Keyword、ShaderGUI、材质 Owner 和测试定义闭合；SourceRef 与 ContentHash 可重算。`ESRenderBudgetEvaluator` 对透明对象、粒子系统、编译 Variant 与帧时间执行确定性上限比较；它只裁决输入快照是否越界，不生成采样数据，也不把静态结果升级为 Profiler/Player 验收。  

批次回归必须使用 `ESRenderEvidenceBatch`、`ESRenderEvidenceBatchDiff` 与 `ESRenderEvidenceBatchBudgetAudit`：先按幂等键去重，再比较 URP 资源身份和性能字段；缺少有效采样时标记为未测量，不能判定通过；任何 `runtimeAcceptance=true` 回执不得进入静态批次。`ESUnityProfilerMetricSource` 只接受调用方显式传入的六类 Profiler 标记名，统一将 CPU/GPU 纳秒转换为毫秒并提交给 `ESRenderMetricCaptureSession`；计数器无效、标记缺失或会话未达到精确样本数时保持失败/未测量，不回填零值。`ESRenderEvidenceReport` 聚合单批次差异、预算和场景摘要；`ESRenderEvidenceAggregateReport` 再聚合多份报告，并以未证明、超预算、漂移优先级裁决总体状态，禁止用稳定子报告掩盖风险报告。
Unity：三档实际生效、RendererFeature/Volume/Shader 导入无错误。  
视觉：同场景同相机下，轮廓、材质层次、状态反馈、镜头与 UI 读图通过截图/录屏对照。  
性能：目标平台预热后稳态 Profiler + Frame Debugger，单变量比较 MPB/材质/Instancing、Quality/Keyword、RendererFeature。  
发布：仅在 Player/IL2CPP/发布合同和真实回执齐全时提升为 `Accepted`/`Released`。

## Non-claims

本条目不声明项目已达到 3A 画质、任何档位帧率达标、实际合批、0 GC、Variant 已剥离、视觉正确、Player/IL2CPP 或发布通过；这些均需对应 Unity/Runtime/Profiler/Release 证据。

## SourceRefs

+ `ProjectSettings/GraphicsSettings.asset` (`8a16c389ea5918a432aa1ab553520c770c01ed9b3b3939435342c2efbb54ed91`)
+ `ProjectSettings/QualitySettings.asset` (`4a324b8ce9543ba0e586057769b5c1ad7d090acd8ad11be6f9d373556a3204f5`)
+ `Assets/Settings/URP-Performant-Renderer.asset` (`11d4463a714b8572792a6818dece9e3c84ee2635dff9048575155b064e056bb9`)
+ `Assets/Settings/URP-Balanced-Renderer.asset` (`8ddaba42cf10b34a3797d185fb1755dd65bf9bcac2d31af190d0ad465e29cb57`)
+ `Assets/Settings/URP-HighFidelity-Renderer.asset` (`cc8be915944c9c27c3478f9931dc0baaf7c3f619cad063a9530ea90af8b542f4`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESCompositeShaderParameters.cs` (`582012f2a6554d29de98ddd24b4e1ef21b13f5df462d1ab2a78fb3886a5dfc37`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderQualityPolicy.cs` (`e4166df4db7e7d7996c9189832c1d6d3d406c65137a0316bb377b72e3f740897`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderStylePreset.cs` (`12337e5dcdfabb07565cfc13941b138a6e2ee0e707362b2a5f829e1164b2b02e`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderStyleCatalog.cs` (`1a82d4e99b65bce5a94d548d231a4ac400e5ba3eef21a700d919eb26adbe7b2b`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderContentTypeProfile.cs` (`a546bfb951b0a40bef0ffbaf3efd228080e2d129e8f50a30edefbd8d326bba11`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderContentTypeManifest.json` (`b8754aa0bf5bb5273923ac466fead0a2eed65be38ac0f8fb3c53631169be9294`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderSceneTemplateManifest.json` (`965bc5537a0b95a73b0bbe574673877486a97de321e1d86f01f41fe4e7cb0e86`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderSceneIntent.cs` (`a0414dfc481469c668f93dd1ca2704f54c645f514d73429d5a685fc2998661e6`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderPlatformProfile.cs` (`8ca411a6b5ff67499d706944aab3591254a53a55f03f9d50137330a2fa9f74fd`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderFeatureRecipe.cs` (`ff476688b3717753e55c7e0e95c629d9a3898d85e02c911c090c415c412e52a7`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderConfigurationResolver.cs` (`84a3ffa91cb1ac63bccc4ec456dd9ce4e15ec83ad193d24685117752c9124a38`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderMaterialRecipe.cs` (`e6a5f9a62d8f085389c0aa39a24531046d3928970980fb7bea90a7fea75135db`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderLightingRecipe.cs` (`d8644d326043561a0d749f689c7ce806ff453cc58f76fef9c1679796f1b70085`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEffectsRecipe.cs` (`bd84faa93115102194aefcbc198b7324f3d717f86a98101db70df787ec87b27f`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateBundle.cs` (`c78fbc7f77c96ca4db231f0e91898717bda5c1b21bcf12a0992548df77837095`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateCatalog.cs` (`c5f77a2abf83c9ad7a066a30858f780489d4a106d8dcf6e2ff3dddc89fe5303a`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateResourceMap.cs` (`6442e5453a41673691b91a264655bc38af312e797b5af437132d031ebad6af6f`)
+ `ES/Tools/Validation/Test-ESRenderTemplateResourceClosure.ps1` (`5c375f66afcd254cd8af6e65a6a68526fb8763bb9b4702c6c8b7f9f779d26c09`)
+ `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRenderModule.cs` (`e5778c06b56d47574c23deeee17e8f2cf8290a116fa2ccdcdb991fcaa2cca14a`)
+ `Assets/Plugins/ES/Editor/ESShader/Tests/ESRenderModuleContractTests.cs` (`867e3367323786cd1b3e9625f02a1b590784e7bfdeb9180ff7400ee89dffc3eb`)
+ `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs` (`3b03a7f0c238e822ca773199b36a2cb15dd8597c893055804307afe8de9e5b6a`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateManifest.json` (`4dd279fb95f67edfffa609749a02a1506aff5b03f59e364cad9db1e08bfd488b`)
+ `Assets/Plugins/ES/0_Stand/Rendering/ESStyleLit.shader` (`e7d5c9eca217de9f8a48287a5ee81df6f0707aabd2c420161397d988d701dc3f`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-NaturalPbr.mat` (`57442089ffd143ab7e39f98691580d030869dcaad4333e1fbd2972968da0493a`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-StylizedToon.mat` (`9bb6d3b489a24461257f92f7dacda8afa899b00cc8f4e4fe7315ca7f26f2cfd3`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-NeonSciFi.mat` (`3ef92a7873c95e1ee3194bc4256ec58f71215162e1f787dbf091631beabb33c7`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-NoirContrast.mat` (`5a82a84e6ca2d6ede2417fac9ce68d50cebdcb50677ac090cee1c76252fecc1d`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-FantasyAtmosphere.mat` (`2d12006d4037d9d02a7049315c343270a1e6cde30e8b4ac68fb3d8d023d82f0a`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-MobileFlat.mat` (`0c78132b13157e86cfff608168f306388191e9a3046b06ee9659c2281a345f38`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-RetroPixel.mat` (`a805acd61a8138745f12bdfa9ea1732552f81c4e73e70a46af919631a415470c`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-HorrorGrit.mat` (`7b7125d9c37a6e0f1108e355429338124f6e54ac433739102871056029ab1788`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-CozyPastel.mat` (`9c8a8727c781e9e5894ae38cd5abb6311ea895819c94ce079339e6cb72bafd72`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-TacticalRealism.mat` (`c604a3edf947992e7a3eef1f024c2f5e6b4bd85add536a2bce612bc47be7ce7d`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-NaturalPbr.volume.json` (`b4edffcec20fe78998e0eb998a0e65c7691b81005a4a5af62dfe0787fe5277af`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-StylizedToon.volume.json` (`3c721b06e2a6656ca74d8423cb1928785c79de7c5bc2132933eff4f0a4955c8f`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-NeonSciFi.volume.json` (`8680ed7f310f5b6d5966cb05727403532975958a0104d772c710066fb9d61ba1`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-NoirContrast.volume.json` (`3b266198dea605baa44d1243162cf0824013791ecd3369f33be2de798c6aa38e`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-FantasyAtmosphere.volume.json` (`bf9308cfd25589f120ae81d98c3d28c1dcb7da5b943ba8553dbe004653fc7834`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-MobileFlat.volume.json` (`4becfcdc75fe5c81cdaecdec09c13a149cb687f39b22adf21c9034bc005c54d9`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-RetroPixel.volume.json` (`b21ec374cdeb729e1e00e711fc76ba42818d2a5a85bf807b7bdd43aff129df25`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-HorrorGrit.volume.json` (`722297e817dda3aa94c59d204bc66733631349c45ff81c7c221bb3c0af4272a3`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-CozyPastel.volume.json` (`29f05dd996e5f75a45e51eb4f188ff62bf6576158663a0276d75584efaa90a83`)
+ `Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-TacticalRealism.volume.json` (`9f6cb96a6c753e3ee28ce47daad582645e95346fbe17b1c01315f211db785786`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateProfiles.json` (`7eb8703087075eab7d46b558d64bc7ad2881aa219ee9363183224cc09b04d0f0`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplatePlan.cs` (`d6ef1808a1c3f7fdd67ed0bd389c163f6de5bd8aa926370a2b54421fe7a200c1`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderIntent.cs` (`c7fdb593c731f6a0c4577889c93ea85a11f36153879a7acb4aa556553a686e3a`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendSnapshot.cs` (`4cfcf950ca36da5dca951bfc157bf02970cf181bb79c0cd582b046eeee79f27b`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendChangePlan.cs` (`9337df2ed1f94934daf410e7fad3e5720c84865e3657e1f9ecfefc69a5701069`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendDiff.cs` (`1b5922d06fa8934c48037d068b50c6a2cda1d17213045ed5966ace926f7817cb`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendApplyGate.cs` (`4ac88d4e7b4095cd874e6d5b292a0b6fa58042cb5367270758dfa269d490d40e`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendReceipt.cs` (`030f75dd9218c1fa49807f957ba9b95a9bb8cd3d98de27f7c7c954899b1d57e5`)
+ `Assets/Plugins/ES/Editor/ESShader/Tests/ESRenderBackendContractTests.cs` (`de1d455dffbc19cc30e1f2b796f63f6b6b6d391510d4f92a093474021905c460`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderMetricSnapshot.cs` (`a41e4b05856e66790ab154367bcc86274ecd773659967849c359a77aa69e161a`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderMetricSamplingRequest.cs` (`89dac23e4b611fa2d4e7b74d90c26a2229b6d99d5435aee2d92a8eaddaf3edc2`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderMetricCaptureSession.cs` (`e65e647655bbeaa94e434dcd63d08a687fc206200265ad08350cc2c20fd775ba`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESUrpCompatibilityPolicy.cs` (`ebbca65191915832facc7bb325da4b1a2331b97a7328d92109ed313f95bb9fdd`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESUrpFeatureBudgetEvaluation.cs` (`c82e874266341f0445b28536304d5a709643f4be9926d8b632904f3c0abaf222`)
+ `ES/Tools/Validation/Test-ESUrpQualityMatrix.ps1` (`7b8240f168728a4b1388bc253db3853a57b112e2464bcd2acdcb4d0a542946b5`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendApplySession.cs` (`722aa95436310cc3af3059202489dae6844d68522aaab9e54ae70d5e8a1e8012`)
+ `Assets/Plugins/ES/Editor/ESShader/ESRenderBackendUnityWriter.cs` (`98ea6a5a41bc19f62569dc825f5bf62d07925a97b27e63b395248e019a69ff1e`)
+ `Assets/Plugins/ES/Editor/ESShader/ESUnityProfilerMetricSource.cs` (`313f54d63e2f04c55b532a7660a3e0c4e67f28aaad65894f36164375b3067651`)
+ `Assets/Plugins/ES/Editor/ESShader/ESUrpRenderControlWindow.cs` (`9300645c79d20298411390709dcc402425d08a5b241a2e9895c7a4bbcb7b247b`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderQualitySamplingQueue.cs` (`972c5fca101c0b6d74da945d6e8741d81b3751e3db6b1396bce06de3808b7a97`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceReport.cs` (`09852b5c76b4f309dd804655cd9d9f6bcdba4f240fd26a43b0cc5fb9dc5651d7`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceAggregateReport.cs` (`8cadf4bdaed6cae43b0cd759acca17f964cbf22e8079b74e3443accea40b63f3`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBackendEvidenceReceipt.cs` (`9eaab959dc9c4c7bb5fea59e096432e1a049493ac4613cd3b61b471c50d62e6f`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderBudgetEvaluation.cs` (`bbb89d30bea670d021867684a41918d4db6dfb120dbf9d412e46f3b6086e1b89`)
+ `Assets/Plugins/ES/Editor/ESShader/ESRenderBackendEvidenceReceiptStore.cs` (`9f80aad8adc5ddb946f7d8dc7bf9d398c3251f0ca091054a3a5ddd5b95d48627`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceBatch.cs` (`1ccd3b42298457733c72f09c0493b39f043bf9391b33fae10c4785d35ce45a56`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceBatchDiff.cs` (`c416776620f47bb15247e1cce34aaa45e46c9eba5cf602574f264605820dc0d0`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceBatchBudgetAudit.cs` (`41d17231a669ccb70df34d8aaf49c5c9fdc680cf7224a58d157c80ee6488e43b`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceBatchDecision.cs` (`a4500904a2516a139870d8b1dae80da2349cfee870947ea3504fe6ef5f3029ab`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidenceScenarioSummary.cs` (`839b7133b60a50d7d320dc914cc9e5d7087b5ef7052c7b84297a3a891e7482c2`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESShaderVariantCompileLogParser.cs` (`ecf180fa7aeacb90b689e4a9ddea9f5e621ed5e0ec1553fd3515dc929a2550e7`)
+ `Assets/Plugins/ES/Editor/ESShader/ESRenderShaderResourceSnapshot.cs` (`a063cb08b7ae55befe37ff5d81e484d6d7a6ebbaf58193ab230fb541573110b5`)
+ `Assets/Plugins/ES/Editor/ESShader/ESRenderVolumeResourceSnapshot.cs` (`039bc415d8075ec265561edae05b1978775daeff5285ea31d0484d91107cc98b`)
+ `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEvidencePathPolicy.cs` (`a8fa49756c8cc586aee99ad8d857ef1fb92584335827f2f174a6aac0504d6e03`)
+ `Assets/Plugins/ES/Editor/ESShader/ESRenderBackendResourceSnapshot.cs` (`5e3657e4cc5ae027512798d868460326eb19afa225479343764aaace9591dbba`)
+ `Documentation/AIKnowledge/ESFramework/project-composite-shader-material/composite-shader-material-contract.md` (`1c0eddf6783c93332555403388c961008004ce3825af3210c90e5b616c3050bd`)
+ `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md` (`663b07bd7624ea5ad1ce497fbd9487cb93405fa45377c2bbfa59c202f71bce3c`)
+ `Documentation/AIKnowledge/Engineering/engineering-rendering-batching-evidence/rendering-batching-evidence-contract.md` (`10e4cdbbf97fc40b85cbe708a3c4d6f5dab48461d1ca780172d9ee47e8900bf8`)
+ `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2e9933512b183976b29b712ab0aeb885a17c8b5b14f79417aac380781ae92edc`)
+ `Documentation/AIKnowledge/ExternalSources/unity-2022.3-rendering-official-calibration-20260830.json` (`f0a08dc9e2dbfe165b92e01859b84ccdd118a1f2fdf557698accc4a193526511`)
