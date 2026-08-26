[CmdletBinding()]
param(
    [string]$SchemaPath,
    [string]$EvidenceSchemaPath,
    [string]$SchemaModulePath,
    [string]$ModulePath
)

$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($SchemaPath)){$SchemaPath=Join-Path $scriptRoot '..\Contracts\es-task-context-runtime-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($EvidenceSchemaPath)){$EvidenceSchemaPath=Join-Path $scriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($SchemaModulePath)){$SchemaModulePath=Join-Path $scriptRoot '..\Contracts\ESJsonSchemaLite.psm1'}
if([string]::IsNullOrWhiteSpace($ModulePath)){$ModulePath=Join-Path $scriptRoot 'ESTaskContextRuntime.psm1'}
$strictUtf8=[Text.UTF8Encoding]::new($false,$true)
$script:SchemaRoot=$strictUtf8.GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $SchemaPath).Path))|ConvertFrom-Json -ErrorAction Stop
Import-Module (Resolve-Path -LiteralPath $SchemaModulePath).Path -Force
$supportedKeywords=@('$schema','$id','$ref','$defs','title','oneOf','allOf','if','then','type','pattern','enum','const','required','properties','additionalProperties','items','uniqueItems','minLength','minimum','maximum','format')

function Add-SchemaError([Collections.Generic.List[string]]$Errors,[string]$Path,[string]$Message){
    [void]$Errors.Add("$Path`: $Message")
}

function Get-ObjectProperties($Value){
    if($Value-is[Collections.IDictionary]){return @($Value.Keys|ForEach-Object{[pscustomobject]@{Name=[string]$_;Value=$Value[$_]}})}
    if($null-ne$Value-and$null-ne$Value.PSObject){return @($Value.PSObject.Properties)}
    return @()
}

function Get-ObjectProperty($Value,[string]$Name){
    if($Value-is[Collections.IDictionary]){if($Value.Contains($Name)){return [pscustomobject]@{Exists=$true;Value=$Value[$Name]}}}
    elseif($null-ne$Value-and$null-ne$Value.PSObject){$property=$Value.PSObject.Properties[$Name];if($null-ne$property){return [pscustomobject]@{Exists=$true;Value=$property.Value}}}
    return [pscustomobject]@{Exists=$false;Value=$null}
}

function Resolve-LocalSchemaRef([string]$Reference){
    if($Reference-notmatch '^#/\$defs/([^/]+)$'){throw "Unsupported schema reference: $Reference"}
    $definition=$script:SchemaRoot.'$defs'.PSObject.Properties[$Matches[1]]
    if($null-eq$definition){throw "Unresolved schema reference: $Reference"}
    return $definition.Value
}

function Test-SupportedSchemaNode($Schema,[string]$Path,[Collections.Generic.List[string]]$Errors){
    foreach($property in Get-ObjectProperties $Schema){
        if($supportedKeywords-notcontains[string]$property.Name){Add-SchemaError $Errors $Path "unsupported keyword '$($property.Name)'";continue}
        switch([string]$property.Name){
            '$defs'{foreach($child in Get-ObjectProperties $property.Value){Test-SupportedSchemaNode $child.Value "$Path/`$defs/$($child.Name)" $Errors}}
            'properties'{foreach($child in Get-ObjectProperties $property.Value){Test-SupportedSchemaNode $child.Value "$Path/properties/$($child.Name)" $Errors}}
            'oneOf'{for($i=0;$i-lt@($property.Value).Count;$i++){Test-SupportedSchemaNode @($property.Value)[$i] "$Path/oneOf/$i" $Errors}}
            'allOf'{for($i=0;$i-lt@($property.Value).Count;$i++){Test-SupportedSchemaNode @($property.Value)[$i] "$Path/allOf/$i" $Errors}}
            'items'{Test-SupportedSchemaNode $property.Value "$Path/items" $Errors}
            'if'{Test-SupportedSchemaNode $property.Value "$Path/if" $Errors}
            'then'{Test-SupportedSchemaNode $property.Value "$Path/then" $Errors}
        }
    }
}

function Test-JsonType($Value,[string]$Type){
    switch($Type){
        'null'{return $null-eq$Value}
        'object'{return $null-ne$Value-and($Value-is[pscustomobject]-or$Value-is[Collections.IDictionary])}
        'array'{return $null-ne$Value-and$Value-is[Array]}
        'string'{return $Value-is[string]}
        'boolean'{return $Value-is[bool]}
        'integer'{return $Value-is[byte]-or$Value-is[int16]-or$Value-is[int32]-or$Value-is[int64]-or$Value-is[uint16]-or$Value-is[uint32]-or$Value-is[uint64]}
        'number'{return $Value-is[ValueType]-and-not($Value-is[bool])}
        default{throw "Unsupported JSON type: $Type"}
    }
}

function Test-SchemaNode($Value,$Schema,[string]$Path,[Collections.Generic.List[string]]$Errors){
    $ref=Get-ObjectProperty $Schema '$ref'
    if($ref.Exists){Test-SchemaNode $Value (Resolve-LocalSchemaRef ([string]$ref.Value)) $Path $Errors;return}

    $oneOf=Get-ObjectProperty $Schema 'oneOf'
    if($oneOf.Exists){
        $matchCount=0
        foreach($candidate in @($oneOf.Value)){
            $candidateErrors=[Collections.Generic.List[string]]::new()
            Test-SchemaNode $Value $candidate $Path $candidateErrors
            if($candidateErrors.Count-eq0){$matchCount++}
        }
        if($matchCount-ne1){Add-SchemaError $Errors $Path "oneOf matched $matchCount schemas instead of exactly one"}
        return
    }

    $typeProperty=Get-ObjectProperty $Schema 'type'
    if($typeProperty.Exists){
        $types=@($typeProperty.Value|ForEach-Object{[string]$_})
        $typeMatched=$false
        foreach($type in $types){if(Test-JsonType $Value $type){$typeMatched=$true;break}}
        if(-not$typeMatched){Add-SchemaError $Errors $Path ('type mismatch; expected '+($types-join'|'));return}
    }

    $const=Get-ObjectProperty $Schema 'const'
    if($const.Exists-and(($Value|ConvertTo-Json -Compress)-cne($const.Value|ConvertTo-Json -Compress))){Add-SchemaError $Errors $Path 'const mismatch'}
    $enum=Get-ObjectProperty $Schema 'enum'
    if($enum.Exists-and@($enum.Value|Where-Object{($_|ConvertTo-Json -Compress)-ceq($Value|ConvertTo-Json -Compress)}).Count-eq0){Add-SchemaError $Errors $Path 'value is not in enum'}

    if($Value-is[string]){
        $pattern=Get-ObjectProperty $Schema 'pattern';if($pattern.Exists-and$Value-cnotmatch[string]$pattern.Value){Add-SchemaError $Errors $Path 'pattern mismatch'}
        $minLength=Get-ObjectProperty $Schema 'minLength';if($minLength.Exists-and$Value.Length-lt[int]$minLength.Value){Add-SchemaError $Errors $Path 'string is shorter than minLength'}
        $format=Get-ObjectProperty $Schema 'format'
        if($format.Exists-and[string]$format.Value-eq'date-time'){$parsed=[datetime]::MinValue;if(-not[datetime]::TryParse($Value,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind,[ref]$parsed)){Add-SchemaError $Errors $Path 'invalid date-time'}}
    }
    if($Value-is[ValueType]-and-not($Value-is[bool])){
        $minimum=Get-ObjectProperty $Schema 'minimum';if($minimum.Exists-and[decimal]$Value-lt[decimal]$minimum.Value){Add-SchemaError $Errors $Path 'value is below minimum'}
        $maximum=Get-ObjectProperty $Schema 'maximum';if($maximum.Exists-and[decimal]$Value-gt[decimal]$maximum.Value){Add-SchemaError $Errors $Path 'value is above maximum'}
    }
    if($Value-is[Array]){
        $unique=Get-ObjectProperty $Schema 'uniqueItems'
        if($unique.Exists-and[bool]$unique.Value){$keys=@($Value|ForEach-Object{$_|ConvertTo-Json -Depth 40 -Compress});if(@($keys|Sort-Object -Unique).Count-ne$keys.Count){Add-SchemaError $Errors $Path 'array items are not unique'}}
        $items=Get-ObjectProperty $Schema 'items'
        if($items.Exists){for($i=0;$i-lt$Value.Count;$i++){Test-SchemaNode $Value[$i] $items.Value "$Path/$i" $Errors}}
    }
    if($Value-is[pscustomobject]-or$Value-is[Collections.IDictionary]){
        $required=Get-ObjectProperty $Schema 'required'
        if($required.Exists){foreach($name in @($required.Value)){if(-not(Get-ObjectProperty $Value ([string]$name)).Exists){Add-SchemaError $Errors $Path "missing required property '$name'"}}}
        $properties=Get-ObjectProperty $Schema 'properties'
        if($properties.Exists){
            $allowed=@((Get-ObjectProperties $properties.Value)|ForEach-Object{[string]$_.Name})
            foreach($propertySchema in Get-ObjectProperties $properties.Value){$actual=Get-ObjectProperty $Value $propertySchema.Name;if($actual.Exists){Test-SchemaNode $actual.Value $propertySchema.Value "$Path/$($propertySchema.Name)" $Errors}}
            $additional=Get-ObjectProperty $Schema 'additionalProperties'
            if($additional.Exists-and$additional.Value-eq$false){foreach($actual in Get-ObjectProperties $Value){if($allowed-notcontains[string]$actual.Name){Add-SchemaError $Errors $Path "additional property '$($actual.Name)' is not allowed"}}}
        }
    }

    $allOf=Get-ObjectProperty $Schema 'allOf';if($allOf.Exists){foreach($candidate in @($allOf.Value)){Test-SchemaNode $Value $candidate $Path $Errors}}
    $ifSchema=Get-ObjectProperty $Schema 'if'
    if($ifSchema.Exists){$ifErrors=[Collections.Generic.List[string]]::new();Test-SchemaNode $Value $ifSchema.Value $Path $ifErrors;if($ifErrors.Count-eq0){$thenSchema=Get-ObjectProperty $Schema 'then';if($thenSchema.Exists){Test-SchemaNode $Value $thenSchema.Value $Path $Errors}}}
}

function Get-SchemaErrors($Value){
    $errors=[Collections.Generic.List[string]]::new()
    Test-SchemaNode $Value $script:SchemaRoot '$' $errors
    return @($errors)
}

Import-Module (Resolve-Path -LiteralPath $ModulePath).Path -Force
. (Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1')
$fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-task-context-schema-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot|Out-Null
Initialize-ESTestRoutePlanRepository $fixtureRoot
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'source.txt'),'schema-source',[Text.UTF8Encoding]::new($false))
$goal=New-ESGoalRevision -ProjectRoot $fixtureRoot -StoreRoot 'state' -GoalId 'goal-schema' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'schema' -Budget ([ordered]@{maxReads=8})
$routePlan=New-ESTestRoutePlan -Root $fixtureRoot -Goal $goal
$state=New-ESTaskContextTask -ProjectRoot $fixtureRoot -StoreRoot 'state' -TaskId 'schema-task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'schema' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{'source-integrity'='platform.file-hash-manifest-v1'}) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
$state=Confirm-ESTaskSourceScope -ProjectRoot $fixtureRoot -StoreRoot 'state' -TaskId 'schema-task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'verify'
$captured=[DateTime]::UtcNow.ToString('o')
$artifactPath=Join-Path $fixtureRoot 'schema-artifact.json'
$artifactPayload=[ordered]@{schemaVersion=1;claimId='source-integrity';sourceScopeHash=$state.verifiedSourceScopeHash;observations=@([ordered]@{path='source.txt';expectedSha256=[string]$state.verifiedSourceScope[0].sha256})}
[IO.File]::WriteAllText($artifactPath,($artifactPayload|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
$artifactHash=(Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
$evidence=[ordered]@{schemaVersion=1;taskId='schema-task';evidenceSetId='schema-evidence';capturedUtc=$captured;items=@([ordered]@{claimId='source-integrity';outcome='passed';capturedUtc=$captured;sourceScopeHash=$state.verifiedSourceScopeHash;evidenceHash=$artifactHash;producerType='platform';artifactPath='schema-artifact.json'});contradictions=@();sourceDrift=@();unverifiedClaims=@()}
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'evidence.json'),($evidence|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
$state=Submit-ESTaskEvidenceSet -ProjectRoot $fixtureRoot -StoreRoot 'state' -TaskId 'schema-task' -EvidenceSetPath 'evidence.json' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'evidence'
$state=Complete-ESTaskContextTask -ProjectRoot $fixtureRoot -StoreRoot 'state' -TaskId 'schema-task' -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey 'complete'
$eventPath=(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'state/schema-task/events') -File|Sort-Object Name|Select-Object -Last 1).FullName
$receiptPath=Join-Path (Join-Path $fixtureRoot 'state') ([string]$state.completionReceipt.path)
$event=$strictUtf8.GetString([IO.File]::ReadAllBytes($eventPath))|ConvertFrom-Json
$receipt=$strictUtf8.GetString([IO.File]::ReadAllBytes($receiptPath))|ConvertFrom-Json

$cases=[Collections.Generic.List[object]]::new()
function Add-Case([string]$Name,[bool]$Passed,[string[]]$Findings){[void]$cases.Add([pscustomobject]@{case=$Name;status=if($Passed){'passed'}else{'failed'};findings=@($Findings)})}
$supportErrors=@(Test-ESJsonSchemaSupported -SchemaPath $SchemaPath);Add-Case 'supported-keyword-closure' ($supportErrors.Count-eq0) $supportErrors
$eventErrors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $event);Add-Case 'representative-event' ($eventErrors.Count-eq0) $eventErrors
$receiptErrors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $receipt);Add-Case 'representative-receipt' ($receiptErrors.Count-eq0) $receiptErrors
$evidenceErrors=@(Test-ESJsonSchemaValue -SchemaPath $EvidenceSchemaPath -DefinitionName 'normalizedEvidenceSet' -Value $event.state.evidenceSet);Add-Case 'representative-evidence-set' ($evidenceErrors.Count-eq0) $evidenceErrors
$badState=$event|ConvertTo-Json -Depth 40|ConvertFrom-Json;$badState.state.taskStatus='Active';$badStateErrors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $badState);Add-Case 'accepted-state-invariant-negative' ($badStateErrors.Count-gt0) $(if($badStateErrors.Count){@()}else{@('invalid accepted state was not rejected')})
$badReceipt=$receipt|ConvertTo-Json -Depth 40|ConvertFrom-Json;$badReceipt.planHash='invalid';$badReceiptErrors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $badReceipt);Add-Case 'invalid-hash-negative' ($badReceiptErrors.Count-gt0) $(if($badReceiptErrors.Count){@()}else{@('invalid receipt hash field was not rejected')})
$extraEvent=$event|ConvertTo-Json -Depth 40|ConvertFrom-Json;$extraEvent|Add-Member -NotePropertyName unexpected -NotePropertyValue $true;$extraErrors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $extraEvent);Add-Case 'additional-property-negative' ($extraErrors.Count-gt0) $(if($extraErrors.Count){@()}else{@('additional event property was not rejected')})

$failed=@($cases|Where-Object status -eq 'failed')
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskContextRuntimeSchema';status=if($failed.Count){'failed'}else{'passed'};caseCount=$cases.Count;passedCount=@($cases|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($cases);schemaPath=(Resolve-Path -LiteralPath $SchemaPath).Path;fixtureRoot=$fixtureRoot;runtimeStatus='runtime-not-run';claimsNotProven=@('Equivalence to an external full Draft 2020-12 implementation','Unity or Worker Runtime','release acceptance')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
