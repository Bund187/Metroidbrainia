param([ValidateSet('Capture','Verify')][string]$Mode = 'Verify')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$referenceRoot = Join-Path $PSScriptRoot 'Reference'
$manifestPath = Join-Path $referenceRoot 'files.json'
$files = @('AGENTS.md') + @(Get-ChildItem (Join-Path $projectRoot 'Assets'),(Join-Path $projectRoot 'Packages'),(Join-Path $projectRoot 'ProjectSettings') -Recurse -File | ForEach-Object { $_.FullName.Substring($projectRoot.Length + 1).Replace('\','/') })
if ($Mode -eq 'Capture') {
    if (Test-Path $manifestPath) { throw 'Reference exists. Do not overwrite the approved baseline.' }
    New-Item -ItemType Directory -Path $referenceRoot -Force | Out-Null
    $records = @($files | Sort-Object | ForEach-Object {
        $filePath = Join-Path $projectRoot $_
        [pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $filePath).Length}
    })
    $records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Scenes/TestArmScene.unity') -Destination (Join-Path $referenceRoot 'TestArmScene.unity.txt')
    Write-Output "Captured $($records.Count) source files. Scene snapshot is outside Assets and is not imported."
} else {
    $records = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $differences = @()
    foreach ($record in $records) {
        $filePath = Join-Path $projectRoot $record.path
        if (!(Test-Path -LiteralPath $filePath)) { $differences += "MISSING $($record.path)" }
        elseif ((Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash -ne $record.sha256) { $differences += "CHANGED $($record.path)" }
    }
    $known = @{}; foreach ($record in $records) { $known[$record.path] = $true }
    foreach ($relativePath in $files) { if (!$known.ContainsKey($relativePath)) { $differences += "ADDED $relativePath" } }
    if ($differences.Count) { $differences | Write-Output; throw 'Reference differs. No files were restored or changed.' }
    Write-Output "REFERENCE_PASS: $($records.Count) source files unchanged."
}
