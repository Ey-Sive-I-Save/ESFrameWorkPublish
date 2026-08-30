[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$files=@(
 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs',
 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationSceneScanPrototype.cs',
 'Assets/Plugins/ES/Editor/ESAutomation/ESFeishuReadAutomation.cs',
 'Assets/Plugins/ES/Editor/ESAutomation/ESFeishuTaskAutomation.cs',
 'Assets/Plugins/ES/Editor/ESAutomation/ESFeishuIdentityAutomation.cs',
 'Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs',
 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
)
$checks=foreach($relative in $files){$path=Join-Path $root $relative;$issues=[Collections.Generic.List[string]]::new();$count=0
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){[void]$issues.Add('missing-file')}
 else{$text=Get-Content -LiteralPath $path -Raw -Encoding UTF8;$methods=[regex]::Matches($text,'(?s)(?:private|public|internal)\s+static\s+ESAutomationRunRecord\s+\w*Read\w*Record\s*\([^)]*\)\s*\{.*?\n\s*\}')
  foreach($method in $methods){$count++;if($method.Value -notmatch 'record\.Validate\s*\(\s*\)'){[void]$issues.Add("read-method-at:$($method.Index):missing-record-validate")}}
  foreach($match in [regex]::Matches($text,'DeserializeObject<ESAutomationRunRecord>')){
      $window=$text.Substring($match.Index,[math]::Min(900,$text.Length-$match.Index))
      if($window -notmatch 'record\.Validate\s*\(\s*\)'){[void]$issues.Add("deserialize-at:$($match.Index):missing-record-validate")}
  }
  foreach($match in [regex]::Matches($text,'(?:DeserializeObject\s*<\s*ESAutomationRunResult\s*>|ToObject\s*<\s*ESAutomationRunResult\s*>|DeserializeObject\s*<\s*ESAutomationRunResult)')){
      $window=$text.Substring($match.Index,[math]::Min(1100,$text.Length-$match.Index))
      if($window -notmatch '(?m)\b(?:result|parsed|finalResult)\.Validate\s*\(\s*\)'){
          [void]$issues.Add("run-result-deserialize-at:$($match.Index):missing-result-validate")
      }
  }
 }
 [pscustomobject][ordered]@{path=$relative;readMethodCount=$count;runResultDeserializeCount=if(Test-Path -LiteralPath $path -PathType Leaf){@([regex]::Matches((Get-Content -LiteralPath $path -Raw -Encoding UTF8),'(?:DeserializeObject\s*<\s*ESAutomationRunResult\s*>|ToObject\s*<\s*ESAutomationRunResult\s*>|DeserializeObject\s*<\s*ESAutomationRunResult)')).Count}else{0};status=if($issues.Count){'failed'}else{'passed'};issues=@($issues);sourceSha256=if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}else{$null}}
}
$failed=@($checks|Where-Object status -eq 'failed')
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESAuthorityDeserialization';status=if($failed.Count){'failed'}else{'passed'};fileCount=$checks.Count;readMethodCount=($checks|Measure-Object readMethodCount -Sum).Sum;passedCount=@($checks|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;checks=$checks;runtimeStatus='runtime-not-run';claimsNotProven=@('all deserialization call sites','host invocation frequency','Unity runtime behavior');capturedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
