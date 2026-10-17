"""Draw the vector gem mark and export Windows icon sizes."""
from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parents[1] / 'Assets'
root.mkdir(exist_ok=True)
svg = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
<rect x="8" y="8" width="240" height="240" rx="54" fill="#161b22"/>
<path d="M76 62H180L218 110L128 210L38 110Z" fill="#58a6ff"/>
<path d="M76 62H128L98 110H38Z" fill="#a8dcff"/>
<path d="M128 62H180L218 110H158Z" fill="#79c0ff"/>
<path d="M98 110H158L128 210Z" fill="#e6edf3"/>
<path d="M38 110H98L128 210Z" fill="#388bfd"/>
<path d="M158 110H218L128 210Z" fill="#2b6cb0"/>
<path d="M128 62L158 110H98Z" fill="#58a6ff"/>
</svg>'''
(root/'app.svg').write_text(svg, encoding='utf-8')
scale=4
image=Image.new('RGBA',(256*scale,256*scale))
draw=ImageDraw.Draw(image)
draw.rounded_rectangle(tuple(v*scale for v in (8,8,248,248)),radius=54*scale,fill='#161b22')
facets=[([(76,62),(180,62),(218,110),(128,210),(38,110)],'#58a6ff'),
 ([(76,62),(128,62),(98,110),(38,110)],'#a8dcff'),
 ([(128,62),(180,62),(218,110),(158,110)],'#79c0ff'),
 ([(98,110),(158,110),(128,210)],'#e6edf3'),
 ([(38,110),(98,110),(128,210)],'#388bfd'),
 ([(158,110),(218,110),(128,210)],'#2b6cb0'),
 ([(128,62),(158,110),(98,110)],'#58a6ff')]
for points,color in facets: draw.polygon([(x*scale,y*scale) for x,y in points],fill=color)
image=image.resize((256,256),Image.Resampling.LANCZOS)
image.save(root/'app.png')
image.save(root/'app.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
print('Exported app.svg, app.png and nine-size app.ico')
