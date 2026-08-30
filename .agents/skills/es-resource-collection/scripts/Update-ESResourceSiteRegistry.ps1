[CmdletBinding()]
param(
  [ValidateSet('Register','Remove')][string]$Action,
  [Parameter(Mandatory=$true)][string]$SiteId,
  [string]$DisplayName,
  [string[]]$Categories = @(),
  [string]$Reason = '',
  [string]$RegistryPath = 'ES/AISpace/Public/可用资源站点/SITES.json'
)
$ErrorActionPreference='Stop'
$full=(Join-Path (Get-Location) $RegistryPath)
$state=@{schemaVersion=1;revision=0;sites=@()}
if(Test-Path -LiteralPath $full){$state=Get-Content $full -Raw -Encoding UTF8|ConvertFrom-Json}
$items=@($state.sites)|Where-Object {$_.siteId -ne $SiteId}
$now=[DateTime]::UtcNow.ToString('o')
if($Action -eq 'Register'){$entry=[pscustomobject]@{siteId=$SiteId;displayName=$DisplayName;categories=$Categories;status='active';updatedAtUtc=$now;reason=$Reason};$items += $entry}
else{$items += [pscustomobject]@{siteId=$SiteId;displayName=$DisplayName;categories=$Categories;status='removed';updatedAtUtc=$now;reason=$Reason}}
$out=[ordered]@{schemaVersion=1;revision=([int]$state.revision+1);sites=@($items|Sort-Object siteId)}
$dir=Split-Path -Parent $full; New-Item -ItemType Directory -Path $dir -Force|Out-Null
[IO.File]::WriteAllText($full,($out|ConvertTo-Json -Depth 6),(New-Object Text.UTF8Encoding($false)))
$out|ConvertTo-Json -Depth 6
