# ESRuntimeWatch 示例

## 最快体验

1. 执行 `【ES】 / 示例与测试 / RuntimeWatch / 安装或修复标准展示组`。
2. 进入 Play Mode。
3. 执行 `【ES】 / 运行时诊断 / RuntimeWatch / 打开运行时观察`。
4. 依次体验顶部的“观察项”“GameObject”“分类”“脚本”和异常筛选。

标准展示组包含五条清晰的演示主线：

1. 实时数据与基础类型：观察变化高亮、枚举、可写属性。
2. 安全方法调用：体验无参方法和 bool、int、float、string、enum 参数。
3. 搜索筛选与嵌套：体验 ShowIf、Player Tag、分类和嵌套数据。
4. Unity 类型与引用：查看 Vector、Color、Bounds、Transform、GameObject 等类型。
5. 异常与性能诊断：主动开启慢 Getter、读取异常和瞬时峰值，再用诊断筛选定位。

诊断案例默认保持健康状态，不会在日常使用时制造异常或性能负担。

## 最小接入示例

`Example_RuntimeWatchActor` 是适合复制到业务代码中的最小示例，覆盖：

- 动态只读属性；
- 私有字段观察；
- 中文分组和中文按钮；
- 无参方法；
- 单参数方法；
- Unity 对象上下文。

完整产品展示位于：

`Assets/Scripts/ESLogic/Samples/ESRuntimeWatchPlayground/`

- `RuntimeWatchVideoCase_1_BasicTypes.cs`：基础类型、实时刷新与可写属性。
- `RuntimeWatchVideoCase_2_Methods.cs`：无参和单参数方法调用。
- `RuntimeWatchVideoCase_3_FilterAndNested.cs`：ShowIf、Tag 与嵌套成员筛选。
- `RuntimeWatchVideoCase_4_UnityTypes.cs`：常用 Unity 类型和对象引用。
- `RuntimeWatchVideoCase_5_Diagnostics.cs`：慢 Getter、读取异常与瞬时变化诊断。

这些案例各自只包含一个 MonoBehaviour，便于独立复制、挂载和录制。对于每帧更新的演示字段，先通过 RuntimeWatch 将“实时更新”设为关闭，再写入目标值；否则业务脚本的 Update 会在下一帧覆盖写入结果。
