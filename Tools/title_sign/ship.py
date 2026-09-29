# -*- coding: utf-8 -*-
"""SHIP the title sign's maps into the game (2026-09-29, GDD_MEVCUT §9.145).

The pipeline, run from this folder:

    py -3 -X utf8 stage_a_masters.py     # the two store masters (Tools/steam_kit/out/logo, local) -> work/masters.npz
    py -3 -X utf8 build_logo.py          # one MAP per screen scale -> out/maps/logo_<set>_map.png (~25 s for all 7)
    py -3 -X utf8 ship.py                # -> Assets/Resources/Logo/logo_<set>_map.bytes

A map is shipped as the PNG's own bytes under a .bytes name, so Unity imports it as a TextAsset and no texture
importer ever sees it (the 2048 cap alone would shrink the x4 set; compression or filtering would break the class /
group / phase channels). TitleSign decodes it with ImageConversion.LoadImage. Set names lose their dot on the way in
(x1.5 -> x1_5): a Resources path reads a dot as an extension. An existing .meta is kept, so a re-ship keeps its GUID;
a new map gets a TextScriptImporter meta with a fresh one. Idempotent: the same maps ship the same bytes.
"""
import os
import shutil
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'out', 'maps')
DST = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'Logo'))
SETS = ['x1', 'x1.25', 'x1.5', 'x2', 'x2.5', 'x3', 'x4']
META = ('fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n'
        '  assetBundleName: \n  assetBundleVariant: \n')


def main():
    os.makedirs(DST, exist_ok=True)
    folder_meta = DST + '.meta'
    if not os.path.exists(folder_meta):
        with open(folder_meta, 'w', newline='\n') as f:
            f.write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
                    '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)
    for s in SETS:
        src = os.path.join(SRC, 'logo_%s_map.png' % s)
        name = 'logo_%s_map.bytes' % s.replace('.', '_')
        dst = os.path.join(DST, name)
        shutil.copyfile(src, dst)
        if not os.path.exists(dst + '.meta'):
            with open(dst + '.meta', 'w', newline='\n') as f:
                f.write(META % uuid.uuid4().hex)
        print('%-24s %7d bytes' % (name, os.path.getsize(dst)))


if __name__ == '__main__':
    main()
