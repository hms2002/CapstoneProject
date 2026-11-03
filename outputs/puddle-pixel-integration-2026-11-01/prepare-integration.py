from pathlib import Path
import hashlib
import json
import shutil

out = Path(__file__).resolve().parent
root = out.parents[1]
baseline = out / 'baseline'
baseline.mkdir(exist_ok=True)
tracked = [
    'Assets/_Project/Art/Shaders/PuddleShaderVisual.shader',
    'Assets/_Project/Runtime/Features/Map/Puddles/PuddlePresentationContracts.cs',
    'Assets/_Project/Runtime/Features/Map/Puddles/AlcoholPuddleArea.cs',
    'Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleParticleVisual.cs',
    'Assets/_Project/Prefabs/Map/Puddles/AlcoholPuddle.prefab',
    'Assets/_Project/Prefabs/Map/Puddles/FirePuddle.prefab',
]
protected = [
    'Assets/_Project/Runtime/Features/Map/Puddles/PuddleAreaBase.cs',
    'Assets/_Project/Runtime/Features/Map/Puddles/PuddleConversionService.cs',
    'Assets/_Project/Runtime/Features/Map/Puddles/FirePuddleArea.cs',
    'Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleShaderVisual.cs',
    'Assets/_Project/Runtime/Presentation/Map/Puddles/PuddleBlobVisual.cs',
    'Assets/_Project/Art/Materials/M_PuddleShaderVisual.mat',
    'Assets/_Project/Prefabs/Map/Puddles/AlcoholPuddle.prefab.meta',
    'Assets/_Project/Prefabs/Map/Puddles/FirePuddle.prefab.meta',
    'Packages/manifest.json', 'Packages/packages-lock.json',
]
protected += [str(p.relative_to(root)).replace('\\', '/') for p in (root / 'ProjectSettings').glob('*.asset')]
if not (out / 'baseline-hashes.json').exists():
    hashes = {}
    for relative in tracked + protected:
        path = root / relative
        hashes[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
        if relative in tracked:
            shutil.copy2(path, baseline / path.name)
    (out / 'baseline-hashes.json').write_text(json.dumps({'hashes': hashes, 'allowed_changed': tracked}, indent=2), encoding='utf-8')
art = root / 'Assets/_Project/Art/Textures/Puddles/ApprovedPixel'
art.mkdir(parents=True, exist_ok=True)
for source, name in {
    'alcohol-sheet.png': 'AlcoholGround.png',
    'fire-ground-sheet.png': 'FireGround.png',
    'center-ignition-sheet.png': 'CenterIgnition.png',
    'alcohol-1.png': 'EmissionShape.png',
    'bubble-variants-sheet.png': 'BubbleVariants.png',
    'ground-flame-particle-sheet.png': 'NearFlames.png',
    'airborne-flame-particle-sheet.png': 'AirborneFlames.png',
}.items():
    shutil.copy2(out / 'source' / source, art / name)
print('Preserved baseline and copied seven native Aseprite exports.')
