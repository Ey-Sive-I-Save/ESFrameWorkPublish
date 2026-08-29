Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Get-ESABCDIterationFeedbackHash($Value){$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($Value|ConvertTo-Json -Compress -Depth 12)))).Replace('-','').ToLowerInvariant())}finally{$sha.Dispose()}}
function Invoke-ESABCDIterationFeedback {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$TargetPath,[Parameter(Mandatory)][string]$Finding,[Parameter(Mandatory)][string]$VerificationPredicate)
 $root=(Resolve-Path $ProjectRoot).Path
 if([IO.Path]::IsPathRooted($TargetPath)-or$TargetPath-match'(^|[/\\])\.\.([/\\]|$)'){throw 'ITERATION_TARGET_NOT_PROJECT_RELATIVE'}
 if($TargetPath -match '(?i)(Knowledge|Experience|Learning|AIKnowledge)'){throw 'ITERATION_TARGET_EXPERIENCE_ONLY'}
 $full=Join-Path $root $TargetPath
 if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'ITERATION_TARGET_SOURCE_MISSING'}
 $hash=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
 $result=[ordered]@{schemaVersion=1;recordType='ABCDIterationFeedback';status='accepted-for-source-review';target=[ordered]@{relativePath=($TargetPath.Replace('\','/'));sourceHash=$hash};finding=$Finding;verificationPredicate=$VerificationPredicate;writeMode='bounded-source-feedback';reusableSourceRef=$true;persistedExperienceWrite=$false;automaticCorePromotion=$false;requiresExplicitApply=$true;receiptHash=$null}
 $result.receiptHash=Get-ESABCDIterationFeedbackHash $result
 [pscustomobject]$result
}
function Apply-ESABCDSourcePatch {
 [CmdletBinding(SupportsShouldProcess)]param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$TargetPath,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$ExpectedHash,[Parameter(Mandatory)][string]$OldText,[Parameter(Mandatory)][string]$NewText)
 $root=(Resolve-Path $ProjectRoot).Path
 if([IO.Path]::IsPathRooted($TargetPath)-or$TargetPath-match'(^|[/\\])\.\.([/\\]|$)'){throw 'PATCH_TARGET_NOT_PROJECT_RELATIVE'}
 if($TargetPath -match '(?i)(Knowledge|Experience|Learning|AIKnowledge)'){throw 'PATCH_TARGET_EXPERIENCE_ONLY'}
 $full=Join-Path $root $TargetPath;if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'PATCH_TARGET_SOURCE_MISSING'}
 $actual=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant();if($actual -cne $ExpectedHash.ToLowerInvariant()){throw 'PATCH_TARGET_HASH_MISMATCH'}
 $text=[IO.File]::ReadAllText($full,[Text.UTF8Encoding]::new($false));$hits=([regex]::Matches($text,[regex]::Escape($OldText))).Count;if($hits -ne 1){throw "PATCH_EXACT_MATCH_COUNT:$hits"}
 $updated=$text.Replace($OldText,$NewText);$sha=[Security.Cryptography.SHA256]::Create();try{$newHash=([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($updated)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
 if($PSCmdlet.ShouldProcess($TargetPath,'apply exact source patch')){$tmp="$full.tmp-$([guid]::NewGuid().ToString('N'))";[IO.File]::WriteAllText($tmp,$updated,[Text.UTF8Encoding]::new($false));Move-Item -LiteralPath $tmp -Destination $full -Force;$verify=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant();if($verify -cne $newHash){[IO.File]::WriteAllText($full,$text,[Text.UTF8Encoding]::new($false));throw 'PATCH_POSTWRITE_HASH_MISMATCH'};[pscustomobject]@{status='applied';targetPath=$TargetPath;beforeHash=$actual;afterHash=$verify;rollbackAvailable=$true}}
 else {[pscustomobject]@{status='planned';targetPath=$TargetPath;beforeHash=$actual;afterHash=$newHash;rollbackAvailable=$false}}
}
Export-ModuleMember -Function Invoke-ESABCDIterationFeedback,Apply-ESABCDSourcePatch,Get-ESABCDIterationFeedbackHash
