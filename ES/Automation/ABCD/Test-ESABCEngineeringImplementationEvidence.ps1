[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $ProjectRoot 'ES/Automation/ABCD/ESABCInnovationRun.psm1') -Force
$relative='ES/Automation/ABCD/ESABCInnovationRun.psm1'
$full=Join-Path $ProjectRoot $relative
$after=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
$before=('a'*64)
$output='ParserErrorCount=0';$outputHash=([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($output))|ForEach-Object ToString x2)-join ''
$base=[ordered]@{status='verified-static';sourceRefs=@($relative);beforeHashes=[pscustomobject]@{$relative=$before};afterHashes=[pscustomobject]@{$relative=$after};changedSymbols=@('Invoke-ESABCEngineeringArchitectureCompetition');verificationCommands=@('PowerShell syntax');verificationResults=@([pscustomobject]@{status='passed';command='PowerShell syntax';exitCode=0;output=$output;outputHash=$outputHash})}
$valid=Test-ESABCEngineeringImplementationEvidence -Architecture ([pscustomobject]@{implementationEvidence=[pscustomobject]$base}) -ProjectRoot $ProjectRoot
$tampered=[ordered]@{};foreach($k in $base.Keys){$tampered[$k]=$base[$k]};$tampered.afterHashes=[pscustomobject]@{$relative=('b'*64)}
$badHash=Test-ESABCEngineeringImplementationEvidence -Architecture ([pscustomobject]@{implementationEvidence=[pscustomobject]$tampered}) -ProjectRoot $ProjectRoot
$missing=[ordered]@{};foreach($k in $base.Keys){$missing[$k]=$base[$k]};$missing.beforeHashes=[pscustomobject]@{}
$badMissing=Test-ESABCEngineeringImplementationEvidence -Architecture ([pscustomobject]@{implementationEvidence=[pscustomobject]$missing}) -ProjectRoot $ProjectRoot
$pass=($valid.status -eq 'passed' -and $badHash.status -eq 'failed' -and @($badHash.missing|Where-Object{$_ -like 'afterHash-mismatch:*'}).Count -eq 1 -and $badMissing.status -eq 'failed' -and @($badMissing.missing|Where-Object{$_ -like 'beforeHash-missing-or-invalid:*'}).Count -eq 1)
$state=if($pass){'passed'}else{'failed'}
[pscustomobject]@{status=$state;valid=$valid.status;tampered=$badHash.status;tamperedReason=$badHash.missing;missingBefore=$badMissing.status;missingBeforeReason=$badMissing.missing}
if(-not $pass){exit 1}
