param([string]$Unity='D:/Programas/UnityHub/Editor/6000.6.3f1/Editor/Unity.exe', [string]$ValidationProject=(Join-Path $env:TEMP 'MetroidbrainiaPhase0-20261109'),[switch]$EditorOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$copy=[IO.Path]::GetFullPath($ValidationProject)
if($copy -eq $root -or $copy.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Separate project required.'}
if(!(Test-Path (Join-Path $copy 'phase0-isolated-copy.marker')) -or (Get-Content (Join-Path $copy 'phase0-isolated-copy.marker') -Raw) -ne $root){throw 'Marked isolated copy required.'}
$phase2=Join-Path $root 'Tools/Hand3DPhase2'
$stageRuntime=@('Assets/Scripts/Experiments/Hand3D/Hand3DInteractionCoordinator.cs','Assets/Scripts/Experiments/Hand3D/Hand3DInputController.cs','Assets/Scripts/Experiments/Hand3D/Hand3DDoorInteractor.cs','Assets/Scripts/Experiments/Hand3D/Hand3DDocumentInteractor.cs','Assets/Scripts/Experiments/Hand3D/Hand3DPoseActions.cs')
$modified='Assets/Scripts/Experiments/Hand3D/Hand3DPoseProfile.cs'
$added=@('Assets/Editor/Experiments/Hand3D/Hand3DPoseLibrary.cs','Assets/Editor/Experiments/Hand3D/Hand3DPosePreview.cs','Assets/Editor/Experiments/Hand3D/Hand3DPoseEditor.cs')
$allowed=@($added)+@($added | ForEach-Object {$_+'.meta'})
$records=Get-Content (Join-Path $PSScriptRoot 'before-files.json') -Raw | ConvertFrom-Json
function Verify {
    $known=@{}
    foreach($r in $records){$known[$r.path]=$true; if($r.path -ne $modified -and (Get-FileHash -LiteralPath (Join-Path $root $r.path) -Algorithm SHA256).Hash -ne $r.sha256){throw "Protected file changed: $($r.path)"}}
    foreach($file in Get-ChildItem (Join-Path $root 'Assets'),(Join-Path $root 'Packages'),(Join-Path $root 'ProjectSettings') -Recurse -File){
        $relative=$file.FullName.Substring($root.Length+1).Replace('\','/'); if(!$known.ContainsKey($relative) -and $allowed -notcontains $relative){throw "Unexpected asset: $relative"}
    }
}
Verify
$utf8=New-Object Text.UTF8Encoding($false)
# The conversion must inspect today's saved scene, not a possibly older scene left in the validation copy.
foreach($r in $records | Where-Object {$_.path.StartsWith('Assets/')}){
    $destination=Join-Path $copy $r.path
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $r.path) -Destination $destination -Force
}
foreach($p in @($stageRuntime)+@($allowed | Where-Object {Test-Path (Join-Path $root $_)})){Copy-Item -LiteralPath (Join-Path $root $p) -Destination (Join-Path $copy $p) -Force}
$editor=Join-Path $copy 'Assets/Editor/Phase1Validation'
$runtime=Join-Path $copy 'Assets/Phase1Snapshots'
New-Item -ItemType Directory -Path $editor,$runtime -Force | Out-Null
# Original assertions, with only the fixture's removed private Input fields rewired to the coordinator.
$phase0=[IO.File]::ReadAllText((Join-Path $root 'Tools/Hand3DPhase0/Phase0Validation.cs'))
$phase0=$phase0.Replace('Set(input, "doorInteractor", contact); Set(input, "documentInteractor", documents);','Set(input, "interactions", coordinator);')
$phase0Path=Join-Path $copy 'Assets/Editor/Phase0Validation/Phase0Validation.cs'
if(!(Test-Path (Split-Path $phase0Path))){throw 'Phase 0 staged harness missing.'}
[IO.File]::WriteAllText($phase0Path,$phase0,$utf8)
$phase1=[IO.File]::ReadAllText((Join-Path $root 'Tools/Hand3DPhase1/Phase1Validation.cs'))
# Before detectors retain their original coordinator; their legacy API is not imposed on production.
$phase1=$phase1.Replace('door = owner.AddComponent<BeforeDoorInteractor>();','owner.AddComponent<BeforeInteractionCoordinator>(); door = owner.AddComponent<BeforeDoorInteractor>();')
$phase1=$phase1.Replace('Get<bool>(door, "grabbing")','(before ? Get<bool>(door, "grabbing") : ((Hand3DDoorInteractor)door).IsGrabbing)')
[IO.File]::WriteAllText((Join-Path $editor 'Phase1Validation.cs'),$phase1,$utf8)
$beforeDoor=[IO.File]::ReadAllText((Join-Path $root 'Tools/Hand3DPhase1/Before/Hand3DDoorInteractor.cs.txt')).Replace('Hand3DDoorInteractor','BeforeDoorInteractor').Replace('Hand3DDoorDebug','BeforeDoorDebug').Replace('Hand3DInteractionCoordinator','BeforeInteractionCoordinator')
$beforeDoor=$beforeDoor.Replace('Physics.RaycastAll(','Metroidbrainia.Phase1Counters.BeforeRaycastAll(').Replace('Hand3DScreenProjection.TryWorldToScreen(armMovement.ArmCamera, armDisplay, handGripPoint.position, out point, out reason)','Metroidbrainia.Phase1Counters.BeforeHandProjection(armMovement.ArmCamera, armDisplay, handGripPoint.position, out point, out reason)')
$beforeDoc=[IO.File]::ReadAllText((Join-Path $root 'Tools/Hand3DPhase1/Before/Hand3DDocumentInteractor.cs.txt')).Replace('Hand3DDocumentInteractor','BeforeDocumentInteractor').Replace('Hand3DDoorInteractor','BeforeDoorInteractor').Replace('Hand3DInteractionCoordinator','BeforeInteractionCoordinator')
$beforeCoordinator=[IO.File]::ReadAllText((Join-Path $phase2 'Before/Hand3DInteractionCoordinator.cs.txt')).Replace('Hand3DInteractionCoordinator','BeforeInteractionCoordinator')
[IO.File]::WriteAllText((Join-Path $runtime 'BeforeDoorInteractor.cs'),$beforeDoor,$utf8)
[IO.File]::WriteAllText((Join-Path $runtime 'BeforeDocumentInteractor.cs'),$beforeDoc,$utf8)
[IO.File]::WriteAllText((Join-Path $runtime 'BeforeInteractionCoordinator.cs'),$beforeCoordinator,$utf8)
Copy-Item (Join-Path $root 'Tools/Hand3DPhase1/SnapshotCounters.cs') $runtime -Force
Copy-Item (Join-Path $phase2 'Phase2Validation.cs') $editor -Force
Copy-Item (Join-Path $root 'Tools/Hand3DPhase3/Phase3Validation.cs') $editor -Force
Copy-Item (Join-Path $PSScriptRoot 'Phase4Validation.cs') $editor -Force
$runId=[Guid]::NewGuid().ToString('N')
$output=Join-Path $PSScriptRoot ('Results/'+$runId)
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Invoke-Check([string]$method,[string]$file,[string]$result){
    $log=Join-Path $copy $file
    $process=Start-Process -FilePath $Unity -WorkingDirectory $copy -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$copy+'"'),'-executeMethod',$method,'-phase0RunId',$runId,'-phase1RunId',$runId,'-phase2RunId',$runId,'-phase3RunId',$runId,'-phase4RunId',$runId,'-logFile',('"'+$log+'"'))
    while(!$process.WaitForExit(2000)){}
    Copy-Item $log $output
    $resultPath=Join-Path $copy $result
    if(Test-Path $resultPath){Copy-Item $resultPath $output}
    if($process.ExitCode -ne 0){throw "Unity failed: $method; inspect $output"}
    $data=Get-Content $resultPath -Raw | ConvertFrom-Json
    if($data.runId -ne $runId){throw 'Stale results.'}
    Write-Output "$method : $($data.assertions) assertions"
}
try {
    if(!$EditorOnly){
    Invoke-Check 'Metroidbrainia.Phase0Validation.Run' 'phase3-baseline.log' 'phase0-results.json'
    Invoke-Check 'Metroidbrainia.Phase1Validation.Run' 'phase3-detection.log' 'phase1-results.json'
    Invoke-Check 'Metroidbrainia.Phase2Validation.Run' 'phase3-sessions.log' 'phase2-results.json'
    Invoke-Check 'Metroidbrainia.Phase3Validation.Run' 'phase3-migration.log' 'phase3-results.json'
    }
    Invoke-Check 'Metroidbrainia.Phase4Validation.Run' 'phase4-editor.log' 'phase4-results.json'
    Copy-Item (Join-Path $copy 'phase3-profile-export.json') $output
    foreach($p in $added){if(!(Test-Path (Join-Path $root ($p+'.meta')))){Copy-Item (Join-Path $copy ($p+'.meta')) (Join-Path $root ($p+'.meta'))}}
    Write-Output "RESULTS=$output"
} finally {
    Verify
    'All previous files unchanged except the data-only pose schema. Functional runtime, TestArmScene, legacy editors/captures and all accepted phase artifacts unchanged. Three new Editor sources and Unity-generated metas allowed.' | Set-Content (Join-Path $output 'integrity.txt')
    @(@($modified)+$allowed | Where-Object {Test-Path (Join-Path $root $_)} | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash}}) | ConvertTo-Json | Set-Content (Join-Path $output 'tested-files.json')
}
