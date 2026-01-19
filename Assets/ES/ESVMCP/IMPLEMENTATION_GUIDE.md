# ESVMCP 实现指南

## 📦 系统架构

ESVMCP采用多态命令架构，提供类型安全的JSON命令执行。

### 核心组件
- **ESVMCPConfig.cs** - 系统配置管理
- **ESVMCPCommandBase.cs** - 命令基类
- **ESVMCPCommandExecutor.cs** - 命令执行器
- **ESVMCPMemory.cs** - 场景记忆系统
- **ESVMCPMemoryAsset.cs** - 持久记忆系统

## 🎯 命令实现

### 创建新命令

1. **继承基类**
```csharp
[ESVMCPCommand("MyCommand", "我的命令描述")]
public class MyCommand : ESVMCPCommandBase
{
    [JsonProperty("target")]
    public string Target { get; set; }

    [JsonProperty("value")]
    public float Value { get; set; }
}
```

2. **实现执行逻辑**
```csharp
public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
{
    // 解析GameObject引用
    GameObject go = ResolveGameObject(Target, context);
    if (go == null)
    {
        return ESVMCPCommandResult.Failed($"未找到对象: {Target}");
    }

    // 执行逻辑
    // ...

    return ESVMCPCommandResult.Succeed("执行成功");
}
```

3. **可选：添加验证**
```csharp
public override ESVMCPValidationResult Validate()
{
    if (string.IsNullOrEmpty(Target))
    {
        return ESVMCPValidationResult.Failure("Target不能为空");
    }
    return ESVMCPValidationResult.Success();
}
```

## 📁 文件夹结构

```
Assets/ES/ESVMCP/RunningData/           # 运行时数据
├── Input/            # JSON命令输入
├── Archive/          # 执行完成的归档
├── Memory/           # 记忆导出文件
└── Logs/             # 执行日志

Assets/ES/ESVMCP/     # 插件代码
├── Core/             # 核心系统
├── Commands/         # 命令实现
├── Memory/           # 记忆系统
├── Editor/           # 编辑器工具
└── Examples/         # 示例文件
```

## 🔧 现有命令类型

### GameObject命令
- `CreateGameObject` - 创建空GameObject
- `DestroyGameObject` - 销毁GameObject
- `SetActiveGameObject` - 设置激活状态
- `CloneGameObject` - 克隆GameObject

### Transform命令
- `SetPosition` - 设置位置
- `SetRotation` - 设置旋转
- `SetScale` - 设置缩放
- `SetParent` - 设置父对象

### Component命令
- `AddComponent` - 添加组件
- `RemoveComponent` - 移除组件
- `SetComponentEnabled` - 启用/禁用组件

### Material命令
- `CreateMaterial` - 创建材质
- `AssignMaterial` - 分配材质
- `CreatePrimitive` - 创建基础几何体

### Memory命令
- `SaveMemory` - 保存记忆
- `LoadMemory` - 加载记忆
- `ExportMemory` - 导出记忆

## 🧠 记忆系统

### 场景记忆 (MonoBehaviour)
- 生命周期：场景运行时
- 存储：GameObject引用、临时数据
- 特点：场景销毁时清空

### 持久记忆 (ScriptableObject)
- 生命周期：跨场景、跨会话
- 存储：项目级配置、长期数据
- 特点：保存为资产文件

## ⚙️ 配置系统

ESVMCPConfig资产控制：
- **BaseFolder**: 基础数据文件夹路径
- **CheckInterval**: 文件监视间隔
- **AutoExecute**: 自动执行开关
- **StopOnError**: 错误时停止执行
- **EnableMemory**: 启用记忆系统

## 🎨 编辑器工具

### 测试工具
```
Tools > ESVMCP > 测试工具 > 打开测试窗口
```

### 快速设置
```
Tools > ESVMCP > 一键设置 > 完整安装ESVMCP
```

## 📊 调试和日志

- 控制台输出详细执行日志
- 错误信息包含堆栈跟踪
- 执行报告显示成功/失败状态
- 记忆导出用于状态检查

## 🚀 扩展指南

1. **添加新命令**：继承`ESVMCPCommandBase`，添加`[ESVMCPCommand]`特性
2. **自定义类型**：使用`JsonConverter`处理复杂类型
3. **错误处理**：返回适当的`ESVMCPCommandResult`
4. **验证逻辑**：实现`Validate()`方法进行参数检查

## 📚 更多资源

- [README.md](./README.md) - 主要使用文档
- [AI_INTERACTION_GUIDE.md](./AI_INTERACTION_GUIDE.md) - AI集成指南
- [README_POLYMORPHIC.md](./README_POLYMORPHIC.md) - 技术实现详情