from pathlib import Path
import html
import json
import re
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.graphics.shapes import Drawing, Rect, String, Line, Polygon
from reportlab.graphics.charts.barcharts import HorizontalBarChart
from reportlab.graphics import renderPDF
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image, PageBreak

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'Docs/Portfolio/LightningSpear.md'
TEMP = ROOT / 'tmp/pdfs/lightning-portfolio'
ASSETS = ROOT / 'Docs/Portfolio/assets'
OUTPUT = ROOT / 'output/pdf/lightning-spear-portfolio.pdf'
TEMP.mkdir(parents=True, exist_ok=True)
ASSETS.mkdir(parents=True, exist_ok=True)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('Malgun', 'C:/Windows/Fonts/malgun.ttf'))
pdfmetrics.registerFont(TTFont('MalgunBold', 'C:/Windows/Fonts/malgunbd.ttf'))
pdfmetrics.registerFontFamily('Malgun', normal='Malgun', bold='MalgunBold', italic='Malgun', boldItalic='MalgunBold')
INK = colors.HexColor('#172B40')
BLUE = colors.HexColor('#176C9B')
MUTED = colors.HexColor('#5B6975')
PALE = colors.HexColor('#EDF4F8')

def make_charts():
    summary = json.loads((ROOT/'Docs/SessionLogs/2026-10-07-test-scene/summary.json').read_text(encoding='utf-8-sig'))
    by_case = {r['Case']:r for r in summary}
    cases = ['open-targeted','overlapping-floor-targeted','walls-and-holes-targeted','open-random','no-ground-targeted']
    # Values are means of 40 requests, not medians. Independent stage comparisons are not combined.
    d = Drawing(510,250)
    chart = HorizontalBarChart()
    chart.x,chart.y,chart.width,chart.height = 132,38,305,174
    chart.data = [[by_case[c]['BaseMs'] for c in cases],[by_case[c]['OptMs'] for c in cases]]
    chart.categoryAxis.categoryNames = ['열린 바닥 표적','중첩 바닥 표적','벽·구멍 표적','열린 바닥 무작위','바닥 없음 표적']
    chart.categoryAxis.labels.fontName = 'Malgun'
    chart.categoryAxis.labels.fontSize = 8.5
    chart.valueAxis.valueMin,chart.valueAxis.valueMax,chart.valueAxis.valueStep = 0,5,1
    chart.valueAxis.labels.fontName = 'Malgun'
    chart.valueAxis.labels.fontSize = 8
    chart.valueAxis.strokeColor = colors.HexColor('#CAD4DC')
    chart.categoryAxis.strokeColor = colors.HexColor('#CAD4DC')
    chart.groupSpacing,chart.barSpacing = 6,1
    chart.bars[0].fillColor,chart.bars[1].fillColor = colors.HexColor('#8393A4'),BLUE
    chart.bars[0].strokeColor=chart.bars[1].strokeColor=None
    chart.barLabels.fontName='Malgun'
    chart.barLabels.fontSize=7
    chart.barLabels.nudge=5
    chart.barLabels.boxAnchor='w'
    chart.barLabelFormat='%.3f'
    d.add(chart)
    d.add(String(0,234,'생성 위치 계획의 평균 시간',fontName='MalgunBold',fontSize=13,fillColor=INK))
    d.add(Rect(302,230,10,7,fillColor=colors.HexColor('#8393A4'),strokeColor=None))
    d.add(String(316,229,'기존 전체 정렬',fontName='Malgun',fontSize=8,fillColor=MUTED))
    d.add(Rect(407,230,10,7,fillColor=BLUE,strokeColor=None))
    d.add(String(421,229,'최적화',fontName='Malgun',fontSize=8,fillColor=MUTED))
    d.add(String(132,17,'평균 계획 시간 (ms) / 각 사례 40개 seed',fontName='Malgun',fontSize=8,fillColor=MUTED))
    renderPDF.drawToFile(d,str(TEMP/'timing-chart.pdf'))

    g=Drawing(510,232)
    def box(x,y,w,h,lines,fill=PALE):
        g.add(Rect(x,y,w,h,rx=5,ry=5,fillColor=fill,strokeColor=colors.HexColor('#B8CDD9')))
        for i,label in enumerate(lines):
            g.add(String(x+w/2,y+h/2+(len(lines)-1)*6-i*12-3,label,textAnchor='middle',fontName='MalgunBold' if i==0 else 'Malgun',fontSize=9,fillColor=INK))
    def arrow(x1,y1,x2,y2):
        g.add(Line(x1,y1,x2,y2,strokeColor=BLUE,strokeWidth=1.3))
        if y2<y1:g.add(Polygon([x2,y2,x2-3,y2+6,x2+3,y2+6],fillColor=BLUE,strokeColor=None))
        else:g.add(Polygon([x2,y2,x2-6,y2-3,x2-6,y2+3],fillColor=BLUE,strokeColor=None))
    box(0,167,115,56,['번개창 연결','대상·수치·순서 입력'])
    box(142,167,156,56,['PlacementFlow','첫 성공 / 제한 수집 / 순환'])
    arrow(115,195,142,195)
    box(325,167,185,56,['기존 실행 소유자','Actor 생성·지연·취소·정리'])
    arrow(298,195,325,195)
    box(142,89,108,49,['후보 공급','고정점·대상·타일·난수'])
    box(270,89,108,49,['최근접 탐색','정확점·힙·진행 재사용'])
    box(398,89,112,49,['검사와 예약','기하 캐시·결과'])
    arrow(196,167,196,138)
    g.add(Line(220,158,455,158,strokeColor=BLUE,strokeWidth=1.3))
    g.add(Line(220,167,220,158,strokeColor=BLUE,strokeWidth=1.3))
    arrow(324,158,324,138);arrow(455,158,455,138)
    box(205,12,257,44,['공통 공간 조회','MapQuery2D / PhysicsQuery2D'])
    for x in [196,324,455]:
        g.add(Line(x,89,x,70,strokeColor=BLUE,strokeWidth=1.3))
    g.add(Line(196,70,455,70,strokeColor=BLUE,strokeWidth=1.3))
    arrow(334,70,334,56)
    g.add(String(0,107,'같은 도구를',fontName='Malgun',fontSize=9,fillColor=MUTED))
    g.add(String(0,93,'다른 입력으로 조합',fontName='Malgun',fontSize=9,fillColor=MUTED))
    renderPDF.drawToFile(g,str(TEMP/'composition-chart.pdf'))

def inline(text):
    # Keep all document wording in the Markdown source; PDF styling only projects it.
    def link(m):
        label=m.group(1);target=m.group(2)
        absolute=(SOURCE.parent/target.split('#')[0]).resolve()
        return f'<link href="{absolute.as_uri()}" color="#176C9B">{html.escape(label)}</link>'
    tokens=[]
    def protect(m):tokens.append(link(m));return f'@@LINK{len(tokens)-1}@@'
    text=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',protect,text)
    text=html.escape(text)
    text=re.sub(r'`([^`]+)`',r'<font color="#176C9B">\1</font>',text)
    text=re.sub(r'\*\*([^*]+)\*\*',r'<b>\1</b>',text)
    for i,t in enumerate(tokens):text=text.replace(f'@@LINK{i}@@',t)
    return text

BODY=ParagraphStyle('Body',fontName='Malgun',fontSize=10.1,leading=14.4,textColor=INK,spaceAfter=6,wordWrap='CJK',alignment=TA_LEFT)
H1=ParagraphStyle('Title',parent=BODY,fontName='MalgunBold',fontSize=22,leading=29,spaceAfter=16)
H2=ParagraphStyle('Heading',parent=BODY,fontName='MalgunBold',fontSize=17,leading=23,spaceBefore=2,spaceAfter=12,keepWithNext=True)
H3=ParagraphStyle('Subheading',parent=BODY,fontName='MalgunBold',fontSize=11.5,leading=17,spaceBefore=5,spaceAfter=6,keepWithNext=True)
CELL=ParagraphStyle('Cell',parent=BODY,fontSize=8.7,leading=11.4,spaceAfter=0)
SMALL=ParagraphStyle('Small',parent=BODY,fontSize=8.2,leading=12,textColor=MUTED,spaceAfter=6)

def table(lines):
    rows=[[x.strip() for x in line.strip().strip('|').split('|')] for line in lines]
    rows=[r for r in rows if not all(re.fullmatch(r':?-+:?',x) for x in r)]
    n=len(rows[0]);width=511
    if n==2:widths=[132,379]
    elif n==3:widths=[144,168,199]
    elif n==4:widths=[211,100,100,100]
    else:widths=[width/n]*n
    data=[[Paragraph(inline(x),CELL)for x in r]for r in rows]
    t=Table(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),PALE),('LINEBELOW',(0,0),(-1,0),.7,colors.HexColor('#B9CDD9')),('LINEBELOW',(0,1),(-1,-1),.3,colors.HexColor('#DCE5EB')),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),3.5),('BOTTOMPADDING',(0,0),(-1,-1),3.5)]))
    return t

def build():
    story=[];lines=SOURCE.read_text(encoding='utf-8-sig').splitlines();i=0
    while i<len(lines):
        line=lines[i].strip()
        if not line:i+=1;continue
        if line=='<!-- pagebreak -->':story.append(PageBreak());i+=1;continue
        if line.startswith('|'):
            rows=[]
            while i<len(lines)and lines[i].strip().startswith('|'):rows.append(lines[i]);i+=1
            story.extend([table(rows),Spacer(1,9)]);continue
        if line.startswith('!['):
            m=re.match(r'!\[([^]]*)\]\(([^)]+)\)',line);img=Image(str(SOURCE.parent/m.group(2)));scale=511/img.imageWidth;img.drawWidth=511;img.drawHeight=img.imageHeight*scale
            story.extend([img,Spacer(1,8)]);i+=1;continue
        if line.startswith('### '):story.append(Paragraph(inline(line[4:]),H3));i+=1;continue
        if line.startswith('## '):story.append(Paragraph(inline(line[3:]),H2));i+=1;continue
        if line.startswith('# '):
            story.append(Paragraph('CAPSTONE PROJECT · GAMEPLAY PROGRAMMING',SMALL));story.append(Paragraph(inline(line[2:]),H1));i+=1;continue
        if line.startswith('- '):story.append(Paragraph('• '+inline(line[2:]),BODY));i+=1;continue
        paragraph=[line];i+=1
        while i<len(lines)and lines[i].strip()and not lines[i].strip().startswith(('#','|','![','- ','<!--')):paragraph.append(lines[i].strip());i+=1
        text=' '.join(paragraph);style=SMALL if text.startswith(('근거:','원본 자료:','구현 근거:')) else BODY
        story.append(Paragraph(inline(text),style))
    def decoration(canvas,doc):
        canvas.saveState();canvas.setFont('Malgun',8);canvas.setFillColor(MUTED)
        canvas.drawString(42,814,'번개창 생성 위치 최적화와 공통 도구 조합 설계')
        canvas.setStrokeColor(colors.HexColor('#DCE5EB'));canvas.line(42,804,553,804)
        canvas.drawString(42,24,'Unity 6000.4.2f1 · C# · 검증 기준 2026.10.10')
        canvas.drawRightString(553,24,str(doc.page));canvas.restoreState()
    doc=SimpleDocTemplate(str(OUTPUT),pagesize=A4,rightMargin=42,leftMargin=42,topMargin=53,bottomMargin=43,title='번개창 생성 위치 최적화와 공통 도구 조합 설계',author='CapstoneProject',subject='Gameplay algorithm optimization and reusable placement tools')
    doc.build(story,onFirstPage=decoration,onLaterPages=decoration)
    print(OUTPUT)

if __name__=='__main__':
    import sys
    if '--charts' in sys.argv:make_charts()
    else:build()
