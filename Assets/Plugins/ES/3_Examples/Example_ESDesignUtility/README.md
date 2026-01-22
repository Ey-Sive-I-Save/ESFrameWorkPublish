# ESDesignUtility API 演示示例

本目录包含 ESDesignUtility 所有工具类的完整 API 演示示例。

## 📁 文件清单

| 文件名 | 工具类 | 主要功能 | 状态 |
|--------|--------|----------|------|
| Example_ColorSelector.cs | ColorSelector | 颜色选择、调色板生成 | ✅ 无错误 |
| Example_Coroutine.cs | Coroutine | 协程封装、延迟执行 | ✅ 无错误 |
| Example_Creator.cs | Creator | 深拷贝、集合克隆 | ✅ 无错误 |
| Example_TransformSetter.cs | TransformSetter | Transform操作 | ✅ 无错误 |
| Example_Sorter.cs | Sorter | 路径排序、贪心算法 | ✅ 无错误 |
| Example_Function.cs | Function | 数学/容器/字符串 | ✅ 无错误 |
| Example_Matcher.cs | Matcher | 序列化、类型转换 | ✅ 无错误 |
| Example_Reflection.cs | Reflection | 反射操作 | ✅ 无错误 |
| Example_Foreach.cs | Foreach | Transform查找遍历 | ✅ 无错误 |
| Example_SafeEditor.cs | SafeEditor | Editor功能封装 | ✅ 无错误 |
| Example_SimpleScriptMaker.cs | SimpleScriptMaker | 脚本生成 | ✅ 无错误 |

## 🎯 使用方法

1. **直接挂载到GameObject**：将示例脚本挂载到任何GameObject上，运行后查看Console输出
2. **逐行学习**：每个示例都有详细注释，从简单到复杂循序渐进
3. **实际应用**：示例中包含真实使用场景，可以直接复制到项目中使用

## 📝 注意事项

### 编译状态
✅ **所有11个示例文件均已通过编译，无任何错误！**

### API 调用说明
所有示例都使用了正确的 API 调用方式：
- `ColorSelector`: 使用 `ESDesignUtility.ColorSelector.ColorName` 枚举
- `Function`: 使用 `GetOne<T>`/`GetSome<T>` 方法 + `EnumCollect.SelectOne` 枚举
- `Matcher`: 手动实现文件读写，演示序列化/反序列化流程
- `Reflection`: 使用正确的泛型签名和 `TryInvokeMethodReturn` 方法
- `Foreach`: 使用 `FindChildWhere` 方法，参数顺序正确
- `Coroutine`: 使用 `ActionRepeat` + 条件判断实现重复执行

### Editor功能说明
- SafeEditor、SimpleScriptMaker等部分功能仅在**Unity Editor模式**下有效
- 运行时调用会安全返回默认值，不会报错
- 标有`#if UNITY_EDITOR`的代码段仅供参考

### 枚举类型说明
示例中使用的枚举类型来自`EnumCollect`类，主要包括：
- `PathSort`：路径排序方式
- `HandleTwoNumber`：两数运算类型
- `CompareTwoNumber`：两数比较类型
- `SelectOne`/`SelectSome`：列表选择类型
- ...等，详见`EnumForComputeFunction.cs`

## 🔍 快速索引

### 按功能分类

#### 🎨 UI与视觉
- **ColorSelector**：100+预定义颜色、互补色、调色板生成

#### ⏱️ 时间与异步
- **Coroutine**：延迟执行、重复执行、可取消协程

#### 📦 数据处理
- **Creator**：深拷贝任意对象、集合克隆
- **Matcher**：JSON/XML/Binary序列化、类型转换
- **Function**：数学运算、容器操作、字符串处理

#### 🔧 Transform操作
- **TransformSetter**：父级设置、批量操作、TRS初始化
- **Foreach**：Transform查找、层级遍历
- **Sorter**：路径排序、贪心最短路径

#### 🪞 反射与元编程
- **Reflection**：字段/属性/方法的动态访问
- **SimpleScriptMaker**：动态生成C#代码

#### 🛠️ Editor工具
- **SafeEditor**：Editor功能安全封装、资产查询

## 💡 最佳实践

1. **性能考虑**：反射操作较慢，建议缓存结果
2. **异常处理**：示例中已包含基本的空检查，实际使用时可根据需要增强
3. **批量操作**：优先使用批量API（如`HandleTransformsAtParent`）提升性能
4. **Editor专用**：开发工具脚本时优先使用SafeEditor，无需手动添加`#if UNITY_EDITOR`

## 📚 相关文档

- ESDesignUtility ReadMe: `Assets/Plugins/ES/1_Design/Design_Tools/ESDesignUtility/0-DesignUtility_ReadMe.cs`
- 枚举定义: `Assets/Plugins/ES/1_Design/ValueType/EnumCollect/BaseEnums/EnumForComputeFunction.cs`

## 🔄 更新日志

- 2026-01-23: 初始版本，包含11个工具类的完整示例
