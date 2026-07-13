$ErrorActionPreference = "Stop"

$workspace = Resolve-Path (Join-Path $PSScriptRoot "..")
$lubanDll = Join-Path $workspace "LubanConfig/Luban/Luban.dll"
$confRoot = Join-Path $workspace "LubanConfig"
$codeDir = Join-Path $workspace "Assets/Plugins/ES/Generated/Luban/CSharp"
$dataDir = Join-Path $workspace "Assets/Plugins/ES/Generated/Luban/Json"

dotnet $lubanDll `
    -t all `
    -d json `
    -c cs-newtonsoft-json `
    --conf (Join-Path $confRoot "luban.conf") `
    -x outputCodeDir=$codeDir `
    -x outputDataDir=$dataDir
