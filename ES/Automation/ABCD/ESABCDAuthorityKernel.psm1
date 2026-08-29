Set-StrictMode -Version Latest;$ErrorActionPreference='Stop'
$script:Modes=[ordered]@{
 'full-depth'=[ordered]@{readBudget=40;capabilities=@('bounded-tool-action','failure-recovery','branch-evaluation','state-transition-guard','environment-trust-gate','audit-evidence-chain');deep=$true;finalGate=$true}
 'shallow-fast'=[ordered]@{readBudget=3;capabilities=@('bounded-tool-action','audit-evidence-chain');deep=$false;finalGate=$false}
 'core-high-risk'=[ordered]@{readBudget=20;capabilities=@('bounded-tool-action','failure-recovery','state-transition-guard','environment-trust-gate','audit-evidence-chain');deep=$true;finalGate=$true}
}
function Resolve-ESABCDAuthorityDecision {
 [CmdletBinding()]param([ValidateSet('full-depth','shallow-fast','core-high-risk')][string]$Mode='core-high-risk',[Parameter(Mandatory)]$Evidence,[string[]]$MissingFields=@())
 $profile=$script:Modes[$Mode];$normalized=[ordered]@{};$safe=@('capturedUtc','timestampUtc','receiptPath');foreach($f in $MissingFields){if($f -in $safe){$normalized[$f]='defaulted-from-local-observation'}}
 $unsafe=@($MissingFields|Where-Object {$_ -notin $safe});$status='accepted';$claim='full';$next='continue';if($unsafe.Count){$status='claim-cap';$claim='claim-cap';$next='replan'};if($Mode -eq 'core-high-risk' -and $unsafe.Count){$status='blocked';$next='stop-and-report'}
 $display=$null;if($status -eq 'blocked'){$display='🛑⛔【ABCD核心阻断】 reason=UNRESOLVED_CORE_EVIDENCE action=stop-and-report'}
 [pscustomobject][ordered]@{schemaVersion=1;authority='ABCD-Authority-Kernel';mode=$Mode;profile=$profile;status=$status;claimLevel=$claim;nextAction=$next;normalizedFields=$normalized;unresolvedFields=$unsafe;selectedCapabilities=@($profile.capabilities);readBudget=$profile.readBudget;deepAudit=[bool]$profile.deep;finalGate=[bool]$profile.finalGate;mechanismsContinue=($status -ne 'blocked');displayLine=$display;nextOutputRequired=($status -eq 'blocked')}
}
Export-ModuleMember -Function Resolve-ESABCDAuthorityDecision
