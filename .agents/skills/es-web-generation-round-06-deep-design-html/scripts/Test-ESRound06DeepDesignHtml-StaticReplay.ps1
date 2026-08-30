[CmdletBinding()]param([string]$ProjectRoot='.')
$ErrorActionPreference='Stop';$m=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '..\static-replay.manifest.json')|ConvertFrom-Json
if(@($m.cases).Count -lt 7){throw 'static replay manifest must contain seven cases'}
[pscustomobject]@{status='passed';skill='es-web-generation-round-06-deep-design-html';cases=@($m.cases).Count;runtimeStatus='runtime-not-run'}
