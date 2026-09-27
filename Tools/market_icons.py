# -*- coding: utf-8 -*-
"""THE MARKET'S ICONS, EACH DRAWN AT THE SIZE IT IS SHOWN (2026-09-27, the author: "Oyundaki market için önceden
ürettiğin iconlarını tekrardan oluştur market iconlarını markette kullanılacak boyutlarına göre özel olarak üret.
Oluştururken marketin stiline uygun olduğunu emin ol.").

What the PALM CARGO tablet was wearing: the five department tabs, the basket's cart, the van, the battery and the
reading card's marks were the green storefront's set (HALLOWAY & SONS, 2026-08-09/10) - off the palette, with a
near-black green outline that vanishes on the open navy key, multiplied by a grey on the resting ones, a 60x30
painted van squeezed to 16x8 in its stamp, and marks drawn 16x14 and top-aligned so they sat a unit high in every
box. The reading card's head squeezed whatever product it was showing into 18 units (0.19x to 0.56x). None of it
was made by a tool here; none of it could be redrawn.

So this draws the market's own set, every icon at the exact size of its slot, 1 art pixel = 1 canvas unit, never
drawn big and shrunk (icon_sizes.py's law: at a size the art was not drawn for, you REDRAW):

  A  mk_tab_*      24x24  the five department keys. Small coloured pictograms in the up_* / sh_p_crate family: a
                          one-pixel keyline in ShopInk (ClubBlue[0]) that carries the shape on the grey resting key
                          and melts into the open navy one, where the BODIES carry it instead - so every body is a
                          ramp step >= 3:1 on #131B3D, two or three steps of ONE ramp lit from the upper left.
  B  mk_head_*     16x16  the same five subjects REDRAWN at 16 for the reading card's navy head bar.
  C  mk_lock/pour/van  16x16  white marks, tinted by the caller, siblings of ChromeArt.Mark: 2-px strokes, one
                          solid silhouette, content inside the 14x14 box centred in the 16.
  D  mk_cart       24x20  the basket band's cart: light on navy, a wire basket that reads as an even grid, not green.
  E  mk_batt       23x9   the tablet's battery, one ink like the wifi beside it.

What is NOT drawn here, and why (the review of the first draft, same day):
  - no star. The bar counts in ONE star (ChromeArt "star", ItemArt.Star, never tinted) and it means the bar's
    standing; a second, tinted star beside "COMFORT +1" read as a rating gained. A gain row wears the house
    Mark("rise") - the arrow the slip's standing row and the buff plate's fallback already wear.
  - no coin. A rim round a disc read as a button or a target at 1x, and the house already has two round marks
    ("tips", and the money icon). A cost row wears the house Mark("cash"), the drawn dollar of the night's slip.
  - no second tick. The first draft's tick was the house Mark("tick") one row lower and one unit larger; two
    ticks that nearly match is the thing the one-star rule was written against. So the tick is FOLDED into the
    house instead: PROPOSED_TICK below is the house tick re-seated on the 16's centre (the house one sits 1.5
    units high, rows 2-10), meant for ChromeArt.Masks["tick"] (--masks prints it as C#), and every sh_g_tick
    site points at Mark("tick"). That also moves the bench's step tick (TycoonServiceFlow.Shaker), so the
    LookTests re-bless once.
  - no new padlock. sh_lock's 21 slate tones were laddered onto ClubBlue + Graphite: the face landed on a
    saturated royal blue (a toy), the grey rungs between made the hue flicker, and a Graphite-only ladder turned
    it a different metal from its own chain (sh_chain_x, the same off-palette slate). The palette has no cool
    desaturated slate; sh_lock and sh_chain_x are a matched pair and stay as they are until they move together.

Every 16 and 24 is an explicit PIXEL MAP below - at these sizes a hand map is the only way to own every pixel.
Every opaque colour is a UITheme token (or pure white for the marks), every alpha is 0 or 255.

The art only lands if its slots do (TycoonHud, not this tool): the card head's mark box is 18x18 and the buff icon
14x14 (Build.cs), both with preserveAspect, so a 16 would be drawn at 1.125x / 0.875x - both go to 16x16. The
resting tab tint (Market.cs, x #7E87A2) turns a coloured pictogram to mud - it goes to white, like the open key.
StateInk's stamp inks were picked for a white plate and the stamp now sits on the dark window - Picked
Amber[3], Ordered Cyan[4], NoFitting ViceRed[4].

Nothing is written into Assets unless --ship is passed, and --ship waits for the author's pick (the house rule:
new art goes to a preview first).

  py -3 -X utf8 Tools/market_icons.py             writes Tools/market_icons_out/<name>.png
  py -3 -X utf8 Tools/market_icons.py --check     ...and asserts size, alpha, palette, centring; prints the table
  py -3 -X utf8 Tools/market_icons.py --preview   ...and _sheet.png, _context.png, _compare.png beside them
  py -3 -X utf8 Tools/market_icons.py --masks     ...and prints PROPOSED_TICK as ChromeArt C#
  py -3 -X utf8 Tools/market_icons.py --ship      copies the finished PNGs into Assets/Resources/Items
"""
import glob
import os
import re
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'market_icons_out')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
FONTS = os.path.join(ROOT, 'Assets', 'Fonts')
CHROMEART = os.path.join(ROOT, 'Assets', 'Scripts', 'UI', 'Art', 'ChromeArt.cs')


def _hex(*hs):
    return [tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) for h in hs]


# UITheme ramps (Assets/Scripts/UI/UITheme.cs), darkest first.
NIGHT = _hex('#0D0813', '#1A1023', '#241830', '#362447', '#4A3160')
MAGENTA = _hex('#5C1B45', '#8F2464', '#C23283', '#E84DA6', '#FF7DC6')
CYAN = _hex('#123B45', '#1B5F66', '#26918F', '#3BC8BE', '#7DF0E3')
AMBER = _hex('#4A2E14', '#8F5A1E', '#C9822B', '#E8A33D', '#F5C97B')
VICERED = _hex('#3D1220', '#6E1B32', '#A62B44', '#D9455C', '#F27D8A')
CLUBBLUE = _hex('#131B3D', '#1F2E66', '#2E4699', '#4467CC', '#6E93F0')
LIME = _hex('#16331B', '#2A5926', '#479938', '#6FCC4B', '#A8F077')
CREAM = _hex('#453E38', '#6E6459', '#9C8F80', '#C9BCA8', '#F2E8D5')
MALT = _hex('#3A2410', '#6B4416', '#9E6A1D', '#C98F2B', '#E6B959')
GRAPHITE = _hex('#14161A', '#24272D', '#383D45', '#545A64', '#808893')
BRICK = _hex('#38161A', '#5C2226', '#7E3130', '#9C4740', '#B96253')
PALETTE = set(NIGHT + MAGENTA + CYAN + AMBER + VICERED + CLUBBLUE + LIME + CREAM + MALT + GRAPHITE + BRICK)
WHITE = (255, 255, 255)

# The site's own surfaces and inks (TycoonHud.cs), for the checks and the mocks - never painted into an icon.
NAVY = CLUBBLUE[0]                     # ShopInk = ShopViceDeep: open tab, basket band, card head
PAPER = _hex('#C0C4CF')[0]             # ShopPaper: resting tab face, reading-card body
PAGE = _hex('#F7F8FB')[0]              # ShopPage
BEVEL_LIT = _hex('#EFF1F8')[0]
BEVEL_SHADE = CLUBBLUE[1]
OSBAR = _hex('#DDE0EA')[0]
STAMP = _hex('#110B18')[0]             # Night[0] at 82% over the Night[2] window
WINDOW = NIGHT[2]
LOCKED_INK = _hex('#DBDEEB')[0]

# One legend for the whole set, so a letter means the same paint in every map.
LEGEND = {
    'k': CLUBBLUE[0],                                  # the keyline: ShopInk, the ink the whole site is cut with
    'W': MALT[4], 'w': MALT[3], 'd': MALT[2],          # wood (the crate is beer's own Malt, as sh_p_crate)
    'C': CYAN[4], 'c': CYAN[3], 'v': CYAN[2],          # glass, the stool's vinyl
    'A': AMBER[4], 'a': AMBER[3], 'b': AMBER[2],       # whisky, citrus
    'r': VICERED[3],                                   # caps, the pimento
    'Q': CREAM[4], 'q': CREAM[3],                      # labels, carton, the cart's wire
    'M': MAGENTA[4], 'm': MAGENTA[3], 'n': MAGENTA[2], # the drink, the grip - the site's lit accent
    'L': LIME[4], 'l': LIME[3], 'g': LIME[2], 'o': LIME[0],   # the up-arrow badge (and the olive)
    'G': GRAPHITE[4],                                  # steel: the stool, the crate's strap, the bottle's screw cap
    # up_* construction (the restock tile, D): its Graphite[1] ink and the Amber wood of up_bar's counter
    'K': GRAPHITE[1], 'S': GRAPHITE[3], 'H': AMBER[1], 'E': AMBER[0],
    '#': WHITE,                                        # the marks: white, for the caller to tint
}

# ── A. THE DEPARTMENT KEYS, 24x24 ─────────────────────────────────────────────────────────────────────────────

# The crate stands on sh_p_crate's own frame - two end posts and a steel strap down the middle that run through
# every seam - so on the navy key, where the seams go dark, the box still holds together instead of floating as
# three bands (the first draft's three slats and three capped sticks read as a layered cake with candles). Each
# bottle comes up cap, neck, then a 4-unit shoulder into the crate, which is a bottle's outline and not a stick's.
TAB_RESTOCK = [
    "........................",
    "..........kkkk..........",
    "..........krrk..........",
    "....kkkk..krrk..kkkk....",
    "....krrk..kCck..krrk....",
    "....krrk..kCck..krrk....",
    "....kCck..kCck..kCck....",
    "....kCck..kCck..kCck....",
    "...kCCcckkCCcckkCCcck...",
    "...kCCcckkCCcckkCCcck...",
    "..kkkkkkkkkkkkkkkkkkkk..",
    "..kWWWWWWWWGGWWWWWWWdk..",
    "..kWwwwwwwwGGwwwwwwwdk..",
    "..kWdddddddGGddddddddk..",
    "..kWkkkkkkkGGkkkkkkkdk..",
    "..kWWWWWWWWGGWWWWWWWdk..",
    "..kWwwwwwwwGGwwwwwwwdk..",
    "..kWdddddddGGddddddddk..",
    "..kWkkkkkkkGGkkkkkkkdk..",
    "..kWWWWWWWWGGWWWWWWWdk..",
    "..kWwwwwwwwGGwwwwwwwdk..",
    "..kWdddddddGGddddddddk..",
    "..kkkkkkkkkkkkkkkkkkkk..",
    "........................",
]

# The label's band is printed in the bottle's own Amber[2], not cut in keyline: a navy dash read as a minus sign
# on the grey key and as a hole on the navy one.
TAB_LIQUOR = [
    "........................",
    "........................",
    ".....kkkk...............",
    ".....kGGk...............",
    ".....kGGk....kkkk.......",
    ".....kCck....krrk.......",
    ".....kCck....krrk.......",
    ".....kCck....kAak.......",
    "....kCCcck.kkkAakkk.....",
    "...kCCcccckAaaaaaabk....",
    "...kCcccckAaaaaaaaabk...",
    "...kCcccckAaaaaaaaabk...",
    "...kCcccckAQQQQQQQQbk...",
    "...kCcccckAQQQQQQQQbk...",
    "...kCcccckAQQbbbbQQbk...",
    "...kCcccckAQQQQQQQQbk...",
    "...kCcccckAQQQQQQQQbk...",
    "...kCcccckAaaaaaaaabk...",
    "...kCcccckAaaaaaaaabk...",
    "...kCcccckAaaaaaaaabk...",
    "...kCcccckAaaaaaaaabk...",
    "...kkkkkkkkkkkkkkkkkk...",
    "........................",
    "........................",
]

# The wheel's flesh is Amber[3] shaded Amber[2], so the Cream pith and spokes stand off it (on Amber[4] they were
# 1.28:1 and the wheel read as a pale biscuit marked with an X).
TAB_MIXERS = [
    "........................",
    "........................",
    ".....kkkk...............",
    ".....kQqk...............",
    "....kQQqqk..............",
    "...kQQQqqqk.............",
    "..kQQQQqqqqk............",
    "..kkkkkkkkkk............",
    "..kQQQQQQQqk............",
    "..kQQQQQQQqk............",
    "..kcccccccvk..kkkk......",
    "..kcccccccvkkkbbbbkk....",
    "..kcccccccvkbbQQQQbbk...",
    "..kQQQQQQQqkbQaaaaQbk...",
    "..kQQQQQQQkbQaQaaQbQbk..",
    "..kQQQQQQQkbQaaQQbbQbk..",
    "..kQQQQQQQkbQaaQQbbQbk..",
    "..kQQQQQQQkbQaQbbQbQbk..",
    "..kQQQQQQQqkbQbbbbQbk...",
    "..kQQQQQQQqkbbQQQQbbk...",
    "..kQQQQQQQqkkkbbbbkk....",
    "..kkkkkkkkkk..kkkk......",
    "........................",
    "........................",
]

# The pick runs unbroken from the olive into the rim (on navy its keyline went dark and the olive floated).
TAB_RECIPES = [
    "........................",
    ".....................C..",
    "................kkkkC...",
    "...............kllggk...",
    "...............klrrgk...",
    "...............kllggk...",
    "................Ckkk....",
    "..kkkkkkkkkkkkkCkkkkkk..",
    "..kCCCCCCCCCCCCCCCCCCk..",
    "...kMMMMMMMMMMMMMMMMk...",
    "....kMmmmmmmmmmmmmnk....",
    ".....kMmmmmmmmmmmnk.....",
    "......kMmmmmmmmmnk......",
    ".......kMmmmmmmnk.......",
    "........kMmmmmnk........",
    ".........kMmmnk.........",
    "..........kCck..........",
    "..........kCck..........",
    "..........kCck..........",
    "........kkkCckkk........",
    ".......kCCCCCCcck.......",
    ".......kkkkkkkkkk.......",
    "........................",
    "........................",
]

# up_seats' own stool: a seat, one pole, a plate - no footrest (the first draft's crossbars made the pedestal a
# double cross, a grave marker), and the pole runs up into the seat so the navy keyline cannot cut it loose.
TAB_UPGRADES = [
    "........................",
    "........................",
    "...............oo.......",
    "..............oLlo......",
    ".............oLlllo.....",
    "............oLlllllo....",
    "...........oLlllllllo...",
    "..........oLlllllllllo..",
    "...kkkkkkkooooLllloooo..",
    "..kCCCCCCCCCCoLlllo.....",
    "..kccccccccccoLlllo.....",
    "..kvvvvvvvvvvoLlllo.....",
    "...kkkkGGkkkkoLlllo.....",
    "......kGGk...oooooo.....",
    "......kGGk..............",
    "......kGGk..............",
    "......kGGk..............",
    "......kGGk..............",
    "......kGGk..............",
    "....kkkGGkkk............",
    "...kGGGGGGGGk...........",
    "...kGGGGGGGGk...........",
    "...kkkkkkkkkk...........",
    "........................",
]

# ── B. THE READING CARD'S HEAD MARKS, 16x16, on the navy head only ─────────────────────────────────────────────

HEAD_RESTOCK = [
    "................",
    ".......rr.......",
    "..rr...rr...rr..",
    "..rr...Cc...rr..",
    "..Cc...Cc...Cc..",
    ".CCcc.CCcc.CCcc.",
    ".CCcc.CCcc.CCcc.",
    ".WWWWWWGGWWWWWd.",
    ".WdddddGGdddddd.",
    ".WkkkkkGGkkkkkd.",
    ".WWWWWWGGWWWWWd.",
    ".WdddddGGdddddd.",
    ".WkkkkkGGkkkkkd.",
    ".WWWWWWGGWWWWWd.",
    ".WdddddGGdddddd.",
    "................",
]

HEAD_LIQUOR = [
    "................",
    "...GG...........",
    "...GG.....rr....",
    "...Cc.....rr....",
    "...Cc.....Aa....",
    "..CCcc....Aa....",
    ".CCcccc.Aaaaab..",
    ".CcccckAaaaaaab.",
    ".CcccckAaaaaaab.",
    ".CcccckAQQQQQQb.",
    ".CcccckAQQQQQQb.",
    ".CcccckAQQQQQQb.",
    ".CcccckAaaaaaab.",
    ".CcccckAaaaaaab.",
    ".CcccckAaaaaaab.",
    "................",
]

# At 16 the wheel has no room for a pith ring, so it is a round 8x8 of Amber[3] flesh in an Amber[2] rind with the
# cream spokes crossing it and one Amber[4] light at the upper left - a true circle (the first draft's bottom row
# sat a unit left of its top row).
HEAD_MIXERS = [
    "................",
    "....Qq..........",
    "...QQqq.........",
    "..QQQqqq........",
    ".QQQQqqqq.......",
    ".kkkkkkkk.......",
    ".QQQQQQQq.......",
    ".ccccccckbbbb...",
    ".cccccckbAaaab..",
    ".QQQQQkbAQaaQab.",
    ".QQQQQkbaaQQaab.",
    ".QQQQQkbaaQQaab.",
    ".QQQQQkbaQaaQab.",
    ".QQQQQQkbaaaab..",
    ".QQQQQQQkbbbb...",
    "................",
]

HEAD_RECIPES = [
    "................",
    "............C...",
    ".........llgC...",
    ".........lrg....",
    ".CCCCCCCCCCCCCC.",
    ".MMMMMMMMMMMMMM.",
    "..Mmmmmmmmmmmn..",
    "...Mmmmmmmmmn...",
    "....Mmmmmmmn....",
    ".....Mmmmmn.....",
    "......Mmmn......",
    ".......CC.......",
    ".......CC.......",
    ".......CC.......",
    "....CCCCCCcc....",
    "................",
]

HEAD_UPGRADES = [
    "................",
    "..........ll....",
    ".........Llll...",
    "........Llllll..",
    ".......Llllllll.",
    ".CCCCCCCoLlll...",
    ".cccccccoLlll...",
    ".vvvvvvvoLlll...",
    "...GG....Llll...",
    "...GG...........",
    "...GG...........",
    "...GG...........",
    "...GG...........",
    "..GGGG..........",
    ".GGGGGG.........",
    "................",
]

# ── C. THE MARKS, 16x16 white, tinted by the caller (ChromeArt.Mark grammar) ──────────────────────────────────

MARK_LOCK = [
    "................",
    ".....######.....",
    "....########....",
    "....##....##....",
    "....##....##....",
    "....##....##....",
    "..############..",
    "..############..",
    "..############..",
    "..#####..#####..",
    "..#####..#####..",
    "..#####..#####..",
    "..############..",
    "..############..",
    "..############..",
    "................",
]

MARK_POUR = [
    "................",
    ".......##.......",
    ".......##.......",
    "......####......",
    "......####......",
    ".....######.....",
    "....########....",
    "...##########...",
    "...##########...",
    "..############..",
    "..############..",
    "..############..",
    "...##########...",
    "....########....",
    "......####......",
    "................",
]

# One silhouette: the cab is solid round a 2x2 windscreen (the first draft's 1-unit wireframe cab left a hollow
# bracket that dissolved at 1080p), and the two wheels stand apart under their arches.
MARK_VAN = [
    "................",
    "................",
    "................",
    ".########.......",
    ".########.......",
    ".############...",
    ".##########..#..",
    ".##########..##.",
    ".##############.",
    ".#....####....#.",
    "...##......##...",
    "..####....####..",
    "..####....####..",
    "...##......##...",
    "................",
    "................",
]

# THE TICK FOLDED INTO THE HOUSE: ChromeArt's own tick, one unit larger and seated on the 16's centre. Not a PNG -
# it is meant for ChromeArt.Masks["tick"] (--masks), so the game keeps one tick.
PROPOSED_TICK = [
    "................",
    "................",
    "................",
    "............###.",
    "...........###..",
    "..........###...",
    ".........###....",
    ".##.....###.....",
    ".###...###......",
    "..###.###.......",
    "...#####........",
    "....###.........",
    ".....#..........",
    "................",
    "................",
    "................",
]

# ── D. THE BASKET'S CART, 24x20, light on the navy band ───────────────────────────────────────────────────────

# An even wire grid: the uprights stand three apart (cols 11, 14, 17) against a 2-unit right wall, so every cell is
# two units wide; the leftmost narrows only as the cart's front leans in. Round casters, not three-toothed ones.
CART = [
    "........................",
    ".MMM....................",
    ".MMMQ...................",
    "....QQ..................",
    ".....QQQQQQQQQQQQQQQQQQ.",
    ".....QQQQQQQQQQQQQQQQQQ.",
    "......QQ...q..q..q..QQ..",
    "......QQ...q..q..q..QQ..",
    ".......QQ..q..q..q..QQ..",
    ".......QQqqqqqqqqqqqQQ..",
    ".......QQ..q..q..q..QQ..",
    "........QQ.q..q..q..QQ..",
    "........QQQQQQQQQQQQQQ..",
    ".........QQ.............",
    ".........QQ.............",
    "........QQQQQQQQQQQQQQ..",
    "........qqqqqqqqqqqqqq..",
    ".........QQ........QQ...",
    "........QQqq......QQqq..",
    ".........qq........qq...",
]

# ── E. THE BATTERY, 23x9, one ink on the OS bar ───────────────────────────────────────────────────────────────

# Cells in ShopInk, the wifi's own ink: Lime[3] was 1.53:1 on the bar, and green is this site's money signal.
BATT = [
    "kkkkkkkkkkkkkkkkkkkkkk.",
    "k....................k.",
    "k.kkkk.kkkk.kkkk.....k.",
    "k.kkkk.kkkk.kkkk.....kk",
    "k.kkkk.kkkk.kkkk.....kk",
    "k.kkkk.kkkk.kkkk.....kk",
    "k.kkkk.kkkk.kkkk.....k.",
    "k....................k.",
    "kkkkkkkkkkkkkkkkkkkkkk.",
]

# name -> (map, size, kind, one line on why)
# ── D. THE RESTOCK TILE, 48x48, in the up_* rail's own construction ────────────────────────────────────────────
#
# The author (2026-09-27, pointing at the upgrade rail: "Restock görseli, paylaştığım görseldeki iconlar. Tekrardan
# tasarlansın anlattığım tarza göre"): the RESTOCK THE WELL tile wore sh_p_crate, a 24x20 crate blown to 48x40 and
# stood at 3x - a picture in another hand beside the upgrade tab's pictograms. It is redrawn in theirs: a 24 grid at
# 2x (upgrade_icons.py's K = 2), the Graphite[1] keyline one cell wide, up_bar's Amber wood lit from the upper left
# with a lit left edge, sh_p_crate's steel strap down the middle, two capped bottles coming up cap - neck -
# shoulder (a third stood behind the badge and showed only as a sliver), and the house's green up-arrow badge in the top-right corner. The badge is not redrawn: it is lifted
# pixel for pixel out of up_bar.png, which carries the author's own 09-23 pass, so the two can never drift apart.
# 48 in the tile's 118 art box stands at a whole 2x, the same 96 the upgrade tiles stand at.
UP_RESTOCK = [
    "........................",
    "........................",
    "...KKKK.KKKK............",
    "...KrrK.KrrK............",
    "...KrrK.KrrK............",
    "...KCcK.KCcK............",
    "...KCcK.KCcK............",
    "...KCcK.KCcK............",
    "..KCCccKCCccK...........",
    "..KCCccKCCccK...........",
    ".KKKKKKKKKKKKKKKKKK.....",
    ".KAAAAAAAGSAAAAAAaK.....",
    ".KAaaaaaaGSaaaaaabK.....",
    ".KAbbbbbbGSbbbbbbHK.....",
    ".KEEEEEEEGSEEEEEEEK.....",
    ".KAaaaaaaGSaaaaaabK.....",
    ".KAbbbbbbGSbbbbbbHK.....",
    ".KAHHHHHHGSHHHHHHEK.....",
    ".KEEEEEEEGSEEEEEEEK.....",
    ".KAbbbbbbGSbbbbbbHK.....",
    ".KAHHHHHHGSHHHHHHEK.....",
    ".KAHHHHHHGSHHHHHHEK.....",
    ".KKKKKKKKKKKKKKKKKK.....",
    "........................",
]


def up_badge():
    """The house's green up-arrow badge exactly as up_bar.png carries it (the author's pass): its Lime pixels in the
    top-right quarter, everything else clear."""
    src = Image.open(os.path.join(ITEMS, 'up_bar.png')).convert('RGBA')
    out = Image.new('RGBA', src.size, (0, 0, 0, 0))
    sp, op = src.load(), out.load()
    for y in range(src.height // 2 + 2):
        for x in range(src.width // 2 - 2, src.width):
            p = sp[x, y]
            if p[3] == 255 and p[:3] in (LIME[0], LIME[3], LIME[4]):
                op[x, y] = p
    return out


def build_up_restock():
    im = from_map(UP_RESTOCK, (24, 24)).resize((48, 48), Image.NEAREST)
    im.alpha_composite(up_badge())
    return im


ICONS = {
    'mk_tab_restock': (TAB_RESTOCK, (24, 24), 'tab', "a Malt crate on sh_p_crate's posts and strap, three capped bottles"),
    'mk_tab_liquor': (TAB_LIQUOR, (24, 24), 'tab', "a whisky bottle with a cream label in front of a clear Cyan one"),
    'mk_tab_mixers': (TAB_MIXERS, (24, 24), 'tab', "a gable-top juice carton beside an orange wheel: rind, pith, segments"),
    'mk_tab_recipes': (TAB_RECIPES, (24, 24), 'tab', "a martini glass in the site's Magenta with an olive on a pick"),
    'mk_tab_upgrades': (TAB_UPGRADES, (24, 24), 'tab', "up_seats' stool and up_*'s green up-arrow badge, redrawn at 24"),
    'mk_head_restock': (HEAD_RESTOCK, (16, 16), 'head', "the crate at 16"),
    'mk_head_liquor': (HEAD_LIQUOR, (16, 16), 'head', "the two bottles at 16"),
    'mk_head_mixers': (HEAD_MIXERS, (16, 16), 'head', "the carton and the wheel at 16"),
    'mk_head_recipes': (HEAD_RECIPES, (16, 16), 'head', "the martini at 16"),
    'mk_head_upgrades': (HEAD_UPGRADES, (16, 16), 'head', "the stool and the badge at 16"),
    'mk_lock': (MARK_LOCK, (16, 16), 'mark', "bad / no room / locked / the fitting lamp"),
    'mk_pour': (MARK_POUR, (16, 16), 'mark', "a use: the drop"),
    'mk_van': (MARK_VAN, (16, 16), 'mark', "on the van"),
    'mk_cart': (CART, (24, 20), 'cart', "the basket band's cart"),
    'mk_batt': (BATT, (23, 9), 'batt', "the OS bar's battery"),
}

# The house marks the market wears instead of drawing its own (read from ChromeArt.cs, never written).
HOUSE = ['rise', 'cash', 'tick']        # 'tick' is PROPOSED_TICK, the fold

# Every place a 16 mark is shown in the market, in the ink it is shown in:
# (where, mark, ink, surface). Marks named without mk_ are the house's.
USES = [
    ('GAIN', 'rise', LIME[1], PAPER),                 # reading card: WriteBuff, was sh_b_star
    ('COST', 'cash', AMBER[1], PAPER),                #   ... was sh_b_coin
    ('BAD', 'mk_lock', VICERED[2], PAPER),            #   ... was sh_b_lock
    ('USE', 'mk_pour', NAVY, PAPER),                  #   ... was sh_b_pour
    ('IN BASKET', 'tick', AMBER[3], STAMP),           # tile stamp, was sh_g_tick in Amber[1]
    ('ON THE VAN', 'mk_van', CYAN[4], STAMP),         #   ... was sh_van in ShopViceDeep
    ('NO ROOM', 'mk_lock', VICERED[4], STAMP),        #   ... was sh_b_lock in ShopCost
    ('WORN', 'tick', LIME[3], STAMP),                 # decor card stamp
    ('LOCKED', 'mk_lock', LOCKED_INK, STAMP),
    ('LAMP', 'mk_lock', MAGENTA[3], PAGE),            # the fitting lamp beside the tabs
    ('LAMP USED', 'mk_lock', VICERED[2], PAGE),
]


# ── building ─────────────────────────────────────────────────────────────────────────────────────────────────

def luma(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def from_map(rows, size):
    w, h = size
    assert len(rows) == h, 'map has %d rows, wants %d' % (len(rows), h)
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(rows):
        assert len(row) == w, 'row %d is %d wide, wants %d: %r' % (y, len(row), w, row)
        for x, ch in enumerate(row):
            if ch == '.':
                continue
            assert ch in LEGEND, 'unknown paint %r at (%d,%d)' % (ch, x, y)
            px[x, y] = LEGEND[ch] + (255,)
    return im


def house_rows(name):
    """A ChromeArt mask as the game draws it: its 16 rows read straight out of ChromeArt.cs."""
    if name == 'tick':
        return PROPOSED_TICK
    src = open(CHROMEART, encoding='utf-8').read()
    i = src.index('["%s"] = new[]' % name)
    body = src[src.index('{', i):src.index('}', i)]
    rows = re.findall(r'"([.#]+)"', body)
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), 'ChromeArt "%s" is not a 16x16 mask' % name
    return rows


def house_built_tick():
    """The tick ChromeArt draws today - only for the compare sheet."""
    src = open(CHROMEART, encoding='utf-8').read()
    i = src.index('["tick"] = new[]')
    return from_map(re.findall(r'"([.#]+)"', src[src.index('{', i):src.index('}', i)]), (16, 16))


def build_all():
    os.makedirs(OUT, exist_ok=True)
    made = {}
    for name, (rows, size, _, _) in ICONS.items():
        im = from_map(rows, size)
        im.save(os.path.join(OUT, name + '.png'))
        made[name] = im
    up = build_up_restock()
    up.save(os.path.join(OUT, 'up_restock.png'))
    made['up_restock'] = up
    # a name the set no longer draws must not linger beside it, or --ship's reader would think it current
    for stale in glob.glob(os.path.join(OUT, 'mk_*.png')):
        if os.path.splitext(os.path.basename(stale))[0] not in ICONS:
            os.remove(stale)
    return made


def all_marks(made):
    marks = {n: made[n] for n, v in ICONS.items() if v[2] == 'mark'}
    for n in HOUSE:
        marks[n] = from_map(house_rows(n), (16, 16))
    return marks


# ── checking ─────────────────────────────────────────────────────────────────────────────────────────────────

def rel_lum(c):
    def ch(v):
        v /= 255.0
        return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
    return 0.2126 * ch(c[0]) + 0.7152 * ch(c[1]) + 0.0722 * ch(c[2])


def contrast(a, b):
    la, lb = rel_lum(a), rel_lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def pixels(im):
    """Every RGBA pixel, flat (Pillow 12 renamed getdata)."""
    get = getattr(im, 'get_flattened_data', None)
    return list(get() if get else im.getdata())


def colours_of(im):
    return sorted({p[:3] for p in pixels(im) if p[3] == 255}, key=luma)


def centre_off(im):
    bb = im.getbbox()
    return bb, (bb[0] + bb[2]) / 2.0 - im.width / 2.0, (bb[1] + bb[3]) / 2.0 - im.height / 2.0


def mark_note(name):
    uses = [u for u in USES if u[1] == name]
    if not uses:
        return ''
    worst = min(uses, key=lambda u: contrast(u[2], u[3]))
    return 'min ink %.2f (%s)' % (contrast(worst[2], worst[3]), worst[0])


def check(made):
    fails = []
    rows = []
    for name, (_, size, kind, _) in ICONS.items():
        im = made[name]
        if im.size != size:
            fails.append('%s is %s, wants %s' % (name, im.size, size))
        alphas = {p[3] for p in pixels(im)}
        if not alphas <= {0, 255}:
            fails.append('%s has alpha %s' % (name, sorted(alphas - {0, 255})))
        cols = colours_of(im)
        if kind == 'mark':
            if cols != [WHITE]:
                fails.append('%s is not one white: %s' % (name, cols))
        else:
            off = [c for c in cols if c not in PALETTE]
            if off:
                fails.append('%s has off-palette %s' % (name, ['#%02X%02X%02X' % c for c in off]))
        bb, dx, dy = centre_off(im)
        if kind in ('tab', 'head'):
            if abs(dx) > 1 or abs(dy) > 1:
                fails.append('%s bbox centre off by (%.1f,%.1f)' % (name, dx, dy))
            if len(cols) > 9:
                fails.append('%s has %d colours (> 9)' % (name, len(cols)))
        if kind == 'mark':
            if bb[0] < 1 or bb[1] < 1 or bb[2] > 15 or bb[3] > 15:
                fails.append('%s leaves the 14x14 box: %s' % (name, bb))
            if abs(dx) > 0.5 or abs(dy) > 0.5:
                fails.append('%s bbox centre off by (%.1f,%.1f)' % (name, dx, dy))
        # contrast on the slot's own surfaces; the keylines (ShopInk, and the badge's own Lime[0] ring, up_*'s
        # language) are lines, not bodies, and may melt into the navy
        body = [c for c in cols if c not in (CLUBBLUE[0], LIME[0])]
        if kind in ('tab', 'head', 'cart'):
            nb = min(contrast(c, NAVY) for c in body)
            floor = 7.0 if kind == 'cart' else 3.0
            note = 'body/navy %.2f' % nb
            if kind == 'tab':
                note += '  key/paper %.2f' % contrast(CLUBBLUE[0], PAPER)
            if nb < floor:
                fails.append('%s: a body under %.0f:1 on navy (%.2f)' % (name, floor, nb))
        elif kind == 'batt':
            note = 'ink/os bar %.2f' % contrast(CLUBBLUE[0], OSBAR)
        else:
            note = mark_note(name)
        rows.append((name, '%dx%d' % im.size, '%d,%d-%d,%d' % (bb[0], bb[1], bb[2] - 1, bb[3] - 1),
                     '%+.1f,%+.1f' % (dx, dy), len(cols), note))
    # D: the restock tile in the up_* construction
    up = made['up_restock']
    if up.size != (48, 48):
        fails.append('up_restock is %s, wants 48x48' % (up.size,))
    if not {p[3] for p in pixels(up)} <= {0, 255}:
        fails.append('up_restock has a partial alpha')
    ucols = colours_of(up)
    uoff = [c for c in ucols if c not in PALETTE]
    if uoff:
        fails.append('up_restock has off-palette %s' % ['#%02X%02X%02X' % c for c in uoff])
    ubb, udx, udy = centre_off(up)
    rows.append(('up_restock', '48x48', '%d,%d-%d,%d' % (ubb[0], ubb[1], ubb[2] - 1, ubb[3] - 1),
                 '%+.1f,%+.1f' % (udx, udy), len(ucols), 'tile window (Night[2]) at 2x'))
    # the house marks the market now wears: drawn by ChromeArt, reported here so the table covers every slot
    for n in HOUSE:
        im = from_map(house_rows(n), (16, 16))
        bb, dx, dy = centre_off(im)
        if n == 'tick' and (abs(dx) > 0.5 or abs(dy) > 0.5 or bb[0] < 1 or bb[1] < 1 or bb[2] > 15 or bb[3] > 15):
            fails.append('the proposed tick is off its centre (%.1f,%.1f)' % (dx, dy))
        label = 'PROPOSED Mark(tick)' if n == 'tick' else 'house Mark(%s)' % n
        rows.append((label, '16x16', '%d,%d-%d,%d' % (bb[0], bb[1], bb[2] - 1, bb[3] - 1),
                     '%+.1f,%+.1f' % (dx, dy), 1, mark_note(n)))
    return rows, fails


def print_table(rows):
    print('%-20s %-6s %-13s %-10s %-4s %s' % ('name', 'size', 'bbox', 'centre', 'cols', 'min contrast on its slot'))
    for r in rows:
        print('%-20s %-6s %-13s %-10s %-4d %s' % r)


def print_masks():
    print('            ["tick"] = new[]')
    print('            {')
    for r in PROPOSED_TICK:
        print('                "%s",' % r)
    print('            },')


# ── previews ─────────────────────────────────────────────────────────────────────────────────────────────────

_fonts = {}


def font(name, size):
    key = (name, size)
    if key not in _fonts:
        _fonts[key] = ImageFont.truetype(os.path.join(FONTS, name), size)
    return _fonts[key]


def text(im, xy, s, colour, face='SilkscreenBold.ttf', size=16, anchor_mid=None):
    """Hard-pixel type: PIL's aliased rasteriser (fontmode 1), so nothing smooth reaches the mock."""
    d = ImageDraw.Draw(im)
    d.fontmode = '1'
    f = font(face, size)
    x, y = xy
    if anchor_mid is not None:
        # vertically centre the cap height in a band [y, y+anchor_mid)
        top, bot = f.getbbox('H')[1], f.getbbox('H')[3]
        y = y + (anchor_mid - (bot - top)) // 2 - top
    d.text((x, y), s, fill=colour, font=f)
    return f.getlength(s)


def tint(im, rgb):
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (r * rgb[0] // 255, g * rgb[1] // 255, b * rgb[2] // 255, a)
    return out


def mul(im, f):
    return tint(im, tuple(int(round(255 * v)) for v in f))


def big(im, k):
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def rect(im, box, colour):
    ImageDraw.Draw(im).rectangle(box, fill=colour)


def bevel(im, x, y, w, h, t=2, raised=True):
    lit, shade = (BEVEL_LIT, BEVEL_SHADE) if raised else (BEVEL_SHADE, BEVEL_LIT)
    rect(im, (x, y + h - t, x + w - 1, y + h - 1), shade)        # bottom
    rect(im, (x + w - t, y, x + w - 1, y + h - 1), shade)        # right
    rect(im, (x, y, x + w - 1, y + t - 1), lit)                  # top
    rect(im, (x, y, x + t - 1, y + h - 1), lit)                  # left


def wifi():
    """ChromeArt.Wifi, the same arithmetic, for the OS bar mock."""
    import math
    im = Image.new('RGBA', (14, 10), (0, 0, 0, 0))
    px = im.load()
    for y in range(10):
        for x in range(14):
            dx, dy = x + 0.5 - 7.0, y + 0.5 - 1.0
            r = math.sqrt(dx * dx + dy * dy)
            ang = abs(math.degrees(math.atan2(dx, max(0.01, dy))))
            arc = ang <= 52 and ((3.1 <= r < 4.4) or (5.9 <= r < 7.2) or (8.6 <= r < 9.9))
            if arc or r < 1.6:
                px[x, 9 - y] = (255, 255, 255, 255)
    return im


TAB_NAMES = ['RESTOCK', 'LIQUOR', 'MIXERS', 'RECIPES', 'UPGRADES']
REST_TODAY = (0x7E / 255.0, 0x87 / 255.0, 0xA2 / 255.0)   # FadeShopTabs' resting icon tint, as built
TAB_ICONS = ['mk_tab_restock', 'mk_tab_liquor', 'mk_tab_mixers', 'mk_tab_recipes', 'mk_tab_upgrades']
HEADS = ['mk_head_restock', 'mk_head_liquor', 'mk_head_mixers', 'mk_head_recipes', 'mk_head_upgrades']


def surfaces_of(name):
    kind = ICONS[name][2] if name in ICONS else 'mark'
    if kind == 'tab':
        return [('paper', PAPER, None), ('navy', NAVY, None)]
    if kind in ('head', 'cart'):
        return [('navy', NAVY, None)]
    if kind == 'batt':
        return [('os bar', OSBAR, None)]
    return [(u[0].lower(), u[3], u[2]) for u in USES if u[1] == name]


def sheet(made, marks):
    """Every icon at 1x and 6x on each surface it lives on (the marks in their real inks), flowed in a grid:
    one block per icon, its name above, a swatch per surface with the 1x beside the 6x."""
    K, pad, width = 6, 10, 1800
    blocks = []
    names = [n for n in ICONS] + HOUSE
    for name in names:
        im = made[name] if name in made else marks[name]
        cells = [(label, bg, tint(im, ink) if ink else im) for label, bg, ink in surfaces_of(name)]
        cw = [a.width * (K + 1) + 3 * pad for _, _, a in cells]
        bw = sum(cw) + pad * (len(cells) - 1)
        bh = max(a.height for _, _, a in cells) * K + 44
        title = name.upper() if name in ICONS else ('PROPOSED HOUSE TICK' if name == 'tick' else 'HOUSE ' + name.upper())
        blocks.append((title, cells, cw, bw, bh))
    rows, row, x = [], [], pad
    for b in blocks:
        if row and x + b[3] > width - pad:
            rows.append(row)
            row, x = [], pad
        row.append(b)
        x += b[3] + 2 * pad
    rows.append(row)
    height = pad + sum(max(b[4] for b in r) + pad for r in rows)
    out = Image.new('RGB', (width, height), (0x2B, 0x2B, 0x33))
    y = pad
    for r in rows:
        x = pad
        for title, cells, cw, bw, bh in r:
            text(out, (x, y), title, (230, 230, 235), 'Silkscreen-Regular.ttf', 8)
            cx = x
            for (label, bg, art), w in zip(cells, cw):
                rect(out, (cx, y + 14, cx + w - 1, y + bh - 1), bg)
                text(out, (cx + 3, y + 17), label.upper(), (128, 128, 136), 'Silkscreen-Regular.ttf', 8)
                out.paste(art, (cx + pad, y + 32), art)
                b6 = big(art, K)
                out.paste(b6, (cx + 2 * pad + art.width, y + 32), b6)
                cx += w + pad
            x += bw + 2 * pad
        y += max(b[4] for b in r) + pad
    out.save(os.path.join(OUT, '_sheet.png'))


def tab_strip(made, marks, open_i, resting_mul):
    W, H = 8 + 5 * 168 + 150, 52
    im = Image.new('RGB', (W, H), PAGE)
    base = H - 6                                             # the page frame's top edge
    rect(im, (0, base, W - 1, base + 1), NAVY)               # the page's 2px frame, which the open key joins
    for i in range(5):
        on = i == open_i
        x = 8 + i * 168
        h = 38 if on else 30
        y = base - h + (2 if on else 0)
        face = NAVY if on else PAPER
        rect(im, (x, y, x + 159, y + h - 1), face)
        bevel(im, x, y, 160, h)
        if on:
            rect(im, (x + 3, y + 2, x + 160 - 4, y + 4), MAGENTA[4])
        icon = made[TAB_ICONS[i]]
        if not on and resting_mul is not None:
            icon = mul(icon, resting_mul)
        iy = y + (h - 24) // 2
        im.paste(icon, (x + 8, iy), icon)
        text(im, (x + 40, y), TAB_NAMES[i], (255, 255, 255) if on else NAVY, size=16, anchor_mid=h)
    # the fitting lamp at the bar's right
    lamp = tint(marks['mk_lock'], MAGENTA[3])
    im.paste(lamp, (8 + 5 * 168 + 4, base - 15 - 8), lamp)
    text(im, (8 + 5 * 168 + 24, base - 15 - 8), '1 UPGRADE TONIGHT', NAVY, 'Silkscreen-Regular.ttf', 8, anchor_mid=16)
    return im


def reading_card(made, marks, head, name, buffs, meta='', body=''):
    """The hover card with its slots at 16 (the head mark box and the buff icon box both go to 16x16)."""
    W, H = 320, 26 + 76
    im = Image.new('RGB', (W, H), PAPER)
    rect(im, (0, 0, W - 1, 25), NAVY)
    m = made[head]
    im.paste(m, (8, 5), m)
    text(im, (30, 0), name, PAGE, size=16, anchor_mid=26)
    text(im, (10, 30), meta, _hex('#5A6073')[0], 'Silkscreen-Regular.ttf', 8)
    rect(im, (10, 44, W - 11, 44), (0xDC, 0xDE, 0xE7))
    text(im, (10, 49), body, NAVY, 'Silkscreen-Regular.ttf', 8)
    y = 64
    for mark, ink, words in buffs:
        mk = tint(marks[mark], ink)
        im.paste(mk, (10, y), mk)
        text(im, (30, y), words, ink, 'Silkscreen-Regular.ttf', 8, anchor_mid=16)
        y += 18
    ImageDraw.Draw(im).rectangle((0, 0, W - 1, H - 1), outline=NAVY)
    return im


def context(made, marks):
    """In-context mocks, drawn at 1x in canvas units then shown at 2x (the look at 1440p)."""
    parts = []
    variants = [('(i) resting untinted  <- RECOMMENDED: white in both states', None, 1),
                ('(ii) resting x (0.85,0.87,0.92)', (0.85, 0.87, 0.92), 2),
                ('(iii) resting x #7E87A2 (as built today)', REST_TODAY, 4)]
    for label, m, open_i in variants:
        cap = Image.new('RGB', (1000, 14), PAGE)
        text(cap, (8, 3), label, NAVY, 'Silkscreen-Regular.ttf', 8)
        parts.append(cap)
        parts.append(tab_strip(made, marks, open_i, m))
    # (b) the basket head band
    band = Image.new('RGB', (1000, 38), PAGE)
    rect(band, (8, 4, 807, 33), NAVY)
    cart = made['mk_cart']
    band.paste(cart, (8 + 10, 4 + 5), cart)
    text(band, (8 + 38, 4), 'BASKET', (255, 255, 255), size=16, anchor_mid=30)
    parts.append(band)
    # (c) the reading cards
    cards = Image.new('RGB', (1000, 2 * 108 + 8), PAGE)
    specs = [('mk_head_restock', 'CRATE OF 6', [('rise', LIME[1], '+2 STOCK ON THE SHELF'), ('cash', AMBER[1], 'COSTS 40 A CRATE')]),
             ('mk_head_liquor', 'OLD HARROW', [('rise', LIME[1], 'PATIENCE +18%'), ('mk_pour', NAVY, '16 POURS A BOTTLE')]),
             ('mk_head_mixers', 'ORANGE JUICE', [('mk_pour', NAVY, '12 POURS A CARTON'), ('cash', AMBER[1], 'COSTS 6')]),
             ('mk_head_recipes', 'COSMOPOLITAN', [('rise', LIME[1], 'NEW ON THE MENU'), ('mk_lock', VICERED[2], 'NEEDS CRANBERRY')]),
             ('mk_head_upgrades', 'CHROME STOOLS', [('rise', LIME[1], 'COMFORT +1'), ('mk_lock', VICERED[2], 'ONE FITTING A NIGHT')])]
    for i, (head, nm, buffs) in enumerate(specs):
        meta, body = [('RESTOCK  -  6 BOTTLES', 'THE SHELF FILLS AT DAWN'), ('BOURBON  -  750 ML', 'ON THE BACK BAR TONIGHT'),
                      ('MIXER  -  1 L', 'KEEPS TWO NIGHTS'), ('RECIPE  -  SHAKEN', 'VODKA, TRIPLE SEC, CRANBERRY'),
                      ('SEATS  -  RUNG 2', 'THE ROOM GIVES')][i]
        c = reading_card(made, marks, head, nm, buffs, meta, body)
        cards.paste(c, (8 + (i % 3) * 330, 4 + (i // 3) * 108))
    parts.append(cards)
    # (d) the tile window corner with the stamp plates, in the light inks
    win = Image.new('RGB', (1000, 3 * 26 + 12), WINDOW)
    stamps = [('tick', AMBER[3], 'IN BASKET'), ('mk_van', CYAN[4], 'ON THE VAN'), ('mk_lock', VICERED[4], 'NO ROOM'),
              ('tick', LIME[3], 'WORN'), ('mk_lock', LOCKED_INK, 'LOCKED')]
    for i, (mark, ink, word) in enumerate(stamps):
        x = 8 + (i % 3) * 180
        y = 6 + (i // 3) * 30
        f = font('SilkscreenBold.ttf', 8)
        w = 4 + 16 + 4 + int(f.getlength(word)) + 6
        rect(win, (x, y, x + w - 1, y + 19), STAMP)
        mk = tint(marks[mark], ink)
        win.paste(mk, (x + 4, y + 2), mk)
        text(win, (x + 24, y), word, ink, 'SilkscreenBold.ttf', 8, anchor_mid=20)
    # the fitting lamp both ways on the page, to its right
    rect(win, (560, 4, 990, 84), PAGE)
    for i, (ink, word) in enumerate([(MAGENTA[3], '1 UPGRADE TONIGHT'), (VICERED[2], 'FITTING TAKEN')]):
        mk = tint(marks['mk_lock'], ink)
        win.paste(mk, (572, 14 + i * 30), mk)
        text(win, (594, 14 + i * 30), word, NAVY if i == 0 else VICERED[2], 'Silkscreen-Regular.ttf', 8, anchor_mid=16)
    parts.append(win)
    # (e) the OS bar
    osb = Image.new('RGB', (1000, 20), OSBAR)
    text(osb, (12, 0), 'DAY 12', NAVY, 'Silkscreen-Regular.ttf', 8, anchor_mid=20)
    wf = tint(wifi(), NAVY)
    osb.paste(wf, (1000 - 52 - 14, 5), wf)
    bt = made['mk_batt']
    osb.paste(bt, (1000 - 14 - 23, 6), bt)
    parts.append(osb)
    # (f) the sealed tile: sh_lock KEPT, at 2x over its own chain on the dark window
    seal = Image.new('RGB', (1000, 160), PAGE)
    chain = Image.open(os.path.join(ITEMS, 'sh_chain_x.png')).convert('RGBA')
    ch = chain.crop((0, (chain.height - 148) // 2, 160, (chain.height - 148) // 2 + 148))
    rect(seal, (8, 6, 8 + 167, 6 + 147), NIGHT[0])
    seal.paste(ch, (12, 6), ch)
    lb = big(Image.open(os.path.join(ITEMS, 'sh_lock.png')).convert('RGBA'), 2)
    seal.paste(lb, (8 + (168 - 56) // 2, 6 + (148 - 84) // 2), lb)
    text(seal, (200, 60), 'SH_LOCK KEPT AS SHIPPED: IT AND SH_CHAIN_X ARE ONE MATCHED SLATE PAIR', NAVY, 'Silkscreen-Regular.ttf', 8)
    text(seal, (200, 74), 'THE PALETTE HAS NO COOL DESATURATED SLATE - THEY MOVE TOGETHER OR NOT AT ALL', NAVY, 'Silkscreen-Regular.ttf', 8)
    parts.append(seal)
    W = max(p.width for p in parts)
    H = sum(p.height + 6 for p in parts)
    out = Image.new('RGB', (W, H), PAGE)
    y = 0
    for p in parts:
        out.paste(p, (0, y))
        y += p.height + 6
    big(out, 2).save(os.path.join(OUT, '_context.png'))


def squeeze(im, box):
    """What a preserveAspect Image does to a sprite in a box under Point filtering: a fractional nearest resize."""
    s = min(box / im.width, box / im.height)
    w, h = max(1, int(round(im.width * s))), max(1, int(round(im.height * s)))
    return im.resize((w, h), Image.NEAREST)


def compare(made, marks):
    old = lambda n: Image.open(os.path.join(ITEMS, n + '.png')).convert('RGBA')
    # (old label, old image, new label, new image, surface)
    pairs = [('sh_i2_restock', old('sh_i2_restock'), 'mk_tab_restock', made['mk_tab_restock'], NAVY),
             ('sh_i2_bottles', old('sh_i2_bottles'), 'mk_tab_liquor', made['mk_tab_liquor'], NAVY),
             ('sh_i2_mixers', old('sh_i2_mixers'), 'mk_tab_mixers', made['mk_tab_mixers'], NAVY),
             ('sh_i2_recipes', old('sh_i2_recipes'), 'mk_tab_recipes', made['mk_tab_recipes'], NAVY),
             ('sh_i2_upgrades', old('sh_i2_upgrades'), 'mk_tab_upgrades', made['mk_tab_upgrades'], NAVY)]
    # the resting key as it is tinted today (x #7E87A2) against the new art untinted
    for o, n in [('sh_i2_restock', 'mk_tab_restock'), ('sh_i2_bottles', 'mk_tab_liquor'), ('sh_i2_mixers', 'mk_tab_mixers'),
                 ('sh_i2_recipes', 'mk_tab_recipes'), ('sh_i2_upgrades', 'mk_tab_upgrades')]:
        pairs.append((o + ' resting', mul(old(o), REST_TODAY), n + ' untinted', made[n], PAPER))
    for o, src, n in [('sh_p_crate @18', 'sh_p_crate', 'mk_head_restock'),
                      ('bottle @18', 'v4_bourbon_old_harrow_front_c', 'mk_head_liquor'),
                      ('tonic @18', 'v4_tonic_quinbury_front_c', 'mk_head_mixers'),
                      ('martini @18', 'glass3d_martini', 'mk_head_recipes'),
                      ('up_seats @18', 'up_seats', 'mk_head_upgrades')]:
        pairs.append((o, squeeze(old(src), 18), n, made[n], NAVY))
    pairs += [('sh_i_cart', old('sh_i_cart'), 'mk_cart', made['mk_cart'], NAVY),
              ('sh_batt', old('sh_batt'), 'mk_batt', made['mk_batt'], OSBAR)]
    for o, n, label, ink, bg in [('sh_b_star', 'rise', 'house Mark(rise)', LIME[1], PAPER),
                                 ('sh_b_coin', 'cash', 'house Mark(cash)', AMBER[1], PAPER),
                                 ('sh_b_lock', 'mk_lock', 'mk_lock', VICERED[2], PAPER),
                                 ('sh_b_pour', 'mk_pour', 'mk_pour', NAVY, PAPER),
                                 ('sh_g_tick', 'tick', 'PROPOSED Mark(tick)', AMBER[3], STAMP)]:
        pairs.append((o, tint(old(o), ink), label, tint(marks[n], ink), bg))
    pairs.append(('Mark(tick) as built', tint(house_built_tick(), AMBER[3]), 'PROPOSED Mark(tick)',
                  tint(marks['tick'], AMBER[3]), STAMP))
    pairs.append(('sh_van @16', tint(squeeze(old('sh_van'), 16), NAVY), 'mk_van', tint(marks['mk_van'], CYAN[4]), STAMP))
    K = 4
    rowh = [max(p[1].height, p[3].height) * K + 26 for p in pairs]
    out = Image.new('RGB', (900, sum(rowh) + 20), (0x2B, 0x2B, 0x33))
    y = 10
    for (oname, oim, nname, nim, bg), h in zip(pairs, rowh):
        rect(out, (10, y, 889, y + h - 6), bg)
        lab = (200, 200, 205) if luma(bg) < 128 else NAVY
        text(out, (14, y + 2), 'OLD ' + oname, lab, 'Silkscreen-Regular.ttf', 8)
        text(out, (454, y + 2), 'NEW ' + nname, lab, 'Silkscreen-Regular.ttf', 8)
        out.paste(oim, (14, y + 16), oim)
        ob = big(oim, K)
        out.paste(ob, (14 + oim.width + 10, y + 16), ob)
        out.paste(nim, (454, y + 16), nim)
        nb = big(nim, K)
        out.paste(nb, (454 + nim.width + 10, y + 16), nb)
        y += h
    out.save(os.path.join(OUT, '_compare.png'))


def ship():
    """The author's pick goes in: every finished PNG into Resources/Items, where PatronArtPostprocessor imports it
    as a Point-filtered, uncompressed sprite. Run only once the preview has been chosen - and only with the slot
    changes in the module header made, or the head and buff marks land at 1.125x / 0.875x."""
    for name in list(ICONS) + ['up_restock']:
        shutil.copyfile(os.path.join(OUT, name + '.png'), os.path.join(ITEMS, name + '.png'))
        print('shipped', name)


def main():
    args = set(sys.argv[1:])
    made = build_all()
    marks = all_marks(made)
    print('wrote %d icons to %s' % (len(made), OUT))
    if '--check' in args or '--preview' in args or '--ship' in args:
        rows, fails = check(made)
        print_table(rows)
        if fails:
            print('\nFAILED:')
            for f in fails:
                print('  ' + f)
            if '--check' in args or '--ship' in args:
                sys.exit(1)                     # nothing reaches Assets that failed a check
        else:
            print('\nall checks pass')
    if '--masks' in args:
        print_masks()
    if '--preview' in args:
        sheet(made, marks)
        context(made, marks)
        compare(made, marks)
        print('previews: _sheet.png _context.png _compare.png')
    if '--ship' in args:
        ship()


if __name__ == '__main__':
    main()
