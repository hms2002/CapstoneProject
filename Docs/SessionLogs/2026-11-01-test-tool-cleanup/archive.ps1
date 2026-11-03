$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$archiveRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'Tools/Archive/2026-11-01-one-off'))
$workspacePrefix = $workspace.TrimEnd('\') + '\'
$archivePrefix = $archiveRoot.TrimEnd('\') + '\'
$plan = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'inventory-plan.json') -Raw | ConvertFrom-Json
if ($plan.incomingReferences.Count -ne 0) { throw 'Incoming source references must be resolved before retirement.' }
$moves = foreach ($entry in $plan.files) {
    $source = [IO.Path]::GetFullPath((Join-Path $workspace $entry.source))
    $suffix = if ([IO.Path]::GetExtension($source) -eq '.png') { '' } else { '.txt' }
    $destination = [IO.Path]::GetFullPath((Join-Path $archiveRoot ($entry.source + $suffix)))
    if (-not $source.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not $destination.StartsWith($archivePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Move escaped intended directories.' }
    if (Test-Path -LiteralPath $destination) { throw "Archive already exists: $destination" }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256) { throw "Source changed since inventory: $source" }
    [PSCustomObject]@{ source=$entry.source; archived=$destination.Substring($workspacePrefix.Length).Replace('\','/'); sha256=$hash; bytes=$entry.bytes }
}
$crimson = Join-Path $workspace 'Assets/_Project/Tests/PlayMode/Procedural/CrimsonChargePlayModeTests.cs'
$crimsonText = [IO.File]::ReadAllText($crimson)
$first = $crimsonText.IndexOf('    [TestCase(0f, 0f, 0f)]', [StringComparison]::Ordinal)
$last = $crimsonText.IndexOf('    [UnityTest]', $first, [StringComparison]::Ordinal)
if ($first -lt 0 -or $last -le $first -or
    -not $crimsonText.Substring($last).StartsWith("    [UnityTest]`r`n    public IEnumerator MovingAim_StaysClose_AndReleasedVisualMatchesDamageCenter()") -and
    -not $crimsonText.Substring($last).StartsWith("    [UnityTest]`n    public IEnumerator MovingAim_StaysClose_AndReleasedVisualMatchesDamageCenter()")) { throw 'Crimson visual test boundaries changed.' }
$removed = $crimsonText.Substring($first, $last-$first)
if (([regex]::Matches($removed,'\[TestCase\(')).Count -ne 8) { throw 'Expected exactly eight visual acceptance cases.' }
$partial = Join-Path $archiveRoot 'Partial/CrimsonChargePlayModeTests.before.cs.txt'
if (Test-Path -LiteralPath $partial) { throw 'Partial archive already exists.' }
New-Item -ItemType Directory -Path (Split-Path $partial -Parent) -Force | Out-Null
Copy-Item -LiteralPath $crimson -Destination $partial
$beforeHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash
foreach ($move in $moves) {
    $source = Join-Path $workspace $move.source
    $destination = Join-Path $workspace $move.archived
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Move-Item -LiteralPath $source -Destination $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $move.sha256) { throw 'Archive copy mismatch.' }
}
[IO.File]::WriteAllText($crimson, $crimsonText.Remove($first,$last-$first), [Text.UTF8Encoding]::new($false))
$manifest = [PSCustomObject]@{
    date='2026-11-01'; automaticTestsExecuted=$false; unityUiUsed=$false;
    wholeTestFilesRetired=6; wholeFileCasesRetired=82; partialCasesRetired=8;
    sourcePlayModeCasesBefore=1215; sourcePlayModeCasesAfter=1125;
    editorToolFilesRetired=17; standaloneToolFilesRetired=6;
    movedFiles=$moves;
    partial=[PSCustomObject]@{source=$crimson.Substring($workspacePrefix.Length).Replace('\','/'); before=$partial.Substring($workspacePrefix.Length).Replace('\','/'); beforeSha256=$beforeHash; afterSha256=(Get-FileHash -LiteralPath $crimson -Algorithm SHA256).Hash; removedMethods=@('MagicCircles_GrowInTwoStages_AndMatchReleasedRange','FlightWarning_HoldsSizeUntilImpact_ThenFadesWithoutShrinking')}
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'archive-manifest.json') -Encoding UTF8
Write-Output "Archived $($moves.Count) files; retired 90 PlayMode source cases and 23 task-specific tools; gameplay/save suites retained."
