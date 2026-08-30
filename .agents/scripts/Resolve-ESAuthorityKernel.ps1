[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ResolverReceipt,
    [string]$HostConsumerId = '',
    [string]$ConsumedAtUtc = '',
    [string]$CorrelationId = ([guid]::NewGuid().ToString()),
    [string]$HostPromptHash = '',
    [string]$HostResolverReceiptHash = '',
    [string]$HostCorrelationId = ''
)
$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$resolver = $ResolverReceipt | ConvertFrom-Json
$decisionStatus = if ($null -ne $resolver.decisionStatus) { [string]$resolver.decisionStatus } elseif ([string]$resolver.status -eq 'triggered') { 'StaticPassed' } elseif ([string]$resolver.status -eq 'review') { 'ClaimCap' } elseif ([string]$resolver.status -eq 'ambiguous') { 'Blocked' } else { 'StaticPassed' }
$raw = [string]$resolver.input.rawPrompt
$promptHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash([Text.Encoding]::UTF8.GetBytes($raw))).Replace('-','').ToLowerInvariant()
$canonical = $resolver | ConvertTo-Json -Depth 30 -Compress
$resolverHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-','').ToLowerInvariant()
$core = @('executionIntent','canExecute','authorityDecision')
$missing=@($core | Where-Object { $null -eq $resolver.$_ -or ([string]::IsNullOrWhiteSpace([string]$resolver.$_) -and $_ -ne 'canExecute') })
$status='ClaimCap'; $completion=$false; $reason='HostConsumptionUnverified'
if($missing.Count -gt 0){$status='ClaimCap';$reason='core-decision-field-missing';$completion=$false}
elseif(-not [string]::IsNullOrWhiteSpace($HostConsumerId) -and -not [string]::IsNullOrWhiteSpace($ConsumedAtUtc) -and $decisionStatus -eq 'StaticPassed' -and [string]$resolver.finalDisposition -notmatch 'p0|review|clarify'){
  $status='Accepted';$reason='host-consumption-receipt-present';$completion=$true
}
if($status -eq 'Accepted' -and ((-not [string]::IsNullOrWhiteSpace($HostPromptHash) -and $HostPromptHash.ToLowerInvariant() -ne $promptHash) -or (-not [string]::IsNullOrWhiteSpace($HostResolverReceiptHash) -and $HostResolverReceiptHash.ToLowerInvariant() -ne $resolverHash) -or (-not [string]::IsNullOrWhiteSpace($HostCorrelationId) -and $HostCorrelationId -ne $CorrelationId))){$status='Blocked';$reason='host-receipt-hash-or-correlation-mismatch';$completion=$false}
[ordered]@{
 schemaVersion=1; correlationId=$CorrelationId; promptHash=$promptHash; resolverReceiptHash=$resolverHash
 decisionStatus=$status; priorDecisionStatus=$decisionStatus; executionIntent=[string]$resolver.executionIntent; canExecute=[bool]$resolver.canExecute
 authorityDecision=[string]$resolver.authorityDecision; hostConsumerId=if($HostConsumerId){$HostConsumerId}else{$null}
 consumedAtUtc=if($ConsumedAtUtc){$ConsumedAtUtc}else{$null}; completionAccepted=$completion
 finalDisposition=$reason; missingCoreFields=$missing; normalizedFields=@('promptHash','resolverReceiptHash','decisionStatus'); correctionCycle=if($reason -eq 'core-decision-field-missing'){'evidence-correction-required'}else{$null}
 claimsNotProven=@('host action semantics unless host receipt is supplied','Unity/Runtime/Player behavior')
} | ConvertTo-Json -Depth 10
