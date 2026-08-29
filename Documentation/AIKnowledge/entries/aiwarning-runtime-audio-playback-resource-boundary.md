# 音频播放与资源边界

`KnowledgeId`: `es.aiwarning.runtime.audio-playback-resource-boundary.v1`  
`Authority`: `AIWarnings + current audio runtime source`  
`RouteKeys`: `aiwarnings`, `runtime`, `audio`, `voice`, `resource`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `20fb1032eee77580d90bc9a83273aa5f6a4587b289df4340438b44d84d7347e0`  
`SourceSetHash`: `20fb1032eee77580d90bc9a83273aa5f6a4587b289df4340438b44d84d7347e0`  
`EntryBodyHash`: `dd422a8f232cd6d4feba145081df27d5acfc604897bf42b00d7e00abebf94e52`  
`StaleWhen`: AudioModule、Cue 身份、Voice/Provider 生命周期、资源 Scope 或预算合同变化。

## 迁移范围

原 Warning 94 行、7,075 UTF-8 字节；现 Warning 保留唯一调度权威、内容身份、Voice/Provider/Owner 生命周期、资源解析、热路径和证据边界。详细链路、业务与 AudioSource 分层、预算抢占、失败码和验收矩阵迁入本条目。

## 当前事实

- 链路为 Gameplay/Entity/Skill/UI/Cutscene → `ESAudioCueKey` + `AudioRequest` → `ESGameManager.Audio` → ConfigKey/RuntimeData/Provider → Voice 准入/预算/抢占 → 池化 Emitter → AudioSource/后端 → VoiceHandle 终态诊断。
- 业务不应长期持有 AudioSource、AudioClip、ESAssetScope、FMOD EventInstance、Bank、URL 或 Provider payload；`PlayOneShot` 仅由音频权威、受控绑定发声器或明确的编辑器预览使用。
- 一次性 Voice 自然结束后回收；循环/附着 Voice 在 Owner 禁用、销毁、Despawn 或回池时显式停止释放。绑定既有发声器时恢复作者配置，`playOnAwake` 必须受控。
- Voice 准入失败、未预热、Provider 切换和 Owner 结束都应通过稳定失败码/终态原因暴露；不得让业务各自停止 AudioSource 或修改模块内部池和状态。
- 运行级验收还需覆盖 OneShot/Attached/Loop、抢占、切换、回池、PlayMode、目标平台 Profiler、GC/CPU/内存及 Player/IL2CPP；当前均未执行。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/音频（Audio）/音频播放与资源边界_AI协作警告.md` (`9a208ede3cd065ab6d014d79dfaf1950c4cf745e306e9f734f3e7b2259ccb712`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESAudioModule.cs` (`16fb72b483fbb07d97d02c7d3bcfd7c814faba393b8f1901e9b3a9fd40621585`)
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Audio/ESAudioCueConfigKeyData.cs` (`4ec7876a695ca685881d2da6c9d2c1861fb4b0f1c82516fdf834e535b1bcc045`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESAudioCueInfo.cs` (`590fbbdd008c14d70e92b0478a6cd5aed655cdd6aada8a1f0c6b8ceeed2e2383`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/10_Audio/OpAudio.cs` (`bfaf704b6e4d89b79ae63b60cbcdf2b378b57372250c70e3e03f0f6c1d4f5b59`)
- `Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip/SkillTrack/SkillTrackItems/SkillTrackItem_Audio.cs` (`399331b01058c23c00db44de07b8322072343543799a638c78d7e29bc492ef0d`)
- `Assets/Scripts/ESLogic/Runtime/Command/Commands/COMMAND_ESCommandAudio.cs` (`50ad1255f96ec4887da2c244d2d3c3a9ab083cdd7397265c5bf0816fbbc081c9`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESVfxAudioEmitter.cs` (`7b949ce87342c8378dc0c64ec3369d40350e94284c3a6d73d7429ce1f41b75b6`)

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESAudioModule.cs`
- `runtime-not-run`
