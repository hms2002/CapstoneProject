$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskOut = $PSScriptRoot
$taskSources = Get-Content -LiteralPath (Join-Path $taskOut 'generation-sources.json') -Raw | ConvertFrom-Json
$taskPaletteHex = @('#363232','#A75064','#E55C5C','#E09656','#EECF5A','#F1E9D4')
$taskPalette = @($taskPaletteHex | ForEach-Object { [System.Drawing.ColorTranslator]::FromHtml($_) })
$taskFrames = @()
$taskStats = @()
foreach ($taskSource in $taskSources) {
    $taskRaw = [System.Drawing.Bitmap]::new($taskSource.source)
    $taskBox = $taskSource.material_bbox
    $taskSourceW = $taskBox[2] - $taskBox[0]
    $taskSourceH = $taskBox[3] - $taskBox[1]
    $taskLongest = [Math]::Max($taskSourceW,$taskSourceH)
    $taskWidth = [int][Math]::Round(24 * $taskSourceW / $taskLongest)
    $taskHeight = [int][Math]::Round(24 * $taskSourceH / $taskLongest)
    $taskLeft = [int][Math]::Floor((32 - $taskWidth) / 2)
    $taskTop = [int][Math]::Floor((32 - $taskHeight) / 2)
    # Sample generated pixel clusters without smoothing.
    $taskSamplePhaseY = 0.5
    $taskMaterial = New-Object 'int[,]' 32,32
    for ($taskY=0; $taskY -lt $taskHeight; $taskY++) {
        for ($taskX=0; $taskX -lt $taskWidth; $taskX++) {
            $taskSampleX = [int][Math]::Floor($taskBox[0] + ($taskX + 0.5) * $taskSourceW / $taskWidth)
            $taskSampleY = [int][Math]::Floor($taskBox[1] + ($taskY + $taskSamplePhaseY) * $taskSourceH / $taskHeight)
            $taskColor = $taskRaw.GetPixel($taskSampleX,$taskSampleY)
            if ($taskColor.A -lt 200) { continue }
            $taskNearest = 0
            $taskDistance = [double]::PositiveInfinity
            for ($taskIndex=0; $taskIndex -lt $taskPalette.Count; $taskIndex++) {
                $taskCandidate = $taskPalette[$taskIndex]
                $taskDR = [int]$taskColor.R - [int]$taskCandidate.R
                $taskDG = [int]$taskColor.G - [int]$taskCandidate.G
                $taskDB = [int]$taskColor.B - [int]$taskCandidate.B
                $taskD = $taskDR*$taskDR + $taskDG*$taskDG + $taskDB*$taskDB
                if ($taskD -lt $taskDistance) { $taskDistance=$taskD; $taskNearest=$taskIndex }
            }
            $taskMaterial[($taskLeft+$taskX),($taskTop+$taskY)] = $taskNearest
        }
    }
    $taskNative = [System.Drawing.Bitmap]::new(32,32,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($taskY=0; $taskY -lt 32; $taskY++) {
        for ($taskX=0; $taskX -lt 32; $taskX++) {
            if ($taskMaterial[$taskX,$taskY] -eq 0) { continue }
            for ($taskDY=-2; $taskDY -le 2; $taskDY++) {
                for ($taskDX=-2; $taskDX -le 2; $taskDX++) {
                    $taskOutlineX=$taskX+$taskDX; $taskOutlineY=$taskY+$taskDY
                    if ($taskOutlineX -ge 0 -and $taskOutlineX -lt 32 -and $taskOutlineY -ge 0 -and $taskOutlineY -lt 32) {
                        $taskNative.SetPixel($taskOutlineX,$taskOutlineY,$taskPalette[0])
                    }
                }
            }
        }
    }
    for ($taskY=0; $taskY -lt 32; $taskY++) {
        for ($taskX=0; $taskX -lt 32; $taskX++) {
            if ($taskMaterial[$taskX,$taskY] -gt 0) { $taskNative.SetPixel($taskX,$taskY,$taskPalette[$taskMaterial[$taskX,$taskY]]) }
        }
    }
    $taskFilename = $taskSource.key + '-draft-v1-32x32.png'
    $taskNative.Save((Join-Path $taskOut $taskFilename),[System.Drawing.Imaging.ImageFormat]::Png)
    $taskFrames += $taskNative
    $taskStats += [ordered]@{key=$taskSource.key;name=$taskSource.name;native=$taskFilename;source=$taskSource.source;sourceMaterialBounds=$taskBox;materialSize=@($taskWidth,$taskHeight);samplingPhaseY=$taskSamplePhaseY;palette=$taskPaletteHex}
    $taskRaw.Dispose()
}
$taskSheet = [System.Drawing.Bitmap]::new(64,64,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($taskI=0; $taskI -lt 4; $taskI++) {
    $taskCol = $taskI % 2
    $taskRow = [int][Math]::Floor($taskI / 2)
    for ($taskY=0; $taskY -lt 32; $taskY++) {
        for ($taskX=0; $taskX -lt 32; $taskX++) {
            $taskSheet.SetPixel(($taskCol*32+$taskX),($taskRow*32+$taskY),$taskFrames[$taskI].GetPixel($taskX,$taskY))
        }
    }
}
$taskSheet.Save((Join-Path $taskOut 'dragon-fang-four-designs-v1-64x64.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$taskPreview = [System.Drawing.Bitmap]::new(512,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($taskY=0; $taskY -lt 512; $taskY++) {
    for ($taskX=0; $taskX -lt 512; $taskX++) {
        $taskPreview.SetPixel($taskX,$taskY,$taskSheet.GetPixel([int][Math]::Floor($taskX/8),[int][Math]::Floor($taskY/8)))
    }
}
$taskPreview.Save((Join-Path $taskOut 'dragon-fang-four-designs-v1-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$taskStats | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOut 'export-draft-v1.json') -Encoding utf8
$taskPreview.Dispose(); $taskSheet.Dispose()
foreach ($taskFrame in $taskFrames) { $taskFrame.Dispose() }
Write-Output 'Saved four 32x32 designs, 64x64 sheet and exact 8x preview.'
