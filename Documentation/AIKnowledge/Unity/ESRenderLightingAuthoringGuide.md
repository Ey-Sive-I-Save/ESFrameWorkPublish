# ES 打光配方协作指南

本指南定义策划、美术、程序共同维护 `ESRenderLightingRecipe` 的最小协作边界。配方是纯数据，不应通过拖动脚本、场景扫描或临时组件实现。

## 角色分工

| 角色 | 负责内容 | 不应直接修改 |
|---|---|---|
| 策划 | 选择视觉风格、质量档位、场景/内容类型；描述目标体验和平台约束 | URP 私有字段、Unity 场景对象、运行时回滚逻辑 |
| 美术 | 主光强度、色温、环境光颜色/强度、阴影风格、反射探针意图 | 质量预算上限、后端应用流程、验证回执 |
| 程序 | 配方校验、质量投影、URP 映射、捕获/差异/回滚、合同测试 | 未经评审的审美默认值和绕过配方的脚本 |

## 推荐流程

1. 使用 `ESRenderLightingRecipe.Create` 的命名参数编写或调整配方。
2. 使用 `TryCreate` 或 `TryResolve` 获取稳定的失败原因。
3. 使用 `Resolve(authoredRecipe, qualityProfile)` 只裁剪预算，保留美术参数。
4. 程序通过 `IESRenderLightingTarget` 显式注入 Unity/URP 目标。
5. 应用前捕获基线，应用后比较快照；失败必须走补偿和回滚。

## 维护不变量

- `BakedOnly` 不开启实时阴影，也不计入实时 Shadow Pass 预算。
- `SoftShadows` 和 `ContactShadows` 需要实时阴影通道。
- `MobileFlat` 不允许附加灯、软阴影和反射探针等高成本特性。
- 质量档位只能裁剪预算，不能重置色温、环境光或主光强度。
- URP 不支持的能力必须返回明确原因，不能伪造成功。
- 不使用 `FindObjectOfType`、`new GameObject`、`AddComponent` 或拖动脚本承载灯光逻辑。

## 变更验收

- 运行时程序集：`dotnet build ES_Stand.csproj --no-restore`。
- 合同测试：覆盖配方有效性、质量投影、快照差异和回滚。
- 资源闭包：`ES/Tools/Validation/Test-ESRenderTemplateResourceClosure.ps1`。
- Unity 运行时验证必须单独记录；静态通过不等于画面或性能验收。

