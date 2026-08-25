# Unity 2022.3 serialization and Prefab identity source snapshot

Captured at `2026-08-23` for ESFramework project version `2022.3.45f1`.

This file is a bounded, normalized evidence snapshot. Each hash is SHA-256 over the UTF-8 bytes of the response text returned by the official endpoint at capture time. The excerpts remove HTML navigation and collapse whitespace; they do not replace the linked authority.

## Official documentation

### Script serialization

- URL: https://docs.unity3d.com/2022.3/Documentation/Manual/script-Serialization.html
- Response text SHA-256: `f7dc82204a5c081b73114149199d07ce0e6304f3f182f995467d3bbf5ee0d0ad`
- Relevant statements:
  - Unity serializes eligible fields rather than C# properties.
  - References to `UnityEngine.Object`-derived objects are supported serialized field values.
  - A custom class is serialized inline by value unless the field uses `[SerializeReference]`; with `[SerializeReference]`, Unity stores a managed reference in the host object's serialized data.

### Asset metadata and GUID continuity

- URL: https://docs.unity3d.com/2022.3/Documentation/Manual/AssetMetadata.html
- Response text SHA-256: `0ccd2894e5be8f988a2ddeffe61374b1af9b6e4505fc834c946b9c660153e467`
- Relevant statements:
  - Unity assigns an asset a unique ID and writes it into the asset's adjacent `.meta` file.
  - Moving or renaming an asset in the Project window moves or renames the `.meta` file with it.
  - Moving an asset outside Unity without its `.meta` file causes Unity to generate a new metadata file; references to the old asset identity are then broken.

### Text serialized object identifiers

- URL: https://docs.unity3d.com/2022.3/Documentation/Manual/FormatDescription.html
- Response text SHA-256: `96f7c80e97e8c21aae73e094db896845c8ca12966b7b415905c70f346617a47f`
- Relevant statements:
  - Unity text serialization uses a custom subset of YAML.
  - Each object in a Scene is written as a separate YAML document.
  - The number following `&` in a document header is an object ID unique within that file; Unity describes its assignment as arbitrary.

### Persistent asset reference pair

- URL: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.TryGetGUIDAndLocalFileIdentifier.html
- Response text SHA-256: `4305fbf00f83448784e0105e66ef6aa623ce58a5cfc6107efb45eb5932837baf`
- Relevant statements:
  - A serialized asset reference points to a GUID and a file ID.
  - The GUID identifies the asset; the file ID is relative to that asset.
  - The `long` local ID overload is required because Prefab local IDs can exceed 32 bits.

### Session-local object handle

- URL: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.GetInstanceID.html
- Response text SHA-256: `20bf7441cffdfeb1d488406de2eb9f65d96ef5c568b961cc31d584378fce3efa`
- Relevant statements:
  - An Instance ID is a handle to an in-memory object instance.
  - It changes between Editor or Player sessions and is not reliable for actions spanning sessions.

### Prefab source correspondence

- URL: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.GetCorrespondingObjectFromSource.html
- Response text SHA-256: `603af987d0a19adaae8bd2a1292010e30832a53859733e61efd302bb043ccb12`
- Relevant statement: `GetCorrespondingObjectFromSource` returns the corresponding Prefab Asset object for an instance object, or `null` when no correspondence can be found.

### Prefab instance overrides

- URL: https://docs.unity3d.com/2022.3/Documentation/Manual/PrefabInstanceOverrides.html
- Response text SHA-256: `6d93c77a80325e1da59fae07a19bb9abe488fdff483ed4143b13441919c48472`
- Relevant statement: an overridden property value on a Prefab instance takes precedence over the value from the Prefab Asset.

### Nested Prefabs

- URL: https://docs.unity3d.com/2022.3/Documentation/Manual/NestedPrefabs.html
- Response text SHA-256: `13be0bfe5039e81f03dabe743166041c4ed30fcaace117fa497ae9b909f7ac55`
- Relevant statement: after a nested Prefab is applied into an outer Prefab Asset, it remains a Prefab instance in its own right and retains its connection to its own Prefab Asset.

## Official C# reference source

### AssetDatabase persistent identifier API

- URL: https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Modules/AssetDatabase/Editor/ScriptBindings/AssetDatabase.bindings.cs
- Response text SHA-256: `4c97a9337284441f0e5f618294cb52bf45ae6041dc7ed8088c698377b2756485`
- Relevant source facts:
  - `TryGetGUIDAndLocalFileIdentifier(Object, out string, out long)` delegates through the object's Instance ID.
  - The obsolete `int` overload explicitly warns that Prefab local IDs can overflow 32 bits.
  - The public `long` overload calls the native `GetGUIDAndLocalIdentifierInFile` boundary.

### PrefabUtility source-object use

- URL: https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Editor/Mono/Prefabs/PrefabUtility.cs
- Response text SHA-256: `be6afebb141b71c6533136c421b06bede4db98179fe4eeb59bd30a75e0b588dd`
- Relevant source fact: Prefab apply paths resolve a corresponding source object before registering Undo and applying changes.

### Managed-reference attribute

- URL: https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Runtime/Export/Serialization/Serialization.cs
- Response text SHA-256: `d04e985282f78fa82781ca21ffcfe69a8a8a75495e5524e0d80813528e5c6724`
- Relevant source fact: `SerializeReference` is a field-only attribute required by native serialization code.

### Field rename migration attribute

- URL: https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Runtime/Export/Serialization/FormerlySerializedAsAttribute.cs
- Response text SHA-256: `32798d432906ab33c84defcd7119e0648eee05d132f09b6e1906cfdb8b66f1a6`
- Relevant source fact: `FormerlySerializedAsAttribute` targets fields, allows multiple previous names, and exposes the stored old name to native serialization code.

## Evidence boundary

This snapshot proves only that the cited Unity 2022.3 documentation and C# reference source were read and hashed at capture time. It does not prove Editor import behavior, Prefab round trips, domain reload, PlayMode, Player, IL2CPP, migration replay, or release acceptance. Runtime status: `runtime-not-run`.
