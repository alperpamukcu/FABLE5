# fx_sink.png — the counter sink, from the author's own art (2026-08-24).
# Source: Tools/AssetPipeline/sources/konsept_art/lavabo.png (copied from the
# author's Desktop "konsept art" folder — a 1024x576 canvas with the sprite
# drawn small in the upper left). This script crops the sprite out of the
# canvas, drops stray lone pixels, and writes it 1:1 into Resources/Fixtures.
# Re-running is idempotent: same source, same crop, same bytes.

from PIL import Image
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Tools", "AssetPipeline", "sources", "konsept_art", "lavabo.png")
OUT = os.path.join(ROOT, "Assets", "Resources", "Fixtures", "fx_sink.png")
PREVIEW = os.path.join(ROOT, "Tools", "sink_preview_8x.png")

img = Image.open(SRC).convert("RGBA")
px = img.load()

# Bounding boxes of CONNECTED ink, so a stray dot far from the sink does not
# stretch the crop. Flood over the alpha channel, 8-connected.
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

count, minx, miny, maxx, maxy = clusters[0]
sink = img.crop((minx, miny, maxx + 1, maxy + 1))
print(f"sink: {sink.width}x{sink.height} at ({minx},{miny}) from {count} px")

sink.save(OUT)
sink.resize((sink.width * 8, sink.height * 8), Image.NEAREST).save(PREVIEW)
print(f"wrote {OUT} and {PREVIEW}")
