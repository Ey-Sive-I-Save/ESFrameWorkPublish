# ES 编辑器绘制与序列化套件：PropertyTree、多目标与迁移边界 AI 协作警告

**状态：现行编辑器约束。** 本文覆盖 `ESEditorSection`、`ESPolymorphicReferenceDrawer`、`ESTypeCatalog`、`ESFieldRow` 与 Odin PropertyTree/Processor；它们是编辑器底层能力，不归类为普通 SimpleTools。

最后核对：2026-08-02。

## 权威数据边界

- 资产的 Unity 序列化字段和 `SerializeReference` 数据是唯一权威；Drawer、PropertyTree、VisualElement、类型目录和 SessionState 都只是编辑投影或 UI 状态。
- 禁止以窗口静态字段、折叠状态、选择缓存或 PropertyTree 缓存作为第二份真源，更不得在重选/域重载后把缓存反写覆盖资产。
- 同一业务对象可以被多个窗口查看，但每个窗口必须独立拥有自己的 OdinEditor、PropertyTree、SerializedObject 和临时桥接对象；这些实例不得跨窗口复用，也不得复制出第二份业务数据。
- 临时桥接对象只负责把权威数据暴露给绘制层，解绑、重选、窗口销毁和 Domain Reload 时必须释放；重建后只能从当前资产与稳定身份重新解析。
- `ESEditorSectionAttributeProcessor` 基于 Odin Attribute Processor 解析 Begin/continue/End Section 并重写 GroupID 维持嵌套。Section 语义由声明和 Processor 解析决定，禁止依赖偶然的绘制顺序或手工拼 GroupID。
- `ESEditorSectionNavigatorIMGUI` 使用 `SessionState` 保存导航选择；该状态只服务当前编辑器会话，不能写入业务配置或迁移数据。

## 多态引用和类型目录

- Drawer 可以消费 `0_Stand/BaseDefine_Law` 中的公共协议，但不得为了绘制方便，在 Editor、Drawer 或 Attributes 文件中临时定义 Runtime/Editor 共用协议。新增或迁移此类契约必须同时遵守 P0 `项目最高警告_P0_公共协议与元数据声明分层_AI协作警告.md`。
- `ESPolymorphicReferenceDrawer` 的选择器只可从 `ESTypeCatalog` 提供的合法、可序列化、可解析具体类型中选择；目录用于候选筛选，不替代序列化类型身份。
- 缺失 `SerializeReference` 类型必须显示原始类型信息并要求用户明确恢复脚本/程序集或选择替代类型。禁止自动置空、自动换成首个候选或静默丢弃旧数据。
- 已存在的 Drawer 会处理多目标不一致、批量编辑上限和 Undo；新增功能必须沿用同一条显式批量赋值路径，不能只改代表对象而声称所有目标已同步。
- 多目标编辑只允许相同 property path、兼容基类和可确认的目标集合。类型或空值不一致时先显示 mixed 状态，只有用户明确选择类型才允许统一覆盖。

## Undo、脏标记与重建

1. 任意资产写入前必须记录 Unity/Odin Undo；批量写入使用同一个明确 Undo 组。
2. 通过 `SerializedProperty` 写 `managedReferenceValue` 后必须 `ApplyModifiedProperties()` 并标记对应资产 Dirty；直接 Odin 值写入也必须走等价 Undo/Dirty 路径。
3. 数组和深层嵌套必须依赖稳定 property path / PropertyTree 在当前帧解析，禁止缓存“第 n 项”的对象引用后跨删除、重排、重选或域重载继续使用。
4. 重选、窗口 Disable/Destroy、Inspector 重建、域重载前后必须解除 UI callback、Dispose/废弃旧 PropertyTree，再从当前序列化资产重建视图。
5. 数据迁移必须是显式、可 Undo 或可恢复、可审计的版本步骤；Drawer 不得为了“显示正常”在 OnGUI 中悄悄迁移资产。

## `ESFieldRow` 与展示层

`ESFieldRow`、`ESTypeCatalog` 和其他 Presentation 组件只解决布局、候选与展示一致性。它们不得承担业务验证、GameCore Bake、资源加载、运行时实例化或数据所有权。

## 必测矩阵

```text
单目标与多目标相同/不一致值、类型切换、清空与 Undo/Redo、缺失类型、
数组重排和深层嵌套、重选资产、窗口关闭重开、域重载、旧数据迁移、
多个 Inspector/窗口同时查看同一资产。
```

现有源码已具备部分多目标、缺失类型和 Undo 处理；Unity Test Runner 与上述完整回归矩阵仍需实测，不能把“Drawer 能显示”写成“序列化迁移已完全验收”。
