[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$ManifestPath,
    [Parameter(Mandatory=$true)][string]$ReportPath
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$utf8=New-Object Text.UTF8Encoding($false,$true)
$issues=New-Object 'System.Collections.Generic.List[string]'
$registryPath=Join-Path $root '.agents/skills/es-static-deep-replay/references/specialized-acceptance-registry.json'
$specializedRegistry=$null;if(Test-Path -LiteralPath $registryPath){try{$specializedRegistry=Get-Content $registryPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{[void]$issues.Add('Specialized acceptance registry is invalid JSON')}}
function Resolve-ProjectPath([string]$relative){
    if([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)){throw "Path must be non-empty and project-relative: $relative"}
    $full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$relative.Replace('/','\')))
    $prefix=$root.TrimEnd([char]92,[char]47)+[char]92
    if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes ProjectRoot: $relative"}
    return $full
}
function Read-Strict([string]$path){return [IO.File]::ReadAllText($path,$utf8)}
function Relative([string]$full){return $full.Substring($root.Length+1).Replace('\','/')}
$manifestFull=Resolve-ProjectPath $ManifestPath
$manifest=Get-Content -LiteralPath $manifestFull -Raw -Encoding UTF8|ConvertFrom-Json
foreach($field in @('schemaVersion','skillName','sourceRoots','cases','staticClaims','runtimeClaimsNotProven','runtimeEscalation','caseAssertions','responsibilityProfile','responsibilityChecks','responsibilityScope')){if($null -eq $manifest.PSObject.Properties[$field]){[void]$issues.Add("Manifest missing $field")}}
$required=@('normal-input','invalid-input','denied-expansion','repeat-idempotency','hash-change-cache-invalidation','interruption-recovery','deterministic-output')
$missing=@($required|Where-Object {@($manifest.cases|ForEach-Object {[string]$_}) -notcontains $_})
if($missing.Count -gt 0){[void]$issues.Add('Missing replay cases: '+($missing -join ', '))}
$skillName=[string]$manifest.skillName
if($skillName -notmatch '^es-[a-z0-9]+(?:-[a-z0-9]+)*$'){[void]$issues.Add('skillName is not a valid ES Skill name')}
$skillDir=Resolve-ProjectPath ('.agents/skills/'+$skillName)
if(-not (Test-Path -LiteralPath $skillDir -PathType Container)){[void]$issues.Add('Skill directory missing: '+$skillName)}
$expectedRoot='.agents/skills/'+$skillName
if(@($manifest.sourceRoots|ForEach-Object {[string]$_}) -notcontains $expectedRoot){[void]$issues.Add('sourceRoots must include the Skill root: '+$expectedRoot)}
$assertions=$manifest.caseAssertions
foreach($case in $required){$value=if($assertions){[string]$assertions.$case}else{''};if([string]::IsNullOrWhiteSpace($value)){[void]$issues.Add("caseAssertions missing: $case")}}
$requiredFiles=@('SKILL.md','governance.json','agents/openai.yaml','static-replay.manifest.json','references/static-replay-adapter.md')
foreach($rel in $requiredFiles){$path=Join-Path $skillDir $rel.Replace('/', '\');if(-not(Test-Path -LiteralPath $path -PathType Leaf)){[void]$issues.Add("Required static artifact missing: $expectedRoot/$rel")}}
foreach($rel in @('SKILL.md','governance.json','agents/openai.yaml','references/static-replay-adapter.md')){ $path=Join-Path $skillDir $rel.Replace('/', '\'); if(Test-Path -LiteralPath $path){try{Read-Strict $path|Out-Null}catch{[void]$issues.Add("Invalid UTF-8: $expectedRoot/$rel")}} }
$adapter=Join-Path $skillDir 'references/static-replay-adapter.md'
if(Test-Path $adapter){$adapterText=Read-Strict $adapter;foreach($case in $required){if($adapterText -notmatch [regex]::Escape($case)){[void]$issues.Add("Adapter does not document case: $case")}}}
$allowedChecks=@('authority-routing','permission-boundary','deterministic-replay','evidence-contract','knowledge-boundary','bounded-output','editor-layout-static','lifecycle-boundary','input-boundary','recovery-cache','change-boundary','resource-projection','runtime-escalation','consistency-cache','compatibility-boundary','operation-allowlist','credential-isolation','external-data-boundary')
$profile=[string]$manifest.responsibilityProfile
if($profile -notin @('governance','knowledge','editor','engineering','authoring','testing','session','release','base')){[void]$issues.Add('Unknown responsibilityProfile: '+$profile)}
$customChecks=@($manifest.responsibilityChecks|ForEach-Object {[string]$_})
if($customChecks.Count -eq 0){[void]$issues.Add('responsibilityChecks must not be empty')}
$unknown=@($customChecks|Where-Object {$allowedChecks -notcontains $_});if($unknown.Count -gt 0){[void]$issues.Add('Unknown responsibilityChecks: '+($unknown -join ', '))}
if([string]::IsNullOrWhiteSpace([string]$manifest.responsibilityScope)){[void]$issues.Add('responsibilityScope must explain the custom acceptance scope')}
if($adapterText){if($adapterText -notmatch ('(?im)^Responsibility profile:\s*'+[regex]::Escape($profile)+'\s*$')){[void]$issues.Add('Adapter responsibility profile does not match manifest')};foreach($check in $customChecks){if($adapterText -notmatch [regex]::Escape($check)){[void]$issues.Add('Adapter does not document custom check: '+$check)}}}
$runner=@(Get-ChildItem -LiteralPath (Join-Path $skillDir 'scripts') -File -Filter '*-StaticReplay.ps1' -ErrorAction SilentlyContinue)
if($runner.Count -lt 1){[void]$issues.Add('No *-StaticReplay.ps1 runner found')}else{foreach($r in $runner){$rt=Read-Strict $r.FullName;$rtNormalized=$rt.Replace('/','\');$manifestNormalized=$ManifestPath.Replace('/','\');if($rtNormalized -notmatch 'Invoke-ESStaticDeepReplay\.ps1' -or $rtNormalized -notmatch [regex]::Escape($manifestNormalized)){[void]$issues.Add('Runner is not bound to the shared replay script and manifest: '+(Relative $r.FullName))}}}
$govPath=Join-Path $skillDir 'governance.json'
if(Test-Path $govPath){try{$gov=Read-Strict $govPath|ConvertFrom-Json;if([bool]$gov.staticDeepReplayRequired -ne $true -or [string]$gov.defaultVerificationOrder -ne 'StaticDeepReplay-first'){[void]$issues.Add('Governance does not require StaticDeepReplay-first')}}catch{[void]$issues.Add('governance.json is not valid JSON')}}
$files=@();foreach($sourceRoot in @($manifest.sourceRoots)){try{$full=Resolve-ProjectPath ([string]$sourceRoot);if(-not(Test-Path $full)){[void]$issues.Add("sourceRoot missing: $sourceRoot")}elseif(Test-Path $full -PathType Leaf){$files+=Get-Item $full}else{$files+=Get-ChildItem $full -Recurse -File}}catch{[void]$issues.Add($_.Exception.Message)}}
$hashes=@($files|Sort-Object FullName -Unique|ForEach-Object {[pscustomobject]@{path=(Relative $_.FullName);sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$skillText='';if(Test-Path (Join-Path $skillDir 'SKILL.md')){$skillText=Read-Strict (Join-Path $skillDir 'SKILL.md')}
$specializedResults=@();$registryItem=$null;if($specializedRegistry){$registryItem=@($specializedRegistry.skills|Where-Object {[string]$_.skillName -eq $skillName}|Select-Object -First 1)}
if($registryItem){
    $spec=$manifest.specializedAcceptance;$guidanceRef=[string]$manifest.specializedGuidanceRef;$guidancePath=Join-Path $skillDir $guidanceRef.Replace('/','\');$specIssues=@()
    if($null -eq $spec){$specIssues+='specializedAcceptance is missing'}else{
        foreach($field in @('id','title','requiredStaticCases','evidenceArtifacts','sourceAssertions','runtimeBoundary')){if($null -eq $spec.PSObject.Properties[$field]){$specIssues+="specializedAcceptance.$field is missing"}}
        if([string]$spec.id -ne [string]$registryItem.acceptanceId){$specIssues+='acceptance id does not match registry'}
        if([string]$guidanceRef -ne [string]$registryItem.guidanceRef){$specIssues+='guidance ref does not match registry'}
        if(@($spec.requiredStaticCases).Count -lt 5){$specIssues+='at least five specialized static cases are required'}
        if(@($spec.sourceAssertions).Count -lt 3){$specIssues+='at least three source assertions are required'}
    }
    $guidanceText='';if(-not(Test-Path $guidancePath -PathType Leaf)){$specIssues+='specialized guidance file is missing'}else{$guidanceText=Read-Strict $guidancePath}
    if($guidanceText){if($guidanceText -notmatch [regex]::Escape([string]$spec.id)){$specIssues+='guidance does not contain acceptance id'};foreach($caseId in @($spec.requiredStaticCases)){if($guidanceText -notmatch [regex]::Escape([string]$caseId)){$specIssues+='guidance missing case: '+$caseId}};foreach($assertion in @($spec.sourceAssertions)){if($skillText -notmatch [regex]::Escape([string]$assertion)){$specIssues+='SKILL.md missing declared assertion: '+$assertion}};foreach($artifact in @($spec.evidenceArtifacts)){try{$artifactPath=Resolve-ProjectPath ('.agents/skills/'+$skillName+'/'+[string]$artifact);if(-not(Test-Path $artifactPath -PathType Leaf)){$specIssues+='evidence artifact missing: '+$artifact}}catch{$specIssues+='evidence artifact path invalid: '+$artifact}}}
    foreach($caseId in @($spec.requiredStaticCases)){$specializedResults += [pscustomobject]@{id=[string]$caseId;status=if($specIssues.Count -eq 0){'passed'}else{'blocked'}}}
    if($specIssues.Count -gt 0){foreach($detail in $specIssues){[void]$issues.Add('Specialized acceptance '+[string]$spec.id+': '+$detail)}}
}
function Add-CustomCheck([string]$id,[bool]$passed,[string]$detail){$script:customResults += [pscustomobject]@{id=$id;status=if($passed){'passed'}else{'blocked'};detail=$detail};if(-not $passed){[void]$issues.Add("Custom check $id failed: $detail")}}
$customResults=@()
foreach($check in $customChecks){switch($check){
 'authority-routing' { Add-CustomCheck $check (($null -ne $gov) -and @($gov.routeKeys).Count -gt 0 -and @($gov.requiredAuthorityRefs).Count -gt 0) 'governance routeKeys and requiredAuthorityRefs are declared' }
 'permission-boundary' { Add-CustomCheck $check (($null -ne $gov) -and $gov.allowDirectExecution -eq $false -and $gov.requiresBrainPlan -eq $true) 'direct execution is denied and plan binding is required' }
 'deterministic-replay' { Add-CustomCheck $check (@($manifest.staticClaims) -contains 'deterministic-replay' -and $hashes.Count -gt 0) 'deterministic claim and source hashes exist' }
 'evidence-contract' { Add-CustomCheck $check ($skillText -match '(?i)evidence|receipt|acceptance') 'Skill declares evidence/receipt/acceptance boundary' }
 'knowledge-boundary' { Add-CustomCheck $check ($skillText -match '(?i)KnowledgeIndex|SourceRef|ContentHash') 'knowledge source and hash boundary is declared' }
 'bounded-output' { Add-CustomCheck $check ($skillText -match '(?i)bounded|output policy|输出') 'bounded output policy is declared' }
 'editor-layout-static' { Add-CustomCheck $check ($skillText -match '(?i)min(Size|imum)|max(Size|imum)|narrow|DPI|layout|窄屏|高 DPI') 'layout constraints and extreme viewport evidence are declared' }
 'lifecycle-boundary' { Add-CustomCheck $check ($skillText -match '(?i)lifecycle|reload|unbind|recovery|休眠|生命周期') 'lifecycle/reload/unbind boundary is declared' }
 'input-boundary' { Add-CustomCheck $check ($skillText -match '(?i)invalid-input|denied-expansion|boundary|非法输入|越界') 'invalid and denied input boundaries are declared' }
 'recovery-cache' { Add-CustomCheck $check ($skillText -match '(?i)interruption|recovery|cache|hash|中断|恢复|缓存') 'recovery and cache/hash behavior is declared' }
 'change-boundary' { Add-CustomCheck $check ($skillText -match '(?i)write scope|change budget|stop condition|boundary|写入范围|停止条件') 'change/write/stop boundary is declared' }
 'resource-projection' { Add-CustomCheck $check (@($manifest.sourceRoots).Count -gt 0 -and $hashes.Count -gt 0) 'responsibility source projection is hash-bound' }
 'runtime-escalation' { Add-CustomCheck $check (($null -ne $manifest.runtimeEscalation) -and $manifest.runtimeClaimsNotProven.Count -gt 0) 'runtime claims and escalation remain explicit' }
 'consistency-cache' { Add-CustomCheck $check ($skillText -match '(?i)consistency|snapshot|stale|cache|hash|一致性|快照|缓存') 'consistency and stale-cache boundary is declared' }
 'compatibility-boundary' { Add-CustomCheck $check ($skillText -match '(?i)compatibility|version|release|兼容|版本|发布') 'compatibility/version boundary is declared' }
 'operation-allowlist' { Add-CustomCheck $check ($skillText -match '(?i)allowlist|allowed operations|fixed route') 'operation allowlist and fixed route are declared' }
 'credential-isolation' { Add-CustomCheck $check ($skillText -match '(?i)credential|secret|token|password') 'credential isolation and non-disclosure boundary is declared' }
 'external-data-boundary' { Add-CustomCheck $check ($skillText -match '(?i)untrusted|external content|external data') 'external data remains untrusted and cannot become project authority' }
}}
$status=if($issues.Count -eq 0){'static-passed'}else{'static-blocked'}
$issueText=$issues -join "`n"
$caseResults=@();foreach($caseId in $required){$assertionValue='';if($assertions -and $assertions.PSObject.Properties.Name -contains $caseId){$assertionValue=[string]$assertions.$caseId};$caseStatus=if($issueText -match [regex]::Escape($caseId)){'blocked'}else{'passed'};$caseResults += [pscustomobject]@{id=$caseId;status=$caseStatus;assertion=$assertionValue}}
$reportFull=Resolve-ProjectPath $ReportPath
$sourceRefs=[ordered]@{}
foreach($hash in $hashes){$sourceRefs[[string]$hash.path]=[string]$hash.sha256}
$planSeed=$skillName+'|'+$ManifestPath+'|'+(($hashes|ForEach-Object {[string]$_.path+'='+[string]$_.sha256}) -join '|')
$planHash=([BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash([Text.Encoding]::UTF8.GetBytes($planSeed)))).Replace('-','').ToLowerInvariant()
$generatedUtc=[DateTime]::UtcNow.ToString('o')
$result=[ordered]@{schemaVersion=1;validator='es-static-deep-replay';skillName=$skillName;case='StaticDeepReplay';status=if($status -eq 'static-passed'){'passed'}else{'blocked'};evidenceLevel='S1';receiptPath=$ReportPath.Replace('\','/');sourceRefs=@($sourceRefs.Keys);sourceRefHashes=$sourceRefs;toolId='es-static-deep-replay';unityVersion='not-run';capturedUtc=$generatedUtc;planHash=$planHash;profile='StaticReview';responsibilityProfile=$profile;responsibilityChecks=$customChecks;staticStatus=$status;runtimeStatus='runtime-not-run';overallVerdict=if($status -eq 'static-passed'){'StaticDeepReplayComplete'}else{'StaticBlocked'};claimsNotProven=@($manifest.runtimeClaimsNotProven);nextAction=if($status -eq 'static-passed'){'Do not start Runtime unless the declared escalation is authorized.'}else{'Resolve StaticDeepReplay issues before Runtime.'};cases=@($caseResults);customCheckResults=@($customResults);specializedAcceptance=$specializedResults;sourceHashes=$hashes;issues=@($issues);runtimeEscalation=$manifest.runtimeEscalation;generatedUtc=$generatedUtc}
$parent=Split-Path -Parent $reportFull;if(-not(Test-Path $parent)){New-Item $parent -ItemType Directory -Force|Out-Null};[IO.File]::WriteAllText($reportFull,($result|ConvertTo-Json -Depth 12),$utf8);$result|ConvertTo-Json -Depth 12
if($status -eq 'static-passed'){exit 0}else{exit 1}
