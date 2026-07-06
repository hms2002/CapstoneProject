[CmdletBinding()]
param(
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..')).Path
}

$targetAssemblies = @('Core', 'Gameplay', 'Infrastructure', 'Presentation', 'UI', 'Editor')

function Get-RelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetFullPath($script:ProjectRoot).TrimEnd('\', '/')
    if ($fullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($root.Length + 1)
    }

    return $fullPath
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

    throw 'MSBuild.exe could not be resolved.'
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

function Assert-SafeProbeRoot {
    param([string]$ProbeRoot)

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($script:ProjectRoot).TrimEnd('\', '/')
    $resolvedTempRoot = [System.IO.Path]::GetFullPath((Join-Path $script:ProjectRoot 'Temp')).TrimEnd('\', '/')
    $resolvedProbeRoot = [System.IO.Path]::GetFullPath($ProbeRoot).TrimEnd('\', '/')

    if (-not $resolvedTempRoot.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected Temp root outside project root: $resolvedTempRoot"
    }

    if (-not $resolvedProbeRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected probe root outside Temp: $resolvedProbeRoot"
    }
}

function ConvertTo-AbsoluteProjectPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or [System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $script:ProjectRoot $Path))
}

function Get-ProjectAsmdefs {
    $asmdefsByName = @{}
    $root = Join-Path $script:ProjectRoot 'Assets\_Project'
    if (-not (Test-Path -LiteralPath $root)) {
        return $asmdefsByName
    }

    Get-ChildItem -LiteralPath $root -Recurse -Filter '*.asmdef' -File | ForEach-Object {
        $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        $references = @()
        if ($null -ne $json.references) {
            $references = @($json.references)
        }

        $asmdefsByName[$json.name] = [pscustomobject]@{
            Name = $json.name
            Path = $_.FullName
            References = $references
        }
    }

    return $asmdefsByName
}

function Test-ProjectHasReference {
    param(
        [xml]$ProjectXml,
        [string]$ReferenceName
    )

    foreach ($reference in @($ProjectXml.Project.ItemGroup.Reference)) {
        if ($null -eq $reference) {
            continue
        }

        $include = [string]$reference.Include
        if ($include -eq $ReferenceName -or $include.StartsWith("$ReferenceName,", [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    foreach ($projectReference in @($ProjectXml.Project.ItemGroup.ProjectReference)) {
        if ($null -eq $projectReference) {
            continue
        }

        $include = [string]$projectReference.Include
        if ([System.IO.Path]::GetFileName($include) -eq "$ReferenceName.csproj") {
            return $true
        }
    }

    return $false
}

function Add-ReferenceItem {
    param(
        [xml]$ProjectXml,
        [string]$ReferenceName,
        [string]$HintPath
    )

    $itemGroup = $ProjectXml.CreateElement('ItemGroup')
    $reference = $ProjectXml.CreateElement('Reference')
    $reference.SetAttribute('Include', $ReferenceName)

    $hint = $ProjectXml.CreateElement('HintPath')
    $hint.InnerText = $HintPath
    [void]$reference.AppendChild($hint)

    $private = $ProjectXml.CreateElement('Private')
    $private.InnerText = 'False'
    [void]$reference.AppendChild($private)

    [void]$itemGroup.AppendChild($reference)
    [void]$ProjectXml.Project.AppendChild($itemGroup)
}

function Add-ProjectReferenceItem {
    param(
        [xml]$ProjectXml,
        [string]$ReferenceName
    )

    $itemGroup = $ProjectXml.CreateElement('ItemGroup')
    $projectReference = $ProjectXml.CreateElement('ProjectReference')
    $projectReference.SetAttribute('Include', "$ReferenceName.csproj")
    [void]$itemGroup.AppendChild($projectReference)
    [void]$ProjectXml.Project.AppendChild($itemGroup)
}

function Convert-ProjectPathsToProbeSafePaths {
    param(
        [xml]$ProjectXml,
        [string]$ProbeRoot
    )

    foreach ($node in @($ProjectXml.SelectNodes('/Project/PropertyGroup/BaseIntermediateOutputPath'))) {
        $node.InnerText = Join-Path $ProbeRoot 'obj\$(MSBuildProjectName)'
    }

    foreach ($node in @($ProjectXml.SelectNodes('/Project/PropertyGroup/OutputPath'))) {
        $node.InnerText = Join-Path $ProbeRoot 'bin\Debug'
    }

    foreach ($item in @($ProjectXml.SelectNodes('/Project/ItemGroup/Compile[@Include] | /Project/ItemGroup/None[@Include] | /Project/ItemGroup/Analyzer[@Include]'))) {
        $item.SetAttribute('Include', (ConvertTo-AbsoluteProjectPath -Path ([string]$item.GetAttribute('Include'))))
    }

    foreach ($hintPath in @($ProjectXml.SelectNodes('/Project/ItemGroup/Reference/HintPath'))) {
        $hintPath.InnerText = ConvertTo-AbsoluteProjectPath -Path ([string]$hintPath.InnerText)
    }
}

function Test-IsUnderNestedAssemblyBoundary {
    param(
        [string]$SourcePath,
        [string]$AsmdefDirectory
    )

    $root = [System.IO.Path]::GetFullPath($AsmdefDirectory).TrimEnd('\', '/')
    $directory = [System.IO.DirectoryInfo]::new([System.IO.Path]::GetDirectoryName($SourcePath))
    while ($null -ne $directory) {
        $current = $directory.FullName.TrimEnd('\', '/')
        if ($current.Equals($root, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }

        if ($current.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase) -and
            ((Get-ChildItem -LiteralPath $current -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | Select-Object -First 1) -or
             (Get-ChildItem -LiteralPath $current -Filter '*.asmref' -File -ErrorAction SilentlyContinue | Select-Object -First 1))) {
            return $true
        }

        $directory = $directory.Parent
    }

    return $false
}

function Add-MissingCompileItemsFromAsmdef {
    param(
        [xml]$ProjectXml,
        [object]$Asmdef
    )

    $asmdefDirectory = [System.IO.Path]::GetDirectoryName($Asmdef.Path)
    if ([string]::IsNullOrWhiteSpace($asmdefDirectory) -or -not (Test-Path -LiteralPath $asmdefDirectory)) {
        return
    }

    $existingCompilePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($compileItem in @($ProjectXml.SelectNodes('/Project/ItemGroup/Compile[@Include]'))) {
        $include = [string]$compileItem.GetAttribute('Include')
        if (-not [string]::IsNullOrWhiteSpace($include)) {
            [void]$existingCompilePaths.Add([System.IO.Path]::GetFullPath($include))
        }
    }

    $missingSources = New-Object System.Collections.Generic.List[string]
    foreach ($sourceFile in Get-ChildItem -LiteralPath $asmdefDirectory -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue) {
        if (Test-IsUnderNestedAssemblyBoundary -SourcePath $sourceFile.FullName -AsmdefDirectory $asmdefDirectory) {
            continue
        }

        $fullSourcePath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
        if ($existingCompilePaths.Contains($fullSourcePath)) {
            continue
        }

        $missingSources.Add($fullSourcePath)
    }

    if ($missingSources.Count -eq 0) {
        return
    }

    $itemGroup = $ProjectXml.CreateElement('ItemGroup')
    foreach ($sourcePath in $missingSources) {
        $compile = $ProjectXml.CreateElement('Compile')
        $compile.SetAttribute('Include', $sourcePath)
        [void]$itemGroup.AppendChild($compile)
    }

    [void]$ProjectXml.Project.AppendChild($itemGroup)
    Write-Host "Probe added Compile items: $($Asmdef.Name) -> $($missingSources.Count)"
}

function Remove-StaleCompileItemsFromProbeProject {
    param(
        [xml]$ProjectXml,
        [string]$AssemblyName
    )

    $removedCount = 0
    $projectRoot = [System.IO.Path]::GetFullPath($script:ProjectRoot).TrimEnd('\', '/')
    foreach ($compileItem in @($ProjectXml.SelectNodes('/Project/ItemGroup/Compile[@Include]'))) {
        $include = [string]$compileItem.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        $fullPath = [System.IO.Path]::GetFullPath($include)
        if (-not $fullPath.StartsWith($projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if (Test-Path -LiteralPath $fullPath) {
            continue
        }

        [void]$compileItem.ParentNode.RemoveChild($compileItem)
        $removedCount++
    }

    if ($removedCount -gt 0) {
        Write-Host "Probe removed stale Compile items: $AssemblyName -> $removedCount"
    }
}

function Patch-AssemblyReferencesFromAsmdef {
    param(
        [xml]$ProjectXml,
        [object]$Asmdef,
        [string]$ProbeRoot
    )

    foreach ($reference in @($Asmdef.References)) {
        if ([string]::IsNullOrWhiteSpace($reference) -or $reference.StartsWith('GUID:', [System.StringComparison]::Ordinal)) {
            continue
        }

        if (Test-ProjectHasReference -ProjectXml $ProjectXml -ReferenceName $reference) {
            continue
        }

        $rootProjectPath = Join-Path $script:ProjectRoot "$reference.csproj"
        if (Test-Path -LiteralPath $rootProjectPath) {
            Add-ProjectReferenceItem -ProjectXml $ProjectXml -ReferenceName $reference
            Write-Host "Probe added ProjectReference: $($Asmdef.Name) -> $reference"
            continue
        }

        $assemblyPath = Join-Path $script:ProjectRoot "Library\ScriptAssemblies\$reference.dll"
        if (Test-Path -LiteralPath $assemblyPath) {
            Add-ReferenceItem -ProjectXml $ProjectXml -ReferenceName $reference -HintPath $assemblyPath
            Write-Host "Probe added Reference: $($Asmdef.Name) -> $reference"
            continue
        }

        Write-Warning "Probe could not resolve asmdef reference for $($Asmdef.Name): $reference"
    }
}

function Copy-And-PatchGeneratedProjects {
    param(
        [string]$ProbeRoot,
        [hashtable]$AsmdefsByName
    )

    $solution = Get-ChildItem -LiteralPath $script:ProjectRoot -Filter '*.slnx' -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $solution) {
        throw 'Generated .slnx file is missing.'
    }

    Copy-Item -LiteralPath $solution.FullName -Destination (Join-Path $ProbeRoot $solution.Name) -Force
    $projectPaths = Select-String -LiteralPath $solution.FullName -Pattern 'Path="([^"]+\.csproj)"' -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique

    foreach ($projectPath in $projectPaths) {
        $sourceProjectPath = Join-Path $script:ProjectRoot $projectPath
        if (-not (Test-Path -LiteralPath $sourceProjectPath)) {
            throw "Generated project listed in solution is missing: $projectPath"
        }

        $destinationProjectPath = Join-Path $ProbeRoot ([System.IO.Path]::GetFileName($projectPath))
        Copy-Item -LiteralPath $sourceProjectPath -Destination $destinationProjectPath -Force

        [xml]$projectXml = Get-Content -LiteralPath $destinationProjectPath -Raw
        Convert-ProjectPathsToProbeSafePaths -ProjectXml $projectXml -ProbeRoot $ProbeRoot

        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        if ($AsmdefsByName.ContainsKey($assemblyName)) {
            Remove-StaleCompileItemsFromProbeProject -ProjectXml $projectXml -AssemblyName $assemblyName
            Add-MissingCompileItemsFromAsmdef -ProjectXml $projectXml -Asmdef $AsmdefsByName[$assemblyName]
            Patch-AssemblyReferencesFromAsmdef -ProjectXml $projectXml -Asmdef $AsmdefsByName[$assemblyName] -ProbeRoot $ProbeRoot
        }

        $projectXml.Save($destinationProjectPath)
    }

    return Join-Path $ProbeRoot $solution.Name
}

function Copy-RestoreArtifacts {
    param(
        [string]$ProbeRoot
    )

    $sourceObjRoot = Join-Path $script:ProjectRoot 'Temp\obj'
    if (-not (Test-Path -LiteralPath $sourceObjRoot)) {
        throw 'Temp\obj is missing. Run a generated project restore once before using the offline compile probe.'
    }

    $projectFiles = Get-ChildItem -LiteralPath $ProbeRoot -Filter '*.csproj' -File
    foreach ($projectFile in $projectFiles) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile.Name)
        $sourceProjectObjRoot = Join-Path $sourceObjRoot $projectName
        $destinationProjectObjRoot = Join-Path $ProbeRoot "obj\$projectName"

        if (-not (Test-Path -LiteralPath (Join-Path $sourceProjectObjRoot 'project.assets.json'))) {
            throw "Restore artifact is missing for $projectName. Expected: $(Get-RelativePath (Join-Path $sourceProjectObjRoot 'project.assets.json'))"
        }

        New-Item -ItemType Directory -Path $destinationProjectObjRoot -Force | Out-Null
        foreach ($restoreArtifact in @(
            'project.assets.json',
            "$projectName.csproj.nuget.g.props",
            "$projectName.csproj.nuget.g.targets"
        )) {
            $sourceArtifact = Join-Path $sourceProjectObjRoot $restoreArtifact
            if (Test-Path -LiteralPath $sourceArtifact) {
                Copy-Item -LiteralPath $sourceArtifact -Destination (Join-Path $destinationProjectObjRoot $restoreArtifact) -Force
            }
        }
    }
}

$script:ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$probeRoot = Join-Path $script:ProjectRoot ("Temp\AssemblySplitOfflineCompileProbe\" + (Get-Date -Format 'yyyyMMdd_HHmmss_ffff'))
Assert-SafeProbeRoot -ProbeRoot $probeRoot

New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null

$asmdefsByName = Get-ProjectAsmdefs
$probeSolutionPath = Copy-And-PatchGeneratedProjects -ProbeRoot $probeRoot -AsmdefsByName $asmdefsByName
Copy-RestoreArtifacts -ProbeRoot $probeRoot
$msbuildPath = Resolve-MSBuildPath

Write-Host "Running offline compile probe against copied generated projects..."
Write-Host "  ProbeRoot: $(Get-RelativePath $probeRoot)"
Write-Host "  Solution:  $(Get-RelativePath $probeSolutionPath)"

$buildExitCode = Invoke-MSBuildWithNormalizedPath -MSBuildPath $msbuildPath -Arguments @($probeSolutionPath, '/t:Build', '/m:1', '/p:Restore=false', '/v:minimal')
if ($buildExitCode -ne 0) {
    throw "Assembly split offline compile probe build failed with exit code $buildExitCode."
}

Write-Host 'Assembly split offline compile probe completed successfully.'
