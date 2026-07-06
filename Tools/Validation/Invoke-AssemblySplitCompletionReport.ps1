[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$MSBuildPath,
    [string]$UnityPath,
    [switch]$SkipStaticAudit,
    [switch]$SkipLinkXmlDryRun,
    [switch]$RunUnityValidation,
    [switch]$RunAddressablesBuildValidation,
    [switch]$RunMSBuild,
    [switch]$FailOnIncomplete,
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

$tempRoot = Join-Path $ProjectRoot 'Temp'
if (-not (Test-Path -LiteralPath $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
}

$reportPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.txt'
$staticAuditLogPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.StaticAudit.log'
$linkXmlLogPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.LinkXmlDryRun.log'
$linkXmlRestoreLogPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.LinkXmlRestoreValidation.log'
$addressablesBuildValidationLogPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.AddressablesBuildValidation.log'
$unityValidationWrapperLogPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.UnityValidationWrapper.log'
$msbuildLogPath = Join-Path $tempRoot 'AssemblySplitCompletionReport.MSBuild.log'

function Get-RegexValue {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Default = ''
    )

    $match = [regex]::Match($Text, $Pattern)
    if (-not $match.Success) {
        return $Default
    }

    return $match.Groups[1].Value
}

function Add-ReportLine {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Text = ''
    )

    $Lines.Add($Text)
}

function Invoke-PowerShellScriptCapture {
    param(
        [string]$ScriptPath,
        [string[]]$Arguments,
        [string]$LogPath
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    Set-Content -LiteralPath $LogPath -Value $output -Encoding UTF8

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n")
        LogPath = $LogPath
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

function Resolve-MSBuildPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "MSBuild executable was not found: $ExplicitPath"
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

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

    throw 'MSBuild executable could not be resolved. Pass -MSBuildPath explicitly.'
}

function Clear-MSBuildTempFiles {
    $tempObjRoot = Join-Path $ProjectRoot 'Temp\obj'
    if (-not (Test-Path -LiteralPath $tempObjRoot)) {
        return 0
    }

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/')
    $resolvedTempObjRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $tempObjRoot).Path).TrimEnd('\', '/')
    if (-not $resolvedTempObjRoot.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected Temp\obj path outside project root: $resolvedTempObjRoot"
    }

    $removedCount = 0
    $tempFiles = @(Get-ChildItem -LiteralPath $resolvedTempObjRoot -Recurse -Filter '*.tmp' -File -ErrorAction SilentlyContinue)
    foreach ($tempFile in $tempFiles) {
        $resolvedTempFile = [System.IO.Path]::GetFullPath($tempFile.FullName)
        if (-not $resolvedTempFile.StartsWith($resolvedTempObjRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected MSBuild temp file path outside Temp\obj: $resolvedTempFile"
        }

        Remove-Item -LiteralPath $resolvedTempFile -Force
        $removedCount++
    }

    return $removedCount
}

function Clear-StaleUnityLockfile {
    $unityProcesses = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    if ($unityProcesses.Count -gt 0) {
        return [pscustomobject]@{
            Removed = $false
            Reason = 'Unity process is still running.'
        }
    }

    $lockFile = Join-Path $ProjectRoot 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $lockFile)) {
        return [pscustomobject]@{
            Removed = $false
            Reason = 'UnityLockfile was absent.'
        }
    }

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/')
    $resolvedLockFile = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $lockFile).Path)
    if (-not $resolvedLockFile.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected UnityLockfile path outside project root: $resolvedLockFile"
    }

    try {
        Remove-Item -LiteralPath $resolvedLockFile -Force
    } catch {
        return [pscustomobject]@{
            Removed = $false
            Reason = "Failed to remove stale lockfile: $resolvedLockFile; Error=$($_.Exception.Message)"
        }
    }

    return [pscustomobject]@{
        Removed = $true
        Reason = "Removed stale lockfile: $resolvedLockFile"
    }
}

function Invoke-UnityValidationRefresh {
    $unityValidationScript = Join-Path $ProjectRoot 'Tools\Validation\Invoke-AssemblySplitUnityValidation.ps1'
    $preLockCleanup = Clear-StaleUnityLockfile
    $unityLockFile = Join-Path $ProjectRoot 'Temp\UnityLockfile'
    $defaultUnityLogPath = Join-Path $tempRoot 'AssemblySplitUnityValidation.log'
    if (Test-Path -LiteralPath $unityLockFile) {
        $output = "Skipped Unity validation because UnityLockfile is still present after pre-cleanup. $($preLockCleanup.Reason)"
        Set-Content -LiteralPath $unityValidationWrapperLogPath -Value $output -Encoding UTF8
        return [pscustomobject]@{
            ExitCode = 1
            Output = $output
            LogPath = $unityValidationWrapperLogPath
            ResultLogPath = $defaultUnityLogPath
            PreLockCleanup = $preLockCleanup
            LockCleanup = $preLockCleanup
        }
    }

    $unityArguments = @(
        '-ProjectRoot', $ProjectRoot,
        '-WaitForUnityClose',
        '-WaitForUnityCloseTimeoutSeconds', [string]$WaitForUnityCloseTimeoutSeconds,
        '-WaitForUnityClosePollSeconds', [string]$WaitForUnityClosePollSeconds,
        '-SkipDotnetBuild'
    )

    if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
        $unityArguments += @('-UnityPath', $UnityPath)
    }

    $result = Invoke-PowerShellScriptCapture `
        -ScriptPath $unityValidationScript `
        -Arguments $unityArguments `
        -LogPath $unityValidationWrapperLogPath

    $lockCleanup = Clear-StaleUnityLockfile
    $resultLogPath = Get-RegexValue $result.Output 'UnityValidationLogPath=([^\r\n]+)' $defaultUnityLogPath

    return [pscustomobject]@{
        ExitCode = $result.ExitCode
        Output = $result.Output
        LogPath = $result.LogPath
        ResultLogPath = $resultLogPath
        PreLockCleanup = $preLockCleanup
        LockCleanup = $lockCleanup
    }
}

function Invoke-AddressablesBuildValidation {
    $addressablesBuildValidationScript = Join-Path $ProjectRoot 'Tools\Validation\Invoke-AssemblySplitAddressablesBuildValidation.ps1'
    $addressablesArguments = @(
        '-ProjectRoot', $ProjectRoot,
        '-WaitForUnityClose',
        '-WaitForUnityCloseTimeoutSeconds', [string]$WaitForUnityCloseTimeoutSeconds,
        '-WaitForUnityClosePollSeconds', [string]$WaitForUnityClosePollSeconds
    )

    if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
        $addressablesArguments += @('-UnityPath', $UnityPath)
    }

    return Invoke-PowerShellScriptCapture `
        -ScriptPath $addressablesBuildValidationScript `
        -Arguments $addressablesArguments `
        -LogPath $addressablesBuildValidationLogPath
}

function Invoke-MSBuildWithNormalizedPath {
    param(
        [string]$ResolvedMSBuildPath,
        [string[]]$Arguments
    )

    $pathEntries = New-Object System.Collections.Generic.List[string]
    $toolDirectory = Split-Path -Parent $ResolvedMSBuildPath
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
    $command = "set `"Path=`" & set `"PATH=$normalizedPath`" & `"$ResolvedMSBuildPath`" $argumentText"
    $output = & cmd.exe /D /C $command 2>&1

    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output -join "`n")
    }
}

function Invoke-GeneratedSolutionMSBuild {
    $solutions = @(
        Get-ChildItem -LiteralPath $ProjectRoot -Filter '*.sln' -File -ErrorAction SilentlyContinue
        Get-ChildItem -LiteralPath $ProjectRoot -Filter '*.slnx' -File -ErrorAction SilentlyContinue
    )

    if ($solutions.Count -eq 0) {
        throw 'No generated .sln or .slnx file exists for MSBuild validation.'
    }

    $resolvedMSBuildPath = Resolve-MSBuildPath -ExplicitPath $MSBuildPath
    $removedTempFiles = Clear-MSBuildTempFiles
    $logLines = [System.Collections.Generic.List[string]]::new()
    $logLines.Add("MSBuild: $resolvedMSBuildPath")
    $logLines.Add("RemovedTempFiles: $removedTempFiles")

    $overallExitCode = 0
    foreach ($solution in $solutions) {
        $logLines.Add("Building: $($solution.FullName)")
        $buildResult = Invoke-MSBuildWithNormalizedPath `
            -ResolvedMSBuildPath $resolvedMSBuildPath `
            -Arguments @($solution.FullName, '/t:Build', '/p:Restore=false', '/v:minimal', '/nologo')
        $logLines.Add($buildResult.Output)
        $logLines.Add("ExitCode: $($buildResult.ExitCode)")

        if ($buildResult.ExitCode -ne 0) {
            $overallExitCode = $buildResult.ExitCode
        }
    }

    Set-Content -LiteralPath $msbuildLogPath -Value $logLines -Encoding UTF8

    return [pscustomobject]@{
        ExitCode = $overallExitCode
        SolutionCount = $solutions.Count
        LogPath = $msbuildLogPath
        RemovedTempFiles = $removedTempFiles
    }
}

$reportLines = [System.Collections.Generic.List[string]]::new()
$incompleteReasons = [System.Collections.Generic.List[string]]::new()

Add-ReportLine $reportLines 'Assembly split completion report'
Add-ReportLine $reportLines "ProjectRoot: $ProjectRoot"
Add-ReportLine $reportLines "GeneratedAtUtc: $([DateTime]::UtcNow.ToString('u'))"
Add-ReportLine $reportLines ''

$unityValidationRefreshResult = $null
if ($RunUnityValidation.IsPresent) {
    $unityValidationRefreshResult = Invoke-UnityValidationRefresh

    Add-ReportLine $reportLines 'Unity validation refresh:'
    Add-ReportLine $reportLines "- Mode: executed"
    Add-ReportLine $reportLines "- ExitCode: $($unityValidationRefreshResult.ExitCode)"
    Add-ReportLine $reportLines "- Log: $($unityValidationRefreshResult.LogPath)"
    Add-ReportLine $reportLines "- ResultLog: $($unityValidationRefreshResult.ResultLogPath)"
    Add-ReportLine $reportLines "- PreLockCleanupRemoved: $($unityValidationRefreshResult.PreLockCleanup.Removed)"
    Add-ReportLine $reportLines "- PreLockCleanupReason: $($unityValidationRefreshResult.PreLockCleanup.Reason)"
    Add-ReportLine $reportLines "- LockCleanupRemoved: $($unityValidationRefreshResult.LockCleanup.Removed)"
    Add-ReportLine $reportLines "- LockCleanupReason: $($unityValidationRefreshResult.LockCleanup.Reason)"

    if ($unityValidationRefreshResult.ExitCode -ne 0) {
        $incompleteReasons.Add("Fresh Unity validation wrapper failed with exit code $($unityValidationRefreshResult.ExitCode).")
    }

    Add-ReportLine $reportLines ''
} else {
    Add-ReportLine $reportLines 'Unity validation refresh:'
    Add-ReportLine $reportLines "- Mode: latest-log-only"
    Add-ReportLine $reportLines "- Log: $unityValidationWrapperLogPath"
    $incompleteReasons.Add('Fresh Unity validation was not run by this report.')
    Add-ReportLine $reportLines ''
}

$addressablesBuildValidationResult = $null
if ($RunAddressablesBuildValidation.IsPresent) {
    $addressablesBuildValidationResult = Invoke-AddressablesBuildValidation

    $addressablesBuildLogPath = Get-RegexValue $addressablesBuildValidationResult.Output 'AddressablesBuildLogPath=([^\r\n]+)' 'unknown'
    $addressablesBuildLinkXmlPath = Get-RegexValue $addressablesBuildValidationResult.Output 'AddressablesBuildLinkXmlPath=([^\r\n]+)' 'unknown'
    $addressablesBuildLinkXmlAssemblies = Get-RegexValue $addressablesBuildValidationResult.Output 'AddressablesBuildLinkXmlAssemblies=(\d+)' 'unknown'
    $addressablesBuildLinkXmlEntries = Get-RegexValue $addressablesBuildValidationResult.Output 'AddressablesBuildLinkXmlEntries=(\d+)' 'unknown'
    $addressablesBuildLinkXmlAssemblyCSharp = Get-RegexValue $addressablesBuildValidationResult.Output 'AddressablesBuildLinkXmlAssemblyCSharpReferences=(\d+)' 'unknown'

    Add-ReportLine $reportLines 'Addressables build link.xml validation:'
    Add-ReportLine $reportLines "- Mode: executed"
    Add-ReportLine $reportLines "- ExitCode: $($addressablesBuildValidationResult.ExitCode)"
    Add-ReportLine $reportLines "- Log: $($addressablesBuildValidationResult.LogPath)"
    Add-ReportLine $reportLines "- UnityLog: $addressablesBuildLogPath"
    Add-ReportLine $reportLines "- GeneratedLinkXml: $addressablesBuildLinkXmlPath"
    Add-ReportLine $reportLines "- GeneratedAssemblies: $addressablesBuildLinkXmlAssemblies"
    Add-ReportLine $reportLines "- GeneratedEntries: $addressablesBuildLinkXmlEntries"
    Add-ReportLine $reportLines "- GeneratedAssemblyCSharpReferences: $addressablesBuildLinkXmlAssemblyCSharp"
    Add-ReportLine $reportLines "- ProjectConfigFolderLinkXmlRole: temporary player-build copy deleted by Addressables editor load"

    if ($addressablesBuildValidationResult.ExitCode -ne 0 -or $addressablesBuildLinkXmlEntries -eq 'unknown' -or $addressablesBuildLinkXmlAssemblyCSharp -ne '0') {
        $incompleteReasons.Add('Addressables build link.xml validation failed or generated stale Assembly-CSharp preserve references.')
    }

    Add-ReportLine $reportLines ''
} else {
    Add-ReportLine $reportLines 'Addressables build link.xml validation:'
    Add-ReportLine $reportLines "- Mode: not-run"
    Add-ReportLine $reportLines "- Log: $addressablesBuildValidationLogPath"
    $incompleteReasons.Add('Fresh Addressables build link.xml validation was not run by this report.')
    Add-ReportLine $reportLines ''
}

$staticAuditResult = $null
if (-not $SkipStaticAudit) {
    $staticAuditScript = Join-Path $ProjectRoot 'Tools\Validation\Invoke-AssemblySplitStaticAudit.ps1'
    $staticAuditResult = Invoke-PowerShellScriptCapture `
        -ScriptPath $staticAuditScript `
        -Arguments @('-ProjectRoot', $ProjectRoot) `
        -LogPath $staticAuditLogPath

    $staticErrors = Get-RegexValue $staticAuditResult.Output 'Errors:\s+(\d+)' 'unknown'
    $staticWarnings = Get-RegexValue $staticAuditResult.Output 'Warnings:\s+(\d+)' 'unknown'
    $staticInfos = Get-RegexValue $staticAuditResult.Output 'Infos:\s+(\d+)' 'unknown'
    $completionSummary = Get-RegexValue $staticAuditResult.Output '(Current completion blockers:[^\r\n]+)' 'not found'

    Add-ReportLine $reportLines 'Static audit:'
    Add-ReportLine $reportLines "- ExitCode: $($staticAuditResult.ExitCode)"
    Add-ReportLine $reportLines "- Errors: $staticErrors"
    Add-ReportLine $reportLines "- Warnings: $staticWarnings"
    Add-ReportLine $reportLines "- Infos: $staticInfos"
    Add-ReportLine $reportLines "- Completion: $completionSummary"
    Add-ReportLine $reportLines "- Log: $staticAuditLogPath"

    if ($staticAuditResult.ExitCode -ne 0 -or $staticErrors -ne '0') {
        $incompleteReasons.Add('Static audit has errors or failed to execute.')
    }

    $secondaryWarnings = Get-RegexValue $completionSummary 'SecondaryAssemblyCSharpWarnings=(\d+)' 'unknown'
    if ($secondaryWarnings -ne '0') {
        $incompleteReasons.Add("Secondary Assembly-CSharp serialized residual warnings remain: $secondaryWarnings.")
    }

    $missingAssetReferences = Get-RegexValue $completionSummary 'MissingAssetReferenceErrors=(\d+)' 'unknown'
    if ($missingAssetReferences -ne '0') {
        $incompleteReasons.Add("Serialized asset references point to missing GUIDs: $missingAssetReferences.")
    }

    Add-ReportLine $reportLines ''
}

$linkXmlResult = $null
if (-not $SkipLinkXmlDryRun) {
    $linkXmlScript = Join-Path $ProjectRoot 'Tools\Validation\Invoke-AddressablesLinkXmlMigrationReport.ps1'
    $linkXmlResult = Invoke-PowerShellScriptCapture `
        -ScriptPath $linkXmlScript `
        -Arguments @('-ProjectRoot', $ProjectRoot) `
        -LogPath $linkXmlLogPath

    $linkXmlRestoreScript = Join-Path $ProjectRoot 'Tools\Validation\Invoke-AddressablesLinkXmlRestore.ps1'
    $linkXmlRestoreResult = Invoke-PowerShellScriptCapture `
        -ScriptPath $linkXmlRestoreScript `
        -Arguments @('-ProjectRoot', $ProjectRoot) `
        -LogPath $linkXmlRestoreLogPath

    $legacyEntries = Get-RegexValue $linkXmlResult.Output 'LegacyEntries=(\d+)' 'unknown'
    $migratedProject = Get-RegexValue $linkXmlResult.Output 'MigratedProject=(\d+)' 'unknown'
    $preservedExternal = Get-RegexValue $linkXmlResult.Output 'PreservedExternal=(\d+)' 'unknown'
    $unresolvedProject = Get-RegexValue $linkXmlResult.Output 'UnresolvedProject=(\d+)' 'unknown'
    $proposalEntries = Get-RegexValue $linkXmlResult.Output 'ProposalEntries=(\d+)' 'unknown'
    $proposalAssemblyCSharp = Get-RegexValue $linkXmlResult.Output 'ProposalAssemblyCSharpReferences=(\d+)' 'unknown'
    $proposalMetaGuid = Get-RegexValue $linkXmlResult.Output 'ProposalMetaGuid=([0-9a-fA-F]{32})' 'unknown'
    $restoreTargetExists = Get-RegexValue $linkXmlRestoreResult.Output 'TargetExists=(True|False)' 'unknown'
    $restoreTargetMetaExists = Get-RegexValue $linkXmlRestoreResult.Output 'TargetMetaExists=(True|False)' 'unknown'
    $restoreTargetMatchesProposal = Get-RegexValue $linkXmlRestoreResult.Output 'TargetMatchesProposal=(True|False)' 'unknown'

    Add-ReportLine $reportLines 'Addressables link.xml dry-run:'
    Add-ReportLine $reportLines "- ExitCode: $($linkXmlResult.ExitCode)"
    Add-ReportLine $reportLines "- LegacyEntries: $legacyEntries"
    Add-ReportLine $reportLines "- MigratedProject: $migratedProject"
    Add-ReportLine $reportLines "- PreservedExternal: $preservedExternal"
    Add-ReportLine $reportLines "- UnresolvedProject: $unresolvedProject"
    Add-ReportLine $reportLines "- ProposalEntries: $proposalEntries"
    Add-ReportLine $reportLines "- ProposalAssemblyCSharpReferences: $proposalAssemblyCSharp"
    Add-ReportLine $reportLines "- ProposalMetaGuid: $proposalMetaGuid"
    Add-ReportLine $reportLines "- Log: $linkXmlLogPath"
    Add-ReportLine $reportLines "- Proposal: $(Join-Path $tempRoot 'AddressablesLinkXmlMigrationProposal.xml')"
    Add-ReportLine $reportLines "- ProposalMeta: $(Join-Path $tempRoot 'AddressablesLinkXmlMigrationProposal.xml.meta')"
    Add-ReportLine $reportLines "- RestoreValidationExitCode: $($linkXmlRestoreResult.ExitCode)"
    Add-ReportLine $reportLines "- RestoreValidationTargetExists: $restoreTargetExists"
    Add-ReportLine $reportLines "- RestoreValidationTargetMetaExists: $restoreTargetMetaExists"
    Add-ReportLine $reportLines "- RestoreValidationTargetMatchesProposal: $restoreTargetMatchesProposal"
    Add-ReportLine $reportLines "- RestoreValidationTargetRole: temporary player-build copy; Addressables deletes this path on editor load"
    Add-ReportLine $reportLines "- RestoreValidationLog: $linkXmlRestoreLogPath"

    if ($linkXmlResult.ExitCode -ne 0 -or $linkXmlRestoreResult.ExitCode -ne 0 -or $unresolvedProject -ne '0' -or $proposalAssemblyCSharp -ne '0' -or $proposalMetaGuid -eq 'unknown') {
        $incompleteReasons.Add('Addressables link.xml migration proposal is not clean.')
    }

    Add-ReportLine $reportLines ''
}

$unityLogPath = Join-Path $tempRoot 'AssemblySplitUnityValidation.log'
if ($unityValidationRefreshResult -ne $null -and -not [string]::IsNullOrWhiteSpace($unityValidationRefreshResult.ResultLogPath)) {
    $unityLogPath = $unityValidationRefreshResult.ResultLogPath
}
Add-ReportLine $reportLines 'Unity validation result log:'
if (Test-Path -LiteralPath $unityLogPath) {
    $unityLogText = Get-Content -LiteralPath $unityLogPath -Raw
    $unitySummary = Get-RegexValue $unityLogText '(Assembly split Editor validation summary:[^\r\n]+)' 'not found'
    $unityErrors = Get-RegexValue $unitySummary 'Errors=(\d+)' 'unknown'
    $unityWarnings = Get-RegexValue $unitySummary 'Warnings=(\d+)' 'unknown'
    $unityInfos = Get-RegexValue $unitySummary 'Infos=(\d+)' 'unknown'

    Add-ReportLine $reportLines "- Summary: $unitySummary"
    Add-ReportLine $reportLines "- Errors: $unityErrors"
    Add-ReportLine $reportLines "- Warnings: $unityWarnings"
    Add-ReportLine $reportLines "- Infos: $unityInfos"
    Add-ReportLine $reportLines "- Log: $unityLogPath"

    if ($unityErrors -ne '0') {
        $incompleteReasons.Add("Latest Unity validation log has errors: $unityErrors.")
    }

    if ($unityWarnings -ne '0') {
        $incompleteReasons.Add("Latest strict Unity validation log still has warnings: $unityWarnings.")
    }
} else {
    Add-ReportLine $reportLines '- Missing log.'
    $incompleteReasons.Add('Unity validation log is missing.')
}

Add-ReportLine $reportLines ''

Add-ReportLine $reportLines 'MSBuild generated solution build:'
if ($RunMSBuild.IsPresent) {
    $msbuildResult = Invoke-GeneratedSolutionMSBuild
    Add-ReportLine $reportLines "- Mode: executed"
    Add-ReportLine $reportLines "- ExitCode: $($msbuildResult.ExitCode)"
    Add-ReportLine $reportLines "- Solutions: $($msbuildResult.SolutionCount)"
    Add-ReportLine $reportLines "- RemovedTempFiles: $($msbuildResult.RemovedTempFiles)"
    Add-ReportLine $reportLines "- Log: $($msbuildResult.LogPath)"

    if ($msbuildResult.ExitCode -ne 0) {
        $incompleteReasons.Add("Fresh MSBuild generated solution build failed with exit code $($msbuildResult.ExitCode).")
    }
} else {
    $latestMsbuildLog = Get-ChildItem -LiteralPath $tempRoot -Filter 'assembly_split_msbuild*.out.log' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    Add-ReportLine $reportLines "- Mode: latest-log-only"
    if ($latestMsbuildLog) {
        Add-ReportLine $reportLines "- Log: $($latestMsbuildLog.FullName)"
        Add-ReportLine $reportLines "- LastWriteTime: $($latestMsbuildLog.LastWriteTime)"
    } else {
        Add-ReportLine $reportLines '- Log: missing'
    }

    $incompleteReasons.Add('Fresh MSBuild generated solution build was not run by this report.')
}

Add-ReportLine $reportLines ''
Add-ReportLine $reportLines 'Current conclusion:'
if ($incompleteReasons.Count -eq 0) {
    Add-ReportLine $reportLines '- No incomplete reasons were detected by this report.'
} else {
    foreach ($reason in $incompleteReasons) {
        Add-ReportLine $reportLines "- $reason"
    }
}

Set-Content -LiteralPath $reportPath -Value $reportLines -Encoding UTF8

Write-Host "Assembly split completion report written: $reportPath"
Get-Content -LiteralPath $reportPath

if ($FailOnIncomplete -and $incompleteReasons.Count -gt 0) {
    exit 2
}
