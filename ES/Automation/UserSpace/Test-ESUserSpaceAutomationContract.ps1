[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$manifestPath = Join-Path $root 'ES\Automation\Contracts\es-userspace-profile-v1.json'
$manifest = Get-Content -Raw -Encoding UTF8 $manifestPath | ConvertFrom-Json
if ($manifest.taskId -ne 'es.userspace.profile' -or $manifest.commandId -ne 'userspace.profile.manage') { throw 'IdentityBindingFailed' }
$schemaPath = Join-Path $root ($manifest.inputContract.path -replace '/', '\')
$workerPath = Join-Path $root ($manifest.worker.entrypoint -replace '/', '\')
if (-not (Test-Path -LiteralPath $schemaPath) -or -not (Test-Path -LiteralPath $workerPath)) { throw 'WorkerOrSchemaMissing' }
if ((Get-FileHash $schemaPath -Algorithm SHA256).Hash.ToLower() -ne $manifest.inputContract.sha256) { throw 'SchemaHashMismatch' }
if ((Get-FileHash $workerPath -Algorithm SHA256).Hash.ToLower() -ne $manifest.worker.entrypointHash) { throw 'WorkerHashMismatch' }
$runRecordSchema = Join-Path $root ($manifest.runRecordSchema -replace '/', '\')
if (-not (Test-Path -LiteralPath $runRecordSchema)) { throw 'RunRecordSchemaMissing' }
Get-Content -Raw -Encoding UTF8 $runRecordSchema | ConvertFrom-Json | Out-Null
if (@($manifest.writeRoots) -notcontains 'ES/AISpace/Public/People') { throw 'PeopleWriteRootMissing' }
if (@($manifest.writeRoots) -contains 'ES/AISpace/Local' -or @($manifest.capabilities) -contains 'WriteAssets') { throw 'PrivateOrAssetsWriteExpanded' }
$catalog = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Assets\Plugins\ES\AICommands\AICommandCatalog.json') | ConvertFrom-Json
$entry = @($catalog.commands | Where-Object { $_.id -eq $manifest.commandId })
if ($entry.Count -ne 1) { throw 'CommandCatalogBindingFailed' }
$commandPath = Join-Path $root ([string]$entry[0].path -replace '/', '\')
if (-not (Test-Path -LiteralPath $commandPath)) { throw 'CommandCatalogPathBindingFailed' }
[pscustomobject]@{ status='passed'; taskId=$manifest.taskId; commandId=$manifest.commandId; cases=@('identity-binding','schema-hash','worker-hash','public-root-only','catalog-binding'); runtimeStatus='runtime-not-run' } | ConvertTo-Json -Compress
