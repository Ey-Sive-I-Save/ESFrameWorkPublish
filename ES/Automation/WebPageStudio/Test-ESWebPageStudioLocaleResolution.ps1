[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ManifestPath,[string]$OutputPath='')
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\';$manifest=(Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path;if(-not $manifest.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ManifestPath must remain under project root.'}
$resolver=Join-Path $root 'ES/Automation/WebPageStudio/Resolve-ESWebPageStudioLocale.ps1';$cases=@(
 [ordered]@{id='query-over-header';query='ar';header='zh-CN, en;q=0.8';expected='ar'},
 [ordered]@{id='exact-header';query='';header='zh-CN, en;q=0.8';expected='zh-CN'},
 [ordered]@{id='base-header';query='';header='zh, en;q=0.8';expected='zh-CN'},
 [ordered]@{id='fallback';query='';header='fr-FR';expected='en'})
$results=[System.Collections.Generic.List[object]]::new();foreach($case in $cases){$args=@{ManifestPath=$manifest;AcceptLanguage=$case.header;QueryLanguage=$case.query};$r=& $resolver @args|ConvertFrom-Json;$results.Add([pscustomobject]@{case=$case.id;expected=$case.expected;selected=$r.selectedLocale;status=if($r.selectedLocale -eq $case.expected){'passed'}else{'failed'};reason=$r.reason})}
$failed=@($results|? status -eq failed).Count;$record=[ordered]@{schemaVersion=1;recordType='WebPageStudioLocaleResolutionReceipt';status=if($failed -eq 0){'passed'}else{'failed'};manifestPath=$manifest;caseCount=$results.Count;passedCount=($results|? status -eq passed).Count;failedCount=$failed;cases=$results;evidenceLevel='S1';runtimeStatus='runtime-not-run';network='disabled';claimsNotProven=@('This is deterministic parser coverage and does not prove browser or server locale negotiation.')}
if(-not [string]::IsNullOrWhiteSpace($OutputPath)){ $out=[IO.Path]::GetFullPath($OutputPath);if(-not $out.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath must remain under project root.'};[IO.File]::WriteAllText($out,($record|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))};$record|ConvertTo-Json -Depth 8
