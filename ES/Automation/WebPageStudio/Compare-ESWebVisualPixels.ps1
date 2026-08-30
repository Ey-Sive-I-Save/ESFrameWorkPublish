[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BaselinePath,
    [Parameter(Mandatory=$true)][string]$CandidatePath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [int]$MaxDiffPixels = 0
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
function Resolve-Project([string]$p){$f=(Resolve-Path -LiteralPath $p -ErrorAction Stop).Path;if(-not $f.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Image path must remain under project root.'};$f}
$base=Resolve-Project $BaselinePath;$cand=Resolve-Project $CandidatePath;$out=Join-Path $root $OutputPath
if([IO.Path]::GetExtension($base) -ne '.png' -or [IO.Path]::GetExtension($cand) -ne '.png'){throw 'Only PNG screenshots are supported.'}
Add-Type -AssemblyName System.Drawing
$b=[Drawing.Bitmap]::new($base);$c=[Drawing.Bitmap]::new($cand)
try {
  if($b.Width -ne $c.Width -or $b.Height -ne $c.Height){$diff=[int]($b.Width*$b.Height);$w=$c.Width;$h=$c.Height}
  else {$w=$b.Width;$h=$b.Height;$diff=0;for($y=0;$y -lt $h;$y++){for($x=0;$x -lt $w;$x++){if($b.GetPixel($x,$y).ToArgb() -ne $c.GetPixel($x,$y).ToArgb()){$diff++}}}}
  $o=[ordered]@{schemaVersion=1;recordType='WebPageStudioVisualPixelComparison';status=if($diff -le $MaxDiffPixels){'passed'}else{'failed'};baselinePath=$BaselinePath.Replace('\','/');candidatePath=$CandidatePath.Replace('\','/');width=$w;height=$h;diffPixels=$diff;maxDiffPixels=$MaxDiffPixels;baselineHash=(Get-FileHash $base -Algorithm SHA256).Hash.ToLowerInvariant();candidateHash=(Get-FileHash $cand -Algorithm SHA256).Hash.ToLowerInvariant();runtimeStatus='runtime-passed';nonClaims=@('pixel-diff-does-not-prove-human-aesthetic-signoff','byte-identical-rendering-does-not-prove-cross-browser-equivalence')}
  $parent=Split-Path -Parent $out;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($out,($o|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false));$o|ConvertTo-Json -Depth 8
} finally {$b.Dispose();$c.Dispose()}
