# AudioCue、VFX、资源与预算运行机制

`KnowledgeId`: `es.project.audio-vfx-runtime.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `audio`, `audio-cue`, `voice`, `vfx`, `variant`, `budget`, `pool`, `resource`, `owner`  
`ContentHash`: `38850d2f84409a29501f690b2459d5249ec21142fcb36d8b7a9f4eb243110ece`

## 定义与运行实例分离

`ESAudioCueInfo` 和 `ESVfxInfo` 是 GameCore 作者定义，包含稳定 Key、Variant、权重/条件、资源引用和领域参数；注入后进入强类型 RuntimeData 表。一次播放不修改定义，而是产生 generation-safe Voice/VFX Handle 和独立活动记录。

## Audio Voice

AudioModule 管理 PendingLoad/Playing/Stopping/Ended 状态、Voice 预算、优先级抢占、Mixer 分类、用户设置、资源 Handle 和有限终态历史。结束原因区分自然结束、显式停止、Owner Destroy/Disable/Despawn、抢占、Provider 切换、后端失败、模块停用和资源 owner 释放；业务与测试使用 enum，不解析中文诊断文本。

Voice 请求在资源未 Ready、Cue 未注册、Variant 不可用、预算拒绝或后端失败时给出机器可读 FailureCode。抢占不是 Destroy AudioSource，而是完成旧 Handle 的终态记录并按所有权释放资源/池实例。

## VFX

VFXModule 同样使用 Handle、状态、结束原因和失败码；Variant 解析后从 Resource/Pool 取得 Prefab。`ESVfxInstanceRoot` 首次缓存 ParticleSystem，Spawn 播放、Despawn 停止，完成判定检查 `isPlaying/IsAlive`。预算超过、Prefab 未预热、Pool 不可用和后端失败是不同诊断，不应统一成 null。

`ESVfxAudioEmitter` 组合 VFX 与音频但不合并所有权：每个子效果仍有自己的 Handle、owner 生命周期和结束原因。池化宿主 Despawn 时必须清理两类 Handle。

## 与 ResourcePlan/Pool 的关系

- 定义的 ConfigKey 只解析 RuntimeData；实际 Clip/Prefab 仍由资源系统加载。
- ResourcePlan 可预载 Cue/VFX 依赖并预热 Prefab Pool，但不自动播放。
- AudioSource/VFX Root 可以池化；定义 ScriptableObject 和稳定 RuntimeData 不进 GameObject Pool。
- Provider 切换、资源 Scope 释放和 Owner 生命周期都能结束实例，必须保留可观察原因。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/音频（Audio）/音频播放与资源边界_AI协作警告.md` (`4c4708f45e7e6c147c5bbd306c4f66604898f82c47edbad04c9d04e3984b80b0`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/特效（VFX）/VFX运行时与制作边界_AI协作警告.md` (`eab9c66838d3b5ea9e8797f0ef231fde30d36c73ec52207ab42edc869387c543`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESAudioCueInfo.cs` (`590fbbdd008c14d70e92b0478a6cd5aed655cdd6aada8a1f0c6b8ceeed2e2383`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESAudioModule.cs` (`16fb72b483fbb07d97d02c7d3bcfd7c814faba393b8f1901e9b3a9fd40621585`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESVfxInfo.cs` (`ad2a5bf071d8baf7e6c145e753626b18b2ada52f8dcc200b124314c68ac6c792`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESVfxModule.cs` (`738dd55a48c2b7ce3e01916d11145ba7a2409ca49e577b7ba4d74da1d3b4babf`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESVfxAudioEmitter.cs` (`7b949ce87342c8378dc0c64ec3369d40350e94284c3a6d73d7429ce1f41b75b6`)

`EvidenceLevel`: `S1`; `StaleWhen`: Cue/VFX 定义、资源解析、预算、Handle 或 Owner 结束语义变化。
