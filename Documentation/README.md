# ESVMCP 文档中心

欢迎使用 ESVMCP（Unity JSON 命令执行系统）！

---

## 📚 文档结构

### 🤖 AI 专用文档（重点）
**位置**: `../Assets/ES/ESVMCP/AI_INTERACTION_GUIDE.md` ⭐

这是 **AI 助手的主要参考文档**，包含：
- ✅ 系统就绪状态确认
- 🚀 快速开始指南（5分钟上手）
- 📋 JSON 命令格式
- 🎯 核心命令速查（66+操作）
- 💡 AI 响应模板
- 📊 常用场景模板
- ⚡ 性能优化技巧
- 🐛 调试与错误处理

**适用对象**: AI 助手、自动化系统  
**重要性**: ⭐⭐⭐⭐⭐（最高优先级）

---

## 📖 用户文档

### 1. 命令参考文档 ⭐
**文件**: [COMMAND_LIST.md](COMMAND_LIST.md)

**内容**:
- 66+ 个可用命令总览
- 按类型分类的命令清单（8大类）
- 智能特性说明（6种目标定位）
- 性能对比表
- 快速开始示例

**适用场景**: 查找可用命令、了解命令参数

---

### 2. 系统架构文档

#### 2.1 架构改进报告
**文件**: [用户文档/ARCHITECTURE_IMPROVEMENT_REPORT.md](用户文档/ARCHITECTURE_IMPROVEMENT_REPORT.md)

**内容**:
- 命名架构调整（Common前缀规范）
- 资源路径管理系统
- 环境数据获取系统（5级详细度）
- 缺失操作分析
- 架构设计原则

**适用场景**: 了解系统架构、设计理念、扩展方向

---

#### 2.2 大规模命令填充报告
**文件**: [用户文档/LARGE_SCALE_COMMANDS_REPORT.md](用户文档/LARGE_SCALE_COMMANDS_REPORT.md)

**内容**:
- 新增命令系统详解
- GameObject/Material/Scene/Asset 命令
- 文档系统介绍
- 110+ JSON 示例
- 使用场景覆盖
- 性能基准测试

**适用场景**: 深入了解命令实现、查看大量示例

---

#### 2.3 缺失操作分析
**文件**: [用户文档/MISSING_OPERATIONS_ANALYSIS.md](用户文档/MISSING_OPERATIONS_ANALYSIS.md)

**内容**:
- 139+ 个常用但缺失的操作
- 按优先级分类（高/中/低）
- UI系统、动画系统、物理系统等
- 实现难度评估
- 推荐实现优先级

**适用场景**: 了解系统局限、未来扩展方向、功能请求

---

### 3. 工具文档

#### 3.1 物理对齐工具
**文件**: [用户文档/PhysicsAlign_CommercialFeatures.md](用户文档/PhysicsAlign_CommercialFeatures.md)

**内容**:
- 智能对齐与分布工具
- 完整的深度对齐功能
- RectTransform 支持
- 商业级尺寸匹配系统
- 使用示例和最佳实践

**适用场景**: 使用 Unity 编辑器对齐工具、UI 布局

---

## 🎯 快速导航

### 我想...

#### 🤖 让 AI 帮我使用 ESVMCP
👉 **阅读** [AI_INTERACTION_GUIDE.md](../Assets/ES/ESVMCP/AI_INTERACTION_GUIDE.md) ⭐⭐⭐⭐⭐

#### 📖 查找特定命令
👉 打开 [COMMAND_LIST.md](COMMAND_LIST.md)，查看分类命令清单

#### 🏗️ 了解系统架构
👉 阅读 [架构改进报告](用户文档/ARCHITECTURE_IMPROVEMENT_REPORT.md)

#### 📝 查看大量示例
👉 参考 [大规模命令报告](用户文档/LARGE_SCALE_COMMANDS_REPORT.md)

#### 💡 请求新功能
👉 查看 [缺失操作分析](用户文档/MISSING_OPERATIONS_ANALYSIS.md)

#### 🛠️ 使用对齐工具
👉 阅读 [对齐工具文档](用户文档/PhysicsAlign_CommercialFeatures.md)

---

## 📂 文件结构

```
ESFrameWorkPublish/
├── Assets/
│   └── ES/
│       └── ESVMCP/
│           ├── AI_INTERACTION_GUIDE.md ⭐⭐⭐⭐⭐ AI专用主文档
│           └── RunningData/
│               ├── Input/     # 放置JSON命令
│               ├── Archive/   # 已执行命令
│               └── Logs/      # 执行日志
│
└── Documentation/
    ├── README.md ⭐ 本文件（文档索引）
    ├── COMMAND_LIST.md ⭐ 命令清单
    └── 用户文档/
        ├── ARCHITECTURE_IMPROVEMENT_REPORT.md
        ├── LARGE_SCALE_COMMANDS_REPORT.md
        ├── MISSING_OPERATIONS_ANALYSIS.md
        └── PhysicsAlign_CommercialFeatures.md
```

---

## ✅ 系统状态

**版本**: v1.0 Commercial Grade  
**状态**: ✅ 已完成交互验证，可以开始使用  
**可用命令**: 66+ 个操作  
**文档完整性**: ✅ 完整  
**AI 就绪**: ✅ 是

---

## 🚀 快速开始（3步）

### 1️⃣ 确认系统就绪
检查文件夹是否存在：
- `Assets/ES/ESVMCP/RunningData/Input/`
- `Assets/ES/ESVMCP/Resources/ESVMCPConfig.asset`

### 2️⃣ 创建第一个命令
创建文件 `test.json` 放入 Input 文件夹：
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
      "saveToMemory": true,
      "memoryKey": "cube"
    },
    {
      "type": "CommonMaterialOperation",
      "operation": "SetColor",
      "target": "memory:cube",
      "propertyName": "_Color",
      "color": {"r": 1, "g": 0, "b": 0, "a": 1}
    }
  ]
}
```

### 3️⃣ 观察执行结果
- Unity 场景中出现红色立方体 ✅
- 命令移动到 Archive 文件夹 ✅
- Logs 文件夹生成执行日志 ✅

---

## 🆘 获取帮助

### 常见问题

**Q: 命令没有执行？**  
A: 检查 JSON 格式、文件夹路径、Unity Console 错误信息

**Q: 找不到对象？**  
A: 使用正确的目标定位方法（推荐使用 `memory:` 前缀）

**Q: 如何调试？**  
A: 查看 `Assets/ES/ESVMCP/RunningData/Logs/` 中的日志文件

**Q: 想要的功能不存在？**  
A: 查看 [缺失操作分析](用户文档/MISSING_OPERATIONS_ANALYSIS.md) 了解未来计划

---

## 📞 技术支持

- **AI 使用问题**: 参考 [AI_INTERACTION_GUIDE.md](../Assets/ES/ESVMCP/AI_INTERACTION_GUIDE.md)
- **命令查找**: 使用 [COMMAND_LIST.md](COMMAND_LIST.md)
- **功能请求**: 查看 [缺失操作分析](用户文档/MISSING_OPERATIONS_ANALYSIS.md)
- **Bug 反馈**: 查看日志文件，记录详细信息

---

**最后更新**: 2026年1月20日  
**文档维护**: ESVMCP 开发团队  
**系统状态**: ✅ 生产就绪

