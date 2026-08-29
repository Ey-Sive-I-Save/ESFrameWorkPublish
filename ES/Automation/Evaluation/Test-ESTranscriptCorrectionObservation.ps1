[CmdletBinding()]
param([string]$ModulePath)

$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ModulePath)){$ModulePath=Join-Path $PSScriptRoot 'ESTranscriptCorrectionObservation.psm1'}
$modules=@(Import-Module (Resolve-Path -LiteralPath $ModulePath).Path -Force -PassThru)
$module=$modules|Select-Object -Last 1
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-transcript-observation-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot|Out-Null
$results=[Collections.Generic.List[object]]::new()

function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual-cne[string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Invoke-Case([string]$Name,[scriptblock]$Body){try{&$Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}}
function New-MessageRow([string]$Role,[string]$Text,[DateTimeOffset]$Timestamp){[ordered]@{timestamp=$Timestamp.ToUniversalTime().ToString('o');type='response_item';payload=[ordered]@{type='message';role=$Role;content=@([ordered]@{type='input_text';text=$Text})}}}
function New-TranscriptFixture {
    param([string]$Name,[string]$Followup)
    if([string]::IsNullOrWhiteSpace($Followup)){$Followup='Wrong: stop editing and perform a read-only check first.'}
    $sessionId=([Guid]::NewGuid().ToString('N'))
    $base=[DateTimeOffset]::UtcNow.AddMinutes(-1)
    $rows=@(
        [ordered]@{timestamp=$base.ToString('o');type='session_meta';payload=[ordered]@{session_id=$sessionId}},
        (New-MessageRow 'user' '# AGENTS.md instructions for injected world state' ($base.AddSeconds(1))),
        (New-MessageRow 'user' 'Please complete the current task.' ($base.AddSeconds(2))),
        (New-MessageRow 'assistant' 'I am starting the task.' ($base.AddSeconds(3))),
        (New-MessageRow 'user' $Followup ($base.AddSeconds(4))),
        (New-MessageRow 'assistant' 'The correction has been applied.' ($base.AddSeconds(5))),
        (New-MessageRow 'user' 'This is a later task.' ($base.AddSeconds(6)))
    )
    $lines=@($rows|ForEach-Object{$_|ConvertTo-Json -Depth 12 -Compress})
    $path=Join-Path $testRoot ($Name+'.jsonl')
    [IO.File]::WriteAllText($path,($lines-join"`n"),[Text.UTF8Encoding]::new($false))
    $slice=@(
        [ordered]@{line=3;timestamp=[string]$rows[2].timestamp;role='user';textSha256=(Get-ESTranscriptSha256 'Please complete the current task.')},
        [ordered]@{line=4;timestamp=[string]$rows[3].timestamp;role='assistant';textSha256=(Get-ESTranscriptSha256 'I am starting the task.')},
        [ordered]@{line=5;timestamp=[string]$rows[4].timestamp;role='user';textSha256=(Get-ESTranscriptSha256 $Followup)}
    )
    $artifact=[pscustomobject][ordered]@{
        taskId='task-a';goalRevisionHash=('a'*64);taskRevision=4;contextVersion=2;sessionId=$sessionId;sourceTranscriptPath=$path
        sourceLineCount=$lines.Count;sourcePrefixSha256=(Get-ESTranscriptSha256 ($lines-join"`n"));startLine=3;endLine=5
        normalizedSliceSha256=(Get-ESTranscriptSha256 ($slice|ConvertTo-Json -Depth 6 -Compress))
    }
    [pscustomobject]@{artifact=$artifact;expected=[pscustomobject]@{taskId='task-a';goalRevisionHash=('a'*64);taskRevision=4;contextVersion=2;sessionId=$sessionId;createdUtc=$base.AddMilliseconds(2500).ToString('o')};path=$path;lines=$lines}
}
function Invoke-Observation($Fixture){Get-ESTaskTranscriptCorrectionObservation -Artifact $Fixture.artifact -ExpectedTask $Fixture.expected -TestTrustedSessionRoot $testRoot}

Invoke-Case 'correction-is-derived-from-first-user-followup'{$fixture=New-TranscriptFixture 'correction';$value=Invoke-Observation $fixture;Assert-True $value.correctionObserved 'Correction was not derived.';Assert-Equal $value.correctionCount 1 'Correction count'}
Invoke-Case 'non-correction-is-derived-as-false'{$fixture=New-TranscriptFixture 'no-correction' 'Acknowledged, continue.';$value=Invoke-Observation $fixture;Assert-True (-not $value.correctionObserved) 'Non-correction was classified as correction.';Assert-Equal $value.correctionCount 0 'Correction count'}
Invoke-Case 'wrong-task-binding-is-rejected'{$fixture=New-TranscriptFixture 'wrong-task';$fixture.artifact.taskId='task-b';$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript binding mismatch: taskId'};Assert-True $threw 'Wrong TaskId was accepted.'}
Invoke-Case 'wrong-goal-binding-is-rejected'{$fixture=New-TranscriptFixture 'wrong-goal';$fixture.artifact.goalRevisionHash=('b'*64);$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript binding mismatch: goalRevisionHash'};Assert-True $threw 'Wrong GoalRevisionHash was accepted.'}
Invoke-Case 'wrong-revision-is-rejected'{$fixture=New-TranscriptFixture 'wrong-revision';$fixture.artifact.taskRevision=3;$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript binding mismatch: taskRevision'};Assert-True $threw 'Wrong TaskRevision was accepted.'}
Invoke-Case 'wrong-context-version-is-rejected'{$fixture=New-TranscriptFixture 'wrong-context';$fixture.artifact.contextVersion=1;$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript binding mismatch: contextVersion'};Assert-True $threw 'Wrong ContextVersion was accepted.'}
Invoke-Case 'wrong-session-is-rejected'{$fixture=New-TranscriptFixture 'wrong-session';$fixture.artifact.sessionId=([Guid]::NewGuid().ToString('N'));$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript binding mismatch: sessionId'};Assert-True $threw 'Wrong SessionId was accepted.'}
Invoke-Case 'forged-prefix-hash-is-rejected'{$fixture=New-TranscriptFixture 'wrong-prefix';$fixture.artifact.sourcePrefixSha256=('0'*64);$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript source prefix hash mismatch.'};Assert-True $threw 'Wrong prefix hash was accepted.'}
Invoke-Case 'forged-slice-hash-is-rejected'{$fixture=New-TranscriptFixture 'wrong-slice';$fixture.artifact.normalizedSliceSha256=('0'*64);$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript normalized slice hash mismatch.'};Assert-True $threw 'Wrong slice hash was accepted.'}
Invoke-Case 'truncated-followup-is-rejected'{$fixture=New-TranscriptFixture 'truncated';$fixture.artifact.endLine=4;$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript endLine must be the first user follow-up after the assistant response.'};Assert-True $threw 'Truncated range was accepted.'}
Invoke-Case 'whole-session-projection-is-rejected'{$fixture=New-TranscriptFixture 'whole-session';$fixture.artifact.endLine=7;$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript endLine must be the first user follow-up after the assistant response.'};Assert-True $threw 'Whole-session range was accepted.'}
Invoke-Case 'injected-user-content-cannot-start-a-task-slice'{$fixture=New-TranscriptFixture 'injected';$fixture.artifact.startLine=2;$fixture.artifact.endLine=5;$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript startLine must be a non-injected user message.'};Assert-True $threw 'Injected content was accepted as the task start.'}
Invoke-Case 'slice-before-task-creation-is-rejected'{$fixture=New-TranscriptFixture 'pre-task-slice';$fixture.expected.createdUtc=([DateTimeOffset]::Parse([string]$fixture.expected.createdUtc)).AddSeconds(3).ToString('o');$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript startLine is not the last user message before task creation.'};Assert-True $threw 'A pre-task interaction was assigned to the task.'}
Invoke-Case 'historical-binding-allows-only-earlier-revisions'{$fixture=New-TranscriptFixture 'historical';$fixture.expected.taskRevision=6;$fixture.expected.contextVersion=4;$fixture.expected|Add-Member -NotePropertyName allowHistoricalRevision -NotePropertyValue $true;$value=Invoke-Observation $fixture;Assert-True $value.correctionObserved 'Historical binding did not replay.';$fixture.artifact.taskRevision=7;$threw=$false;try{Invoke-Observation $fixture|Out-Null}catch{$threw=$_.Exception.Message-eq'Task transcript historical binding is invalid: taskRevision'};Assert-True $threw 'Future revision was accepted as historical.'}
Invoke-Case 'appended-transcript-preserves-prefix-replay'{$fixture=New-TranscriptFixture 'append';[IO.File]::AppendAllText($fixture.path,"`n"+((New-MessageRow 'assistant' 'An appended message.' ([DateTimeOffset]::UtcNow))|ConvertTo-Json -Depth 12 -Compress),[Text.UTF8Encoding]::new($false));$value=Invoke-Observation $fixture;Assert-True $value.correctionObserved 'Append invalidated the bound prefix.'}

$failed=@($results|Where-Object status -eq 'failed')
$status=if($failed.Count){'failed'}else{'passed'}
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTranscriptCorrectionObservation';status=$status;caseCount=$results.Count;passedCount=@($results|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($results);testRoot=$testRoot;runtimeStatus='runtime-not-run';claimsNotProven=@('Production Codex transcript availability','production route integration','Unity or Release acceptance')}|ConvertTo-Json -Depth 10
if($failed.Count){exit 1}
