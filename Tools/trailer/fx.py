# -*- coding: utf-8 -*-
"""THE TRAILER'S CAMERA, IN THE EDIT (2026-10-02, v4). The author's notes on v3: "kamera titremesi, kamera oynaması,
zoom, odak, kayma ... bunlar olmadan çok yavan oluyor"; "roxy'nin konuşmasını yaptığı sahnelerde ışık roxy'ye
odaklansın"; a run of reactions with "çok hızlı bir geçiş efekti ve geçiş sesi"; the music too loud and badly chosen;
the picture too soft. So every frame of the cut is composed here, in Python, from the films the game shot of itself:

  camera   keyframed zoom and centre per segment (eased), sub-pixel crops so a move glides; or TRACK a subject the
           film marked ("focus roxy x y", "focus react3 x y") and the frame follows it
  shake    a decaying shake on impacts - the kick, the stamp, a shake of the tin, the full stream, a reaction
  light    a spotlight on the subject (Roxy while she talks), the rest of the room dimmed
  focus    the background blurred around the subject (rack focus)
  in       whip (a smear across, with a whoosh), flash (out of white), punch (in from a zoom), cut
  words    Roxy's plate, the kinetic titles and the sign, from overlays.py

Every frame goes to one encoder (H.264 High, CRF 12, 1080p60 - ~30-50 Mbit/s): what Steam asks for, with headroom.

    python3 Tools/trailer/fx.py              cuts/v4_60.json, 16:9 and 9:16
    python3 Tools/trailer/fx.py --wide --lang tr
    python3 Tools/trailer/fx.py --audition   the candidate tracks, 12 s each, one file
"""
import json
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageFilter

import edit as E
import overlays as O
import story as S

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
WORK = os.path.join(OUT, 'work_fx')
FPS = 60
SRC_W, SRC_H = 1920, 1080

# impacts that shake the frame when the film marks them: amplitude (output px), decay (s)
SHAKE_ON = {
    'kicked': (22, 0.45), 'full stream': (5, 0.6), 'shake': (9, 2.2), 'served': (7, 0.4), 'slip': (6, 0.5),
    'stamp': (18, 0.4), 'capped': (6, 0.3), 'bought': (8, 0.35), 'card open': (5, 0.3), 'liar': (6, 0.3),
}
for k in range(1, 9):
    SHAKE_ON['react %d' % k] = (10, 0.35)
for k in range(1, 8):
    SHAKE_ON['room %d' % k] = (7, 0.3)
WHOOSH = ('whoosh', 0.9)
# marks that flash the frame white for a beat (the room changing its clothes)
FLASH_ON = {'room %d' % k for k in range(1, 8)}
SFX = dict(S.SFX)
SFX['closing'] = [('day_close', 0.9, 0.0), ('curtain', 0.8, 0.6)]
SFX['book open'] = [('book_open', 0.9, -0.1)]
SFX['book shut'] = [('book_close', 0.9, -0.1)]
for p in range(1, 9):
    SFX['page %d' % p] = [('page_turn_%d' % (1 + (p - 1) % 3), 0.8, -0.05)]
for k in range(1, 9):
    SFX['react %d' % k] = [('serve_clink_%d' % (1 + (k - 1) % 3), 0.8, 0.0)]
for k in range(1, 11):                                      # a bottle set down on the bench as it lands
    SFX['bottle %d' % k] = [('bottle_set_%d' % (1 + (k - 1) % 3), 0.8, 0.35)]
SFX['garnish salt_rim'] = [('rim_turn', 0.9, 0.3), ('rim_done', 0.9, 1.2)]
SFX['garnish lemon_twist'] = [('garnish', 0.9, 0.4)]


def smooth(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


def keyed(keys, u):
    """[ [u, z, cx, cy], ... ] eased between keys."""
    if not keys:
        return 1.0, 0.5, 0.5
    if u <= keys[0][0]:
        return tuple(keys[0][1:4])
    for a, b in zip(keys, keys[1:]):
        if u <= b[0]:
            w = smooth((u - a[0]) / max(1e-6, b[0] - a[0]))
            return tuple(a[i] + (b[i] - a[i]) * w for i in (1, 2, 3))
    return tuple(keys[-1][1:4])


def focus_track(marks, who, start, speed, dur):
    """[(t, x, y)] of a subject's marks inside the window, in segment time."""
    pts = []
    for m in marks:
        parts = m['mark'].split()
        if len(parts) == 4 and parts[0] == 'focus' and parts[1] == who:
            t = (m['t'] - start) / speed
            pts.append((t, float(parts[2]), float(parts[3])))
    pts.sort()
    near = [p for p in pts if -1.5 <= p[0] <= dur + 1.5]
    return near or ([min(pts, key=lambda p: abs(p[0]))] if pts else [])


def at_track(track, t):
    if not track:
        return None
    if t <= track[0][0]:
        return track[0][1:]
    for a, b in zip(track, track[1:]):
        if t <= b[0]:
            w = (t - a[0]) / max(1e-6, b[0] - a[0])
            return (a[1] + (b[1] - a[1]) * w, a[2] + (b[2] - a[2]) * w)
    return track[-1][1:]


def frames_of(film, start, n, speed):
    """n RGB frames of the film from `start`, at `speed`, padded with the last frame."""
    cmd = ['ffmpeg', '-v', 'error', '-ss', '%.3f' % start, '-t', '%.3f' % (n / FPS * speed + 0.4),
           '-i', os.path.join(S.films(), film + '.mp4'),
           '-vf', 'fps=%d,setpts=(PTS-STARTPTS)/%f,fps=%d' % (FPS, speed, FPS),
           '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-']
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    size = SRC_W * SRC_H * 3
    last = None
    for _ in range(n):
        buf = p.stdout.read(size)
        if len(buf) == size:
            last = np.frombuffer(buf, np.uint8).reshape(SRC_H, SRC_W, 3)
        if last is None:
            last = np.zeros((SRC_H, SRC_W, 3), np.uint8)
        yield last
    p.stdout.close()
    p.kill()


class Spot:
    """A soft spotlight mask, computed small and scaled up."""

    def __init__(self, ow, oh):
        self.ow, self.oh = ow, oh
        self.sw, self.sh = ow // 8, oh // 8
        yy, xx = np.mgrid[0:self.sh, 0:self.sw]
        self.xx, self.yy = xx / self.sw, yy / self.sh

    def mask(self, x, y, r, dark, soft=0.16):
        aspect = self.ow / self.oh
        d = np.sqrt(((self.xx - x) * aspect) ** 2 + (self.yy - y) ** 2)
        m = np.clip(1 - (d - r) / soft, 0, 1)
        m = m * m * (3 - 2 * m)
        m = dark + (1 - dark) * m
        img = Image.fromarray((m * 255).astype(np.uint8), 'L').resize((self.ow, self.oh), Image.BILINEAR)
        return np.asarray(img).astype(np.float32)[..., None] / 255.0


def compose(frame, z, cx, cy, ow, oh, dx, dy):
    """The camera: a window of the source around (cx, cy) at zoom z, shaken by (dx, dy) output px."""
    tall = oh > ow
    ch = SRC_H / z
    cw = ch * ow / oh if tall else SRC_W / z
    if not tall:
        ch = cw * oh / ow
    sx, sy = cw / ow, ch / oh
    x0 = cx * SRC_W - cw / 2 - dx * sx
    y0 = cy * SRC_H - ch / 2 - dy * sy
    x0 = max(0.0, min(SRC_W - cw, x0))
    y0 = max(0.0, min(SRC_H - ch, y0))
    img = Image.fromarray(frame).resize((ow, oh), Image.LANCZOS, box=(x0, y0, x0 + cw, y0 + ch))
    if z > 1.2:                                              # a zoomed film is soft: give the edges back
        img = img.filter(ImageFilter.UnsharpMask(1.0, 50, 2))
    return img, (x0, y0, cw, ch)


def whip(img, k, n, direction):
    """A whip pan's smear for frame k of n: copies of the frame dragged along x and averaged."""
    u = 1 - k / n
    if u <= 0:
        return img
    a = np.asarray(img).astype(np.float32)
    span = int(220 * u)
    acc = np.zeros_like(a)
    taps = 7
    for i in range(taps):
        sh = int(direction * span * (i / (taps - 1) - 0.5))
        acc += np.roll(a, sh, axis=1)
    out = acc / taps
    return Image.fromarray(np.clip(out * (1 + 0.15 * u), 0, 255).astype(np.uint8))


def shake_at(t, events):
    """Output-pixel offset at t from decaying impacts [(t0, amp, decay)]."""
    dx = dy = 0.0
    for t0, amp, decay in events:
        if t < t0:
            continue
        a = amp * math.exp(-(t - t0) / decay) if decay < 1.5 else amp * (1.0 if t - t0 < decay else 0.0)
        dx += a * (math.sin(t * 71.0 + t0) * 0.6 + math.sin(t * 37.0 + 1.3 * t0) * 0.4)
        dy += a * (math.sin(t * 53.0 + 2.1 * t0) * 0.6 + math.sin(t * 29.0 + t0) * 0.4)
    return dx, dy


def seq_frame(d, i):
    p = os.path.join(d, '%04d.png' % i)
    return Image.open(p) if os.path.exists(p) else None


def plates():
    """The opening and the end card, made from the Steam art's own masters (they ride LFS, so they are built here
    rather than shipped twice): the end card is the main capsule's native pixel art at exactly 4x."""
    art = os.path.join(HERE, 'art')
    os.makedirs(art, exist_ok=True)
    nano = os.path.join(E.ROOT, 'Tools', 'steam_page', 'out', 'nano')
    end = os.path.join(art, 'endcard.png')
    if not os.path.exists(end):
        a = np.asarray(Image.open(os.path.join(nano, 'snap_main_capsule_pro_1.png')).convert('RGB'))
        a = np.pad(a, ((2, 2), (0, 1), (0, 0)), mode='edge')        # 266x479 -> 270x480
        Image.fromarray(a).resize((1920, 1080), Image.NEAREST).save(end)
    opening = os.path.join(art, 'opening.png')
    if not os.path.exists(opening):
        Image.open(os.path.join(nano, 'page_background_pro_2.png')).convert('RGB') \
            .resize((1920, 1080), Image.LANCZOS).save(opening)


def render(cut_name='v4_60', lang='en', tall=False, audio_only=False):
    cut = json.load(open(os.path.join(HERE, 'cuts', cut_name + '.json')))
    lines_all = json.load(open(os.path.join(HERE, 'captions.json')))
    lines = dict(lines_all.get('en', {}))
    lines.update(lines_all.get(lang, {}))
    os.makedirs(WORK, exist_ok=True)
    ow, oh = (1080, 1920) if tall else (1920, 1080)
    # the composed beat when the cut names it (the onset tracker drifts on some tracks), measured otherwise
    period = cut.get('beat') or E.beat_grid(cut['music'], 0.0)[0]
    spot = Spot(ow, oh)
    name = cut_name + ('' if lang == 'en' else '_' + lang) + ('_tall' if tall else '')
    video_tmp = os.path.join(WORK, name + '_picture.mp4')
    # audio-only (2026-10-03): the picture already rendered is kept and only the sound is mixed again
    enc = None if audio_only else subprocess.Popen(
        ['ffmpeg', '-y', '-v', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '%dx%d' % (ow, oh),
         '-r', str(FPS), '-i', '-', '-c:v', 'libx264', '-preset', 'slow', '-crf', '12',
         '-profile:v', 'high', '-pix_fmt', 'yuv420p', video_tmp], stdin=subprocess.PIPE)
    sounds, talk_windows, t_total = [], [], 0.0
    carry = []                                               # words that outlive their segment: (dir, xy, from, to)
    print('%s: %s, beat %.3f s' % (name, cut['music'], period))

    for si, seg in enumerate(cut['segments']):
        dur = max(period, round(seg['sec'] / period) * period)
        n = int(round(dur * FPS))
        # a designed picture instead of a film (2026-10-03: the opening and the end card are art, not the game)
        if seg.get('image'):
            take = ('image', 0.0, [])
        else:
            take = S.find_take(seg.get('take', []))
        if take is None:
            print('  - %2d skipped: nothing filmed for %s' % (si, seg.get('take', [[None]])[0][0]))
            continue
        film, start, marks = take
        speed = float(seg.get('speed', 1.0))
        keys = seg.get('cam') or [[0, 1.0, 0.5, 0.5], [1, 1.04, 0.5, 0.5]]
        if tall and seg.get('cam_tall'):
            keys = seg['cam_tall']
        who = seg.get('track')
        track = focus_track(marks, who, start, speed, dur) if who else []
        lit = seg.get('spot')
        lit_track = focus_track(marks, lit, start, speed, dur) if isinstance(lit, str) else []
        # impacts: the film's own marks inside the window, and any the segment adds
        events = []
        for m in marks:
            key = m['mark']
            if key in SHAKE_ON:
                rel = (m['t'] - start) / speed
                if 0 <= rel < dur:
                    events.append((rel,) + SHAKE_ON[key])
        for t0, amp, decay in seg.get('shake', []):
            events.append((t0, amp, decay))
        flashes = [(m['t'] - start) / speed for m in marks if m['mark'] in FLASH_ON]
        flashes = [f for f in flashes if 0 <= f < dur]
        # words
        layers = []
        if seg.get('say'):
            st = seg.get('say_at', 2.4 if seg.get('end') else 0.0)
            secs = seg.get('say_sec', dur - st)
            d, xy, bl = O.narrator(lines[seg['say']], secs, os.path.join(WORK, 'say_%d_%d' % (si, oh)),
                                   ow, oh, top=seg.get('box') == 'top')
            layers.append((d, xy, st))
            sounds += [('key_press', 0.3, t_total + st + b) for b in bl]
            talk_windows.append((t_total + st, t_total + st + secs))
        if seg.get('sign') or seg.get('end'):
            sign = S.sign_image(840)
            d = O.sign_on(sign, dur, os.path.join(WORK, 'sign_%d_%d' % (si, oh)))
            sx = int(seg['sign_x'] * ow - sign.width / 2) if 'sign_x' in seg else (ow - sign.width) // 2
            layers.append((d, (sx, int(oh * seg.get('sign_y', 0.05 if not tall else 0.08)) if seg.get('end')
                               else (oh - sign.height) // 2 - (40 if not tall else 200)), 0.25))
            sounds.append(('synth_swell', 0.8, t_total))
        if seg.get('end'):
            face = None if lang == 'en' else E.face_for(lang, lines.get('wishlist', ''))
            ca, wa = seg.get('cta_at', 1.0), seg.get('when_at', 1.6)
            d, xy = O.title(lines.get('wishlist', 'WISHLIST ON STEAM'), dur - ca, os.path.join(WORK, 'cta_%d_%d' % (si, oh)),
                            ow, oh, lang_face=face, y_frac=seg.get('cta_y', 0.40 if not tall else 0.32), max_scale=3, band=True)
            if 'cta_x' in seg:
                xy = (int(seg['cta_x'] * ow - (ow - 2 * xy[0]) / 2), xy[1])
            layers.append((d, xy, ca))
            if lines.get('when'):
                d, xy = O.title(lines['when'], dur - wa, os.path.join(WORK, 'when_%d_%d' % (si, oh)), ow, oh,
                                lang_face=face, y_frac=seg.get('when_y', 0.50 if not tall else 0.38), max_scale=2, band=True)
                if 'cta_x' in seg:
                    xy = (int(seg['cta_x'] * ow - (ow - 2 * xy[0]) / 2), xy[1])
                layers.append((d, xy, wa))
        if seg.get('title'):
            face = None if lang == 'en' else E.face_for(lang, lines.get(seg['title'], ''))
            span = float(seg.get('title_sec', dur))
            d, xy = O.title(lines.get(seg['title'], seg['title']), span, os.path.join(WORK, 'title_%d_%d' % (si, oh)), ow, oh,
                            lang_face=face, y_frac=seg.get('title_y', 0.12 if not tall else 0.16), max_scale=4, band=True)
            carry.append((d, xy, t_total + seg.get('title_at', 0.0), t_total + seg.get('title_at', 0.0) + span))
            sounds.append(('click_2', 0.5, t_total + seg.get('title_at', 0.0)))
        trans = seg.get('in', 'cut')
        if trans in ('whip', 'punch'):
            sounds.append((WHOOSH[0], WHOOSH[1], max(0.0, t_total - 0.08)))
        direction = 1 if si % 2 else -1

        art = None
        if seg.get('image') and not audio_only:
            plates()
            art = np.asarray(Image.open(os.path.join(HERE, seg['image'])).convert('RGB').resize((SRC_W, SRC_H), Image.LANCZOS))
        src = frames_of(film, start, n, speed) if not seg.get('still') and art is None and not audio_only else None
        still = None
        if seg.get('still') and not audio_only:
            fr = next(frames_of(film, start, 1, 1.0))
            still = fr
        for i in range(0 if audio_only else n):
            t = i / FPS
            frame = art if art is not None else (still if still is not None else next(src))
            z, cx, cy = keyed(keys, i / max(1, n - 1))
            if track:
                p = at_track(track, t)
                if p:
                    cx = cx * 0.15 + p[0] * 0.85
                    cy = cy * 0.15 + p[1] * 0.85
            if trans == 'punch' and t < 0.18:
                z *= 1 + 0.35 * (1 - smooth(t / 0.18))
            dx, dy = shake_at(t, events)
            img, (x0, y0, cw, ch) = compose(frame, z, cx, cy, ow, oh, dx, dy)
            # the light on the subject, and the room around it softened
            if lit:
                if isinstance(lit, str):
                    p = at_track(lit_track, t)
                else:
                    p = lit
                if p:
                    px = (p[0] * SRC_W - x0) / cw
                    py = (p[1] * SRC_H - y0) / ch
                    r = seg.get('spot_r', 0.16)
                    m = spot.mask(px, py, r, seg.get('dark', 0.38))
                    a = np.asarray(img).astype(np.float32)
                    if seg.get('blur'):
                        soft = img.resize((ow // 4, oh // 4), Image.BILINEAR).filter(ImageFilter.GaussianBlur(2.2)) \
                            .resize((ow, oh), Image.BILINEAR)
                        b = np.asarray(soft).astype(np.float32)
                        mm = (m - seg.get('dark', 0.38)) / max(1e-3, 1 - seg.get('dark', 0.38))
                        a = a * mm + b * (1 - mm)
                    a = a * m
                    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
            if trans == 'whip' and i < 8:
                img = whip(img, i, 8, direction)
            if trans == 'flash' and t < 0.16:
                w = 1 - t / 0.16
                a = np.asarray(img).astype(np.float32)
                img = Image.fromarray((a * (1 - 0.85 * w) + 255 * 0.85 * w).astype(np.uint8))
            for f in flashes:
                if f <= t < f + 0.12:
                    w = 1 - (t - f) / 0.12
                    a = np.asarray(img).astype(np.float32)
                    img = Image.fromarray((a * (1 - 0.8 * w) + 255 * 0.8 * w).astype(np.uint8))
            if seg.get('flicker_on'):
                glow = 0.0
                for t0, v in ((0.0, 0.0), (0.18, 0.55), (0.24, 0.05), (0.34, 0.8), (0.40, 0.3), (0.52, 1.0)):
                    if t >= t0:
                        glow = v
                if glow < 1.0:
                    img = Image.fromarray((np.asarray(img).astype(np.float32) * glow).astype(np.uint8))
            if seg.get('end') and t < 0.3:
                a = np.asarray(img).astype(np.float32) * (t / 0.3)
                img = Image.fromarray(a.astype(np.uint8))
            now = t_total + t
            live = [(d, xy, st - t_total) for d, xy, st, en in carry if st <= now < en]
            if layers or live:
                img = img.convert('RGBA')
                for d, (lx, ly), st in layers + live:
                    k = int(round((t - st) * FPS))
                    if k < 0:
                        continue
                    ov = seq_frame(d, k)
                    if ov is not None:
                        img.alpha_composite(ov, (int(lx), int(ly)))
                img = img.convert('RGB')
            enc.stdin.write(img.tobytes())
        # the film's own sounds for every mark inside the window
        for m in marks:
            rel = (m['t'] - start) / speed
            for nm, gain, off in SFX.get(m['mark'], []):
                at = rel + off
                if 0 <= at < dur:
                    sounds.append((nm, gain, t_total + at))
        for nm, gain, at in seg.get('sfx', []):
            sounds.append((nm, gain, t_total + at))
        if seg.get('type_blips'):                              # the game's own plate typing out on screen
            a, b, step = seg['type_blips']
            k = 0
            while a + k * step < min(b, dur):
                sounds.append(('key_press', 0.28, t_total + a + k * step))
                k += 1
            talk_windows.append((t_total + a, t_total + min(dur, b + 0.4)))
        print('  %2d %-16s %4.1fs  %s%s' % (si, film, dur, trans, '  "' + lines[seg['say']] + '"' if seg.get('say') else ''))
        t_total += dur
    if enc:
        enc.stdin.close()
        enc.wait()

    # sound: the music under everything and lower still while Roxy talks; the game's effects on top
    duck = '+'.join('between(t,%.2f,%.2f)' % (a - 0.15, b) for a, b in talk_windows) or '0'
    mvol = cut.get('music_gain', 0.5)
    # the trailer's own score when the cut names it (score.py writes it to the cut's grid), else a game track
    music_path = os.path.join(OUT, 'score_%s.wav' % cut_name) if cut.get('music') == 'score' \
        else os.path.join(E.AUDIO, cut['music'] + '.ogg')
    a_in = ['-ss', '%.3f' % cut.get('music_from', 0.0), '-t', '%.3f' % t_total, '-i', music_path]
    fade = cut.get('music_fade', 1.5)
    filt = ["[1:a]volume='if(gt(%s,0),%.3f,%.3f)':eval=frame,afade=t=in:st=0:d=%.2f,afade=t=out:st=%f:d=%.2f[m]"
            % (duck, mvol * 0.55, mvol, cut.get('music_fade_in', 0.6), t_total - fade, fade)]
    mix = ['[m]']
    for k, (nm, gain, at) in enumerate(sounds):
        path = os.path.join(E.AUDIO, nm + '.wav')
        if not os.path.exists(path):
            continue
        a_in += ['-i', path]
        idx = len(mix) + 1
        filt.append('[%d:a]atrim=0:2.5,adelay=%d|%d,volume=%f[s%d]' % (idx, int(at * 1000), int(at * 1000), gain, k))
        mix.append('[s%d]' % k)
    filt.append('%samix=inputs=%d:normalize=0:duration=first,loudnorm=I=-14:TP=-1.5:LRA=11[a]' % (''.join(mix), len(mix)))
    final = os.path.join(OUT, name + '.mp4')
    E.run(['ffmpeg', '-y', '-v', 'error', '-i', video_tmp] + a_in +
          ['-filter_complex', ';'.join(filt), '-map', '0:v', '-map', '[a]', '-c:v', 'copy',
           '-c:a', 'aac', '-b:a', '320k', '-ar', '48000', '-movflags', '+faststart', '-shortest', final])
    br = int(subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=bit_rate', '-of', 'csv=p=0', final],
                            capture_output=True, text=True).stdout.strip() or 0)
    print('  -> %s (%.1f s, %.1f MB, %.1f Mbit/s)' % (os.path.relpath(final, E.ROOT), t_total,
                                                     os.path.getsize(final) / 1e6, br / 1e6))
    return final


def audition(tracks):
    """The candidate tracks back to back, 12 s each from where they get going, a beep between."""
    os.makedirs(WORK, exist_ok=True)
    parts = []
    for i, (track, start) in enumerate(tracks):
        p = os.path.join(WORK, 'aud_%d.wav' % i)
        E.run(['ffmpeg', '-y', '-v', 'error', '-ss', str(start), '-t', '12', '-i', os.path.join(E.AUDIO, track + '.ogg'),
               '-af', 'afade=t=in:d=0.3,afade=t=out:st=11.4:d=0.6', '-ar', '48000', '-ac', '2', p])
        parts.append(p)
    lst = os.path.join(WORK, 'aud.txt')
    open(lst, 'w').write(''.join("file '%s'\n" % p for p in parts))
    out = os.path.join(OUT, 'muzik_secenekleri.mp4')
    # a card naming each track while it plays
    cards = []
    for i, (track, start) in enumerate(tracks):
        img = Image.new('RGB', (1280, 720), (26, 16, 35))
        sign = O.glyphs('%d  %s' % (i + 1, track.replace('music_', '').upper()), __import__('PIL.ImageFont', fromlist=['x']).truetype(O.ARCADE, 16), O.CREAM)
        x = 0
        line = Image.new('RGBA', (int(sum(a for _, a in sign)) + 4, 24), (0, 0, 0, 0))
        for g, adv in sign:
            line.alpha_composite(g, (int(x), 2))
            x += adv
        big = line.resize((line.width * 3, line.height * 3), Image.NEAREST)
        img.paste(big, ((1280 - big.width) // 2, 320), big)
        cp = os.path.join(WORK, 'aud_card_%d.png' % i)
        img.save(cp)
        cards.append(cp)
    clst = os.path.join(WORK, 'aud_cards.txt')
    open(clst, 'w').write(''.join("file '%s'\nduration 12\n" % c for c in cards) + "file '%s'\n" % cards[-1])
    E.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', clst, '-f', 'concat', '-safe', '0', '-i', lst,
           '-vf', 'fps=30,format=yuv420p', '-c:v', 'libx264', '-crf', '28', '-c:a', 'aac', '-b:a', '192k', '-shortest', out])
    print('  ->', os.path.relpath(out, E.ROOT))


def main(argv):
    if '--audition' in argv:
        audition([('music_night_1', 17.5), ('music_night_3', 18.5), ('music_night_8', 17.5),
                  ('music_dayend_1', 30.0), ('music_menu_1', 60.0), ('music_lastcall_1', 20.0)])
        return
    lang = argv[argv.index('--lang') + 1] if '--lang' in argv else 'en'
    names = [a for a in argv if not a.startswith('--') and a != lang] or ['v4_60']
    shapes = [False, True]
    if '--wide' in argv:
        shapes = [False]
    if '--tall' in argv:
        shapes = [True]
    for nm in names:
        for tall in shapes:
            render(nm, lang, tall, audio_only='--audio' in argv)


if __name__ == '__main__':
    main(sys.argv[1:])
