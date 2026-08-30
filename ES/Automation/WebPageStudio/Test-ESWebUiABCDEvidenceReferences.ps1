[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $ProjectRoot 'ES\Automation\ABCD\ESABCDEvidence.psm1') -Force
$relative = @(
    'ES/Automation/WebPageStudio/fixtures/network-receipt.accepted.synthetic.json',
    'ES/Automation/WebPageStudio/fixtures/preview-receipt.accepted.synthetic.json',
    'ES/Automation/WebPageStudio/fixtures/visual-receipt.accepted.synthetic.json',
    'ES/Automation/WebPageStudio/fixtures/release-receipt.accepted.synthetic.json'
)
$refs = @($relative | ForEach-Object { [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $ProjectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() } })
$validated = @(Assert-ESABCDEvidenceReferences -ProjectRoot $ProjectRoot -References $refs)
$tampered = @($refs | ForEach-Object { [ordered]@{ path = $_.path; sha256 = ('0' * 64) } })
$tamperBlocked = $false
try { Assert-ESABCDEvidenceReferences -ProjectRoot $ProjectRoot -References $tampered | Out-Null } catch { $tamperBlocked = $_.Exception.Message -like '*EVIDENCE_ARTIFACT_HASH_MISMATCH*' }
$ok = $validated.Count -eq 4 -and $tamperBlocked
[ordered]@{validator='web-ui-abcd-evidence-references';status=if($ok){'passed'}else{'failed'};validatedCount=$validated.Count;tamperBlocked=$tamperBlocked;runtimeStatus='runtime-not-run';nonClaims=@('read-only-abcd-check','no-runtime-or-worker-dispatch','absolute-source-paths-not-consumed')}|ConvertTo-Json -Depth 8
if(-not $ok){exit 1}
