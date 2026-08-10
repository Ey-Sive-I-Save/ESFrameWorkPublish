# ES 编辑器反馈音效方案

`Default/` 是标准兜底方案。`EditorFeedback/` 下的每个子目录都是一个可切换方案：

本目录位于 `Assets/` 之外，由 `ESEditorFeedbackSound` 直接读取 WAV 并在运行时创建 AudioClip。

```text
EditorFeedback/
  Default/         标准方案
  MyScheme/        自定义方案
    scheme.json    可选：显示名与启用音效列表
    click.wav      可选：覆盖 Default 中的对应音效
    ...
```

自定义方案缺少某个 WAV 时，会自动回退到 `Default/` 中的同名 WAV。

`scheme.json` 示例：

```json
{
  "displayName": "我的方案",
  "enabledKinds": ["click", "success", "warning", "error"]
}
```

`enabledKinds` 为空或不存在时，表示启用所有音效类型。

方案窗口中的“试听整套”会按 18 个标准音效类型顺序播放，试听期间不会修改当前方案；
点击“应用”才会切换方案。试听队列是临时的，窗口关闭、刷新缓存或域重载时会自动停止。
“精细试听”可以独立选择方案和单个音效类型，只播放目标 WAV，用于逐项比较和排查映射。

## 编辑器事件反馈

- Player Build 和 ES UnityPackage 发布完成：成功为 `success`，失败为 `error`。
- UnityPackage 导入：成功为 `success`，失败为 `error`。
- Prefab 实例更新：`confirm`；受控工具可分别调用应用/还原通知，映射为 `confirm` / `cancel`。
- PlayMode 进入/退出：`open` / `close`，使用独立开关，默认关闭。
- Hierarchy 创建或实例化/删除/复制粘贴/移动或重挂父级/重命名：
  `open` / `close` / `copy` / `navigate` / `refresh`，使用结构事件差量、名称快照和 120ms debounce。
- 用户资产新建/删除/移动或重命名：`open` / `close` / `refresh`，使用导入批次和 180ms debounce；代码与文本自动导入不发声。
- 场景打开或 EditMode Active Scene 切换：`scene`。同一路径在 750ms 内去重，
  ES 顶部工具栏和 Unity 场景生命周期共用同一通知入口，不会重复播放。
- “手动保存并刷新项目”：`refresh`。不监听无法区分自动保存和 Ctrl+S 的全局
  `sceneSaved` 回调，避免工具自动保存时产生额外反馈。
- ES 显式接入的枚举控件变更：`navigate`，PlayMode 中短路。

Hierarchy、资产变化等高频事件属于增强反馈；总开关或增强反馈关闭时不会统计其差量。
18 个标准音效类型分别维护独立冷却时间，不存在跨类型全局冷却；刷新缓存或切换方案时会清空冷却状态。
进入或即将进入 PlayMode 后，Hierarchy、资产、Selection、组件和 Prefab Authoring 回调全部短路；
运行态对象实例化、销毁、Transform 变化和重命名不会触发编辑器反馈。
不监听全局每次点击、全部键盘输入、每次代码自动导入，也不反射扫描 Unity 菜单。
通用 Unity/Odin 枚举和 Odin `[Button]` 没有稳定的全局公开执行事件；仅对 ES 自有控件和
关键按钮显式接入，不通过 GUI 事件猜测或第三方 Drawer 劫持制造误报。

`Default/` 当前使用 Kenney Interface Sounds（CC0 1.0）中的 WAV。许可证见同目录 `LICENSE.txt`。
完整 100 音素材包不在 Git 中，可自行从 https://kenney.nl/assets/interface-sounds 获取。
当前四套交付方案共 72 个 WAV，按方案和音效类型使用不同源样本，已通过全局 SHA-256 去重校验（72/72 唯一）。

内置备用方案：

```text
Default/    Default / Kenney Interface
Soft/       柔和 / Soft
Tech/       科技 / Tech
Arcade/     街机 / Arcade
```

当前支持的标准音效类型：

```text
click / success / warning / error / open / close / navigate / copy / locate
refresh / scene / confirm / cancel / type / addcomponent / removecomponent
prefabopen / prefabdirty
```

默认音量按使用频率分层，并可在方案窗口的“音量设置（18 类）”中逐类覆盖：

```text
click 45%      success 85%   warning 75%   error 90%
open 55%       close 55%     navigate 30%  copy 45%
locate 40%     refresh 45%   scene 65%     confirm 65%
cancel 65%     type 20%      add/remove component 35%
prefabopen 40%                prefabdirty 30%
```

“恢复全部默认音量”只删除本机 EditorPrefs 音量覆盖，不修改 WAV、方案配置或项目资产。

`type` 用于命令面板搜索输入；`addcomponent` / `removecomponent` 用于编辑器内添加和移除组件或脚本；
`prefabopen` / `prefabdirty` 用于预制件阶段打开和首次变脏。

Undo/Redo 属于增强反馈，默认关闭；开启后使用低音量 `navigate` 提示，默认音量为 18%，
可在方案窗口单独调节或设为 0 静音。显式保存/刷新默认音量为 12%，脚本编译成功默认音量为 30%，
二者都可独立调节；编译失败仍使用高音量 `error`。这些触发与 ES 工具内的
打开、复制、定位、刷新、场景跳转等反馈复用同一套 18 类音效。

自定义 WAV 格式约束：

```text
RIFF/WAVE
PCM 16/24/32-bit 或 IEEE float 32-bit
1-2 声道
采样率不高于 192 kHz
单文件不超过 4 MiB
播放时长不超过 2 秒
```

自定义文件不满足上述约束时会被拒绝并回退到 Default 方案或系统提示音。

## 播放诊断语义

Windows 和其他 Editor 平台优先复用 `AudioEditorSampler` 的最小预览链：创建
`HideAndDontSave` 对象，挂一个 `AudioSource`，设置 Clip 后直接 `Play()`；结束或刷新时销毁该对象。
该路径不创建 `AudioListener`，也不进入 Runtime Audio 系统。原生 `winmm PlaySound` 和
`AudioUtil` 仅作为该路径失败时的降级。

播放前会检查 `EditorUtility.audioMasterMute`。处于 Unity 编辑器主静音状态时不会绕过静音，
需要由操作者点击“取消 Unity 编辑器静音”菜单后重试。Windows 日志中的“播放已提交”表示
`winmm` 接受了请求；`AudioUtil` 路径会在 API 可用时用 `IsPreviewClipPlaying` 复验启动状态。
两者都不能证明操作系统音量、输出设备或 Unity 编辑器最终产生了可听输出。

`PlaybackFailed` 表示 Windows 原生播放与 AudioUtil 都不可用、拒绝请求或调用失败。
加载失败与播放调用失败分别按“类别 + 实际文件路径 + 原因”去重，不会因 Default 回退吞掉后续播放错误。

## 发布与安装边界

`ES/EditorFeedback/` 位于 `Assets/` 之外，必须随源码控制保留，或在
UnityPackage / UPM / 安装流程中显式复制到项目根。只复制
`Assets/Plugins/ES/` 不会带上这些 WAV，因此新环境会表现为“无声音”。
缺少方案或 WAV 时，请先检查项目根 `ES/EditorFeedback/` 是否存在。

全局 Selection 选择反馈，以及 `type / addcomponent / removecomponent / prefabopen / prefabdirty`
属于增强反馈，默认关闭，需要从菜单或方案窗口开启“增强反馈”后才会随 `Play()` 播放。
菜单“一键试听全部音效”属于显式试听，即使总开关关闭也会按顺序播放当前方案全部 18 类音效。
