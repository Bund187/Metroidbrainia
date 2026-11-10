param(
    [string]$Unity = 'D:/Programas/UnityHub/Editor/6000.6.3f1/Editor/Unity.exe',
    [string]$OutputRoot = (Join-Path $env:TEMP ('MetroidbrainiaPhase0-' + [Guid]::NewGuid().ToString('N'))),
    [switch]$ReuseIsolatedCopy
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$validationRoot = [IO.Path]::GetFullPath($OutputRoot)
if ($validationRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'The validation copy must be outside the source project.' }
& (Join-Path $PSScriptRoot 'Reference.ps1') -Mode Verify
if ($ReuseIsolatedCopy) {
    $marker = Join-Path $validationRoot 'phase0-isolated-copy.marker'
    if (!(Test-Path $marker) -or (Get-Content $marker -Raw) -ne $projectRoot) { throw 'Not an isolated copy created by this runner.' }
    $records = Get-Content (Join-Path $PSScriptRoot 'Reference/files.json') -Raw | ConvertFrom-Json
    foreach ($record in $records | Where-Object {$_.path -like 'Assets/*.cs' -or $_.path -eq 'Assets/Scenes/TestArmScene.unity'}) {
        if ((Get-FileHash -LiteralPath (Join-Path $validationRoot $record.path) -Algorithm SHA256).Hash -ne $record.sha256) { throw "Validation source changed: $($record.path)" }
    }
} else {
if (Test-Path -LiteralPath $validationRoot) { throw 'Use a new destination or explicitly reuse a marked isolated copy.' }
New-Item -ItemType Directory -Path $validationRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets'),(Join-Path $projectRoot 'ProjectSettings') -Destination $validationRoot -Recurse
New-Item -ItemType Directory -Path (Join-Path $validationRoot 'Packages') | Out-Null
# Resolve only packages already present locally. No package versions in the source project are changed.
$manifest = Get-Content (Join-Path $projectRoot 'Packages/manifest.json') -Raw | ConvertFrom-Json
$dependencies = [ordered]@{}
foreach ($property in $manifest.dependencies.PSObject.Properties) { $dependencies[$property.Name] = $property.Value }
foreach ($packageDirectory in Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache') -Directory) {
    $packageJson = Join-Path $packageDirectory.FullName 'package.json'
    if (!(Test-Path $packageJson)) { continue }
    $package = Get-Content $packageJson -Raw | ConvertFrom-Json
    $dependencies[$package.name] = 'file:' + $packageDirectory.FullName.Replace('\','/')
}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $validationRoot 'Packages/manifest.json'), (@{dependencies=$dependencies} | ConvertTo-Json -Depth 8), $utf8)
}
$editorDirectory = Join-Path $validationRoot 'Assets/Editor/Phase0Validation'
New-Item -ItemType Directory -Path $editorDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Phase0Validation.cs') -Destination $editorDirectory
[IO.File]::WriteAllText((Join-Path $validationRoot 'phase0-isolated-copy.marker'), $projectRoot, (New-Object Text.UTF8Encoding($false)))
Write-Output "VALIDATION_PROJECT=$validationRoot"
$runId = [Guid]::NewGuid().ToString('N')
$process = Start-Process -FilePath $Unity -WorkingDirectory $validationRoot -ArgumentList @('-batchmode','-projectPath',('"'+$validationRoot+'"'),'-executeMethod','Metroidbrainia.Phase0Validation.Run','-phase0RunId',$runId,'-logFile',('"'+(Join-Path $validationRoot 'phase0.log')+'"')) -WindowStyle Hidden -PassThru
while (!$process.WaitForExit(2000)) { }
& (Join-Path $PSScriptRoot 'Reference.ps1') -Mode Verify
if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)). Inspect $validationRoot/phase0.log" }
if (!(Test-Path (Join-Path $validationRoot 'phase0-results.json'))) { throw 'Unity exited without producing results.' }
$result = Get-Content (Join-Path $validationRoot 'phase0-results.json') -Raw
if (($result | ConvertFrom-Json).runId -ne $runId) { throw 'Stale results do not belong to this run.' }
$result
