"""Build reviewed static UI bindings; dynamic labels retain their controller owner."""
import csv
import json
from pathlib import Path

folder = Path('DataSheets/Localization')
languages = ('ko', 'en', 'ja', 'zh-Hans', 'zh-Hant')
with (folder / 'CommonTranslations.tsv').open(encoding='utf-8-sig', newline='') as f:
    translations = {r['ko'].replace('\\n', '\n'): {k: r[k].replace('\\n', '\n') for k in languages}
                    for r in csv.DictReader(f, delimiter='\t')}
with (folder / 'GameText.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
keys = {r['key'] for r in rows}
inventory = json.loads((folder / 'Inventory.json').read_text(encoding='utf-8'))['rows']
# Exact reviewed captions; sentence matching is only an authoring migration step.
captions = set('일시정지|계속하기|설정|메인 화면|게임종료|화면 모드|해상도|마스터 볼륨|효과음 볼륨|BGM 볼륨|화면 흔들림|UI크기|언어|키 매핑 설정|나가기|적용하기|설정 초기화|키 설정|예|아니오|획득|닫기|확정|메인 퀘스트|서브 퀘스트|마왕을 토벌하기 위해 전진하자.|레벨업 가능|무기 교체|게임 시작|게임 종료|플레이 타임 :|업그레이드 진행도|마정석 개수|클리어 횟수|삭제하기|취소|삭제|귀환|인벤토리 확인|시작 지점|← 대쉬 통로|공격 · 스킬 · 상자|우클릭 충전 · 대상 4개|Q 스킬 · 대상 3개|상자 / 무기 교체 배치|↑ 보스전|보스 전투 구역'.split('|'))
bindings, seen = [], set()
for r in inventory:
    if r['field'] != 'm_text' or r['text'].strip() not in captions:
        continue
    text = r['text'].strip()
    if text not in translations:
        raise ValueError('Missing reviewed static caption: ' + text)
    if r['key'] not in keys:
        rows.append(dict(key=r['key'], **translations[text]))
        keys.add(r['key'])
    identity = (r['path'], r['owner'])
    if identity not in seen:
        bindings.append(r)
        seen.add(identity)
with (folder / 'GameText.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=('key',) + languages)
    writer.writeheader()
    writer.writerows(rows)
(folder / 'StaticUiBindings.json').write_text(json.dumps({'rows': bindings}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(bindings)} reviewed static UI bindings.')
