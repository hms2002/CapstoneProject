from pathlib import Path
import json,hashlib
root=Path(r'C:/Users/user/Desktop/CapstoneProject')
out=Path(__file__).parent
parent=out.parent
s=(parent/'v3-purple-simple/export-draft-v3.ps1').read_text(encoding='utf-8-sig')
s=s.replace('exec-bce3280e-e655-4ae3-9ed7-8940ca37a341.png','exec-b0f4471b-9197-427b-97b6-31722f5e5002.png').replace('-v3-','-v4-')
s=s.replace('Crop=@(252,568,384,402)','Crop=@(725,79,432,470)').replace('Crop=@(290,1044,312,398)','Crop=@(157,704,314,472)')
for n in ['01-burning-feather','04-crimson-crown']:
 s=s.replace(f"File='{n}-draft-v4-32x32.png';",f"File='{n}-draft-v4-32x32.png'; Original='../{n}-draft-v1-32x32.png';")
marker='    foreach ($item in $items) {'
insert="""
        if ($item.ContainsKey('Original')) {
            $inputPath = Join-Path $outputDir $item.Original
            $targetPath = Join-Path $outputDir $item.File
            if (Test-Path -LiteralPath $targetPath) { throw "Refusing to overwrite $targetPath" }
            Copy-Item -LiteralPath $inputPath -Destination $targetPath
            $frames.Add([System.Drawing.Bitmap]::new($targetPath))
            continue
        }
"""
assert s.count(marker)==1
s=s.replace(marker,marker+insert)
(out/'export.ps1').write_text(s,encoding='utf-8-sig')
old=json.loads((parent/'verification-draft-v1.json').read_text(encoding='utf-8-sig'))
baseline={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in old['protectedHashes']}
(out/'protected-baseline.json').write_text(json.dumps(baseline,indent=2),encoding='utf-8')
print('Prepared technical export and protected-file snapshot.')

