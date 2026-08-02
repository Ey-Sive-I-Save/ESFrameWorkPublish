$ErrorActionPreference = "Stop"

$workspace = Resolve-Path (Join-Path $PSScriptRoot "../../..")
$lubanDll = Join-Path $PSScriptRoot "Luban/Luban.dll"
$confRoot = $PSScriptRoot
$codeDir = Join-Path $workspace "Assets/Plugins/ES/Generated/Luban/CSharp"
$dataDir = Join-Path $workspace "Assets/Plugins/ES/Generated/Luban/Json"

dotnet $lubanDll `
    -t all `
    -d json `
    -c cs-newtonsoft-json `
    --conf (Join-Path $confRoot "luban.conf") `
    -x outputCodeDir=$codeDir `
    -x outputDataDir=$dataDir
