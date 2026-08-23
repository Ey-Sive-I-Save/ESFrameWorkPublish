[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$ProjectRoot,
  [string]$OutputPath
)
$ErrorActionPreference='Stop'
$projectRootResolved=(Resolve-Path -LiteralPath $ProjectRoot).Path
$rootNormalized=$projectRootResolved.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
if(-not [string]::IsNullOrWhiteSpace($OutputPath)) {
  if([IO.Path]::IsPathRooted($OutputPath) -or $OutputPath -match '(^|[\\/])\.\.([\\/]|$)'){ throw 'OutputPath must be project-relative and cannot escape ProjectRoot.' }
  $outputFull=[IO.Path]::GetFullPath((Join-Path $rootNormalized $OutputPath))
  if(-not ($outputFull.Equals($rootNormalized,[StringComparison]::OrdinalIgnoreCase) -or $outputFull.StartsWith($rootNormalized + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){ throw 'OutputPath escapes ProjectRoot.' }
}
$warnings=Join-Path $projectRootResolved 'Assets/Plugins/ES/AIWarnings'
if(-not (Test-Path -LiteralPath $warnings -PathType Container)){throw "AIWarnings root not found: $warnings"}
$domains=@(Get-ChildItem -LiteralPath $warnings -Directory | Sort-Object Name | ForEach-Object {
  $md=@(Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter '*.md')
  $fileEntries=@($md | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($projectRootResolved.Length+1).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLower()} })
  [ordered]@{domain=$_.Name; markdownFiles=$md.Count; filesIncludingMeta=@(Get-ChildItem -LiteralPath $_.FullName -Recurse -File).Count; files=$fileEntries; sha256=((($fileEntries | ForEach-Object {$_.sha256}) -join '') | ForEach-Object { $sha=[Security.Cryptography.SHA256]::Create(); ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($_)))).Replace('-','').ToLower() })}
})
$markdownTotal=0; $fileTotal=0; foreach($d in $domains){$markdownTotal += [int]$d.markdownFiles; $fileTotal += [int]$d.filesIncludingMeta}
$fileTotal += @((Get-ChildItem -LiteralPath $warnings -File)).Count
$result=[ordered]@{schemaVersion=1; generatedAtUtc=[DateTime]::UtcNow.ToString('o'); sourceRoot='Assets/Plugins/ES/AIWarnings'; domains=$domains; markdownFiles=$markdownTotal; filesIncludingMeta=$fileTotal}
$json=$result|ConvertTo-Json -Depth 6
if([string]::IsNullOrWhiteSpace($OutputPath)){ $json } else { [IO.File]::WriteAllText((Join-Path (Resolve-Path -LiteralPath $ProjectRoot).Path $OutputPath),$json,(New-Object Text.UTF8Encoding($false,$true))); Write-Output "Wrote $OutputPath" }
