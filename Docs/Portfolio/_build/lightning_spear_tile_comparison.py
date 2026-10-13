"""Compare legacy and optimized planning on one reused PDF grid form.

The shared form guarantees identical tiles, sprites and selected coordinates.
Algorithm labels differ; example counts and benchmark counts are kept separate.
"""
from pathlib import Path
import ast
import math
import re
from PIL import Image
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph
from lightning_spear_sprite_guide import extract_sprites

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'Docs/Portfolio/LightningSpearIllustrated.md'
OUTPUT = ROOT / 'output/pdf/lightning-spear-tile-comparison.pdf'
INK, GREY, BLUE, TEAL, AMBER, RED, LINE = map(colors.HexColor, ['#19354A','#607587','#137BA4','#197E75','#AA6B20','#B9564D','#CDDDE4'])


def main():
    md = SOURCE.read_text(encoding='utf-8-sig')
    block = md.split('## 타일 그리드로 보는 생성 위치 결정\n',1)[1].split('\n## ',1)[0]
    data = {m[1]:ast.literal_eval(m[2].strip()) for m in re.finditer(r'^\| ([^|\n]+?) \| ([^|\n]+?) \|$',block,re.M) if m[1] not in ('도식 입력','---')}
    sprites = extract_sprites()
    for name,path in [('MG','malgun.ttf'),('MGB','malgunbd.ttf')]: pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+path))
    pdfmetrics.registerFontFamily('MG',normal='MG',bold='MGB',italic='MG',boldItalic='MGB')
    OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    c=canvas.Canvas(str(OUTPUT),pagesize=(960,720))
    c.setTitle('번개창 생성 위치 비교')
    c.setAuthor('CapstoneProject')
    c.setSubject('Identical Ground/Wall grid form and original sprites; two-position example, separate benchmark evidence.')

    def text(x,y,s,size=10,bold=False,color=INK):
        c.setFillColor(color); c.setFont('MGB' if bold else 'MG',size); c.drawString(x,y,s)

    def para(x,top,w,s,size=10,color=INK):
        p=Paragraph(s,ParagraphStyle('body',fontName='MG',fontSize=size,leading=size*1.4,textColor=color,wordWrap='CJK'))
        _,h=p.wrap(w,720); p.drawOn(c,x,top-h); return h

    def sprite(key,cx,bottom,height):
        im=sprites[key]; im=im.crop(im.getbbox()); width=height*im.width/im.height
        im=im.resize((im.width*10,im.height*10),Image.Resampling.NEAREST)
        c.drawImage(ImageReader(im),cx-width/2,bottom,width=width,height=height,mask='auto')

    def panel(x,y,w,h,fill):
        c.setFillColor(fill); c.roundRect(x,y,w,h,6,fill=1,stroke=0)

    cols,rows=data['그리드']; walls=set(data['Wall 칸']); voids=set(data['바닥 없음 칸'])
    owner=data['플레이어 좌표']; target=data['몬스터 목표 좌표']; existing=data['기존 표식 좌표']
    points=[data[f'후보 {i}'] for i in (1,2,3,4)]
    cell=42
    def pt(p): return p[0]*cell,p[1]*cell

    # Draw once, then invoke the same form in both panels without any different overlays.
    c.beginForm('IDENTICAL_GRID',-23,-21,cols*cell+5,rows*cell+15)
    for col in range(cols): text((col+.5)*cell-3,rows*cell+8,chr(65+col),8,color=GREY)
    for row in range(rows):
        text(-15,(row+.5)*cell-3,str(row),8,color=GREY)
        for col in range(cols):
            c.setFillColor(colors.HexColor('#F7F8F9') if (col,row) in voids else colors.HexColor('#EDF5F2'))
            c.setStrokeColor(colors.HexColor('#ABC0C9')); c.setLineWidth(.5)
            c.rect(col*cell,row*cell,cell,cell,fill=1,stroke=1)
            if (col,row) in walls:
                c.setFillColor(colors.HexColor('#586977')); c.rect(col*cell+2,row*cell+2,cell-4,cell-4,fill=1,stroke=0)
            if (col,row) not in voids and math.dist((col+.5,row+.5),owner)<=data['생성 반경']:
                c.setFillColor(colors.HexColor('#86A8B6')); c.circle((col+.5)*cell,(row+.5)*cell,1.9,fill=1,stroke=0)
            elif (col,row) not in voids: text(col*cell+4,row*cell+17,'범위 밖',7.8,color=GREY)
    text(5.24*cell,4.72*cell,'Wall',9,True,colors.white)
    text(6.14*cell,.42*cell,'바닥 없음',8,color=GREY)
    ex,ey=pt(existing)
    c.setStrokeColor(TEAL); c.setLineWidth(1.2); c.setDash(4,2)
    c.circle(ex,ey,data['창 최소 간격']*cell,fill=0,stroke=1); c.setDash()
    sprite('mark',ex,ey,34); text(ex-24,ey+41,'기존 표식',8,True,TEAL)
    ox,oy=pt(owner); sprite('player',ox,oy,28); text(ox-20,oy-13,'플레이어',8,True)
    tx,ty=pt(target)
    c.setStrokeColor(BLUE); c.setLineWidth(1.3); c.circle(tx,ty,data['착지 검사 반경']*cell,fill=0,stroke=1)
    sprite('monster',tx,ty,24)
    text(tx-26,ty+30,'목표',8,True,BLUE)
    for i,p in enumerate(points,1):
        x,y=pt((math.floor(p[0])+(.80 if i==2 else .14),math.floor(p[1])+.82))
        c.setFillColor(RED if i<3 else TEAL); c.circle(x,y,6.5,fill=1,stroke=0)
        c.setFont('MGB',8); c.setFillColor(colors.white); c.drawCentredString(x,y-3,str(i))
        if i>=3:
            px,py=pt(p); c.setFillColor(colors.HexColor('#C5E5D8')); c.setStrokeColor(TEAL)
            c.circle(px,py,8,fill=1,stroke=1); sprite('mark',px,py,28)
    c.setFillColor(colors.HexColor('#EDF5F2')); c.setStrokeColor(colors.HexColor('#ABC0C9'))
    c.rect(0,-18,8,8,fill=1,stroke=1); text(13,-17,'Ground',8)
    c.setFillColor(colors.HexColor('#586977')); c.rect(70,-18,8,8,fill=1,stroke=0); text(83,-17,'Wall',8)
    c.setStrokeColor(TEAL); c.setDash(2,1); c.circle(132,-14,4,fill=0,stroke=1); c.setDash()
    text(143,-17,'점선 원: 기존 창의 간격 2',8,color=GREY)
    c.endForm()

    text(36,680,'번개창 생성 위치 비교',25,True)
    para(36,659,790,'같은 Ground·Wall에서 표식 두 개를 추가한다. 최종 위치는 같고, 반복하는 계산만 달라진다.',11.5)
    panel(858,650,66,51,colors.HexColor('#213546'))
    sprite('skill',880,660,31); sprite('lightning',914,654,42)
    text(54,620,'기존  매번 후보를 다시 준비',16,True,AMBER)
    text(554,620,'개선  준비한 후보와 진행을 재사용',16,True,TEAL)
    for x in (54,554):
        c.saveState(); c.translate(x,331); c.doForm('IDENTICAL_GRID'); c.restoreState()
    # Only the search-state badges differ: the tiles and sprite form stay identical.
    for i in (1,2):
        p=points[i-1]
        bx,by=pt((math.floor(p[0])+(.80 if i==2 else .14),math.floor(p[1])+.82))
        c.setFillColor(colors.HexColor('#D9E2E6')); c.setStrokeColor(GREY); c.setLineWidth(.7)
        c.circle(554+bx,331+by,6.5,fill=1,stroke=1)
        c.setFillColor(GREY); c.setFont('MGB',8); c.drawCentredString(554+bx,331+by-3,str(i))
    text(54,298,'1 간격 탈락 / 2 Wall 탈락 / 3 첫 표식 / 4 다음 표식',9.2)
    text(554,298,'회색 1·2: 다음 탐색에서 생략 / 결과는 같은 3·4',9.2,color=TEAL)

    for x,color in [(36,AMBER),(536,TEAL)]:
        panel(x,217,388,65,colors.HexColor('#F3F6F8'))
        text(x+12,262,'첫 표식   목표 → 1 → 2 → 3 선택',11,True,color)
        sequence='다음 표식   목표 → 1 → 2 → 3 → 4 선택' if x==36 else '다음 표식   목표 → 3 → 4 선택'
        text(x+12,239,sequence,11,True,color)
    text(36,201,'같은 계획에서도 1·2를 다시 검사한다.',10.5,color=AMBER)
    text(536,201,'1·2의 탐색을 되돌리지 않는다. 3은 새 간격에서 탈락.',10.5,color=TEAL)

    rows=[
        ('후보 준비','수집 2회 + 전체 정렬 2회','수집 1회 + 힙 구성 1회'),
        ('탐색 진행','다음 창도 목록 처음부터','다음 창은 유지한 후보 3부터'),
        ('간격 탈락','물리 검사 후에 간격에서 탈락','간격 먼저 확인 → 물리 검사 생략'),
    ]
    for i,(label,before,after) in enumerate(rows):
        y=172-i*24
        text(36,y,label,10,True)
        text(154,y,before,10.4,color=AMBER)
        text(536,y,after,10.4,color=TEAL)
    para(36,104,888,'기하 결과도 보관해 재조회가 필요할 때 재사용한다. 후보·캐시·진행은 한 번의 동기 위치 계획 안에서만 유지하고, 창 간격은 매번 확인한다.',9.2,GREY)
    c.setStrokeColor(LINE); c.setLineWidth(.7); c.line(36,80,924,80)
    text(36,62,'별도 실측: 열린 바닥 표적 사례',9.5,True)
    text(240,62,'후보 수집 5→1회 / 전체 정렬 5→0회 / 경로·착지 조회 각각 87→6회',10.3,True,BLUE)
    text(36,44,'대표 표적 세 사례 계획 시간 43.6~52.3% 감소(각 40회 평균). FPS 개선율 아님. 바닥 없음 표적은 19.8% 느려짐.',8.8,color=GREY)
    text(36,26,'그림은 두 위치의 계산 흐름 예시 / 실측은 별도 fixture / 실제 스프라이트 그대로 사용 / 상세 조건은 Markdown 원본 참조',8.2,color=GREY)
    c.showPage(); c.save()
    print(f'Created {OUTPUT.name}: one landscape page, identical shared grid drawn twice.')


if __name__=='__main__': main()
