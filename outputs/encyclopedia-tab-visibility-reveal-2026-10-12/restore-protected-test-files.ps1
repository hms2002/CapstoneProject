$ErrorActionPreference = 'Stop'
$evidenceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$manifest = Get-Content -LiteralPath (Join-Path $evidenceRoot 'save-before-manifest.json') -Raw | ConvertFrom-Json
$snapshotTime = (Get-Item -LiteralPath (Join-Path $evidenceRoot 'save-before-manifest.json')).LastWriteTimeUtc
$workspaceRoot = [IO.Path]::GetFullPath($manifest.workspace).TrimEnd('\')
$saveRoot = [IO.Path]::GetFullPath($manifest.saveRoot).TrimEnd('\')
$expectedWorkspace = 'C:\Users\nadom\Desktop\졸업작품\CapstoneProject'
$expectedSaveRoot = 'C:\Users\nadom\AppData\LocalLow\DefaultCompany\Capstone_Project'
if ($workspaceRoot -ne $expectedWorkspace -or $saveRoot -ne $expectedSaveRoot) { throw 'Unexpected restoration roots.' }
$editors = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.ExecutablePath -match '[\\/]Editor[\\/]Unity\.exe$'
})
if ($editors.Count -gt 0) { throw 'Close Unity Editor before restoring test files.' }
$backupRoot = Join-Path $evidenceRoot 'after-test-backup'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$records = @()
$index = 0
foreach ($entry in $manifest.files) {
    $target = [IO.Path]::GetFullPath($entry.path)
    $parent = [IO.Path]::GetDirectoryName($target)
    if ($parent -ne $workspaceRoot -and $parent -ne $saveRoot) { throw "Out-of-scope restoration target: $target" }
    $source = [IO.Path]::GetFullPath($entry.backup)
    if (-not $source.StartsWith($evidenceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Out-of-scope backup.' }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.hash) { throw 'Backup hash mismatch.' }
    if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $backupRoot "current-$index.backup") }
    Copy-Item -LiteralPath $source -Destination $target -Force
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($hash -ne $entry.hash) { throw "Restored hash mismatch: $target" }
    $records += [pscustomobject]@{ path = $target; restoredHash = $hash; passed = $true }
    $index++
}
$knownPaths = @($manifest.files | ForEach-Object { [IO.Path]::GetFullPath($_.path) })
$extras = @()
$unclassifiedFiles = @()
foreach ($root in @($workspaceRoot, $saveRoot)) {
    foreach ($pattern in @('GameData*.json', 'GameSave*.json')) {
        foreach ($entry in (Get-ChildItem -LiteralPath $root -Filter $pattern -File)) {
            $target = [IO.Path]::GetFullPath($entry.FullName)
            if ([IO.Path]::GetDirectoryName($target) -ne $root) { throw 'Unexpected new test-file parent.' }
            if ($knownPaths -contains $target) { continue }
            if ($entry.CreationTimeUtc -lt $snapshotTime) { $unclassifiedFiles += $target; continue }
            Copy-Item -LiteralPath $target -Destination (Join-Path $backupRoot "extra-$($extras.Count).backup")
            Remove-Item -LiteralPath $target
            $extras += $target
        }
    }
}
$rendererResults = @()
foreach ($entry in (Get-ChildItem -LiteralPath (Join-Path $evidenceRoot 'renderer-before') -Filter '*.png' -File)) {
    $target = Join-Path $workspaceRoot ('outputs\encyclopedia-implementation-2026-10-11\' + $entry.Name)
    if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $evidenceRoot ('current-' + $entry.Name)) -Force }
    Copy-Item -LiteralPath $entry.FullName -Destination $target -Force
    $same = (Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $entry.FullName).Hash
    if (-not $same) { throw 'Historical renderer backup did not restore.' }
    $rendererResults += [pscustomobject]@{ path = $target; passed = $same }
}
[pscustomobject]@{ passed = $true; restoredFiles = $records; removedNewTestSaves = $extras; untouchedOlderUnclassifiedFiles = $unclassifiedFiles; historicalRendererFiles = $rendererResults } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidenceRoot 'test-file-restoration-report.json') -Encoding UTF8
Write-Output "Restored $($records.Count) protected files and $($rendererResults.Count) historical renders; removed $($extras.Count) newly created test saves."
