"""Read-only extraction of Unity sprite references for an Aseprite approval mockup."""
from pathlib import Path
import hashlib, json, re, struct, subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
catalog = (ROOT / 'Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset').read_text(encoding='utf-8-sig')
refs = []
monster_section = catalog.split('  monsterEntries:\n')[1].split('  bossEntries:\n')[0]
for i, block in enumerate(re.split(r'(?=^  - id:)', monster_section, flags=re.M)):
    if not block.strip(): continue
    ident = re.search(r'^  - id: (.+)$', block, re.M).group(1)
    match = re.search(r'    previewFrames:\n    - \{fileID: (-?\d+), guid: (\w+)', block)
    if not match:
        match = re.search(r'^    image: \{fileID: (-?\d+), guid: (\w+)', block, re.M)
    refs.append((ident, *match.groups()))
boss_section = catalog.split('  bossEntries:\n')[1].split('  npcEntries:\n')[0]
for block in re.split(r'(?=^  - id:)', boss_section, flags=re.M):
    if not block.strip(): continue
    ident = re.search(r'^  - id: (.+)$', block, re.M).group(1)
    match = re.search(r'^      sprite: \{fileID: (-?\d+), guid: (\w+)', block, re.M)
    refs.append((ident, *match.groups()))
refs += [('heart.empty','-4538568396499552771','369e5a5850cde054c8a4361c28ba81b9'),
         ('heart.full','3919147750717110911','e75f469ea29530246a4a0543396316af')]
guids = sorted({r[2] for r in refs})
(OUT/'source-guids.txt').write_text('\n'.join(guids), encoding='utf-8')
result = subprocess.run(['rg','-l','-F','-f',str(OUT/'source-guids.txt'),'--glob','*.meta','Assets'], cwd=ROOT, capture_output=True, encoding='utf-8', check=True)
metas = {}
for rel in result.stdout.splitlines():
    path = ROOT / rel
    text = path.read_text(encoding='utf-8-sig')
    guid = re.search(r'^guid: (\w+)',text,re.M).group(1)
    if guid in guids: metas[guid]=(path,text)
sources={}
for ident, fileid, guid in refs:
    meta, text = metas[guid]
    path = meta.with_suffix('')
    assert path.suffix.lower()=='.png', path
    data = path.read_bytes()
    width,height = struct.unpack('>II', data[16:24])
    rect=[0,0,width,height]
    if fileid!='21300000':
        sprites=text.split('    sprites:\n')[1].split('\n    outline:')[0] if '    sprites:\n' in text else ''
        blocks=re.split(r'(?=^    - serializedVersion:)',sprites,flags=re.M)
        selected=next((b for b in blocks if re.search(r'^      internalID: '+re.escape(fileid)+r'\s*$',b,re.M)),None)
        assert selected, (ident,fileid,path)
        coords=[int(float(re.search(r'^        '+k+r': ([\d.]+)',selected,re.M).group(1))) for k in ['x','y','width','height']]
        rect=[coords[0],height-coords[1]-coords[3],coords[2],coords[3]]
    sources[ident]={'path':path.as_posix(),'rect':rect,'guid':guid,'fileID':fileid,'sha256':hashlib.sha256(data).hexdigest()}
(OUT/'sources.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
lua=['return {']
for ident, entry in sources.items():
    lua.append('  ['+json.dumps(ident)+']={path='+json.dumps(entry['path'],ensure_ascii=False)+',rect={'+','.join(map(str,entry['rect']))+'}},')
lua.append('}')
(OUT/'sources.lua').write_text('\n'.join(lua),encoding='utf-8')
protected=['Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab','Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset','Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCreatureTab.cs','Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaCreatureDetailView.cs']
(OUT/'before-hashes.json').write_text(json.dumps({p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in protected},indent=2),encoding='utf-8')
print('Resolved',len(sources),'original game sprites; no project assets changed.')
