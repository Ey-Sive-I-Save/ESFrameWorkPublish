[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$files=@(
  'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs',
  'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationSceneScanPrototype.cs',
  'Assets/Plugins/ES/Editor/ESAutomation/ESFeishuReadAutomation.cs',
  'Assets/Plugins/ES/Editor/ESAutomation/ESFeishuTaskAutomation.cs'
)
$checks=[Collections.Generic.List[object]]::new()
foreach($relative in $files){
  $path=Join-Path $root $relative;$issues=[Collections.Generic.List[string]]::new()
  if(-not(Test-Path -LiteralPath $path -PathType Leaf)){[void]$issues.Add('missing-file')}
  else{
    $text=Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $matches=[regex]::Matches($text,'(?s)(?:completionDecision\.accepted\s*=\s*.*?CanAccept|decision\.accepted\s*=\s*.*?CanAccept)')
    foreach($m in $matches){
      $start=[math]::Max(0,$m.Index-2500);$length=[math]::Min(5000,$text.Length-$start);$window=$text.Substring($start,$length)
      if($window -notmatch 'CanAccept\s*\('){[void]$issues.Add("offset:$($m.Index):missing-CanAccept")}
      if($window -notmatch 'authorityDomain'){[void]$issues.Add("offset:$($m.Index):missing-authorityDomain")}
      if($window -notmatch 'evidence|criterionResults|executionStatus|traceReconciled'){[void]$issues.Add("offset:$($m.Index):missing-evidence-state")}
    }
  }
  [void]$checks.Add([pscustomobject][ordered]@{path=$relative;status=if($issues.Count){'failed'}else{'passed'};decisionWriteCount=if(Test-Path -LiteralPath $path -PathType Leaf){$matches.Count}else{0};issues=@($issues);sourceSha256=if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}else{$null}})
}
$failed=@($checks|Where-Object status -eq 'failed')
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESAuthorityDecisionDataflow';status=if($failed.Count){'failed'}else{'passed'};fileCount=$checks.Count;decisionWriteCount=($checks|Measure-Object decisionWriteCount -Sum).Sum;passedCount=@($checks|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;checks=$checks;runtimeStatus='runtime-not-run';claimsNotProven=@('compiler control-flow completeness','host invocation frequency','Unity runtime behavior');capturedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
