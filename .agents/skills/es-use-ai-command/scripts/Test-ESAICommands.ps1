[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (& git rev-parse --show-toplevel 2>$null)
}
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    throw 'Cannot resolve the Git project root. Pass -ProjectRoot.'
}

$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())
$commandRoot = Join-Path $ProjectRoot 'Assets\Plugins\ES\AICommands'
if (-not (Test-Path -LiteralPath $commandRoot -PathType Container)) {
    throw "AICommands directory not found: $commandRoot"
}

$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$metadataPatterns = @(
    @{ name = 'command-type'; pattern = '(?m)^\u547D\u4EE4\u7C7B\u578B\uFF1A\s*\S+' },
    @{ name = 'default-write'; pattern = '(?m)^\u9ED8\u8BA4\u6539\u6587\u4EF6\uFF1A\s*\S+' },
    @{ name = 'risk-level'; pattern = '(?m)^\u98CE\u9669\u7B49\u7EA7\uFF1A\s*\S+' }
)
$results = New-Object Collections.Generic.List[object]
$files = Get-ChildItem -LiteralPath $commandRoot -Filter '*.md' -File -Recurse | Sort-Object FullName

foreach ($file in $files) {
    $errors = New-Object Collections.Generic.List[string]
    try {
        $text = $strictUtf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $errors.Add("Strict UTF-8 decoding failed: $($_.Exception.Message)")
        $text = ''
    }

    if ($text.Contains([char]0xFFFD)) {
        $errors.Add('Contains Unicode replacement character U+FFFD.')
    }

    foreach ($metadata in $metadataPatterns) {
        if ($text -notmatch $metadata.pattern) {
            $errors.Add("Missing metadata: $($metadata.name)")
        }
    }

    $pathMatches = [regex]::Matches($text, '(?m)^(Assets|Documentation|ES|Packages)/[^\r\n`]+')
    foreach ($match in $pathMatches) {
        $relativePath = $match.Value.Trim()
        $candidate = Join-Path $ProjectRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $candidate)) {
            $errors.Add("Referenced path does not exist: $relativePath")
        }
    }

    $results.Add([pscustomobject]@{
        file = $file.FullName.Substring($ProjectRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace([IO.Path]::DirectorySeparatorChar, '/')
        valid = $errors.Count -eq 0
        errors = $errors.ToArray()
    })
}

$invalid = @($results | Where-Object { -not $_.valid })
$report = [pscustomobject]@{
    projectRoot = $ProjectRoot
    commandCount = $files.Count
    invalidCount = $invalid.Count
    valid = $invalid.Count -eq 0
    commands = $results.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
}
else {
    "AICommands: $($report.commandCount), invalid: $($report.invalidCount)"
    foreach ($item in $invalid) {
        "[INVALID] $($item.file)"
        foreach ($error in $item.errors) { "  - $error" }
    }
}

if (-not $report.valid) { exit 1 }
