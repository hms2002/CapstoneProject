from pathlib import Path
import json
import re
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph
from reportlab.lib.enums import TA_LEFT

ROOT=Path(__file__).resolve().parents[3]
SOURCE=ROOT/'Docs/Portfolio/LightningSpearSummary.md'
OUTPUT=ROOT/'output/pdf/lightning-spear-portfolio-summary.pdf'
OUTPUT.parent.mkdir(parents=True,exist_ok=True)
pdfmetrics.registerFont(TTFont('MG','C:/Windows/Fonts/malgun.ttf'))
pdfmetrics.registerFont(TTFont('MGB','C:/Windows/Fonts/malgunbd.ttf'))
pdfmetrics.registerFontFamily('MG',normal='MG',bold='MGB',italic='MG',boldItalic='MGB')
INK=colors.HexColor('#163047'); BLUE=colors.HexColor('#1876AA'); TEAL=colors.HexColor('#147F78')
GREY=colors.HexColor('#66788A'); PALE=colors.HexColor('#EDF5FA'); LINE=colors.HexColor('#CFDEE8')
c=canvas.Canvas(str(OUTPUT),pagesize=A4)
c.setTitle('번개창 개선 한눈에 보기');c.setAuthor('CapstoneProject')

def text(x,y,s,size=10,bold=False,color=INK):
    c.setFillColor(color);c.setFont('MGB' if bold else 'MG',size);c.drawString(x,y,s)
def para(x,top,w,s,size=10,color=INK,leading=None):
    p=Paragraph(s,ParagraphStyle('p',fontName='MG',fontSize=size,leading=leading or size*1.45,textColor=color,wordWrap='CJK',alignment=TA_LEFT))
    _,h=p.wrap(w,800);p.drawOn(c,x,top-h);return h
def box(x,y,w,h,fill=PALE,stroke=LINE):
    c.setFillColor(fill);c.setStrokeColor(stroke);c.roundRect(x,y,w,h,7,fill=1,stroke=1)
def arrow(x1,y1,x2,y2,color=BLUE):
    c.setStrokeColor(color);c.setLineWidth(1.5);c.line(x1,y1,x2,y2)
    p=c.beginPath();p.moveTo(x2,y2);p.lineTo(x2-5,y2+3);p.lineTo(x2-5,y2-3);p.close()
    c.setFillColor(color);c.drawPath(p,fill=1,stroke=0)
def mini(x,y,w,label,color=INK,fill=colors.white):
    box(x,y,w,23,fill);c.setFillColor(color);c.setFont('MG',9);c.drawCentredString(x+w/2,y+8,label)

source=SOURCE.read_text(encoding='utf-8-sig')
title=source.splitlines()[0][2:]
text(38,806,title,25,True)
para(38,783,519,'같은 계산을 덜 반복하고, 세 가지 생성 패턴을 같은 도구로 만들었다.',12)

text(38,745,'1  반복 계산 줄이기',15,True)
para(38,725,519,'창을 꽂을 자리를 찾을 때, 같은 공간과 후보를 여러 번 계산하던 비용을 줄였다.',10,GREY)
box(38,538,248,165,colors.HexColor('#F4F6F8'))
box(302,538,255,165,PALE)
text(52,681,'기존  매번 다시 계산',12,True,GREY)
text(316,681,'개선  준비한 것을 재사용',12,True,BLUE)
for i,y in enumerate([640,605,570]):
    text(51,y+8,str(i+1),10,True,GREY)
    mini(68,y,59,'후보 찾기',GREY)
    arrow(129,y+11,139,y+11,GREY)
    mini(141,y,59,'전체 정렬',GREY)
    arrow(202,y+11,212,y+11,GREY)
    mini(214,y,59,'자리 확인',GREY)
mini(316,640,100,'후보 한 번 모음',BLUE)
arrow(419,651,434,651)
mini(437,640,105,'가까운 순 준비',BLUE)
text(318,617,'다음 후보부터 이어서 확인',11,True,BLUE)
text(318,594,'확인한 벽·바닥 결과 재사용',10)
text(318,571,'겹치는 자리는 비싼 검사 전에 제외',9.6)
text(38,523,'* 정확한 목표 자리가 유효하면 후보 수집도 생략. 재사용은 한 번의 위치 계획 안에서만.',8,GREY)

stats=[('43.6~52.3%','계획 시간 감소','대표 표적 사례 · 각 40회 평균'),('5회 → 1회','후보 수집','열린 바닥 · 표적 생성'),('87회 → 6회','경로·착지 확인 각각','같은 열린 바닥 사례')]
for i,(big,label,small)in enumerate(stats):
    x=38+i*176;box(x,446,167,64,colors.white)
    text(x+10,486,big,18,True,BLUE);text(x+10,467,label,9.5,True);text(x+10,453,small,7.2,color=GREY)
para(38,436,519,'모든 경우가 빨라진 것은 아니다. 바닥 없음 표적 사례는 19.8% 느려졌다. 생성 계획만 측정했으며 FPS 개선율이 아니다.',8.5,GREY)

text(38,395,'2  같은 도구를 다르게 조합하기',15,True)
para(38,374,519,'공통 기능은 도구로 만들고, 번개창은 대상·반경·간격과 사용할 순서를 전달한다.',10,GREY)
tool_labels=['후보 만들기','가까운 곳 찾기','검사·예약','반복·보충']
for i,label in enumerate(tool_labels):
    x=38+i*132;box(x,318,123,34,colors.HexColor('#EAF6F3'),colors.HexColor('#B9DAD3'))
    c.setFont('MGB',11);c.setFillColor(TEAL);c.drawCentredString(x+61.5,330,label)

rows=[('3타',['목표 주변','가까운 유효 자리','한 개 선택']),('일반 비',['무작위 후보','검사·예약','개수까지 반복']),('표적 비',['대상별 자리','검사·예약','부족분 보충'])]
for i,(name,labels)in enumerate(rows):
    y=276-i*35;text(40,y+9,name,10,True)
    for j,label in enumerate(labels):
        x=107+j*149;mini(x,y,128,label,TEAL,colors.white)
        if j<2:arrow(x+131,y+11,x+143,y+11,TEAL)
para(38,198,519,'공통 도구는 번개창 데이터·유물 이름을 모른다. 실제 창 생성·취소·정리는 기존 번개창 실행자가 맡는다.',8.7,GREY)

box(38,103,519,68,PALE)
text(52,148,'동작 보존 확인',11,True)
text(200,148,'1,048쌍 일치',17,True,BLUE)
text(400,148,'테스트 17/17',15,True,TEAL)
para(52,134,490,'위치·개수·순서·방·난수 상태·작업량 비교. 테스트 중 7개는 번개창 없이 같은 도구를 조합해 실행했다.',8.5)
para(38,91,519,'구조 조합에는 추가 비용도 있다: 반복 요청 최대 +8.9%, 새 버퍼 첫 요청 최대 +13.3%. 할당량·Player FPS는 미검증이며, 초기 시간 개선과 다른 비교다.',8,GREY)
text(38,38,'측정: Unity 6000.4.2f1 Editor / 자세한 코드·조건·원본 결과는 상세 문서 참조',7.5,color=GREY)
text(38,24,'요약 작성 2026.10.11 · 검증 자료 2026.10.07~10',7,color=GREY)
c.save()
print(OUTPUT)
