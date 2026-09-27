# -*- coding: utf-8 -*-
"""SHIP THE 1-BIT BUFF MARKS (2026-09-27, the author: "1-bit_Pixel_Icons klasörü ... buff, debuff veya
metinlerdeki açıklamalar için kullanılabilir" and, on the evaluation page, "Önerilenleri uygula").

Source: the author's copy of "1-bit Pixel Icons" by Nikoichu (https://nikoichu.itch.io/pixel-icons),
CC0 1.0 - no restrictions; the author asks for credit and it is given in Docs/KREDILER.md. The pack sits
at the project root (outside Assets, so it never enters a build); only the picks below are shipped.

Each pick is a 16x16 two-colour icon (white fill, 1px black ring). It is snapped onto the palette - the
fill to Cream[4], the ring to Night[0], exactly the ink the buff row gives a mask - and written to
Assets/Resources/Items/ib_<key>.png, where the Items import rule already makes it a point-filtered,
uncompressed sprite, with a meta copied from the house's own 16 (heart3d_16) under a fresh guid.
TycoonHud.BuffIcon reads ib_<key> first and falls back to ChromeArt's drawn mark.

Idempotent: a file whose pixels already match the snapped pick is left untouched (its meta too).

  py -3 -X utf8 Tools/icons_1bit_ship.py            ship / refresh the picks
  py -3 -X utf8 Tools/icons_1bit_ship.py --check    report only
"""
import hashlib
import io
import os
import re
import sys
import uuid

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
PACK = os.path.join(ROOT, "1-bit_Pixel_Icons", "Sprites")
ITEMS = os.path.join(ROOT, "Assets", "Resources", "Items")
META_FROM = os.path.join(ITEMS, "heart3d_16.png.meta")
CREAM4 = (242, 232, 213, 255)
NIGHT0 = (13, 8, 19, 255)

# buff key -> the pack's sprite (the evaluation page's recommended letter, 2026-09-27)
PICKS = {
    "price":         "Map_Markers_Price_Tag_Store_Dollar_Sign",
    "patience":      "Software_Hourglass_Sand_Time_Wait",
    "lateness":      "Software_Clock_Time_Wait_Alarm_Sleep_Wake_Up",
    "late_tip":      "RPG_Loot_Bag_Money_Coins_Purse",
    "refill":        "Arrows_Media_Controls_Loop_Reload_Refresh",
    "round":         "Food_Alcohol_Drink_Martini_Cocktail_Olive",
    "arrivals":      "Map_Markers_Doorway_Enterance_Exit_Door",
    "grace":         "Boardgames_Card_Defense_Shield",
    "window":        "RPG_Stat_Accuracy_Ranged_Target",
    "shake_speed":   "Software_Power_Electricity_Battery_Thunder_Lightning_Bolt_Zap",
    "pour_speed":    "Alchemy_Element_Water",
    "wash":          "Cosmetics_Soap_Bar_Bubbles_Foam",
    "free_drain":    "Software_Trashcan_Garbage_Bin_Rubbish_Delete_Erase_1",
    "pay_floor":     "RPG_Loot_Bag_Money_Coins_Purse_Dollars_Bank",
    "refusal":       "Software_Sign_Crossout_Cancel_Forbidden_Illegal_1",
    "mess":          "Tools_Crafting_Broom_Sweeping_Cleaning",
    "room":          "Emoji_Face_Happy",
    "stock_premium": "RPG_Banknotes_Money_Currency_Dollar_Bills_Stack",
    "glass_rung":    "Food_Alcohol_Drink_Wine_Glass",
    "kegs":          "Travel_Petrol_Oil_Barrel_Fuel",
    # THE FRONT DOOR'S KEYS (2026-09-27, the author: "ana menüyü geliştir ... butonların üstündeki iconları
    # güncelle"): shown at exactly 2x in the pack key's 32 glyph box (TycoonHud.MainMenu)
    "m_continue":    "Software_Save_Button_Floppy_Disk",
    "m_new_run":     "Arrows_Media_Controls_Play_Triangle",
    "m_settings":    "Software_Options_Settings_Cogwheel_Gear_Mechanics",
    "m_quit":        "Arrows_Power_Button_Switch_Turn_On_Off",
    "m_audio":       "Media_Audio_Sound_Volume_3",
    "m_language":    "Software_Planet_Geography_Localization_Global_Language_Translation_1",
    "m_credits":     "Software_Text_Document_Credits_Roll_Attributions",
    "m_steam":       "Platforms_Steam_Valve",
    "m_pointer":     "Arrows_Pointer_Right_East",
}


def snapped(name):
    im = Image.open(os.path.join(PACK, name + ".png")).convert("RGBA")
    assert im.size == (16, 16), (name, im.size)
    px = im.load()
    for y in range(16):
        for x in range(16):
            r, g, b, a = px[x, y]
            px[x, y] = (0, 0, 0, 0) if a < 128 else (CREAM4 if r + g + b > 384 else NIGHT0)
    return im


def digest(im):
    return hashlib.sha1(im.convert("RGBA").tobytes()).hexdigest()


def main(check):
    meta_src = io.open(META_FROM, encoding="utf-8", newline="").read()
    wrote = kept = 0
    for key, name in PICKS.items():
        want = snapped(name)
        out = os.path.join(ITEMS, "ib_%s.png" % key)
        if os.path.exists(out) and digest(Image.open(out)) == digest(want):
            kept += 1
            continue
        if check:
            print("  would ship ib_%s <- %s" % (key, name))
            continue
        want.save(out)
        meta = out + ".meta"
        if not os.path.exists(meta):
            guid = uuid.uuid5(uuid.NAMESPACE_URL, "lastcall/ib_" + key).hex
            io.open(meta, "w", encoding="utf-8", newline="").write(
                re.sub(r"guid: [0-9a-f]{32}", "guid: " + guid, meta_src, count=1))
        wrote += 1
        print("  shipped ib_%s <- %s" % (key, name))
    print("1-bit marks: %d written, %d already current" % (wrote, kept))


if __name__ == "__main__":
    main("--check" in sys.argv)
