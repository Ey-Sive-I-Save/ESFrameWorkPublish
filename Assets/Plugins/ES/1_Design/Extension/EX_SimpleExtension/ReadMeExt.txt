using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

# ES框架 - 扩展方法库完整清单

## 📋 概述

ES框架扩展方法库提供了12个扩展类，涵盖Unity开发中最常用的类型扩展。所有扩展方法以 `_` 前缀命名，便于智能提示和识别。

## 🗂️ 完整文件清单

### 📁 Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/

| 文件名 | 扩展对象 | 核心功能 | 方法数量 |
|--------|----------|----------|----------|
| **ExtForString_Main.cs** | `string` | 字符串处理、格式化、验证 | 30+ |
| **ExtForGameObject.cs** | `GameObject` | 游戏对象操作、组件管理 | 15+ |
| **ExtForCompoent.cs** | `Component` | 组件通用操作 | 8+ |
| **ExtForVector.cs** | `Vector2/3/4` | 向量数学运算 | 12+ |
| **ExtForColor.cs** | `Color` | 颜色处理、转换 | 10+ |
| **ExtForNum.cs** | `int/float/double` | 数值操作、格式化 | 15+ |
| **ExtForEnum.cs** | `Enum` | 枚举处理、转换 | 6+ |
| **ExtForEnumable.cs** | `IEnumerable` | 集合操作、LINQ扩展 | 20+ |
| **ExtForCouroutine.cs** | `Coroutine` | 协程管理 | 5+ |
| **ExtForDateTime.cs** | `DateTime` | 时间处理、格式化 | 12+ |
| **ExtForUnityObject.cs** | `UnityEngine.Object` | Unity对象通用操作 | 8+ |
| **ExtNormal.cs** | `通用类型` | 常用扩展方法 | 10+ |
| **ReadMeExt.txt** | `开发模板` | 扩展方法开发规范 | 模板 |

## 🎯 主要功能分类

### 🎮 Unity核心扩展
- **GameObject**: 组件获取、激活管理、层级操作
- **Component**: 生命周期、查找、状态管理  
- **UnityObject**: 空值检查、销毁、克隆操作

### 🔢 数据类型扩展
- **String**: 截取、验证、格式化、转换、哈希
- **Number**: 范围检查、格式化、数学运算、插值
- **Enum**: 类型转换、描述获取、随机选择
- **DateTime**: 格式化、计算、时区转换

### 📐 数学与图形
- **Vector**: 数学运算、方向计算、插值、转换
- **Color**: 颜色空间转换、混合、亮度调整

### 🔄 集合与流程
- **IEnumerable**: LINQ增强、查找、转换、统计
- **Coroutine**: 启动、停止、管理、链式调用

### 🛠️ 通用工具
- **ExtNormal**: 空值处理、类型判断、通用操作

## 🚀 核心特性

### 性能优化
- ✅ 预编译正则表达式
- ✅ 对象池复用机制
- ✅ 内存分配优化
- ✅ 批量操作支持

### 安全性
- ✅ 空值保护机制
- ✅ 异常处理完善
- ✅ 类型安全检查
- ✅ 默认值支持

### 易用性
- ✅ 智能提示友好 (`_` 前缀)
- ✅ 链式调用支持
- ✅ 完整XML文档
- ✅ 丰富使用示例

## 📝 使用示例 (基于实际方法)

### 🔤 字符串处理 (ExtForString_Main.cs)
```csharp
string path = "Assets/Scripts/Test.cs";
string folder = path._KeepBeforeByLast("/");      // "Assets/Scripts"  
string filename = path._KeepAfterByLast("/");     // "Test.cs"
string name = filename._KeepBeforeByFirst(".");   // "Test"

bool valid = "user@test.com"._IsValidEmail();     // true
bool hasSpace = "hello world"._HasSpace();        // true
bool hasChinese = "Hello世界"._ContainsChineseCharacter(); // true

string messy = "if(true){var x=1;return x;}";
string formatted = messy._ToCode();               // 自动格式化代码

int number = "123"._AsInt(0);                     // 123
float price = "19.99"._AsFloat(0f);               // 19.99f
string hash = "password"._ToMD5Hash();            // MD5哈希值
```

### 🎮 GameObject操作 (ExtForGameObject.cs)  
```csharp
GameObject obj = new GameObject("TestObject");

// 获取或添加组件
Rigidbody rb = obj._GetOrAddComponent<Rigidbody>();
Component[] all = obj._GetAllComponents();

// 安全操作
obj._SafeSetActive(true);
obj._SafeToggleActive();
obj._SafeSetLayer(8, true);  // 设置层级，包含子物体

// 层级检查
LayerMask mask = 1 << 8;
bool inMask = obj._IsInLayerMask(mask);  // true
obj._SafeDestroy(2f);  // 2秒后销毁
```

### 📐 向量计算 (ExtForVector.cs)
```csharp
Vector3 pos = transform.position;

// 链式修改分量
Vector3 newPos = pos._WithY(10f)._WithX(5f)._WithZ(0f);
Vector3 noY = pos._NoY();  // Y设为0

// 向量运算
Vector3 scale = new Vector3(2, 3, 4);
Vector3 result = pos._MutiVector3(scale);  // 分量相乘

Vector3 divisor = new Vector3(2, 2, 2);
Vector3 divided = pos._SafeDivideVector3Safe(divisor);  // 安全除法

// 距离和判断
float distance = transform.position._DistanceToHorizontal(target.position);
bool nearZero = pos._IsApproximatelyZero(0.001f);
```

### 📊 集合操作 (ExtForEnumable.cs)
```csharp
var numbers = new List<int> { 1, 2, 3, 4, 5 };
var items = new string[] { "apple", "banana", "cherry" };

// 随机操作
int randomNum = numbers._RandomItem(-1);        // 随机数字，空时返回-1
string randomFruit = items._RandomItem("none"); // 随机水果

// 打乱顺序 (原地修改)
numbers._RandomShuffle();
items._RandomShuffle();

// 空值检查
bool isEmpty = numbers._IsNullOrEmpty();        // false
bool arrayEmpty = items._IsNullOrEmpty();       // false
```

### 🔢 数值操作 (ExtForNum.cs)
```csharp
float value = 15.7f;
int count = 42;

// 数值运算
float safe = value._SafeDivide(0f);           // 除零保护
float clamped = value._Clamp(0f, 10f);        // 限制范围: 10f
float normalized = value._Clamp01();          // 限制0-1: 1f

// 角度处理
float angle = 450f._AsNormalizeAngle();       // 归一化: 90f
float angle180 = angle._AsNormalizeAngle180(); // 限制±180: 90f

// 映射和插值
float remapped = value._Remap(0, 20, 0, 100); // 映射到新范围: 78.5f
float lerped = 0f._LerpTo(10f, 0.5f);         // 插值: 5f

// 判断
bool isEven = count._IsEven();                 // true
bool isOdd = count._IsOdd();                  // false
bool positive = value._IsPositive();          // true

// 格式化
string percent = 0.85f._ToString_Percentage(1); // "85.0%"
string ordinal = count._ToString_DateOrdinal();  // "42nd"
string roman = 9._ToString_Roman();              // "IX"
```

### 🎨 颜色操作 (ExtForColor.cs)
```csharp
Color color = Color.red;

// 通道修改
Color newColor = color._WithAlpha(0.5f);      // 半透明红色
Color blueish = color._WithB(1f);             // 紫色
Color rgb = color._WithRGB(0.8f, 0.2f, 0.9f); // 自定义RGB

// 透明度操作
Color faded = color._MultiplyAlpha(0.5f);     // 透明度减半
Color premult = color._RGBMultiAlpha();        // RGB预乘透明度

// 颜色转换
string hex = color._ToHex16String();            // "#FF0000"
Color fromHex = "#00FF00"._ColorFromHex();    // 绿色
Color inverted = color._Invert();             // 青色

// 亮度和灰度
Color darker = color._WithRGBMulti(0.5f);      // 变暗
Color gray = color._AsGrayscale();            // 转灰度
float brightness = color._GetGrayscale();      // 获取灰度值
```

### ⚡ 组件操作 (ExtForCompoent.cs)
```csharp
Component comp = GetComponent<Transform>();

// 距离计算
float dist = comp._DistanceTo(target);        // 到目标的距离
bool inRange = comp._IsInRange(enemy, 5f);    // 是否在5米内

// 组件获取
List<Transform> children = comp._GetCompoentsInChildExcludeSelf<Transform>();
Transform parent = comp._GetCompoentInParentExcludeSelf<Transform>();

// Transform专属
Transform t = transform;
t._Reset();                    // 重置位置旋转缩放
t._SetPositionY(10f);         // 设置Y位置
t._SetLocalPositionX(5f);     // 设置本地X位置

Transform[] oneLayer = t._GetChildrensOneLayer();  // 获取一层子物体
t._DestroyAllChildren();      // 销毁所有子物体

// 屏幕位置
Vector3 screenPos = comp._GetScreenPosition(Camera.main);
```

### 📅 时间处理 (ExtForDateTime.cs)
```csharp
// 时间显示格式化
float seconds = 3661f;  // 1小时1分1秒
string timeStr = seconds._ToStringDate_hh_mm_ss();  // "01:01:01"
string shortTime = seconds._ToStringDate_mm_ss();   // "61:01"
string chinese = seconds._ToStringDate_简短中文天小时分秒();  // "1.0小时"

// DateTime操作
DateTime now = DateTime.Now;
DateTime tomorrow = now.AddDays(1);

bool isToday = now._IsToday();           // true
bool isTomorrow = tomorrow._IsTomorrow(); // true
DateTime dayStart = now._StartOfDay();    // 当天00:00:00
DateTime dayEnd = now._EndOfDay();        // 当天23:59:59

// 时间差计算
int daysBetween = now._DaysBetween(tomorrow);      // 1
int daysFromNow = tomorrow._TotalDaysFromNowToThis(); // 1

// 相对时间
string relativeTime = now.AddHours(-2).ToStringDate_过去的中文相对时间表达(); // "2小时前"
```

### 🔖 枚举操作 (ExtForEnum.cs)
```csharp
[Flags]
public enum GameState
{
    None = 0,
    Playing = 1,
    Paused = 2,
    GameOver = 4
}

GameState state = GameState.Playing;

// 标志操作
GameState newState = state._AddFlag(GameState.Paused);     // Playing | Paused
GameState removed = newState._RemoveFlag(GameState.Playing); // Paused
GameState toggled = state._ToggleFlag(GameState.Paused);   // Playing | Paused

// 标志检查
bool hasAll = newState._HasAllFlags(GameState.Playing, GameState.Paused); // true
bool hasAny = state._HasAnyFlags(GameState.Paused, GameState.GameOver);   // false

// 枚举遍历
IEnumerable<GameState> allStates = ExtensionForEnum._GetEnumValues<GameState>();
GameState next = state._Next();        // 下一个枚举值
GameState prev = state._Previous();    // 上一个枚举值
GameState random = ExtensionForEnum._Random<GameState>(); // 随机枚举值

// 描述获取 (需要Description特性)
string desc = state._GetDescription();
bool defined = state._IsDefined();     // 检查是否有效
```

### 🛠️ 通用工具 (ExtNormal.cs)
```csharp
// 创建单元素集合
string item = "single";
List<string> singleList = item._AsListOnlySelf();      // ["single"]
string[] singleArray = item._AsArrayOnlySelf();       // ["single"]

```

### 🔄 协程操作 (ExtForCouroutine.cs)
```csharp
// 协程扩展
IEnumerator myCoroutine = WaitAndPrint();
myCoroutine._StartAt(this);  // 在当前MonoBehaviour上启动

private IEnumerator WaitAndPrint()
{
    yield return new WaitForSeconds(1f);
    Debug.Log("协程完成!");
}
```

### 🔗 Unity对象 (ExtForUnityObject.cs)
```csharp
GameObject obj = someGameObject;

// 安全调用
obj._TryUse()?.SetActive(true);  // 空值安全调用

// 获取GUID (仅编辑器)
string guid = obj._GetGUID();  // 资源GUID
```

