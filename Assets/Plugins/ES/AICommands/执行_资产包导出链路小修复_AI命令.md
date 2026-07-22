# 执行：资产包导出链路小修复 AI 命令

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
默认改文件：是，仅导出链路小范围。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/CodexNotes/资产包分离窗口_预览与导出链路_AI协作警告.md
Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs
Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESAssetPackageBakeData.cs
```

## 执行要求

```text
修复导出链路显示、重复判断、目标丢失判断。禁止改成自动 _1，禁止只看目标文件存在。
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：必须编译 Assembly-CSharp-Editor.csproj
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```