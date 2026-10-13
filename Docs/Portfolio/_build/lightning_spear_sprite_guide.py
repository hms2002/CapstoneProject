"""Render original game sprite frames beside native PDF explanatory text.

Markdown is the content source. Crops use exact Unity sprite rectangles;
nearest-neighbor scaling preserves pixels without generative repainting.
"""
from pathlib import Path
import hashlib
import json
import re
from PIL import Image
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph
from lightning_spear_grid_page import draw_grid_page

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'Docs/Portfolio/LightningSpearIllustrated.md'
ASSETS = ROOT / 'Docs/Portfolio/assets/lightning-spear-sprites'
OUTPUT = ROOT / 'output/pdf/lightning-spear-sprite-guide.pdf'
SPECS = {
    'player': ('Assets/_Project/Art/Sprites/Player/Pixel_Plains_Premium_Pack/Characters 24x24/Character 1 v1.png', 'Character 1 v1_0'),
    'monster': ('Assets/_Project/Art/Sprites/Monsters/CommonMonster/Orc Kid Sprite Sheet.png', 'Orc Kid Sprite Sheet_0'),
    'skill': ('Assets/_Project/Art/Sprites/ThirdParty/WeaponAndSandBack/WeaponSkill/LightningSpear/LinghtningSkill2Icon.png', None),
    'lightning': ('Assets/_Project/Art/Sprites/Items/Weapon/LightningSpear/LightningDropMarkEffect.png', 'LightningDropMarkEffect_1'),
    'mark': ('Assets/_Project/Art/Sprites/Items/Weapon/LightningSpear/LightningSpearClone.png', None),
}
INK, BLUE, TEAL, GREY, LINE, DARK = map(colors.HexColor, ['#19354A', '#137BA4', '#197E75', '#607587', '#D8E4EB', '#213546'])


def extract_sprites():
    ASSETS.mkdir(parents=True, exist_ok=True)
    result, records = {}, []
    for key, (relative, name) in SPECS.items():
        path = ROOT / relative
        meta = path.with_suffix(path.suffix + '.meta').read_text(encoding='utf-8-sig')
        before = hashlib.sha256(path.read_bytes()).hexdigest()
        with Image.open(path) as raw:
            raw = raw.convert('RGBA')
            if name:
                block = meta.split('      name: ' + name + '\n', 1)[1]
                rect = block.split('      alignment:', 1)[0]
                v = {k: int(re.search(r'^        ' + k + r': (\d+)$', rect, re.M)[1]) for k in ('x', 'y', 'width', 'height')}
                x, y, w, h = (v[k] for k in ('x', 'y', 'width', 'height'))
                top = raw.height - y - h
                box = (x, top, x+w, top+h)
                internal_id = int(re.search(r'^      internalID: (-?\d+)$', block, re.M)[1])
            else:
                assert re.search(r'^  spriteMode: 1$', meta, re.M)
                box = (0, 0, raw.width, raw.height)
                internal_id = 21300000
            frame = raw.crop(box)
            frame.save(ASSETS / (key + '.png'))
            result[key] = frame
        assert before == hashlib.sha256(path.read_bytes()).hexdigest()
        records.append({'key': key, 'source': relative, 'guid': re.search(r'^guid: (\w+)$', meta, re.M)[1], 'sprite': name, 'fileID': internal_id, 'png_box': box, 'source_sha256': before, 'frame_sha256': hashlib.sha256(frame.tobytes()).hexdigest()})
    (ASSETS / 'sources.json').write_text(json.dumps(records, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    return result


def main():
    sprites = extract_sprites()
    md = SOURCE.read_text(encoding='utf-8-sig')
    sections = {m[1]: m[2].strip() for m in re.finditer(r'^## ([^\n]+)\n(.*?)(?=^## |\Z)', md, re.M|re.S)}
    title = md.splitlines()[0].removeprefix('# ')
    lead = re.search(r'^\*\*(.+)\*\*$', md, re.M)[1]
    steps = re.findall(r'^\d\. \*\*(.+?)\*\*: (.+)$', sections['반복 계산을 줄인 방법'], re.M)
    patterns = re.findall(r'^\| (3타 유물 효과|일반 번개비|표적 생성 유물 효과) \| (.+?) \|$', sections['같은 도구로 세 가지 생성 구성'], re.M)
    for name, path in [('MG','malgun.ttf'),('MGB','malgunbd.ttf')]:
        pdfmetrics.registerFont(TTFont(name, 'C:/Windows/Fonts/'+path))
    pdfmetrics.registerFontFamily('MG', normal='MG', bold='MGB', italic='MG', boldItalic='MGB')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4)
    c.setTitle(title)
    c.setAuthor('CapstoneProject')
    c.setSubject('Original Unity sprite pixels; content source: LightningSpearIllustrated.md')

    def text(x, y, value, size=10, bold=False, color=INK):
        c.setFillColor(color)
        c.setFont('MGB' if bold else 'MG', size)
        c.drawString(x, y, value)

    def para(x, top, width, value, size=10, color=INK):
        p = Paragraph(value, ParagraphStyle('body',fontName='MG',fontSize=size,leading=size*1.45,textColor=color,wordWrap='CJK'))
        _, h = p.wrap(width, 600)
        p.drawOn(c, x, top-h)
        return h

    def sprite(key, cx, bottom, height, trim=True):
        im = sprites[key]
        if trim: im = im.crop(im.getbbox())
        width = height*im.width/im.height
        enlarged = im.resize((im.width*10, im.height*10), Image.Resampling.NEAREST)
        c.drawImage(ImageReader(enlarged),cx-width/2,bottom,width=width,height=height,mask='auto')

    def arrow(x1,y1,x2,y2,color=GREY):
        c.setStrokeColor(color)
        c.setLineWidth(1.2)
        c.line(x1,y1,x2,y2)
        p=c.beginPath()
        p.moveTo(x2,y2); p.lineTo(x2-5,y2+3); p.lineTo(x2-5,y2-3); p.close()
        c.setFillColor(color); c.drawPath(p,stroke=0,fill=1)

    def dot(x,y,r=3,color=LINE):
        c.setFillColor(color); c.circle(x,y,r,fill=1,stroke=0)

    def cross(x,y):
        c.setStrokeColor(colors.HexColor('#E38778')); c.setLineWidth(1.2)
        c.line(x-4,y-4,x+4,y+4); c.line(x-4,y+4,x+4,y-4)

    def panel(x,y,w,h):
        c.setFillColor(DARK); c.roundRect(x,y,w,h,6,fill=1,stroke=0)

    text(36,806,title,23,True)
    para(36,785,523,lead,11.5)
    text(36,750,'표적 생성은 어떤 일을 하나',12,True)
    panel(36,627,523,111)
    centers=[95,222,348,483]
    sprite('player',95,661,47)
    sprite('skill',222,663,44,False)
    sprite('monster',348,660,39)
    sprite('lightning',483,665,65)
    sprite('mark',483,655,22)
    for a,b in [(139,175),(265,303),(394,439)]: arrow(a,685,b,685,colors.HexColor('#8FA6B8'))
    for x,label in zip(centers,['플레이어','스킬 사용','몬스터 주변','낙뢰 표식']):
        c.setFillColor(colors.white); c.setFont('MGB',10); c.drawCentredString(x,642,label)
    para(36,617,523,'생성 범위 안에서 유효한 자리를 찾는다. 벽·구멍·기존 창과의 간격 등을 확인하고,<br/>그 위치에 낙뢰 표식을 남긴다. 표식은 이후 돌진의 목표로 사용한다.',10)

    text(36,571,'반복 계산을 줄인 방법',14,True)
    for i,(heading,body) in enumerate(steps):
        top=552-i*51
        panel(36,top-42,125,43)
        if i==0:
            sprite('monster',58,top-29,23)
            for dx in [88,107,126]: dot(dx,top-15,3,colors.HexColor('#72C8E5'))
            arrow(83,top-29,139,top-29,colors.HexColor('#7CCDC3'))
        elif i==1:
            sprite('monster',67,top-31,26)
            for dx in [96,119,142]: dot(dx,top-20,3,colors.HexColor('#72C8E5'))
            cross(96,top-20); arrow(97,top-31,121,top-31,colors.HexColor('#72C8E5'))
        else:
            sprite('mark',64,top-32,28)
            c.setStrokeColor(colors.HexColor('#72C8E5')); c.circle(64,top-20,18,stroke=1,fill=0)
            dot(78,top-20,2); cross(78,top-20); dot(128,top-20,3,colors.HexColor('#7CCDC3'))
        text(174,top-3,heading,10.7,True,BLUE)
        display_body = body.replace('를 한 번의 위치 계획', '를<br/>한 번의 위치 계획')
        display_body = display_body.replace('한다. ', '한다.<br/>', 1)
        para(174,top-10,385,display_body,9.8)

    text(36,385,'같은 도구로 세 가지 생성 구성',14,True)
    para(36,372,523,'후보 만들기·자리 찾기·검사·자리 확보·반복과 보충을 공유한다.',10,GREY)
    for i,(label,flow) in enumerate(patterns):
        y=327-i*35
        panel(36,y-11,91,33)
        if i==0:
            sprite('monster',59,y-5,23); sprite('mark',106,y-6,26)
        elif i==1:
            sprite('player',72,y-7,27)
            for cx,b in [(49,y+5),(106,y+2),(99,y-10)]: sprite('mark',cx,b,18)
        else:
            sprite('monster',68,y-7,24); sprite('mark',95,y-6,22); sprite('mark',116,y-6,22)
        text(140,y+6,label,10.5,True)
        text(140,y-9,flow,9.5,color=TEAL)
    para(36,240,523,'공통 도구는 번개창 데이터·유물 이름을 모른다. 번개창은 대상·반경·간격과<br/>호출 순서를 전달하고, 실제 생성·취소·수명을 맡는다.',9.7,GREY)

    c.setStrokeColor(LINE); c.setLineWidth(.6); c.line(36,204,559,204)
    text(36,184,'확인한 효과',12,True)
    text(36,157,'43.6~52.3% 감소',18,True,BLUE)
    text(36,141,'대표 표적 사례의 평균 위치 계획 시간',8.8,color=GREY)
    text(350,157,'1,048쌍 일치',18,True,TEAL)
    text(350,141,'도구 조합 후 동작 비교 · 테스트 17/17',8.5,color=GREY)
    text(36,120,'열린 바닥 표적: 후보 수집 5→1회 / 경로·착지 조회 각각 87→6회',9.5)
    para(36,103,523,'초기 시간 비교: Editor에서 대표 표적 세 사례 각 40회 평균. FPS 개선율이 아니다.<br/>바닥 없음 표적은 19.8% 느려졌다. 별도 구조 비교는 사례별 짝 비교 시간비 중앙값 기준:<br/>반복 요청 최대 +8.9%, 새 버퍼 첫 요청 최대 +13.3%. 할당량·Player FPS는 미검증이다.',8.3,GREY)
    text(36,44,'그림: 실제 게임 스프라이트 프레임 / 설명 기호만 추가 / 원본 모양과 색 유지',7.8,color=GREY)
    text(36,29,'2026.10.11 · 검증 자료 2026.10.07~10 · 상세 조건: Docs/Portfolio/LightningSpear.md',7.3,color=GREY)
    c.showPage()
    draw_grid_page(c, md, sprite, text, para)
    c.showPage(); c.save()
    print(f'Created {OUTPUT.name}: 2 pages, 5 exact project sprite crops and tile grid')


if __name__ == '__main__': main()
