# -*- coding: utf-8 -*-
"""Inline the two mock-up frames into the preview page as data URIs.

Kept as a step of its own because the Artifact host blocks every external request: a page
that references drawer_open.png by path renders with two broken images and no error. The
source file carries __CLOSED__ / __OPEN__ placeholders so it stays editable by hand.
"""
import base64, io, os

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'scene_cast_raw')
SRC = os.path.join(RAW, 'drawer_preview.src.html')
DST = os.path.join(RAW, 'drawer_preview.html')


def b64(name):
    with open(os.path.join(RAW, name), 'rb') as f:
        return base64.b64encode(f.read()).decode()


html = io.open(SRC, encoding='utf-8').read()
for token, png in (('__CLOSED__', 'drawer_closed.png'), ('__OPEN__', 'drawer_open.png')):
    if token not in html:
        raise SystemExit('placeholder %s not found in %s' % (token, SRC))
    html = html.replace(token, b64(png))
io.open(DST, 'w', encoding='utf-8').write(html)
print('wrote %s  (%.0f KB)' % (DST, os.path.getsize(DST) / 1024.0))
