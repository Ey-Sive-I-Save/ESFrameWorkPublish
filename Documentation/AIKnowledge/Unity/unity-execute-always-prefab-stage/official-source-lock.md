# ExecuteAlways and Prefab Stage official source lock

Captured UTC: `2026-08-23T16:16:48.2794115Z`.

Unity version: `2022.3.45f1` (`a13dfa44d684`).

This file locks the authoritative inputs used by
`es.unity.execute-always-prefab-stage.v1`. It is evidence for static source
closure only and does not claim that Unity Editor, Prefab Mode, Domain Reload,
Undo, Save, or Play Mode was executed.

## Unity 2022.3 official pages

| Source | HTTP | UTF-8 response SHA-256 |
|---|---:|---|
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ExecuteAlways.html | 200 | `5a343cf69e3afe15e89eac7ac8589a740f1ae1481bd0f9c247be05b63d73eede` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application.IsPlaying.html | 200 | `238f1a5927dd2cb125bdd166a475e4bda7d892363f5944025511bacf4b5e3b31` |
| https://docs.unity3d.com/2022.3/Documentation/Manual/EditingInPrefabMode.html | 200 | `83611457ff7f00fd79c505ce63a0df3501f141896d1c266ef9a5bb0dbc07ca0d` |
| https://docs.unity.cn/2022.3/Documentation/Manual/EditingInPrefabMode.html | 200 | `875b50b5354271f3c31c94d890b0a41e6b33da36b938c556aff4b9187756f071` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.PrefabStage.html | 200 | `48869540f2d99a74ae196ee89d962fe2e867c9b2d4b71df2d65e2f93109ea369` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.PrefabStageUtility.GetPrefabStage.html | 200 | `a0d71013aeac6610264a66a9413e86d3af284c66a0fb3870f9d52068dcd85358` |

## Installed Unity API documentation

| Source | SHA-256 |
|---|---|
| `D:/UnityEdi/2022.3.45f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml` | `ce120ca131e9d371794fa1d453bdd97d8ed3d39dee97c40f8467f7cac2b1bbce` |
| `D:/UnityEdi/2022.3.45f1/Editor/Data/Managed/UnityEditor.xml` | `85c417af947171f08ec139c80231e896102d3ea09f086b1b91bf2d0b20e60020` |

Verified excerpts:

- `UnityEngine.ExecuteAlways`: "Makes instances of a script always execute,
  both as part of Play Mode and when editing."
- `UnityEngine.Application.IsPlaying(UnityEngine.Object)`: "Returns true if
  the given object is part of the playing world either in any kind of built
  Player or in Play Mode."
- `UnityEditor.SceneManagement.PrefabStage`: "represents an editing context
  for Prefab Assets."
- `PrefabStage.mode`: a Prefab Stage can be opened in isolation or in context.
- `PrefabStageUtility.GetCurrentPrefabStage`: returns the current Prefab Stage,
  or null if there is none.
- `PrefabStageUtility.GetPrefabStage(GameObject)`: returns the Prefab Stage
  containing the supplied GameObject.
- `PrefabStage.IsPartOfPrefabContents(GameObject)`: identifies whether a
  GameObject is part of the loaded Prefab Asset contents.
- The official `ExecuteAlways` page requires scripts to avoid Play logic that
  modifies an object while it is in Edit Mode or outside the playing world; it
  specifically warns that a Prefab being edited in Prefab Mode can otherwise
  be modified and saved by game logic.
- The same page states that, outside the playing world, `Update` is called only
  when something in the Scene changes rather than continuously.
- The Prefab Mode manual states that changes made in Prefab Mode affect all
  instances of that Prefab. Its Auto Save option is enabled by default and
  automatically saves changes to the Prefab Asset.
- The same manual states that edits to a Prefab Asset can be undone only while
  still in Prefab Mode. After exiting Prefab Mode for that Prefab Asset, those
  edits are no longer available in the undo history.

## Evidence boundary

The response hashes prove which official pages were read on 2026-08-24. The
installed XML excerpts prove the local 2022.3.45f1 API descriptions. Neither
proves the behavior of any ESFramework component or asset in a running Unity
Editor.
