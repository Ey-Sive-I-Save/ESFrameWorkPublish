[CmdletBinding()]
param(
  [string]$Root = "ES/AISpace/Public/可用资源站点",
  [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$mapPath = Join-Path $PSScriptRoot '..\references\site-folder-map.json'
$map = Get-Content -LiteralPath $mapPath -Raw -Encoding UTF8 | ConvertFrom-Json
$lookup = @{}
$map.PSObject.Properties | ForEach-Object { $lookup[$_.Name] = [string]$_.Value }
$report = [System.Collections.Generic.List[object]]::new()
$prefix = [string][char]0x3010
Get-ChildItem -LiteralPath $rootPath -Directory | Where-Object { $_.Name.StartsWith($prefix) } | ForEach-Object {
  $site = $_
  Get-ChildItem -LiteralPath $site.FullName -Directory | ForEach-Object {
    $child = $_
    $typeName = $null
    if($lookup.ContainsKey($child.Name)){ $typeName = $lookup[$child.Name] }
    elseif($child.Name -in @('resources','assets','downloads','package','packages','sample')){
      $ext = Get-ChildItem -LiteralPath $child.FullName -Recurse -File | Group-Object Extension | Sort-Object Count -Descending | Select-Object -First 1
      $typeName = if($ext.Name -in @('.fbx','.glb','.gltf','.obj','.blend')){'模型'} elseif($ext.Name -in @('.png','.jpg','.jpeg','.tga','.exr','.hdr')){'纹理'} elseif($ext.Name -in @('.wav','.mp3','.ogg','.flac','.aif')){'音效'} elseif($ext.Name -in @('.ttf','.otf','.woff','.woff2')){'字体'} else {'工具'}
    }
    else { return }
    $typeDir = Join-Path $site.FullName $typeName
    if(-not (Test-Path -LiteralPath $typeDir)){ if($Apply){ New-Item -ItemType Directory -Path $typeDir | Out-Null } }
    Get-ChildItem -LiteralPath $child.FullName -Recurse -File | ForEach-Object {
      $relative = $_.FullName.Substring($child.FullName.Length).TrimStart([char[]]"\\/")
      $inner = $relative.Split([char]92)[0]
      if($inner -in @('resources','assets','downloads','package','packages','sample','textures','audio','models','fonts','ui','tool','hdris')){
        $relative = $relative.Substring($inner.Length).TrimStart([char[]]"\\/")
      }
      $target = Join-Path $typeDir $relative
      $parent = Split-Path -Parent $target
      if($Apply -and -not (Test-Path -LiteralPath $parent)){ New-Item -ItemType Directory -Path $parent -Force | Out-Null }
      if(Test-Path -LiteralPath $target){
        $srcHash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        $dstHash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        if($srcHash -eq $dstHash){ if($Apply){ Remove-Item -LiteralPath $_.FullName -Force }; return }
        throw "name collision: $target"
      }
      if($Apply){ Move-Item -LiteralPath $_.FullName -Destination $target }
    }
    if($Apply){
      Get-ChildItem -LiteralPath $child.FullName -Recurse -Directory | Sort-Object FullName -Descending | ForEach-Object {
        if((Get-ChildItem -LiteralPath $_.FullName -Force | Measure-Object).Count -eq 0){ Remove-Item -LiteralPath $_.FullName -Force }
      }
      if((Get-ChildItem -LiteralPath $child.FullName -Force | Measure-Object).Count -eq 0){ Remove-Item -LiteralPath $child.FullName -Force }
    }
    $report.Add([pscustomobject]@{site=$site.Name;source=$child.Name;target=$typeName;applied=$Apply.IsPresent})
  }
}
# Flatten only redundant wrappers immediately below a semantic type folder.
Get-ChildItem -LiteralPath $rootPath -Directory | Where-Object { $_.Name.StartsWith($prefix) } | ForEach-Object {
  $site = $_
  Get-ChildItem -LiteralPath $site.FullName -Directory | Where-Object { $_.Name -in @('纹理','HDRI','模型','动画','音效','字体','UI','工具') } | ForEach-Object {
    $typeDir = $_
    Get-ChildItem -LiteralPath $typeDir.FullName -Directory | Where-Object { $_.Name -in @('resources','assets','downloads','package','packages','sample','textures','audio','models','fonts','ui','hdris','staging') } | ForEach-Object {
      $wrapper = $_
      Get-ChildItem -LiteralPath $wrapper.FullName -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($wrapper.FullName.Length).TrimStart([char[]]"\\/")
        $target = Join-Path $typeDir.FullName $relative
        $parent = Split-Path -Parent $target
        if($Apply -and -not (Test-Path -LiteralPath $parent)){ New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        if(Test-Path -LiteralPath $target){
          if((Get-FileHash $_.FullName -Algorithm SHA256).Hash -eq (Get-FileHash $target -Algorithm SHA256).Hash){ if($Apply){ Remove-Item -LiteralPath $_.FullName -Force }; return }
          throw "name collision: $target"
        }
        if($Apply){ Move-Item -LiteralPath $_.FullName -Destination $target }
      }
      if($Apply){
        Get-ChildItem -LiteralPath $wrapper.FullName -Recurse -Directory | Sort-Object FullName -Descending | ForEach-Object { if((Get-ChildItem $_.FullName -Force | Measure-Object).Count -eq 0){ Remove-Item -LiteralPath $_.FullName -Force } }
        if((Get-ChildItem $wrapper.FullName -Force | Measure-Object).Count -eq 0){ Remove-Item -LiteralPath $wrapper.FullName -Force }
      }
    }
  }
}
$report | ConvertTo-Json -Depth 3
