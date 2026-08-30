[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PreflightPath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
function Resolve-RootPath([string]$p){$f=if([IO.Path]::IsPathRooted($p)){[IO.Path]::GetFullPath($p)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $p))};if(-not $f.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside project root.'};$f}
$preflightFull=Resolve-RootPath $PreflightPath;$pf=Get-Content -LiteralPath $preflightFull -Raw -Encoding UTF8|ConvertFrom-Json
if([string]$pf.status -ne 'accepted'){throw 'P0_DESIGN_NOT_ACCEPTED: preflight is not accepted.'}
if(-not $pf.promptPlan){throw 'P0_DESIGN_NOT_ACCEPTED: promptPlan is missing.'}
if(-not $pf.capabilityProfile -or [string]$pf.capabilityProfile.status -ne 'accepted'){throw 'P0_DESIGN_NOT_ACCEPTED: open-source capability profile is missing or not accepted.'}
$stageNames=@($pf.stages|ForEach-Object {[string]$_.stage});foreach($required in @('intent-review','prompt-generation','layout-thinking')){if($stageNames -notcontains $required){throw "P0_DESIGN_NOT_ACCEPTED: missing stage $required."}}
$objective=[string]$pf.intent.objective;$isGithub=$objective -match '(?i)github|repository|仓库'
if($isGithub){
  $capabilities=@(
    [ordered]@{id='repository-overview';title='Repository overview';purpose='Understand identity, visibility, health and activity';priority='primary';region='repo-header'},
    [ordered]@{id='code-browser';title='Code browser';purpose='Browse branch, file tree and README';priority='primary';region='code-panel'},
    [ordered]@{id='issue-triage';title='Issue triage';purpose='Search, filter and prioritize work items';priority='primary';region='issues-panel'},
    [ordered]@{id='pull-request-review';title='Pull request review';purpose='Track review, CI and merge risk';priority='primary';region='pulls-panel'},
    [ordered]@{id='commit-activity';title='Commit activity';purpose='Scan recent changes and contributors';priority='secondary';region='activity-panel'},
    [ordered]@{id='repository-insights';title='Repository insights';purpose='Summarize languages, releases and maintenance risk';priority='secondary';region='insights-panel'}
  )
  $regions=@(
    [ordered]@{id='global-nav';label='Global navigation';role='navigation';order=1},
    [ordered]@{id='repo-header';label='Repository identity and primary actions';role='banner';order=2},
    [ordered]@{id='repo-tabs';label='Repository routes';role='navigation';order=3},
    [ordered]@{id='overview-grid';label='Repository overview';role='main';order=4},
    [ordered]@{id='code-panel';label='Code and README';role='region';order=5},
    [ordered]@{id='issues-panel';label='Issues';role='region';order=6},
    [ordered]@{id='pulls-panel';label='Pull requests';role='region';order=7},
    [ordered]@{id='activity-panel';label='Commits and activity';role='region';order=8},
    [ordered]@{id='insights-panel';label='Insights';role='region';order=9}
  )
} else {
  $capabilities=@(
    [ordered]@{id='core-task';title='Core task';purpose='Complete the primary user outcome';priority='primary';region='primary-content'},
    [ordered]@{id='supporting-tools';title='Supporting tools';purpose='Reduce friction around the core task';priority='secondary';region='supporting-content'},
    [ordered]@{id='feedback-and-recovery';title='Feedback and recovery';purpose='Explain progress, errors and recovery';priority='primary';region='state-panel'},
    [ordered]@{id='insights';title='Insights';purpose='Help users decide what to do next';priority='secondary';region='insights-panel'}
  )
  $regions=@(
    [ordered]@{id='global-nav';label='Global navigation';role='navigation';order=1},
    [ordered]@{id='primary-content';label='Primary content and action';role='main';order=2},
    [ordered]@{id='supporting-content';label='Supporting capabilities';role='region';order=3},
    [ordered]@{id='state-panel';label='Feedback and recovery';role='region';order=4},
    [ordered]@{id='insights-panel';label='Insights';role='region';order=5}
  )
}
$design=[ordered]@{
  schemaVersion=1;recordType='WebPageStudioDeepDesignSpec';designStatus='accepted';decisionStatus='accepted';designEngine='ESWebPageStudioDeepDesign';sourcePreflightPath=$preflightFull.Substring($root.Length).Replace('\','/');sourcePromptHash=(Get-FileHash -LiteralPath $preflightFull -Algorithm SHA256).Hash.ToLowerInvariant();objective=$objective;pageKind=[string]$pf.intent.pageKind;audience=[string]$pf.intent.audience;primaryAction=[string]$pf.intent.primaryAction;
  generationInput=[ordered]@{prompt=[string]$pf.generatedPrompt;plan=$pf.promptPlan;capabilityProfileId=[string]$pf.capabilityProfile.profileId};frameworkCapabilities=$pf.capabilityProfile;capabilities=$capabilities;regions=$regions;
  interactions=@([ordered]@{id='primary-action';trigger='primary action';result='visible action feedback';recovery='retry or return'},[ordered]@{id='route-navigation';trigger='route/tab selection';result='active region and URL fragment';recovery='fallback to overview'},[ordered]@{id='filter-and-search';trigger='filter/search input';result='filtered result count';recovery='clear filters'},[ordered]@{id='error-recovery';trigger='error state';result='explanation and retry';recovery='offline snapshot or empty state'});
  visualSystem=[ordered]@{style='premium-tech';focus='repository identity plus next action';semanticColors=@('canvas','surface','text','muted','brand','accent','danger','success','warning');typeScale=@('display','title','body','meta');spacing='4pt/8pt rhythm';motionLayers=@('hero-intro','section-reveal','micro-interaction');reducedMotion='required';forcedColors='required'};
  responsiveProfiles=@([ordered]@{id='desktop';width=1440;layout='two-column overview with persistent navigation'},[ordered]@{id='mobile';width=390;layout='single-column linear reading with horizontal route tabs'});
  states=@('default','loading','empty','error','success','offline','permission-denied','not-found');
  dataContract=[ordered]@{repository=@('owner','name','visibility','description','defaultBranch','stars','forks','watchers');commit=@('id','message','author','timestamp','changedFiles');issue=@('id','title','status','labels','author','updatedAt');pullRequest=@('id','title','status','reviewStatus','ciStatus','author','updatedAt')};
  htmlDirectives=[ordered]@{rootClass='designed-workbench';requiredDataAttributes=@('data-design-status=accepted','data-capability-id');capabilityElement='section';capabilityClass='design-capability';interactiveControls=@('primary-action','route-navigation','filter-and-search','error-recovery');visualClasses=@('design-grid','repo-meta','capability-priority');};acceptanceCriteria=@('every capability maps to a region id','every route target resolves to a region id','primary and recovery interactions are represented','desktop and mobile profiles are represented','loading/empty/error/success/offline states are represented','visual tokens and reduced-motion are represented','static output remains offline and deterministic');
  knowledgeRefs=@($pf.promptPlan.knowledgeRefs);runtimeStatus='runtime-not-run';nonClaims=@('deep design spec is a static design decision, not browser or production proof','no network or backend was invoked')
}
$out=Resolve-RootPath $OutputPath;New-Item -ItemType Directory -Path (Split-Path $out) -Force|Out-Null;$json=$design|ConvertTo-Json -Depth 20;[IO.File]::WriteAllText($out,$json,[Text.UTF8Encoding]::new($false));$json
