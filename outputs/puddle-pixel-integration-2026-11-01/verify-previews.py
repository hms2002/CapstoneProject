from pathlib import Path
from PIL import Image, ImageChops
import hashlib
import json

out = Path(__file__).resolve().parent
result = {}
for name, prefix, count in [('native-comparison', 'comparison-', 80), ('native-center-ignition', 'ignition-', 21)]:
    gif = Image.open(out / (name + '.gif'))
    assert gif.n_frames == count and gif.size == (640, 320)
    max_error = 0
    duration = 0
    for i in range(count):
        gif.seek(i)
        duration += gif.info.get('duration', 0)
        png = Image.open(out / 'native' / (prefix + f'{i:03d}.png')).convert('RGB')
        diff = ImageChops.difference(gif.convert('RGB'), png)
        max_error = max(max_error, max(v[1] for v in diff.getextrema()))
    result[name] = {'frames': count, 'duration_ms': duration, 'max_channel_error': max_error}
    assert max_error <= 1, result[name]

sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
save_dir = Path.home() / 'AppData/LocalLow/DefaultCompany/Capstone_Project'
result['saves_unchanged'] = {p.name: sha(p) == sha(save_dir / p.name) for p in (out / 'save-backup').glob('*.json')}
assert len(result['saves_unchanged']) == 3 and all(result['saves_unchanged'].values())
(out / 'preview-verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
