"""Draw a schematic tile grid, using the Markdown example and exact game sprites.

This is a document illustration, not a Unity scene or a physics benchmark.
"""
import ast
import math
import re
from reportlab.lib import colors


def draw_grid_page(c, md, sprite, text, para):
    section = md.split('## 타일 그리드로 보는 생성 위치 결정\n', 1)[1].split('\n## ', 1)[0]
    data = {m[1]: ast.literal_eval(m[2].strip()) for m in re.finditer(r'^\| ([^|\n]+?) \| ([^|\n]+?) \|$', section, re.M) if m[1] != '도식 입력' and m[1] != '---'}
    columns, rows = data['그리드']
    walls, voids = set(data['Wall 칸']), set(data['바닥 없음 칸'])
    origin, target, existing = data['플레이어 좌표'], data['몬스터 목표 좌표'], data['기존 표식 좌표']
    candidates = [data[f'후보 {i}'] for i in (1,2,3)]
    spacing, landing = data['창 최소 간격'], data['착지 검사 반경']
    ink, grey, blue, teal, red = map(colors.HexColor, ['#19354A','#607587','#137BA4','#197E75','#B9564D'])
    ground, wall, border = map(colors.HexColor, ['#EDF5F2','#586977','#ABC0C9'])
    x0, y0, size = 54, 468, 36

    def point(position):
        return x0+position[0]*size, y0+position[1]*size

    def line(a,b,color,dashed=False):
        c.setStrokeColor(color); c.setLineWidth(1.2)
        c.setDash(3,2) if dashed else c.setDash()
        c.line(*a,*b); c.setDash()

    def cross(position):
        x,y=point(position)
        c.setStrokeColor(red); c.setLineWidth(1.8)
        c.line(x-5,y-5,x+5,y+5); c.line(x-5,y+5,x+5,y-5)

    def badge(number,cell,color):
        x,y=point((cell[0]+.19,cell[1]+.82))
        c.setFillColor(color); c.circle(x,y,7.5,fill=1,stroke=0)
        c.setFillColor(colors.white); c.setFont('MGB',8.5); c.drawCentredString(x,y-3,str(number))

    text(36,806,'타일 그리드로 보는 위치 결정',22,True)
    para(36,783,523,'Ground 중심점은 후보일 뿐이다. Wall·간격·경로·착지 조건을 통과한 자리에 표식을 남긴다.',11)
    text(36,743,'표적 생성의 최근접 탐색 예시',12,True)
    text(36,724,'목표점이 유효하면 그대로 선택 / 아래는 목표점이 실패한 경우',9,color=grey)

    for col in range(columns):
        text(x0+(col+.5)*size-3,y0+rows*size+8,chr(65+col),8,color=grey)
    for row in range(rows):
        text(x0-16,y0+(row+.5)*size-3,str(row),8,color=grey)
        for col in range(columns):
            c.setFillColor(colors.HexColor('#F7F8F9') if (col,row) in voids else ground)
            c.setStrokeColor(border); c.setLineWidth(.5)
            c.rect(x0+col*size,y0+row*size,size,size,fill=1,stroke=1)
            if (col,row) in walls:
                c.setFillColor(wall)
                c.rect(x0+col*size+2,y0+row*size+2,size-4,size-4,fill=1,stroke=0)
            if (col,row) not in voids and math.dist((col+.5,row+.5),origin)<=data['생성 반경']:
                px,py=point((col+.5,row+.5))
                c.setFillColor(colors.HexColor('#86A8B6'))
                c.circle(px,py,1.9,fill=1,stroke=0)
            elif (col,row) not in voids:
                text(x0+col*size+3,y0+row*size+14,'범위 밖',7.4,color=grey)
    text(*point((5.12,4.55)),'Wall',8,True,colors.white)
    text(*point((6.1,.4)),'바닥 없음',7.5,color=grey)

    ex,ey=point(existing)
    c.setStrokeColor(teal); c.setLineWidth(1.4); c.setDash(4,2)
    c.circle(ex,ey,spacing*size,fill=0,stroke=1); c.setDash()
    sprite('mark',ex,ey,35)
    text(ex-24,ey+41,'기존 표식',8.2,True,teal)
    ox,oy=point(origin)
    sprite('player',ox,oy,31)
    text(ox-22,oy-13,'플레이어',8.2,True)
    tx,ty=point(target)
    sprite('monster',tx,ty+18,26)
    line((tx,ty+17),(tx,ty+3),grey)
    c.setFillColor(blue); c.circle(tx,ty,3.5,fill=1,stroke=0)
    c.setStrokeColor(blue); c.setLineWidth(1.3)
    c.circle(tx,ty,landing*size,fill=0,stroke=1)
    text(tx-22,ty+49,'몬스터 목표점',7.8,True,blue)

    for i,p in enumerate(candidates,1):
        badge(i,(math.floor(p[0]),math.floor(p[1])),teal if i==3 else red)
        if i<3: cross(p)
    sx,sy=point(candidates[2])
    line((ox,oy),(sx,sy),blue,True)
    c.setFillColor(colors.HexColor('#C5E5D8')); c.setStrokeColor(teal)
    c.circle(sx,sy,10,fill=1,stroke=1)
    sprite('mark',sx,sy,25)
    text(sx-23,sy-14,'새 표식',8.2,True,teal)

    descriptions = [
        ('목표점 먼저 확인','착지 반경이 옆 Wall과 겹쳐 실패한다. 유효했다면 그 좌표를 그대로 쓴다.'),
        ('1  간격 때문에 탈락','기존 표식과 2단위보다 가깝다. 물리 검사 전에 제외한다.'),
        ('2  Wall 때문에 탈락','Ground 위에 Wall이 겹친 칸이다. 후보에 들어와도 선택할 수 없다.'),
        ('3  유효한 자리 선택','간격·벽·경로·착지·바닥 등 조건을 통과한다. 새 위치를 예약한다.'),
    ]
    for i,(heading,body) in enumerate(descriptions):
        top=680-i*55
        text(365,top,heading,10.2,True,blue if i==0 else (teal if i==3 else red))
        para(365,top-6,194,body,9.2)

    legend_y=442
    c.setFillColor(ground); c.setStrokeColor(border); c.rect(36,legend_y,10,10,fill=1,stroke=1)
    text(51,legend_y+1,'Ground',8.3)
    c.setFillColor(wall); c.rect(118,legend_y,10,10,fill=1,stroke=0)
    text(133,legend_y+1,'Wall',8.3)
    c.setFillColor(colors.HexColor('#86A8B6')); c.circle(184,legend_y+5,2,fill=1,stroke=0)
    text(193,legend_y+1,'타일 중심 후보',8.3)
    text(307,legend_y+1,'점선 원: 창 간격 / 파란 선: 경로 검사',8.2,color=grey)
    para(36,425,523,'숫자는 검사 순서이며 이동 순서가 아니다. 창 간격은 꽂힌 좌표끼리의 거리다.<br/>Wall 아래에 Ground가 있어도 생성할 수 없다. 바닥 없음 칸에는 Ground와 바닥 콜라이더가 없다.',9.3,grey)

    text(36,370,'검사와 재사용은 어디에서 하나',14,True)
    flows=[
        ('가벼운 조건 먼저','범위 → 같은 방 → 플레이어 최소 거리 → 기존·예약 창 간격'),
        ('기하 결과 재사용','캐시 → Wall 타일 → 경로 물리 검사 → 착지 물리 검사 → Ground'),
        ('선택한 위치 예약','승인된 위치를 예약하고, 다음 창은 늘어난 간격 조건을 다시 검사'),
    ]
    for i,(heading,body) in enumerate(flows):
        y=339-i*43
        text(36,y,heading,10.5,True,teal)
        text(36,y-18,body,10)
    para(36,214,523,'같은 계획에서 Ground 후보 목록·목표별 탐색 진행·기하 결과를 재사용한다.<br/>창이 추가되어 달라지는 간격은 캐시하지 않는다. 일반 번개비의 무작위 후보는 타일 중심에 한정되지 않는다.',9.8)

    c.setStrokeColor(border); c.setLineWidth(.6); c.line(36,169,559,169)
    para(36,154,523,'1칸=1단위. 이 예는 창 간격 2, 착지 반경 0.35, 경로 반경 0.25를 사용한다.<br/>같은 거리의 후보는 X → Y 순으로 고른다. 3번과 위쪽 대칭 후보가 동률이면 아래쪽 3번을 먼저 확인한다.',9.2,grey)
    para(36,113,523,'도식은 생성 위치 결정용 설명 예시다. 벽 콜라이더는 Wall 칸을 덮고 다른 장애물은 없다고 가정한다. 바닥 없음은 모든 HoleTrap에 대한 자동 필터를 뜻하지 않는다.',8.7,grey)
    text(36,57,'코드: CandidateSources / NearestPositionSearch / PlacementSession / MapQuery2D',8,color=grey)
    text(36,39,'실제 게임 스프라이트 사용 · 타일 색과 선은 설명 기호 · 2026.10.11',8,color=grey)
