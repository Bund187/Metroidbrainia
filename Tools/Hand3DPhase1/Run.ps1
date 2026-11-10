param(
    [string]$Unity = 'D:/Programas/UnityHub/Editor/6000.6.3f1/Editor/Unity.exe',
    [string]$ValidationProject = (Join-Path $env:TEMP 'MetroidbrainiaPhase0-20261109')
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$validationRoot = [IO.Path]::GetFullPath($ValidationProject)
if ($validationRoot -eq $projectRoot -or $validationRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Use a separate validation copy.' }
$marker = Join-Path $validationRoot 'phase0-isolated-copy.marker'
if (!(Test-Path $marker) -or (Get-Content $marker -Raw) -ne $projectRoot) { throw 'A marked full Phase 0 validation copy is required.' }
$changed = @('Assets/Scripts/Experiments/Hand3D/Hand3DDoorInteractor.cs','Assets/Scripts/Experiments/Hand3D/Hand3DDocumentInteractor.cs','Assets/Scripts/Experiments/Hand3D/Hand3DDocumentDisplay.cs','Assets/Editor/Experiments/Hand3D/Hand3DDocumentEditors.cs','Assets/Scenes/TestArmScene.unity')
$added = @('Assets/Scripts/Experiments/Hand3D/Hand3DContactDetection.cs','Assets/Scripts/Experiments/Hand3D/Hand3DContactDetection.cs.meta')
$records = Get-Content (Join-Path $projectRoot 'Tools/Hand3DPhase0/Reference/files.json') -Raw | ConvertFrom-Json
$accepted = @(Get-ChildItem (Join-Path $projectRoot 'Tools/Hand3DPhase0'),(Join-Path $projectRoot 'Tools/Hand3DPhase05') -Recurse -File | ForEach-Object { [pscustomobject]@{path=$_.FullName;hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
function Verify-Protected {
    $known = @{}; foreach ($record in $records) {
        $known[$record.path] = $true
        if ($changed -notcontains $record.path -and (Get-FileHash -LiteralPath (Join-Path $projectRoot $record.path) -Algorithm SHA256).Hash -ne $record.sha256) { throw "Protected file changed: $($record.path)" }
    }
    foreach ($file in Get-ChildItem (Join-Path $projectRoot 'Assets'),(Join-Path $projectRoot 'Packages'),(Join-Path $projectRoot 'ProjectSettings') -Recurse -File) {
        $relative = $file.FullName.Substring($projectRoot.Length+1).Replace('\','/')
        if (!$known.ContainsKey($relative) -and $added -notcontains $relative) { throw "Unexpected added asset: $relative" }
    }
    foreach ($file in $accepted) { if ((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash -ne $file.hash) { throw "Accepted artifact changed: $($file.path)" } }
}
function Get-DocumentBlock([string]$text) {
    $blocks = [regex]::Split($text,'(?m)(?=^--- !u!)')
    return @($blocks | Where-Object {$_ -match 'm_EditorClassIdentifier: .*Hand3DExperiment.Hand3DDocumentInteractor\r?\n'})[0]
}
function Remove-Detection([string]$text) { return [regex]::Replace($text,'(?m)^  detection:\r?\n(?:^    .*\r?\n)*','') }
function Verify-Scene {
    $text = [IO.File]::ReadAllText((Join-Path $projectRoot 'Assets/Scenes/TestArmScene.unity'))
    $baseline = [IO.File]::ReadAllText((Join-Path $projectRoot 'Tools/Hand3DPhase0/Reference/TestArmScene.unity.txt'))
    $block = Get-DocumentBlock $text
    if ($text.Replace($block,(Remove-Detection $block)) -cne $baseline) { throw 'Scene changed beyond the new document detection settings.' }
}
Verify-Protected
Verify-Scene
foreach ($relative in @($changed | Where-Object {$_ -ne 'Assets/Scenes/TestArmScene.unity'}) + @($added | Where-Object {Test-Path (Join-Path $projectRoot $_)})) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $relative) -Destination (Join-Path $validationRoot $relative) -Force
}
Copy-Item (Join-Path $projectRoot 'Assets/Scenes/TestArmScene.unity') (Join-Path $validationRoot 'Assets/Scenes/TestArmScene.unity') -Force
$stage = Join-Path $validationRoot 'Assets/Editor/Phase1Validation'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Phase1Validation.cs') $stage -Force
$beforeDoor = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Before/Hand3DDoorInteractor.cs.txt')).Replace('Hand3DDoorInteractor','BeforeDoorInteractor').Replace('Hand3DDoorDebug','BeforeDoorDebug')
$beforeDoor = $beforeDoor.Replace('Physics.RaycastAll(', 'Metroidbrainia.Phase1Counters.BeforeRaycastAll(')
$beforeDoor = $beforeDoor.Replace('Hand3DScreenProjection.TryWorldToScreen(armMovement.ArmCamera, armDisplay, handGripPoint.position, out point, out reason)', 'Metroidbrainia.Phase1Counters.BeforeHandProjection(armMovement.ArmCamera, armDisplay, handGripPoint.position, out point, out reason)')
$beforeDoc = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Before/Hand3DDocumentInteractor.cs.txt')).Replace('Hand3DDocumentInteractor','BeforeDocumentInteractor').Replace('Hand3DDoorInteractor','BeforeDoorInteractor')
$utf8 = New-Object Text.UTF8Encoding($false)
$runtimeStage = Join-Path $validationRoot 'Assets/Phase1Snapshots'
New-Item -ItemType Directory -Path $runtimeStage -Force | Out-Null
foreach ($name in @('BeforeDoorInteractor.cs','BeforeDocumentInteractor.cs')) {
    $oldStage = Join-Path $stage $name
    if (Test-Path -LiteralPath $oldStage) { Remove-Item -LiteralPath $oldStage }
}
[IO.File]::WriteAllText((Join-Path $runtimeStage 'BeforeDoorInteractor.cs'),$beforeDoor,$utf8)
[IO.File]::WriteAllText((Join-Path $runtimeStage 'BeforeDocumentInteractor.cs'),$beforeDoc,$utf8)
Copy-Item (Join-Path $PSScriptRoot 'SnapshotCounters.cs') $runtimeStage -Force
$runId = [Guid]::NewGuid().ToString('N')
$output = Join-Path $PSScriptRoot ('Results/' + $runId)
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Invoke-Unity([string]$method,[string]$log) {
    $process = Start-Process -FilePath $Unity -WorkingDirectory $validationRoot -ArgumentList @('-batchmode','-projectPath',('"'+$validationRoot+'"'),'-executeMethod',$method,'-phase0RunId',$runId,'-phase1RunId',$runId,'-logFile',('"'+(Join-Path $validationRoot $log)+'"')) -WindowStyle Hidden -PassThru
    while (!$process.WaitForExit(2000)) { }
    Copy-Item (Join-Path $validationRoot $log) $output
    if ($process.ExitCode -ne 0) { throw "Unity failed: $method. Inspect $output/$log" }
}
try {
    Invoke-Unity 'Metroidbrainia.Phase1Validation.Migrate' 'phase1-migration.log'
    $saved = [IO.File]::ReadAllText((Join-Path $validationRoot 'Assets/Phase1Migrated.unity'))
    $fragment = [regex]::Match((Get-DocumentBlock $saved),'(?m)^  detection:\r?\n(?:^    .*\r?\n)*').Value
    if (!$fragment) { throw 'Unity did not serialize independent contact settings.' }
    $scenePath = Join-Path $projectRoot 'Assets/Scenes/TestArmScene.unity'
    $current = [IO.File]::ReadAllText($scenePath); $block = Get-DocumentBlock $current
    $newline = if ($current.Contains("`r`n")) {"`r`n"} else {"`n"}
    $fragment = $fragment.Replace("`r`n","`n").Replace("`n",$newline)
    $clean = Remove-Detection $block
    $insertion = $clean.IndexOf('  input:')
    if ($insertion -lt 0) { throw 'Missing document input field.' }
    $patched = $current.Replace($block,$clean.Insert($insertion,$fragment))
    [IO.File]::WriteAllText($scenePath,$patched,$utf8)
    Copy-Item $scenePath (Join-Path $validationRoot 'Assets/Scenes/TestArmScene.unity') -Force
    $meta = Join-Path $projectRoot $added[1]
    if (!(Test-Path $meta)) { Copy-Item (Join-Path $validationRoot $added[1]) $meta }
    Verify-Scene
    Invoke-Unity 'Metroidbrainia.Phase0Validation.Run' 'phase1-regressions.log'
    Copy-Item (Join-Path $validationRoot 'phase0-results.json') (Join-Path $output 'baseline.json')
    $baseline = Get-Content (Join-Path $output 'baseline.json') -Raw | ConvertFrom-Json
    if ($baseline.runId -ne $runId -or $baseline.assertions -ne 611 -or @($baseline.results | Where-Object {$_.status -eq 'FAIL'}).Count) { throw 'Original 611 regressions failed.' }
    Invoke-Unity 'Metroidbrainia.Phase1Validation.Run' 'phase1-tests.log'
    Copy-Item (Join-Path $validationRoot 'phase1-results.json') (Join-Path $output 'report.json')
    $report = Get-Content (Join-Path $output 'report.json') -Raw | ConvertFrom-Json
    if ($report.runId -ne $runId) { throw 'Stale results.' }
    Write-Output "RESULTS=$output"
    $report | ConvertTo-Json -Depth 8
} finally {
    Verify-Protected; Verify-Scene
    'Protected production files and accepted artifacts unchanged. Scene differs only by added document contact settings.' | Set-Content (Join-Path $output 'integrity.txt')
}
