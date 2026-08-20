# -*- coding: utf-8 -*-
"""The room, drawn in the CAST's language (2026-08-20).

Why this file exists, in one measurement: the cast and the room were generated from
two briefs that contradict each other on the single most visible rule. `scene_v3_gen`'s
CALM litany asks, in words, for

    "clean 1px outlines in each material's darkest tone"

and patron_prompts.HOUSE_RULES forbids exactly that, twice over:

    "absolutely no black outline and no dark keyline ... every edge is shown by a
     change of colour alone"

So the room was ASKED for the thing the people were FORBIDDEN. That is not a drifting
style, it is two briefs pulling opposite ways, and no amount of re-rolling the room
would have closed it. This file is the room asked for the cast's rule instead.

WHAT IS KEPT FROM THE SHIPPED ROOM: its shape. The author's third batch (club_room.png,
Nano Banana, 14 §11) composed the venue in a way the code has already been measured
against - the left-wall shopfront, the wall-floor line just past half height, the plum
accent on the right, the three recessed downlights - and 14 §5b's "as shipped" table is
the record of it. Moving those lines means re-measuring DiegeticStage by hand, which is
a separate job with its own risk. So the geometry is quoted from that table and only the
LANGUAGE changes.

WHAT IS NOT COPIED FROM scene_v3_gen: the palette quantize step. Measured 2026-08-20,
none of the shipped art is on the 55: club_room 0/37, counter 0/30, and all ten cast
faces 0%, on 32-57 colours each. The cast looks right anyway, so the 55 is not what is
holding it together - the brief and the gate are. Quantizing this plate would make it
match a palette nothing else matches, and stop it matching the people. That is a real
open question for 14 §3 and it is named here rather than silently decided: this file
ships the raw plate, like the cast does.

THE GATE IS THE POINT. The cast's lesson was not "write a better prompt", it was that
PixelLab treats `outline` as soft guidance and the same setting sprays 3% to 77% keyline,
so the only method is roll N, MEASURE all of them, keep the best and keep the losers'
numbers on the record (patron_trial_gen.roll). judge() below is that, with the four
background gates named in §4.

Commands:  queue | fetch | judge | report | status
State:     Tools/scene_cast_state.json     Raw: Tools/scene_cast_raw/
"""
import base64, io, json, os, re, sys, time
from PIL import Image
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STATE = os.path.join(HERE, 'scene_cast_state.json')
RAW = os.path.join(HERE, 'scene_cast_raw')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')

# The style plate that rides along as create_image_pro's style image. silverbob, because
# she is the cleanest roll the cast has by the two numbers that matter here: 1.7%
# near-black on 47 colours, where clubgirl is 57.6% and spanishsuit 50.9%. A style image
# carrying half a canvas of black trousers would teach this room the wrong lesson.
STYLE_PLATE = os.path.join(ROOT, 'Assets', 'Resources', 'Patron', 'silverbob',
                           'idle', 'idle_00.png')

# ROUND FIVE'S STYLE PLATE IS THE AUTHOR'S OWN RENDER of this same room - a warm cream
# ceiling, panelled walls with real material variation, a deep aubergine right wall, and
# the window already cut to alpha. Same SUBJECT as well as same language, which is more
# than a character sprite could ever offer: round one proved silverbob moved none of the
# gates, and the honest reading of that is not "style images do nothing" but "a person is
# the wrong reference for a room".
ROOM_PLATE = os.path.join(ROOT, 'Tools', 'AssetPipeline', 'sources', 'pixellab_user',
                          'room_ref.png')

# ── the cast's house rules, said for a ROOM ─────────────────────────────────
# Line for line out of patron_prompts.HOUSE_RULES. What is dropped is only what cannot
# apply to a room (adult proportions, full body, transparent background); what is added
# is 14 §5's shell law - the room is EMPTY and every stick of furniture is its own prop.
# Nothing here contradicts the cast. That is the whole design.
CAST_RULES = (
    # 1. NO LIGHT, NO REFLECTION. Verbatim, and it was already the house rule on both
    #    sides - 14 §7b says it for the room, HOUSE_RULES says it for people. The room
    #    is lit in URP by 2D lights; a baked highlight is a second sun.
    "flat local colour only, absolutely no baked lighting, no highlights, no specular "
    "gleam, no reflections, no glow, no cast shadow; "
    "even matte tone with simple hand-placed shade steps, "
    # 2. NO KEYLINE, IN WORDS. The clause the cast added 2026-08-20 after eight of ten
    #    faces came back between 3% and 39% keyline from one identical setting. Named
    #    the way the failures actually look - it is never only the silhouette that gets
    #    a line. "garment" becomes "surface or object"; nothing else changes.
    "absolutely no black outline and no dark keyline, not around any surface and not "
    "around any object, no ink lines, no drawn seams, no dark piping, no black edges; "
    "every edge is shown by a change of colour alone, "
    # 3. DETAIL. The cast's cure for a plate that reads flat beside its neighbours
    #    (shaved came back on 27 colours where the cast runs 37-57). For a room the
    #    fabric folds become the material's own grain.
    "detailed shading with visible material texture and clear surface steps, "
    # 4. 14 §5: the room master is EMPTY, everything standing in it is its own sprite.
    "completely empty room, no furniture, no bar, no counter, no stools, no tables, "
    "no bottles, no plants, no people, no text, no signage, no logo"
)

# ── the shape, quoted from 14 §5b's "as shipped" table ──────────────────────
# These are the numbers DiegeticStage was hand-measured against; they are the reason
# this is a re-skin and not a new room. Said as fractions of the frame rather than in
# pixels, because the model reads proportion and does not read coordinates.
#
# WRITTEN TO A BUDGET: create_image_pro caps `description` at 2000 characters and the
# first take came to 2225. Everything cut came out of the geometry, none of it out of
# CAST_RULES - the rules are the entire reason this file exists, and a room that comes
# back with the wrong sized window is a re-roll while a room that comes back inked is
# the same room we already have.
ROOM_DESC = (
    'pixel art, empty Miami bar room, mild one-point perspective from slightly above: '
    'flat back wall parallel to the picture plane, left wall, right wall and floor '
    'receding gently to one central vanishing point. '
    # ceiling: the shipped plate runs one flat grey band across the whole top fifth
    'Top fifth: a flat plain warm-grey concrete ceiling band with three small round '
    'recessed downlight discs evenly spaced across it, unlit, plain pale discs. '
    # back wall
    'Middle: back wall of large plain poured-concrete panels in warm pale grey with '
    'form-tie marks and faint irregular staining, panel joints drawn as a slightly '
    'darker grey line, never as a black line. '
    # the LEFT shopfront - the shipped room's signature, and the keyed hole
    'The LEFT wall recedes toward the viewer and is almost entirely one tall shopfront '
    'window from ceiling to floor, slim warm-grey frame, every pane FLAT solid pure '
    'green #00FF00 split by thin mullions, nothing behind the glass. '
    # the plum accent on the right, from the shipped plate
    'The RIGHT third of the back wall and the right wall beside it are a deep saturated '
    'plum-violet matte painted panel meeting the concrete at a clean vertical edge, one '
    'narrow pale pilaster at the far right. '
    # the floor and THE line the whole stage is measured against
    'Wall meets floor in a straight horizontal line just past HALF the image height. '
    'The floor fills the bottom, seen from above, receding to the vanishing point: warm '
    'reddish-brown wood parquet in a basketweave of short blocks alternating between two '
    'close brown tones, seams a darker brown and never black, sparse grain ticks. '
    'Medium detail. ' + CAST_RULES
)

# ── round two (2026-08-20), and what round one actually proved ──────────────
# ROUND ONE WORKED ON THE THING IT WAS BUILT FOR. All four plates came back at 0.0-0.1%
# ink, against club_room's 2.5% and counter's 6.3%. The cast's no-keyline clause
# transfers to a room whole - that question is answered and does not need asking again.
#
# It failed on two other numbers, and both are the same fault the cast already has a name
# for:
#   FLAT. 22-29 colours against a floor of 34 and a cast that runs 37-57. This is exactly
#   `shaved` ("came back on 27 colours where the cast runs 37-57, and read flat beside
#   them"), and the cast's cure was to ask for the detail in words rather than hope for
#   it. "visible material texture" was not enough; the concrete needs to be told it is
#   speckled and the parquet needs to be told its blocks differ from each other.
#   THE FLOOR LINE SAT TOO LOW. Measured 264-296 where 14 §5b's as-shipped table says
#   181 - a tall wall and a thin strip of floor, which is the one thing this re-skin may
#   not change, because DiegeticStage's constants were hand-measured against that line
#   and the counter covers everything below y 232. "just past HALF the image height" was
#   read as a suggestion, so round two says it as a fraction and repeats it.
# Both are now GATED (floor_line below) rather than left to the eye, which is the only
# lesson this pipeline has ever really taught.
# Written to the same 2000-character budget, so every clause added below is paid for by
# one taken out. What went: "Medium detail." (it argues with the new detail clause) and
# the long-hand perspective sentence, which round one proved the model already reads -
# all four plates came back in the right projection from the short form.
ROOM_DESC2 = (ROOM_DESC
    .replace(
        'mild one-point perspective from slightly above: '
        'flat back wall parallel to the picture plane, left wall, right wall and floor '
        'receding gently to one central vanishing point. ',
        'mild one-point perspective from slightly above, flat back wall, left and right '
        'walls and floor receding to one central vanishing point. ')
    .replace('Medium detail. ', '')
    # Two more clauses sold to buy the detail words, both of them decoration: the
    # downlights do not need to be described twice, and one pilaster at the far right
    # is a thing the eye will not miss.
    .replace('unlit, plain pale discs. ', 'unlit. ')
    .replace(' meeting the concrete at a clean vertical edge, one '
             'narrow pale pilaster at the far right. ',
             ' meeting the concrete at a clean vertical edge. ')
    .replace(
        'Wall meets floor in a straight horizontal line just past HALF the image height. '
        'The floor fills the bottom, seen from above, receding to the vanishing point:',
        'The back wall is SHORT and the floor is LARGE: wall meets floor in one straight '
        'horizontal line HALFWAY down the image, and the floor fills the whole bottom '
        'half of the picture, seen from above:')
    .replace(
        'with form-tie marks and faint irregular staining',
        'heavily speckled and mottled in many close tones, form-tie marks, water '
        'staining, patches of discolouration')
    .replace(
        'alternating between two close brown tones',
        'every block a slightly different brown, four or five close wood tones')
    .replace(
        'detailed shading with visible material texture and clear surface steps',
        'richly detailed shading, many subtle tone steps within every material, visible '
        'grain speckle and wear'))

# ── round three (2026-08-20): ISOLATE THE FAULT ─────────────────────────────
# Round two changed four things at once and came back WORSE on the one number round one
# had won outright: ink went from 0.0-0.1% to 2.4-7.5%, while colours (24-31) and the
# floor line (244-263) barely moved. So the detail words bought nothing and cost the
# keyline - which is the cast's oldest lesson wearing new clothes. There, a blazer, a
# button placket, a waistcoat and a leopard print each dragged ink in with them, and the
# cure was never to ask harder but to find WHICH WORDS carried the lines and take those
# words out. "richly detailed shading ... visible grain speckle and wear" is this room's
# waistcoat: asking a model for rendered detail asks it to draw, and drawing means lines.
#
# So round three keeps round one's rule text EXACTLY as it stood when it measured 0.0%,
# and changes only the two clauses that describe the room rather than the rendering: the
# horizon, and how many tones each material is made of. If ink stays at zero and colours
# rise, the fault is proven to be in the shading words. If ink rises anyway, it is the
# material words, and the next roll takes those out instead. One variable, one answer.
ROOM_DESC3 = (ROOM_DESC
    .replace(
        'mild one-point perspective from slightly above: '
        'flat back wall parallel to the picture plane, left wall, right wall and floor '
        'receding gently to one central vanishing point. ',
        'mild one-point perspective from well above, flat back wall, left and right '
        'walls and floor receding to one central vanishing point. ')
    .replace('Medium detail. ', '')
    .replace('Top fifth: a flat plain warm-grey concrete ceiling band with three small '
             'round recessed downlight discs evenly spaced across it, unlit, plain pale '
             'discs. ',
             'A thin warm-grey concrete ceiling band across the top with three small '
             'round recessed downlights, unlit. ')
    .replace(' meeting the concrete at a clean vertical edge, one '
             'narrow pale pilaster at the far right. ',
             ' meeting the concrete at a clean vertical edge. ')
    # THE HORIZON, said three ways. Eight plates in a row put it at 244-288 where the
    # shipped room has it at 206, so "just past HALF" and "HALFWAY down" have both been
    # tried and both been read as a suggestion. This says it as a proportion of the wall
    # AND as a proportion of the floor AND as a camera height, because the one thing the
    # model reliably obeys across all eight is how high the camera is.
    .replace(
        'Wall meets floor in a straight horizontal line just past HALF the image height. '
        'The floor fills the bottom, seen from above, receding to the vanishing point:',
        'A HIGH CAMERA looking down: the back wall is SHORT, only the middle third of '
        'the picture, and meets the floor in one straight horizontal line ABOVE the '
        'middle of the image. The floor is the largest thing here and fills the whole '
        'bottom half, seen steeply from above:')
    # Tones, WITHOUT asking for rendering. The concrete and the wood are told they are
    # made of many colours; nothing here asks for shading, detail, grain or wear.
    .replace(
        'with form-tie marks and faint irregular staining',
        'mottled and blotchy in many close greys and beiges, form-tie marks, patches of '
        'discolouration')
    .replace(
        'alternating between two close brown tones',
        'every block a slightly different brown, five or six close wood tones'))

# ── round four (2026-08-20): the parquet is the waistcoat ───────────────────
# Round three answered its question and asked a better one. The horizon clause WORKED -
# floor y 191-207 on all four against a band of 190-222 and a shipped plate at 206, after
# eight straight plates at 244-288. Say it as a camera height and the model obeys; say it
# as "halfway down" and it does not.
#
# But ink went to 8.6-13.0%, and LOOKING at the plate says why in one glance: the walls
# are clean and the FLOOR is a field of dark seams. Two things moved at once again, so
# the honest reading is not "material words cause ink" - it is that a high camera makes
# the floor the biggest thing in the picture, and this floor is drawn with a black line
# between every block. The parquet is doing exactly what silverbob's blazer and
# spanishsuit's waistcoat did: it is a subject the model knows how to draw WITH LINES.
#
# The cure is the cast's, not a new one. It never worked to ask harder; what worked was
# to name the offending thing and take its lines out of the words. ROOM_DESC has said
# "seams a darker brown and never black" since round one and the model has ignored it in
# every roll, because "seam" is itself a line word. So the seam goes: the blocks are
# separated by a change of wood tone, which is CAST_RULES clause 2 applied to the one
# surface that has been breaking it.
# Paid for by the concrete's word list, which round three showed is not where the ink
# comes from - the walls in every round-three plate are clean.
ROOM_DESC4 = (ROOM_DESC3
    .replace('mottled and blotchy in many close greys and beiges, form-tie marks, '
             'patches of discolouration',
             'mottled in many close greys and beiges, form-tie marks')
    .replace('seams a darker brown and never black, sparse grain ticks',
             'no seam lines between the blocks and no dark gaps, each block set off from '
             'its neighbours by its wood tone alone'))

# ROUND FOUR'S ANSWER, and where this file stands (2026-08-20). Taking the word "seam"
# out of the floor did it: cast_room4_b came back at 0.0% ink with the horizon at y 209
# against club_room's 206, and passes every gate except one. Fifteen rolls to get both
# numbers right at once, and the two that mattered were both WORD choices, neither of
# them a setting - "a high camera looking down" for the horizon, and "no seam lines,
# each block set off by its wood tone alone" for the ink. That is the third time this
# project has found the same thing: the model draws what the words invite, and the cure
# for an unwanted line is to stop naming the thing that has one.
#
# THE ONE GATE IT STILL FAILS is colours, 26 against a floor of 34 - and that floor is
# BORROWED, not measured. It is the cast's number, set on ten human figures; the only
# room ever judged against it is club_room at 38. Whether a wall of poured concrete
# should carry as many tones as a person in a print blouse is an author's question, not
# a measurement, so it is left failing rather than quietly relaxed to fit the plate that
# happens to be in hand. Whoever answers it should either lower the floor with a reason
# written beside it, or roll again for more tones - and if it is the latter, note that
# round two and three both bought tones with ink and neither was worth it.
#
# NOT INSTALLED. The plate is staged, not copied over club_room.png: 14 §5b's own warning
# is that regenerating a background means RE-MEASURING DiegeticStage's constants by hand,
# and the counter, the stools and every fixture anchor sit on those numbers.

# ROUND FIVE'S RESULT, measured by hand because the gate could not see it (2026-08-20).
# Wall ends / dado / floor starts, centre column:
#     room_ref      181  --       182   the angle to match
#     cast_room5_b  170  174-214  215   +33
#     cast_room5_a  210  217-260  262   +80
# Both miss, and THE FAULT IS IN THIS FILE'S OWN PROMPT, not in the model. The dado band
# - "along its bottom a band of deeper plum" - is a clause added for the wallpaper, and it
# eats forty pixels of wall and pushes the floor line down by exactly that much. The
# reference has no dado: its wall runs straight into the floor at 182. Take the clause out
# and 5_b's wall ends at 170, a dozen pixels from the target.
# So the next roll is a one-clause deletion, not a re-roll - noted here rather than spent,
# because the author asked for two and two is what was spent.
#
# ── round five (2026-08-20): the author's own plate is the reference ────────
# The author put a NEW render of this room on the table - the same geometry as
# club_room.png but a different hand: a warm cream ceiling instead of the flat grey,
# metal-panelled walls with real material variation, a deep aubergine right wall with
# panel texture in it, and the window panes already cut to alpha. That plate, not
# silverbob, is now the style image: it is the same SUBJECT as well as the same
# language, which is the strongest lever create_image_pro has and the one worth
# spending the last rolls on.
#
# TWO ROLLS ONLY, at the author's instruction ("cok fazla pixellab kredisi kullaniliyor").
# Fifteen went into finding the two words that mattered; these two are the cash-in, so
# they change nothing that has already been proven. The horizon clause stays exactly as
# round three wrote it and round four confirmed, and the no-seam floor clause stays as
# round four wrote it.
#
# THE FLOOR IS NO LONGER PARQUET, on the author's call ("zemin parke olmak zorunda
# degil") - and it lands on the one surface this pipeline kept failing. Parquet is a grid
# of short blocks, which is to say a grid of edges, and every roll that drew it drew dark
# lines between them; round four only got to 0.0% by forbidding the seams a parquet
# floor is MADE of. Long planks running away from the viewer have a fraction of the
# boundaries, so the clause is no longer fighting the subject.
ROOM_DESC5 = (
    # Geometry, unchanged since round one.
    'pixel art, empty Miami bar room, mild one-point perspective from well above, flat '
    'back wall, side walls and floor receding to one central vanishing point. '
    # The ceiling the author's own render has, replacing club_room's flat grey band.
    'A warm cream ceiling band with three small recessed downlights, unlit. '
    # THE WALL IS PAPERED, NOT POURED (the author: "beton cok goz yoran bir arkaplan
    # oluyor ... duvar kagidi vs. olabilir"). A wallpaper is also the LOW-INK choice, and
    # that is not a coincidence: concrete panels are a grid of joints, and this pipeline
    # has now watched the model draw a black line down every joint it was given. A
    # pattern is a field of colour with no edges in it at all. Deco, because 14 §5a's
    # room is art-deco Miami and its own earlier takes speak that vocabulary - but the
    # chair-rail, the fluting and the panel joints those takes asked for are all LINE
    # words, so the dado arrives here as a change of colour instead.
    'The back wall is soft art-deco WALLPAPER: a warm dusty-rose and muted plum field '
    'with a quiet low-contrast repeating motif of slim vertical fans, reading as one '
    'soft colour from a distance; along its bottom a band of deeper plum, a change of '
    'colour and not a moulding or rail. '
    # The left shopfront: the shipped room's signature and the keyed hole.
    'The LEFT wall recedes toward the viewer and is almost entirely one tall shopfront '
    'window from ceiling to floor, slim warm-grey frame, every pane FLAT solid pure '
    'green #00FF00 split by thin mullions, nothing behind the glass. '
    # The aubergine right wall, taken from the author's render rather than club_room's
    # flatter violet.
    'The RIGHT third of the back wall and the wall beside it are deep aubergine matte '
    'panel, meeting the wallpaper at a clean vertical edge. '
    # THE HORIZON, in round three's exact words - the clause that finally moved it, after
    # eight plates ignored "just past HALF" and "HALFWAY down".
    # THE ANGLE, MEASURED OFF THE REFERENCE AND MATCHED EXACTLY (the author: "aci tam
    # olarak ayni olmali"). room_ref at 640x360 divides into three clean bands - cream
    # ceiling 0-60, wall 60-182, floor 182-360 - which is a sixth, a third and a half.
    # Said as fractions because rounds one and two proved the model reads proportion and
    # ignores "halfway down", and kept inside round three's HIGH CAMERA framing because
    # that is the clause that finally moved the horizon at all.
    'A HIGH CAMERA looking down: the ceiling band takes the top SIXTH, the back wall is '
    'SHORT and takes only the next THIRD, and wall meets floor in one straight '
    'horizontal line at the exact middle of the image. The floor fills the whole bottom '
    'HALF, seen steeply from above: '
    # THE FLOOR IS PLANKS, NOT PARQUET (the author: "zemin parke olmak zorunda degil"),
    # and it lands on the surface this pipeline kept failing. Parquet is a grid of short
    # blocks - a grid of edges - and every roll that drew it drew dark lines between
    # them; round four reached 0.0% only by forbidding the seams parquet is MADE of.
    # Long boards have a fraction of the boundaries, so the clause stops fighting its
    # own subject. Round four's no-seam wording is kept verbatim.
    'a warm reddish-brown wood plank floor, long boards running away toward the '
    'vanishing point, five or six close wood tones board to board, no dark seam lines '
    'and no black gaps anywhere, each board set off from its neighbours by its wood '
    'tone alone. ' + CAST_RULES)

# ROUND SIX'S RESULT (2026-08-20). Saying the geometry as WHERE THE WALLS ARE did what
# four rounds of horizon wording could not:
#     floor line   y183, against the reference's y182 - one pixel
#     window       18 x 90, longest edge 90, under the author's 160 cap
#     ink          1.6%, below club_room's own 2.5%
#     step 25, span 156, black 28.5% - all inside their bands
# It fails ONE gate, colours at 26 against a floor of 34, and that floor is still the
# borrowed one flagged two rounds ago: it is the cast's number, set on ten human figures,
# and the only room ever measured against it is club_room at 38. Still an author's
# question, still not quietly relaxed to fit the plate in hand.
#
# WHAT THE WIDE LENS COST, said plainly because no gate sees it: the side walls are now
# such narrow slivers that the room reads close to a flat elevation. The reference keeps a
# visible aubergine plane receding on the right, and that plane is a good part of where
# its depth comes from. If the next roll wants both, it has to ask for the floor to reach
# the bottom corners AND for the right wall to stay a real receding surface - this round
# bought the first with the second.
#
# ── round six (2026-08-20): the FOV was the fault, not the horizon ──────────
# The author looked at round five and named something none of the gates measure: "oda cok
# kucuk gozukuyor". In the reference the floor spreads corner to corner along the whole
# bottom edge and the side walls are slivers; in every plate this file has generated, the
# side walls run all the way down INTO the bottom corners and the floor is a narrow
# trapezoid between them. Same horizon, different lens - the plates are drawn through a
# long lens and the reference through a wide one, and a wide room seen narrow reads small
# no matter where its horizon sits. So the geometry is now said as WHERE THE WALLS ARE,
# not only as where the floor starts.
#
# THE WINDOW IS CAPPED AT 160 PX on its longest side, the author's one hard rule this
# round. Everything before it drew the left wall as a full-height shopfront, which is 176
# px tall at 14 §5b's own as-shipped numbers - over the cap and, at that size, most of
# the left half of the room.
#
# COLOUR COMES FROM THE BAR ITSELF now, not from concrete. The author pointed at
# backbar_pixellab and counter_pixellab_wood and said use those tones, so they were
# sampled rather than guessed: the back bar runs dusty mauve #9c8e8f over plum #634261,
# #4a2d4a and #392139 with pale cream #c5b5af / #b2a29f panels, and the counter runs
# mahogany #351014, #5e2614, #672e18, #7e3f1a with amber #cc8a42. A room built from its
# own furniture's palette is the cheapest way to make the three plates look related.
ROOM_DESC6 = (
    'pixel art, empty Miami vice cocktail bar room, WIDE ANGLE one-point perspective '
    'from high above, one central vanishing point. '
    # The lens, said as wall placement - this is the round's whole point.
    'The room is LARGE and OPEN: the side walls are only narrow strips at the extreme '
    'left and right edges, and the floor spreads across the FULL WIDTH of the bottom '
    'edge, corner to corner. '
    'Top sixth: a pale cream #c5b5af ceiling band with three small recessed downlights, '
    'unlit. '
    'The back wall takes only the next third and meets the floor in one dead straight '
    'horizontal line at the EXACT MIDDLE of the image. '
    # The wall in the back bar's own materials. The dado that cost round five forty
    # pixels of wall is gone; what replaces it is a change of colour with no band.
    'Back wall: deep plum #634261 panelling with large pale cream #b2a29f pressed-tin '
    'panel insets in a quiet deco pattern, shading to darker plum #4a2d4a toward the floor as '
    'a gradual change of colour, with no rail and no moulding. '
    # The window, under the cap.
    'On the LEFT wall one SMALL window, no larger than 160 pixels on its longest side, '
    'slim cream frame, panes FLAT solid pure green #00FF00, nothing behind the glass. '
    'The far RIGHT wall strip is deep aubergine #392139. '
    # The floor in the counter's mahogany, with round four's no-seam wording kept whole.
    'The floor fills the whole bottom half seen steeply from above: dark mahogany '
    '#5e2614 boards running away to the vanishing point, tones #672e18, #7e3f1a and '
    '#351014 board to board, sparse warm amber #cc8a42 glints in the grain, no dark seam '
    'lines and no black gaps, each board set off from its neighbours by its wood tone '
    'alone. ' + CAST_RULES)

# ── round seven (2026-08-20): the author's own colour call ──────────────────
# Round six settled the lens and the author kept it ("aci guzel"), so every geometry
# clause below is round six's, word for word - the wide-angle framing, the wall placement
# that produced it, and the exact-middle horizon. Nothing that works is being retyped.
#
# What changes is the author's, stated as values rather than moods, which is the easiest
# kind of note to honour:
#   - the walls are #EAD1C2, and the pressed-tin deco panelling round six drew is gone
#     ("duvarlari begenmedim") - a painted wall, not a panelled one;
#   - the RIGHT wall and the right edge of the back wall are ONE continuous #4B0082
#     surface, full height. Said as one surface rather than as two the same colour,
#     because "birlesik" is the whole point: a seam there would read as two walls;
#   - the window frames are GREY;
#   - the glass goes FULL HEIGHT ("boydan camlar"). Round six's 18x90 was legal under the
#     160 cap and far too timid.
#
# THE CAP AND "FULL HEIGHT" NEARLY COLLIDE, so it is worth writing down which wins. In
# this composition the wall runs from the ceiling band to the horizon - about 150 px - so
# a floor-to-ceiling window fits under 160 at the far end. On a receding LEFT wall the
# near end is taller, and that is where the cap can be broken. Both are asked for, the
# cap is repeated in the prompt, and the result is MEASURED afterwards rather than
# assumed - if it comes back over, that is a real conflict for the author to settle, not
# something to quietly crop.
ROOM_DESC7 = (
    # ── geometry: round six verbatim, because it hit y183 against a target of y182 ──
    'pixel art, empty Miami vice cocktail bar room, WIDE ANGLE one-point perspective '
    'from high above, one central vanishing point. '
    'The room is LARGE and OPEN: the side walls are only narrow strips at the extreme '
    'left and right edges, and the floor spreads across the FULL WIDTH of the bottom '
    'edge, corner to corner. '
    'Top sixth: a pale cream ceiling band with three small recessed downlights, unlit. '
    'The back wall takes only the next third and meets the floor in one dead straight '
    'horizontal line at the EXACT MIDDLE of the image. '
    # ── the author's colours ──
    'Back wall: smooth matte painted plaster in warm cream #EAD1C2 with quiet tonal '
    'variation across it, no panels, no insets and no mouldings. '
    'The RIGHT wall and the right edge of the back wall are ONE single continuous '
    'surface of deep indigo #4B0082, unbroken from ceiling to floor, meeting the cream '
    'at one clean vertical edge. '
    'The LEFT wall is filled with TALL FLOOR-TO-CEILING WINDOWS running its entire '
    'height, slim GREY frames and thin grey mullions, every pane FLAT solid pure green '
    '#00FF00, nothing behind the glass, no window taller than 160 pixels. '
    # ── the floor: round four's no-seam wording, round six's mahogany ──
    'The floor fills the whole bottom half seen steeply from above: dark mahogany '
    '#5e2614 boards running away to the vanishing point, tones #672e18, #7e3f1a and '
    '#351014 board to board, sparse warm amber #cc8a42 glints, no dark seam lines and '
    'no black gaps, each board set off from its neighbours by its wood tone alone. '
    + CAST_RULES)

# The style image is the strongest lever this file has and also the one most likely to
# drag a person's colours onto a wall, so it is tested rather than assumed - the same
# reason the cast rolls a batch and measures it instead of arguing about one picture.
# Round one's answer: it made no difference the gates could see (styled 0.0/0.1% ink and
# 29/25 colours, unstyled 0.0/0.0% and 22/28), so round two spends all four seeds on the
# revised words instead, and the style plate rides on half of them purely to keep the
# comparison alive.
SEEDS = [
    ('cast_room_a', 43001, True, 'ROOM_DESC'),
    ('cast_room_b', 43002, True, 'ROOM_DESC'),
    ('cast_room_c', 43003, False, 'ROOM_DESC'),
    ('cast_room_d', 43004, False, 'ROOM_DESC'),
    ('cast_room2_a', 43101, True, 'ROOM_DESC2'),
    ('cast_room2_b', 43102, True, 'ROOM_DESC2'),
    ('cast_room2_c', 43103, False, 'ROOM_DESC2'),
    ('cast_room2_d', 43104, False, 'ROOM_DESC2'),
    ('cast_room3_a', 43201, True, 'ROOM_DESC3'),
    ('cast_room3_b', 43202, True, 'ROOM_DESC3'),
    ('cast_room3_c', 43203, False, 'ROOM_DESC3'),
    ('cast_room3_d', 43204, False, 'ROOM_DESC3'),
    ('cast_room4_a', 43301, True, 'ROOM_DESC4'),
    ('cast_room4_b', 43302, True, 'ROOM_DESC4'),
    ('cast_room4_c', 43303, False, 'ROOM_DESC4'),
    ('cast_room4_d', 43304, False, 'ROOM_DESC4'),
    # TWO ROLLS, and only two, at the author's instruction. Both carry the author's own
    # render as the style plate; the only thing that differs between them is the seed.
    ('cast_room5_a', 43401, ROOM_PLATE, 'ROOM_DESC5'),
    ('cast_room5_b', 43402, ROOM_PLATE, 'ROOM_DESC5'),
    # ONE roll, at the author's instruction.
    ('cast_room6_a', 43501, ROOM_PLATE, 'ROOM_DESC6'),
    ('cast_room7_a', 43601, ROOM_PLATE, 'ROOM_DESC7'),
]


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def log(rec):
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec) + '\n')


def texts(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                out.append(c['text'])
    return '\n'.join(out)


def queue(only=None):
    st = load()
    for key, seed, styled, which in SEEDS:
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            print('%-14s already queued %s' % (key, st[key]['id']))
            continue
        desc = globals()[which]
        # create_image_pro caps `description` at 2000 and answers an over-long one with a
        # pydantic error that reads like a server fault. Caught here instead, because the
        # queue loop otherwise burns through every seed printing the same wall of text.
        if len(desc) > 2000:
            raise SystemExit('%s is %d characters, cap is 2000 - trim the geometry, '
                             'never CAST_RULES' % (which, len(desc)))
        args = dict(width=640, height=360, no_background=False,
                    description=desc, seed=seed)
        # `styled` is False, True (the cast plate) or a path - round five needs its own
        # reference and one global was no longer enough.
        plate = None if not styled else (STYLE_PLATE if styled is True else styled)
        if plate:
            if not os.path.exists(plate):
                raise SystemExit('style plate missing: %s' % plate)
            args['style_image_base64'] = base64.b64encode(
                io.open(plate, 'rb').read()).decode()
        msgs = pixellab.call('create_image_pro', args, timeout=900)
        body = texts(msgs)
        m = UUID.search(body)
        st[key] = {'id': m.group(0) if m else None, 'seed': seed,
                   'styled': styled, 'desc': which}
        save(st)
        log({'asset': key, 'tool': 'create_image_pro', 'seed': seed,
             'prompt': desc, 'job': st[key]['id'],
             'style': STYLE_PLATE if styled else None,
             'event': 'queued' if m else 'queue-failed', 'raw': body[:300]})
        print('%-14s -> %s' % (key, st[key]['id'] or body[:160].replace('\n', ' ')))
        time.sleep(0.6)


def images(msgs):
    """The finished plate comes back as base64 image CONTENT, not as a download URL.

    Paid for once, 2026-08-20: the first version of this called get_image with
    `image_id` (the argument is `job_id`) and then regexed the response for a URL. Both
    wrong, and they failed together in a way that looked like a server problem - the
    validation error's own "for further information visit https://errors.pydantic.dev"
    was the only URL in the body, so the regex found it, fetched it, and reported HTTP
    403 eight times in a row. scene_v3_gen has had this right since 2026-08-17.
    """
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGB'))
    return out


def fetch():
    if not os.path.isdir(RAW):
        os.makedirs(RAW)
    st = load()
    for _ in range(40):
        pending = [k for k, _, _, _ in SEEDS
                   if (st.get(k) or {}).get('id')
                   and not os.path.exists(os.path.join(RAW, k + '.png'))]
        if not pending:
            break
        moved = False
        for key in pending:
            rec = st[key]
            msgs = pixellab.call('get_image', {'job_id': rec['id']}, timeout=300)
            ims, body = images(msgs), texts(msgs)
            if ims:
                path = os.path.join(RAW, key + '.png')
                ims[0].save(path)
                rec['file'] = path
                save(st)
                print('fetched %-14s -> %s' % (key, path))
                log({'asset': key, 'event': 'fetched'})
                moved = True
            elif 'failed' in body.lower():
                print('FAILED %-14s %s' % (key, body[:180].replace('\n', ' ')))
                log({'asset': key, 'event': 'failed', 'raw': body[:300]})
                rec['id'] = None
                save(st)
                moved = True
        if not moved:
            print(' %d pending...' % len(pending))
            time.sleep(25)


# ── §4 · the four background gates ──────────────────────────────────────────
# Translated one for one from patron_trial_gen.judge, which is the only part of the cast
# pipeline that actually made a rule stick. What changes is only HOW each is measured: a
# room has no alpha silhouette, so the keyline is looked for INSIDE the picture.

def ink(im):
    """What share of the picture's colour boundaries is drawn as a dark LINE, in percent.

    Same definition as patron_trial_gen.edge_darkness and for the same reason: a keyline
    is not "a dark pixel", it is A RIM DARKER THAN WHAT IT SEPARATES. Here there is no
    silhouette, so a boundary pixel is one whose left and right neighbours differ, and it
    counts as ink when it is near-black AND both sides three steps out are markedly
    lighter. A dark floor scores nothing; a seam drawn in black scores everywhere.

    This is the number counter.png fails on: its cabinet edges are #060409 against a
    #666164 face, which is a black keyline whether or not anyone called it one.
    """
    px = im.load()
    w, h = im.size
    edge = inked = 0
    for y in range(3, h - 3):
        for x in range(3, w - 3):
            c = px[x, y]
            l, r = px[x - 1, y], px[x + 1, y]
            if c == l and c == r:
                continue
            edge += 1
            far_l, far_r = px[x - 3, y], px[x + 3, y]
            if (max(c[:3]) < 80
                    and max(far_l[:3]) - max(c[:3]) >= 45
                    and max(far_r[:3]) - max(c[:3]) >= 45):
                inked += 1
    return 100.0 * inked / max(1, edge)


def steps(im, cols=None):
    """The median VISIBLE step between neighbouring shading bands, in RGB units.

    The gate the counter taught us and the cast never needed. counter.png spends twelve
    bands on one vertical face at 3-6 RGB apart - an airbrush gradient posterised, which
    is invisible as shading and reads as a smooth render beside hand-drawn neighbours.
    club_room.png runs 10-75. A ramp step you cannot see is not a ramp step.
    """
    px = im.load()
    w, h = im.size
    ds = []
    for x in (cols or range(20, w - 20, 7)):
        prev = None
        for y in range(0, h):
            c = px[x, y][:3]
            if prev is not None and c != prev:
                ds.append(max(abs(c[i] - prev[i]) for i in range(3)))
            prev = c
    ds.sort()
    return ds[len(ds) // 2] if ds else 0


def floor_line(im):
    """The y where the back wall meets the floor, at the centre of the frame.

    A gate rather than a note because round one missed it by 80-115 px on all four
    plates and every one of them still looked fine on its own. It is only wrong against
    the GAME: DiegeticStage's constants were hand-measured off club_room.png (14 §5b as
    shipped, y = 181) and the counter strip covers everything below y 232, so a plate
    whose floor starts at 290 hands the stage a 0 px band of floor to stand a prop on.

    AND IT IS NOT TO BE TRUSTED ON A PLATE WITH A DARK DADO (2026-08-20, third failure).
    Round five put a band of deeper plum along the bottom of the wall, and the classifier
    below assigns that band to the FLOOR because it is dark - so cast_room5_b reported
    y 170 when the wood actually starts at 215. A wood-specific test was tried next and
    failed the other way, reading the reference's own dark floor as not-wood and
    answering 346. Three heuristics, three confident wrong answers.

    Where that leaves it: the two-class split below is right on plates whose wall runs
    straight to the floor (room_ref 182, club_room 206, both confirmed by hand) and wrong
    on plates with a dado. It is kept because it is right on the reference, and because
    the honest alternative - the cast's own precedent with the head ratio - is to read the
    number and not obey it blindly. When a plate has a band along the bottom of its wall,
    walk the centre column by hand; it takes one print and it is certain.

    NOT the biggest vertical colour jump. That was the first version and it refused
    club_room.png itself - the plate this gate exists to protect - by reporting y 117:
    a concrete panel joint on the back wall is a bigger jump than the wall-floor seam,
    and "biggest edge" has no way to know which edge means something. A gate that fails
    its own reference is measuring the wrong thing, which is the lesson the cast learned
    with the head ratio (patron_trial_gen: two versions tried, both measured HAIR).

    The second version keyed on WARMTH - wood is warm, concrete is not - and it failed
    the other way round: the candidates' concrete is a warm beige, so three of four
    plates reported their ceiling. Two heuristics, two different wrong answers, both of
    them confident.

    What works is asking the picture for its own two materials instead of naming either.
    The bottom rows ARE the floor and a band a third of the way down IS the wall,
    whatever colours those happen to be; every row between is assigned to whichever of
    the two centroids it is nearer, and the seam is where the answer flips. It reads the
    shipped plate at y 206, against 205 from a hand-walked column - close enough to trust
    on art it has never seen.
    """
    px = im.load()
    w, h = im.size
    x0, x1 = w // 3, 2 * w // 3
    n = float(x1 - x0)

    def row(y):
        return tuple(sum(px[x, y][i] for x in range(x0, x1)) / n for i in range(3))

    def centroid(y0, y1):
        rows = [row(y) for y in range(y0, y1)]
        return tuple(sum(r[i] for r in rows) / len(rows) for i in range(3))

    floor_c = centroid(h - 16, h)
    wall_c = centroid(int(h * 0.28), int(h * 0.34))
    # A room whose floor and wall are the same colour has no seam to find, and saying so
    # beats returning a number somebody would trust.
    if sum((floor_c[i] - wall_c[i]) ** 2 for i in range(3)) ** 0.5 < 20:
        return -1
    for y in range(h - 16, int(h * 0.34), -1):
        m = row(y)
        if (sum((m[i] - wall_c[i]) ** 2 for i in range(3))
                < sum((m[i] - floor_c[i]) ** 2 for i in range(3))):
            return y + 1
    return int(h * 0.34)


def stats(im):
    px = im.load()
    w, h = im.size
    seen = {}
    dark = 0
    vals = []
    for y in range(h):
        for x in range(w):
            c = px[x, y][:3]
            seen[c] = seen.get(c, 0) + 1
            v = max(c)
            vals.append(v)
            if v < 80:
                dark += 1
    vals.sort()
    n = len(vals)
    return {'ink': ink(im), 'colours': len(seen), 'step': steps(im),
            'floor': floor_line(im),
            'black': 100.0 * dark / n,
            'v05': vals[n // 20], 'v95': vals[n * 19 // 20],
            'span': vals[n * 19 // 20] - vals[n // 20]}


# Bands, calibrated the way the cast's were - on art that already exists, not guessed.
# I guessed first and the guesses were wrong in both directions, so the measured numbers
# are written here instead:
#
#   club_room.png  (the author drew it and it is in the game)  ink 2.5%  colours 38
#                  step 29  span 167 (V 0-167)  black  9.2%
#   counter.png    (the plate that does not match)             ink 6.3%  colours 31
#                  step 10  span 115 (V 9-124)  black 52.9%
#
# HONEST ABOUT n=2: two plates, one approved and one rejected, is a first calibration and
# not a law. The cast's own bands were set on ten faces and still had to be recalibrated
# once (patron_trial_gen: "the first guess was badly wrong"). Expect these to move.
#
# Note what each gate does and does not catch. `step` as a whole-image median is WEAK on
# the counter - it reads 10 because the crisp panel edges outvote the smooth face, while
# the face itself measured 3-6 when its own column was walked. So `step` is a coarse net;
# `ink` and `colours` are the two that actually refuse the counter, and `black` is what
# refuses a plate that reads as a hole rather than as a lit room.
INK_MAX = 4.0        # club_room 2.5, counter 6.3 - the line sits between them
COLOURS_MIN = 34     # the cast's own floor; club_room 38, counter 31
STEP_MIN = 16        # club_room 29, counter 10
SPAN_MIN = 130       # club_room 167, counter 115: a room needs a lit end and a dark end
BLACK_MAX = 30.0     # club_room 9.2, counter 52.9 - this is the "black bar" number
# THE FLOOR LINE, and the discrepancy is now RESOLVED (2026-08-20). This comment used to
# say that 14 §5b claims y 181 while club_room.png measures 206, and that nobody should
# guess which was right. The author's own newer render settles it: room_ref measures 182.
# So §5b's table was written against THIS plate, not against club_room.png - the 181 was
# never wrong, it was describing a different picture. club_room.png at 206 is the older
# render, and DiegeticStage's constants sit between the two.
#
# The band is therefore set around the REFERENCE, because that is the angle the author
# has asked new plates to match exactly ("aci tam olarak ayni olmali"), +-16 as before.
# Note what this means for the earlier rounds: cast_room4_b at y 209 passed the old band
# and misses the new one by 11 px. It was measured against the wrong picture.
FLOOR_BAND = (166, 198)


def judge(name, im):
    s = stats(im)
    ok = (s['ink'] <= INK_MAX and s['colours'] >= COLOURS_MIN
          and s['step'] >= STEP_MIN and s['span'] >= SPAN_MIN
          and s['black'] <= BLACK_MAX
          and FLOOR_BAND[0] <= s['floor'] <= FLOOR_BAND[1])
    print('  %-16s ink %5.1f%%  colours %3d  step %3d  span %3d  floor y%3d  '
          'black %4.1f%%  -> %s'
          % (name, s['ink'], s['colours'], s['step'], s['span'], s['floor'],
             s['black'], 'IN BAND' if ok else 'OUT OF BAND, re-roll'))
    return ok, s


def report():
    """Measure every fetched candidate, and the two shipped plates beside them."""
    print('\nreference (already in the game):')
    for p in ('Assets/Art/Backgrounds/club_room.png',
              'Assets/Art/Backgrounds/counter.png'):
        f = os.path.join(ROOT, p)
        if os.path.exists(f):
            judge(os.path.basename(f)[:-4], Image.open(f).convert('RGB'))
    print('\ncandidates:')
    st = load()
    best = None
    for key, _, styled, which in SEEDS:
        f = (st.get(key) or {}).get('file')
        if not f or not os.path.exists(f):
            print('  %-14s not fetched' % key)
            continue
        ok, s = judge(key + ('  [styled]' if styled else ''),
                      Image.open(f).convert('RGB'))
        st[key]['stats'] = s
        # Best = passes the gates, then fewest ink. Ties go to the higher colour count,
        # which is the cast's own tiebreak (a plate that reads flat beside its neighbours
        # is the fault `shaved` was re-rolled for).
        if ok and (best is None or (s['ink'], -s['colours']) < best[0]):
            best = ((s['ink'], -s['colours']), key)
    save(st)
    print('\nbest in band: %s' % (best[1] if best else 'NONE - re-roll with new seeds'))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if cmd == 'queue':
        queue(only=set(sys.argv[2:]) or None)
    elif cmd == 'fetch':
        fetch()
    elif cmd in ('judge', 'report'):
        report()
    else:
        print(json.dumps(load(), indent=1))
