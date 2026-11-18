param([string]$Unity='D:/Programas/UnityHub/Editor/6000.6.3f1/Editor/Unity.exe', [string]$ValidationProject=(Join-Path $env:TEMP 'MetroidbrainiaPhase0-20261109'),[switch]$PointOnly,[switch]$PrepareOnly,[switch]$BuildPlayer,[switch]$QuaternionAudit,[string]$CaseFilter)
$ErrorActionPreference='Stop'
if($CaseFilter -and (!$PointOnly -or $PrepareOnly -or $BuildPlayer)){throw 'CaseFilter requires PointOnly without PrepareOnly or BuildPlayer.'}
if($CaseFilter -and $CaseFilter -notmatch '^[A-Za-z0-9_*?]+$'){throw 'CaseFilter supports case names and * or ? wildcards only.'}
if($PointOnly -and $QuaternionAudit){throw 'QuaternionAudit requires the full validation route.'}
$focused=$PointOnly -and !$PrepareOnly -and !$BuildPlayer
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$copy=[IO.Path]::GetFullPath($ValidationProject)
if($copy -eq $root -or $copy.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Separate project required.'}
if(!(Test-Path (Join-Path $copy 'phase0-isolated-copy.marker')) -or (Get-Content (Join-Path $copy 'phase0-isolated-copy.marker') -Raw) -ne $root){throw 'Marked isolated copy required.'}
$phase2=Join-Path $root 'Tools/Hand3DPhase2'
$stageRuntime=@()
$modified=@('Assets/Scripts/Experiments/Hand3D/Hand3DContactDetection.cs','Assets/Scenes/TestArmScene.unity')
$added=@('Assets/Scripts/Experiments/Hand3D/Hand3DPointInteractor.cs','Assets/Scripts/Experiments/Hand3D/Hand3DPointWorldAction.cs','Assets/Editor/Experiments/Hand3D/Hand3DPointEditor.cs')
$runtimeFiles=@('Assets/Experiments/Hand3D/Actions.meta','Assets/Experiments/Hand3D/Actions/TestArmPointClick.asset','Assets/Experiments/Hand3D/Actions/TestArmPointClick.asset.meta','Assets/Experiments/Hand3D/Actions/TestArmPointWorld.asset','Assets/Experiments/Hand3D/Actions/TestArmPointWorld.asset.meta')
$allowed=@($added)+@($added | ForEach-Object {$_+'.meta'})
$records=Get-Content (Join-Path $PSScriptRoot 'before-files.json') -Raw | ConvertFrom-Json
# Focused runs validate today's manually integrated project, not the original
# Point installation baseline. Protect its current files for the entire run.
if($focused){
    $records=@(Get-ChildItem (Join-Path $root 'Assets'),(Join-Path $root 'Packages'),(Join-Path $root 'ProjectSettings') -Recurse -File | ForEach-Object {
        [pscustomobject]@{path=$_.FullName.Substring($root.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
function Verify-Scene {
    $installation=Join-Path $PSScriptRoot 'scene-installation.json'
    if(!$focused -and (Test-Path $installation)){
        $expected=(Get-Content $installation -Raw | ConvertFrom-Json).sha256
        if((Get-FileHash (Join-Path $root 'Assets/Scenes/TestArmScene.unity') -Algorithm SHA256).Hash -ne $expected){throw 'Scene differs from the explicitly reviewed Point installation.'}
    }
}
function Verify {
    Verify-Scene
    foreach($r in (Get-Content (Join-Path $PSScriptRoot 'historical-files.json') -Raw | ConvertFrom-Json)){
        if((Get-FileHash -LiteralPath (Join-Path $root $r.path) -Algorithm SHA256).Hash -ne $r.sha256){throw "Historical artifact changed: $($r.path)"}
    }
    $known=@{}
    foreach($r in $records){$known[$r.path]=$true; if(($focused -or $modified -notcontains $r.path) -and (Get-FileHash -LiteralPath (Join-Path $root $r.path) -Algorithm SHA256).Hash -ne $r.sha256){throw "Protected file changed: $($r.path)"}}
    foreach($file in Get-ChildItem (Join-Path $root 'Assets'),(Join-Path $root 'Packages'),(Join-Path $root 'ProjectSettings') -Recurse -File){
        $relative=$file.FullName.Substring($root.Length+1).Replace('\','/'); if(!$known.ContainsKey($relative) -and $allowed -notcontains $relative -and $runtimeFiles -notcontains $relative){throw "Unexpected asset: $relative"}
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
foreach($p in @($stageRuntime)+@(($allowed+$runtimeFiles) | Where-Object {Test-Path (Join-Path $root $_)})){
    $destination=Join-Path $copy $p;New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $p) -Destination $destination -Force
}
if($focused){
    # Phase5.Prepare previously selected Legacy in the fixture scene. Preserve
    # that starting backend without regenerating the already published library.
    $scenePath=Join-Path $copy 'Assets/Scenes/TestArmScene.unity'
    $sceneText=[IO.File]::ReadAllText($scenePath)
    $sceneText=[regex]::Replace($sceneText,'(?m)^(  poseSystem: )[01](\r?)$','${1}0${2}')
    [IO.File]::WriteAllText($scenePath,$sceneText,$utf8)
}
$editor=Join-Path $copy 'Assets/Editor/Phase1Validation'
$runtime=Join-Path $copy 'Assets/Phase1Snapshots'
New-Item -ItemType Directory -Path $editor,$runtime -Force | Out-Null
# Reused copies may contain suites requiring historical instrumentation. Move only
# known harnesses outside Assets; full runs restage them from their canonical sources.
if($focused){
    $inactive=Join-Path $copy 'PointInactiveHarnesses'
    foreach($name in @('Phase0Validation','Phase2Validation','Phase3Validation','Phase4Validation','Phase6Validation','Phase7Validation','Phase8Validation','QuaternionValidation')){
        foreach($file in Get-ChildItem (Join-Path $copy 'Assets/Editor') -Recurse -File | Where-Object {$_.Name -eq ($name+'.cs') -or $_.Name -eq ($name+'.cs.meta')}){
            $destination=Join-Path $inactive $file.FullName.Substring($copy.Length+1)
            New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
            Move-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
    }
}
if(!$focused){
# Original assertions, with only the fixture's removed private Input fields rewired to the coordinator.
$phase0=[IO.File]::ReadAllText((Join-Path $root 'Tools/Hand3DPhase0/Phase0Validation.cs'))
$phase0=$phase0.Replace('Set(input, "doorInteractor", contact); Set(input, "documentInteractor", documents);','Set(input, "interactions", coordinator);')
$phase0=$phase0.Replace('Get<bool>(actions, "clicking")','actions.IsRunning')
$phase0Path=Join-Path $copy 'Assets/Editor/Phase0Validation/Phase0Validation.cs'
if(!(Test-Path (Split-Path $phase0Path))){throw 'Phase 0 staged harness missing.'}
[IO.File]::WriteAllText($phase0Path,$phase0,$utf8)
}
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
if(!$focused){
$sessionTests=[IO.File]::ReadAllText((Join-Path $phase2 'Phase2Validation.cs')).Replace('Get<bool>(actions,"clicking")','actions.IsRunning')
[IO.File]::WriteAllText((Join-Path $editor 'Phase2Validation.cs'),$sessionTests,$utf8)
Copy-Item (Join-Path $root 'Tools/Hand3DPhase3/Phase3Validation.cs') $editor -Force
Copy-Item (Join-Path $root 'Tools/Hand3DPhase4/Phase4Validation.cs') $editor -Force
}
Copy-Item (Join-Path $root 'Tools/Hand3DPhase5/Phase5Validation.cs') $editor -Force
if(!$focused){Copy-Item (Join-Path $root 'Tools/Hand3DPhase6/Phase6Validation.cs') $editor -Force}
Copy-Item (Join-Path $root 'Tools/Hand3DPhase6/Phase6PlayerProbe.cs') (Join-Path $runtime 'Phase5PlayerProbe.cs') -Force
if($QuaternionAudit){
    Copy-Item (Join-Path $root 'Tools/Hand3DQuaternionFix/QuaternionValidation.cs') $editor -Force
    $composition=Join-Path $copy 'Assets/Scripts/Experiments/Hand3D/Hand3DPoseComposition.cs'
    $text=[IO.File]::ReadAllText($composition)
    $text=$text.Replace('public sealed class Hand3DPoseComposition'+"`r`n    {",'public sealed class Hand3DPoseComposition'+"`r`n    {`r`n        public static Action<string,Quaternion> RotationAudit;")
    if(!$text.Contains('RotationAudit;')){$text=$text.Replace("public sealed class Hand3DPoseComposition`n    {","public sealed class Hand3DPoseComposition`n    {`n        public static Action<string,Quaternion> RotationAudit;")}
    $text=$text.Replace('matrices[i]=matrix*Matrix4x4.TRS','RotationAudit?.Invoke("TRS/"+owner.bones[i].name,rotations[i]);matrices[i]=matrix*Matrix4x4.TRS')
    $text=$text.Replace('worldRotations[i]=rotation*rotations[i];','worldRotations[i]=rotation*rotations[i];RotationAudit?.Invoke("world/"+owner.bones[i].name,worldRotations[i]);')
    $text=$text.Replace('worldRotations[i]=owner.NormalizeRotation(rotation*rotations[i],i,phase,"parent world * local rotation");','worldRotations[i]=owner.NormalizeRotation(rotation*rotations[i],i,phase,"parent world * local rotation");RotationAudit?.Invoke("world/"+owner.bones[i].name,worldRotations[i]);')
    [IO.File]::WriteAllText($composition,$text,$utf8)
}

if(!$focused){
Copy-Item (Join-Path $root 'Tools/Hand3DPhase7/Phase7Validation.cs') $editor -Force
Copy-Item (Join-Path $root 'Tools/Hand3DPhase8/Phase8Validation.cs') $editor -Force
}
Copy-Item (Join-Path $PSScriptRoot 'PointValidation.cs') $editor -Force
if(!$focused){
# Transparent operation counters live only in the isolated sources, never in production.
$overlayPath=Join-Path $copy 'Assets/Scripts/Experiments/Hand3D/Hand3DThumbOverlay.cs'
$overlayText=[IO.File]::ReadAllText($overlayPath).Replace('private Mesh baked;', 'public static long Phase7BakeCalls, Phase7ProjectionCalls; private Mesh baked;')
$overlayText=$overlayText.Replace('thumbRenderer.BakeMesh(baked, true);','Phase7BakeCalls++; thumbRenderer.BakeMesh(baked, true);').Replace('Vector3 viewport = armCamera.WorldToViewportPoint','Phase7ProjectionCalls++; Vector3 viewport = armCamera.WorldToViewportPoint')
[IO.File]::WriteAllText($overlayPath,$overlayText,$utf8)
$compositionPath=Join-Path $copy 'Assets/Scripts/Experiments/Hand3D/Hand3DPoseComposition.cs'
$compositionText=[IO.File]::ReadAllText($compositionPath).Replace('private readonly Hand3DRuntimePoses asset;', 'public static long Phase7WorldUpdates; private readonly Hand3DRuntimePoses asset;')
$compositionText=$compositionText.Replace('var parent=owner.bones[0].parent;var parentMatrix=', 'Phase7WorldUpdates++; var parent=owner.bones[0].parent;var parentMatrix=')
[IO.File]::WriteAllText($compositionPath,$compositionText,$utf8)
}
$runId=[Guid]::NewGuid().ToString('N')
$output=Join-Path $root ('Tools/Hand3DLocalResults/Point/'+$runId)
New-Item -ItemType Directory -Path $output -Force | Out-Null
@($modified | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $copy $_) -Algorithm SHA256).Hash}}) | ConvertTo-Json | Set-Content (Join-Path $output 'isolated-files.json')
function Invoke-Check([string]$method,[string]$file,[string]$result){
    $log=Join-Path $copy $file
    $selection=if($CaseFilter -and $method -eq 'Metroidbrainia.PointValidation.Run'){@('-pointCase',$CaseFilter)}else{@()}
    $process=Start-Process -FilePath $Unity -WorkingDirectory $copy -WindowStyle Hidden -PassThru -ArgumentList (@('-batchmode','-projectPath',('"'+$copy+'"'),'-executeMethod',$method,'-phase0RunId',$runId,'-phase1RunId',$runId,'-phase2RunId',$runId,'-phase3RunId',$runId,'-phase4RunId',$runId,'-phase5RunId',$runId,'-logFile',('"'+$log+'"'))+$selection)
    while(!$process.WaitForExit(2000)){}
    Copy-Item $log $output
    $resultPath=if($result){Join-Path $copy $result}else{$null}
    if($resultPath -and (Test-Path $resultPath)){Copy-Item $resultPath $output}
    if($process.ExitCode -ne 0){throw "Unity failed: $method; inspect $output"}
    if(!$resultPath -or [IO.Path]::GetExtension($resultPath) -ne '.json'){return}
    $data=Get-Content $resultPath -Raw | ConvertFrom-Json
    if($data.runId -ne $runId){throw 'Stale results.'}
    if($data.cases | Where-Object {$_.status -eq 'FAIL'}){throw "Failed case: $method"}
    Write-Output "$method : $($data.assertions) assertions"
}
try {
    if($PrepareOnly){
        Invoke-Check 'Metroidbrainia.PointValidation.Prepare' 'point-prepare.log' 'point-preparation.json'
        Copy-Item (Join-Path $copy 'Assets/PointConfigured.unity') $output
        foreach($p in $runtimeFiles+$allowed){if(Test-Path (Join-Path $copy $p)){$dst=Join-Path $output ('GeneratedAssets/'+$p);New-Item -ItemType Directory -Path (Split-Path $dst) -Force | Out-Null;Copy-Item (Join-Path $copy $p) $dst -Force}}
        Write-Output ('RESULTS='+$output)
        return
    }
    if(!$focused){
    Invoke-Check 'Metroidbrainia.Phase5Validation.Prepare' 'phase5-prepare.log' ''
    Copy-Item (Join-Path $copy 'Assets/Phase5Configured.unity') (Join-Path $copy 'Assets/Scenes/TestArmScene.unity') -Force
    }
    if(!$PointOnly){
    Invoke-Check 'Metroidbrainia.Phase0Validation.Run' 'phase3-baseline.log' 'phase0-results.json'
    Invoke-Check 'Metroidbrainia.Phase1Validation.Run' 'phase3-detection.log' 'phase1-results.json'
    Invoke-Check 'Metroidbrainia.Phase2Validation.Run' 'phase3-sessions.log' 'phase2-results.json'
    Invoke-Check 'Metroidbrainia.Phase3Validation.Run' 'phase3-migration.log' 'phase3-results.json'
    Invoke-Check 'Metroidbrainia.Phase4Validation.Run' 'phase4-editor.log' 'phase4-results.json'
    }
    if(!$PointOnly){
    Invoke-Check 'Metroidbrainia.Phase5Validation.Run' 'phase5-universal.log' 'phase5-results.json'
    }
    if($QuaternionAudit -and !$PointOnly){Invoke-Check 'Metroidbrainia.QuaternionValidation.Run' 'quaternion-audit.log' 'quaternion-results.json'}
    if(!$PointOnly){Invoke-Check 'Metroidbrainia.Phase6Validation.Run' 'phase6-actions.log' 'phase6-results.json'}
    if(!$PointOnly){Invoke-Check 'Metroidbrainia.Phase7Validation.Run' 'phase7-profile.log' 'phase7-results.json'
    Copy-Item (Join-Path $copy 'phase7-poses.json') $output}
    if(!$PointOnly){Invoke-Check 'Metroidbrainia.Phase8Validation.Run' 'phase8-consolidation.log' 'phase8-results.json'}
    Invoke-Check 'Metroidbrainia.PointValidation.Run' 'point-actions.log' 'point-results.json'
    if(!$focused){Copy-Item (Join-Path $copy 'phase5-import.txt') $output}
    if($BuildPlayer){
        Invoke-Check 'Metroidbrainia.Phase5Validation.BuildPlayer' 'phase5-build.log' 'phase5-build.txt'
        $probe=Start-Process -FilePath (Join-Path $copy 'Phase5Player/HandValidation.exe') -WorkingDirectory $copy -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-nographics','-phase5Output',('"'+(Join-Path $output 'player-result.txt')+'"'),'-logFile',('"'+(Join-Path $output 'player.log')+'"'))
        $deadline=[DateTime]::UtcNow.AddMinutes(3)
        while(!$probe.WaitForExit(2000)){if([DateTime]::UtcNow -gt $deadline){$probe.Kill();throw 'Player probe timeout.'}}
        if($probe.ExitCode -ne 0){throw 'Standalone player probe failed.'}
    }
    Write-Output "RESULTS=$output"
} finally {
    Verify
    $integrity=if($focused){'All current Assets, Packages and ProjectSettings files match their start-of-run hashes. Historical phase 0-8 artifacts unchanged. Only the marked isolated project was used for validation.'}else{'Protected production files, original Editor library and all phase 0-8 artifacts unchanged. TestArmScene matches its reviewed Point installation hash. Only shared experimental detection, the explicitly prepared scene blocks and new Point scripts/assets are allowed to change.'}
    $integrity | Set-Content (Join-Path $output 'integrity.txt')
    @(@($modified)+$allowed+$runtimeFiles+@('Assets/Scenes/TestArmScene.unity') | Where-Object {Test-Path (Join-Path $root $_)} | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash}}) | ConvertTo-Json | Set-Content (Join-Path $output 'tested-files.json')
}
