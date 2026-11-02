from pathlib import Path
from PIL import Image

raw = Image.open(r"C:\Users\user\.codex\generated_images\01a16857-125c-7cf0-ab03-75d5246a1137\exec-f9ee5cd3-5ce9-4db1-9b4c-fdf570a1d242.png").convert("RGBA")
# Built-in generation added a 3px frame on all sides; crop it before sampling the 8px grid.
native = raw.crop((3, 3, 1251, 1251)).resize((156, 156), Image.Resampling.NEAREST)
native.save("tmp/imagegen/sasha-smug-generated-156-preview.png")
native.resize((1248, 1248), Image.Resampling.NEAREST).save("tmp/imagegen/sasha-smug-generated-1248-preview.png")

src = Image.open(r"C:\Users\user\AppData\Local\Temp\codex-clipboard-db4d2e24-96d8-492b-aff9-829d487ce106.png").convert("RGBA")
from collections import Counter
for name, image in (("source", src), ("generated", native)):
    colors = Counter(image.crop((64, 56, 104, 96)).getdata())
    print(name, len(colors))
    print(colors.most_common(24))

face_box = (64, 56, 104, 96)
comparison = Image.new("RGBA", (640, 320), (0, 0, 0, 0))
comparison.paste(src.crop(face_box).resize((320, 320), Image.Resampling.NEAREST), (0, 0))
comparison.paste(native.crop(face_box).resize((320, 320), Image.Resampling.NEAREST), (320, 0))
comparison.save("tmp/imagegen/sasha-face-source-left-generated-right.png")
