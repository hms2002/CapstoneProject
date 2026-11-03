param(
    [string]$Method = 'PuddlePixelVisualValidation.ApplyAndCapture',
    [string]$LogName = 'unity-apply-capture-final.log',
    [switch]$Tests
)
$puddleProject=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$puddleEditor='C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe'
$puddleDeadline=[DateTime]::UtcNow.AddMinutes(12)
$puddleWasBusy=$false
while ($true) {
    $puddleBusy=@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
        -not $_.CommandLine -or ($_.CommandLine.IndexOf($puddleProject,[StringComparison]::OrdinalIgnoreCase) -ge 0 -and $_.CommandLine -notmatch '-adb2')
    })
    if ($puddleBusy.Count -eq 0) { break }
    if (-not $puddleWasBusy) { Write-Output ('Waiting for existing Unity process: '+($puddleBusy.ProcessId -join ',')); $puddleWasBusy=$true }
    if ([DateTime]::UtcNow -ge $puddleDeadline) { throw 'Existing Unity operation still owns the project; no process was interrupted.' }
    Start-Sleep -Seconds 3
}
$puddleLog=Join-Path $PSScriptRoot $LogName
$puddleArgs=@('-batchmode','-projectPath',('"'+$puddleProject+'"'),'-logFile',('"'+$puddleLog+'"'))
if ($Tests) {
    $puddleResult=Join-Path $PSScriptRoot 'playmode-results.xml'
    $puddleArgs+=@('-runTests','-testPlatform','PlayMode','-testFilter','PuddleProjectileVisualPlayModeTests;AlcoholPuddleContactPlayModeTests;AlcoholExposurePlayModeTests','-testResults',('"'+$puddleResult+'"'),'-uiBatchValidationTests')
} else {
    $puddleArgs+=@('-executeMethod',$Method,'-quit')
}
$puddleProcess=Start-Process -FilePath $puddleEditor -ArgumentList $puddleArgs -WorkingDirectory $puddleProject -WindowStyle Hidden -PassThru
Write-Output ('Started Unity process '+$puddleProcess.Id)
$puddleProcess.WaitForExit()
Write-Output ('Unity exit '+$puddleProcess.ExitCode)
Select-String -LiteralPath $puddleLog -Pattern 'error CS|Exception:|PUDDLE_PIXEL|Aborting batchmode|Test Run Summary|Tests failed' | Select-Object -Last 12
exit $puddleProcess.ExitCode
