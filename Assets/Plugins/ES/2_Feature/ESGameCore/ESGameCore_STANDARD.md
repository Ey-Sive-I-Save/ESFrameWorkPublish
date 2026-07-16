# ESGameCore Standard

## Position

`ESGameManager` is the runtime root for a normal game project.
It owns four domains and delegates real work to modules.

```text
RuntimeDomain
WorldDomain
PlayerDomain
PresentationDomain
```

## Rule

`ESGameManager` is allowed to be a global entry, lifecycle owner, and cross-domain coordinator.
It should not directly implement input, UI, save, combat, scene, entity, or audio details.

## First Runtime Module

`ESCommandModule` belongs to `RuntimeDomain`.
It drives:

```text
ESCommandPlayerRunner.TickAll(time, deltaTime)
```

`ESCommandPlayer` has no `Update`.

## Template Classes

Domain templates:

```text
ESGameDomain<TModule>
ESRuntimeDomain
ESWorldDomain
ESPlayerDomain
ESPresentationDomain
```

Module templates:

```text
ESGameModule<TDomain>
ESRuntimeModule
ESWorldModule
ESPlayerModule
ESPresentationModule
```

New modules should inherit the semantic module parent:

```csharp
public sealed class MySaveModule : ESRuntimeModule
{
}

public sealed class MyPlayerCombatModule : ESPlayerModule
{
}
```

The template parents expose short accessors:

```text
Game
Runtime
World
Player
Presentation
GetModule<T>()
```
