"""Render vector originals to PNG and multi-resolution Windows ICO."""
from pathlib import Path
from io import BytesIO
import resvg_py
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
assets = root / 'assets'
for name in ('icon-a-loop', 'icon-b-robot'):
    svg = (assets / f'{name}.svg').read_text(encoding='utf-8')
    png = resvg_py.svg_to_bytes(svg_string=svg)
    (assets / f'{name}.png').write_bytes(png)
    im = Image.open(BytesIO(png)).convert('RGBA')
    im.save(assets / f'{name}.ico', sizes=[(s,s) for s in (16,20,24,32,40,48,64,128,256)])

canvas = Image.new('RGB', (1200, 670), '#0b0c10')
d = ImageDraw.Draw(canvas)
font_dir = Path('C:/Windows/Fonts')
title = ImageFont.truetype(str(font_dir/'consolab.ttf'), 32)
body = ImageFont.truetype(str(font_dir/'consola.ttf'), 21)
d.text((48, 30), 'ROBOT LOOP / ICON STUDY', font=title, fill='#f2f3f6')
for x, name, caption in ((48,'icon-a-loop','A / LOOP CURSOR'),(648,'icon-b-robot','B / ROBOT MASK')):
    icon = Image.open(assets/f'{name}.png').convert('RGBA')
    large = icon.resize((350,350),Image.Resampling.LANCZOS)
    canvas.paste(large,(x+78,100),large)
    d.text((x,480),caption,font=title,fill='#f43145')
    for offset,size in enumerate((16,24,32,48,64)):
        small=icon.resize((size,size),Image.Resampling.LANCZOS)
        canvas.paste(small,(x+offset*94,550+(64-size)//2),small)
    d.text((x,630),'16     24     32     48     64 px',font=body,fill='#9498a5')
canvas.save(assets/'icon-options.png')
