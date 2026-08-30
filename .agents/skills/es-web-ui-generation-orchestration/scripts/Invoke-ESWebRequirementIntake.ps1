[CmdletBinding()]
param([Parameter(Mandatory)][string]$PromptText,[string]$OutputPath='ES/Output/WebPageStudio/bootstrap/requirement-intake.json')
$ErrorActionPreference='Stop'
$root=(Resolve-Path '.').Path
$full=[IO.Path]::GetFullPath((Join-Path $root $OutputPath))
if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath must remain under project root.'}
New-Item -ItemType Directory -Force (Split-Path $full)|Out-Null
$bytes=[Text.UTF8Encoding]::new($false).GetBytes($PromptText)
$hash=[Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
$hex=(-join($hash|%{$_.ToString('x2')}))
$result=[ordered]@{schemaVersion=1;recordType='ESWebRequirementIntakeReceipt';stageId='requirement-intake';status='accepted';input=[ordered]@{rawPrompt=$PromptText;inputHash=$hex;receivedUtc=[DateTime]::UtcNow.ToString('o')};aiAnalysis='Requirement received; scope and authorization boundaries are separated from later stages.';execution='Persist raw prompt and input hash only; no downstream resource or runtime invocation.';decision=[ordered]@{objective='complete requirement intake';allowedScope=@('create requirement intake receipt');forbiddenScope=@('FocusContext','TaskContext','Knowledge','SubAgent execution','ABCD execution','page generation','network','Unity','Git','release');unknowns=@('business objective not yet provided','acceptance signals not yet provided')};returnReceipt=[ordered]@{status='accepted';receiptPath=$OutputPath;inputHash=$hex};requiredNextStage='task-focus-lock';nonClaims=@('does not prove requirement understanding','does not create FocusContext or TaskContext','does not execute page or runtime capability')}
[IO.File]::WriteAllText($full,($result|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json -Depth 10
