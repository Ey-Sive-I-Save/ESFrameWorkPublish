[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$TransferPath)
$ErrorActionPreference='Stop';$u=[Text.UTF8Encoding]::new($false,$true);$r=Get-Content -Raw -Encoding UTF8 $TransferPath|ConvertFrom-Json
$files=@(Get-ChildItem (Split-Path $TransferPath) -Filter ($r.transferId+'-receipt-*.json') -File);$nodes=@{};$zero='0'*64
foreach($f in $files){$q=Get-Content -Raw -Encoding UTF8 $f.FullName|ConvertFrom-Json;if($q.transferId -ne $r.transferId -or $q.spaceId -ne $r.spaceId){throw 'ReceiptBindingMismatch'};$rev=[int]$q.revision;if($nodes.ContainsKey($rev)){throw "DuplicateRevision:$rev"};$nodes[$rev]=$q}
if($nodes.Count -gt 0){$ordered=@($nodes.Values|Sort-Object revision);if($ordered[0].previousReceiptHash -ne $zero){throw 'GenesisMismatch'};$prev=$ordered[0].receiptHash;for($i=1;$i -lt $ordered.Count;$i++){if([int]$ordered[$i].revision -ne ([int]$ordered[$i-1].revision+1)){throw 'RevisionGapOrRollback'};if($ordered[$i].previousReceiptHash -ne $prev){throw 'BrokenChainOrFork'};$prev=$ordered[$i].receiptHash};if($ordered[-1].receiptHash -ne $r.receiptHash){throw 'LatestReceiptMismatch'}}
[pscustomobject]@{status='passed';receipts=$nodes.Count;maxRevision=if($nodes.Count){[int](($nodes.Values|Measure-Object revision -Maximum).Maximum)}else{[int]$r.expectedRevision};runtimeStatus='runtime-not-run'}|ConvertTo-Json -Compress
