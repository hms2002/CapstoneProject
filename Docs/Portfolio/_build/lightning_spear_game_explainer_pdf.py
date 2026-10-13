"""Package the reviewed portfolio illustration as a one-page PDF without cropping.

Facts and source references: Docs/Portfolio/LightningSpearIllustrated.md.
The image is produced with image_gen; this script does not modify its pixels.
"""
from pathlib import Path

from PIL import Image
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'Docs/Portfolio/LightningSpearIllustrated.md'
ART = ROOT / 'Docs/Portfolio/assets/lightning-spear-game-explainer.png'
OUTPUT = ROOT / 'output/pdf/lightning-spear-game-explainer.pdf'


def main():
    title = SOURCE.read_text(encoding='utf-8-sig').splitlines()[0].removeprefix('# ')
    with Image.open(ART) as image:
        pixel_width, pixel_height = image.size
        assert pixel_width > 0 and pixel_height > 0
    page_width = 960
    page_height = page_width * pixel_height / pixel_width
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    pdf = canvas.Canvas(str(OUTPUT), pagesize=(page_width, page_height))
    pdf.setTitle(title)
    pdf.setAuthor('CapstoneProject')
    pdf.setSubject('Illustrated explanation; conceptual reconstruction based on project sprites. See LightningSpearIllustrated.md for measurements and limits.')
    pdf.drawImage(ImageReader(str(ART)), 0, 0, width=page_width, height=page_height, mask='auto')
    pdf.showPage()
    pdf.save()
    print(f'{OUTPUT}: 1 page, source image {pixel_width}x{pixel_height}')


if __name__ == '__main__':
    main()
