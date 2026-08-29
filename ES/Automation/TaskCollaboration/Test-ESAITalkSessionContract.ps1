[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SessionPath,
    [string]$ProjectRoot,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (& git rev-parse --show-toplevel 2>$null) }
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())
$session = (Resolve-Path -LiteralPath $SessionPath -ErrorAction Stop).Path
$sessionsRoot = Join-Path $ProjectRoot 'Assets/Plugins/ES/AITalk/Sessions'
$errors = [Collections.Generic.List[string]]::new()
if (-not $session.StartsWith($sessionsRoot, [StringComparison]::OrdinalIgnoreCase)) { $errors.Add('Session path escapes the AITalk Sessions root.') }
$item = Get-Item -LiteralPath $session -Force
if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { $errors.Add('Session root is a reparse point.') }
foreach ($child in @(Get-ChildItem -LiteralPath $session -Force -Recurse -ErrorAction Stop)) { if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { $errors.Add("Reparse point found: $($child.FullName)") } }
$intro = Get-ChildItem -LiteralPath $session -File -Filter '00_*.md' | Select-Object -First 1
if ($null -eq $intro) { $errors.Add('Session introduction file 00_*.md is missing.') }
else {
    $introText = [IO.File]::ReadAllText($intro.FullName, [Text.UTF8Encoding]::new($false, $true))
    if ($introText -notmatch 'AITalk') { $errors.Add('Session introduction does not identify AITalk.') }
    if ($introText -notmatch [regex]::Escape($item.Name)) { $errors.Add('Session introduction does not contain the directory session id.') }
}
$messages = Join-Path $session 'Messages'
if (-not (Test-Path -LiteralPath $messages -PathType Container)) { $errors.Add('Messages directory is missing.') }
else {
    $files = @(Get-ChildItem -LiteralPath $messages -File -Filter '*.md' | Sort-Object Name)
    $sequences = [Collections.Generic.HashSet[int]]::new()
    foreach ($file in $files) {
        if ($file.BaseName -notmatch '^(\d{1,8})_') { $errors.Add("Invalid message filename: $($file.Name)"); continue }
        if (-not $sequences.Add([int]$Matches[1])) { $errors.Add("Duplicate message sequence: $($Matches[1])") }
        try { [IO.File]::ReadAllText($file.FullName, [Text.UTF8Encoding]::new($false, $true)) | Out-Null } catch { $errors.Add("Invalid UTF-8 message: $($file.Name)") }
    }
}
$report = [pscustomobject][ordered]@{ status=if($errors.Count){'failed'}else{'passed'}; sessionPath=$session; messageCount=if($messages -and (Test-Path -LiteralPath $messages)){@(Get-ChildItem -LiteralPath $messages -File -Filter '*.md').Count}else{0}; errors=$errors.ToArray(); runtimeStatus='runtime-not-run' }
if ($Json) { $report | ConvertTo-Json -Depth 6 } else { "AITalk session contract: $($report.status); messages=$($report.messageCount)" }
if ($errors.Count) { exit 1 }
exit 0
