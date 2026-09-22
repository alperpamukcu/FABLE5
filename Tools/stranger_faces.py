# -*- coding: utf-8 -*-
"""The strangers' photographs: faces the bar never draws, for the borrowed licence (2026-09-22).

Run:  py -3 Tools/stranger_faces.py            writes Assets/Resources/Strangers/<slug>.png
      py -3 Tools/stranger_faces.py --sheet P  also writes a contact sheet to P

WHY (the author's eighth list: "Kimlikteki fotoğrafta mevcut karakterin görseli yerine başka oyunumuzda dahil
olmayan adamların görselleri"). A borrowed card used to wear another drinker's face - somebody the player may have
served an hour ago, so the tell was a memory test. It wears a stranger's now: a man the bar has never drawn.

WHERE THEY COME FROM. Every one of these was a drinker once and was cut from the cast (2026-08-25, 2026-09-07,
2026-09-15) - for a clip that would not animate, or for being too old for the room - and their frames went with
them. The FACES are still good photographs, drawn in the cast's own hand, and git kept them: each is read from the
parent of the commit that deleted it, through LFS, never redrawn and never regenerated.

WHOLE PIXELS ONLY. The cast's faces are 64x64 and the card draws them at exactly 2x; these were cut at 54..68. A
smaller one is set on a 64 canvas (centred, shoulders on the bottom edge) and a larger one loses its outermost
columns and its lowest rows - a face is never resampled, because a photo at 1.18x is a photo with some rows drawn
twice (the same rule patron_faces.py keeps).
"""
import io
import os
import subprocess
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Strangers')
S = 64

# slug -> the commit that deleted its face (the face is read from that commit's parent)
SOURCES = {
    'busdriver': '73fb3d58',
    'fisherman': '73fb3d58',
    'guayabera': '73fb3d58',
    'linecook': '73fb3d58',
    'mechanic': '73fb3d58',
    'retiree': '73fb3d58',
    'spanishsuit': 'fad254bd',
    'racerboy': '9a5b4515',
    'rider': '9a5b4515',
    'roma': '9a5b4515',
    'shanghai': '9a5b4515',
    'tokyodj': '9a5b4515',
}


def git_face(slug, commit):
    path = 'Assets/Resources/Patron/%s/face.png' % slug
    pointer = subprocess.run(['git', 'show', '%s^:%s' % (commit, path)], cwd=ROOT, capture_output=True, check=True).stdout
    data = subprocess.run(['git', 'lfs', 'smudge'], cwd=ROOT, input=pointer, capture_output=True, check=True).stdout
    return Image.open(io.BytesIO(data)).convert('RGBA')


def to_64(im):
    w, h = im.size
    if w > S:                                   # lose the outer columns, evenly
        left = (w - S) // 2
        im = im.crop((left, 0, left + S, h))
        w = S
    if h > S:                                   # lose the lowest rows: the head keeps its top
        im = im.crop((0, 0, w, S))
        h = S
    canvas = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((S - w) // 2, S - h))   # centred, shoulders on the bottom edge
    return canvas


def main():
    os.makedirs(OUT, exist_ok=True)
    faces = {}
    for slug, commit in SOURCES.items():
        face = to_64(git_face(slug, commit))
        dst = os.path.join(OUT, slug + '.png')
        buf = io.BytesIO()
        face.save(buf, 'PNG')
        if not os.path.exists(dst) or open(dst, 'rb').read() != buf.getvalue():
            open(dst, 'wb').write(buf.getvalue())
            print('wrote', os.path.relpath(dst, ROOT))
        faces[slug] = face
    if '--sheet' in sys.argv:
        dst = sys.argv[sys.argv.index('--sheet') + 1]
        cols = 6
        sheet = Image.new('RGBA', (cols * (S * 2 + 8), ((len(faces) + cols - 1) // cols) * (S * 2 + 8)), (214, 202, 178, 255))
        for i, (slug, face) in enumerate(faces.items()):
            sheet.alpha_composite(face.resize((S * 2, S * 2), Image.NEAREST), ((i % cols) * (S * 2 + 8) + 4, (i // cols) * (S * 2 + 8) + 4))
        sheet.save(dst)
        print('sheet', dst)


if __name__ == '__main__':
    main()
