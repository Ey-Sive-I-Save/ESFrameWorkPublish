# ES Composite Shader 独立测试资产

本目录提供四类 ES Composite Shader 的纯测试 Scene、对象、Material 和程序化纹理。它不引用 `Assets/ESNormalAssets`、`Assets/ESNative/Demo` 或旧 `InternalAssets/ShaderExamples`。

## 快速入口

在 Unity 执行：

```text
【ES】/验证与诊断/验证环境/Shader/创建或刷新 Composite Shader 独立测试资产
```

生成成功后会在 Project 窗口自动定位：

```text
Assets/ESTestAssets/CompositeShaders/Generated/Scenes/00_CompositeShader_TestOverview.unity
```

也可执行：

```text
【ES】/验证与诊断/验证环境/Shader/打开 Composite Shader 测试总览
```

在需要无人值守地同时材质化 ES UI ScreenSpec、刷新六个场景并检查产物时，可在确认没有 Unity 实例占用项目后执行：

```powershell
.\Tools\Build-ESCompositeShaderShowcase.ps1 -UnityPath "<UnityEditor.exe>"
```

脚本会先拒绝已锁定项目，随后按固定顺序生成 ScreenSpec Prefab/Fixture，再调用唯一场景构建器刷新六个场景，并检查最终产物数量。

构建器是所有生成 Scene、Material 和 Texture 的布局权威。不要直接修改生成场景来替代构建器修改。刷新已有场景前，构建器会将同名场景备份到 `ES/Bak/Local/CompositeShaderTestAssets/<UTC运行标识>`，并写入包含源路径、时间、大小和 SHA-256 的 `BACKUP_MANIFEST.md`。

## 目录

```text
CompositeShaders/
  Editor/                 # 显式菜单构建器，Editor-only
  Runtime/                # Test-only MPB / UI 动态演示
  Generated/
    Textures/             # RGBA 图标、噪声、流图、4x4 序列帧
    Materials/
      01_2D/
      02_UI/
      03_3D_Lit/
      04_3D_VFX/
      05_ProductionRecipes/
      90_Environment/
    Scenes/
```

## 场景与案例

| 场景 | 内容 |
|---|---|
| `00_CompositeShader_TestOverview` | 分类入口和代表效果总览 |
| `01_CompositeShader_2D_Cases` | 16 个 Sprite 案例：扫光方向、两类溶解、描边、全息、故障、状态、风格、运动和扰动 |
| `02_CompositeShader_UI_Cases` | 10 个 Canvas Image 案例：按钮反馈、科技面板、稀有度、冷却、揭示、换肤，以及真实 Stencil / RectMask2D 裁剪链 |
| `03_CompositeShader_3D_Lit_Cases` | 10 个受光 Primitive 案例：Rim、扫光、消散、投影、状态、迷彩与附魔金属 |
| `04_CompositeShader_3D_VFX_Cases` | 无效果基准、10 个 VFX 配方及一个粒子顶点流案例：序列帧、Polar UV、Flow Map、径向遮罩、深度交界等 |
| `05_CompositeShader_ProductionRecipes` | 无效果基准、Basic/Standard/High 对比、三种扫光方向、效果顺序和共享材质 MPB 差异 |

共生成 57 个独立案例材质，另有 2 个独立环境材质。每个分类场景都有无效果基准；PlayMode 工作台支持单案例选择、当前效果相关参数滑条、开关/枚举分段控件、Subtle/Standard/Hero 预设、自动动画、当前/全部回正、单独观察、宿主识别、诊断提示、六场景漫游导航与返回总览。完整视觉结构由 `UI/ESCompositeShaderObservationPanel.uxml` 与同名 `.uss` 承载，脚本只绑定案例、参数、状态和动作，视图资产缺失时直接禁用并明确报错，不再用程序化 UGUI 静默替代。可复审的 ES UI 装配合同位于 `Assets/UI/Contracts/ESCompositeShaderShowcase.screen-spec.v3.json`，用于后续由 `ESUIGameScreenMaterializer` 生成 Prefab、Fixture Scene 与视觉证据。Renderer 参数仅写入 `MaterialPropertyBlock`，UI 参数仅写入可销毁的运行时材质实例，不修改生成材质资产。

## 生效顺序检查

生产配方场景专门保留以下观察组：

- `UV -> Color`：先看 UV 扰动后的轮廓，再检查分离色调是否作用于扰动结果。
- `Fade -> Status`：Shader 先计算溶解可见度与边缘色，再叠加冰冻状态；案例用于核对真实处理顺序，避免把标签写反。
- `Quality Basic / Standard / High Exact`：同源纹理和同组开关横向比较，不能用 Inspector 显隐代替真实画面差异。
- `Shine -> / Shine up / Shine diagonal`：三个材质只改变 `_ShineDirection`，用于排查方向参数被忽略或被固定轴覆盖。
- `MPB same material A/B`：两个 SpriteRenderer 共享同一材质资产，运行时用不同 MPB 相位改变扫光强度；材质资产不被改写。

## 最小验收

1. 打开总览和五个分类场景，确认 Console 没有 Shader 导入、材质属性或 Missing Script 错误。
2. 进入 PlayMode，检查时间驱动效果、动态溶解和 MPB A/B 是否产生不同结果。
3. 使用右侧 ES Composite Shader Lab：选择案例、应用预设、拖动参数、切换自动演示/单独观察/高级参数、执行当前或全部回正，并在六个场景之间切换。
4. 在 2D/UI 场景检查外描边是否被 Sprite/Canvas 几何边界裁切；被裁切时属于宿主几何限制，不得误报为参数未接入。
5. 在 UI 场景分别检查 `传奇流变 · Stencil` 与 `主题换肤 · RectMask2D`，确认自定义 Shader 经过 Unity UI 的模板和矩形裁剪链后仍正确显示。
6. 在 VFX 场景确认 URP Asset 和目标 Camera 的 Depth Texture 条件，再判断软粒子/深度交界结果。
7. 用 Frame Debugger/Profiler 比较质量档、关闭父开关后的采样与变体成本。
8. Player、GLES3/Vulkan 和 IL2CPP 仍需按发布目标单独验收；场景存在不等于发布通过。

## 生成边界

- 菜单只在用户显式触发时生成，不使用 `InitializeOnLoad` 或域重载全盘扫描。
- 生成器只写 `Assets/ESTestAssets/CompositeShaders/Generated` 和对应 Local before 备份。
- UI 动态案例创建并释放自己的运行时材质实例；Renderer 动态案例使用 `MaterialPropertyBlock`。
- 动态演示脚本仅在 `UNITY_EDITOR` 编译，供 Editor PlayMode 验收使用，不进入正式 Player 程序集。
- Noise / Flow Map 按线性数据纹理导入；4x4 序列帧关闭 mipmap，避免图块边界串帧。
- 质量 Keyword 由四类公开参数 API 同步，不只写 `_QualityTier` 浮点值。
- 生成场景是专用视觉验收环境，禁止加入正式 Build Settings；测试脚本在 Player 中有意不存在。
