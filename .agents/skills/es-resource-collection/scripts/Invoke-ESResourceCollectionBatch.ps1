[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [int]$MaxFiles = 512,
    [int]$MaxParallel = 3,
    [int]$MaxFileSizeMb = 100,
    [int64]$MaxTotalBytes = 10MB,
    [string]$StatePath = 'ES/Output/ResourceCollection/collection-batch.json',
    [string]$CancelFile = '',
    [string]$SchedulePath = '',
    [switch]$AutoParallel
)
$ErrorActionPreference = 'Stop'
$env:PYTHONIOENCODING = 'utf-8'
if ($SchedulePath -and (Test-Path -LiteralPath $SchedulePath)) {
    $scheduleValidator = Join-Path $PSScriptRoot 'Test-ESResourceCollectionSchedule.ps1'
    $scheduleValidation = & $scheduleValidator -JsonPath $SchedulePath | ConvertFrom-Json
    if (-not $scheduleValidation.valid) { throw ('Invalid SchedulePath: ' + (($scheduleValidation.errors -join '; '))) }
    $schedule = Get-Content -LiteralPath $SchedulePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($schedule.schemaVersion -ne 1) { throw 'Schedule schemaVersion must be 1' }
    if ($schedule.maxFiles) { $MaxFiles = [int]$schedule.maxFiles }
    if ($schedule.maxParallel) { $MaxParallel = [int]$schedule.maxParallel }
    if ($schedule.maxFileSizeMb) { $MaxFileSizeMb = [int]$schedule.maxFileSizeMb }
    if ($schedule.autoParallel -eq $true) { $AutoParallel = $true }
}
if ($MaxFiles -lt 1 -or $MaxFiles -gt 10000) { throw 'MaxFiles must be between 1 and 10000' }
if ($MaxParallel -lt 1 -or $MaxParallel -gt 32) { throw 'MaxParallel must be between 1 and 32' }
if ($MaxFileSizeMb -lt 1 -or $MaxFileSizeMb -gt 1024) { throw 'MaxFileSizeMb must be between 1 and 1024' }
if ($MaxTotalBytes -lt 1MB -or $MaxTotalBytes -gt 1GB) { throw 'MaxTotalBytes must be between 1MB and 1GB' }
$rootFull = (Resolve-Path -LiteralPath $Root).Path
$sw = [Diagnostics.Stopwatch]::StartNew()
$stateFull = [IO.Path]::GetFullPath((Join-Path (Get-Location) $StatePath))
$previous = $null
if (Test-Path -LiteralPath $stateFull) { try { $previous = Get-Content -LiteralPath $stateFull -Raw -Encoding UTF8 | ConvertFrom-Json } catch { $previous = $null } }
$old = @{}; foreach ($x in @($previous.files)) { if ($x.path) { $old[[string]$x.path] = $x } }
$files = [Collections.Generic.List[object]]::new(); $selectedBytes = [int64]0
foreach ($candidate in @(Get-ChildItem -LiteralPath $rootFull -File -Recurse | Sort-Object FullName)) {
    if ($files.Count -ge $MaxFiles) { break }
    if ($candidate.Length -gt ($MaxFileSizeMb * 1MB)) { continue }
    if (($selectedBytes + [int64]$candidate.Length) -gt $MaxTotalBytes) { continue }
    [void]$files.Add($candidate); $selectedBytes += [int64]$candidate.Length
}
$totalBytes = [int64](($files | Measure-Object -Property Length -Sum).Sum)
$effectiveParallel = $MaxParallel
$parallelReason = 'fixed-maxParallel'
if ($AutoParallel) {
    if ($files.Count -lt 8 -or $totalBytes -lt 32MB) { $effectiveParallel = 1; $parallelReason = 'small-count-or-byte-budget' }
    elseif ($files.Count -lt 64 -or $totalBytes -lt 256MB) { $effectiveParallel = [Math]::Min($MaxParallel, 2); $parallelReason = 'medium-count-or-byte-budget' }
    else { $parallelReason = 'large-count-and-byte-budget' }
}
$readerPath = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Invoke-ESResourceReader.ps1'
$worker = { param($path, $reader) & $reader -Path $path | ConvertFrom-Json }
$pending = [Collections.Generic.List[object]]::new(); $results = [Collections.Generic.List[object]]::new(); $reused = 0; $failed = 0; $canceled = $false
$delimitedByRelative = @{}
$delimitedBatchElapsedMilliseconds = 0
$delimitedManifest = @()
$jsonByPath = @{}
$jsonManifest = @()
$markupByPath = @{}
$markupManifest = @()
$structuredByPath = @{}
$structuredManifest = @()
$binaryByPath = @{}
$binaryManifest = @()
$fileInfo = @{}
foreach ($f in $files) {
    $relative = $f.FullName.Substring($rootFull.Length).TrimStart('\','/').Replace('\','/')
    $hash = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $ext = [IO.Path]::GetExtension($f.FullName).ToLowerInvariant().TrimStart('.')
    $fileInfo[$f.FullName] = [pscustomobject]@{ relative=$relative; hash=$hash; ext=$ext }
    if (($ext -eq 'csv' -or $ext -eq 'tsv') -and -not ($old.ContainsKey($relative) -and [string]$old[$relative].sha256 -eq $hash)) {
        $delimitedManifest += [ordered]@{path=$f.FullName;format=$ext;relative=$relative;sha256=$hash}
    }
}
foreach($f in $files){$i=$fileInfo[$f.FullName]; if($i.ext -in @('png','jpg','jpeg','gif','mp3','ogg','wav','mp4','webm','fbx','obj','ttf','otf','pdf','xlsx')){if(-not($old.ContainsKey($i.relative)-and [string]$old[$i.relative].sha256 -eq $i.hash)){$binaryManifest += [ordered]@{path=$f.FullName;format=$i.ext;relative=$i.relative;sha256=$i.hash}}}}
if($binaryManifest.Count -gt 0){$bm=Join-Path (Split-Path -Parent $stateFull) ('.binary-manifest-'+[Guid]::NewGuid().ToString('N')+'.json');$binaryManifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $bm -Encoding UTF8;$bo=Join-Path (Split-Path -Parent $stateFull) ('.binary-output-'+[Guid]::NewGuid().ToString('N')+'.json');& python.exe (Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Parse-ESBinaryBatch.py') $bm|Set-Content -LiteralPath $bo -Encoding UTF8;$bv=& python.exe (Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Test-ESBinaryBatchJson.py') $bo|ConvertFrom-Json;if(-not $bv.valid){throw ('Binary batch validation failed: '+$bv.error)};$be=Get-Content -Raw -Encoding UTF8 $bo|ConvertFrom-Json;$bi=@($be.items);if($bi.Count -ne $binaryManifest.Count){throw 'Binary batch item count mismatch'};$bp=@($binaryManifest|ForEach-Object {[string]$_.path}|Sort-Object);$op=@($bi|ForEach-Object {[string]$_.path}|Sort-Object);if((ConvertTo-Json $bp -Compress) -ne (ConvertTo-Json $op -Compress)){throw 'Binary batch output paths do not match manifest'};foreach($p in $bi){$binaryByPath[[string]$p.path]=$p}}
foreach ($f in $files) {
    $i=$fileInfo[$f.FullName]; if($i.ext -in @('sqlite','db','toml','ini','zip','unitypackage','tar','gz')) { if(-not ($old.ContainsKey($i.relative) -and [string]$old[$i.relative].sha256 -eq $i.hash)) { $fmt=if($i.ext -eq 'db'){'sqlite'}elseif($i.ext -in @('tar','gz')){'archive'}else{$i.ext}; $structuredManifest += [ordered]@{path=$f.FullName;format=$fmt;relative=$i.relative;sha256=$i.hash} } }
}
if($structuredManifest.Count -gt 0) {
    $sm=Join-Path (Split-Path -Parent $stateFull) ('.structured-manifest-'+[Guid]::NewGuid().ToString('N')+'.json'); $structuredManifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $sm -Encoding UTF8
    $so=Join-Path (Split-Path -Parent $stateFull) ('.structured-output-'+[Guid]::NewGuid().ToString('N')+'.json'); & python.exe (Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Parse-ESStructuredBatch.py') $sm|Set-Content -LiteralPath $so -Encoding UTF8
    $sv=& python.exe (Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Test-ESStructuredBatchJson.py') $so|ConvertFrom-Json; if(-not $sv.valid){throw ('Structured batch validation failed: '+$sv.error)}
    $se=Get-Content -Raw -Encoding UTF8 $so|ConvertFrom-Json; $si=@($se.items); if($si.Count -ne $structuredManifest.Count){throw 'Structured batch item count mismatch'}; $sp=@($structuredManifest|ForEach-Object {[string]$_.path}|Sort-Object); $op=@($si|ForEach-Object {[string]$_.path}|Sort-Object); if((ConvertTo-Json $sp -Compress) -ne (ConvertTo-Json $op -Compress)){throw 'Structured batch output paths do not match manifest'}
    foreach($p in $si){$structuredByPath[[string]$p.path]=$p}
}
foreach ($f in $files) {
    $relative = $f.FullName.Substring($rootFull.Length).TrimStart('\','/').Replace('\','/')
    $hash = $fileInfo[$f.FullName].hash
    $ext = $fileInfo[$f.FullName].ext
    if (($ext -eq 'json' -or $ext -eq 'jsonl' -or $ext -eq 'ndjson') -and -not ($old.ContainsKey($relative) -and [string]$old[$relative].sha256 -eq $hash)) { $jsonManifest += [ordered]@{path=$f.FullName;format=$(if($ext -in @('jsonl','ndjson')){'jsonl'}else{'json'});relative=$relative;sha256=$hash} }
}
if ($jsonManifest.Count -gt 0) {
    $jsonManifestPath = Join-Path (Split-Path -Parent $stateFull) ('.json-manifest-' + [Guid]::NewGuid().ToString('N') + '.json')
    $jsonManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonManifestPath -Encoding UTF8
    $jsonParser = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Parse-ESJsonBatch.py'
    $jsonOutputPath = Join-Path (Split-Path -Parent $stateFull) ('.json-output-' + [Guid]::NewGuid().ToString('N') + '.json')
    & python.exe $jsonParser $jsonManifestPath | Set-Content -LiteralPath $jsonOutputPath -Encoding UTF8
    $jsonValidator = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Test-ESJsonBatchJson.py'
    $jsonValidation = & python.exe $jsonValidator $jsonOutputPath | ConvertFrom-Json
    if (-not $jsonValidation.valid) { throw ('JSON batch validation failed: ' + [string]$jsonValidation.error) }
    $jsonEnvelope = Get-Content -LiteralPath $jsonOutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $jsonItems = @($jsonEnvelope.items)
    if ($jsonItems.Count -ne $jsonManifest.Count) { throw "JSON batch item count mismatch: expected $($jsonManifest.Count), got $($jsonItems.Count)" }
    $jsonManifestPaths = @($jsonManifest | ForEach-Object { [string]$_.path } | Sort-Object)
    $jsonOutputPaths = @($jsonItems | ForEach-Object { [string]$_.path } | Sort-Object)
    if ((ConvertTo-Json $jsonManifestPaths -Compress) -ne (ConvertTo-Json $jsonOutputPaths -Compress)) { throw 'JSON batch output paths do not match manifest' }
    foreach ($p in $jsonItems) { $jsonByPath[[string]$p.path] = $p }
}
foreach ($f in $files) {
    $relative = $f.FullName.Substring($rootFull.Length).TrimStart('\','/').Replace('\','/')
    $hash = $fileInfo[$f.FullName].hash
    $ext = $fileInfo[$f.FullName].ext
    $isUnityYaml = $ext -in @('meta','unity','scene','prefab','asset','mat','controller','anim','shader','compute')
    if (($ext -eq 'yaml' -or $ext -eq 'yml' -or $ext -eq 'html' -or $ext -eq 'htm' -or $ext -eq 'md' -or $ext -eq 'markdown' -or $ext -eq 'xml' -or $isUnityYaml) -and -not ($old.ContainsKey($relative) -and [string]$old[$relative].sha256 -eq $hash)) { $markupFormat = if($isUnityYaml){'unityyaml'}else{$ext}; $markupManifest += [ordered]@{path=$f.FullName;format=$markupFormat;relative=$relative;sha256=$hash} }
}
if ($markupManifest.Count -gt 0) {
    $markupManifestPath = Join-Path (Split-Path -Parent $stateFull) ('.markup-manifest-' + [Guid]::NewGuid().ToString('N') + '.json')
    $markupManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $markupManifestPath -Encoding UTF8
    $markupParser = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Parse-ESMarkupBatch.py'
    $markupOutputPath = Join-Path (Split-Path -Parent $stateFull) ('.markup-output-' + [Guid]::NewGuid().ToString('N') + '.json')
    & python.exe $markupParser $markupManifestPath | Set-Content -LiteralPath $markupOutputPath -Encoding UTF8
    $markupValidator = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Test-ESMarkupBatchJson.py'
    $markupValidation = & python.exe $markupValidator $markupOutputPath | ConvertFrom-Json
    if (-not $markupValidation.valid) { throw ('Markup batch validation failed: ' + [string]$markupValidation.error) }
    $markupEnvelope = Get-Content -LiteralPath $markupOutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $markupItems = @($markupEnvelope.items)
    if ($markupItems.Count -ne $markupManifest.Count) { throw "Markup batch item count mismatch: expected $($markupManifest.Count), got $($markupItems.Count)" }
    $markupManifestPaths = @($markupManifest | ForEach-Object { [string]$_.path } | Sort-Object)
    $markupOutputPaths = @($markupItems | ForEach-Object { [string]$_.path } | Sort-Object)
    if ((ConvertTo-Json $markupManifestPaths -Compress) -ne (ConvertTo-Json $markupOutputPaths -Compress)) { throw 'Markup batch output paths do not match manifest' }
    foreach ($p in $markupItems) { $markupByPath[[string]$p.path] = $p }
}
if ($delimitedManifest.Count -gt 0) {
    $manifestPath = Join-Path (Split-Path -Parent $stateFull) ('.delimited-manifest-' + [Guid]::NewGuid().ToString('N') + '.json')
    $delimitedManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $batchParser = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Parse-ESDelimitedBatch.py'
    $batchOutputPath = Join-Path (Split-Path -Parent $stateFull) ('.delimited-output-' + [Guid]::NewGuid().ToString('N') + '.json')
    & python.exe $batchParser $manifestPath | Set-Content -LiteralPath $batchOutputPath -Encoding UTF8
    $batchValidator = Join-Path $PSScriptRoot '..\..\es-resource-reader\scripts\Test-ESDelimitedBatchJson.py'
    $batchValidation = & python.exe $batchValidator $batchOutputPath | ConvertFrom-Json
    if (-not $batchValidation.valid) { throw ('Delimited batch validation failed: ' + [string]$batchValidation.error) }
    $batchEnvelope = Get-Content -LiteralPath $batchOutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $delimitedBatchElapsedMilliseconds = $batchEnvelope.elapsedMilliseconds
    $parsedBatch = @($batchEnvelope.items)
    if ($parsedBatch.Count -ne $delimitedManifest.Count) { throw "Delimited batch item count mismatch: expected $($delimitedManifest.Count), got $($parsedBatch.Count)" }
    $manifestPaths = @($delimitedManifest | ForEach-Object { [string]$_.path } | Sort-Object)
    $outputPaths = @($parsedBatch | ForEach-Object { [string]$_.path } | Sort-Object)
    if ((ConvertTo-Json $manifestPaths -Compress) -ne (ConvertTo-Json $outputPaths -Compress)) { throw 'Delimited batch output paths do not match manifest' }
    foreach ($p in @($parsedBatch)) { $delimitedByRelative[[string]$p.path] = $p }
}
foreach ($f in $files) {
    $relative = $f.FullName.Substring($rootFull.Length).TrimStart('\','/').Replace('\','/')
    $hash = $fileInfo[$f.FullName].hash
    if ($old.ContainsKey($relative) -and [string]$old[$relative].sha256 -eq $hash) { $old[$relative].status = 'reused'; [void]$results.Add($old[$relative]); $reused++; continue }
    $ext = [IO.Path]::GetExtension($f.FullName).ToLowerInvariant().TrimStart('.')
    if (($ext -eq 'csv' -or $ext -eq 'tsv') -and $delimitedByRelative.ContainsKey($f.FullName)) {
        $p = $delimitedByRelative[$f.FullName]
        if ($p.status -eq 'passed') {
            $projection = [ordered]@{projectionVersion=1;sourcePath=$f.FullName;sourceSha256=$hash;parserId=$p.parserId;detectedFormat=$ext;summary=$p.summary;entries=$p.entries;warnings=@();errors=@();nonClaims=@('semantic completeness','Unity import','runtime behavior','network behavior');cacheKey=($hash+':'+$p.parserId+':1')}
            [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='parsed';projection=$projection})
        } else { $failed++; [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='failed';error=$p.error;projection=$null}) }
        continue
    }
    if (($ext -eq 'json' -or $ext -eq 'jsonl' -or $ext -eq 'ndjson') -and $jsonByPath.ContainsKey($f.FullName)) {
        $p = $jsonByPath[$f.FullName]
        if ($p.status -eq 'passed') {
            $projection = [ordered]@{projectionVersion=1;sourcePath=$f.FullName;sourceSha256=$hash;parserId=$p.parserId;detectedFormat=$ext;summary=$p.summary;entries=$p.entries;warnings=@();errors=@();nonClaims=@('semantic completeness','Unity import','runtime behavior','network behavior');cacheKey=($hash+':'+$p.parserId+':1')}
            [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='parsed';projection=$projection})
        } else { $failed++; [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='failed';error=$p.error;projection=$null}) }
        continue
    }
    if (($ext -eq 'yaml' -or $ext -eq 'yml' -or $ext -eq 'html' -or $ext -eq 'htm' -or $ext -eq 'md' -or $ext -eq 'markdown' -or $ext -eq 'xml' -or $ext -in @('meta','unity','scene','prefab','asset','mat','controller','anim','shader','compute')) -and $markupByPath.ContainsKey($f.FullName)) {
        $p = $markupByPath[$f.FullName]
        if ($p.status -eq 'passed') {
            $projection = [ordered]@{projectionVersion=1;sourcePath=$f.FullName;sourceSha256=$hash;parserId=$p.parserId;detectedFormat=$ext;summary=$p.summary;entries=$p.entries;warnings=@();errors=@();nonClaims=@('semantic completeness','Unity import','runtime behavior','network behavior');cacheKey=($hash+':'+$p.parserId+':1')}
            [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='parsed';projection=$projection})
        } else { $failed++; [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='failed';error=$p.error;projection=$null}) }
        continue
    }
    if ($ext -in @('sqlite','db','toml','ini','zip','unitypackage','tar','gz') -and $structuredByPath.ContainsKey($f.FullName)) {
        $p=$structuredByPath[$f.FullName]; if($p.status -eq 'passed'){ $projection=[ordered]@{projectionVersion=1;sourcePath=$f.FullName;sourceSha256=$hash;parserId=$p.parserId;detectedFormat=$ext;summary=$p.summary;entries=$p.entries;warnings=@();errors=@();nonClaims=@('semantic completeness','Unity import','runtime behavior','network behavior');cacheKey=($hash+':'+$p.parserId+':1')}; [void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='parsed';projection=$projection}) } else {$failed++;[void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='failed';error=$p.error;projection=$null})}; continue
    }
    if ($ext -in @('png','jpg','jpeg','gif','mp3','ogg','wav','mp4','webm','fbx','obj','ttf','otf','pdf','xlsx') -and $binaryByPath.ContainsKey($f.FullName)) {
        $p=$binaryByPath[$f.FullName]; if($p.status -eq 'passed'){$projection=[ordered]@{projectionVersion=1;sourcePath=$f.FullName;sourceSha256=$hash;parserId=$p.parserId;detectedFormat=$ext;summary=$p.summary;entries=$p.entries;warnings=@();errors=@();nonClaims=@('semantic completeness','Unity import','runtime behavior','network behavior');cacheKey=($hash+':binary.batch.v1:1')};[void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='parsed';projection=$projection})}else{$failed++;[void]$results.Add([ordered]@{path=$relative;sha256=$hash;status='failed';error=$p.error;projection=$null})};continue
    }
    if ($CancelFile -and (Test-Path -LiteralPath $CancelFile)) { $canceled = $true; break }
    $ps = [PowerShell]::Create(); [void]$ps.AddScript($worker).AddArgument($f.FullName).AddArgument($readerPath)
    $pending.Add([pscustomobject]@{ PS=$ps; Handle=$ps.BeginInvoke(); Relative=$relative; Hash=$hash })
    if ($pending.Count -ge $effectiveParallel) {
        $job = $pending[0]; $pending.RemoveAt(0)
        try { $r = $job.PS.EndInvoke($job.Handle); $projection = @($r | Select-Object -Last 1)[0]; [void]$results.Add([ordered]@{path=$job.Relative;sha256=$job.Hash;status='parsed';projection=$projection}) } catch { $failed++; [void]$results.Add([ordered]@{path=$job.Relative;sha256=$job.Hash;status='failed';error=$_.Exception.Message;projection=$null}) } finally { $job.PS.Dispose() }
    }
}
foreach ($job in @($pending)) { try { $r=$job.PS.EndInvoke($job.Handle); $projection=@($r|Select-Object -Last 1)[0]; [void]$results.Add([ordered]@{path=$job.Relative;sha256=$job.Hash;status='parsed';projection=$projection}) } catch { $failed++; [void]$results.Add([ordered]@{path=$job.Relative;sha256=$job.Hash;status='failed';error=$_.Exception.Message;projection=$null}) } finally { $job.PS.Dispose() } }
$ordered = @($results.ToArray() | ForEach-Object { [pscustomobject]@{ path=[string]$_.path; sha256=[string]$_.sha256; status=[string]$_.status; error=$_.error; projection=$_.projection } } | Sort-Object -Property path)
$sw.Stop(); $parsedCount=@($ordered|Where-Object status -eq 'parsed').Count; $rate=0; if($sw.Elapsed.TotalSeconds -gt 0){$rate=[Math]::Round($ordered.Count/$sw.Elapsed.TotalSeconds,2)}; $hitRate=0; if($ordered.Count -gt 0){$hitRate=[Math]::Round($reused/$ordered.Count,4)}
$out = [ordered]@{ schemaVersion=1; batchId='es-resource-collection.batch.v1'; root=$rootFull; maxParallel=$MaxParallel; effectiveParallel=$effectiveParallel; autoParallel=[bool]$AutoParallel; parallelReason=$parallelReason; maxFiles=$MaxFiles; maxTotalBytes=$MaxTotalBytes; totalBytes=$totalBytes; delimitedBatchElapsedMilliseconds=$delimitedBatchElapsedMilliseconds; fileCount=$ordered.Count; reusedCount=$reused; parsedCount=$parsedCount; failedCount=$failed; incrementalHitRate=$hitRate; elapsedMilliseconds=$sw.ElapsedMilliseconds; filesPerSecond=$rate; canceled=$canceled; files=$ordered; nonClaims=@('Unity import','runtime loading','network retrieval','release') }
$dir=Split-Path -Parent $stateFull; if ($dir -and -not(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null}; $tmpPath=$stateFull+'.tmp-'+[Guid]::NewGuid().ToString('N'); $json=$out|ConvertTo-Json -Depth 16; [IO.File]::WriteAllText($tmpPath,$json,(New-Object Text.UTF8Encoding($false))); try { if(Test-Path -LiteralPath $stateFull){[IO.File]::Replace($tmpPath,$stateFull,$null,$true)} else {[IO.File]::Move($tmpPath,$stateFull)} } catch { if(Test-Path -LiteralPath $tmpPath){Move-Item -LiteralPath $tmpPath -Destination $stateFull -Force} }; $json
