"""Compare active Ink against Git baseline after removing only localization tags."""
import json, pathlib, re, subprocess
rows=json.loads(pathlib.Path('DataSheets/Localization/InkInventory.json').read_text(encoding='utf-8'))['rows']
paths=sorted({row['path'] for row in rows})
def normalize(text):
    return re.sub(r' # loc:[A-Za-z0-9_.-]+', '', text).replace('\r\n','\n').lstrip('\ufeff')
for path in paths:
    original=subprocess.run(['git','show','HEAD:'+path],check=True,capture_output=True).stdout.decode('utf-8-sig')
    current=pathlib.Path(path).read_text(encoding='utf-8-sig')
    assert normalize(original)==normalize(current), 'Non-localization Ink change: '+path
print(f'PASS: {len(paths)} active Ink files preserve original text, branches and side-effect tags after removing loc annotations.')
