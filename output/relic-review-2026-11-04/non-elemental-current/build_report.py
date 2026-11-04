from pathlib import Path
import csv, hashlib, io, json, math, re
from PIL import Image

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
def readjson(p): return json.loads((ROOT / p).read_text(encoding='utf-8-sig'))
def dump(p, obj): (OUT / p).write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')
def plain(text):
    for _ in range(4): text = re.sub(r'\{(?:em|pos|neg|val|key):([^{}]*)\}', r'\1', text)
    return text.replace('[[', '').replace(']]', '')

old = readjson('output/relic-review-2026-11-04/unremade/data.json')
known = {r['guid']: r['source'] for r in old}
applications = []
for folder, key in [('six-approved-icons','items'), ('six-approved-icons-followup','records'), ('six-approved-icons-mandragora','records')]:
    applications += readjson('output/relic-review-2026-11-04/' + folder + '/verification.json')[key]
for r in applications:
    guid = re.search(r'^guid: (\w+)', (ROOT / (r['png']+'.meta')).read_text(), re.M)[1]
    known[guid] = r['png']
updatedstems = [Path(r['definition']).stem for r in applications]
assert len(set(updatedstems)) == 18
excluded = set()
for category in ['electric','fire']:
    excluded.update(Path(r['definition']).stem for r in readjson('output/relic-review-2026-11-03/current-relics/'+category+'-data.json') if r['updated'])
assert len(excluded) == 20
maps = {}
for folder in ['Assets/_Project/Data/Items/Relics','Assets/_Project/Unused/Items/Relics']:
    for meta in (ROOT / folder).rglob('*.asset.meta'):
        guid = re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
        assert guid not in maps
        maps[guid] = Path(str(meta)[:-5])
db = ROOT / 'Assets/_Project/Data/Items/ItemDatabase.asset'
block = re.search(r'(?ms)^  allRelics:\n(.*?)(?=^  \w)', db.read_text(encoding='utf-8'))[1]
registered = re.findall(r'guid: (\w+)', block)
assert len(registered) == len(set(registered)) == 75
cp = ROOT / 'DataSheets/Localization/GameText.csv'
csvrows = {r[0]:r for r in csv.reader(cp.open(encoding='utf-8-sig', newline='')) if r}
loc = ROOT / 'Assets/_Project/Data/Localization'
ko = (loc/'GameText_ko.asset').read_text(encoding='utf-8')
shared = (loc/'GameText Shared Data.asset').read_text(encoding='utf-8')
ids = {m[2]:m[1] for m in re.finditer(r'm_Id: (\d+)\n    m_Key: ([^\n]+)', shared)}
def localized(key):
    value = csvrows[key][1]
    m = re.search(r'(?ms)^  - m_Id: '+ids[key]+r'\n    m_Localized: (.*?)(?=\n    m_Metadata:)', ko)
    assert m, key
    scalar = re.sub(r'\\\n[ \t]*', '', m[1])
    scalar = re.sub(r'\n[ \t]*', ' ', scalar)
    scalar = re.sub(r'\\x([0-9a-fA-F]{2})', r'\\u00\1', scalar)
    actual = json.loads(scalar) if scalar.startswith('"') else scalar
    assert actual.replace('\r\n','\n') == value.replace('\r\n','\n'), (key, value, actual)
    return value

protected = {db, cp, loc/'GameText_ko.asset', loc/'GameText Shared Data.asset'}
data = []
for guid in registered:
    p = maps[guid]
    if p.stem in excluded: continue
    t = p.read_text(encoding='utf-8')
    icon = re.search(r'(?m)^  icon: \{fileID: (-?\d+), guid: (\w+), type: 3\}', t)
    assert icon, p
    fid, sg = icon.groups()
    assert sg in known, (p, sg)
    src = ROOT / known[sg]
    meta = Path(str(src)+'.meta')
    mt = meta.read_text()
    assert re.search(r'^guid: (\w+)', mt, re.M)[1] == sg
    w, h = Image.open(src).size
    mode = int(re.search(r'(?m)^  spriteMode: (\d+)', mt)[1])
    sprite = src.stem
    if mode == 1:
        assert fid == '21300000'
        rect = [0,0,w,h]
    else:
        pairs = {m[1]:m[2] for m in re.finditer(r'213: (-?\d+)\n    second: ([^\n]+)', mt)}
        sprite = pairs.get(fid)
        if sprite is None:
            for b in re.split(r'(?m)^    - serializedVersion:', mt):
                nm = re.search(r'(?m)^      name: ([^\n]+)', b)
                ident = re.search(r'(?m)^      internalID: (-?\d+)', b)
                if nm and ident and ident[1] == fid:
                    sprite = nm[1]; break
        assert sprite, (p, fid)
        match = re.search(r'(?ms)^      name: '+re.escape(sprite)+r'\n(.*?)(?=^    - serializedVersion:|^    outline:|\Z)', mt)
        assert match, (meta, sprite)
        b = match[1]
        x,y,sw,sh = [int(re.search(r'(?m)^        '+key+r': (\d+)', b)[1]) for key in ['x','y','width','height']]
        rect = [x,h-y-sh,sw,sh]
        assert 0 <= rect[1] and x+sw <= w
    name = localized('asset.'+guid+'.displayName')
    description = localized('asset.'+guid+'.description')
    effect = plain(description).replace('\r','').replace('\n',' · ').strip()
    if len(effect) > 102: effect = effect[:100]+'…'
    data.append(dict(name=name, definition=p.relative_to(ROOT).as_posix(), definition_guid=guid, source=src.relative_to(ROOT).as_posix(), fileID=fid, guid=sg, sprite=sprite, rect=rect, description=description, effect=effect, nativeFile='icons/'+p.stem+'.png', updated=p.stem in updatedstems))
    protected.update([p, Path(str(p)+'.meta'), src, meta])
assert len(data) == 55
updated = sorted([r for r in data if r['updated']], key=lambda r:updatedstems.index(Path(r['definition']).stem))
remaining = [r for r in data if not r['updated']]
assert len(updated) == 18 and len(remaining) == 37
ordered = updated+remaining
for i,r in enumerate(ordered,1): r['number'] = i
pages = []
for category,records,chunk in [('updated',updated,9), ('remaining',remaining,12)]:
    count = math.ceil(len(records)/chunk)
    for i in range(count):
        subset = records[i*chunk:(i+1)*chunk]
        title = '교체한 유물' if category == 'updated' else '기존 이미지 유물'
        pages.append(dict(stem=category+'-'+str(i+1).zfill(2), title='마왕성 · '+title+' '+str(i+1)+'/'+str(count), subtitle='화염·전기 각 10종 제외 · 대상 55종 중 교체 18종 / 기존 37종 · 2026-11-04', hint='현재 이름 · 이미지 · 효과 설명    |    원본 픽셀을 정수 배율로 확대', rows=math.ceil(len(subset)/3), items=subset))
dump('data.json', ordered)
dump('pages.json', pages)
dump('protected-before.json', {p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in protected})

base = (ROOT/'output/relic-review-2026-11-04/unremade/render-01.ps1').read_text(encoding='utf-8-sig')
base = base.replace("$data=Get-Content -LiteralPath (Join-Path $outDir 'page-01-data.json') -Raw | ConvertFrom-Json\n$output=Join-Path $outDir 'unremade-01.png'\n$bmp=[Drawing.Bitmap]::new(1260,1390)", "$pages=Get-Content -LiteralPath (Join-Path $outDir 'pages.json') -Raw -Encoding UTF8 | ConvertFrom-Json\nforeach($page in $pages) {\n$data=$page.items\n$output=Join-Path $outDir ($page.stem+'.png')\n$height=164+([int]$page.rows)*294+50\n$bmp=[Drawing.Bitmap]::new(1260,$height)")
base = base.replace("'마왕성 · 다음 리메이크 검토 1/5'", '[string]$page.title').replace("'현재 등록 유물 75종 · 이번 대화 교체 20종 제외 · 남은 55종 · 2026-11-04'", '[string]$page.subtitle').replace("'현재 이름·이미지·효과 요약    |    기존 전용 PNG도 포함 · 원본 정수 배율 확대'", '[string]$page.hint')
base = base.replace("$native.Save((Join-Path $outDir $r.nativeFile),[Drawing.Imaging.ImageFormat]::Png)", "if($sw -eq $src.Width -and $sh -eq $src.Height -and [int]$r.rect[0] -eq 0 -and [int]$r.rect[1] -eq 0) {\n [IO.File]::WriteAllBytes((Join-Path $outDir $r.nativeFile),[IO.File]::ReadAllBytes((Join-Path $root $r.source)))\n } else { $native.Save((Join-Path $outDir $r.nativeFile),[Drawing.Imaging.ImageFormat]::Png) }")
base = base.replace("'보기용 자료 · 이 대화의 교체 완료 20종 기준 · 프로젝트 에셋과 문서 변경 없음'", "'보기용 자료 · 긴 효과는 요약 · 전체 설명은 README 참조 · 최신 설명 기준'").replace(',38,1350)', ',38,($height-36))')
base = base.replace("$effectRect=[Drawing.RectangleF]::new(($x+16),($y+204),358,65)", "$effectRect=[Drawing.RectangleF]::new(($x+16),($y+204),358,65)\n $effectFormat=[Drawing.StringFormat]::new()\n $effectFormat.Trimming=[Drawing.StringTrimming]::EllipsisCharacter\n $effectFormat.FormatFlags=[Drawing.StringFormatFlags]::LineLimit")
base = base.replace("$g.DrawString([string]$r.effect,$bodyFont,$light,$effectRect)", "$g.DrawString([string]$r.effect,$bodyFont,$light,$effectRect,$effectFormat)\n $effectFormat.Dispose()")
(OUT/'render.ps1').write_text(base+'\n}\n', encoding='utf-8-sig')
md = '# 화염·전기 외 유물 최신 자료 — 2026-11-04\n\n현재 ItemDatabase 등록 75종에서 기존 화염 10종·전기 10종을 제외한 55종입니다. 이 대화에서 최근 교체한 18종을 먼저 모으고, 아직 교체하지 않은 37종도 별도로 담았습니다. 교체 완료 여부는 전용 PNG 존재가 아니라 이 대화의 승인 적용 이력을 기준으로 합니다.\n\n현재 정의 GUID/icon fileID·PNG 메타데이터를 대조하고 현재 번역 CSV와 한국어 StringTable에서 이름·설명을 확인했습니다. 카드의 긴 설명은 요약하며 아래에는 전체 설명을 기록했습니다. 효과는 현재 프로젝트 설명 기준이며 레벨별 실측 결과가 아닙니다.\n\n만드라고라는 이동속도 증가 없이 치명타 확률만 레벨별 2/4/6/8/10% 증가하는 최신 변경을 반영했습니다. 교체하지 않은 날개 부츠는 일반 대시 유물로 포함했습니다. 뇌창 전용 유물 2종은 기존 일반 전기 10종 목록과 별도이므로 기존 이미지 목록에 포함했습니다.\n\n'
for page in pages:
    md += '## '+page['title'].replace('마왕성 · ','')+'\n\n!['+page['title']+']('+page['stem']+'.png)\n\n| 번호 / 현재 이름 | PNG | 현재 효과 설명 |\n| --- | --- | --- |\n'
    for r in page['items']:
        desc = plain(r['description']).replace('|','&#124;').replace('\n','<br>')
        md += f"| {r['number']:02} [{r['name']}](../../../{r['definition']}) | [PNG]({r['nativeFile']}) | {desc} |\n"
    md += '\n'
md += '## 생성 범위와 확인\n\n이 폴더에 카드 PNG 6장, 개별 이미지 사본/추출본 55장, 현재 데이터와 페이지 JSON, 렌더링 PowerShell, 자료 생성 Python, 검증 기록 및 이 README를 생성했습니다. 프로젝트의 그림과 효과는 추가 변경하지 않았습니다.\n\n자료 대조 과정에서 직전 적용한 만드라고라 한국어·영어 설명에 남아 있던 YAML 줄바꿈 뒷부분을 발견해 두 StringTable 항목의 전체 값을 정정했습니다. 다른 3개 언어도 전체 값 일치를 확인했습니다. 키/ID·다른 항목은 보존했습니다. [정정 기록](description-correction/verification.json).\n\n[검증 기록](verification.json) · [현재 전체 데이터](data.json) · [SessionLog](../../../Docs/SessionLogs/2026-11-04.md)\n\nUnity import·컴파일·Play Mode·실제 UI 및 자동화 테스트는 실행하지 않았습니다. Doc Impact Check: SessionLog. ErrorLog/DecisionLog/Presentation HTML 변경 및 새로운 HTML stale candidate 없음.\n'
(OUT/'README.md').write_text(md, encoding='utf-8')
print('Registered 75; excluded 20; current 55; updated 18; existing 37; pages',len(pages),'; protected files',len(protected))
