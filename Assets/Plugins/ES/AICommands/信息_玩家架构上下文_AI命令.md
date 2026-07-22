# 信息：玩家架构上下文 AI 命令

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

命令类型：信息补全。
默认改文件：否。
风险等级：L1。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/PlayerArchitecture/玩家对象模型重构_AI协作说明.md
Assets/Plugins/ES/AIWarnings/PlayerArchitecture/角色通用架构验证_MMO开放世界角色切换剧情RPG战斗_AI协作说明.md
Assets/Plugins/ES/AIWarnings/PlayerArchitecture/模型重构_今日修正_CoreDomain与AI域控制_AI协作警告.md
```

## 执行要求

```text
不改代码不改场景。说明外壳、Core、Domain、Module、AI 域、State、Input、RuntimeMode 的关系，指出过时理解。
```

## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：无需编译
5. 剩余风险：列出仍需人工确认的点。
```

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```