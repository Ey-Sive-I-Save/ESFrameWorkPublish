[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ArtifactDirectory,
    [string]$ReportPath = ''
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
$dir=(Resolve-Path -LiteralPath $ArtifactDirectory -ErrorAction Stop).Path
if(-not $dir.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ArtifactDirectory must remain under project root.'}
$checks=[Collections.Generic.List[object]]::new()
function Add-Check([string]$Id,[bool]$Ok,[string]$Detail){$checks.Add([pscustomobject]@{check=$Id;status=if($Ok){'passed'}else{'failed'};detail=$Detail})}
$required=@('index.html','web-page-contract.json','contract-validation.json','design-tokens.json','deep-design.json','site.webmanifest','robots.txt','sitemap.xml','icon.svg')
foreach($name in $required){Add-Check "file-$name" (Test-Path -LiteralPath (Join-Path $dir $name) -PathType Leaf) "$name exists"}
$files=@(Get-ChildItem -LiteralPath $dir -File -Recurse | Where-Object {
    $_.Name -notlike '*.log' -and $_.Name -ne 'artifact-integrity.json' -and
    $_.FullName -notmatch '(?i)\\\.(?:perf|visual-matrix)-[^\\]+\\'
})
$hashes=[ordered]@{}
foreach($f in $files){$rel=$f.FullName.Substring($dir.Length).TrimStart('\').Replace('\','/');$hashes[$rel]=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
$indexPath=Join-Path $dir 'index.html';$index='';if(Test-Path $indexPath){$index=Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8}
Add-Check 'index-utf8' ((Test-Path $indexPath) -and -not $index.Contains([char]0xFFFD)) 'entrypoint decodes as UTF-8 without replacement characters'
Add-Check 'no-external-fetches' (-not($index -match '(?is)\b(?:src|href|action|poster|content)\s*=\s*["'']https?://|\bfetch\s*\(')) 'entrypoint has no external fetch-bearing URL or fetch call'
$contractPath=Join-Path $dir 'web-page-contract.json';$contract=$null;if(Test-Path $contractPath){try{$contract=Get-Content $contractPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
Add-Check 'contract-json' ($null -ne $contract -and [string]$contract.recordType -eq 'WebPageGenerationContract') 'generation contract is valid JSON with expected record type'
$entryName=if($contract -and $contract.artifactPlan){[string]$contract.artifactPlan.entryFile}else{'index.html'}
Add-Check 'contract-entry-binding' ($entryName -eq 'index.html' -and $index.Length -gt 0) 'contract entryFile resolves to index.html'
$tokensPath=Join-Path $dir 'design-tokens.json';$tokens=$null;if(Test-Path $tokensPath){try{$tokens=Get-Content $tokensPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
Add-Check 'design-tokens-json' ($null -ne $tokens -and [string]$tokens.recordType -eq 'WebPageStudioDesignTokens' -and [string]$tokens.visualStyle -match '^(premium-tech|editorial|aurora|minimal)$' -and [string]$tokens.motionLevel -match '^(none|subtle|expressive)$') 'design token manifest binds style and motion choices'
$designPath=Join-Path $dir 'deep-design.json';$deep=$null;if(Test-Path $designPath){try{$deep=Get-Content $designPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
Add-Check 'deep-design-json' ($null -ne $deep -and [string]$deep.recordType -eq 'WebPageStudioDeepDesignSpec' -and [string]$deep.designStatus -eq 'accepted' -and @($deep.capabilities).Count -gt 0 -and @($deep.regions).Count -gt 0) 'accepted deep design is present and structurally bound'
$manifestPath=Join-Path $dir 'site.webmanifest';$manifest=$null;if(Test-Path $manifestPath){try{$manifest=Get-Content $manifestPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
Add-Check 'manifest-json' ($null -ne $manifest -and -not [string]::IsNullOrWhiteSpace([string]$manifest.name)) 'site manifest is parseable and named'
$sitemapPath=Join-Path $dir 'sitemap.xml';$sitemap='';if(Test-Path $sitemapPath){$sitemap=Get-Content $sitemapPath -Raw -Encoding UTF8}
Add-Check 'sitemap-xml' ($sitemap -match '(?is)<urlset\b' -and $sitemap -match '(?is)<loc>') 'sitemap contains a urlset and at least one location'
$passed=@($checks|Where-Object status -eq 'passed').Count
$receipt=[ordered]@{schemaVersion=1;recordType='WebPageStudioArtifactIntegrityReceipt';status=if($passed -eq $checks.Count){'passed'}else{'failed'};artifactDirectory=$dir;fileCount=$files.Count;files=$hashes;checks=$checks;runtimeStatus='runtime-not-run';network='disabled';evidenceLevel='S1';nonClaims=@('Static artifact integrity does not prove browser rendering, HTTP staging, CDN invalidation, Lighthouse, Unity, or production rollback.','Hash manifest is scoped to this artifact snapshot.')}
$receipt.checkCount=$checks.Count;$receipt.passedCount=$passed;$receipt.failedCount=$checks.Count-$passed
$json=$receipt|ConvertTo-Json -Depth 12
if(-not [string]::IsNullOrWhiteSpace($ReportPath)){$report=[IO.Path]::GetFullPath((Join-Path (Get-Location) $ReportPath));if(-not $report.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath must remain under project root.'};New-Item -ItemType Directory -Path (Split-Path $report) -Force|Out-Null;[IO.File]::WriteAllText($report,$json,[Text.UTF8Encoding]::new($false))}
$json;if($passed -ne $checks.Count){exit 1}
