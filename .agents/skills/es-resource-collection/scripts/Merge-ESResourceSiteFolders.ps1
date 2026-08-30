[CmdletBinding()]
param(
    [string]$Root = 'ES/AISpace/Public/可用资源站点',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$open = [char]0x3010; $close = [char]0x3011; $sep = [char]0x3001; $check = [char]0x2713; $cross = [char]0x2717
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$dirs = @(Get-ChildItem -LiteralPath $rootPath -Directory)
$groups = @{}
foreach ($dir in $dirs) {
    if (-not $dir.Name.StartsWith($open)) { continue }
    $firstClose = $dir.Name.IndexOf($close)
    if ($firstClose -lt 2) { continue }
    $core = $dir.Name.Substring($dir.Name.IndexOf($close) + 1)
    if ($core.Contains($open)) { $domain = $core.Substring(0, $core.IndexOf($open)) } else { $domain = $core }
    if (-not $groups.ContainsKey($domain)) { $groups[$domain] = @() }
    $groups[$domain] += $dir
}

foreach ($domain in ($groups.Keys | Sort-Object)) {
    $members = @($groups[$domain])
    $allFiles = @($members | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse })
    $resources = @($allFiles | Where-Object { $_.Name -notin @('README.md','site.md','provenance.json') })
    $available = $resources.Count -ge 10 -and -not ($resources | Where-Object { $_.Extension -match '^(?i)\.(zip|rar|7z|tar|gz)$' })
    $types = New-Object System.Collections.Generic.List[string]
    foreach ($member in $members) {
        foreach ($childDir in @(Get-ChildItem -LiteralPath $member.FullName -Directory)) {
            if ($childDir.Name -notin @('resources','assets','downloads','package','packages','sample')) {
                if (-not $types.Contains($childDir.Name)) { $types.Add($childDir.Name) }
            }
        }
    }
    if ($types.Count -eq 0) { $types.Add('pending') }
    $typeText = ($types | Select-Object -First 4) -join $sep
    $prefix = if ($available) { $open + $check + $close } else { $open + $cross + $close }
    $targetName = $prefix + $open + $typeText + $close + $domain
    $target = Join-Path $rootPath $targetName
    Write-Output (('{0} <- {1} | resources={2}' -f $targetName, ($members.Name -join ', '), $resources.Count))
    if (-not $Apply) { continue }
    if (-not (Test-Path -LiteralPath $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
    foreach ($metaName in @('site.md','provenance.json')) {
        $metaSource = $members | ForEach-Object { Join-Path $_.FullName $metaName } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        $metaTarget = Join-Path $target $metaName
        if ($metaSource -and -not (Test-Path -LiteralPath $metaTarget)) { Copy-Item -LiteralPath $metaSource -Destination $metaTarget }
    }
    foreach ($member in $members) {
        if ($member.FullName -eq $target) { continue }
        foreach ($child in @(Get-ChildItem -LiteralPath $member.FullName -Force)) {
            if ($child.Name -in @('README.md','site.md','provenance.json')) { continue }
            $destination = Join-Path $target $child.Name
            if (Test-Path -LiteralPath $destination) {
                if ($child.PSIsContainer) {
                    foreach ($item in @(Get-ChildItem -LiteralPath $child.FullName -Force -Recurse)) {
                        if ($item.PSIsContainer) { continue }
                        $relative = $item.FullName.Substring($child.FullName.Length).TrimStart('\')
                        $destFile = Join-Path $destination $relative
                        $destParent = Split-Path -Parent $destFile
                        if (-not (Test-Path -LiteralPath $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
                        if (Test-Path -LiteralPath $destFile) {
                            $a=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash; $b=(Get-FileHash -LiteralPath $destFile -Algorithm SHA256).Hash
                            if ($a -ne $b) { throw "Collision: $destFile" }
                        } else { Move-Item -LiteralPath $item.FullName -Destination $destFile }
                    }
                } else {
                    $a=(Get-FileHash -LiteralPath $child.FullName -Algorithm SHA256).Hash; $b=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
                    if ($a -ne $b) { throw "Collision: $destination" }
                }
            } else { Move-Item -LiteralPath $child.FullName -Destination $destination }
        }
        if ((Get-ChildItem -LiteralPath $member.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $member.FullName -Force }
    }
    $readme = Join-Path $target 'README.md'
    $text = "# $domain`r`n`r`nStatus: $prefix`r`nTypes: $typeText`r`nSite exploration notes and provenance are recorded in site.md and provenance.json.`r`n"
    [System.IO.File]::WriteAllText($readme, $text, (New-Object System.Text.UTF8Encoding($false)))
    $siteMd = Join-Path $target 'site.md'
    if (-not (Test-Path -LiteralPath $siteMd)) { [System.IO.File]::WriteAllText($siteMd, "# $domain exploration notes`r`n`r`n- Site: $domain`r`n- Technique: prefer site search and public direct links; record license, format, size, and download limits.`r`n", (New-Object System.Text.UTF8Encoding($false))) }
}
