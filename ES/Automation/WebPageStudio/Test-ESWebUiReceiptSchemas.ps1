[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $ProjectRoot 'ES\Automation\Contracts\ESJsonSchemaLite.psm1') -Force
$fixtureRoot=Join-Path $ProjectRoot 'ES\Automation\WebPageStudio\fixtures';$contractRoot=Join-Path $ProjectRoot 'ES\Automation\Contracts'
$cases=@(
 @{name='network';schema='es-web-network-runtime-receipt-v1.schema.json';positive='network-receipt.valid.json';negative='network-receipt.cancel-invalid.json'},
 @{name='preview';schema='es-web-preview-runtime-receipt-v1.schema.json';positive='preview-receipt.valid.json';negative='preview-receipt.drift-invalid.json'},
 @{name='visual';schema='es-web-visual-regression-receipt-v1.schema.json';positive='visual-receipt.valid.json';negative='visual-receipt.diff-invalid.json'},
 @{name='release';schema='es-web-release-acceptance-receipt-v1.schema.json';positive='release-receipt.valid.json';negative='release-receipt.rollback-invalid.json'}
)
$results=@()
foreach($case in $cases){$schema=Join-Path $contractRoot $case.schema;$pos=Get-Content -Raw -Encoding UTF8 (Join-Path $fixtureRoot $case.positive)|ConvertFrom-Json;$neg=Get-Content -Raw -Encoding UTF8 (Join-Path $fixtureRoot $case.negative)|ConvertFrom-Json;$pe=@(Test-ESJsonSchemaValue -SchemaPath $schema -Value $pos);$ne=@(Test-ESJsonSchemaValue -SchemaPath $schema -Value $neg);$unknown=$pos|ConvertTo-Json -Depth 20|ConvertFrom-Json;$unknown|Add-Member -NotePropertyName unexpectedField -NotePropertyValue 'drift';$ue=@(Test-ESJsonSchemaValue -SchemaPath $schema -Value $unknown);$missing=$pos|ConvertTo-Json -Depth 20|ConvertFrom-Json;$missing.PSObject.Properties.Remove('receiptId');$me=@(Test-ESJsonSchemaValue -SchemaPath $schema -Value $missing);$results += [pscustomobject]@{name=$case.name;positiveErrors=$pe.Count;negativeSchemaErrors=$ne.Count;unknownFieldErrors=$ue.Count;missingRequiredErrors=$me.Count;negativeFixturePresent=($null -ne $neg);passed=($pe.Count -eq 0 -and $ue.Count -gt 0 -and $me.Count -gt 0)}}
$failed=@($results|Where-Object{-not $_.passed});[ordered]@{validator='web-ui-receipt-schemas';status=if($failed.Count){'failed'}else{'passed'};schemaCount=$results.Count;results=$results;runtimeStatus='runtime-not-run';nonClaims=@('schema-static-validation','does-not-prove-runtime-or-release')}|ConvertTo-Json -Depth 8;if($failed.Count){exit 1}
