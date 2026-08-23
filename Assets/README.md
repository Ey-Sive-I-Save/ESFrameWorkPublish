# Assets 顶层目录规则

本目录按 Unity 的导入规则管理。移动任何已经被场景、Prefab、ScriptableObject 或构建配置引用的资产前，必须先确认 GUID 引用、字符串路径引用和生成流程。

## Unity 特殊目录：不按普通资源整理

- `Editor`：仅编辑器代码和编辑器专用资产。
- `Editor Default Resources`：供编辑器 API 按名称加载的资源。
- `Resources`：运行时 `Resources.Load` 入口；不要把普通资源随意放入。
- `StreamingAssets`：原样复制到构建产物的运行时文件入口。
- `Settings`：渲染与项目级资产设置。

## 项目主干

- `Scripts/ESLogic`：当前项目运行代码，具体规则见 `Scripts/ESLogic/README.md`。
- `Plugins/ES`：ES 框架底层、设计层、编辑器和框架示例。
- `Scenes`：当前项目场景；Build Settings 中的首场景必须来自此处或在本文件中明确例外。
- `ESNormalAssets`：项目常规资源和非构建默认场景；资源转为正式玩法内容前应明确归属。

## 第三方、样例与导入内容

- `Plugins/ES`：项目自有 ESFramework 源码、编辑器工具、AI 规则和示例；按 ES 的程序集与目录约定维护。
- `Plugins/<Vendor>`、`Packages`：已嵌入的第三方代码/二进制。禁止手工修改包内部；通过版本替换或外层适配升级。
- `Samples`：Package Manager 导入的样例，仅作学习和验证，不能作为正式玩法资产的默认归属。
- 顶层供应商目录（例如动画包、模型包、`Third Parts`）：第三方内容，保留原始许可证和 `.meta`；需要产品化时由项目资源层引用，不要直接改包内部结构。

## 维护规则

1. 新增项目玩法资源优先进入项目主资产目录，不在 `Samples`、第三方包或 `Resources` 中创建。
2. 新增运行脚本不放在顶层美术包、`TutorialInfo` 或临时导入目录。
3. 空目录、历史测试目录和模板目录必须明确标记为 `Tests~`、`Samples~`、`Developer/Templates` 或 `Obsolete`，不能伪装成正式运行能力。
4. 清理候选先做引用审计；确认无引用后再删除或移入归档目录。
5. `TutorialInfo`、顶层 `Editor` 的遗留调试资产、重复样例场景属于待审计项，未确认前不移动。
