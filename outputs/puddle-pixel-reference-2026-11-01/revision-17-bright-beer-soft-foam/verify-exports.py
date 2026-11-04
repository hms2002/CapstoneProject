"""Read-only checks of native Aseprite exports; never draws/edits artwork."""
from collections import Counter
from pathlib import Path
import csv
import hashlib
import json
import struct
from PIL import Image

out = Path(__file__).resolve().parent
root = out.parents[2]
prev = out.parent / 'revision-15-darker-ground-flames'
old_beer = out.parent / 'revision-16-beer-bubbles-center-ignition'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def rgba(path):
    return Image.open(path).convert('RGBA')

def pixels(path):
    return list(rgba(path).get_flattened_data())

def native_header(path):
    data = path.read_bytes()
    magic, frames, width, height, depth = struct.unpack_from('<5H', data, 4)
    assert magic == 0xA5E0
    return {'frames': frames, 'width': width, 'height': height, 'depth': depth}

preserved = []
for path in out.iterdir():
    if path.is_file() and path.name.startswith(('fire-ground.', 'fire-ground-', 'ground-flame-particle.', 'ground-flame-particle-', 'airborne-flame-particle.', 'airborne-flame-particle-')):
        assert sha(path) == sha(prev / path.name), path.name
        preserved.append(path.name)
assert len(preserved) == 29, len(preserved)
game_bubble = root / 'Assets/_Project/Art/Sprites/VFX/Common/AlcholeBubble.aseprite'
assert sha(game_bubble) == sha(out / 'existing-game-bubble.aseprite')
brightness = []
alpha_values = set()
for f in range(1, 5):
    before = pixels(old_beer / f'alcohol-frame-{f}.png')
    after = pixels(out / f'alcohol-frame-{f}.png')
    assert len(before) == len(after) == 4096
    visible = []
    for a, b in zip(before, after):
        assert bool(a[3]) == bool(b[3]), 'Footprint changed'
        alpha_values.add(b[3])
        if a[3]:
            visible.append(sum(b[:3]) / 3 - sum(a[:3]) / 3)
    brightness.append(round(sum(visible) / len(visible), 3))
    for layer in (1, 2):
        a_layer = pixels(out / f'check-before-layer-{layer}-frame-{f}.png')
        b_layer = pixels(out / f'check-after-layer-{layer}-frame-{f}.png')
        for a, b in zip(a_layer, b_layer):
            assert a[3] == b[3]
            if a[3]:
                assert b[0] >= a[0] and b[1] > a[1] and b[2] > a[2]
assert min(brightness) > 15
assert pixels(out / 'alcohol-to-fire-center-frame-1.png') == pixels(out / 'alcohol-frame-1.png')
assert pixels(out / 'alcohol-to-fire-center-frame-16.png') == pixels(out / 'fire-ground-frame-1.png')
coverage = list(csv.DictReader((out / 'ignition-coverage.csv').open()))
counts = [int(r['burned_pixels']) for r in coverage]
assert counts[0] == 0 and counts[-1] == 1625 and counts == sorted(counts)
assert (out / 'ignition-coverage.csv').read_bytes() == (old_beer / 'ignition-coverage.csv').read_bytes()
assert pixels(out / 'alcohol-to-fire-center-frame-2.png')[32 * 64 + 32] == pixels(out / 'fire-ground-frame-1.png')[32 * 64 + 32]

bubbles = list(csv.DictReader((out / 'bubble-motion.csv').open()))
per_frame = Counter(int(r['time_ms']) for r in bubbles)
assert min(per_frame.values()) == 2 and max(per_frame.values()) == 3
assert max(int(r['spawn_y']) + 8 - int(r['y']) for r in bubbles) >= 7
previous_bubbles = list(csv.DictReader((old_beer / 'bubble-motion.csv').open()))
assert len(bubbles) == len(previous_bubbles)
for current, before in zip(bubbles, previous_bubbles):
    assert all(current[k] == v for k, v in before.items())
assert set(int(r['variant']) for r in bubbles) == {1, 2, 3}
foam_sizes = {}
for name in ['bubble-particle', 'bubble-particle-small', 'bubble-particle-soft']:
    for f in range(1, 6):
        im = rgba(out / f'{name}-frame-{f}.png')
        visible = [p for p in im.get_flattened_data() if p[3]]
        assert visible and all(p[3] in {110, 125, 190, 235} for p in visible)
        assert sum(p[3] == 235 for p in visible) <= 2, 'Too much solid white rim'
        if f == 3:
            assert im.getpixel((4, 4))[3] == 0, 'Peak foam center filled'
            x0, y0, x1, y1 = im.getbbox()
            foam_sizes[name] = [x1-x0, y1-y0]
assert len({tuple(v) for v in foam_sizes.values()}) == 3

gifs = {}
for name, size, duration in [
    ('comparison-preview', (608, 320), 3200),
    ('alcohol-with-bubbles-preview', (320, 320), 3200),
    ('center-ignition-preview', (320, 320), 4800),
    ('beer-before-after-preview', (640, 320), 3200),
]:
    with Image.open(out / f'{name}.gif') as im:
        assert im.size == size
        total = 0
        first = im.convert('RGBA')
        png_pixels = pixels(out / f'{name}.png')
        pairs = list(zip(first.get_flattened_data(), png_pixels))
        first_error = max(max(abs(x-y) for x, y in zip(a[:3], b[:3])) for a, b in pairs)
        # GIF has a256-color limit; native RGBA PNG/Aseprite sources remain exact.
        assert first_error <= 2, (name, first_error)
        for f in range(im.n_frames):
            im.seek(f)
            total += im.info.get('duration', 0)
        assert total == duration, (name, total)
        gifs[name] = {'size': size, 'frames': im.n_frames, 'duration_ms': total, 'first_frame_max_RGB_quantization_error': first_error}

result = {
    'preserved_fire_files': preserved,
    'existing_game_bubble_sha256': sha(game_bubble),
    'beer_body_and_ripple_alpha_unchanged': True,
    'beer_footprint_unchanged': True,
    'surface_foam_reauthored_with_softer_alpha': True,
    'flattened_beer_alpha_levels': sorted(alpha_values),
    'beer_average_RGB_brightness_increase_per_frame': brightness,
    'ignition_burned_pixels_per_frame': counts,
    'ignition_endpoints_exact': True,
    'bubble_active_count': [min(per_frame.values()), max(per_frame.values())],
    'foam_peak_sizes': foam_sizes,
    'foam_partial_alpha_and_sparse_glints': True,
    'bubble_trajectories_unchanged': True,
    'center_ignition_mask_and_timing_unchanged': True,
    'native_sources': {name: native_header(out / f'{name}.aseprite') for name in ['alcohol', 'bubble-particle', 'bubble-particle-small', 'bubble-particle-soft', 'alcohol-to-fire-center']},
    'preview_gifs': gifs,
    'unity_import_compile_playmode': 'not executed; sample assets only',
}
(out / 'export-verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
