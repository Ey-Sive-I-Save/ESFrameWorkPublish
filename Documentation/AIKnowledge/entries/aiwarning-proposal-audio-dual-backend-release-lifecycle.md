# 提案：音频双后端与发布资源生命周期验收

`KnowledgeId`: `es.aiwarning.proposal.audio-dual-backend-release-lifecycle.v1`  
`Authority`: `AIWarnings proposal + current resource/release source`  
`RouteKeys`: `aiwarnings`, `proposal`, `audio`, `unity`, `fmod`, `resourceplan`, `release`, `payload`, `acceptance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `73259c8888acab6981bc8c4310a4afc71f4c62595e5e49782cdcd16e28068758`  
`SourceSetHash`: `73259c8888acab6981bc8c4310a4afc71f4c62595e5e49782cdcd16e28068758`  
`EntryBodyHash`: `a76616db14c9bbd1fcad8cd06adbd0de36c11768860e5efa7dfa99664f522f1f`  
`StaleWhen`: `AudioModule、ResourcePlan、Provider、Release Payload 或后端验收合同变化。`

## 保真迁移

原提案 414 行、31,842 UTF-8 字节；现 Warning 保留阶段提案状态、双后端/统一发布真相源、Cue 入口、授权边界和未验收声明。完整架构、P1-P12 验收矩阵、发布 Payload 方案、调用点迁移和风险决策迁移至本条目。

## 核心架构

业务只引用 `ESAudioCueKey`，经 `ESAudioModule` 进入 UnityAudioBackend 或 FmodAudioBackend；Clip、Bank、Scope、URL 和原生句柄不向业务泄漏。`ResourcePlan.audioCues` 是新内容入口，编辑器按后端展开为 Unity Clip 依赖或 FMOD Bank Participant；既有 `audioClips` 仅作内部/兼容依赖。

Unity Clip 继续走 ConfigKey → AssetIdentity → RuntimeMap → Provider；`.bank` 是独立签名 Runtime Payload，不能伪装为 AssetBundle 或 CodePackage，也不能旁路拼 URL。Downloader、Root Manifest、依赖闭包、哈希校验、缓存和 LastKnownGood 回退必须复用同一发布协议。

## 生命周期不变量

- `ESGameManager.Audio` 是稳定服务引用；Provider/后端切换先拒绝新请求、停止或淡出 Voice、释放 Clip/Bank lease，完成 Quiesce 后再重建并按 Cue 恢复，旧回调不得写回新代际。
- 每个 Voice 终态幂等；Owner 销毁、重复 Stop、抢占、Plan 释放、Pool 复用和迟到回调不得泄漏或污染新 Voice。Emitter 回池与资源持有释放同时完成。
- Cue/Clip/Bank 只经 Catalog/Manifest 稳定身份解析；禁止路径、GUID、裸字符串、URL 或旧 RuntimeKey fallback。用户设置、游戏存档和运行态严格分层，底层句柄/lease 不落盘。
- 热路径使用预分配容器和受限诊断缓冲，禁止每帧反射、LINQ、临时集合、隐式加载或业务直接创建 AudioSource/Scope/EventInstance。

## 分阶段与验收

阶段 A 为不安装 FMOD 的 Unity 正式工作流；阶段 B 为通用签名 Runtime Payload；阶段 C 才加入 FMOD 原生库、Bank lease、EventInstance 与 IL2CPP。P1-P7 覆盖 Unity 播放/Plan/并发/Provider/HotUpdate，P8-P12 覆盖 Bank 校验、生命周期、增量 Consumer、回退和 IL2CPP 压测。必须记录平台、Cue/Bank、Voice、Plan retain、Payload、失败原因、内存和 Console；当前未运行 Unity/PlayMode/Profiler/Player/IL2CPP/发布。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/音频系统_双后端与发布资源生命周期_待验收提案.md`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESRuntimeDataAssetLoadingService.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESResourcePlanInfo.cs`
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeReleaseDownloader.cs`
- `Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip/SkillTrack/SkillTrackItems/SkillTrackItem_Audio.cs`
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/10_Audio/OpAudio.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/音频系统_双后端与发布资源生命周期_待验收提案.md` (`68aaf9f2d9318bd72fe7ae93b50afda43e308c1835ed06be1e9b9626177ea0a4`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESRuntimeDataAssetLoadingService.cs` (`4e822cf4d1b854bd177b27c7021563f3bcaa1ac6a0a3a8aba143c438f22db8a5`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs` (`d31785b5343bf2e4856bd8cdae1ab1690b03d593bfbcfc7387c6a14dfba52ec6`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs` (`b7d63add470de84de3516c374a5f85d41fb1f74181946664b520ec753b153b22`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESResourcePlanInfo.cs` (`20eb8d22012e8fa72d5394b405ffec91bd67087b74d752782b270e3e3bb71822`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeReleaseDownloader.cs` (`50ea89012643e14501c07f2ca6964b2eb46175d885fb10ccaf22fe998552a117`)
- `Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip/SkillTrack/SkillTrackItems/SkillTrackItem_Audio.cs` (`399331b01058c23c00db44de07b8322072343543799a638c78d7e29bc492ef0d`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/10_Audio/OpAudio.cs` (`bfaf704b6e4d89b79ae63b60cbcdf2b378b57372250c70e3e03f0f6c1d4f5b59`)
