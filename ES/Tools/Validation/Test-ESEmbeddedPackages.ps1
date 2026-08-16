[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path

$packages = @(
    [pscustomobject]@{
        Name = 'com.code-philosophy.hybridclr'
        Path = Join-Path $root 'Packages/com.code-philosophy.hybridclr'
        Remote = 'https://github.com/focus-creative-games/hybridclr_unity.git#v8.12.0'
        MenuPrefix = 'HybridCLR/'
    },
    [pscustomobject]@{
        Name = 'com.code-philosophy.luban'
        Path = Join-Path $root 'Packages/com.code-philosophy.luban'
        Remote = 'https://github.com/focus-creative-games/luban_unity.git'
        MenuPrefix = 'Luban/'
    }
)

$hardFailures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$manifestPath = Join-Path $root 'Packages/manifest.json'
$lockPath = Join-Path $root 'Packages/packages-lock.json'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    $hardFailures.Add("缺少 Packages/manifest.json：$manifestPath")
}

$manifest = $null
$lock = $null
if ($hardFailures.Count -eq 0) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
    $lock = Get-Content -LiteralPath $lockPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

foreach ($package in $packages) {
    if (-not (Test-Path -LiteralPath $package.Path -PathType Container)) {
        $hardFailures.Add("嵌入包目录不存在：$($package.Path)")
        continue
    }

    $packageJsonPath = Join-Path $package.Path 'package.json'
    if (-not (Test-Path -LiteralPath $packageJsonPath -PathType Leaf)) {
        $hardFailures.Add("缺少 package.json：$packageJsonPath")
        continue
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($packageJson.name -ne $package.Name) {
        $hardFailures.Add("package.json.name 不匹配：目录=$($package.Name)，实际=$($packageJson.name)")
    }

    $nestedGit = Get-ChildItem -LiteralPath $package.Path -Force -Recurse -Directory -Filter '.git' -ErrorAction SilentlyContinue
    if ($nestedGit) {
        $hardFailures.Add("嵌入包包含嵌套 .git：$($nestedGit.FullName -join ', ')")
    }

    $activeMenuAttributes = @(
        $menuPattern = '\[MenuItem\("' + [regex]::Escape($package.MenuPrefix)
        Get-ChildItem -LiteralPath $package.Path -Recurse -File -Filter '*.cs' |
            ForEach-Object {
                $file = $_
                Get-Content -LiteralPath $file.FullName -Encoding UTF8 |
                    Where-Object { $_ -match $menuPattern -and $_ -notmatch '^\s*//' } |
                    ForEach-Object { "$($file.FullName):$_" }
            }
    )
    if ($activeMenuAttributes.Count -gt 0) {
        $hardFailures.Add("发现未注释的 $($package.MenuPrefix) 菜单注册：$($activeMenuAttributes -join ' | ')")
    }

    if ($null -eq $manifest -or -not ($manifest.dependencies.PSObject.Properties.Name -contains $package.Name)) {
        $hardFailures.Add('manifest.json missing dependency: ' + [string]$package.Name)
    }
    elseif ($manifest.dependencies.($package.Name) -ne $package.Remote) {
        $warnings.Add('manifest fallback declaration changed: ' + [string]$package.Name)
    }

    if ($null -ne $lock -and $lock.dependencies.PSObject.Properties.Name -contains $package.Name) {
        $lockEntry = $lock.dependencies.($package.Name)
        if ($lockEntry.source -ne 'embedded') {
            $warningText = 'packages-lock.json source is not embedded: ' + [string]$package.Name + ' source=' + [string]$lockEntry.source
            $warnings.Add($warningText)
        }
    }
    else {
        $warnings.Add('packages-lock.json entry missing: ' + [string]$package.Name)
    }
}

Write-Output "ES Embedded Package Gate"
Write-Output "ProjectRoot: $root"
Write-Output "Packages: $($packages.Name -join ', ')"
Write-Output "HardFailures: $($hardFailures.Count)"
Write-Output "Warnings: $($warnings.Count)"

if ($warnings.Count -gt 0) {
    Write-Output 'Warnings:'
    $warnings | ForEach-Object { Write-Output "  - $_" }
}
if ($hardFailures.Count -gt 0) {
    Write-Output 'Failures:'
    $hardFailures | ForEach-Object { Write-Output "  - $_" }
    exit 1
}

Write-Output 'Result: PASS (embedded packages present, names match, menus isolated)'
if ($warnings.Count -gt 0) {
    Write-Output 'Note: warnings are evidence gaps; Unity Package Manager must still be observed to confirm Embedded.'
}
exit 0
