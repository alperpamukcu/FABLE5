using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part Build: construction: the whole UI is built in code, and these are the bricks.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        private static bool Showing(RectTransform rt) => rt != null && rt.gameObject.activeSelf;

        // ── construction ────────────────────────────────────────────────────────

        private void BuildUi()
        {
            LoadPatronFrames();   // the seated customer's animation clips (Resources/Patron/*)

            var canvasGo = new GameObject("TycoonHud", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            // Match height (like the stage, 2026-07-22): every canvas scales off the same axis
            // so nothing drifts out of alignment when the window's aspect changes.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            // ...but the HUD is built in a FIXED field inside it (2026-08-11, the author:
            // "boyutu değiştirdikçe arkaplana göre oturttuğumuz nesneler kayıyor —
            // bardaklar, butonlar, çöp kutusu, kasa"). Match-height pins the vertical and
            // lets the width run with the window, and everything here that stands ON the
            // room — the glass shelf, the bin, the till, the buttons — was placed against
            // a width that moved. The field is 1280x720 forever and the camera is
            // windowboxed to the same 16:9, so the props and the room they stand in are
            // measured in the same units at every window shape. See DesignFrame.
            var root = DesignFrame.Wrap((RectTransform)canvasGo.transform,
                                        new Vector2(1280f, 720f));
            _hudRoot = root;

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            // The board over the back counter (the author, 2026-08-02: a top bar that
            // belongs to this bar). A dark fascia, lit along its top edge where the room
            // catches it and burning along its bottom in neon; the three readings sit on
            // it as PLAQUES, each seated on its own coloured rule — the licence card's
            // language, so the game's two instrument surfaces read as one hand.
            // The BOARD bleeds to the window; the instruments on it do not (2026-08-11).
            // A fascia that stops at the safe frame reads as a strip floating in the middle
            // of a wide monitor, and one that carries its plaques out to the window edges
            // is the drift the frame was built to stop. So the dark board, its hairline and
            // its neon run edge to edge, and the clock, the till and the standing stay
            // anchored where they were placed inside the frame.
            var top = NewRect("TopBar", root);
            Stretch(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -TopBarH), Vector2.zero);
            // THE BOARD IS A BEAM CARRYING INSTRUMENTS (2026-08-19, the author: "bu
            // şeriti tekrardan tasarla ... hepsinin yeri profesyonelce konumlandırılmalı.
            // Haftalık takvim göstergesi daha profesyonelce olmalı"). The 2026-08-14 cut
            // ("kutu kutu" refused) put ONE case on the beam and let the week float as
            // bulbs on a wire — and the wire is what the author is looking at now: signage
            // reads as decoration, not as a panel. So the beam still runs edge to edge with
            // its lit face and its neon foot, and what stands on it is now TWO matched
            // instruments and one jewel:
            //   · the HOUR — a case and a dark glass, the digits hand-drawn pixel masks
            //     (SegmentClock, 2026-08-19: "gerçekten kodlar pixel pixel yapsak")
            //   · the WEEK — a GENERATED plate ("pixellabden arkaplan oluştur", the one
            //     written exception to chrome-is-never-generated): teal-capped navy metal,
            //     the counter at its head, the seven nights as lamps in a slotted row
            //     (BuildWeekStrip); calendar data lives in the calendar instrument
            //   · the STANDING — five 3D gold stars straight on the beam (their box was
            //     refused 2026-08-14 and stays refused; the vice-fade tint lasted one
            //     build and was refused too), no number
            //   · one key at the far end; NEW RUN lives inside it
            // Everything else stands directly on the beam, on one centre line.
            var fascia = Panel(top, "Fascia", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, UITheme.Night[1]);
            BleedWidth.Apply(fascia);
            Band(fascia, "Face", 0f, 3f, UITheme.Night[3]);            // the top the room lights
            Band(fascia, "Turn", 3f, 1f, new Color(0f, 0f, 0f, 0.45f)); // where it turns down
            Band(fascia, "Foot", TopBarH - 3f, 3f, UITheme.Night[0]);   // shadow gathering at the tube

            // The tube: a bright core over a wider glow, bleeding below the panel. It is also
            // THE STATE LIGHT now — the whole board burns magenta at last call, which says it
            // from across the room without a word on it (it used to be a 2px rule under one
            // plaque, which nobody was ever going to notice).
            var tube = NewRect("Neon", fascia);
            tube.anchorMin = new Vector2(0, 0); tube.anchorMax = new Vector2(1, 0);
            tube.pivot = new Vector2(0.5f, 0);
            tube.sizeDelta = new Vector2(0, 2);
            tube.anchoredPosition = Vector2.zero;
            _neonTube = tube.gameObject.AddComponent<Image>();
            _neonTube.color = UITheme.Amber[4]; _neonTube.raycastTarget = false;
            var bloom = NewRect("NeonBloom", fascia);
            bloom.anchorMin = new Vector2(0, 0); bloom.anchorMax = new Vector2(1, 0);
            bloom.pivot = new Vector2(0.5f, 1);
            bloom.sizeDelta = new Vector2(0, 5);
            bloom.anchoredPosition = Vector2.zero;
            _neonBloom = bloom.gameObject.AddComponent<Image>();
            _neonBloom.color = new Color(UITheme.Amber[2].r, UITheme.Amber[2].g, UITheme.Amber[2].b, 0.30f);
            _neonBloom.raycastTarget = false;

            // ── the hour, left: what the night is measured in ──────────────────
            //
            // A DIGITAL CLOCK IS ITS SEGMENTS, NOT ITS TYPEFACE (2026-08-14, the author:
            // "dijital saat olmamış hiç dijital saate benzememiş"). The first pass set the
            // hour in the UI's pixel face and laid a dim copy of it behind — a caption in
            // costume, and it read as one. What a readout actually is: four seven-bar digits,
            // the unlit bars still faintly on the glass, a colon keeping its own half-second.
            // Then it was too small to read ("saat okunaklı değil"), so the case is the full
            // height of the beam and the digits are 28 units tall — the biggest thing on the
            // board, which is right, because the night is measured in it. See SegmentClock.
            // THE WEEK MOVED OUT (2026-08-19). It shared this glass for a build ("saat ve
            // kaçıncı hafta olduğu düzgün bir sayfa düzeninde olsun", 2026-08-14) and came
            // out as a two-line caption squeezed against the digits — the cramped corner the
            // author called unprofessional. The week is calendar data and reads at the
            // calendar's own head now; this instrument holds the hour and nothing else.
            //
            // A WELL, NOT A CASE (the fourth cut, same evening: "profesyonel bir UI/UX
            // designer gibi düşün"). The case was a raised box with a glass inside — two
            // nested rectangles, and the author called it boxing. A well is ONE object cut
            // INTO the beam: its floor is the display glass, its top edge is the dark one
            // (light comes from above — a recess shades at the top, a box shines there),
            // and its bottom lip catches the room. See ChromeArt.Well.
            var clockWell = NewRect("Clock", top);
            Place(clockWell, new Vector2(0, 0.5f), new Vector2(134, 40), new Vector2(16, 0));
            var clockImg = clockWell.gameObject.AddComponent<Image>();
            clockImg.sprite = ChromeArt.Well();
            clockImg.type = Image.Type.Sliced;
            clockImg.raycastTarget = false;

            var digits = NewRect("Digits", clockWell);
            Place(digits, new Vector2(0, 0.5f), new Vector2(110, 28), new Vector2(12, 0));
            // The metrics stopped being arguments (2026-08-19): the digits are hand-drawn
            // 11×14 masks at exactly 2× now — see SegmentClock's own header — so the one
            // size that exists is the drawing's, and passing another would be a lie.
            _clock = new SegmentClock(digits, UITheme.Cyan[4]);

            BuildWeekStrip(top);

            // ── the till is not up here any more (2026-08-14, the author: "para ise
            // kasada olsun") ───────────────────────────────────────────────────
            // The money reads off the REGISTER in the room, where the drawer is, and every
            // rise and fall floats off it. A copy of the same number in the fascia was the
            // thing that made the till in the room decorative.

            // ── the standing, right: the stars and who they brought in ─────────
            // NO PLATE UNDER THEM (2026-08-14, the author: "yıldızlar hala üst barda kutu
            // içerisinde gösteriliyor" — still standing). Stars are already a shape; boxing
            // them made them a widget. They stand on the beam with the crowd named on the
            // caption line above.
            // AND NO NUMBER BESIDE THEM (2026-08-19, the author: "0.0 neden gösteriliyor,
            // daha çok görsel bir şerit olmalı"). The display-size decimal that shared the
            // row is gone — the fill fraction says the same thing visually, and the exact
            // figure still prints where figures belong, in the ledger and the market. The
            // block is just the five-star run now, right-aligned into the same 40-unit gap
            // off the key the old block kept.
            const float RightEdge = -16f;                     // the grid's outer margin
            const float BlockRight = RightEdge - 40f;
            float starsW = _ratingStars.Length * StarGap;

            var standing = NewRect("Standing", top);
            Place(standing, new Vector2(1, 0.5f), new Vector2(starsW, TopBarH), new Vector2(BlockRight, 0));
            standing.pivot = new Vector2(1, 0.5f);

            // Inside the block: the row sits low enough to clear its own caption and high
            // enough to clear the neon. 32 tall centred at -5 spans -21..+11 in a 54 beam.
            const float RowY = -5f, CapRowY = 18f;

            var starsRow = NewRect("Stars", standing);
            Place(starsRow, new Vector2(0, 0.5f), new Vector2(starsW, StarSize),
                new Vector2(0, RowY));
            starsRow.pivot = new Vector2(0, 0.5f);
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"B{i}", starsRow);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                // The socket is the same 3D drawing in dark glass — its own sprite, not a
                // tint: multiplying the gold star dark turns its facets to mud, and the
                // socket has to read as the same OBJECT unlit, shading intact.
                img.sprite = ItemArt.Load("star3d_socket");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = Color.white;
            }
            _starsFill = NewRect("Fill", starsRow);
            _starsFill.anchorMin = new Vector2(0, 0); _starsFill.anchorMax = new Vector2(0, 1);
            _starsFill.pivot = new Vector2(0, 0.5f);
            _starsFill.sizeDelta = Vector2.zero;
            _starsFill.anchoredPosition = Vector2.zero;
            _starsFill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"F{i}", _starsFill);
                star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);   // fixed x: the mask slides over it
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(StarSize, StarSize);
                star.anchoredPosition = new Vector2(i * StarGap + StarGap * 0.5f, 0);
                var img = star.gameObject.AddComponent<Image>();
                // 3D GOLD, BAKED (2026-08-19, the author: "Yıldızlarda 3 boyutlu yıldız
                // iconlarından olsun fade geçiş rengini beğenmedim"). The vice-fade tint
                // lasted one build; the fill is the generated star's own gold now, so the
                // Image tints nothing — colour is IN the sprite, white multiplies clean.
                img.sprite = ItemArt.Load("star3d");
                img.preserveAspect = true; img.raycastTarget = false;
                img.color = Color.white;
                _ratingStars[i] = img;
            }

            // THE HOUSE'S TWO READINGS (GDD 27 §4.4, H5, 2026-09-05): what the drinks have
            // been worth tonight (the heart — SERVICE) and what the room is worth this
            // minute (the medallion — COMFORT), as two strips of five left of the standing.
            // No numbers, exactly as the stars carry none: the fill IS the reading, and the
            // night files the lower of the two under the stars' menu ceiling.
            var house = NewRect("House", top);
            Place(house, new Vector2(1, 0.5f), new Vector2(HouseStripW, TopBarH),
                new Vector2(BlockRight - starsW - 26f, 0));
            house.pivot = new Vector2(1, 0.5f);
            _serviceFill = IconStrip(house, "Service", ItemArt.Heart(false, 16f), ItemArt.Heart(true, 16f), RowY - 9f);
            _comfortFill = IconStrip(house, "Comfort", ItemArt.Medal(false, 16f), ItemArt.Medal(true, 16f), RowY + 9f);

            // Centred over the block it belongs to, not right-aligned to one edge of it.
            _crowdText = NewText("Crowd", standing, _body, 8, TextAnchor.MiddleCenter, UITheme.Cream[3]);
            Place(_crowdText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(starsW + 90f, 12),
                new Vector2(0, CapRowY));
            _crowdText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── the quiet end: one key, and nothing else that is not the night ──
            // NEW RUN went inside it (2026-08-14, the author). A button that throws away the
            // night you are playing does not belong one thumb away from the thing you press
            // all night, and it was the last word up here competing with the readings.
            // 26, NOT 30. A key inlays its mark 5 units a side, and the cog is drawn at 16;
            // at 30 the mark comes out 20 wide, which is 1.25x of a 16-pixel drawing — the
            // cog arrived with its teeth at two different widths. Pixel art scales at whole
            // multiples or it does not scale (see the house rule about the 8px faces).
            var cogKey = NewButton(top, "SETTINGS", new Vector2(1, 0.5f), new Vector2(26, 26),
                new Vector2(RightEdge, 0), UITheme.Night[2], ToggleSettings, ChromeArt.Mark("cog"));
            Hairline(cogKey, new Vector2(0, 1), new Vector2(1, 1), UITheme.Night[3]);
            Hairline(cogKey, new Vector2(0, 0), new Vector2(1, 0), new Color(0f, 0f, 0f, 0.55f));
            HairlineV(cogKey, 0f, UITheme.Night[3]);
            HairlineV(cogKey, 1f, new Color(0f, 0f, 0f, 0.55f));
            BuildSettings(root);
            BuildOrderTip(root);

            // THE CURTAIN, above everything the HUD owns. Its own canvas at 30 so it also
            // covers the market (22), the licence (20) and the guide (24) — a night that
            // starts from black starts from black whatever was left open.
            _curtain = NewRect("Curtain", root);
            Stretch(_curtain, Vector2.zero, Vector2.one, new Vector2(-64, -64), new Vector2(64, 64));
            var curtainCanvas = _curtain.gameObject.AddComponent<Canvas>();
            curtainCanvas.overrideSorting = true;
            curtainCanvas.sortingOrder = 30;
            _curtainImg = _curtain.gameObject.AddComponent<Image>();
            _curtainImg.color = new Color(0f, 0f, 0f, 0f);
            _curtainImg.raycastTarget = false;   // black, not a wall: it never eats a click
            BuildCurtainCard(_curtain);
            _curtain.gameObject.SetActive(false);

            // BIN GLASS retired (v5 P13 / C7): a drink is thrown away by carrying it to the bin
            // on the counter, which is the same verb that serves it.

            // Refusal notices ("NOT ENOUGH MONEY") drop in just under the top bar. On their
            // OWN canvas above every override (2026-08-19): the market sorts at 22 and half
            // of what the toast says — "ONE UPGRADE A NIGHT", "BASKET IS EMPTY" — is said
            // IN the market, where a toast without its own order drew under the scrim and
            // the refusal was never seen at all.
            _toast = NewText("Toast", root, _display, 14, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            _toastInk = _toast.color;
            // 620, not 500: at display-14 the box held ~34 glyphs, and the longer notices
            // — "STILL IN THE SHAKER — POUR IT INTO A GLASS", and the perfect pour's own
            // line — wrapped onto a second row that overhung the top bar (2026-08-25).
            Place(_toast.rectTransform, new Vector2(0.5f, 1), new Vector2(620, 32), new Vector2(0, -66));
            var toastCanvas = _toast.gameObject.AddComponent<Canvas>();
            toastCanvas.overrideSorting = true;
            toastCanvas.sortingOrder = 30;   // above the market (22), guide (24), bench (25)
            // …and it must never EAT one. A notice that sits above everything is a notice
            // that can swallow the click under it; it says a thing and takes nothing.
            _toast.raycastTarget = false;
            _toast.gameObject.SetActive(false);

            // Six stools along the counter: each customer is a bust sitting at the bar with a
            // floating order tag above their head; click anywhere on them to read or serve.
            const float seatStartX = 118f;
            for (int i = 0; i < SeatSlots; i++)
            {
                int index = i;
                var seat = new SeatView();
                seat.Index = i;
                seat.SeatX = seatStartX + i * SeatGap;

                // The click zone spans the bust and its tag; a clear image catches the ray.
                seat.Root = NewRect($"Seat{i}", root);
                seat.Root.anchorMin = seat.Root.anchorMax = new Vector2(0, 0);
                seat.Root.pivot = new Vector2(0.5f, 0);
                seat.Root.sizeDelta = new Vector2(150f, CharWinH + 110f);
                seat.Root.anchoredPosition = new Vector2(seat.SeatX, SeatLineY);
                var hit = seat.Root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);   // invisible, but catches clicks
                var button = seat.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnSeatClicked(index));
                seat.Group = seat.Root.gameObject.AddComponent<CanvasGroup>();

                // THE CUSTOMER STANDS IN THE ROOM, NOT ON THE HUD (2026-08-10). They were an
                // Image inside a RectMask2D window on this canvas — and an overlay canvas is
                // composited after the camera, so no Light2D could ever reach them: the room
                // was lit and the people in it were not. The body is a world sprite now, and
                // the mask is gone with it, because the BAR takes their legs. Sorting 25 puts
                // them over the room and under the counter, which is the same crop the window
                // was faking, done by the object that would really do it.
                //
                // Everything else about a stool stays here: the click target, the order tag,
                // the patience gauge and the fade all belong to the interface, not the room.
                var stageForBody = stage != null ? stage : FindFirstObjectByType<DiegeticStage>();
                if (stageForBody != null)
                {
                    seat.Body = stageForBody.NewStageSprite($"Patron{i}", 25);
                    seat.Body.gameObject.SetActive(false);
                    // CLICKING A DRINKER IS THE GAME'S FIRST VERB and nothing said so. The
                    // stool's hit plate is a transparent rectangle over a WORLD sprite, so
                    // the affordance has to be the person themselves: they brighten a step
                    // under the pointer, the same step the beer font and the cellar's
                    // bottles take (2026-08-25).
                    var seatGlow = seat.Root.gameObject.AddComponent<HoverGlow>();
                    seatGlow.Sprites = new[] { seat.Body };
                }

                // The order tag, floating above the head.
                // THE ORDER BUBBLE (2026-08-19). It was a flat tinted rectangle with one
                // neon rule under it; it is a drawn balloon now — the palette's white, a hot
                // magenta edge, and a spout pointing down at the head it belongs to
                // (ChromeArt.Bubble / BubbleTail, and see that file for why it is drawn
                // rather than generated).
                //
                // 9-SLICED, which is the whole reason it can carry a drawing at all. This
                // rect changes size on nearly every refresh — its width is its longest line,
                // its height is how many lines there are — so a picture hung on it plainly
                // would be stretched every time somebody ordered something with a long name.
                // Sliced, Unity draws the four real corners 1:1 at any size and repeats the
                // one-unit runs between them, and nothing distorts at any width or height.
                //
                // THE SPOUT IS NOT IN THAT SPRITE. A tail inside a stretched band smears
                // along it, so it is its own Image under the middle — the same split
                // BackBarArt's bottle card makes, for the same reason.
                seat.Tag = NewRect("Tag", seat.Root);
                seat.Tag.anchorMin = seat.Tag.anchorMax = new Vector2(0.5f, 0);
                seat.Tag.pivot = new Vector2(0.5f, 0);
                seat.Tag.sizeDelta = new Vector2(TagMinW, 40f);
                seat.Tag.anchoredPosition = new Vector2(0, CharWinH + TagLift);
                seat.TagBg = seat.Tag.gameObject.AddComponent<Image>();
                seat.TagBg.sprite = ChromeArt.Bubble();
                seat.TagBg.type = Image.Type.Sliced;
                seat.TagBg.raycastTarget = false;

                // The spout hangs BELOW the plate and overlaps it by its own three skirt
                // rows, which are plain fill and erase the plate's bottom band (two rows of
                // edge and the foot) exactly where the balloon should be open. Drawn at
                // 11x9, the size it was authored at, and never scaled — the one thing in
                // this ticket that must not be.
                var tailRt = NewRect("Tail", seat.Tag);
                tailRt.anchorMin = tailRt.anchorMax = new Vector2(0.5f, 0);
                tailRt.pivot = new Vector2(0.5f, 1);
                tailRt.sizeDelta = new Vector2(11f, 9f);
                tailRt.anchoredPosition = new Vector2(0, 3f);
                seat.Tail = tailRt.gameObject.AddComponent<Image>();
                seat.Tail.sprite = ChromeArt.BubbleTail();
                seat.Tail.raycastTarget = false;

                // ── THE SPEECH BALLOON (2026-09-04, the author: "konuşmalar normalde
                // kafalarının üstlerinde bulunan kartlardan ayrı olarak klasik diyalog
                // baloncuğunda olmalı ... birkaç saniye görünüp sonra yok olmalı").
                //
                // Its own object, deliberately. The ticket beside it is a standing readout —
                // it is up for as long as somebody is on the stool, and everything on it is
                // a FACT about the order. A line of speech is an EVENT: it arrives when they
                // say it and it is gone a few seconds later. Those two things cannot share a
                // rect without one of them behaving like the other, which is what happened
                // when the pour note went into the ticket's status row: the plate grew a
                // paragraph and then held that size for the length of the drink.
                //
                // Same balloon art, so the bar has one speech language — but the DRINK tone,
                // whose edge is the club's blue, because everything this bubble ever says is
                // said over a glass that has already been handed over. It sits exactly where
                // the ticket sits and the ticket stands down while it is up: one thing over
                // one head, which is the author's "kutucuk yerine".
                seat.Say = NewRect("Say", seat.Root);
                seat.Say.anchorMin = seat.Say.anchorMax = new Vector2(0.5f, 0);
                seat.Say.pivot = new Vector2(0.5f, 0);
                seat.Say.sizeDelta = new Vector2(TagMinW, 40f);
                seat.Say.anchoredPosition = new Vector2(0, CharWinH + TagLift);
                seat.SayBg = seat.Say.gameObject.AddComponent<Image>();
                seat.SayBg.sprite = ChromeArt.Bubble(ChromeArt.BubbleTone.Drink);
                seat.SayBg.type = Image.Type.Sliced;
                seat.SayBg.raycastTarget = false;

                var sayTail = NewRect("Tail", seat.Say);
                sayTail.anchorMin = sayTail.anchorMax = new Vector2(0.5f, 0);
                sayTail.pivot = new Vector2(0.5f, 1);
                sayTail.sizeDelta = new Vector2(11f, 9f);
                sayTail.anchoredPosition = new Vector2(0, 3f);
                seat.SayTail = sayTail.gameObject.AddComponent<Image>();
                seat.SayTail.sprite = ChromeArt.BubbleTail(ChromeArt.BubbleTone.Drink);
                seat.SayTail.raycastTarget = false;

                // The ticket's own face and size, because it is the same mouth talking.
                seat.SayText = NewText("Line", seat.Say, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Night[0]);
                Stretch(seat.SayText.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(TagPad, 0), new Vector2(-TagPad, -TagPad));
                seat.SayText.horizontalOverflow = HorizontalWrapMode.Wrap;
                seat.SayText.verticalOverflow = VerticalWrapMode.Overflow;
                seat.SayLineH = MeasuredLineHeight(seat.SayText);
                seat.Say.gameObject.SetActive(false);

                // THE THREE ROWS ARE ONE FACE AT ONE SIZE (2026-08-20, the author: "alkolün
                // yazdığı fontu değiştir diğerleriyle aynı yap") — the display face at 8, with
                // the NAME double-struck bold (PixelBold) so the heading is a weight rather
                // than a second typeface. The order row was the last thing set in the body
                // face; a ticket in two faces read as two objects stacked, and the name could
                // only out-rank a 16pt body row by being 16pt itself, which is what put a
                // 224-unit name on a 180-unit stool in the first place.
                //
                // The measurements that decided it are kept, because they are what rules the
                // alternatives out (8 or 16, never between — GDD 16 §0):
                //
                //   Silkscreen 8   caps ~5u   "SEX ON THE BEACH" 88u    ← the old ticket
                //   Press Start 8  caps  8u   "SEX ON THE BEACH" 128u
                //   Silkscreen 16  caps ~11u  "SEX ON THE BEACH" 176u
                //   Press Start 16 caps  16u  "SERENA FONTANA"   224u   ← the 236 cap, alone
                //
                // The last line is the whole overlap: ONE name in the display face at 16 is
                // wider than the gap between two stools, so every named customer pushed their
                // ticket over their neighbour's head. It cannot be fixed by moving the plate
                // — it has to not be that wide.
                //
                // So: every row takes the display face at 8 — 8-unit caps against the old
                // ticket's 5, bigger than what was called unreadable and half of what was
                // called too big — and "SEX ON THE BEACH" comes out 128 units, which leaves a
                // 142-unit ticket inside a 168 cap. Only the two longest drinks in the book
                // wrap, and the plate has always grown downward to take a second row.
                //
                // THE NAME IS THE TITLE, and now it is a WEIGHT (2026-08-20, the author:
                // "isimler kalın yazsın"): the same face and size as the rows under it,
                // double-struck one pixel wide by PixelBold — which is the only bold an 8px
                // face has, and is why FontStyle.Bold is not used (see that file).
                //
                // THE INKS TURNED OVER with the plate (2026-08-19). Cream, pale cyan and pale
                // magenta were chosen to sit on a near-black card; on a white one they are
                // invisible. Each row keeps its ROLE and takes the dark step of the same ramp:
                // the name is the room's own near-black, the status line is the plate's own
                // magenta, and the order is read blackest of all because it is the thing the
                // player is actually being asked to make.
                seat.Name = NewText("Name", seat.Tag, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Night[1]);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -8));
                seat.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                seat.Name.gameObject.AddComponent<PixelBold>().Distance = 1f;

                seat.Wants = NewText("Wants", seat.Tag, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Magenta[1]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -18));
                seat.Wants.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Order = NewText("Order", seat.Tag, _display, 8, TextAnchor.UpperCenter,
                    UITheme.Night[0]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -28));
                seat.Order.horizontalOverflow = HorizontalWrapMode.Overflow;

                // ASK THE FONT how tall its line is, once, here — the faces and the sizes are
                // fixed from this point on. Every frame's plate is the sum of the rows it is
                // showing, so a row height guessed even two units wrong is a plate with the
                // type sitting off-centre in it, which is precisely what a constant did.
                seat.NameLineH = MeasuredLineHeight(seat.Name);
                seat.WantsLineH = MeasuredLineHeight(seat.Wants);
                seat.OrderLineH = MeasuredLineHeight(seat.Order);

                // WHAT THEY WANT, BESIDE HOW THEY WANT IT (2026-08-19, the author: "istenilen
                // alkol iconun yanında nasıl servis edilmesi isteniyorsa onlarında iconu |
                // ayrılık gösterilecek ... ikonografi ile anlatmak önemli"). One row under the
                // order: the drink's own glass, a rule, then one mark per thing on the serving
                // spec — ice, a twist, a salted rim, a sugared rim.
                //
                // The rule between them is DRAWN, a one-unit Image, not a typed "|". Nothing
                // in this project asks a font for a picture (ChromeArt's own header) and a
                // pipe character in the pixel face is a different height on every row it sits
                // beside. The whole row is laid out in RefreshSeats, because how wide it is
                // depends on how many things they asked for.
                var icons = NewRect("Icons", seat.Tag);
                icons.anchorMin = icons.anchorMax = new Vector2(0.5f, 1);
                icons.pivot = new Vector2(0.5f, 1);
                icons.sizeDelta = new Vector2(IconRowH, IconRowH);
                seat.IconRow = icons;

                var iconRt = NewRect("OrderIcon", icons);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0, 0.5f);
                iconRt.sizeDelta = new Vector2(24, 24);
                seat.Icon = iconRt.gameObject.AddComponent<Image>();
                seat.Icon.preserveAspect = true;
                seat.Icon.raycastTarget = false;

                var ruleRt = NewRect("Rule", icons);
                ruleRt.anchorMin = ruleRt.anchorMax = new Vector2(0, 0.5f);
                ruleRt.pivot = new Vector2(0, 0.5f);
                ruleRt.sizeDelta = new Vector2(1f, 14f);
                seat.IconRule = ruleRt.gameObject.AddComponent<Image>();
                seat.IconRule.color = UITheme.Magenta[1];
                seat.IconRule.raycastTarget = false;

                // TWO marks and no more. ServingSpec.Roll never asks for a third (65% one,
                // else two), and a row that can grow without a bound is a row that will one
                // day run off the plate it is centred on.
                seat.Garnish = new Image[2];
                for (int g = 0; g < seat.Garnish.Length; g++)
                {
                    var gr = NewRect("G" + g, icons);
                    gr.anchorMin = gr.anchorMax = new Vector2(0, 0.5f);
                    gr.pivot = new Vector2(0, 0.5f);
                    gr.sizeDelta = new Vector2(16, 16);
                    var gi = gr.gameObject.AddComponent<Image>();
                    gi.color = UITheme.Night[1];
                    gi.raycastTarget = false;
                    seat.Garnish[g] = gi;
                }
                // The patience gauge rides the BODY, not the ticket (P15, absorbs the P8
                // gauge item): a slim bar floating just over the head, so reading who is
                // about to walk means looking at the people, not at their paperwork.
                // IT IS AN INSTRUMENT, AND IT IS DIVIDED IN THREE (2026-09-04, the author:
                // "sabır barını 3'e böleceğiz — kırmızı, sarı, yeşil … sabır barı için
                // profesyonel bir ui üret, temaya ve renklere uyan, miami 80s'lere uygun").
                //
                // It was a black rectangle with a coloured stripe in it, which said how much
                // was left and nothing about what that meant. This is the same instrument the
                // night's standing gauge and the book's ratio boxes are drawn on —
                // ChromeArt.GaugeTube for the chassis, GaugeGlass over it for the shine and
                // the measures — asked for THREE steps, so the two scratches in the glass fall
                // exactly on the band edges the till pays by (ServiceJudge.GreenBand/AmberBand).
                // The empty track behind carries the three bands in their own dark tones, so
                // the thirds can be read before the fill has drained into them, and a neon
                // strip under the glass takes the live band's colour — the counter's own trick,
                // which is where this room's light comes from.
                const float GaugeW = 78f, GaugeH = 10f;
                var clockBg = NewRect("ClockBg", seat.Root);
                clockBg.anchorMin = clockBg.anchorMax = new Vector2(0.5f, 0);
                clockBg.pivot = new Vector2(0.5f, 0);
                clockBg.sizeDelta = new Vector2(GaugeW, GaugeH);
                clockBg.anchoredPosition = new Vector2(0, CharWinH + 1f);
                var clockTube = clockBg.gameObject.AddComponent<Image>();
                clockTube.sprite = ChromeArt.GaugeTube((int)GaugeW, (int)GaugeH);
                clockTube.color = UITheme.Night[1];
                clockTube.raycastTarget = false;
                // The gauge's length IS the value, and it is re-hung off each look's own head
                // every frame. Snapping either to whole units would make patience tick down in
                // visible steps and park the bar off the head it belongs to. See UiAuditExempt.
                UiAuditExempt.Mark(clockBg, "a patience gauge whose width is the value itself, "
                    + "re-hung on the customer's own head each frame — snapping it would make "
                    + "the clock tick in steps");
                seat.Gauge = clockBg;   // re-hung off each look's own head, below

                var neon = NewRect("Neon", clockBg);
                neon.anchorMin = new Vector2(0, 0); neon.anchorMax = new Vector2(1, 0);
                neon.pivot = new Vector2(0.5f, 1);
                neon.offsetMin = new Vector2(3f, -2f); neon.offsetMax = new Vector2(-3f, 0f);
                seat.PatienceNeon = neon.gameObject.AddComponent<Image>();
                seat.PatienceNeon.raycastTarget = false;

                var clockInner = NewRect("Inner", clockBg);
                Stretch(clockInner, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
                // Left to right: the last third of the wait, the middle, the first. The
                // drink drains right to left, so the fill arrives in each band in turn.
                var bandInk = new[] { UITheme.ViceRed[1], UITheme.Amber[1], UITheme.Lime[1] };
                for (int b = 0; b < 3; b++)
                {
                    var zone = NewRect("Band" + b, clockInner);
                    zone.anchorMin = new Vector2(b / 3f, 0f);
                    zone.anchorMax = new Vector2((b + 1) / 3f, 1f);
                    zone.offsetMin = Vector2.zero; zone.offsetMax = Vector2.zero;
                    var zi = zone.gameObject.AddComponent<Image>();
                    zi.color = new Color(bandInk[b].r, bandInk[b].g, bandInk[b].b, 0.5f);
                    zi.raycastTarget = false;
                }
                seat.PatienceFill = FillBar(clockInner, UITheme.Lime[3]);

                var clockGlass = NewRect("Glass", clockBg);
                Stretch(clockGlass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var clockGi = clockGlass.gameObject.AddComponent<Image>();
                clockGi.sprite = ChromeArt.GaugeGlass((int)GaugeW, (int)GaugeH, 3);
                clockGi.raycastTarget = false;

                seat.Root.gameObject.SetActive(false);
                _seats.Add(seat);
            }

            // The primary action: open the menu to build a drink (GDD 24 §1), bottom-centred.
            // THE KEYS SIT ON THE BAR'S FACE, not across its shelves. At y 40 they lay
            // over the compartments the glassware stands in — the bar front is the shelf,
            // and the two most-pressed controls in the game were parked on it. The face
            // band (art rows 9..45) is empty drawn panelling and puts them nearer the
            // counter, which is where the hand already is.
            // THE MAKING VERB LEFT THE HUD (2026-08-22). It is on the cellar's own lid now,
            // beside the arrow that says which way the roller goes — one door, one key, and
            // the roller's own writing says OPEN and the room around it shuts the cellar again.
            // See DiegeticStage.BuildOpenSign / BuildCellarCatcher.

            // THE BOOK IS A BOOK ON THE BAR (2026-08-25, the author: "Book butonu ise
            // tezgahin ustune sabitlensin ve yeni uretilen book ... kapali kucuk bir
            // goruntusunu olusturup tezgahin ustune yerlestirelim ona tiklayarak menu
            // acilacak"). It was a 84x40 grey key floating on the bar's front panel with the
            // word BOOK on it — the last piece of menu-of-things-you-click left in a room
            // that had turned everything else into an object you reach for.
            //
            // The drawing is DERIVED from the open booklet rather than drawn beside it
            // (Tools/book_closed_gen.py reads menu_booklet.png's own colours), because the
            // thing standing on the counter has to be the thing that opens — this project
            // has paid three times for a second take coming back as a different object.
            var bookKey = BuildBookProp(root);
            // The badge rides the book's top-right corner, so the news is where the way in
            // already is. Built once and parked; RefreshBookBadge raises it.
            _bookBadge = NewRect("Badge", bookKey);
            Place(_bookBadge, new Vector2(1, 1), new Vector2(20, 20), new Vector2(6, 4));
            var badgeImg = _bookBadge.gameObject.AddComponent<Image>();
            badgeImg.color = BkPlatinum;
            badgeImg.raycastTarget = false;
            _bookBadgeText = NewText("N", _bookBadge, _display, 8, TextAnchor.MiddleCenter,
                new Color(0.16f, 0.18f, 0.24f));
            Stretch(_bookBadgeText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _bookBadge.gameObject.SetActive(false);
            BuildRecipeBook(root);

            BuildDrinkGlass(root);
            BuildMiniPreps(root);   // the counter's own garnish rail, beside where it rests
            // Built LAST of the room's furniture, so the caption draws over every prop it
            // can be raised by rather than under one of them.
            BuildPropTip(root);
            BuildSnackRow(root);
            BuildServiceLog(root);
            BuildIdCard(root);
            BuildLastCall(root);

            // Day end. The panel is a SCRIM over the whole room, not a slab (the author,
            // 2026-08-07: "hala mor bir çerçeve var"). The old 940x600 plate showed a
            // purple margin all round the tablet, which read as a window frame nobody
            // asked for — now the night dims and the device is the only lit thing.
            _dayEndPanel = NewRect("DayEnd", root);
            var dayEndCanvas = _dayEndPanel.gameObject.AddComponent<Canvas>();
            dayEndCanvas.overrideSorting = true;
            dayEndCanvas.sortingOrder = 22;   // the market covers the whole room, till included
            _dayEndPanel.gameObject.AddComponent<GraphicRaycaster>();
            Stretch(_dayEndPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panelImg = _dayEndPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.88f);

            var title = _dayEndTitle = NewText("Title", _dayEndPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(900, 24), new Vector2(0, -22));
            title.text = "LAST CALL — THE BOOKS";

            // THE NIGHT IS CALLED BEFORE IT IS COUNTED (2026-08-11, the author: at two in
            // the morning a line says the day is over, the room goes dark behind it, and
            // only once the words have gone does the till start printing). It is the beat
            // the sequence was missing — the slip used to arrive while the bar was still
            // lit, so the night ended and was totalled in the same movement.
            _lastCallCard = NewText("Called", _dayEndPanel, _display, 24, TextAnchor.MiddleCenter,
                UITheme.Amber[4]);
            Place(_lastCallCard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 40),
                new Vector2(0, 10f));
            _lastCallCard.horizontalOverflow = HorizontalWrapMode.Overflow;
            _lastCallCard.text = "THAT'S LAST CALL";
            _lastCallCard.raycastTarget = false;
            var calledUnder = NewText("CalledSub", _dayEndPanel, _body, 16, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.68f, 0.62f));
            Place(calledUnder.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 20),
                new Vector2(0, -22f));
            calledUnder.horizontalOverflow = HorizontalWrapMode.Overflow;
            calledUnder.text = "doors shut · counting the money";
            calledUnder.raycastTarget = false;
            _lastCallRt = NewRect("Call", _dayEndPanel);   // one group carries both lines
            Stretch(_lastCallRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _lastCallCard.transform.SetParent(_lastCallRt, false);
            calledUnder.transform.SetParent(_lastCallRt, false);
            _lastCallGroup = _lastCallRt.gameObject.AddComponent<CanvasGroup>();
            _lastCallGroup.blocksRaycasts = false;
            _lastCallRt.gameObject.SetActive(false);

            // Left column: the till slip (v5 P13). Cream stock, and pinned to 16pt — a whole
            // multiple of the face's 8px design size, so the monospace columns the receipt is
            // set in actually land on the pixel grid instead of blurring between it.
            // THE SLIP, CENTRED (2026-08-10). It hung from the top of the screen at a
            // fixed 470, so a quiet night left a hand's width of blank paper under the
            // total and the bill sat high on a screen that had room for it in the middle.
            // THE PAPER IS GENERATED NOW (2026-08-10, the author: the drawn sheet and its
            // marks read as filler). PixelLab painted a receipt torn off a roll — jagged at
            // both ends, warm, slightly handled — at 140x192, drawn here at a WHOLE 3x like
            // the licence, because pixel art magnifies only in whole steps. FIXED size, and
            // that is a receipt being honest: the paper is the roll's, the print is the
            // night's, and a quiet night leaves blank stock above the foot tear.
            var bill = _dayEndBill = NewRect("Bill", _dayEndPanel);
            Place(bill, new Vector2(0.5f, 0.5f), new Vector2(BillW, BillH), Vector2.zero);
            var sheet = bill.gameObject.AddComponent<Image>();
            sheet.sprite = ItemArt.Load("bill_sheet");
            if (sheet.sprite == null) { sheet.color = BillPaper; Frame(bill, 2f, BillEdge); }

            // The head is PRINTED, not banded: a receipt is one ink on one paper, and the
            // navy band belonged to the licence, not to a till roll.
            var headTitle = NewText("T", bill, _display, 24, TextAnchor.MiddleCenter, BillInk);
            Place(headTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(BillW - 60f, 26),
                new Vector2(0, -30f));
            headTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            headTitle.text = "LAST CALL";
            _billWhen = NewText("W", bill, _body, 16, TextAnchor.MiddleCenter, BillQuiet);
            Place(_billWhen.rectTransform, new Vector2(0.5f, 1), new Vector2(BillW - 60f, 20),
                new Vector2(0, -54f));
            _billWhen.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Every line lands in here, one rect to a row, so the columns cannot bend.
            _invoiceRows = NewRect("Rows", bill);
            _invoiceRows.anchorMin = new Vector2(0, 1); _invoiceRows.anchorMax = new Vector2(1, 1);
            _invoiceRows.pivot = new Vector2(0.5f, 1);
            _invoiceRows.sizeDelta = new Vector2(-BillInset * 2f, 0);
            _invoiceRows.anchoredPosition = new Vector2(0, -BillRowsTop);

            // The bill's OWN way forward (2026-08-07). The day-end button moved inside the
            // tablet, and the tablet is only up on the market step — which left the books
            // with no door out of them at all. The slip carries its own now.
            // ON THE SLIP'S FOOT, not at a fixed 530 down the panel: the bill is centred
            // and its length is the night's, so a fixed key sat on top of a short slip and
            // under a long one. RebuildDayEnd puts it where the paper actually ends.
            _billNext = NewRect("BillNext", _dayEndPanel);
            Place(_billNext, new Vector2(0.5f, 0.5f), new Vector2(BillW, 44), Vector2.zero);
            var billNextImg = _billNext.gameObject.AddComponent<Image>();
            billNextImg.color = UITheme.PrimaryAction;
            var billNextBtn = _billNext.gameObject.AddComponent<Button>();
            billNextBtn.targetGraphic = billNextImg;
            billNextBtn.onClick.AddListener(OnDayEndAdvance);
            // The one key on the slip, and it answered nothing until now — a full-width amber
            // slab that only changed when it was already pressed.
            var nextGlow = _billNext.gameObject.AddComponent<HoverGlow>();
            nextGlow.Graphics = new UnityEngine.UI.Graphic[] { billNextImg };
            nextGlow.Gain = 1.12f;     // amber at 1.22 goes to paper-white
            _billNextLabel = NewText("Label", _billNext, _display, 16, TextAnchor.MiddleCenter,
                UITheme.TextOnAmber);
            Stretch(_billNextLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-10, 0));
            _billNextLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _billNextLabel.text = "CONTINUE";

            // The slip is the night's money; these two are the night's PLACE — where it sits
            // in the week, and what it did to the bar. See the block above RebuildDayEnd.
            BuildNightBoards(_dayEndPanel);

            // THE DEVICE. 1096 x 700 wearing sh_ipad2, which is drawn at 274 x 175 — the
            // same ratio to five decimals — with a 28px border. A sliced Image draws its
            // border ring 1:1 whatever the rect, so the bezel lands at exactly the 28 the
            // page insets by and is even on all four sides. The old pairing put a 62px ring
            // measured off a grey mat onto a rect of a different aspect, which is the grey
            // slab with a dark edge on only two sides in the author's screenshot.
            var tablet = _dayEndTablet = NewRect("Tablet", _dayEndPanel);
            Place(tablet, new Vector2(0.5f, 0.5f), new Vector2(TabletW, TabletH), Vector2.zero);
            var tabletImg = tablet.gameObject.AddComponent<Image>();
            var shellArt = ItemArt.Load("sh_ipad2");
            if (shellArt != null)
            {
                tabletImg.sprite = shellArt;
                tabletImg.type = Image.Type.Sliced;
            }
            else tabletImg.color = TabletShell;

            // The two pieces of jewellery that make it a device, placed in CODE. In the art
            // they would sit in a stretched band and smear along it.
            var lens = NewRect("Lens", tablet);
            Place(lens, new Vector2(0.5f, 1), new Vector2(8, 8), new Vector2(0, -10));
            var lensImg = lens.gameObject.AddComponent<Image>();
            lensImg.color = TabletLens; lensImg.raycastTarget = false;
            var home = NewRect("Home", tablet);
            Place(home, new Vector2(0.5f, 0), new Vector2(120, 4), new Vector2(0, 10));
            var homeImg = home.gameObject.AddComponent<Image>();
            homeImg.color = new Color(0.42f, 0.42f, 0.46f, 1f); homeImg.raycastTarget = false;

            // THE PAGE — 1040 x 644 inside the bezel. Every band below adds up to it:
            //   20 + 40 + 32 + 8 + 400 + 8 + 128 + 8 = 644
            var screen = NewRect("Screen", tablet);
            Stretch(screen, Vector2.zero, Vector2.one,
                new Vector2(BezelX, BezelY), new Vector2(-BezelX, -BezelY));
            screen.gameObject.AddComponent<Image>().color = ShopPage;

            // THE DEVICE'S OWN STATUS BAR. It belongs to the tablet, not to the shop.
            var osBar = NewRect("OsBar", screen);
            osBar.anchorMin = new Vector2(0, 1); osBar.anchorMax = new Vector2(1, 1);
            osBar.pivot = new Vector2(0.5f, 1);
            osBar.sizeDelta = new Vector2(0, OsBarH);
            osBar.anchoredPosition = Vector2.zero;
            osBar.gameObject.AddComponent<Image>().color = new Color(0.867f, 0.878f, 0.918f, 1f);
            // The derived "02:{Day*7%60}" was never a clock — it was noise dressed as one.
            _osClock = NewText("OsClock", osBar, _body, 8, TextAnchor.MiddleLeft, ShopInk);
            Place(_osClock.rectTransform, new Vector2(0, 0.5f), new Vector2(120, 12), new Vector2(12, 0));
            var osCarrier = NewText("OsCarrier", osBar, _body, 8, TextAnchor.MiddleCenter, ShopInkSoft);
            Place(osCarrier.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200, 12), Vector2.zero);
            osCarrier.text = "TRADE NET";
            var osWifi = NewRect("OsWifi", osBar);
            Place(osWifi, new Vector2(1, 0.5f), new Vector2(20, 14), new Vector2(-52, 0));
            var wifiImg = osWifi.gameObject.AddComponent<Image>();
            wifiImg.sprite = ItemArt.Load("sh_wifi"); wifiImg.preserveAspect = true;
            wifiImg.raycastTarget = false;
            if (wifiImg.sprite == null) wifiImg.color = ShopInkSoft;
            var osBatt = NewRect("OsBatt", osBar);
            Place(osBatt, new Vector2(1, 0.5f), new Vector2(23, 9), new Vector2(-14, 0));
            var battImg = osBatt.gameObject.AddComponent<Image>();
            battImg.sprite = ItemArt.Load("sh_batt"); battImg.preserveAspect = true;
            battImg.raycastTarget = false;
            if (battImg.sprite == null) battImg.color = ShopInkSoft;

            // THE TITLE BAR (2026-08-19). This is the storefront's whole identity in one
            // 40-unit run: the vice fade, the mark and wordmark on it, and the money. It
            // replaced a white app bar with a green wordmark on it.
            //
            // The fade is `ChromeArt.FadeStrip` — one texel per band, drawn point-filtered,
            // so however wide the bar gets it comes back as FLAT runs with a hard edge
            // between them: twenty-six of 40 across this 1040 (the author asked the first
            // take's eight for "daha smooth", and more bands is how a band set smooths —
            // never interpolation, 16 §6.10). No sprite is stretched into a smear either:
            // the failure the old sh_bar had was that its middle carried MARKS, and a flat
            // run has no detail to smear.
            var strip = NewRect("Strip", screen);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, AppBarH);
            strip.anchoredPosition = new Vector2(0, -OsBarH);
            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.sprite = ChromeArt.FadeStrip();
            stripImg.color = Color.white;
            // The bar stands ON the window, so it closes with the frame's own dark edge.
            Hairline(strip, new Vector2(0, 0), new Vector2(1, 0), ShopViceDeep);

            // The mark: PALM CARGO's palm on its island, drawn in ChromeArt like every mark
            // (chrome is never generated) and authored at exactly this 28x24 — 1x, no
            // preserveAspect needed. White on the fade, the same footing as the wordmark, so
            // mark and name read as one printing.
            var mark = NewRect("Mark", strip);
            Place(mark, new Vector2(0, 0.5f), new Vector2(28, 24), new Vector2(10, 0));
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.sprite = ChromeArt.Isle();
            markImg.raycastTarget = false;

            // The wordmark IS the logo, and it is the one place the display face earns its
            // width. "BOOZE CRUISE" = 12 x 16 = 192 in a 200 box. It sits WHITE on the fade
            // now: on a title bar the name is the brightest thing, and the fade is dark
            // enough at both ends (ClubBlue[2] 4.9:1, Magenta[3] 4.6:1) to carry it.
            const float BrandX = 46f;
            var brand = NewText("Brand", strip, _display, 16, TextAnchor.MiddleLeft, Color.white);
            Place(brand.rectTransform, new Vector2(0, 0.5f), new Vector2(200, 20),
                new Vector2(BrandX, 0));
            brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            brand.text = ShopBrand;

            // NO WINDOW BOXES (2026-08-19). The bar had the era's three — min, max, close —
            // and the author sent them away: they were a misreading of "buttons like Windows
            // 98", which asked for the era's BUTTON STYLE, not its window furniture. Their
            // going is also honest UI: two of the three were drawn dead, and the live one
            // was a second door out of a market that already has its own key — the foot's
            // OPEN TOMORROW, which keeps the ClosingAsk guard. One exit, one key (16 §2).

            // The account, two rows so the word and the number cannot print through each
            // other — and cut as a SUNKEN well rather than wearing sh_k_account, which was
            // drawn for the green storefront and reads as a green plate on a pink bar.
            // A readout is a hole in the face, and the bevel is what says so (16 §1, GLASS).
            // Flush right at the boxes' old margin, since it is the bar's right-most thing.
            var balance = NewRect("Balance", strip);
            Place(balance, new Vector2(1, 0.5f), new Vector2(150, 32), new Vector2(-10, 0));
            var balanceImg = balance.gameObject.AddComponent<Image>();
            balanceImg.color = ShopViceDeep;
            Bevel(balance, 2f, raised: false);
            var balanceLabel = NewText("BalanceL", balance, _body, 8, TextAnchor.MiddleLeft,
                new Color(0.667f, 0.702f, 0.847f, 1f));   // 6.2:1 on the well
            Place(balanceLabel.rectTransform, new Vector2(0, 1), new Vector2(80, 10), new Vector2(10, -6));
            balanceLabel.text = "ACCOUNT";
            _tabletTill = NewText("Till", balance, _display, 16, TextAnchor.MiddleRight, Color.white);
            Place(_tabletTill.rectTransform, new Vector2(1, 0), new Vector2(126, 18), new Vector2(-10, 6));
            _tabletTill.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tabletTill.verticalOverflow = VerticalWrapMode.Truncate;
            _tillFloats = balance;   // the deltas fall out of the account box, below it
            // THE SHEET THE DEPARTMENT OPENS (the author: give it a filing feel, and let
            // the frame go round the products too). The aisle sits on a framed page whose
            // top edge runs UNDER the tabs: the lit tab is drawn with no bottom rim and
            // overlaps that edge by two units, so it does not sit beside the page, it is
            // attached to it. Four flat Images make the frame, which cannot distort at any
            // size — the thing the stretched sprites kept getting wrong.
            // THE SHEET IS THE AISLE'S OWN RECT (2026-08-11, the author: the box the
            // products are listed in and the area the listing is CUT at are not the same,
            // there is white between them). They were not: the page was a FIXED 416 tall
            // hanging from the tabs while the mask stretched to the foot, so the two only
            // ever agreed at one screen height — measured at 26 units of white below the
            // last row and 12 above the first, and the page running 2 units INTO the foot.
            //
            // It also means last night's gutter went to the wrong rect. The air belongs
            // between the PAGE and the foot, where it reads as a margin; putting it under
            // the mask just pushed the cut further up its own sheet.
            //
            // So there is one set of numbers now: the page hangs from the tabs and stops a
            // gutter clear of the foot, and the mask and the scroll track are inset inside
            // it by the same amount on every side.
            var page = NewRect("Page", screen);
            Stretch(page, Vector2.zero, Vector2.one,
                new Vector2(4f, FootH + FootY + AisleGutter),
                new Vector2(-4f, -(OsBarH + AppBarH + TabBarH - 4f)));
            page.gameObject.AddComponent<Image>().color = ShopPage;
            Frame(page, 2f, ShopViceDeep);
            // THE WALLPAPER (2026-08-19). A 90s trade site had one, and this one is the
            // room's own weather: a banded sun, a grid running back to it, two palms.
            // Drawn in code (`ChromeArt.PalmWall`) because chrome is never generated here.
            //
            // It is hung at 13% of the house blue and it has to stay there. The page's one
            // job is that forty products can be read on it, and the wallpaper is allowed to
            // be the thing you notice SECOND — the moment it competes with a bottle
            // silhouette it has stopped being a wall and started being noise. Measured on
            // the palest tile (PlateSealed): the type still clears 11:1 over it.
            HangWall(page, 1032, 390, new Color(ShopVice.r, ShopVice.g, ShopVice.b, 0.13f));

            // THE DEPARTMENT BAR, across the top. It used to be a 230-wide rail down the
            // left; demolishing it hands 274 units back to the aisle, which is the
            // difference between eight products on screen and twelve.
            var tabBar = NewRect("TabBar", screen);
            tabBar.anchorMin = new Vector2(0, 1); tabBar.anchorMax = new Vector2(1, 1);
            tabBar.pivot = new Vector2(0.5f, 1);
            tabBar.sizeDelta = new Vector2(0, TabBarH);
            tabBar.anchoredPosition = new Vector2(0, -(OsBarH + AppBarH));
            // No fill: the tabs stand on the page's own shoulder, and a band behind them
            // would draw a second horizontal line right where the seam should be.
            var tabBarImg = tabBar.gameObject.AddComponent<Image>();
            tabBarImg.color = new Color(1f, 1f, 1f, 0f);
            tabBarImg.raycastTarget = false;

            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int tab = i;
                // DRAWN, NOT DRAWN-ON (2026-08-10, the author's file brief). A sprite
                // cannot do the one thing a file tab has to do — carry a seam under the
                // tabs you are not reading and NO seam under the one you are — so the tabs
                // are rects with the page's own 2px outline, and the COLOUR does the work:
                //
                //   resting  face fill + the frame's own dark outline on all four sides,
                //            so its bottom edge is a visible seam lying on the page's border
                //   open     that dark colour as the FILL, so its outline vanishes into it
                //            and its bottom edge merges with the page's border — no seam
                //
                // The colour changed to the vice frame's ClubBlue[0] (2026-08-19) and the
                // trick did not: it never depended on the hue, only on the open tab's fill
                // and the page's border being the SAME colour. If those two ever drift
                // apart the seam comes back and the file stops being a file.
                //
                // Both draw at 2 units, the page's own thickness, on the page's own plane,
                // and the open one stands taller. Nothing is stretched, so nothing smears.
                var key = NewRect($"Tab{i}", tabBar);
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(0, 0);
                key.sizeDelta = new Vector2(TabKeyW, TabRestH);
                key.anchoredPosition = new Vector2(8f + i * (TabKeyW + 8f), 0);
                var bg = key.gameObject.AddComponent<Image>();
                // RAISED, not framed (2026-08-19): a 98 site's tab is a button that stands
                // off the page, and the whole storefront speaks that button now. The lit
                // magenta edge stays — the bevel says "pressable", the edge says "open".
                Bevel(key, 2f, raised: true);
                // The lit top edge, shown only on the open tab (RefreshDayEnd switches it).
                var lit = NewRect("Lit", key);
                lit.anchorMin = new Vector2(0, 1); lit.anchorMax = new Vector2(1, 1);
                lit.pivot = new Vector2(0.5f, 1);
                lit.sizeDelta = new Vector2(-6f, 3f);
                lit.anchoredPosition = new Vector2(0, -2f);
                var litImg = lit.gameObject.AddComponent<Image>();
                litImg.color = ShopTabLit; litImg.raycastTarget = false;
                _shopTabLits[i] = litImg;
                var btn = key.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                // HoverWarm and not HoverGlow: this is the market, and the market's tabs are
                // repainted whenever one of them opens. Warmth captured on enter and put back
                // on exit is what that page has always used (MarkHoverable), and using the
                // room's multiplier here would be a second dialect on one screen.
                MarkHoverable(key, bg);
                btn.onClick.AddListener(() =>
                {
                    if (_shopTab != tab)
                    {
                        _justOrdered.Clear(); _shopScrollAt = 1f;
                        Sfx.Play("key_press", 0.55f);
                    }
                    _shopTab = tab;
                    RebuildDayEnd();
                });
                // AT ITS OWN SIZE. The icons are cut on a 24 canvas and were drawn into a
                // 20 box: 0.833x, a fractional shrink of pixel art, which rounds some rows
                // away and keeps others. 24 in a 30-tall key is 1:1 and has room to breathe.
                var icon = NewRect("I", key);
                Place(icon, new Vector2(0, 0.5f), new Vector2(24, 24), new Vector2(8, 0));
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.sprite = ItemArt.Load(ShopTabIcons[i]);
                iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                if (iconImg.sprite == null) iconImg.color = new Color(1, 1, 1, 0);
                _shopTabIcons[i] = iconImg;
                var label = NewText("L", key, _shop, 16, TextAnchor.MiddleLeft, ShopInk);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(40, 0), new Vector2(-6, 0));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = ShopTabs[i];
                _shopTabKeys[i] = bg;
                _shopTabLabels[i] = label;
                MarkHoverable(key, bg);
            }

            // Tonight's fitting, said ONCE, at the right of the department bar — it used to
            // be repeated in five places and still be missed.
            var lamp = NewRect("Lamp", tabBar);
            Place(lamp, new Vector2(1, 0.5f), new Vector2(16, 16), new Vector2(-152, 0));
            var lampImg = lamp.gameObject.AddComponent<Image>();
            lampImg.sprite = ItemArt.Load("sh_b_lock");
            lampImg.preserveAspect = true; lampImg.raycastTarget = false;
            _fittingLamp = lampImg;
            _fittingNote = NewText("Fitting", tabBar, _body, 8, TextAnchor.MiddleRight, ShopInk);
            Place(_fittingNote.rectTransform, new Vector2(1, 0.5f), new Vector2(130, 12),
                new Vector2(-12, 0));
            _fittingNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _fittingNote.verticalOverflow = VerticalWrapMode.Truncate;

            // THE AISLE. 1004 wide at x 8..1012; the mask cuts at 1012 and the scroll track
            // starts at 1022, so a 6-column grid of 1000 has 4 units of slack inside the
            // mask and 14 units of air before the bar. The old grid asked for 790 in a 730
            // viewport and lost a third of every fourth card behind the scrollbar.
            // A GUTTER UNDER THE AISLE (2026-08-11, the author: there is no gap at all
            // between the shelf and the boxes under it, and it does not look professional).
            // There were eight units — sixteen screen pixels — between the mask's foot and
            // the inspector's head, which at a glance is the two panels touching. Three
            // times that is the difference between a page with a foot and a page that ran
            // out of room.
            var offerView = NewRect("OfferView", screen);
            Stretch(offerView, Vector2.zero, Vector2.one,
                new Vector2(8f, FootH + FootY + AisleGutter + PageInset),
                new Vector2(-(BarW + 18f), -(OsBarH + AppBarH + TabBarH + 8f)));
            offerView.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.003f);
            offerView.gameObject.AddComponent<RectMask2D>();
            _offerRow = NewRect("Offers", offerView);
            _offerRow.anchorMin = new Vector2(0, 1); _offerRow.anchorMax = Vector2.one;
            _offerRow.pivot = new Vector2(0.5f, 1);
            _offerRow.offsetMin = Vector2.zero; _offerRow.offsetMax = Vector2.zero;
            var shopLayout = _offerRow.gameObject.AddComponent<VerticalLayoutGroup>();
            shopLayout.spacing = 8;
            shopLayout.childControlWidth = true; shopLayout.childForceExpandWidth = true;
            shopLayout.childControlHeight = true; shopLayout.childForceExpandHeight = false;
            var shopFit = _offerRow.gameObject.AddComponent<ContentSizeFitter>();
            shopFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var shopScroll = _shopScroll = offerView.gameObject.AddComponent<ScrollRect>();
            shopScroll.viewport = offerView; shopScroll.content = _offerRow;
            shopScroll.horizontal = false;
            shopScroll.movementType = ScrollRect.MovementType.Clamped;
            // One notch of the wheel used to throw the aisle most of a screen (110 was set
            // when the author asked for a faster page and it overshot by twenty). A notch
            // should move the shelf, not the department.
            shopScroll.scrollSensitivity = 5.5f;
            shopScroll.inertia = false;

            var barTrack = NewRect("ScrollTrack", screen);
            barTrack.anchorMin = new Vector2(1, 0); barTrack.anchorMax = new Vector2(1, 1);
            barTrack.pivot = new Vector2(1, 1);
            barTrack.sizeDelta = new Vector2(BarW,
                -(OsBarH + AppBarH + TabBarH + 8f + FootH + FootY + AisleGutter + PageInset));
            barTrack.anchoredPosition = new Vector2(-8f, -(OsBarH + AppBarH + TabBarH + 8f));
            barTrack.gameObject.AddComponent<Image>().color = ShopAisle;
            Bevel(barTrack, 1f, raised: false);   // a trough is cut INTO the face
            var scrollbar = barTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = NewRect("Handle", barTrack);
            Stretch(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = ShopVice;
            Bevel(handle, 2f, raised: true);      // and the thumb stands out of it
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handle;
            shopScroll.verticalScrollbar = scrollbar;
            shopScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // THE FOOT: 8 + 640 + 8 + 232 + 8 + 136 + 8 = 1040.
            var foot = NewRect("Foot", screen);
            foot.anchorMin = new Vector2(0, 0); foot.anchorMax = new Vector2(1, 0);
            foot.pivot = new Vector2(0.5f, 0);
            foot.sizeDelta = new Vector2(0, FootH);
            foot.anchoredPosition = new Vector2(0, FootY);

            // THE READING CARD, ON THE POINTER (2026-08-11, the author). The description
            // used to be a 560-wide dark slab bolted into the foot — two thirds of the bar
            // spent on a sentence you read once, while the basket it sat beside had 312
            // units to list a night's shopping in. A description belongs to the thing the
            // pointer is on, so it goes to the pointer, and the foot goes to the basket.
            //
            // Built on the day-end PANEL rather than the foot: it has to be able to sit
            // over the aisle it is describing, and a child of the foot would be clipped to
            // it. Nothing in it may take a raycast — the panel sits under the cursor, and a
            // graphic that answers the pointer reads as leaving the tile underneath, which
            // hides the panel, which hands the pointer back, many times a second.
            _shopCard = NewRect("ShopCard", _dayEndPanel);
            Place(_shopCard, new Vector2(0.5f, 0.5f), new Vector2(ShopCardW, 132), Vector2.zero);
            _shopCard.pivot = new Vector2(0, 1);
            var cardBg = _shopCard.gameObject.AddComponent<Image>();
            cardBg.color = InspectorBack;
            var cardEdge = new Color(ShopViceLit.r, ShopViceLit.g, ShopViceLit.b, 0.85f);
            Hairline(_shopCard, new Vector2(0, 0), new Vector2(1, 0), cardEdge);
            Hairline(_shopCard, new Vector2(0, 1), new Vector2(1, 1), cardEdge);
            HairlineV(_shopCard, 0f, cardEdge);
            HairlineV(_shopCard, 1f, cardEdge);

            // ONE TEXT COLUMN AND ONE ICON GUTTER (the author: align it, and use the
            // space). The icons keep a gutter of their own and EVERY text starts beside it.
            const float CardGutter = 10f, CardText = 34f;
            const float CardCol = ShopCardW - CardText - 10f;

            // The heaviest ink on the card: white, in the shop's bold face. Everything
            // else here is specification; this is the product.
            _cardIdentity = NewText("Identity", _shopCard, _shop, 16, TextAnchor.UpperLeft, Color.white);
            Place(_cardIdentity.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 20),
                new Vector2(CardText, -8));
            _cardIdentity.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardIdentity.verticalOverflow = VerticalWrapMode.Overflow;

            // Sizes UP from 8 (the author: the market's small print was unreadable). The
            // card is narrower than the slab was, so every line wraps sooner — and the card
            // grows down to fit rather than truncating, which is what the fixed slab did.
            _cardMeta = NewText("CardMeta", _shopCard, _body, 10, TextAnchor.UpperLeft, InspectorDim);
            Place(_cardMeta.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 14),
                new Vector2(CardText, -30));
            _cardMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardMeta.verticalOverflow = VerticalWrapMode.Overflow;

            // A rule under the head, so the identity block and the description read as two
            // things rather than five loose lines on a slate.
            _shopCardRule = NewRect("Rule", _shopCard);
            Place(_shopCardRule, new Vector2(0, 1), new Vector2(ShopCardW - 20f, 1),
                new Vector2(CardGutter, -48));
            var ruleImg = _shopCardRule.gameObject.AddComponent<Image>();
            ruleImg.color = UITheme.ClubBlue[1];
            ruleImg.raycastTarget = false;

            // The product's own mark, in the gutter beside the identity.
            var cardMark = NewRect("Mark", _shopCard);
            Place(cardMark, new Vector2(0, 1), new Vector2(20, 20), new Vector2(CardGutter, -8));
            _cardMarkImg = cardMark.gameObject.AddComponent<Image>();
            _cardMarkImg.preserveAspect = true;
            _cardMarkImg.raycastTarget = false;

            _cardBody = NewText("CardBody", _shopCard, _body, 10, TextAnchor.UpperLeft, InspectorInk);
            Place(_cardBody.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 40),
                new Vector2(CardText, -54));
            _cardBody.supportRichText = true;
            _cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cardBody.verticalOverflow = VerticalWrapMode.Overflow;

            for (int i = 0; i < 2; i++)
            {
                var icon = NewRect("BuffI" + i, _shopCard);
                Place(icon, new Vector2(0, 1), new Vector2(14, 14), new Vector2(CardGutter, -100f));
                var ii = icon.gameObject.AddComponent<Image>();
                ii.preserveAspect = true; ii.raycastTarget = false;
                var line = NewText("Buff" + i, _shopCard, _body, 10, TextAnchor.UpperLeft, InspectorInk);
                Place(line.rectTransform, new Vector2(0, 1), new Vector2(CardCol, 14),
                    new Vector2(CardText, -100f));
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.verticalOverflow = VerticalWrapMode.Overflow;
                if (i == 0) { _cardBuffAIcon = ii; _cardBuffA = line; }
                else { _cardBuffBIcon = ii; _cardBuffB = line; }
            }
            _shopCard.gameObject.SetActive(false);

            // THE POINTER GETS THE RECIPE (2026-08-10, the author). The inspector says what
            // a drink IS in a sentence; buying one is a question about the POUR, and a pour
            // is a table. The panel rides the cursor the way the licence's does, and for the
            // same reason: parked anywhere fixed it either covers the tile you are reading
            // or sits too far from it to belong to it.
            _shopSpec = NewRect("ShopSpec", _dayEndPanel);
            Place(_shopSpec, new Vector2(0.5f, 0.5f), new Vector2(ShopSpecW, 120), Vector2.zero);
            _shopSpec.pivot = new Vector2(0, 1);
            var specBg = _shopSpec.gameObject.AddComponent<Image>();
            specBg.color = new Color(InspectorBack.r, InspectorBack.g, InspectorBack.b, 0.97f);
            specBg.raycastTarget = false;
            var specEdge = new Color(ShopViceLit.r, ShopViceLit.g, ShopViceLit.b, 0.85f);
            Hairline(_shopSpec, new Vector2(0, 0), new Vector2(1, 0), specEdge);
            Hairline(_shopSpec, new Vector2(0, 1), new Vector2(1, 1), specEdge);
            HairlineV(_shopSpec, 0f, specEdge);
            HairlineV(_shopSpec, 1f, specEdge);
            _shopSpecBody = NewRect("Body", _shopSpec);
            Stretch(_shopSpecBody, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            _shopSpec.gameObject.SetActive(false);

            // THE BASKET IS THE FOOT (2026-08-11, the author: "alt barı tamamen sepet olarak
            // kullanırız ... böylece çok ürün sepete eklenince gözükmeme problemini
            // kaldırırız"). 880 units instead of 312, and what is in it is DRAWN rather than
            // listed: the four names at 8 with "+2 more" under them were both unreadable and
            // a lie about how much the basket holds.
            var order = NewRect("Basket", foot);
            Place(order, new Vector2(0, 0.5f), new Vector2(BasketW, FootH), new Vector2(8, 0));
            order.gameObject.AddComponent<Image>().color = ShopPage;
            // The status panel of a 98 window, and it stands OUT of the frame — the basket
            // is the one surface on this page you commit from, so it is the one that is
            // raised rather than cut in.
            Bevel(order, 2f, raised: true);

            // THE HEAD BAND carries the whole control: what is in the basket, what it comes
            // to, and the key that buys it — one row, all at a size that reads.
            var orderHead = NewRect("BasketHead", order);
            Place(orderHead, new Vector2(0, 1), new Vector2(BasketW, 30), Vector2.zero);
            orderHead.gameObject.AddComponent<Image>().color = ShopViceDeep;
            var orderIcon = NewRect("BasketI", orderHead);
            Place(orderIcon, new Vector2(0, 0.5f), new Vector2(20, 20), new Vector2(10, 0));
            var orderIconImg = orderIcon.gameObject.AddComponent<Image>();
            orderIconImg.sprite = ItemArt.Load("sh_i_cart");
            orderIconImg.preserveAspect = true; orderIconImg.raycastTarget = false;
            _cartHeadLabel = NewText("BasketHL", orderHead, _shop, 16, TextAnchor.MiddleLeft, Color.white);
            Place(_cartHeadLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(220, 20),
                new Vector2(38, 0));
            _cartHeadLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cartHeadLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _cartHeadLabel.text = "BASKET";

            // TWO NUMBERS, RIGHT TO LEFT: what the order comes to, and what the till has
            // left after it. The band used to carry the total alone beside a PLACE ORDER
            // key; that key and the way out were merged into ONE (2026-09-04), which freed
            // the whole right end — and the author asked for the second reading to live in
            // it ("sepete ürün eklendiğinde kalan bakiyeyi göstermeli"), because the top bar
            // says what the till HOLDS and the basket said what the order COSTS and nobody
            // should be doing that subtraction in their head at the till.
            //
            // The columns are hand-set from the band's right edge and they do not move:
            //   -10 … -130  the total          -138 … -198  "TOTAL"
            //  -206 … -326  what is left       -334 … -464  "LEFT IN THE TILL"
            // 464 units in a band 808 wide, and BASKET (n) ends 550 from the right.
            _cartTotalLabel = NewText("TotalL", orderHead, _shop, 8, TextAnchor.MiddleRight, ShopPaper);
            Place(_cartTotalLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(60, 12),
                new Vector2(-138f, 0));
            _cartTotalLabel.text = "TOTAL";
            _cartTotal = NewText("BasketTotal", orderHead, _display, 16, TextAnchor.MiddleRight,
                Color.white);
            Place(_cartTotal.rectTransform, new Vector2(1, 0.5f), new Vector2(120, 20),
                new Vector2(-10f, 0));
            _cartTotal.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cartTotal.verticalOverflow = VerticalWrapMode.Overflow;

            _cartLeftLabel = NewText("LeftL", orderHead, _shop, 8, TextAnchor.MiddleRight, ShopPaper);
            Place(_cartLeftLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(130, 12),
                new Vector2(-334f, 0));
            _cartLeftLabel.text = "LEFT IN THE TILL";
            _cartLeft = NewText("BasketLeft", orderHead, _display, 16, TextAnchor.MiddleRight,
                Color.white);
            Place(_cartLeft.rectTransform, new Vector2(1, 0.5f), new Vector2(120, 20),
                new Vector2(-206f, 0));
            _cartLeft.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cartLeft.verticalOverflow = VerticalWrapMode.Overflow;

            // THE ROW OF WHAT IS PICKED. Chips are built into it on every rebuild.
            _cartChips = NewRect("BasketChips", order);
            Place(_cartChips, new Vector2(0, 1), new Vector2(BasketW - 20f, FootH - 38f),
                new Vector2(10, -34));
            _cartEmpty = NewText("BasketEmpty", order, _body, 12, TextAnchor.MiddleLeft, ShopInkSoft);
            Place(_cartEmpty.rectTransform, new Vector2(0, 1), new Vector2(BasketW - 20f, 40),
                new Vector2(12, -46));
            _cartEmpty.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cartEmpty.verticalOverflow = VerticalWrapMode.Overflow;
            _cartEmpty.text = ShopIdleTip;

            // ONE KEY, NOT TWO (2026-09-04, the author: "satın al butonu ve güne geç butonu
            // yerine ... 2 butonu 1 buton yapıyoruz"). The market asked two questions from
            // two corners — PLACE ORDER in the basket's head band, OPEN TOMORROW down here —
            // and only ever one of them had an answer: with an empty basket the order key
            // said NOTHING PICKED, and with a full one the way out threw the picks away and
            // had to ask whether you meant it. So it is ONE key in one place: it BUYS while
            // there is something to buy, and it opens tomorrow once there is not. Emptying
            // the basket is how you get past it without spending — the chips do that, and
            // Escape still walks the same guarded door.
            //
            // Bottom right of the device, and since the title bar lost its close box
            // (2026-08-19) the only way out, which is why it is the biggest key on the
            // device. The 98 face replaced sh_k_exit's baked bevel for the same reason the
            // checkout dropped sh_k_order: one button language per site.
            //
            // THE LAMP CAME WITH THE ORDER KEY (2026-08-14, the author: "satın alma butonu
            // biraz daha dikkat edici olmalı"). It burns only when there is an order to
            // place, so the eye is pulled to the key exactly when spending is the right
            // thing to do and never as decoration (GDD 16 §5, §6) — which is also what
            // tells the key's two faces apart without a second control.
            _marketKeyLamp = NewRect("Lamp", foot);
            Place(_marketKeyLamp, new Vector2(0, 0.5f), new Vector2(ExitW + 16f, FootH + 24f),
                new Vector2(BasketW + 8f, 0));
            _marketKeyLampImg = _marketKeyLamp.gameObject.AddComponent<Image>();
            _marketKeyLampImg.sprite = ChromeArt.LampGlow();
            _marketKeyLampImg.raycastTarget = false;
            _marketKeyLampImg.color = new Color(1f, 1f, 1f, 0f);

            _marketKey = NewRect("OpenTomorrow", foot);
            Place(_marketKey, new Vector2(0, 0.5f), new Vector2(ExitW, FootH), new Vector2(BasketW + 16f, 0));
            var otImg = _marketKeyImg = _marketKey.gameObject.AddComponent<Image>();
            otImg.sprite = ChromeArt.Win98Key();
            otImg.type = Image.Type.Sliced;
            otImg.color = MarketKeyNight;
            var otBtn2 = _marketKey.gameObject.AddComponent<Button>();
            otBtn2.targetGraphic = otImg;
            otBtn2.onClick.AddListener(OnMarketKey);
            MarkHoverable(_marketKey, otImg);
            // 24, not 16 (2026-09-04). The pixel faces only rasterise cleanly at whole
            // multiples of their 8px design size, so the step from 16 is to 24 — and a
            // 208-wide key holds it: TOMORROW is the longest word on either face at 8
            // characters, which sets 150 of the 196 the caption has.
            _marketKeyLabel = NewText("Label", _marketKey, _shop, 24, TextAnchor.MiddleCenter,
                MarketKeyNightInk);
            Stretch(_marketKeyLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, 0), new Vector2(-4, 0));
            _marketKeyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _marketKeyLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _marketKeyLabel.text = "OPEN\nTOMORROW";
            var otPress = _marketKey.gameObject.AddComponent<Win98Press>();
            otPress.Face = otImg;
            otPress.Caption = _marketKeyLabel.rectTransform;

            BuildClosingAsk(_dayEndTablet);
            BuildHostNote(_dayEndTablet);

            _dayEndPanel.gameObject.SetActive(false);

            _bannerText = NewText("Closed", root, _display, 22, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 120), new Vector2(0, 60));
            _bannerText.gameObject.SetActive(false);

            BuildLedgerPanel(root);
            BuildGuide(root);
            BuildDevBench(root);
        }

        // ── tiny UI helpers (mirroring the house style) ─────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = NewRect(name, parent);
            Stretch(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        /// <summary>A key with a word on it — or, when <paramref name="icon"/> is handed one,
        /// with a DRAWN mark on it instead. A button too small for its word used to borrow a
        /// glyph from the font (the cog was a "⚙"), which no pixel face carries: it arrived
        /// from whatever fallback the system had, at a weight belonging to no other control
        /// on the screen.</summary>
        private RectTransform NewButton(RectTransform parent, string label, Vector2 anchor,
            Vector2 size, Vector2 pos, Color fill, Action onClick, Sprite icon = null)
        {
            var rt = NewRect(label, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var button = rt.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            // A face of its own, so the hover lift moves the label with the plate rather than
            // sliding the plate out from under it (PressSink moves one transform).
            var face = NewRect("Face", rt);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // THE ONE KEY (GDD 16 §2). This used to be a flat coloured rect — the third of
            // four button dialects the game was speaking at once.
            KeyPlate.Dress(rt, fill, button, face);

            Color ink = fill == UITheme.PrimaryAction ? UITheme.TextOnAmber : UITheme.TextPrimary;
            if (icon != null)
            {
                // A MARK IS SQUARE OR IT IS NOTHING (GDD 16 §3). Insetting the bottom by the
                // throw made the box 16x13, and `preserveAspect` then fitted the 16px drawing
                // into 13 — 0.813x, the exact defect this key was made to fix one commit ago.
                // The mark keeps its square and is LIFTED off the throw instead.
                var mark = NewRect("Mark", face);
                Stretch(mark, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
                mark.anchoredPosition += new Vector2(0, KeyPlate.Throw * 0.5f);
                var mi = mark.gameObject.AddComponent<Image>();
                mi.sprite = icon; mi.color = ink; mi.preserveAspect = true; mi.raycastTarget = false;
                return rt;
            }

            // 8 OR 16, NEVER 12. The pixel faces rasterise at whole multiples of their 8px
            // design size and nowhere else (CLAUDE.md), and every worded key in the game was
            // drawn at 12 — a size that face does not have — so it arrived softened on the
            // buttons the player presses all night. Found by `LastCall → Audit UI`.
            //
            // The size comes from the KEY, not from one global guess: a tall key is a primary
            // action and carries the bigger face, a short one would be crowded by it. 8 alone
            // left MENU — MAKE A DRINK as a whisper inside a 300-unit amber slab.
            int labelSize = rt.sizeDelta.y >= 32f ? 16 : 8;
            var text = NewText("Label", face, _body, labelSize, TextAnchor.MiddleCenter, ink);
            // Riding up off the throw, the way the market's key has always set its caption.
            Stretch(text.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(4, KeyPlate.Throw), new Vector2(-4, 0));
            text.text = label;
            return rt;
        }

        private Text NewText(string name, Transform parent, Font font, int size,
            TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Five of one icon at 16 px, the sockets always there and the lit ones
        /// under a mask whose width is the reading — the top bar's star row, at the small
        /// size, for the house's two symbols (GDD 27 §4.4).</summary>
        private RectTransform IconStrip(RectTransform parent, string name, Sprite socket, Sprite lit, float y)
        {
            var row = NewRect(name, parent);
            Place(row, new Vector2(0, 0.5f), new Vector2(HouseStripW, HouseIcon), new Vector2(0, y));
            row.pivot = new Vector2(0, 0.5f);
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("S" + i, row);
                cell.anchorMin = cell.anchorMax = new Vector2(0, 0.5f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.sizeDelta = new Vector2(HouseIcon, HouseIcon);
                cell.anchoredPosition = new Vector2(i * HouseGap + HouseGap * 0.5f, 0);
                var img = cell.gameObject.AddComponent<Image>();
                img.sprite = socket; img.preserveAspect = true; img.raycastTarget = false;
                if (socket == null) img.color = new Color(1f, 1f, 1f, 0.25f);
            }
            var fill = NewRect("Fill", row);
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1);
            fill.pivot = new Vector2(0, 0.5f);
            fill.sizeDelta = Vector2.zero;
            fill.anchoredPosition = Vector2.zero;
            fill.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < BarRating.MaxStars; i++)
            {
                var cell = NewRect("F" + i, fill);
                cell.anchorMin = cell.anchorMax = new Vector2(0, 0.5f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.sizeDelta = new Vector2(HouseIcon, HouseIcon);
                cell.anchoredPosition = new Vector2(i * HouseGap + HouseGap * 0.5f, 0);
                var img = cell.gameObject.AddComponent<Image>();
                img.sprite = lit; img.preserveAspect = true; img.raycastTarget = false;
                if (lit == null) img.color = UITheme.Amber[4];
            }
            return fill;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        /// <summary>
        /// A 98 BEVEL: four edges, lit top and left, shadowed right and bottom — or the
        /// other way round, which is the whole difference between a control that stands up
        /// and a well that is cut into the page (16 §1: "a bevel is four of them").
        ///
        /// It is built from rects and not from a sliced sprite on purpose. A bevel is the
        /// one piece of chrome whose thickness must NOT scale with the thing it is on: two
        /// units on a 1040-wide page and two units on a 60-wide key, or the window stops
        /// reading as one machine. Four flat Images cannot distort at any size.
        ///
        /// The corners overlap rather than mitre. At two units nobody can see a mitre, and
        /// every 16-colour toolkit this is quoting overlapped them too.
        /// </summary>
        private void Bevel(RectTransform parent, float t, bool raised)
            => Bevel(parent, t, raised ? BevelLit : BevelShade, raised ? BevelShade : BevelLit);

        private void Bevel(RectTransform parent, float t, Color topLeft, Color bottomRight)
        {
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                // i: 0 bottom, 1 top, 2 left, 3 right. Top and left take one colour.
                bool lit = i == 1 || i == 2;
                var rt = NewRect("Bev", parent);
                rt.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rt.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rt.pivot = new Vector2(horizontal ? 0.5f : i - 2, horizontal ? i : 0.5f);
                rt.sizeDelta = horizontal ? new Vector2(0, t) : new Vector2(t, 0);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.color = lit ? topLeft : bottomRight;
                img.raycastTarget = false;
            }
        }

        /// <summary>
        /// The wallpaper, hung under whatever is handed in. Faint on purpose: it is the
        /// thing you notice on the second look, and the aisle has to win the first one.
        /// Drawn at half size and stretched x2 (`ChromeArt.PalmWall`), so it stays pixel art.
        /// </summary>
        private void HangWall(RectTransform parent, int w, int h, Color tint)
        {
            var rt = NewRect("Wall", parent);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ChromeArt.PalmWall(w, h);
            img.color = tint;
            img.raycastTarget = false;
            rt.SetAsFirstSibling();   // under the aisle, not over it
        }

        /// <summary>A border on all four sides, t units thick. `Hairline` cannot do it —
        /// it hardcodes a 1-unit height — and a picked tile needs to be visible from six
        /// columns away, which one thin rule at the top was not.</summary>
        private void Frame(RectTransform parent, float t, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                var rt = NewRect("Edge", parent);
                bool horizontal = i < 2;
                rt.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rt.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rt.pivot = new Vector2(horizontal ? 0.5f : i - 2, horizontal ? i : 0.5f);
                rt.sizeDelta = horizontal ? new Vector2(0, t) : new Vector2(t, 0);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.color = c;
                img.raycastTarget = false;
            }
        }

        private static void PlaceProduct(RectTransform rt, Sprite s, float boxH)
        {
            // Fitted and stood by the DRAWING, not by the sheet it was saved on (VesselArt,
            // 2026-08-11). A product saved with air around it — the juice cartons are, on a
            // 96x168 sheet — used to spend the box on that air: it drew a third small and then
            // hovered above the shelf line every other product on the page stands on. For art
            // that fills its sheet, which is most of it, this is the same arithmetic as before.
            var m = VesselArt.Of(s);
            float w = s.rect.width, h = s.rect.height;
            float k = Mathf.Min(ContentW / m.Drawing.width, boxH / m.Drawing.height);
            if (k >= 1f) k = Mathf.Floor(k);
            Place(rt, new Vector2(0.5f, 0f), new Vector2(w * k, h * k),
                new Vector2((w * 0.5f - m.Drawing.center.x) * k, ProductFootY - m.Drawing.y * k));
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max,
            Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
