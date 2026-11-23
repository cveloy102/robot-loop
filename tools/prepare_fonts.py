"""Create named static fonts for Windows GDI from the shared variable sources."""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root = Path(__file__).resolve().parents[1]
for filename, weight, family in (
    ('inter_display_black', 900, 'RobotLoop Display'),
    ('inter_medium', 500, 'RobotLoop Sans'),
    ('jetbrains_mono_medium', 500, 'RobotLoop Mono'),
):
    font = TTFont(root / 'assets/fonts/source' / (filename + '.ttf'))
    if 'fvar' in font:
        axes = {axis.axisTag: (weight if axis.axisTag == 'wght' else axis.defaultValue) for axis in font['fvar'].axes}
        font = instantiateVariableFont(font, axes, inplace=True)
    for record in font['name'].names:
        values = {1: family, 2: 'Regular', 4: family, 6: family.replace(' ', '-'), 16: family, 17: 'Regular'}
        if record.nameID in values:
            record.string = values[record.nameID].encode(record.getEncoding())
    # Each bundled weight is its own family. GDI must see it as the regular
    # face of that family, rather than synthesize a different family name.
    font['OS/2'].usWeightClass = 400
    font['OS/2'].fsSelection = (font['OS/2'].fsSelection & ~0x21) | 0x40
    font['head'].macStyle &= ~3
    if 'STAT' in font:
        del font['STAT']
    for record in font['name'].names:
        if record.nameID in (3, 25):
            record.string = family.replace(' ', '-').encode(record.getEncoding())
    font.save(root / 'assets/fonts' / (filename + '.ttf'))
