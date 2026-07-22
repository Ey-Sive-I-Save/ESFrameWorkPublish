# GameObject 对象池：GameManager 模块协作警告

## 当前结论

GameObject 对象池是 `ESGameManager` 的运行模块，不属于 `Item`、`Shot`、VFX 或某个具体玩法。

源码位置：

```text
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs
Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/PrefabPrewarmDataInfo.cs
```

`ESGameManager` 默认会自动创建：

```text
autoCreateGameObjectPoolModule = true
ESGameManager.PoolModule
ESGameManager.GetModuleFast<ESGameObjectPoolModule>()
```

## API 口径

贴近 `ESSimplePool` 手感：

```text
GetInPool(prefab, position, rotation, parent)
GetInPool(prefab, position, rotation, parent, autoReturn, autoReturnDelay)
GetInPool(key, position, rotation, parent)
GetInPool(key, position, rotation, parent, autoReturn, autoReturnDelay)
PushToPool(instance)
Prewarm(prefab, count, key, config)
Prewarm(PrefabPrewarmDataInfo)
Clear(prefab)
Clear(key)
ClearAll()
```

纠偏：GameObject 对象池主命名必须学习 `ESSimplePool`：取用叫 `GetInPool`，归还叫 `PushToPool`，预热叫 `Prewarm`。`Rent / TryReturn / Return / RequestReturn` 已从 GameObject 对象池删除，不要在新代码和文档里继续传播。

## Key 规则

支持两种入口：

```text
GameObject prefab
string key
```

高频路径优先用 prefab 入口，走 `Dictionary<GameObject, Group>`，不生成字符串。

字符串 key 用于数据表、关卡预热和跨系统配置。key 必须在预热或注册阶段建立，不要在高频 Tick 中动态拼接字符串。

## 计数与自动修补

每个池组维护：

```text
activeCount
inactiveCount
totalCount
createdCount
rentCount
returnCount
missCount
repairCount
overflowDestroyCount
```

查询：

```text
TryGetStats(key, out stats)
TryGetStats(prefab, out stats)
```

自动修补由配置控制：

```text
autoRepair
repairInactiveTarget
autoRepairInterval
```

用于高频对象池在运行中保持一定空闲余量，避免短时间突发时连续 Instantiate。

## PrefabPrewarmDataInfo

用于关卡/玩法打开前集中预热：

```text
PrefabPrewarmDataInfo
  supportAllScenes
  supportedScenes
  entries
    key
    prefab
    prewarmCount
    useCustomConfig
    config
```

调用：

```text
ESGameManager.PoolModule.Prewarm(dataInfo)
ESGameManager.PoolModule.LoadPrewarmForScene(dataInfo, sceneName)
ESGameManager.PoolModule.LoadPrewarmForCurrentScene(dataInfo)
ESGameManager.PoolModule.UnloadPrewarmForScene(dataInfo, sceneName)
ESGameManager.PoolModule.UnloadPrewarmForCurrentScene(dataInfo)
```

后续可以再做 Group/Pack，但第一版先保持单个 DataInfo 简洁可用。

`LoadPrewarmForScene` 是按场景幂等入口：同一个 `PrefabPrewarmDataInfo` 在同一个场景重复 Load 不会重复预热。多个场景同时持有同一个 DataInfo 时，需要分别 Unload。

释放规则：

```text
ReleasePrewarm(dataInfo, clearExclusiveInactive)
```

会移除该 DataInfo 对池组的预热源引用。如果释放后该 prefab 没有其他预热源：

- 默认清理该池组的空闲对象。
- 如果该池组无活跃对象，会从字典移除并销毁池节点。
- 如果仍有活跃对象，只清空闲对象，避免活跃实例归还时找不到池。

不要在不确定独占时强制销毁活跃对象。

## 自动归还

实例会自动挂：

```text
ESPooledGameObject
```

支持：

```text
RequestPushToPool()
GetInPool(... autoReturn, autoReturnDelay)
配置 defaultAutoReturn / defaultAutoReturnDelay
```

这适合 VFX、临时命中特效、短寿命提示物。复杂生命周期仍由外部显式 PushToPool。

## 0GC 边界

已做：

- 池组字典有初始容量。
- prefab 入口不动态生成 key。
- GetInPool/PushToPool 内部复用 List 缓冲读取 ParticleSystem / TrailRenderer / Resettable。
- 统计查询用 struct out。

仍需遵守：

- 不要在高频 Tick 动态拼 string key。
- 不要在高频路径注册新池组。
- 不要依赖运行时自动扩容解决所有峰值，关卡前用 `PrefabPrewarmDataInfo` 预热。
- 不要把对象池当业务系统，它只管 GameObject 生命周期。

## 职责边界

对象池模块只管：

```text
创建
预热
借出
归还
重设
清理
统计
自动修补
```

不管：

```text
伤害
Buff
Shot 飞行
命中规则
VFX 业务含义
音效业务含义
网络同步
```

`Item/Shot` 只能发回收请求或由 Op 调用池服务，不要直接拥有全局对象池。
