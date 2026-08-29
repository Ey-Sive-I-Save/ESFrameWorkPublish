[CmdletBinding()]
param([string]$BundlesDirectory='ES/Output/WebPageStudio/bundles',[string]$OutputPath='')
$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
$readiness=& (Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioBundleReplayReadiness.ps1') -BundlesDirectory $BundlesDirectory|ConvertFrom-Json
$templates=[System.Collections.Generic.List[object]]::new()
foreach($b in @($readiness.bundles|? {!$_.replayReady})) {
  $actions=[System.Collections.Generic.List[string]]::new()
  foreach($reason in @($b.reasons)) {
    switch($reason) {
      'missing-request-en' { $actions.Add('Provide the original request-en.json snapshot or rebuild from an authoritative request.') }
      'missing-manifest' { $actions.Add('Provide manifest.json and bind locale directories and contracts.') }
      'missing-locale-contracts' { $actions.Add('Run Convert-ESWebPageStudioRequest.ps1 for each locale to create contracts.') }
    }
  }
  $templates.Add([pscustomobject]@{bundle=$b.bundle;status='recovery-required';missingReasons=$b.reasons;actions=@($actions);destructiveActionsAllowed=$false})
}
$report=[pscustomobject]@{schemaVersion=1;recordType='WebPageStudioBundleRecoveryTemplateReport';status=if($templates.Count -eq 0){'none-required'}else{'templates-generated'};templateCount=$templates.Count;templates=$templates;evidenceLevel='S1';runtimeStatus='runtime-not-run';claimsNotProven=@('Templates do not invent missing source files and do not modify historical artifacts.')}
if($OutputPath){$out=[IO.Path]::GetFullPath((Join-Path $root $OutputPath));if(-not $out.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath must remain under project root.'};[IO.File]::WriteAllText($out,($report|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))}
$report|ConvertTo-Json -Depth 10
