# Unity rendering and batching official source lock

Captured on `2026-08-24` for the ESFramework rendering Knowledge domain. The project
version remains `2022.3.45f1`; unversioned Unity pages below are current documentation
observations and therefore raise a compatibility risk, not a project-version Runtime fact.

Each response hash is SHA-256 over the UTF-8 bytes of the HTML response text returned by
the official endpoint. A changed response, Unity version, URP version, or UGUI version makes
the dependent interpretation stale and requires a fresh review.

## Online documentation

| URL | HTTP | Response SHA-256 | Bounded statement |
|---|---:|---|---|
| https://docs.unity3d.com/ScriptReference/MaterialPropertyBlock.html | 200 | `613f0547539206c875fe64bbf7822d654de3aeefdf6066373957bfd3b6c270a2` | MPB applies values through `Graphics.RenderMesh` or `Renderer.SetPropertyBlock`; it cannot change render state. The current page warns that MPB is incompatible with SRP Batcher and may reduce performance in SRP-based pipelines. |
| https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Graphic.html | 200 | `0dcfb11f360c5c0d7449b095097d71c889675fdee362a9c56d7e27ce1b4437c5` | `materialForRendering` is the material actually sent to `CanvasRenderer` and may differ from `Graphic.material`. |
| https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.RectMask2D.html | 200 | `6a9272723b090654b8407f9353ab1595e68fa76534e47ea5a83e22582c2653ec` | `RectMask2D.PerformClipping` culls or clips child elements during the Canvas update loop. This does not by itself prove a Stencil material path or a batch result. |
| https://docs.unity3d.com/Manual/FrameDebugger.html | 200 | `b72073d3a389b1a790a5bb9fee34d0029b8fa17eb52ead1368268311029e830f` | Frame Debugger exposes the ordered rendering events and graphical state for a selected frame; it does not by itself provide a target-device performance result. |
| https://docs.unity3d.com/Manual/Profiler.html | 200 | `302a8d13a844c41da1894c27fedc680a5e4db633ae64ea5d64e23f08402a30cc` | Profiler can connect to the intended release platform; Editor profiling is an overview and must not be promoted to target Player evidence. |

## Installed package corroboration

- `com.unity.render-pipelines.universal@14.0.11/Documentation~/renderer-feature-decal.md`
  states that URP decals do not support SRP Batcher because they use material property
  blocks. This is a concrete URP 14 example, not proof that every ES Renderer path has the
  same measured cost.
- `com.unity.render-pipelines.universal@14.0.11/Documentation~/shaders-in-universalrp.md`
  requires material properties to be declared in one `UnityPerMaterial` CBUFFER for Shader
  SRP Batcher compatibility.

## Locked decision

- MPB remains a valid Renderer mechanism for per-object property ownership and avoiding
  accidental `renderer.material` instances.
- In URP, that ownership benefit must not be translated into an unconditional batching or
  performance claim. When SRP Batcher matters, compare at least MPB, controlled material
  instances or variants, and any applicable instancing path using the same scene and input.
- UGUI does not inherit the Renderer MPB decision. Inspect `materialForRendering`,
  `CanvasRenderer`, Mask or RectMask2D behavior, and runtime Material ownership separately.
- Static documentation cannot choose the fastest path for an ES scene. A conclusion needs
  Frame Debugger plus warmed, steady-state Profiler evidence on the intended platform.

## Evidence boundary

This lock proves only that the listed official responses and installed package documentation
were read and bounded as above. It does not prove ESCompositeShader SRP Batcher compatibility,
actual draw-call count, material lifetime, RectMask2D visual correctness, target-device
performance, or release readiness. Runtime status: `runtime-not-run`.
