[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$UnityPath,
    [switch]$WaitForUnityClose,
    [ValidateRange(1, 86400)]
    [int]$WaitForUnityCloseTimeoutSeconds = 600,
    [ValidateRange(1, 300)]
    [int]$WaitForUnityClosePollSeconds = 5
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..')).Path
}

function Resolve-UnityPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "Unity executable was not found: $ExplicitPath"
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $runningUnity = Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($runningUnity -and -not [string]::IsNullOrWhiteSpace($runningUnity.Path)) {
        return $runningUnity.Path
    }

    $commonCandidates = @(
        'D:\Unity\6000.4.2f1\Editor\Unity.exe',
        'C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe'
    )

    foreach ($candidate in $commonCandidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw 'Unity executable could not be resolved. Pass -UnityPath explicitly.'
}

function Get-UnityProcessSummary {
    $processes = @(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id, StartTime, Path)
    if ($processes.Count -eq 0) {
        return 'No Unity process was visible to PowerShell.'
    }

    return (($processes | ForEach-Object {
        "PID=$($_.Id), Start=$($_.StartTime), Path=$($_.Path)"
    }) -join '; ')
}

function Test-UnityProjectLock {
    $lockFile = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
    return Test-Path -LiteralPath $lockFile
}

function Assert-UnityProjectIsClosed {
    if (Test-UnityProjectLock) {
        $lockFile = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
        throw "Unity project appears to be open. Close the Editor before running Addressables build validation. Lockfile=$lockFile; Processes=$(Get-UnityProcessSummary)"
    }
}

function Wait-UnityProjectClosed {
    param(
        [int]$TimeoutSeconds,
        [int]$PollSeconds
    )

    $lockFile = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (Test-UnityProjectLock) {
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "Timed out waiting for Unity project lock to be released. Lockfile=$lockFile; TimeoutSeconds=$TimeoutSeconds; Processes=$(Get-UnityProcessSummary)"
        }

        Write-Host "Waiting for Unity project lock to be released. Lockfile=$lockFile; Processes=$(Get-UnityProcessSummary)"
        Start-Sleep -Seconds $PollSeconds
    }

    Write-Host 'Unity project lockfile is absent; Addressables build validation can continue.'
}

function ConvertTo-CmdArgumentString {
    param([string[]]$Arguments)

    return [string]::Join(' ', ($Arguments | ForEach-Object {
        $argument = [string]$_
        if ($argument -notmatch '[\s"]') {
            return $argument
        }

        return '"' + ($argument -replace '"', '\"') + '"'
    }))
}

function Get-AddressablesBuildLinkXmlCandidates {
    $addressablesBuildRoot = Join-Path $script:ProjectRoot 'Library\com.unity.addressables\aa'
    if (-not (Test-Path -LiteralPath $addressablesBuildRoot)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $addressablesBuildRoot -Recurse -Filter 'link.xml' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]AddressablesLink[\\/]link\.xml$' } |
        Sort-Object LastWriteTime -Descending)
}

function Assert-UnityLogHasNoAddressablesBuildErrors {
    param([string]$LogPath)

    if (-not (Test-Path -LiteralPath $LogPath)) {
        throw "Unity Addressables build log was not produced: $LogPath"
    }

    $errorPatterns = @(
        '\berror CS\d{4}\b',
        '\berror BC\d{4}\b',
        'Compilation failed',
        'Scripts have compiler errors',
        'Addressable content build failure',
        'Failed to build Addressables content',
        'BuildFailedException'
    )

    $errors = @(Select-String -LiteralPath $LogPath -Pattern ($errorPatterns -join '|') -ErrorAction SilentlyContinue)
    if ($errors.Count -eq 0) {
        return
    }

    Write-Host 'Unity Addressables build error lines:'
    $errors | Select-Object -First 80 | ForEach-Object { Write-Host $_.Line }
    Write-Host 'Unity log tail:'
    Get-Content -LiteralPath $LogPath -Tail 160

    throw "Unity Addressables build log contains errors. Count=$($errors.Count)"
}

function Get-XmlTypeCount {
    param([xml]$Xml)

    return (@($Xml.linker.assembly) | ForEach-Object { @($_.type).Count } | Measure-Object -Sum).Sum
}

$script:ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$resolvedUnityPath = Resolve-UnityPath -ExplicitPath $UnityPath

if ($WaitForUnityClose.IsPresent) {
    Wait-UnityProjectClosed -TimeoutSeconds $WaitForUnityCloseTimeoutSeconds -PollSeconds $WaitForUnityClosePollSeconds
} else {
    Assert-UnityProjectIsClosed
}

$tempRoot = Join-Path $script:ProjectRoot 'Temp'
if (-not (Test-Path -LiteralPath $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
}

$logPath = Join-Path $tempRoot 'AssemblySplitAddressablesBuildValidation.log'
if (Test-Path -LiteralPath $logPath) {
    try {
        Remove-Item -LiteralPath $logPath -Force
    } catch {
        $timestamp = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
        $logPath = Join-Path $tempRoot "AssemblySplitAddressablesBuildValidation.$timestamp.log"
    }
}

$buildStartedAt = Get-Date
$beforeCandidates = @(Get-AddressablesBuildLinkXmlCandidates)

$arguments = @(
    '-batchmode',
    '-quit',
    '-projectPath',
    $script:ProjectRoot,
    '-logFile',
    $logPath,
    '-executeMethod',
    'UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent'
)

Write-Host 'Running Addressables player content build validation...'
Write-Host "Unity=$resolvedUnityPath"
Write-Host "Log=$logPath"
Write-Host "AddressablesBuildLinkXmlCandidatesBefore=$($beforeCandidates.Count)"

$argumentText = ConvertTo-CmdArgumentString -Arguments $arguments
$unityProcess = Start-Process -FilePath $resolvedUnityPath -ArgumentList $argumentText -Wait -PassThru
if ($unityProcess.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host 'Unity log tail:'
        Get-Content -LiteralPath $logPath -Tail 160
    }

    throw "Unity Addressables build validation failed with exit code $($unityProcess.ExitCode)."
}

Assert-UnityLogHasNoAddressablesBuildErrors -LogPath $logPath

$afterCandidates = @(Get-AddressablesBuildLinkXmlCandidates)
$generatedLinkXml = $afterCandidates |
    Where-Object { $_.LastWriteTime -ge $buildStartedAt.AddMinutes(-1) } |
    Select-Object -First 1

if (-not $generatedLinkXml) {
    $latest = $afterCandidates | Select-Object -First 1
    if ($latest) {
        throw "Addressables build link.xml was not regenerated by this validation. Latest=$($latest.FullName); LastWriteTime=$($latest.LastWriteTime); BuildStartedAt=$buildStartedAt"
    }

    throw 'Addressables build did not produce AddressablesLink/link.xml.'
}

$generatedText = Get-Content -LiteralPath $generatedLinkXml.FullName -Raw
try {
    [xml]$generatedXml = $generatedText
} catch {
    throw "Generated Addressables link.xml is invalid XML: $($_.Exception.Message)"
}

if (-not $generatedXml.linker) {
    throw 'Generated Addressables link.xml root must be <linker>.'
}

$assemblyCSharpReferenceCount = @($generatedText | Select-String -Pattern 'Assembly-CSharp' -AllMatches).Matches.Count
if ($assemblyCSharpReferenceCount -ne 0) {
    throw "Generated Addressables link.xml still contains Assembly-CSharp references. Count=$assemblyCSharpReferenceCount"
}

$assemblyCount = @($generatedXml.linker.assembly).Count
$typeCount = Get-XmlTypeCount -Xml $generatedXml
if ($typeCount -le 0) {
    throw 'Generated Addressables link.xml contains no linker type entries.'
}

Write-Host 'Addressables build link.xml validation complete.'
Write-Host "AddressablesBuildLogPath=$logPath"
Write-Host "AddressablesBuildLinkXmlPath=$($generatedLinkXml.FullName)"
Write-Host "AddressablesBuildLinkXmlLastWriteTime=$($generatedLinkXml.LastWriteTime.ToString('o'))"
Write-Host "AddressablesBuildLinkXmlAssemblies=$assemblyCount"
Write-Host "AddressablesBuildLinkXmlEntries=$typeCount"
Write-Host "AddressablesBuildLinkXmlAssemblyCSharpReferences=$assemblyCSharpReferenceCount"
