# ES文档窗口 (ESDocWindow)

## 概述

ES文档窗口是一个专门用于创建和管理Unity项目文档的编辑器工具。它支持丰富的内容元素，包括文本、代码块、表格、图片、链接等，并可以导出为Markdown和HTML格式。

## 功能特性

### ✨ 核心功能

- **📝 丰富的内容元素** - 支持10+种文档内容元素类型
- **📑 文档模板** - 预置API、教程、设计、技术规范等模板
- **📤 多格式导出** - 支持导出为Markdown、HTML和纯文本
- **🎨 可视化编辑** - 基于Odin Inspector的直观编辑界面
- **📂 分类管理** - 按分类组织文档库
- **🔍 搜索功能** - 快速查找文档

### 📋 支持的内容元素

#### 文本类
- **ESDocText** - 普通文本段落
- **ESDocQuote** - 引用块，支持来源标注
- **ESDocAlert** - 警告框 (Info/Success/Warning/Error)

#### 代码类
- **ESDocCodeBlock** - 代码块，支持10种编程语言语法高亮
  - C#, JavaScript, Python, Java, C++
  - XML, JSON, SQL, HTML, CSS

#### 列表类
- **ESDocUnorderedList** - 无序列表 (圆点标记)
- **ESDocOrderedList** - 有序列表 (数字标记)

#### 表格类
- **ESDocTable** - 表格，支持自定义列标题和行数据

#### 媒体类
- **ESDocImage** - 图片，支持Unity资产和外部路径
- **ESDocLink** - 超链接，支持URL和描述

#### 格式类
- **ESDocDivider** - 水平分隔线

## 使用指南

### 打开窗口

通过菜单打开: `Tools -> ES工具 -> ES文档窗口` 或按 `F11`

### 创建文档

1. 点击 **"创建新文档"** 或使用预置模板
2. 填写文档基本信息:
   - 文档标题
   - 分类
   - 作者
3. 选择模板类型:
   - 空白文档
   - API文档模板
   - 教程模板
   - 设计文档模板
   - 技术规范模板
4. 指定保存路径
5. 点击 **"创建文档"** 按钮

### 编辑文档

1. 在文档库中选择要编辑的文档
2. 使用Odin Inspector界面编辑内容:
   - 添加/删除章节
   - 添加/编辑内容元素
   - 调整内容顺序 (拖拽)
3. Unity会自动保存更改

### 导出文档

在文档页面底部点击:
- **导出为Markdown** - 生成.md文件
- **导出为HTML** - 生成.html文件
- **复制为纯文本** - 复制到剪贴板

## 预置模板详解

### API文档模板

包含章节:
- 概述
- 快速开始 (含代码示例)
- API参考 (含方法表格)
- 注意事项 (含警告框)

### 教程模板

包含章节:
- 学习目标 (无序列表)
- 准备工作
- 操作步骤 (有序列表)
- 完整示例 (代码块)
- 总结

### 设计文档模板

包含章节:
- 设计目标
- 系统架构 (含架构图)
- 技术栈 (表格)
- 接口设计 (代码块)
- 风险评估 (警告框)

### 技术规范模板

包含章节:
- 规范说明
- 命名规范 (表格)
- 代码规范 (代码块)
- 注释规范 (代码块)
- 参考资料 (链接)

## 最佳实践

### 📂 文档组织

建议按以下分类组织文档:
- **API** - API接口文档
- **教程** - 使用教程和指南
- **设计文档** - 系统设计和架构文档
- **技术规范** - 编码规范和标准
- **用户手册** - 面向用户的手册
- **更新日志** - 版本更新记录
- **最佳实践** - 经验总结和技巧

### ✍️ 内容编写

1. **使用清晰的标题** - 章节标题应简洁明了
2. **合理使用元素** - 选择最适合的内容元素类型
3. **代码示例完整** - 提供可运行的完整代码
4. **表格简洁** - 避免表格过于复杂
5. **图片优化** - 使用适当大小的图片
6. **链接有效** - 定期检查外部链接有效性

### 🎯 工作流程

**推荐工作流**:
1. 使用模板快速开始
2. 根据需要添加/删除章节
3. 填充内容元素
4. 预览效果 (导出HTML查看)
5. 迭代优化
6. 导出为最终格式

## 技术细节

### 文件结构

```
ESDocWindow/
├── ESDocWindow.cs              # 主窗口类
├── ESDocumentComponents.cs     # 文档组件定义
└── README.md                   # 使用说明
```

### 核心类

- **ESDocWindow** - 主窗口，继承自 ESMenuTreeWindowAB
- **ESDocumentPageBase** - 文档ScriptableObject基类
- **ESDocSection** - 文档章节
- **ESDocContentBase** - 所有内容元素的抽象基类

### 扩展开发

如需添加新的内容元素类型:

1. 创建新类继承 `ESDocContentBase`
2. 实现三个抽象方法:
   ```csharp
   public abstract string ToMarkdown();
   public abstract string ToHTML();
   public abstract string ToPlainText();
   ```
3. 添加必要的字段和Odin属性
4. 在模板初始化方法中使用

示例:
```csharp
[Serializable]
public class ESDocCustomElement : ESDocContentBase
{
    [LabelText("内容")]
    public string content = "";
    
    public override string ToMarkdown() => $"**{content}**";
    public override string ToHTML() => $"<strong>{content}</strong>";
    public override string ToPlainText() => content;
}
```

## 常见问题

### Q: 文档保存在哪里?
A: 默认保存在 `Assets/ES/Documentation/` 目录下，可在创建时自定义路径。

### Q: 如何批量管理文档?
A: 文档作为ScriptableObject资产存储，可以使用Unity的资产管理工具批量操作。

### Q: 导出的Markdown/HTML不显示图片?
A: 确保图片路径正确，建议使用相对路径或将图片放在同一目录。

### Q: 如何分享文档?
A: 可以导出为Markdown或HTML格式分享，或直接分享.asset文件(需要完整项目环境)。

### Q: 支持团队协作吗?
A: 支持，文档作为Unity资产可通过版本控制系统(如Git)协作管理。

## 更新日志

### v1.0.0 (2024)
- ✅ 初始版本发布
- ✅ 支持10+种内容元素
- ✅ 4种预置模板
- ✅ Markdown/HTML/纯文本导出
- ✅ 可视化编辑界面
- ✅ 分类管理和搜索

## 许可证

本工具是ES Framework的一部分，使用与ES Framework相同的许可证。

---

**技术支持**: 如有问题请在项目中提Issue
**反馈建议**: 欢迎提供改进建议
