"""The achievements' Steam icons (2026-09-28, the author: "İconları üret. Aynı sanat stiline ait olmalı, Kokteyl
iconları kullanılabilir ama arkaplan hep aynı olmalı üstündeki icon değişecek, kilitli olanlarda kilit olacak
açılmamış hallerinde.").

One plate for every icon, drawn here in the game's palette on a 32x32 grid: the night field, a pink neon edge
(the logo's), a soft disc behind the mark. On it, the achievement's mark from the 1-bit pack (Nikoichu, CC0 -
the same set the buffs and the menu's keys wear), or, for the unachieved state, a padlock drawn here in the
pack's own hand (white fill, 1px black ring) because the pack has none. The pack's star and heart are never
used (one star, one heart).

    py -3 -X utf8 Tools/steamworks/achievement_icons.py            write Tools/steamworks/out/icons/
    py -3 -X utf8 Tools/steamworks/achievement_icons.py --sheet    also a contact sheet of all 48, both states

Out: <API>_achieved.jpg / <API>_unachieved.jpg at 256x256 (Steam's recommended size, x8) and 64x64 (x2) under
out/icons/256 and out/icons/64 - whole multiples, so every pixel stays square. Nothing enters Assets from here.
"""

import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PACK = os.path.join(ROOT, "1-bit_Pixel_Icons", "Sprites")
BOOK = os.path.join(ROOT, "Assets", "Resources", "Data", "achievements.json")
OUT = os.path.join(HERE, "out", "icons")

# the palette (UITheme ramps, darkest first)
NIGHT = [(0x0D, 0x08, 0x13), (0x1A, 0x10, 0x23), (0x24, 0x18, 0x30), (0x36, 0x24, 0x47), (0x4A, 0x31, 0x60)]
MAGENTA = [(0x5C, 0x1B, 0x45), (0x8F, 0x24, 0x64), (0xC2, 0x32, 0x83), (0xE8, 0x4D, 0xA6), (0xFF, 0x7D, 0xC6)]
AMBER = [(0x4A, 0x2E, 0x14), (0x8F, 0x5A, 0x1E), (0xC9, 0x82, 0x2B), (0xE8, 0xA3, 0x3D), (0xF5, 0xC9, 0x7B)]
CREAM = [(0x45, 0x3E, 0x38), (0x6E, 0x64, 0x59), (0x9C, 0x8F, 0x80), (0xC9, 0xBC, 0xA8), (0xF2, 0xE8, 0xD5)]

# achievement -> the pack's mark (the evaluation of 2026-09-28; the page shows every one)
PICKS = {
    "FIRST_ROUND": "Food_Alcohol_Drink_Martini_Cocktail",
    "LIGHTS_OUT": "Software_Lighting_Bulb_Idea_Brainstorming_Electricity_Dim_Off",
    "A_NEW_PAGE": "Software_File_Document_Page_Blank",
    "HARD_SHAKE": "RPG_Stat_Strength_Fist_Melee_Attack",
    "GOOD_HEADS": "Food_Alcohol_Drink_Beer_Mug_Tavern",
    "GETTING_THE_HANG": "Emoji_Hand_Thumbs_Up",
    "ELBOW_GREASE": "Tools_Crafting_Broom_Sweeping_Cleaning",
    "SAME_AGAIN": "Arrows_Reload_Refresh_Rotate_Clockwise",
    "STOCKING_UP": "Tools_Crafting_Box_Crate_Shipping",
    "HOME_IMPROVEMENT": "Software_Image_File_Picture_Framed_Painting_Landscape_Photo_Decoration",
    "TALK_OF_THE_STREET": "Software_Speech_Bubble_Three_Dots_Dialogue",
    "SIX_NIGHTS": "Alchemy_Silver_Luna_Moon",
    "THE_USUAL": "RPG_Trade_Handshake_Deal_Exchange",
    "A_WEEKS_WORK": "Software_Clipboard_Todo_Tasks_Done_Checkmark",
    "THE_BLOCKS_BEST": "Map_Markers_Buildings_Houses_Homes_Village_Town",
    "NOT_TONIGHT": "Software_Sign_Crossout_Cancel_Forbidden_Illegal_1",
    "HUNDRED_CLUB": "Food_Alcohol_Drink_Cocktail_Straw_Juice",
    "TIP_JAR": "RPG_Loot_Bag_Money_Coins_Purse",
    "CLEAN_SHEET": "Software_Checkbox_Checkmarked_Yes_Done_Todo",
    "TWO_MORE_STOOLS": "Boardgames_Dice_Cube_D6_Six_Pips_Dots",
    "A_FORTNIGHT_OPEN": "Software_Calendar_Dates_Organizer_Month_1",
    "LAST_ORDERS": "Travel_Bell_Handle_Town_Crier_News",
    "CONFIDANT": "Hats_Glasses_Incognito_Spy_Hacker_Hidden",
    "TALK_OF_THE_TOWN": "Software_Speech_Bubble_Exclaimation_Mark_Quest_New",
    "STIRRED_NOT_SHAKEN": "Food_Restaurant_Eating_Utensils_Spoon",
    "WELL_READ": "Hats_Glasses_Spectacles",
    "DOWN_TO_THE_DROP": "Tools_Crafting_Graphic_Design_Eyedropper_Color_Picker",
    "THE_CITYS_BEST": "Map_Markers_Building_Castle_Fortress_City_Roof",
    "SEEN_THEM_ALL": "Software_Magnifier_Zoom_Looking_Magnifying_Glass",
    "BIG_NIGHT": "RPG_Banknote_Money_Currency_Dollar_Bill",
    "TAPMASTER": "Travel_Petrol_Oil_Barrel_Fuel",
    "THE_BOUNCER": "RPG_Item_Stat_Shield_Defense_Armor",
    "FOUR_WEEKS_OPEN": "Software_Calendar_Dates_Organizer_Month_2",
    "NIGHT_OWL": "Software_Clock_Time_Wait_Alarm_Sleep_Wake_Up",
    "FIVE_HUNDRED_POURS": "Food_Alcohol_Drink_Martini_Cocktail_Olive",
    "DISH_PIG": "Cosmetics_Soap_Bar_Bubbles_Foam",
    "TEN_OUT_OF_TEN": "Sports_Winner_Award_Medal_1st_First_Place_Gold",
    "A_THOUSAND_POURS": "RPG_Gem_Jewelcrafting_Diamond_Points_Currency",
    "THE_COUNTRYS_BEST": "Map_Markers_Flagpole",
    "FIVE_STARS_TONIGHT": "RPG_Magic_Sparkles_Enchantment",
    "MONEY_IN_THE_BANK": "Map_Markers_Building_Bank_Greek_Temple",
    "LEGENDARY_GLASSWARE": "Food_Alcohol_Drink_Wine_Glass",
    "THE_BEST_THERE_IS": "Hats_Crown_King_Monarch_Ruler_Emperor",
    "FIVE_STAR_ROOM": "Map_Markers_Sign_Enterance_Tavern_Shop",
    "THE_WHOLE_BOOK": "Map_Markers_Scroll_Map_Blank",
    "AN_INSTITUTION": "Tools_Crafting_Archaeology_Greek_Ruins_Column",
    "WRONG_CALL": "Emoji_Hand_Thumbs_Down",
    "BLOWOUT": "RPG_Item_Bomb_Grenade_Explosive",
}

# the padlock, in the pack's own hand: '#' the black ring, 'o' the white fill, '.' clear
PADLOCK = [
    "................",
    ".....######.....",
    "....#oooooo#....",
    "...#oo####oo#...",
    "...#o#....#o#...",
    "...#o#....#o#...",
    "..############..",
    "..#oooooooooo#..",
    "..#oooo##oooo#..",
    "..#ooo#..#ooo#..",
    "..#oooo#.#ooo#..",
    "..#oooo#.#ooo#..",
    "..#ooooo#oooo#..",
    "..#oooooooooo#..",
    "..############..",
    "................",
]


def plate():
    """The one background: 32x32, the night field, the pink neon edge, a soft disc behind the mark."""
    img = Image.new("RGB", (32, 32), NIGHT[2])
    px = img.load()
    for y in range(32):
        for x in range(32):
            edge = min(x, y, 31 - x, 31 - y)
            dx, dy = x - 15.5, y - 15.5
            d = (dx * dx + dy * dy) ** 0.5
            if edge == 0:
                px[x, y] = NIGHT[0]
            elif edge == 1:
                px[x, y] = MAGENTA[3]
            elif edge == 2:
                px[x, y] = MAGENTA[1]
            elif d <= 10.5:
                px[x, y] = NIGHT[4] if d <= 7.5 else NIGHT[3]
            else:
                px[x, y] = NIGHT[1] if edge == 3 else NIGHT[2]
    # the edge's corners, rounded by one pixel
    for cx, cy in ((1, 1), (30, 1), (1, 30), (30, 30)):
        px[cx, cy] = NIGHT[0]
    return img


def mark_from_pack(name):
    src = Image.open(os.path.join(PACK, name + ".png")).convert("RGBA")
    if src.size != (16, 16):
        raise SystemExit(f"{name} is {src.size}, not 16x16")
    return src


def mark_from_rows(rows):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c == "#":
                px[x, y] = (0, 0, 0, 255)
            elif c == "o":
                px[x, y] = (255, 255, 255, 255)
    return img


def stamp(base, mark, fill, ring):
    """The mark's white onto `fill`, its black onto `ring`, centred on the plate."""
    out = base.copy()
    px, mk = out.load(), mark.load()
    for y in range(16):
        for x in range(16):
            r, g, b, a = mk[x, y]
            if a < 128:
                continue
            px[8 + x, 8 + y] = fill if r + g + b > 384 else ring
    return out


def save_all(img, api, state):
    for size in (256, 64):
        folder = os.path.join(OUT, str(size))
        os.makedirs(folder, exist_ok=True)
        big = img.resize((size, size), Image.NEAREST)
        big.save(os.path.join(folder, f"{api}_{state}.jpg"), quality=95, subsampling=0)
        if size == 256:
            big.save(os.path.join(folder, f"{api}_{state}.png"))


def main(argv):
    book = [a["id"] for a in json.load(open(BOOK, encoding="utf-8"))["achievements"]]
    missing = [a for a in book if a not in PICKS]
    if missing:
        raise SystemExit("no mark picked for: " + ", ".join(missing))
    base = plate()
    lock = stamp(base, mark_from_rows(PADLOCK), CREAM[2], NIGHT[0])
    sheet_rows = []
    for api in book:
        done = stamp(base, mark_from_pack(PICKS[api]), CREAM[4], NIGHT[0])
        save_all(done, api, "achieved")
        save_all(lock, api, "unachieved")
        sheet_rows.append((api, done))
    if "--sheet" in argv:
        cols, cell = 8, 32 * 4 + 16
        rows = (len(sheet_rows) + cols - 1) // cols + 1
        sheet = Image.new("RGB", (cols * cell, rows * cell), NIGHT[0])
        for i, (api, img) in enumerate(sheet_rows):
            sheet.paste(img.resize((128, 128), Image.NEAREST), ((i % cols) * cell + 8, (i // cols) * cell + 8))
        sheet.paste(lock.resize((128, 128), Image.NEAREST), (8, (rows - 1) * cell + 8))
        sheet.save(os.path.join(OUT, "sheet.png"))
    print(f"{len(book)} achievements x 2 states -> {OUT} (256 and 64)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
