# ESGenericLife：通用生命周期组织器

状态：代码已接线，Unity 工程刷新、ES_Logic 编译与 Unity Test Runner 待最终验收。

## 定位

`ESGenericLife` 是一个根 GameObject 的通用生命周期组织器。它不属于 `Entity`、`Item`、Tag 或对象池，也不持有任何业务状态。

它当前已落地的第一个功能分部是 **GameObject Pool 生命周期**。以后只有出现真实调用者和明确边界时，才增加其他分部；不能因为名称中有 Generic 就预先创造泛化回调接口。

```text
ESGameObjectPoolModule
  -> ESPooledGameObject
  -> ESGenericLife.NotifyPoolSpawned / NotifyPoolDespawned
  -> IESGameObjectPoolLifecycle
```

`OnPoolSpawned` 与 `OnPoolDespawned` 是 Pool 分部的明确方法名，保留 Pool 语义，不改写成含义模糊的 `Activate`、`Reset` 或统一大接口。

## Pool 分部

```csharp
public interface IESGameObjectPoolLifecycle
{
    void OnPoolSpawned();
    void OnPoolDespawned();
}
```

每个根对象有一个主接收者；`Entity`、`Item`、Projectile Root 或 VFX Root 都可以担任它。主接收者必须是和 `ESGenericLife` 同一个 GameObject 上的 `MonoBehaviour`。

可按需注入扩展接收者：

```csharp
life.BindPoolRoot(entity);
life.RegisterPoolExtension(myFeature);
```

- 扩展不要求 Entity 引用或了解它。
- 扩展可为普通 C# 对象或 `MonoBehaviour`；不会从子节点自动扫描。
- 同一个 `ESGenericLife` 中，一个具体 Extension 类型只能注册一次。
- Root 不能同时作为 Extension 注册。
- 注册、注销和替换只能在 inactive / Despawn 状态完成；Spawn/Despawn 回调过程中禁止改变列表。

派发顺序固定：

```text
Spawn   ：Root -> Extensions（注册顺序）
Despawn ：Extensions（逆注册顺序） -> Root
```

这样扩展可以在 Root 状态已经建立后接入，也能在 Root 收口之前先释放自己持有的资源。

## Pool 状态与异常收口

对象池生命周期以 Pool 账本为权威：

```text
创建实例
-> 立即 inactive
-> Bind ESGenericLife
-> OnPoolDespawned（建立初始回收基线）
-> inactive 队列

借出
-> active 账本登记
-> OnPoolSpawned（仍 inactive）
-> SetActive(true)
-> 交给调用者

归还
-> 从 active 账本移除
-> OnPoolDespawned
-> SetActive(false)
-> inactive 队列或溢出销毁
```

`Awake()` 会由 Unity 在 `Instantiate` 期间触发，框架不能将它推迟。因此 `Awake` 只能做本地引用准备，不能依赖已借出状态、Tag 定义、外部绑定或 Pool 回调。所有依赖 Pool 的初始化放在 `OnPoolSpawned`。

单个接收者异常会被隔离并记录；Pool 仍通过 `try/finally` 将实例收口到 inactive 队列或明确销毁，不能让对象同时脱离 active 与 inactive 账本。Spawn 回调中请求归还会延迟到整个 Spawn 派发结束后执行，禁止 Spawn/Despawn 重入。

## 自动防错与性能

首次绑定、Prefab 校验和构建门禁可检查根节点上：

- 是否恰好存在一个未注册为 Extension 的 Root Pool 接收者；
- Root 是否与 `ESGenericLife` 同根；
- 是否存在跨根序列化引用；
- 是否有多个未注册接收者；
- Extension 是否重复。

这类检查只扫描**根 GameObject**，绝不扫描子树。热路径只读取缓存 Root，并循环实际注入的少数扩展：

- 没有 `GetComponentsInChildren`；
- 没有整棵层级缓存；
- 没有空对象的 Extension List 分配；
- 稳态 Spawn/Despawn 为 `O(1 + ExtensionCount)`，不产生 GC。

如果运行时动态增删根接收者，属于非法结构修改。开发版应报错；正式逻辑不能为容忍这种错误在每次借还重新扫描根节点。

## 禁止事项

- 不要恢复 `IESGameObjectPoolResettable` 或 Pool 借还时全子树广播。
- 不要让 Pool 直接识别 Entity、Item、Buff、Tag 或任意玩法类型。
- 不要把 Extension 列表序列化为第二套 Prefab 权威；Extension 由实际功能在 inactive 状态显式注入和释放。
- 不要将 `ESGenericLife` 误改为只有 Pool 的包装类型，也不要提前添加没有真实调用者的通用生命周期大接口。
- 不要把 `OnEnable` 当作 Pool Spawn 的等价物。

## 当前验证

`ESGenericLifePoolTests` 覆盖：新建、预热、重复借还、Spawn 异常回收、扩展唯一性与顺序、多主接收者拒绝、跨根序列化引用拒绝。Unity Test Runner 运行结果仍应作为最终运行时验收证据。
