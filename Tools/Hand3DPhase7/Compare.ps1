param([Parameter(Mandatory=$true)][string]$Before,[Parameter(Mandatory=$true)][string]$After)
$ErrorActionPreference='Stop'
$reference=Get-Content (Join-Path $Before 'phase7-results.json') -Raw | ConvertFrom-Json
$current=Get-Content (Join-Path $After 'phase7-results.json') -Raw | ConvertFrom-Json
$oldPoses=(Get-Content (Join-Path $Before 'phase7-poses.json') -Raw | ConvertFrom-Json).poses
$newPoses=(Get-Content (Join-Path $After 'phase7-poses.json') -Raw | ConvertFrom-Json).poses
if($oldPoses.Count -ne $newPoses.Count){throw 'Trace sizes differ.'}
$maxPosition=0.0; $maxAngle=0.0; $checks=0
for($sample=0;$sample -lt $oldPoses.Count;$sample++){
    $old=$oldPoses[$sample];$new=$newPoses[$sample]
    if($old.backend -ne $new.backend -or $old.mask -ne $new.mask -or $old.positions.Count -ne $new.positions.Count){throw 'Trace identities differ.'}
    for($joint=0;$joint -lt $old.positions.Count;$joint++){
        $a=$old.positions[$joint];$b=$new.positions[$joint]
        $distance=[math]::Sqrt([math]::Pow($a.x-$b.x,2)+[math]::Pow($a.y-$b.y,2)+[math]::Pow($a.z-$b.z,2))
        if([double]::IsNaN($distance) -or [double]::IsInfinity($distance) -or $distance -gt 0.00005){throw "Position changed at sample $sample joint $joint : $distance"}
        $maxPosition=[math]::Max($maxPosition,$distance);$checks++
        $a=$old.rotations[$joint];$b=$new.rotations[$joint]
        $an=$a.x*$a.x+$a.y*$a.y+$a.z*$a.z+$a.w*$a.w
        $bn=$b.x*$b.x+$b.y*$b.y+$b.z*$b.z+$b.w*$b.w
        $dot=[math]::Abs(($a.x*$b.x+$a.y*$b.y+$a.z*$b.z+$a.w*$b.w)/[math]::Sqrt($an*$bn))
        $angle=2*[math]::Acos([math]::Min(1.0,$dot))*180/[math]::PI
        if([double]::IsNaN($angle) -or [math]::Abs($an-1) -gt 0.00001 -or [math]::Abs($bn-1) -gt 0.00001 -or $angle -gt 0.05){throw "Rotation changed at sample $sample joint $joint : $angle"}
        $maxAngle=[math]::Max($maxAngle,$angle);$checks++
    }
}
$table=@()
foreach($old in $reference.measurements){
    $new=$current.measurements | Where-Object operation -eq $old.operation
    if(!$new){throw "Missing measurement $($old.operation)"}
    $a=($old.microseconds | Sort-Object)[2];$b=($new.microseconds | Sort-Object)[2]
    $table+=[pscustomobject]@{operation=$old.operation;beforeUs=$a;afterUs=$b;differenceUs=$b-$a;beforeBytes=($old.bytes|Sort-Object)[2];afterBytes=($new.bytes|Sort-Object)[2];beforeBakes=$old.bakeCalls;afterBakes=$new.bakeCalls;beforeProjections=$old.projections;afterProjections=$new.projections;beforeWorldUpdates=$old.worldUpdates;afterWorldUpdates=$new.worldUpdates}
}
[pscustomobject]@{beforeRun=$reference.runId;afterRun=$current.runId;scope='Synthetic Edit Mode invocations, not gameplay frame times or FPS. Median of five batches. Exact inputs and independent scene loads. Bytes=-1 means unavailable, never zero.';beforeAllocationCounterSupported=$reference.allocationCounterSupported;afterAllocationCounterSupported=$current.allocationCounterSupported;poseAssertions=$checks;maxPosition=$maxPosition;maxAngleDegrees=$maxAngle;measurements=$table} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $After 'comparison.json') -Encoding UTF8
$table | Format-Table operation,beforeUs,afterUs,differenceUs,beforeWorldUpdates,afterWorldUpdates -AutoSize
Write-Output "Pose comparisons PASS: $checks; max position=$maxPosition; max angle=$maxAngle degrees"
