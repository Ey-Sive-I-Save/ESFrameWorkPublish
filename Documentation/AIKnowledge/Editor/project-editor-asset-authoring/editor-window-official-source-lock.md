# Unity 2022.3 EditorWindow 恢复官方来源锁

本文件是 `es.unity.editor-window-lifecycle-menu.v1` 的可重读外部来源快照，只锁定 Unity
2022.3 官方文档的来源身份、HTTP 响应哈希和本条目使用的最小 API 语义。它不替代 ES
owner/PendingFollowOwner P0，也不代表运行过 Unity。

## Online documentation

SHA-256 对读取到的 HTTP 响应正文按 UTF-8 字节计算。前五项读取于 2026-08-23，后四项
读取于 2026-08-24，均返回 HTTP 200。版本化页面仍可能更新；响应哈希变化时必须把依赖
结论标记为 stale，并重新核对而不是沿用本锁。

| URL | Raw UTF-8 SHA-256 | 支撑范围 |
|---|---|---|
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorWindow.html | `5676fdb13b497446be79d1babdf6e53f7390a9254000aa40ed1e2ef69b803781` | EditorWindow 生命周期与宿主 API 范围 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorWindow.GetWindow.html | `04a3ae243ada217140ab4b125e5626d6f304c51f7705e5342044a9840fa17803` | 同类型可见窗口复用，否则创建并显示 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssemblyReloadEvents.html | `bdfd802e9762aec3c08b46fd7ff1133b55d87285389d0332b00ddfd19a148437` | Reload 前后事件入口 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SessionState.html | `3cd28e66d7a85d1f3a6011f05da521b3d62703dcd89da9bc75cdee62a033afbe` | 跨程序集重载、退出 Unity 后清空的会话状态 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MenuItem.html | `0cbb2da1b132889d62e310d75c91bd0a37bc75ad9db93674e5e6d90c157b3adc` | 静态菜单入口和验证函数合同 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorPrefs.html | `e84d2fdc369f3e579a4d872e3cb58e4f6871104455c6e8fcc51f177a0c9b1cf2` | 本机 Editor 用户偏好的跨会话存储 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/InitializeOnLoadAttribute.html | `760c8e91bd8cb922ecad1935702cb42500e67caa5b88f3daed93e2b832834102` | 自动执行时机与资产导入尚未完成时加载可能失败的风险 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/script-Serialization.html | `f7dc82204a5c081b73114149199d07ce0e6304f3f182f995467d3bbf5ee0d0ad` | 受支持实例字段和不序列化 static/const/readonly 的边界 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GlobalObjectId.html | `98a64e228b51080738dc7cf60edfd9e4cf719284db73601b755558609181c304` | 项目范围持久对象身份，以及对象移动 Scene 后 ID 改变 |

## Locked interpretation

- `SessionState`、`EditorPrefs`、Unity 序列化字段和 `GlobalObjectId` 解决不同生命周期的问题，不能互相替代。
- 自动初始化只能安全承担轻量注册；依赖资产导入结果的解析必须延后并有失败/重试边界。
- `GlobalObjectId` 解析失败不是按名称、层级或最近选择猜测对象的许可；Scene 移动是必须覆盖的失效场景。
- 官方文档不定义 ES 的 ownerKey、PendingFollowOwner、真实关闭脱离意图或窗口菜单架构，这些仍由项目 P0 和当前源码拥有。

## Non-claims

本来源锁没有启动 Unity、触发 Domain Reload、打开窗口、移动 Scene 对象、运行 Test Runner、
采集 Profiler 或验证 Player/IL2CPP。HTTP 200 和响应哈希只证明当次读取到的页面字节。
