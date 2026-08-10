# ESResWindow 设置与构建页布局整改需求

## 1. 目标

把 `ESResWindow` 的“设置与构建”页调整为紧凑、可直接使用、符合 ES 现有 Odin 规范的布局，不引入自定义工作台。

## 2. 当前问题

- 页面信息密度过高，完整设置、资源库、构建流程同时堆在页面里。
- 此前的自定义滚动窗、splitter、固定 footer 不适合 ES 的 Odin 页面，也造成过按钮/设置/文件夹消失。
- “构建与运行”和“文件夹”是 `ESGlobalResSetting` 里的两组真实配置，不应只被塞进完整 Inspector。

## 3. 目标布局

### 3.1 页面整体

页面从上到下只保留：

1. 紧凑“发布设置”。
2. 主体工作区。

### 3.2 顶部“发布设置”

- 只显示核心摘要：应用平台、资源加载模式、游戏版本号。
- 提供“完整设置”折叠项，默认折叠。
- 展开后保留原有完整 `ESGlobalResSetting` Inspector 绘制能力，用于低频高级配置。

### 3.3 主体工作区

主体工作区分左右两列：

#### 左列

按从上到下的顺序合并：

1. “资源库”
   - Library 搜索。
   - 仅参与构建/仅异常过滤。
   - Library 列表、参与构建开关、Book/Page/AB 信息。
2. “构建与运行”
   - 应用平台。
   - 资源加载模式。
   - 游戏版本号。
   - 输出资源详细流程日志。

#### 右列

按从上到下的顺序：

1. “五步发布”
   - 1. 烘焙引用。
   - 2. 规划并标记。
   - 3. 构建资源包。
   - Consumer 代码包准备。
   - 4. 发布资源包。
   - 5. 打开远端发布工作台。
2. “文件夹”
   - 服务器网络路径。
   - 默认资源库放置文件夹。
   - 全局排除文件夹。
   - 下载持久相对路径。
   - 高频目录与管线目录快捷打开。

## 4. 非目标

- 不引入自定义 `DrawEditors` 工作区。
- 不引入 `EditorGUILayout.BeginScrollView` 作为页面主体。
- 不引入固定 footer、splitter、窄窗口上下切换。
- 不引入动效、庆祝横幅、阶段动画。
- 不重写资源管线、烘焙、规划、构建、发布逻辑。
- 不自动创建 Consumer、不自动改名、不自动设 Total Consumer。

## 5. 必须保留

- Odin 原生的分组和绘制方式。
- Library 搜索、过滤、选择恢复、参与构建开关。
- 五步发布按钮和 Consumer 代码包准备按钮。
- 构建/发布门禁逻辑。
- `ESGlobalResSetting` 的序列化字段、Undo、保存行为。
- “文件夹”区保留生成目录快捷打开能力。
- 页面关闭后清理序列化编辑器和临时 UI 状态。

## 6. 实施边界

可改文件：

- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/ResWindow/ESResWindow.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData/ESGlobalResSetting.cs`

不要改：

- `GameCore` 稳定壳模型。
- `ConfigTable` 代际状态。
- `Provider` 切换、加载、回收逻辑。
- `Catalog/Plan/Build/Publish` 管线逻辑。
- 其他 AI 或用户的脏工作树改动。

## 7. 验收标准

### 7.1 静态验收

- `ES_Editor.csproj` 编译通过，0 warning / 0 error。
- 修改中文文件后 UTF-8 Guard 通过。
- `git diff --check` 通过。

### 7.2 Unity 实机验收

- 打开“资源管理窗口”，进入“设置与构建”页。
- 页面无需滚动即可看到：
  - 顶部发布设置摘要。
  - 左侧资源库与构建与运行。
  - 右侧五步发布与文件夹。
- 所有按钮、字段、列表均可见且不重叠。
- 无 Odin Group 报错，无 NRE。
- 展开“完整设置”后仍可编辑原有配置。
- 打开、关闭、Domain Reload 后页面恢复正常。
- 窄窗口和长中文名下不出现按钮丢失或字段截断。

## 8. 最终状态口径

只有 Unity 实机验收通过后，才能标记为：

> UI Layout Implemented / Unity Visual Acceptance Passed

dotnet 编译通过不能替代 Unity 实机验收。
