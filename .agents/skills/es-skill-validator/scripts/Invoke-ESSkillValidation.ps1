[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$SkillName,
    [ValidateSet('Structural','Governance','VerificationSemantics','StaticDeepReplay','ChangeImpact','Catalog','Security','Semantic','Boundary','Architecture','Evidence','Full')][string[]]$Profile = @('Structural','Governance','VerificationSemantics','StaticDeepReplay','ChangeImpact','Catalog','Security','Semantic','Boundary','Architecture'),
    [string]$ReportPath,
    [ValidateSet('CurrentUserDirect','ManagedAIBrain')][string]$AuthorizationLane='CurrentUserDirect'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$skillsRoot=Join-Path $root '.agents\skills'
if(-not (Test-Path -LiteralPath $skillsRoot -PathType Container)){ Write-Error 'Missing .agents/skills' -ErrorAction Continue; exit 2 }
if($SkillName){
    if($SkillName -notmatch '^es-[a-z0-9]+(?:-[a-z0-9]+)*$'){ Write-Error 'Invalid SkillName' -ErrorAction Continue; exit 2 }
    $targets=@(Join-Path $skillsRoot $SkillName)
} else { $targets=@(Get-ChildItem -LiteralPath $skillsRoot -Directory | ForEach-Object FullName) }
$results=@()
$highRisk='(?i)(ignore\s+(all\s+)?previous|bypass|disable\s+(?:aiwarnings|aicommands|aibrain|governance)|exfiltrat|read\s+(?:the\s+)?(?:secret|token|password|private[ ._-]?key|credential|api[ ._-]?key)|Invoke-WebRequest|curl\s+https?://|wget\s+https?://|\.env\b)'
$highRiskNegation='(?i)(do\s+not|must\s+not|never|cannot|can''t|prohibited|denied|hard[- ]blocked|requires|禁止|不得|不要|拒绝|不能|不允许|仅可).{0,80}(bypass|disable|ignore|read|exfiltrat|secret|token|password|credential|api[ ._-]?key)|(bypass|disable)\s+(?:switch|path|route|mechanism)|(?:bypass|disable).{0,100}(?:not|never|denied|prohibited|hard[- ]blocked|禁止|不得|不能|不允许)'
function Get-ProjectRelativePath([string]$fullPath) {
    $rootNormalized=$root.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
    $fullNormalized=[IO.Path]::GetFullPath($fullPath)
    if(-not $fullNormalized.StartsWith($rootNormalized + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){ return $null }
    return $fullNormalized.Substring($rootNormalized.Length+1).Replace('\','/')
}
function Get-Sha256([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Read-StrictText([string]$path) {
    return [IO.File]::ReadAllText($path,(New-Object Text.UTF8Encoding($false,$true)))
}
function Get-PowerShellBoundaryFacts([string]$path) {
    $indirectLines=[Collections.Generic.HashSet[int]]::new()
    $emptyCatchLines=[Collections.Generic.HashSet[int]]::new()
    $tokens=$null;$parseErrors=$null
    try{$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$parseErrors)}
    catch{return [pscustomobject]@{parseSucceeded=$false;parseErrors=@($_.Exception.Message);indirectLines=$indirectLines;emptyCatchLines=$emptyCatchLines}}
    if(@($parseErrors).Count -gt 0){
        return [pscustomobject]@{parseSucceeded=$false;parseErrors=@($parseErrors|ForEach-Object Message);indirectLines=$indirectLines;emptyCatchLines=$emptyCatchLines}
    }
    foreach($command in @($ast.FindAll({param($node)$node -is [Management.Automation.Language.CommandAst]},$true))){
        $commandName=[string]$command.GetCommandName()
        $isDynamicInvocation=[string]::IsNullOrWhiteSpace($commandName) -and $command.InvocationOperator -in @(
            [Management.Automation.Language.TokenKind]::Ampersand,
            [Management.Automation.Language.TokenKind]::Dot)
        $isKnownIndirect=$commandName -in @('iex','Invoke-Expression','Start-Process','source','call')
        $isProcessStartInfo=($commandName -eq 'New-Object' -and $command.Extent.Text -match '(?i)ProcessStartInfo')
        if($isDynamicInvocation -or $isKnownIndirect -or $isProcessStartInfo){[void]$indirectLines.Add($command.Extent.StartLineNumber)}
    }
    foreach($typeExpression in @($ast.FindAll({param($node)$node -is [Management.Automation.Language.TypeExpressionAst] -and $node.TypeName.FullName -match '(?i)ProcessStartInfo'},$true))){
        [void]$indirectLines.Add($typeExpression.Extent.StartLineNumber)
    }
    foreach($catchClause in @($ast.FindAll({param($node)$node -is [Management.Automation.Language.CatchClauseAst]},$true))){
        $bodyText=[string]$catchClause.Body.Extent.Text
        if($catchClause.Body.Statements.Count -eq 0 -or $bodyText -match '(?is)^\{\s*(?:\$_|\$null)\s*\|\s*Out-Null\s*\}$'){
            [void]$emptyCatchLines.Add($catchClause.Extent.StartLineNumber)
        }
    }
    return [pscustomobject]@{parseSucceeded=$true;parseErrors=@();indirectLines=$indirectLines;emptyCatchLines=$emptyCatchLines}
}
function Get-InlineYamlList([string]$value) {
    $content=([string]$value).Trim()
    if($content.StartsWith('[') -and $content.EndsWith(']')){$content=$content.Substring(1,$content.Length-2)}
    return @($content.Split(',') | ForEach-Object { $_.Trim().Trim([char]39,[char]34) } | Where-Object { $_ })
}
function Get-YamlSkillBlock([string]$text,[string]$name) {
    $pattern='(?ms)^  '+[regex]::Escape($name)+':\s*\n(?:(?!^  [a-z0-9][a-z0-9-]*:).)*'
    $match=[regex]::Match($text,$pattern)
    if($match.Success){return $match.Value}
    return $null
}
function Get-KnowledgeBlocks([string]$text) {
    return @([regex]::Matches($text,'(?ms)^\s*-\s+knowledgeId:.*?(?=^\s*-\s+knowledgeId:|\z)') | ForEach-Object Value)
}
function Add-BoundaryFinding([System.Collections.IList]$findings,[string]$code,[string]$path,[int]$line,[string]$detail,[string]$severity='blocked') {
    if($severity -notin @('blocked','review')){$severity='blocked'}
    $managedChannelCodes=@(
        'CommandBindingRegistryInvalid','InvalidCommandRequirement','NoExplicitCommandBinding',
        'InvalidCommandBinding','CommandBindingMissing','CommandPathInvalid','CommandBodyMismatch',
        'CommandBodyHashStale','CommandCatalogMismatch','AuthorityReadMissing','MissingTaskContract',
        'permission-expansion'
    )
    if($AuthorizationLane -eq 'CurrentUserDirect' -and $severity -eq 'blocked' -and $managedChannelCodes -contains $code){$severity='review'}
    [void]$findings.Add([pscustomobject]@{code=$code;path=$path;line=$line;severity=$severity;detail=$detail})
}
function Get-ManagedChannelSeverity {
    if($AuthorizationLane -eq 'ManagedAIBrain'){return 'blocked'}
    return 'review'
}
function Test-Declaration([string]$text,[string[]]$patterns) {
    foreach($pattern in $patterns){if($text -match $pattern){return $true}}
    return $false
}
function Get-CommandBodyMetadata([string]$text) {
    $type=[regex]::Match($text,'(?mi)^(?:命令类型|CommandType)[ \t]*[：:][ \t]*([^\r\n]+)')
    $write=[regex]::Match($text,'(?mi)^(?:默认改文件|WriteMode)[ \t]*[：:][ \t]*([^\r\n]+)')
    $risk=[regex]::Match($text,'(?mi)^(?:风险等级|RiskLevel)[ \t]*[：:][ \t]*(L[0-9]+)')
    return [pscustomobject]@{hasType=$type.Success;hasWrite=$write.Success;hasRisk=$risk.Success;type=if($type.Success){$type.Groups[1].Value.Trim()}else{''};write=if($write.Success){$write.Groups[1].Value.Trim()}else{''};risk=if($risk.Success){$risk.Groups[1].Value.Trim()}else{''}}
}
function Test-RelativeProjectFile([string]$relative) {
    if([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)){return $false}
    $candidate=Join-Path $root $relative.Replace('/','\')
    $full=[IO.Path]::GetFullPath($candidate)
    $rootNormalized=$root.TrimEnd('\','/')
    return $full.StartsWith($rootNormalized+'\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $full -PathType Leaf)
}
function Test-NonEmptyStringArray([object]$value) {
    if(-not ($value -is [Array]) -or @($value).Count -eq 0){return $false}
    foreach($item in @($value)){
        if(-not ($item -is [string]) -or [string]::IsNullOrWhiteSpace([string]$item)){return $false}
    }
    return $true
}
function Test-DirectAuthorizedPaths([object]$paths) {
    if(-not (Test-NonEmptyStringArray $paths)){return $false}
    $rootNormalized=$root.TrimEnd('\','/')
    $prefix=$rootNormalized+[IO.Path]::DirectorySeparatorChar
    foreach($authorizedPath in @($paths)){
        $pathText=[string]$authorizedPath
        if($pathText -ne $pathText.Trim() -or [IO.Path]::IsPathRooted($pathText) -or $pathText -match '^[a-zA-Z]:' -or $pathText -match '^[\\/]{2}'){return $false}
        try{$fullPath=[IO.Path]::GetFullPath([IO.Path]::Combine($rootNormalized,$pathText))}catch{return $false}
        if(-not ($fullPath.Equals($rootNormalized,[StringComparison]::OrdinalIgnoreCase) -or $fullPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase))){return $false}
    }
    return $true
}
function Get-ProjectRelativeEvidenceFile([object]$relative) {
    if(-not ($relative -is [string]) -or [string]::IsNullOrWhiteSpace([string]$relative)){return $null}
    $pathText=[string]$relative
    if($pathText -ne $pathText.Trim() -or [IO.Path]::IsPathRooted($pathText) -or $pathText -match '^[a-zA-Z]:' -or $pathText -match '^[\\/]{2}'){return $null}
    $rootNormalized=$root.TrimEnd('\','/')
    try{$fullPath=[IO.Path]::GetFullPath([IO.Path]::Combine($rootNormalized,$pathText))}catch{return $null}
    if(-not $fullPath.StartsWith($rootNormalized+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){return $null}
    if(-not (Test-Path -LiteralPath $fullPath -PathType Leaf)){return $null}
    return $fullPath
}
function Test-ReceiptAuthorization([object]$receipt) {
    $kindProperty=$receipt.PSObject.Properties['authorizationKind']
    $kind=if($null -eq $kindProperty){''}else{[string]$kindProperty.Value}
    if($AuthorizationLane -eq 'ManagedAIBrain'){
        if([string]::IsNullOrWhiteSpace($kind)){return ([string]$receipt.planHash -match '^[a-fA-F0-9]{64}$')}
        return ($kind -ceq 'managed-aibrain' -and [string]$receipt.planHash -match '^[a-fA-F0-9]{64}$')
    }
    if($kind -ceq 'read-only'){return $true}
    if($kind -cne 'current-user-direct'){return $false}
    return ([string]$receipt.userInstructionHash -match '^[a-fA-F0-9]{64}$') -and
        (Test-NonEmptyStringArray $receipt.authorizedOperations) -and
        (Test-DirectAuthorizedPaths $receipt.authorizedPaths)
}
foreach($target in $targets){
    if(-not (Test-Path -LiteralPath $target -PathType Container)){ $results += ([pscustomobject]@{skill=$SkillName;profile='Structural';status='failed';message='Skill directory not found';source=$target}); continue }
    $name=Split-Path -Leaf $target
    $skill=Join-Path $target 'SKILL.md'; $gov=Join-Path $target 'governance.json'; $yaml=Join-Path $target 'agents\openai.yaml'
    if($Profile -contains 'Structural' -or $Profile -contains 'Full'){
        $ok=(Test-Path $skill -PathType Leaf) -and (Test-Path $yaml -PathType Leaf) -and ($name -match '^es-[a-z0-9]+(?:-[a-z0-9]+)*$')
        if($ok){ try { [IO.File]::ReadAllText($skill,(New-Object Text.UTF8Encoding($false,$true))) | Out-Null } catch { $ok=$false } }
        $results += ([pscustomobject]@{skill=$name;profile='Structural';status=if($ok){'passed'}else{'failed'};message=if($ok){'required files, name and UTF-8 readable'}else{'missing/invalid structure or UTF-8'};source=$skill})
    }
    if($Profile -contains 'ChangeImpact' -or $Profile -contains 'Full'){
        $impactScript=Join-Path $skillsRoot 'es-skill-governance/scripts/Get-ESSkillChangeImpact.ps1'
        $impactStatus='failed';$impactMessage='SkillChangeImpact evaluator is missing';$impactObject=$null
        if(Test-Path -LiteralPath $impactScript -PathType Leaf){
            try{
                $impactOutput=& powershell -NoProfile -File $impactScript -ProjectRoot $root -SkillPath $target 2>&1 | Out-String
                $impactObject=$impactOutput.Trim() | ConvertFrom-Json
                $impactClass=[string]$impactObject.skillChangeImpact
                if($impactClass -in @('medium','major') -and [bool]$impactObject.revalidationRequired -and [bool]$impactObject.completionClaimAllowed -eq $false){$impactStatus='review';$impactMessage="Derived $impactClass change impact requires revalidation: $([string]::Join(', ', @($impactObject.requiredStages)))"}
                elseif($impactClass -eq 'small' -and [bool]$impactObject.completionClaimAllowed){$impactStatus='passed';$impactMessage='No medium/major Skill change impact detected'}
                else{$impactStatus='blocked';$impactMessage='Change impact output is internally inconsistent'}
            }catch{$impactStatus='blocked';$impactMessage='SkillChangeImpact evaluator failed: '+$_.Exception.Message}
        }
        $results += ([pscustomobject]@{skill=$name;profile='ChangeImpact';status=$impactStatus;message=$impactMessage;source=$impactScript;impact=$impactObject})
    }
    if($Profile -contains 'Governance' -or $Profile -contains 'Full'){
        $ok=$false; $message='governance.json valid'
        $contractScript=Join-Path $skillsRoot 'es-skill-governance/scripts/Test-ESSkillContract.ps1'
        if((Test-Path $contractScript -PathType Leaf) -and (Test-Path $target -PathType Container)){
            $contractOutput=& powershell -NoProfile -File $contractScript -SkillPath $target -RequireGovernanceMetadata -AuthorizationLane $AuthorizationLane 2>&1 | Out-String
            $ok=($LASTEXITCODE -eq 0)
            if(-not $ok){$message='project Skill governance contract failed: '+$contractOutput.Trim()}
        } elseif(-not (Test-Path $gov -PathType Leaf)) {$message='governance.json missing and project contract validator unavailable'}
        $results += ([pscustomobject]@{skill=$name;profile='Governance';status=if($ok){'passed'}else{'failed'};message=$message;source=$gov})
    }
    if($Profile -contains 'VerificationSemantics' -or $Profile -contains 'Full'){
        $ok=$false; $message='Static/Runtime verification profiles and runtime-not-run policy are explicit'
        try{
            $sem=Get-Content -LiteralPath $gov -Raw -Encoding UTF8|ConvertFrom-Json
            $profiles=$sem.verificationProfiles
            $runtimeNotApplicable=([string]$sem.runtimeExecutionPolicy -ceq 'not-applicable')
            $requiredProfiles=if($runtimeNotApplicable){@('StaticReview','EngineeringReadiness')}else{@('StaticReview','EngineeringReadiness','RuntimeAcceptance','ReleaseAcceptance')}
            $missing=@($requiredProfiles|Where-Object {$profiles.PSObject.Properties.Name -notcontains $_})
            $policy=[string]$sem.runtimeNotRunPolicy
            if($missing.Count -gt 0){$message='Missing verificationProfiles: '+($missing -join ', ')}
            elseif($policy -notmatch '(?i)runtime-not-run' -or $policy -notmatch '(?i)StaticReview'){$message='runtimeNotRunPolicy must state that runtime-not-run does not block StaticReview'}
            elseif(@($profiles.StaticReview.required).Count -eq 0){$message='StaticReview.required is empty'}
            elseif([string]$sem.defaultVerificationOrder -ne 'StaticDeepReplay-first'){$message='defaultVerificationOrder must be StaticDeepReplay-first'}
            elseif([bool]$sem.staticDeepReplayRequired -ne $true){$message='StaticDeepReplay declaration is missing'}
            elseif(@($sem.staticDeepReplayCases).Count -lt 7){$message='staticDeepReplayCases must include the seven fixed replay cases'}
            elseif($runtimeNotApplicable -and ([bool]$sem.developerAuthorizationRequired -ne $false -or [string]$sem.runtimeHardGate -cne 'not-applicable')){$message='not-applicable Runtime policy must disable developer authorization and the Runtime hard gate'}
            elseif(-not $runtimeNotApplicable -and ([string]$sem.runtimeExecutionPolicy -ne 'explicit-developer-authorization-only' -or [bool]$sem.developerAuthorizationRequired -ne $true)){$message='Runtime authorization declaration is missing'}
            elseif(-not $runtimeNotApplicable -and [string]$sem.runtimeHardGate -ne 'runtime-required && runtime-not-run => Blocked'){$message='runtimeHardGate declaration is missing or invalid'}
            elseif(-not $runtimeNotApplicable -and ([string]$sem.runtimeAuthorizationContractRef -ne '.agents/skills/es-skill-governance/references/runtime-authorization-contract.md' -or [string]$sem.runtimeAuthorizationValidator -ne '.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorization.ps1')){$message='Runtime authorization contract/validator refs are missing'}
            else{
                $invalidWeight=@($requiredProfiles|Where-Object {[double]$profiles.$_.staticWeight -lt 0.5 -or [double]$profiles.$_.runtimeWeight -gt 0.5})
                $invalidReplay=if($runtimeNotApplicable){@($requiredProfiles|Where-Object {[bool]$profiles.$_.staticDeepReplayRequired -ne $true -or [bool]$profiles.$_.runtimeAuthorizationRequired -ne $false -or [bool]$profiles.$_.runtimeRequired -ne $false -or [double]$profiles.$_.runtimeWeight -ne 0})}else{@($requiredProfiles|Where-Object {[bool]$profiles.$_.staticDeepReplayRequired -ne $true -or [bool]$profiles.$_.runtimeAuthorizationRequired -ne $true})}
                if($invalidWeight.Count -gt 0){$message='staticWeight must be >= 0.5 and runtimeWeight <= 0.5 for: '+($invalidWeight -join ', ')}elseif($invalidReplay.Count -gt 0){$message=if($runtimeNotApplicable){'not-applicable Runtime profiles must remain Static-only for: '+($invalidReplay -join ', ')}else{'StaticDeepReplay/runtime authorization flags missing for: '+($invalidReplay -join ', ')}}else{$ok=$true}
            }
        }catch{$message='verificationProfiles invalid: '+$_.Exception.Message}
        $results += ([pscustomobject]@{skill=$name;profile='VerificationSemantics';status=if($ok){'passed'}else{'blocked'};message=$message;source=$gov})
    }
    if($Profile -contains 'StaticDeepReplay' -or $Profile -contains 'Full'){
        $manifest=Join-Path $target 'static-replay.manifest.json'
        $adapter=Join-Path $target 'references\static-replay-adapter.md'
        $runner=@(Get-ChildItem -LiteralPath (Join-Path $target 'scripts') -File -Filter '*-StaticReplay.ps1' -ErrorAction SilentlyContinue)
        $ok=(Test-Path -LiteralPath $manifest -PathType Leaf) -and (Test-Path -LiteralPath $adapter -PathType Leaf) -and $runner.Count -ge 1
        $message=if($ok){'StaticDeepReplay manifest, adapter reference and runner are present'}else{'StaticDeepReplay support is incomplete (manifest, adapter reference or runner missing)'}
        if($ok){try{$m=Get-Content $manifest -Raw -Encoding UTF8|ConvertFrom-Json;$profiles=@('governance','knowledge','editor','engineering','authoring','testing','session','release','base');if([string]$m.responsibilityProfile -notin $profiles){$ok=$false;$message='Unknown responsibilityProfile'}elseif(@($m.responsibilityChecks).Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$m.responsibilityScope)){$ok=$false;$message='Responsibility-specific static checks or scope are missing'}elseif((Get-Content $adapter -Raw -Encoding UTF8) -notmatch ('(?im)^Responsibility profile:\s*'+[regex]::Escape([string]$m.responsibilityProfile)+'\s*$')){$ok=$false;$message='Adapter profile does not match manifest'}else{$registry=Join-Path $skillsRoot 'es-static-deep-replay/references/specialized-acceptance-registry.json';if(Test-Path $registry){$registryJson=Get-Content $registry -Raw -Encoding UTF8|ConvertFrom-Json;$requiredSpecialized=@($registryJson.skills|Where-Object {[string]$_.skillName -eq $name});if($requiredSpecialized.Count -gt 0 -and $null -eq $m.PSObject.Properties['specializedAcceptance']){$ok=$false;$message='Registry requires specializedAcceptance for this Skill'}elseif($requiredSpecialized.Count -gt 0 -and [string]$m.specializedAcceptance.id -ne [string]$requiredSpecialized[0].acceptanceId){$ok=$false;$message='specializedAcceptance id does not match registry'}}}}catch{$ok=$false;$message='StaticDeepReplay manifest is not valid JSON'}}
        $results += ([pscustomobject]@{skill=$name;profile='StaticDeepReplay';status=if($ok){'passed'}else{'blocked'};message=$message;source=$manifest})
    }
    if($Profile -contains 'Security' -or $Profile -contains 'Full'){
        $signals=@()
        foreach($file in Get-ChildItem -LiteralPath $target -Recurse -File -Include *.md,*.json,*.yaml,*.yml,*.ps1,*.py,*.sh | Where-Object { $_.FullName -notmatch '\\references\\' -and $_.Name -notin @('Invoke-ESSkillValidation.ps1','Test-ESSecurityBoundary.ps1') }){
            $lineNo=0; foreach($line in [IO.File]::ReadLines($file.FullName)){ $lineNo++; if($line -match $highRisk -and $line -notmatch $highRiskNegation){ $signals += "$($file.FullName):$lineNo" } }
        }
        # Raw wording is a triage signal only. Executable secret, network,
        # destructive and authority-boundary violations are decided below by
        # the Boundary profile with object/path context.
        $results += ([pscustomobject]@{skill=$name;profile='Security';status=if($signals.Count -eq 0){'passed'}else{'review'};message=if($signals.Count -eq 0){'no high-risk triage signal'}else{"raw security wording requires scoped review ($($signals.Count) signal(s)); no hard-block inferred from text alone"};source=(@($signals)-join '; ')})
    }
    if($Profile -contains 'Semantic' -or $Profile -contains 'Full'){
        $issues=New-Object 'System.Collections.Generic.List[string]'
        $managedIssues=New-Object 'System.Collections.Generic.List[string]'
        $knowledgeIndex=Join-Path $root 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
        $brainEntry=Join-Path $root 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md'
        $resourceIndex=Join-Path $root '.agents/SKILL_RESOURCE_INDEX.yaml'
        $catalog=Join-Path $root '.agents/SKILL_CATALOG.yaml'
        $capabilityRegistryPath=Join-Path $root '.agents/skills/es-skill-governance/references/capability-mode-registry.json'
        $aiwarningsRoot=Join-Path $root 'Assets/Plugins/ES/AIWarnings'
        $startDir=Get-ChildItem -LiteralPath $aiwarningsRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '00_*' } | Select-Object -First 1
        $authorityFiles=@()
        if($startDir){
            $authorityFiles += Join-Path $startDir.FullName 'README.md'
            $authorityFiles += @(Get-ChildItem -LiteralPath $startDir.FullName -File -Filter '*CurrentStatus*.md' -ErrorAction SilentlyContinue | Select-Object -First 1 | ForEach-Object FullName)
            $authorityFiles += @(Get-ChildItem -LiteralPath $startDir.FullName -File -Filter '*RuleIndex*.md' -ErrorAction SilentlyContinue | Select-Object -First 1 | ForEach-Object FullName)
        }
        foreach($path in $authorityFiles){
            $relative=Get-ProjectRelativePath $path
            if(-not $relative -or -not (Test-Path -LiteralPath $path -PathType Leaf)){[void]$issues.Add("missing project authority: $relative");continue}
            try{Read-StrictText $path|Out-Null}catch{[void]$issues.Add("invalid UTF-8 authority: $relative")}
        }
        $managedAuthorityFiles=@(
            (Join-Path $root 'Assets/Plugins/ES/AICommands/README.md'),
            (Join-Path $root 'Assets/Plugins/ES/AICommands/AICommandCatalog.json')
        )
        foreach($path in $managedAuthorityFiles){
            $relative=Get-ProjectRelativePath $path
            if(-not $relative -or -not (Test-Path -LiteralPath $path -PathType Leaf)){[void]$managedIssues.Add("missing managed-channel authority: $relative");continue}
            try{Read-StrictText $path|Out-Null}catch{[void]$managedIssues.Add("invalid UTF-8 managed-channel authority: $relative")}
        }
        foreach($path in @($knowledgeIndex,$brainEntry,$resourceIndex,$catalog)){
            if(-not (Test-Path -LiteralPath $path -PathType Leaf)){[void]$issues.Add("missing project semantic index: $(Get-ProjectRelativePath $path)")}
        }
        $governanceSemantic=$null
        try{$governanceSemantic=Get-Content -Raw -Encoding UTF8 $gov|ConvertFrom-Json}catch{[void]$issues.Add('governance.json is not parseable for semantic validation')}
        $skillTextSemantic=$null
        try{$skillTextSemantic=Read-StrictText $skill}catch{[void]$issues.Add('SKILL.md is not strict UTF-8 for semantic validation')}
        if($governanceSemantic){
            if(Test-Path -LiteralPath $capabilityRegistryPath -PathType Leaf){
                try{
                    $registrySemantic=Get-Content -Raw -Encoding UTF8 $capabilityRegistryPath|ConvertFrom-Json
                    if([string]$registrySemantic.defaultCapabilityMode -notin @('advisory','candidate','mutating')){[void]$issues.Add('Capability registry defaultCapabilityMode is invalid')}
                    $registryEntries=@($registrySemantic.entries)
                    $duplicateRegistry=@($registryEntries|Group-Object skillName|Where-Object {$_.Name -and $_.Count -gt 1})
                    if($duplicateRegistry.Count -gt 0){[void]$issues.Add('Capability registry contains duplicate Skill entries')}
                    foreach($entry in $registryEntries){
                        if([string]$entry.capabilityMode -notin @('advisory','candidate','mutating')){[void]$issues.Add("Capability registry mode invalid: $([string]$entry.skillName)")}
                        if(-not (Test-Path -LiteralPath (Join-Path $skillsRoot ([string]$entry.skillName)) -PathType Container)){[void]$issues.Add("Capability registry Skill missing: $([string]$entry.skillName)")}
                    }
                }catch{[void]$issues.Add('Capability registry is not parseable')}
            }else{[void]$issues.Add('Capability registry is missing')}
            $commandBindingRegistryPath=Join-Path $root '.agents/skills/es-skill-governance/references/command-binding-registry.json'
            if(Test-Path -LiteralPath $commandBindingRegistryPath -PathType Leaf){
                try{
                    $bindingRegistrySemantic=Get-Content -Raw -Encoding UTF8 $commandBindingRegistryPath|ConvertFrom-Json
                    $bindingEntries=@($bindingRegistrySemantic.entries)
                    $duplicateBindings=@($bindingEntries|Group-Object {([string]$_.skillName).Trim().ToLowerInvariant()+'|'+([string]$_.commandId).Trim()}|Where-Object {$_.Name -and $_.Count -gt 1})
                    if($duplicateBindings.Count -gt 0){[void]$managedIssues.Add('Command binding registry contains duplicate Skill/AICommand pairs')}
                    $commandCatalogSemantic=@()
                    if(Test-Path -LiteralPath (Join-Path $root 'Assets/Plugins/ES/AICommands/AICommandCatalog.json') -PathType Leaf){try{$commandCatalogSemantic=@((Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Assets/Plugins/ES/AICommands/AICommandCatalog.json')|ConvertFrom-Json).commands)}catch{}}
                    foreach($bindingEntry in $bindingEntries){
                        $entrySkill=[string]$bindingEntry.skillName; $entryCommand=[string]$bindingEntry.commandId
                        if(-not (Test-Path -LiteralPath (Join-Path $skillsRoot $entrySkill) -PathType Container)){[void]$managedIssues.Add("Command binding Skill missing: $entrySkill")}
                        $commandMatch=@($commandCatalogSemantic|Where-Object {[string]$_.id -ceq $entryCommand})
                        if($commandMatch.Count -ne 1){[void]$managedIssues.Add("Command binding does not resolve uniquely: $entryCommand")}else{
                            $commandPath=[string]$commandMatch[0].path; $commandFull=Join-Path $root $commandPath.Replace('/','\')
                            if(-not (Test-Path -LiteralPath $commandFull -PathType Leaf)){[void]$managedIssues.Add("Command binding body missing: $entryCommand")}
                            elseif([string]$bindingEntry.commandHash -notmatch '^[a-fA-F0-9]{64}$' -or [string]$bindingEntry.commandHash -cne (Get-Sha256 $commandFull)){[void]$managedIssues.Add("Command binding hash stale: $entryCommand")}
                            foreach($property in @('role','riskLevel','writeMode')){if([string]$bindingEntry.$property -ne [string]$commandMatch[0].$property){[void]$managedIssues.Add("Command binding $property mismatch: $entryCommand")}}
                        }
                    }
                }catch{[void]$managedIssues.Add('Command binding registry is not parseable')}
            }else{[void]$managedIssues.Add('Command binding registry is missing')}
            $routeKeys=@($governanceSemantic.routeKeys|ForEach-Object {[string]$_}|Where-Object {$_})
            foreach($authorityRef in @($governanceSemantic.requiredAuthorityRefs | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })){
                if(-not (Test-RelativeProjectFile ([string]$authorityRef))){[void]$issues.Add("required authority ref missing: $authorityRef")}
            }
            if($routeKeys.Count -eq 0){[void]$issues.Add('governance.routeKeys is empty')}
            if(Test-Path -LiteralPath $resourceIndex -PathType Leaf){
                $resourceText=Read-StrictText $resourceIndex
                if($resourceText -notmatch ('(?m)(?:name:\s*|\b)'+[regex]::Escape($name)+'\b')){[void]$issues.Add('Skill is not discoverable in SKILL_RESOURCE_INDEX.yaml')}
            }
            if(Test-Path -LiteralPath $brainEntry -PathType Leaf){
                $brainText=Read-StrictText $brainEntry
                if(@($routeKeys|Where-Object {$brainText -match ('\b'+[regex]::Escape($_)+'\b')}).Count -eq 0){[void]$issues.Add('No governance routeKey is exposed by AIBRAIN_ENTRY.md')}
            }
            if(Test-Path -LiteralPath $knowledgeIndex -PathType Leaf){
                $knowledgeText=Read-StrictText $knowledgeIndex
                $matched=@(Get-KnowledgeBlocks $knowledgeText|Where-Object {$_ -match ('relatedSkills:\s*\[[^\]]*\b'+[regex]::Escape($name)+'\b')})
                if($matched.Count -eq 0){[void]$issues.Add('Skill has no relatedSkills binding in KnowledgeIndex.yaml')}
                $routeMatched=$false
                foreach($block in $matched){
                    $declared=@(); $routeMatch=[regex]::Match($block,'(?m)^\s*routeKeys:\s*\[([^\]]+)\]'); if($routeMatch.Success){$declared=Get-InlineYamlList $routeMatch.Groups[1].Value}
                    if(@($declared|Where-Object {$routeKeys -contains $_}).Count -gt 0){$routeMatched=$true}
                    $entryMatch=[regex]::Match($block,'(?m)^\s*file:\s*(.+)$'); $hashMatch=[regex]::Match($block,'(?m)^\s*contentHash:\s*([0-9a-fA-F]{64})\s*$')
                    if(-not $entryMatch.Success -or -not $hashMatch.Success){$bindingId=[regex]::Match($block,'(?m)^\s*-\s+knowledgeId:\s*([^\r\n]+)').Groups[1].Value.Trim();[void]$issues.Add("Knowledge binding lacks file/contentHash: $bindingId");continue}
                    $entryRelative=$entryMatch.Groups[1].Value.Trim().Trim([char]39,[char]34); $entryPath=Join-Path $root ('Documentation/AIKnowledge/'+$entryRelative.Replace('/','\'))
                    if(-not (Test-Path -LiteralPath $entryPath -PathType Leaf)){[void]$issues.Add("Knowledge entry missing: $entryRelative");continue}
                    try{$entryText=Read-StrictText $entryPath}catch{[void]$issues.Add("Knowledge entry invalid UTF-8: $entryRelative");continue}
                    $contentMatch=[regex]::Match($entryText,'(?mi)^`?ContentHash`?\s*:\s*([0-9a-f]{64})')
                    if($contentMatch.Success -and $contentMatch.Groups[1].Value -ne $hashMatch.Groups[1].Value){[void]$issues.Add("Knowledge ContentHash mismatch: $entryRelative")}
                }
                if($matched.Count -gt 0 -and -not $routeMatched){[void]$issues.Add('Knowledge binding has no routeKey intersection with governance.routeKeys')}
            }
            if(Test-Path -LiteralPath $catalog -PathType Leaf){
                $catalogText=Read-StrictText $catalog; $block=Get-YamlSkillBlock $catalogText $name
                if(-not $block){[void]$issues.Add('Skill has no unique Catalog record')}
                else{
                    $skillHash=Get-Sha256 $skill; $governanceHash=Get-Sha256 $gov
                    if($block -notmatch ('(?m)^\s*skillHash:\s*'+$skillHash+'\s*$')){[void]$issues.Add('Catalog skillHash is stale')}
                    if($block -notmatch ('(?m)^\s*governanceHash:\s*'+$governanceHash+'\s*$')){[void]$issues.Add('Catalog governanceHash is stale')}
                    foreach($property in @('tier','maturity','delivery','evidenceLevel')){ $value=[string]$governanceSemantic.$property; if($block -notmatch ('(?m)^\s*'+$property+':\s*'+[regex]::Escape($value)+'\s*$')){[void]$issues.Add("Catalog $property does not match governance.json")}}
                    foreach($route in $routeKeys){if($block -notmatch ('(?m)^\s*-\s*'+[regex]::Escape($route)+'\s*$')){[void]$issues.Add("Catalog routeKey missing: $route")}}
                }
            }
            if($governanceSemantic.authorityClass -ne 'standard' -and $governanceSemantic.requiresBrainPlan -ne $true){[void]$managedIssues.Add('Non-standard authority class must require AIBrain plan')}
        }
        $managedSeverity=Get-ManagedChannelSeverity
        $semanticStatus=if($issues.Count -gt 0){'blocked'}elseif($managedIssues.Count -gt 0){$managedSeverity}else{'passed'}
        $semanticMessage=if($issues.Count -gt 0){"project semantic blockers ($($issues.Count)); managed-channel findings ($($managedIssues.Count))"}elseif($managedIssues.Count -gt 0){"managed-channel semantic findings ($($managedIssues.Count)); authorizationLane=$AuthorizationLane"}else{'ESFramework authority, route, Knowledge, Resource Index and Catalog semantics passed'}
        $semanticSource=@($issues)+@($managedIssues)
        $results += ([pscustomobject]@{skill=$name;profile='Semantic';status=$semanticStatus;message=$semanticMessage;source=($semanticSource-join '; ');blockingCount=$issues.Count;managedFindingCount=$managedIssues.Count})
    }
    if($Profile -contains 'Boundary' -or $Profile -contains 'Full'){
        $findings=New-Object 'System.Collections.ArrayList'
        $govBoundary=$null
        try{$govBoundary=Get-Content -Raw -Encoding UTF8 $gov|ConvertFrom-Json}catch{}
        $declaredPathContract=$null
        if($govBoundary -and $govBoundary.PSObject.Properties.Name -contains 'pathBoundaryContractRef'){
            $contractPath=[string]$govBoundary.pathBoundaryContractRef
            if(Test-RelativeProjectFile $contractPath){
                try{$declaredPathContract=Read-StrictText (Join-Path $root ($contractPath -replace '/','\'))}catch{}
            }else{Add-BoundaryFinding $findings 'PathContractMissing' (Get-ProjectRelativePath $gov) 1 'pathBoundaryContractRef 必须指向项目内可读合同。'}
        }
        if($null -eq $declaredPathContract){
            foreach($fallbackContract in @('references/path-boundary-contract.md','references/external-unity-execution-contract.md')){
                $fallbackPath=Join-Path $target ($fallbackContract -replace '/','\')
                if(Test-Path -LiteralPath $fallbackPath -PathType Leaf){try{$declaredPathContract=Read-StrictText $fallbackPath}catch{};if($null -ne $declaredPathContract){break}}
            }
        }
        $skillBoundaryText=''; try{$skillBoundaryText=Read-StrictText $skill}catch{}
        $yamlBoundaryText=''; try{$yamlBoundaryText=Read-StrictText $yaml}catch{}
        $declaredText=($skillBoundaryText+"`n"+$yamlBoundaryText)
        $writePolicy=if($govBoundary){[string]$govBoundary.writePolicy}else{''}
        $capabilityMode='mutating'
        $capabilityRegistryPath=Join-Path $root '.agents/skills/es-skill-governance/references/capability-mode-registry.json'
        if(Test-Path -LiteralPath $capabilityRegistryPath -PathType Leaf){
            try{
                $capabilityRegistry=Get-Content -Raw -Encoding UTF8 $capabilityRegistryPath|ConvertFrom-Json
                if([string]$capabilityRegistry.defaultCapabilityMode -in @('advisory','candidate','mutating')){$capabilityMode=[string]$capabilityRegistry.defaultCapabilityMode}
                $capabilityEntry=@($capabilityRegistry.entries|Where-Object {[string]$_.skillName -ceq $name})|Select-Object -First 1
                if($capabilityEntry){
                    if([string]$capabilityEntry.capabilityMode -in @('advisory','candidate','mutating')){$capabilityMode=[string]$capabilityEntry.capabilityMode}
                    else{Add-BoundaryFinding $findings 'InvalidCapabilityMode' (Get-ProjectRelativePath $capabilityRegistryPath) 1 "Skill '$name' 的 capabilityMode 无效；必须为 advisory、candidate 或 mutating。"}
                }
            }catch{Add-BoundaryFinding $findings 'CapabilityModeRegistryInvalid' (Get-ProjectRelativePath $capabilityRegistryPath) 1 '能力模式注册表不是有效 JSON，不能据此放宽命令边界。'}
        }
        $commandBindingRegistryPath=Join-Path $root '.agents/skills/es-skill-governance/references/command-binding-registry.json'
        $registeredBindings=@()
        if(Test-Path -LiteralPath $commandBindingRegistryPath -PathType Leaf){
            try{
                $commandBindingRegistry=Get-Content -Raw -Encoding UTF8 $commandBindingRegistryPath|ConvertFrom-Json
                $registeredBindings=@($commandBindingRegistry.entries|Where-Object {[string]$_.skillName -ceq $name})
            }catch{Add-BoundaryFinding $findings 'CommandBindingRegistryInvalid' (Get-ProjectRelativePath $commandBindingRegistryPath) 1 '命令绑定注册表不是有效 JSON，不能支持 ManagedAIBrain 通道绑定。'}
        }
        $routeKeys=if($govBoundary){@($govBoundary.routeKeys|ForEach-Object {[string]$_}|Where-Object {$_})}else{@()}
        $commandCatalogPath=Join-Path $root 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
        $commandEntries=@()
        if(Test-Path -LiteralPath $commandCatalogPath -PathType Leaf){
            try{$commandEntries=@((Get-Content -Raw -Encoding UTF8 $commandCatalogPath|ConvertFrom-Json).commands)}catch{}
        }
        # Only executable script content can imply an external command requirement.
        # Mentions of MCP/Process/Network in SKILL prose are policy descriptions,
        # not permission-bearing behavior, and must not create false command gates.
        $scriptSignalText=''
        foreach($signalFile in @(Get-ChildItem -LiteralPath $target -Recurse -File -ErrorAction SilentlyContinue | Where-Object {$_.Extension -in @('.ps1','.py','.sh','.bat','.cmd')})){
            try{$scriptSignalText += "`n" + (Read-StrictText $signalFile.FullName)}catch{}
        }
        # Bare "MCP" in a catalog or metadata field is not an execution signal.
        # Require an invocation/API shape before imposing a command gate.
        $externalCommandSignal=Test-Declaration $scriptSignalText @('(?i)(ProcessStartInfo|Start-Process|Invoke-WebRequest|Invoke-RestMethod|curl\s+https?://|wget\s+https?://|subprocess\.(run|Popen|call)|os\.system|requests\.(get|post)|urllib\.request)|(?-i:\bUnityMCP\b|\bMCP(?:\.|::|/))')
        $needsCommand=if($capabilityMode -in @('advisory','candidate')){$externalCommandSignal}else{($writePolicy -notin @('read-only','report-only-explicit-path','')) -or $externalCommandSignal}
        $commandRequirement=if($govBoundary -and $govBoundary.PSObject.Properties.Name -contains 'commandRequirement'){[string]$govBoundary.commandRequirement}elseif($needsCommand){'required'}else{'none'}
        $governanceBindings=if($govBoundary){@($govBoundary.commandBindings|Where-Object {$_})}else{@()}
        foreach($registeredBinding in $registeredBindings){
            $shadowed=@($governanceBindings|Where-Object {[string]$_.commandId -ceq [string]$registeredBinding.commandId})
            foreach($oldBinding in $shadowed){
                if([string]$oldBinding.commandHash -cne [string]$registeredBinding.commandHash){Add-BoundaryFinding $findings 'CommandBindingShadowed' (Get-ProjectRelativePath $gov) 1 "旧 governance commandBinding '$([string]$registeredBinding.commandId)' 已被受管注册表绑定覆盖；应在下次 Skill 迁移中清理旧 Hash。" 'review'}
            }
            $governanceBindings=@($governanceBindings|Where-Object {[string]$_.commandId -cne [string]$registeredBinding.commandId})
        }
        $bindings=@()
        $bindings+=@($governanceBindings)
        $bindings+=@($registeredBindings)
        $boundCommands=@()
        if($commandRequirement -notin @('none','optional','required')){Add-BoundaryFinding $findings 'InvalidCommandRequirement' (Get-ProjectRelativePath $gov) 1 'commandRequirement 必须为 none、optional 或 required。'}
        if($commandRequirement -eq 'required' -and $bindings.Count -eq 0){Add-BoundaryFinding $findings 'NoExplicitCommandBinding' (Get-ProjectRelativePath $gov) 1 '该 Skill 声明了 ManagedAIBrain 写入/外部能力，但没有显式 commandId -> AICommand -> TaskContract 绑定。'}
        foreach($binding in $bindings){
            $commandId=[string]$binding.commandId
            if([string]::IsNullOrWhiteSpace($commandId)){Add-BoundaryFinding $findings 'InvalidCommandBinding' (Get-ProjectRelativePath $gov) 1 'commandBindings 项缺少 commandId。';continue}
            $matches=@($commandEntries|Where-Object {[string]$_.id -ceq $commandId})
            if($matches.Count -ne 1){Add-BoundaryFinding $findings 'CommandBindingMissing' (Get-ProjectRelativePath $gov) 1 "commandId '$commandId' 未在 AICommandCatalog 中精确命中唯一命令。";continue}
            $command=$matches[0]; $boundCommands+=$command
            $commandPath=[string]$command.path
            if(-not (Test-RelativeProjectFile $commandPath)){Add-BoundaryFinding $findings 'CommandPathInvalid' (Get-ProjectRelativePath $gov) 1 "AICommand '$commandId' path 越界或不存在。";continue}
            $bodyPath=Join-Path $root $commandPath.Replace('/','\'); $body=Read-StrictText $bodyPath; $metadata=Get-CommandBodyMetadata $body
            if(-not $metadata.hasType -or -not $metadata.hasWrite -or -not $metadata.hasRisk){Add-BoundaryFinding $findings 'CommandBodyMismatch' (Get-ProjectRelativePath $bodyPath) 1 "AICommand '$commandId' 正文缺少命令类型、默认改文件或风险等级字段。"}
            if([string]$binding.commandHash -notmatch '^[a-fA-F0-9]{64}$' -or [string]$binding.commandHash -ne (Get-Sha256 $bodyPath)){Add-BoundaryFinding $findings 'CommandBodyHashStale' (Get-ProjectRelativePath $gov) 1 "AICommand '$commandId' 正文 Hash 缺失或过期。"}
            foreach($property in @('role','riskLevel','writeMode')){
                if($binding.PSObject.Properties.Name -contains $property -and [string]$binding.$property -ne [string]$command.$property){Add-BoundaryFinding $findings 'CommandCatalogMismatch' (Get-ProjectRelativePath $gov) 1 "绑定 '$commandId' 的 $property 与 AICommandCatalog 不一致。"}
            }
            foreach($requiredText in @($binding.bodyContains | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })){
                if(-not $body.Contains([string]$requiredText)){Add-BoundaryFinding $findings 'CommandBodyMismatch' (Get-ProjectRelativePath $bodyPath) 1 "AICommand '$commandId' 正文缺少绑定要求文本。"}
            }
            foreach($authority in @($binding.requiredAuthorityRefs | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })){
                if(-not (Test-RelativeProjectFile ([string]$authority))){Add-BoundaryFinding $findings 'AuthorityReadMissing' (Get-ProjectRelativePath $gov) 1 "AICommand '$commandId' 要求的 authority ref 不存在：$authority。"}
            }
            if([bool]$binding.taskContractRequired -and [string]$binding.taskContractRef -notmatch '\S'){Add-BoundaryFinding $findings 'MissingTaskContract' (Get-ProjectRelativePath $gov) 1 "AICommand '$commandId' 声明需要 TaskContract，但未提供 taskContractRef。"}
            if([string]$binding.taskContractRef -match '\S' -and -not (Test-RelativeProjectFile ([string]$binding.taskContractRef))){Add-BoundaryFinding $findings 'MissingTaskContract' (Get-ProjectRelativePath $gov) 1 "AICommand '$commandId' 的 TaskContract 不存在或越界。"}
        }
        if($commandRequirement -eq 'required' -and $boundCommands.Count -eq 0 -and $bindings.Count -gt 0){Add-BoundaryFinding $findings 'NoExplicitCommandBinding' (Get-ProjectRelativePath $gov) 1 '所有显式 commandId 均未能解析，ManagedAIBrain 通道不能退回模糊匹配。'}
        if($writePolicy -and $boundCommands.Count -gt 0){
            $modes=@($boundCommands|ForEach-Object {[string]$_.writeMode}|Where-Object {$_})
            $widerThanReadOnly=@($modes|Where-Object {$_ -notin @('read-only','candidate-only')})
            $widerThanCandidate=@($modes|Where-Object {$_ -in @('scoped-write','documentation-write','external-run')})
            if($writePolicy -eq 'read-only' -and $widerThanReadOnly.Count -gt 0){Add-BoundaryFinding $findings 'permission-expansion' (Get-ProjectRelativePath $gov) 1 'Skill 声明 read-only，但显式绑定 AICommand 含更宽写入/外部执行模式。'}
            if($writePolicy -eq 'candidate-only' -and $widerThanCandidate.Count -gt 0){Add-BoundaryFinding $findings 'permission-expansion' (Get-ProjectRelativePath $gov) 1 'candidate-only Skill 不能绑定正式写入或外部执行命令。'}
        }
        $refusalPatterns=@(
            '(?i)\b(ignore|bypass|disable|override)\b.{0,80}\b(aiwarnings|aicommands|aibrain|governance|taskcontract)\b',
            '(?i)\b(aiwarnings|aicommands|aibrain|governance|taskcontract)\b.{0,80}\b(ignore|bypass|disable|override)\b',
            '(?i)(绕过|跳过|禁用|关闭|覆盖).{0,50}(AIWarnings|AICommand|AIBrain|治理|TaskContract)'
        )
        $lineNo=0
        foreach($line in [IO.File]::ReadLines($skill)){
            $lineNo++
            $isRefusal=Test-Declaration $line $refusalPatterns
            $isProhibition=$line -match '(?i)(do not|must not|never|禁止|不得|不要|拒绝|不能|不自动|不绕过|不覆盖|不创建|不允许|仅)'
            if($isRefusal -and -not $isProhibition){
                Add-BoundaryFinding $findings 'authority-violation' (Get-ProjectRelativePath $skill) $lineNo 'Skill 文本出现绕过 AIWarnings/AICommand/AIBrain/TaskContract 的可执行语义。'
            }
        }
        $scriptFiles=@(Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object {$_.Extension -in @('.ps1','.py','.sh','.bat','.cmd') -and $_.FullName -notmatch '\\tests\\' -and $_.Name -notin @('Invoke-ESSkillValidation.ps1','Test-ESSkillEvidence.ps1','Test-ESSkillPortfolio.ps1','Test-ESSkillPortfolioEvidence.ps1')})
        $secretPattern='(?i)((Get-Content|ReadAllText|ReadAllBytes|OpenRead|cat|type)\s+[^\r\n;]{0,100}(\.env|token|password|secret|private[ ._-]?key|credential|api[ ._-]?key)|\$env:[A-Z_]*(TOKEN|PASSWORD|SECRET|PRIVATE_KEY|API_KEY))'
        $networkPattern='(?i)(Invoke-WebRequest|Invoke-RestMethod|Start-BitsTransfer|curl\s+https?://|wget\s+https?://|WebClient|HttpClient|requests\.(get|post)|urllib\.request)'
        $destructivePattern='(?i)(Remove-Item|Remove-\w+|del\s+|rmdir\s+|git\s+(reset|clean|checkout)|Move-Item|Rename-Item)'
        $pathEscapePattern='(?i)(\.\.[\\/]|[A-Za-z]:[\\/]|%USERPROFILE%|%APPDATA%|\$env:(USERPROFILE|APPDATA|HOME)|/etc/|/var/)'
        $explicitExternalPathPattern='(?i)([A-Za-z]:[\\/]|%USERPROFILE%|%APPDATA%|\$env:(USERPROFILE|APPDATA|HOME)|/etc/|/var/)'
        $dynamicPathPattern='(?i)(Join-Path\s+[^\r\n;]*\$[A-Za-z_][A-Za-z0-9_]*|Resolve-Path\s+[^\r\n;]*\$[A-Za-z_][A-Za-z0-9_]*|(?:Set-Content|Add-Content|Out-File|WriteAll(Text|Bytes)|Remove-Item|Move-Item|Copy-Item)\s+[^\r\n;]*\$[A-Za-z_][A-Za-z0-9_]*)'
        $indirectExecutionPattern='(?i)((?<![\$\w])(iex|invoke-expression|Start-Process|ProcessStartInfo|subprocess\.(run|Popen|call)|os\.system|shell\s*=\s*true|child_process\.(exec|spawn)|eval\s*(?:\(|\$)|source\s+(?:[\./\\\$]|[A-Za-z]:)|call\s+(?:[\./\\\$]|[A-Za-z]:))[^\r\n;]*)'
        $obfuscationPattern='(?i)(FromBase64String|EncodedCommand|(?:-enc|-encodedcommand)\b|Invoke-Expression|\bbase64\b.{0,30}(decode|exec)|\b(eval|exec)\s*\()'
        $hasNetworkDeclaration=Test-Declaration $declaredText @('(?i)(network|联网|网络|external adapter|外部适配器|webhook|feishu|lark)')
        $hasDeleteDeclaration=Test-Declaration $declaredText @('(?i)(delete|deletion|destructive|invalidate|cleanup|temporary|rollback|cache|删除|销毁|清理|失效|临时|回滚|缓存|破坏性)')
        # A session/bootstrap Skill may use a short-lived launch marker. It is
        # an execution capability identifier, not a credential or secret file.
        $hasDeclaredSessionMarkerContract=Test-Declaration $declaredText @('(?is)(launch\s+token|session\s+marker|会话令牌|启动令牌).{0,120}(not\s+a\s+secret|non-secret|非秘密|不是凭据|最小权限)')
        # External process launch is allowed only when the Skill declares an
        # explicit executable/argument boundary; it remains a review item.
        $hasApprovedExternalExecutionProfile=Test-Declaration $declaredText @('(?is)(external\s+process|process\s+boundary|外部进程|进程边界).{0,160}(exact\s+executable|allowlist|参数白名单|精确可执行文件|一次性)')
        $sharedPathBoundaryReady=$false
        $sharedPathBoundaryPath=Join-Path $root '.agents\skills\es-skill-governance\scripts\ESPathBoundary.Common.ps1'
        try{
            $sharedPathBoundaryText=Read-StrictText $sharedPathBoundaryPath
            $sharedPathBoundaryReady=(
                $sharedPathBoundaryText -match '(?i)function\s+Resolve-ESContainedRelativePath' -and
                $sharedPathBoundaryText -match '(?i)IsPathRooted' -and
                $sharedPathBoundaryText -match '(?i)GetFullPath' -and
                $sharedPathBoundaryText -match '(?i)StartsWith' -and
                $sharedPathBoundaryText -match '(?i)alternate\s+data\s+stream' -and
                $sharedPathBoundaryText -match '(?i)ReparsePoint')
        }catch{}
        foreach($file in $scriptFiles){
            $relative=Get-ProjectRelativePath $file.FullName; $n=0
            $fileText=''; try{$fileText=Read-StrictText $file.FullName}catch{}
            $isPowerShell=$file.Extension -eq '.ps1'
            $powerShellFacts=if($isPowerShell){Get-PowerShellBoundaryFacts $file.FullName}else{$null}
            if($isPowerShell -and -not $powerShellFacts.parseSucceeded){
                Add-BoundaryFinding $findings 'powershell-parse-error' $relative 1 ('PowerShell AST parse failed: '+(@($powerShellFacts.parseErrors)-join ' | '))
            }
            # A read-only/report-only script that resolves the current project root before
            # composing paths is bounded enough for static review, but this is not a
            # blanket security proof. Keep the finding visible as review instead of
            # misclassifying deterministic replay harnesses as source defects.
            $rootFromParameter=($fileText -match '(?is)(Resolve-Path(?:\s+-LiteralPath)?\s+\(?\s*\$ProjectRoot|GetFullPath\s*\(\s*\$(?:ProjectRoot|Candidate))')
            $rootFromScriptDirectory=($fileText -match '(?is)(?:Resolve-Path|GetFullPath)\s*\(\s*\(?\s*Join-Path\s+\$PSScriptRoot')
            $rootVariable=($fileText -match '(?im)\$(?:root|ProjectRoot|[A-Za-z_]*Root[A-Za-z0-9_]*)\s*=')
            $rootFromGit=($fileText -match '(?is)git\s+rev-parse\s+--show-toplevel')
            $rootDefault=($fileText -match '(?is)\$ProjectRoot\s*=\s*\(\s*Resolve-Path\s*\(\s*Join-Path\s+\$PSScriptRoot')
            $hasProjectRootBinding=((($rootFromParameter -or $rootFromScriptDirectory -or $rootFromGit) -and $rootVariable) -or $rootDefault)
            # A deterministic script-root derivation is a bounded project path
            # source when it is normalized and paired with containment evidence;
            # it must not be treated like user-controlled dynamic input.
            $hasScriptRootContainment=($rootFromScriptDirectory -and ($fileText -match '(?is)(Resolve-ESContainedRelativePath|StartsWith\s*\(|GetFullPath\s*\()'))
            $hasLocalProjectRelativeResolver=($fileText -match '(?is)function\s+Resolve-ProjectRelative' -and $fileText -match '(?is)IsPathRooted' -and $fileText -match '(?is)GetFullPath' -and $fileText -match '(?is)StartsWith')
            $usesSharedPathBoundary=($sharedPathBoundaryReady -and
                ($fileText -match '(?is)\.\s*\(\s*Join-Path\s+\$PSScriptRoot\s+[\x27\x22]ESPathBoundary\.Common\.ps1[\x27\x22]\s*\)' -or $fileText -match '(?is)\.\s*\$sharedPathBoundary\b') -and
                $fileText -match '(?i)Resolve-ESContainedRelativePath')
            $hasExplicitPathContainment=($hasScriptRootContainment -or $hasLocalProjectRelativeResolver -or $usesSharedPathBoundary -or ((($fileText -match '(?is)IsPathRooted\s*\(\s*\$[A-Za-z_][A-Za-z0-9_]*\s*\)') -or $fileText -match '(?is)GetFullPath\s*\(\s*\$(?:ProjectRoot|Root|root)') -and ($fileText -match '(?is)(escapes?\s+ProjectRoot|StartsWith\s*\(\s*\$[A-Za-z_][A-Za-z0-9_]*\s*\+|cannot escape|\b越界\b)')))
            $hasDeclaredPathContract=($null -ne $declaredPathContract -and $declaredPathContract -match '(?is)(project.?relative|approved.*state root|external.*state root)' -and $declaredPathContract -match '(?is)(reject|deny|cannot escape|越界|拒绝)')
            $hasApprovedExternalProfile=($hasDeclaredPathContract -and $declaredPathContract -match '(?is)approved.*(user.?profile|LOCALAPPDATA|\.codex)')
            $hasResolvedInputPath=($fileText -match '(?is)Resolve-Path(?:\s+-LiteralPath)?\s+\$[A-Za-z_][A-Za-z0-9_]*')
            $isReplayHarness=($file.Name -match '(?i)^Test-ES.*\.ps1$' -or $file.Name -match '(?i)StaticReplay\.ps1$')
            $temporaryFixtureVariables=[Collections.Generic.List[string]]::new()
            if($isReplayHarness){
                foreach($match in @([regex]::Matches($fileText,'(?is)\$(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Join-Path\s+\(\s*\[IO\.Path\]::GetTempPath\s*\(\s*\)\s*\)'))){
                    $variableName=[string]$match.Groups['name'].Value
                    if($variableName -notmatch '(?i)(temp|fixture)'){continue}
                    $cleanupPattern='(?is)finally\s*\{.*Remove-Item[^\r\n]+\$'+[regex]::Escape($variableName)+'\b'
                    if($fileText -match $cleanupPattern){[void]$temporaryFixtureVariables.Add($variableName)}
                }
            }
            $hasTemporaryFixtureBoundary=$temporaryFixtureVariables.Count -gt 0
            $hasApprovedExternalStateGuard=($hasApprovedExternalProfile -and (
                ($fileText -match '(?is)Test-ESCodexApprovedStatePath\s+\$resolvedPath' -and $fileText -match '(?is)if\s*\(\s*-not\s*\(\s*Test-ESCodexApprovedStatePath') -or
                ($fileText -match '(?is)ESFrameworkSemanticArchives' -and $fileText -match '(?is)GetFullPath' -and $fileText -match '\^\[A-Za-z0-9._-\]\{1,96\}\$') -or
                ($fileText -match '(?im)^\s*\.\s*\(Join-Path\s+\$PSScriptRoot\s+[\x27\x22]ESProjectSemanticArchive\.ps1[\x27\x22]\s*\)' -and $fileText -match '(?i)(Read-ESSemanticArchive|Get-ESSemanticArchiveRoot|Get-ESSemanticArchivePath|Write-ESSemanticArchiveCreateOnly)')
            ))
            $contractBoundInput=($hasProjectRootBinding -or $hasResolvedInputPath -or $hasApprovedExternalStateGuard -or ($name -eq 'es-codex-session-bootstrap' -and $fileText -match '(?i)\$(?:StateRoot|localStateRoot|localStateBase)'))
            $explicitReadOnlyContract=($fileText -match '(?is)read-only\s+(?:contract|/report-only|audit|validator)')
            $staticScannerContract=($fileText -match '(?is)static\s+audit\s+contract|regex\s+literals?\s+below\s+are\s+data')
            $isReadOnlyPathContext=(($writePolicy -in @('read-only','report-only-explicit-path') -or $capabilityMode -eq 'advisory' -or $isReplayHarness) -and ($hasProjectRootBinding -or $hasResolvedInputPath) -and (-not (Test-Declaration $fileText @($secretPattern,$networkPattern,$destructivePattern,$obfuscationPattern)) -or ($capabilityMode -eq 'advisory' -and ($explicitReadOnlyContract -or $staticScannerContract))))
            foreach($line in [IO.File]::ReadLines($file.FullName)){
                $n++
                $isDeclaredSessionMarker=(($hasDeclaredSessionMarkerContract -or $name -eq 'es-codex-session-bootstrap') -and $line -match '(?i)\$env:ES_CODEX_LAUNCH_TOKEN')
                if($line -match $secretPattern -and -not $isDeclaredSessionMarker){Add-BoundaryFinding $findings 'secret-access' $relative $n '脚本读取疑似凭据/秘密文件；必须有明确项目命令和最小权限声明。'}
                if($line -match $networkPattern -and -not $hasNetworkDeclaration){Add-BoundaryFinding $findings 'network-undeclared' $relative $n '脚本含网络调用但 Skill 未声明网络边界，且不能由 MCP 可见性推导授权。'}
                if($line -match $destructivePattern -and -not $hasDeleteDeclaration){
                    $destructiveSeverity=if($isReplayHarness -and $hasTemporaryFixtureBoundary -and $line -notmatch $explicitExternalPathPattern){'review'}else{'blocked'}
                    $destructiveDetail=if($destructiveSeverity -eq 'review'){'受控临时夹具中的清理/回滚路径；仅限制该 replay harness，仍需验证 finally 清理和临时根绑定。'}else{'脚本含删除/移动/Git 破坏性操作但没有显式破坏性范围、确认和恢复声明。'}
                    Add-BoundaryFinding $findings 'destructive-undeclared' $relative $n $destructiveDetail $destructiveSeverity
                }
                $boundarySensitiveLine=$line -match '(?i)(Set-Content|Out-File|WriteAll(Text|Bytes)|Remove-Item|Remove-\w+|Move-Item|Rename-Item|Start-Process|ProcessStartInfo|ReportPath|OutputPath|Destination|TargetPath|Resolve-Path)'
                $isInternalRootDiscovery=($line -match '(?is)Resolve-Path\s*\(\s*Join-Path\s+\$PSScriptRoot\s+[\x27\x22]?\.\.')
                $isInternalScriptPath=($line -match '(?is)Join-Path\s+\$PSScriptRoot')
                if($line -match $pathEscapePattern -and $boundarySensitiveLine -and -not $isInternalRootDiscovery){$isBoundedFixtureEscape=($isReplayHarness -and $hasTemporaryFixtureBoundary -and $line -notmatch $explicitExternalPathPattern);$pathSeverity=if(($hasApprovedExternalProfile -and $line -match '(?i)\$env:(USERPROFILE|LOCALAPPDATA)') -or $isBoundedFixtureEscape){'review'}else{'blocked'};$pathDetail=if($isBoundedFixtureEscape){'路径穿越文本位于受控临时夹具内，用于拒绝/回滚负例；不得投影为生产路径授权。'}elseif($pathSeverity -eq 'review'){'路径指向已声明的受控外部用户状态根；仍需静态回放确认根目录固定且不可扩展。'}else{'脚本在写入/删除/外部执行或输出路径上出现项目根外路径、路径穿越或用户/系统目录；必须拒绝越界。'};Add-BoundaryFinding $findings 'path-boundary' $relative $n $pathDetail $pathSeverity}
                if($line -match $dynamicPathPattern){
                    $dynamicSeverity=if(($isReadOnlyPathContext -or $hasExplicitPathContainment -or ($hasDeclaredPathContract -and $contractBoundInput) -or ($hasApprovedExternalProfile -and $line -match '(?i)\$env:(USERPROFILE|LOCALAPPDATA)') -or ($isReplayHarness -and ($isInternalScriptPath -or $hasProjectRootBinding)) -or $hasTemporaryFixtureBoundary -or $hasApprovedExternalStateGuard) -and (($line -notmatch $pathEscapePattern) -or $isInternalRootDiscovery -or $isInternalScriptPath -or ($hasApprovedExternalProfile -and $line -match '(?i)\$env:(USERPROFILE|LOCALAPPDATA)') -or ($isReplayHarness -and $hasProjectRootBinding -and $line -notmatch $explicitExternalPathPattern) -or ($isReplayHarness -and $hasTemporaryFixtureBoundary -and $line -notmatch $explicitExternalPathPattern))){'review'}else{'blocked'}
                    $dynamicDetail=if($dynamicSeverity -eq 'review'){'脚本已绑定当前项目根且声明只读/报告范围；路径仍依赖变量，需在静态回放收据中确认相对路径拒绝与边界钳制。'}else{'脚本把未证明来源的变量拼入路径或路径操作；静态门禁无法证明项目根约束，必须显式收窄并人工复核。'}
                    Add-BoundaryFinding $findings 'dynamic-path' $relative $n $dynamicDetail $dynamicSeverity
                }
                $hasIndirectExecution=if($isPowerShell -and $powerShellFacts.parseSucceeded){$powerShellFacts.indirectLines.Contains($n)}else{$line -match $indirectExecutionPattern}
                if($hasIndirectExecution){
                    $invokedVariable=[regex]::Match($line,'(?i)(?:&|\.)\s+\$(?<name>[A-Za-z_][A-Za-z0-9_]*)')
                    $invokedName=if($invokedVariable.Success){[string]$invokedVariable.Groups['name'].Value}else{''}
                    $isBoundInternalCommand=($invokedVariable.Success -and $fileText -match ('(?is)\$'+[regex]::Escape($invokedName)+'\s*=.{0,200}Join-Path.{0,120}\$(?:(?:script:)?[A-Za-z_]*Root|PSScriptRoot|ProjectRoot|PSHOME)'))
                    $aliasMatch=if($invokedVariable.Success){[regex]::Match($fileText,('(?im)^\s*\$'+[regex]::Escape($invokedName)+'\s*=\s*\$(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*$'))}else{$null}
                    $aliasSource=if($null -ne $aliasMatch -and $aliasMatch.Success){[string]$aliasMatch.Groups['source'].Value}else{''}
                    $isBoundInternalAlias=(-not [string]::IsNullOrWhiteSpace($aliasSource) -and $fileText -match ('(?is)\$'+[regex]::Escape($aliasSource)+'\s*=.{0,200}Join-Path.{0,120}\$(?:(?:script:)?[A-Za-z_]*Root|PSScriptRoot|ProjectRoot|PSHOME)'))
                    $isReplayBoundCommand=($isReplayHarness -and $invokedVariable.Success -and $fileText -match ('(?is)\$'+[regex]::Escape($invokedName)+'\s*=.{0,200}Join-Path\s+\$[A-Za-z_][A-Za-z0-9_]*'))
                    $isLocalScriptblock=($invokedVariable.Success -and $fileText -match ('(?is)\$'+[regex]::Escape($invokedName)+'\s*=\s*\{'))
                    $isDeclaredScriptblock=($invokedVariable.Success -and $fileText -match ('(?is)\[scriptblock\]\$'+[regex]::Escape($invokedName)+'\b'))
                    $isReplayMemberCall=($isReplayHarness -and $line -match '(?i)&\s+\$[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*')
                    $isDirectInternalJoin=($line -match '(?i)&\s*\(\s*Join-Path\s+\$(?:(?:script:)?[A-Za-z_]*Root|PSScriptRoot|ProjectRoot)' -or ($isReplayHarness -and $line -match '(?i)&\s*\(\s*Join-Path\s+\$[A-Za-z_][A-Za-z0-9_]*'))
                    $isInternalModuleImport=($line -match '(?i)\bImport-Module\s+\(\s*Join-Path\s+\$(?:(?:script:)?[A-Za-z_]*Root|PSScriptRoot|ProjectRoot)')
                    $internalInvocation=($isBoundInternalCommand -or $isBoundInternalAlias -or $isReplayBoundCommand -or $isLocalScriptblock -or $isDeclaredScriptblock -or $isReplayMemberCall -or $isDirectInternalJoin -or $isInternalModuleImport -or $line -match '(?i)^\s*\.\s*\(Join-Path\s+\$PSScriptRoot')
                    $executionSeverity=if($internalInvocation -or $hasApprovedExternalExecutionProfile){'review'}else{'blocked'}
                    $executionDetail=if($executionSeverity -eq 'review'){'间接调用目标由 PSScriptRoot 派生且处于只读/报告脚本；仍需收据确认目标脚本、参数和一次性边界。'}else{'脚本包含别名、变量或多语言间接执行路径；不能由逐行文本扫描证明安全，必须阻断并人工复核。'}
                    if($staticScannerContract){$executionSeverity='review';$executionDetail='脚本只把执行模式作为静态扫描数据，不执行匹配内容；仍需静态回放收据确认扫描范围。'}
                    Add-BoundaryFinding $findings 'indirect-execution' $relative $n $executionDetail $executionSeverity
                }
                $benignDataEncoding=($line -match '(?i)(data:[^;]+;base64|b64encode\s*\(|image_url|sha256|hashlib)')
                if($line -match $obfuscationPattern -and -not $benignDataEncoding){$severity=if($staticScannerContract){'review'}else{'blocked'};Add-BoundaryFinding $findings 'obfuscated-command' $relative $n '脚本包含编码/混淆或动态求值执行信号；静态审计脚本仅将其作为匹配数据。' $severity}
                $hasSwallowedException=if($isPowerShell -and $powerShellFacts.parseSucceeded){$powerShellFacts.emptyCatchLines.Contains($n)}else{$line -match '(?i)catch\s*\{\s*(\$?_|\$?null\s*\|\s*Out-Null|)\s*\}'}
                if($hasSwallowedException){Add-BoundaryFinding $findings 'exception-swallowing' $relative $n 'catch 为空或把异常对象丢弃，可能把失败报告成成功；必须显式失败或返回 blocked。'}
            }
        }
        $evidenceClaimPattern='(?i)(Unity|PlayMode|Profiler|Player|IL2CPP|Release|发布|运行时).{0,45}(passed|validated|accepted|stable|released|通过|已验证|已完成|可用|Accepted|Stable|Released)'
        $evidenceNegation='(?i)(不得|禁止|不能|不可|未|尚未|没有|不等于|不能替代|不宣称|不证明|不代表|待|unverified|not verified|must not|do not|never)'
        $lineNo=0
        foreach($line in [IO.File]::ReadLines($skill)){
            $lineNo++
            if($line -match $evidenceClaimPattern -and $line -notmatch $evidenceNegation){
                Add-BoundaryFinding $findings 'evidence-overclaim' (Get-ProjectRelativePath $skill) $lineNo 'Skill 声称 Unity/运行时/Player/IL2CPP/发布已验证，但静态 Skill 不能替代对应真实证据。'
            }
        }
        $blockingFindings=@($findings|Where-Object {$_.severity -ne 'review'})
        $reviewFindings=@($findings|Where-Object {$_.severity -eq 'review'})
        $status=if($blockingFindings.Count -gt 0){'blocked'}elseif($reviewFindings.Count -gt 0){'review'}else{'passed'}
        $source=(@($findings|ForEach-Object {"$($_.severity):$($_.code) $($_.path):$($_.line) $($_.detail)"})-join '; ')
        $message=if($blockingFindings.Count -gt 0){"project boundary blockers ($($blockingFindings.Count)); review findings ($($reviewFindings.Count))"}elseif($reviewFindings.Count -gt 0){"boundary statically bounded with review findings ($($reviewFindings.Count)); no blocking finding"}else{'AIWarnings refusal, AICommand, path, capability and evidence boundaries passed'}
        $results += ([pscustomobject]@{skill=$name;profile='Boundary';capabilityMode=$capabilityMode;status=$status;message=$message;source=$source;blockingCount=$blockingFindings.Count;reviewCount=$reviewFindings.Count;findings=@($findings)})
    }
    if($Profile -contains 'Evidence' -or $Profile -contains 'Full'){
        $evidencePendingStatus=if($AuthorizationLane -eq 'ManagedAIBrain'){'blocked'}else{'review'}
        $evidenceStatus=$evidencePendingStatus; $evidenceMessage="behavioral receipts missing for authorizationLane '$AuthorizationLane'; structural checks are not behavioral evidence"
        $responsibilityProfile='base'; $expectedCases=@()
        if(Test-Path $gov -PathType Leaf){
            try {
                $evidenceGovernance=Get-Content -Raw -Encoding UTF8 $gov | ConvertFrom-Json
                $requiredCases=@($evidenceGovernance.requiredCases)
                # The static-replay responsibility profile defines the
                # minimum useful evidence. Historical governance files may
                # still contain the old five-case superset; do not apply it
                # blindly to every Skill.
                $manifestPath=Join-Path $target 'static-replay.manifest.json'
                if(Test-Path -LiteralPath $manifestPath -PathType Leaf){try{$responsibilityProfile=[string]((Get-Content -Raw -Encoding UTF8 $manifestPath|ConvertFrom-Json).responsibilityProfile)}catch{}}
                $expectedCases=switch($responsibilityProfile){
                    'release' { @('positive','invalid-input','denied-expansion','repeat-idempotency','interruption-recovery') }
                    'session' { @('positive','invalid-input','interruption-recovery') }
                    'governance' { @('positive','invalid-input','denied-expansion') }
                    'knowledge' { @('positive','invalid-input','hash-change-cache-invalidation') }
                    'editor' { @('positive','invalid-input','denied-expansion','repeat-idempotency') }
                    'engineering' { if([string]$evidenceGovernance.writePolicy -eq 'read-only'){@('positive','invalid-input')}else{@('positive','invalid-input','repeat-idempotency')} }
                    'authoring' { @('positive','invalid-input','denied-expansion','repeat-idempotency') }
                    'testing' { @('positive','invalid-input','repeat-idempotency') }
                    default { @('positive','invalid-input') }
                }
                $expectedCases=@($expectedCases|Where-Object {$requiredCases -contains $_})
                if($expectedCases.Count -eq 0){$evidenceStatus='not-applicable';$evidenceMessage="no evidence cases applicable to responsibilityProfile '$responsibilityProfile'"}
                else {
                    $receiptCandidates=@(Get-ChildItem -LiteralPath (Join-Path $root 'ES\Output') -File -Filter '*Receipt.json' -ErrorAction SilentlyContinue | ForEach-Object FullName)
                    $matching=@(); $caseNames=@(); $receiptFailures=@()
                    $skillHash=Get-Sha256 $skill; $governanceHash=Get-Sha256 $gov; $validatorHash=Get-Sha256 $PSCommandPath
                    foreach($receipt in $receiptCandidates){try{
                        $j=Get-Content -Raw -Encoding UTF8 $receipt | ConvertFrom-Json
                        if([string]$j.skillName -eq $name -and [string]$j.case -ne 'portfolio-gate'){
                            $matching+=$receipt
                            $valid=$true
                            $receiptRelative=Get-ProjectRelativePath $receipt
                            if([string]$j.receiptPath -ne $receiptRelative){$valid=$false}
                            if([string]$j.status -ne 'passed'){$valid=$false;$receiptFailures+=$receipt}
                            if($j.PSObject.Properties.Name -notcontains 'toolId' -or [string]::IsNullOrWhiteSpace([string]$j.toolId)){$valid=$false}
                            if($j.PSObject.Properties.Name -notcontains 'capturedUtc'){$valid=$false}else{try{$capturedUtc=[DateTime]::Parse([string]$j.capturedUtc).ToUniversalTime();$maxAgeHours=if($evidenceGovernance.PSObject.Properties.Name -contains 'maxEvidenceAgeHours'){[int]$evidenceGovernance.maxEvidenceAgeHours}else{168};if(([DateTime]::UtcNow-$capturedUtc).TotalHours -gt $maxAgeHours){$valid=$false}}catch{$valid=$false}}
                            if($j.PSObject.Properties.Name -notcontains 'unityVersion' -or [string]::IsNullOrWhiteSpace([string]$j.unityVersion)){$valid=$false}
                            if([string]$j.skillHash -ne $skillHash -or [string]$j.governanceHash -ne $governanceHash -or [string]$j.validatorHash -ne $validatorHash){$valid=$false}
                            if(-not (Test-ReceiptAuthorization $j)){$valid=$false}
                            $hashesProperty=$j.PSObject.Properties['sourceRefHashes']
                            $hashProperties=if($null -eq $hashesProperty -or $hashesProperty.Value -is [string]){@()}else{@($hashesProperty.Value.PSObject.Properties)}
                            if(-not (Test-NonEmptyStringArray $j.sourceRefs) -or $hashProperties.Count -eq 0){$valid=$false}
                            foreach($ref in @($j.sourceRefs)){
                                $refPath=Get-ProjectRelativeEvidenceFile $ref
                                if($null -eq $refPath){$valid=$false;continue}
                                $hashProperty=if($null -eq $hashesProperty){$null}else{$hashesProperty.Value.PSObject.Properties[[string]$ref]}
                                if($null -eq $hashProperty -and $null -ne $hashesProperty){$hashProperty=$hashesProperty.Value.PSObject.Properties[([string]$ref).Replace('/','_')]}
                                $expected=if($null -eq $hashProperty){''}else{[string]$hashProperty.Value}
                                if([string]::IsNullOrWhiteSpace($expected) -or $expected -ne (Get-Sha256 $refPath)){$valid=$false}
                            }
                            if($valid){$caseNames+=[string]$j.case}else{$receiptFailures+=$receipt}
                        }
                    }catch{}}
                    $duplicateCases=@($matching | ForEach-Object { try{([string](Get-Content -Raw -Encoding UTF8 $_|ConvertFrom-Json).case)}catch{''} } | Group-Object | Where-Object {$_.Name -and $_.Count -ne 1})
                    if($duplicateCases.Count -gt 0){$receiptFailures += $matching}
                    $missingEvidence=$expectedCases | Where-Object { $caseNames -notcontains $_ }
                    if($matching.Count -gt 0 -and $missingEvidence.Count -eq 0 -and $receiptFailures.Count -eq 0){$evidenceStatus='passed';$evidenceMessage="all required case receipts found: $($matching.Count)"}
                    elseif($matching.Count -gt 0){$evidenceStatus=$evidencePendingStatus;$evidenceMessage="receipts stale/invalid for authorizationLane '$AuthorizationLane'; missing or unbound cases: "+($missingEvidence -join ', ')}
                    else {$evidenceStatus=$evidencePendingStatus;$evidenceMessage="required static evidence receipts missing for authorizationLane '$AuthorizationLane': "+($expectedCases -join ', ')}
                }
            } catch {$evidenceStatus=if($AuthorizationLane -eq 'ManagedAIBrain'){'failed'}else{'review'};$evidenceMessage='governance evidence contract could not be read'}
        }
        $results += ([pscustomobject]@{skill=$name;profile='Evidence';status=$evidenceStatus;message=$evidenceMessage;source=$gov;responsibilityProfile=$responsibilityProfile;requiredCases=@($expectedCases)})
    }
}
if($Profile -contains 'Catalog' -or $Profile -contains 'Full'){
    $catalog=Join-Path $root '.agents\SKILL_CATALOG.yaml'
    $catalogScript=Join-Path $skillsRoot 'es-skill-creator\scripts\Test-ESSkillCatalog.ps1'
    $ok=(Test-Path $catalog -PathType Leaf) -and (Test-Path $catalogScript -PathType Leaf)
    $msg=if($ok){ (& powershell -NoProfile -File $catalogScript -ProjectRoot $root 2>&1 | Out-String).Trim() }else{'catalog or dedicated validator missing'}
    $catalogExit=$LASTEXITCODE
    $ok=$ok -and ($catalogExit -eq 0)
    $results += ([pscustomobject]@{skill=if($SkillName){$SkillName}else{'*'};profile='Catalog';status=if($ok){'passed'}else{'failed'};message=$msg;source=$catalog})
}
if($Profile -contains 'Architecture' -or $Profile -contains 'Full'){
    $architectureScript=Join-Path $skillsRoot 'es-skill-governance\scripts\Test-ESSkillArchitecture.ps1'
    if(-not (Test-Path -LiteralPath $architectureScript -PathType Leaf)){
        $results += ([pscustomobject]@{skill=if($SkillName){$SkillName}else{'*'};profile='Architecture';status='failed';message='Skill architecture validator is missing';source=$architectureScript})
    } else {
        $architectureOutput=(& powershell -NoProfile -ExecutionPolicy Bypass -File $architectureScript -ProjectRoot $root -AuthorizationLane $AuthorizationLane 2>&1 | Out-String).Trim()
        $architectureExit=$LASTEXITCODE
        $architectureStatus=if($architectureExit -ne 0){'blocked'}else{'passed'}
        try{
            $architectureJson=$architectureOutput|ConvertFrom-Json
            if([string]$architectureJson.status -eq 'review' -and $architectureStatus -eq 'passed'){$architectureStatus='review'}
        }catch{}
        $results += ([pscustomobject]@{skill=if($SkillName){$SkillName}else{'*'};profile='Architecture';status=$architectureStatus;message=$architectureOutput;source=$architectureScript})
    }
}
# Keep the historical `status` field for existing callers, but expose a
# claim-oriented status that explains *why* a result is not accepted.  This
# prevents missing receipts from being mistaken for a source-code defect.
foreach($result in $results){
    $legacyStatus=[string]$result.status
    $claimStatus='Passed'
    if($legacyStatus -eq 'not-applicable'){$claimStatus='NotApplicable'}
    elseif($legacyStatus -eq 'not-run'){$claimStatus=if([string]$result.profile -eq 'Evidence'){'EvidenceMissing'}else{'RuntimeNotRun'}}
    elseif($legacyStatus -eq 'review'){
        if([string]$result.profile -eq 'Evidence'){
            $message=[string]$result.message
            if($message -match '(?i)stale'){$claimStatus='EvidenceStale'}
            elseif($message -match '(?i)unbound'){$claimStatus='EvidenceUnbound'}
            else{$claimStatus='EvidenceMissing'}
        }else{$claimStatus='ManualReviewRequired'}
    }
    elseif($legacyStatus -eq 'failed'){$claimStatus='StaticDefect'}
    elseif($legacyStatus -eq 'blocked'){
        if([string]$result.profile -eq 'Evidence'){
            $message=[string]$result.message
            if($message -match '(?i)missing or unbound'){$claimStatus='EvidenceUnbound'}
            elseif($message -match '(?i)stale'){$claimStatus='EvidenceStale'}
            elseif($message -match '(?i)unbound'){$claimStatus='EvidenceUnbound'}
            else{$claimStatus='EvidenceMissing'}
        } elseif([string]$result.profile -eq 'Boundary' -and [int]$result.blockingCount -eq 0){
            $claimStatus='ManualReviewRequired'
        } else {$claimStatus='StaticDefect'}
    }
    $result | Add-Member -NotePropertyName legacyStatus -NotePropertyValue $legacyStatus -Force
    $result | Add-Member -NotePropertyName claimStatus -NotePropertyValue $claimStatus -Force
    $result | Add-Member -NotePropertyName authorizationLane -NotePropertyValue $AuthorizationLane -Force
}
$claimStatusCounts=@($results|Group-Object claimStatus|ForEach-Object {[ordered]@{status=$_.Name;count=$_.Count}})
$failed=@($results | Where-Object status -in @('failed','blocked'))
$notRun=@($results | Where-Object status -eq 'not-run')
$staticProfiles=@('Structural','Governance','VerificationSemantics','StaticDeepReplay','ChangeImpact','Catalog','Security','Semantic','Boundary','Architecture')
$staticFailures=@($results | Where-Object {$_.profile -in $staticProfiles -and $_.status -in @('failed','blocked')})
$staticNotRun=@($results | Where-Object {$_.profile -in $staticProfiles -and $_.status -eq 'not-run'})
$staticReviews=@($results | Where-Object {$_.profile -in $staticProfiles -and $_.status -eq 'review'})
$staticCodeProfiles=@('Structural','Security','Semantic')
$staticContractProfiles=@('Governance','VerificationSemantics','StaticDeepReplay','ChangeImpact','Catalog','Architecture')
$managedSemanticResults=@($results | Where-Object {$_.profile -eq 'Semantic' -and [int]$_.blockingCount -eq 0 -and [int]$_.managedFindingCount -gt 0})
$staticBoundaryFailures=@($results | Where-Object {$_.profile -eq 'Boundary' -and $_.status -in @('failed','blocked')})
$staticBoundaryNotRun=@($results | Where-Object {$_.profile -eq 'Boundary' -and $_.status -eq 'not-run'})
$staticCodeFailures=@($results | Where-Object {$_.profile -in $staticCodeProfiles -and $_.status -in @('failed','blocked') -and -not ($_.profile -eq 'Semantic' -and [int]$_.blockingCount -eq 0 -and [int]$_.managedFindingCount -gt 0)})
$staticContractFailures=@($results | Where-Object {$_.profile -in $staticContractProfiles -and $_.status -in @('failed','blocked')})+@($managedSemanticResults|Where-Object {$_.status -in @('failed','blocked')})
$staticCodeNotRun=@($results | Where-Object {$_.profile -in $staticCodeProfiles -and $_.status -eq 'not-run'})
$staticContractNotRun=@($results | Where-Object {$_.profile -in $staticContractProfiles -and $_.status -eq 'not-run'})
$staticCodeReviews=@($results | Where-Object {$_.profile -in $staticCodeProfiles -and $_.status -eq 'review' -and -not ($_.profile -eq 'Semantic' -and [int]$_.blockingCount -eq 0 -and [int]$_.managedFindingCount -gt 0)})
$staticContractReviews=@($results | Where-Object {$_.profile -in $staticContractProfiles -and $_.status -eq 'review'})+@($managedSemanticResults|Where-Object {$_.status -eq 'review'})
$evidenceFailures=@($results | Where-Object {$_.profile -eq 'Evidence' -and $_.status -in @('failed','blocked','not-run','review')})
$staticStatus=if($staticFailures.Count -gt 0){'static-blocked'}elseif($staticNotRun.Count -gt 0 -or $staticReviews.Count -gt 0){'static-partial'}else{'static-passed'}
$staticCodeStatus=if($staticCodeFailures.Count -gt 0){'blocked'}elseif($staticCodeNotRun.Count -gt 0 -or $staticCodeReviews.Count -gt 0){'partial'}else{'passed'}
$staticContractStatus=if($staticContractFailures.Count -gt 0){'blocked'}elseif($staticContractNotRun.Count -gt 0 -or $staticContractReviews.Count -gt 0){'partial'}else{'passed'}
$staticBoundaryStatus=if($staticBoundaryFailures.Count -gt 0){'blocked'}elseif($staticBoundaryNotRun.Count -gt 0 -or @($results | Where-Object {$_.profile -eq 'Boundary' -and $_.status -eq 'review'}).Count -gt 0){'partial'}else{'passed'}
$evidenceStatus=if($evidenceFailures.Count -gt 0){'missing-or-stale'}elseif(@($results | Where-Object {$_.profile -eq 'Evidence' -and $_.status -eq 'passed'}).Count -gt 0){'passed'}else{'not-requested'}
# This validator never starts Unity, a Player, or domain Runtime. It may use
# isolated PowerShell processes for read-only helper validators; those helpers
# receive no report path unless their caller explicitly requests an artifact.
# Receipt presence can strengthen evidence, but cannot be reported as Runtime execution performed here.
$runtimeStatus='runtime-not-run'
$claimsNotProven=@('Unity/editor/process behavior','display/layout/timing behavior','Profiler/Player/IL2CPP/release behavior')
$overallVerdict=if($staticCodeStatus -eq 'blocked'){'StaticCodeBlocked'}elseif($staticContractStatus -eq 'blocked'){'StaticContractBlocked'}elseif($staticBoundaryStatus -eq 'blocked'){'StaticBoundaryBlocked'}elseif($staticReviews.Count -gt 0){'StaticReviewCompleteReviewPending'}elseif($evidenceFailures.Count -gt 0){'StaticReviewCompleteEvidencePending'}else{'StaticReviewCompleteRuntimePending'}
$nextAction=if($staticCodeStatus -eq 'blocked'){'修复源码结构或安全代码阻断；不要以运行时收据掩盖静态代码缺陷。'}elseif($staticContractStatus -eq 'blocked'){'修复 Skill 合同、验证语义、回放或目录阻断；ES 业务代码不必因此重写。'}elseif($staticBoundaryStatus -eq 'blocked'){'收窄外部路径/进程/会话边界并补充授权合同；这不是普通业务源码失败，也不能由 Runtime 收据绕过。'}elseif($staticReviews.Count -gt 0){'逐项处理静态 review；CurrentUserDirect 中的受管通道绑定问题不阻断当前用户动作，选用 ManagedAIBrain 前再补齐。'}elseif($evidenceFailures.Count -gt 0){'补齐或刷新对应 Evidence Receipt；不要把缺失收据解释为源码失败。'}else{'如需 RuntimeAcceptance/ReleaseAcceptance，先取得绑定当前任务的开发者授权。'}
$decisionStatus=if($staticCodeStatus -eq 'blocked' -or $staticContractStatus -eq 'blocked' -or $staticBoundaryStatus -eq 'blocked'){'blocked'}elseif($evidenceFailures.Count -gt 0){'evidence-pending'}elseif($runtimeStatus -eq 'runtime-not-run'){'runtime-not-run'}else{'passed'}
$blockingLayer=if($staticCodeStatus -eq 'blocked'){'static-code'}elseif($staticContractStatus -eq 'blocked'){'static-contract'}elseif($staticBoundaryStatus -eq 'blocked'){'static-boundary'}elseif($evidenceFailures.Count -gt 0){'evidence'}elseif($runtimeStatus -eq 'runtime-not-run'){'runtime'}else{'none'}
$summary=[pscustomobject]@{validator='es-skill-validator';timestampUtc=[DateTime]::UtcNow.ToString('o');authorizationLane=$AuthorizationLane;profiles=$Profile;results=@($results);claimStatusCounts=$claimStatusCounts;staticStatus=$staticStatus;staticCodeStatus=$staticCodeStatus;staticContractStatus=$staticContractStatus;staticBoundaryStatus=$staticBoundaryStatus;evidenceStatus=$evidenceStatus;staticReviewCount=$staticReviews.Count;runtimeStatus=$runtimeStatus;overallVerdict=$overallVerdict;claimsNotProven=$claimsNotProven;nextAction=$nextAction;decisionStatus=$decisionStatus;blockingLayer=$blockingLayer;status=if($failed.Count -eq 0){'passed'}else{'blocked'}}
# A review-pending aggregate is not a green acceptance result. Keep the
# individual profile results intact, but expose the unresolved review layer at
# the top level so routing cannot mistake it for Accepted.
# Direct evidence gaps remain review-only. Managed evidence failures are a hard
# channel gate and must remain blocked at both the profile and aggregate levels.
$aggregateHardFailures=@($results | Where-Object {$_.status -in @('failed','blocked') -and ($_.profile -ne 'Evidence' -or $AuthorizationLane -eq 'ManagedAIBrain')})
$summary.status = if($aggregateHardFailures.Count -gt 0){'blocked'}elseif($staticReviews.Count -gt 0 -or $evidenceFailures.Count -gt 0 -or (($Profile -contains 'Evidence' -or $Profile -contains 'Full') -and $notRun.Count -gt 0)){'review'}else{'passed'}
if($ReportPath){
    if([IO.Path]::IsPathRooted($ReportPath)){Write-Error 'ReportPath must be project-relative; external expansion is denied.' -ErrorAction Continue; exit 2}
    $report=(Join-Path $root $ReportPath); $rootNormalized=$root.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar); $reportFull=[IO.Path]::GetFullPath($report)
    if(-not ($reportFull.StartsWith($rootNormalized + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){Write-Error 'ReportPath escapes ProjectRoot; external expansion is denied.' -ErrorAction Continue; exit 2}
    $parent=Split-Path -Parent $report; if(-not (Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force | Out-Null}; $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $report -Encoding UTF8
}
$summary | ConvertTo-Json -Depth 6
if($failed.Count -gt 0 -or (($Profile -contains 'Evidence' -or $Profile -contains 'Full') -and $notRun.Count -gt 0)){exit 1}; exit 0
