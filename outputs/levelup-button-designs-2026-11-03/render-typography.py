"""Rasterize the project's font for Aseprite's separate type layers."""
from pathlib import Path
import os, sys, re, json
sys.path.insert(0, str(Path(os.environ['TEMP']) / 'codex-art-pillow'))
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
TYPE = OUT / 'typography'
TYPE.mkdir(exist_ok=True)
FONT = ROOT / 'Assets/_Project/Data/Localization/Fonts/Galmuri/Galmuri9.ttf'

def text(name, value, size, color, max_width=None):
    font = ImageFont.truetype(str(FONT), size)
    if max_width:
        while font.getlength(value) > max_width and size > 12:
            size -= 1
            font = ImageFont.truetype(str(FONT), size)
    box = font.getbbox(value)
    im = Image.new('RGBA', (max(1, box[2]-box[0]), max(1, box[3]-box[1])))
    ImageDraw.Draw(im).text((-box[0], -box[1]), value, font=font, fill=color)
    im.putalpha(im.getchannel('A').point(lambda a: 255 if a >= 100 else 0))
    im.save(TYPE / (name+'.png'))

palettes = ['#F1DEC0', '#443125', '#DFE5E3', '#DBE8FF', '#382B31', '#89858C']
for i,col in enumerate(palettes, 1):
    for key, value, size in [('close','닫기',16),('reroll','리롤',16),('esc','ESC',10),('r','R',12),('count','5/5',12),('zero','0/5',12)]:
        text(f'{i}-{key}',value,size,col)

text('title','Level UP',48,'#FFFFFF')
text('current-close','[ESC] 닫기',32,'#0D0D0D')
text('current-reroll','[R] 리롤 5/5',32,'#0D0D0D')
text('board-title','LEVEL UP / 하단 버튼 디자인',42,'#EFE8DB')
text('board-sub','ASEPRITE  ·  5가지 시안  ·  닫기 + 리롤  ·  확대 비교',22,'#A9ABB8')
text('board-foot','원본 픽셀 3배 확대 / 게임 적용 시 2배 / 글자·키·배경 분리',19,'#9396A5')
text('screen-board-title','현재 구성 + 5가지 적용 목업',36,'#EFE8DB')
text('screen-board-sub','프리팹 배치와 기존 게임 배경을 사용한 재구성 · 실제 플레이 캡처 아님',20,'#AAAAB8')
names = ['A  고전 금테','B  양피지','C  철제','D  마력석','E  카드 탭']
descs = ['차분한 검정 + 금빛 테두리','밝은 종이 + 따뜻한 갈색','각진 금속 + 묵직한 음영','보랏빛 바탕 + 청록 포인트','카드 프레임 + 붉은 책갈피']
for i,(name,desc) in enumerate(zip(names,descs),1):
    text(f'option-{i}',name,27,'#EFE8DB')
    text(f'desc-{i}',desc,18,'#AAACB9')
    text(f'caption-{i}',name+' / 적용 목업',24,'#EFE8DB')
text('caption-0','현재 구성 / 회색 버튼',24,'#EFE8DB')
text('mockup-note','DESIGN MOCKUP · 프리팹 기반 재구성',17,'#9EA1B2')

samples = [
 ('강철의 수련', ['최대 체력 -2','피해를 4회 받으면 저주가 풀리고','기존 최대 체력보다','최대 체력 +2']),
 ('사냥의 흐름', ['5초 안에 적 5명을 처치하면','5초 동안 공격 속도 +20%','효과 중 적을 처치 시 지속 시간 초기화']),
 ('벌컥벌컥', ['포션을 사용할 때마다','공격 속도가 2% 증가'])
]
for i,(title,lines) in enumerate(samples,1):
    text(f'card-{i}-title',title,32,'#20221F')
    for j,line in enumerate(lines,1):
        color = '#A8324A' if i==1 and j==1 else '#197A67' if (i==1 and j==4) or (i==2 and j==2) else '#20221F'
        text(f'card-{i}-{j}',line,30,color,max_width=398)
print('Typography exported from project Galmuri9; no game files modified.')
