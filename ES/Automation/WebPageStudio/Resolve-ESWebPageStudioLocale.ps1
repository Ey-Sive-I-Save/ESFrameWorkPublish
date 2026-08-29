[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$ManifestPath,
 [string]$AcceptLanguage='en',
 [string]$QueryLanguage=''
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\';$full=(Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path
if(-not $full.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ManifestPath must remain under project root.'}
$m=Get-Content -Raw -Encoding UTF8 $full|ConvertFrom-Json;$supported=@($m.locales|%{[string]$_});if($supported.Count -eq 0){throw 'Manifest has no locales.'}
$chosen='';$reason='default';$query=($QueryLanguage -replace '_','-');if($supported -contains $query){$chosen=$query;$reason='query'}
if(-not $chosen){$prefs=@($AcceptLanguage -split ','|%{ $p=($_ -split ';')[0].Trim() -replace '_','-';if($p -and $p -ne '*'){$p}});foreach($p in $prefs){$exact=$supported|?{$_ -ieq $p}|select -First 1;if($exact){$chosen=$exact;$reason='accept-language';break};$base=$p.Split('-')[0];$partial=$supported|?{$_ -ieq $base -or $_ -like "$base-*"}|select -First 1;if($partial){$chosen=$partial;$reason='accept-language-base';break}}}
if(-not $chosen){$chosen=if($supported -contains 'en'){'en'}else{$supported[0]};$reason='fallback'}
$entry=$m.entries|? locale -ieq $chosen|select -First 1
[pscustomobject]@{schemaVersion=1;recordType='WebPageStudioLocaleResolution';status='resolved';manifestPath=$full;queryLanguage=$QueryLanguage;acceptLanguage=$AcceptLanguage;selectedLocale=$chosen;reason=$reason;fallbackChain=@($entry.fallbackChain);directory=$entry.directory;contract=$entry.contract;runtimeStatus='runtime-not-run';network='disabled';nonClaims=@('This deterministic resolver does not inspect browser headers or perform server-side negotiation.')}|ConvertTo-Json -Depth 8
