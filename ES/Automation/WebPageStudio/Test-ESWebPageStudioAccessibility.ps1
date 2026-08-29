[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$HtmlPath)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\';$full=(Resolve-Path -LiteralPath $HtmlPath -ErrorAction Stop).Path
if(-not $full.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'HtmlPath must remain under the project root.'}
$html=Get-Content -LiteralPath $full -Encoding UTF8 -Raw;$checks=[System.Collections.Generic.List[object]]::new()
function Add-Check([string]$id,[bool]$ok,[string]$detail){$checks.Add([pscustomobject]@{check=$id;status=if($ok){'passed'}else{'failed'};detail=$detail})}
Add-Check 'document-language' ($html -match '(?is)<html\b[^>]*\blang=["''][^"'']+["'']') 'html declares a non-empty language'
Add-Check 'text-direction' ($html -match '(?is)<html\b[^>]*\bdir=["''](?:ltr|rtl)["'']') 'html declares ltr or rtl direction'
Add-Check 'main-landmark' ($html -match '(?is)<main\b[^>]*\bid=["'']main-content["'']') 'main landmark has stable id'
$h1Count=[regex]::Matches($html,'(?is)<h1\b').Count;Add-Check 'heading-root' ($h1Count -eq 1) 'document has exactly one primary h1 heading'
Add-Check 'skip-link' ($html -match '(?is)<a\b[^>]*class=["'']skip-link["''][^>]*href=["'']#main-content["'']') 'skip link targets main landmark'
Add-Check 'focus-policy' ($html -match '(?is)focus-visible') 'visible focus policy is present'
Add-Check 'motion-policy' ($html -match '(?is)prefers-reduced-motion') 'reduced motion policy is present'
Add-Check 'contrast-policy' ($html -match '(?is)forced-colors:active') 'forced colors policy is present'
$formOk=$true;foreach($form in [regex]::Matches($html,'(?is)<form\b[^>]*>(.*?)</form>')){foreach($input in [regex]::Matches($form.Groups[1].Value,'(?is)<(?:input|select|textarea)\b[^>]*\bid=["'']([^"'']+)["'']')){if($form.Groups[1].Value -notmatch ('(?is)<label\b[^>]*\bfor=["'']'+[regex]::Escape($input.Groups[1].Value)+'["'']')){$formOk=$false}}}
Add-Check 'form-labels' $formOk 'every form control id has a matching label'
$namedButtons=@($html|Out-Null;[regex]::Matches($html,'(?is)<button\b([^>]*)>(.*?)</button>')|Where-Object{([regex]::Replace($_.Groups[2].Value,'<[^>]+>','')).Trim().Length -eq 0 -and $_.Groups[1].Value -notmatch '(?is)aria-label=["'']'})
Add-Check 'control-names' ($namedButtons.Count -eq 0) 'buttons have visible text or aria-label'
$positiveTab=$html -match '(?is)tabindex=["''][1-9]';Add-Check 'tab-order' (-not $positiveTab) 'no positive tabindex overrides document order'
$images=@([regex]::Matches($html,'(?is)<img\b([^>]*)>')|Where-Object{$_.Groups[1].Value -notmatch '(?is)\balt=["'']'})
Add-Check 'image-alternatives' ($images.Count -eq 0) 'all img elements provide alt text'
$passed=@($checks|Where-Object status -eq 'passed').Count;$failed=$checks.Count-$passed
[pscustomobject]@{schemaVersion=1;recordType='WebPageStudioAccessibilityReceipt';status=if($failed -eq 0){'passed'}else{'failed'};htmlPath=$full;checkCount=$checks.Count;passedCount=$passed;failedCount=$failed;evidenceLevel='S1';runtimeStatus='runtime-not-run';checks=$checks;claimsNotProven=@('This dependency-free static scan does not replace axe-core, browser accessibility tree, or assistive-technology testing.') }|ConvertTo-Json -Depth 8
