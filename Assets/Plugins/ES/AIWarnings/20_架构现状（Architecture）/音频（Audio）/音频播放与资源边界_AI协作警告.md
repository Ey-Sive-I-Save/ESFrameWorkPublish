# 音频播放与资源边界：AI 协作警告

状态：现行约束；音频运行时代码、音频内容接入和相关验收按本文件执行。  
最后核对：2026-08-07。

## 负责范围

本规则约束 `ESAudioModule`、`AudioCue`、音频 Operation、Skill Track 音频、VFX 音频发射器、音频资源解析、Voice 预算、抢占和 Provider 生命周期。

本文件描述当前源码已经建立的边界；它不把未完成的 Unity、PlayMode、Profiler 或发布验收写成“已验证”。归档目录中的双后端/FMOD 提案仍是提案，不能作为当前实现依据。

## 当前权威链路

```text
Gameplay / Entity / Skill / UI / Cutscene
  -> ESAudioCueKey + AudioRequest
  -> ESGameManager.Audio（ESAudioModule）
  -> ConfigKey / RuntimeData / ResourceProvider 解析
  -> Voice 准入、分类预算与抢占
  -> 池化 Emitter 或受控的绑定 Emitter
  -> AudioSource / 后端执行
  -> VoiceHandle 与终态诊断
```

- `ESAudioModule` 是运行时唯一的音频调度和执行权威；业务系统不得另建 AudioManager、音频单例或按功能拆分的平行发声系统。
- 稳定内容身份是 `ESAudioCueKey` / ConfigKey / RuntimeData。运行时不得把裸 `AudioClip`、资源路径、URL 或 Provider 私有对象当作正式内容身份。
- 普通玩法入口优先使用 `ESGameManager.Audio.PlayOneShot(cue, request)`、`PlayAttached(cue, transform, request)` 等 Cue 请求接口。
- `VoiceHandle`、状态枚举、失败码和终态原因是调用方获取结果的正式协议；禁止用本地化日志文本作为控制分支。

## 业务与 AudioSource 边界

- 业务、角色、技能、武器、UI 和 Cutscene 只选择 Cue 并提交请求，不直接持有或管理长期 `AudioSource`、`AudioClip`、`ESAssetScope`、FMOD EventInstance、Bank、URL 或 Provider payload。
- `AudioSource.PlayOneShot` 只能由音频权威、受控的绑定发声器或明确隔离的编辑器预览使用；不得把它作为各业务模块自行播放的默认方案。
- 禁止在玩法热路径中 `GetComponent<AudioSource>`、`AddComponent<AudioSource>`、按路径查找资源或每帧创建发声器。
- 发声器由音频模块的池和绑定发声器契约管理。模块创建的 `AudioSource` 必须显式控制 `playOnAwake = false`；绑定既有发声器时，使用结束后必须恢复其作者配置。
- 一次性音效自然结束后由模块回收；循环、附着或长生命周期 Voice 必须在所属者禁用、销毁、脱离或回池时显式停止/释放。
- Direct `AudioClip` 接口若仍存在，只能视为兼容、内部或迁移路径；新内容不得以此绕过 Cue、资源身份和 Voice 管理。

## Voice、预算与抢占

- 每次被接受的播放都必须成为可诊断的 Voice，并受到总 Voice 预算和分类预算约束。
- 抢占必须通过音频模块的确定性仲裁完成，不能由业务各自停止“看起来不重要”的 AudioSource。
- 准入失败、资源未预热、Provider 切换和拥有者生命周期结束必须返回稳定的失败码或终态原因。
- 自动播放队列、Voice 池、准入池和 Emitter 池应复用现有预分配结构；不得为单个玩法再包一层转发调度器或临时队列。
- 音频抢占涉及跨系统活跃请求仲裁时，还必须遵循 `10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md`。

## 拥有者与资源生命周期

- 附着 Voice 的拥有者被禁用、销毁、Despawn 或回池时，必须停止并释放其 Voice 与资源租约；回池后不得残留上一位拥有者的播放或回调。
- Provider 切换必须先结束并关闭旧后端/旧资源所有权，再恢复或重新提交请求；不得让两个 Provider 同时持有同一 Voice 的执行权。
- Cue 的资源解析必须复用项目既有 ConfigKey、RuntimeMap、ResourcePlan、Scope 和 Provider 链路，不得另建音频专用资源表或运行时路径查找表。
- `CueClipNotPrewarmed` 等失败码表示缺少预热/资源证据，应修复内容或预热配置，不得通过业务侧偷偷加载或永久缓存来掩盖。

## 性能约束

- 音频每帧更新不得引入 LINQ、反射、临时集合、字符串拼接或未受控的托管分配。
- 不得在每帧重新解析 Cue、Profile、资源 Scope 或发声器组件；应使用模块维护的稳定引用、池和预分配队列。
- 自动播放必须受每帧启动上限和 Voice 预算约束，避免音效事件风暴吞没主线程、音频线程或内存。
- “无明显分配”只能由目标 Unity/Player 与 Profiler 证据确认，静态代码检查不能替代运行时性能结论。

## 禁止的无意义层

- 禁止新增 `EntityAudioModule`、`WeaponAudioManager`、`SkillAudioDispatcher` 等只转发 Cue 请求的 MonoBehaviour 或单例。
- 禁止用“每个武器一个 AudioSource”“每个技能一个播放器”替代集中 Voice 预算和生命周期管理。
- 禁止为了包装一个参数新增中间 Request、Emitter、Provider 或资源表；只有当它拥有明确的生命周期、仲裁或资源所有权时才允许增加层次。
- 禁止让玩法系统直接修改音频模块内部池、Voice 状态或 Provider 状态。

## 验收门槛

至少应分别取得以下证据，才能宣称音频系统运行级稳定：

1. 源码与严格 UTF-8 检查通过，且不存在绕过 `ESAudioModule` 的新增业务播放路径。
2. Unity Editor 编译、Domain Reload 和音频 Cue 解析通过。
3. OneShot、Attached、Loop、自然结束、显式停止和终态诊断通过。
4. Owner 禁用、销毁、Despawn、回池和重新租出后无残留 Voice、回调或资源租约。
5. 总预算、分类预算、抢占和资源未预热失败路径符合预期。
6. Provider/资源切换不会双重执行或泄漏旧所有权。
7. PlayMode 与目标平台 Profiler 证明 Voice 数、GC、CPU 和内存处于预算内；发布范围包含 Player/IL2CPP 时还需完成对应验收。

当前源码已具备上述多数运行时骨架和控制点；在没有实际 Unity、PlayMode、Profiler 及发布证据前，状态只能写为“实现已建立，运行级验收待完成”。

## 主要证据入口

```text
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESAudioModule.cs
Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Audio/ESAudioCueConfigKeyData.cs
Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESAudioCueInfo.cs
Assets/Scripts/ESLogic/Runtime/Operation/Operations/10_Audio/OpAudio.cs
Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip/SkillTrack/SkillTrackItems/SkillTrackItem_Audio.cs
Assets/Scripts/ESLogic/Runtime/Command/Commands/COMMAND_ESCommandAudio.cs
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESVfxAudioEmitter.cs
Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/音频系统_双后端与发布资源生命周期_待验收提案.md
```
