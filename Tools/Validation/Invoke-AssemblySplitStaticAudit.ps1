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
$targetAssemblySourceRoots = @{
    Core = 'Assets/_Project/Runtime/Core'
    Gameplay = 'Assets/_Project/Runtime/Features'
    Infrastructure = 'Assets/_Project/Runtime/Infrastructure'
    Presentation = 'Assets/_Project/Runtime/Presentation'
    UI = 'Assets/_Project/Runtime/UI'
    Editor = 'Assets/_Project/Editor'
}
$testOnlyProjectSourceRoots = @(
    'Assets/_Project/Tests'
)
$allowedAssetSourceAssemblies = @(
    'Core',
    'Gameplay',
    'Infrastructure',
    'Presentation',
    'UI',
    'Editor',
    'PlayModeTests',
    'DOTween.Modules',
    'DOTweenPro.Scripts',
    'DOTweenPro.Scripts.Editor',
    'Ink-Libraries',
    'InkEditor',
    'Ink.Demos.Basic',
    'Ink.Demos.Basic.Editor'
)
$expectedTargetAsmdefPaths = @{
    Core = 'Assets/_Project/Runtime/Core/Core.asmdef'
    Gameplay = 'Assets/_Project/Runtime/Features/Gameplay.asmdef'
    Infrastructure = 'Assets/_Project/Runtime/Infrastructure/Infrastructure.asmdef'
    Presentation = 'Assets/_Project/Runtime/Presentation/Presentation.asmdef'
    UI = 'Assets/_Project/Runtime/UI/UI.asmdef'
    Editor = 'Assets/_Project/Editor/Editor.asmdef'
}
$expectedSupportAsmdefPaths = @{
    PlayModeTests = 'Assets/_Project/Tests/PlayMode/PlayModeTests.asmdef'
    'DOTween.Modules' = 'Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef'
    'DOTweenPro.Scripts' = 'Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.Scripts.asmdef'
    'DOTweenPro.Scripts.Editor' = 'Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenPro.Scripts.Editor.asmdef'
    'Ink-Libraries' = 'Assets/Ink/InkLibs/Ink-Libraries.asmdef'
    InkEditor = 'Assets/Ink/Editor/InkEditor.asmdef'
    'Ink.Demos.Basic' = 'Assets/Ink/Demos/Basic Demo/Scripts/Ink.Demos.Basic.asmdef'
    'Ink.Demos.Basic.Editor' = 'Assets/Ink/Demos/Basic Demo/Scripts/Editor/Ink.Demos.Basic.Editor.asmdef'
}
$allowedProjectReferences = @{
    Core = @()
    Gameplay = @('Core')
    Infrastructure = @('Core', 'Gameplay')
    Presentation = @('Core', 'Gameplay', 'Infrastructure')
    UI = @('Core', 'Gameplay', 'Infrastructure', 'Presentation')
    Editor = @('Core', 'Gameplay', 'Infrastructure', 'Presentation', 'UI')
}

$runtimeAssemblies = @('Core', 'Gameplay', 'Infrastructure', 'Presentation', 'UI')

$safeUnityEventTargets = @(
    'UpgradeTreeUI, Assembly-CSharp',
    'UnlockResultUI, Assembly-CSharp',
    'TutorialSceneSequenceDirector, Assembly-CSharp',
    'TutorialPlayerAutoMove, Assembly-CSharp',
    'TutorialInfoTrigger, Assembly-CSharp',
    'TutorialCombatIntroSequence, Assembly-CSharp'
)

$safeScriptGuidReplacements = @{
    '2ac5f84fdf6c49fdb88721db1b68ef98' = '4a4e8e4b6b0b77a45a9ed3732ce9ad4f'
    '6f5a90f75efdf6745b16ec72c1d92a8c' = 'af77d418566312547b4c270f72388509'
}

$knownPackageMissingScriptGuids = @{
    '65bae8b9f1bd244b3a27e92af4b23b2a' = 'Unity.VisualScripting DictionaryAsset'
    '95e66c6366d904e98bc83428217d4fd7' = 'Unity.VisualScripting ScriptGraphAsset'
    '765181c9ef4b24d32a4f7cbd2ef370dc' = 'Unity.VisualScripting SceneVariables'
    'e741851cba3ad425c91ecf922cc6b379' = 'Unity.VisualScripting Variables'
}

$findings = New-Object System.Collections.Generic.List[object]

function Add-Finding {
    param(
        [ValidateSet('Info', 'Warning', 'Error')]
        [string]$Severity,
        [string]$Category,
        [string]$Path,
        [string]$Message
    )

    $script:findings.Add([pscustomobject]@{
        Severity = $Severity
        Category = $Category
        Path = $Path
        Message = $Message
    })
}

function Get-RelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetFullPath($script:ProjectRoot).TrimEnd('\', '/')
    if ($fullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($root.Length + 1)
    }

    return $fullPath
}

function ConvertTo-ProjectSlashPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }

    return $Path.Replace('\', '/')
}

function Get-MetaGuid {
    param([string]$MetaPath)

    $match = Select-String -LiteralPath $MetaPath -Pattern '^guid: ([0-9a-f]{32})' -List -ErrorAction SilentlyContinue
    if ($match -and $match.Line -match '^guid: ([0-9a-f]{32})') {
        return $Matches[1]
    }

    return $null
}

function Resolve-GitExecutable {
    if ($null -ne $script:cachedGitExecutable) {
        return $script:cachedGitExecutable
    }

    $candidates = @(
        'git',
        'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe',
        'C:\Program Files\Git\cmd\git.exe'
    )

    foreach ($candidate in $candidates) {
        try {
            $null = & $candidate --version 2>$null
            if ($LASTEXITCODE -eq 0) {
                $script:cachedGitExecutable = $candidate
                return $script:cachedGitExecutable
            }
        } catch {
        }
    }

    $script:cachedGitExecutable = ''
    return $null
}

function Get-HeadFileText {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $null
    }

    if ($null -eq $script:cachedHeadFileTextByPath) {
        $script:cachedHeadFileTextByPath = @{}
    }

    $projectPath = ConvertTo-ProjectSlashPath $RelativePath
    if ($script:cachedHeadFileTextByPath.ContainsKey($projectPath)) {
        return $script:cachedHeadFileTextByPath[$projectPath]
    }

    $git = Resolve-GitExecutable
    if ([string]::IsNullOrWhiteSpace($git)) {
        $script:cachedHeadFileTextByPath[$projectPath] = $null
        return $null
    }

    $headPath = "HEAD:$projectPath"
    $output = & $git -C $script:ProjectRoot show $headPath 2>$null
    if ($LASTEXITCODE -ne 0) {
        $script:cachedHeadFileTextByPath[$projectPath] = ''
        return ''
    }

    $text = $output -join "`n"
    $script:cachedHeadFileTextByPath[$projectPath] = $text
    return $text
}

function Test-SerializedAssetReferenceExistedInHead {
    param(
        [string]$RelativePath,
        [string]$Guid
    )

    $headText = Get-HeadFileText -RelativePath $RelativePath
    if ($null -eq $headText -or [string]::IsNullOrEmpty($headText)) {
        return $false
    }

    return $headText.Contains("guid: $Guid")
}

function Get-KnownAsmdefGuidNameMap {
    if ($null -ne $script:cachedAsmdefGuidNameMap) {
        return $script:cachedAsmdefGuidNameMap
    }

    $guidNames = @{}
    foreach ($root in @('Assets', 'Packages', 'Library\PackageCache')) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if ([string]::IsNullOrWhiteSpace($json.name)) {
                    return
                }

                $metaPath = "$($_.FullName).meta"
                if (-not (Test-Path -LiteralPath $metaPath)) {
                    return
                }

                $guid = Get-MetaGuid -MetaPath $metaPath
                if (-not [string]::IsNullOrWhiteSpace($guid)) {
                    $guidNames[$guid] = [string]$json.name
                }
            } catch {
                Add-Finding Error 'AsmdefReference' (Get-RelativePath $_.FullName) "Failed to parse asmdef while building GUID reference map: $($_.Exception.Message)"
            }
        }
    }

    $script:cachedAsmdefGuidNameMap = $guidNames
    return $guidNames
}

function Resolve-AsmdefReferenceName {
    param([string]$Reference)

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        return $Reference
    }

    if ($Reference -match '^GUID:([0-9a-f]{32})$') {
        $guidNames = Get-KnownAsmdefGuidNameMap
        $guid = $Matches[1]
        if ($guidNames.ContainsKey($guid)) {
            return $guidNames[$guid]
        }
    }

    return $Reference
}

function Get-MetaGuidMap {
    if ($null -ne $script:cachedMetaGuidMap) {
        return $script:cachedMetaGuidMap
    }

    $guidMap = @{}
    foreach ($root in @('Assets', 'Packages', 'Library\PackageCache')) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.meta' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $guid = Get-MetaGuid -MetaPath $_.FullName
            if (-not [string]::IsNullOrWhiteSpace($guid)) {
                $guidMap[$guid] = $_.FullName
            }
        }
    }

    $script:cachedMetaGuidMap = $guidMap
    return $guidMap
}

function Get-ProjectAsmdefs {
    $asmdefsByName = @{}
    $root = Join-Path $script:ProjectRoot 'Assets\_Project'
    if (-not (Test-Path -LiteralPath $root)) {
        Add-Finding Error 'Asmdef' 'Assets/_Project' 'Project source root is missing.'
        return $asmdefsByName
    }

    Get-ChildItem -LiteralPath $root -Recurse -Filter '*.asmdef' -File | ForEach-Object {
        $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        $references = @()
        if ($null -ne $json.references) {
            $references = @($json.references | ForEach-Object { Resolve-AsmdefReferenceName -Reference ([string]$_) })
        }

        $rootNamespace = ''
        if ($null -ne $json.rootNamespace) {
            $rootNamespace = [string]$json.rootNamespace
        }

        $includePlatforms = @()
        if ($null -ne $json.includePlatforms) {
            $includePlatforms = @($json.includePlatforms)
        }

        $excludePlatforms = @()
        if ($null -ne $json.excludePlatforms) {
            $excludePlatforms = @($json.excludePlatforms)
        }

        $optionalUnityReferences = @()
        if ($null -ne $json.optionalUnityReferences) {
            $optionalUnityReferences = @($json.optionalUnityReferences)
        }

        $precompiledReferences = @()
        if ($null -ne $json.precompiledReferences) {
            $precompiledReferences = @($json.precompiledReferences)
        }

        $defineConstraints = @()
        if ($null -ne $json.defineConstraints) {
            $defineConstraints = @($json.defineConstraints)
        }

        $versionDefines = @()
        if ($null -ne $json.versionDefines) {
            $versionDefines = @($json.versionDefines)
        }

        $overrideReferences = $false
        if ($null -ne $json.overrideReferences) {
            $overrideReferences = [bool]$json.overrideReferences
        }

        $allowUnsafeCode = $false
        if ($null -ne $json.allowUnsafeCode) {
            $allowUnsafeCode = [bool]$json.allowUnsafeCode
        }

        $autoReferenced = $true
        if ($null -ne $json.autoReferenced) {
            $autoReferenced = [bool]$json.autoReferenced
        }

        $noEngineReferences = $false
        if ($null -ne $json.noEngineReferences) {
            $noEngineReferences = [bool]$json.noEngineReferences
        }

        $asmdefsByName[$json.name] = [pscustomobject]@{
            Name = $json.name
            Path = Get-RelativePath $_.FullName
            RootNamespace = $rootNamespace
            References = $references
            IncludePlatforms = $includePlatforms
            ExcludePlatforms = $excludePlatforms
            OptionalUnityReferences = $optionalUnityReferences
            PrecompiledReferences = $precompiledReferences
            DefineConstraints = $defineConstraints
            VersionDefines = $versionDefines
            OverrideReferences = $overrideReferences
            AllowUnsafeCode = $allowUnsafeCode
            AutoReferenced = $autoReferenced
            NoEngineReferences = $noEngineReferences
        }
    }

    return $asmdefsByName
}

function Test-AsmdefGraph {
    $asmdefsByName = Get-ProjectAsmdefs
    $targetSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$targetAssemblies)
    $missingTargetCount = 0
    $wrongTargetPathCount = 0

    foreach ($assemblyName in $targetAssemblies) {
        if (-not $asmdefsByName.ContainsKey($assemblyName)) {
            $missingTargetCount++
            Add-Finding Error 'Asmdef' '' "Target assembly is missing: $assemblyName"
            continue
        }

        $asmdefPath = ConvertTo-ProjectSlashPath $asmdefsByName[$assemblyName].Path
        $expectedPath = $expectedTargetAsmdefPaths[$assemblyName]
        if ($asmdefPath -ne $expectedPath) {
            $wrongTargetPathCount++
            Add-Finding Error 'AsmdefPath' $asmdefsByName[$assemblyName].Path "Target assembly asmdef is in the wrong path: $assemblyName. Expected=$expectedPath"
        } else {
            Add-Finding Info 'AsmdefPath' $asmdefsByName[$assemblyName].Path "Target assembly asmdef path is valid: $assemblyName"
        }

        Add-Finding Info 'Asmdef' $asmdefsByName[$assemblyName].Path "Target assembly found: $assemblyName"
    }

    $unexpectedProjectAsmdefCount = 0
    $testExceptionCount = 0
    foreach ($asmdef in $asmdefsByName.Values | Sort-Object Name) {
        if ($targetSet.Contains($asmdef.Name)) {
            continue
        }

        if ($asmdef.Name -eq 'PlayModeTests') {
            $testExceptionCount++
            Add-Finding Info 'Asmdef' $asmdef.Path 'Test-only asmdef exists outside the six production assemblies by Unity Test Runner design.'
        } else {
            $unexpectedProjectAsmdefCount++
            Add-Finding Error 'Asmdef' $asmdef.Path "Unexpected project asmdef outside target assemblies: $($asmdef.Name)"
        }
    }

    if ($missingTargetCount -eq 0 -and $wrongTargetPathCount -eq 0 -and $unexpectedProjectAsmdefCount -eq 0) {
        Add-Finding Info 'ProjectAssemblySet' 'Assets/_Project' "Project-owned production asmdef set is exactly the six target assemblies. Production=$([string]::Join(', ', $targetAssemblies)); TestExceptions=PlayModeTests:$testExceptionCount; ProjectAsmdefs=$($asmdefsByName.Count)"
    }

    $invalidProjectReferenceCount = 0
    foreach ($assemblyName in $targetAssemblies) {
        if (-not $asmdefsByName.ContainsKey($assemblyName)) {
            continue
        }

        $allowed = [System.Collections.Generic.HashSet[string]]::new([string[]]$allowedProjectReferences[$assemblyName])
        foreach ($reference in $asmdefsByName[$assemblyName].References) {
            if (-not $targetSet.Contains($reference)) {
                continue
            }

            if (-not $allowed.Contains($reference)) {
                $invalidProjectReferenceCount++
                Add-Finding Error 'Asmdef' $asmdefsByName[$assemblyName].Path "Invalid project assembly reference: $assemblyName -> $reference"
            }
        }
    }

    $visiting = [System.Collections.Generic.HashSet[string]]::new()
    $visited = [System.Collections.Generic.HashSet[string]]::new()
    $stack = New-Object System.Collections.Generic.List[string]
    $cycleFindings = New-Object System.Collections.Generic.List[string]

    function Visit-Assembly {
        param([string]$AssemblyName)

        if ($visited.Contains($AssemblyName)) {
            return
        }

        if ($visiting.Contains($AssemblyName)) {
            $cycleFindings.Add($AssemblyName)
            Add-Finding Error 'Asmdef' '' "Project assembly cycle detected at $AssemblyName. Stack=$([string]::Join(' -> ', $stack))"
            return
        }

        if (-not $asmdefsByName.ContainsKey($AssemblyName)) {
            return
        }

        [void]$visiting.Add($AssemblyName)
        $stack.Add($AssemblyName)

        foreach ($reference in $asmdefsByName[$AssemblyName].References) {
            if ($targetSet.Contains($reference)) {
                Visit-Assembly $reference
            }
        }

        $stack.RemoveAt($stack.Count - 1)
        [void]$visiting.Remove($AssemblyName)
        [void]$visited.Add($AssemblyName)
    }

    foreach ($assemblyName in $targetAssemblies) {
        Visit-Assembly $assemblyName
    }

    if ($asmdefsByName.ContainsKey('Core')) {
        $coreReferences = @($asmdefsByName['Core'].References)
        if ($coreReferences.Count -eq 0) {
            Add-Finding Info 'CoreDependency' $asmdefsByName['Core'].Path 'Core target asmdef declares zero assembly references.'
        } else {
            Add-Finding Error 'CoreDependency' $asmdefsByName['Core'].Path "Core target asmdef must not reference any assembly. References=$([string]::Join(', ', $coreReferences))"
        }
    }

    if ($invalidProjectReferenceCount -eq 0 -and $cycleFindings.Count -eq 0) {
        Add-Finding Info 'AsmdefGraph' 'Assets/_Project' "Project target assembly graph follows allowed dependency directions and contains no cycles. Assemblies=$([string]::Join(', ', $targetAssemblies))"
    }
}

function Test-AsmdefNameUniqueness {
    $pathsByName = @{}
    foreach ($root in @('Assets', 'Packages', 'Library\PackageCache')) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if ([string]::IsNullOrWhiteSpace($json.name)) {
                    Add-Finding Error 'AsmdefName' (Get-RelativePath $_.FullName) 'Asmdef has no assembly name.'
                    return
                }

                $name = [string]$json.name
                if (-not $pathsByName.ContainsKey($name)) {
                    $pathsByName[$name] = New-Object System.Collections.Generic.List[string]
                }

                $pathsByName[$name].Add((Get-RelativePath $_.FullName))
            } catch {
                Add-Finding Error 'AsmdefName' (Get-RelativePath $_.FullName) "Failed to parse asmdef while checking name uniqueness: $($_.Exception.Message)"
            }
        }
    }

    $duplicateCount = 0
    foreach ($entry in $pathsByName.GetEnumerator() | Sort-Object Name) {
        if ($entry.Value.Count -le 1) {
            continue
        }

        $duplicateCount++
        Add-Finding Error 'AsmdefName' ([string]$entry.Value[0]) "Duplicate asmdef assembly name found: $($entry.Key). Paths=$([string]::Join(', ', @($entry.Value)))"
    }

    if ($duplicateCount -eq 0) {
        Add-Finding Info 'AsmdefName' 'Assets; Packages; Library/PackageCache' 'All asmdef assembly names are unique across Assets, Packages, and Library/PackageCache.'
    }
}

function Test-AssetAsmdefAllowedPaths {
    $expectedPaths = @{}
    foreach ($entry in $expectedTargetAsmdefPaths.GetEnumerator()) {
        $expectedPaths[$entry.Key] = $entry.Value
    }

    foreach ($entry in $expectedSupportAsmdefPaths.GetEnumerator()) {
        $expectedPaths[$entry.Key] = $entry.Value
    }

    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $assetsRoot)) {
        Add-Finding Error 'AsmdefAssetPath' 'Assets' 'Assets folder is missing.'
        return
    }

    $issueCount = 0
    $asmdefCount = 0
    foreach ($asmdefFile in Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue) {
        $asmdefCount++
        $relativePath = ConvertTo-ProjectSlashPath (Get-RelativePath $asmdefFile.FullName)
        try {
            $json = Get-Content -LiteralPath $asmdefFile.FullName -Raw | ConvertFrom-Json
            $assemblyName = [string]$json.name
        } catch {
            $issueCount++
            Add-Finding Error 'AsmdefAssetPath' (Get-RelativePath $asmdefFile.FullName) "Failed to parse asmdef while checking allowed asset paths: $($_.Exception.Message)"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($assemblyName)) {
            $issueCount++
            Add-Finding Error 'AsmdefAssetPath' (Get-RelativePath $asmdefFile.FullName) 'Asmdef has no assembly name.'
            continue
        }

        if (-not $expectedPaths.ContainsKey($assemblyName)) {
            $issueCount++
            Add-Finding Error 'AsmdefAssetPath' (Get-RelativePath $asmdefFile.FullName) "Assets asmdef is not an approved target/test/support assembly: $assemblyName"
            continue
        }

        $expectedPath = $expectedPaths[$assemblyName]
        if ($relativePath -ne $expectedPath) {
            $issueCount++
            Add-Finding Error 'AsmdefAssetPath' (Get-RelativePath $asmdefFile.FullName) "Approved asmdef is in the wrong path: $assemblyName. Expected=$expectedPath"
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefAssetPath' 'Assets' "All Assets asmdefs are approved target/test/support assemblies in expected paths. Count=$asmdefCount"
    }
}

function Test-TargetSourceRootNestedAssemblyBoundaries {
    $issueCount = 0
    foreach ($assemblyName in $targetAssemblies) {
        $relativeRoot = $targetAssemblySourceRoots[$assemblyName]
        $absoluteRoot = Join-Path $script:ProjectRoot ($relativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            $issueCount++
            Add-Finding Error 'AsmdefNestedBoundary' $relativeRoot "Target assembly source root is missing: $assemblyName"
            continue
        }

        $expectedPath = $expectedTargetAsmdefPaths[$assemblyName]
        $expectedFullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $script:ProjectRoot ($expectedPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))))

        foreach ($boundaryFile in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -ErrorAction SilentlyContinue) {
            if ($boundaryFile.Extension -ne '.asmdef' -and $boundaryFile.Extension -ne '.asmref') {
                continue
            }

            $boundaryFullPath = [System.IO.Path]::GetFullPath($boundaryFile.FullName)
            if ($boundaryFullPath.Equals($expectedFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $issueCount++
            Add-Finding Error 'AsmdefNestedBoundary' (Get-RelativePath $boundaryFile.FullName) "Nested asmdef/asmref found under target source root: $assemblyName"
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefNestedBoundary' 'Assets/_Project' 'No nested asmdef or asmref files were found inside the six target source roots.'
    }
}

function Test-ProjectSourceRootOwnership {
    $projectSourceRoot = Join-Path $script:ProjectRoot 'Assets\_Project'
    if (-not (Test-Path -LiteralPath $projectSourceRoot)) {
        Add-Finding Error 'ProjectSourceRoot' 'Assets/_Project' 'Project source root is missing.'
        return
    }

    $absoluteTargetRoots = @($targetAssemblies | ForEach-Object {
        [System.IO.Path]::GetFullPath(
            (Join-Path $script:ProjectRoot ($targetAssemblySourceRoots[$_].Replace('/', [System.IO.Path]::DirectorySeparatorChar))))
    })

    $absoluteTestRoots = @($testOnlyProjectSourceRoots | ForEach-Object {
        [System.IO.Path]::GetFullPath(
            (Join-Path $script:ProjectRoot ($_.Replace('/', [System.IO.Path]::DirectorySeparatorChar))))
    })

    $issueCount = 0
    $productionSourceCount = 0
    $testSourceCount = 0
    foreach ($sourceFile in Get-ChildItem -LiteralPath $projectSourceRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue) {
        $sourcePath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
        $isTestSource = $false
        foreach ($testRoot in $absoluteTestRoots) {
            if (Test-IsPathUnderDirectory -Path $sourcePath -Directory $testRoot) {
                $isTestSource = $true
                break
            }
        }

        if ($isTestSource) {
            $testSourceCount++
            continue
        }

        $productionSourceCount++
        $isOwnedByTargetRoot = $false
        foreach ($targetRoot in $absoluteTargetRoots) {
            if (Test-IsPathUnderDirectory -Path $sourcePath -Directory $targetRoot) {
                $isOwnedByTargetRoot = $true
                break
            }
        }

        if (-not $isOwnedByTargetRoot) {
            $issueCount++
            Add-Finding Error 'ProjectSourceRoot' (Get-RelativePath $sourceFile.FullName) 'Production C# source under Assets/_Project is outside the six target source roots.'
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'ProjectSourceRoot' 'Assets/_Project' "All production C# source under Assets/_Project is inside the six target source roots. Sources=$productionSourceCount; TestSources=$testSourceCount"
    }
}

function Test-ProjectTestAsmdefPolicy {
    $asmdefsByName = Get-ProjectAsmdefs
    $issueCount = 0

    if (-not $asmdefsByName.ContainsKey('PlayModeTests')) {
        Add-Finding Info 'TestAsmdefPolicy' 'Assets/_Project/Tests' 'No PlayModeTests asmdef is present.'
        return
    }

    $playModeTests = $asmdefsByName['PlayModeTests']
    $expectedPath = $expectedSupportAsmdefPaths['PlayModeTests']
    if ((ConvertTo-ProjectSlashPath $playModeTests.Path) -ne $expectedPath) {
        $issueCount++
        Add-Finding Error 'TestAsmdefPolicy' $playModeTests.Path "PlayModeTests asmdef is outside its expected test path. Expected=$expectedPath"
    }

    $optionalUnityReferences = @($playModeTests.OptionalUnityReferences)
    if ($optionalUnityReferences.Count -ne 1 -or $optionalUnityReferences[0] -ne 'TestAssemblies') {
        $issueCount++
        Add-Finding Error 'TestAsmdefPolicy' $playModeTests.Path "PlayModeTests asmdef must declare optionalUnityReferences exactly as TestAssemblies. Actual=$([string]::Join(',', $optionalUnityReferences))"
    }

    $testsRoot = Join-Path $script:ProjectRoot 'Assets\_Project\Tests'
    $playModeRoot = Join-Path $script:ProjectRoot 'Assets\_Project\Tests\PlayMode'
    $sourceCount = 0
    $outOfBoundaryCount = 0
    if (Test-Path -LiteralPath $testsRoot) {
        Get-ChildItem -LiteralPath $testsRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $sourceCount++
            if (-not (Test-IsPathUnderDirectory -Path $_.FullName -Directory $playModeRoot)) {
                $outOfBoundaryCount++
                $issueCount++
                Add-Finding Error 'TestAsmdefPolicy' (Get-RelativePath $_.FullName) 'Project test C# source is outside the PlayModeTests asmdef boundary.'
            }
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'TestAsmdefPolicy' 'Assets/_Project/Tests' "Project test asmdef policy is valid: PlayModeTests is test-marked and owns all project test C# source. Sources=$sourceCount"
    }
}

function Test-AsmdefPlatformSettings {
    $asmdefsByName = Get-ProjectAsmdefs
    $issueCount = 0

    foreach ($assemblyName in $runtimeAssemblies) {
        if (-not $asmdefsByName.ContainsKey($assemblyName)) {
            continue
        }

        $asmdef = $asmdefsByName[$assemblyName]
        if (@($asmdef.IncludePlatforms).Count -gt 0) {
            $issueCount++
            Add-Finding Error 'AsmdefPlatform' $asmdef.Path "Runtime target assembly must not restrict includePlatforms: $assemblyName -> $([string]::Join(',', @($asmdef.IncludePlatforms)))"
        }

        if (@($asmdef.ExcludePlatforms).Count -gt 0) {
            $issueCount++
            Add-Finding Error 'AsmdefPlatform' $asmdef.Path "Runtime target assembly must not restrict excludePlatforms: $assemblyName -> $([string]::Join(',', @($asmdef.ExcludePlatforms)))"
        }
    }

    if ($asmdefsByName.ContainsKey('Editor')) {
        $editorAsmdef = $asmdefsByName['Editor']
        $includePlatforms = @($editorAsmdef.IncludePlatforms)
        $excludePlatforms = @($editorAsmdef.ExcludePlatforms)
        if ($includePlatforms.Count -ne 1 -or $includePlatforms[0] -ne 'Editor') {
            $issueCount++
            Add-Finding Error 'AsmdefPlatform' $editorAsmdef.Path "Editor target assembly must include only the Editor platform. Actual=$([string]::Join(',', $includePlatforms))"
        }

        if ($excludePlatforms.Count -gt 0) {
            $issueCount++
            Add-Finding Error 'AsmdefPlatform' $editorAsmdef.Path "Editor target assembly should not also set excludePlatforms. Actual=$([string]::Join(',', $excludePlatforms))"
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefPlatform' 'Assets/_Project' 'Target asmdef platform settings are valid: runtime assemblies are unrestricted and Editor is Editor-only.'
    }
}

function Test-AsmdefReferenceOptionSettings {
    $asmdefsByName = Get-ProjectAsmdefs
    $issueCount = 0

    foreach ($assemblyName in $targetAssemblies) {
        if (-not $asmdefsByName.ContainsKey($assemblyName)) {
            continue
        }

        $asmdef = $asmdefsByName[$assemblyName]
        if (-not [string]::IsNullOrWhiteSpace($asmdef.RootNamespace)) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly rootNamespace must stay empty during the split: $assemblyName -> $($asmdef.RootNamespace)"
        }

        if ($asmdef.OverrideReferences) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must not override automatic references during the split: $assemblyName"
        }

        if (@($asmdef.PrecompiledReferences).Count -gt 0) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must not directly pin precompiledReferences during the split: $assemblyName -> $([string]::Join(',', @($asmdef.PrecompiledReferences)))"
        }

        if ($asmdef.NoEngineReferences) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must keep Unity engine references enabled: $assemblyName"
        }

        if ($asmdef.AllowUnsafeCode) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must not enable allowUnsafeCode during the split: $assemblyName"
        }

        if (-not $asmdef.AutoReferenced) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must stay autoReferenced during the split: $assemblyName"
        }

        if (@($asmdef.DefineConstraints).Count -gt 0) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must not be hidden behind defineConstraints during the split: $assemblyName -> $([string]::Join(',', @($asmdef.DefineConstraints)))"
        }

        if (@($asmdef.VersionDefines).Count -gt 0) {
            $issueCount++
            Add-Finding Error 'AsmdefOptions' $asmdef.Path "Target assembly must not be hidden behind versionDefines during the split: $assemblyName"
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefOptions' 'Assets/_Project' 'Target asmdef reference options are valid: empty rootNamespace, no overrideReferences, direct precompiledReferences, noEngineReferences, allowUnsafeCode, defineConstraints, or versionDefines, and autoReferenced stays enabled.'
    }
}

function Test-RuntimeAsmdefEditorReferences {
    $asmdefsByName = Get-ProjectAsmdefs
    $issueCount = 0

    foreach ($assemblyName in $runtimeAssemblies) {
        if (-not $asmdefsByName.ContainsKey($assemblyName)) {
            continue
        }

        $asmdef = $asmdefsByName[$assemblyName]
        foreach ($reference in @($asmdef.References)) {
            if ([string]::IsNullOrWhiteSpace($reference)) {
                continue
            }

            if ($reference -notmatch 'Editor') {
                continue
            }

            $issueCount++
            Add-Finding Error 'AsmdefEditorReference' $asmdef.Path "Runtime target assembly references an Editor-only assembly: $assemblyName -> $reference"
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefEditorReference' 'Assets/_Project' 'Runtime target asmdefs do not reference Editor-only assemblies.'
    }
}

function Test-AsmdefMetaImporters {
    $root = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $root)) {
        return
    }

    $asmdefCount = 0
    $issueCount = 0
    Get-ChildItem -LiteralPath $root -Recurse -Filter '*.asmdef' -File | ForEach-Object {
        $asmdefCount++
        $metaPath = $_.FullName + '.meta'
        $relativePath = Get-RelativePath $_.FullName

        if (-not (Test-Path -LiteralPath $metaPath)) {
            $issueCount++
            Add-Finding Error 'AsmdefMeta' $relativePath 'Asmdef meta file is missing; Unity cannot preserve the asmdef asset GUID.'
            return
        }

        $metaText = Get-Content -LiteralPath $metaPath -Raw
        if ($metaText -notmatch '(?m)^AssemblyDefinitionImporter:') {
            $issueCount++
            Add-Finding Error 'AsmdefMeta' (Get-RelativePath $metaPath) 'Asmdef meta file is missing AssemblyDefinitionImporter metadata.'
            return
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefMeta' 'Assets' "All Assets asmdef meta files are present and contain AssemblyDefinitionImporter metadata. Count=$asmdefCount"
    }
}

function Test-AsmdefReferenceResolution {
    $knownAssemblyNames = [System.Collections.Generic.HashSet[string]]::new()
    $knownAsmdefGuids = Get-KnownAsmdefGuidNameMap
    foreach ($root in @('Assets', 'Packages', 'Library\PackageCache')) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if (-not [string]::IsNullOrWhiteSpace($json.name)) {
                    [void]$knownAssemblyNames.Add([string]$json.name)
                }
            } catch {
                Add-Finding Error 'AsmdefReference' (Get-RelativePath $_.FullName) "Failed to parse asmdef while resolving references: $($_.Exception.Message)"
            }
        }
    }

    $missingCount = 0
    $projectRoot = Join-Path $script:ProjectRoot 'Assets\_Project'
    Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter '*.asmdef' -File | ForEach-Object {
        $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        foreach ($reference in @($json.references)) {
            if ([string]::IsNullOrWhiteSpace($reference)) {
                continue
            }

            if ($reference -match '^GUID:([0-9a-f]{32})$') {
                $guid = $Matches[1]
                if ($knownAsmdefGuids.ContainsKey($guid)) {
                    continue
                }

                $missingCount++
                Add-Finding Error 'AsmdefReference' (Get-RelativePath $_.FullName) "Asmdef GUID reference does not resolve to a known asmdef meta GUID: $reference"
                continue
            }

            $resolvedReference = Resolve-AsmdefReferenceName -Reference ([string]$reference)
            if ($knownAssemblyNames.Contains([string]$resolvedReference)) {
                continue
            }

            $missingCount++
            Add-Finding Error 'AsmdefReference' (Get-RelativePath $_.FullName) "Asmdef reference does not resolve to any known asmdef in Assets, Packages, or Library/PackageCache: $reference"
        }
    }

    if ($missingCount -eq 0) {
        Add-Finding Info 'AsmdefReference' 'Assets/_Project' 'All project asmdef references resolve to known asmdef names or GUIDs.'
    }
}

function Test-AssetAsmdefReferencePolicy {
    $knownAssemblyNames = [System.Collections.Generic.HashSet[string]]::new()
    $knownAsmdefGuids = Get-KnownAsmdefGuidNameMap
    foreach ($root in @('Assets', 'Packages', 'Library\PackageCache')) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if (-not [string]::IsNullOrWhiteSpace($json.name)) {
                    [void]$knownAssemblyNames.Add([string]$json.name)
                }
            } catch {
                Add-Finding Error 'AsmdefAssetReference' (Get-RelativePath $_.FullName) "Failed to parse asmdef while resolving asset references: $($_.Exception.Message)"
            }
        }
    }

    $targetSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$targetAssemblies)
    $supportSet = [System.Collections.Generic.HashSet[string]]::new([string[]]@($expectedSupportAsmdefPaths.Keys))
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $assetsRoot)) {
        Add-Finding Error 'AsmdefAssetReference' 'Assets' 'Assets folder is missing.'
        return
    }

    $asmdefCount = 0
    $issueCount = 0
    Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
            $assemblyName = [string]$json.name
            $references = @()
            if ($null -ne $json.references) {
                $references = @($json.references)
            }

            $asmdefCount++
            foreach ($referenceValue in $references) {
                $reference = [string]$referenceValue
                if ([string]::IsNullOrWhiteSpace($reference)) {
                    continue
                }

                if ($reference -eq 'Assembly-CSharp' -or $reference -eq 'Assembly-CSharp-Editor') {
                    $issueCount++
                    Add-Finding Error 'AsmdefAssetReference' (Get-RelativePath $_.FullName) "Assets asmdef must not reference Unity default assemblies: $assemblyName -> $reference"
                    continue
                }

                $resolvedReference = Resolve-AsmdefReferenceName -Reference $reference
                if ($reference -match '^GUID:([0-9a-f]{32})$') {
                    $guid = $Matches[1]
                    if (-not $knownAsmdefGuids.ContainsKey($guid)) {
                        $issueCount++
                        Add-Finding Error 'AsmdefAssetReference' (Get-RelativePath $_.FullName) "Assets asmdef GUID reference does not resolve to a known asmdef meta GUID: $assemblyName -> $reference"
                        continue
                    }

                    $resolvedReference = $knownAsmdefGuids[$guid]
                } elseif (-not $knownAssemblyNames.Contains($resolvedReference)) {
                    $issueCount++
                    Add-Finding Error 'AsmdefAssetReference' (Get-RelativePath $_.FullName) "Assets asmdef reference does not resolve to any known asmdef in Assets, Packages, or Library/PackageCache: $assemblyName -> $reference"
                    continue
                }

                if ($supportSet.Contains($assemblyName) -and $assemblyName -ne 'PlayModeTests' -and $targetSet.Contains($resolvedReference)) {
                    $issueCount++
                    Add-Finding Error 'AsmdefAssetReference' (Get-RelativePath $_.FullName) "Vendor/support asmdef must not reference project target assemblies: $assemblyName -> $resolvedReference"
                }
            }
        } catch {
            $issueCount++
            Add-Finding Error 'AsmdefAssetReference' (Get-RelativePath $_.FullName) "Failed to parse Assets asmdef while checking reference policy: $($_.Exception.Message)"
        }
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'AsmdefAssetReference' 'Assets' "All Assets asmdef references resolve, avoid Assembly-CSharp defaults, and vendor/support asmdefs do not reference project targets. Count=$asmdefCount"
    }
}

function Test-AsmrefReferenceResolution {
    $knownAssemblyNames = [System.Collections.Generic.HashSet[string]]::new()
    $knownAsmdefGuids = @{}

    foreach ($root in @('Assets', 'Packages', 'Library\PackageCache')) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if (-not [string]::IsNullOrWhiteSpace($json.name)) {
                    [void]$knownAssemblyNames.Add([string]$json.name)
                }

                $metaPath = "$($_.FullName).meta"
                if (Test-Path -LiteralPath $metaPath) {
                    $guid = Get-MetaGuid -MetaPath $metaPath
                    if (-not [string]::IsNullOrWhiteSpace($guid)) {
                        $knownAsmdefGuids[$guid] = [string]$json.name
                    }
                }
            } catch {
                Add-Finding Error 'AsmrefReference' (Get-RelativePath $_.FullName) "Failed to parse asmdef while resolving asmref references: $($_.Exception.Message)"
            }
        }
    }

    $asmrefFiles = @(Get-ChildItem -LiteralPath (Join-Path $script:ProjectRoot 'Assets') -Recurse -Filter '*.asmref' -File -ErrorAction SilentlyContinue)
    if ($asmrefFiles.Count -eq 0) {
        Add-Finding Info 'AsmrefReference' 'Assets' 'No asmref files were found under Assets.'
        return
    }

    $missingCount = 0
    foreach ($asmrefFile in $asmrefFiles) {
        try {
            $json = Get-Content -LiteralPath $asmrefFile.FullName -Raw | ConvertFrom-Json
        } catch {
            $missingCount++
            Add-Finding Error 'AsmrefReference' (Get-RelativePath $asmrefFile.FullName) "Failed to parse asmref: $($_.Exception.Message)"
            continue
        }

        $reference = [string]$json.reference
        if ([string]::IsNullOrWhiteSpace($reference)) {
            $missingCount++
            Add-Finding Error 'AsmrefReference' (Get-RelativePath $asmrefFile.FullName) 'Asmref has no reference value.'
            continue
        }

        if ($reference -match '^GUID:([0-9a-f]{32})$') {
            $guid = $Matches[1]
            if ($knownAsmdefGuids.ContainsKey($guid)) {
                continue
            }

            $missingCount++
            Add-Finding Error 'AsmrefReference' (Get-RelativePath $asmrefFile.FullName) "Asmref GUID reference does not resolve to a known asmdef meta GUID: $reference"
            continue
        }

        if ($knownAssemblyNames.Contains($reference)) {
            continue
        }

        $missingCount++
        Add-Finding Error 'AsmrefReference' (Get-RelativePath $asmrefFile.FullName) "Asmref reference does not resolve to any known asmdef name: $reference"
    }

    if ($missingCount -eq 0) {
        Add-Finding Info 'AsmrefReference' 'Assets' "All asmref references resolve to known asmdefs. Files=$($asmrefFiles.Count)"
    }
}

function Test-AsmdefRequiredExternalReferences {
    $requiredReferenceRules = @(
        [pscustomobject]@{ Reference = 'DOTween.Modules'; Pattern = '(?m)^\s*using\s+DG\.Tweening\s*;|\bDG\.Tweening\.'; Description = 'DOTween APIs' },
        [pscustomobject]@{ Reference = 'Ink-Libraries'; Pattern = '(?m)^\s*using\s+Ink\.Runtime\s*;|\bInk\.Runtime\.'; Description = 'Ink runtime APIs' },
        [pscustomobject]@{ Reference = 'Unity.2D.Animation.Runtime'; Pattern = '(?m)^\s*using\s+UnityEngine\.U2D\.Animation\s*;|\bSpriteLibraryAsset\b'; Description = '2D Animation sprite-library APIs' },
        [pscustomobject]@{ Reference = 'Unity.2D.PixelPerfect'; Pattern = '\bPixelPerfectCamera\b'; Description = '2D Pixel Perfect camera APIs' },
        [pscustomobject]@{ Reference = 'Unity.Behavior'; Pattern = '(?m)^\s*using\s+Unity\.Behavior\s*;|\bUnity\.Behavior\.'; Description = 'Unity Behavior APIs' },
        [pscustomobject]@{ Reference = 'Unity.Cinemachine'; Pattern = '(?m)^\s*using\s+Unity\.Cinemachine\s*;|\bUnity\.Cinemachine\.'; Description = 'Cinemachine APIs' },
        [pscustomobject]@{ Reference = 'Unity.InputSystem'; Pattern = '(?m)^\s*using\s+UnityEngine\.InputSystem(?:\.|\s*;)|\bUnityEngine\.InputSystem\.'; Description = 'Input System APIs' },
        [pscustomobject]@{ Reference = 'Unity.TextMeshPro'; Pattern = '(?m)^\s*using\s+TMPro\s*;|\bTMPro\.'; Description = 'TextMeshPro APIs' },
        [pscustomobject]@{ Reference = 'UnityEngine.UI'; Pattern = '(?m)^\s*using\s+UnityEngine\.UI\s*;|\bUnityEngine\.UI\.'; Description = 'Unity UI APIs' },
        [pscustomobject]@{ Reference = 'Unity.Addressables'; Pattern = '(?m)^\s*using\s+UnityEngine\.AddressableAssets(?:\.|\s*;)|\bUnityEngine\.AddressableAssets\.'; Description = 'Addressables APIs' },
        [pscustomobject]@{ Reference = 'Unity.ResourceManager'; Pattern = '(?m)^\s*using\s+UnityEngine\.ResourceManagement(?:\.|\s*;)|\bUnityEngine\.ResourceManagement\.'; Description = 'Resource Manager APIs' },
        [pscustomobject]@{ Reference = 'Unity.RenderPipelines.Universal.Runtime'; Pattern = '(?m)^\s*using\s+UnityEngine\.Rendering\.Universal\s*;|\bUnityEngine\.Rendering\.Universal\.'; Description = 'URP runtime APIs' },
        [pscustomobject]@{ Reference = 'Unity.RenderPipelines.Universal.2D.Runtime'; Pattern = '\b(Light2D|ShadowCaster2D)\b'; Description = 'URP 2D renderer APIs' }
    )

    $asmdefsByName = Get-ProjectAsmdefs
    $missingCount = 0

    foreach ($asmdef in $asmdefsByName.Values | Sort-Object Name) {
        $asmdefPath = Join-Path $script:ProjectRoot $asmdef.Path
        if (-not (Test-Path -LiteralPath $asmdefPath)) {
            continue
        }

        $sourceRoot = Split-Path -Parent $asmdefPath
        $sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue)
        if ($sourceFiles.Count -eq 0) {
            continue
        }

        $references = [System.Collections.Generic.HashSet[string]]::new([string[]]@($asmdef.References))
        $text = ($sourceFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"

        foreach ($rule in $requiredReferenceRules) {
            if ($references.Contains($rule.Reference)) {
                continue
            }

            if ($text -notmatch $rule.Pattern) {
                continue
            }

            $missingCount++
            Add-Finding Error 'AsmdefExternalReference' $asmdef.Path "Source uses $($rule.Description), but asmdef does not reference package assembly: $($rule.Reference)"
        }
    }

    if ($missingCount -eq 0) {
        Add-Finding Info 'AsmdefExternalReference' 'Assets/_Project' 'All detected external package API usages have matching asmdef references.'
    }
}

function Remove-CSharpTrivia {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    $withoutBlockComments = [regex]::Replace($Text, '(?s)/\*.*?\*/', ' ')
    $withoutLineComments = [regex]::Replace($withoutBlockComments, '(?m)//.*$', ' ')
    $withoutVerbatimStrings = [regex]::Replace($withoutLineComments, '@"(?:[^"]|"")*"', '""')
    $withoutStrings = [regex]::Replace($withoutVerbatimStrings, '"(?:\\.|[^"\\])*"', '""')
    return [regex]::Replace($withoutStrings, "'(?:\\.|[^'\\])'", "''")
}

function Remove-CSharpComments {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    $withoutBlockComments = [regex]::Replace($Text, '(?s)/\*.*?\*/', ' ')
    return [regex]::Replace($withoutBlockComments, '(?m)//.*$', ' ')
}

function Test-KnownForbiddenConcreteDependencies {
    $rules = @(
        [pscustomobject]@{
            Assembly = 'Core'
            Root = 'Assets/_Project/Runtime/Core'
            ForbiddenTypes = @(
                'SoundManager',
                'CombatHitAudioRouter',
                'CameraBootstrap',
                'CameraShakeService',
                'WorldPresentationRuntime',
                'PresentationSpawnService',
                'DamagePopupService',
                'DamagePopupListener2D',
                'GlobalUIRoot',
                'DialogueView',
                'GameSettingsService',
                'AttackTelegraphService',
                'AttackTelegraphView',
                'BossGroggyHeadTimer',
                'TimedAnimatedHitEffect2D',
                'GameplayCue_HitSparkParticles',
                'PlayerIntentInput2D',
                'ChestUIManager',
                'UIManager'
            )
        },
        [pscustomobject]@{
            Assembly = 'Gameplay'
            Root = 'Assets/_Project/Runtime/Features'
            ForbiddenTypes = @(
                'CameraBootstrap',
                'CameraShakeService',
                'WorldPresentationRuntime',
                'PresentationSpawnService',
                'AttackTelegraphService',
                'AttackTelegraphView',
                'DialogueView',
                'ChestUIManager',
                'UIManager',
                'GameOverPresentationController',
                'EndingOutroView',
                'TutorialInfoPanel',
                'TutorialPresentationHpView',
                'AffectionUI',
                'RewardDisplayService',
                'UpgradeTreeUI',
                'BossHudController',
                'DamagePopupService'
            )
        }
    )

    $hitCount = 0
    foreach ($rule in $rules) {
        $absoluteRoot = Join-Path $script:ProjectRoot ($rule.Root.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $relativePath = Get-RelativePath $_.FullName
            $sourceText = Remove-CSharpTrivia -Text (Get-Content -LiteralPath $_.FullName -Raw)
            foreach ($typeName in $rule.ForbiddenTypes) {
                $pattern = '\b' + [regex]::Escape($typeName) + '\b'
                if (-not [regex]::IsMatch($sourceText, $pattern)) {
                    continue
                }

                $hitCount++
                Add-Finding Error 'SourceDependencyConcreteType' $relativePath "$($rule.Assembly) source references forbidden concrete upper-layer type: $typeName"
            }
        }
    }

    if ($hitCount -eq 0) {
        Add-Finding Info 'SourceDependencyConcreteType' 'Assets/_Project/Runtime/Core; Assets/_Project/Runtime/Features' 'No known forbidden concrete upper-layer type references were found in Core or Gameplay source after removing comments and string literals.'
    }
}

function Test-LowerLayerForbiddenNamespaceReferences {
    $namespacePattern = '^\s*namespace\s+(?<Name>[A-Za-z_][A-Za-z0-9_.]*)\b'
    $namespaceOwners = @{}

    foreach ($assemblyName in $targetAssemblies) {
        $relativeRoot = $targetAssemblySourceRoots[$assemblyName]
        $absoluteRoot = Join-Path $script:ProjectRoot ($relativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                if ($line -notmatch $namespacePattern) {
                    continue
                }

                $namespace = $Matches['Name']
                if (-not $namespaceOwners.ContainsKey($namespace)) {
                    $namespaceOwners[$namespace] = [System.Collections.Generic.HashSet[string]]::new()
                }

                [void]$namespaceOwners[$namespace].Add($assemblyName)
            }
        }
    }

    $rules = @(
        [pscustomobject]@{
            Assembly = 'Core'
            Root = 'Assets/_Project/Runtime/Core'
            ForbiddenAssemblies = @('Gameplay', 'Infrastructure', 'Presentation', 'UI', 'Editor')
            AllowedProviderAssemblies = @('Core')
        },
        [pscustomobject]@{
            Assembly = 'Gameplay'
            Root = 'Assets/_Project/Runtime/Features'
            ForbiddenAssemblies = @('Infrastructure', 'Presentation', 'UI', 'Editor')
            AllowedProviderAssemblies = @('Core', 'Gameplay')
        }
    )

    $hitCount = 0
    foreach ($rule in $rules) {
        $forbiddenSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$rule.ForbiddenAssemblies)
        $allowedSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$rule.AllowedProviderAssemblies)
        $forbiddenNamespaces = New-Object System.Collections.Generic.List[string]

        foreach ($entry in $namespaceOwners.GetEnumerator()) {
            $hasForbiddenOwner = $false
            foreach ($owner in $entry.Value) {
                if ($forbiddenSet.Contains($owner)) {
                    $hasForbiddenOwner = $true
                    break
                }
            }

            if (-not $hasForbiddenOwner) {
                continue
            }

            $hasAllowedOwner = $false
            foreach ($owner in $entry.Value) {
                if ($allowedSet.Contains($owner)) {
                    $hasAllowedOwner = $true
                    break
                }
            }

            if (-not $hasAllowedOwner) {
                $forbiddenNamespaces.Add([string]$entry.Key)
            }
        }

        $absoluteRoot = Join-Path $script:ProjectRoot ($rule.Root.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        if ($forbiddenNamespaces.Count -eq 0) {
            continue
        }

        $namespaceAlternation = (($forbiddenNamespaces | Sort-Object { $_.Length } -Descending | ForEach-Object { [regex]::Escape($_) }) -join '|')
        $usingRegex = New-Object System.Text.RegularExpressions.Regex('(?m)^\s*using\s+(?:static\s+)?(?<Namespace>' + $namespaceAlternation + ')(?:\.[A-Za-z_][A-Za-z0-9_.]*)?\s*;')
        $qualifiedRegex = New-Object System.Text.RegularExpressions.Regex('\b(?<Namespace>' + $namespaceAlternation + ')\.')

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $relativePath = Get-RelativePath $_.FullName
            $sourceText = Remove-CSharpTrivia -Text (Get-Content -LiteralPath $_.FullName -Raw)
            $usingHits = [System.Collections.Generic.HashSet[string]]::new()
            $qualifiedHits = [System.Collections.Generic.HashSet[string]]::new()

            foreach ($match in $usingRegex.Matches($sourceText)) {
                $namespace = $match.Groups['Namespace'].Value
                if ($usingHits.Add($namespace)) {
                    $hitCount++
                    Add-Finding Error 'SourceDependencyNamespace' $relativePath "$($rule.Assembly) source imports upper-layer namespace: $namespace"
                }
            }

            foreach ($match in $qualifiedRegex.Matches($sourceText)) {
                $namespace = $match.Groups['Namespace'].Value
                if ($usingHits.Contains($namespace)) {
                    continue
                }

                if ($qualifiedHits.Add($namespace)) {
                    $hitCount++
                    Add-Finding Error 'SourceDependencyNamespace' $relativePath "$($rule.Assembly) source references upper-layer namespace with a qualified name: $namespace"
                }
            }
        }
    }

    if ($hitCount -eq 0) {
        Add-Finding Info 'SourceDependencyNamespace' 'Assets/_Project/Runtime/Core; Assets/_Project/Runtime/Features' 'No upper-layer-only namespace imports or qualified references were found in Core or Gameplay source after removing comments and string literals.'
    }
}

function Test-LowerLayerForbiddenPresentationApiReferences {
    $forbiddenApis = @(
        [pscustomobject]@{ Pattern = '(?m)^\s*using\s+TMPro\s*;|\bTMPro\.|\bTMP_Text\b|\bTextMeshPro(?:UGUI)?\b'; Description = 'TextMeshPro concrete UI APIs' },
        [pscustomobject]@{ Pattern = '(?m)^\s*using\s+UnityEngine\.UI\s*;|\bUnityEngine\.UI\.'; Description = 'UnityEngine.UI concrete UI APIs' },
        [pscustomobject]@{ Pattern = '(?m)^\s*using\s+Unity\.Cinemachine\s*;|\bUnity\.Cinemachine\.|\bCinemachine(?:Camera|Brain|ImpulseSource|VirtualCameraBase)\b'; Description = 'Cinemachine concrete camera APIs' },
        [pscustomobject]@{ Pattern = '(?m)^\s*using\s+DG\.Tweening\s*;|\bDG\.Tweening\.|\bDOTween\b|\bDOVirtual\b'; Description = 'DOTween concrete tween APIs' },
        [pscustomobject]@{ Pattern = '(?m)^\s*using\s+UnityEngine\.Rendering\.Universal\s*;|\bUnityEngine\.Rendering\.Universal\.|\bLight2D\b|\bShadowCaster2D\b'; Description = 'URP concrete 2D lighting APIs' }
    )
    $rules = @(
        [pscustomobject]@{ Assembly = 'Core'; Root = 'Assets/_Project/Runtime/Core' },
        [pscustomobject]@{ Assembly = 'Gameplay'; Root = 'Assets/_Project/Runtime/Features' }
    )

    $hitCount = 0
    foreach ($rule in $rules) {
        $absoluteRoot = Join-Path $script:ProjectRoot ($rule.Root.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $relativePath = Get-RelativePath $_.FullName
            $sourceText = Remove-CSharpTrivia -Text (Get-Content -LiteralPath $_.FullName -Raw)
            foreach ($api in $forbiddenApis) {
                if ($sourceText -notmatch $api.Pattern) {
                    continue
                }

                $hitCount++
                Add-Finding Error 'SourceDependencyPresentationApi' $relativePath "$($rule.Assembly) source references forbidden concrete presentation API: $($api.Description)"
            }
        }
    }

    if ($hitCount -eq 0) {
        Add-Finding Info 'SourceDependencyPresentationApi' 'Assets/_Project/Runtime/Core; Assets/_Project/Runtime/Features' 'No concrete TextMeshPro, Unity UI, Cinemachine, DOTween, or URP 2D lighting API references were found in Core or Gameplay source after removing comments and string literals.'
    }
}

function Test-ProjectSourceDefaultAssemblyLiterals {
    $projectRoot = Join-Path $script:ProjectRoot 'Assets\_Project'
    if (-not (Test-Path -LiteralPath $projectRoot)) {
        Add-Finding Error 'SourceDefaultAssemblyLiteral' 'Assets/_Project' 'Project source root is missing.'
        return
    }

    $issueCount = 0
    $sourceCount = 0
    foreach ($sourceFile in Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue) {
        $relativePath = ConvertTo-ProjectSlashPath (Get-RelativePath $sourceFile.FullName)
        if ($relativePath.StartsWith('Assets/_Project/Editor/Tools/Validation/', [System.StringComparison]::Ordinal)) {
            continue
        }

        $sourceCount++
        $sourceText = Remove-CSharpComments -Text (Get-Content -LiteralPath $sourceFile.FullName -Raw)
        if ($sourceText -notmatch 'Assembly-CSharp(?:-Editor)?') {
            continue
        }

        $issueCount++
        Add-Finding Error 'SourceDefaultAssemblyLiteral' $relativePath 'Project source contains a hardcoded default Unity assembly name outside validation tooling.'
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'SourceDefaultAssemblyLiteral' 'Assets/_Project' "No hardcoded Assembly-CSharp or Assembly-CSharp-Editor literals were found in project source outside validation tooling. Sources=$sourceCount"
    }
}

function Test-SourceCoverage {
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    $uncovered = New-Object System.Collections.Generic.List[string]

    Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.cs' -File | ForEach-Object {
        $directory = $_.Directory
        $covered = $false

        while ($null -ne $directory -and $directory.FullName.StartsWith($assetsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            if ((Get-ChildItem -LiteralPath $directory.FullName -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | Select-Object -First 1) -or
                (Get-ChildItem -LiteralPath $directory.FullName -Filter '*.asmref' -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
                $covered = $true
                break
            }

            $directory = $directory.Parent
        }

        if (-not $covered) {
            $uncovered.Add((Get-RelativePath $_.FullName))
        }
    }

    if ($uncovered.Count -eq 0) {
        Add-Finding Info 'SourceCoverage' 'Assets' 'All C# source files under Assets are covered by an asmdef or asmref boundary.'
        return
    }

    foreach ($path in $uncovered) {
        Add-Finding Error 'SourceCoverage' $path 'C# source file is outside any asmdef or asmref boundary.'
    }
}

function Get-SourceAssemblyOwner {
    param([string]$SourcePath)

    $assetsRoot = [System.IO.Path]::GetFullPath((Join-Path $script:ProjectRoot 'Assets')).TrimEnd('\', '/')
    $directory = [System.IO.DirectoryInfo]::new([System.IO.Path]::GetDirectoryName($SourcePath))
    while ($null -ne $directory) {
        $current = [System.IO.Path]::GetFullPath($directory.FullName).TrimEnd('\', '/')
        if (-not ($current.Equals($assetsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
            $current.StartsWith($assetsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) {
            break
        }

        $asmdef = Get-ChildItem -LiteralPath $current -Filter '*.asmdef' -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($asmdef) {
            try {
                $json = Get-Content -LiteralPath $asmdef.FullName -Raw | ConvertFrom-Json
                return [string]$json.name
            } catch {
                return '<invalid-asmdef>'
            }
        }

        $asmref = Get-ChildItem -LiteralPath $current -Filter '*.asmref' -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($asmref) {
            try {
                $json = Get-Content -LiteralPath $asmref.FullName -Raw | ConvertFrom-Json
                return Resolve-AsmdefReferenceName -Reference ([string]$json.reference)
            } catch {
                return '<invalid-asmref>'
            }
        }

        $directory = $directory.Parent
    }

    return $null
}

function Test-AssetSourceAssemblyOwners {
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $assetsRoot)) {
        Add-Finding Error 'SourceAssemblyOwner' 'Assets' 'Assets folder is missing.'
        return
    }

    $allowedOwners = [System.Collections.Generic.HashSet[string]]::new([string[]]$allowedAssetSourceAssemblies, [System.StringComparer]::OrdinalIgnoreCase)
    $ownerCounts = @{}
    $issueCount = 0
    $sourceCount = 0

    foreach ($sourceFile in Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue) {
        $sourceCount++
        $owner = Get-SourceAssemblyOwner -SourcePath $sourceFile.FullName
        if ([string]::IsNullOrWhiteSpace($owner)) {
            $issueCount++
            Add-Finding Error 'SourceAssemblyOwner' (Get-RelativePath $sourceFile.FullName) 'C# source has no asmdef/asmref owner and would compile into a default Unity assembly.'
            continue
        }

        if (-not $ownerCounts.ContainsKey($owner)) {
            $ownerCounts[$owner] = 0
        }

        $ownerCounts[$owner]++
        if (-not $allowedOwners.Contains($owner)) {
            $issueCount++
            Add-Finding Error 'SourceAssemblyOwner' (Get-RelativePath $sourceFile.FullName) "C# source is owned by an unapproved Assets assembly: $owner"
        }
    }

    if ($issueCount -eq 0) {
        $summary = ($ownerCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
        Add-Finding Info 'SourceAssemblyOwner' 'Assets' "All C# source under Assets is owned by approved target/test/support assemblies. Sources=$sourceCount; Owners=$summary"
    }
}

function Test-ProjectNamespaceAssemblySpans {
    $assemblyRoots = @(
        [pscustomobject]@{ Assembly = 'Core'; Root = 'Assets/_Project/Runtime/Core' },
        [pscustomobject]@{ Assembly = 'Gameplay'; Root = 'Assets/_Project/Runtime/Features' },
        [pscustomobject]@{ Assembly = 'Infrastructure'; Root = 'Assets/_Project/Runtime/Infrastructure' },
        [pscustomobject]@{ Assembly = 'Presentation'; Root = 'Assets/_Project/Runtime/Presentation' },
        [pscustomobject]@{ Assembly = 'UI'; Root = 'Assets/_Project/Runtime/UI' },
        [pscustomobject]@{ Assembly = 'Editor'; Root = 'Assets/_Project/Editor' }
    )

    $namespaceAssemblies = @{}
    foreach ($entry in $assemblyRoots) {
        $absoluteRoot = Join-Path $script:ProjectRoot ($entry.Root.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                if ($line -notmatch '^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\b') {
                    continue
                }

                $namespace = $Matches[1]
                if (-not $namespaceAssemblies.ContainsKey($namespace)) {
                    $namespaceAssemblies[$namespace] = [System.Collections.Generic.HashSet[string]]::new()
                }

                [void]$namespaceAssemblies[$namespace].Add([string]$entry.Assembly)
            }
        }
    }

    $spans = New-Object System.Collections.Generic.List[string]
    foreach ($namespace in $namespaceAssemblies.Keys | Sort-Object) {
        if ($namespaceAssemblies[$namespace].Count -lt 2) {
            continue
        }

        $assemblies = @($namespaceAssemblies[$namespace] | Sort-Object)
        $spans.Add("$namespace($([string]::Join(',', $assemblies)))")
    }

    if ($spans.Count -eq 0) {
        Add-Finding Info 'NamespaceSpan' 'Assets/_Project' 'No declared namespace is shared by multiple target project assemblies.'
        return
    }

    $sample = [string]::Join('; ', @($spans | Select-Object -First 8))
    Add-Finding Info 'NamespaceSpan' 'Assets/_Project' "Declared namespaces span multiple target project assemblies. Count=$($spans.Count); Sample=$sample. Treat namespaces as API/serialization compatibility labels, not as assembly-boundary proof."
}

function Test-CSharpMetaPairing {
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    $sourceCount = 0
    $metaCount = 0
    $missingMetaCount = 0
    $missingGuidCount = 0
    $orphanMetaCount = 0

    Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.cs' -File | ForEach-Object {
        $sourceCount++
        $metaPath = $_.FullName + '.meta'
        if (-not (Test-Path -LiteralPath $metaPath)) {
            $missingMetaCount++
            Add-Finding Error 'ScriptMeta' (Get-RelativePath $_.FullName) 'C# source file is missing its .cs.meta pair; script GUID preservation is not proven.'
            return
        }

        $guid = Get-MetaGuid -MetaPath $metaPath
        if ([string]::IsNullOrWhiteSpace($guid)) {
            $missingGuidCount++
            Add-Finding Error 'ScriptMeta' (Get-RelativePath $metaPath) 'C# meta file is missing a Unity GUID.'
        }
    }

    Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.cs.meta' -File | ForEach-Object {
        $metaCount++
        $sourcePath = $_.FullName.Substring(0, $_.FullName.Length - 5)
        if (Test-Path -LiteralPath $sourcePath) {
            return
        }

        $orphanMetaCount++
        Add-Finding Warning 'ScriptMeta' (Get-RelativePath $_.FullName) 'C# meta file has no matching .cs source file.'
    }

    if ($missingMetaCount -eq 0 -and $missingGuidCount -eq 0 -and $orphanMetaCount -eq 0) {
        Add-Finding Info 'ScriptMeta' 'Assets' "All C# source files have .cs.meta pairs with GUIDs, and no orphan .cs.meta files were found. Sources=$sourceCount; Metas=$metaCount"
    }
}

function Get-GitExecutableForAudit {
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $gitCommand) {
        return $gitCommand.Source
    }

    $visualStudioGitPath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
    if (Test-Path -LiteralPath $visualStudioGitPath) {
        return $visualStudioGitPath
    }

    return $null
}

function Test-MovedScriptSourceMetaPairing {
    $gitPath = Get-GitExecutableForAudit
    if ([string]::IsNullOrWhiteSpace($gitPath)) {
        Add-Finding Warning 'ScriptMetaMovePair' 'git diff' 'Git is not available; deleted C# source/meta move pairing could not be verified.'
        return
    }

    $deletedSourcePaths = @(& $gitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false diff --name-only --diff-filter=D -- '*.cs' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Add-Finding Warning 'ScriptMetaMovePair' 'git diff' 'Git diff failed while reading deleted C# source files.'
        return
    }

    $deletedMetaPaths = @(& $gitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false diff --name-only --diff-filter=D -- '*.cs.meta' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Add-Finding Warning 'ScriptMetaMovePair' 'git diff' 'Git diff failed while reading deleted C# meta files.'
        return
    }

    if ($deletedSourcePaths.Count -eq 0 -and $deletedMetaPaths.Count -eq 0) {
        Add-Finding Info 'ScriptMetaMovePair' 'git diff' 'No deleted C# source or meta files were found in the current diff.'
        return
    }

    $deletedSourceSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $deletedSourcePaths) {
        [void]$deletedSourceSet.Add((ConvertTo-ProjectSlashPath $path))
    }

    $deletedMetaSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $deletedMetaPaths) {
        [void]$deletedMetaSet.Add((ConvertTo-ProjectSlashPath $path))
    }

    $issueCount = 0
    foreach ($sourcePath in $deletedSourceSet | Sort-Object) {
        $expectedMetaPath = "$sourcePath.meta"
        if ($deletedMetaSet.Contains($expectedMetaPath)) {
            continue
        }

        $issueCount++
        Add-Finding Error 'ScriptMetaMovePair' $sourcePath "Deleted C# source file does not have its matching deleted .cs.meta in the current diff: $expectedMetaPath"
    }

    foreach ($metaPath in $deletedMetaSet | Sort-Object) {
        if (-not $metaPath.EndsWith('.cs.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $expectedSourcePath = $metaPath.Substring(0, $metaPath.Length - '.meta'.Length)
        if ($deletedSourceSet.Contains($expectedSourcePath)) {
            continue
        }

        $issueCount++
        Add-Finding Error 'ScriptMetaMovePair' $metaPath "Deleted C# meta file does not have its matching deleted .cs source in the current diff: $expectedSourcePath"
    }

    if ($issueCount -eq 0) {
        Add-Finding Info 'ScriptMetaMovePair' 'git diff' "Deleted C# source/meta move pairing passed. DeletedSources=$($deletedSourceSet.Count); DeletedMetas=$($deletedMetaSet.Count)"
    }
}

function Test-MovedScriptMetaGuidPreservation {
    $gitPath = Get-GitExecutableForAudit
    if ([string]::IsNullOrWhiteSpace($gitPath)) {
        Add-Finding Warning 'ScriptMetaGuidPreservation' 'git diff' 'Git is not available; deleted script meta GUID preservation could not be verified.'
        return
    }

    $deletedMetaPaths = @(& $gitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false diff --name-only --diff-filter=D -- '*.cs.meta' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Add-Finding Warning 'ScriptMetaGuidPreservation' 'git diff' 'Git diff failed; deleted script meta GUID preservation could not be verified.'
        return
    }

    if ($deletedMetaPaths.Count -eq 0) {
        Add-Finding Info 'ScriptMetaGuidPreservation' 'git diff' 'No deleted C# meta files were found in the current diff.'
        return
    }

    $currentGuidToPaths = @{}
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.cs.meta' -File -ErrorAction SilentlyContinue | ForEach-Object {
        $guid = Get-MetaGuid -MetaPath $_.FullName
        if ([string]::IsNullOrWhiteSpace($guid)) {
            return
        }

        if (-not $currentGuidToPaths.ContainsKey($guid)) {
            $currentGuidToPaths[$guid] = New-Object System.Collections.Generic.List[string]
        }

        $currentGuidToPaths[$guid].Add((Get-RelativePath $_.FullName))
    }

    $preservedCount = 0
    $missingCount = 0
    foreach ($deletedMetaPath in $deletedMetaPaths) {
        $oldMetaText = @(& $gitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false show "HEAD:$deletedMetaPath" 2>$null)
        if ($LASTEXITCODE -ne 0) {
            Add-Finding Warning 'ScriptMetaGuidPreservation' $deletedMetaPath 'Could not read deleted meta file from HEAD.'
            continue
        }

        $oldGuid = $null
        foreach ($line in $oldMetaText) {
            if ($line -match '^guid: ([0-9a-f]{32})') {
                $oldGuid = $Matches[1]
                break
            }
        }

        if ([string]::IsNullOrWhiteSpace($oldGuid)) {
            Add-Finding Error 'ScriptMetaGuidPreservation' $deletedMetaPath 'Deleted C# meta file had no GUID in HEAD.'
            continue
        }

        if (-not $currentGuidToPaths.ContainsKey($oldGuid)) {
            $missingCount++
            Add-Finding Error 'ScriptMetaGuidPreservation' $deletedMetaPath "Deleted C# meta GUID is not present anywhere under current Assets. Guid=$oldGuid"
            continue
        }

        $preservedCount++
    }

    if ($missingCount -eq 0) {
        Add-Finding Info 'ScriptMetaGuidPreservation' 'git diff' "Deleted C# meta GUID preservation passed. DeletedMetas=$($deletedMetaPaths.Count); PreservedGuids=$preservedCount"
    }
}

function Test-AssetMetaGuidUniqueness {
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $assetsRoot)) {
        Add-Finding Error 'AssetMetaGuid' 'Assets' 'Assets folder is missing; asset meta GUID uniqueness cannot be verified.'
        return
    }

    $guidToPath = @{}
    $metaCount = 0
    $missingGuidCount = 0
    $duplicateGuidCount = 0

    Get-ChildItem -LiteralPath $assetsRoot -Recurse -Filter '*.meta' -File -ErrorAction SilentlyContinue | ForEach-Object {
        $metaCount++
        $relativePath = Get-RelativePath $_.FullName
        $guid = Get-MetaGuid -MetaPath $_.FullName
        if ([string]::IsNullOrWhiteSpace($guid)) {
            $missingGuidCount++
            Add-Finding Error 'AssetMetaGuid' $relativePath 'Asset meta file is missing a Unity GUID.'
            return
        }

        if ($guidToPath.ContainsKey($guid)) {
            $duplicateGuidCount++
            Add-Finding Error 'AssetMetaGuid' $relativePath "Duplicate Unity meta GUID found. Guid=$guid First=$($guidToPath[$guid])"
            return
        }

        $guidToPath[$guid] = $relativePath
    }

    if ($missingGuidCount -eq 0 -and $duplicateGuidCount -eq 0) {
        Add-Finding Info 'AssetMetaGuid' 'Assets' "All asset meta files under Assets have unique Unity GUIDs. Metas=$metaCount; UniqueGuids=$($guidToPath.Count)"
    }
}

function Get-BraceDelta {
    param([string]$Line)

    if ([string]::IsNullOrEmpty($Line)) {
        return 0
    }

    $delta = 0
    foreach ($character in $Line.ToCharArray()) {
        if ($character -eq '{') {
            $delta++
        } elseif ($character -eq '}') {
            $delta--
        }
    }

    return $delta
}

function Test-DuplicateTargetTypeDeclarations {
    $namespacePattern = '^\s*namespace\s+(?<Name>[A-Za-z_][A-Za-z0-9_.]*)\b'
    $typeDeclarationPattern = '^\s*(?:\[[^\]]+\]\s*)*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|unsafe|new)\s+)*(?:class|interface|struct|record(?:\s+struct|\s+class)?|enum)\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)\b'
    $declarationsByName = @{}

    foreach ($assemblyName in $targetAssemblies) {
        $relativeRoot = $targetAssemblySourceRoots[$assemblyName]
        $absoluteRoot = Join-Path $script:ProjectRoot ($relativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $relativePath = Get-RelativePath $_.FullName
            $currentNamespace = ''
            $typeDeclarationDepth = 0
            $braceDepth = 0
            $lineNumber = 0

            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                $lineNumber++

                if ($line -match $namespacePattern) {
                    $currentNamespace = $Matches['Name']
                    $typeDeclarationDepth = if ($line -match ';') { $braceDepth } else { $braceDepth + 1 }
                }

                if ($braceDepth -eq $typeDeclarationDepth -and $line -match $typeDeclarationPattern) {
                    $typeName = $Matches['Name']
                    $fullName = if ([string]::IsNullOrWhiteSpace($currentNamespace)) { $typeName } else { "$currentNamespace.$typeName" }
                    if (-not $declarationsByName.ContainsKey($fullName)) {
                        $declarationsByName[$fullName] = New-Object System.Collections.Generic.List[object]
                    }

                    $declarationsByName[$fullName].Add([pscustomobject]@{
                        Assembly = $assemblyName
                        Path = $relativePath
                        Line = $lineNumber
                    })
                }

                $braceDepth += Get-BraceDelta -Line $line
            }
        }
    }

    $duplicateCount = 0
    foreach ($entry in $declarationsByName.GetEnumerator() | Sort-Object Name) {
        $assemblies = @($entry.Value | Select-Object -ExpandProperty Assembly -Unique)
        if ($assemblies.Count -le 1) {
            continue
        }

        $duplicateCount++
        $sample = [string]::Join(', ', @($entry.Value | Select-Object -First 8 | ForEach-Object { "$($_.Assembly):$($_.Path):$($_.Line)" }))
        Add-Finding Error 'DuplicateType' $entry.Key "Top-level type is declared in multiple target assemblies: $sample"
    }

    if ($duplicateCount -eq 0) {
        Add-Finding Info 'DuplicateType' 'Assets/_Project' "No duplicate top-level type declarations were found across target assemblies. Types=$($declarationsByName.Count)"
    }
}

function Get-TargetTopLevelTypeAssemblyMap {
    param([string[]]$AssemblyNames)

    $namespacePattern = '^\s*namespace\s+(?<Name>[A-Za-z_][A-Za-z0-9_.]*)\b'
    $typeDeclarationPattern = '^\s*(?:\[[^\]]+\]\s*)*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|unsafe|new)\s+)*(?:class|interface|struct|record(?:\s+struct|\s+class)?|enum)\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)\b'
    $typeMap = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)

    foreach ($assemblyName in $AssemblyNames) {
        $relativeRoot = $targetAssemblySourceRoots[$assemblyName]
        $absoluteRoot = Join-Path $script:ProjectRoot ($relativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object {
            $currentNamespace = ''
            $typeDeclarationDepth = 0
            $braceDepth = 0

            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                if ($line -match $namespacePattern) {
                    $currentNamespace = $Matches['Name']
                    $typeDeclarationDepth = if ($line -match ';') { $braceDepth } else { $braceDepth + 1 }
                }

                if ($braceDepth -eq $typeDeclarationDepth -and $line -match $typeDeclarationPattern) {
                    $typeName = $Matches['Name']
                    $fullName = if ([string]::IsNullOrWhiteSpace($currentNamespace)) { $typeName } else { "$currentNamespace.$typeName" }
                    if (-not $typeMap.ContainsKey($fullName)) {
                        $typeMap.Add($fullName, $assemblyName)
                    }
                }

                $braceDepth += Get-BraceDelta -Line $line
            }
        }
    }

    return $typeMap
}

function Test-HasTypeResponsibilityContext {
    param(
        [string[]]$Lines,
        [int]$LineIndex
    )

    $start = [Math]::Max(0, $LineIndex - 12)
    $end = [Math]::Min($Lines.Length - 1, $LineIndex + 6)
    for ($i = $start; $i -le $end; $i++) {
        if ($Lines[$i] -match '책임|Responsibility') {
            return $true
        }
    }

    return $false
}

function Get-TypeDeclarationRanges {
    param(
        [string[]]$Lines,
        [string]$TypeDeclarationPattern
    )

    $ranges = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -notmatch $TypeDeclarationPattern) {
            continue
        }

        $typeName = $Matches['Name']
        $braceDepth = 0
        $opened = $false
        $endLine = $i

        for ($j = $i; $j -lt $Lines.Count; $j++) {
            foreach ($character in $Lines[$j].ToCharArray()) {
                if ($character -eq '{') {
                    $braceDepth++
                    $opened = $true
                } elseif ($character -eq '}') {
                    $braceDepth--
                }
            }

            if ($opened -and $braceDepth -le 0) {
                $endLine = $j
                break
            }
        }

        $ranges.Add([pscustomobject]@{
            Name = $typeName
            StartLine = $i + 1
            EndLine = $endLine + 1
            HasResponsibility = Test-HasTypeResponsibilityContext -Lines $Lines -LineIndex $i
        })
    }

    return $ranges
}

function Get-ChangedNewLineNumbers {
    param(
        [string]$GitPath,
        [string]$RelativePath
    )

    $changedLineNumbers = [System.Collections.Generic.HashSet[int]]::new()
    $diffLines = @(& $GitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false diff --unified=0 --no-ext-diff -- $RelativePath 2>$null)
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    $newLineNumber = 0
    foreach ($diffLine in $diffLines) {
        if ($diffLine -match '^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@') {
            $newLineNumber = [int]$Matches[1]
            continue
        }

        if ($newLineNumber -le 0) {
            continue
        }

        if ($diffLine.StartsWith('+++')) {
            continue
        }

        if ($diffLine.StartsWith('+')) {
            $contentLine = $diffLine.Substring(1)
            if (-not [string]::IsNullOrWhiteSpace($contentLine)) {
                [void]$changedLineNumbers.Add($newLineNumber)
            }

            $newLineNumber++
            continue
        }

        if ($diffLine.StartsWith('-')) {
            continue
        }

        $newLineNumber++
    }

    return ,$changedLineNumbers
}

function Test-TypeResponsibilityComments {
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    $gitPath = $null
    if ($null -ne $gitCommand) {
        $gitPath = $gitCommand.Source
    } else {
        $visualStudioGitPath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
        if (Test-Path -LiteralPath $visualStudioGitPath) {
            $gitPath = $visualStudioGitPath
        }
    }

    if ([string]::IsNullOrWhiteSpace($gitPath)) {
        Add-Finding Warning 'TypeResponsibility' 'git diff' 'Git is not available; changed type responsibility comments could not be verified.'
        return
    }

    $typeDeclarationPattern = '^\s*(?:\[[^\]]+\]\s*)*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|unsafe|new)\s+)*(?:class|interface|struct|record(?:\s+struct|\s+class)?)\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)\b'
    $statusLines = @(& $gitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false diff --name-status --diff-filter=ACMRT -- '*.cs' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Add-Finding Warning 'TypeResponsibility' 'git diff' 'Git diff failed; changed type responsibility comments could not be verified.'
        return
    }

    if ($statusLines.Count -eq 0) {
        Add-Finding Info 'TypeResponsibility' 'git diff' 'No changed C# files were found for responsibility-comment verification.'
        return
    }

    $changedFiles = New-Object System.Collections.Generic.List[string]
    foreach ($statusLine in $statusLines) {
        if ([string]::IsNullOrWhiteSpace($statusLine)) {
            continue
        }

        $parts = $statusLine -split "`t"
        $path = $parts[$parts.Length - 1]
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $absolutePath = Join-Path $script:ProjectRoot ($path.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if (Test-Path -LiteralPath $absolutePath) {
            $changedFiles.Add($path)
        }
    }

    $addedDeclarationCount = 0
    $missingAddedContextCount = 0
    foreach ($relativePath in $changedFiles) {
        $absolutePath = Join-Path $script:ProjectRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $fileLines = @(Get-Content -LiteralPath $absolutePath)
        $diffLines = @(& $gitPath -C $script:ProjectRoot -c core.autocrlf=false -c core.safecrlf=false diff --unified=12 --no-ext-diff -- $relativePath 2>$null)
        if ($LASTEXITCODE -ne 0) {
            Add-Finding Warning 'TypeResponsibility' $relativePath 'Git diff failed for this file; added type responsibility comments could not be verified.'
            continue
        }

        $newLineNumber = 0
        foreach ($diffLine in $diffLines) {
            if ($diffLine -match '^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@') {
                $newLineNumber = [int]$Matches[1]
                continue
            }

            if ($newLineNumber -le 0) {
                continue
            }

            if ($diffLine.StartsWith('+++')) {
                continue
            }

            if ($diffLine.StartsWith('+')) {
                $contentLine = $diffLine.Substring(1)
                if ($contentLine -match $typeDeclarationPattern) {
                    $addedDeclarationCount++
                    $lineIndex = [Math]::Max(0, $newLineNumber - 1)
                    if (-not (Test-HasTypeResponsibilityContext -Lines $fileLines -LineIndex $lineIndex)) {
                        $missingAddedContextCount++
                        Add-Finding Error 'TypeResponsibility' "${relativePath}:$newLineNumber" 'Added or changed type declaration is missing a nearby responsibility comment.'
                    }
                }

                $newLineNumber++
                continue
            }

            if ($diffLine.StartsWith('-')) {
                continue
            }

            $newLineNumber++
        }
    }

    if ($missingAddedContextCount -eq 0) {
        Add-Finding Info 'TypeResponsibility' 'git diff' "Added/changed type declaration responsibility scan passed. Changed files=$($changedFiles.Count); added declarations=$addedDeclarationCount"
    }

    $touchedTypeCount = 0
    $missingTouchedContextCount = 0
    foreach ($relativePath in $changedFiles) {
        $absolutePath = Join-Path $script:ProjectRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $fileLines = @(Get-Content -LiteralPath $absolutePath)
        $typeRanges = @(Get-TypeDeclarationRanges -Lines $fileLines -TypeDeclarationPattern $typeDeclarationPattern)
        if ($typeRanges.Count -eq 0) {
            continue
        }

        $changedLineNumbers = Get-ChangedNewLineNumbers -GitPath $gitPath -RelativePath $relativePath
        if ($null -eq $changedLineNumbers) {
            Add-Finding Warning 'TypeResponsibility' $relativePath 'Git diff failed for this file; touched type responsibility comments could not be verified.'
            continue
        }

        foreach ($typeRange in $typeRanges) {
            $isTouched = $false
            foreach ($lineNumber in $changedLineNumbers) {
                if ($lineNumber -ge $typeRange.StartLine -and $lineNumber -le $typeRange.EndLine) {
                    $isTouched = $true
                    break
                }
            }

            if (-not $isTouched) {
                continue
            }

            $touchedTypeCount++
            if ($typeRange.HasResponsibility) {
                continue
            }

            $missingTouchedContextCount++
            Add-Finding Error 'TypeResponsibility' "${relativePath}:$($typeRange.StartLine)" "Touched type is missing a nearby responsibility comment: $($typeRange.Name)"
        }
    }

    if ($missingTouchedContextCount -eq 0) {
        Add-Finding Info 'TypeResponsibility' 'git diff' "Touched type responsibility scan passed. Touched declarations=$touchedTypeCount"
    }

    $legacyMissing = New-Object System.Collections.Generic.List[string]
    $changedTypeDeclarationCount = 0
    foreach ($relativePath in $changedFiles) {
        $absolutePath = Join-Path $script:ProjectRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $fileLines = @(Get-Content -LiteralPath $absolutePath)
        for ($i = 0; $i -lt $fileLines.Count; $i++) {
            if ($fileLines[$i] -notmatch $typeDeclarationPattern) {
                continue
            }

            $changedTypeDeclarationCount++
            if (Test-HasTypeResponsibilityContext -Lines $fileLines -LineIndex $i) {
                continue
            }

            $legacyMissing.Add("${relativePath}:$($i + 1)")
        }
    }

    if ($legacyMissing.Count -eq 0) {
        Add-Finding Info 'TypeResponsibility' 'git diff' "All type declarations in changed C# files have nearby responsibility comments. Declarations=$changedTypeDeclarationCount"
    } else {
        $sample = ($legacyMissing | Select-Object -First 20) -join ', '
        Add-Finding Warning 'TypeResponsibility' 'git diff' "Advisory: type declarations in changed C# files without nearby responsibility comments=$($legacyMissing.Count) of $changedTypeDeclarationCount. Some may be legacy declarations not edited in this migration slice. Sample: $sample"
    }
}

function Test-UnguardedRuntimeUnityEditor {
    $runtimeRoot = Join-Path $script:ProjectRoot 'Assets\_Project\Runtime'
    $unityEditorMatches = New-Object System.Collections.Generic.List[string]
    $guardedUnityEditorMatches = New-Object System.Collections.Generic.List[string]
    $guardedFiles = @{}
    $editorConditionalMatches = New-Object System.Collections.Generic.List[string]
    $editorConditionalFiles = @{}
    $editorConditionalOnlyFiles = New-Object System.Collections.Generic.List[string]
    $editorPathMatches = New-Object System.Collections.Generic.List[string]
    $editorApiMatches = New-Object System.Collections.Generic.List[string]
    $editorApiNames = @(
        'EditorWindow',
        'EditorGUILayout',
        'EditorGUI',
        'CustomEditor',
        'MenuItem',
        'AssetDatabase',
        'PrefabUtility',
        'SerializedObject',
        'SerializedProperty',
        'EditorApplication',
        'EditorUtility',
        'SceneView',
        'Handles'
    )

    Get-ChildItem -LiteralPath $runtimeRoot -Recurse -Filter '*.cs' -File | ForEach-Object {
        $relativeFile = Get-RelativePath $_.FullName
        $normalizedRelativeFile = ConvertTo-ProjectSlashPath $relativeFile
        if ($normalizedRelativeFile -match '/Editor/' -or $normalizedRelativeFile -match 'Editor\.cs$') {
            $editorPathMatches.Add($relativeFile)
        }

        $sourceText = Remove-CSharpTrivia -Text (Get-Content -LiteralPath $_.FullName -Raw)
        foreach ($apiName in $editorApiNames) {
            if ([regex]::IsMatch($sourceText, '\b' + [regex]::Escape($apiName) + '\b')) {
                $editorApiMatches.Add("${relativeFile}:$apiName")
            }
        }

        $sourceLines = Get-Content -LiteralPath $_.FullName
        if (Test-FullyUnityEditorConditionalSource -Lines $sourceLines) {
            $editorConditionalOnlyFiles.Add($relativeFile)
        }

        $editorDepth = 0
        $lineNumber = 0
        foreach ($line in $sourceLines) {
            $lineNumber++
            $trimmed = $line.Trim()
            if ($trimmed -match '^#if\s+.*UNITY_EDITOR') {
                $editorConditionalMatches.Add("${relativeFile}:$lineNumber")
                $editorConditionalFiles[$relativeFile] = $true
                $editorDepth++
                continue
            }

            if ($trimmed -match '^#endif' -and $editorDepth -gt 0) {
                $editorDepth--
                continue
            }

            if ($line -notmatch 'UnityEditor') {
                continue
            }

            if ($editorDepth -eq 0) {
                $unityEditorMatches.Add("${relativeFile}:$lineNumber")
                continue
            }

            $guardedUnityEditorMatches.Add("${relativeFile}:$lineNumber")
            $guardedFiles[$relativeFile] = $true
        }
    }

    foreach ($path in $editorPathMatches) {
        Add-Finding Error 'UnityEditor' $path 'Runtime source is under an Editor path/name and should live in the Editor assembly.'
    }

    foreach ($path in $editorApiMatches) {
        Add-Finding Error 'UnityEditor' $path 'Runtime source references a known UnityEditor API surface after removing comments and string literals.'
    }

    if ($unityEditorMatches.Count -eq 0 -and $editorPathMatches.Count -eq 0 -and $editorApiMatches.Count -eq 0) {
        Add-Finding Info 'UnityEditor' 'Assets/_Project/Runtime' 'No runtime Editor source paths, known UnityEditor API surface references, or unguarded UnityEditor references were found.'
        if ($editorConditionalMatches.Count -gt 0) {
            Add-Finding Info 'RuntimeEditorConditional' 'Assets/_Project/Runtime' "Runtime UNITY_EDITOR conditionals remain without known UnityEditor API surface references. Files=$($editorConditionalFiles.Count); Occurrences=$($editorConditionalMatches.Count)"
        }

        if ($editorConditionalOnlyFiles.Count -gt 0) {
            $sample = ($editorConditionalOnlyFiles | Select-Object -First 10) -join ', '
            Add-Finding Info 'RuntimeEditorConditionalOnlySource' 'Assets/_Project/Runtime' "Runtime source files fully wrapped in UNITY_EDITOR remain in runtime asmdef roots. Files=$($editorConditionalOnlyFiles.Count); Sample=$sample"
        } else {
            Add-Finding Info 'RuntimeEditorConditionalOnlySource' 'Assets/_Project/Runtime' 'No runtime source files are fully wrapped in UNITY_EDITOR under runtime asmdef roots.'
        }

        if ($guardedUnityEditorMatches.Count -gt 0) {
            Add-Finding Info 'UnityEditor' 'Assets/_Project/Runtime' "Guarded runtime UnityEditor references remain behind UNITY_EDITOR. Files=$($guardedFiles.Count); Occurrences=$($guardedUnityEditorMatches.Count)"
        }
        return
    }

    foreach ($path in $unityEditorMatches) {
        Add-Finding Error 'UnityEditor' $path 'Runtime source references UnityEditor outside a UNITY_EDITOR guard.'
    }
}

function Test-FullyUnityEditorConditionalSource {
    param([string[]]$Lines)

    if ($null -eq $Lines -or $Lines.Count -eq 0) {
        return $false
    }

    $firstIndex = -1
    $lastIndex = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ([string]::IsNullOrWhiteSpace($Lines[$i])) {
            continue
        }

        if ($firstIndex -lt 0) {
            $firstIndex = $i
        }
        $lastIndex = $i
    }

    if ($firstIndex -lt 0 -or $lastIndex -lt 0) {
        return $false
    }

    if ($Lines[$firstIndex].Trim() -notmatch '^#if\s+.*\bUNITY_EDITOR\b') {
        return $false
    }

    if ($Lines[$lastIndex].Trim() -notmatch '^#endif\b') {
        return $false
    }

    $depth = 0
    for ($i = $firstIndex; $i -le $lastIndex; $i++) {
        $trimmed = $Lines[$i].Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed -match '^#if\b') {
            $depth++
            continue
        }

        if ($trimmed -match '^#endif\b') {
            $depth--
            if ($depth -lt 0) {
                return $false
            }
            continue
        }

        if ($trimmed -match '^#else\b' -or $trimmed -match '^#elif\b') {
            if ($depth -eq 1) {
                return $false
            }
            continue
        }

        if ($depth -eq 0) {
            return $false
        }
    }

    return $depth -eq 0
}

function Test-SerializedReferences {
    $scanRoots = @('Assets/_Project', 'Assets/AddressableAssetsData', 'ProjectSettings')
    $serializedExtensions = [System.Collections.Generic.HashSet[string]]::new([string[]]@('.asset', '.prefab', '.unity', '.xml', '.controller', '.overrideController'))

    $guidMap = Get-MetaGuidMap
    $fileCount = 0
    $filesByExtension = @{}
    $unityEventAssemblyCSharpCount = 0
    $nonCacheAssemblyCSharpCount = 0
    $safeMissingScriptCount = 0
    $knownPackageMissingScriptCount = 0
    $unknownMissingScriptCount = 0
    $nonScriptGuidTargetCount = 0
    $missingAssetReferenceCount = 0
    $preExistingMissingAssetReferenceCount = 0
    $managedReferenceProblemCount = 0

    foreach ($root in $scanRoots) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File | Where-Object {
            $serializedExtensions.Contains($_.Extension)
        } | ForEach-Object {
            $fileCount++
            $extension = $_.Extension.ToLowerInvariant()
            if (-not $filesByExtension.ContainsKey($extension)) {
                $filesByExtension[$extension] = 0
            }

            $filesByExtension[$extension]++
            $lineNumber = 0
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                $lineNumber++
                if ($line -match 'm_TargetAssemblyTypeName: (.*Assembly-CSharp.*)$') {
                    $unityEventAssemblyCSharpCount++
                    $target = $Matches[1].Trim()
                    if ($safeUnityEventTargets -contains $target) {
                        Add-Finding Warning 'SerializedReference' "$(Get-RelativePath $_.FullName):$lineNumber" "UnityEvent target assembly has a known safe migration candidate: $target"
                    } else {
                        Add-Finding Error 'SerializedReference' "$(Get-RelativePath $_.FullName):$lineNumber" "UnityEvent target assembly still points at Assembly-CSharp: $target"
                    }
                }

                if ($line -match 'Assembly-CSharp' -and
                    $line -notmatch 'm_TargetAssemblyTypeName:' -and
                    $line -notmatch 'm_EditorClassIdentifier:') {
                    $nonCacheAssemblyCSharpCount++
                    Add-Finding Error 'SerializedReference' "$(Get-RelativePath $_.FullName):$lineNumber" "Serialized data still contains Assembly-CSharp outside supported UnityEvent/editor-cache fields."
                }

                if ($line -match 'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}') {
                    $guid = $Matches[1]
                    if ($guidMap.ContainsKey($guid)) {
                        $resolvedMetaPath = [string]$guidMap[$guid]
                        if (-not $resolvedMetaPath.EndsWith('.cs.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
                            $nonScriptGuidTargetCount++
                            Add-Finding Error 'MissingScript' "$(Get-RelativePath $_.FullName):$lineNumber" "m_Script GUID resolves to a non-C# asset meta file. Guid=$guid Meta=$(Get-RelativePath $resolvedMetaPath)"
                        }

                        continue
                    }

                    if ($safeScriptGuidReplacements.ContainsKey($guid)) {
                        $safeMissingScriptCount++
                        Add-Finding Warning 'MissingScript' "$(Get-RelativePath $_.FullName):$lineNumber" "Missing script GUID has a known safe replacement: $guid"
                    } elseif ($knownPackageMissingScriptGuids.ContainsKey($guid)) {
                        $knownPackageMissingScriptCount++
                        Add-Finding Warning 'MissingScript' "$(Get-RelativePath $_.FullName):$lineNumber" "Missing script GUID belongs to missing package content: $($knownPackageMissingScriptGuids[$guid])"
                    } else {
                        $unknownMissingScriptCount++
                        Add-Finding Error 'MissingScript' "$(Get-RelativePath $_.FullName):$lineNumber" "Missing m_Script GUID: $guid"
                    }
                }

                if ($line -notmatch 'm_Script:' -and $line -match 'guid:\s*([0-9a-f]{32}),\s*type:\s*3') {
                    foreach ($guidMatch in [regex]::Matches($line, 'guid:\s*([0-9a-f]{32}),\s*type:\s*3')) {
                        $guid = $guidMatch.Groups[1].Value
                        if ($guid -eq '00000000000000000000000000000000') {
                            continue
                        }

                        if (-not $guidMap.ContainsKey($guid)) {
                            $relativePath = Get-RelativePath $_.FullName
                            if (Test-SerializedAssetReferenceExistedInHead -RelativePath $relativePath -Guid $guid) {
                                $preExistingMissingAssetReferenceCount++
                                Add-Finding Info 'PreExistingMissingAssetReference' "$relativePath`:$lineNumber" "Serialized asset reference GUID is missing from Assets, Packages, and Library/PackageCache metadata, but the same GUID reference already existed in HEAD for this file. Guid=$guid"
                            } else {
                                $missingAssetReferenceCount++
                                Add-Finding Error 'MissingAssetReference' "$relativePath`:$lineNumber" "Serialized asset reference GUID is missing from Assets, Packages, and Library/PackageCache metadata. Guid=$guid"
                            }
                        }
                    }
                }

                if ($line -match '^\s*(m_HasMissingTypeInManagedRef|m_BlackboardMissingManagedRef|m_GraphMissingManagedRef|m_WasCompileWithPlaceholderNode|IsPlaceholder):\s*1\s*$') {
                    $managedReferenceProblemCount++
                    Add-Finding Error 'SerializedReference' "$(Get-RelativePath $_.FullName):$lineNumber" "Serialized managed-reference integrity flag is set: $($Matches[1])"
                }
            }
        }
    }

    $extensionSummary = ($filesByExtension.Keys | Sort-Object | ForEach-Object { "$_=$($filesByExtension[$_])" }) -join ', '
    Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' "Serialized scan covered $fileCount files. $extensionSummary"

    if ($unityEventAssemblyCSharpCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' 'No UnityEvent m_TargetAssemblyTypeName references to Assembly-CSharp were found.'
    }

    if ($nonCacheAssemblyCSharpCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' 'No non-cache serialized Assembly-CSharp strings were found.'
    }

    if ($unknownMissingScriptCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' "No unknown missing m_Script GUIDs were found. Known-package missing scripts=$knownPackageMissingScriptCount; known-safe replacements remaining=$safeMissingScriptCount."
    }

    if ($nonScriptGuidTargetCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' 'All resolved m_Script GUIDs point to C# MonoScript meta files.'
    }

    if ($missingAssetReferenceCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' "No newly introduced missing serialized asset reference GUIDs were found. PreExistingMissingAssetReferences=$preExistingMissingAssetReferenceCount"
    }

    if ($managedReferenceProblemCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSummary' 'Assets/_Project; Assets/AddressableAssetsData; ProjectSettings' 'No serialized managed-reference missing-type or placeholder flags were found.'
    }
}

function Test-SecondarySerializedAssemblyCSharpResiduals {
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $assetsRoot)) {
        return
    }

    $primaryScanRoots = @('Assets/_Project', 'Assets/AddressableAssetsData') |
        ForEach-Object {
            $path = Join-Path $script:ProjectRoot $_
            if (Test-Path -LiteralPath $path) {
                [System.IO.Path]::GetFullPath($path).TrimEnd('\', '/')
            }
        }

    $serializedExtensions = [System.Collections.Generic.HashSet[string]]::new([string[]]@('.asset', '.prefab', '.unity', '.xml', '.controller', '.overrideController'))
    $filesWithResiduals = New-Object System.Collections.Generic.List[string]
    $filesWithNonCacheResiduals = New-Object System.Collections.Generic.List[string]
    $occurrenceCount = 0
    $editorClassIdentifierCount = 0
    $unityEventTargetCount = 0
    $otherSerializedCount = 0
    $secondaryUnityEventTargetLines = New-Object System.Collections.Generic.List[string]

    Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $serializedExtensions.Contains($_.Extension)
    } | ForEach-Object {
        $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
        foreach ($primaryRoot in $primaryScanRoots) {
            if ($fullPath.StartsWith($primaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                return
            }
        }

        $fileOccurrenceCount = 0
        $fileNonCacheCount = 0
        foreach ($match in Select-String -LiteralPath $fullPath -Pattern 'Assembly-CSharp' -ErrorAction SilentlyContinue) {
            $fileOccurrenceCount += $match.Matches.Count

            $line = $match.Line
            if ($line -match 'm_EditorClassIdentifier:\s*(Assembly-CSharp(?:-Editor)?)(?:::|$)') {
                $editorClassIdentifierCount += $match.Matches.Count
            }
            elseif ($line -match 'm_TargetAssemblyTypeName: .*Assembly-CSharp') {
                $unityEventTargetCount += $match.Matches.Count
                $fileNonCacheCount += $match.Matches.Count
                $secondaryUnityEventTargetLines.Add("$(Get-RelativePath $fullPath):$($match.LineNumber): $($line.Trim())")
            }
            else {
                $otherSerializedCount += $match.Matches.Count
                $fileNonCacheCount += $match.Matches.Count
            }
        }

        if ($fileOccurrenceCount -eq 0) {
            return
        }

        $occurrenceCount += $fileOccurrenceCount
        $relativePath = Get-RelativePath $fullPath
        $filesWithResiduals.Add($relativePath)
        if ($fileNonCacheCount -gt 0) {
            $filesWithNonCacheResiduals.Add($relativePath)
        }
    }

    if ($filesWithResiduals.Count -eq 0) {
        Add-Finding Info 'SerializedReferenceSecondaryScope' 'Assets' 'No Assembly-CSharp serialized strings were found outside the primary serialized scan roots.'
        return
    }

    $sample = ($filesWithResiduals | Sort-Object | Select-Object -First 20) -join ', '
    $nonCacheSample = ($filesWithNonCacheResiduals | Sort-Object | Select-Object -First 10) -join ', '
    $message = "Assembly-CSharp serialized strings remain outside primary scan roots. Files=$($filesWithResiduals.Count); Occurrences=$occurrenceCount; EditorClassIdentifierCache=$editorClassIdentifierCount; UnityEventTargets=$unityEventTargetCount; OtherSerialized=$otherSerializedCount; Sample=$sample"
    if (-not [string]::IsNullOrWhiteSpace($nonCacheSample)) {
        $message += "; NonCacheSample=$nonCacheSample"
    }

    Add-Finding Warning 'SerializedReferenceSecondaryScope' 'Assets' $message

    foreach ($targetLine in ($secondaryUnityEventTargetLines | Sort-Object | Select-Object -First 20)) {
        Add-Finding Warning 'SerializedReferenceSecondaryUnityEvent' $targetLine 'Secondary-scope UnityEvent target assembly still points at Assembly-CSharp. Migrate only after confirming the root/recovery asset should be kept.'
    }
}

function Test-SecondarySerializedScriptReferences {
    $assetsRoot = Join-Path $script:ProjectRoot 'Assets'
    if (-not (Test-Path -LiteralPath $assetsRoot)) {
        return
    }

    $primaryScanRoots = @('Assets/_Project', 'Assets/AddressableAssetsData') |
        ForEach-Object {
            $path = Join-Path $script:ProjectRoot $_
            if (Test-Path -LiteralPath $path) {
                [System.IO.Path]::GetFullPath($path).TrimEnd('\', '/')
            }
        }

    $guidMap = Get-MetaGuidMap
    $serializedExtensions = [System.Collections.Generic.HashSet[string]]::new([string[]]@('.asset', '.prefab', '.unity', '.xml', '.controller', '.overrideController'))
    $fileCount = 0
    $missingScriptCount = 0
    $nonScriptGuidTargetCount = 0
    $scriptReferenceCount = 0

    Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $serializedExtensions.Contains($_.Extension)
    } | ForEach-Object {
        $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
        foreach ($primaryRoot in $primaryScanRoots) {
            if ($fullPath.StartsWith($primaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                return
            }
        }

        $fileCount++
        $lineNumber = 0
        foreach ($line in Get-Content -LiteralPath $fullPath) {
            $lineNumber++
            if ($line -notmatch 'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}') {
                continue
            }

            $scriptReferenceCount++
            $guid = $Matches[1]
            if (-not $guidMap.ContainsKey($guid)) {
                $missingScriptCount++
                Add-Finding Warning 'SerializedReferenceSecondaryMissingScript' "$(Get-RelativePath $fullPath):$lineNumber" "Secondary-scope m_Script GUID is missing from Assets/Packages/Library metadata: $guid"
                continue
            }

            $resolvedMetaPath = [string]$guidMap[$guid]
            if (-not $resolvedMetaPath.EndsWith('.cs.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
                $nonScriptGuidTargetCount++
                Add-Finding Warning 'SerializedReferenceSecondaryMissingScript' "$(Get-RelativePath $fullPath):$lineNumber" "Secondary-scope m_Script GUID resolves to a non-C# asset meta file. Guid=$guid Meta=$(Get-RelativePath $resolvedMetaPath)"
            }
        }
    }

    if ($missingScriptCount -eq 0 -and $nonScriptGuidTargetCount -eq 0) {
        Add-Finding Info 'SerializedReferenceSecondaryMissingScript' 'Assets' "No missing or non-C# m_Script GUID references were found outside the primary serialized scan roots. Files=$fileCount; ScriptReferences=$scriptReferenceCount"
    }
}

function Test-SecondaryResidualReferenceUse {
    $scanRoots = @('Assets', 'ProjectSettings', 'Packages')
    $textExtensions = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
        '.asset',
        '.prefab',
        '.unity',
        '.xml',
        '.controller',
        '.overrideController',
        '.meta',
        '.json',
        '.asmdef',
        '.asmref'
    ))

    $scanFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    foreach ($root in $scanRoots) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
            $textExtensions.Contains($_.Extension)
        } | ForEach-Object {
            $scanFiles.Add($_)
        }
    }

    $globalCopyPrefabPath = Join-Path $script:ProjectRoot 'Assets\GlobalUIRoot Copy.prefab'
    $globalCopyPrefabMetaPath = "$globalCopyPrefabPath.meta"
    $globalCopyGuid = $null
    if (Test-Path -LiteralPath $globalCopyPrefabMetaPath) {
        $globalCopyGuid = Get-MetaGuid -MetaPath $globalCopyPrefabMetaPath
    }

    $globalCopyReferenceHits = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($globalCopyGuid)) {
        foreach ($file in $scanFiles) {
            $fullPath = [System.IO.Path]::GetFullPath($file.FullName)
            if ([System.String]::Equals($fullPath, [System.IO.Path]::GetFullPath($globalCopyPrefabPath), [System.StringComparison]::OrdinalIgnoreCase) -or
                [System.String]::Equals($fullPath, [System.IO.Path]::GetFullPath($globalCopyPrefabMetaPath), [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $matches = @(Select-String -LiteralPath $fullPath -SimpleMatch -Pattern $globalCopyGuid, 'GlobalUIRoot Copy' -ErrorAction SilentlyContinue)
            foreach ($match in $matches) {
                $globalCopyReferenceHits.Add("$(Get-RelativePath $fullPath):$($match.LineNumber): $($match.Line.Trim())")
            }
        }
    }

    if ($globalCopyReferenceHits.Count -eq 0) {
        Add-Finding Info 'SecondaryResidualReferenceUse' 'Assets/GlobalUIRoot Copy.prefab' 'No references to the root GlobalUIRoot Copy prefab GUID or path/name were found outside its own prefab/meta files.'
    } else {
        $sample = ($globalCopyReferenceHits | Sort-Object | Select-Object -First 10) -join '; '
        Add-Finding Warning 'SecondaryResidualReferenceUse' 'Assets/GlobalUIRoot Copy.prefab' "Root GlobalUIRoot Copy prefab appears to be referenced outside its own files. Hits=$($globalCopyReferenceHits.Count); Sample=$sample"
    }

    $recoveryRoot = Join-Path $script:ProjectRoot 'Assets\_Recovery'
    if (-not (Test-Path -LiteralPath $recoveryRoot)) {
        Add-Finding Info 'SecondaryResidualReferenceUse' 'Assets/_Recovery' 'Recovery folder is absent.'
        return
    }

    $recoveryRootFullPath = [System.IO.Path]::GetFullPath($recoveryRoot).TrimEnd('\', '/')
    $recoveryGuids = New-Object System.Collections.Generic.List[string]
    Get-ChildItem -LiteralPath $recoveryRoot -Recurse -Filter '*.unity.meta' -File -ErrorAction SilentlyContinue | ForEach-Object {
        $guid = Get-MetaGuid -MetaPath $_.FullName
        if (-not [string]::IsNullOrWhiteSpace($guid)) {
            $recoveryGuids.Add($guid)
        }
    }

    $recoveryReferenceHits = New-Object System.Collections.Generic.List[string]
    $recoveryPathPatterns = @('Assets/_Recovery', 'Assets\_Recovery', '_Recovery/')
    foreach ($file in $scanFiles) {
        $fullPath = [System.IO.Path]::GetFullPath($file.FullName)
        if ($fullPath.StartsWith($recoveryRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $patterns = New-Object System.Collections.Generic.List[string]
        foreach ($guid in $recoveryGuids) {
            $patterns.Add($guid)
        }

        foreach ($pattern in $recoveryPathPatterns) {
            $patterns.Add($pattern)
        }

        if ($patterns.Count -eq 0) {
            continue
        }

        $matches = @(Select-String -LiteralPath $fullPath -SimpleMatch -Pattern $patterns.ToArray() -ErrorAction SilentlyContinue)
        foreach ($match in $matches) {
            $recoveryReferenceHits.Add("$(Get-RelativePath $fullPath):$($match.LineNumber): $($match.Line.Trim())")
        }
    }

    if ($recoveryReferenceHits.Count -eq 0) {
        Add-Finding Info 'SecondaryResidualReferenceUse' 'Assets/_Recovery' "No references to recovery scene GUIDs or paths were found outside Assets/_Recovery. RecoverySceneGuids=$($recoveryGuids.Count)"
    } else {
        $sample = ($recoveryReferenceHits | Sort-Object | Select-Object -First 10) -join '; '
        Add-Finding Warning 'SecondaryResidualReferenceUse' 'Assets/_Recovery' "Recovery scenes appear to be referenced outside Assets/_Recovery. Hits=$($recoveryReferenceHits.Count); Sample=$sample"
    }
}

function Test-RecoveryAssemblyCSharpCacheOnly {
    $recoveryRoot = Join-Path $script:ProjectRoot 'Assets\_Recovery'
    if (-not (Test-Path -LiteralPath $recoveryRoot)) {
        return
    }

    $fileCount = 0
    $assemblyCSharpCount = 0
    $editorClassIdentifierCount = 0
    $nonCacheCount = 0
    $nonCacheSamples = New-Object System.Collections.Generic.List[string]

    Get-ChildItem -LiteralPath $recoveryRoot -Filter '*.unity' -File -ErrorAction SilentlyContinue | ForEach-Object {
        $fileCount++
        foreach ($match in Select-String -LiteralPath $_.FullName -Pattern 'Assembly-CSharp' -ErrorAction SilentlyContinue) {
            $assemblyCSharpCount += $match.Matches.Count
            if ($match.Line -match 'm_EditorClassIdentifier:\s*(Assembly-CSharp(?:-Editor)?)(?:::|$)') {
                $editorClassIdentifierCount += $match.Matches.Count
                continue
            }

            $nonCacheCount += $match.Matches.Count
            $nonCacheSamples.Add("$(Get-RelativePath $_.FullName):$($match.LineNumber): $($match.Line.Trim())")
        }
    }

    if ($nonCacheCount -eq 0) {
        Add-Finding Info 'SecondaryRecoveryCacheOnly' 'Assets/_Recovery' "Recovery scene Assembly-CSharp strings are editor class identifier cache only. Files=$fileCount; Occurrences=$assemblyCSharpCount; EditorClassIdentifierCache=$editorClassIdentifierCount"
    } else {
        $sample = ($nonCacheSamples | Sort-Object | Select-Object -First 10) -join '; '
        Add-Finding Warning 'SecondaryRecoveryCacheOnly' 'Assets/_Recovery' "Recovery scenes contain non-cache Assembly-CSharp serialized strings. NonCache=$nonCacheCount; Sample=$sample"
    }
}

function Test-VisualScriptingResidualCleanupReadiness {
    $manifestPath = Join-Path $script:ProjectRoot 'Packages\manifest.json'
    $packageInstalled = $false
    if (Test-Path -LiteralPath $manifestPath) {
        $packageInstalled = (Get-Content -LiteralPath $manifestPath -Raw) -match '"com\.unity\.visualscripting"'
    }

    $graphAssetPaths = @(
        'Assets/_Project/Data/VisualScripting/Graphs/Hazard.asset',
        'Assets/_Project/Data/VisualScripting/Graphs/Input Movement.asset',
        'Assets/_Project/Data/VisualScripting/Graphs/Scale Wave.asset'
    )

    $scanRoots = @('Assets/_Project', 'Assets/AddressableAssetsData', 'ProjectSettings')
    $textExtensions = [System.Collections.Generic.HashSet[string]]::new([string[]]@('.asset', '.prefab', '.unity', '.meta', '.controller', '.overrideController', '.xml'))
    $scanFiles = New-Object System.Collections.Generic.List[string]
    foreach ($root in $scanRoots) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
            $textExtensions.Contains($_.Extension)
        } | ForEach-Object {
            $scanFiles.Add($_.FullName)
        }
    }

    $existingGraphAssets = 0
    $unreferencedGraphAssets = 0
    $referencedGraphAssets = 0
    $unreferencedGraphAssetPaths = New-Object System.Collections.Generic.List[string]
    foreach ($graphAssetPath in $graphAssetPaths) {
        $absoluteGraphPath = Join-Path $script:ProjectRoot ($graphAssetPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $absoluteGraphMetaPath = "$absoluteGraphPath.meta"
        if (-not (Test-Path -LiteralPath $absoluteGraphPath) -or -not (Test-Path -LiteralPath $absoluteGraphMetaPath)) {
            continue
        }

        $existingGraphAssets++
        $guid = Get-MetaGuid -MetaPath $absoluteGraphMetaPath
        if ([string]::IsNullOrWhiteSpace($guid)) {
            Add-Finding Warning 'VisualScriptingResidualReadiness' (ConvertTo-ProjectSlashPath $graphAssetPath) 'Visual Scripting graph asset meta has no GUID; cleanup readiness cannot be proven.'
            continue
        }

        $externalReferenceCount = 0
        foreach ($scanFile in $scanFiles) {
            $relativeScanFile = ConvertTo-ProjectSlashPath (Get-RelativePath $scanFile)
            if ($relativeScanFile -eq $graphAssetPath -or $relativeScanFile -eq "$graphAssetPath.meta") {
                continue
            }

            if ((Get-Content -LiteralPath $scanFile -Raw -ErrorAction SilentlyContinue) -match [regex]::Escape($guid)) {
                $externalReferenceCount++
            }
        }

        if ($externalReferenceCount -eq 0) {
            $unreferencedGraphAssets++
            $unreferencedGraphAssetPaths.Add($graphAssetPath)
        } else {
            $referencedGraphAssets++
            Add-Finding Warning 'VisualScriptingResidualReadiness' (ConvertTo-ProjectSlashPath $graphAssetPath) "Visual Scripting graph asset is still externally referenced. Guid=$guid References=$externalReferenceCount"
        }
    }

    $projectSettingsPath = Join-Path $script:ProjectRoot 'ProjectSettings\VisualScriptingSettings.asset'
    $projectSettingsExists = Test-Path -LiteralPath $projectSettingsPath
    $pixelLightScenePath = Join-Path $script:ProjectRoot 'Assets\_Project\Scenes\PixelLightTest.unity'
    $pixelLightMissingVisualScriptingComponents = 0
    if (Test-Path -LiteralPath $pixelLightScenePath) {
        $sceneText = Get-Content -LiteralPath $pixelLightScenePath -Raw
        foreach ($guid in @('765181c9ef4b24d32a4f7cbd2ef370dc', 'e741851cba3ad425c91ecf922cc6b379')) {
            $pixelLightMissingVisualScriptingComponents += [regex]::Matches($sceneText, [regex]::Escape($guid)).Count
        }
    }

    if ($packageInstalled) {
        Add-Finding Warning 'VisualScriptingResidualReadiness' 'Packages/manifest.json' 'Visual Scripting package is installed; residual cleanup should not delete authored Visual Scripting assets.'
        return
    }

    if ($referencedGraphAssets -eq 0) {
        Add-Finding Info 'VisualScriptingResidualReadiness' 'Assets/_Project/Data/VisualScripting; ProjectSettings; PixelLightTest' "Visual Scripting residual cleanup preconditions are statically satisfied: PackageInstalled=False; ExistingGraphAssets=$existingGraphAssets; UnreferencedGraphAssets=$unreferencedGraphAssets; ProjectSettingsExists=$projectSettingsExists; PixelLightMissingComponents=$pixelLightMissingVisualScriptingComponents"

        $impactParts = New-Object System.Collections.Generic.List[string]
        if ($pixelLightMissingVisualScriptingComponents -gt 0) {
            $impactParts.Add("Modify=Assets/_Project/Scenes/PixelLightTest.unity")
        }

        if ($unreferencedGraphAssetPaths.Count -gt 0) {
            $graphImpact = ($unreferencedGraphAssetPaths | ForEach-Object { "$_;$_.meta" }) -join ', '
            $impactParts.Add("DeleteGraphAssets=$graphImpact")
        }

        if ($projectSettingsExists) {
            $impactParts.Add('Delete=ProjectSettings/VisualScriptingSettings.asset')
        }

        if ($impactParts.Count -gt 0) {
            Add-Finding Info 'VisualScriptingResidualCleanupImpact' 'Assets/_Project/Data/VisualScripting; ProjectSettings; PixelLightTest' ($impactParts -join '; ')
        }
    }
}

function Test-AddressableAssetEntries {
    $groupsRoot = Join-Path $script:ProjectRoot 'Assets\AddressableAssetsData\AssetGroups'
    if (-not (Test-Path -LiteralPath $groupsRoot)) {
        Add-Finding Error 'Addressables' 'Assets/AddressableAssetsData/AssetGroups' 'Addressables asset group folder is missing.'
        return
    }

    $guidMap = Get-MetaGuidMap
    $entryCount = 0
    $problemCount = 0
    $entryLocationsByGuid = @{}
    $groupFiles = @(Get-ChildItem -LiteralPath $groupsRoot -Filter '*.asset' -File -ErrorAction SilentlyContinue)

    foreach ($groupFile in $groupFiles) {
        $lines = @(Get-Content -LiteralPath $groupFile.FullName)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -notmatch '^\s*-\s*m_GUID:\s*([0-9a-f]{32})\s*$') {
                continue
            }

            $entryCount++
            $entryGuid = $Matches[1]
            $entryLineNumber = $i + 1
            $entryLocation = "$(Get-RelativePath $groupFile.FullName):$entryLineNumber"
            $entryAddress = $null

            if ($entryLocationsByGuid.ContainsKey($entryGuid)) {
                $problemCount++
                Add-Finding Error 'Addressables' $entryLocation "Duplicate Addressable entry GUID found. Guid=$entryGuid First=$($entryLocationsByGuid[$entryGuid])"
            } else {
                $entryLocationsByGuid[$entryGuid] = $entryLocation
            }

            for ($j = $i + 1; $j -lt $lines.Count; $j++) {
                if ($lines[$j] -match '^\s*-\s*m_GUID:') {
                    break
                }

                if ($lines[$j] -notmatch '^\s*m_Address:\s*(.*)$') {
                    continue
                }

                $entryAddress = $Matches[1].Trim()
                for ($k = $j + 1; $k -lt $lines.Count; $k++) {
                    if ($lines[$k] -match '^\s*-\s*m_GUID:' -or
                        $lines[$k] -match '^\s*m_(ReadOnly|SerializedLabels):' -or
                        $lines[$k] -match '^\s*FlaggedDuringContentUpdateRestriction:') {
                        break
                    }

                    if ($lines[$k] -match '^\s{6,}(.+)$') {
                        $entryAddress = "$entryAddress $($Matches[1].Trim())"
                    } else {
                        break
                    }
                }

                break
            }

            if (-not $guidMap.ContainsKey($entryGuid)) {
                $problemCount++
                Add-Finding Error 'Addressables' "$(Get-RelativePath $groupFile.FullName):$entryLineNumber" "Addressable entry GUID does not resolve to any asset meta: $entryGuid"
            }

            if ([string]::IsNullOrWhiteSpace($entryAddress)) {
                $problemCount++
                Add-Finding Warning 'Addressables' "$(Get-RelativePath $groupFile.FullName):$entryLineNumber" "Addressable entry has no m_Address field: $entryGuid"
                continue
            }

            if ($entryAddress -notmatch '^Assets/') {
                continue
            }

            $addressPathPart = $entryAddress.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            $assetPath = Join-Path $script:ProjectRoot $addressPathPart
            if (-not (Test-Path -LiteralPath $assetPath)) {
                $problemCount++
                Add-Finding Error 'Addressables' "$(Get-RelativePath $groupFile.FullName):$entryLineNumber" "Addressable entry address path is missing: $entryAddress"
                continue
            }

            $addressMetaPath = "$assetPath.meta"
            if (-not (Test-Path -LiteralPath $addressMetaPath)) {
                $problemCount++
                Add-Finding Error 'Addressables' "$(Get-RelativePath $groupFile.FullName):$entryLineNumber" "Addressable entry address has no .meta file: $entryAddress"
                continue
            }

            $addressMetaGuid = Get-MetaGuid -MetaPath $addressMetaPath
            if ($addressMetaGuid -ne $entryGuid) {
                $problemCount++
                Add-Finding Error 'Addressables' "$(Get-RelativePath $groupFile.FullName):$entryLineNumber" "Addressable entry GUID does not match address .meta GUID. Entry=$entryGuid Address=$entryAddress Meta=$addressMetaGuid"
            }
        }
    }

    if ($problemCount -eq 0) {
        Add-Finding Info 'Addressables' 'Assets/AddressableAssetsData/AssetGroups' "Addressable group entries resolve to existing asset GUIDs, have unique entry GUIDs, and match address .meta files. Entries=$entryCount; UniqueGuids=$($entryLocationsByGuid.Count)"
    }
}

function Test-AddressablesLinkXml {
    $linkXmlPath = Join-Path $script:ProjectRoot 'Assets\AddressableAssetsData\link.xml'
    $linkXmlMetaPath = "$linkXmlPath.meta"
    if (-not (Test-Path -LiteralPath $linkXmlPath)) {
        if (Test-Path -LiteralPath $linkXmlMetaPath) {
            Add-Finding Warning 'AddressablesLinkXml' 'Assets/AddressableAssetsData/link.xml.meta' 'link.xml is absent but link.xml.meta remains.'
            return
        }

        Add-Finding Info 'AddressablesLinkXml' 'Assets/AddressableAssetsData/link.xml' 'Addressables ConfigFolder link.xml is absent. This path is a temporary player-build copy deleted by Addressables on editor load; use Addressables build link.xml validation for the generated preserve output.'
        return
    }

    if (-not (Test-Path -LiteralPath $linkXmlMetaPath)) {
        Add-Finding Error 'AddressablesLinkXml' 'Assets/AddressableAssetsData/link.xml.meta' 'link.xml exists without a .meta file; Unity asset GUID preservation is not proven.'
    }

    $assemblyCSharpMatches = @(Select-String -LiteralPath $linkXmlPath -Pattern 'Assembly-CSharp' -ErrorAction SilentlyContinue)
    foreach ($match in $assemblyCSharpMatches) {
        Add-Finding Error 'AddressablesLinkXml' "$(Get-RelativePath $linkXmlPath):$($match.LineNumber)" "link.xml still references Assembly-CSharp: $($match.Line.Trim())"
    }

    if ($assemblyCSharpMatches.Count -eq 0) {
        Add-Finding Info 'AddressablesLinkXml' 'Assets/AddressableAssetsData/link.xml' 'link.xml contains no Assembly-CSharp preserve references.'
    }

    Test-AddressablesLinkXmlProjectTypeMappings -LinkXmlPath $linkXmlPath
}

function Test-AddressablesLinkXmlProjectTypeMappings {
    param([string]$LinkXmlPath)

    try {
        [xml]$linkXml = Get-Content -LiteralPath $LinkXmlPath -Raw
    } catch {
        Add-Finding Error 'AddressablesLinkXmlProjectTypes' (Get-RelativePath $LinkXmlPath) "Failed to parse link.xml as XML: $($_.Exception.Message)"
        return
    }

    $runtimeSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$runtimeAssemblies)
    $typeMap = Get-TargetTopLevelTypeAssemblyMap -AssemblyNames $runtimeAssemblies
    $entryCountsByAssembly = @{}
    $projectEntryCount = 0
    $issueCount = 0

    foreach ($assemblyNode in @($linkXml.linker.assembly)) {
        $assemblyFullName = [string]$assemblyNode.fullname
        if ([string]::IsNullOrWhiteSpace($assemblyFullName)) {
            continue
        }

        $assemblyName = $assemblyFullName.Split(',')[0].Trim()
        foreach ($typeNode in @($assemblyNode.type)) {
            $typeFullName = [string]$typeNode.fullname
            if ([string]::IsNullOrWhiteSpace($typeFullName)) {
                continue
            }

            $outerTypeName = $typeFullName.Split('/')[0]
            if ($typeMap.ContainsKey($outerTypeName)) {
                $expectedAssembly = $typeMap[$outerTypeName]
                if ($assemblyName -ne $expectedAssembly) {
                    $issueCount++
                    Add-Finding Error 'AddressablesLinkXmlProjectTypes' (Get-RelativePath $LinkXmlPath) "Project preserve type is under the wrong assembly block: $typeFullName. Actual=$assemblyName Expected=$expectedAssembly"
                    continue
                }

                $projectEntryCount++
                if (-not $entryCountsByAssembly.ContainsKey($assemblyName)) {
                    $entryCountsByAssembly[$assemblyName] = 0
                }

                $entryCountsByAssembly[$assemblyName]++
                continue
            }

            if ($runtimeSet.Contains($assemblyName)) {
                $issueCount++
                Add-Finding Error 'AddressablesLinkXmlProjectTypes' (Get-RelativePath $LinkXmlPath) "Project assembly preserve entry does not resolve to a current top-level type declaration: $assemblyName -> $typeFullName"
            }
        }
    }

    if ($issueCount -eq 0) {
        $summary = [string]::Join(', ', @($entryCountsByAssembly.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }))
        Add-Finding Info 'AddressablesLinkXmlProjectTypes' 'Assets/AddressableAssetsData/link.xml' "link.xml project preserve entries resolve to current runtime target assembly type declarations. Entries=$projectEntryCount; $summary"
    }
}

function Test-EditorClassIdentifierCaches {
    $scanRoots = @('Assets/_Project', 'Assets/AddressableAssetsData', 'ProjectSettings')
    $serializedExtensions = [System.Collections.Generic.HashSet[string]]::new([string[]]@('.asset', '.prefab', '.unity', '.xml', '.controller', '.overrideController'))
    $cacheCountsByAssembly = @{}

    foreach ($root in $scanRoots) {
        $absoluteRoot = Join-Path $script:ProjectRoot $root
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File | Where-Object {
            $serializedExtensions.Contains($_.Extension)
        } | ForEach-Object {
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                if ($line -notmatch 'm_EditorClassIdentifier:\s*(Assembly-CSharp(?:-Editor)?)(?:::|$)') {
                    continue
                }

                $assemblyName = $Matches[1]
                if (-not $cacheCountsByAssembly.ContainsKey($assemblyName)) {
                    $cacheCountsByAssembly[$assemblyName] = 0
                }

                $cacheCountsByAssembly[$assemblyName]++
            }
        }
    }

    if ($cacheCountsByAssembly.Count -eq 0) {
        Add-Finding Info 'EditorClassIdentifierCache' 'Assets/_Project' 'No stale Assembly-CSharp editor class identifier cache strings were found.'
        return
    }

    foreach ($assemblyName in ($cacheCountsByAssembly.Keys | Sort-Object)) {
        Add-Finding Info 'EditorClassIdentifierCache' 'Assets/_Project' "Stale m_EditorClassIdentifier cache strings remain for ${assemblyName}: $($cacheCountsByAssembly[$assemblyName]). These should be cleared by Unity reserialization, not broad text replacement."
    }
}

function ConvertTo-AbsoluteProjectPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $script:ProjectRoot $Path))
}

function Test-IsPathUnderDirectory {
    param(
        [string]$Path,
        [string]$Directory
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($Directory)) {
        return $false
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    return $fullPath.Equals($fullDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($fullDirectory + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
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

function Test-UnityProjectLock {
    $lockFile = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $lockFile)) {
        Add-Finding Info 'UnityProjectLock' 'Temp/UnityLockfile' 'Unity project lockfile is absent; batch import/compile validation can be attempted.'
        return
    }

    $unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
    if ($unityProcesses.Count -eq 0) {
        Add-Finding Info 'UnityProjectLock' (Get-RelativePath $lockFile) 'Unity project lockfile is present, but no Unity process was found. Remove only after confirming no Editor owns this project.'
        return
    }

    $processSummary = ($unityProcesses |
        Sort-Object Id |
        ForEach-Object {
            try {
                $start = if ($_.StartTime) { $_.StartTime.ToString('yyyy-MM-dd HH:mm:ss') } else { 'unknown-start' }
            } catch {
                $start = 'unknown-start'
            }

            "PID=$($_.Id), Start=$start"
        }) -join '; '

    Add-Finding Info 'UnityProjectLock' (Get-RelativePath $lockFile) "Unity project lockfile is present. Batch import/compile validation should wait until the Editor closes. Processes=$processSummary"
}

function Test-UnityCompileOutputs {
    $scriptAssembliesRoot = Join-Path $script:ProjectRoot 'Library\ScriptAssemblies'
    if (-not (Test-Path -LiteralPath $scriptAssembliesRoot)) {
        Add-Finding Error 'CompileOutput' 'Library/ScriptAssemblies' 'Unity script assembly output directory is missing.'
        return
    }

    foreach ($assemblyName in $targetAssemblies) {
        $path = Join-Path $scriptAssembliesRoot ($assemblyName + '.dll')
        if (Test-Path -LiteralPath $path) {
            Add-Finding Info 'CompileOutput' (Get-RelativePath $path) "Target assembly output exists: $assemblyName.dll"
        } else {
            Add-Finding Error 'CompileOutput' 'Library/ScriptAssemblies' "Target assembly output is missing: $assemblyName.dll"
        }
    }

    foreach ($legacyName in @('Assembly-CSharp.dll', 'Assembly-CSharp-Editor.dll', 'Assembly-CSharp-firstpass.dll', 'Assembly-CSharp-Editor-firstpass.dll')) {
        $path = Join-Path $scriptAssembliesRoot $legacyName
        if (Test-Path -LiteralPath $path) {
            Add-Finding Warning 'CompileOutput' (Get-RelativePath $path) "Legacy default assembly output still exists: $legacyName"
        } else {
            Add-Finding Info 'CompileOutput' 'Library/ScriptAssemblies' "Legacy default assembly output is absent: $legacyName"
        }
    }
}

function Test-GeneratedProjectCompileItems {
    param(
        [string]$ProjectFile,
        [string]$AssemblyName,
        [object]$Asmdef
    )

    $asmdefPath = Join-Path $script:ProjectRoot $Asmdef.Path
    if (-not (Test-Path -LiteralPath $asmdefPath)) {
        return
    }

    $asmdefDirectory = [System.IO.Path]::GetDirectoryName($asmdefPath)
    if ([string]::IsNullOrWhiteSpace($asmdefDirectory) -or -not (Test-Path -LiteralPath $asmdefDirectory)) {
        return
    }

    [xml]$projectXml = Get-Content -LiteralPath $ProjectFile -Raw
    $existingCompilePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $unexpectedCompileItems = New-Object System.Collections.Generic.List[string]
    foreach ($compileItem in @($projectXml.SelectNodes('/Project/ItemGroup/Compile[@Include]'))) {
        $include = [string]$compileItem.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        $absoluteIncludePath = ConvertTo-AbsoluteProjectPath -Path $include
        [void]$existingCompilePaths.Add($absoluteIncludePath)

        $isUnderAsmdef = Test-IsPathUnderDirectory -Path $absoluteIncludePath -Directory $asmdefDirectory
        $isUnderNestedAssembly = $isUnderAsmdef -and (Test-IsUnderNestedAssemblyBoundary -SourcePath $absoluteIncludePath -AsmdefDirectory $asmdefDirectory)
        if (-not $isUnderAsmdef -or $isUnderNestedAssembly) {
            $unexpectedCompileItems.Add((Get-RelativePath $absoluteIncludePath))
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

        $missingSources.Add((Get-RelativePath $fullSourcePath))
    }

    if ($missingSources.Count -eq 0) {
        Add-Finding Info 'GeneratedProject' (Get-RelativePath $ProjectFile) "Generated project file includes all current asmdef source Compile items: $AssemblyName"
    } else {
        $sample = [string]::Join(', ', @($missingSources | Sort-Object | Select-Object -First 12))
        Add-Finding Error 'GeneratedProject' (Get-RelativePath $ProjectFile) "Generated project file is stale; Compile items are missing for current asmdef sources: Assembly=$AssemblyName; Count=$($missingSources.Count); Sample=$sample"
    }

    if ($unexpectedCompileItems.Count -eq 0) {
        Add-Finding Info 'GeneratedProject' (Get-RelativePath $ProjectFile) "Generated project file contains no Compile items outside the current asmdef source boundary: $AssemblyName"
    } else {
        $unexpectedSample = [string]::Join(', ', @($unexpectedCompileItems | Sort-Object | Select-Object -First 12))
        Add-Finding Error 'GeneratedProject' (Get-RelativePath $ProjectFile) "Generated project file includes Compile items outside the current asmdef source boundary: Assembly=$AssemblyName; Count=$($unexpectedCompileItems.Count); Sample=$unexpectedSample"
    }
}

function Test-GeneratedProjectLegacyDefaultReferences {
    param(
        [string]$ProjectFile,
        [string]$AssemblyName
    )

    $matches = @(Select-String -LiteralPath $ProjectFile -Pattern 'Assembly-CSharp' -SimpleMatch -ErrorAction SilentlyContinue)
    if ($matches.Count -eq 0) {
        Add-Finding Info 'GeneratedProject' (Get-RelativePath $ProjectFile) "Generated project file contains no legacy default assembly references: $AssemblyName"
        return
    }

    $sample = [string]::Join(', ', @($matches | Select-Object -First 5 | ForEach-Object { "line $($_.LineNumber): $($_.Line.Trim())" }))
    Add-Finding Error 'GeneratedProject' (Get-RelativePath $ProjectFile) "Generated target project still contains legacy default assembly references: Assembly=$AssemblyName; Count=$($matches.Count); Sample=$sample"
}

function Test-GeneratedSolutionContents {
    param([System.IO.FileInfo[]]$SolutionFiles)

    foreach ($solutionFile in $SolutionFiles) {
        $solutionText = Get-Content -LiteralPath $solutionFile.FullName -Raw
        foreach ($assemblyName in $targetAssemblies) {
            $projectFileName = "$assemblyName.csproj"
            if ($solutionText -match [regex]::Escape($projectFileName)) {
                continue
            }

            Add-Finding Error 'GeneratedSolution' (Get-RelativePath $solutionFile.FullName) "Generated solution does not include target project: $projectFileName"
        }

        $legacyProjectNames = @(
            'Assembly-CSharp.csproj',
            'Assembly-CSharp-Editor.csproj',
            'Assembly-CSharp-firstpass.csproj',
            'Assembly-CSharp-Editor-firstpass.csproj'
        )
        foreach ($legacyProjectName in $legacyProjectNames) {
            if ($solutionText -notmatch [regex]::Escape($legacyProjectName)) {
                continue
            }

            Add-Finding Error 'GeneratedSolution' (Get-RelativePath $solutionFile.FullName) "Generated solution still includes legacy default project: $legacyProjectName"
        }
    }

    if ($SolutionFiles.Count -gt 0) {
        Add-Finding Info 'GeneratedSolution' '.' "Generated solution contents include all target project files and no legacy Assembly-CSharp project files. Solutions=$($SolutionFiles.Count)"
    }
}

function Test-GeneratedProjectFiles {
    $solutionFiles = @(
        Get-ChildItem -LiteralPath $script:ProjectRoot -Filter '*.sln' -File -ErrorAction SilentlyContinue
        Get-ChildItem -LiteralPath $script:ProjectRoot -Filter '*.slnx' -File -ErrorAction SilentlyContinue
    )
    if ($solutionFiles.Count -eq 0) {
        Add-Finding Error 'GeneratedProject' '.' 'Generated solution file is missing; Unity project files must be regenerated before solution build verification.'
    } else {
        foreach ($solutionFile in $solutionFiles) {
            Add-Finding Info 'GeneratedProject' (Get-RelativePath $solutionFile.FullName) "Generated solution file exists. LastWriteTime=$($solutionFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
        }

        Test-GeneratedSolutionContents -SolutionFiles $solutionFiles
    }

    $asmdefsByName = Get-ProjectAsmdefs
    foreach ($assemblyName in $targetAssemblies) {
        $projectFile = Join-Path $script:ProjectRoot ($assemblyName + '.csproj')
        if (Test-Path -LiteralPath $projectFile) {
            Add-Finding Info 'GeneratedProject' (Get-RelativePath $projectFile) "Generated project file exists for target assembly: $assemblyName.csproj"
        } else {
            Add-Finding Error 'GeneratedProject' '.' "Generated project file is missing for target assembly: $assemblyName.csproj"
            continue
        }

        if (-not $asmdefsByName.ContainsKey($assemblyName)) {
            continue
        }

        Test-GeneratedProjectLegacyDefaultReferences -ProjectFile $projectFile -AssemblyName $assemblyName

        $projectText = Get-Content -LiteralPath $projectFile -Raw
        foreach ($reference in @($asmdefsByName[$assemblyName].References)) {
            if ([string]::IsNullOrWhiteSpace($reference)) {
                continue
            }

            $escapedReference = [regex]::Escape([string]$reference)
            $hasAssemblyReference = $projectText -match "<Reference\s+Include=`"$escapedReference(`"|,)"
            $hasProjectReference = $projectText -match "<ProjectReference\s+Include=`"$escapedReference\.csproj`""
            if ($hasAssemblyReference -or $hasProjectReference) {
                continue
            }

            Add-Finding Error 'GeneratedProject' (Get-RelativePath $projectFile) "Generated project file is stale; asmdef reference is missing from csproj: $assemblyName -> $reference"
        }

        Test-GeneratedProjectCompileItems -ProjectFile $projectFile -AssemblyName $assemblyName -Asmdef $asmdefsByName[$assemblyName]
    }

    foreach ($legacyProjectName in @('Assembly-CSharp.csproj', 'Assembly-CSharp-Editor.csproj', 'Assembly-CSharp-firstpass.csproj', 'Assembly-CSharp-Editor-firstpass.csproj')) {
        $projectFile = Join-Path $script:ProjectRoot $legacyProjectName
        if (Test-Path -LiteralPath $projectFile) {
            Add-Finding Warning 'GeneratedProject' (Get-RelativePath $projectFile) "Legacy generated project file still exists: $legacyProjectName"
        } else {
            Add-Finding Info 'GeneratedProject' '.' "Legacy generated project file is absent: $legacyProjectName"
        }
    }
}

function Add-CompletionGateSummary {
    $compileOutputErrorCount = @($findings | Where-Object { $_.Severity -eq 'Error' -and $_.Category -eq 'CompileOutput' }).Count
    $generatedProjectErrorCount = @($findings | Where-Object { $_.Severity -eq 'Error' -and $_.Category -eq 'GeneratedProject' }).Count
    $knownPackageMissingScriptCount = @($findings | Where-Object { $_.Category -eq 'MissingScript' -and $_.Message -like 'Missing script GUID belongs to missing package content:*' }).Count
    $unknownMissingScriptCount = @($findings | Where-Object { $_.Category -eq 'MissingScript' -and $_.Severity -eq 'Error' }).Count
    $missingAssetReferenceCount = @($findings | Where-Object { $_.Category -eq 'MissingAssetReference' -and $_.Severity -eq 'Error' }).Count
    $preExistingMissingAssetReferenceCount = @($findings | Where-Object { $_.Category -eq 'PreExistingMissingAssetReference' }).Count
    $secondaryAssemblyCSharpWarningCount = @($findings | Where-Object { $_.Category -in @('SerializedReferenceSecondaryScope', 'SerializedReferenceSecondaryUnityEvent') -and $_.Severity -eq 'Warning' }).Count
    $unityLockPresent = @($findings | Where-Object { $_.Category -eq 'UnityProjectLock' -and $_.Path -eq 'Temp\UnityLockfile' }).Count -gt 0

    Add-Finding Info 'CompletionGateSummary' '.' "Current completion blockers: UnityLock=$unityLockPresent; MissingTargetAssemblyOutputs=$compileOutputErrorCount; StaleGeneratedProjectErrors=$generatedProjectErrorCount; KnownPackageMissingScripts=$knownPackageMissingScriptCount; UnknownMissingScriptErrors=$unknownMissingScriptCount; MissingAssetReferenceErrors=$missingAssetReferenceCount; PreExistingMissingAssetReferences=$preExistingMissingAssetReferenceCount; SecondaryAssemblyCSharpWarnings=$secondaryAssemblyCSharpWarningCount"

    if ($unityLockPresent) {
        Add-Finding Info 'CompletionGateSummary' 'Temp/UnityLockfile' 'Unity import/compile, generated target DLL output validation, generated project regeneration, final solution build, and AssetDatabase-backed scene/prefab/ScriptableObject/Addressables validation are pending until the open Editor releases the project lock.'
    }

    if ($knownPackageMissingScriptCount -gt 0) {
        Add-Finding Info 'CompletionGateSummary' 'Assets/_Project; ProjectSettings' 'Remaining known-package missing-script cleanup can touch scene, asset, or ProjectSettings files. Treat cleanup as an explicit approval step before editing those assets.'
    }

    if ($secondaryAssemblyCSharpWarningCount -gt 0) {
        Add-Finding Info 'CompletionGateSummary' 'Assets/GlobalUIRoot Copy.prefab; Assets/_Recovery' 'Remaining secondary serialized cleanup touches root prefab or recovery assets outside the primary scan roots. Treat cleanup, migration, deletion, or exclusion as an explicit approval step.'
    }
}

Test-AsmdefGraph
Test-AsmdefNameUniqueness
Test-AssetAsmdefAllowedPaths
Test-TargetSourceRootNestedAssemblyBoundaries
Test-ProjectSourceRootOwnership
Test-ProjectTestAsmdefPolicy
Test-AsmdefPlatformSettings
Test-AsmdefReferenceOptionSettings
Test-RuntimeAsmdefEditorReferences
Test-AsmdefMetaImporters
Test-AsmdefReferenceResolution
Test-AssetAsmdefReferencePolicy
Test-AsmrefReferenceResolution
Test-AsmdefRequiredExternalReferences
Test-KnownForbiddenConcreteDependencies
Test-LowerLayerForbiddenNamespaceReferences
Test-LowerLayerForbiddenPresentationApiReferences
Test-ProjectSourceDefaultAssemblyLiterals
Test-SourceCoverage
Test-AssetSourceAssemblyOwners
Test-ProjectNamespaceAssemblySpans
Test-DuplicateTargetTypeDeclarations
Test-CSharpMetaPairing
Test-MovedScriptSourceMetaPairing
Test-MovedScriptMetaGuidPreservation
Test-AssetMetaGuidUniqueness
Test-TypeResponsibilityComments
Test-UnguardedRuntimeUnityEditor
Test-SerializedReferences
Test-SecondarySerializedAssemblyCSharpResiduals
Test-SecondarySerializedScriptReferences
Test-SecondaryResidualReferenceUse
Test-RecoveryAssemblyCSharpCacheOnly
Test-VisualScriptingResidualCleanupReadiness
Test-AddressableAssetEntries
Test-AddressablesLinkXml
Test-EditorClassIdentifierCaches
Test-UnityProjectLock
Test-UnityCompileOutputs
Test-GeneratedProjectFiles
Add-CompletionGateSummary

$groups = $findings | Group-Object Severity
$errorCount = ($groups | Where-Object Name -eq 'Error').Count
$warningCount = ($groups | Where-Object Name -eq 'Warning').Count
$infoCount = ($groups | Where-Object Name -eq 'Info').Count

Write-Host "Assembly split static audit summary:"
Write-Host "  Errors:   $errorCount"
Write-Host "  Warnings: $warningCount"
Write-Host "  Infos:    $infoCount"

$findings |
    Sort-Object @{ Expression = { @{ Error = 0; Warning = 1; Info = 2 }[$_.Severity] } }, Category, Path |
    Format-Table -AutoSize Severity, Category, Path, Message

if ($errorCount -gt 0) {
    exit 1
}

exit 0
