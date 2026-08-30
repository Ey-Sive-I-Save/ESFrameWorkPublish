[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
$fixture=Join-Path $ProjectRoot 'ES/Automation/WebPageStudio/fixtures/visual-receipt.valid.json';$matrix=Join-Path $ProjectRoot 'ES/Automation/WebPageStudio/ui-validation-matrix.yaml';$obj=Get-Content -Raw -Encoding UTF8 $fixture|ConvertFrom-Json;$matrixHash=(Get-FileHash -LiteralPath $matrix -Algorithm SHA256).Hash.ToLowerInvariant();$obj.matrix|Add-Member -NotePropertyName matrixHash -NotePropertyValue $matrixHash -Force
$validPath=Join-Path $env:TEMP 'es-web-ui-visual-matrix-valid.json';[IO.File]::WriteAllText($validPath,($obj|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));$validRaw=& (Join-Path $PSScriptRoot 'Test-ESWebVisualRegressionReceipt.ps1') -ReceiptPath $validPath -MatrixPath $matrix 2>$null;$valid=($validRaw -join "`n")|ConvertFrom-Json
$obj.matrix.matrixHash=('0'*64);$driftPath=Join-Path $env:TEMP 'es-web-ui-visual-matrix-drift.json';[IO.File]::WriteAllText($driftPath,($obj|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));$driftRaw=& (Join-Path $PSScriptRoot 'Test-ESWebVisualRegressionReceipt.ps1') -ReceiptPath $driftPath -MatrixPath $matrix 2>$null;$drift=($driftRaw -join "`n")|ConvertFrom-Json
$ok=([string]$valid.status -eq 'passed' -and [string]$drift.status -eq 'blocked' -and @($drift.findings) -contains 'MATRIX_HASH_DRIFT')
[ordered]@{validator='web-ui-visual-matrix-hash';status=if($ok){'passed'}else{'failed'};validBinding=$valid.status;driftStatus=$drift.status;driftFinding=(@($drift.findings)-contains 'MATRIX_HASH_DRIFT');runtimeStatus='runtime-not-run';nonClaims=@('static-matrix-hash-fixture','no-browser-or-pixel-runtime')}|ConvertTo-Json -Depth 8
if(-not $ok){exit 1}
