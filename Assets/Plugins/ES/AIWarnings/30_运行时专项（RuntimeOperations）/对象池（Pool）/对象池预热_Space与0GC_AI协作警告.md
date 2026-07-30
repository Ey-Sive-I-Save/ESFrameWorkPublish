# 对象池预热、场景、Space 与 0GC 协作警告

职责：这是 GameManager 的 GameObject 对象池模块协作说明，不属于 Item、Shot、VFX、伤害或 Buff。

## 当前实现

源码位置：

```text
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs
Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/PrefabPrewarmDataInfo.cs
Assets/Scripts/ESLogic/Runtime/Data/For_Info/GroupType/PrefabPrewarmDataGroup.cs
Assets/Scripts/ESLogic/Runtime/Data/For_Info/PackType/PrefabPrewarmDataPack.cs
```

模块入口可以直接配置：

```text
prewarmSources
loadPrewarmOnStart
autoLoadOnSceneLoaded
unloadPrewarmOnSceneUnloaded
currentSpaceName
```

`PrefabPrewarmDataInfo` 支持：

```text
supportAllScenes / supportedScenes
supportAllSpaces / supportedSpaces
entries
```

## 推荐用法

打开关卡或玩法前，把本场景需要高频生成的 Prefab 放进 `PrefabPrewarmDataInfo`，再挂到 `ESGameObjectPoolModule.prewarmSources`。

如果运行时从模块入口新增配置：

```text
RegisterPrewarmSource(dataInfo, loadImmediately: true)
```

如果只是登记，稍后再统一载入：

```text
RegisterPrewarmSource(dataInfo, loadImmediately: false)
LoadConfiguredPrewarmForCurrentScene()
```

场景切换：

```text
SceneManager.sceneLoaded -> 自动尝试载入该 Scene + currentSpaceName
SceneManager.sceneUnloaded -> 自动释放该 Scene + currentSpaceName
```

Space 切换：

```text
NotifySpaceChanged(spaceName)
NotifySpaceChanged(spaceName, unloadOldSpace)
```

对象池不强依赖具体 Space 系统。外部 Space 管理器只要在切换时通知对象池即可。

## 重要边界

- 预热重复判断按 `PrefabPrewarmDataInfo + sceneName + spaceName` 记录，不再只按场景名记录。
- `GetInPool/PushToPool` 热路径不遍历预热配置，不检查 Scene/Space，不拼接动态业务 key。
- 预热、场景加载、Space 切换属于管理路径，可以初始化少量字典/HashSet；真正帧级热路径必须提前注册或预热。
- 高频使用优先走 prefab 或已注册 key。不要在 Tick 中临时拼 string key，也不要在高频路径首次建池。
- 对象池只管创建、预热、借出、归还、重设、清理、统计、自动修补；不要把伤害、命中、Buff、VFX 含义、Shot 飞行逻辑塞进对象池。

## 给后续 AI 的警告

不要为了支持场景和 Space，把对象池改成业务调度中心。Scene/Space 只是预热作用域，不是对象池的世界规则。

如果需要更强的分区回收，先扩展预热作用域和外部调用入口，不要污染 `GetInPool/PushToPool`。热路径 0GC 的核心是：提前建池、复用组件查询 List、避免运行时字符串和集合扩容。
