# 游戏 UI 视觉输入与测量来源快照

`KnowledgeId`: `es.project.game-ui-vision-input-source-snapshot.v1`
`Authority`: `Pinned external vision/design-source snapshots for UI reference ingestion`
`RouteKeys`: `ui-automation`, `ui-reference-measurement`, `ui-vision-input`, `design-ir`, `source-map`, `visual-diagnosis`
`HashSchema`: `v2`
`ContentHash`: `c607207e65f8f322e63cb2e0526c1a97190f1c1cd1cfd75461568a0458b59afa`
`SourceSetHash`: `c607207e65f8f322e63cb2e0526c1a97190f1c1cd1cfd75461568a0458b59afa`
`EntryBodyHash`: `71f93274531ce40e87c78e609404cff7f8457983e7f2c5b7f3771b82c5942481`
`EvidenceLevel`: `S0`
`StaleWhen`: pinned repository commit, model/API version, project reference-ingestion executor, ScreenSpec/LayoutPlan/Materializer contract or any SourceRef hash changes.

本文件只保存外部方案对“截图解析、结构化设计输入和运行时视觉观察”的最小校准信息。
它不拥有 ES 的 ScreenSpec、LayoutPlan、AssetManifest 或 Unity 运行证据，也不把第三方
仓库的 README 声明升级为当前项目能力。

`RetrievedAtUtc`: `2026-08-25T18:02:06.8479823Z`
`Retrieval`: GitHub API/raw content at pinned commits; bounded README lookup
`Scope`: screenshot element detection, design-source IR, live observation and visual diagnosis.

## Pinned sources

| Repository | Commit | License signal | Locked lesson |
|---|---|---|---|
| `microsoft/OmniParser` | `354021201345a96178360b28733573e27269f2de` | MIT for the released detector/caption components; verify each model | Screenshot parsing can detect interactive regions and caption icons, but it grounds actions; it does not recover Unity parent constraints or business semantics. |
| `itsnik-scrpt/FigmaBridge` | `e84b933460e2a1da5276859faed4c25c4bb60793` | MIT | Figma Auto Layout AST -> versioned IR, asset hash cache, JSON Patch and state-preserving hot reload are stronger inputs than screenshot guessing. |
| `kevinkicho/agent-vision-unity` | `abc1fe4b89bc2a0844448fde729f07ca0d4e040a` | repository license must be checked before reuse | Screenshot + JSON state + webhook loop is useful for observation and pixel diagnosis, but the project explicitly labels itself work in progress and not production-ready. |

| Repository path | Raw URL | SHA-256 |
|---|---|---|
| `microsoft/OmniParser/README.md` | `https://raw.githubusercontent.com/microsoft/OmniParser/354021201345a96178360b28733573e27269f2de/README.md` | `f4ebaafcc0ab0b9e9cc55cb587678e5c83a5bbd64469fb2cc52bc63843c5cc6d` |
| `itsnik-scrpt/FigmaBridge/README.md` | `https://raw.githubusercontent.com/itsnik-scrpt/FigmaBridge/e84b933460e2a1da5276859faed4c25c4bb60793/README.md` | `1824eda3140b72e8ab09d9b919167c75819109580c4e02dbc1a28b9f3827b663` |
| `kevinkicho/agent-vision-unity/README.md` | `https://raw.githubusercontent.com/kevinkicho/agent-vision-unity/abc1fe4b89bc2a0844448fde729f07ca0d4e040a/README.md` | `bafe86e8e0416b217051fbef5c3a3c172f7d3a8a4f37adfda6abbfcfc403cd48` |

## Locked observations

1. A screenshot parser produces candidate boxes, masks, captions and interaction likelihoods. It
   cannot determine whether a region is anchored, stretched, driven by a LayoutGroup or owned by a
   safe-area container without additional design or runtime evidence.
2. A design-source IR should preserve stable node IDs, hierarchy, local/global geometry, constraints,
   typography, variants, assets and source-map links. The IR is an input contract, not Unity write
   authorization.
3. A live observation bridge should pair every frame with a state identity and input/event record.
   Pixel diagnosis can detect blank, pink or overexposed output, but cannot prove player usability or
   domain correctness.
4. Hot reload must preserve focus, text values, scroll offsets and event registrations when a node ID
   remains matched; recreating the entire tree destroys those runtime facts.
5. Third-party model weights, generated images and downloaded assets need an independent license and
   provenance check before entering an ES AssetManifest.

## ES adaptation boundary

| External pattern | ES target | Current status |
|---|---|---|
| detector/mask/OCR candidate regions | `Reference Ingestor` + `ReferenceDesignEvidence` | protocol/knowledge only; no detector executor |
| design AST / IR / source map | `ScreenSpec` + `LayoutPlan` + source-map extension | ScreenSpec/Adapter exists; no general IR importer |
| asset hash cache | `AssetManifest` resolver | manifest contract exists; resolver is missing |
| visual frame + JSON state | `Fixture Driver` + Evidence Ledger | deterministic fixture/static capture exists; live observer is missing |
| visual diff / repair suggestion | `Visual Evaluator` + `Repair Planner` | no automatic scoring or repair loop |

## Evidence boundary

This snapshot proves only the pinned public README bytes and the derived architecture notes above.
It does not prove third-party installation, model quality, license clearance for a specific asset,
Unity import, UGUI layout recovery, PlayMode, GPU capture, input behavior, performance, Player,
IL2CPP or release acceptance. Those claims remain `runtime-not-run` unless a current project receipt
proves them.

## StaleWhen

Any pinned commit, license status, model/API version, upstream protocol, project ScreenSpec/Materializer/
Fixture contract or source hash changes.

## SourceRefs

- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md` (`f276bff04711fab6e8d6713079c9854acfaad653e2f39ac5a5cae00cc1329344`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
