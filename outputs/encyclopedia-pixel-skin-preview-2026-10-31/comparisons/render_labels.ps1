param([string]$OutputDir,[string]$ProjectDir)
Add-Type -AssemblyName System.Drawing
$fonts=New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $ProjectDir 'Assets/_Project/Data/Localization/Fonts/Galmuri/Galmuri9.ttf'))
$format=[System.Drawing.StringFormat]::GenericTypographic.Clone()
$format.FormatFlags=[System.Drawing.StringFormatFlags]::NoWrap
foreach($kind in @('monster','boss')) {
 $bitmap=New-Object System.Drawing.Bitmap(2432,850,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
 $graphics.Clear([System.Drawing.Color]::Transparent)
 $graphics.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
 $font=New-Object System.Drawing.Font($fonts.Families[0],27,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
 $small=New-Object System.Drawing.Font($fonts.Families[0],18,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
 $ink=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#F8D9A7'))
 $muted=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#BDA992'))
 $category=if($kind -eq 'monster'){'몬스터'}else{'보스'}
 $graphics.DrawString(($category+' · 기존 화면'),$font,$ink,24,18,$format)
 $graphics.DrawString(($category+' · 새 픽셀 UI 견본'),$font,$ink,1228,18,$format)
 $graphics.DrawString('같은 크기로 비교 · 왼쪽은 기존 Unity 캡처, 오른쪽은 적용 전 합성 견본',$small,$muted,24,821,$format)
 $graphics.DrawString('실루엣·물음표는 미조우 상태 예시',$small,$muted,1810,821,$format)
 $bitmap.Save((Join-Path $OutputDir ($kind+'-labels.png')),[System.Drawing.Imaging.ImageFormat]::Png)
 $ink.Dispose();$muted.Dispose();$font.Dispose();$small.Dispose();$graphics.Dispose();$bitmap.Dispose()
}
$format.Dispose();$fonts.Dispose()
