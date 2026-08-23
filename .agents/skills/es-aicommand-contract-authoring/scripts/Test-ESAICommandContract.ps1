[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$CommandPath
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($CommandPath)){throw 'CommandPath must be project-relative.'}
$normalized=$CommandPath.Replace('\','/').Trim()
if($normalized -notmatch '^Assets/Plugins/ES/AICommands/.+\.md$' -or @($normalized.Split('/')|?{$_ -in @('','.','..')}).Count -gt 0){throw 'CommandPath is outside the managed AICommand contract root.'}
$full=Join-Path $root ($normalized.Replace('/',[IO.Path]::DirectorySeparatorChar))
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "AICommand contract missing: $normalized"}
$strict=[Text.UTF8Encoding]::new($false,$true);$text=$strict.GetString([IO.File]::ReadAllBytes($full))
foreach($field in @('\u547D\u4EE4\u7C7B\u578B\uFF1A','\u9ED8\u8BA4\u6539\u6587\u4EF6\uFF1A','\u98CE\u9669\u7B49\u7EA7\uFF1A')){if($text -notmatch ('(?m)^'+$field+'\s*\S+')){throw "AICommand contract missing metadata: $field"}}
if($text -notmatch '(?m)\u53D6\u6D88|\u56DE\u6EDA|\u9A8C\u8BC1'){throw 'AICommand contract lacks cancellation, recovery or verification semantics.'}
Write-Output "PASS: AICommand contract is bounded and readable: $normalized"
