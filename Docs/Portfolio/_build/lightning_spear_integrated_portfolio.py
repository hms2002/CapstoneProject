"""Build one three-page portfolio from the edited Markdown and original sprites.

Page 2 preserves the existing native PDF grid comparison, including its shared
grid form. Pages 1/3 explain the skill, intent, tool composition and evidence.
"""
from io import BytesIO
from pathlib import Path
import re
from PIL import Image
from pypdf import PdfReader, PdfWriter
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph
from lightning_spear_sprite_guide import extract_sprites
import lightning_spear_tile_comparison as comparison

ROOT=Path(__file__).resolve().parents[3]
SOURCE=ROOT/'Docs/Portfolio/LightningSpearIllustrated.md'
OUTPUT=ROOT/'output/pdf/lightning-spear-portfolio-integrated.pdf'
TMP=ROOT/'tmp/pdfs/lightning-spear-integrated-bookends.pdf'
INK,GREY,BLUE,TEAL,LINE,DARK=map(colors.HexColor,['#19354A','#607587','#137BA4','#197E75','#CDDDE4','#213546'])


def main():
    md=SOURCE.read_text(encoding='utf-8-sig')
    title=md.splitlines()[0].removeprefix('# ')
    sections={m[1]:m[2].strip() for m in re.finditer(r'^## ([^\n]+)\n(.*?)(?=^## |\Z)',md,re.M|re.S)}
    patterns=re.findall(r'^\| (3타 유물 효과|일반 번개비|표적 생성 유물 효과) \| (.+?) \|$',sections['같은 도구로 세 가지 생성 구성'],re.M)
    tools=re.findall(r'^\| (CandidateSources|MapQuery2D / PhysicsQuery2D|NearestPositionSearch|PlacementSession|PlacementFlow) \| (.+?) \|$',sections['같은 도구로 세 가지 생성 구성'],re.M)
    assert len(patterns)==3 and len(tools)==5
    comparison.main()
    sprites=extract_sprites()
    for name,path in [('MG','malgun.ttf'),('MGB','malgunbd.ttf')]: pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+path))
    pdfmetrics.registerFontFamily('MG',normal='MG',bold='MGB',italic='MG',boldItalic='MGB')
    TMP.parent.mkdir(parents=True,exist_ok=True)
    c=canvas.Canvas(str(TMP),pagesize=(960,720))

    def text(x,y,s,size=10,bold=False,color=INK):
        c.setFillColor(color);c.setFont('MGB' if bold else 'MG',size);c.drawString(x,y,s)
    def para(x,top,w,s,size=11,color=INK):
        p=Paragraph(s,ParagraphStyle('body',fontName='MG',fontSize=size,leading=size*1.45,textColor=color,wordWrap='CJK'))
        _,h=p.wrap(w,720);p.drawOn(c,x,top-h);return h
    def panel(x,y,w,h,fill=colors.HexColor('#F2F6F8')):
        c.setFillColor(fill);c.roundRect(x,y,w,h,7,fill=1,stroke=0)
    def sprite(key,cx,bottom,h):
        im=sprites[key];im=im.crop(im.getbbox());w=h*im.width/im.height
        im=im.resize((im.width*10,im.height*10),Image.Resampling.NEAREST)
        c.drawImage(ImageReader(im),cx-w/2,bottom,width=w,height=h,mask='auto')
    def arrow(x1,y,x2,color=GREY):
        c.setStrokeColor(color);c.setLineWidth(1.5);c.line(x1,y,x2,y)
        p=c.beginPath();p.moveTo(x2,y);p.lineTo(x2-6,y+4);p.lineTo(x2-6,y-4);p.close()
        c.setFillColor(color);c.drawPath(p,stroke=0,fill=1)
    def footer(page):
        text(36,40,'실제 게임 스프라이트 사용 · 타일·화살표는 설명 기호 · 검증 자료 2026.10.07~10',8.5,color=GREY)
        text(36,25,'2026.10.11 · 원본과 상세 조건: Docs/Portfolio/LightningSpearIllustrated.md',8,color=GREY)
        text(901,25,f'{page}/3',8.5,color=GREY)

    # Page 1: understand the skill and why the work was needed.
    text(36,680,title,26,True)
    para(36,656,888,'같은 낙뢰 표식을 만들면서, 반복 계산은 줄이고 기능은 공통 도구로 나눴다.',12)
    text(36,613,'1  스킬은 어떤 일을 하나',16,True)
    panel(36,456,888,140,DARK)
    text(52,576,'표적 생성 유물 효과의 예시',10,True,colors.HexColor('#B4D3E4'))
    sprite('player',155,494,63);sprite('skill',370,496,60);sprite('monster',582,493,51)
    sprite('lightning',792,493,88);sprite('mark',792,485,29)
    for a,b in [(225,296),(437,506),(651,722)]:arrow(a,526,b,colors.HexColor('#8FA6B8'))
    for x,label in [(155,'플레이어'),(370,'낙뢰 표식 사용'),(582,'몬스터 주변 위치 결정'),(792,'창 생성·표식 유지')]:
        c.setFillColor(colors.white);c.setFont('MGB',11);c.drawCentredString(x,471,label)
    para(36,443,888,'낙뢰 표식은 창을 떨어뜨리고, 남은 표식을 돌진 목표로 사용하는 스킬이다.<br/>일반 번개비는 주변 무작위 배치, 표적 생성은 유물 효과로 대상 주변을 우선한다. 3타 표식도 해당 유물 효과가 있을 때 생성된다.',11)

    text(36,384,'무엇을 바꾸려 했나',16,True)
    goals=[('반복 계산 감소','같은 범위의 후보 수집·정렬과<br/>같은 위치의 지형 검사를 줄인다.'),('기능을 도구로 분리','후보 공급·탐색·검사·반복을<br/>여러 구성에서 사용하는 도구로 만든다.'),('기존 동작 보존','위치·개수·순서·난수와<br/>실제 생성·취소·수명을 유지한다.')]
    for i,(label,body) in enumerate(goals):
        x=36+i*300;panel(x,289,288,77)
        text(x+14,344,label,12,True,BLUE);para(x+14,332,260,body,10.7)
    text(36,264,'유지한 배치 규칙',14,True)
    rules=[('정확한 목표점 우선','유효하면 그 좌표를 그대로 선택한다. 타일 후보 수집도 생략할 수 있다.'),('Ground는 후보, Wall은 제한','Ground가 있어도 Wall·경로·착지 등 조건에서 탈락할 수 있다.'),('간격과 범위는 계속 확인','같은 방·생성 반경·플레이어 거리·기존 창과 새로 예약한 창의 간격을 검사한다.')]
    for i,(label,body) in enumerate(rules):
        y=234-i*42;text(36,y,label,11,True,TEAL);text(256,y,body,10.6)
    panel(36,81,888,38)
    text(50,95,'다음 페이지: 같은 그리드와 같은 결과에서, 다음 창을 찾는 계산이 어떻게 줄었는지 비교한다.',10.7,True,BLUE)
    footer(1);c.showPage()

    # Page 3: reusable tools, responsibility boundaries and honest validation.
    text(36,680,'기능을 도구로 나누고 동작을 검증',24,True)
    para(36,657,888,'알고리즘 최적화 뒤에, 좌표·수치·맵 조건을 받는 공통 도구로 세 생성 방식을 구성했다.',11.5)
    text(36,620,'3  번개창은 규칙을 전달하고, 도구는 기능을 수행',14,True)
    labels=['후보 만들기','맵·물리 조회','가까운 자리 찾기','검사·예약','반복·보충']
    summaries=['좌표·타일·고정점,<br/>지연 난수 후보 만들기','바닥·벽·방 확인,<br/>물리 조건 조회','목표점·가까운 후보 찾기,<br/>탐색 진행 재사용','후보 검사·기하 결과,<br/>예약·계획 결과 관리','첫 선택·제한 수집,<br/>순환·부족분 보충']
    for i,((name,body),label) in enumerate(zip(tools,labels)):
        x=36+i*179;panel(x,497,171,103)
        text(x+11,576,label,12,True,TEAL)
        para(x+11,564,149,name,8.2,GREY)
        para(x+11,536,149,summaries[i],9.5)
    text(36,470,'같은 도구, 세 가지 구성',14,True)
    for i,(label,flow) in enumerate(patterns):
        y=427-i*47;panel(36,y-9,124,37,DARK)
        if i==0:sprite('monster',66,y-4,25);sprite('mark',129,y-4,27)
        elif i==1:
            sprite('player',85,y-4,27)
            for x,b in [(55,y+3),(127,y+2),(119,y-6)]:sprite('mark',x,b,19)
        else:sprite('monster',71,y-4,25);sprite('mark',117,y-3,23);sprite('mark',143,y-3,23)
        text(178,y+11,label,11.3,True);text(178,y-6,flow,10.5,color=TEAL)
        if i==0:text(597,y+11,'탐색 실패 시 고정점·난수 보충 순서도 유지',9,color=GREY)
    text(36,303,'계획과 실행의 소유권',11,True,BLUE)
    para(192,315,732,'공통 도구는 위치 계획·검사·예약을 맡는다. 번개창 실행자는 Actor 생성·대기·취소·수명 정리를 맡는다.<br/>지연 생성 전에 계획을 복사해 다음 요청의 버퍼 재사용과 구별한다.',10)
    c.setStrokeColor(LINE);c.setLineWidth(.6);c.line(36,277,924,277)
    text(36,249,'1,048쌍 일치',21,True,BLUE);text(480,249,'선택한 테스트 17/17',21,True,TEAL)
    text(36,229,'위치·개수·순서·방·난수·작업량 비교',10,color=GREY)
    text(480,229,'Unity Play Mode 검증 · 비무기 도구 구성 7개 포함',10,color=GREY)
    para(36,205,888,'공통 도구에는 번개창 데이터·유물·GAS 참조가 없다. 다른 좌표·수치 구성으로 실행하는 테스트를 통과했다.<br/>현재 실제 콘텐츠 연결은 번개창 하나이며, 다른 콘텐츠의 마이그레이션 실적은 아직 없다.',10.3)
    panel(36,83,888,83)
    text(50,145,'구조 개선은 재사용성과 책임 분리를 위한 변경',11,True,BLUE)
    para(50,136,859,'이미 최적화된 코드와 비교한 별도 측정이다. 사례별 짝 비교 시간비 중앙값의 최댓값은 반복 요청 +8.9%, 새 버퍼 첫 요청 +13.3%였다.<br/>초기 43.6~52.3% 시간 감소와 합산하지 않는다. 개별 호출의 최대 지연을 뜻하지 않는다. 할당량·Player FPS는 미검증이다.',9.8,GREY)
    footer(3);c.showPage();c.save()

    ends=PdfReader(str(TMP));middle=PdfReader(str(comparison.OUTPUT))
    stamp=BytesIO();overlay=canvas.Canvas(stamp,pagesize=(960,720))
    overlay.setFont('MG',8.5);overlay.setFillColor(GREY);overlay.drawString(901,26,'2/3');overlay.save()
    stamp.seek(0);middle.pages[0].merge_page(PdfReader(stamp).pages[0])
    writer=PdfWriter()
    for page in (ends.pages[0],middle.pages[0],ends.pages[1]):writer.add_page(page)
    writer.add_metadata({'/Title':title,'/Author':'CapstoneProject','/Subject':'Skill intent, identical-grid algorithm comparison, reusable tools and evidence. Source: LightningSpearIllustrated.md.'})
    for i,label in enumerate(['스킬 동작과 개선 의도','같은 그리드의 기존·개선 비교','공통 도구 구성과 검증']):writer.add_outline_item(label,i)
    OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    with OUTPUT.open('wb') as file:writer.write(file)
    print(f'Created {OUTPUT.name}: 3 native-text landscape pages.')


if __name__=='__main__':main()
