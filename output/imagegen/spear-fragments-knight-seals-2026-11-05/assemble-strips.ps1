$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskOut=$PSScriptRoot
$taskJobs=Get-Content -LiteralPath (Join-Path $taskOut 'generation-sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskVertical=[Drawing.Bitmap]::new(32,128,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$taskHorizontal=[Drawing.Bitmap]::new(128,32,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for($i=0;$i -lt 4;$i++) {
 $native=[Drawing.Bitmap]::new((Join-Path $taskOut ($taskJobs[$i].key+'-32x32.png')))
 $preview=[Drawing.Bitmap]::new(256,256,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 for($y=0;$y -lt 32;$y++) { for($x=0;$x -lt 32;$x++) {
  $color=$native.GetPixel($x,$y)
  $taskVertical.SetPixel($x,($y+$i*32),$color)
  $taskHorizontal.SetPixel(($x+$i*32),$y,$color)
  for($dy=0;$dy -lt 8;$dy++) { for($dx=0;$dx -lt 8;$dx++) { $preview.SetPixel(($x*8+$dx),($y*8+$dy),$color) } }
 }}
 $preview.Save((Join-Path $taskOut ($taskJobs[$i].key+'-preview.png')),[Drawing.Imaging.ImageFormat]::Png)
 $preview.Dispose();$native.Dispose()
}
$taskVertical.Save((Join-Path $taskOut 'four-relics-32x128.png'),[Drawing.Imaging.ImageFormat]::Png)
$taskHorizontal.Save((Join-Path $taskOut 'four-relics-128x32.png'),[Drawing.Imaging.ImageFormat]::Png)
$taskVertical.Dispose();$taskHorizontal.Dispose()
