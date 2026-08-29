Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

$script:Statuses=@('candidate','pending-confirmation','confirmed','ambiguous','superseded','closed')
$script:SafeId='^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$'
function ConvertTo-FocusCanonical($v){
  if($null -eq $v){return 'null'}
  if($v -is [string] -or $v -is [char]){return ([string]$v|ConvertTo-Json -Compress)}
  if($v -is [bool]){return $(if($v){'true'}else{'false'})}
  if($v -is [Collections.IDictionary]){return '{'+((@($v.Keys|%{[string]$_}|sort)|%{('{0}:{1}' -f ($_|ConvertTo-Json -Compress),(ConvertTo-FocusCanonical $v[$_]))}) -join ',')+'}'}
  if($v -is [pscustomobject]){return '{'+((@($v.PSObject.Properties|sort Name)|%{('{0}:{1}' -f ($_.Name|ConvertTo-Json -Compress),(ConvertTo-FocusCanonical $_.Value))}) -join ',')+'}'}
  if($v -is [Collections.IEnumerable] -and $v -isnot [string]){return '['+((@($v)|%{ConvertTo-FocusCanonical $_}) -join ',')+']'}
  return ([string]$v|ConvertTo-Json -Compress)
}
function Get-FocusHash($v){$sha=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-FocusCanonical $v)))).Replace('-','').ToLowerInvariant())}finally{$sha.Dispose()}}
function Assert-FocusId([string]$v,[string]$n){if([string]::IsNullOrWhiteSpace($v)-or$v-notmatch$script:SafeId){throw "$n is invalid"}}
function Get-FocusScopeHash($AllowedScope,$ForbiddenExpansion){Get-FocusHash ([ordered]@{allowedScope=@($AllowedScope);forbiddenExpansion=@($ForbiddenExpansion)})}
function Get-FocusContextId([int]$Revision,[string]$ProposalHash){'FC-'+(Get-FocusHash ([ordered]@{revision=$Revision;proposalHash=$ProposalHash})).Substring(0,16)}
function Get-FocusConfirmationReceiptHash($Receipt){$base=[ordered]@{};foreach($property in $Receipt.PSObject.Properties){if($property.Name -ne 'receiptHash'){$base[$property.Name]=$property.Value}};Get-FocusHash $base}
function Assert-FocusConfirmationReceipt($Context,$Receipt){if($null -eq $Receipt){throw 'Elevated or critical Focus confirmation requires a confirmation receipt.'};if([string]$Receipt.receiptType -cne 'TaskFocusConfirmation' -or [string]$Receipt.proposalId -cne [string]$Context.proposalId -or [string]$Receipt.proposalHash -cne [string]$Context.proposalHash -or [int]$Receipt.focusRevision -ne [int]$Context.revision -or [string]$Receipt.decision -cne 'confirm' -or [string]$Receipt.receiptHash -notmatch '^[a-f0-9]{64}$' -or (Get-FocusConfirmationReceiptHash $Receipt) -cne [string]$Receipt.receiptHash){throw 'Confirmation receipt is invalid or does not match the pending FocusContext.'};[string]$Receipt.receiptHash}
function New-TaskFocusProposal {
 param([Parameter(Mandatory)]$Focus,[ValidateSet('normal','elevated','critical')][string]$Priority='normal',[string]$Source='user-request',[string[]]$AllowedScope=@(),[string[]]$ForbiddenExpansion=@(),[string[]]$RequiredReads=@(),[string[]]$AcceptanceSignals=@(),[string]$ProposalId)
 if([string]::IsNullOrWhiteSpace($Focus)){throw 'Focus is invalid'}; if([string]::IsNullOrWhiteSpace($ProposalId)){$ProposalId='FP-'+(Get-FocusHash ([ordered]@{focus=$Focus;priority=$Priority;source=$Source})).Substring(0,16)} else {Assert-FocusId $ProposalId 'ProposalId'}
 [ordered]@{schemaVersion=1;proposalId=$ProposalId;focus=$Focus;priority=$Priority;source=$Source;allowedScope=@($AllowedScope);forbiddenExpansion=@($ForbiddenExpansion);requiredReads=@($RequiredReads);acceptanceSignals=@($AcceptanceSignals);proposalHash=''} | % { $_.proposalHash=Get-FocusHash ([ordered]@{proposalId=$_.proposalId;focus=$_.focus;priority=$_.priority;source=$_.source;allowedScope=$_.allowedScope;forbiddenExpansion=$_.forbiddenExpansion;requiredReads=$_.requiredReads;acceptanceSignals=$_.acceptanceSignals}); [pscustomobject]$_ }
}
function Invoke-TaskFocusProposal {
 param($Current,[Parameter(Mandatory)]$Proposal,[ValidateSet('none','confirm','reject')][string]$UserDecision='none',[int]$ExpectedRevision=0,[string]$ConfirmationReceiptHash,$ConfirmationReceipt)
 if($null -eq $Current){$Current=[pscustomobject]@{revision=0;status='candidate';proposalId='';proposalHash='';focus='';supersedes=$null;focusContextId=$null;focusRevision=$null;focusProposalHash=$null;focusReceiptHash=$null;focusScopeHash=$null}}
 if($ExpectedRevision -ne [int]$Current.revision){return [pscustomobject]@{status='ambiguous';reason='stale-revision';revision=[int]$Current.revision;proposalId=$Proposal.proposalId}}
 if([string]$Current.proposalHash -eq [string]$Proposal.proposalHash){
   if($UserDecision -eq 'confirm' -and [string]$Current.status -eq 'pending-confirmation'){
     if([string]$Proposal.priority -in @('elevated','critical')){$ConfirmationReceiptHash=Assert-FocusConfirmationReceipt $Current $ConfirmationReceipt}
     $copy=$Current|ConvertTo-Json -Depth 10|ConvertFrom-Json;$copy.status='confirmed';if(-not [string]::IsNullOrWhiteSpace($ConfirmationReceiptHash)){$copy.focusReceiptHash=$ConfirmationReceiptHash};return $copy
   }
   return $Current
 }
 if([int]$Current.revision -gt 0 -and $UserDecision -eq 'none'){return [pscustomobject]@{status='ambiguous';reason='conflicting-proposal';revision=[int]$Current.revision;proposalId=$Proposal.proposalId;supersedes=$Current.proposalId}}
 if($UserDecision -eq 'confirm' -and [string]$Proposal.priority -in @('elevated','critical')){$ConfirmationReceiptHash=Assert-FocusConfirmationReceipt $Current $ConfirmationReceipt}
 $nextRevision=[int]$Current.revision+1
 $next=[ordered]@{schemaVersion=1;revision=$nextRevision;focusRevision=$nextRevision;status=$(if($UserDecision -eq 'confirm'){'confirmed'}elseif($UserDecision -eq 'reject'){'closed'}else{'pending-confirmation'});proposalId=$Proposal.proposalId;proposalHash=$Proposal.proposalHash;focusContextId=Get-FocusContextId $nextRevision $Proposal.proposalHash;focusProposalHash=$Proposal.proposalHash;focusReceiptHash=$(if([string]::IsNullOrWhiteSpace($ConfirmationReceiptHash)){$null}else{$ConfirmationReceiptHash});focusScopeHash=Get-FocusScopeHash $Proposal.allowedScope $Proposal.forbiddenExpansion;focus=$Proposal.focus;priority=$Proposal.priority;source=$Proposal.source;allowedScope=@($Proposal.allowedScope);forbiddenExpansion=@($Proposal.forbiddenExpansion);requiredReads=@($Proposal.requiredReads);acceptanceSignals=@($Proposal.acceptanceSignals);supersedes=$(if([int]$Current.revision -gt 0){$Current.proposalId}else{$null})}
 [pscustomobject]$next
}
function Invoke-TaskFocusDefaultActivation {
 param([Parameter(Mandatory)]$Proposal,$Current,[Parameter(Mandatory)][string]$LatestUserMessage,[string[]]$ExplicitOptOutTokens,[int]$ExpectedRevision=0)
 if([string]::IsNullOrWhiteSpace($LatestUserMessage)){throw 'LatestUserMessage is required'}
 if($null -eq $ExplicitOptOutTokens -or @($ExplicitOptOutTokens).Count -eq 0){$ExplicitOptOutTokens=@(([char]0x53D6+[char]0x6D88),([char]0x4E0D+[char]0x7528),([char]0x5173+[char]0x95ED+[char]0x805A+[char]0x7126))}
 if($null -eq $ExplicitOptOutTokens -or @($ExplicitOptOutTokens).Count -eq 0){$ExplicitOptOutTokens=@('cancel','opt-out')}
 foreach($token in @($ExplicitOptOutTokens)){if(-not [string]::IsNullOrWhiteSpace($token) -and $LatestUserMessage.Contains($token)){return Invoke-TaskFocusProposal -Current $Current -Proposal $Proposal -UserDecision reject -ExpectedRevision $ExpectedRevision}}
 $pending=if($null -eq $Current){Invoke-TaskFocusProposal -Proposal $Proposal -UserDecision none -ExpectedRevision 0}else{Invoke-TaskFocusProposal -Current $Current -Proposal $Proposal -UserDecision none -ExpectedRevision ([int]$ExpectedRevision)}
 if([string]$pending.status -ne 'pending-confirmation'){return $pending}
 if([string]$Proposal.priority -ne 'normal'){return $pending}
 return Invoke-TaskFocusProposal -Current $pending -Proposal $Proposal -UserDecision confirm -ExpectedRevision ([int]$pending.revision)
}
function New-FocusConfirmationReceipt {
 param([Parameter(Mandatory)]$Context,[Parameter(Mandatory)][string]$UserMessage,[Parameter(Mandatory)][ValidateSet('confirm','reject')][string]$Decision)
 if([string]$Context.status -ne 'pending-confirmation'){throw 'Confirmation requires pending-confirmation context'}
 if([string]::IsNullOrWhiteSpace($UserMessage)){throw 'UserMessage is required'}
 $base=[ordered]@{schemaVersion=1;receiptType='TaskFocusConfirmation';receiptId='FCR-'+([guid]::NewGuid().ToString('N'));proposalId=[string]$Context.proposalId;proposalHash=[string]$Context.proposalHash;focusRevision=[int]$Context.revision;decision=$Decision;userMessage=$UserMessage;issuedUtc=[DateTime]::UtcNow.ToString('o')}
 $base.receiptHash=Get-FocusHash $base; [pscustomobject]$base
}
function New-FocusContextProjection {
 param([Parameter(Mandatory)]$Context,[string[]]$IncludedBlocks=@('task-focus','goal-revision','required-reads'))
 if([string]$Context.status -notin @('confirmed','pending-confirmation')){throw 'Projection requires pending or confirmed context'}
 $base=[ordered]@{schemaVersion=1;projectionType='TaskFocusContextProjection';projectionId='FCP-'+([guid]::NewGuid().ToString('N'));focusRevision=[int]$Context.revision;status=[string]$Context.status;focus=[string]$Context.focus;allowedScope=@($Context.allowedScope);forbiddenExpansion=@($Context.forbiddenExpansion);requiredReads=@($Context.requiredReads);acceptanceSignals=@($Context.acceptanceSignals);includedBlocks=@($IncludedBlocks);sourceProposalHash=[string]$Context.proposalHash;projectedUtc=[DateTime]::UtcNow.ToString('o')};$base.projectionHash=Get-FocusHash $base;[pscustomobject]$base
}
function New-FocusCheckpoint {
 param([Parameter(Mandatory)]$Context,[Parameter(Mandatory)][string]$TaskId,[string]$LastCompletedStage='focus')
 Assert-FocusId $TaskId 'TaskId';if([string]$Context.status -cne 'confirmed'){throw 'Checkpoint requires confirmed FocusContext'};$base=[ordered]@{schemaVersion=1;checkpointType='TaskFocusCheckpoint';checkpointId='FCK-'+([guid]::NewGuid().ToString('N'));taskId=$TaskId;focusRevision=[int]$Context.revision;contextStatus=[string]$Context.status;context=$Context;lastCompletedStage=$LastCompletedStage;createdUtc=[DateTime]::UtcNow.ToString('o')};$base.checkpointHash=Get-FocusHash $base;[pscustomobject]$base
}
function Restore-FocusCheckpoint {
 param([Parameter(Mandatory)]$Checkpoint,[Parameter(Mandatory)][string]$TaskId)
 Assert-FocusId $TaskId 'TaskId';if([string]$Checkpoint.taskId -cne $TaskId){throw 'checkpoint task mismatch'};$hashInput=[ordered]@{};foreach($p in $Checkpoint.PSObject.Properties){if($p.Name -ne 'checkpointHash'){$hashInput[$p.Name]=$p.Value}};if((Get-FocusHash $hashInput)-cne[string]$Checkpoint.checkpointHash){throw 'checkpoint hash mismatch'};return $Checkpoint.context
}
Export-ModuleMember -Function New-TaskFocusProposal,Invoke-TaskFocusProposal,Invoke-TaskFocusDefaultActivation,Get-FocusHash,New-FocusConfirmationReceipt,New-FocusContextProjection,New-FocusCheckpoint,Restore-FocusCheckpoint
