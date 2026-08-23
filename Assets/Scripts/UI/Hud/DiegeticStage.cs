using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The diegetic gameplay stage: the painted night-club room authored at 640×360 that
    /// sits BEHIND the UI overlay. What lives here today is exactly what the player sees —
    /// the room art with its lettered neon sign, the bar counter, and the clickable till
    /// with the wallet in its display window. Everything interactive above it (seats,
    /// patrons, glasses, menus) belongs to <see cref="TycoonHud"/> and the service flow.
    ///
    /// IN THE WORLD NOW, NOT ON A CANVAS (2026-08-10). The room and the counter draw as
    /// world-space SpriteRenderers under the URP 2D Renderer, because that is the one
    /// place a Light2D can reach them: an overlay canvas is composited after the camera
    /// and no light or bloom can ever touch it. The room carries real light — a global
    /// wash, a warm pool under each of the four painted lamps, and the sign's own spill —
    /// and the modular fixtures (bar upgrades) will be world sprites lit by the same
    /// system. What must draw OVER the HUD's patrons stays canvas: the till (order 6),
    /// its shadow and the wallet plaque (−10), and the lettered sign (−9), whose pixel
    /// text needs the canvas rasterizer.
    ///
    /// One world unit is one stage unit, and the PixelPerfectCamera draws it at a WHOLE
    /// number of screen pixels — two at a 1052px window, three at 1440p. How much room
    /// that shows follows from the window: 526 units of height at 1052px, not 360, with
    /// the backdrop, the picture and the counter all bleeding out to fill it. Spare window
    /// is spare bar; there are no black bars, and the camera is never cropped.
    ///
    /// The overlay canvases are pinned to that same whole number (see
    /// <see cref="DesignFrame"/>) rather than scaling smoothly off the height, which is
    /// what they used to do and is what made the props slide on the counter: the world
    /// drew at 2× while the UI drew at 2.92×. All public coordinates remain in the
    /// 640×360 reference space with a bottom-left origin.
    /// </summary>
    public sealed class DiegeticStage : MonoBehaviour
    {
        // ── layout (stage units, bottom-left origin) ────────────────────────────
        private static readonly Vector2 Reference = new Vector2(640, 360);
        private const float RegisterX = 604f;              // till pushed to the counter's right edge

        /// <summary>
        /// The bar's FRONT EDGE in stage units — the brass line a customer leans on, and the
        /// line their body is cut off at. Public because TycoonHud draws the seats and has to
        /// agree with it exactly; it used to keep its own copy of the number and the two drifted
        /// apart when the art changed.
        ///
        /// This is the rest line, NOT the sprite's top: the counter art carries two transparent
        /// rows above its brass edge, so cutting the bodies at the sprite's top left a two-unit
        /// sliver of backdrop showing under every customer — measured at 8 screen pixels on a
        /// 1440p frame (2026-07-29).
        /// </summary>
        public const float CounterTopY = CounterRestY;

        // ── the bar front's shelf compartments ───────────────────────────────────
        // The counter art is not decoration: it carries EIGHT compartments across its
        // front, and those are where the bought glassware belongs. RE-MEASURED off the
        // 2026-08-18 marble-and-graphite counter (14 v3 §11.F: new background, new
        // measurement) by sampling the cabinet band's column profile.
        //
        // THE CELLS ARE NO LONGER EVENLY SPACED, and that is the whole reason this is a
        // table now. The old turquoise bar front was eight identical 80-wide bays, so a
        // pitch was enough. The new front is joinery — two cabinet doors, three glass
        // fridges, three open bays — and a pitch would stand a glass squarely on a stile.
        //
        // RE-MEASURED AGAIN for the 2026-08-19 brutalist counter, and THIS TABLE IS ART-BOUND:
        // it is measured off whatever counter.png currently is, and a new counter invalidates
        // it silently — a stale table stands a bought glass on a steel stile. Re-measure by
        // running the column-edge scan and then LOOKING at the eight ticks drawn on the plate.
        //
        // MEASURED ON THE BAR AS DRAWN, not on the sprite: the counter is 9-sliced and tiled
        // out to 807 art px (see CounterMiddleTiles), so the sprite's own x is not where a
        // thing ends up. Working in drawn space is also what keeps every cell ON SCREEN —
        // the first pass mapped the sprite's table through the slice and put cells 1 and 8
        // outside the frame, where a bought glass is drawn and never seen.
        //
        // What is visible of the drawn bar, inside the screen's 83..722: one cabinet door
        // (91..139) and three glazed bays (168..330, 336..497, 503..681). The right-hand door
        // lands almost entirely off-frame — 20 px of it survive — so it carries nothing. The
        // eight cells are spread over the rest in proportion to width: the door takes one and
        // the three bays take two, two and three.
        private static readonly float[] ShelfCentrePx =
            { 115f, 208f, 290f, 376f, 457f, 533f, 592f, 651f };
        //
        // The numbers below are in the ART's own pixels. They only equal stage units at
        // the reference aspect: the counter is scaled by visibleWidth/640 and hangs from
        // the rest line, so at 16:10 the cells sit narrower AND higher. ShelfCell resolves
        // that from the live fit rather than assuming 16:9.
        //
        // The band a glass is drawn in. 124 is where it stands and 84 is the headroom above,
        // measured to sit inside the bays' own openings on the 2026-08-19 front — this
        // counter draws no shelf boards, so the band is a display line rather than a plank,
        // and it is checked the same way the columns are: by drawing it on the plate. The
        // band is 40 art px against the older front's 52, so the glassware run draws a little
        // smaller; that follows the art and is not a number to tune back up on its own.
        private const float ShelfFloorPx = 124f;    // the near edge, art px from the art's top
        /// <summary>How deep the drawn shelf surface is, front edge to back, in art px.</summary>
        public const float ShelfDepthPx = 9f;
        private const float ShelfCeilPx = 84f;      // the shelf board above it

        private Transform _counterTr;
        private Vector2 _counterNative;
        private float _counterScale;                // stage units per counter-art pixel

        // ── the cellar drawer (2026-08-22) ──────────────────────────────────────
        // The back bar is not a room you travel to any more; it is the counter's own body,
        // shut behind a roller. MEASURED off the two pieces rather than chosen: the shelf
        // opening is rows 65..241 of the cropped counter — 176 tall, which is the shutter's
        // height to the pixel, because the author drew the two to each other.
        private const float ShutterOpeningTopPx = 65f;   // art px below the counter's top edge
        /// <summary>How far the room rises to bring the cellar into frame. Read off the
        /// author's own mock-ups: the slab's dark band sits at screen row 240 shut and 119
        /// open, and nothing had to be invented to fill the gap because the counter hangs
        /// exactly this far below the screen.</summary>
        public const float DrawerTravel = 121f;

        /// <summary>
        /// WHERE THE BAR TOP IS WITH THE CELLAR OPEN, as a fraction up the screen — the line a
        /// bench has to close onto so it lands ON the counter instead of leaving a stripe of
        /// room showing above it (2026-08-22, the author: "açılan arkaplan ui'si backbar
        /// açıkken tam olarak tezgahın üstüne kapansın").
        ///
        /// DERIVED, not typed: the counter hangs from its own rest line, the drawer lifts the
        /// whole room by its own travel, and this is those two added.
        ///
        /// IT FOLLOWS THE DRAWER rather than assuming it open (2026-08-22). The shaker and the
        /// glass are only reachable through the cellar, so for them the drawer always is — but
        /// the DRAUGHT station's door is the font standing in the room, and the cellar behind
        /// it may well be shut. A fixed line would have landed that bench 121 px above its own
        /// counter, which is the same stripe of room the other two just stopped showing.
        /// </summary>
        public float BenchSurfaceFraction =>
            (CounterRestY + CounterSurfaceInset + DrawerTravel * _drawerT) / Reference.y;
        /// <summary>How far the roller drops to clear the opening. The author's mock-ups
        /// put its top at screen row 305 shut and 356 open while the room rose 121, so against
        /// the room it travels 356 - (305 - 121) = 172 — its own height, near enough, less the
        /// four pixels the open frame leaves showing at the sill. It goes DOWN, which is what
        /// the pink arrow drawn on it has been pointing at all along.</summary>
        private const float ShutterTravel = 172f;
        /// <summary>Where the cellar's one key sits, in stage units up from the screen's
        /// bottom. On the roller's face when it is down — the same band the pink arrow is
        /// drawn in — and it does not move when the room does.</summary>
        private const float CellarKeyY = 40f;
        private const float DrawerSeconds = 0.42f;
        private Transform _shutterTr;
        private Vector2 _shutterNative;
        private float _shutterRestLocalY;

        // ── where a bottle stands in the cellar ─────────────────────────────────
        // MEASURED on the installed counter (638x241), not chosen. The blue posts scan at
        // x 7-32, 209-226, 412-429 and 605-630, which leaves three bays; the two shelf
        // boards are 12 px thick at rows 138..149 and 228..239, so a bottle's foot sits on
        // the board's TOP row. Three bottles a bay is what the author's open mock-up shows.
        private static readonly float[] CellarBayCentrePx = { 120f, 319f, 517f };
        private const float CellarBayWidthPx = 175f;      // the narrowest of the three
        private static readonly float[] CellarShelfFootPx = { 138f, 228f };
        private const int CellarPerBay = 3;
        /// <summary>How many bottles the cellar can show at once.</summary>
        public const int CellarSlots = 3 * 2 * CellarPerBay;
        /// <summary>Drawn height of a bottle in the cellar. The shallower compartment is
        /// 138 - 65 = 73 px tall, so this leaves the stock clear of the board above it.</summary>
        private const float CellarBottleH = 62f;
        private readonly List<SpriteRenderer> _cellarStock = new List<SpriteRenderer>();
        private RectTransform _cellarDoorRoot;
        private CanvasGroup _cellarDoorGroup;
        private readonly List<RectTransform> _cellarDoors = new List<RectTransform>();
        private System.Action<int> _onCellarPick;
        private RectTransform _shutterDoor;
        private RectTransform _cellarCloseKey;
        private Image _cellarCloseArt;
        private Text _cellarKeyLabel;
        private IReadOnlyList<string> _cellarIds;
        private CanvasGroup[] _registerFade;

        /// <summary>
        /// Each door carries its bottle's id in its NAME. It costs nothing and it buys two
        /// things: a PlayMode test can ask for the vodka rather than for "slot 0", and a
        /// hierarchy full of CellarDoor0..17 stops being a puzzle the moment something is in
        /// the wrong bay. The same trick the fixtures use ("Fx_" + def.Id).
        /// </summary>
        private void NameCellarDoors(int n)
        {
            for (int i = 0; i < n && i < _cellarDoors.Count; i++)
            {
                string id = _cellarIds != null && i < _cellarIds.Count ? _cellarIds[i] : null;
                string want = string.IsNullOrEmpty(id) ? "CellarDoor" + i : "CellarDoor_" + id;
                if (_cellarDoors[i].name != want) _cellarDoors[i].name = want;
            }
        }

        /// <summary>Who to tell when a bottle in the cellar is picked, by its index in the
        /// list <see cref="SetCellar"/> was given.</summary>
        public void SetCellarHandler(System.Action<int> onPick) => _onCellarPick = onPick;
        private float _drawerT;                     // 0 shut, 1 open
        private float _drawerTarget;

        /// <summary>Is the cellar open, or on its way there?</summary>
        public bool DrawerOpen => _drawerTarget > 0.5f;
        /// <summary>0 shut, 1 open — for anything that has to fade with the drawer.</summary>
        public float DrawerPhase => _drawerT;

        /// <summary>
        /// Opens or shuts the counter's cellar. The whole room rides up: the author's two
        /// mock-ups differ by exactly <see cref="DrawerTravel"/> in every landmark, patrons
        /// included, so this moves the world root rather than the counter alone.
        /// </summary>
        public void SetDrawerOpen(bool open, bool instant = false)
        {
            _drawerTarget = open ? 1f : 0f;
            if (instant || Motion.Reduced) { _drawerT = _drawerTarget; ApplyDrawer(); }
        }

        /// <summary>
        /// What is standing in the counter's cellar. The stage is TOLD, the same way the till
        /// is told the money — it never reads the run. Anything past <see cref="CellarSlots"/>
        /// is not drawn, because there is no shelf for it to stand on.
        /// </summary>
        public void SetCellar(IReadOnlyList<Sprite> bottles, IReadOnlyList<string> ids = null)
        {
            int n = bottles == null ? 0 : Mathf.Min(bottles.Count, CellarSlots);
            _cellarIds = ids;
            while (_cellarStock.Count < n)
                _cellarStock.Add(WorldSprite("Stock" + _cellarStock.Count, null, order: 31));
            for (int i = 0; i < _cellarStock.Count; i++)
            {
                var sr = _cellarStock[i];
                bool on = i < n && bottles[i] != null;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (!on) continue;
                sr.sprite = bottles[i];
                PlaceCellarSlot(sr, i);
            }
            BuildCellarDoors(n);
            NameCellarDoors(n);
            LayOutCellarDoors();
        }

        /// <summary>
        /// One invisible hit plate per bottle, over the room — the SAME door the draught font
        /// keeps (BuildTapDoor), for the same reason: a world sprite cannot take a click, and
        /// a plate that swallows at the edges is a door the player learns not to trust.
        /// </summary>
        private void BuildCellarDoors(int n)
        {
            if (n > 0 && _cellarDoorRoot == null)
            {
                _cellarDoorRoot = OverlayCanvas("CellarDoors", 7, raycasts: true);
                _cellarDoorGroup = _cellarDoorRoot.gameObject.AddComponent<CanvasGroup>();
                UiAuditExempt.Mark(_cellarDoorRoot, "the cellar doors are hit plates over the "
                    + "stock standing in the counter, sized to each bottle's own slot");
            }
            BuildCellarCloseKey();
            while (_cellarDoors.Count < n)
            {
                int index = _cellarDoors.Count;
                var plate = NewRect("CellarDoor" + index, _cellarDoorRoot);   // renamed once fed
                plate.anchorMin = plate.anchorMax = new Vector2(0, 0);
                plate.pivot = new Vector2(0.5f, 0);
                var hit = plate.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                // THE CELLAR STAYS OPEN BEHIND YOU (2026-08-22, the author: "alkol yapma
                // sahnesinde arkada zaten backbar açık olacak"). It shut for one afternoon,
                // which meant every second bottle cost a full open-and-close of the room —
                // and a bar with the cellar shut behind the tin is a bar you left. The bench
                // slides in over it instead, and slides off it again.
                btn.onClick.AddListener(() => _onCellarPick?.Invoke(index));
                _cellarDoors.Add(plate);
            }
            for (int i = 0; i < _cellarDoors.Count; i++)
                if (_cellarDoors[i].gameObject.activeSelf != (i < n))
                    _cellarDoors[i].gameObject.SetActive(i < n);
        }

        /// <summary>
        /// Puts the doors where the bottles are. They ride the drawer the long way round: the
        /// stock is in the world and moves with it, the plates are on a fixed overlay, so the
        /// lift has to be added here by hand. THE DOORS ARE SHUT WHILE THE ROLLER IS —
        /// anything else lets a click reach through a closed shutter and take a bottle.
        /// </summary>
        private void LayOutCellarDoors()
        {
            if (_cellarDoorGroup != null)
                _cellarDoorGroup.blocksRaycasts = _drawerT > 0.99f;
            if (_cellarCloseKey != null)
            {
                // ONE KEY, ONE PLACE, TWO WORDS (2026-08-22, the author: "bunu kapağın üstüne
                // taşı, Shut It ile aynı yapıda olmalı ekranda aynı yerde kalmalı"). The verb
                // used to live on the HUD at the bottom of the screen and the way out lived on
                // the counter's slab, riding up with the room — two keys, two places, for what
                // is one door. This is that door: it sits ON THE LID, which is where the
                // author's own arrow is drawn, and it does NOT ride, so pressing it twice
                // presses the same pixels.
                if (!_cellarCloseKey.gameObject.activeSelf)
                    _cellarCloseKey.gameObject.SetActive(true);
                _cellarCloseKey.anchoredPosition = new Vector2(0f, CellarKeyY);
                if (_cellarKeyLabel != null)
                {
                    string word = _drawerT > 0.5f ? "SHUT IT" : "MENU — MAKE A DRINK";
                    if (_cellarKeyLabel.text != word) _cellarKeyLabel.text = word;
                }
            }
            if (_cellarDoors.Count == 0) return;
            float slotW = CellarBayWidthPx / CellarPerBay;
            float left = (Reference.x - _counterNative.x) * 0.5f;
            for (int i = 0; i < _cellarDoors.Count; i++)
            {
                if (!_cellarDoors[i].gameObject.activeSelf) continue;
                CellarSlotArt(i, out float artX, out float artFoot);
                _cellarDoors[i].sizeDelta = new Vector2(slotW - 4f, CellarBottleH);
                _cellarDoors[i].anchoredPosition = new Vector2(
                    left + artX,
                    CounterRestY + CounterSurfaceInset - artFoot + DrawerTravel * _drawerT);
            }
        }

        /// <summary>
        /// The roller's own door. THE AFFORDANCE WAS ALREADY DRAWN: the author put a pink
        /// arrow at the shutter's top centre pointing down, which is the way it travels, so
        /// the plate only has to make the picture answer. It rides the roller, which means
        /// the same plate shuts the cellar again from the sliver the open frame leaves at
        /// the sill — there is nothing else to click down there.
        /// </summary>
        private void BuildShutterDoor()
        {
            var root = OverlayCanvas("ShutterDoor", 6, raycasts: true);
            UiAuditExempt.Mark(root, "the shutter door is a hit plate over the roller in the "
                + "room, sized to the roller's own art");
            _shutterDoor = NewRect("ShutterDoor", root);
            _shutterDoor.anchorMin = _shutterDoor.anchorMax = new Vector2(0, 0);
            _shutterDoor.pivot = new Vector2(0.5f, 1f);        // hung by its top, like the art
            _shutterDoor.sizeDelta = _shutterNative;
            var hit = _shutterDoor.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var btn = _shutterDoor.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SetDrawerOpen(!DrawerOpen));
        }

        private void LayOutShutterDoor()
        {
            if (_shutterDoor == null) return;
            _shutterDoor.anchoredPosition = new Vector2(
                Reference.x * 0.5f,
                CounterRestY + CounterSurfaceInset - ShutterOpeningTopPx
                    + (DrawerTravel - ShutterTravel) * _drawerT);
        }

        /// <summary>
        /// THE WAY BACK OUT (2026-08-22, the author: "backbar açıldıktan sonra kapatılmıyor
        /// onun için başka buton ekle"). The roller shuts the cellar and its arrow is the
        /// diegetic way to do it — but once the drawer is open the roller has travelled to
        /// the sill and only a few pixels of it are left to aim at, which is not a door, it
        /// is a keyhole. This key sits on the counter's own slab, over the shelves, and is
        /// only there while the cellar is.
        ///
        /// It is drawn with the author's PINK KEY, nine-sliced: one drawing that fits any
        /// rectangle, which is the whole reason that art exists.
        /// </summary>
        private void BuildCellarCloseKey()
        {
            if (_cellarCloseKey != null || _cellarDoorRoot == null) return;
            _cellarCloseKey = NewRect("CellarKey", _cellarDoorRoot);
            _cellarCloseKey.anchorMin = _cellarCloseKey.anchorMax = new Vector2(0.5f, 0);
            _cellarCloseKey.pivot = new Vector2(0.5f, 0.5f);
            // Wide enough for the longer of its two captions; it carries both.
            _cellarCloseKey.sizeDelta = new Vector2(168, 30);
            var btn = _cellarCloseKey.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            // THE ONE KEY (GDD 16 §2), not a fifth dialect. A generated 132x143 plate was
            // tried here first and thrown out by the author ("çok düşük kalitede ve çok büyük
            // pixellerden"): its own nine-slice corners are 18 and 24, which do not fit in a
            // 30-tall button at all, so Unity squashed them. ChromeArt.Key is drawn at 20x20
            // and greyscale by construction, so it stays crisp at any size and takes the
            // making verb's own colour — which is how this key and that one are the SAME key.
            var face = NewRect("Face", _cellarCloseKey);
            Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _cellarCloseArt = KeyPlate.Dress(_cellarCloseKey, UITheme.MakeAction, btn, face);
            btn.onClick.AddListener(() => SetDrawerOpen(!DrawerOpen));

            var label = NewText("Label", face, _display, 8,
                                TextAnchor.MiddleCenter, UITheme.TextPrimary);
            // Inset along the bottom by the key's throw: a caption sitting on the throw
            // looks dropped (KeyPlate.Throw).
            Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(0, KeyPlate.Throw), Vector2.zero);
            label.raycastTarget = false;
            _cellarKeyLabel = label;
        }

        /// <summary>Slot i's foot, in the counter art's own pixels. One reading, so the
        /// drawn bottle and the plate that catches its click cannot disagree.</summary>
        private void CellarSlotArt(int i, out float artX, out float artFoot)
        {
            int perShelf = CellarBayCentrePx.Length * CellarPerBay;
            int shelf = i / perShelf, rest = i % perShelf;
            int bay = rest / CellarPerBay, pos = rest % CellarPerBay;
            float step = CellarBayWidthPx / CellarPerBay;
            artX = CellarBayCentrePx[bay] - CellarBayWidthPx * 0.5f + step * (pos + 0.5f);
            artFoot = CellarShelfFootPx[Mathf.Min(shelf, CellarShelfFootPx.Length - 1)];
        }

        /// <summary>Slot i, filled shelf by shelf and bay by bay, the way a bar restocks.</summary>
        private void PlaceCellarSlot(SpriteRenderer sr, int i)
        {
            CellarSlotArt(i, out float artX, out float artFoot);

            float h = sr.sprite.bounds.size.y;
            float k = h > 0.0001f ? CellarBottleH / h : 1f;
            sr.transform.localScale = new Vector3(k, k, 1f);

            // The counter hangs from its own rest line; the cellar is measured off its TOP
            // edge, so both live on the same number and a moved bar takes its stock with it.
            float counterTop = CounterRestY + CounterSurfaceInset - Reference.y * 0.5f;
            sr.transform.position = new Vector3(
                artX - _counterNative.x * 0.5f,
                counterTop - artFoot + CellarBottleH * 0.5f, 0f);
        }

        private void ApplyDrawer()
        {
            if (_world != null)
                _world.position = new Vector3(
                    0f, DrawerTravel * _drawerT * Mathf.Max(0.0001f, _worldScale), 0f);
            if (_shutterTr != null)
            {
                var lp = _shutterTr.localPosition;
                _shutterTr.localPosition =
                    new Vector3(lp.x, _shutterRestLocalY - ShutterTravel * _drawerT, lp.z);
            }
            LayOutCellarDoors();
            LayOutShutterDoor();
            if (_registerFade != null)
                foreach (var g in _registerFade)
                {
                    if (g == null) continue;
                    g.alpha = 1f - _drawerT;
                    g.blocksRaycasts = _drawerT < 0.01f;
                }
        }

        private void StepDrawer()
        {
            if (Mathf.Approximately(_drawerT, _drawerTarget)) return;
            // Unscaled: the cellar opens while the night's clock is running, and a paused or
            // slowed run must not leave the roller half down.
            float step = Time.unscaledDeltaTime / DrawerSeconds;
            _drawerT = Mathf.MoveTowards(_drawerT, _drawerTarget, step);
            ApplyDrawer();
        }

        // ── the bar runs past both edges (2026-08-19) ───────────────────────────
        // The art ends inside the frame: its slab is drawn with depth, so the back edge
        // tapers in over the last fifty pixels at each end and the bar visibly STOPS, with
        // floor showing beside its cut corners. The fix is not a wider drawing and NOT a
        // stretched one — the sprite is 9-sliced (border 168 / 305, FullRect mesh, already
        // set on the importer) and drawn TILED, so the middle bay repeats at its own size
        // and the two ends keep their joinery. Everything past the frame is off-screen: what
        // the player sees is a bar that carries on past both edges.
        //
        // How many times the middle bay repeats. TWO is the smallest number that both pushes
        // the tapered ends out of frame (the counter draws 26% wider than the room, so ~13%
        // hangs off each side) and lands on a WHOLE tile — Continuous tiling clips a partial
        // last tile, and a bay sliced down the middle beside the end cabinet is the one
        // artefact this could produce.
        private const int CounterMiddleTiles = 2;
        private float _counterDrawWidth;            // art px actually drawn, native if untiled

        /// <summary>
        /// Puts the bar on the renderer's 9-slice, and answers how many art pixels wide it
        /// will actually draw.
        ///
        /// Everything is read off the SPRITE rather than written down twice: the border it
        /// was imported with says where the caps end, so re-cutting the counter art moves
        /// this with it. A sprite that arrives without a border (or on a Tight mesh, which
        /// cannot 9-slice at all) is drawn plain at its native width — a bar that stops
        /// inside the frame is a blemish, and a silent fallback beats a torn one.
        /// </summary>
        private float SetUpCounterTiling(SpriteRenderer sr)
        {
            var border = sr.sprite.border;          // x left, y bottom, z right, w top
            float middle = _counterNative.x - border.x - border.z;
            if (border.x <= 0f || border.z <= 0f || middle <= 0f)
            {
                Debug.LogWarning("DiegeticStage: the counter sprite has no left/right border, " +
                                 "so it cannot be widened without stretching — drawing it at " +
                                 "its native width. Set the border on the importer.");
                return _counterNative.x;
            }
            float drawn = border.x + middle * CounterMiddleTiles + border.z;
            sr.drawMode = SpriteDrawMode.Tiled;
            // Continuous, not Adaptive: Adaptive stretches the tile to make it fit, which is
            // the one thing this is here to avoid. With a whole number of tiles there is
            // nothing left to fit.
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(drawn, _counterNative.y);
            return drawn;
        }

        /// <summary>
        /// Where shelf compartment <paramref name="index"/> is standing right now, in STAGE
        /// units: the centre of its opening, the floor a glass stands on, and how much
        /// headroom there is under the shelf board. False when the bar was never drawn.
        ///
        /// ShelfCentrePx is measured on the bar AS DRAWN — 9-sliced out to _counterDrawWidth
        /// — so a cell is simply its offset from that bar's left edge. Should the sprite ever
        /// arrive without a border, the bar falls back to its native width and these cells
        /// are the wrong ones; SetUpCounterTiling logs that case rather than letting it pass.
        /// </summary>
        public bool ShelfCell(int index, out float centerX, out float floorY, out float height)
        {
            centerX = 0f; floorY = 0f; height = 0f;
            if (_counterTr == null || _counterNative.x <= 0f) return false;
            if (index < 0 || index >= ShelfCentrePx.Length) return false;
            float scale = _counterScale;
            // The art's own top edge, in stage units: the rest line is CounterSurfaceInset
            // art-pixels below it, and that line is pinned to CounterRestY.
            float artTopY = CounterRestY + CounterSurfaceInset * scale;
            float drawn = _counterDrawWidth > 0f ? _counterDrawWidth : _counterNative.x;
            centerX = (ShelfCentrePx[index] - drawn * 0.5f) * scale;
            floorY = artTopY - ShelfFloorPx * scale;
            height = (ShelfFloorPx - ShelfCeilPx) * scale;
            return true;
        }

        /// <summary>Where the till's base sits: twelve units forward of the rest line, near
        /// the front of the bar top, where something on the bartender's side of the bar
        /// actually stands.
        ///
        /// WRITTEN AS AN OFFSET, not as a number (2026-08-19). It was 104 against a rest line
        /// of 116, and when the counter moved the till stayed where it was and sank into the
        /// bar top — the same way the beer fonts had been floating twelve units above it
        /// since the counter came down. Anything standing on the counter rides the counter's
        /// one dial now, so the bar can be moved again without a hunt for what came loose.</summary>
        private const float RegisterBaseY = CounterRestY - 12f;

        // The till's display window, as fractions of the sprite — measured off the register
        // art (x 8..42 of 49, y 5..12 of 43, y from the TOP). Read them again if the till is
        // redrawn; the money is placed from these so it lands in the window rather than near it.
        private const float DisplayLeft = 8f / 49f, DisplayRight = 43f / 49f;
        private const float DisplayTop = 5f / 43f, DisplayBottom = 12f / 43f;

        // 128 -> 116 (2026-08-19, the author, in play: "masayi biraz asagi cek"), then
        // 116 -> 131 the same evening ("tezgahi Y ekseninde -122'ye al"). The author reads
        // this one in the INSPECTOR, not here: the counter is a child of a world root whose
        // scale is 1 at 16:9, so the transform's y is CounterRestY + CounterSurfaceInset −
        // the art's own half height (75) − half the stage (180) = CounterRestY − 253. −122
        // is 131. The whole counter layer rides this number, which is why it is the dial —
        // and everything standing ON the counter is now written as an offset from it rather
        // than as its own constant, because the last two moves left the till and the beer
        // fonts behind.
        // 131 -> 120 (2026-08-21, the drawer counter). READ OFF THE AUTHOR'S OWN MOCK-UP
        // rather than tuned: in mockup_drawer_closed.png the slab's near-black band starts
        // at screen row 240 of 360, which is 120 above the bottom edge. The same slab sits
        // at row 119 in the OPEN frame - a difference of exactly 121, which is the drawer's
        // whole travel and the number Phase 2 will slide the scene by.
        private const float CounterRestY = 120f;           // counter-top rest line (till, glassware)
        // Measured off the art: the bar's far edge — where a glass is set down — is this far
        // below the sprite's top (2026-07-29).
        private const float CounterSurfaceInset = 2f;
        private const float CounterFrontY = 96f;           // surface line: the bottom 96px band
        private const float Overscan = 48f;         // bleed past screen edges (aspect safety)

        // ── the light plan (2026-08-10) ─────────────────────────────────────────
        // The room's ceiling lights, measured off club_room.png by clustering its
        // warm-bright pixels (Tools would guess; the art knows). RE-MEASURED TWICE: v2's
        // room hung FOUR pendant lamps low over the floor at y 84; the 2026-08-18 painted
        // shell put THREE recessed downlights at x 222/331/455, y 51; the PixelLab room
        // that replaced it that evening draws the same three a little higher and a little
        // wider apart. The clustering is in Tools/scene_variants_gen.py (`measure`), so the
        // next room does not need this done by hand — and it tests warm-over-cool, not just
        // brightness, because this room's cornice carries a CYAN rim light that is every bit
        // as bright as a downlight and is not one.
        // Each gets a warm pool; the global wash is slightly cool and slightly below 1
        // so the pools read as light and not as paint.
        // RE-MEASURED A THIRD TIME (2026-08-19) for the author's own PixelLab-site room
        // (Tools/scene_user_post.py): the same three recessed downlights, now on the
        // ceiling plane at y 103 - lower than the last room because this one's ceiling
        // is a perspective plane seen from inside, not a top strip.
        private static readonly Vector2[] LampArtPx =
            { new Vector2(211, 57), new Vector2(331, 57), new Vector2(468, 57) };
        private static readonly Color GlobalTint = new Color(0.86f, 0.85f, 0.95f);
        private const float GlobalIntensity = 0.85f;
        private static readonly Color LampTint = new Color(1f, 0.80f, 0.52f);
        private const float LampIntensity = 0.55f;
        private const float LampRadius = 92f;

        // ── THE ROOM IS LIT BY ITS OWN WINDOW (2026-08-19) ──────────────────────
        //
        // The sky outside is 55 frames of an evening, and from here on it is not just a
        // picture in a hole: it is the room's light source. Every frame the window puts up,
        // the glass is READ — the hot band along the horizon becomes the light coming
        // through it, the frame's own average becomes the wash over everything, and how
        // bright the whole plate is decides whether it is still day. Nothing here is a
        // hand-picked colour; the art is the input (the author: "sahne ışıklandırması için
        // camdaki renkleri referans alacağız").
        //
        // What that reads as, measured off the shipped sheet: frame 0's horizon is #FDA911
        // and its sky #8D2486, so the bar opens drenched in orange from the left with a
        // plum wash and its ceiling nearly off; frame 54's horizon has fallen to #822C8B
        // over a #252063 sky, so the window goes cold and quiet and the three downlights
        // become the only warm thing in the room. The ceiling coming UP as the window goes
        // down is the whole point — the room answers the evening (the author: "camdan vuran
        // ışık ve mekanın ışığı da değişmeli").
        //
        // AND IT IS DELIBERATELY OVERDONE (the author: "ışıklandırmanın abartı bir boyutta
        // değişmesini istiyorum"). Two knobs do it and they are the ones to turn: SkyPunch
        // drives the sampled colour away from its own grey, and the Day/Night pairs below
        // are far enough apart that the same room reads as two different times of night.
        /// <summary>How far a sampled colour is pushed off its own luma. 1 = as measured.</summary>
        private const float SkyPunch = 2.1f;
        /// <summary>The light through the glass, at its loudest and at its quietest.</summary>
        private const float WindowDay = 1.30f, WindowNight = 0.25f;

        // ── A WINDOW IS AN AREA, NOT A BULB (2026-08-19) ────────────────────────
        //
        // The author: "camdan vuran ışığın tamamı müşterilerin üzerinde olması mantıksal
        // olarak doğru mu? camla aynı hizadalar — bir arka plana vuran ışığın bir kısmı
        // müşterilerin üstüne vursa daha mantıklı olmaz mı?" It would, and the numbers said
        // so: the light sat at the glass (art x 54.5) and the first stool stands at x 59 —
        // four and a half units, well inside the light's own inner core — so the person
        // beside the window took the full throw while the back wall, 265 units off, took a
        // fifth of it. Measured over the six stools: 0.97 at the first against 0.19 on the
        // wall. A window lit its neighbour and barely lit the room.
        //
        // Setting the source back and giving it a cone was the first fix and it was not
        // enough: pushed to 1200 units with the radius to match, the ratio only fell from
        // 5.5 to 2.6 and the whole room went dim with it. The reason is structural — a POINT
        // light always favours what is nearest, and a window does not. A window is an AREA
        // source: a small room lit through one is lit ALL OVER by it, and what stands beside
        // the glass catches a graze on top of that, not the whole of it.
        //
        // So the window's light is TWO things now. Most of it is a FILL: the room's own wash
        // takes the window's colour, evenly, so the back wall and the far side are lit by the
        // sunset exactly as the near side is. What is left is a small hot patch at the glass
        // — the light on the wall beside a window, which the nearest customer stands in the
        // edge of. That is the split the instruction describes: the background takes the
        // light, and PART of it lands on the people.
        /// <summary>How far the room's wash is dragged to the window's own colour by day.</summary>
        private const float WindowFillShare = 0.25f;

        // ── AN AMBIENT IS A TINT, NOT A COAT OF PAINT (2026-08-19) ──────────────
        //
        // The author: "ilk sahnelerde gerçekçi olmayacak seviyede sarı ışık var, biraz fazla
        // sarı oluyor mekan." It was, and the arithmetic says exactly how it got there. At
        // frame 0 the sky measures #A74D44; SkyPunch 2.1 drove that to #EE301D, and 55% of
        // the way to the beam's #FFB700 left the GLOBAL light — the one that lights every
        // surface equally — at #F67419, 90% saturated. A global light multiplies everything,
        // so a saturated orange one does not warm a room, it REPLACES it: the concrete
        // stopped being concrete and the plum wall came out brown.
        //
        // The step that was missing: a room's ambient is not the sky's colour. It is the
        // sky's colour arriving on the room's own surfaces — grey concrete under a sunset is
        // warm GREY, not orange. So the ambient is pulled back toward neutral before it is
        // used, and only the WINDOW'S OWN BEAM keeps the full punch, because a shaft of
        // sunset light really is that colour. Measured after: ambient #DF968F at 42%
        // saturation instead of 90%, the plum wall plum again, and the sunset still blazing
        // where it belongs — in the glass.
        /// <summary>How much of the sky's hue the room's ambient takes. 1 = the old paint.</summary>
        private const float AmbientPull = 0.55f;
        /// <summary>The ambient's own punch. Lower than the beam's: it is a fill, not a shaft.</summary>
        private const float WashPunch = 1.45f;
        /// <summary>The wash over the whole room, ditto.</summary>
        private const float WashDay = 1.10f, WashNight = 0.52f;
        /// <summary>The ceiling. It runs the OTHER way: the room lights up as the sky dies.</summary>
        private const float CeilingDay = 0.16f, CeilingNight = 1.85f;

        // ── the room after dark (2026-08-19, the author: "mekanın içerisindeki
        // ışıklandırma hava karardığında daha etkili ve aydınlatmalı") ──────────
        //
        // Turning the ceiling up alone did not make the room LIT: three tight pools on a
        // dark floor read as three lamps in a cave, and the bar — which is the bottom third
        // of the screen and the only place the player works — sat outside all of them. Two
        // things fix that and both are what a real room does after dark.
        //
        // The pools GROW. A downlight over a dark room throws further than the same light
        // over a sunlit one, because there is nothing left to out-shine it; at 92 they
        // stopped at the wall behind the counter, and at 168 they reach the bar and each
        // other, so the three read as one lit ceiling instead of three spots.
        /// <summary>How far a ceiling pool reaches, by day and by night.</summary>
        private const float LampRadiusDay = 92f, LampRadiusNight = 168f;
        //
        // And the WASH warms. At noon a room's ambient is the sky; at two in the morning
        // there is no sky left in it — what fills the shadows is the room's own lamps coming
        // back off the walls. Leaving the wash on the sky's cold blue is what kept the night
        // reading as a blue cave rather than as a bar with its lights on.
        /// <summary>How far the night's wash is dragged off the sky toward the lamps.</summary>
        private const float NightBounce = 0.62f;
        /// <summary>What the room bounces: its own tungsten, one step down from the bulb.</summary>
        private static readonly Color BounceTint = new Color(1f, 0.72f, 0.42f);

        // THE LIGHT OVER THE BAR. The ceiling alone left the one place the player actually
        // works — the counter, the bottom third of the screen — in shadow, because the
        // downlights hang at art y 57 and the bar's rest line is at 128: even a pool that
        // reaches the back wall arrives at the counter as nothing. A bar has its own light
        // over the bar; this is that light, and like the ceiling it comes up as the sky
        // goes down. It is what makes the night READ as service rather than as ambience.
        /// <summary>The bar's own light, by day (the window carries it) and by night.</summary>
        private const float BarLightDay = 0.05f, BarLightNight = 1.15f;
        /// <summary>Hung over the counter's rest line, centred, in the room art's own space.</summary>
        private static readonly Vector2 BarLightArtPx = new Vector2(320f, 150f);
        private const float BarLightRadius = 300f;
        private Light2D _barLight;
        /// <summary>How far the window's light reaches. The glass is a wall, not a lamp —
        /// this is wide enough to carry across the room and die on the far side.</summary>
        private const float WindowRadius = 230f;

        // ── THE WINDOW THROWS, IT DOES NOT RADIATE (2026-08-19) ─────────────────
        //
        // The author: "camdan vuran ışığın tamamı müşterilerin üzerinde olması mantıksal
        // olarak doğru mu? camla aynı hizadalar." It was not, and the numbers say why: the
        // light sat AT the glass (art x 54.5) and the first stool stands at x 59 — four and
        // a half units away, well inside the light's own 84-unit inner core. So the person
        // beside the window was the closest object to the bulb and took the full 2.35, while
        // the back wall — the surface a window actually faces — sat 265 units off and took
        // a fraction of it. A window lit its neighbour and barely lit the room.
        //
        // Two changes put it right, and both are what the real thing does. The source moves
        // BEHIND THE GLASS: light through a window comes from outside, so its origin belongs
        // out there, and then nobody in the room is standing on it. And it becomes a CONE
        // aimed into the room, because a window throws one way — the beam lands on the far
        // wall and the floor, which is where the light in the reference photograph is, and
        // the people at the counter catch its EDGE. Part of it, as asked, rather than all.
        /// <summary>How far outside the glass the sun's origin sits, in art px.</summary>
        private const float WindowSetback = 40f;
        /// <summary>The cone: wide enough to fill the room, tight enough to have a direction.</summary>
        private const float WindowConeOuter = 168f, WindowConeInner = 78f;
        /// <summary>Where the cone points, in degrees about Z. A URP 2D spot opens along its
        /// own UP axis, so −100 aims it right and a little down: across the room and onto the
        /// floor, which is the way light falls through a window standing above head height.
        /// THE ONE NUMBER TO CHECK IN THE ENGINE — if the cone comes out pointing up, the
        /// convention is the transform's right and this wants −10 instead.</summary>
        private const float WindowAimDegrees = -100f;
        /// <summary>The luma range the evening actually spans, MEASURED over the 81 frames
        /// with these same weights, over the SKY pixels only (see SkyAlphaCut): the opening
        /// plate reads 0.495 and the last 0.156. Normalising over the real range is what
        /// makes the swing fill the knobs above instead of a third of them — guessed at
        /// 0.205..0.350 first, and frame 0 came out at two-thirds of a day with its ceiling
        /// already half on.</summary>
        private const float SkyLumaNight = 0.156f, SkyLumaDay = 0.495f;

        /// <summary>
        /// How much of the sky the horizon glow covers when the sun is still in it — 6.8%
        /// of the sky pixels at frame 0, measured. The key colour is blended toward the
        /// plain sky average by this, and that blend is load-bearing: the BRIGHTEST pixels
        /// of a night sky are lit tower windows, which stay warm long after the sun is
        /// gone. Peak alone would have kept throwing sunset into the room at two in the
        /// morning. Area is what separates a sunset from a scattering of lamps — the glow
        /// falls from 6.8% to under 0.4% of the sky, so the window's light goes cold on
        /// its own, from the art, with nothing about the hour written down.
        /// </summary>
        private const float SkyGlowFull = 0.068f;

        /// <summary>
        /// Only what stands ABOVE the horizon is sky. The sheet marks every pixel warped
        /// from below the skyline's base at alpha 254 (Tools/window_cycle.py, HORIZON_ROW)
        /// — invisible on the glass, unmistakable here — and the light read refuses them,
        /// because the city's lit windows were brightening the ROOM as they came on (the
        /// author, 2026-08-19: "şehir ışıkları mekanı aydınlatamaz"). An unmarked sheet has
        /// only 255s, so the gate degrades to the old whole-pane read.
        /// </summary>
        private const float SkyAlphaCut = 0.998f;

        private Light2D _windowLight;
        // WHERE THE EVENING ACTUALLY IS, between two plates. Everything that asks the room
        // what it is lit by — the closing beat, the back bar's canvas — reads these rather
        // than a frame's own measurement, so they glide with the light instead of stepping
        // with the picture.
        private Color _keyNow = Color.white, _washNow = Color.white;
        private float _dayNow = 1f;
        private Color[] _skyKey, _skyWash;      // sampled per frame, lazily
        private float[] _skyDay;
        private bool[] _skyRead;
        // The sky-driven bases the closing beat dims FROM. They used to be the two consts
        // above; a beat that lerped from a constant would have snapped the room back to
        // noon-of-nowhere the moment the last call began.
        private float _washBase = GlobalIntensity, _ceilingBase = LampIntensity;

        // ── the fixture slots (2026-08-10): where bought dressing stands ────────
        // Named hooks in the ROOM ART's own space (art px, bottom-left origin — identical
        // to stage units at the native 640×360), each marking the BOTTOM CENTRE of whatever
        // stands there. They were seven constants in this file, which made a new place to
        // put a plant a CODE change in a project whose first rule about content is that
        // content is data. They come out of fixtures.json now, beside the fixtures that
        // name them, and the parser refuses a fixture whose slot the room does not have.
        private readonly Dictionary<string, LastCall.Game.StageSlot> _slots =
            new Dictionary<string, LastCall.Game.StageSlot>();

        /// <summary>Hands the room its hooks. Told, not read — the same way the till is told
        /// the money — so the stage never reaches into the run or the loader.</summary>
        public void SetSlots(IReadOnlyList<LastCall.Game.StageSlot> slots)
        {
            _slots.Clear();
            if (slots == null) return;
            foreach (var s in slots) _slots[s.Id] = s;
        }

        private readonly List<(LastCall.Core.FixtureDefinition Def, Transform Body, Light2D Glow)>
            _placedFixtures = new List<(LastCall.Core.FixtureDefinition, Transform, Light2D)>();

        private Font _display;
        [SerializeField] private Font displayFont;         // Press Start 2P (headings/numbers)

        /// <summary>Installed environment art. When set, the full-screen club background and
        /// the bar counter replace their flat procedural placeholders.</summary>
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite counterSprite;
        /// <summary>The roller shutter that shuts the counter's cellar (2026-08-22). Optional:
        /// with no shutter the cabinet simply stands open, which is what the stage did before
        /// the drawer existed.</summary>
        [SerializeField] private Sprite shutterSprite;
        [SerializeField] private Sprite registerSprite;   // cash register, shows the wallet
        private Text _moneyText;

        /// <summary>Per-archetype ID photos for the licence card. Falls back to a flat silhouette.</summary>
        [System.Serializable]
        public struct PortraitSprite { public string archetypeId; public Sprite sprite; }
        [SerializeField] private PortraitSprite[] portraits;
        private readonly Dictionary<string, Sprite> _portraits = new Dictionary<string, Sprite>();


        // ── the world ───────────────────────────────────────────────────────────
        private Transform _world;                   // root of every world-space stage object
        private Material _litMaterial;              // Sprite-Lit-Default, shared by the stage
        private SpriteRenderer _backdropSr, _backgroundSr, _windowSr, _glassSr;

        // ── the palms, and the wind in them (2026-08-23) ────────────────────────
        // The author: "ağaçları görselden ayıralım, ağaçlara animasyonu ayrı vereceğiz çünkü
        // ağaçlar çok daha fazla sallanması gerekiyor". They are their own drawing on their
        // own transparency, standing between the sky and the room - so the room's mullions
        // cut them exactly as they cut the view behind.
        //
        // THE WIND IS CODE, NOT FRAMES, and that is the point of separating them. A drawn
        // sway is one amplitude at one speed for ever; this one BENDS - each horizontal
        // band is pushed by an amount that grows with its height, so the trunk leans and
        // the fronds at the top travel furthest, which is what a palm does. It costs
        // nothing to make the wind stronger, and the two trees can be out of step.
        //
        // ONE PIECE, LEANING FROM THE ROOT (2026-08-23, the author: "palmiyelerin gövdesi
        // beraber hareket etmeli dalgalanma olmamalı"). The first take sliced the layer into
        // 22 bands and pushed each by its own height — a real bend, and wrong: every band
        // rounds its offset to a whole pixel, the bands cross their rounding thresholds at
        // different moments, and the trunk crawls like a snake instead of leaning. A trunk
        // is stiff. It goes over as ONE THING.
        //
        // So each layer is one sprite and the wind is a small ROTATION about its foot. The
        // trunk keeps its shape by construction — there is no seam left to ripple — and the
        // crown still travels furthest simply because it is furthest from the pivot.
        //
        // SIX LAYERS, SIX RHYTHMS (2026-08-23, the author again: "iki ağacın yaprakları da
        // bağımsız olarak hareket etmeli"). One sprite fixed the ripple and bought a second
        // wrong: both trees leaned in perfect lockstep, which is the one thing a pair of
        // trees never does. So the plate is cut — ONCE, offline, by
        // Tools/window_palms_split.py — into a pole and a crown for each tree and the ground
        // plants at each foot, and every piece gets its own speed and its own phase.
        //
        // A crown hangs off ITS OWN TRUNK in the hierarchy, so it inherits the trunk's lean
        // and adds its own turn about the junction on top. That is the whole trick: the
        // fronds have a life of their own without ever coming off the tree, and the trunk
        // below the junction never feels it.
        //
        // The pivots are art px on the WINDOW'S OWN 141×274 opening, y from the TOP, printed
        // by the split script — the ROOTS are off the opening on purpose, because the sill
        // cuts each trunk long before the ground does, and a tree that turns about its
        // visible foot swings like a hanged sign.
        //
        // THE PLATES ARE BIGGER THAN THE OPENING (2026-08-23, the author, having drawn the
        // crowns out whole by hand: "png de ekran dışında kaldığından kesiliyor yapraklar"). A
        // finished crown reaches past the opening on the far side of each tree, and a canvas
        // cut to the opening chops those fronds off in the FILE, where no amount of care in
        // the game can bring them back. So the split pads every layer evenly — the plate
        // stays centred on the opening, the art has room, and the room's own wall (order 10,
        // over all of these) hides whatever falls outside. Because the pad is even, an offset
        // measured in opening px is still measured from the plate's centre, and no number
        // here has to know how wide the pad is.
        private const float PalmLeanDegrees = 2.2f;    // the trunk, each way at full gust
        private const float PalmCrownDegrees = 3.0f;   // the fronds, on top of that
        private const float PlantLeanDegrees = 2.6f;   // the plants on the sill

        private sealed class WindLayer
        {
            public SpriteRenderer Sr;
            public Transform Pivot;      // the point this piece turns about
            public float Amplitude, Speed, Phase;
            public Vector2 PivotArt;     // that point, in plate px, y from the top
            public WindLayer Parent;     // the trunk a crown hangs off, null for a trunk
        }

        private readonly List<WindLayer> _wind = new List<WindLayer>();
        private Transform _windRoot;
        private Vector2 _palmPlate;      // the plate's own size, read off the art

        // ── the view out of the window, played on the shift's clock (2026-08-19) ────
        // A Miami skyline that runs from a golden sun through the pink band into deep
        // night, sliced from ONE sheet: Assets/Resources/Scene/window_cycle.png, built by
        // Tools/window_cycle.py out of six PixelLab animation sheets (one of them
        // generated brightening and played reversed — the chain is pinned there).
        //
        // Each cell is the room's window hole — its bounding box, carrying its alpha — so the
        // view is cut to the glass at build time and cannot slide off it at any aspect, and
        // the painted glazing bars split it into panes for free.
        //
        // The picture is stood in the opening's own PLANE rather than cropped to it: this
        // window is seen at an angle (183 art rows tall at its near edge, 120 at its far
        // one) and each frame is fitted to that trapezoid whole, by nearest sampling, so it
        // tilts with the wall without going soft. That work is all done at build time; at
        // runtime this is a plain sprite swap.
        //
        // The cell size is the hole's size, MEASURED (window_cycle.py build prints it beside
        // the centre below). The frame COUNT is deliberately not a constant — it is read off
        // the sheet, so re-generating with more frames needs no edit here.
        // RE-MEASURED for the 2026-08-22 room (the author: "mevcut ana sahne
        // arkaplanının camlarına gün batım animasyonumuzu ekleyelim"). The old room's hole
        // was 109x182; this one is 141x274 and leans harder — 274 rows at its near edge
        // against 141 at its far one. Every number here was PRINTED by the tool that cut the
        // sheet (Tools/window_cycle.py build), never typed: the cell IS the hole, so if these
        // two ever disagree the view slides off the glass.
        private const int WindowCellW = 141, WindowCellH = 274;
        /// <summary>The hole's centre in the room art's own bottom-left space.</summary>
        private static readonly Vector2 WindowCentreArtPx = new Vector2(70.5f, 212.0f);
        private Sprite[] _windowFrames;
        private int _windowFrame = -1;
        private Vector2 _backgroundNative;
        private float _backgroundScale = 1f;        // stage units per background-art pixel
        private Light2D _globalLight;
        private float _lastVisibleW = -1f;

        /// <summary>Update the diegetic wallet - the number standing over the till.</summary>
        public void SetMoney(string text)
        {
            if (_moneyText != null) _moneyText.text = text;
        }

        /// <summary>The window goes red when the bar is under water (2026-08-14): the till is
        /// the only place the money is written now, so it is the only place debt can show.</summary>
        public void SetMoneyInDebt(bool red)
        {
            if (_moneyText != null) _moneyText.color = red ? UITheme.ViceRed[3] : UITheme.Money;
        }

        private System.Action _onRegisterClicked;

        /// <summary>Wires the till click to the ledger-history popup (GDD 24 §7).</summary>
        public void SetRegisterHandler(System.Action onClick) => _onRegisterClicked = onClick;

        private System.Action _onTapClicked;

        /// <summary>Wires the beer font on the counter to the draught station (2026-08-15).
        /// The stage owns WHERE the font stands, so it owns the hit plate; the HUD owns what
        /// clicking it means — the same split the till has had since the ledger landed.</summary>
        public void SetTapHandler(System.Action onClick) => _onTapClicked = onClick;

        /// <summary>The ID photo for an archetype, for the tycoon floor's licence card.</summary>
        public Sprite PortraitSpriteFor(string archetypeId) =>
            !string.IsNullOrEmpty(archetypeId) && _portraits.TryGetValue(archetypeId, out var s) ? s : null;

        private void Awake()
        {
            Application.runInBackground = true; // keep animations advancing unfocused
            FillTheWindow();
            _display = displayFont != null
                ? displayFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (portraits != null)
                foreach (var p in portraits)
                    if (p.sprite != null && !string.IsNullOrEmpty(p.archetypeId)) _portraits[p.archetypeId] = p.sprite;
            BuildScene();
        }

        private void Update()
        {
            StepClosing();
            StepDrawer();
            StepPalms();
            float w = VisibleWidth();
            if (!Mathf.Approximately(w, _lastVisibleW)) Refit(w);
            // The room is built once at the reference and MAGNIFIED to cover the window.
            // One transform carries it, so the picture, the counter, the lamps, the fixtures
            // and their shadows all take the same scale by construction — there is no second
            // number for any of them to disagree with.
            float k = DesignFrame.SceneScale;
            if (_world != null && !Mathf.Approximately(k, _worldScale))
            {
                _worldScale = k;
                _world.localScale = new Vector3(k, k, 1f);
            }
        }

        private float _worldScale = -1f;

        /// <summary>
        /// Makes sure the camera fills the window rather than boxing itself inside it.
        ///
        /// Windowboxing it was the wrong half of the right idea (2026-08-11): it did stop
        /// the room from stretching, and it did it by drawing black bars, which the author
        /// rejected on sight — a wide monitor should get more bar, not a smaller one in a
        /// frame. So the camera keeps showing 360 × aspect and the ROOM fills whatever that
        /// is; what stopped moving instead is everything the player reads or touches, which
        /// now lives in a fixed safe frame (see <see cref="DesignFrame"/>).
        /// </summary>
        private static void FillTheWindow()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // The PixelPerfectCamera has to go, and it is worth saying why: it snaps the
            // camera to a WHOLE zoom, which means the height it shows jumps in steps as the
            // window is dragged — 360 stage units at one size, 526 at the next. Nothing that
            // scales smoothly can stay glued to something that jumps, and the author's
            // requirement is that nothing move at all. So the camera holds the reference
            // height exactly and the scene takes one continuous scale instead
            // (DesignFrame.SceneScale). Pixel art pays for that in the sizes where the scale
            // is not a whole number; a scene that comes apart when you drag a corner costs
            // more.
            var ppc = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            if (ppc != null && ppc.enabled) ppc.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = Reference.y * 0.5f;
        }

        /// <summary>The width the room is BUILT at — the reference, always. The window is not
        /// the room's business any more: everything under the stage root is laid out at
        /// 640×360 and the root itself is magnified by <see cref="DesignFrame.SceneScale"/>,
        /// so the picture and everything standing in it grow by exactly the same amount and
        /// cannot come apart.</summary>
        private static float VisibleWidth() => Reference.x;

        /// <summary>Stage units (bottom-left origin) → world position. One world unit is one
        /// stage unit; the camera sits over the stage's centre.</summary>
        private static Vector3 StageToWorld(float x, float y) =>
            new Vector3(x - Reference.x * 0.5f, y - Reference.y * 0.5f, 0f);

        // ── scene construction ──────────────────────────────────────────────────

        private void BuildScene()
        {
            _world = new GameObject("StageWorld").transform;
            _world.position = Vector3.zero;

            // The one material the whole stage shares. Sprites-Default is unlit — under the
            // 2D renderer an unlit sprite ignores every Light2D, which would make the whole
            // migration a very quiet no-op.
            var litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (litShader != null) _litMaterial = new Material(litShader);
            else Debug.LogWarning("DiegeticStage: Sprite-Lit-Default not found — the stage will draw unlit.");

            // Opaque backdrop behind everything, overscanned past the screen edges so no
            // aspect-ratio border ever exposes the clear colour / editor checker.
            _backdropSr = WorldSprite("Backdrop",
                Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f),
                order: 0);
            _backdropSr.color = UITheme.Night[0];


            // The room. Real club background when installed, else the flat procedural
            // placeholders on their own canvas, so a broken reference is still a visible bar.
            if (backgroundSprite != null)
            {
                // THE VIEW, BEHIND THE ROOM (14 v3 §5's layer order, §7's plate). The shell's
                // window panes are keyed to transparent holes, so without this the backdrop
                // shows through them and the bar looks out on Night[0] — black rectangles
                // where daylight belongs (measured in play, 2026-08-18).
                //
                // Loaded by NAME rather than serialized, because what hangs here changes with
                // the shift and one inspector slot cannot hold a whole evening. It is an
                // ANIMATION now (2026-08-19): a Miami skyline that goes from a low sun to lit
                // windows across the night, sliced from one sheet and stepped by the clock in
                // SetSkyFraction. The single day plate stays as the fallback, so a missing or
                // half-built sheet still leaves daylight in the glass rather than black holes.
                _windowFrames = LoadWindowFrames();
                var windowPlate = _windowFrames != null && _windowFrames.Length > 0
                    ? _windowFrames[0]
                    : Resources.Load<Sprite>("Scene/window_day");
                if (windowPlate != null)
                {
                    _windowSr = WorldSprite("WindowPlate", windowPlate, order: 5);
                    _windowFrame = _windowFrames != null ? 0 : -1;
                }

                _backgroundSr = WorldSprite("Background", backgroundSprite, order: 10);
                _backgroundNative = backgroundSprite.rect.size;

                // THE PANE ITSELF, over the room and under everything standing in it (the
                // counter is 30). A window is TWO things: the view stands flat behind the
                // wall and shows through the keyed panes, and this is the sheet of glass in
                // front of it. Without it the opening reads as a hole in a wall rather than
                // something glazed (the author, 2026-08-22). Cut to the room's own mask by
                // the same tool that cuts the animation, so the sheen cannot sit a pixel off
                // the glass — Tools/window_cycle.py glass.
                var glass = Resources.Load<Sprite>("Scene/window_glass");
                if (glass != null) _glassSr = WorldSprite("WindowGlass", glass, order: 11);
                BuildPalms();

                // The lamps the picture already painted, made real: a warm pool under each
                // bulb. Positions are measured art pixels, converted per-fit in Refit.
                for (int i = 0; i < LampArtPx.Length; i++)
                    _lamps.Add(PointLight("Lamp" + i, LampTint, LampIntensity, LampRadius));

                // THE SUN, standing where the window is. It is one light and not a shaped
                // one: what sells daylight through glass is the DIRECTION it falls from and
                // the colour it carries, and both of those are already true of a point hung
                // in the window's own opening. Its colour and its strength are the glass's
                // to decide, every frame — see ApplySkyLight.
                _windowLight = PointLight("WindowLight", LampTint, 0f, WindowRadius);
                // A throw, not a bulb: see WindowSetback / WindowAimDegrees above.
                _windowLight.pointLightOuterAngle = WindowConeOuter;
                _windowLight.pointLightInnerAngle = WindowConeInner;
                _windowLight.transform.rotation = Quaternion.Euler(0f, 0f, WindowAimDegrees);
                _barLight = PointLight("BarLight", LampTint, 0f, BarLightRadius);

            }
            else
            {
                BuildFallbackRoom();
            }

            // The global wash: what "the room is dim" means to the lighting system. Slightly
            // cool and slightly below 1, so the warm pools have something to be warmer than.
            _globalLight = new GameObject("GlobalLight").AddComponent<Light2D>();
            _globalLight.transform.SetParent(_world, false);
            _globalLight.lightType = Light2D.LightType.Global;
            _globalLight.color = GlobalTint;
            _globalLight.intensity = GlobalIntensity;
            LightAllLayers(_globalLight);

            // The bar. A drawn asset: it carries EMPTY shelves, which is not decoration but
            // structure — glassware is a buyable upgrade and those shelves are where the
            // bought glasses get drawn. There is no procedural counter to fall back on, so a
            // lost reference is an invisible bar and no other symptom (2026-07-29).
            if (counterSprite == null)
                Debug.LogWarning("DiegeticStage: no counterSprite — the bar will not be drawn. " +
                                 "Check the reference in the scene, or re-run LastCall > Create Debug Scene.");
            if (counterSprite != null)
            {
                var sr = WorldSprite("Counter", counterSprite, order: 30);
                _counterTr = sr.transform;
                _counterNative = counterSprite.rect.size;
                _counterDrawWidth = SetUpCounterTiling(sr);
            }
            // Order 33: over the counter's cabinet (30) AND over the stock standing in it
            // (31), and under anything on the bar top (35). The roller has to hide the
            // bottles or it is not shutting anything.
            if (shutterSprite != null)
            {
                var sh = WorldSprite("Shutter", shutterSprite, order: 33);
                _shutterTr = sh.transform;
                _shutterNative = shutterSprite.rect.size;
                BuildShutterDoor();
            }

            BuildRegister();

            Refit(VisibleWidth());

            // The bar opens at the hour the window says it is. Without this the room stood
            // at the old constants until the clock happened to step a frame — which is a
            // whole second and a half of the wrong evening, right at the moment the player
            // is looking hardest.
            ApplySkyLight(_windowFrame >= 0 ? _windowFrame : 0f);

            if (!Motion.Reduced) StartCoroutine(Ambient());
        }

        /// <summary>
        /// A world-space sprite on the stage, for something the HUD owns but the ROOM has to
        /// light — the seated drinkers (2026-08-10). They were UI Images on an overlay canvas,
        /// which no Light2D can reach, so a lamp bought for the corner lit the wall behind a
        /// customer and not the customer. The caller keeps driving it; all this hands out is a
        /// renderer already parented to the stage and already on the lit material.
        ///
        /// Sorting orders are the stage's own ledger: room 10, wall dressing 20, DRINKERS 25,
        /// the bar 30, whatever stands on the bar 35. A drinker at 25 is drawn over by the
        /// counter, which is how the bar takes their legs — the honest version of the mask the
        /// canvas needed.
        /// </summary>
        public SpriteRenderer NewStageSprite(string name, int order) =>
            WorldSprite(name, null, order);

        /// <summary>One world-space stage sprite on the shared lit material.</summary>
        private SpriteRenderer WorldSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (_litMaterial != null) sr.sharedMaterial = _litMaterial;
            return sr;
        }

        private Light2D PointLight(string name, Color tint, float intensity, float radius)
        {
            var l = new GameObject(name).AddComponent<Light2D>();
            l.transform.SetParent(_world, false);
            l.lightType = Light2D.LightType.Point;
            l.color = tint;
            l.intensity = intensity;
            l.pointLightInnerRadius = radius * 0.15f;
            l.pointLightOuterRadius = radius;
            l.falloffIntensity = 0.62f;
            LightAllLayers(l);
            return l;
        }

        /// <summary>
        /// A CONTACT SHADOW under something standing on the floor — a soft dark ellipse at
        /// its feet, the trick that already sells the till as resting on the bar and the
        /// rack glasses as standing in their bays.
        ///
        /// This replaces a real ShadowCaster2D, and the reason is measured rather than
        /// assumed (2026-08-10): with the room frozen, switching every point light's
        /// shadows on changed ZERO pixels. URP's 2D shadows cast radially from the light,
        /// and in a head-on composition where every caster is flat at the same depth and
        /// every lamp hangs above, the shadows all fall below the counter — behind the HUD,
        /// where nothing can see them. The technique that reads at this camera is the one
        /// the game already uses, so this is it, applied to the dressing.
        /// </summary>
        private void ContactShadow(Transform under, float width)
        {
            var art = ItemArt.Load("shadow_soft");
            var go = new GameObject(under.name + "_Shadow");
            go.transform.SetParent(_world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            // OVER THE COUNTER (31), not under the dressing. The floor slots sit at the bar's
            // own rest line, so a blob at 19 was drawn and then covered by the counter at 30 —
            // a shadow you cannot see is not a shadow. 31 puts it on the bar top where the
            // thing casting it is standing, still under anything that stands ON the bar (35).
            sr.sortingOrder = 31;
            sr.color = new Color(0f, 0f, 0f, art != null ? 0.38f : 0f);
            if (_litMaterial != null) sr.sharedMaterial = _litMaterial;
            _shadows.Add((under, sr, width));
        }

        private readonly List<(Transform Under, SpriteRenderer Blob, float Width)> _shadows =
            new List<(Transform, SpriteRenderer, float)>();

        /// <summary>
        /// Light2D ships targeting whatever sorting layers its serialized default says, and
        /// that default is not a public API — so the target list is set outright to every
        /// layer the project has. The UI never suffers for it: overlay canvases are composited
        /// after the camera and no 2D light can reach them at all.
        /// </summary>
        private static void LightAllLayers(Light2D light)
        {
            var f = typeof(Light2D).GetField("m_ApplyToSortingLayers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) return;
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            f.SetValue(light, ids);
        }

        /// <summary>
        /// Everything whose size depends on the window: the backdrop, the room's cover fit,
        /// the counter's width fit, and the lights that hang off the room's own pixels.
        /// Scaled by ONE factor each, so every art pixel stays square (the StageArtFit rule,
        /// now in world units).
        /// </summary>
        private void Refit(float visibleW)
        {
            _lastVisibleW = visibleW;
            float visibleH = Reference.y;

            if (_backdropSr != null)
            {
                var b = _backdropSr.sprite.bounds.size;
                _backdropSr.transform.localScale = new Vector3(
                    (visibleW + Overscan * 2f) / b.x, (visibleH + Overscan * 2f) / b.y, 1f);
            }

            if (_windowSr != null && _windowFrames == null)
            {
                // THE SAME COVER FIT AS THE ROOM, deliberately: the still plate is authored at
                // the room's own 640x360 with the sky sitting in the room's own hole, so any
                // fit that is not identical slides the daylight off the window. The animated
                // frames are not plates and are fitted after the room instead — see below.
                var wb = _windowSr.sprite.bounds.size;
                float wk = Mathf.Max(visibleW / wb.x, visibleH / wb.y);
                _windowSr.transform.localScale = new Vector3(wk, wk, 1f);
            }

            if (_backgroundSr != null)
            {
                // Cover: the larger ratio fills both axes, overflow is cropped by the frame.
                var b = _backgroundSr.sprite.bounds.size;
                float k = Mathf.Max(visibleW / b.x, visibleH / b.y);
                _backgroundSr.transform.localScale = new Vector3(k, k, 1f);
                _backgroundScale = k * (b.x / _backgroundNative.x);   // stage units per art px

                // The animated view is a CUT-OUT of the room's own window hole, not a plate:
                // it is the hole's bounding box and carries the hole's alpha. So it rides the
                // room's own art scale and stands at the hole's own centre — which is why it
                // is fitted here, after _backgroundScale exists, and not in the block above.
                if (_windowSr != null && _windowFrames != null)
                {
                    _windowSr.transform.localScale =
                        new Vector3(_backgroundScale, _backgroundScale, 1f);
                    _windowSr.transform.position = StageArtPointToWorld(WindowCentreArtPx);
                    if (_glassSr != null)
                    {
                        _glassSr.transform.position = _windowSr.transform.position;
                        _glassSr.transform.localScale = _windowSr.transform.localScale;
                    }
                    // The sun stands OUTSIDE the opening and is re-hung with it: the window
                    // moves with the room's fit, so a light left at a build-time position
                    // would slide off the glass the moment the window is not 16:9. The
                    // setback is in ART px and goes through the same fit, so the source keeps
                    // its distance behind the glass at every aspect rather than drifting into
                    // the room as the picture grows.
                    if (_windowLight != null)
                        _windowLight.transform.position = StageArtPointToWorld(
                            new Vector2(WindowCentreArtPx.x - WindowSetback, WindowCentreArtPx.y));
                    if (_barLight != null)
                        _barLight.transform.position = StageArtPointToWorld(BarLightArtPx);
                }

                // The lamps hang where the picture drew them: art px from the TOP, through
                // the same cover fit the picture itself got.
                for (int i = 0; i < LampArtPx.Length; i++)
                {
                    var lamp = _world.Find("Lamp" + i);
                    if (lamp == null) continue;
                    lamp.position = ArtPxToWorld(LampArtPx[i]);
                }
            }

            if (_counterTr != null)
            {
                // THE BAR GROWS BY REPEATING, NOT BY STRETCHING (2026-08-19, the author:
                // "ekrandaki tezgahı unity'nin özelliğiyle sağa ve sola doğru genişlet ...
                // kenarlara uzattıkça sündüren değil görüntüyü üreten metodla").
                //
                // It used to span the window with a UNIFORM SCALE - k = visibleW / artW -
                // which on a 16:9 window is exactly 1 and invisible, and on anything wider
                // is the smear the author is looking at: the counter grows sideways AND
                // upward, its pixels stop being square with the room's, and everything the
                // stage stands on it (the till, the taps, the rest line) drifts with it.
                //
                // Unity's own answer is SpriteDrawMode.Tiled: the sprite's 9-slice border
                // draws at 1:1 and the CENTRE repeats to fill whatever width is asked for.
                // The border is set on import (PatronArtPostprocessor) at the drawing's own
                // cabinet dividers, so a wider window buys more cabinet run. Scale stays 1,
                // which means one art pixel is one stage unit for good - the counter can no
                // longer drift against the room whatever shape the window is.
                var sr = _counterTr.GetComponent<SpriteRenderer>();
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                // Rounded UP to a whole art pixel, and never shorter than the drawing:
                // at exactly 16:9 that is 640 and the caps land pixel-for-pixel where they
                // were drawn, while a wider window is covered without a hairline gap.
                sr.size = new Vector2(
                    Mathf.Max(_counterNative.x, Mathf.Ceil(visibleW)), _counterNative.y);
                _counterTr.localScale = Vector3.one;
                _counterScale = 1f;                            // stage units per art px
                // Hung from the rest line: the art's top is CounterSurfaceInset above it.
                float artTopStage = CounterRestY + CounterSurfaceInset * _counterScale;
                float artHStage = _counterNative.y * _counterScale;
                _counterTr.position = new Vector3(0f, artTopStage - artHStage * 0.5f - Reference.y * 0.5f, 0f);
                if (_shutterTr != null)
                {
                    // Laid out SHUT, then the drawer's own offset is re-applied on top, so a
                    // window resize mid-open does not slam the roller back into the sill.
                    // NOT tiled, unlike the counter: the roller carries one pink arrow at its
                    // top centre and a repeat would draw a row of them. It stays at its drawn
                    // 592 against the counter's 638, which leaves the blue side posts showing
                    // when shut — the author drew it that way and it reads as deliberate.
                    float counterTop = artTopStage - Reference.y * 0.5f;
                    _shutterTr.position = new Vector3(
                        0f, counterTop - ShutterOpeningTopPx - _shutterNative.y * 0.5f, 0f);
                    _shutterRestLocalY = _shutterTr.localPosition.y;
                    ApplyDrawer();
                }
            }

            PlaceFixtures();
        }

        /// <summary>
        /// The palms, sliced into horizontal bands so the wind can BEND them rather than
        /// swing them rigidly. One texture, many renderers: each band shows its own strip of
        /// it and is pushed sideways on its own, and the push grows with height.
        /// </summary>
        private void BuildPalms()
        {
            _windRoot = new GameObject("WindRoot").transform;
            _windRoot.SetParent(_world, false);

            // The far tree is drawn first, then the near one, then the plants on the sill —
            // which is the order the split already assumes, since it is the LEFT crown that
            // overlaps the right one. Orders 6–8 all sit over the view (5) and under the room
            // (10), so the room's own frame and glazing bars cut every layer with one drawing
            // and not one of them needs a mask.
            var rTrunk = WindPiece("window_palm_r", new Vector2(153.3f, 274f), 6,
                                   PalmLeanDegrees, 0.74f, 1.9f, null);
            WindPiece("window_palm_r_crown", new Vector2(113f, 94f), 6,
                      PalmCrownDegrees, 1.31f, 0.6f, rTrunk);
            var lTrunk = WindPiece("window_palm_l", new Vector2(-5.4f, 274f), 7,
                                   PalmLeanDegrees, 0.90f, 0f, null);
            WindPiece("window_palm_l_crown", new Vector2(35f, 69f), 7,
                      PalmCrownDegrees, 1.53f, 2.4f, lTrunk);
            WindPiece("window_plants_l", new Vector2(22f, 274f), 8,
                      PlantLeanDegrees, 1.10f, 1.2f, null);
            WindPiece("window_plants_r", new Vector2(124f, 274f), 8,
                      PlantLeanDegrees, 1.22f, 3.1f, null);
        }

        /// <summary>
        /// One piece of the window's greenery, hung on a pivot of its own. <paramref
        /// name="parent"/> is the trunk a crown belongs to — passing it is what keeps the
        /// fronds on the tree while they move by themselves.
        /// </summary>
        private WindLayer WindPiece(string res, Vector2 pivotArt, int order,
                                    float amplitude, float speed, float phase,
                                    WindLayer parent)
        {
            var sprite = Resources.Load<Sprite>("Scene/" + res);
            if (sprite == null) return null;
            var plate = new Vector2(sprite.texture.width, sprite.texture.height);
            if (_palmPlate != Vector2.zero && plate != _palmPlate)
                Debug.LogWarning($"[Stage] {res} is {plate} but the other window layers are "
                                 + $"{_palmPlate}. They all hang at the opening's centre, so a "
                                 + "layer on a different canvas lands somewhere else.");
            _palmPlate = plate;

            var pivot = new GameObject(res).transform;
            pivot.SetParent(parent != null ? parent.Pivot : _windRoot, false);
            var sr = WorldSprite(res + "Art", sprite, order);
            sr.transform.SetParent(pivot, false);

            var layer = new WindLayer
            {
                Sr = sr,
                Pivot = pivot,
                Amplitude = amplitude,
                Speed = speed,
                Phase = phase,
                PivotArt = pivotArt,
                Parent = parent,
            };
            _wind.Add(layer);
            return layer;
        }

        /// <summary>
        /// One frame of wind. Every layer leans a couple of degrees about its own pivot on
        /// its own clock, and a crown leans about ITS TRUNK'S junction after that trunk has
        /// already leaned — so the fronds travel further than the pole carrying them without
        /// ever leaving it.
        /// </summary>
        private void StepPalms()
        {
            if (_windRoot == null || _windowSr == null || _wind.Count == 0) return;

            // Every layer keeps the plate's own canvas, so the whole set is hung at the
            // view's centre off the view's own transform rather than measured twice — they
            // cannot come apart, whatever the drawer does to the stage under them.
            _windRoot.position = _windowSr.transform.position;
            float s = _windowSr.transform.localScale.x;
            float t = Time.unscaledTime;
            float k = Motion.Reduced ? 0f : 1f;

            for (int i = 0; i < _wind.Count; i++)
            {
                var L = _wind[i];
                if (L == null || L.Sr == null) continue;

                // The pivot stands at its art point; the sprite is pushed back by the same
                // offset, which lands the plate exactly over the view again. Turning the
                // pivot then turns the plate about that art point and nothing else.
                //
                // A crown is measured from ITS TRUNK'S pivot, not from the plate's centre —
                // the difference of two offsets, never the offset of a difference. Handing
                // ArtOffset a delta re-applies the centring and throws the crown half a
                // plate off its own tree, which is exactly what it did once.
                L.Pivot.localPosition = L.Parent == null
                    ? ArtOffset(L.PivotArt, s)
                    : ArtOffset(L.PivotArt, s) - ArtOffset(L.Parent.PivotArt, s);
                L.Sr.transform.localPosition = -ArtOffset(L.PivotArt, s);
                L.Sr.transform.localScale = new Vector3(s, s, 1f);

                // Two waves at rates that do not divide, so no layer ever repeats a beat and
                // no two layers fall into step with one another.
                float w = L.Speed * t + L.Phase;
                float wave = Mathf.Sin(w) * 0.7f + Mathf.Sin(w * 1.7f + 1.3f) * 0.3f;
                L.Pivot.localRotation = Quaternion.Euler(0f, 0f, L.Amplitude * wave * k);
            }
        }

        /// <summary>
        /// Window-opening px (y from the TOP) → an offset from the plate's centre, in the
        /// stage's own units at the view's scale. The two centres are the same point because
        /// the plates are padded evenly, which is why the pad appears in no number here.
        /// </summary>
        private Vector3 ArtOffset(Vector2 openingPx, float scale) => new Vector3(
            (openingPx.x - WindowCellW * 0.5f) * scale,
            (WindowCellH * 0.5f - openingPx.y) * scale, 0f);

        /// <summary>Background-art pixel (y from the TOP, the way art is measured) → world.</summary>
        private Vector3 ArtPxToWorld(Vector2 artPx) =>
            StageArtPointToWorld(new Vector2(artPx.x, _backgroundNative.y - artPx.y));

        // ── the bought dressing ─────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the room's dressing to match what the run says the bar owns. Called by
        /// the HUD whenever the owned set changes (a buy, a refund, a new run) — the stage
        /// itself never reads the run, it is told, the same way the till is told the money.
        /// Each piece is a world sprite in the fixture's own slot; a piece whose definition
        /// shines gets a Light2D of its own, hung at the sprite's glow line.
        /// </summary>
        public void SyncFixtures(IReadOnlyList<LastCall.Core.FixtureDefinition> owned)
        {
            foreach (var placed in _placedFixtures)
            {
                if (placed.Body != null) Destroy(placed.Body.gameObject);
                if (placed.Glow != null) Destroy(placed.Glow.gameObject);
            }
            _placedFixtures.Clear();
            foreach (var door in _tapDoors) if (door != null) Destroy(door.gameObject);
            _tapDoors.Clear();
            foreach (var sh in _shadows) if (sh.Blob != null) Destroy(sh.Blob.gameObject);
            _shadows.Clear();
            if (owned == null || _world == null) return;

            foreach (var def in owned)
            {
                if (!_slots.ContainsKey(def.Slot))
                {
                    Debug.LogWarning($"DiegeticStage: fixture '{def.Id}' wants unknown slot " +
                                     $"'{def.Slot}' — the stage was never handed it.");
                    continue;
                }
                var sprite = Resources.Load<Sprite>("Fixtures/" + def.Sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"DiegeticStage: fixture '{def.Id}' has no sprite " +
                                     $"'Fixtures/{def.Sprite}' — sold but not drawn.");
                    continue;
                }
                // Room dressing draws between the picture (10) and the bar (30); a piece
                // standing ON the counter must draw over the counter that holds it — the
                // candle was sorting behind the bar top and simply vanished (measured on
                // the first proof shot, 2026-08-10). The slot says which it is, so a new
                // counter-top place needs no code either.
                bool onCounter = _slots[def.Slot].OnCounter;
                var sr = WorldSprite("Fx_" + def.Id, sprite, order: onCounter ? 35 : 20);

                Light2D glow = null;
                if (def.HasLight)
                {
                    glow = PointLight("FxGlow_" + def.Id,
                        new Color(def.LightR, def.LightG, def.LightB),
                        def.LightIntensity, def.LightRadius);
                }
                _placedFixtures.Add((def, sr.transform, glow));

                // WHAT SELLS "STANDING ON" RATHER THAN "FLOATING NEAR": only pieces that
                // touch a surface get one. A sconce on the wall and a lantern on a cord
                // touch nothing, and a blob under them would read as a stain.
                if (!onCounter && !def.HasLight)
                    ContactShadow(sr.transform, sprite.rect.width * 0.9f);

                // A beer font is the door onto the draught station, so it answers the pointer.
                if (def.IsTap) BuildTapDoor(def, sr);
            }
            PlaceFixtures();
        }

        // ── the beer font is a door (2026-08-15) ────────────────────────────────
        // The author: "bira musluğuna tıklanması gereksin ... musluğa tıklanınca direkt bira
        // koyma sahnesi gelecek". The kegs left the back-bar wall, so the only way to a pint
        // is walking to the tap — which means the prop has to be clickable.

        private RectTransform _tapDoorRoot;
        private readonly List<RectTransform> _tapDoors = new List<RectTransform>();

        /// <summary>
        /// An invisible plate over a beer font, sized and stood exactly where the font is.
        ///
        /// The plate is CANVAS, not world: it inherits the flow's scrim for free (a panel open
        /// over the room blocks it, the way the till is blocked), which a physics raycast into
        /// world space would have had to re-implement. Its coordinates are the slot's own —
        /// the room art is authored at the design frame's own 640×360, so an art pixel IS a
        /// design unit and the cover fit is the identity. The till's fixed X/Y have always
        /// assumed the same thing; the ratio is written out anyway so the assumption is
        /// visible if a wider room is ever painted.
        ///
        /// Order 7: over the patrons (5) and the till (6), under the service flow (12). Over
        /// the patrons on purpose — a seat's hit rect is half again as wide as the bust in it,
        /// so the far edges of two of them reach across the fonts, and a door that the room
        /// swallows at the edges is a door the player learns not to trust. What it costs is a
        /// sliver of empty seat-rect beside each font, nowhere near anybody's body.
        /// </summary>
        private void BuildTapDoor(LastCall.Core.FixtureDefinition def, SpriteRenderer body)
        {
            LastCall.Game.StageSlot slot;
            if (!_slots.TryGetValue(def.Slot, out slot)) return;
            if (_tapDoorRoot == null)
            {
                _tapDoorRoot = OverlayCanvas("TapDoors", 7, raycasts: true);
                UiAuditExempt.Mark(_tapDoorRoot, "the tap door is a hit plate over a prop in "
                    + "the room, sized to the font's own art and stood in the font's own slot");
            }

            var art = body.sprite.rect.size;
            float sx = Reference.x / Mathf.Max(1f, _backgroundNative.x);
            float sy = Reference.y / Mathf.Max(1f, _backgroundNative.y);

            var plate = NewRect("TapDoor_" + def.Id, _tapDoorRoot);
            plate.anchorMin = plate.anchorMax = new Vector2(0, 0);
            plate.pivot = new Vector2(0.5f, 0);
            plate.sizeDelta = new Vector2(art.x * sx, art.y * sy);
            plate.anchoredPosition = new Vector2(slot.X * sx, slot.Y * sy);
            var hit = plate.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);   // invisible, but catches clicks
            var btn = plate.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => _onTapClicked?.Invoke());

            // THE AFFORDANCE IS THE PROP, not the plate: an invisible plate cannot light up
            // without drawing a rectangle over the counter, so the pointer lifts the FONT's
            // own brass instead. The base colour is read off the renderer rather than assumed
            // white, so a font standing in a dimmed room brightens from where it actually is.
            var lit = body;
            var rest = body.color;
            var trig = plate.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (lit != null) lit.color = rest * 1.22f; });
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (lit != null) lit.color = rest; });
            trig.triggers.Add(exit);

            _tapDoors.Add(plate);
        }

        /// <summary>Stands every placed piece in its slot for the CURRENT fit — the slots
        /// ride the room art the way the painted lamps do, so a re-fitted window moves the
        /// dressing with the picture rather than away from it.</summary>
        private void PlaceFixtures()
        {
            foreach (var placed in _placedFixtures)
            {
                LastCall.Game.StageSlot slot;
                if (!_slots.TryGetValue(placed.Def.Slot, out slot)) continue;
                var basePos = _backgroundSr != null
                    ? StageArtPointToWorld(new Vector2(slot.X, slot.Y))
                    : StageToWorld(slot.X, slot.Y);
                float k = _backgroundSr != null ? _backgroundScale : 1f;
                placed.Body.localScale = new Vector3(k, k, 1f);
                float h = placed.Body.GetComponent<SpriteRenderer>().sprite.bounds.size.y * k;
                placed.Body.position = basePos + new Vector3(0f, h * 0.5f, 0f);
                // The glow hangs at the piece's own light line — the flame, the belly of
                // the shade — which for every launch fixture is about ⅔ up the sprite.
                if (placed.Glow != null)
                    placed.Glow.transform.position = basePos + new Vector3(0f, h * 0.66f, 0f);
            }

            // The blobs ride their own pieces, so a re-fitted window moves the shadow with
            // the thing casting it rather than leaving it on the old floor.
            foreach (var sh in _shadows)
            {
                if (sh.Blob == null || sh.Under == null || sh.Blob.sprite == null) continue;
                float k = _backgroundSr != null ? _backgroundScale : 1f;
                var b = sh.Under.GetComponent<SpriteRenderer>();
                float footY = b != null ? b.bounds.min.y : sh.Under.position.y;
                var art = sh.Blob.sprite.bounds.size;
                sh.Blob.transform.localScale = new Vector3(
                    sh.Width * k / art.x, sh.Width * k * 0.28f / art.y, 1f);
                sh.Blob.transform.position = new Vector3(sh.Under.position.x, footY + 1f * k, 0f);
            }
        }

        /// <summary>
        /// The sky outside, on the shift's own clock: 0 at opening (18:00, the sun still
        /// golden over the skyline) and 1 at closing (02:00, the city deep in night).
        /// Straight proportion, the way the author asked for it — the frames are one
        /// continuous evening, so the hour and the picture advance together.
        ///
        /// TOLD, NOT READ, like the money and the slots: the stage never reaches into the run.
        /// Cheap to call every frame — it only touches the renderer when the frame changes,
        /// which at 81 frames over a 95-second shift is about once every 1.2 seconds.
        /// </summary>
        public void SetSkyFraction(float fraction)
        {
            if (_windowSr == null || _windowFrames == null || _windowFrames.Length == 0) return;
            int last = _windowFrames.Length - 1;
            // THE PICTURE STEPS AND THE LIGHT DOES NOT (2026-08-19, the author: "ışık
            // geçişleri havayla beraber smooth olmalı, kesik ışık geçişleri olmasa").
            //
            // These are two different kinds of thing and they were being driven as one. The
            // sky in the glass is PIXEL ART: 81 whole pictures, and it must land on one of
            // them — a blended sky is a blurred sky and that is the law this project is built
            // on (16 §6.10). Light is not drawn: it is a number, and a room whose light moves
            // in 81 steps over a shift changes brightness every 1.2 seconds in a visible
            // clunk. So the sprite takes the NEAREST frame and the light takes the EXACT
            // position between two of them.
            float exact = Mathf.Clamp01(fraction) * last;
            int i = Mathf.Clamp(Mathf.RoundToInt(exact), 0, last);
            if (i != _windowFrame)
            {
                _windowFrame = i;
                _windowSr.sprite = _windowFrames[i];
            }
            ApplySkyLight(exact);
        }

        /// <summary>
        /// READS THE SKY AND LIGHTS THE ROOM WITH IT.
        ///
        /// The sky, not the glass: only pixels above the horizon count (see SkyAlphaCut),
        /// because the city's own windows are pictures of light, not sources of it. Three
        /// numbers come out of one frame and each one has a job: the mean of the sky's
        /// brightest few per cent is the SUN — the band along the horizon, and the only part
        /// of a sky that behaves like a light source; the mean of the whole sky is the
        /// WASH, because ambient light is the average of everything the heavens send in; and
        /// the sky's own luma says how much evening is left. Sampled with a stride: this
        /// is a colour average, and averaging every ninth pixel of twenty thousand lands
        /// within a unit of averaging all of them.
        ///
        /// Cached per frame — the shift walks the frames once, but a new day walks them
        /// again, and re-reading twenty thousand pixels for a picture already measured is
        /// work nobody asked for.
        /// </summary>
        private void ApplySkyLight(float exact)
        {
            if (_windowFrames == null || _windowFrames.Length == 0) return;
            int last = _windowFrames.Length - 1;
            int a = Mathf.Clamp(Mathf.FloorToInt(exact), 0, last);
            int b = Mathf.Min(a + 1, last);
            float f = Mathf.Clamp01(exact - a);
            ReadSky(a);
            ReadSky(b);

            // Between the two plates the hour is standing between. Every number the room is
            // lit by is continuous; only the picture in the glass is not.
            _keyNow = Color.Lerp(_skyKey[a], _skyKey[b], f);
            _washNow = Color.Lerp(_skyWash[a], _skyWash[b], f);
            _dayNow = Mathf.Lerp(_skyDay[a], _skyDay[b], f);
            float day = _dayNow;

            if (_windowLight != null)
            {
                _windowLight.color = Punch(_keyNow, SkyPunch);
                _windowLight.intensity = Mathf.Lerp(WindowNight, WindowDay, day);
            }
            _washBase = Mathf.Lerp(WashNight, WashDay, day);
            _ceilingBase = Mathf.Lerp(CeilingNight, CeilingDay, day);
            if (_globalLight != null)
            {
                // Tinted, not painted — see AmbientPull. The beam above keeps SkyPunch.
                var wash = Neutralise(Punch(_washNow, WashPunch), AmbientPull);
                wash = Color.Lerp(wash, BounceTint, (1f - day) * NightBounce);
                var keyAmbient = Neutralise(Punch(_keyNow, SkyPunch), AmbientPull);
                _globalLight.color = Color.Lerp(wash, keyAmbient, WindowFillShare * day);
                _globalLight.intensity = _washBase;
            }
            if (_barLight != null)
                _barLight.intensity = Mathf.Lerp(BarLightNight, BarLightDay, day);
            float lampR = Mathf.Lerp(LampRadiusNight, LampRadiusDay, day);
            for (int i = 0; i < _lamps.Count; i++)
            {
                if (_lamps[i] == null) continue;
                _lamps[i].intensity = _ceilingBase;
                _lamps[i].pointLightOuterRadius = lampR;
                _lamps[i].pointLightInnerRadius = lampR * 0.15f;
            }
        }

        /// <summary>
        /// Measures ONE plate, once. The reading is what costs — twenty thousand pixels — so
        /// it is cached per frame and the lighting above only ever lerps between two answers
        /// it already has.
        /// </summary>
        private void ReadSky(int frame)
        {
            if (_windowFrames == null || frame < 0 || frame >= _windowFrames.Length) return;
            if (_skyRead == null)
            {
                int n = _windowFrames.Length;
                _skyRead = new bool[n]; _skyKey = new Color[n];
                _skyWash = new Color[n]; _skyDay = new float[n];
            }

            if (!_skyRead[frame])
            {
                var sp = _windowFrames[frame];
                var r = sp.textureRect;
                var px = sp.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                float sr = 0f, sg = 0f, sb = 0f, sl = 0f;
                int seen = 0;
                // Two passes over the same sample: the second wants the mean's own luma to
                // know what "the brightest few per cent" even means, so it cannot be folded
                // into the first.
                for (int p = 0; p < px.Length; p += 9)
                {
                    var c = px[p];
                    // Under 0.5 is the mullions and the frame; under the cut is the city,
                    // marked at alpha 254 by the build. Neither is sky.
                    if (c.a < SkyAlphaCut) continue;
                    float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    sr += c.r; sg += c.g; sb += c.b; sl += l; seen++;
                }
                if (seen == 0) { _skyRead[frame] = true; _skyKey[frame] = LampTint;
                                 _skyWash[frame] = GlobalTint; _skyDay[frame] = 1f; }
                else
                {
                    float meanL = sl / seen;
                    // The sun: everything brighter than halfway between the mean and white.
                    // A percentile would want a sort; this is the same cut without one, and
                    // on a sky — which is mostly flat bands — it lands on the horizon glow.
                    float cut = meanL + (1f - meanL) * 0.5f;
                    float hr = 0f, hg = 0f, hb = 0f; int hot = 0;
                    for (int p = 0; p < px.Length; p += 9)
                    {
                        var c = px[p];
                        if (c.a < SkyAlphaCut) continue;   // sky only, same gate as above
                        if (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f < cut) continue;
                        hr += c.r; hg += c.g; hb += c.b; hot++;
                    }
                    var wash = new Color(sr / seen, sg / seen, sb / seen, 1f);
                    _skyWash[frame] = wash;
                    // The glow, weighted by how much of the pane it actually covers — see
                    // SkyGlowFull. No area, no sun: the light through the glass becomes the
                    // sky's own colour and goes as cold as the sky is.
                    float glow = Mathf.Clamp01(hot / (float)seen / SkyGlowFull);
                    _skyKey[frame] = hot > 0
                        ? Color.Lerp(wash, new Color(hr / hot, hg / hot, hb / hot, 1f), glow)
                        : wash;
                    _skyDay[frame] = Mathf.Clamp01(
                        Mathf.InverseLerp(SkyLumaNight, SkyLumaDay, meanL));
                    // THE EVENING ONLY EVER DARKENS. The art wobbles — the city's glow
                    // catches the clouds for a frame near the end and the sky read comes
                    // back a shade brighter — and a room that brightens at midnight reads
                    // as dawn. The frames play in order, so the previous frame is measured
                    // by the time this one is, and this clamp makes the wobble one-way.
                    if (frame > 0 && _skyRead[frame - 1])
                        _skyDay[frame] = Mathf.Min(_skyDay[frame], _skyDay[frame - 1]);
                    _skyRead[frame] = true;
                }
            }
        }

        /// <summary>
        /// WHAT THE ROOM IS LIT BY RIGHT NOW, for the surfaces that a Light2D cannot reach.
        ///
        /// The back bar is a canvas — the player turns to face a wall of bottles and that
        /// wall is UI, so no light in the world touches it, and it used to sit at whatever
        /// brightness it was drawn at while the room around it moved through an evening
        /// (the author, 2026-08-19: "backbar sahnesi çok aydınlık, ortamın ışığına uygun
        /// değil"). Reading these lets it wear the same hour by hand.
        ///
        /// Told rather than computed twice: the sampling is <see cref="ApplySkyLight"/>'s and
        /// this is only the answer it already has.
        /// </summary>
        public float RoomDaylight => _dayNow;

        /// <summary>The light coming through the glass — warm at the sunset, cold at two.</summary>
        public Color RoomKeyLight => Punch(_keyNow, SkyPunch);

        /// <summary>The wash over everything, already dragged toward the lamps after dark.</summary>
        public Color RoomWashLight
        {
            get
            {
                // The SAME ambient the room is standing in — neutralised, not the raw sky.
                // The back bar reads this to dress its canvas, and a wash that is one colour
                // in the room and a louder one on the wall of bottles is two rooms.
                var wash = Neutralise(Punch(_washNow, WashPunch), AmbientPull);
                return Color.Lerp(wash, BounceTint, (1f - _dayNow) * NightBounce);
            }
        }

        /// <summary>
        /// Walks a colour back toward white by <paramref name="keep"/>. What a light does to
        /// a surface is multiply it, so a light that is only PART of the way from white to a
        /// hue tints what it falls on; one that goes all the way paints over it.
        /// </summary>
        private static Color Neutralise(Color c, float keep) =>
            new Color(1f + (c.r - 1f) * keep, 1f + (c.g - 1f) * keep, 1f + (c.b - 1f) * keep, 1f);

        /// <summary>
        /// Drives a colour away from its own grey. The sampled sky is a colour the eye reads
        /// as ATMOSPHERE — soft, because it is an average of a whole picture — and an average
        /// used straight as a light washes the room in mud. This keeps the hue the glass
        /// actually has and gives it the conviction a light needs.
        /// </summary>
        private static Color Punch(Color c, float amount)
        {
            float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return new Color(
                Mathf.Clamp01(l + (c.r - l) * amount),
                Mathf.Clamp01(l + (c.g - l) * amount),
                Mathf.Clamp01(l + (c.b - l) * amount), 1f);
        }

        /// <summary>
        /// The window's frames, cut out of the one sheet in Resources.
        ///
        /// The trailing cells of the last row are usually EMPTY — a frame count rarely fills a
        /// grid — so they are dropped by reading their alpha rather than by trusting a count
        /// constant that would go stale the first time the sheet is rebuilt. The scan is one
        /// cell's worth of pixels at a time and only ever reaches the tail, because it stops
        /// at the first cell that has something in it.
        /// </summary>
        private static Sprite[] LoadWindowFrames()
        {
            var sheet = Resources.Load<Texture2D>("Scene/window_cycle");
            if (sheet == null) return null;
            int cols = sheet.width / WindowCellW, rows = sheet.height / WindowCellH;
            if (cols < 1 || rows < 1)
            {
                Debug.LogWarning($"DiegeticStage: window_cycle is {sheet.width}×{sheet.height}, " +
                                 $"too small for a {WindowCellW}×{WindowCellH} cell — " +
                                 "the still plate will be used instead.");
                return null;
            }

            int count = cols * rows;
            while (count > 1 && IsCellEmpty(sheet, count - 1, cols)) count--;

            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                int r = i / cols, c = i % cols;
                // Sprite rects are measured from the texture's BOTTOM; the sheet is laid out
                // top-down, so the row index counts back from the top.
                var rect = new Rect(c * WindowCellW, sheet.height - (r + 1) * WindowCellH,
                                    WindowCellW, WindowCellH);
                // PPU 1: the stage runs at one world unit per art pixel, the same rule the
                // fixtures import under.
                frames[i] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), 1f);
            }
            return frames;
        }

        private static bool IsCellEmpty(Texture2D sheet, int index, int cols)
        {
            int r = index / cols, c = index % cols;
            int y = sheet.height - (r + 1) * WindowCellH;
            if (y < 0) return true;
            var px = sheet.GetPixels(c * WindowCellW, y, WindowCellW, WindowCellH);
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0.5f) return false;
            return true;
        }

        /// <summary>A point in the background art's own bottom-left space → world, through the
        /// cover fit (scaled about the centre, so the mapping is scale-about-centre too).</summary>
        private Vector3 StageArtPointToWorld(Vector2 artPoint) => new Vector3(
            (artPoint.x - _backgroundNative.x * 0.5f) * _backgroundScale,
            (artPoint.y - _backgroundNative.y * 0.5f) * _backgroundScale, 0f);

        // THE LETTERED SIGN IS GONE (2026-08-19, the author: "sahnedeki Last Call
        // neonunu ve ışığını kaldır"). It hung at art (470,300) on its own overlay canvas
        // at -9, blinked on NeonBlink, and threw a magenta spill into the room; all of it
        // went together, because a sign's light without its sign is a magenta stain on a
        // wall with nothing making it. The room's light now comes from the window and its
        // own lamps, and nothing else.

        // ── the till (canvas: it must draw OVER the HUD's seated patrons) ─────────

        private void BuildRegister()
        {
            if (registerSprite == null) return;

            // THE TILL STANDS ON THE BAR, SO IT STANDS IN FRONT OF THE DRINKERS.
            // The customers are HUD objects at sorting 5 — so the register gets its own
            // canvas at 6: over the seats, under the service flow (12) and the licence (20).
            // Its shadow and the wallet plaque draw at −7: under the patrons and the
            // dressing (−5), visible through the till's display window — and ABOVE the
            // fallback room, which also sits at −10. On the old single canvas the plaque
            // outdrew the fallback by sibling order; two canvases on the same order have
            // no defined order at all, and a lost art reference would have taken the
            // wallet with it.
            var backRoot = OverlayCanvas("RegisterBack", -7, raycasts: false);
            var frontRoot = OverlayCanvas("RegisterLayer", 6, raycasts: true);
            // THE TILL GOES OUT WITH THE CELLAR (2026-08-22). It stands on the bar and it does
            // NOT ride the room up — it is on its own overlay — so when the drawer lifts the
            // shelves, the till is left hanging over them like a price tag on the stock. It
            // is also nowhere in the author's open mock-up. Fading it is the honest reading:
            // while the cellar is open you are behind the bar, not at the register.
            _registerFade = new[]
            {
                backRoot.gameObject.AddComponent<CanvasGroup>(),
                frontRoot.gameObject.AddComponent<CanvasGroup>(),
            };
            // The till is a PROP standing on the counter, not a piece of the UI's furniture:
            // it is drawn at a hi-bit density into a fixed 57-unit footprint and everything
            // that floats off it is measured from where it stands. See UiAuditExempt.
            UiAuditExempt.Mark(backRoot, "the register is a prop in the room, placed where it "
                + "stands on the counter and drawn at 2x density into a fixed footprint");
            UiAuditExempt.Mark(frontRoot, "the register is a prop in the room, placed where it "
                + "stands on the counter and drawn at 2x density into a fixed footprint");

            var reg = NewRect("Register", frontRoot);
            reg.anchorMin = reg.anchorMax = new Vector2(0, 0);
            reg.pivot = new Vector2(0.5f, 0);
            // Fixed footprint (hi-bit): a 2x-density sprite renders finer pixels
            // into the same 57px slot instead of doubling on screen.
            const float regW = 57f;
            reg.sizeDelta = new Vector2(regW, regW * registerSprite.rect.height / registerSprite.rect.width);
            reg.anchoredPosition = new Vector2(RegisterX, RegisterBaseY);
            var regImg = reg.gameObject.AddComponent<Image>();
            regImg.sprite = registerSprite; regImg.preserveAspect = true;
            // The till is clickable: it opens the ledger of days gone by (GDD 24 §7).
            regImg.raycastTarget = true;
            var regBtn = reg.gameObject.AddComponent<Button>();
            regBtn.targetGraphic = regImg;
            regBtn.transition = Selectable.Transition.None;
            regBtn.onClick.AddListener(() => _onRegisterClicked?.Invoke());

            // A soft contact shadow under it — the thing that actually sells "resting on"
            // rather than "floating near".
            var shadow = NewRect("TillShadow", backRoot);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0, 0);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            shadow.sizeDelta = new Vector2(regW * 0.92f, 5f);
            shadow.anchoredPosition = new Vector2(RegisterX, RegisterBaseY + 1f);
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.color = new Color(0f, 0f, 0f, 0.42f); shImg.raycastTarget = false;

            // THE NUMBER, NOT THE PLAQUE (2026-08-19, the author: "ikisine gerek yok,
            // sadece sayi ile kasanin ustunde paramiz yazsin, bar kaldirilsin"). The
            // sunken display window and its dark plaque are gone; the wallet is one plain
            // number standing over the till - and on the FRONT layer, because the back
            // canvas draws behind the room and a number nobody can read is not a wallet.
            float regH = reg.sizeDelta.y;
            var money = NewRect("Money", frontRoot);
            money.anchorMin = money.anchorMax = new Vector2(0, 0);
            money.pivot = new Vector2(0.5f, 0f);
            money.sizeDelta = new Vector2(200, 20);
            money.anchoredPosition = new Vector2(RegisterX, RegisterBaseY + regH + 2f);
            _moneyText = NewText("Value", money, _display, 16, TextAnchor.LowerCenter, UITheme.Money);
            Stretch((RectTransform)_moneyText.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var moneyEdge = _moneyText.gameObject.AddComponent<Outline>();
            moneyEdge.effectColor = new Color(0f, 0f, 0f, 0.85f);
            moneyEdge.effectDistance = new Vector2(1f, -1f);
            _moneyText.raycastTarget = false;
            _moneyText.text = "$0";

            // WHERE THE MONEY MOVES, THE CHANGE SHOWS (2026-08-14, the author). The till is
            // the wallet now — the top bar's copy of it is gone — so every rise and fall says
            // so ON THE MACHINE: +$12 in green, −$14 in red, lifting off the drawer and fading
            // over two seconds. The spawn point is a child of the plaque, so it needs no
            // coordinate conversion and follows the register wherever the room's fit puts it.
            // ON THE FRONT LAYER, not the plaque's own. The money window is drawn on the
            // register's BACK canvas (order −7) so the room stands in front of it — which is
            // right for a number sunk into the machine, and wrong for anything that has to be
            // seen: the first cut of this floated the change behind the bar (measured, and
            // invisible). It rides the layer the till's own click surface already uses, at the
            // same screen coordinates, because both canvases are the same overlay at the same
            // reference size.
            _moneyFloatHost = NewRect("Change", frontRoot);
            _moneyFloatHost.anchorMin = _moneyFloatHost.anchorMax = new Vector2(0, 0);
            _moneyFloatHost.pivot = new Vector2(0.5f, 0f);
            _moneyFloatHost.sizeDelta = new Vector2(200, 22);
            _moneyFloatHost.anchoredPosition = new Vector2(
                RegisterX, RegisterBaseY + regH + 24f);
        }

        private RectTransform _moneyFloatHost;

        /// <summary>How long a change hangs over the till before it is gone.</summary>
        private const float MoneyFloatSeconds = 2f;

        /// <summary>
        /// Lifts a change off the till (2026-08-14, the author's brief, to the letter): the
        /// figure in GREEN when it rises and RED when it falls, a WHITE outline around that,
        /// and a BLACK outline outside the white. Two stacked outlines is the only way to get
        /// two rings out of one label, and the order matters — the black one is added last so
        /// it draws furthest out.
        /// </summary>
        public void FloatMoney(int delta)
        {
            if (delta == 0 || _moneyFloatHost == null) return;
            var rt = NewRect("D", _moneyFloatHost);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = NewText("T", rt, _display, 16, TextAnchor.MiddleCenter,
                delta > 0 ? UITheme.Lime[3] : UITheme.ViceRed[3]);
            Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = (delta > 0 ? "+" : "−") + "$" + Mathf.Abs(delta);

            var white = text.gameObject.AddComponent<Outline>();
            white.effectColor = Color.white;
            white.effectDistance = new Vector2(1.5f, 1.5f);
            var black = text.gameObject.AddComponent<Outline>();
            black.effectColor = Color.black;
            black.effectDistance = new Vector2(3f, 3f);

            StartCoroutine(LiftAndFade(rt, text));
        }

        private System.Collections.IEnumerator LiftAndFade(RectTransform rt, Text text)
        {
            float t = 0f;
            var from = rt.anchoredPosition;
            while (t < MoneyFloatSeconds && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / MoneyFloatSeconds);
                rt.anchoredPosition = from + new Vector2(0, 26f * Mathf.SmoothStep(0f, 1f, k));
                // It holds its colour for the first half and then goes; a fade that starts at
                // once reads as a flicker rather than as money leaving the room.
                float a = k < 0.5f ? 1f : 1f - (k - 0.5f) * 2f;
                var c = text.color; c.a = a; text.color = c;
                foreach (var o in text.GetComponents<Outline>())
                {
                    var oc = o.effectColor; oc.a = a; o.effectColor = oc;
                }
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        private static RectTransform OverlayCanvas(string name, int order, bool raycasts)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            if (raycasts) go.AddComponent<GraphicRaycaster>();
            // Fixed field, like the HUD's: what draws over the room has to be measured in
            // the same units the room is, and the room is now windowboxed to 640x360.
            return DesignFrame.Wrap((RectTransform)go.transform, Reference);
        }

        // ── the procedural fallback room (no environment art wired) ──────────────

        /// <summary>The flat stand-in room, canvas-drawn as it always was: a dev safety net,
        /// not a lit scene. A broken art reference should look wrong, not invisible.</summary>
        private void BuildFallbackRoom()
        {
            var root = OverlayCanvas("FallbackRoom", -10, raycasts: false);

            var sky = FullLayer(root, "SkyCity", UITheme.Night[0]);
            Window(sky, new Vector2(60, 40), new Vector2(70, 300));
            Window(sky, new Vector2(80, 44), new Vector2(510, 300));

            var far = NewRect("ClubFar", root);
            Stretch(far, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var wall = NewRect("BackWall", far);
            Stretch(wall, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, CounterFrontY), Vector2.zero);
            var wallImg = wall.gameObject.AddComponent<Image>();
            wallImg.color = UITheme.Night[1]; wallImg.raycastTarget = false;
            AddCrowd(far);

            var mid = FullLayer(root, "ClubMid", new Color(0, 0, 0, 0));
            AddNeonSigns(mid);
        }

        /// <summary>
        /// Ambient life, deliberately sparse: the room flickers for a frame every few
        /// seconds — the GLOBAL LIGHT dips now, which is what a mains stutter is. Purely
        /// cosmetic; this jitter never touches RunRng, so run determinism is unaffected.
        /// </summary>
        // ── the closing beat (GDD 26 §7, PLAN_last_call S4) ─────────────────────
        //
        // When the last customer is on their stool the room says so, in the language it
        // already has: the ceiling comes down, the wash thins, the neon over the door burns
        // harder, and ONE lamp finds the person at the bar. Nothing is drawn for this — every
        // number below is an intensity on a light that was already hanging there, which is
        // what keeps it from reading as a different game for thirty seconds.

        /// <summary>How far the ceiling and the wash drop for the last call. The sign used
        /// to burn HARDER here (ClosingSign 1.9) — it went with the sign itself, 2026-08-19.</summary>
        private const float ClosingCeiling = 0.22f, ClosingWash = 0.55f;

        /// <summary>The ceiling, kept by reference: the closing beat dims every one of them
        /// each frame, and finding them by name would rebuild four strings a frame to do it.</summary>
        private readonly List<Light2D> _lamps = new List<Light2D>();

        /// <summary>The lamp over the guest, built dark and only ever lit for them.</summary>
        private Light2D _guestLight;
        private bool _closing;
        private float _closingT;            // 0 = the ordinary room, 1 = the last call
        private float _guestWorldX;

        /// <summary>
        /// Turns the closing beat on or off and says WHERE the person is, in HUD units (the
        /// stool's own x). Called every frame by the HUD — it is idempotent, and the fade is
        /// this class's business, not the caller's.
        /// </summary>
        public void SetClosingBeat(bool on, float hudX)
        {
            _closing = on;
            _guestWorldX = hudX / (720f / 360f);
        }

        /// <summary>Drives the fade. Separate from <see cref="Ambient"/> because that one is a
        /// coroutine that Motion.Reduced switches off, and a player who has asked for less
        /// movement still gets the light — it simply arrives without the ramp.</summary>
        private void StepClosing()
        {
            float target = _closing ? 1f : 0f;
            if (Motion.Reduced) _closingT = target;
            else if (!Mathf.Approximately(_closingT, target))
                _closingT = Mathf.MoveTowards(_closingT, target, Time.unscaledDeltaTime / 1.1f);
            else if (_guestLight == null || !_closing) { if (_closingT <= 0f) return; }

            float t = Mathf.SmoothStep(0f, 1f, _closingT);

            // FROM the hour the sky left the room, DOWN TO A LEVEL — and the level is
            // absolute, not a fraction of wherever it started. "The ceiling comes down"
            // means the room actually goes dark for the beat, whatever time it is; taking
            // 22% off a night that is already at 1.85 leaves 0.41, which is brighter than
            // the room this beat was written against and is not a room going dark at all.
            // So the sky decides where the fall STARTS and the beat decides where it ENDS.
            if (_globalLight != null)
                _globalLight.intensity = Mathf.Lerp(_washBase, GlobalIntensity * ClosingWash, t);
            float ceilingY = 0f;
            for (int i = 0; i < _lamps.Count; i++)
            {
                if (_lamps[i] == null) continue;
                _lamps[i].intensity = Mathf.Lerp(_ceilingBase, LampIntensity * ClosingCeiling, t);
                ceilingY = _lamps[i].transform.position.y;
            }
            // The window dies with the room: at the last call the light outside is not what
            // the beat is about, and leaving it burning kept a bright hole in a dark room.
            if (_windowLight != null)
                _windowLight.intensity = Mathf.Lerp(_windowLight.intensity,
                    Mathf.Lerp(WindowNight, WindowDay, _dayNow) * Mathf.Lerp(1f, ClosingWash, t),
                    1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));

            if (_guestLight == null && _world != null && t > 0.001f)
                _guestLight = PointLight("LastCallLamp", LampTint, 0f, LampRadius * 1.15f);
            if (_guestLight != null)
            {
                // It hangs where the other lamps hang — the room has one ceiling, and a pool
                // that floated at mid-wall would read as a spotlight from nowhere.
                // Against the beat's OWN ceiling, which the block above lands on an absolute
                // level — so this is the constant it always was, and the contrast it buys
                // (2.1 against 0.22 of the same base) is nine to one at every hour.
                _guestLight.intensity = Mathf.Lerp(0f, LampIntensity * 2.1f, t);
                _guestLight.transform.position = new Vector3(_guestWorldX, ceilingY, 0f);
                if (_guestLight.gameObject.activeSelf != (t > 0.002f))
                    _guestLight.gameObject.SetActive(t > 0.002f);
            }
        }

        private System.Collections.IEnumerator Ambient()
        {
            float nextFlicker = Random.Range(3f, 7f);
            while (true)
            {
                nextFlicker -= Time.unscaledDeltaTime;
                if (nextFlicker <= 0f && _globalLight != null)
                {
                    // OFF THE HOUR'S OWN LEVEL, not off a constant (2026-08-19). The flicker
                    // dipped to GlobalIntensity*0.72 and then RESTORED to GlobalIntensity —
                    // which was fine while the wash was a constant and is a bug now that the
                    // sky sets it: the first flicker of the night pinned the room at 0.85
                    // for good, and every hour after it lit the same. It dips from where the
                    // evening left the room and puts it back there.
                    _globalLight.intensity = _washBase * 0.72f;
                    yield return new WaitForSecondsRealtime(0.05f);
                    if (_globalLight != null) _globalLight.intensity = _washBase;
                    nextFlicker = Random.Range(3f, 7f);
                }
                yield return null;
            }
        }

        private RectTransform FullLayer(RectTransform root, string name, Color fill)
        {
            var layer = NewRect(name, root);
            Stretch(layer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = layer.gameObject.AddComponent<Image>();
            img.color = fill;
            img.raycastTarget = false;
            return layer;
        }

        private void Window(RectTransform layer, Vector2 size, Vector2 pos)
        {
            var w = NewRect("Window", layer);
            Place(w, new Vector2(0, 0), size, pos);
            var img = w.gameObject.AddComponent<Image>();
            img.color = new Color(UITheme.ClubBlue[1].r, UITheme.ClubBlue[1].g, UITheme.ClubBlue[1].b, 0.55f);
            img.raycastTarget = false;
        }

        private void AddCrowd(RectTransform layer)
        {
            // Dim head + shoulder silhouettes across the dance floor, well above the bar
            // surface so they read as patrons in the club behind the counter.
            for (int i = 0; i < 9; i++)
            {
                var head = NewRect($"Head{i}", layer);
                Place(head, new Vector2(0, 0), new Vector2(22, 34), new Vector2(40 + i * 70, 196));
                var img = head.gameObject.AddComponent<Image>();
                img.color = i % 2 == 0 ? UITheme.Night[2] : UITheme.Night[3];
                img.raycastTarget = false;
            }
        }

        private void AddNeonSigns(RectTransform layer)
        {
            // Two small accents high on the back wall — the procedural stand-ins for a
            // missing room picture. The "LAST CALL" sign that hung between them went with
            // the real one (2026-08-19): a fallback that shows a sign the room no longer
            // has is a fallback that lies about the room.
            NeonSign(layer, UITheme.Cyan[3], new Vector2(56, 10), new Vector2(120, 322), null);
            NeonSign(layer, UITheme.Magenta[3], new Vector2(48, 10), new Vector2(548, 316), null);
        }

        private void NeonSign(RectTransform layer, Color c, Vector2 size, Vector2 center, string label)
        {
            // Glow halo (dim, larger) + bright core — glow = hand-placed halo, no shader.
            var halo = NewRect("Halo", layer);
            Place(halo, new Vector2(0.5f, 0.5f), size + new Vector2(12, 12), center);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.color = new Color(c.r, c.g, c.b, 0.25f); haloImg.raycastTarget = false;
            var core = NewRect("Sign", layer);
            Place(core, new Vector2(0.5f, 0.5f), size, center);
            var coreImg = core.gameObject.AddComponent<Image>();
            coreImg.color = c; coreImg.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText("Label", core, _display, 12, TextAnchor.MiddleCenter, UITheme.Night[0]);
                Stretch((RectTransform)t.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
