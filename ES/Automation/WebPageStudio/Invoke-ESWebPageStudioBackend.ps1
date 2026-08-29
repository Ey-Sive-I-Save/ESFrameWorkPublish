[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory=$true)][string]$ContractPath,
    [Parameter(Mandatory=$true)][ValidateSet('GET','HEAD')][string]$Method,
    [string]$ResourcePath = '/',
    [switch]$ExecuteNetwork,
    [ValidateRange(1,300)][int]$TimeoutSeconds = 10,
    [string]$ReceiptPath = ''
)
$ErrorActionPreference='Stop'; $OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
$full=(Resolve-Path -LiteralPath $ContractPath -ErrorAction Stop).Path
if(-not $full.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ContractPath must remain under the project root.'}
$contract=Get-Content -LiteralPath $full -Encoding UTF8 -Raw|ConvertFrom-Json
if([string]$contract.recordType -ne 'WebPageStudioBackendContract'){throw 'Unsupported backend contract.'}
if([string]$contract.operationPolicy.readOnly -ne 'True' -or @($contract.operationPolicy.allowedMethods) -notcontains $Method){throw 'Only allowlisted read-only methods are permitted.'}
if([string]$contract.mode -ne 'user-authorized-service' -or -not [bool]$contract.network.enabled){throw 'A user-authorized-service contract with network enabled is required.'}
if(-not $ExecuteNetwork){
  [pscustomobject]@{status='not-run';reason='ExecuteNetwork switch not provided';networkCalls=0;contractId=$contract.contractId;runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 8
  exit 0
}
$base=[Uri]$contract.network.apiBase; $target=[Uri]::new($base, $ResourcePath)
if(@($contract.network.allowlist|Where-Object { $_ -eq $target.Host -or ($_ -like '*.*' -and $target.Host.EndsWith(($_ -replace '^\*',''))) }).Count -eq 0){throw 'Target host is not covered by the contract allowlist.'}
if([string]::IsNullOrWhiteSpace($ReceiptPath)){$ReceiptPath=Join-Path (Split-Path $full) 'backend-runtime-receipt.json'}
$receiptFull=[IO.Path]::GetFullPath($ReceiptPath); if(-not $receiptFull.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ReceiptPath must remain under the project root.'}
$max=[int]$contract.retry.maxAttempts; $attempt=0; $response=$null; $lastError=''
while($attempt -le $max){$attempt++; try{$response=Invoke-WebRequest -Method $Method -Uri $target.AbsoluteUri -TimeoutSec ([Math]::Min($TimeoutSeconds,[int]$contract.network.timeoutSeconds)) -MaximumRedirection 0 -UseBasicParsing; break}catch{$lastError=$_.Exception.Message;if($attempt -le $max){Start-Sleep -Milliseconds ([int]([Math]::Min([double]$contract.retry.maxBackoffSeconds,([double]$contract.retry.backoffSeconds*$attempt))*1000))}}}
$status=if($null -ne $response){'passed'}else{'failed'}
$record=[ordered]@{schemaVersion=1;recordType='WebPageStudioBackendRuntimeReceipt';status=$status;contractId=$contract.contractId;method=$Method;targetHost=$target.Host;attempts=$attempt;networkCalls=if($null -ne $response){1}else{0};httpStatus=if($null -ne $response){[int]$response.StatusCode}else{0};error=if($status -eq 'failed'){'[REDACTED]'}else{''};runtimeStatus=if($status -eq 'passed'){'runtime-passed'}else{'runtime-failed'};nonClaims=@('Response body and sensitive headers are not persisted.','This receipt does not prove production availability or security posture.')}
$parent=Split-Path $receiptFull;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($receiptFull,($record|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false));$record|ConvertTo-Json -Depth 8
