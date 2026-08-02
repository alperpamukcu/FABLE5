# -*- coding: utf-8 -*-
"""
The bottle set, generated per BRAND on PixelLab (the author, 2026-08-02).

Four vodkas shared one drawing because the art was keyed by STYLE. Every card gets
its own vessel now, drawn in the SAME language and at the same grain as the shelf
already uses (side view, ~92x168 before the quantize chain), and each silhouette is
one people already know from a real shelf — with the label left BLANK, because the
label is ours to print later.

Two passes:
  pass 1  create_1_direction_object   the closed bottle
  pass 2  create_object_state         the same bottle with its closure removed —
                                      a DESIGNED open mouth, not the sprite cropped

State is kept in bottles_state.json so the run can be resumed: generation is
30-90s a bottle and there are 41 of them.
"""
import json, io, os, sys, time, base64, urllib.request
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'bottles_raw')
STATE = os.path.join(HERE, 'bottles_state.json')
os.makedirs(OUT, exist_ok=True)

# One shared tail so all 41 read as one shelf: the project's own art language.
TAIL = ("side view, clean pixel art, soft shading with subtle dithering, no anti-aliasing, "
        "dark outline, transparent background, standing upright and centred, "
        "the label area is a COMPLETELY BLANK plain panel with no text and no writing, "
        "no letters, no words, no numbers, no logo")

# id -> (size, prompt). The prompts describe the shapes people recognise; no brand
# is named, because what carries is the silhouette, not the trademark.
BOTTLES = {
 # ── vodka ──
 'vodka_astra': (160, "a clear glass vodka bottle shaped like a squat apothecary medicine bottle, straight cylindrical sides, rounded shoulder, short neck, brushed silver screw cap, a blank rectangular label panel across the middle"),
 'vodka_vor': (160, "a frosted white glass vodka bottle, tall with square shoulders and a long slim neck, silver screw cap, a blank pale label panel on the body and a blank narrow band on the neck"),
 'vodka_leonid': (160, "a tall slender frosted glass vodka bottle, very long elegant neck, tall black cap, a blank tall label panel on the body"),
 'vodka_okhta': (160, "an ornate crystal vodka decanter, faceted rounded belly, long narrow neck, gold stopper, a blank oval label panel on the belly"),
 # ── gin ──
 'gin_boothby': (160, "a dark green glass gin bottle, round shoulders, classic short neck, cork stopper, a blank cream label panel across the middle"),
 'gin_juniper_crown': (160, "a clear glass gin bottle with flat square shoulders and straight sides, silver screw cap, a blank tall label panel on the front"),
 'gin_thornwood': (160, "a dark green gin bottle shaped like a cocktail shaker, wide base tapering all the way up to a narrow top, black wax seal over the neck, a blank label band around the widest part"),
 'gin_veilcrest': (160, "a deep green cathedral-shaped gin decanter, tall arched body, long neck, gold stopper, a blank arched label panel"),
 # ── whiskey ──
 'bourbon_redline': (160, "a square-shouldered whiskey bottle of brown glass, flat sides, black screw cap, a blank rectangular label panel on the front"),
 'bourbon_old_harrow': (160, "a round amber whiskey bottle, sloped shoulders, cork stopper, a blank rectangular label panel"),
 'bourbon_ashfall': (150, "a squat wide amber whiskey bottle with a short thick neck, thick black wax seal dripping over the neck, a blank square label panel"),
 'bourbon_hollow_oak': (152, "an antique squat dark brown whiskey bottle, heavy glass, deep red wax seal over the neck, a blank aged label panel and a blank neck band"),
 # ── rum ──
 'rum_cane_coral': (160, "a clear glass rum bottle with rounded shoulders and a medium neck, silver screw cap, a blank label panel across the middle"),
 'rum_tidewater': (150, "a squat wide amber rum bottle with a rope-wrapped neck, cork stopper, a blank label panel"),
 'rum_windward': (156, "a dark brown aged rum bottle, round belly, gold stopper, a blank oval label panel and a blank gold neck band"),
 'rum_reina_del_mar': (160, "a dark green ornate rum decanter with a long neck and gold stopper, a blank shield-shaped label panel"),
 # ── tequila ──
 'tequila_sonora': (160, "a tall slim clear tequila bottle with a very long neck, silver screw cap, a blank tall label panel"),
 'tequila_alta_luna': (162, "a tall clear tequila bottle with a long neck and a cork stopper, a blank label panel and a blank neck band"),
 'tequila_sol_viejo': (148, "a short wide hand-blown amber tequila bottle with a rounded belly, thick glass, round cork stopper, a blank round label panel"),
 'tequila_cielo_roto': (150, "a hand-blown blue glass tequila bottle, rounded belly, short wide neck, pewter round stopper, a blank round label panel"),
 # ── the rest of the back bar ──
 'amaro_notte': (158, "a very dark almost black glass amaro bottle, tall with rounded shoulders, black screw cap, a blank label panel"),
 'vermouth_velvet': (160, "a slender dark green vermouth bottle with a long neck, silver screw cap, a blank tall label panel"),
 'liqueur_delia': (148, "a squat round clear glass orange liqueur bottle with a short neck and cork stopper, a blank label panel"),
 'liqueur_kafa': (152, "a rounded dark coffee liqueur bottle with a distinctive short wide neck, black screw cap, a blank label panel"),
 'syrup_house': (156, "a tall narrow clear glass syrup bottle with a long neck and a black pour spout cap, a blank tall label panel"),
 'grenadine_rubis': (156, "a tall narrow clear glass syrup bottle filled with nothing, long neck, black pour spout cap, a blank tall label panel"),
 # ── fizz, juice, garnish ──
 'cola_marlow': (156, "a classic contour glass cola bottle with a waisted middle and fluted sides, metal crown cap, a blank label band around the middle"),
 'tonic_quinbury': (138, "a small clear glass tonic water bottle, gently waisted, metal crown cap, a blank label panel"),
 'soda_klara': (160, "a clear glass soda siphon with a straight body and a large chrome siphon head with a side lever, a blank label panel on the body"),
 'ginger_kicker': (144, "a stubby brown glass ginger beer bottle, metal crown cap, a blank label panel"),
 'energy_volt': (150, "a tall slim aluminium energy drink CAN, not a bottle, straight metal walls, tapered top with a pull tab, a blank label band around the middle"),
 'lemon_fresh': (140, "a small clear glass bottle of lemon juice, gently waisted, silver screw cap, a blank label panel"),
 'lime_fresh': (140, "a small green glass bottle of lime juice, gently waisted, silver screw cap, a blank label panel"),
 'orange_grove': (146, "a clear glass bottle of orange juice, gently waisted, silver screw cap, a blank label panel"),
 'cranberry_north': (146, "a clear glass bottle of deep red cranberry juice, gently waisted, silver screw cap, a blank label panel"),
 'pineapple_isla': (146, "a clear glass bottle of pineapple juice, gently waisted, silver screw cap, a blank label panel"),
 'mint_fresh': (128, "a short wide clear glass preserving jar of fresh mint leaves, metal screw lid, a blank label panel"),
 'olive_luca': (132, "a clear glass jar of green olives in brine, metal screw lid, a blank label panel"),
 # ── beer ──
 'beer_kestrel': (156, "a green glass beer longneck bottle, metal crown cap, a blank label panel and a blank neck band"),
 'beer_collier': (146, "a stubby dark brown stout beer bottle, metal crown cap, a blank label panel"),
 'beer_marigold': (156, "a brown glass beer longneck bottle, metal crown cap, a blank label panel and a blank neck band"),
}

OPEN_EDIT = ("remove the cap and stopper completely so the bottle is OPEN, "
             "show the empty open mouth of the neck from the side, keep everything "
             "else identical, the label stays completely blank with no text")


def load_state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save_state(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def result_text(tool, args, timeout=600):
    b = io.StringIO()
    import contextlib
    with contextlib.redirect_stdout(b):
        pixellab.call(tool, args, timeout=timeout)
    return b.getvalue()


def queue_all():
    """Pass 1: queue every closed bottle that has no id yet."""
    st = load_state()
    for cid, (size, prompt) in BOTTLES.items():
        if st.get(cid, {}).get('object_id'):
            continue
        txt = result_text('create_1_direction_object', {
            'description': f'{prompt}, {TAIL}', 'size': size, 'view': 'sidescroller'})
        oid = None
        for line in txt.splitlines():
            if line.strip().startswith('id:'):
                oid = line.split('id:')[1].strip()
        st.setdefault(cid, {})['object_id'] = oid
        st[cid]['size'] = size
        save_state(st)
        print(f'{cid:22} -> {oid}')
        time.sleep(1.0)
    return st


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'queue'
    if cmd == 'queue':
        queue_all()
