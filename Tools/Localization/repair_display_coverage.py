"""Add explicit omitted variant keys and captions for dynamically generated stat links."""
import csv, json
from pathlib import Path
folder=Path('DataSheets/Localization')
languages=('ko','en','ja','zh-Hans','zh-Hant')
with (folder/'GameText.csv').open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
by_key={r['key']:r for r in rows}
def add(key, values):
    if key not in by_key:
        row=dict(zip(('key',)+languages,(key,)+tuple(values)));rows.append(row);by_key[key]=row
add('weapon.lightning_spear.sweep.title',('뇌창 휩쓸기','Thunder Spear Sweep','雷槍の薙ぎ払い','雷枪横扫','雷槍橫掃'))
add('weapon.lightning_spear.rush.title',('뇌창 돌격','Thunder Spear Rush','雷槍の突進','雷枪突击','雷槍突擊'))
add('weapon.lightning_spear.sweep.detail',(
    '● {em:낙뢰 표식이 없을 때} 제자리에서 전방을 휩쓸기\n● 보유한 {em:회수 창}을 조준 방향 앞쪽에서 부채꼴로 순차 발사',
    '● {em:Without a Lightning Mark}, sweep in front without moving\n● Fire stored {em:Recovered Spears} one by one in a fan toward the aim direction',
    '● {em:落雷の印がない時}、その場で前方を薙ぎ払う\n● 所持する{em:回収した槍}を照準方向へ扇状に順次発射',
    '● {em:没有落雷标记时}，原地横扫前方\n● 将持有的{em:回收长枪}朝瞄准方向依次呈扇形发射',
    '● {em:沒有落雷標記時}，原地橫掃前方\n● 將持有的{em:回收長槍}朝瞄準方向依次呈扇形發射'))
add('weapon.lightning_spear.rush.detail',(
    '● 커서 주변의 [[낙뢰 표식]]으로 즉시 돌진\n● 이동 경로와 도착 지점을 공격\n● 표식 소모 시 {pos:뇌창 돌격 재사용 대기시간 초기화}\n● {em:회수 창} {val:1개} 획득',
    '● Rush instantly to a [[낙뢰 표식]] near the cursor\n● Attack along the path and at the destination\n● Consuming the mark {pos:resets Thunder Spear Rush cooldown}\n● Gain {val:1} {em:Recovered Spear}',
    '● カーソル付近の[[낙뢰 표식]]へ即座に突進\n● 移動経路と到着地点を攻撃\n● 印を消費すると{pos:雷槍の突進のクールダウンをリセット}\n● {em:回収した槍}を{val:1本}獲得',
    '● 立即冲向光标附近的[[낙뢰 표식]]\n● 攻击路径及终点\n● 消耗标记时{pos:重置雷枪突击冷却时间}\n● 获得{val:1把}{em:回收长枪}',
    '● 立即衝向游標附近的[[낙뢰 표식]]\n● 攻擊路徑及終點\n● 消耗標記時{pos:重置雷槍突擊冷卻時間}\n● 獲得{val:1把}{em:回收長槍}'))
inventory=json.loads((folder/'AssetInventory.json').read_text(encoding='utf-8'))['rows']
count=0
for asset in inventory:
    field=asset['field'].split('.')[-1]
    if field not in ('attributeName','displayNameOverride'):continue
    translation=by_key[asset['key']]
    key='term.'+asset['text']
    if key not in by_key:
        add(key,[translation[l] for l in languages]);count+=1
with (folder/'GameText.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=('key',)+languages);writer.writeheader();writer.writerows(rows)
print(f'{len(rows)} keys; {count} dynamic stat caption keys added.')
