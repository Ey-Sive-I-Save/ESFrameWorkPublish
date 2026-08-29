[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'; $worker=Join-Path $PSScriptRoot 'Invoke-ESOwnershipTransfer.ps1'; $dir=Join-Path $env:TEMP ('es-transfer-test-'+[Guid]::NewGuid().ToString('N')); [IO.Directory]::CreateDirectory($dir)|Out-Null
$path=Join-Path $dir 'transfer.json'; $space=('a'*32); $from=('b'*32); $to=('c'*32); $passed=0
function Run([string[]]$a){ $h=@{}; for($i=0;$i -lt $a.Count;$i+=2){$k=$a[$i].TrimStart('-');$v=$a[$i+1];if($k -eq 'ExpiresAtUtc'){$v=[datetime]$v};$h[$k]=$v}; Write-Verbose ("run "+$h.Action) -Verbose; & $worker @h }
Run @('-Action','Offer','-TransferPath',$path,'-SpaceId',$space,'-FromOwnerId',$from,'-ToOwnerId',$to,'-Reason','team takeover')|Out-Null
if(-not(Test-Path $path)){throw 'OfferDidNotCreateTransfer'}; $passed++
Run @('-Action','Validate','-TransferPath',$path)|Out-Null; $passed++
Run @('-Action','Accept','-TransferPath',$path,'-ExpectedRevision','1')|Out-Null
$receipts=@(Get-ChildItem $dir -Filter '*-receipt-2.json'); if($receipts.Count -ne 1){throw 'MissingAcceptReceipt'}; $passed++
$failed=$false; try { Run @('-Action','Revoke','-TransferPath',$path,'-ExpectedRevision','1')|Out-Null } catch { $failed=$true }; if(-not $failed){throw 'StaleRevisionWasAccepted'}; $passed++
Run @('-Action','Activate','-TransferPath',$path,'-ExpectedRevision','2')|Out-Null
Run @('-Action','Validate','-TransferPath',$path)|Out-Null; $passed++
$expired=Join-Path $dir 'expired.json'; Run @('-Action','Offer','-TransferPath',$expired,'-SpaceId',$space,'-FromOwnerId',$from,'-ToOwnerId',$to,'-Reason','expired','-ExpiresAtUtc',(Get-Date).ToUniversalTime().AddMinutes(-1).ToString('o'))|Out-Null
$failed=$false; try { Run @('-Action','Accept','-TransferPath',$expired,'-ExpectedRevision','1')|Out-Null } catch { $failed=$true }; if(-not $failed){throw 'ExpiredTransferWasAccepted'}; $passed++
[pscustomobject]@{status='passed';cases=$passed;runtimeStatus='runtime-not-run';fixture=$dir}|ConvertTo-Json -Compress
