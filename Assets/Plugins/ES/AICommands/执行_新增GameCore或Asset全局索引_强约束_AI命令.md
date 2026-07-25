# 执行：新增 GameCore 或 Asset 全局索引强约束 AI 命令

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按“命令类型”和“默认改文件”决定是否允许改代码。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和相关入口文件，避免误改其他 AI 或用户的改动。
5. 只做本文允许的事情；如果用户需求超出本文范围，先说明需要换用哪个命令。
6. 结束时必须给出：已读规则、执行内容、改动文件、验证结果、剩余风险。
```

命令类型：安全执行。
默认改文件：是，仅新增目标类型对应的 ConfigKey、EnumKey、RuntimeData、Editor Drawer、注入入口和必要说明。
风险等级：L3。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/README.md
Assets/Plugins/ES/AIWarnings/通用架构理解_跨系统纠偏_AI协作警告.md
Assets/Plugins/ES/AIWarnings/GameCoreGlobalData与AICommands_AI协作警告.md
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs
Assets/Plugins/ES/0_Stand/Stand_Tools/SimpleTools/ESEnumScriptJump.cs
```

## 执行要求

```text
为一类新的 GameCore 或 Asset 建立全局索引支持。必须沿用现有 RuntimeDataGameCore / RuntimeDataAsset 分层、ConfigKey 风格、EnumKey 跳转机制和编辑器入口，不允许发明新架构。

开始写代码前，必须从用户需求或上下文确认以下信息；缺失时先让用户补齐：
1. 新类型归属：GameCore 或 Asset。
2. 英文类型名：例如 Quest、Dialogue、Font、Shader。
3. 中文显示名：用于 Inspector、提示文案和 AI 交接说明。
4. RuntimeData 命名：默认 ES<类型>RuntimeData，除非用户指定。
5. 是否需要 ScriptableObject 源字段：GameCore 通常可引用 SO 本体；Asset 通常记录资产键和可选注入来源。
6. Asset 类型：仅 Asset 归属需要，例如 GameObject、AudioClip、Material、Mesh、Sprite、Texture2D、UnityEngine.Object。
7. 是否接入 Editor 阶段注入：Asset 类型默认要支持从现有库或配置入口注入静态表。
8. 是否需要 ESGameManager.StaticCache 快捷入口：只有用户确认需要才加。

实现时必须遵守：
1. RuntimeDataGameCore 和 RuntimeDataAsset 必须继续分开，不要混成一个总表。
2. 新 ConfigKey 必须有对应 EnumKey；EnumKey 必须标记 [ESEnumScript("Assets/.../对应脚本.cs")]。
3. 枚举跳转只能走 ESEnumScriptJump / AssemblyStream 注册结果；禁止 Directory.GetFiles、AllDirectories、AssetDatabase.FindAssets 这类全项目兜底扫描。
4. 表注册使用 Upsert 思路，重复载入必须稳定，不允许重复追加污染。
5. Asset ConfigData 只保存键、索引、路径/地址/分组等轻量数据，以及 typed nullable loadedAsset 和 bool loadedAssetReady。
6. Asset ConfigData 不管理 refCount；引用计数属于加载器、池、预热或使用方。
7. 允许注销或空置状态：释放资源本体后保留键和元信息，loadedAssetReady 变 false，后续可恢复注入。
8. Pack 不是主要查询层；除非现有模板明确要求，否则不要给 Pack 增加多余查询逻辑。
9. 中文文本用于编辑器和文档；代码标识使用清晰英文，不要使用难记术语。
10. 只改目标类型相关文件，发现无关编译错误必须单独报告，不要顺手扩大修复范围。
```

## 推荐改动范围

```text
GameCore 类型通常涉及：
1. Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs
2. Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
3. Assets/Plugins/ES/Editor/ESDrawer/Normal/ESGameCoreConfigKeyDrawer.cs
4. 目标类型自己的 SharedData / RuntimeData / DataInfo 文件，按现有模板选择。

Asset 类型通常涉及：
1. Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs
2. Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
3. Assets/Scripts/ESLogic/Editor/ESRuntimeDataAssetEditorInjector.cs
4. Assets/Plugins/ES/Editor/ESDrawer/Normal/ESAssetConfigKeyDrawer.cs
5. 目标 Asset ConfigData 文件，按现有模板选择。
```

## 禁止事项

```text
1. 禁止把 GameCore 数据和 Asset 数据塞进同一个静态类或同一张表。
2. 禁止给 Asset 表加 refCount、自动加载器、对象池职责。
3. 禁止为了枚举跳转扫描全项目脚本。
4. 禁止为了“全量支持”一次性重构无关类型。
5. 禁止新增兼容旧 API，除非用户明确要求。
6. 禁止用 Pack 替代 RuntimeKey / RuntimeData 的主查询职责。
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 用户补充：列出本次确认到的归属、英文名、中文名、Asset 类型、是否注入、是否快捷入口。
3. 执行结论：用短句说明新增了哪类全局索引支持。
4. 改动文件：列出所有改动文件；没有改文件就写“无”。
5. 验证结果：必须编译 ES_Logic.csproj；若涉及 Editor 代码，也必须编译 ES_Editor.csproj。已有无关报错要单独标注。
6. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充：GameCore/Asset、英文类型名、中文显示名、Asset 资产类型、是否需要 SO 源字段、是否 Editor 注入、是否 StaticCache 快捷入口、具体玩法用途>
```
