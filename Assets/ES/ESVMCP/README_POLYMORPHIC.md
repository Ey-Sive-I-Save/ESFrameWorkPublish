# ESVMCP 多态命令架构

## ✨ 核心特性

- ✅ **类型安全** - 编译时检查参数类型
- ✅ **自动序列化** - 支持Vector3、Color等Unity类型
- ✅ **IntelliSense支持** - 完整的代码提示
- ✅ **易于扩展** - 简单添加新命令类
- ✅ **运行时安全** - 减少JSON解析错误

## 📝 命令实现示例

### 基本命令结构
```csharp
[ESVMCPCommand("CreateCube", "创建一个立方体")]
public class CreateCubeCommand : ESVMCPCommandBase
{
    // 强类型参数
    [JsonProperty("position")]
    public Vector3 Position { get; set; }

    [JsonProperty("color")]
    public Color Color { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = "Cube";

    // 执行逻辑
    public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = Name;
        cube.transform.position = Position;

        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color;
        }

        return ESVMCPCommandResult.Succeed("立方体创建成功",
            new Dictionary<string, object> { ["gameObject"] = cube });
    }
}
```

### JSON使用
```json
{
  "commands": [
    {
      "type": "CreateCube",
      "id": "my_cube",
      "position": [0, 1, 0],
      "color": [1, 0, 0, 1],
      "name": "RedCube"
    }
  ]
}
```

## 🔧 架构优势

### 对比传统方式

**传统方式（字符串字典）:**
```csharp
// 不安全，运行时才知道错误
public override bool Execute(Dictionary<string, object> parameters)
{
    string name = (string)parameters["name"];  // 可能抛异常
    Vector3 pos = ParseVector3(parameters["position"]);  // 自定义解析
}
```

**多态方式（强类型）:**
```csharp
// 类型安全，编译时检查
[JsonProperty("name")]
public string Name { get; set; }

[JsonProperty("position")]
public Vector3 Position { get; set; }  // 自动解析
```

## 🎯 内置命令类型

### GameObject操作 (5个)
- `CreateGameObject` - 创建GameObject
- `DestroyGameObject` - 销毁GameObject
- `SetActiveGameObject` - 设置激活状态
- `CloneGameObject` - 克隆GameObject
- `FindGameObject` - 查找GameObject

### Transform操作 (6个)
- `SetPosition` - 设置位置
- `SetRotation` - 设置旋转
- `SetScale` - 设置缩放
- `SetParent` - 设置父对象
- `SetTransform` - 设置完整Transform
- `LookAt` - 看向目标

### Component操作 (3个)
- `AddComponent` - 添加组件
- `RemoveComponent` - 移除组件
- `SetComponentEnabled` - 启用/禁用组件

### Material操作 (3个)
- `CreateMaterial` - 创建材质
- `AssignMaterial` - 分配材质
- `CreatePrimitive` - 创建几何体+材质

### Memory操作 (5个)
- `SaveMemory` - 保存记忆
- `LoadMemory` - 加载记忆
- `RemoveMemory` - 移除记忆
- `ClearMemory` - 清空记忆
- `ExportMemory` - 导出记忆

**总计：22个命令类型**

## 🧠 记忆系统

### 双重架构
- **场景记忆**: MonoBehaviour，运行时数据
- **持久记忆**: ScriptableObject，跨会话数据

### 自动管理
- 命令结果自动保存到上下文
- 变量引用`{{command_id}}`自动解析
- 记忆导出为AI可读格式

## ⚙️ 配置系统

### 灵活配置
- 单个基础文件夹设置
- 自动派生子文件夹路径
- 运行时动态调整

### 编辑器集成
- Odin Inspector可视化配置
- 一键创建文件夹结构
- 实时验证配置有效性

## 🚀 快速开始

1. **安装系统**
   ```
   Tools > ESVMCP > 一键设置 > 完整安装ESVMCP
   ```

2. **创建命令**
   ```csharp
   [ESVMCPCommand("MyCommand", "描述")]
   public class MyCommand : ESVMCPCommandBase
   {
       // 定义参数和执行逻辑
   }
   ```

3. **使用命令**
   ```json
   {
     "commands": [
       {"type": "MyCommand", "参数": "值"}
     ]
   }
   ```

## 📚 技术细节

### 自动注册
- 反射扫描所有`ESVMCPCommand`特性
- 运行时构建命令类型映射
- 支持热重载和动态加载

### 类型转换器
- `Vector3Converter` - Unity向量类型
- `ColorConverter` - Unity颜色类型
- `EnumConverter` - 枚举类型支持

### 错误处理
- 编译时类型检查
- 运行时参数验证
- 详细错误报告和日志

### 性能优化
- 命令对象池复用
- 延迟执行避免阻塞
- 异步文件监视

## 🎨 最佳实践

### 命令设计
- 使用描述性的命令名称
- 提供有意义的默认值
- 实现参数验证逻辑

### 错误处理
- 返回具体的错误信息
- 使用适当的日志级别
- 提供恢复建议

### 记忆管理
- 合理使用场景vs持久记忆
- 及时清理不需要的数据
- 使用有意义的键名

### 扩展开发
- 遵循命名约定
- 添加必要的特性标签
- 编写单元测试

## 🔧 调试工具

### 编辑器工具
- 测试窗口 - 实时执行JSON
- 记忆查看器 - 检查记忆状态
- 日志查看器 - 执行历史记录

### 调试模式
```json
{
  "options": {
    "debugMode": true,
    "simulateExecution": true
  }
}
```

## 📖 相关文档

- [README.md](./README.md) - 主要使用指南
- [AI_INTERACTION_GUIDE.md](./AI_INTERACTION_GUIDE.md) - AI集成说明
- [IMPLEMENTATION_GUIDE.md](./IMPLEMENTATION_GUIDE.md) - 开发实现指南