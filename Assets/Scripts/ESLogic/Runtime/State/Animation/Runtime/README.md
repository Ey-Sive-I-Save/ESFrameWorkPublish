# AnimationCalculatorRuntime - 统一运行时数据

## 📋 概述

`AnimationCalculatorRuntime` 是所有动画计算器共享的统一运行时数据类,替代了之前每个Calculator独立的RuntimeData子类。

## 🎯 设计目标

- **统一类型**: 所有Calculator使用同一个Runtime类型,简化类型转换
- **方便使用**: 无需频繁的类型转换(`as`操作符)
- **字段复用**: 不同Calculator复用相同字段,减少重复定义
- **可扩展**: 新增Calculator时只需添加字段,无需创建新类

## 📦 包含字段

### 通用字段
```csharp
public bool IsInitialized;                    // 初始化标记
public AnimationMixerPlayable mixer;          // Mixer (BlendTree/Direct)
public AnimationClipPlayable[] playables;     // Clip数组
public AnimationClipPlayable singlePlayable;  // 单个Clip (SimpleClip)
```

### 1D混合树专用
```csharp
public float lastInput;           // 上一帧输入值
public float inputVelocity;       // 输入平滑速度
```

### 2D混合树专用
```csharp
public Vector2 lastInput2D;       // 上一帧2D输入
public Vector2 inputVelocity2D;   // 2D输入平滑速度
public Triangle[] triangles;      // Delaunay三角形缓存
```

### Direct混合专用
```csharp
public float[] currentWeights;    // 当前权重
public float[] targetWeights;     // 目标权重
public float[] weightVelocities;  // 权重变化速度
```

## 💡 使用示例

### 单个角色
```csharp
// 创建配置(可序列化,可共享)
var calculator = new BlendTree1DCalculator { ... };

// 创建运行时数据(独立实例)
var runtime = calculator.CreateRuntimeData();

// 初始化
Playable output = Playable.Null;
calculator.Initialize(runtime, graph, ref output);

// 每帧更新
calculator.UpdateWeights(runtime, context, deltaTime);

// 清理
runtime.Cleanup();
```

### 享元模式 - 多角色共享配置
```csharp
// 1个共享配置
var sharedCalculator = new BlendTree1DCalculator { ... };

// 100个角色,每个独立运行时
var runtimes = new AnimationCalculatorRuntime[100];
for (int i = 0; i < 100; i++)
{
    runtimes[i] = sharedCalculator.CreateRuntimeData();
    Playable output = Playable.Null;
    sharedCalculator.Initialize(runtimes[i], graph, ref output);
}

// 每个角色独立更新
for (int i = 0; i < 100; i++)
{
    sharedCalculator.UpdateWeights(runtimes[i], context, deltaTime);
}
```

## 🔍 内存占用分析

### 单个Runtime实例
```
基础大小: ~200 bytes
- Playable引用: 16 bytes × 3
- float字段: 4 bytes × 2
- Vector2字段: 8 bytes × 2
- 数组引用: 8 bytes × 4
```

### 实际占用(取决于Clip数量)
```
SimpleClip:    ~50 bytes  (仅1个Playable)
BlendTree1D:   ~300 bytes (4个Clip场景)
BlendTree2D:   ~500 bytes (8个Clip + 三角形缓存)
DirectBlend:   ~400 bytes (6个Clip + 权重数组)
```

### 享元模式优势
```
100个角色场景:
- 配置: 1个 × 4 KB = 4 KB
- 运行时: 100个 × 300 bytes = 30 KB
- 总计: 34 KB

传统方式:
- 配置+运行时: 100个 × 4 KB = 400 KB
- 节省: 366 KB (91.5%)
```

## ⚠️ 注意事项

### 未使用字段
- 某些Calculator不会使用所有字段(例如SimpleClip不使用mixer)
- 这是**空间换便利**的设计权衡
- 未使用字段通常为null,不占用额外heap内存

### 线程安全
- Runtime对象**不是线程安全**的
- 每个线程应使用独立的Runtime实例
- Playable本身是Unity托管,自动处理线程安全

### 生命周期
- Runtime必须在不再需要时调用`Cleanup()`
- Cleanup会销毁所有Playable,释放GPU资源
- 配置对象可以长期持有,Runtime应及时释放

## 🔧 扩展指南

### 添加新Calculator类型
1. 在`AnimationCalculatorRuntime`中添加需要的字段
2. 在Calculator中重写`CreateRuntimeData()`(默认实现通常够用)
3. 实现`Initialize/UpdateWeights/GetCurrentClip`方法

```csharp
public class MyNewCalculator : AnimationClipPlayableCalculator
{
    // 如果需要特殊初始化,可以重写(通常不需要)
    public override AnimationCalculatorRuntime CreateRuntimeData()
    {
        var runtime = base.CreateRuntimeData();
        // 特殊初始化逻辑
        return runtime;
    }
    
    public override bool Initialize(AnimationCalculatorRuntime runtime, ...)
    {
        // 使用runtime.mixer, runtime.playables等
    }
}
```

## 📊 性能特性

- **零GC**: Runtime在Update中不产生GC allocation
- **缓存友好**: 字段紧密排列,访问性能好
- **池化友好**: 可以配合对象池使用,进一步减少GC

## 🔗 相关文件

- `StateAnimationConfigData.cs` - Calculator配置类
- `AnimationCalculatorUsageExample.cs` - 使用示例
- `ES_REFCOUNT_USAGE_GUIDE.md` - 引用计数指南
