# SO 表格规则测试样例

## 目录

- `01_Scripts/01_NativeSO_ObjectRow`：普通 SO，一行对应一个 SO。
- `01_Scripts/02_NativeSO_ListRow`：普通 SO，一行对应 SO 内部一个 List 元素。
- `01_Scripts/03_SoData_InfoRow`：SoData Group/Info，一行对应一个 Info。
- `01_Scripts/04_SoData_InfoListRow`：SoData Group/Info，一行对应 Info 内部一个 List 元素。
- `01_Scripts/99_Pack_Unsupported`：Pack 不支持示例，仅用于边界说明。
- `02_Assets/01_NativeSO_ObjectRow`：普通 SO 对象行资产。
- `02_Assets/02_NativeSO_ListRow`：普通 SO List 行资产。
- `02_Assets/03_SoData_InfoRow`：Group / Info 对象行资产。
- `02_Assets/04_SoData_InfoListRow`：Group / Info List 行资产。
- `02_Assets/第一关的`、`02_Assets/第二关的`：已有导入生成测试资产，保留不移动。
- `03_Tables`：建议作为导出表格目录。

测试 SO 资产单独放在：

- `Assets/ESNormalAssets/Data/Example_SOTableRule/NativeSO`：可直接拖到构建阶段的普通 SO 资产。
- `Assets/ESNormalAssets/Data/Example_SOTableRule/SoData/Info`：可直接拖到构建阶段的 Info 资产。
- `Assets/ESNormalAssets/Data/Example_SOTableRule/SoData/Group`：空 Group 资产，用于测试 Group 类型识别。
Pack 已不作为 SoTableRule 支持目标。

## 当前案例脚本

- `ExampleNativeSoTableSource`：普通 SO，一行对应一个 SO，包含枚举、Flags、List、嵌套字段。
- `ExampleNativeSoListTableSource`：普通 SO 持有 `rewards`，一行对应一个 `ExampleNativeSoRewardRow`。
- `ExampleSoTableInfo` + `ExampleSoTableGroup`：SoData Group/Info，一行对应一个 Info。
- `ExampleSoTableDialogueInfo` + `ExampleSoTableDialogueGroup`：SoData Group/Info 持有 `lines`，一行对应一个 `ExampleSoTableDialogueLine`。

## 面板结构模板

在 `高级配置 -> 表格结构模板` 里可一键应用：

- `普通SO：对象行` -> `01_NativeSO_ObjectRow`
- `普通SO：List行` -> `02_NativeSO_ListRow`
- `Group/Info：Info行` -> `03_SoData_InfoRow`
- `Group/Info：List行` -> `04_SoData_InfoListRow`

模板会自动设置对象体系、Group 列、行绑定、List 字段、元素 Key、嵌套展开和默认批次策略。

## 第一轮测试

1. 创建 `ESSoTableDataRule`。
2. 构建阶段拖入普通 SO 资产，或拖入 `01_Scripts/01_NativeSO_ObjectRow/ExampleNativeSoTableSource.cs` 作为脚本类型。
3. 绑定并构建字段映射。
4. 使用阶段导出 CSV 到 `03_Tables`。
5. 修改 CSV 后导入，观察普通 SO 字段变化。
6. 重点看枚举和列表字段：`quality`、`tags`、`keywords`、`levelRewards`、`qualityOptions`。

## 第二轮测试

1. 构建阶段拖入 `Assets/ESNormalAssets/Data/Example_SOTableRule/SoData/Info/Example_Info_A.asset`。
2. 绑定并构建字段映射。
3. 确认 `KeyName`、普通字段、枚举字段、列表字段能被扫描。
4. Group 脚本和资产可用于测试类型识别；Pack 不作为当前工具目标。

## List 行测试

普通 SO：

1. 构建阶段拖入 `01_Scripts/02_NativeSO_ListRow/ExampleNativeSoListTableSource.cs` 脚本或对应资产。
2. 高级配置里点击 `普通SO：List行`。
3. 确认 `List 字段 = rewards`。
4. 确认 `元素 Key 字段 = rowKey`。
6. 重建字段映射后导出，表格一行对应一个奖励行。

Group/Info：

1. 构建阶段拖入 `01_Scripts/04_SoData_InfoListRow/ExampleSoTableDialogueInfo.cs` 或 `ExampleSoTableDialogueGroup.cs`。
2. 高级配置里点击 `Group/Info：List行`。
3. 确认 `List 字段 = lines`。
4. 确认 `元素 Key 字段 = rowKey`。
7. 重建字段映射后导出，表格会包含 `group`、`key`、`rowKey`，一行对应一个对白行。
