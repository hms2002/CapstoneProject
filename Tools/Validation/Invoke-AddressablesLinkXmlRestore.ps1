[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$ProposalPath,
    [string]$ProposalMetaPath,
    [string]$TargetLinkXmlPath,
    [string]$ExpectedMetaGuid = '01fd12cf0f26bc7468d405cc646d5eaa',
    [switch]$Apply,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..')).Path
}

if ([string]::IsNullOrWhiteSpace($ProposalPath)) {
    $ProposalPath = Join-Path $ProjectRoot 'Temp\AddressablesLinkXmlMigrationProposal.xml'
}

if ([string]::IsNullOrWhiteSpace($ProposalMetaPath)) {
    $ProposalMetaPath = "$ProposalPath.meta"
}

if ([string]::IsNullOrWhiteSpace($TargetLinkXmlPath)) {
    $TargetLinkXmlPath = Join-Path $ProjectRoot 'Assets\AddressableAssetsData\link.xml'
}

function Resolve-ExistingPath {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required path does not exist: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-ProjectPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}

function Assert-InProjectRoot {
    param(
        [string]$Path,
        [string]$Description
    )

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description is outside project root: $resolvedPath"
    }

    return $resolvedPath
}

function Get-FileHashText {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return ''
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

$resolvedProposalPath = Assert-InProjectRoot -Path (Resolve-ExistingPath -Path $ProposalPath) -Description 'Proposal XML path'
$resolvedProposalMetaPath = Assert-InProjectRoot -Path (Resolve-ExistingPath -Path $ProposalMetaPath) -Description 'Proposal meta path'
$resolvedTargetLinkXmlPath = Assert-InProjectRoot -Path (Resolve-ProjectPath -Path $TargetLinkXmlPath) -Description 'Target link.xml path'
$resolvedTargetMetaPath = Assert-InProjectRoot -Path "$resolvedTargetLinkXmlPath.meta" -Description 'Target link.xml.meta path'

$targetDirectory = Split-Path -Parent $resolvedTargetLinkXmlPath
if (-not (Test-Path -LiteralPath $targetDirectory)) {
    throw "Target directory does not exist: $targetDirectory"
}

$proposalText = Get-Content -LiteralPath $resolvedProposalPath -Raw
try {
    [xml]$proposalXml = $proposalText
} catch {
    throw "Proposal XML is invalid: $($_.Exception.Message)"
}

if (-not $proposalXml.linker) {
    throw 'Proposal XML root must be <linker>.'
}

$assemblyCSharpReferenceCount = @($proposalText | Select-String -Pattern 'Assembly-CSharp' -AllMatches).Matches.Count
if ($assemblyCSharpReferenceCount -ne 0) {
    throw "Proposal XML still contains Assembly-CSharp references. Count=$assemblyCSharpReferenceCount"
}

$proposalAssemblyCount = @($proposalXml.linker.assembly).Count
$proposalTypeCount = (@($proposalXml.linker.assembly) | ForEach-Object { @($_.type).Count } | Measure-Object -Sum).Sum
if ($proposalTypeCount -le 0) {
    throw 'Proposal XML contains no linker type entries.'
}

$proposalMetaText = Get-Content -LiteralPath $resolvedProposalMetaPath -Raw
$proposalMetaGuidMatch = [regex]::Match($proposalMetaText, '(?m)^\s*guid:\s*([0-9a-fA-F]{32})\s*$')
if (-not $proposalMetaGuidMatch.Success) {
    throw 'Proposal meta does not contain a valid Unity GUID.'
}

$proposalMetaGuid = $proposalMetaGuidMatch.Groups[1].Value.ToLowerInvariant()
if ($proposalMetaGuid -ne $ExpectedMetaGuid.ToLowerInvariant()) {
    throw "Proposal meta GUID mismatch. Actual=$proposalMetaGuid Expected=$ExpectedMetaGuid"
}

if ($proposalMetaText -notmatch '(?m)^TextScriptImporter:\s*$') {
    throw 'Proposal meta does not use TextScriptImporter.'
}

$proposalHash = Get-FileHashText -Path $resolvedProposalPath
$proposalMetaHash = Get-FileHashText -Path $resolvedProposalMetaPath

$targetExistsBefore = Test-Path -LiteralPath $resolvedTargetLinkXmlPath
$targetMetaExistsBefore = Test-Path -LiteralPath $resolvedTargetMetaPath
$targetHash = Get-FileHashText -Path $resolvedTargetLinkXmlPath
$targetMetaHash = Get-FileHashText -Path $resolvedTargetMetaPath
$targetMatchesProposalBefore = $targetExistsBefore -and $targetMetaExistsBefore -and $proposalHash -eq $targetHash -and $proposalMetaHash -eq $targetMetaHash

if (($targetExistsBefore -or $targetMetaExistsBefore) -and -not $targetMatchesProposalBefore -and -not $Force.IsPresent) {
    throw 'Target link.xml or link.xml.meta already exists and differs from the proposal. Pass -Force to overwrite after review.'
}

$mode = if ($Apply.IsPresent) { 'Apply' } else { 'ValidateOnly' }

if ($Apply.IsPresent) {
    Copy-Item -LiteralPath $resolvedProposalPath -Destination $resolvedTargetLinkXmlPath -Force
    Copy-Item -LiteralPath $resolvedProposalMetaPath -Destination $resolvedTargetMetaPath -Force
}

$targetExists = Test-Path -LiteralPath $resolvedTargetLinkXmlPath
$targetMetaExists = Test-Path -LiteralPath $resolvedTargetMetaPath
$targetHash = Get-FileHashText -Path $resolvedTargetLinkXmlPath
$targetMetaHash = Get-FileHashText -Path $resolvedTargetMetaPath
$targetMatchesProposal = $targetExists -and $targetMetaExists -and $proposalHash -eq $targetHash -and $proposalMetaHash -eq $targetMetaHash

Write-Host 'Addressables link.xml restore validation complete.'
Write-Host "Mode=$mode"
Write-Host "Proposal=$resolvedProposalPath"
Write-Host "ProposalMeta=$resolvedProposalMetaPath"
Write-Host "Target=$resolvedTargetLinkXmlPath"
Write-Host "TargetMeta=$resolvedTargetMetaPath"
Write-Host 'TargetRole=TemporaryAddressablesPlayerBuildCopy'
Write-Host 'TargetExpectedToPersist=False'
Write-Host "ProposalAssemblies=$proposalAssemblyCount; ProposalEntries=$proposalTypeCount; ProposalAssemblyCSharpReferences=$assemblyCSharpReferenceCount; ProposalMetaGuid=$proposalMetaGuid"
Write-Host "TargetExistsBefore=$targetExistsBefore; TargetMetaExistsBefore=$targetMetaExistsBefore; TargetMatchesProposalBefore=$targetMatchesProposalBefore"
Write-Host "TargetExists=$targetExists; TargetMetaExists=$targetMetaExists; TargetMatchesProposal=$targetMatchesProposal"
