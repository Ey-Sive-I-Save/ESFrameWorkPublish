# ES 扩展方法库 — 总览（示例/文档）

本文件为 ES 框架在 `Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/` 下的扩展方法集合总览，按文件列出在代码中实际存在的扩展方法（仅列出仓库中检索到的方法，避免列出不存在的方法）。所有扩展均在 `ES` 命名空间下使用。

**使用说明**
- 示例脚本位于 `Assets/Plugins/ES/3_Examples/Example_Ext/`，可将对应脚本挂到场景中的 GameObject 上运行查看效果。
- 扩展方法命名以 `_` 前缀开头，如 `_SafeDestroy()`。
- 编辑器专用 API（例如 AssetDatabase、EditorUtility）在运行时不可用，相关方法在源码中用 `#if UNITY_EDITOR` 条件编译保护。

**文件与方法总览**
- **ExtForGameObject.cs**: GameObject/Transform 相关
  - `_GetOrAddComponent<T>(this GameObject)` — 获取或添加组件
  - `_GetAllComponents(this GameObject)`
  - `_SafeSetActive(this GameObject, bool)`
  - `_SafeToggleActive(this GameObject)`
  - `_SafeDestroy(this GameObject, float delay = 0f)`
  - `_SafeSetLayer(this GameObject, int layer, bool includeChildren = false, bool includeInactive = false)`
  - `_IsInLayerMask(this GameObject, LayerMask)`
  - `_SetParentKeepWorld(this GameObject, Transform parent, bool keepScale = true)`
  - `_FindOrCreateChild(this GameObject parent, string name, Action<GameObject> initAction = null)`
  - `_CopyTransform(this Transform src, Transform dst, TransformCopyFlags flags = TransformCopyFlags.All)`
  - `_ApplyTransform(this Transform dst, Transform src, TransformCopyFlags flags = TransformCopyFlags.All)`
  - `_DestroyChildren(this GameObject, float delay = 0f)`
  - `_DestroyChildrenImmediate(this GameObject)`
  - `_SetActiveRecursive(this GameObject, bool)`
  - `_SetActiveRecursivelyIncludeInactive(this GameObject, bool active, bool includeInactive)`

- **ExtForUnityObject.cs**: UnityEngine.Object 通用扩展
  - `_TryUse<T>(this T ob) where T : UnityEngine.Object` — 空安全访问
  - `_TryUse<T>(this T ob, Action<T> action)`
  - `_GetGUID(this UnityEngine.Object)` — 编辑器下获取 Asset GUID（运行时返回空）
  - `_IsAsset(this UnityEngine.Object)` — 编辑器下判断是否为资产
  - `_IsNullOrDestroyed(this UnityEngine.Object)` — 伪 null 判定
  - `_GetScenePath(this UnityEngine.Object)` — 返回 Scene:HierarchyPath
  - `_IsInResources(this UnityEngine.Object)` — 编辑器下判断是否位于 Resources
  - `_SafeDestroy(this UnityEngine.Object, bool allowDestroyingAssets = false)`
  - `_GetHierarchyPath(this UnityEngine.Object)`
  - `_IsPrefabAsset(this UnityEngine.Object)`
  - `_IsPrefabInstance(this UnityEngine.Object)`

- **ExtForVector.cs**: Vector3 / Vector2 实用方法
  - `_MutiVector3(this Vector3, Vector3)`
  - `_SafeDivideVector3(this Vector3, Vector3)`
  - `_DivideVector3(this Vector3, Vector3)`
  - `_NoY(this Vector3)`
  - `_WithY(this Vector3, float)` / `_WithX` / `_WithZ`
  - `_WithYMuti` / `_WithXMuti` / `_WithZMuti`
  - `_DistanceToHorizontal(this Vector3 from, Vector3 to)`
  - `_IsApproximatelyZero(this Vector3, float threshold = 0.001f)`
  - `_ToVector2XZ(this Vector3)` / `_FromXZ(this Vector2, float y = 0f)`
  - `_AngleHorizontal(this Vector3 from, Vector3 to)`
  - `_SignedAngleXZ(this Vector3 fromDir, Vector3 toDir)`
  - `_ApproxEquals(this Vector3 a, Vector3 b, float eps = 1e-6f)`
  - `_PerpendicularXZ(this Vector3 v)`
  - `_MoveTowardsXZ(this Vector3 current, Vector3 target, float maxDelta)`

- **ExtForNum.cs**: 数值扩展（int / float）
  - `_SafeDivide(this float, float)`
  - `_Clamp(this float, float min, float max)` / `_Clamp(this int, int min, int max)` / `_Clamp01(this float)`
  - `_AsNormalizeAngle(this float)` / `_AsNormalizeAngle180(this float)`
  - `_Remap(this float, float fromMin, float fromMax, float toMin = 0, float toMax = 1)`
  - `_LerpTo(this float start, float end, float t)`
  - `_SmoothDamp(this float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = 0.02f)`
  - `_InverseLerp(this float value, float from, float to)`
  - `_RoundInt(this float)` / `_ToRadians` / `_ToDegrees` / `_Cycle` / `_IsInRange` / `_IsApproximatelyZero` / `_IsApproximately` / `_IsEven` / `_IsOdd` / `_IsDivisibleBy` / `_IsPositive` / `_IsNegative` / `_Sign`
  - 若干字符串化格式方法（`_ToString_*`）和范围枚举辅助 `_GetIEnumerable_TargetRangeInts`

- **ExtForEnum.cs**: 枚举工具
  - `_AddFlag<T>(this T enumValue, T flag)` / `_RemoveFlag<T>` / `_ToggleFlag<T>` / `_SwitchFlag<T>`
  - `_HasAllFlags<T>(this T enumValue, params T[] flags)` / `_HasAnyFlags<T>`
  - `_GetEnumValues<T>()` / `_GetDescription(this Enum)` / `_IsDefined<T>(this T enumValue)`
  - `_Next<T>(this T enumValue)` / `_Previous<T>(this T enumValue)`
  - 以及 64 位安全变体（例如 `_AddFlag64`）

- **ExtForEnumable.cs**: 集合与枚举可枚举扩展
  - `_RandomItem<T>(this T[] array, T ifNullOrEmpty = default)`，以及带 `System.Random` 的重载
  - `_RandomItem<T>(this List<T> list, T ifNullOrEmpty = default)`，以及带 `System.Random` 的重载
  - `_RandomShuffle<T>(this List<T>)` / `_RandomShuffle<T>(this T[] array)`
  - `_IsNullOrEmpty<T>(this T[] / List<T>)`
  - `_TryRandomItem<T>(this T[] / List<T>, out T item)`
  - `_WeightedRandomIndex(this int[] / float[])` / `_GetRandomIndices(int n, int maxExclusive, bool withReplacement = false)`

- **ExtForString_Main.cs**: 字符串处理与格式化（常用）
  - 截取/保留：`_KeepBeforeByFirst/_KeepBeforeByLast/_KeepAfterByFirst/_KeepAfterByLast/_KeepBetween` 等
  - 验证：`_IsValidEmail/_IsValidUrl/_IsNumeric/_HasSpace/_ContainsChineseCharacter` 等
  - 转换：`_AsInt/_AsFloat/_AsDateTime/_AsLong/_ToMD5Hash/_ToSHA1Hash/_ToSha256Hash` 等
  - 还有大量辅助（Join、Trim、Code format、Builder 支持等）

- **ExtForCouroutine.cs**: 协程扩展
  - `_StartAt(this IEnumerator, MonoBehaviour behaviour = null)`
  - `_StartAtDelayed(this IEnumerator, float delaySeconds, MonoBehaviour behaviour = null)`
  - `_StartRepeating(this Func<IEnumerator> enumeratorFactory, float intervalSeconds, int count = 0, MonoBehaviour behaviour = null)`
  - `_StopAt(this Coroutine, MonoBehaviour behaviour = null)`
  - `_RunDelayOnMainThread(this Action action, float delaySeconds = 0f)`

- **ExtForCompoent.cs / ExtForComponent.cs**: Component / Transform 专用工具
  - `_GetCompoentInParentExcludeSelf<T>(this Component, bool includeInactive = true)`
  - `_GetCompoentsInChildExcludeSelf<T>(this Component, bool includeInactive = true)`（及 Fast 版本）
  - `_DistanceTo(this Component, Component)` / `_DistanceTo(this Component, Vector3)`
  - `_IsInRange(this Component, Component, float range)` 等距离判断
  - Transform 专属：`_Reset/_ResetLocal/_SetPositionX/_SetPositionY/_SetPositionZ/_SetLocalPositionX/_GetChildrensOneLayer/_GetChildrensOneLayerFast/_DestroyAllChildren` 等
  - `_GetOrAddComponent<T>(this Component)` 和 Fast 版本

- **ExtForColor.cs**: Color 处理（查到的方法）
  - `_WithR/_WithG/_WithB/_WithRGB`（以及 `ref` 版本 `_WithRRef/_WithGRef/_WithBRef`）
  - `_WithAlpha/_AlphaMultiply/_AlphaMultiplyRef/_RGBMultiAlpha/_RGBMultiAlphaRef`
  - `_ToHex16String/_AsHexFormat/_ToColorFromHex/_TryToColorFromHex`
  - `_Invert/_IsApproximatelyEachRGBA/_IsApproximatelyTogether/_GetGrayscale/_RandomColorRef/_AsGrayscale/_WithRGBMulti/_SetRGBMulti/_WithRGBMutiRef`

- **ExtNormal.cs**: 通用工具
  - `_AsListOnlySelf<T>(this T?)` / `_AsArrayOnlySelf<T>(this T?)`
  - `_Swap<T>(ref T a, ref T b)`
  - `_GetTypeDisplayName(this Type)`（带线程安全缓存）


**说明与约束**
- 本总览严格基于源码检索到的 `public static` 扩展方法签名，避免列出未实现或已被移除的方法。
- 某些方法在源代码中使用条件编译（`#if UNITY_EDITOR`），在运行时（Player）不可用。
- 若希望我把方法按用途生成更详细的 API 文档（参数说明、示例代码片段），我可以把每个方法展开为带示例的文档页。

**下一步建议**
- 生成 `docs/` 子目录并把每个扩展拆成单独的 Markdown 文档，便于发布与搜索。
- 为编辑器专用方法列出运行时替代方案或在 README 中明确标注“编辑器专用”。
- 将 README 的方法索引同步到项目的主 README（或 package.json）以便自动化发布。

如果你同意，我现在可以把此 README 写入文件并提交到仓库（已完成写入）；你还希望把其中的每个方法展开成示例或更详细文档吗？