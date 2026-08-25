[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$ContractPath,
  [ValidateSet('positive','invalid-input','denied-expansion','repeat-idempotency','interruption-recovery')][string]$Case='positive',
  [string]$OutputPath
)
$ErrorActionPreference='Stop'
$resolved=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ContractPath).Path)
$errors=[System.Collections.Generic.List[string]]::new()
try {$raw=[IO.File]::ReadAllText($resolved,[Text.UTF8Encoding]::new($false,$true)); $c=$raw|ConvertFrom-Json} catch {throw "invalid-json:$($_.Exception.Message)"}
$required=@('contractVersion','objective','scope','mustPreserve','allowedTransitions','forbiddenTransitions','acceptanceSignals','counterexamples','nonGoals','assumptions','unresolvedQuestions','intentAlignmentStatus','executionDecision','revision','sourceRefs')
foreach($n in $required){if($null -eq $c.PSObject.Properties[$n]){$errors.Add("missing:$n")}}
if([string]$c.contractVersion -ne '1'){$errors.Add('contractVersion-not-1')}
if([int]$c.revision -lt 1){$errors.Add('revision-invalid')}
if(@($c.mustPreserve).Count -lt 1){$errors.Add('mustPreserve-empty')}
if(@($c.acceptanceSignals).Count -lt 1){$errors.Add('acceptanceSignals-empty')}
if(@($c.counterexamples).Count -lt 1){$errors.Add('counterexamples-empty')}
if(@($c.nonGoals).Count -lt 1){$errors.Add('nonGoals-empty')}
$map=@{aligned='allow';partial='analyze-only';unverifiable='analyze-only';misaligned='deny'}
$status=[string]$c.intentAlignmentStatus; $decision=[string]$c.executionDecision
if(!$map.ContainsKey($status)){$errors.Add('intentAlignmentStatus-invalid')} elseif($map[$status] -ne $decision){$errors.Add('executionDecision-mismatch')}
foreach($name in @('mustPreserve','allowedTransitions','forbiddenTransitions','acceptanceSignals','counterexamples','nonGoals','assumptions','unresolvedQuestions','sourceRefs')){ $vals=@($c.$name); if(($vals|Sort-Object -Unique).Count -ne $vals.Count){$errors.Add("duplicate:$name")} }
if($Case -eq 'denied-expansion' -and $decision -eq 'allow'){$errors.Add('denied-expansion-allowed')}
if($Case -eq 'interruption-recovery' -and $null -eq $c.PSObject.Properties['recovery']){$errors.Add('recovery-missing')}
if($Case -eq 'repeat-idempotency'){
  $second=$raw|ConvertFrom-Json|ConvertTo-Json -Depth 20 -Compress
  $first=$c|ConvertTo-Json -Depth 20 -Compress
  if($first -ne $second){$errors.Add('repeat-output-drift')}
}
$hash=([Security.Cryptography.SHA256]::Create()).ComputeHash([Text.Encoding]::UTF8.GetBytes(($c|ConvertTo-Json -Depth 20 -Compress)))
$sha=([BitConverter]::ToString($hash)).Replace('-','').ToLowerInvariant()
$result=[ordered]@{schemaVersion=1;validator='Test-ESIntentContract';case=$Case;status=($(if($errors.Count){'failed'}else{'passed'}));contractPath=$resolved;contractRevision=[int]$c.revision;intentAlignmentStatus=$status;executionDecision=$decision;normalizedSha256=$sha;findings=@($errors);runtimeStatus='not-applicable';claimsNotProven=@('Semantic correctness beyond declared contract','Runtime behavior')}
$json=$result|ConvertTo-Json -Depth 8
if($OutputPath){$out=[IO.Path]::GetFullPath($OutputPath);[IO.File]::WriteAllText($out,$json,[Text.UTF8Encoding]::new($false))}
$json
if($errors.Count){exit 1}
