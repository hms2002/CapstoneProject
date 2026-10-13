from pathlib import Path
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

evidence = Path(__file__).resolve().parent
root = evidence.parent.parent
checks = []
def check(name, passed, **details):
    checks.append(dict(name=name, passed=bool(passed), **details))

result = ET.parse(evidence / 'playmode-final-results.xml').getroot()
check('native_tests_26_passed', result.attrib['result'] == 'Passed' and result.attrib['passed'] == '26'
      and result.attrib['failed'] == '0', counts=result.attrib)
preservation = json.loads((evidence / 'integration-preservation-report.json').read_text(encoding='utf-8-sig'))
check('independent_preservation', preservation['passed'], count=len(preservation['checks']))
restoration = json.loads((evidence / 'test-file-restoration-report.json').read_text(encoding='utf-8-sig'))
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()
check('restored_files_still_equal_snapshot', all(sha(entry['path']) == entry['restoredHash']
      for entry in restoration['restoredFiles']), count=len(restoration['restoredFiles']))
states = json.loads((evidence / 'native-reveal-states.json').read_text(encoding='utf-8-sig'))
check('native_phase_visibility', len(states) == 4 and all(not row['itemRootVisible'] for row in states)
      and all(row['coverIndex'] > row['creatureRightIndex'] and row['coverIndex'] < row['fullViewIndex'] for row in states)
      and states[0]['coverVisible'] and not states[-1]['coverVisible'])

scoped = [
    'Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemRightPage.cs',
    'Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaItemTab.cs',
    'Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaScreen.cs',
    'Assets/_Project/Editor/Tools/Encyclopedia/EncyclopediaCreatureUIAuthoring.cs',
    'Assets/_Project/Tests/PlayMode/Procedural/EncyclopediaCreaturePlayModeTests.cs',
    'Docs/ActiveTasks/encyclopedia-tab-visibility-reveal.md',
    'Docs/TaskIndex.md', 'Docs/StructureMemory/EncyclopediaCreatureTabs.md',
    'Docs/ErrorLog.md', 'Docs/SessionLogs/2026-10-12.md',
]
diff = subprocess.run(['git', 'diff', '--check', '--', *scoped], cwd=root, capture_output=True, text=True)
check('scoped_git_whitespace', diff.returncode == 0, output=diff.stdout + diff.stderr)

link_errors = []
documents = [root / scoped[5], root / scoped[6], root / scoped[7], evidence / 'README.md']
session = root / scoped[9]
session_text = session.read_text(encoding='utf-8-sig').split('## 도감 이전 Item 상세 숨김과 Creature Reveal 통합', 1)[1]
error = root / scoped[8]
error_text = error.read_text(encoding='utf-8-sig').split('### 2026-10-12 - Creature tabs must hide', 1)[1].split('\n## ', 1)[0]
link_sources = [(p, p.read_text(encoding='utf-8-sig')) for p in documents] + [(session, session_text), (error, error_text)]
for path, text in link_sources:
    for destination in re.findall(r'\]\((<[^>]+>|[^)]+)\)', text):
        destination = destination.strip('<>').split('#', 1)[0]
        if not destination or re.match(r'\w+://', destination):
            continue
        if not (path.parent / destination).resolve().exists():
            link_errors.append(dict(file=str(path), destination=destination))
check('updated_markdown_links', not link_errors, errors=link_errors)

redactions = {}
for path in evidence.glob('*.log'):
    text = path.read_text(encoding='utf-8-sig', errors='replace')
    cleaned, count = re.subn(r'(-accessToken(?:\s+|=))[^\s\r\n]+', r'\1[REDACTED]', text)
    if count:
        path.write_text(cleaned, encoding='utf-8')
        redactions[path.name] = count
check('logs_access_tokens_redacted', all(not re.search(r'-accessToken(?:\s+|=)(?!\[REDACTED\])[^\s\r\n]+',
      p.read_text(encoding='utf-8-sig', errors='replace')) for p in evidence.glob('*.log')), redactions=redactions)

report = dict(passed=all(row['passed'] for row in checks), checks=checks)
(evidence / 'final-evidence-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(dict(passed=report['passed'], checks=len(checks), failed=[r for r in checks if not r['passed']]), ensure_ascii=False))
raise SystemExit(0 if report['passed'] else 1)
