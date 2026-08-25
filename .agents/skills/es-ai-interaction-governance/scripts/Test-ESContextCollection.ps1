[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)
$ErrorActionPreference='Stop';$root=(Resolve-Path $ProjectRoot).Path;$runner=Join-Path $PSScriptRoot 'Invoke-ESContextCollection.ps1'
$warning=(Get-ChildItem (Join-Path $root 'Assets/Plugins/ES/AIWarnings') -Recurse -Filter '*.md' | Select-Object -First 1).FullName.Substring($root.Length+1).Replace('\','/')
$paths=@('.agents/skills/es-ai-interaction-governance/SKILL.md','Documentation/AIKnowledge/entries/ai-interaction-governance.md',$warning)
$temp='ES/Output/Interaction/context-collection-test.json'
$out=@(& $runner -ProjectRoot $root -TaskKey test-context -PlanHash ('b'*64) -Selection skill-knowledge-aiwarnings -ReadPaths $paths -OutputPath $temp)|Out-String
$r=$out|ConvertFrom-Json
if($r.decision -ne 'collected' -or @($r.readSet).Count -ne 3 -or $r.readSetHash -notmatch '^[0-9a-f]{64}$'){throw 'positive collection receipt invalid'}
$failed=$false;try{& $runner -ProjectRoot $root -TaskKey test-context -PlanHash ('b'*64) -Selection skill-only -ReadPaths $paths -OutputPath $temp *> $null}catch{$failed=$true};if(!$failed){throw 'selection mismatch was not rejected'}
[pscustomobject]@{status='passed';positive='passed';selectionMismatch='passed';runtimeStatus='runtime-not-run'}|ConvertTo-Json
exit 0
