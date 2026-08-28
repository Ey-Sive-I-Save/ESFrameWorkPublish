$ErrorActionPreference = 'Stop'

Describe 'ES Codex recent conversation summary coverage' {
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $coverage = (Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter 'Test-ESCodexTimelineCoverage.ps1' -File | Select-Object -First 1).FullName
    $archiveIdLabel = [regex]::Unescape('\u7A97\u53E3\u6863\u6848ID')
    $archiveTimeLabel = [regex]::Unescape('\u5F52\u6863\u65F6\u95F4')
    $requestLabel = [regex]::Unescape('\u7528\u6237\u8981\u6C42')
    $summaryLabel = [regex]::Unescape('\u5F53\u65F6\u7B54\u590D\u6458\u8981')
    $remainingLabel = [regex]::Unescape('\u5269\u4F59\u9879')
    $fixtureRoot = Join-Path $projectRoot ('Temp\ESCodexTimelineRecent-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

    function Invoke-CoverageFixture([int]$Count, [int]$MissingSummary) {
        $session = Join-Path $fixtureRoot ('session-' + $Count + '-' + $MissingSummary + '.jsonl')
        $archive = Join-Path $fixtureRoot ('archive-' + $Count + '-' + $MissingSummary + '.md')
        $jsonl = 1..$Count | ForEach-Object { '{"type":"event_msg","payload":{"type":"user_message","message":"turn"}}' }
        [IO.File]::WriteAllLines($session, $jsonl, [Text.UTF8Encoding]::new($false))
        $lines = [Collections.Generic.List[string]]::new()
        $lines.Add(('{0}: `TEST`' -f $archiveIdLabel)); $lines.Add(('{0}: 2026-08-27 10:00:00 +08:00' -f $archiveTimeLabel))
        for($i=1; $i -le $Count; $i++) {
            $n = $i.ToString('000'); $lines.Add("### T$n")
            $lines.Add(('- **{0}**: test' -f $requestLabel))
            if($i -ne $MissingSummary){$lines.Add(('- **{0}**: test' -f $summaryLabel))}
            $lines.Add(('- **{0}**: none' -f $remainingLabel)); $lines.Add('')
        }
        [IO.File]::WriteAllLines($archive, $lines, [Text.UTF8Encoding]::new($false))
        $output = @(& $coverage -SessionPath $session -ArchivePath $archive 2>&1 | Where-Object { $_ -is [string] }) -join ([Environment]::NewLine)
        return ($output | ConvertFrom-Json)
    }

    It 'fails when the latest node lacks a summary even if older nodes have enough summaries' {
        $result = Invoke-CoverageFixture 11 11
        $result.Passed | Should Be $false
        @($result.MissingRecentSummaryNodes | Where-Object { $_ -eq 'T011' }).Count | Should Be 1
    }

    It 'checks every available node when fewer than ten turns exist' {
        $result = Invoke-CoverageFixture 3 2
        $result.Passed | Should Be $false
        @($result.MissingRecentSummaryNodes | Where-Object { $_ -eq 'T002' }).Count | Should Be 1
    }
}
