# ESResMaster Legacy Download Archive

`ResMaster.Runtime.Download.legacy.cs.txt` is the retired v4-style downloader.
It consumed `GameIdentity`, `LibraryIdentity`, `AssetKeys`, and `ABMetadata`
directly, owned its own retry/coroutine state, and injected the old
`GlobalAssetKeys` / `GlobalABKeys` maps.

The active runtime path is `ESRuntimeReleaseDownloader` plus the Provider
pipeline. This archive is intentionally stored as `.cs.txt`, so it cannot be
compiled or reached by new code. Restore it only as a dedicated migration,
never by adding a call from the current bootstrap or ResourcePlan flow.

The `ResMaster.Temp` and `TempESAssetLibrary` archives are unused v4 build
scratch state. `ResMaster.AnlyzeFromJson` only exposed an unused `ABNames`
field. They are retained here for source history, not as runtime compatibility.
