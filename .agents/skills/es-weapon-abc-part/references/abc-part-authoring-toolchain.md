# ABCP 创建与 ABC 验证工具链

## 创建一个 Part

1. 复制 `abc-part-authoring-request.template.json` 到项目内的候选目录，
   修改 `partId`、A/B/C 描述、能力映射和 RouteStage 模板。当前工具链是
   `es-weapon-abc-part` 专用，`domain` 必须保持为 `weapon`；其他领域应先复制
   该工具链并登记自己的 RouteStage/闭环产物，不能伪装成武器 Part。
2. 运行（路径必须相对项目根）：

```powershell
& .agents/skills/es-weapon-abc-part/scripts/New-ESAbcPartContract.ps1 `
  -ProjectRoot (Get-Location).Path `
  -RequestPath .agents/skills/<part>/references/<request>.json `
  -OutputPath ES/Automation/Candidates/ABCP/<part>.json `
  -ReceiptPath ES/Output/ABC/<part>-authoring.receipt.json
```

生成器会读取 ABCC Core、Part schema 和 RouteStage Registry，拒绝未知能力、
未映射能力、未登记阶段、数据流断裂、非法回退和重复输出。已有相同内容时
只报告 `reused`；不同内容必须显式传 `-Force`。

## 用 ABC 系统验证

```powershell
& .agents/skills/es-weapon-abc-part/scripts/Test-ESAbcPartContract.ps1 `
  -ProjectRoot (Get-Location).Path `
  -PartPath ES/Automation/Candidates/ABCP/<part>.json `
  -ReportPath ES/Output/ABC/<part>-abc-validation.json
```

验证顺序固定为：Part schema/语义 → RouteStage 数据流 → ABCC A↔B 接口回放 →
ABCC Core StaticDeepReplay → ABCP StaticDeepReplay。报告中的
`abcSystemUsed=true`、`interfaceReplayCount`、`abccCoreReplay` 和
`weaponAbcpReplay` 必须同时成立才会得到 `ABCPStaticAcceptedThroughABCC`。

## 证据边界

这套工具链只产生静态合同和回放证据。它不导入 Prefab、不执行开火、不启动
Unity/Player/网络/宿主进程，也不证明伤害、输入、性能、Profiler、IL2CPP 或
发布行为；这些必须在获得单独授权后走 Runtime/Release acceptance。
