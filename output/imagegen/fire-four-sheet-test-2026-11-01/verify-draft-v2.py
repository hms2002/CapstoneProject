from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,re
root=Path(r'C:/Users/user/Desktop/CapstoneProject')
out=root/'output/imagegen/fire-four-sheet-test-2026-11-01'
names=['불타는 깃털','녹아내린 종','매캐한 향로','홍련의 왕관']
files=['01-burning-feather-draft-v2-32x32.png','02-melted-bell-draft-v2-32x32.png','03-smoky-incense-draft-v2-32x32.png','04-crimson-crown-draft-v2-32x32.png']
fire=['#363232','#A75064','#E55C5C','#E09656','#EECF5A']
steel=['#363232','#8599B0','#C2C2C2','#E55C5C','#E09656','#EECF5A']
allowed=[fire,steel,steel,fire]
palette=np.asarray(Image.open(root/'.codex-attachments/7993a607-8492-44a9-ba6a-fc86a7b47c92/80706e69-39f2-4305-97ee-f1ee32f3e34a/51208.png').convert('RGBA'))
palette_hex={'#%02X%02X%02X'%tuple(x) for x in palette[palette[:,:,3]>0,:3]}
sheet=np.asarray(Image.open(out/'fire-four-draft-v2-32x128.png').convert('RGBA'))
grid=np.asarray(Image.open(out/'fire-four-draft-v2-2x2-64x64.png').convert('RGBA'))
assert sheet.shape==(128,32,4) and grid.shape==(64,64,4)
results=[]
for i,(name,fn) in enumerate(zip(names,files)):
 im=Image.open(out/fn).convert('RGBA'); a=np.asarray(im); m=a[:,:,3]>0
 rgb=a[:,:,:3]; colors=sorted({'#%02X%02X%02X'%tuple(x) for x in rgb[m]})
 body=m&np.any(rgb!=[54,50,50],axis=2)
 padded=np.pad(body,2)
 dilated=np.zeros_like(body)
 for dy in range(5):
  for dx in range(5):
   dilated|=padded[dy:dy+32,dx:dx+32]
 b=list(im.getbbox()); margins=dict(left=b[0],top=b[1],right=32-b[2],bottom=32-b[3])
 sx=(i%2)*32;sy=(i//2)*32
 checks={
  'native32x32':im.size==(32,32),
  'atMostSixColors':len(colors)<=6,
  'onlyAssignedPalette':set(colors)<=set(allowed[i]),
  'onlyUserPalette':set(colors)<=palette_hex,
  'binaryAlpha':set(np.unique(a[:,:,3]))=={0,255},
  'minimumTwoPixelTransparentMargins':min(margins.values())>=2,
  'twoPixelContour':np.array_equal(dilated,m),
  'verticalFrameExact':np.array_equal(sheet[i*32:(i+1)*32],a),
  'gridFrameExact':np.array_equal(grid[sy:sy+32,sx:sx+32],a)
 }
 assert all(checks.values()),(fn,checks)
 results.append({'index':i+1,'name':name,'file':fn,'size':[32,32],'colors':colors,'colorCount':len(colors),'bounds':b,'margins':margins,'checks':checks})
vp=np.asarray(Image.open(out/'fire-four-draft-v2-vertical-preview.png').convert('RGBA'))
gp=np.asarray(Image.open(out/'fire-four-draft-v2-2x2-preview.png').convert('RGBA'))
vertical_ok=np.array_equal(vp,np.repeat(np.repeat(sheet,8,axis=0),8,axis=1))
grid_ok=np.array_equal(gp,np.repeat(np.repeat(grid,8,axis=0),8,axis=1))
assert vertical_ok and grid_ok
old=json.loads((out/'verification-draft-v1.json').read_text(encoding='utf-8-sig'))
hashes={s:hashlib.sha256((root/s).read_bytes()).hexdigest() for s in old['protectedHashes']}
assert hashes==old['protectedHashes']
proof={
 'title':'화염 유물 4종 정리 시안 v2','date':'2026-11-01','mode':'test art draft only',
 'tool':'built-in image_gen','generationCalls':1,'generationLayout':'2x2','finalSheetSize':[32,128],
 'sourceGeneration':r'C:/Users/user/.codex/generated_images/01a17b9a-dd0d-7952-934c-baa8ac291ab7/exec-1a56b6f5-a03e-4a83-b0fb-f10b5a641812.png',
 'items':results,
 'globalColorCount':len(set(sum([x['colors'] for x in results],[]))),
 'verticalPreviewExact8xNearest':vertical_ok,'overviewPreviewExact8xNearest':grid_ok,
 'protectedHashes':hashes,'protectedProjectSourcesUnchanged':True,
 'export':'Uniform proportional nearest sampling from generated sheet; per-icon palette quantization; exact 2px native charcoal contour; pixel-exact frame concatenation. No hand-painted content.',
 'changes':'Simplified flame clusters; gray-blue and pale gray metal body for bell and incense; feather/crown five colors each and bell/incense six colors each.',
 'projectAssetsChanged':False,'unityVerification':'not executed; draft only',
 'docImpact':'no doc update needed','SessionLogs':'unchanged','ErrorLog':'unchanged','DecisionLog':'unchanged','presentationHTMLAffected':False
}
target=out/'verification-draft-v2.json'
assert not target.exists()
target.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'items':results,'globalColorCount':proof['globalColorCount'],'checks':'all passed','protectedFileCount':len(hashes)},ensure_ascii=False))

