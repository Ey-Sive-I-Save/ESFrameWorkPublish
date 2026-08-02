# 新增 GameTag AI 命令

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
默认改文件：是，仅 GameTag 语义、默认说明和必要引用。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/通用架构理解_跨系统纠偏_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md
Documentation/ESTAG_FULL_LIFECYCLE_STANDARD.md
```

## 执行要求

```text
新增 GameTag。说明分组、语义、归属系统、互斥关系，优先使用 Reserved 位，不用 Tag 替代 Buff/State/Skill。

P0：策划、业务代码和 Inspector 高频接触的 Tag 名称、字段、菜单与 Picker 文案必须使用直接常用词；禁止用生僻或歧义英语包装日常功能。

P1：禁止为一份 Tag 列表创建无职责 Config/Data/Info 包装。写入者直接持有 `List<ESTagStableReference> tags`，运行时以 `ESTagLeaseSet` 管理自身申请的生命周期；除非新类型明确承担多字段不变量、独立生命周期、版本迁移或独立验证，否则不得新增。

`ESTagGrantConfig` 已移除：禁止恢复、兼容或复制该模式。
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：必须编译 ES_Logic.csproj
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
