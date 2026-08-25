# Unity 官方 Agent Skills UI 来源快照

本文件是 Unity 官方 `Unity-Technologies/skills` 仓库中 UI 相关 Skill 的有界来源快照，
只保存本项目防错适配层需要的合同摘要，不复制完整仓库，也不把 Skill 文本当作 Unity
运行时证据。

## Provenance

- `repository`: `https://github.com/Unity-Technologies/skills`
- `commit`: `87fac23d66a1f44f5e06c2935eccce0b40b9715a`
- `retrievedAtUtc`: `2026-08-24T12:58:10Z`
- `retrieval`: raw GitHub content at the pinned commit; HTTP 200 during bounded lookup
- `scope`: `README.md`, `skills/ui/SKILL.md`, `skills/ui-ugui/SKILL.md`, `skills/ui-uitk/SKILL.md`, `skills/ui-imgui/SKILL.md`, and the referenced UI setup guides listed below

## Source hashes

SHA-256 is computed over the UTF-8 response body returned from the pinned raw URL.

| Path | Raw URL | SHA-256 |
|---|---|---|
| `README.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/README.md` | `91f4032de7266606af3e6b3d730f6ceaaac52e013f1606bc9df49604a9a772af` |
| `skills/ui/SKILL.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui/SKILL.md` | `e6a72162eb07c3c74e126a4a330fcf829fcdc9dca35c2421e8fd9b75785fa902` |
| `skills/ui-ugui/SKILL.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-ugui/SKILL.md` | `7d139724f86bcbf9cb0b7ff514199eea60564907e7eff6bfce0cec3db3f0ba2d` |
| `skills/ui-uitk/SKILL.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-uitk/SKILL.md` | `439f63fead3fe7a388659a72a116a9b86e693bea6538879c2e450244bf79adf5` |
| `skills/ui-imgui/SKILL.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-imgui/SKILL.md` | `15b5f5dc5b2a0fe3873f16dd5096f4c829366ae580c6cf0d0fec4ddc0619097f` |
| `skills/ui-ugui/references/scrollview-setup.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-ugui/references/scrollview-setup.md` | `41d6a37cb5e78b632baea633583129e27b3bed353904ab6425cc954944873eaf` |
| `skills/ui-uitk/references/common-issues.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-uitk/references/common-issues.md` | `76a9a47dc56f675382d94ef9cfcd9b89546a8bfe6a53d594770368b75656b8d0` |
| `skills/ui-uitk/references/uss-guide.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-uitk/references/uss-guide.md` | `d50efbc200c78b5317e14e0ad9ed0286cd0096869d57c6310e3aefebcac0bd8f` |
| `skills/ui-uitk/references/ui-runtime-binding.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-uitk/references/ui-runtime-binding.md` | `81c0f5a1dd2261330fb738ec9b0859df6d72bd8382fd70b877087122bcada727` |
| `skills/ui-uitk/references/custom-elements.md` | `https://raw.githubusercontent.com/Unity-Technologies/skills/87fac23d66a1f44f5e06c2935eccce0b40b9715a/skills/ui-uitk/references/custom-elements.md` | `f418e2cf17775f0f83382ff4d7496608c2d05e2ec7de120918c96da4353d4999` |

## Locked facts used by the ES adapter

### System routing

- The official `ui` Skill routes by concrete evidence: `.uxml`/`.uss`/`UIDocument`/`CreateGUI`
  to UI Toolkit, Canvas/RectTransform/Canvas Prefab to uGUI, and `OnGUI`/`OnInspectorGUI` to
  IMGUI.
- Existing projects should follow the system already in use. New runtime UI without a preference
  defaults to uGUI; older Unity versions bias toward uGUI.
- The official UI Skill does not provide client-side Figma import. A Figma request must be treated
  as a reference-image input that still needs a separate ScreenSpec/layout/materialization workflow.

### uGUI authoring traps

- A Canvas needs a CanvasScaler and GraphicRaycaster for the normal screen-space setup; an
  interactive scene also needs exactly one EventSystem and a compatible input module.
- Parent LayoutGroups own child sizing when Control Child Size is enabled. Child anchors and
  sizeDelta must not be treated as an independent second layout owner.
- ContentSizeFitter on the same object as a controlling LayoutGroup can conflict; in a ScrollView,
  the required ScrollRect/Viewport/Content hierarchy and Mask/Image relationship must be explicit.
- Anchor preset, pivot, then position/offset is the safe authoring order. Zero-size, out-of-bounds,
  obscured, or alpha-zero graphics are rendering failures even when the hierarchy exists.
- The Skill distinguishes visual hierarchy generation from scripts: scripts are added only when
  the user requests code, logic, or functional behavior. “Proper buttons” and “working UI” do not
  prove a business callback or runtime system exists.

### UI Toolkit authoring traps

- The official `ui-uitk` Skill targets Unity 6.0+ and uses UXML/USS with flex layout; it is not a
  drop-in replacement for a Unity 2022.3 uGUI project.
- PanelSettings is required for a runtime UIDocument to render. UXML must link USS, have one top
  level container, and must not use inline `style` attributes.
- USS is a restricted subset: unsupported CSS properties such as `gap`, `z-index`, `border`
  shorthand, `pointer-events`, and CSS gradients must not be emitted as if full CSS were supported.
- The official Skill states that external validation cannot prove UXML/USS import or rendering;
  Unity Editor reimport and Console/runtime evidence remain required.
- UI Toolkit data binding must expose properties for binding and should use `nameof()` when
  building `PropertyPath`; string paths, binding mode, datasource ownership, and UI reload
  lifecycle are separate inputs. A binding declaration is not proof that a business data source
  exists.
- Unity 6 custom UXML elements require `[UxmlElement]`, a `partial` class, and namespace-only
  UXML declarations. Assembly names or class names in the namespace declaration are invalid.
- `PanelRenderer` binding reload callbacks are version-gated (Unity 6.6+ in the referenced guide);
  they must not be copied into a project whose Unity version does not provide that API.

## Evidence boundary

These facts prove only the content of the pinned official Skill files. They do not prove that this
project uses UI Toolkit, that any generated UI imported, that a Canvas or PanelSettings is assigned,
that input callbacks work, or that a screenshot is visually acceptable. Re-read the pinned commit
and recompute these hashes when the official repository revision changes.

## StaleWhen

The repository commit, any listed raw response hash, Unity major/minor version, project UI system,
UGUI/Input System package, UI Toolkit package, ScreenSpec/Materializer contract, or official Skill
routing text changes.
