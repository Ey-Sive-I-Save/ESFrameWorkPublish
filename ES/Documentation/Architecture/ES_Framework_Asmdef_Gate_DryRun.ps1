param(
    [string]$ProjectRoot = 'F:\aaProject\ESFrameWorkPublish',
    [switch]$ShowAll
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ProjectRoot)) {
    throw "ProjectRoot not found: $ProjectRoot"
}

$root = (Resolve-Path -LiteralPath $ProjectRoot).Path

function Get-AsmdefGuidMap {
    param([string]$Root)
    $map = @{}
    Get-ChildItem -Path $Root -Recurse -File -Filter '*.asmdef.meta' -ErrorAction SilentlyContinue | ForEach-Object {
        $text = Get-Content -LiteralPath $_.FullName -Encoding UTF8 -Raw
        if ($text -match 'guid:\s*([0-9a-f]{32})') {
            $asmdefPath = $_.FullName -replace '\.meta$', ''
            try {
                $asm = Get-Content -LiteralPath $asmdefPath -Encoding UTF8 -Raw | ConvertFrom-Json
                $map[$Matches[1]] = $asm.name
            } catch {
                $map[$Matches[1]] = '<unparsed>'
            }
        }
    }
    return $map
}

function Resolve-References {
    param([object]$Asmdef, [hashtable]$GuidMap)
    $result = @()
    foreach ($raw in @($Asmdef.references)) {
        if ($raw -match '^GUID:([0-9a-f]{32})$') {
            $name = $GuidMap[$Matches[1]]
            if (-not $name) {
                $name = "<UNRESOLVED:$($Matches[1])>"
            }
            $result += $name
        } else {
            $result += $raw
        }
    }
    return $result
}

function Get-AsmdefClass {
    param([object]$Asmdef)
    if ($Asmdef.name -match 'Obsolete') { return 'Obsolete' }
    if ($Asmdef.name -match 'Tests') { return 'Tests' }
    if (@($Asmdef.includePlatforms) -contains 'Editor') { return 'Editor' }
    if ($Asmdef.name -eq 'ESPlayer') { return 'Player' }
    if ($Asmdef.name -eq 'ESFramework.AITest.Contracts') { return 'AITestContracts' }
    if ($Asmdef.name -eq 'ESFramework.AITest.Runtime') { return 'AITestRuntime' }
    if ($Asmdef.name -eq 'ES_AITest.Runtime.Adapters') { return 'AITestAdapters' }
    return 'Runtime'
}

function Test-DefineConstraintsCover {
    param([object]$Source, [object]$Target)
    foreach ($required in @($Target.DefineConstraints)) {
        if (@($Source.DefineConstraints) -notcontains $required) {
            return $false
        }
    }
    return $true
}

$guidMap = Get-AsmdefGuidMap -Root $root
$asmdefs = @()

Get-ChildItem -Path (Join-Path $root 'Assets') -Recurse -File -Filter '*.asmdef' -ErrorAction SilentlyContinue | ForEach-Object {
    $asm = Get-Content -LiteralPath $_.FullName -Encoding UTF8 -Raw | ConvertFrom-Json
    $asmdefs += [PSCustomObject]@{
        Name = $asm.name
        Path = $_.FullName
        Class = Get-AsmdefClass -Asmdef $asm
        References = @(Resolve-References -Asmdef $asm -GuidMap $guidMap)
        DefineConstraints = @($asm.defineConstraints)
    }
}

$aitestPackage = Join-Path $root 'Packages\com.esframework.aitest'
if (Test-Path -LiteralPath $aitestPackage) {
    Get-ChildItem -Path $aitestPackage -Recurse -File -Filter '*.asmdef' -ErrorAction SilentlyContinue | ForEach-Object {
        $asm = Get-Content -LiteralPath $_.FullName -Encoding UTF8 -Raw | ConvertFrom-Json
        $asmdefs += [PSCustomObject]@{
            Name = $asm.name
            Path = $_.FullName
            Class = Get-AsmdefClass -Asmdef $asm
            References = @(Resolve-References -Asmdef $asm -GuidMap $guidMap)
            DefineConstraints = @($asm.defineConstraints)
        }
    }
}

$byName = @{}
foreach ($asm in $asmdefs) {
    if (-not $byName.ContainsKey($asm.Name)) {
        $byName[$asm.Name] = $asm
    }
}

$baselineViolations = @()
$pendingDecisions = @()

foreach ($asm in $asmdefs) {
    foreach ($refName in @($asm.References)) {
        $target = $null
        if ($refName -and $byName.ContainsKey($refName)) {
            $target = $byName[$refName]
        }

        if ($asm.Class -in @('Runtime', 'Player') -and $target -and $target.Class -eq 'Editor') {
            $baselineViolations += [PSCustomObject]@{
                Rule = 'RuntimeMustNotReferenceEditor'
                Source = $asm.Name
                Target = $refName
                Detail = "$($asm.Path) references $($target.Path)"
            }
        }

        if ($asm.Class -eq 'Editor' -and $target -and $target.Class -in @('AITestContracts', 'AITestRuntime', 'AITestAdapters') -and -not (Test-DefineConstraintsCover -Source $asm -Target $target)) {
            $baselineViolations += [PSCustomObject]@{
                Rule = 'EditorDefineMustCoverAITestDependency'
                Source = $asm.Name
                Target = $refName
                Detail = "$($asm.Path) references $($target.Path) without covering its defineConstraints"
            }
        }

        if ($asm.Class -notin @('Tests', 'Editor', 'Obsolete') -and $target -and $target.Class -eq 'Obsolete') {
            $baselineViolations += [PSCustomObject]@{
                Rule = 'ActiveMustNotReferenceObsolete'
                Source = $asm.Name
                Target = $refName
                Detail = "$($asm.Path) references obsolete assembly $($target.Path)"
            }
        }
    }
}

Write-Output "ProjectRoot: $root"
Write-Output "Assemblies inspected: $($asmdefs.Count)"
Write-Output "BaselineViolations: $($baselineViolations.Count)"
Write-Output "PendingDecisions: $($pendingDecisions.Count)"

if ($baselineViolations.Count -gt 0) {
    Write-Output ''
    Write-Output '--- Baseline Violations ---'
    $baselineViolations | Format-Table -AutoSize -Wrap
}

if ($pendingDecisions.Count -gt 0) {
    Write-Output ''
    Write-Output '--- Pending Decisions ---'
    $pendingDecisions | Format-Table -AutoSize -Wrap
}

if ($ShowAll) {
    Write-Output ''
    Write-Output '--- Assembly Matrix ---'
    $asmdefs | Sort-Object Class, Name | Select-Object Name, Class, @{Name='References'; Expression={$_.References -join ', '}} | Format-Table -AutoSize -Wrap
}
