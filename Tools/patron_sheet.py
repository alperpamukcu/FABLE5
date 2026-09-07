# -*- coding: utf-8 -*-
"""THE CAST AS ONE PAGE (2026-09-07): a self-contained HTML sheet of every patron and every
clip, playing at the game's own 12 fps, so the whole cast can be looked at without opening
Unity. The editor window (LastCall > Patron Preview) is the in-game answer; this is the one
that travels - it opens in a browser and it can be sent.

    py -3 -X utf8 Tools/patron_sheet.py            -> Tools/patron_sheet.html
    py -3 -X utf8 Tools/patron_sheet.py --open     ...and opens it

The frames are COPIED beside the page (Tools/patron_sheet_frames/) rather than inlined: 24
patrons x ~100 frames is 29 MB of base64 in one file, which a browser parses before it draws
anything. As files it opens instantly and the page itself is a few kilobytes.
"""
import io
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
PATRON = os.path.join(ROOT, 'Assets', 'Resources', 'Patron')
OUT = os.path.join(HERE, 'patron_sheet.html')
FRAMES = os.path.join(HERE, 'patron_sheet_frames')
CLIPS = ('idle', 'order', 'drink', 'cheer', 'upset', 'walk', 'look_right', 'look_left')
FPS = 12
DRINK_CYCLE = 4.4


def publish(path, slug, clip, index):
    """Copy one frame beside the page and answer its relative URL."""
    folder = os.path.join(FRAMES, slug)
    os.makedirs(folder, exist_ok=True)
    name = '%s_%02d.png' % (clip, index)
    shutil.copyfile(path, os.path.join(folder, name))
    return 'patron_sheet_frames/%s/%s' % (slug, name)


def clip_frames(slug, clip):
    d = os.path.join(PATRON, slug, clip)
    if not os.path.isdir(d):
        return []
    return [os.path.join(d, n) for n in sorted(n for n in os.listdir(d) if n.endswith('.png'))]


def main():
    if os.path.isdir(FRAMES):
        shutil.rmtree(FRAMES)          # a stale frame is a lie about the art
    cast = {}
    for slug in sorted(os.listdir(PATRON)):
        if not os.path.isdir(os.path.join(PATRON, slug)):
            continue
        entry = {}
        for clip in CLIPS:
            entry[clip] = [publish(p, slug, clip, i) for i, p in enumerate(clip_frames(slug, clip))]
        face = os.path.join(PATRON, slug, 'face.png')
        entry['face'] = (publish(face, slug, 'face', 0) if os.path.exists(face)
                         else (entry['idle'][0] if entry['idle'] else ''))
        cast[slug] = entry
        print('  %-14s %s' % (slug, ' '.join('%s:%d' % (c, len(entry[c])) for c in CLIPS)))

    html = TEMPLATE.replace('__CAST__', json.dumps(cast)).replace('__CLIPS__', json.dumps(list(CLIPS)))
    html = html.replace('__FPS__', str(FPS)).replace('__DRINK__', str(DRINK_CYCLE))
    io.open(OUT, 'w', encoding='utf-8').write(html)
    total = sum(os.path.getsize(os.path.join(r, f))
                for r, _, fs in os.walk(FRAMES) for f in fs)
    print('sheet %s (%.0f KB page + %.1f MB frames, %d patrons)'
          % (OUT, os.path.getsize(OUT) / 1e3, total / 1e6, len(cast)))
    if '--open' in sys.argv:
        os.startfile(OUT)


TEMPLATE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>LAST CALL — the cast</title>
<style>
  :root { --ink:#f2e8d5; --ground:#241830; --panel:#2f2040; --line:#4a3560; --lit:#e84da6; }
  * { box-sizing: border-box; }
  body { margin:0; background:var(--ground); color:var(--ink);
         font:13px/1.5 ui-monospace, "Cascadia Mono", Consolas, monospace; }
  header { display:flex; gap:12px; align-items:center; padding:10px 14px;
           background:var(--panel); border-bottom:1px solid var(--line); position:sticky; top:0; z-index:2; }
  header h1 { font-size:14px; margin:0 12px 0 0; letter-spacing:.08em; }
  button, select { background:#3a2a4e; color:var(--ink); border:1px solid var(--line);
                   padding:4px 10px; border-radius:3px; font:inherit; cursor:pointer; }
  button.on { background:var(--lit); color:#20132c; border-color:var(--lit); }
  main { display:grid; grid-template-columns:200px 1fr; gap:0; height:calc(100vh - 49px); }
  #list { overflow:auto; border-right:1px solid var(--line); }
  .row { display:flex; gap:8px; align-items:center; padding:6px 8px; cursor:pointer; }
  .row:hover { background:#33234a; }
  .row.sel { background:#3d2a58; }
  .row img { width:34px; height:34px; image-rendering:pixelated; }
  .row b { font-weight:600; }
  .row small { display:block; opacity:.6; }
  #stage { overflow:auto; padding:14px; }
  #big { image-rendering:pixelated; display:block; background:#1a1024; border:1px solid var(--line); }
  #strip { display:flex; flex-wrap:wrap; gap:4px; margin-top:12px; }
  #strip img { width:64px; height:64px; image-rendering:pixelated; background:#1a1024;
               border:1px solid transparent; cursor:pointer; }
  #strip img.lit { border-color:var(--lit); background:#33234a; }
  #grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(130px,1fr)); gap:10px; padding:14px; }
  #grid figure { margin:0; text-align:center; cursor:pointer; }
  #grid img { width:120px; height:120px; image-rendering:pixelated; background:#1a1024;
              border:1px solid var(--line); }
  #grid figcaption { font-size:11px; opacity:.75; margin-top:3px; }
  .meta { opacity:.7; margin:6px 0 0; }
</style></head>
<body>
<header>
  <h1>LAST CALL · THE CAST</h1>
  <button id="mode">Whole cast</button>
  <button id="play" class="on">Pause</button>
  <button id="restart">Restart</button>
  <label>Clip <select id="clip"></select></label>
  <label>Zoom <input id="zoom" type="range" min="1" max="4" value="2"></label>
  <span id="count" class="meta"></span>
</header>
<main>
  <div id="list"></div>
  <div id="stage">
    <img id="big" alt="">
    <p class="meta" id="info"></p>
    <div id="strip"></div>
  </div>
</main>
<div id="grid" hidden></div>
<script>
const CAST = __CAST__, CLIPS = __CLIPS__, FPS = __FPS__, DRINK_CYCLE = __DRINK__;
const slugs = Object.keys(CAST);
let picked = slugs[0], clip = 'order', playing = true, t = 0, zoom = 2, sheet = false;
const $ = id => document.getElementById(id);

function drinkTicks(i, n) { const mid = (n - 1) >> 1, d = Math.abs(i - mid);
  return d <= 1 ? 5 : d <= 4 ? 2 : 1; }

function frameIndex(clip, t, n) {
  if (n <= 1) return 0;
  if (clip === 'walk') return Math.floor(t * FPS) % n;
  if (clip === 'drink') {
    let u = (t % DRINK_CYCLE) * FPS, acc = 0;
    for (let i = 0; i < n; i++) { acc += drinkTicks(i, n); if (u < acc) return i; }
    return n - 1;
  }
  const cycle = n / FPS + 0.8;
  return Math.min(n - 1, Math.floor((t % cycle) * FPS));
}

function buildList() {
  $('list').innerHTML = slugs.map(s => {
    const missing = CLIPS.filter(c => !CAST[s][c].length).length;
    return `<div class="row ${s === picked ? 'sel' : ''}" data-slug="${s}">
      <img src="${CAST[s].face}"><div><b>${s}</b>
      <small>${8 - missing} clips${missing ? ' · ' + missing + ' missing' : ''}</small></div></div>`;
  }).join('');
  $('list').querySelectorAll('.row').forEach(r => r.onclick = () => {
    picked = r.dataset.slug; t = 0; buildList(); buildStrip(); });
}

function buildStrip() {
  const frames = CAST[picked][clip] || [];
  $('strip').innerHTML = frames.map((f, i) => `<img src="${f}" data-i="${i}">`).join('');
  $('strip').querySelectorAll('img').forEach(img => img.onclick = () => {
    playing = false; $('play').textContent = 'Play'; $('play').classList.remove('on');
    t = (+img.dataset.i) / FPS; });
}

function buildGrid() {
  $('grid').innerHTML = slugs.map(s =>
    `<figure data-slug="${s}"><img data-slug="${s}"><figcaption>${s}</figcaption></figure>`).join('');
  $('grid').querySelectorAll('figure').forEach(f => f.onclick = () => {
    picked = f.dataset.slug; sheet = false; t = 0; applyMode(); buildList(); buildStrip(); });
}

function applyMode() {
  $('grid').hidden = !sheet;
  document.querySelector('main').hidden = sheet;
  $('mode').textContent = sheet ? 'One patron' : 'Whole cast';
}

let last = performance.now();
function tick(now) {
  const dt = (now - last) / 1000; last = now;
  if (playing) t += dt;
  if (sheet) {
    $('grid').querySelectorAll('img').forEach(img => {
      const frames = CAST[img.dataset.slug][clip] || [];
      if (frames.length) img.src = frames[frameIndex(clip, t, frames.length)];
    });
  } else {
    const frames = CAST[picked][clip] || [];
    if (frames.length) {
      const i = frameIndex(clip, t, frames.length);
      $('big').src = frames[i];
      $('big').style.width = (220 * zoom) + 'px';
      $('info').textContent = `${picked} · ${clip} · frame ${i + 1}/${frames.length}` +
        ` · ${(frames.length / FPS).toFixed(2)}s at ${FPS} fps`;
      $('strip').querySelectorAll('img').forEach((im, k) => im.classList.toggle('lit', k === i));
    } else { $('big').removeAttribute('src'); $('info').textContent = picked + ' · ' + clip + ' · no frames'; }
  }
  requestAnimationFrame(tick);
}

$('clip').innerHTML = CLIPS.map(c => `<option${c === clip ? ' selected' : ''}>${c}</option>`).join('');
$('clip').onchange = e => { clip = e.target.value; t = 0; buildStrip(); };
$('play').onclick = () => { playing = !playing;
  $('play').textContent = playing ? 'Pause' : 'Play'; $('play').classList.toggle('on', playing); };
$('restart').onclick = () => { t = 0; };
$('zoom').oninput = e => { zoom = +e.target.value; };
$('mode').onclick = () => { sheet = !sheet; applyMode(); };
$('count').textContent = slugs.length + ' patrons · ' + CLIPS.length + ' clips each';
buildList(); buildStrip(); buildGrid(); applyMode();
requestAnimationFrame(tick);
</script>
</body></html>
"""

if __name__ == '__main__':
    main()
