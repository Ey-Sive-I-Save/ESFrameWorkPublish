# ESTestAssets

`Assets/ESTestAssets` 是项目测试资产的独立根目录，不属于正式内容资产，也不参与 `Assets/ESNormalAssets` 的内容注册、资源库、Manifest 或发布寻址。

## 边界

- 只保存测试场景、测试物体、测试材质、程序化测试纹理和测试专用脚本。
- 禁止引用 `Assets/ESNormalAssets` 中的角色、Prefab、材质、贴图或数据资产。
- 禁止引用第三方 Demo/Sample 资产作为测试成立的前提。
- 测试资产可以依赖被测 ES Runtime/Shader API 和 Unity/URP 内置能力。
- 正式资源收集与发布时应排除本目录；本目录不能冒充正式产品内容。

当前首个独立测试包：[`CompositeShaders`](CompositeShaders/README.md)。
