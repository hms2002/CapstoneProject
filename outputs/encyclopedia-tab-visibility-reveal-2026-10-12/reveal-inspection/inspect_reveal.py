"""Read-only source alpha/serialized RectTransform inspection. Writes evidence only."""
from pathlib import Path
import re, json, math
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
PREFAB = ROOT / 'Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab'
ART = ROOT / 'Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites'
FRAMES = ART / 'Inventory Book/Book Content Appear/Style 1'
text = PREFAB.read_text(encoding='utf-8-sig')
blocks = {}
for kind, ident, body in re.findall(r'^--- !u!(\d+) &(\d+)\s*\n(.*?)(?=^--- !u!|\Z)', text, re.M|re.S):
    blocks[int(ident)] = (int(kind), body)
def scalar(body, key, default=0):
    m = re.search(r'^  '+re.escape(key)+r': (.+)$', body, re.M)
    return m.group(1) if m else default
def ref(body, key):
    m = re.search(r'^  '+re.escape(key)+r': \{fileID: (\d+)', body, re.M)
    return int(m.group(1)) if m else 0
def vec(body, key, default):
    m = re.search(r'^  '+re.escape(key)+r': \{([^}]+)', body, re.M)
    return {k:float(v) for k,v in re.findall(r'([xyz]): ([^,}]+)', m.group(1))} if m else default
gos={i: {'name':scalar(b,'m_Name',''), 'active':scalar(b,'m_IsActive','1')} for i,(k,b) in blocks.items() if k==1}
rects={i: {'body':b, 'go':ref(b,'m_GameObject'), 'father':ref(b,'m_Father')} for i,(k,b) in blocks.items() if k==224}
by_go={r['go']:i for i,r in rects.items()}
book=next(i for i,r in rects.items() if gos[r['go']]['name']=='Book')
def path(i):
    r=rects[i]; name=gos[r['go']]['name']
    return path(r['father'])+'/'+name if r['father'] in rects else name
cache={book: (-1160.,-740.,2320.,1480.)}
def bounds(i):
    if i in cache:return cache[i]
    r=rects[i]
    if not r['father'] in rects: return None
    p=bounds(r['father'])
    if p is None:return None
    b=r['body']; amin=vec(b,'m_AnchorMin',{'x':.5,'y':.5}); amax=vec(b,'m_AnchorMax',{'x':.5,'y':.5})
    d=vec(b,'m_SizeDelta',{'x':0.,'y':0.}); pivot=vec(b,'m_Pivot',{'x':.5,'y':.5}); pos=vec(b,'m_AnchoredPosition',{'x':0.,'y':0.})
    w=p[2]*(amax['x']-amin['x'])+d['x']; h=p[3]*(amax['y']-amin['y'])+d['y']
    x=p[0]+p[2]*(amin['x']+(amax['x']-amin['x'])*pivot['x'])+pos['x']-w*pivot['x']
    y=p[1]+p[3]*(amin['y']+(amax['y']-amin['y'])*pivot['y'])+pos['y']-h*pivot['y']
    cache[i]=(x,y,w,h); return cache[i]
targets={}
for i,r in rects.items():
    name=gos[r['go']]['name']; pp=path(i)
    if '/Book/' not in pp:continue
    if (name in ('Illustration','MonsterName','IdleFrame','CreatureLeftPage','CreatureRightPage','PreviousPage','NextPage','Page','TitleGroup')
        or name=='Title' and '/CreatureLeftPage/' in pp):
        if bounds(i) and bounds(i)[2]>0 and bounds(i)[3]>0:targets[pp.split('/Book/')[1]]=bounds(i)
# Item title group is sized at runtime by its HorizontalLayoutGroup + ContentSizeFitter.
# childControlWidth=false uses the two authored child widths (70 + 194.09), zero spacing/padding.
# Use the group center and authored 50-unit height, plus the 70-high icon bound.
title_group=next(i for i,r in rects.items() if path(i).endswith('/Book/LeftPage/TitleGroup'))
g=bounds(title_group)
targets['LeftPage/TitleGroup (layout-derived)']=(g[0]-264.09/2,g[1],264.09,g[3])
targets['LeftPage/TitleIcon (layout-derived)']=(g[0]-264.09/2,g[1]-10,70,70)
cover=Image.open(FRAMES/'1.png').convert('RGBA')
ca=np.array(cover)[:,:,3]
book_image=Image.open(ART/'Inventory Book SpriteSheet.png').convert('RGBA')
book_tile=book_image.crop((2448,book_image.height-2160-720,2448+816,book_image.height-2160))
ba=np.array(book_tile)[:,:,3]
SCALE=1480/720
def cover_alpha(xs,ys,candidate,alpha=ca):
    height, dx, dy=candidate['height'],candidate.get('x',0),candidate.get('y',0)
    width=min(candidate.get('width',2320),height*896/720)
    height=width*720/896
    xx=np.floor((xs-dx)/width*896+448).astype(int)
    yy=np.floor(360-(ys-dy)/height*720).astype(int)
    good=(xx>=0)&(xx<896)&(yy>=0)&(yy<720)
    vals=np.zeros(np.broadcast_shapes(xx.shape,yy.shape),dtype=np.uint8)
    xxb,yyb=np.broadcast_arrays(xx,yy); vals[good]=alpha[yyb[good],xxb[good]]
    return vals
def metric(rect,cand,alpha=ca):
    x,y,w,h=rect
    xs=np.arange(x+.25,x+w,.5)[None,:];ys=np.arange(y+.25,y+h,.5)[:,None]
    a=cover_alpha(xs,ys,cand,alpha)
    exposed=(a<250)
    return {'samples':int(a.size),'exposed_samples':int(exposed.sum()),'exposed_ui_area':round(float(exposed.sum())*.25,3),'exposed_percent':round(float(exposed.mean())*100,5),
        'exposed_top_edge_percent':round(float(exposed[-1,:].mean())*100,4),'exposed_bottom_edge_percent':round(float(exposed[0,:].mean())*100,4)}
candidates=[{'label':'baseline','height':1480,'x':0,'y':0},
            {'label':'raise_55','height':1480,'x':0,'y':55},
            {'label':'raise_52','height':1480,'x':0,'y':52},
            {'label':'height_1600_raise_29','height':1600,'x':0,'y':29},
            {'label':'height_1540_raise_40','height':1540,'x':0,'y':40},
            {'label':'height_1750','height':1750,'x':0,'y':0}]
def outside(cand):
    xs=np.arange(-940+.25,940,.5)[None,:];ys=np.arange(-600+.25,600,.5)[:,None]
    ac=cover_alpha(xs,ys,cand)
    sx=np.floor(xs/SCALE+408).astype(int);sy=np.floor(360-ys/SCALE).astype(int)
    sx,sy=np.broadcast_arrays(sx,sy); good=(sx>=0)&(sx<816)&(sy>=0)&(sy<720)
    ab=np.zeros(ac.shape,dtype=np.uint8);ab[good]=ba[sy[good],sx[good]]
    return {'opaque_ui_area_outside_book_alpha':round(float(((ac>=250)&(ab<250)).sum())*.25,3)}
results=[]
for c in candidates:
    results.append(dict(c,targets={k:metric(v,c) for k,v in targets.items()},**outside(c)))
# Search continuous height/y pairs, requiring the full expanded portrait and left footer rectangles.
portrait=next(v for k,v in targets.items() if k.endswith('/Illustration'))
footer={k:v for k,v in targets.items() if k in ('CreatureLeftPage/Page','CreatureLeftPage/PreviousPage','CreatureLeftPage/NextPage')}
search=[]
for height in range(1480,1751,10):
    for y in range(0,61,2):
        c={'height':height,'y':y,'x':0}
        if metric(portrait,c)['exposed_samples']==0 and all(metric(v,c)['exposed_samples']==0 for v in footer.values()):
            search.append(c);break
frame_results=[]
left_ranges=(138,436)
right_ranges=(468,759)
gap_candidate=np.zeros(ca.shape,dtype=bool)
original_top={}
for lo,hi in (left_ranges,right_ranges):
    for xx in range(lo,hi):
        opaque=np.flatnonzero(ca[:,xx]>=250)
        if opaque.size:
            top=int(opaque[0]);original_top[xx]=top
            if top>197:gap_candidate[197:top,xx]=True
fill_counts=[]
for n in range(1,37):
    a=np.array(Image.open(FRAMES/f'{n}.png').convert('RGBA'))[:,:,3]
    # First-frame top deficit distinguishes static silhouette mismatch from intentionally revealed pixels.
    top=(portrait[0],portrait[1]+portrait[3]-60,portrait[2],60)
    frame_results.append({'source_frame':n,'used_by_content_clip':n<=19,'portrait_top_60':metric(top,candidates[0],a)})
    if n<=19:
        fill=np.zeros_like(a,dtype=bool)
        for xx,tt in original_top.items():
            # Frame3's original artwork shifts its edge by one PNG pixel.
            # Admit its nearby boundary, but do not scan below large revealed holes.
            nearby=np.flatnonzero(a[tt:tt+3,xx]>=250)
            if tt>197 and nearby.size:fill[197:tt+int(nearby[0]),xx]=True
        fill_counts.append({'source_frame':n,'suggested_additive_opaque_pixels':int(fill.sum()),
                            'retain_current_top_boundary_transparency':True})
# Aligned book art + source mask and target boundaries. This is an inspection projection, not a Unity render.
for c in (candidates[0],candidates[1],candidates[-1]):
    ui_w,ui_h=1880,1200;res=.5
    xs=np.arange(-940+.25,940,.5)[None,:];ys=np.arange(600-.25,-600,-.5)[:,None]
    sx=np.floor(xs/SCALE+408).astype(int);sy=np.floor(360-ys/SCALE).astype(int);sx,sy=np.broadcast_arrays(sx,sy)
    good=(sx>=0)&(sx<816)&(sy>=0)&(sy<720)
    canvas=np.zeros((*sx.shape,4),dtype=np.uint8);canvas[:]=[40,42,48,255]
    sprite=np.zeros_like(canvas);sprite[good]=np.array(book_tile)[sy[good],sx[good]]
    render=Image.fromarray(canvas).convert('RGBA');render.alpha_composite(Image.fromarray(sprite))
    aa=cover_alpha(xs,ys,c);mask=np.zeros_like(canvas);mask[:,:,0:3]=[110,40,180];mask[:,:,3]=(aa.astype(float)*.50).astype(np.uint8)
    render.alpha_composite(Image.fromarray(mask));draw=ImageDraw.Draw(render)
    for k,(x,y,w,h) in targets.items():
        if k.endswith('/Illustration') or k.endswith('/MonsterName') or k.endswith('/Title') or k=='CreatureLeftPage/Page':
            box=((x+940)*2,(600-y-h)*2,(x+w+940)*2,(600-y)*2)
            draw.rectangle(box,outline=(0,255,240,255),width=2);draw.text((box[0]+4,box[1]+4),k,fill=(0,255,240,255))
    render.resize((1880,1200),Image.Resampling.NEAREST).save(OUT/f"projection-{c['label']}.png")
report={'notes':['Static serialized geometry and PNG nearest-point alpha inspection; no Unity render or import was executed.',
                 'Target rectangles include transparent TMP container space; font glyph-level coverage is not implied.',
                 'Subsequent reveal frames intentionally expose more pixels, so exposed percentages after frame1 are not blanket defects.',
                 'Book outside alpha is visual overdraw on the book silhouette, not UI raycast coverage (PageCover raycastTarget=false).'],
        'book_target_dimensions':[2320,1480],'source_cover_dimensions':list(cover.size),'source_book_tile_dimensions':list(book_tile.size),
        'target_rects_ui':targets,'candidates':results,'full_portrait_and_footer_candidate_search':search,'source_frames':frame_results,
        'additive_flat_top_candidate': {'flat_source_y':197,'page_x_ranges_exclusive_end':[left_ranges,right_ranges],
             'algorithm':'Only fill the first-frame rounded-gap pixels above y=197 in each column, using current frame alpha/color at the original top boundary or within 2 PNG rows below it (frame3 has a pre-existing one-pixel edge shift). Never scan farther down into intentionally revealed holes. This is a proposal, no source pixels changed.',
             'frames':fill_counts}}
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'coverage-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'target_rects':targets,'candidate_portrait':[{k:v for k,v in c.items() if k!='targets'}|{'portrait':next(v for k,v in c['targets'].items() if k.endswith('/Illustration')),'footer':{k:v for k,v in c['targets'].items() if k in footer}} for c in results], 'candidate_search':search},ensure_ascii=False,indent=2))
