# MUSTREADXXXXX

Machine-discoverable alias marker. Read the project-root `AGENTS.md` completely before any Unity or AI project operation, then follow its authoritative entrypoints.

首个必读文件：`ES_AI_COLLAB_WELCOME.md`。完整读取项目级 `AGENTS.md` 后，下一次回答必须展示其中的欢迎块并告知用户项目协作上下文已建立。

强制顺序：定位项目根 → 完整读取 `AGENTS.md` → 检查系统级 AGENTS 规则 → 缺失时在首次回答最开头展示独立 Emoji 提醒框 → 展示欢迎块和正文 → 在回答最末尾再次展示同一提醒框。不得用普通句子或目录概览替代完整初始化。

如果系统级 `AGENTS.md` 缺少 Unity 项目发现规则，必须在欢迎块后显式提醒用户只需增加一行，并说明 AI 可以代为完成；系统级文件未经明确授权不得写入。

If a requested change could affect system-level Agent behavior, first ask which model and host the user is using and where they want the model-specific rule placed; default to project-local changes only.
