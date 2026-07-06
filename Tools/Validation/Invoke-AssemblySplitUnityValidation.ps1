[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$UnityPath,
    [switch]$ApplyVisualScriptingCleanup,
    [switch]$SkipDotnetBuild,
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
        throw "Unity project appears to be open. Close the Editor before running batch validation. Lockfile=$lockFile; Processes=$(Get-UnityProcessSummary)"
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

    Write-Host 'Unity project lockfile is absent; batch validation can continue.'
}

function Assert-UnityLogHasNoValidationErrors {
    param([string]$LogPath)

    if (-not (Test-Path -LiteralPath $LogPath)) {
        throw "Unity validation log was not produced: $LogPath"
    }

    $validationErrorPatterns = @(
        '\berror CS\d{4}\b',
        '\berror BC\d{4}\b',
        'Compilation failed',
        'Scripts have compiler errors',
        'All compiler errors have to be fixed',
        'The referenced script on this Behaviour.*is missing',
        'The referenced script \(.*\) on this Behaviour is missing',
        'The associated script can\s*not be loaded',
        'The associated script cannot be loaded',
        'has missing script',
        'missing script components',
        'Serialized managed-reference integrity flag is set',
        'm_Script GUID is missing from the AssetDatabase',
        'm_Script GUID resolves to a non-C# asset path',
        'AssetDatabase could not load the main asset',
        'AssetDatabase could not resolve a main asset type'
    )
    $validationErrorPattern = $validationErrorPatterns -join '|'
    $validationErrors = @(Select-String -LiteralPath $LogPath -Pattern $validationErrorPattern -ErrorAction SilentlyContinue)
    if ($validationErrors.Count -eq 0) {
        return
    }

    Write-Host 'Unity compile/import/serialization error lines:'
    $validationErrors | Select-Object -First 80 | ForEach-Object { Write-Host $_.Line }
    Write-Host 'Unity log tail:'
    Get-Content -LiteralPath $LogPath -Tail 160

    throw "Unity batch validation log contains compile/import/serialization errors. Count=$($validationErrors.Count)"
}

function Invoke-UnityBatchValidation {
    param(
        [string]$ResolvedUnityPath,
        [bool]$RunVisualScriptingCleanup
    )

    $logPath = Join-Path $script:ProjectRoot 'Temp\AssemblySplitUnityValidation.log'
    $arguments = @(
        '-batchmode',
        '-quit',
        '-projectPath',
        $script:ProjectRoot,
        '-logFile',
        $logPath
    )

    $arguments += @(
        '-executeMethod',
        'AssemblySplitSerializedReferenceValidatorWindow.RunAllValidationsFromCommandLine'
    )

    if ($RunVisualScriptingCleanup) {
        $arguments += '-assemblySplitApplyVisualScriptingCleanup'
    }

    Write-Host "Running Unity batch validation..."
    Write-Host "  Unity: $ResolvedUnityPath"
    Write-Host "  Log:   $logPath"

    $logDirectory = Split-Path -Parent $logPath
    if (-not (Test-Path -LiteralPath $logDirectory)) {
        New-Item -ItemType Directory -Path $logDirectory | Out-Null
    }

    if (Test-Path -LiteralPath $logPath) {
        try {
            Remove-Item -LiteralPath $logPath -Force
        } catch {
            $timestamp = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
            $fallbackLogPath = Join-Path $script:ProjectRoot "Temp\AssemblySplitUnityValidation.$timestamp.log"
            Write-Host "Default Unity validation log could not be removed. Using fallback log path. Original=$logPath; Fallback=$fallbackLogPath; Error=$($_.Exception.Message)"
            $logPath = $fallbackLogPath
            $logFileArgumentIndex = [Array]::IndexOf($arguments, '-logFile')
            if ($logFileArgumentIndex -ge 0 -and $logFileArgumentIndex + 1 -lt $arguments.Count) {
                $arguments[$logFileArgumentIndex + 1] = $logPath
            }
        }
    }

    Write-Host "UnityValidationLogPath=$logPath"

    $argumentText = ConvertTo-CmdArgumentString -Arguments $arguments
    $unityProcess = Start-Process -FilePath $ResolvedUnityPath -ArgumentList $argumentText -Wait -PassThru
    $exitCode = $unityProcess.ExitCode
    if ($exitCode -ne 0) {
        if (Test-Path -LiteralPath $logPath) {
            Write-Host 'Unity log tail:'
            Get-Content -LiteralPath $logPath -Tail 160
        }

        throw "Unity batch validation failed with exit code $exitCode."
    }

    Assert-UnityLogHasNoValidationErrors -LogPath $logPath
}

function Invoke-StaticAudit {
    $auditScript = Join-Path $script:ProjectRoot 'Tools\Validation\Invoke-AssemblySplitStaticAudit.ps1'
    & powershell -ExecutionPolicy Bypass -File $auditScript -ProjectRoot $script:ProjectRoot
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Assembly split static audit failed with exit code $exitCode."
    }
}

function Resolve-MSBuildPath {
    $commonCandidates = @(
        'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
    )

    foreach ($candidate in $commonCandidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Clear-MSBuildTempFiles {
    $tempObjRoot = Join-Path $script:ProjectRoot 'Temp\obj'
    if (-not (Test-Path -LiteralPath $tempObjRoot)) {
        return
    }

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($script:ProjectRoot).TrimEnd('\', '/')
    $resolvedTempObjRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $tempObjRoot).Path).TrimEnd('\', '/')
    if (-not $resolvedTempObjRoot.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected Temp\obj path outside project root: $resolvedTempObjRoot"
    }

    $tempFiles = @(Get-ChildItem -LiteralPath $resolvedTempObjRoot -Recurse -Filter '*.tmp' -File -ErrorAction SilentlyContinue)
    foreach ($tempFile in $tempFiles) {
        $resolvedTempFile = [System.IO.Path]::GetFullPath($tempFile.FullName)
        if (-not $resolvedTempFile.StartsWith($resolvedTempObjRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected MSBuild temp file path outside Temp\obj: $resolvedTempFile"
        }

        Remove-Item -LiteralPath $resolvedTempFile -Force
    }

    if ($tempFiles.Count -gt 0) {
        Write-Host "Removed stale MSBuild temp files: $($tempFiles.Count)"
    }
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

function Invoke-MSBuildWithNormalizedPath {
    param(
        [string]$MSBuildPath,
        [string[]]$Arguments
    )

    $pathEntries = New-Object System.Collections.Generic.List[string]
    $toolDirectory = Split-Path -Parent $MSBuildPath
    if (-not [string]::IsNullOrWhiteSpace($toolDirectory)) {
        $pathEntries.Add($toolDirectory)
    }

    foreach ($pathEntry in ([string]$env:PATH -split ';')) {
        if (-not [string]::IsNullOrWhiteSpace($pathEntry) -and -not $pathEntries.Contains($pathEntry)) {
            $pathEntries.Add($pathEntry)
        }
    }

    $normalizedPath = [string]::Join(';', $pathEntries)
    $argumentText = ConvertTo-CmdArgumentString -Arguments $Arguments
    $command = "set `"Path=`" & set `"PATH=$normalizedPath`" & `"$MSBuildPath`" $argumentText"

    & cmd.exe /D /C $command | Out-Host
    $exitCode = $LASTEXITCODE
    return $exitCode
}

function Invoke-SolutionBuild {
    if ($script:SkipDotnetBuild) {
        Write-Host 'Skipping solution build because -SkipDotnetBuild was specified.'
        return
    }

    $solutions = @(
        Get-ChildItem -LiteralPath $script:ProjectRoot -Filter '*.sln' -File
        Get-ChildItem -LiteralPath $script:ProjectRoot -Filter '*.slnx' -File
    )
    if ($solutions.Count -eq 0) {
        throw 'No generated .sln or .slnx file exists after Unity batch validation.'
    }

    $msbuildPath = Resolve-MSBuildPath
    foreach ($solution in $solutions) {
        Write-Host "Building generated solution: $($solution.FullName)"
        if (-not [string]::IsNullOrWhiteSpace($msbuildPath)) {
            Clear-MSBuildTempFiles
            $restoreExitCode = Invoke-MSBuildWithNormalizedPath -MSBuildPath $msbuildPath -Arguments @($solution.FullName, '/t:Restore', '/v:minimal')
            if ($restoreExitCode -ne 0) {
                throw "MSBuild restore failed for $($solution.Name) with exit code $restoreExitCode."
            }

            $buildExitCode = Invoke-MSBuildWithNormalizedPath -MSBuildPath $msbuildPath -Arguments @($solution.FullName, '/t:Build', '/p:Restore=false', '/v:minimal')
            if ($buildExitCode -ne 0) {
                throw "MSBuild build failed for $($solution.Name) with exit code $buildExitCode."
            }

            continue
        }

        dotnet build $solution.FullName
        $dotnetExitCode = $LASTEXITCODE
        if ($dotnetExitCode -ne 0) {
            throw "dotnet build failed for $($solution.Name) with exit code $dotnetExitCode."
        }
    }
}

$script:ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$script:SkipDotnetBuild = $SkipDotnetBuild.IsPresent

$resolvedUnityPath = Resolve-UnityPath -ExplicitPath $UnityPath
if ($WaitForUnityClose.IsPresent) {
    Wait-UnityProjectClosed -TimeoutSeconds $WaitForUnityCloseTimeoutSeconds -PollSeconds $WaitForUnityClosePollSeconds
} else {
    Assert-UnityProjectIsClosed
}
Invoke-UnityBatchValidation -ResolvedUnityPath $resolvedUnityPath -RunVisualScriptingCleanup:$ApplyVisualScriptingCleanup.IsPresent
Invoke-StaticAudit
Invoke-SolutionBuild

Write-Host 'Assembly split Unity validation completed successfully.'
