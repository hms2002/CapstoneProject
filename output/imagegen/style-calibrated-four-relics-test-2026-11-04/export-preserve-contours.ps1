$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskOut=$PSScriptRoot
$taskSources=Get-Content -LiteralPath (Join-Path $taskOut 'generation-sources.json') -Raw | ConvertFrom-Json
$taskFrames=@()
$taskStats=@()
foreach($taskSource in $taskSources){
    $taskRaw=[Drawing.Bitmap]::new($taskSource.source)
    $taskPalette=@($taskSource.palette | ForEach-Object {[Drawing.ColorTranslator]::FromHtml($_)})
    $taskBox=$taskSource.visible_bbox
    $taskSourceWidth=$taskBox[2]-$taskBox[0]
    $taskSourceHeight=$taskBox[3]-$taskBox[1]
    $taskLongest=[Math]::Max($taskSourceWidth,$taskSourceHeight)
    $taskWidth=[int][Math]::Round(28*$taskSourceWidth/$taskLongest)
    $taskHeight=[int][Math]::Round(28*$taskSourceHeight/$taskLongest)
    $taskLeft=[int][Math]::Floor((32-$taskWidth)/2)
    $taskTop=[int][Math]::Floor((32-$taskHeight)/2)
    $taskNative=[Drawing.Bitmap]::new(32,32,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    # Preserve the AI-authored silhouette, interior charcoal and transparent holes.
    # No dilation, new border, extra color pixels or mandatory color count.
    for($taskY=0;$taskY -lt $taskHeight;$taskY++){
        for($taskX=0;$taskX -lt $taskWidth;$taskX++){
            $taskSampleX=[int][Math]::Floor($taskBox[0]+($taskX+0.5)*$taskSourceWidth/$taskWidth)
            $taskSampleY=[int][Math]::Floor($taskBox[1]+($taskY+0.5)*$taskSourceHeight/$taskHeight)
            $taskColor=$taskRaw.GetPixel($taskSampleX,$taskSampleY)
            if($taskColor.A -lt 200){continue}
            $taskNearest=0
            $taskBest=[double]::PositiveInfinity
            for($taskI=0;$taskI -lt $taskPalette.Count;$taskI++){
                $taskCandidate=$taskPalette[$taskI]
                $taskDR=[int]$taskColor.R-[int]$taskCandidate.R
                $taskDG=[int]$taskColor.G-[int]$taskCandidate.G
                $taskDB=[int]$taskColor.B-[int]$taskCandidate.B
                $taskDistance=$taskDR*$taskDR+$taskDG*$taskDG+$taskDB*$taskDB
                if($taskDistance -lt $taskBest){$taskBest=$taskDistance;$taskNearest=$taskI}
            }
            $taskNative.SetPixel(($taskLeft+$taskX),($taskTop+$taskY),$taskPalette[$taskNearest])
        }
    }
    $taskFilename=$taskSource.key+'-draft-v1-32x32.png'
    $taskNative.Save((Join-Path $taskOut $taskFilename),[Drawing.Imaging.ImageFormat]::Png)
    $taskFrames+=$taskNative
    $taskStats+=[ordered]@{name=$taskSource.name;file=$taskFilename;source=$taskSource.source;sourceVisibleBounds=$taskBox;fitSize=@($taskWidth,$taskHeight);authoredOutlinePreserved=$true;borderDilation=$false;palette=$taskSource.palette}
    $taskRaw.Dispose()
}
$taskSheet=[Drawing.Bitmap]::new(64,64,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for($taskI=0;$taskI -lt 4;$taskI++){
    $taskLeft=($taskI%2)*32
    $taskTop=[int][Math]::Floor($taskI/2)*32
    for($taskY=0;$taskY -lt 32;$taskY++){
        for($taskX=0;$taskX -lt 32;$taskX++){
            $taskSheet.SetPixel(($taskLeft+$taskX),($taskTop+$taskY),$taskFrames[$taskI].GetPixel($taskX,$taskY))
        }
    }
}
$taskSheet.Save((Join-Path $taskOut 'style-calibrated-four-v1-64x64.png'),[Drawing.Imaging.ImageFormat]::Png)
$taskPreview=[Drawing.Bitmap]::new(512,512,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for($taskY=0;$taskY -lt 512;$taskY++){
    for($taskX=0;$taskX -lt 512;$taskX++){
        $taskPreview.SetPixel($taskX,$taskY,$taskSheet.GetPixel([int][Math]::Floor($taskX/8),[int][Math]::Floor($taskY/8)))
    }
}
$taskPreview.Save((Join-Path $taskOut 'style-calibrated-four-v1-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
$taskStats | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOut 'export-draft-v1.json') -Encoding utf8
$taskPreview.Dispose();$taskSheet.Dispose()
foreach($taskFrame in $taskFrames){$taskFrame.Dispose()}
Write-Output 'Saved native icons and sheet while preserving authored contours.'

