param(
    [string]$Unity = 'D:/Programas/UnityHub/Editor/6000.6.3f1/Editor/Unity.exe',
    [string]$ValidationProject = (Join-Path $env:TEMP 'MetroidbrainiaPhase0-20261109')
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$validationRoot = [IO.Path]::GetFullPath($ValidationProject)
$referenceScript = Join-Path $projectRoot 'Tools/Hand3DPhase0/Reference.ps1'
$acceptedFiles = @(Get-ChildItem (Join-Path $projectRoot 'Tools/Hand3DPhase0') -Recurse -File | ForEach-Object { [pscustomobject]@{path=$_.FullName; hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
if ($validationRoot -eq $projectRoot -or $validationRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Use a separate temporary project.' }
& $referenceScript -Mode Verify
if (!(Test-Path $validationRoot)) {
    & (Join-Path $projectRoot 'Tools/Hand3DPhase0/Run.ps1') -Unity $Unity -OutputRoot $validationRoot
}
$marker = Join-Path $validationRoot 'phase0-isolated-copy.marker'
if (!(Test-Path $marker) -or (Get-Content $marker -Raw) -ne $projectRoot) { throw 'Unrecognized validation copy.' }
$records = Get-Content (Join-Path $projectRoot 'Tools/Hand3DPhase0/Reference/files.json') -Raw | ConvertFrom-Json
foreach ($record in $records | Where-Object {$_.path -like 'Assets/*.cs' -or $_.path -eq 'Assets/Scenes/TestArmScene.unity'}) {
    if ((Get-FileHash -LiteralPath (Join-Path $validationRoot $record.path) -Algorithm SHA256).Hash -ne $record.sha256) { throw "Isolated runtime differs from reference: $($record.path)" }
}
$stage = Join-Path $validationRoot 'Assets/Editor/Phase05Probe'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FullPoseIkProbe.cs') -Destination $stage
$runId = [Guid]::NewGuid().ToString('N')
$resultRoot = Join-Path $PSScriptRoot ('Results/' + $runId)
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
function Invoke-ProbeUnity([string]$method,[string]$logName) {
    $process = Start-Process -FilePath $Unity -WorkingDirectory $validationRoot -ArgumentList @('-batchmode','-projectPath',('"'+$validationRoot+'"'),'-executeMethod',$method,'-phase0RunId',$runId,'-phase05RunId',$runId,'-logFile',('"'+(Join-Path $validationRoot $logName)+'"')) -WindowStyle Hidden -PassThru
    while (!$process.WaitForExit(2000)) { }
    return $process.ExitCode
}
try {
    $baselineExit = Invoke-ProbeUnity 'Metroidbrainia.Phase0Validation.Run' 'phase05-baseline.log'
    Copy-Item (Join-Path $validationRoot 'phase0-results.json') (Join-Path $resultRoot 'baseline.json')
    Copy-Item (Join-Path $validationRoot 'phase05-baseline.log') $resultRoot
    $baseline = Get-Content (Join-Path $resultRoot 'baseline.json') -Raw | ConvertFrom-Json
    if ($baselineExit -ne 0 -or $baseline.runId -ne $runId -or $baseline.assertions -ne 611 -or @($baseline.results | Where-Object {$_.status -eq 'FAIL'}).Count) { throw 'Phase 0 regression rerun failed.' }
    $probeExit = Invoke-ProbeUnity 'Metroidbrainia.FullPoseIkProbe.Run' 'phase05.log'
    Copy-Item (Join-Path $validationRoot 'phase05.log') $resultRoot
    $probeOutput = Join-Path $validationRoot ('phase05-output/' + $runId)
    if (!(Test-Path $probeOutput)) { throw 'No probe output produced.' }
    Copy-Item (Join-Path $probeOutput '*') $resultRoot -Recurse
    $report = Get-Content (Join-Path $resultRoot 'report.json') -Raw | ConvertFrom-Json
    if ($report.runId -ne $runId) { throw 'Stale probe results.' }
    Write-Output "RESULTS=$resultRoot"
    $report | ConvertTo-Json -Depth 8
    if ($probeExit -ne 0) { throw 'Probe has failed checks; inspect its report without changing production code.' }
} finally {
    & $referenceScript -Mode Verify
    foreach ($file in $acceptedFiles) { if ((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash -ne $file.hash) { throw "Accepted Phase 0 file changed: $($file.path)" } }
    'Production reference and accepted Phase 0 artifacts unchanged.' | Set-Content (Join-Path $resultRoot 'integrity.txt')
}
