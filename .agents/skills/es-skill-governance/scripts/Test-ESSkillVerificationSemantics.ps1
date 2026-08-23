[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ReportPath
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$skillsRoot=Join-Path $root '.agents/skills'
if(-not (Test-Path -LiteralPath $skillsRoot -PathType Container)){throw 'Missing .agents/skills'}
$utf8=New-Object Text.UTF8Encoding($false,$true)
$runtimePattern='(?i)runtime|unity|playmode|visual|profiler|reload.?domain|high.?dpi|compile|interaction|window'
$staticPattern='(?i)static|source|contract|hash|utf.?8|script|evidence|manifest|schema|read.?only|deterministic|boundary|structural'
$rows=@()
foreach($dir in Get-ChildItem -LiteralPath $skillsRoot -Directory | Sort-Object Name){
    $skillPath=Join-Path $dir.FullName 'SKILL.md'
    $govPath=Join-Path $dir.FullName 'governance.json'
    $text=[IO.File]::ReadAllText($skillPath,$utf8)
    $gov=$null
    try {
        $gov=Get-Content -LiteralPath $govPath -Raw -Encoding UTF8|ConvertFrom-Json
    }
    catch {
        Write-Warning ("Unable to parse governance profile for '" + $dir.Name + "': " + $_.Exception.Message)
    }
    $runtimeSignals=[regex]::Matches($text,$runtimePattern).Count
    $staticSignals=[regex]::Matches($text,$staticPattern).Count
    $declared=($null -ne $gov -and @($gov.PSObject.Properties.Name) -contains 'verificationProfiles')
    $classification=if($runtimeSignals -gt 0 -and $staticSignals -eq 0){'runtime-only'}elseif($runtimeSignals -gt 0 -and $staticSignals -gt 0){'dual-signal'}elseif($staticSignals -gt 0){'static-only'}else{'unclassified'}
    $rows += [pscustomobject]@{skill=$dir.Name;classification=$classification;runtimeSignals=$runtimeSignals;staticSignals=$staticSignals;explicitVerificationProfiles=$declared;runtimeRequiresReview=($runtimeSignals -gt 0 -and -not $declared)}
}
$result=[ordered]@{schemaVersion=1;validator='Test-ESSkillVerificationSemantics';generatedUtc=[DateTime]::UtcNow.ToString('o');staticRuntimePolicy='governance/references/verification-semantics.md';skillCount=$rows.Count;runtimeOnlyCount=@($rows|Where-Object classification -eq 'runtime-only').Count;missingExplicitProfileCount=@($rows|Where-Object {-not $_.explicitVerificationProfiles}).Count;rows=$rows}
if($ReportPath){
    if([IO.Path]::IsPathRooted($ReportPath)){throw 'ReportPath must be project-relative'}
    $full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$ReportPath))
    $rootPrefix=$root.TrimEnd([char]92,[char]47)+[char]92
    if(-not $full.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath escapes ProjectRoot'}
    $parent=Split-Path -Parent $full
    if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
    [IO.File]::WriteAllText($full,($result|ConvertTo-Json -Depth 8),$utf8)
}
$result|ConvertTo-Json -Depth 8
if($result.runtimeOnlyCount -gt 0){exit 1}else{exit 0}
