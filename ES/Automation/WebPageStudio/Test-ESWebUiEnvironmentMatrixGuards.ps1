[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
$lock=Get-Content -Raw -Encoding UTF8 (Join-Path $ProjectRoot 'ES/Automation/WebPageStudio/browser-environment.lock.json')|ConvertFrom-Json
$matrix=Get-Content -Raw -Encoding UTF8 (Join-Path $ProjectRoot 'ES/Automation/WebPageStudio/ui-validation-matrix.yaml')
$lockPath=Join-Path $ProjectRoot 'ES/Automation/WebPageStudio/browser-environment.lock.json';$lockFileHash=(Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant();$lockHashOk=($lockFileHash -match '^[a-f0-9]{64}$')
$matrixPath=Join-Path $ProjectRoot 'ES/Automation/WebPageStudio/ui-validation-matrix.yaml';$matrixFileHash=(Get-FileHash -LiteralPath $matrixPath -Algorithm SHA256).Hash.ToLowerInvariant();$matrixHashOk=($matrixFileHash -match '^[a-f0-9]{64}$')
$lockOk=([string]$lock.network -eq 'disabled' -and [string]$lock.locale -eq 'zh-CN' -and [string]$lock.timezone -eq 'Asia/Shanghai' -and [string]$lock.engine -eq 'chromium' -and [string]$lock.gpu -like 'disabled*')
$matrixOk=($matrix -match '(?m)^\s*- profileId: desktop' -and $matrix -match '(?m)^\s*- profileId: tablet' -and $matrix -match '(?m)^\s*- profileId: mobile' -and $matrix -match '(?m)^themes:\s*\[.*light.*dark')
$driftLock=$lock|ConvertTo-Json -Depth 20|ConvertFrom-Json;$driftLock.network='enabled';$lockDriftDetected=([string]$driftLock.network -ne 'disabled')
$driftMatrix=$matrix -replace 'dark','contrast-drift';$matrixDriftDetected=($driftMatrix -notmatch '(?m)^themes:\s*\[.*light.*dark')
$ok=$lockOk -and $lockHashOk -and $matrixOk -and $matrixHashOk -and $lockDriftDetected -and $matrixDriftDetected
[ordered]@{validator='web-ui-environment-matrix-guards';status=if($ok){'passed'}else{'failed'};lockStaticCheck=$lockOk;lockFileHashValid=$lockHashOk;matrixStaticCheck=$matrixOk;matrixFileHashValid=$matrixHashOk;lockDriftDetected=$lockDriftDetected;matrixDriftDetected=$matrixDriftDetected;runtimeStatus='runtime-not-run';nonClaims=@('configuration-static-check','no-browser-or-visual-runtime')}|ConvertTo-Json -Depth 8
if(-not $ok){exit 1}
