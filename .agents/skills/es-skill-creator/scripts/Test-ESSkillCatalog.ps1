[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$ProjectRoot,
  [string]$CatalogPath = '.agents/SKILL_CATALOG.yaml'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$catalog=Join-Path $root $CatalogPath
if(-not (Test-Path -LiteralPath $catalog -PathType Leaf)){ throw "Skill catalog not found: $catalog" }
$raw=[IO.File]::ReadAllText($catalog,(New-Object Text.UTF8Encoding($false,$true)))
if($raw -notmatch '(?m)^schemaVersion:\s*1\s*$' -or $raw -notmatch '(?m)^skills:\s*$'){ throw 'Catalog schema header is invalid' }
$skillDirs=Get-ChildItem -LiteralPath (Join-Path $root '.agents\skills') -Directory
$names=@($skillDirs | Where-Object { Test-Path (Join-Path $_.FullName 'SKILL.md') } | ForEach-Object Name)
foreach($n in $names){
  if($raw -notmatch "(?m)^  $([regex]::Escape($n)):\s*$"){ throw "Missing catalog record: $n" }
}
$records=[regex]::Matches($raw,'(?m)^  ([a-z0-9][a-z0-9-]{0,63}):\s*$') | ForEach-Object { $_.Groups[1].Value }
$dupes=@($records | Group-Object | Where-Object Count -gt 1)
if($dupes.Count -gt 0){ throw "Duplicate catalog records: $($dupes.Name -join ', ')" }
foreach($n in $names){
  $dir=Join-Path $root ".agents\skills\$n"
  $skillHash=(Get-FileHash -LiteralPath (Join-Path $dir 'SKILL.md') -Algorithm SHA256).Hash.ToLowerInvariant()
  $gov=Join-Path $dir 'governance.json'
  $block=[regex]::Match($raw,"(?ms)^  $([regex]::Escape($n)):\s*\n(?:(?!^  [a-z0-9]).)*")
  if($block.Value -notmatch "skillHash:\s*$skillHash"){ throw "Stale skillHash: $n" }
  if(-not (Test-Path -LiteralPath $gov -PathType Leaf)) {
    if($block.Value -notmatch 'registrationState:\s*Draft' -or $block.Value -notmatch 'delivery:\s*NotReady') {
      throw "Missing governance.json without explicit Draft/NotReady catalog state: $n"
    }
    continue
  }
  $govHash=(Get-FileHash -LiteralPath $gov -Algorithm SHA256).Hash.ToLowerInvariant()
  if($block.Value -notmatch "governanceHash:\s*$govHash"){ throw "Stale governanceHash: $n" }
}
Write-Output "PASS: Skill Catalog contains $($names.Count) direct Skills with current hashes"
