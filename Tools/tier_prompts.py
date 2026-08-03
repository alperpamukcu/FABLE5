# -*- coding: utf-8 -*-
"""
The upper tiers of every spirit, generated per BRAND (the author, 2026-08-03).

Astra, Boothby, Coral, Redline and Sonora own the shelf's approved art; the three
brands above each of them wear the same bottle, so a 48-dollar vodka looks exactly
like the house pour. Each of the fifteen gets its own vessel, drawn in the shelf's own
language and at its grain, and the silhouettes climb with the price the way they do on
a real back bar: a plain screw-capped bottle at the bottom, a decanter with a stopper
at the top.

Every one is asked for twice - shut, and with its closure off - because the pour stage
shows the open shot and cropping the top off a bottle is not an open bottle.
"""
import contextlib, io, json, os, sys, time
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
STATE = os.path.join(HERE, 'tiers_state.json')

TAIL = ("side view, clean pixel art, soft shading with subtle dithering, no "
        "anti-aliasing, thick dark outline all the way round, transparent background, "
        "standing upright and centred, the label is a COMPLETELY BLANK plain panel, "
        "absolutely no text, no letters, no words, no numbers, no logo, no writing")

OPEN = ("the closure has been taken OFF and is gone - no cap, no cork, no stopper, no "
        "foil - and the open mouth of the neck is drawn as a small oval rim seen from "
        "slightly above, everything else about the bottle identical, ")

# id -> what the bottle looks like. The shapes are the ones people know from a real
# shelf, ordered so the money shows: straight glass and a screw cap low down, weight
# and stoppers and decanters as the price climbs.
BOTTLES = {
    # ── vodka: 6 / 22 / 48 ──
    'vodka_vor': "a frosted white glass vodka bottle, tall with square shoulders and a "
                 "long slim neck, brushed silver screw cap, a blank pale label panel on "
                 "the body and a blank narrow band on the neck",
    'vodka_leonid': "a tall slender frosted glass vodka bottle, very long elegant neck, "
                    "tall matte black cap, a blank tall label panel down the body",
    'vodka_okhta': "an ornate cut-crystal vodka decanter, faceted rounded belly catching "
                   "the light, long narrow neck, heavy gold stopper, a blank oval label "
                   "panel on the belly",
    # ── gin: 6 / 24 / 52 ──
    'gin_juniper_crown': "a clear glass gin bottle with flat square shoulders and "
                         "straight sides, silver screw cap, a blank tall label panel on "
                         "the front",
    'gin_thornwood': "a dark green gin bottle shaped like a cocktail shaker, wide base "
                     "tapering all the way up to a narrow top, black wax seal over the "
                     "neck, a blank label band around the widest part",
    'gin_veilcrest': "a deep green cathedral-shaped gin decanter, tall arched body, long "
                     "neck, gold stopper, a blank arched label panel",
    # ── rum: 7 / 25 / 55 ──
    'rum_tidewater': "a squat wide amber rum bottle with a rope-wrapped neck, cork "
                     "stopper, a blank label panel across the belly",
    'rum_windward': "a dark brown aged rum bottle, round belly, gold stopper, a blank "
                    "oval label panel and a blank gold band on the neck",
    'rum_reina_del_mar': "a dark green ornate rum decanter with a long neck and a heavy "
                         "gold stopper, a blank shield-shaped label panel",
    # ── whiskey: 7 / 28 / 58 ──
    'bourbon_old_harrow': "a round amber whiskey bottle, sloped shoulders, cork stopper, "
                          "a blank rectangular label panel",
    'bourbon_ashfall': "a squat wide amber whiskey bottle with a short thick neck, thick "
                       "black wax seal dripping over the neck, a blank square label panel",
    'bourbon_hollow_oak': "an antique squat dark brown whiskey bottle of heavy glass, "
                          "deep red wax seal over the neck, a blank aged label panel and "
                          "a blank band on the neck",
    # ── tequila: 15 / 30 / 60 ──
    'tequila_alta_luna': "a tall clear tequila bottle with a long neck and a cork "
                         "stopper, a blank label panel and a blank band on the neck",
    'tequila_sol_viejo': "a short wide hand-blown amber tequila bottle with a rounded "
                         "belly, thick glass, round cork stopper, a blank round label "
                         "panel",
    'tequila_cielo_roto': "a hand-blown blue glass tequila bottle, rounded belly, short "
                          "wide neck, heavy pewter round stopper, a blank round label "
                          "panel",
}

SIZE = 160          # the approved bottles run 157-162 tall; this is their grain


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def queue():
    st = load()
    for bid, look in BOTTLES.items():
        for state in ('shut', 'open'):
            key = '%s_%s' % (bid, state)
            if st.get(key):
                print('%-28s already queued' % key)
                continue
            desc = ('%s%s, %s' % (OPEN if state == 'open' else '', look, TAIL))
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                pixellab.call('create_1_direction_object',
                              {'description': desc, 'size': SIZE, 'view': 'sidescroller'},
                              timeout=900)
            out = buf.getvalue()
            oid = None
            for line in out.splitlines():
                if line.strip().startswith('id:'):
                    oid = line.split('id:')[1].strip()
            st[key] = oid
            save(st)
            print('%-28s -> %s' % (key, oid))
            if oid is None:
                print(out[:300])
            time.sleep(1.0)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'queue':
        queue()
