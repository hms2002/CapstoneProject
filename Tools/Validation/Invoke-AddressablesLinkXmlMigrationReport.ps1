[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$LegacyLinkXmlPath,
    [string]$LegacyLinkXmlMetaPath,
    [string]$OutputReportPath,
    [string]$OutputProposalPath,
    [string]$OutputProposalMetaPath,
    [string]$GitPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..')).Path
}

if ([string]::IsNullOrWhiteSpace($OutputReportPath)) {
    $OutputReportPath = Join-Path $ProjectRoot 'Temp\AddressablesLinkXmlMigrationReport.txt'
}

if ([string]::IsNullOrWhiteSpace($OutputProposalPath)) {
    $OutputProposalPath = Join-Path $ProjectRoot 'Temp\AddressablesLinkXmlMigrationProposal.xml'
}

if ([string]::IsNullOrWhiteSpace($OutputProposalMetaPath)) {
    $OutputProposalMetaPath = "$OutputProposalPath.meta"
}

$runtimeAssemblies = @('Core', 'Gameplay', 'Infrastructure', 'Presentation', 'UI')
$targetAssemblySourceRoots = @{
    Core = 'Assets/_Project/Runtime/Core'
    Gameplay = 'Assets/_Project/Runtime/Features'
    Infrastructure = 'Assets/_Project/Runtime/Infrastructure'
    Presentation = 'Assets/_Project/Runtime/Presentation'
    UI = 'Assets/_Project/Runtime/UI'
}

function Resolve-GitPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "Git executable was not found: $ExplicitPath"
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $command = Get-Command git -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $knownGitPath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
    if (Test-Path -LiteralPath $knownGitPath) {
        return $knownGitPath
    }

    throw 'Git executable could not be resolved. Pass -GitPath explicitly or provide -LegacyLinkXmlPath.'
}

function Get-LegacyLinkXmlText {
    if (-not [string]::IsNullOrWhiteSpace($LegacyLinkXmlPath)) {
        if (-not (Test-Path -LiteralPath $LegacyLinkXmlPath)) {
            throw "Legacy link.xml path does not exist: $LegacyLinkXmlPath"
        }

        return Get-Content -LiteralPath $LegacyLinkXmlPath -Raw
    }

    $resolvedGitPath = Resolve-GitPath -ExplicitPath $GitPath
    $legacyText = & $resolvedGitPath -C $ProjectRoot show HEAD:Assets/AddressableAssetsData/link.xml
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($legacyText)) {
        throw 'Could not read HEAD:Assets/AddressableAssetsData/link.xml. Provide -LegacyLinkXmlPath if the old file is not available from git.'
    }

    return ($legacyText -join "`n")
}

function Get-LegacyLinkXmlMetaText {
    if (-not [string]::IsNullOrWhiteSpace($LegacyLinkXmlMetaPath)) {
        if (-not (Test-Path -LiteralPath $LegacyLinkXmlMetaPath)) {
            throw "Legacy link.xml.meta path does not exist: $LegacyLinkXmlMetaPath"
        }

        return Get-Content -LiteralPath $LegacyLinkXmlMetaPath -Raw
    }

    if (-not [string]::IsNullOrWhiteSpace($LegacyLinkXmlPath)) {
        $candidateMetaPath = "$LegacyLinkXmlPath.meta"
        if (Test-Path -LiteralPath $candidateMetaPath) {
            return Get-Content -LiteralPath $candidateMetaPath -Raw
        }
    }

    $resolvedGitPath = Resolve-GitPath -ExplicitPath $GitPath
    $legacyMetaText = & $resolvedGitPath -C $ProjectRoot show HEAD:Assets/AddressableAssetsData/link.xml.meta
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($legacyMetaText)) {
        throw 'Could not read HEAD:Assets/AddressableAssetsData/link.xml.meta. Provide -LegacyLinkXmlMetaPath if the old meta file is not available from git.'
    }

    return ($legacyMetaText -join "`n")
}

function Get-BraceDelta {
    param([string]$Line)

    if ([string]::IsNullOrEmpty($Line)) {
        return 0
    }

    $opens = ([regex]::Matches($Line, '\{')).Count
    $closes = ([regex]::Matches($Line, '\}')).Count
    return $opens - $closes
}

function Get-CurrentRuntimeTypeAssemblyMap {
    $namespacePattern = '^\s*namespace\s+(?<Name>[A-Za-z_][A-Za-z0-9_.]*)\b'
    $typeDeclarationPattern = '^\s*(?:\[[^\]]+\]\s*)*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|unsafe|new)\s+)*(?:class|interface|struct|record(?:\s+struct|\s+class)?|enum)\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)\b'
    $typeMap = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)

    foreach ($assemblyName in $runtimeAssemblies) {
        $relativeRoot = $targetAssemblySourceRoots[$assemblyName]
        $absoluteRoot = Join-Path $ProjectRoot ($relativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
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

function ConvertTo-LinkerAssemblyFullName {
    param([string]$AssemblyName)

    return "$AssemblyName, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
}

function Escape-XmlAttribute {
    param([string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

$legacyText = Get-LegacyLinkXmlText
$legacyMetaText = Get-LegacyLinkXmlMetaText
$legacyMetaGuidMatch = [regex]::Match($legacyMetaText, '(?m)^\s*guid:\s*([0-9a-fA-F]{32})\s*$')
if (-not $legacyMetaGuidMatch.Success) {
    throw 'Legacy link.xml.meta does not contain a valid Unity GUID.'
}

$legacyMetaGuid = $legacyMetaGuidMatch.Groups[1].Value.ToLowerInvariant()

try {
    [xml]$legacyXml = $legacyText
} catch {
    throw "Failed to parse legacy link.xml: $($_.Exception.Message)"
}

$typeMap = Get-CurrentRuntimeTypeAssemblyMap
$legacyEntries = New-Object System.Collections.Generic.List[object]
$resolvedEntriesByAssembly = @{}
$externalEntriesByAssembly = @{}
$unresolvedProjectEntries = New-Object System.Collections.Generic.List[object]
$legacyAssemblyCounts = @{}

foreach ($assemblyNode in @($legacyXml.linker.assembly)) {
    $assemblyFullName = [string]$assemblyNode.fullname
    if ([string]::IsNullOrWhiteSpace($assemblyFullName)) {
        continue
    }

    $legacyAssemblyName = $assemblyFullName.Split(',')[0].Trim()
    if (-not $legacyAssemblyCounts.ContainsKey($legacyAssemblyName)) {
        $legacyAssemblyCounts[$legacyAssemblyName] = 0
    }

    foreach ($typeNode in @($assemblyNode.type)) {
        $typeFullName = [string]$typeNode.fullname
        if ([string]::IsNullOrWhiteSpace($typeFullName)) {
            continue
        }

        $preserve = [string]$typeNode.preserve
        if ([string]::IsNullOrWhiteSpace($preserve)) {
            $preserve = 'all'
        }

        $legacyAssemblyCounts[$legacyAssemblyName]++
        $outerTypeName = $typeFullName.Split('/')[0]
        $entry = [pscustomobject]@{
            LegacyAssembly = $legacyAssemblyName
            Type = $typeFullName
            Preserve = $preserve
            CurrentAssembly = $null
        }
        $legacyEntries.Add($entry)

        if (-not $typeMap.ContainsKey($outerTypeName)) {
            if ($legacyAssemblyName -eq 'Assembly-CSharp') {
                $unresolvedProjectEntries.Add($entry)
                continue
            }

            if (-not $externalEntriesByAssembly.ContainsKey($assemblyFullName)) {
                $externalEntriesByAssembly[$assemblyFullName] = New-Object System.Collections.Generic.List[object]
            }

            $externalEntriesByAssembly[$assemblyFullName].Add($entry)
            continue
        }

        $currentAssembly = $typeMap[$outerTypeName]
        $entry.CurrentAssembly = $currentAssembly
        if (-not $resolvedEntriesByAssembly.ContainsKey($currentAssembly)) {
            $resolvedEntriesByAssembly[$currentAssembly] = New-Object System.Collections.Generic.List[object]
        }

        $resolvedEntriesByAssembly[$currentAssembly].Add($entry)
    }
}

$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('Addressables link.xml migration dry-run')
$reportLines.Add("ProjectRoot: $ProjectRoot")
$reportLines.Add("Legacy source: $(if ([string]::IsNullOrWhiteSpace($LegacyLinkXmlPath)) { 'git HEAD:Assets/AddressableAssetsData/link.xml' } else { $LegacyLinkXmlPath })")
$reportLines.Add("Legacy meta source: $(if ([string]::IsNullOrWhiteSpace($LegacyLinkXmlMetaPath)) { if ([string]::IsNullOrWhiteSpace($LegacyLinkXmlPath)) { 'git HEAD:Assets/AddressableAssetsData/link.xml.meta' } else { "$LegacyLinkXmlPath.meta or git HEAD fallback" } } else { $LegacyLinkXmlMetaPath })")
$reportLines.Add('')
$reportLines.Add("Legacy meta GUID: $legacyMetaGuid")
$reportLines.Add('')
$reportLines.Add("Legacy entries: $($legacyEntries.Count)")
$reportLines.Add("Migrated legacy Assembly-CSharp entries to current runtime target assemblies: $(($resolvedEntriesByAssembly.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum)")
$reportLines.Add("Preserved external/package entries unchanged: $(($externalEntriesByAssembly.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum)")
$reportLines.Add("Unresolved legacy Assembly-CSharp entries: $($unresolvedProjectEntries.Count)")
$reportLines.Add('')
$reportLines.Add('Legacy assembly counts:')
foreach ($pair in $legacyAssemblyCounts.GetEnumerator() | Sort-Object Name) {
    $reportLines.Add("- $($pair.Key): $($pair.Value)")
}

$reportLines.Add('')
$reportLines.Add('Resolved current assembly counts:')
foreach ($assemblyName in $runtimeAssemblies) {
    $count = if ($resolvedEntriesByAssembly.ContainsKey($assemblyName)) { $resolvedEntriesByAssembly[$assemblyName].Count } else { 0 }
    $reportLines.Add("- ${assemblyName}: $count")
}

$reportLines.Add('')
$reportLines.Add('External/package entries preserved unchanged:')
if ($externalEntriesByAssembly.Count -eq 0) {
    $reportLines.Add('- none')
} else {
    foreach ($pair in $externalEntriesByAssembly.GetEnumerator() | Sort-Object Name) {
        $assemblyName = $pair.Key.Split(',')[0].Trim()
        $reportLines.Add("- ${assemblyName}: $($pair.Value.Count)")
    }
}

$reportLines.Add('')
$reportLines.Add('Unresolved legacy Assembly-CSharp entries:')
if ($unresolvedProjectEntries.Count -eq 0) {
    $reportLines.Add('- none')
} else {
    foreach ($entry in $unresolvedProjectEntries | Sort-Object Type) {
        $reportLines.Add("- $($entry.Type)")
    }
}

$proposalLines = New-Object System.Collections.Generic.List[string]
$proposalLines.Add('<linker>')
foreach ($assemblyName in $runtimeAssemblies) {
    if (-not $resolvedEntriesByAssembly.ContainsKey($assemblyName) -or $resolvedEntriesByAssembly[$assemblyName].Count -eq 0) {
        continue
    }

    $proposalLines.Add("  <assembly fullname=`"$(Escape-XmlAttribute (ConvertTo-LinkerAssemblyFullName -AssemblyName $assemblyName))`">")
    foreach ($entry in $resolvedEntriesByAssembly[$assemblyName] | Sort-Object Type -Unique) {
        $proposalLines.Add("    <type fullname=`"$(Escape-XmlAttribute $entry.Type)`" preserve=`"$(Escape-XmlAttribute $entry.Preserve)`" />")
    }

    $proposalLines.Add('  </assembly>')
}

foreach ($pair in $externalEntriesByAssembly.GetEnumerator() | Sort-Object Name) {
    $proposalLines.Add("  <assembly fullname=`"$(Escape-XmlAttribute $pair.Key)`">")
    foreach ($entry in $pair.Value | Sort-Object Type -Unique) {
        $proposalLines.Add("    <type fullname=`"$(Escape-XmlAttribute $entry.Type)`" preserve=`"$(Escape-XmlAttribute $entry.Preserve)`" />")
    }

    $proposalLines.Add('  </assembly>')
}

$proposalLines.Add('</linker>')

try {
    [xml]$proposalXml = ($proposalLines -join "`n")
} catch {
    throw "Generated proposal XML is invalid: $($_.Exception.Message)"
}

$proposalAssemblyCount = @($proposalXml.linker.assembly).Count
$proposalTypeCount = (@($proposalXml.linker.assembly) | ForEach-Object { @($_.type).Count } | Measure-Object -Sum).Sum
$proposalAssemblyCSharpReferenceCount = @($proposalLines | Where-Object { $_ -match 'Assembly-CSharp' }).Count

$reportLines.Add('')
$reportLines.Add('Proposal validation:')
$reportLines.Add("- Assemblies: $proposalAssemblyCount")
$reportLines.Add("- Type entries: $proposalTypeCount")
$reportLines.Add("- Assembly-CSharp references: $proposalAssemblyCSharpReferenceCount")

$outputReportDirectory = Split-Path -Parent $OutputReportPath
if (-not (Test-Path -LiteralPath $outputReportDirectory)) {
    New-Item -ItemType Directory -Path $outputReportDirectory | Out-Null
}

$outputProposalDirectory = Split-Path -Parent $OutputProposalPath
if (-not (Test-Path -LiteralPath $outputProposalDirectory)) {
    New-Item -ItemType Directory -Path $outputProposalDirectory | Out-Null
}

$outputProposalMetaDirectory = Split-Path -Parent $OutputProposalMetaPath
if (-not (Test-Path -LiteralPath $outputProposalMetaDirectory)) {
    New-Item -ItemType Directory -Path $outputProposalMetaDirectory | Out-Null
}

Set-Content -LiteralPath $OutputReportPath -Value $reportLines -Encoding UTF8
Set-Content -LiteralPath $OutputProposalPath -Value $proposalLines -Encoding UTF8
Set-Content -LiteralPath $OutputProposalMetaPath -Value $legacyMetaText -Encoding UTF8

Write-Host "Addressables link.xml migration dry-run complete."
Write-Host "Report:       $OutputReportPath"
Write-Host "Proposal:     $OutputProposalPath"
Write-Host "ProposalMeta: $OutputProposalMetaPath"
Write-Host "LegacyEntries=$($legacyEntries.Count); MigratedProject=$((($resolvedEntriesByAssembly.Values | ForEach-Object { $_.Count }) | Measure-Object -Sum).Sum); PreservedExternal=$((($externalEntriesByAssembly.Values | ForEach-Object { $_.Count }) | Measure-Object -Sum).Sum); UnresolvedProject=$($unresolvedProjectEntries.Count)"
Write-Host "ProposalAssemblies=$proposalAssemblyCount; ProposalEntries=$proposalTypeCount; ProposalAssemblyCSharpReferences=$proposalAssemblyCSharpReferenceCount"
Write-Host "ProposalMetaGuid=$legacyMetaGuid"

if ($unresolvedProjectEntries.Count -gt 0) {
    throw "Legacy Assembly-CSharp preserve entries could not be mapped to current runtime target assemblies. Count=$($unresolvedProjectEntries.Count)"
}

if ($proposalAssemblyCSharpReferenceCount -gt 0) {
    throw "Generated proposal still contains Assembly-CSharp references. Count=$proposalAssemblyCSharpReferenceCount"
}

if ($proposalTypeCount -ne $legacyEntries.Count) {
    throw "Generated proposal does not preserve the legacy type entry count. Legacy=$($legacyEntries.Count); Proposal=$proposalTypeCount"
}
