param([Parameter(Mandatory=$true)][string]$Prepared)
$ErrorActionPreference='Stop'
$Prepared=(Resolve-Path -LiteralPath $Prepared).Path
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scenePath=Join-Path $root 'Assets/Scenes/TestArmScene.unity'
$before=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Before/TestArmScene.unity.txt'))
$generated=[IO.File]::ReadAllText((Join-Path $Prepared 'PointConfigured.unity')).Replace("`r`n","`n")
$pattern='(?ms)^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\z)'
$old=@{};foreach($m in [regex]::Matches($before,$pattern)){$old[$m.Groups[1].Value]=$m.Value.Replace("`r`n","`n")}
$new=@{};foreach($m in [regex]::Matches($generated,$pattern)){$new[$m.Groups[1].Value]=$m.Value}
if(@($old.Keys | Where-Object {!$new.ContainsKey($_)}).Count){throw 'A source YAML object disappeared.'}
$added=@($new.Keys | Where-Object {!$old.ContainsKey($_)})
if($added.Count -ne 4){throw 'Expected only Point adapter and one puzzle GameObject/Transform/component.'}
$adapter=@($added | Where-Object {$new[$_] -match 'Hand3DPointInteractor'})
$puzzle=@($added | Where-Object {$new[$_] -match 'Metroidbrainia.BellSequencePuzzle'})
$puzzleRoot=@($added | Where-Object {$new[$_] -match '^--- !u!4 '})
if($adapter.Count -ne 1 -or $puzzle.Count -ne 1 -or $puzzleRoot.Count -ne 1){throw 'Unexpected added object types.'}
$changes=@();$bellChanges=0;$bindingChanges=0;$prefabChanges=0;$rootChanges=0
foreach($key in $old.Keys){
    if($old[$key] -ceq $new[$key]){continue}
    $scrubbed=$new[$key]
    if($scrubbed -match 'Metroidbrainia.BellInteractable'){
        $scrubbed=$scrubbed.Replace(('  puzzle: {fileID: '+$puzzle[0]+'}'),'  puzzle: {fileID: 0}');$bellChanges++
    }elseif($scrubbed -match 'Metroidbrainia.Hand3DExperiment.Hand3DPoseActions'){
        $scrubbed=[regex]::Replace($scrubbed,'(?ms)^  bindings:.*\z',"  bindings: []`n");$bindingChanges++
    }elseif($scrubbed -match '^--- !u!1001 '){
        $scrubbed=[regex]::Replace($scrubbed,'(?m)^    - targetCorrespondingSourceObject: [^\n]+\n      insertIndex: -1\n      addedObject: \{fileID: '+$adapter[0]+'\}\n','');$prefabChanges++
    }elseif($scrubbed -match '^--- !u!1660057539 '){
        $scrubbed=$scrubbed.Replace(('  - {fileID: '+$puzzleRoot[0]+'}'+"`n"),'');$rootChanges++
    }else{throw "Unexpected modified YAML block: $key"}
    if($scrubbed -cne $old[$key]){throw "Unrelated serialized changes in block $key"}
    $changes+=$key
}
if($bellChanges -ne 4 -or $bindingChanges -ne 1 -or $prefabChanges -ne 1 -or $rootChanges -ne 1){throw 'Unexpected scene modification counts.'}
$eol=if($before.Contains("`r`n")){"`r`n"}else{"`n"}
$merged=[regex]::Replace($before,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m)
    $key=$m.Groups[1].Value
    if($key -eq '9223372036854775807'){
        $blocks=($added | Sort-Object | ForEach-Object {$new[$_]}) -join ''
        return ($blocks+$new[$key]).Replace("`n",$eol)
    }
    if($changes -contains $key){return $new[$key].Replace("`n",$eol)}
    return $m.Value
})
# Unity-generated metadata/assets only; never overwrite source scripts from an older preparation copy.
if([IO.File]::ReadAllText($scenePath) -cne $before -and [IO.File]::ReadAllText($scenePath) -cne $merged){throw 'Saved scene differs from both the source and reviewed merge. Do not overwrite user edits.'}
$artifacts=Join-Path $Prepared 'GeneratedAssets'
foreach($file in Get-ChildItem $artifacts -Recurse -File | Where-Object {$_.Extension -eq '.meta' -or $_.Extension -eq '.asset'}){
    $relative=$file.FullName.Substring($artifacts.Length+1);$destination=Join-Path $root $relative
    if(Test-Path $destination){if((Get-FileHash $destination).Hash -ne (Get-FileHash $file.FullName).Hash){throw "Existing generated asset differs: $relative"};continue}
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
[IO.File]::WriteAllText($scenePath,$merged,(New-Object Text.UTF8Encoding($false)))
[pscustomobject]@{prepared=$Prepared;changedBlocks=$changes;addedBlocks=$added;bellChanges=$bellChanges;allOtherBlocksIdentical=$true;sha256=(Get-FileHash $scenePath -Algorithm SHA256).Hash} | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'scene-installation.json') -Encoding UTF8
Write-Output 'Installed only seven reviewed YAML block edits and four new scene objects. Original serialized values otherwise unchanged.'
