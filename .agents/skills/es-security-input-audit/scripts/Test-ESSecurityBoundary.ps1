[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
# Static audit contract: regex literals below are data used to detect unsafe
# text; this script never invokes, decodes, downloads, writes, or deletes it.
$targets=@((Join-Path $root '.agents/skills'),(Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation'))
$signals=@();foreach($dir in $targets){if(-not(Test-Path -LiteralPath $dir -PathType Container)){throw "Audit target missing: $dir"};foreach($f in Get-ChildItem -LiteralPath $dir -Recurse -File -Include *.ps1,*.py,*.cs,*.json -ErrorAction Stop|Where-Object{$_.FullName -notmatch '\\references\\' -and $_.Name -ne 'Test-ESSecurityBoundary.ps1'}){$line=0;foreach($l in [IO.File]::ReadLines($f.FullName)){ $line++;if($l -match '(?i)ExecutionPolicy\s+Bypass|-EncodedCommand|Invoke-Expression'){ $signals+="$($f.FullName):$line" }}}}
if($signals.Count -gt 0){throw "Unsafe input/execution signal(s): $($signals -join '; ')"}
Write-Output "PASS: security boundary scan is fail-closed; no high-risk execution signal found"
