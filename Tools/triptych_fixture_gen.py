# fx_triptych.png — the flamingo triptych on the back wall, from the author's own
# art (2026-08-24). Source: Tools/AssetPipeline/sources/konsept_art/flamingo_triptych.png
# (copied from the author's Desktop "konsept art" folder, original name Sprite-0003.png).
# Three framed panels with transparent gaps between them, so the crop is the UNION of
# every real ink cluster — the sink script's largest-cluster rule would keep one panel
# and throw away two. Lone specks still stay out. Re-running is idempotent: same
# source, same crop, same bytes.

from PIL import Image
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Tools", "AssetPipeline", "sources", "konsept_art",
                   "flamingo_triptych.png")
OUT = os.path.join(ROOT, "Assets", "Resources", "Fixtures", "fx_triptych.png")
PREVIEW = os.path.join(ROOT, "Tools", "triptych_preview_4x.png")

# A cluster smaller than this is a stray dot, not a panel.
MIN_CLUSTER = 10

img = Image.open(SRC).convert("RGBA")
px = img.load()

# Bounding boxes of CONNECTED ink (8-connected flood over alpha), so a stray dot far
# from the panels does not stretch the crop.
seen = [[False] * img.width for _ in range(img.height)]
clusters = []
for y in range(img.height):
    for x in range(img.width):
        if seen[y][x] or px[x, y][3] == 0:
            continue
        stack, minx, miny, maxx, maxy, count = [(x, y)], x, y, x, y, 0
        seen[y][x] = True
        while stack:
            cx, cy = stack.pop()
            count += 1
            minx, maxx = min(minx, cx), max(maxx, cx)
            miny, maxy = min(miny, cy), max(maxy, cy)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < img.width and 0 <= ny < img.height \
                            and not seen[ny][nx] and px[nx, ny][3] > 0:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
        clusters.append((count, minx, miny, maxx, maxy))

clusters.sort(reverse=True)
print("clusters (px count, bbox):")
for c in clusters[:8]:
    print("  ", c)

kept = [c for c in clusters if c[0] >= MIN_CLUSTER]
minx = min(c[1] for c in kept)
miny = min(c[2] for c in kept)
maxx = max(c[3] for c in kept)
maxy = max(c[4] for c in kept)
art = img.crop((minx, miny, maxx + 1, maxy + 1))
print(f"triptych: {art.width}x{art.height} at ({minx},{miny}) "
      f"from {len(kept)} panels ({len(clusters) - len(kept)} specks dropped)")

art.save(OUT)
art.resize((art.width * 4, art.height * 4), Image.NEAREST).save(PREVIEW)
print(f"wrote {OUT} and {PREVIEW}")
