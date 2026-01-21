# ESVMCP AI 交互指南

> **⚠️ 重要：AI助手职责说明**  
> **你的主要职责**: 根据用户需求生成ESVMCP JSON命令  
> **关于本文档**: 这是理解系统能力的参考文档，阅读它不会触发实际操作  
> **JSON命令生成**: 直接将JSON命令写在响应中，**不要**让用户复制粘贴  
> **脚本需求**: 当用户需要脚本或其他文件时，正常创建或编辑即可  
> **只有这个Guide**: 仅对本指南文档保持只读，其他工作正常进行

---

> **📌 文档说明**  
> **本文档专门为 AI 助手编写**，提供精准、结构化的系统信息。  
> **目标**: 让 AI 快速理解系统能力，验证就绪状态，立即开始协助用户。  
> **使用方式**: 通过Unity菜单 `Tools/【ESVMCP】/【AI指导】` 获取本文档+实时路径信息。

---

## ✅ 系统验证与就绪确认

**请 AI 确认以下信息后开始工作：**

### 系统状态
- ✅ **版本**: v1.0 Commercial Grade
- ✅ **验证状态**: 已完成交互验证
- ✅ **可用命令**: 80+ 个操作（11大类）
- ✅ **核心特性**: 6种目标定位 + 自动记忆 + 智能类型适配 + 批处理操作
- ✅ **文档完整性**: 已验证
- ✅ **渲染管线**: Universal Render Pipeline (URP) - 所有Material操作使用URP shaders

### 实际路径信息
**重要**: 用户通过菜单获取本文档时，会自动追加当前项目的实际路径。  
请在文档末尾查看 `📂 当前项目实际路径信息` 章节获取准确路径。

### AI 确认清单
阅读完本文档后，AI 应能回答：
- [x] 我知道如何生成JSON命令
- [x] 我知道JSON文件应放入哪个文件夹（Input文件夹位于...\ESVMCP\RunningData\Input，千万别忘了RunningData）
- [x] 我了解系统中所有可用的命令类型
- [x] 我知道如何使用记忆系统提升性能
- [x] 我知道6种目标定位方法
- [x] 我可以开始协助用户使用ESVMCP
- [x] **我明白只有本Guide文档是参考资料，其他工作正常进行**
- [x] **我会直接写JSON命令，不会让用户复制粘贴**
- [x] **我会在开始工作前快速扫描Commands文件夹验证命令**
- [x] **我知道项目使用URP，所有Material操作使用URP shaders**

**✅ 确认完成后，即可开始为用户生成JSON命令。**

---

## 🚀 快速开始（AI 必读）

### 工作前准备（重要！）
在开始生成命令前，**务必先扫描Commands文件夹**确认可用命令：
```
使用工具快速扫描: Assets/Plugins/ES/0_Stand/Stand_Tools/ESVMCP/Commands/
查找所有 [ESVMCPCommand] 特性标记的命令类
确保文档中提到的命令实际存在
```

### 核心工作流（5步）
```
1. 用户描述需求 
   ↓
2. （如有疑问）扫描Commands确认可用命令
   ↓
3. AI直接生成JSON命令文件（使用create_file工具）
   ↓
4. 将文件保存到Input文件夹，系统自动检测并执行
   ↓
5. 返回执行结果
```

### 重要说明
- ✅ **直接创建JSON文件**: 使用create_file工具直接在Input文件夹创建命令文件
- ✅ **正常处理脚本需求**: 用户需要脚本或其他文件时正常创建/编辑
- ✅ **快速验证命令**: 不确定命令是否存在时，快速扫描Commands文件夹
- ❌ **不编辑本Guide**: 仅本AI_INTERACTION_GUIDE.md文档保持只读

### 如何访问文件夹
**重要**: 不要在响应中使用硬编码路径！  
正确做法：
```
请将JSON保存为文件（如 task_001.json），放入Input文件夹。
**重要强调**: Input文件夹位于 ...\ESVMCP\RunningData\Input，千万别忘了RunningData！
可通过Unity菜单访问：Tools/【ESVMCP】/系统/打开文件夹/打开Input文件夹
```

**实际路径**: 见本文档末尾的 `📂 当前项目实际路径信息` 章节（动态生成）。

### 第一个命令
```json
{
  "commandId": "test_001",
  "description": "创建红色立方体",
  "commands": [
    {
      "type": "CommonGameObjectOperation",
      "operation": "Create",
      "name": "RedCube",
      "primitiveType": "Cube",
      "position": {"x": 0, "y": 1, "z": 0},
      "saveToMemory": true,
      "memoryKey": "red_cube"
    },
    {
      "type": "CommonMaterialOperation",
      "operation": "SetColor",
      "target": "memory:red_cube",
      "propertyName": "_Color",
      "color": {"r": 1, "g": 0, "b": 0, "a": 1}
    }
  ]
}
```

---

## 📋 JSON 命令格式

### 基础结构
```json
{
  "commandId": "唯一标识符",
  "description": "命令描述",
  "commands": [
    {
      "type": "命令类型",
      "operation": "操作名称",
      "target": "目标对象",
      "参数名": "参数值",
      "saveToMemory": true,
      "memoryKey": "记忆键名"
    }
  ]
}
```

### 实际可用的命令类型（已验证）

#### 1. GameObject操作 - CommonGameObjectOperation
**枚举操作**: Create, Destroy, SetActive, Rename, SetTag, SetLayer, Duplicate, FindByName, FindByTag, FindInChildren, GetChildren, GetParent

#### 2. Transform操作 - CommonTransformOperation  
**枚举操作**: SetTransform, SetPosition, SetRotation, SetScale, SetParent, LookAt

#### 3. Component操作 - CommonComponentOperation
**枚举操作**: Add, Remove, Get, SetEnabled, GetAll, Copy

#### 4. Material操作 - CommonMaterialOperation
**枚举操作**: SetColor, SetFloat, SetTexture, SetShader, GetColor, GetFloat, EnableKeyword, DisableKeyword, CreateMaterial, ApplyToRenderer

#### 5. Light操作 - CommonLightOperation
**枚举操作**: SetIntensity, SetColor, SetType, SetRange, SetSpotAngle, SetShadowType, Enable, Disable, GetProperties

#### 6. Environment操作 - CommonEnvironmentOperation
**枚举操作**: SetAmbientLight, SetAmbientMode, SetSkybox, SetFog, SetFogColor, SetFogDensity, SetFogMode, SetReflectionIntensity, GetEnvironmentInfo

#### 7. 组件配置操作 - CommonComponentConfigOperation
**枚举操作**: ConfigureCollider, ConfigureRigidbody, ConfigureCamera, ConfigureLight, ConfigureAudioSource, ConfigureParticleSystem

#### 8. Scene操作 - CommonSceneOperation
**枚举操作**: LoadScene, UnloadScene, SaveScene, CreateScene, GetActiveScene, SetActiveScene, GetAllScenes, FindObjects, GetSceneInfo

#### 9. Asset操作 - CommonAssetOperation
**枚举操作**: CreateAsset, LoadAsset, SaveAsset, DeleteAsset, CopyAsset, MoveAsset, RenameAsset, GetAssetPath, CreateFolder, ImportAsset, RefreshAssets, FindAssets

#### 10. Memory操作 - MemoryOperation
**枚举操作**: Save, Load, Remove, Clear, Export, Has

#### 11. 高级操作
- **SetProperty**: 设置任意组件的任意属性（通用）
- **BatchOperation**: 批量操作多个对象
- **BatchOperationByTag**: 按Tag批量操作
- **DuplicateAndModify**: 复制并修改对象
- **ApplyMaterialToMultiple**: 批量应用材质
- **ConditionalExecute**: 条件执行
- **GetEnvironmentData**: 获取环境数据

---

## 🎯 核心命令速查

### GameObject操作
```json
// 创建对象
{"type": "CommonGameObjectOperation", "operation": "Create", "name": "MyObject"}

// 创建几何体
{"type": "CommonGameObjectOperation", "operation": "Create", "name": "MyCube", "primitiveType": "Cube"}

// 销毁对象
{"type": "CommonGameObjectOperation", "operation": "Destroy", "target": "MyObject"}

// 激活/停用
{"type": "CommonGameObjectOperation", "operation": "SetActive", "target": "MyObject", "active": true}

// 复制对象
{"type": "CommonGameObjectOperation", "operation": "Duplicate", "target": "MyObject"}

// 按Tag查找
{"type": "CommonGameObjectOperation", "operation": "FindByTag", "tag": "Enemy"}
```

### Transform操作
```json
// 设置位置
{"type": "CommonTransformOperation", "operation": "SetPosition", "target": "MyObject", "position": {"x": 0, "y": 1, "z": 0}}

// 设置旋转
{"type": "CommonTransformOperation", "operation": "SetRotation", "target": "MyObject", "rotation": {"x": 0, "y": 90, "z": 0}}

// 设置缩放
{"type": "CommonTransformOperation", "operation": "SetScale", "target": "MyObject", "scale": {"x": 2, "y": 2, "z": 2}}

// 设置父对象
{"type": "CommonTransformOperation", "operation": "SetParent", "target": "Child", "parent": "Parent"}

// 看向目标
{"type": "CommonTransformOperation", "operation": "LookAt", "source": "Camera", "target": "Player"}
```

### Component操作
```json
// 添加组件
{"type": "CommonComponentOperation", "operation": "Add", "target": "MyObject", "component": "Rigidbody"}

// 移除组件
{"type": "CommonComponentOperation", "operation": "Remove", "target": "MyObject", "component": "BoxCollider"}

// 启用/禁用组件
{"type": "CommonComponentOperation", "operation": "SetEnabled", "target": "MyObject", "component": "MeshRenderer", "enabled": true}
```

### Material操作
```json
// 创建材质（URP环境）
{"type": "CommonMaterialOperation", "operation": "CreateMaterial", "shaderName": "Universal Render Pipeline/Lit", "assetName": "NewMaterial"}

// 设置颜色
{"type": "CommonMaterialOperation", "operation": "SetColor", "target": "MyObject", "propertyName": "_BaseColor", "color": {"r": 1, "g": 0, "b": 0, "a": 1}}

// 设置纹理
{"type": "CommonMaterialOperation", "operation": "SetTexture", "target": "MyObject", "propertyName": "_MainTex", "texturePath": "Assets/Textures/wood.png"}
```

### 光照操作
```json
// 设置光源强度
{"type": "CommonLightOperation", "operation": "SetIntensity", "target": "MainLight", "intensity": 1.5}

// 设置光源颜色
{"type": "CommonLightOperation", "operation": "SetColor", "target": "MainLight", "color": {"r": 1, "g": 0.95, "b": 0.8, "a": 1}}

// 设置光源类型
{"type": "CommonLightOperation", "operation": "SetType", "target": "MainLight", "lightType": "Directional"}

// 启用/禁用光源
{"type": "CommonLightOperation", "operation": "Enable", "target": "MainLight"}
```

### 环境操作
```json
// 设置环境光
{"type": "CommonEnvironmentOperation", "operation": "SetAmbientLight", "color": {"r": 0.2, "g": 0.2, "b": 0.3, "a": 1}}

// 设置雾效
{"type": "CommonEnvironmentOperation", "operation": "SetFog", "fogEnabled": true, "color": {"r": 0.5, "g": 0.5, "b": 0.5, "a": 1}, "fogDensity": 0.01}

// 设置天空盒
{"type": "CommonEnvironmentOperation", "operation": "SetSkybox", "skyboxMaterial": "Assets/Materials/Skybox.mat"}
```

### 组件配置操作
```json
// 配置碰撞体
{"type": "CommonComponentConfigOperation", "operation": "ConfigureCollider", "target": "MyObject", "isTrigger": false}

// 配置刚体
{"type": "CommonComponentConfigOperation", "operation": "ConfigureRigidbody", "target": "MyObject", "mass": 1.0, "drag": 0.1, "useGravity": true}

// 配置相机
{"type": "CommonComponentConfigOperation", "operation": "ConfigureCamera", "target": "MainCamera", "fieldOfView": 60, "nearClipPlane": 0.3, "farClipPlane": 1000}
```

### 批处理操作
```json
// 按标签批量操作
{"type": "BatchOperationByTag", "tag": "Enemy", "operation": "SetActive", "active": false}

// 复制并修改
{"type": "DuplicateAndModify", "source": "Template", "name": "Enemy_1", "position": {"x": 2, "y": 0, "z": 0}}

// 批量应用材质
{"type": "ApplyMaterialToMultiple", "targets": ["Obj1", "Obj2", "Obj3"], "materialName": "Assets/Materials/Wood.mat"}
```

### 记忆操作
```json
// 保存记忆
{"type": "MemoryOperation", "operation": "Save", "key": "player_health", "value": 100}

// 加载记忆
{"type": "MemoryOperation", "operation": "Load", "key": "player_health"}

// 删除记忆
{"type": "MemoryOperation", "operation": "Remove", "key": "old_data"}

// 检查记忆
{"type": "MemoryOperation", "operation": "Has", "key": "player_id"}
```

---

## 🎨 目标定位方法（6种）

### 1. 直接名称
```json
{"target": "Player"}
```

### 2. 记忆键 ⭐推荐
```json
{"target": "memory:player_id"}
```

### 3. 场景路径
```json
{"target": "path:Environment/House/Door"}
```

### 4. Tag查找
```json
{"target": "tag:Enemy"}
```

### 5. 实例ID
```json
{"target": "id:12345"}
```

### 6. 特征匹配
```json
{"target": "feature:hasComponent(Rigidbody)"}
```

---

## 💡 AI 响应模板（标准格式）

**AI 必须遵循此格式回复用户：**

```markdown
[简要说明操作目的，1-2句话]

**JSON命令已生成**（文件将自动保存到Input文件夹）

```json
{JSON代码}
```

执行后将会：
- [预期结果1]
- [预期结果2]
```

### 示例对话（参考）

**用户**: "创建一个会旋转的立方体"

**AI**:
```
我来帮你创建一个会旋转的立方体。

**JSON命令已生成**

```json
{
  "commandId": "rotating_cube",
  "description": "创建旋转立方体",
  "commands": [
    {
      "type": "CommonGameObjectOperation",
      "operation": "Create",
      "name": "RotatingCube",
      "primitiveType": "Cube",
      "position": {"x": 0, "y": 1, "z": 0},
      "saveToMemory": true,
      "memoryKey": "cube"
    },
    {
      "type": "CommonComponentOperation",
      "operation": "Add",
      "target": "memory:cube",
      "component": "Rigidbody"
    },
    {
      "type": "SetProperty",
      "target": "memory:cube",
      "component": "Rigidbody",
      "angularVelocity": {"x": 0, "y": 2, "z": 0}
    }
  ]
}
```

执行后将会：
- 在场景中创建一个立方体
- 添加物理组件
- 设置绕Y轴旋转
```

### 关于文件操作的重要说明

**JSON命令文件**: 使用 `create_file` 工具直接创建到Input文件夹（路径：...\ESVMCP\RunningData\Input，千万别忘了RunningData）  
**脚本需求**: 用户需要Unity脚本或其他代码文件时，正常创建或编辑  
**本Guide文档**: 仅此AI_INTERACTION_GUIDE.md保持只读，不要编辑  
**其他文档**: 项目中的其他文档可以正常编辑

---

## 📊 常用场景模板

### 场景1: 创建完整角色
```json
{
  "commandId": "create_player",
  "commands": [
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Player", "primitiveType": "Capsule", "saveToMemory": true, "memoryKey": "player"},
    {"type": "CommonTransformOperation", "operation": "SetPosition", "target": "memory:player", "position": {"x": 0, "y": 1, "z": 0}},
    {"type": "CommonComponentOperation", "operation": "Add", "target": "memory:player", "componentType": "Rigidbody"},
    {"type": "CommonComponentOperation", "operation": "Add", "target": "memory:player", "componentType": "CapsuleCollider"},
    {"type": "CommonMaterialOperation", "operation": "SetColor", "target": "memory:player", "propertyName": "_Color", "color": {"r": 0, "g": 0.5, "b": 1, "a": 1}}
  ]
}
```

### 场景2: 批量创建对象
```json
{
  "commandId": "create_obstacles",
  "commands": [
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Obstacle1", "primitiveType": "Cube", "position": {"x": 2, "y": 0.5, "z": 0}},
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Obstacle2", "primitiveType": "Cube", "position": {"x": 4, "y": 0.5, "z": 0}},
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Obstacle3", "primitiveType": "Cube", "position": {"x": 6, "y": 0.5, "z": 0}}
  ]
}
```

### 场景3: 室内房间场景搭建
```json
{
  "commandId": "indoor_room_setup",
  "description": "创建完整的室内房间场景",
  "commands": [
    // 基础结构
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Floor", "primitiveType": "Cube", "position": {"x": 0, "y": -0.5, "z": 0}, "saveToMemory": true, "memoryKey": "floor"},
    {"type": "CommonTransformOperation", "operation": "SetScale", "target": "memory:floor", "scale": {"x": 20, "y": 1, "z": 20}},
    
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Wall_North", "primitiveType": "Cube", "position": {"x": 0, "y": 5, "z": 10}, "saveToMemory": true, "memoryKey": "wall_north"},
    {"type": "CommonTransformOperation", "operation": "SetScale", "target": "memory:wall_north", "scale": {"x": 20, "y": 10, "z": 1}},
    
    // 家具对象
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "Table", "primitiveType": "Cube", "position": {"x": 0, "y": 1, "z": 0}, "saveToMemory": true, "memoryKey": "table"},
    {"type": "CommonTransformOperation", "operation": "SetScale", "target": "memory:table", "scale": {"x": 3, "y": 0.2, "z": 2}},
    
    // 光照系统
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "MainLight", "saveToMemory": true, "memoryKey": "main_light"},
    {"type": "CommonComponentOperation", "operation": "Add", "target": "memory:main_light", "component": "Light"},
    {"type": "CommonLightOperation", "operation": "SetType", "target": "memory:main_light", "lightType": "Directional"},
    {"type": "CommonLightOperation", "operation": "SetIntensity", "target": "memory:main_light", "intensity": 1.2},
    {"type": "CommonLightOperation", "operation": "SetColor", "target": "memory:main_light", "color": {"r": 1, "g": 0.95, "b": 0.9, "a": 1}},
    {"type": "CommonTransformOperation", "operation": "SetRotation", "target": "memory:main_light", "rotation": {"x": 50, "y": -30, "z": 0}},
    
    // 环境设置
    {"type": "CommonEnvironmentOperation", "operation": "SetAmbientLight", "color": {"r": 0.3, "g": 0.3, "b": 0.4, "a": 1}},
    {"type": "CommonEnvironmentOperation", "operation": "SetFog", "fogEnabled": true, "color": {"r": 0.8, "g": 0.8, "b": 0.9, "a": 1}, "fogDensity": 0.005},
    
    // 材质系统（URP环境）
    {"type": "CommonMaterialOperation", "operation": "CreateMaterial", "shaderName": "Universal Render Pipeline/Lit", "assetName": "WoodMaterial", "saveToMemory": true, "memoryKey": "wood_mat"},
    {"type": "CommonMaterialOperation", "operation": "SetColor", "target": "memory:wood_mat", "propertyName": "_BaseColor", "color": {"r": 0.6, "g": 0.4, "b": 0.2, "a": 1}},
    {"type": "ApplyMaterialToMultiple", "targets": ["memory:table"], "materialName": "Assets/Materials/WoodMaterial.mat"},
    
    // 层级组织
    {"type": "CommonGameObjectOperation", "operation": "Create", "name": "RoomStructure", "saveToMemory": true, "memoryKey": "room_group"},
    {"type": "CommonTransformOperation", "operation": "SetParent", "target": "memory:floor", "parent": "memory:room_group"},
    {"type": "CommonTransformOperation", "operation": "SetParent", "target": "memory:wall_north", "parent": "memory:room_group"}
  ]
}
```

---

## 🎯 记忆系统使用

### 保存到记忆
```json
{
  "type": "CommonGameObjectOperation",
  "operation": "Create",
  "name": "ImportantObject",
  "saveToMemory": true,
  "memoryKey": "important_obj"
}
```

### 从记忆读取
```json
{
  "type": "CommonTransformOperation",
  "operation": "SetPosition",
  "target": "memory:important_obj",
  "position": {"x": 5, "y": 0, "z": 0}
}
```

### 记忆操作命令
```json
// 手动保存
{"type": "MemoryOperation", "operation": "Save", "key": "player_health", "value": 100}

// 读取记忆
{"type": "MemoryOperation", "operation": "Load", "key": "player_health"}

// 删除记忆
{"type": "MemoryOperation", "operation": "Remove", "key": "old_data"}

// 检查记忆是否存在
{"type": "MemoryOperation", "operation": "Has", "key": "player_id"}

// 导出所有记忆
{"type": "MemoryOperation", "operation": "Export"}

// 清除所有记忆（危险操作）
{"type": "MemoryOperation", "operation": "Clear"}
```

---

## 📐 数据类型规范

### Vector3（位置/旋转/缩放）
```json
{"x": 0.0, "y": 1.0, "z": 0.0}
```

### Color（颜色）
```json
{"r": 1.0, "g": 0.0, "b": 0.0, "a": 1.0}
// 值域: 0.0 - 1.0
```

### 基本类型
```json
{"name": "字符串"}
{"count": 42}
{"enabled": true}
{"probability": 0.75}
```

### 数组
```json
{"tags": ["Enemy", "AI", "Ground"]}
{"positions": [{"x": 0, "y": 0, "z": 0}, {"x": 1, "y": 0, "z": 0}]}
```

---

## ⚡ 性能优化技巧

### 1. 使用记忆键定位（50-100倍提升）
```json
// ❌ 每次都查找名称
{"target": "Player"}

// ✅ 使用记忆键
{"target": "memory:player_id"}
```

### 2. 批量操作
```json
{
  "type": "BatchOperation",
  "operations": [
    {"type": "CommonTransformOperation", "operation": "SetPosition", "target": "Obj1", "position": {"x": 1, "y": 0, "z": 0}},
    {"type": "CommonTransformOperation", "operation": "SetPosition", "target": "Obj2", "position": {"x": 2, "y": 0, "z": 0}}
  ]
}
```

### 3. 合理使用Tag查找
```json
// 找到后保存到记忆
{
  "type": "CommonGameObjectOperation",
  "operation": "FindByTag",
  "tag": "Player",
  "saveToMemory": true,
  "memoryKey": "player"
}
```

---

## 🐛 调试与错误处理

### 查看日志
- **访问方式**: Unity菜单 `Tools/【ESVMCP】/系统/打开文件夹/打开Logs文件夹`
- **文件命名**: `execution_log_YYYYMMDD_HHmmss.json`

### 查看归档
- **访问方式**: Unity菜单 `Tools/【ESVMCP】/系统/打开文件夹/打开Archive文件夹`
- **内容**: 已执行的命令文件（带时间戳）

### 常见错误

#### 对象未找到
```
错误: Target 'PlayerX' not found
解决: 检查对象名称拼写，或使用 FindByName 先查找
```

#### 组件不存在
```
错误: Component 'Rigidbody' not found on 'Player'
解决: 先使用 CommonComponentOperation Add 添加组件
```

#### 参数类型错误
```
错误: Invalid position format
解决: 使用正确格式 {"x": 0, "y": 0, "z": 0}
```

---

## 📚 命令完整清单

### 核心命令类型（11大类）

#### 1. GameObject操作 (CommonGameObjectOperation)
**12个操作**: Create, Destroy, SetActive, Rename, SetTag, SetLayer, Duplicate, FindByName, FindByTag, FindInChildren, GetChildren, GetParent

#### 2. Transform操作 (CommonTransformOperation)
**6个操作**: SetTransform, SetPosition, SetRotation, SetScale, SetParent, LookAt

#### 3. Component操作 (CommonComponentOperation)
**6个操作**: Add, Remove, Get, SetEnabled, GetAll, Copy

#### 4. Material操作 (CommonMaterialOperation)
**10个操作**: SetColor, SetFloat, SetTexture, SetShader, GetColor, GetFloat, EnableKeyword, DisableKeyword, CreateMaterial, ApplyToRenderer

#### 5. Light操作 (CommonLightOperation)
**9个操作**: SetIntensity, SetColor, SetType, SetRange, SetSpotAngle, SetShadowType, Enable, Disable, GetProperties

#### 6. Environment操作 (CommonEnvironmentOperation)
**9个操作**: SetAmbientLight, SetAmbientMode, SetSkybox, SetFog, SetFogColor, SetFogDensity, SetFogMode, SetReflectionIntensity, GetEnvironmentInfo

#### 7. 组件配置操作 (CommonComponentConfigOperation)
**6个操作**: ConfigureCollider, ConfigureRigidbody, ConfigureCamera, ConfigureLight, ConfigureAudioSource, ConfigureParticleSystem

#### 8. Scene操作 (CommonSceneOperation)
**9个操作**: LoadScene, UnloadScene, SaveScene, CreateScene, GetActiveScene, SetActiveScene, GetAllScenes, FindObjects, GetSceneInfo

#### 9. Asset操作 (CommonAssetOperation)
**12个操作**: CreateAsset, LoadAsset, SaveAsset, DeleteAsset, CopyAsset, MoveAsset, RenameAsset, GetAssetPath, CreateFolder, ImportAsset, RefreshAssets, FindAssets

#### 10. Memory操作 (MemoryOperation)
**6个操作**: Save, Load, Remove, Clear, Export, Has

#### 11. 高级/批处理操作
- **SetProperty**: 通用属性设置
- **BatchOperation**: 批量操作
- **BatchOperationByTag**: 按Tag批量操作
- **DuplicateAndModify**: 复制并修改
- **ApplyMaterialToMultiple**: 批量应用材质
- **ConditionalExecute**: 条件执行
- **GetEnvironmentData**: 获取环境数据

### 快速扫描命令方法

**AI在使用前应该快速扫描Commands文件夹**：
```
路径: Assets/Plugins/ES/0_Stand/Stand_Tools/ESVMCP/Commands/
方法: 搜索 [ESVMCPCommand] 特性标记
目的: 确保使用的命令实际存在
```

---

## ✅ 最佳实践

### 1. 命名规范
- ✅ 使用有意义的 commandId
- ✅ 添加清晰的 description
- ✅ memoryKey 使用下划线命名法

### 2. 记忆管理
- ✅ 重要对象保存到记忆
- ✅ 及时清理不需要的记忆
- ✅ 使用描述性的键名

### 3. 错误预防
- ✅ 创建前检查对象是否存在
- ✅ 修改前确认目标有效
- ✅ 使用合适的目标定位方法

### 4. 性能优化
- ✅ 优先使用记忆键定位
- ✅ 批量操作合并执行
- ✅ 避免重复查找

---

## 🎓 学习路径

### 第1天：基础掌握
1. 理解JSON格式
2. 学会创建GameObject
3. 掌握Transform操作
4. 了解记忆系统

### 第2天：进阶应用
1. Component管理
2. Material设置
3. 目标定位方法
4. 批量操作

### 第3天：高级技巧
1. Scene管理
2. Asset操作
3. 条件执行
4. 性能优化

---

## 🚦 系统状态检查

### 验证系统是否就绪
1. 使用Unity菜单：`Tools/【ESVMCP】/系统/【查看状态】` 检查系统状态
2. 使用Unity菜单：`Tools/【ESVMCP】/系统/【一键安装】` 确保系统安装完整
3. 检查场景：是否有 `ESVMCP_Memory` GameObject
4. 测试命令：放入第一个命令JSON到Input文件夹，观察是否执行

### 环境检查清单
- [x] Input文件夹存在（位于...\ESVMCP\RunningData\Input）
- [x] Archive文件夹存在
- [x] Logs文件夹存在
- [x] ESVMCPConfig配置文件存在
- [x] 场景中有ESVMCPMemory组件
- [x] 项目使用Universal Render Pipeline (URP)
- [x] 系统已完成交互验证

---

## 📞 快速参考

### 访问系统资源
```
配置文件: Unity菜单 → Tools/【ESVMCP】/系统/资产/选择配置资产
Input文件夹: Unity菜单 → Tools/【ESVMCP】/系统/打开文件夹/打开Input文件夹
Archive文件夹: Unity菜单 → Tools/【ESVMCP】/系统/打开文件夹/打开Archive文件夹
Logs文件夹: Unity菜单 → Tools/【ESVMCP】/系统/打开文件夹/打开Logs文件夹
```

### 命令模板
```json
{
  "commandId": "唯一ID",
  "description": "描述",
  "commands": [
    {
      "type": "Common[类型]Operation",
      "operation": "操作名",
      "target": "目标",
      "saveToMemory": true,
      "memoryKey": "键名"
    }
  ]
}
```

### 颜色参考
```
红色: {"r": 1, "g": 0, "b": 0, "a": 1}
绿色: {"r": 0, "g": 1, "b": 0, "a": 1}
蓝色: {"r": 0, "g": 0, "b": 1, "a": 1}
白色: {"r": 1, "g": 1, "b": 1, "a": 1}
黑色: {"r": 0, "g": 0, "b": 0, "a": 1}
```

---

## ✅ AI 最终确认

**在开始协助用户前，请确认：**

1. ✅ 我已理解JSON命令格式（11大类，50+核心操作）
2. ✅ 我知道如何使用记忆系统（`saveToMemory: true` + `memoryKey`）
3. ✅ 我掌握6种目标定位方法（推荐使用 `memory:` 前缀）
4. ✅ 我了解响应模板格式（直接创建JSON文件）
5. ✅ 我知道实际路径在文档末尾动态生成部分
6. ✅ 我可以生成准确、可执行的JSON命令
7. ✅ **我会在开始前快速扫描Commands文件夹验证命令**
8. ✅ **我会直接创建JSON文件，不让用户复制粘贴**
9. ✅ **我知道脚本和其他文件需求应正常处理**
10. ✅ **我明白只有本Guide文档是只读的**

**状态**: ✅ 系统就绪，可以开始工作！

### 重要提醒

✅ **理解项目结构**: 完全理解ESVMCP的工作流程和命令执行机制  
✅ **不执行实际操作**: 仅通过阅读本文档理解系统，不会触发任何实际操作  
✅ **正常生成JSON**: 当用户需要时，直接创建JSON命令文件到Input文件夹（路径：...\ESVMCP\RunningData\Input，千万别忘了RunningData）  
✅ **正常处理脚本**: 用户需要Unity脚本或其他代码时，正常创建/编辑  
✅ **快速验证命令**: 不确定时快速扫描Commands文件夹确认命令存在  
✅ **URP环境**: 项目使用Universal Render Pipeline，所有Material操作优先使用URP shaders（如"Universal Render Pipeline/Lit"）

---

**最后更新**: 2026年1月20日  
**文档类型**: AI专用交互指南  
**目标读者**: AI助手/自动化系统  
**获取方式**: Unity菜单 → Tools/【ESVMCP】/【AI指导】

---

> **📌 提醒**  
> 接下来会告诉你 `📂 当前项目实际路径信息`，包含所有实际文件夹路径。  
> 该部分由系统动态生成，每次获取时更新。  
> 请优先使用该部分的路径信息！
