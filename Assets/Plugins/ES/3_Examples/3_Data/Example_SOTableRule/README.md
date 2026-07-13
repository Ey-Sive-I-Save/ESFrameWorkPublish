# SO 表格规则测试样例

## 目录

- `01_Scripts/NativeSO`：普通 ScriptableObject 测试源。
- `01_Scripts/SoData`：SoData Info / Group / Pack 测试类型。
- `03_Tables`：建议作为导出表格目录。

测试 SO 资产单独放在：

- `Assets/NormalResources/Data/Example_SOTableRule/NativeSO`：可直接拖到构建阶段的普通 SO 资产。
- `Assets/NormalResources/Data/Example_SOTableRule/SoData/Info`：可直接拖到构建阶段的 Info 资产。
- `Assets/NormalResources/Data/Example_SOTableRule/SoData/Group`：空 Group 资产，用于测试 Group 类型识别。
- `Assets/NormalResources/Data/Example_SOTableRule/SoData/Pack`：空 Pack 资产，用于测试 Pack 类型识别。

## 第一轮测试

1. 创建 `ESSoTableDataRule`。
2. 构建阶段拖入 `Assets/NormalResources/Data/Example_SOTableRule/NativeSO/Example_NativeSO.asset`。
3. 绑定并构建字段映射。
4. 使用阶段导出 CSV 到 `03_Tables`。
5. 修改 CSV 后导入，观察普通 SO 字段变化。
6. 重点看枚举和列表字段：`quality`、`tags`、`keywords`、`levelRewards`、`qualityOptions`。

## 第二轮测试

1. 构建阶段拖入 `Assets/NormalResources/Data/Example_SOTableRule/SoData/Info/Example_Info_A.asset`。
2. 绑定并构建字段映射。
3. 确认 `KeyName`、普通字段、枚举字段、列表字段能被扫描。
4. Group / Pack 脚本和资产可用于测试类型识别；实际导入导出先从 Info 资产或 Info 文件夹开始。
