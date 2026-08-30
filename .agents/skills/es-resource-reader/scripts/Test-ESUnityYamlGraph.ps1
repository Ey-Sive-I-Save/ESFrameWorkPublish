[CmdletBinding()]
param([string]$ProjectRoot='.')
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
$reader=Join-Path $root '.agents/skills/es-resource-reader/scripts/Invoke-ESResourceReader.ps1'
$temp=[IO.Path]::Combine([IO.Path]::GetTempPath(), 'es-reader-unityyaml-' + [Guid]::NewGuid().ToString('N') + '.prefab')
$yaml="%YAML 1.1`n--- !u!1 &100`nGameObject:`n  m_Component:`n  - component: {fileID: 200}`n--- !u!114 &200`nMonoBehaviour:`n  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}`n"
try {
    [IO.File]::WriteAllText($temp, $yaml, [Text.UTF8Encoding]::new($false))
    $packet=& $reader -Path $temp | ConvertFrom-Json
    $nodes=@($packet.entries)
    $ok=$packet.detectedFormat -eq 'unityyaml' -and $nodes.Count -eq 2 -and $nodes[1].stableId -eq '114:200' -and @($nodes[1].dependencyGuids).Count -eq 1 -and $packet.summary.dependencyEdgeCount -eq 1
    $out=[ordered]@{validator='Test-ESUnityYamlGraph';valid=$ok;nodeCount=$nodes.Count;dependencyEdgeCount=$packet.summary.dependencyEdgeCount;runtimeStatus='runtime-not-run'}
    $out|ConvertTo-Json -Depth 6
    if(-not $ok){exit 1}
}
finally { if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force} }
