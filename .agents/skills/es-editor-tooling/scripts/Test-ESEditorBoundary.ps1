[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;$map=Join-Path $root '.agents/skills/es-editor-tooling/references/project-map.md';if(-not(Test-Path -LiteralPath $map -PathType Leaf)){throw 'Editor project map missing.'}
$bridge=(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation') -File -Filter '*.cs'|Where-Object{$_.Name -match 'Bridge'}|Select-Object -First 1);if($null -eq $bridge){throw 'Editor automation bridge source missing.'};$text=[IO.File]::ReadAllText($bridge.FullName,[Text.UTF8Encoding]::new($false,$true));foreach($token in @('modifyActiveScene','dryRun','operations','Undo')){if($text -notmatch [regex]::Escape($token)){throw "Editor boundary token missing: $token"}}
Write-Output 'PASS: editor tooling boundary exposes guarded scene mutation and project map'
