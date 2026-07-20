# Context 目录说明

`Runtime/Context` 是运行时上下文值池系统，不再放在 `Shared`。

## 当前分类

```text
Core        ContextPool、ContextOperation 等核心定义
Values      IContextitectureValue 及具体值类型
Operations  可序列化 Context 操作
Docs        说明文档
```

## 放置规则

- `ContextPool` 是独立运行时系统，不塞回 `Shared`。
- `CacherPool` 是 Operation 执行期缓存，放在 `Runtime/Operation/Cache`。
- 开发者挂载入口仍可放在 `Runtime/Developer/Components/Context`。
- 新增 Context 值类型放 `Values`。
- 新增 Context 操作放 `Operations`。
