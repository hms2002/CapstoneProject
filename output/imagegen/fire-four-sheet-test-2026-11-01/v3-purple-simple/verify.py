from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,re
root=Path(r'C:/Users/user/Desktop/CapstoneProject')
out=root/'output/imagegen/fire-four-sheet-test-2026-11-01/v3-purple-simple'
names=['불타는 깃털','녹아내린 종','매캐한 향로','홍련의 왕관']
files=['01-burning-feather-draft-v3-32x32.png','02-melted-bell-draft-v3-32x32.png','03-smoky-incense-draft-v3-32x32.png','04-crimson-crown-draft-v3-32x32.png']
fire={'#363232','#A75064','#E55C5C','#E09656','#EECF5A'}
purple={'#363232','#9D5789','#BE8EC1','#E55C5C','#E09656','#EECF5A'}
allowed=[fire,purple,purple,fire]
palette=np.asarray(Image.open(root/'.codex-attachments/7993a607-8492-44a9-ba6a-fc86a7b47c92/80706e69-39f2-4305-97ee-f1ee32f3e34a/51208.png').convert('RGBA'))
palette_hex={'#%02X%02X%02X'%tuple(x) for x in palette[palette[:,:,3]>0,:3]}
sheet=np.asarray(Image.open(out/'fire-four-draft-v3-32x128.png').convert('RGBA'))
grid=np.asarray(Image.open(out/'fire-four-draft-v3-2x2-64x64.png').convert('RGBA'))
assert sheet.shape==(128,32,4) and grid.shape==(64,64,4)
results=[]
for i,(name,fn) in enumerate(zip(names,files)):
 im=Image.open(out/fn).convert('RGBA');a=np.asarray(im);m=a[:,:,3]>0;rgb=a[:,:,:3]
 colors=sorted({'#%02X%02X%02X'%tuple(x) for x in rgb[m]})
 body=m&np.any(rgb!=[54,50,50],axis=2);padded=np.pad(body,2);dilated=np.zeros_like(body)
 for dy in range(5):
  for dx in range(5):dilated|=padded[dy:dy+32,dx:dx+32]
 b=list(im.getbbox());margins=dict(left=b[0],top=b[1],right=32-b[2],bottom=32-b[3]);sx=(i%2)*32;sy=(i//2)*32
 checks={'native32x32':im.size==(32,32),'atMostSixColors':len(colors)<=6,'onlyAssignedPalette':set(colors)<=allowed[i],'onlyUserPalette':set(colors)<=palette_hex,'binaryAlpha':set(np.unique(a[:,:,3]))=={0,255},'minimumTwoPixelTransparentMargins':min(margins.values())>=2,'twoPixelContour':np.array_equal(dilated,m),'verticalFrameExact':np.array_equal(sheet[i*32:(i+1)*32],a),'gridFrameExact':np.array_equal(grid[sy:sy+32,sx:sx+32],a)}
 if i in (1,2):checks['purpleMetalPairPresent']={'#9D5789','#BE8EC1'}<=set(colors)
 assert all(checks.values()),(fn,checks)
 results.append({'index':i+1,'name':name,'file':fn,'size':[32,32],'colors':colors,'colorCount':len(colors),'bounds':b,'margins':margins,'checks':checks})
for fn,a in [('fire-four-draft-v3-vertical-preview.png',sheet),('fire-four-draft-v3-2x2-preview.png',grid)]:
 assert np.array_equal(np.asarray(Image.open(out/fn).convert('RGBA')),np.repeat(np.repeat(a,8,0),8,1))
baseline=json.loads((out/'protected-baseline.json').read_text(encoding='utf-8-sig'))
hashes={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in baseline}
assert hashes==baseline
proof={'title':'화염 유물 4종 — 디테일 간소화·보라색 금속 시안','date':'2026-11-01','mode':'Implementation limited to disposable output draft, not project application','tool':'built-in image_gen','generationCalls':1,'generationRequestedLayout':'2x2','actualGenerationLayout':'1x4 vertical','finalSheetSize':[32,128],'sourceGeneration':'C:/Users/user/.codex/generated_images/01a17b9a-dd0d-7952-934c-baa8ac291ab7/exec-bce3280e-e655-4ae3-9ed7-8940ca37a341.png','sourceSize':[887,1774],'materialCrops':[[252,50,368,443],[252,568,384,402],[290,1044,312,398],[233,1506,425,216]],'items':results,'globalColorCount':len(set().union(*(set(x['colors']) for x in results))),'previewsExact8xNearest':True,'protectedHashes':hashes,'protectedProjectSourcesUnchanged':True,'projectApplied':False,'unityVerification':'not executed; draft only','export':'System.Drawing proportional nearest sampling, assigned exact palette, native 2px contour, lossless frame packing. Python only for read-only image checks and text evidence.','docImpact':'no doc update needed','SessionLogs':'unchanged','ErrorLog':'unchanged','DecisionLog':'unchanged','presentationHTMLAffected':False}
(out/'verification.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
rows='\n'.join('| %d / y=%d–%d | %s | [PNG](%s) | %d | %s |'%(x['index'],(x['index']-1)*32,x['index']*32-1,x['name'],x['file'],x['colorCount'],' / '.join(str(x['margins'][k]) for k in ['left','top','right','bottom'])) for x in results)
md='''# 화염 유물 4종 — 간소화·보라색 금속 시안

2026-11-01 · 테스트용 · 프로젝트 미적용

![2×2 확대 미리보기](fire-four-draft-v3-2x2-preview.png)

불꽃의 잔가지·작은 하이라이트·여러 겹의 내부 색띠를 줄이고, 기존 전기 유물처럼 큰 색면 위주로 정리했습니다. 종과 향로의 금속 몸통은 제공 팔레트의 보라색 #9D5789와 밝은 보라 #BE8EC1로 구분했습니다. 기존 유물 종류·방향·넓고 낮은 왕관의 비율을 유지하는 방향의 초안입니다.

| 순서 / 세로 구간 | 유물 | 개별 32×32 | 외곽선 포함 색 수 | 좌 / 상 / 우 / 하 여백(px) |
| --- | --- | --- | --- | --- | --- |
'''+rows+'''

- [32×128 시트 원본](fire-four-draft-v3-32x128.png)
- [세로 시트 8배 확대](fire-four-draft-v3-vertical-preview.png)
- [2×2 모아보기](fire-four-draft-v3-2x2-preview.png)
- [이전 따뜻한 색 시안](../fire-four-draft-v1-2x2-preview.png)
- [생성 프롬프트](prompt.txt)
- [기술 내보내기 스크립트](export-draft-v3.ps1)
- [검증 기록](verification.json)

깃털·왕관은 #363232 외곽선, #A75064 붉은 그림자, #E55C5C 빨강, #E09656 주황, #EECF5A 노랑의 5색을 사용합니다. 종·향로는 붉은 그림자 대신 보라색 2색을 사용해 각각 6색입니다. 시트 전체 합계는 7색이며 모두 제공 팔레트에 있습니다. 투명은 색 수에 포함하지 않습니다.

내장 image_gen으로 한 번에 네 유물을 수정했습니다. 2×2 배치로 요청했으나 실제 생성 결과는 세로 네 그림이었으며, 각 그림을 비율 보존·최근접 방식으로 32×32에 맞추고 정확한 팔레트·2픽셀 외곽선을 적용한 뒤 세로 32×128로 연결했습니다. 원본 생성 파일은 검증 기록에 있습니다.

검증: 각 32×32, 최종 시트 32×128, 유물별 6색 이하, 제공 팔레트 포함, 알파 0/255, 2픽셀 외곽선, 상하좌우 최소 2픽셀 투명 여백, 개별 PNG와 시트 구간의 픽셀 일치, 정확한 8배 최근접 확대를 확인했습니다. 기존 원본 이미지·메타데이터·유물 정의 보호 파일 10개의 해시도 기존 기준과 같습니다.

변경 파일은 이 폴더의 비교용 PNG·프롬프트·내보내기 스크립트·검증 기록·이 문서뿐입니다. Assets와 Docs는 수정하지 않았습니다. Unity import·컴파일·Play Mode 및 실제 게임 화면 검증은 실행하지 않았습니다. 현재는 시각 검토용 초안입니다.

SessionLogs / ErrorLog / DecisionLog 변경 없음. Doc Impact Check: no doc update needed. Presentation HTML 영향 없음.
'''
(out/'README.md').write_text(md,encoding='utf-8')
links=re.findall(r'\]\(([^)]+)\)',md)
assert all((out/p).exists() for p in links)
print(json.dumps({'items':[{'name':x['name'],'colors':x['colorCount'],'margins':x['margins']} for x in results],'globalColorCount':proof['globalColorCount'],'checks':'passed','protectedFiles':len(hashes),'links':len(links)},ensure_ascii=False))

