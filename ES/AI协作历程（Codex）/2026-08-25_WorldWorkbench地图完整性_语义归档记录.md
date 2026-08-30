# World Workbench 地图完整性与语义归档记录

## 记录范围

- 本记录只保存本轮目标、结论、证据边界与后续差距，不保存完整对话、窗口身份或终端历史。
- 用户决定暂不继续更新工作台，仅保留当前实现与语义归档。
- 对应语义归档：`ESFrameWorkPublish/5d21a0c3c1cd48c2ac12ed45331aba94.json`

## 当前目标

恢复并验证 World Workbench 的完整地图预览与可用交互，重点关注地图显示不完整、总览裁剪和工作台关闭安全边界。

## 已确认根因与修改

1. 游戏预览切换到“总览”后仍沿用流式半径裁剪，远端放置物被隐藏。
2. 切换相机模式只更新相机，不重建 PreviewScene，已被裁剪的远端对象不会恢复。
3. 已在 `Assets/Scripts/ESLogic/Editor/World/ESWorldAuthoringViewport.cs` 修正：
   - 总览模式跳过玩家/第三人称的流式放置物裁剪；
   - 相机模式变更后重建预览内容。

## 已有证据

- `es-editor-availability-validator`：`static-passed`，`blockedCount=0`。
- 目标文件差异检查通过。
- 语义归档文件已读取并通过 JSON、Schema、项目标识、相对路径和无对话恢复字段检查。

## 未证实与明确边界

- Unity 编译、ReloadDomain、EditMode、交互和视觉运行时验收尚未执行。
- 程序化植被层、散布层尚未有正式资源驱动的完整 PreviewScene 视觉证据，不能宣称最终地图已完整。
- 工作台关闭会释放 PreviewScene 和编辑会话；若草稿仍脏，关闭会丢弃未提交 Draft，因此关闭前必须确认已保存。

## 后续恢复入口

- 读取语义归档：`Get-ESProjectSemanticArchive.ps1 -ProjectKey ESFrameWorkPublish -ArchiveId 5d21a0c3c1cd48c2ac12ed45331aba94`
- 继续工作台地图修复时，先核对当前源码、Unity 状态和运行时证据，不把本记录或语义归档当作完成证明。

