# ESVMCP AI 交互指南

## 🤖 快速开始

ESVMCP 是 Unity 的 JSON 命令执行系统，让 AI 可以通过 JSON 文件控制 Unity 编辑器。

**核心工作流：**
1. AI 分析用户需求 → 生成 JSON 命令
2. 用户将 JSON 放入 `Assets/ES/ESVMCP/RunningData/Input/` 文件夹
3. ESVMCP 自动检测并执行命令
4. 返回执行结果和记忆数据

**重要提示**: 系统会自动处理 Unity 的 `.meta` 文件，避免资产数据库错误。

---

## 📝 JSON 命令格式

### 基础结构
```json
{
  "commandId": "unique_id",
  "description": "命令描述",
  "commands": [
    {
      "type": "CommandType",
      "id": "optional_id",
      "param1": "value1",
      "param2": [1, 2, 3]
    }
  ],
  "memory": {
    "load": ["key1", "key2"],
    "save": {
      "new_key": "{{reference}}"
    }
  }
}
```

### 变量引用
- `{{command_id}}` - 引用命令创建的对象
- `{{memory_key}}` - 引用记忆中的值
- `{{command_id.property}}` - 引用命令输出属性

---

## 🎯 核心命令

### 🎨 创建与修改

#### 创建几何体
```json
{
  "type": "CreatePrimitive",
  "id": "cube",
  "name": "RedCube",
  "primitiveType": "Cube",
  "position": [0, 1, 0],
  "scale": [1, 1, 1],
  "color": [1, 0, 0, 1]
}
```

#### 创建空对象
```json
{
  "type": "CreateGameObject",
  "id": "container",
  "name": "Container",
  "position": [0, 0, 0]
}
```

#### 添加组件
```json
{
  "type": "AddComponent",
  "target": "{{cube}}",
  "component": "Rigidbody"
}
```

### 📐 Transform 操作

#### 设置位置
```json
{
  "type": "SetPosition",
  "target": "{{cube}}",
  "position": [2, 1, 0]
}
```

#### 设置旋转
```json
{
  "type": "SetRotation",
  "target": "{{cube}}",
  "rotation": [0, 45, 0]
}
```

#### 设置缩放
```json
{
  "type": "SetScale",
  "target": "{{cube}}",
  "scale": [2, 2, 2]
}
```

#### 设置父对象
```json
{
  "type": "SetParent",
  "target": "{{cube}}",
  "parent": "{{container}}"
}
```

### 🧠 记忆操作

#### 保存记忆
```json
{
  "type": "SaveMemory",
  "key": "my_object",
  "value": "{{cube}}",
  "persistent": true
}
```

#### 加载记忆
```json
{
  "type": "LoadMemory",
  "key": "my_object",
  "persistent": true
}
```

#### 导出记忆
```json
{
  "type": "ExportMemory",
  "format": "json"
}
```

---

## 📋 AI 响应模板

### 🎯 标准响应格式

当用户请求 Unity 操作时，请按以下格式响应：

```
我来帮你[简要描述操作]。

请将以下 JSON 保存为文件，放入 Assets/ES/ESVMCP/RunningData/Input/ 文件夹：

[JSON 代码块]

执行后将会：
- [结果1]
- [结果2]
- [保存到记忆的键值]
```

### 💡 示例响应

**用户**: "创建一个红色的立方体在位置 (0,1,0)"

**AI响应**:
```
我来帮你创建一个红色的立方体。

请将以下 JSON 保存为文件，放入 Assets/ES/ESVMCP/RunningData/Input/ 文件夹：

```json
{
  "commandId": "create_red_cube",
  "description": "创建红色的立方体",
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "red_cube",
      "name": "RedCube",
      "primitiveType": "Cube",
      "position": [0, 1, 0],
      "color": [1, 0, 0, 1]
    }
  ],
  "memory": {
    "save": {
      "red_cube_id": "{{red_cube}}"
    }
  }
}
```

执行后将会创建一个红色的立方体，并将其 ID 保存到记忆中以便后续使用。
```

---

## 🎨 完整场景示例

### 🏠 创建房间
```json
{
  "commandId": "create_room",
  "description": "创建完整的房间场景",
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "floor",
      "name": "Floor",
      "primitiveType": "Plane",
      "position": [0, 0, 0],
      "scale": [10, 1, 10],
      "color": [0.8, 0.8, 0.8, 1]
    },
    {
      "type": "CreatePrimitive",
      "id": "wall_north",
      "name": "Wall_North",
      "primitiveType": "Cube",
      "position": [0, 2.5, 5],
      "scale": [10, 5, 0.2],
      "color": [0.9, 0.9, 0.9, 1]
    },
    {
      "type": "CreatePrimitive",
      "id": "wall_south",
      "name": "Wall_South",
      "primitiveType": "Cube",
      "position": [0, 2.5, -5],
      "scale": [10, 5, 0.2],
      "color": [0.9, 0.9, 0.9, 1]
    },
    {
      "type": "CreatePrimitive",
      "id": "wall_east",
      "name": "Wall_East",
      "primitiveType": "Cube",
      "position": [5, 2.5, 0],
      "scale": [0.2, 5, 10],
      "color": [0.9, 0.9, 0.9, 1]
    },
    {
      "type": "CreatePrimitive",
      "id": "wall_west",
      "name": "Wall_West",
      "primitiveType": "Cube",
      "position": [-5, 2.5, 0],
      "scale": [0.2, 5, 10],
      "color": [0.9, 0.9, 0.9, 1]
    }
  ],
  "memory": {
    "save": {
      "room_floor": "{{floor}}",
      "room_walls": ["{{wall_north}}", "{{wall_south}}", "{{wall_east}}", "{{wall_west}}"]
    }
  }
}
```

### 🪑 添加家具
```json
{
  "commandId": "add_furniture",
  "description": "在房间中添加家具",
  "memory": {
    "load": ["room_floor"]
  },
  "commands": [
    {
      "type": "CreatePrimitive",
      "id": "table",
      "name": "Table",
      "primitiveType": "Cube",
      "position": [0, 0.8, 0],
      "scale": [2, 0.1, 1],
      "color": [0.6, 0.4, 0.2, 1]
    },
    {
      "type": "CreatePrimitive",
      "id": "chair1",
      "name": "Chair1",
      "primitiveType": "Cube",
      "position": [1.5, 0.5, 0],
      "scale": [0.8, 1, 0.8],
      "color": [0.4, 0.2, 0.1, 1]
    },
    {
      "type": "CreatePrimitive",
      "id": "chair2",
      "name": "Chair2",
      "primitiveType": "Cube",
      "position": [-1.5, 0.5, 0],
      "scale": [0.8, 1, 0.8],
      "color": [0.4, 0.2, 0.1, 1]
    }
  ],
  "memory": {
    "save": {
      "room_furniture": ["{{table}}", "{{chair1}}", "{{chair2}}"]
    }
  }
}
```

---

## 📚 命令参考表

| 命令类型 | 描述 | 必需参数 | 可选参数 |
|---------|------|---------|---------|
| **创建类** | | | |
| CreatePrimitive | 创建基础几何体 | primitiveType | name, position, rotation, scale, color |
| CreateGameObject | 创建空对象 | - | name, position, rotation, scale, parent |
| CloneGameObject | 克隆对象 | target | name, position |
| **Transform类** | | | |
| SetPosition | 设置位置 | target, position | - |
| SetRotation | 设置旋转 | target, rotation | - |
| SetScale | 设置缩放 | target, scale | - |
| SetParent | 设置父对象 | target, parent | - |
| **组件类** | | | |
| AddComponent | 添加组件 | target, component | - |
| RemoveComponent | 移除组件 | target, component | - |
| SetComponentEnabled | 启用/禁用组件 | target, component, enabled | - |
| **记忆类** | | | |
| SaveMemory | 保存记忆 | key, value | persistent |
| LoadMemory | 加载记忆 | key | persistent |
| ExportMemory | 导出记忆 | - | format |

---

## 🎯 数据类型规范

### 坐标与向量
- **Position**: `[x, y, z]` - 世界坐标位置
- **Rotation**: `[x, y, z]` - 欧拉角旋转 (度)
- **Scale**: `[x, y, z]` - 缩放比例

### 颜色
- **Color**: `[r, g, b, a]` - RGBA 值 (0.0-1.0)
- **支持格式**: `[1, 0, 0, 1]` 或 `"#FF0000"`

### 其他
- **String**: `"文本内容"` - 名称、路径等
- **Bool**: `true`/`false` - 开关状态
- **Float**: `1.5` - 数值参数

---

## 🚀 最佳实践

### 1. 命令设计
- ✅ 使用有意义的 `commandId` 和 `id`
- ✅ 添加清晰的 `description`
- ✅ 合理使用记忆保存重要对象

### 2. 错误处理
- ✅ 验证参数完整性
- ✅ 使用变量引用而非硬编码值
- ✅ 提供有意义的错误信息

### 3. 性能优化
- ✅ 批量操作合并到单个 JSON
- ✅ 合理使用命令延迟
- ✅ 及时清理不需要的记忆

### 4. 记忆管理
- ✅ 保存重要的 GameObject 引用
- ✅ 使用描述性的记忆键名
- ✅ 区分场景记忆和持久记忆

---

## 🔧 调试技巧

### 查看执行结果
- 检查 `Assets/ES/ESVMCP/RunningData/Archive/` 中的归档文件
- 查看 `Assets/ES/ESVMCP/RunningData/Logs/` 中的日志
- 使用编辑器工具查看记忆状态

### 常见问题
- **命令未执行**: 检查 JSON 格式和文件夹路径
- **对象未找到**: 确认变量引用和记忆键名
- **组件添加失败**: 检查组件名称拼写

---

## 📞 快速帮助

**忘记命令格式？** 查看上面的"JSON 命令格式"部分

**需要完整示例？** 参考"完整场景示例"部分

**找不到合适命令？** 查看"命令参考表"

**AI 不知道怎么响应？** 参考"AI 响应模板"

---

*ESVMCP - 让 AI 像专业 Unity 开发者一样工作！* 🚀