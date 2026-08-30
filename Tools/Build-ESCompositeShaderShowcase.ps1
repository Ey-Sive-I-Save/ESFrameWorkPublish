[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$RunId = ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$lockPath = Join-Path $root 'Temp/UnityLockfile'
$projectUnity = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match [regex]::Escape($root) }
if ((Test-Path -LiteralPath $lockPath) -or $projectUnity) {
    $pids = @($projectUnity | Select-Object -ExpandProperty ProcessId)
    throw "ES Composite Shader showcase build refused while the project is locked. Unity PIDs: $($pids -join ', ')"
}

$specPath = 'Assets/UI/Contracts/ESCompositeShaderShowcase.screen-spec.v3.json'
$contractPath = '.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md'
$evidenceRoot = "ES/UIEvidence/es-composite-shader-showcase/$RunId"
$resultPath = "$evidenceRoot/batch-materialization-result.json"
$specAbsolute = Join-Path $root ($specPath -replace '/', [IO.Path]::DirectorySeparatorChar)
$contractAbsolute = Join-Path $root ($contractPath -replace '/', [IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $specAbsolute -PathType Leaf)) { throw "Missing ScreenSpec: $specPath" }
if (-not (Test-Path -LiteralPath $contractAbsolute -PathType Leaf)) { throw "Missing materializer contract: $contractPath" }

$specHash = (Get-FileHash -LiteralPath $specAbsolute -Algorithm SHA256).Hash.ToLowerInvariant()
$contractHash = (Get-FileHash -LiteralPath $contractAbsolute -Algorithm SHA256).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Force -Path (Join-Path $root ($evidenceRoot -replace '/', [IO.Path]::DirectorySeparatorChar)) | Out-Null

function Invoke-UnityBatch([string[]]$Arguments) {
    & $UnityPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Unity batch command failed with exit code $LASTEXITCODE" }
}

Invoke-UnityBatch @(
    '-batchmode', '-quit', '-nographics', '-projectPath', $root,
    '-executeMethod', 'ES.Editor.ESUIGameScreenMaterializer.RegenerateFromSpecBatchMode',
    '-esUiSpecPath', $specPath,
    '-esUiContractHash', $contractHash,
    '-esUiSpecHash', $specHash,
    '-esUiEvidenceRoot', $evidenceRoot,
    '-esUiResultPath', $resultPath,
    '-esUiRunId', $RunId,
    '-esUiProfiles', 'wide,narrow',
    '-esUiStates', 'default,selected,disabled,loading,error,long-content',
    '-logFile', 'Logs/ESCompositeShaderShowcase.materialize.log'
)

Invoke-UnityBatch @(
    '-batchmode', '-quit', '-nographics', '-projectPath', $root,
    '-executeMethod', 'ES.TestAssets.Editor.ESCompositeShaderTestAssetsBuilder.CreateOrRefreshAll',
    '-logFile', 'Logs/ESCompositeShaderShowcase.builder.log'
)

$prefab = Join-Path $root 'Assets/UI/Prefabs/Generated/ESCompositeShaderShowcase.prefab'
$fixture = Join-Path $root 'Assets/UI/Scenes/Generated/ESCompositeShaderShowcaseFixture.unity'
$scenes = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/ESTestAssets/CompositeShaders/Generated/Scenes') -Filter '*.unity' -File)
if (-not (Test-Path -LiteralPath $prefab) -or -not (Test-Path -LiteralPath $fixture) -or $scenes.Count -ne 6) {
    throw "Showcase build completed without the expected Prefab, Fixture, and six generated scenes."
}

[pscustomobject]@{
    status = 'completed'
    runId = $RunId
    prefab = 'Assets/UI/Prefabs/Generated/ESCompositeShaderShowcase.prefab'
    fixture = 'Assets/UI/Scenes/Generated/ESCompositeShaderShowcaseFixture.unity'
    sceneCount = $scenes.Count
    evidenceRoot = $evidenceRoot
    specSha256 = $specHash
    contractSha256 = $contractHash
} | ConvertTo-Json | Write-Output
