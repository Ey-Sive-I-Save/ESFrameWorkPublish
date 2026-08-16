# VFX 运行时与制作边界 AI 协作警告

状态：现行专项约束；当前实现等级 `Implemented-Unverified`。

最后核对：2026-08-16。

适用范围：`ESVfxInfo`、`ESVfxGroup`、`ESVfxKey`、`ESVfxGameCoreTable`、`ESVfxModule`、`ESVfxHandle`、ParticleSystem/VFX Graph 后端、特效预览、模板制作、AssetPackage 特效分析和运行预算。

## 模块定位

ES 的目标不是替代 Unity ParticleSystem、VFX Graph 或 Shader Graph，而是提供一套稳定的特效内容身份、请求、资源、实例、生命周期、池化、预算、变体、预览和验收协议。

```text
作者资产/模板
  -> ESVfxInfo + ESVfxGroup
  -> 稳定 ESVfxKey / GameCore Table
  -> ResourcePlan / Provider / Scope
  -> ESVfxPlayRequest
  -> ESVfxModule
  -> 具体 ParticleSystem 或 VisualEffect 后端
  -> Handle / 状态 / 结束原因 / 回池 / 资源释放
```

作者 Group 负责内容聚合与注入；运行时预算分组负责并发、优先级、抢占和降级。两者不得因为都叫 Group 而合并为一个权威对象。

## 入口文件

```text
Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESVfxInfo.cs
Assets/Scripts/ESLogic/Runtime/Data/For_Info/GroupType/ESVfxGroup.cs
Assets/Scripts/ESLogic/Data/GameCoreConfigKey/VFX/ESVfxConfigKeyData.cs
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESVfxModule.cs
Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESAssetPackageBakeData.cs
Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs
```

## 当前源码事实

- 已存在 `ESVfxInfo`、`ESVfxGroup`、`ESVfxKey`、`ESVfxGameCoreTable`、`ESVfxModule`、`ESVfxHandle`、播放请求和实例状态骨架。
- 当前 `ESVfxInstanceRoot` 缓存并驱动 `ParticleSystem[]`。这是现有运行后端的明确边界。
- 当前没有可核对的 `UnityEngine.VFX.VisualEffect` 运行后端、VFX Graph Event 映射、Exposed Property 类型合同、Graph 完成判定、GPU 粒子预算或 VFX Graph 专属池化重置协议。
- AssetPackage 可以识别 ParticleSystem 数量与 VFX Graph 候选，并生成 EditorOnly 分析快照；候选识别不等于 Graph 已可播放、可调参、可回池或可发布。
- 本轮只取得 S1 源码复核，没有重新取得静态编译、Unity 导入、编辑器交互、PlayMode、Profiler、Player 或发布证据。

## 过时理解，禁止继续传播

- [过时] “ES 要先自研一套通用 VFX Graph 编辑器，用户才算能做特效。”
  当前最小产品是模板、参数合同、预览、保存副本和运行治理；Graph 创作继续使用 Unity 官方工具。
- [过时] “存在 `ESVfxModule` 就代表 ParticleSystem 与 VFX Graph 都已支持。”
  当前实例根只驱动 `ParticleSystem[]`，VisualEffect 后端必须单独准入。
- [过时] “`ESVfxGroup` 可以顺便承担运行时预算和抢占。”
  作者聚合与运行调度是两个职责，预算归模块和运行策略。
- [过时] “AI 只能调整作者手工额外暴露的一份参数表。”
  应优先从 Unity/ES 现有类型化属性合同提取，再由作者补充语义和限制；仍禁止直接编辑 Graph YAML。

## 制作能力边界

### AI 可以直接做

- 分析现有 Prefab、材质、Shader、贴图、ParticleSystem、VisualEffect 与依赖关系；
- 生成结构化制作方案、参数合同、变体矩阵、预算建议和模板实例化请求；
- 通过受控 Editor API 修改公开且类型明确的 SerializedProperty、ParticleSystem Module 或 Exposed Property；
- 在 `HideAndDontSave` 的预览实例上试调，并把差异提交给用户确认；
- 从已验收模板派生新资产，保留来源、版本、许可证和依赖闭包。

### AI 不得直接做

- 手写或字符串修改 VFX Graph、Shader Graph、Prefab、Scene 的 YAML；
- 绕过 Unity Editor 资产 API 创建或保存正式 Graph/Prefab；
- 把预览实例、缓存帧、截图或临时 PreviewScene 当作正式资产；
- 未经用户确认把试调结果直接覆盖正式 VFX Group 或原始第三方资源；
- 用候选识别、一次播放成功或 `.csproj` 编译冒充 PlayMode、Profiler 或发布验收。

## 模板优先的最小产品路线

当前阶段优先提供“完整特效模板 + 安全参数面板 + 预览 + 保存副本”，而不是先建设通用 Graph 编辑器。

每个模板至少声明：

- 稳定模板 ID、版本、来源包、许可证与适用渲染管线；
- 支持的后端、Prefab 入口、资源依赖和最低平台；
- 可调参数的稳定 ID、显示名、类型、范围、单位、默认值和写入目标；
- 触发事件、循环/一次性、预计时长、完成判定和停止策略；
- Pool 重置、Owner 销毁、场景切换、资源 Scope 释放和失败降级；
- CPU/GPU/粒子/Overdraw/灯光/Decal/音频预算标签；
- 预览配置、缩略图、推荐视角和验收场景。

参数不应要求资源作者为 AI 单独复制一份描述。优先从 ParticleSystem 序列化模块、`VisualEffectAsset` 暴露属性、Material/Shader 属性和 ES 模板合同自动提取，再由作者只补充语义、范围和禁止组合。

## VFX Graph 后端准入

接入 `VisualEffect` 前必须先明确：

1. 包依赖与 asmdef 是否允许可选安装，未安装时如何编译降级；
2. `VisualEffectAsset`、Prefab 变体和 Exposed Property 的稳定身份如何烘焙；
3. Bool/Int/UInt/Float/Vector/Color/Texture/Mesh/Gradient/Curve 等类型如何验证和写入；
4. Event 名称、初始事件、停止事件和无效事件如何报告；
5. 一次性 Graph 如何判断结束，循环 Graph 如何停止，无法判定时采用何种显式时长策略；
6. Reinit、Stop、资源替换、回池与再次租出时如何清空旧状态；
7. GPU 粒子、Overdraw、Bounds、排序、相机数和平台变体如何进入预算与 Profiler；
8. VisualEffect 后端如何与现有 `ESVfxHandle`、结束原因、Owner、Pool 和 Resource Scope 保持同一合同。

任何一项没有确定协议时，允许保持 ParticleSystem 后端，不允许用反射或字符串兜底偷偷播放 Graph。

## 预览与正式保存

- 试调实例必须是 `HideAndDontSave`，位于 ES 受管预览上下文，禁止写回源资产。
- “保存为正式效果”必须显式选择目标路径、模板/源版本、变体身份和 Group；默认创建新资产或新变体，不静默覆盖来源包。
- 保存前生成差异预览，列出 Prefab、ParticleSystem/VFX Graph、Material、Shader、Texture、事件和参数变化。
- 保存必须走 Unity Editor 的 SerializedObject、PrefabUtility、AssetDatabase 或对应官方 API，并接入 Undo、Dirty、失败回滚和外部漂移检查。
- 保存成功只证明目标作者资产写入；运行时请求、回池、性能和发布仍需单独验收。

## 验收矩阵

达到 `Accepted` 前至少验证：

1. ParticleSystem 与可选 VisualEffect 后端分别完成加载、播放、自然结束、Stop、Owner 销毁、场景切换和回池再用；
2. 模板参数的类型、范围、默认值、非法值、事件和版本迁移均有自动化测试；
3. 预览切换、窗口关闭、Domain Reload 和 PlayMode 切换后无残留对象、RT、回调或缓存泄漏；
4. CPU 主线程、Render Thread、GPU、粒子数、Overdraw、GC Alloc 和内存预算有 Profiler 证据；
5. 低/中/高平台变体、缺资源、缺包、Shader 不兼容和超预算都有可解释降级；
6. Player/IL2CPP 与资源发布链能从稳定 Key 定位同一已烘焙内容。

## 禁止事项

- 禁止把 `ESVfxGroup` 当运行时预算组或万能管理器。
- 禁止业务代码直接 `Instantiate` 正式 VFX Prefab 绕过模块、Pool 和资源 Scope。
- 禁止 ParticleSystem 与 VisualEffect 各建一套对外请求、Handle 和结束语义。
- 禁止把 Audio、Light、Decal、Camera Shake 的最终写入权塞入 VFX；VFX 只通过既有领域请求或受控桥接编排。
- 禁止把模板数量、预览截图或候选分析数量当作“用户具备完整特效制作能力”的证据。

## 下一步

1. 先把模板合同、参数提取和预览差异保存做成一个 ParticleSystem 样板闭环。
2. 再以可选包方式实现 `VisualEffect` 后端和 Exposed Property/Event 合同。
3. 最后补预算面板、Profiler 场景、平台变体和 Player/发布验收；在此之前状态保持 `Implemented-Unverified`。
