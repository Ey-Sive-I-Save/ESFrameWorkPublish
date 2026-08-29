[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$Title,
    [string]$Description = '',
    [ValidateSet('marketing','dashboard')][string]$PageKind = 'marketing',
    [string]$PrimaryAction = 'Learn more',
    [ValidateSet('en','zh-CN','ar')][string]$Language = 'en',
    [ValidateSet('static','dynamic')][string]$RenderMode = 'static',
    [ValidateSet('inline','same-origin-endpoint','authorized-backend-contract')][string]$DataSource = 'inline',
    [string]$Endpoint = '',
    [ValidateRange(1,300)][int]$TimeoutSeconds = 10,
    [string]$ThemeName = 'es-lime-cyan',
    [string]$OutputDirectory = '',
    [string]$OutputPath = ''
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
if($RenderMode -eq 'static' -and $DataSource -ne 'inline'){throw 'Static renderMode requires inline data.'}
if($RenderMode -eq 'dynamic' -and $DataSource -eq 'inline' -and $Endpoint){throw 'Inline dynamic data cannot carry an endpoint.'}
if($DataSource -ne 'inline' -and [string]::IsNullOrWhiteSpace($Endpoint)){throw 'A non-inline data source requires a same-origin endpoint path.'}
if($Endpoint -and ($Endpoint -notmatch '^/[^/].*' -or $Endpoint -match '[?#]|\.\.|^//|^https?://')){throw 'Endpoint must be a same-origin absolute path without query, fragment, or traversal.'}
$desc=if($Description){$Description}else{"$Title - ES WebPageStudio kernel page"}
$slug=(($Title.ToLowerInvariant()-replace '[^a-z0-9]+','-')-replace '(^-|-$)','');if(-not $slug){$slug='kernel-page'}
if(-not $OutputDirectory){$OutputDirectory="ES/Output/WebPageStudio/kernel/$slug"}
if([IO.Path]::IsPathRooted($OutputDirectory)-or $OutputDirectory -match '(^|[/\\])\.\.([/\\]|$)'){throw 'OutputDirectory must remain project-relative.'}
$dark=[ordered]@{ink='#f4f7f5';muted='#9aa7a3';background='#101516';panel='#172021';line='#2b3938';accent='#75e1d1';positive='#d1f36a';critical='#f28a9a'}
$light=[ordered]@{ink='#172021';muted='#536562';background='#f4f7f5';panel='#ffffff';line='#c8d6d2';accent='#087f78';positive='#8ab51f';critical='#b63d5a'}
$contentBindings=@();if($RenderMode -eq 'dynamic'){$contentBindings=@('content.title','content.summary')};$contentKind=if($PageKind -eq 'dashboard'){'content'}else{'hero'}
$components=@([ordered]@{id='page-shell';kind='layout';semanticRole='document shell';children=@('primary-nav','page-content','page-footer');dataBindings=@()},[ordered]@{id='primary-nav';kind='navigation';semanticRole='primary navigation';children=@();dataBindings=@()},[ordered]@{id='page-content';kind=$contentKind;semanticRole='main content';children=@();dataBindings=$contentBindings},[ordered]@{id='page-footer';kind='footer';semanticRole='content information';children=@();dataBindings=@()})
$routes=@([ordered]@{id='home';path='/';entry='index.html';renderMode=$RenderMode})
$request=[ordered]@{schemaVersion=1;recordType='ESWebPageStudioKernelRequest';format='html-css-esm';renderMode=$RenderMode;page=[ordered]@{title=$Title;description=$desc;pageKind=$PageKind;primaryAction=$PrimaryAction;language=$Language};theme=[ordered]@{name=$ThemeName;dark=$dark;light=$light};data=[ordered]@{source=$DataSource;endpoint=$Endpoint;timeoutSeconds=$TimeoutSeconds};routes=$routes;components=$components;output=[ordered]@{directory=$OutputDirectory}}
if(-not $OutputPath){$OutputPath=Join-Path $root "ES/Output/WebPageStudio/kernel-requests/$slug-$([guid]::NewGuid().ToString('N')).json"}
$full=[IO.Path]::GetFullPath($OutputPath);if(-not $full.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath must remain under project root.'};if(Test-Path -LiteralPath $full){throw "Refusing to overwrite request: $full"};$parent=Split-Path -Parent $full;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($full,($request|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false));$request|ConvertTo-Json -Depth 10
