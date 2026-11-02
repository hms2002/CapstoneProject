param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$draftDir = 'C:/Users/user/Desktop/CapstoneProject/output/imagegen/fire-four-sheet-test-2026-11-01/v2-simple-metal'
$generatedPath = 'C:/Users/user/.codex/generated_images/01a17b9a-dd0d-7952-934c-baa8ac291ab7/exec-c35ee505-b001-4c2c-bcc7-9a9c01dc2176.png'
$source = [Drawing.Bitmap]::new($generatedPath)
$outline = [Drawing.ColorTranslator]::FromHtml('#363232')
$warm = @('#A75064','#E55C5C','#E09656','#EECF5A')
$metal = @('#8599B0','#C2C2C2','#E55C5C','#E09656','#EECF5A')
$items = @(
    @{name='불타는 깃털';file='01-burning-feather-draft-v2-32x32.png';crop=@(84,74,439,510);colors=$warm},
    @{name='녹아내린 종';file='02-melted-bell-draft-v2-32x32.png';crop=@(709,69,464,514);colors=$metal},
    @{name='매캐한 향로';file='03-smoky-incense-draft-v2-32x32.png';crop=@(141,696,342,490);colors=$metal},
    @{name='홍련의 왕관';file='04-crimson-crown-draft-v2-32x32.png';crop=@(684,825,512,304);colors=$warm}
)
$sheet = [Drawing.Bitmap]::new(32,128,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$grid = [Drawing.Bitmap]::new(64,64,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for($i=0;$i -lt $items.Count;$i++){
    $item=$items[$i]
    $crop=$item.crop
    $scale=[Math]::Min(24.0/$crop[2],24.0/$crop[3])
    $width=[Math]::Max(1,[int][Math]::Round($crop[2]*$scale))
    $height=[Math]::Max(1,[int][Math]::Round($crop[3]*$scale))
    $left=[int][Math]::Floor((32-$width)/2.0)
    $top=[int][Math]::Floor((32-$height)/2.0)
    $material=[Drawing.Bitmap]::new(32,32,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $allowed=@($item.colors | ForEach-Object { [Drawing.ColorTranslator]::FromHtml($_) })
    for($y=0;$y -lt $height;$y++){
        $sy=$crop[1]+[int][Math]::Round($y*($crop[3]-1)/[Math]::Max(1,$height-1))
        for($x=0;$x -lt $width;$x++){
            $sx=$crop[0]+[int][Math]::Round($x*($crop[2]-1)/[Math]::Max(1,$width-1))
            $c=$source.GetPixel($sx,$sy)
            if($c.A -lt 200 -or [Math]::Max($c.R,[Math]::Max($c.G,$c.B)) -le 90){continue}
            $best=$allowed[0]
            $distance=[double]::MaxValue
            foreach($candidate in $allowed){
                $d=($c.R-$candidate.R)*($c.R-$candidate.R)+($c.G-$candidate.G)*($c.G-$candidate.G)+($c.B-$candidate.B)*($c.B-$candidate.B)
                if($d -lt $distance){$distance=$d;$best=$candidate}
            }
            $material.SetPixel($left+$x,$top+$y,$best)
        }
    }
    $native=[Drawing.Bitmap]::new(32,32,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for($y=0;$y -lt 32;$y++){
        for($x=0;$x -lt 32;$x++){
            if($material.GetPixel($x,$y).A -eq 0){continue}
            for($dy=-2;$dy -le 2;$dy++){
                for($dx=-2;$dx -le 2;$dx++){
                    $nx=$x+$dx;$ny=$y+$dy
                    if($nx -ge 0 -and $nx -lt 32 -and $ny -ge 0 -and $ny -lt 32){$native.SetPixel($nx,$ny,$outline)}
                }
            }
        }
    }
    for($y=0;$y -lt 32;$y++){
        for($x=0;$x -lt 32;$x++){
            $c=$material.GetPixel($x,$y)
            if($c.A -gt 0){$native.SetPixel($x,$y,$c)}
            $p=$native.GetPixel($x,$y)
            $sheet.SetPixel($x,$i*32+$y,$p)
            $grid.SetPixel(($i%2)*32+$x,[int][Math]::Floor($i/2.0)*32+$y,$p)
        }
    }
    $native.Save((Join-Path $draftDir $item.file),[Drawing.Imaging.ImageFormat]::Png)
    $material.Dispose()
    $native.Dispose()
}
$sheet.Save((Join-Path $draftDir 'fire-four-draft-v2-32x128.png'),[Drawing.Imaging.ImageFormat]::Png)
$grid.Save((Join-Path $draftDir 'fire-four-draft-v2-2x2-64x64.png'),[Drawing.Imaging.ImageFormat]::Png)
foreach($entry in @(
    @{bitmap=$sheet;file='fire-four-draft-v2-vertical-preview.png'},
    @{bitmap=$grid;file='fire-four-draft-v2-2x2-preview.png'}
)){
    $b=$entry.bitmap
    $preview=[Drawing.Bitmap]::new($b.Width*8,$b.Height*8,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for($y=0;$y -lt $preview.Height;$y++){
        for($x=0;$x -lt $preview.Width;$x++){
            $preview.SetPixel($x,$y,$b.GetPixel([int][Math]::Floor($x/8.0),[int][Math]::Floor($y/8.0)))
        }
    }
    $preview.Save((Join-Path $draftDir $entry.file),[Drawing.Imaging.ImageFormat]::Png)
    $preview.Dispose()
}
$source.Dispose()
$sheet.Dispose()
$grid.Dispose()
Write-Output 'Exported four native 32x32 icons, vertical 32x128 sheet and nearest-neighbor previews.'

