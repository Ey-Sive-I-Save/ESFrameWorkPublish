# ESSearchDropdown 标准展示

打开菜单：

```text
【ES】/自动化与开发/文档与示例/编辑器示例/ESSearchDropdown 标准展示
```

展示内容：

1. 任意集合的一行式 `OpenItems<T>` API。
2. 固定命令使用的 Builder API。
3. 图标、分组、描述、关键词、徽章和当前选中状态。
4. 仅在打开时扫描项目的延迟 Provider。
5. Provider 与选择回调的异常隔离。

这个窗口既是开发示例，也是视频录制和功能回归入口。示例不会创建或修改项目资产；资源选择只会定位现有 Prefab 或 Scene。

## 可复制代码案例

`Editor/ExampleESSearchDropdownCodeSamples.cs` 提供六个互相独立的案例：

- 最简字符串集合
- Builder 固定命令
- 泛型业务对象
- 完整结构化条目
- 延迟资源 Provider
- 标准 IMGUI 按钮调用
