import sys
from PIL import Image
def peek(path, out, picks=(0.15, 0.4, 0.7, 0.99), cols=2):
    g = Image.open(path); n = g.n_frames
    idx = [min(n - 1, int(p * n)) for p in picks]
    W, H = g.size
    rows = -(-len(idx) // cols)
    S = Image.new('RGB', (W * cols, H * rows))
    for i, k in enumerate(idx):
        g.seek(k); S.paste(g.convert('RGB'), ((i % cols) * W, (i // cols) * H))
    S.save(out); print(n, 'frames')
if __name__ == '__main__':
    peek(sys.argv[1], sys.argv[2])
