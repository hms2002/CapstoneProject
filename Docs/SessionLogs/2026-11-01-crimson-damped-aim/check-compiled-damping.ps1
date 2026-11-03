# Executes the compiled Gameplay method with Unity's real managed Vector3/Mathf implementation.
# This is a managed math check, not Unity Play Mode or a scene test.
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
[void][System.Reflection.Assembly]::LoadFrom('C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll')
[void][System.Reflection.Assembly]::LoadFrom((Join-Path $projectRoot 'Temp/bin/Debug/Core.dll'))
$assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $projectRoot 'Temp/bin/Debug/Gameplay.dll'))
$method = $assembly.GetType('AbilityLogic_CrimsonBoundaryCharge', $true).GetMethod('DampChargeTarget')
$script:checks = 0
function Check($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Distance($a, $b) {
    return [Math]::Sqrt([Math]::Pow($a.x - $b.x, 2) + [Math]::Pow($a.y - $b.y, 2) + [Math]::Pow($a.z - $b.z, 2))
}
function Follow($current, $target, [single]$dt) {
    return $method.Invoke($null, @($current, $target, $dt))
}
$results = @()
foreach ($fps in @(30, 60, 120)) {
    $currentPoint = [UnityEngine.Vector3]::new(0, 0, 0)
    $targetPoint = [UnityEngine.Vector3]::new(.5, 0, 0)
    for ($i = 0; $i -lt $fps / 5; $i++) {
        $before = Distance $currentPoint $targetPoint
        $currentPoint = Follow $currentPoint $targetPoint (1.0 / $fps)
        Check ((Distance $currentPoint $targetPoint) -lt $before) "Monotonic approach at $fps FPS"
        Check ($currentPoint.x -lt .5 -and $currentPoint.x -gt 0) 'Smooth approach without overshoot'
    }
    Check ([Math]::Abs($currentPoint.x - .482163) -lt .0001) "Same0.2s response at $fps FPS"
    $settledAtPointTwo = $currentPoint.x
    $maximumLag = 0.0
    for ($i = 0; $i -lt $fps * 2; $i++) {
        $t = $i / [double]$fps
        $targetPoint = [UnityEngine.Vector3]::new(100 * [Math]::Sin($t * 12), 80 * [Math]::Cos($t * 9), 0)
        $currentPoint = Follow $currentPoint $targetPoint (1.0 / $fps)
        $lag = Distance $currentPoint $targetPoint
        $maximumLag = [Math]::Max($maximumLag, $lag)
        Check ($lag -le .7501) "Lag limit during rapid motion at $fps FPS"
    }
    $paused = Follow $currentPoint ([UnityEngine.Vector3]::new(-100, -100, 0)) 0
    Check (($paused.x -eq $currentPoint.x) -and ($paused.y -eq $currentPoint.y) -and ($paused.z -eq $currentPoint.z)) 'Pause freezes position'
    $results += [PSCustomObject]@{FPS=$fps;PositionAtPointTwoSeconds=$settledAtPointTwo;MaximumObservedLag=$maximumLag}
}
$output = [PSCustomObject]@{Passed=$true;Checks=$script:checks;Execution='Compiled Gameplay.DampChargeTarget using real Unity managed math outside Editor';Cases=$results;Limit='No scene, coroutine, physics, native rendering or Play Mode coverage'}
$output | ConvertTo-Json -Depth 4 | Tee-Object -FilePath (Join-Path $PSScriptRoot 'compiled-damping-results.json')
