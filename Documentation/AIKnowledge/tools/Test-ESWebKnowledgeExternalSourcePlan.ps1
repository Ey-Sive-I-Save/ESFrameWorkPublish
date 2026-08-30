[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[string]$PlanPath='Documentation/AIKnowledge/WebKnowledgeExternalSourcePlan.yaml',[switch]$Json)
$ErrorActionPreference='Stop'; $root=(Resolve-Path -LiteralPath $ProjectRoot).Path; $findings=[Collections.Generic.List[object]]::new()
function Add-F([string]$c,[string]$m){$findings.Add([pscustomobject]@{code=$c;message=$m})}
$p=Join-Path $root $PlanPath
if(-not(Test-Path -LiteralPath $p -PathType Leaf)){Add-F 'MISSING_PLAN' $PlanPath}else{
  try{$t=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($p))}catch{Add-F 'INVALID_UTF8' $_.Exception.Message; $t=''}
  if($t -notmatch '(?m)^status:\s+(deferred-awaiting-explicit-network-authorization|calibrated-static-awaiting-runtime-framework-validation)'){Add-F 'BOUNDARY_MISSING' 'status: deferred or calibrated-static'}
  foreach($required in @('authorizationRequired: true','authority: official-documentation-only','totalMaxPages: 40','timeoutMinutes: 15','stopOnLicenseAmbiguity: true','stopOnDomainMismatch: true','stopOnVersionAmbiguity: true')){if($t -notmatch [regex]::Escape($required)){Add-F 'BOUNDARY_MISSING' $required}}
  if($t -notmatch '(?m)^network:\s+(disabled-by-default|disabled-after-bounded-snapshot)'){Add-F 'BOUNDARY_MISSING' 'network disabled state'}
  $domains=[regex]::Matches($t,'(?m)^    domains:\s*\[([^\]]+)\]')|ForEach-Object{$_.Groups[1].Value -split ','|ForEach-Object{$_.Trim()}}
  $allowed=@('developer.mozilla.org','www.w3.org','web.dev','developers.google.com','docs.astro.build','nextjs.org','nuxt.com','vite.dev')
  foreach($d in $domains){if($d -notin $allowed){Add-F 'DOMAIN_NOT_ALLOWED' $d}}
  if($domains.Count -ne 8){Add-F 'DOMAIN_COUNT' "expected=8 actual=$($domains.Count)"}
}
$status=if($findings.Count -eq 0){'passed'}else{'blocked'}; $r=[ordered]@{validator='es-web-knowledge-external-source-plan';status=$status;staticStatus=if($status -eq 'passed'){'static-passed'}else{'static-blocked'};findingCount=$findings.Count;findings=@($findings);runtimeStatus='runtime-not-run';networkExecuted=$false}
if($Json){$r|ConvertTo-Json -Depth 6}else{$r|Format-List}; if($findings.Count -gt 0){exit 1}
