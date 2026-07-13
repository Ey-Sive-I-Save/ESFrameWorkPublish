# ES Luban Config

This folder is the source config workspace for Luban.

- `Defines/`: schema definitions.
- `Tables/xlsx/`: xlsx authoring templates and future Excel tables.
- `Tables/csv/`: csv authoring tables currently used by the base pipeline.
- `Luban/`: Luban 3.1.0 command line tool copied from `ESFrameWork_Core`.
- `gen-json.bat` / `gen-json.ps1`: generate Newtonsoft.Json based C# config classes and json data.

Generated files are written to:

- `Assets/Plugins/ES/Generated/Luban/CSharp`
- `Assets/Plugins/ES/Generated/Luban/Json`
- `Assets/Plugins/ES/Generated/Luban/Bytes`
- `Assets/Plugins/ES/Generated/Luban/ScriptableObjects`

Rule: generated files can be deleted and recreated. Do not hand-edit generated output.

The first tables reserve the Pack / Group / Info shape:

- `ConfigPackInfo`
- `ConfigGroupInfo`
- `TextInfo`
- `SkillDefinitionInfo`
