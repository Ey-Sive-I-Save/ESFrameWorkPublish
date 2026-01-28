# 资产自动收集功能 - 使用指南

## 📋 功能概述

资产自动收集系统为 ESLibrary 提供了智能的资源分类和推荐功能，通过优先级配置自动将资产分配到最合适的 Library 和 Book 中。

## 🎯 核心特性

### 1. **优先级配置系统**
- **6级优先级**：禁用 → 最低 → 较低 → 中等 → 较高 → 最高
- **分类别配置**：每个 Library 可针对 12 种资产类型独立设置优先级
- **总体优先级**：一键设置所有类型的默认优先级

### 2. **12 种资产类型支持**
- Prefab（预制体）
- Scene（场景）
- Material（材质）
- Texture（纹理）
- Model（模型）
- Audio（音频）
- Animation（动画）
- Script（脚本）
- Shader（着色器）
- Font（字体）
- Video（视频）
- Other（其他）

### 3. **独占模式**
- 一键将某个 Library 设为某类型资产的"独占优先库"
- 自动将该类型的其他所有 Library 降为最低优先级
- 适用于需要强制集中管理某类资产的场景

### 4. **DefaultBook 智能匹配**
- 每个 DefaultBook 可标记推荐资产类别
- 通过 `GetDefaultBookByCategory()` 快速查找对应 Book
- ResLibrary 已预配置 12 个专用 DefaultBook

### 5. **资产去重保护**
- Book 内自动检测重复资产
- 拖拽添加时自动跳过已存在的资源
- 防止同一资产在 Book 中重复添加

## 🎨 UI 操作指南

### 在 Library 面板设置收集优先级

1. **打开 Library 编辑窗口**
   - 选择任意 Library 资产
   - 在 Inspector 面板中找到"收集配置"按钮

2. **配置优先级**
   ```
   点击"收集配置"按钮
   ↓
   选择资产类别（总体优先级/预制体/场景/材质/...）
   ↓
   选择优先级等级
     - 🚫 禁用
     - 1️⃣ 最低
     - 2️⃣ 较低
     - 3️⃣ 中等
     - 4️⃣ 较高
     - 5️⃣ 最高
   ```

3. **使用独占模式**
   ```
   点击"收集配置"按钮
   ↓
   选择资产类别
   ↓
   点击"⭐ 独占模式（设为最高，其他最低）"
   ↓
   确认对话框
   ```

### 配置示例

#### 场景 1：UI 专用库
```
Library: "UI资源库"
- 纹理：最高（独占模式）
- 预制体：较高
- 字体：最高（独占模式）
- 其他：最低
```

#### 场景 2：音效库
```
Library: "音效资源库"
- 音频：最高（独占模式）
- 其他：禁用
```

#### 场景 3：通用库
```
Library: "通用资源库"
- 总体优先级：中等
```

## 🔧 代码集成

### 获取推荐 Library

```csharp
// 方法1：根据资产对象自动判断类型
var config = ESGlobalResToolsSupportConfig.Instance;
var recommendedLibraries = config.GetRecommendedLibraryForAsset(myTexture);

foreach (var (libraryName, priority) in recommendedLibraries)
{
    Debug.Log($"推荐库: {libraryName}, 优先级: {priority}");
}

// 方法2：根据已知的资产类别查询
var libraries = config.GetRecommendedLibraries(ESAssetCategory.Audio);
```

### 获取推荐 DefaultBook

```csharp
// 在 Library 中查找适合音频的 DefaultBook
var audioBook = myLibrary.GetDefaultBookByCategory(ESAssetCategory.Audio);
if (audioBook != null)
{
    // 将音频资产添加到该 Book
}
```

### 手动配置优先级

```csharp
var config = ESGlobalResToolsSupportConfig.Instance;

// 设置单个优先级
var libConfig = config.GetOrCreateConfig("UI资源库");
libConfig.SetPriority(ESAssetCategory.Texture, ESAssetCollectionPriority.Highest);

// 设置独占模式
config.SetExclusivePriority("音效资源库", ESAssetCategory.Audio, ESAssetCollectionPriority.Highest);

EditorUtility.SetDirty(config);
AssetDatabase.SaveAssets();
```

### 资产去重检查

```csharp
// Book 基类已自动集成去重功能
// 拖拽添加资源时会自动调用 IsDuplicateAsset 检查
myBook.EditorOnly_DragAtArea(new[] { myAsset });
// 如果 myAsset 已存在，会输出警告并跳过
```

## 📁 相关文件

| 文件 | 路径 | 说明 |
|------|------|------|
| **ESGlobalResToolsSupportConfig.cs** | `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData/` | 配置数据类，存储优先级设置 |
| **6-SoLibrary.cs** | `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/LibraryBookPage/` | Library/Book/Page 基类，包含去重和查询方法 |
| **ESLibraryTemplate.cs** | `Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/` | Library 编辑器模板，包含 UI 配置按钮 |
| **ResLibrary.cs** | `Assets/Plugins/ES/0_Stand/_Res/Master/Shared/SoSupport/` | ResLibrary 实现，包含 12 个预配置 DefaultBook |

## 🔍 配置文件位置

优先级配置保存在：
```
Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData/ESGlobalResToolsSupportConfig.asset
```

## 🎓 最佳实践

### ✅ 推荐做法

1. **按类型划分 Library**
   - 为不同资产类型创建专用 Library（UI库、音效库、模型库等）
   - 对专用 Library 使用独占模式确保资源不会误入其他库

2. **设置合理的优先级梯度**
   - 主要库：最高/较高
   - 备用库：中等
   - 临时库：较低/最低
   - 不适用的库：禁用

3. **利用 DefaultBook 分类**
   - 为每个 DefaultBook 设置正确的 PreferredAssetCategory
   - 使用 GetDefaultBookByCategory 快速定位目标 Book

4. **定期清理配置**
   - 删除不再使用的 Library 后，点击"清理无效配置"
   - 使用"重置所有配置"恢复到默认状态

### ❌ 避免的问题

1. **不要为所有 Library 设置相同优先级**
   - 会导致推荐系统无法区分优先级

2. **不要滥用独占模式**
   - 只在确实需要强制集中管理时使用

3. **不要忘记保存配置**
   - 修改优先级后会自动保存，但自定义代码需手动调用 SaveAssets

## 🐛 故障排除

### 问题：配置菜单不显示

**解决方案**：
1. 检查 Library 是否为 null
2. 检查 ESGlobalResToolsSupportConfig.Instance 是否正常加载
3. 查看 Console 是否有错误日志

### 问题：去重功能不生效

**解决方案**：
1. 确认 Page 类型包含 `OB` 字段（UnityEngine.Object）
2. 检查是否通过 `EditorOnly_DragAtArea` 添加资源
3. 查看 Console 的警告日志确认去重检测是否触发

### 问题：DefaultBook 匹配失败

**解决方案**：
1. 确认 DefaultBook 的 PreferredAssetCategory 已正确设置
2. 检查 Library 的 DefaultBooks 属性是否已初始化
3. 使用 Debug.Log 输出 GetDefaultBookByCategory 的返回值

## 📝 更新日志

### Version 1.0（2024-01-XX）
- ✅ 实现优先级配置系统（6级）
- ✅ 支持 12 种资产类型分类
- ✅ 添加独占模式功能
- ✅ 实现 Book 内资产去重
- ✅ 为 DefaultBook 添加类别标记
- ✅ 集成到 Library 编辑器 UI

## 🔗 相关文档

- [命令列表](COMMAND_LIST.md)
- [内存使用指南](MEMORY_USAGE.md)
- [OnDisable测试指南](ONDISABLE_TEST_GUIDE.md)

---

**提示**：如有任何问题或建议，请联系开发团队。
