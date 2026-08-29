# 音频播放与资源边界

Status: current
StableId: es.aiwarnings.runtime.audio-playback-resource-boundary
Authority: AIWarnings
RouteKeys: aiwarnings, runtime, audio, voice, resource
Applicability: 修改 ESAudioModule、AudioCue、音频 Operation/Skill、VFX 音频发射器、Provider 或 Voice 预算时。
EvidenceRef: Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESAudioModule.cs -RouteId es.aiwarnings.runtime.audio-playback-resource-boundary
Owner: ES Runtime/Audio
StaleWhen: AudioModule、Cue 身份、Voice/Provider 生命周期、资源 Scope 或预算合同变化。
Knowledge: es.aiwarning.runtime.audio-playback-resource-boundary.v1

长期约束：
- `ESAudioModule` 是运行时唯一调度/执行权威；业务只提交 `ESAudioCueKey`/Cue 请求，不得新增并行 AudioManager、单例或转发调度层。
- 正式身份使用 Cue/ConfigKey/RuntimeData；不得用裸 AudioClip、路径、URL 或 Provider 私有 payload 作为内容身份。
- AudioSource、Emitter、Voice、Provider 和资源租约由模块池及绑定发声器契约管理；热路径禁止组件查找、动态添加、按路径加载和每帧创建。
- 每次播放必须受总/分类 Voice 预算和确定性抢占约束，并返回稳定失败码或终态原因；Owner 禁用、销毁、脱离或回池时必须停止并释放。
- Provider 切换先结束旧所有权；Cue 解析复用 ConfigKey、RuntimeData、ResourcePlan、Scope 和 Provider 链路。不得偷偷加载或永久缓存掩盖未预热失败。
- 热路径不得引入 LINQ、反射、临时集合、字符串拼接或未控分配；静态检查不能证明 GC、CPU、内存、PlayMode、Player 或发布预算。
