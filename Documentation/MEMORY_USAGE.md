# ESVMCP Memory 使用指南

## 概述

ESVMCP 提供了两种记忆操作方式：

1. **顶层 memory 部分** - 在命令序列执行前后统一处理记忆（推荐用于批量操作）
2. **SaveMemory/LoadMemory 命令** - 在命令流程中精确控制记忆操作（推荐用于细粒度控制）

## 🎯 记忆保存机制重构

### 多态设计：命令级记忆保存

**新的设计理念**：每个命令都可以独立决定如何保存记忆，不再依赖全局的 `memory.save` 部分。

#### 命令基类的新方法

```csharp
public abstract class ESVMCPCommandBase
{
    // ... 其他属性 ...

    /// <summary>
    /// 命令执行后的记忆保存（多态设计）
    /// 每个命令可以重写此方法，实现自己独立的记忆保存逻辑
    /// </summary>
    public virtual void SaveToMemory(ESVMCPCommandResult result, ESVMCPExecutionContext context)
    {
        // 默认实现：如果SaveToMemory为true，使用PostExecute保存
        if (SaveToMemory)
        {
            PostExecute(result, context);
        }
    }
}
```

#### 执行流程变化

**旧流程**：
```
解析JSON → 执行命令 → 处理全局 memory.save → 生成报告
```

**新流程**：
```
解析JSON → 执行命令 → 调用命令.SaveToMemory() → 生成报告
```

### 📝 命令级记忆控制

每个命令现在可以通过以下属性控制记忆保存：

```json
{
  "type": "CreateGameObject",
  "id": "myObject",
  "saveToMemory": true,        // 是否保存到记忆（默认true）
  "memoryKey": "player",       // 记忆键名（可选，默认使用id）
  "persistent": false          // 是否持久化（默认false）
}
```

### 🔧 自定义记忆保存

**子类重写示例**：

```csharp
public class CreateGameObjectCommand : ESVMCPCommandBase
{
    public override void SaveToMemory(ESVMCPCommandResult result, ESVMCPExecutionContext context)
    {
        if (!result.Success || context.SceneMemory == null) return;
        
        // 自定义保存逻辑
        var createdObject = result.OutputData["gameObject"] as GameObject;
        if (createdObject != null)
        {
            // 保存到短期记忆
            context.SceneMemory.SaveGameObject(MemoryKey, createdObject, Persistent);
            
            // 额外保存对象信息
            var objectInfo = new Dictionary<string, object>
            {
                { "name", createdObject.name },
                { "position", createdObject.transform.position },
                { "createdAt", DateTime.Now }
            };
            context.SceneMemory.SavePrimitive($"{MemoryKey}_info", objectInfo);
        }
    }
}
```

### ✅ 优势

- **独立性**：每个命令独立控制自己的记忆保存逻辑
- **灵活性**：可以根据命令特点实现不同的保存策略
- **可扩展性**：新命令可以轻松添加自定义记忆逻辑
- **简洁性**：不再需要全局的 memory.save 配置
- **类型安全**：编译时检查，避免运行时错误

### 📋 迁移指南

**对于现有命令**：
- 默认行为保持不变（使用 `PostExecute`）
- 可以选择重写 `SaveToMemory` 方法实现自定义逻辑

**对于新命令**：
- 继承 `ESVMCPCommandBase`
- 根据需要重写 `SaveToMemory` 方法
- 使用 `context.SceneMemory` 或 `context.PersistentMemory` 保存数据

**JSON 配置简化**：
```json
// 旧的复杂配置
{
  "memory": {
    "save": {
      "result": "{{output.data}}",
      "config": {"value": "{{output.config}}", "longTerm": true}
    }
  },
  "commands": [...]
}

// 新的简化配置
{
  "commands": [
    {
      "type": "MyCommand",
      "saveToMemory": true,
      "persistent": true
    }
  ]
}
```

## 两种记忆类型

### 短期记忆 (Scene Memory)
- 存储在场景组件 `ESVMCPMemoryEnhanced` 中
- **生命周期**: 场景存在期间
- **用途**: 临时数据、场景内对象引用、会话状态
- **默认行为**: 所有操作默认使用短期记忆

### 长期记忆 (Persistent Memory)
- 存储在资源文件 `ESVMCPMemoryAssetEnhanced` 中
- **生命周期**: 持久化，跨场景、跨会话
- **用途**: 配置数据、用户偏好、跨场景状态
- **加载方式**: 通过全局配置 `ESVMCPConfig.Instance.GetPersistentMemory()` 获取
- **统一管理**: 所有组件都使用全局配置引用，避免重复加载

## 方式一：顶层 memory 部分（推荐用于预检查和批量保存）

### JSON 结构

```json
{
  "commandId": "example_001",
  "description": "示例命令",
  "memory": {
    "load": ["key1", "key2"],           // 预检查这些键是否存在
    "save": {
      "result": "{{output.data}}",      // 保存到短期记忆
      "config": {                        // 保存到长期记忆
        "value": "{{output.config}}",
        "longTerm": true
      }
    }
  },
  "commands": [...]
}
```

### memory.load - 预检查记忆键

**作用**: 在命令执行前检查指定的记忆键是否存在
- ✓ 如果存在，记录日志并显示当前值
- ⚠ 如果不存在，记录警告（不会阻止执行）

**执行顺序**: 
1. 优先检查短期记忆
2. 如果短期记忆没有，再检查长期记忆

**示例**:
```json
"memory": {
  "load": ["playerName", "lastScore", "gameConfig"]
}
```

**日志输出**:
```
✓ 短期记忆可用: playerName = John
✓ 长期记忆可用: gameConfig = {"difficulty": "hard"}
⚠ 记忆键不存在: 'lastScore' (将在后续命令中创建)
```

### memory.save - 批量保存结果

**作用**: 在所有命令执行完成后，统一保存数据到记忆

**两种保存格式**:

#### 1. 简单格式（保存到短期记忆）
```json
"memory": {
  "save": {
    "key1": "simple value",
    "key2": "{{result.someData}}"  // 支持变量引用
  }
}
```

#### 2. 完整格式（指定长期/短期）
```json
"memory": {
  "save": {
    "tempData": "short term value",           // 短期记忆（默认）
    "config": {
      "value": "persistent value",             // 长期记忆
      "longTerm": true
    },
    "userName": {
      "value": "{{input.name}}",               // 支持变量引用
      "longTerm": true
    }
  }
}
```

**变量引用支持**:
- `{{output.xxx}}` - 引用命令输出数据
- `{{result.xxx}}` - 引用命令结果
- `{{memory.xxx}}` - 引用现有记忆数据
- `{{input.xxx}}` - 引用输入参数

**执行时机**: 
- 在所有 commands 执行完成后
- 在生成执行报告前

## 方式二：SaveMemory/LoadMemory 命令（推荐用于精确控制）

### SaveMemory 命令

在命令流程中的任意位置保存数据。

```json
{
  "type": "SaveMemory",
  "id": "save_config",
  "key": "gameConfig",
  "value": {
    "difficulty": "hard",
    "volume": 0.8
  },
  "longTerm": true,  // true=长期记忆, false=短期记忆（默认）
  "overwrite": true
}
```

### LoadMemory 命令

在命令流程中的任意位置加载数据。

```json
{
  "type": "LoadMemory",
  "id": "load_config",
  "key": "gameConfig",
  "longTerm": false  // 优先从短期记忆加载，如果没有再尝试长期记忆
}
```

## 选择哪种方式？

### 使用顶层 memory 的场景：

✅ **预检查依赖**: 确保命令执行前某些数据已存在
```json
"memory": {
  "load": ["必需的配置键1", "必需的配置键2"]
}
```

✅ **批量保存结果**: 命令执行完成后统一保存多个结果
```json
"memory": {
  "save": {
    "totalScore": "{{output.score}}",
    "timestamp": "{{output.time}}",
    "userName": {
      "value": "{{output.name}}",
      "longTerm": true
    }
  }
}
```

✅ **简化 JSON**: 避免在命令流程中添加额外的 SaveMemory 命令
```json
"commands": [
  {"type": "CreateGameObject", ...},
  {"type": "SetPosition", ...}
  // 不需要单独的 SaveMemory 命令
],
"memory": {
  "save": {"result": "{{output.finalObject}}"}
}
```

### 使用 SaveMemory/LoadMemory 命令的场景：

✅ **条件保存**: 根据前一个命令的结果决定是否保存
```json
"commands": [
  {"type": "CheckCondition", "id": "check"},
  {
    "type": "ConditionalExecute",
    "condition": "{{check.result}} == true",
    "commands": [
      {"type": "SaveMemory", "key": "success", "value": "true"}
    ]
  }
]
```

✅ **中间数据传递**: 在命令之间传递数据
```json
"commands": [
  {"type": "CreateGameObject", "id": "create"},
  {"type": "SaveMemory", "key": "tempObj", "value": "{{create.gameObject}}"},
  {"type": "LoadMemory", "key": "tempObj", "id": "load"},
  {"type": "SetPosition", "target": "{{load.value}}"}
]
```

✅ **精确时机控制**: 需要在特定命令之间保存/加载
```json
"commands": [
  {"type": "Step1", ...},
  {"type": "SaveMemory", "key": "checkpoint1"},
  {"type": "Step2", ...},
  {"type": "LoadMemory", "key": "checkpoint1"},
  {"type": "Step3", ...}
]
```

## 最佳实践

### 1. 短期 vs 长期的选择原则

**短期记忆**（默认）:
- GameObject 引用
- 临时计算结果
- 当前场景状态
- 会话临时数据

**长期记忆**（需要 `longTerm: true`）:
- 用户配置
- 游戏进度
- 跨场景数据
- 需要持久化的状态

### 2. 混合使用示例

```json
{
  "commandId": "game_session_001",
  "description": "游戏会话管理",
  
  "memory": {
    "load": ["gameConfig", "playerProfile"],  // 预检查长期配置
    "save": {
      "sessionId": "{{output.sessionId}}",    // 短期：会话ID
      "lastPlayTime": {                        // 长期：最后游戏时间
        "value": "{{output.timestamp}}",
        "longTerm": true
      }
    }
  },
  
  "commands": [
    {
      "type": "LoadMemory",
      "key": "gameConfig",
      "longTerm": true,
      "id": "config"
    },
    {
      "type": "CreateGameObject",
      "name": "Player",
      "id": "player"
    },
    {
      "type": "SaveMemory",
      "key": "currentPlayer",
      "value": "{{player.gameObject}}",
      "longTerm": false  // 短期：当前玩家引用
    },
    {
      "type": "SaveMemory",
      "key": "playCount",
      "value": "{{config.playCount + 1}}",
      "longTerm": true  // 长期：累计游戏次数
    }
  ]
}
```

### 3. 调试技巧

启用详细日志查看记忆操作：
```
[12:34:56] ✓ 短期记忆可用: playerName = John
[12:34:56] ✓ 长期记忆可用: gameConfig = {"difficulty": "hard"}
[12:34:57] 📝 保存到短期记忆: sessionId = abc123
[12:34:57] 💾 保存到长期记忆: lastPlayTime = 2026-01-21T12:34:57
```

## 总结

### 🎯 新多态设计的核心优势

**命令级记忆控制**：
- ✅ **独立性**：每个命令独立控制自己的记忆保存逻辑
- ✅ **灵活性**：可以根据命令特点实现不同的保存策略
- ✅ **可扩展性**：新命令可以轻松添加自定义记忆逻辑
- ✅ **简洁性**：不再需要全局的 memory.save 配置
- ✅ **类型安全**：编译时检查，避免运行时错误

**传统 memory 部分仍然有用**：
- ✅ **预检查机制** - 确保依赖的数据存在
- ✅ **批量保存** - 简化 JSON 结构
- ✅ **执行顺序保证** - 在所有命令完成后统一保存
- ✅ **长短期记忆支持** - 灵活的持久化选择

### 📋 设计选择指南

| 场景 | 推荐方式 | 理由 |
|------|----------|------|
| 简单命令序列 | 命令级 `saveToMemory` 属性 | 简洁明了，每个命令独立控制 |
| 复杂数据保存 | 重写 `SaveToMemory()` 方法 | 完全自定义保存逻辑 |
| 批量预检查 | 顶层 `memory.load` | 统一检查多个依赖 |
| 批量保存结果 | 顶层 `memory.save` | 避免重复的 SaveMemory 命令 |
| 条件保存 | SaveMemory/LoadMemory 命令 | 根据执行结果动态决定 |
| 中间数据传递 | SaveMemory/LoadMemory 命令 | 精确控制传递时机 |

### 💡 最佳实践

1. **优先使用命令级控制**：对于大多数情况，设置 `saveToMemory: true` 即可
2. **复杂逻辑重写方法**：需要特殊保存逻辑时，重写 `SaveToMemory()` 方法
3. **混合使用传统方式**：预检查和批量保存仍然是有效的补充
4. **保持向后兼容**：现有命令默认行为不变，可以逐步迁移

**调试技巧**：
启用详细日志查看记忆操作：
```
[12:34:56] ✓ 短期记忆可用: playerName = John
[12:34:56] ✓ 长期记忆可用: gameConfig = {"difficulty": "hard"}
[12:34:57] 📝 命令保存: CreateGameObject → player (短期)
[12:34:57] 💾 命令保存: SaveConfig → gameConfig (长期)
```
