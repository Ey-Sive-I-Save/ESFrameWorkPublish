# ES 枚举脚本跳转基础模板

职责：给编辑器、数据面板、AI Command 提供统一的枚举脚本定位和补充请求模板。

推荐入口：

```csharp
ESEnumScriptJump.OpenEnum(typeof(MyEnum), openInsertLine: true);
ESEnumScriptJump.OpenEnumMember(typeof(MyEnum), "SomeValue");
ESEnumScriptJump.CopyAppendRequest(typeof(MyEnum), "New Config Key", "None");
```

约定：

- 枚举脚本只追加，不重排已有值。
- `None = 0` 保持为无效/空置语义。
- 如果数据天然是动态外部配置，不应强行补枚举，继续使用 `stringKey`。
- 业务 Drawer 不再自己扫描文件，统一调用 Stand 的 `ESEnumScriptJump`。
