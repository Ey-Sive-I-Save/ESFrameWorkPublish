# AudioCue、VFX、资源与预算运行机制

`KnowledgeId`: `es.project.audio-vfx-runtime.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `audio`, `audio-cue`, `voice`, `vfx`, `variant`, `budget`, `pool`, `resource`, `owner`  
`ContentHash`: `6c2bb2e4a2186f550bbefa98ca2d232104090331a9fe4279d3524ebbb8623b12`

## 定义与运行实例分离

`ESAudioCueInfo` 和 `ESVfxInfo` 是 GameCore 作者定义，包含稳定 Key、Variant、权重/条件、资源引用和领域参数；注入后进入强类型 RuntimeData 表。一次播放不修改定义，而是产生 generation-safe Voice/VFX Handle 和独立活动记录。

## Audio Voice

AudioModule 管理 PendingLoad/Playing/Stopping/Ended 状态、Voice 预算、优先级抢占、Mixer 分类、用户设置、资源 Handle 和有限终态历史。结束原因区分自然结束、显式停止、Owner Destroy/Disable/Despawn、抢占、Provider 切换、后端失败、模块停用和资源 owner 释放；业务与测试使用 enum，不解析中文诊断文本。

Voice 请求在资源未 Ready、Cue 未注册、Variant 不可用、预算拒绝或后端失败时给出机器可读 FailureCode。抢占不是 Destroy AudioSource，而是完成旧 Handle 的终态记录并按所有权释放资源/池实例。

Admission 事务在租借时登记到 `pendingAdmissions`，完成或取消时统一移除；非绑定池化 Emitter 还保存 `ESPooledGameObject.Version`，发现代际变化时结束旧 Voice，并拒绝对新借用者执行 Stop、重置或回池。绑定既有 AudioSource 不走该池化回收路径，而是按快照恢复作者配置。

## VFX

VFXModule 同样使用 Handle、状态、结束原因和失败码；Variant 解析后从 Resource/Pool 取得 Prefab。`ESVfxInstanceRoot` 首次缓存 ParticleSystem，Spawn 播放、Despawn 停止，完成判定检查 `isPlaying/IsAlive`。预算超过、Prefab 未预热、Pool 不可用、回池代际失配和后端失败是不同诊断，不应统一成 null。终态历史按完整 `ESVfxHandle(id,generation)` 索引，并以 FIFO 保留最近记录；PoolModule 不可用时才销毁无法回池的实例，代际失配时不触碰后来借用者。

VFX 对外入口与 Audio 保持同一组简洁双轨语义：`PlayOneShot`、`PlayAttached`、`PlayLoop`、`PlayAtPosition`、`Stop/StopAll/StopCategory` 和 `TryGetVfxStatus`。`PlayOneShot`/`PlayLoop` 只覆盖本次实例的循环行为，不改变 `ESVfxInfo` 定义；显式循环必须提供最大生命周期。所有入口仍必须经过 VFX Key、GameCore、ResourcePlan、Pool 和 Handle 闭环。

### 真实调用流程

```csharp
ESVfxHandle handle = ESGameManager.Vfx.PlayAttached(vfxKey, owner);
if (handle.IsValid)
{
    // 将 Handle 绑定到 Operation/Owner 的生命周期；不要保存裸 GameObject。
    if (ESGameManager.Vfx.TryGetVfxStatus(handle, out ESVfxStatus status)
        && status.State != ESVfxState.Ended)
    {
        ESGameManager.Vfx.Stop(handle);
    }
}

var failures = new List<ESVfxDiagnostic>();
ESGameManager.Vfx.CopyRecentFailures(failures);
```

世界坐标播放使用 `PlayAtPosition(vfxKey, position)`；循环播放使用 `PlayLoop(vfxKey, request)` 或带 Owner 的重载，并确保 `ESVfxInfo.maxLifetime > 0`。请求被预算、资源预热、Owner 或后端拒绝时返回无效 Handle，失败原因从 `ESVfxDiagnostic.FailureCode` 读取，而不是解析日志文本。

第二资源轨接受 `ESAssetConfigPayloadLease<GameObject>` 与 `ESVfxPrefabPlayConfig`：Lease 在调用时转移给 VFXModule，准入拒绝立即释放，准入成功则随终态 Handle 释放；不提供裸 `GameObject` 入口，避免绕过 ResourcePlan/Provider/Scope。

`ESVfxAudioEmitter` 组合 VFX 与音频但不合并所有权：每个子效果仍有自己的 Handle、owner 生命周期和结束原因。池化宿主 Despawn 时必须清理两类 Handle。

## 与 ResourcePlan/Pool 的关系

- 定义的 ConfigKey 只解析 RuntimeData；实际 Clip/Prefab 仍由资源系统加载。
- ResourcePlan 可预载 Cue/VFX 依赖并预热 Prefab Pool，但不自动播放。
- AudioSource/VFX Root 可以池化；定义 ScriptableObject 和稳定 RuntimeData 不进 GameObject Pool。
- Provider 切换、资源 Scope 释放和 Owner 生命周期都能结束实例，必须保留可观察原因。

`StaleWhen`: Audio/VFX 模块、资源 Provider/Pool、预算合同、AIWarnings 或任一 SourceRef 哈希变化。

## 静态测试证据

`ESAudioCueRuntimeTests.cs` 覆盖 Provider 切换、资源 Owner 结束、Owner 销毁、预算抢占/拒绝、待处理 admission 取消、Handle generation、终态历史和机器可读失败码；`ESVfxOpSupportLifecycleTests.cs` 覆盖 VFX 句柄一次性取出、替换、累积和失败诊断；`ESVfxModuleApiContractTests.cs` 覆盖 VFX 对称入口和诊断标签。当前仍没有与 `ESVfxModule` 对等的 Unity 运行时集成测试，因此 VFX 的 Spawn/Despawn、粒子结束判定、Owner/Emitter 对象池代际和 Pool 回收仍不能声明 Runtime 已通过。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/音频（Audio）/音频播放与资源边界_AI协作警告.md` (`9a208ede3cd065ab6d014d79dfaf1950c4cf745e306e9f734f3e7b2259ccb712`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/特效（VFX）/VFX运行时与制作边界_AI协作警告.md` (`a6531ed0d60c4e137dad6c18db4481ee50d6f771b9649a5932b61fae887a1118`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESAudioCueInfo.cs` (`590fbbdd008c14d70e92b0478a6cd5aed655cdd6aada8a1f0c6b8ceeed2e2383`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESAudioModule.cs` (`fe13e2579021f4e9837e5c3c0e87c664a9fae9064f4670e4d1db49bc56815f30`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESVfxInfo.cs` (`ad2a5bf071d8baf7e6c145e753626b18b2ada52f8dcc200b124314c68ac6c792`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESVfxModule.cs` (`b3d0f17f566ae10a5983d3b4844d7aac7105d61cc867c5c4620b7f568c28ee24`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESVfxAudioEmitter.cs` (`7b949ce87342c8378dc0c64ec3369d40350e94284c3a6d73d7429ce1f41b75b6`)
- `Assets/Plugins/ES/1_Design/Tests/ESAudioCueRuntimeTests.cs` (`57a234d4b6338f8e5582db52a1fdb547cd222806bbd9798c2e54176df2adcf41`)
- `Assets/Plugins/ES/1_Design/Tests/ESVfxModuleApiContractTests.cs` (`74fda4e587eb27a6fdd37903bce72175d06c6c796d94fe0fe585967377eade6f`)
- `Assets/Plugins/ES/1_Design/Tests/ESVfxOpSupportLifecycleTests.cs` (`b8dcb5bfa2ee2fbf931313fa6b0891db3e5f3af35ad5c67ae18b26de55941cee`)

`EvidenceLevel`: `S1`; `StaleWhen`: Cue/VFX 定义、资源解析、预算、Handle 或 Owner 结束语义变化。
