Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

function Get-ESTranscriptSha256([string]$Text){
    $sha=[Security.Cryptography.SHA256]::Create()
    try{return([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-','').ToLowerInvariant()}
    finally{$sha.Dispose()}
}

function Get-ESTrustedCodexSessionRoot([string]$TestRoot){
    $root=if([string]::IsNullOrWhiteSpace($TestRoot)){
        $base=if(-not[string]::IsNullOrWhiteSpace([string]$env:CODEX_HOME)){[string]$env:CODEX_HOME}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex'}
        [IO.Path]::GetFullPath((Join-Path $base 'sessions')).TrimEnd('\','/')
    }else{[IO.Path]::GetFullPath($TestRoot).TrimEnd('\','/')}
    if(-not(Test-Path -LiteralPath $root -PathType Container)){throw 'Trusted Codex session root is unavailable.'}
    $item=Get-Item -LiteralPath $root -Force
    if($item.LinkType-or($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Trusted Codex session root cannot be a reparse point.'}
    return $root
}

function Resolve-ESTrustedCodexTranscript([string]$Path,[int64]$MaxTranscriptBytes,[string]$TestRoot){
    if(-not[IO.Path]::IsPathRooted($Path)){throw 'Codex transcript path must be absolute.'}
    $root=Get-ESTrustedCodexSessionRoot $TestRoot
    $full=[IO.Path]::GetFullPath($Path)
    if(-not$full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Codex transcript path is outside the trusted session root.'}
    if([IO.Path]::GetExtension($full)-cne'.jsonl'-or-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'Codex transcript path must identify a readable .jsonl file.'}
    $item=Get-Item -LiteralPath $full -Force
    if($item.LinkType-or($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Codex transcript cannot be a reparse point.'}
    if($item.Length-gt$MaxTranscriptBytes){throw 'Codex transcript exceeds maxTranscriptBytes.'}
    return $full
}

function Get-ESTranscriptMessageRecord($Row,[int]$Line){
    $payload=$Row.payload
    if($Row.type-ceq'response_item'-and$payload.type-ceq'message'-and[string]$payload.role-in@('user','assistant')){
        $text=((@($payload.content)|ForEach-Object{if($_.text){[string]$_.text}})-join"`n").Trim()
        if([string]::IsNullOrWhiteSpace($text)){return $null}
        $injected=[bool]($text-match'(?i)<recommended_plugins>|<permissions instructions>|# AGENTS\.md instructions|<skills_instructions>|<codex_internal_context')
        if($injected){return $null}
        return [pscustomobject][ordered]@{line=$Line;timestamp=[string]$Row.timestamp;role=[string]$payload.role;text=$text}
    }
    if($Row.type-ceq'event_msg'-and$payload.type-ceq'agent_message'){
        $text=([string]$payload.message).Trim()
        if(-not[string]::IsNullOrWhiteSpace($text)){return [pscustomobject][ordered]@{line=$Line;timestamp=[string]$Row.timestamp;role='assistant';text=$text}}
    }
    return $null
}

function Test-ESTranscriptCorrectionText([string]$Text){
    return [bool]($Text-match'(?i)\u4e0d\u5bf9|\u4e0d\u662f|\u6211\u8bf4|\u4e0d\u8981|\u7ea0\u6b63|\u9519\u8bef|\u4e0d\u5e94\u8be5|wrong|correction')
}

function Get-ESTaskTranscriptCorrectionObservation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]$Artifact,
        [Parameter(Mandatory=$true)]$ExpectedTask,
        [ValidateRange(1,104857600)][int64]$MaxTranscriptBytes=33554432,
        [ValidateRange(2,4096)][int]$MaxSliceLines=512,
        [ValidateRange(1024,16777216)][int]$MaxSliceChars=1048576,
        [string]$TestTrustedSessionRoot
    )
    foreach($binding in @(@('taskId',$ExpectedTask.taskId),@('goalRevisionHash',$ExpectedTask.goalRevisionHash))){
        $name=[string]$binding[0]
        if([string]$Artifact.$name-cne[string]$binding[1]){throw "Task transcript binding mismatch: $name"}
    }
    if([string]::IsNullOrWhiteSpace([string]$ExpectedTask.sessionId)-or[string]$Artifact.sessionId-cne[string]$ExpectedTask.sessionId){throw 'Task transcript binding mismatch: sessionId'}
    try{$taskCreatedUtc=[DateTimeOffset]::Parse([string]$ExpectedTask.createdUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)}catch{throw 'Task transcript frozen creation timestamp is invalid.'}
    $historical=$null-ne$ExpectedTask.PSObject.Properties['allowHistoricalRevision']-and[bool]$ExpectedTask.allowHistoricalRevision
    foreach($name in @('taskRevision','contextVersion')){
        if($historical){if([int]$Artifact.$name-lt1-or[int]$Artifact.$name-gt[int]$ExpectedTask.$name){throw "Task transcript historical binding is invalid: $name"}}
        elseif([int]$Artifact.$name-ne[int]$ExpectedTask.$name){throw "Task transcript binding mismatch: $name"}
    }
    $path=Resolve-ESTrustedCodexTranscript ([string]$Artifact.sourceTranscriptPath) $MaxTranscriptBytes $TestTrustedSessionRoot
    $strict=[Text.UTF8Encoding]::new($false,$true)
    $raw=$strict.GetString([IO.File]::ReadAllBytes($path))
    $lines=@($raw-split'\r?\n')
    if($lines.Count-gt0-and[string]::IsNullOrEmpty([string]$lines[-1])){$lines=$lines[0..($lines.Count-2)]}
    $sourceLineCount=[int]$Artifact.sourceLineCount
    if($sourceLineCount-lt1-or$sourceLineCount-gt$lines.Count){throw 'Task transcript sourceLineCount exceeds the readable transcript snapshot.'}
    $prefix=($lines[0..($sourceLineCount-1)]-join"`n")
    if((Get-ESTranscriptSha256 $prefix)-cne[string]$Artifact.sourcePrefixSha256){throw 'Task transcript source prefix hash mismatch.'}
    try{$meta=$lines[0]|ConvertFrom-Json -ErrorAction Stop}catch{throw 'Task transcript session metadata is malformed.'}
    $sessionId=if(-not[string]::IsNullOrWhiteSpace([string]$meta.payload.session_id)){[string]$meta.payload.session_id}else{[string]$meta.payload.id}
    if($meta.type-cne'session_meta'-or$sessionId-cne[string]$Artifact.sessionId){throw 'Task transcript session identity mismatch.'}
    $start=[int]$Artifact.startLine;$end=[int]$Artifact.endLine
    if($start-lt2-or$end-lt$start-or$end-gt$sourceLineCount-or($end-$start+1)-gt$MaxSliceLines){throw 'Task transcript line range is invalid or exceeds maxSliceLines.'}
    $messages=[Collections.Generic.List[object]]::new()
    $chars=0
    for($line=$start;$line-le$sourceLineCount;$line++){
        try{$row=$lines[$line-1]|ConvertFrom-Json -ErrorAction Stop}catch{throw "Task transcript JSONL parse failed at line $line."}
        $message=Get-ESTranscriptMessageRecord $row $line
        if($null-ne$message){$chars+=$message.text.Length;if($chars-gt$MaxSliceChars){throw 'Task transcript slice exceeds maxSliceChars.'};$messages.Add($message)}
    }
    $startMessage=@($messages|Where-Object{$_.line-eq$start})|Select-Object -First 1
    if($null-eq$startMessage-or[string]$startMessage.role-cne'user'){throw 'Task transcript startLine must be a non-injected user message.'}
    $assistantSeen=$false;$followup=$null
    foreach($message in @($messages|Where-Object{$_.line-gt$start}|Sort-Object line)){
        if([string]$message.role-ceq'assistant'){$assistantSeen=$true;continue}
        if($assistantSeen-and[string]$message.role-ceq'user'){$followup=$message;break}
    }
    if($null-eq$followup){throw 'Task transcript has no bounded user follow-up after an assistant response.'}
    if([int]$followup.line-ne$end){throw 'Task transcript endLine must be the first user follow-up after the assistant response.'}
    try{$startUtc=[DateTimeOffset]::Parse([string]$startMessage.timestamp,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind);$followupUtc=[DateTimeOffset]::Parse([string]$followup.timestamp,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)}catch{throw 'Task transcript message timestamp is invalid.'}
    if($startUtc-gt$taskCreatedUtc){throw 'Task transcript start message occurs after task creation.'}
    $laterUserBeforeCreation=@($messages|Where-Object{[string]$_.role-ceq'user'-and[int]$_.line-gt$start-and([DateTimeOffset]::Parse([string]$_.timestamp,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind))-le$taskCreatedUtc})
    if($laterUserBeforeCreation.Count-gt0){throw 'Task transcript startLine is not the last user message before task creation.'}
    if($followupUtc-le$taskCreatedUtc){throw 'Task transcript correction follow-up does not occur after task creation.'}
    $slice=@($messages|Where-Object{$_.line-ge$start-and$_.line-le$end}|ForEach-Object{[ordered]@{line=[int]$_.line;timestamp=[string]$_.timestamp;role=[string]$_.role;textSha256=Get-ESTranscriptSha256 ([string]$_.text)}})
    $normalized=($slice|ConvertTo-Json -Depth 6 -Compress)
    $normalizedHash=Get-ESTranscriptSha256 $normalized
    if($normalizedHash-cne[string]$Artifact.normalizedSliceSha256){throw 'Task transcript normalized slice hash mismatch.'}
    $correction=[bool](Test-ESTranscriptCorrectionText ([string]$followup.text))
    return [pscustomobject][ordered]@{
        schemaVersion=1;recordType='InteractionCorrectionObservation';scope='task-object';taskId=[string]$Artifact.taskId;goalRevisionHash=[string]$Artifact.goalRevisionHash
        taskRevision=[int]$Artifact.taskRevision;contextVersion=[int]$Artifact.contextVersion;sessionId=$sessionId;startLine=$start;endLine=$end
        sourceLineCount=$sourceLineCount;sourcePrefixSha256=[string]$Artifact.sourcePrefixSha256;normalizedSliceSha256=$normalizedHash
        correctionObserved=$correction;correctionCount=if($correction){1}else{0};eligibleTaskCount=1;messageCount=$slice.Count
    }
}

Export-ModuleMember -Function Get-ESTaskTranscriptCorrectionObservation,Get-ESTranscriptSha256,Test-ESTranscriptCorrectionText
