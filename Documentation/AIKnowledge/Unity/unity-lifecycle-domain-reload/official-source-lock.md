# Unity 2022.3 生命周期官方来源锁

本文件是 `unity-lifecycle-domain-reload` 条目的可重读来源摘要。在线页面来自 Unity
2022.3 官方手册；API 摘要来自本机 Unity Hub 注册的 `2022.3.45f1` Editor 安装。
这里只锁定来源身份、原始响应哈希和支撑本条目的最小原文语义，不代表运行过 Unity。

## Online manuals

以下 SHA-256 对 2026-08-23 读取到的 HTTP 响应正文按 UTF-8 字节计算。Unity 的版本化
文档页面仍可能更新；响应哈希变化时，知识条目应视为 stale 并重新核对。

| URL | HTTP | Raw UTF-8 SHA-256 | 支撑范围 |
|---|---:|---|---|
| https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html | 200 | `1522acc3a2425b9434eb84db26f5a6a1829fa8825cda00002f401cf5f4257e13` | Awake、OnEnable、Start、Update 与对象间顺序边界 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/DomainReloading.html | 200 | `7313d20cee0b2ccbe78484a93fbaffa9daec1ce2acf925409cc2fd3c6262c1df` | Domain Reload、静态字段、静态事件与显式重置 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/ConfigurableEnterPlayMode.html | 200 | `c3b82130e000bc0ee89d79e935c9d595232bc221f6f44a1db2be2fb0c2795245` | Enter Play Mode 的 Domain/Scene 两个独立开关 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/SceneReloading.html | 200 | `bf199b6437d7266f68dc5d229b15186071af8bb16d988b34db58c1b1bbcbdfda` | 禁用 Scene Reload 的回调模拟与启动差异 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/class-MonoManager.html | 200 | `e25ce76b5787e7b378ce0400c6f4b84f08540ce485221efca2f5dbb2fb203dac` | Script Execution Order、DefaultExecutionOrder 与限制 |

## Installed API documentation

Unity Hub 注册安装：`D:\UnityEdi\2022.3.45f1\Editor\Unity.exe`。

- `D:\UnityEdi\2022.3.45f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml`
  - 完整文件 SHA-256：`ce120ca131e9d371794fa1d453bdd97d8ed3d39dee97c40f8467f7cac2b1bbce`
  - `DefaultExecutionOrder`：指定一个 MonoBehaviour 派生类型相对其他派生类型的执行顺序。
  - `RuntimeInitializeLoadType.BeforeSceneLoad`：首个场景对象已载入内存、但 Awake 尚未调用。
  - `RuntimeInitializeLoadType.AfterSceneLoad`：首个场景对象已载入内存且 Awake 已调用。
  - `RuntimeInitializeLoadType.SubsystemRegistration`：运行时启动、首个场景载入前调用。
  - `RuntimeInitializeOnLoadMethodAttribute`：用于运行时启动并加载首个场景时的回调。
- `D:\UnityEdi\2022.3.45f1\Editor\Data\Managed\UnityEditor.xml`
  - 完整文件 SHA-256：`85c417af947171f08ec139c80231e896102d3ea09f086b1b91bf2d0b20e60020`
  - `EditorSettings.enterPlayModeOptionsEnabled`：决定 Enter Play Mode Options 是否启用。
  - `EnterPlayModeOptions.DisableDomainReload`：进入 Play Mode 时不销毁、创建和重载
    .NET Application Domain。
  - `EnterPlayModeOptions.DisableSceneReload`：进入 Play Mode 时不从磁盘重载整个 Scene，
    而是重置 Scene 状态并模拟所需回调。
  - `EnterPlayModeOptions.None`：进入 Play Mode 时重载 .NET Application Domain 和整个 Scene。

## Locked interpretation

- 默认 Domain Reload 会重置脚本状态，包括静态字段和已注册的静态事件处理器。
- 禁用 Domain Reload 后，静态字段与静态事件跨 Play 循环保留；运行时代码应在
  `RuntimeInitializeLoadType.SubsystemRegistration` 阶段显式恢复可重复初始状态。
- 对场景资产中的对象，Awake/OnEnable 阶段整体先于 Start；运行时实例化和不同对象之间
  不提供可依赖的全局顺序，跨对象依赖应放到显式协调阶段。
- Script Execution Order 只定义不同 MonoBehaviour 类型在同一事件类别中的相对顺序；
  它不为 `RuntimeInitializeOnLoadMethod` 回调排序，也不解决同类型实例之间的顺序。
- Domain Reload 与 Scene Reload 是独立轴。关闭 Scene Reload 仍有 Unity 模拟的生命周期
  回调，并且进入 Play Mode 的启动行为与构建启动不再完全等价。

## Non-claims

本来源锁没有运行 Editor、PlayMode、Domain Reload、场景重载、测试、Profiler、Player 或
IL2CPP。在线响应哈希只证明本次读取到的页面字节，不证明未来页面内容不变。
