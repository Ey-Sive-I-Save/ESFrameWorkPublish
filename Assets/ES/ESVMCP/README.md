# ESVMCP - Unity JSON命令执行系统

## 🚀 快速开始

### 1. 初始化系统
```bash
# 在Unity菜单中
Tools > ESVMCP > 一键设置 > 完整安装ESVMCP
```

### 2. 创建指令包JSON文件
```json
{
  "commandId": "my_workflow_step",
  "description": "我的工作流步骤",
  "order": 1,
  "enabled": true,
  "timestamp": "2026-01-20T10:00:00",
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "cube",
      "name": "MyCube",
      "primitiveType": "Cube",
      "position": [0, 1, 0],
      "color": [1, 0, 0, 1]
    }
  ]
}
```

### 3. 执行方式

#### 单个执行
将JSON文件放入 `Assets/ES/ESVMCP/RunningData/Input/` 文件夹，然后：
```
Tools > ESVMCP > 测试工具 > 打开测试窗口
```
选择文件并点击"执行 JSON 命令"

#### 批量执行
将多个JSON文件放入 `Assets/ES/ESVMCP/RunningData/Input/` 文件夹，然后：
```
Tools > ESVMCP > 测试工具 > 🔄 批量执行 Input 文件夹
```
系统将按 `order` 字段排序，依次执行所有启用的文件，执行完成后自动移动到 `Archive/` 文件夹。

**注意**: 系统会自动同时移动JSON文件及其对应的 `.meta` 文件，避免Unity资产数据库错误。

## 📋 系统特性

- ✅ **多态命令架构** - 类型安全的命令实现
- ✅ **指令包系统** - 支持多命令组合执行
- ✅ **工作流管理** - 按顺序批量处理多个JSON文件
- ✅ **自动文件管理** - 执行完成后自动归档
- ✅ **记忆系统** - 场景记忆 + 持久记忆
- ✅ **实时监视** - 文件夹自动检测新命令
- ✅ **错误恢复** - 详细的执行报告和日志
- ✅ **编辑器工具** - 可视化测试和调试

## 📦 指令包系统

每个JSON文件称为一个"指令包"，包含：

### 指令包结构
```json
{
  "commandId": "unique_identifier",
  "description": "指令包描述",
  "order": 1,
  "enabled": true,
  "timestamp": "2026-01-20T10:00:00",
  "memory": {
    "load": ["key1", "key2"],
    "save": {
      "result_key": "value"
    }
  },
  "commands": [
    // 多个命令按顺序执行
  ]
}
```

### 字段说明
- **commandId**: 唯一标识符
- **description**: 描述信息
- **order**: 执行顺序 (数字，越小越先执行)
- **enabled**: 是否启用 (true/false)
- **timestamp**: 时间戳
- **memory**: 记忆系统配置
- **commands**: 命令列表

## 🔄 工作流管理

### 批量执行流程
1. 将多个指令包放入 `Data/ESVMCP/Input/` 文件夹
2. 每个文件设置不同的 `order` 值
3. 点击"批量执行"按钮
4. 系统按顺序执行所有启用的指令包
5. 成功执行的文件自动移动到 `Archive/` 文件夹

### 执行顺序规则
- 按 `order` 字段升序排序
- `order` 相同则按文件名排序
- 只执行 `enabled: true` 的文件
- 支持错误时停止或继续的配置

### GameObject操作
- `CreateGameObject` - 创建游戏对象
- `DestroyGameObject` - 销毁游戏对象
- `SetActiveGameObject` - 设置激活状态
- `CloneGameObject` - 克隆游戏对象

### Transform操作
- `SetPosition` - 设置位置
- `SetRotation` - 设置旋转
- `SetScale` - 设置缩放
- `SetParent` - 设置父对象

### Component操作
- `AddComponent` - 添加组件
- `RemoveComponent` - 移除组件
- `SetComponentEnabled` - 启用/禁用组件

### Material操作
- `CreateMaterial` - 创建材质
- `AssignMaterial` - 分配材质
- `CreatePrimitive` - 创建基础几何体

### Memory操作
- `SaveMemory` - 保存记忆
- `LoadMemory` - 加载记忆
- `ExportMemory` - 导出记忆

## 📁 文件夹结构

```
Assets/ES/ESVMCP/RunningData/     # 数据文件夹
├── Input/                       # 待执行的JSON文件
├── Archive/                     # 已执行的JSON归档
├── Memory/                      # 导出的记忆文件
└── Logs/                        # 执行日志

Assets/ES/ESVMCP/                # 插件代码
├── Core/                        # 核心系统
├── Commands/                    # 命令实现
├── Memory/                      # 记忆系统
├── Editor/                      # 编辑器工具
└── Examples/                    # 示例文件
```

## ⚙️ 配置选项

在 `ESVMCPConfig` 资产中配置：

- **基础文件夹**: `Assets/ES/ESVMCP/RunningData` - 所有数据的根目录
- **检查间隔**: 1.0秒 - 监视输入文件夹的频率
- **自动执行**: 开启 - 检测到新文件时自动执行
- **遇错停止**: 关闭 - 遇到错误时是否停止执行
- **启用记忆**: 开启 - 是否启用记忆系统

## 🧠 记忆系统

### 场景记忆 (ESVMCPMemory)
- 生命周期: 场景运行时
- 用途: 临时存储GameObject引用和运行时数据

### 持久记忆 (ESVMCPMemoryAsset)
- 生命周期: 跨场景、跨会话
- 用途: 存储需要长期保留的项目数据

## 🎨 使用示例

### 单个指令包
```json
{
  "commandId": "create_scene",
  "description": "创建基础场景",
  "order": 1,
  "enabled": true,
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "cube",
      "name": "RedCube",
      "primitiveType": "Cube",
      "position": [0, 1, 0],
      "color": [1, 0, 0, 1]
    }
  ]
}
```

### 工作流示例
创建以下文件到 `Assets/ES/ESVMCP/RunningData/Input/` 文件夹：

**step_01_init.json** (order: 1)
```json
{
  "commandId": "init_scene",
  "description": "初始化场景",
  "order": 1,
  "enabled": true,
  "commands": [
    {"type": "CreateGameObject", "id": "root", "name": "SceneRoot"}
  ]
}
```

**step_02_floor.json** (order: 2)
```json
{
  "commandId": "create_floor", 
  "description": "创建地板",
  "order": 2,
  "enabled": true,
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "floor",
      "name": "Floor",
      "primitiveType": "Cube",
      "position": [0, 0, 0],
      "scale": [20, 0.5, 20],
      "color": [0.8, 0.8, 0.8, 1]
    }
  ]
}
```

**step_03_objects.json** (order: 3)
```json
{
  "commandId": "add_objects",
  "description": "添加场景对象",
  "order": 3,
  "enabled": true,
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "cube1",
      "name": "Cube1",
      "primitiveType": "Cube",
      "position": [0, 2, 0],
      "color": [1, 0, 0, 1]
    }
  ]
}
```

然后执行批量处理，系统会按1→2→3的顺序执行这些步骤。

## 🔧 自定义命令

```csharp
[ESVMCPCommand("MyCustomCommand", "我的自定义命令")]
public class MyCustomCommand : ESVMCPCommandBase
{
    [JsonProperty("target")]
    public string Target { get; set; }

    public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
    {
        // 实现你的逻辑
        return ESVMCPCommandResult.Succeed("执行成功");
    }
}
```

## 🐛 调试和测试

### 测试窗口
```
Tools > ESVMCP > 测试工具 > 打开测试窗口
```

### 执行示例
```
Tools > ESVMCP > 测试工具 > 执行 - 基础场景示例
Tools > ESVMCP > 测试工具 > 执行 - 记忆系统示例
```

## 📚 更多文档

- [AI交互指南](./AI_INTERACTION_GUIDE.md) - 详细的AI使用说明
- [多态命令架构](./README_POLYMORPHIC.md) - 技术实现详情
- [实现指南](./IMPLEMENTATION_GUIDE.md) - 开发和扩展指南

---

**ESVMCP - 让AI通过JSON命令控制Unity编辑器！** 🚀
