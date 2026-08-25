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
    // TycoonHud, part Book: the recipe book: the drawn booklet, its page turns, its contents and its pages.
    //
    // One class in nine files (2026-08-25). The HUD had grown to 13,359 lines in
    // one place: every edit had to read it whole, every grep answered out of it,
    // and two sessions could not work on two different screens without landing in
    // the same diff. The STATE stays in TycoonHud.cs -- every field, every const,
    // every nested type, in its original order -- and only whole methods moved, so
    // nothing about construction order or serialisation can have changed.
    public sealed partial class TycoonHud
    {
        /// <summary>The mark on the BOOK key: how many pages are waiting to be looked at.
        /// It is the only part of the news that survives the notice fading.</summary>
        private void RefreshBookBadge()
        {
            if (_bookBadge == null) return;
            bool any = _perfectNews.Count > 0;
            _bookBadge.gameObject.SetActive(any);
            if (any && _bookBadgeText != null)
                _bookBadgeText.text = _perfectNews.Count.ToString();
        }

        /// <summary>Books a face to a visit — and, when this is the first sight of the person
        /// behind it, to them for the rest of the run — and stamps it as the most recently
        /// seen, so the next stranger through the door is given somebody else.</summary>
        private PatronLook BookFace(CustomerVisit visit, string person, PatronLook look)
        {
            _faceOfVisit[visit] = look;
            if (person != null) _faceOfPerson[person] = look;
            _faceLastSeen[look] = ++_faceClock;
            return look;
        }

        /// <summary>What the books say about one day, or null for a day they never kept
        /// (the calendar can be wound forward by the dev tool, which books nothing).</summary>
        private static DayResult BookFor(TycoonRun run, int day)
        {
            var history = run.Ledger.History;
            for (int i = history.Count - 1; i >= 0; i--)
                if (history[i].Day == day) return history[i];
            return null;
        }

        /// <summary>
        /// THE BOOK, SHUT, STANDING ON THE BAR. Clicking it opens the menu — the same verb
        /// the grey BOOK key carried, given the object it was always a label for.
        ///
        /// Drawn at 1:2, one art pixel to two HUD units, which is the counter's own grain
        /// (StageToHud): the room is 640x360 drawn at 1280x720, so anything standing in it at
        /// any other ratio has finer or coarser pixels than the bar it is standing on. The
        /// sprite is struck at exactly the size it draws (Tools/book_closed_gen.py) for the
        /// same reason.
        ///
        /// No hover LIFT, only the glow: it is standing on a surface, and a book that rises
        /// off the bar under the pointer is a book nobody put down.
        /// </summary>
        private RectTransform BuildBookProp(RectTransform root)
        {
            var art = ItemArt.Load("book_closed");

            // The contact shadow first, so it draws under: the thing that actually sells
            // "resting on" rather than "floating near" (the till's own words).
            _bookShadow = NewRect("BookShadow", root);
            _bookShadow.anchorMin = _bookShadow.anchorMax = new Vector2(0.5f, 0);
            _bookShadow.pivot = new Vector2(0.5f, 0.5f);
            _bookShadow.sizeDelta = new Vector2(52f, 10f);
            var shadow = _bookShadow.gameObject.AddComponent<Image>();
            shadow.sprite = BackBarArt.BottleShadow();
            shadow.color = new Color(0f, 0f, 0f, 0.42f);
            shadow.raycastTarget = false;

            var prop = NewRect("BookProp", root);
            prop.anchorMin = prop.anchorMax = new Vector2(0.5f, 0);
            prop.pivot = new Vector2(0.5f, 0);           // stood on its own foot
            prop.sizeDelta = art != null
                ? art.rect.size * StageToHud
                : new Vector2(56f, 110f);
            var img = prop.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.preserveAspect = true;
            // No art on disk: a plain board in the cover's own colour, still pressable. A
            // missing sprite must never take the way into the book with it.
            if (art == null) img.color = UITheme.Amber[0];
            // THE HAND REACHES THE BOOK LOW (2026-08-25, measured by the PlayMode suite the
            // day the book moved beside the sink): the stool rects are 150 wide on a 180
            // pitch and 330 tall, so their click columns cover the whole counter band and a
            // 56-wide prop cannot stand between them. At x -336 the book's full-height
            // catch sat exactly under Seat1's centre (298, 224) and, built later, WON the
            // raycast — the suite's stool click opened the menu instead of the licence,
            // four runs straight, on two editor instances. The ART no longer raycasts; the
            // bottom 60 units carry the click (top edge 213, under the seat row's 224), and
            // the Button and HoverGlow on the prop hear it by event bubbling.
            img.raycastTarget = false;
            var reach = NewRect("Reach", prop);
            reach.anchorMin = new Vector2(0f, 0f);
            reach.anchorMax = new Vector2(1f, 0f);
            reach.pivot = new Vector2(0.5f, 0f);
            reach.offsetMin = Vector2.zero;
            reach.offsetMax = new Vector2(0f, 60f);
            var reachImg = reach.gameObject.AddComponent<Image>();
            reachImg.color = new Color(0f, 0f, 0f, 0.001f);
            var btn = prop.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(ToggleRecipeBook);
            _bookGlow = prop.gameObject.AddComponent<HoverGlow>();
            _bookGlow.Graphics = new UnityEngine.UI.Graphic[] { img };
            _bookImg = img;
            UiAuditExempt.Mark(prop, "the recipe book is a prop standing on the counter, "
                + "drawn at the counter's own grain from its own closed art");

            // THE BOOK SAYS WHAT IT IS (2026-08-25, the author: "secilebilir oldugu
            // anlasilmasi icin parlamali ve mouse ile ustune gelindiginde menuyu ac
            // yazmali"). The glow was already there and could not be seen, because the
            // book's own art is the brightest thing on the bar and 1.22x of bright is
            // bright. Tinting it with the room (below) is what gives the glow somewhere to
            // go; this is the other half — a prop that opens a whole screen deserves to
            // say so before it is clicked, rather than after.
            _bookLabel = NewRect("BookLabel", root);
            _bookLabel.anchorMin = _bookLabel.anchorMax = new Vector2(0.5f, 0);
            _bookLabel.pivot = new Vector2(0.5f, 0f);
            _bookLabel.sizeDelta = new Vector2(132f, 22f);
            var plate = _bookLabel.gameObject.AddComponent<Image>();
            plate.sprite = ChromeArt.Card();
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.Night[1];
            plate.raycastTarget = false;
            var line = NewText("Line", _bookLabel, _display, 8, TextAnchor.MiddleCenter,
                               UITheme.Amber[4]);
            Stretch((RectTransform)line.transform, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);
            line.text = "OPEN THE MENU";
            line.raycastTarget = false;
            _bookLabelGroup = _bookLabel.gameObject.AddComponent<CanvasGroup>();
            _bookLabelGroup.alpha = 0f;
            _bookLabelGroup.blocksRaycasts = false;
            _bookLabelGroup.interactable = false;
            var relay = prop.gameObject.AddComponent<HoverRelay>();
            relay.Entered = () => _bookHovered = true;
            relay.Exited = () => _bookHovered = false;

            _bookProp = prop;
            PlaceBookProp();
            return prop;
        }

        private HoverGlow _bookGlow;
        private Image _bookImg;
        private RectTransform _bookLabel;
        private CanvasGroup _bookLabelGroup;
        private bool _bookHovered;

        /// <summary>How fast the label arrives and leaves. The roller's own peek time, so
        /// every hint in this room answers at the same speed.</summary>
        private const float BookLabelFade = 0.14f;

        /// <summary>
        /// The book, lit by the room and answering the pointer. Called every frame from
        /// <see cref="PlaceBookProp"/>, which already runs there.
        ///
        /// THE TINT IS THE POINT (2026-08-25, the author: "golgelendirmelerden etkilenmiyor
        /// kasa gibi etkilenmeli"). The room is lit by URP 2D lights and everything standing
        /// IN it is tinted by them — but the book is a prop on a CANVAS, and no light in
        /// Unity reaches a canvas. So it wore its own daylight colours through every hour of
        /// the night, which is precisely the "pasted on" the author is describing: not a
        /// question of where it stands, but that it is the one thing on the bar the evening
        /// never touches. <see cref="DiegeticStage.RoomWashLight"/> is the room's own answer
        /// to that and has been sitting unread since the back bar page that used it was
        /// deleted; this is its consumer.
        /// </summary>
        private void DressBookProp()
        {
            if (stage == null) return;
            if (_bookGlow != null) _bookGlow.Retint(stage.RoomWashLight);
            else if (_bookImg != null) _bookImg.color = stage.RoomWashLight;

            if (_bookLabelGroup != null)
            {
                float want = _bookHovered && !_bookOpen ? 1f : 0f;
                _bookLabelGroup.alpha = Motion.Reduced ? want : Mathf.MoveTowards(
                    _bookLabelGroup.alpha, want, Time.unscaledDeltaTime / BookLabelFade);
            }
        }

        /// <summary>
        /// Stands the book on the bar for THIS frame. The counter rises when the cellar opens
        /// (DrawerTravel), and anything resting on it has to rise with it or the bar comes up
        /// through the book — the same lift the stools take, read off the same dial.
        /// </summary>
        private void PlaceBookProp()
        {
            if (_bookProp == null) return;
            float lift = CounterLift;
            // ON THE DRAWN SURFACE, not on the rest line. CounterLineY is the counter's BACK
            // edge — the line the room crops a drinker at — and the top the bar's props
            // actually stand on reads 36 units lower in the scene. The dirty glass learned
            // this the same way, off the author's own report, and carries the same number.
            var foot = new Vector2(BookPropX, CounterLineY - 36f + lift);
            _bookProp.anchoredPosition = foot;
            if (_bookShadow != null) _bookShadow.anchoredPosition = foot + new Vector2(0f, 2f);
            // The label rides above the book's own head, so it follows the counter's lift
            // and never has to be placed twice.
            if (_bookLabel != null)
                _bookLabel.anchoredPosition = foot + new Vector2(0f, _bookProp.sizeDelta.y + 6f);
            DressBookProp();
        }

        internal void ToggleRecipeBook()
        {
            if (_bookPanel == null) return;
            bool open = !_bookOpen;
            _bookOpen = open;
            Sfx.Play("click", 0.6f);
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            // A close mid-turn abandons the turn where it stands; the next open rebuilds
            // the resting spread, so the leaf can never be left hanging over the gutter.
            if (_bookTurnAnim != null)
            {
                StopCoroutine(_bookTurnAnim);
                _bookTurnAnim = null;
                _bookTurning = false;
            }
            if (open)
            {
                if (!_bookPanel.gameObject.activeSelf)
                {
                    // First frame of the drop: book parked above the screen, scrim clear.
                    sheet.anchoredPosition = new Vector2(0, BkParkY);
                    var c = UITheme.Scrim;
                    _bookPanel.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0);
                }
                _bookPanel.gameObject.SetActive(true);
                _bookPanel.SetAsLastSibling();   // over the service log and everything else
                RebuildRecipeBook();
            }
            if (_bookAnim != null) StopCoroutine(_bookAnim);
            _bookAnim = StartCoroutine(BookSlide(open));
        }

        /// <summary>The board drops down from above and lifts back away (the author asked
        /// for a smooth open and close). Reduced motion snaps, as everywhere.</summary>
        private System.Collections.IEnumerator BookSlide(bool open)
        {
            var sheet = _bookPanel.Find("Sheet") as RectTransform;
            var scrim = _bookPanel.GetComponent<Image>();
            var c = UITheme.Scrim;
            float fromY = sheet.anchoredPosition.y, toY = open ? 0f : BkParkY;
            float fromA = scrim.color.a, toA = open ? c.a : 0f;
            if (!Motion.Reduced)
            {
                float dur = open ? 0.42f : 0.32f;   // a board this size does not snap
                for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
                {
                    float k = t / dur;
                    k = open ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k * k;
                    sheet.anchoredPosition = new Vector2(0, Mathf.Lerp(fromY, toY, k));
                    scrim.color = new Color(c.r, c.g, c.b, Mathf.Lerp(fromA, toA, k));
                    yield return null;
                }
            }
            sheet.anchoredPosition = new Vector2(0, toY);
            scrim.color = new Color(c.r, c.g, c.b, toA);
            if (!open) _bookPanel.gameObject.SetActive(false);
            _bookAnim = null;
        }

        private void BuildRecipeBook(RectTransform root)
        {
            _bookPanel = NewRect("RecipeBook", root);
            Stretch(_bookPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = _bookPanel.gameObject.AddComponent<Image>();
            scrim.color = UITheme.Scrim;
            // THE DIM CLOSES IT (2026-08-11, the author, reversing the earlier ruling that
            // only the X may: "kimlikteki gibi assetin dışına arka plana tıklandığında
            // otomatik kapanmalı ya da esc"). It is the licence's own behaviour, so the two
            // sheets now shut the same way, and it is the reason the X could go. The board
            // itself still swallows its clicks — BoardCatch below — so reading the page
            // cannot close the page.
            var scrimBtn = _bookPanel.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(() => { if (_bookOpen) ToggleRecipeBook(); });
            // The book also outranks the back-bar flow (canvas 12):
            // its own canvas at 15 lets the BOOK key on the flow's ledge show the thing.
            var bookCanvas = _bookPanel.gameObject.AddComponent<Canvas>();
            bookCanvas.overrideSorting = true;
            bookCanvas.sortingOrder = 15;
            _bookPanel.gameObject.AddComponent<GraphicRaycaster>();

            // THE MENU IS AN OPEN BOOK (2026-08-24): menu_booklet.png at exactly 2× —
            // the clipboard board was 396×248 stretched onto 1148×719, a 2.899× fractional
            // upscale, and the reason its clip and its grain never looked as crisp as the
            // room behind it. Every rect below is placed against the generator's own
            // printed ruler (Tools/menu_booklet.py), never measured off the PNG after
            // the fact — the same law HeadY lives under.
            var sheet = NewRect("Sheet", _bookPanel);
            Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(BkSheetW, BkSheetH), Vector2.zero);
            var boardImg = sheet.gameObject.AddComponent<Image>();
            var boardSprite = ItemArt.Load("menu_booklet");
            if (boardSprite != null) boardImg.sprite = boardSprite;
            else boardImg.color = UITheme.Cream[4];
            // The visible leather swallows its clicks so reading the page cannot close the
            // page; the dim around it still can. The catcher stops at the board's edge —
            // the ribbon's tail hangs below it, and a ribbon is not a door.
            boardImg.raycastTarget = false;
            var boardCatch = NewRect("BoardCatch", sheet);
            Place(boardCatch, new Vector2(0.5f, 0.5f), new Vector2(BkSheetW, BkBoardH),
                new Vector2(0, BkLiftY));
            var bcImg = boardCatch.gameObject.AddComponent<Image>();
            bcImg.color = new Color(0, 0, 0, 0.001f);
            boardCatch.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // No X (2026-08-11). The dim behind the book closes it and so does Escape, which
            // is what the licence has always done — and a corner button that duplicates a
            // gesture the player already has is a button that has to be found first.

            // THE FOUR PRINT WINDOWS AND THE LEAF, in paint order. Each page's print — the
            // gold furniture, the chapter heading, the cards, the folio — lives in one
            // container inside a clipping window, so a turn can CLIP it at the fold and
            // SHIFT it with the sheet, never scale it (menu_booklet.py: "Nothing anywhere
            // is scaled, so there is nothing to smear and nothing to slide"). Bottom to
            // top: the page being revealed right of the roll, the resting left page, the
            // drawn leaf, the front print on the unturned half, and the back-face print
            // riding the flipped half over the gutter.
            _bookWinInR = BookWindow(sheet, "WinInR", out _bookPageInR);
            _bookWinRestL = BookWindow(sheet, "WinRestL", out _bookPageRestL);
            var leafRt = NewRect("Leaf", sheet);
            Place(leafRt, new Vector2(0.5f, 0.5f), new Vector2(BkLeafW, BkPageH),
                new Vector2(0, BkLiftY));
            _bookLeaf = leafRt.gameObject.AddComponent<Image>();
            _bookLeaf.raycastTarget = false;
            _bookLeaf.enabled = false;
            _bookWinRestR = BookWindow(sheet, "WinRestR", out _bookPageRestR);
            _bookWinInL = BookWindow(sheet, "WinInL", out _bookPageInL);

            // The page corners turn the page — the same corner the drawn peel lifts from.
            BookCorner(sheet, "TurnFwd", +1);
            BookCorner(sheet, "TurnBack", -1);
            // And the corners SAY so now (2026-08-24, the author: "sağ ve sol ok
            // butonları koy"): a drawn paper key on each page's bottom outer corner,
            // standing only while there is a page on that side to turn to.
            _bookPrevKey = BookPaperKey(sheet, "PrevKey", "<", -1);
            _bookNextKey = BookPaperKey(sheet, "NextKey", ">", +1);
            // And over the back arrow, the way home (the author: "en başa geç oku da
            // olmalı") — it jumps to the title spread rather than riffling there.
            _bookHomeKey = BookPaperKey(sheet, "HomeKey", "<<", 0);
            // BESIDE the back arrow, not above it: stacked, it stood on the provenance
            // line's own row, and a long origin ("THE JULEP'S CITY COUSIN, 1880S") grows
            // toward it. The keys share the foot; the folio is 130px to their right.
            _bookHomeKey.anchoredPosition = _bookPrevKey.anchoredPosition + new Vector2(44f, 0f);

            _bookLeafFrames = new Sprite[BkTurnFrames];
            for (int i = 0; i < BkTurnFrames; i++)
                _bookLeafFrames[i] = ItemArt.Load($"menu_page_{i:00}");

            _bookPanel.gameObject.SetActive(false);
        }

        /// <summary>A full-width band `h` units tall, `down` units below the parent's top
        /// edge. `Hairline` can only sit ON an edge and is one unit thick; a beam is built
        /// from a few bands stacked down its face, which is what gives it a top the room
        /// lights and a front that falls away from it.</summary>
        private Image Band(RectTransform parent, string name, float down, float h, Color c)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, h);
            rt.anchoredPosition = new Vector2(0, -down);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        private void Hairline(RectTransform parent, Vector2 aMin, Vector2 aMax, Color c)
        {
            var r = NewRect("HL", parent);
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.pivot = new Vector2(0.5f, aMin.y);
            r.sizeDelta = new Vector2(0, 1);
            r.anchoredPosition = Vector2.zero;
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        private void HairlineV(RectTransform parent, float ax, Color c)
        {
            var r = NewRect("VL", parent);
            r.anchorMin = new Vector2(ax, 0); r.anchorMax = new Vector2(ax, 1);
            r.pivot = new Vector2(ax, 0.5f);
            r.sizeDelta = new Vector2(1, 0);
            r.anchoredPosition = Vector2.zero;
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        private void RebuildRecipeBook()
        {
            var run = Run;
            if (run == null || _bookPanel == null) return;
            _bookPages.Clear();
            _bookChapters.Clear();

            // The opening spread: the house's title plate, and the contents facing it —
            // the menu itself starts when that first page is turned (the author:
            // "ilk sayfa çevrilince menü başlamalı").
            _bookPages.Add(new BookPage { Kind = BookPageKind.Title });
            _bookPages.Add(new BookPage { Kind = BookPageKind.Contents });

            // ONE PAGE, ONE RECIPE, grouped whole (the author: "Tüm menü gruplandırılır"):
            // every drink stands under its tier in rank order, the unowned ones locked in
            // place among their own — the book is the progression map, chapter by chapter.
            var all = new List<(RecipeDefinition Recipe, bool Locked)>();
            foreach (var r in run.MenuRecipes) all.Add((r, false));
            foreach (var r in run.LockedRecipes) all.Add((r, true));
            all.Sort((a, b) => a.Recipe.Rank != b.Recipe.Rank
                ? a.Recipe.Rank.CompareTo(b.Recipe.Rank)
                : string.CompareOrdinal(a.Recipe.Id, b.Recipe.Id));

            BookChapter chapter = null;
            foreach (var (r, isLocked) in all)
            {
                string tier = TierName(r.Rank);
                if (chapter == null || chapter.Title != tier)
                {
                    chapter = new BookChapter { Title = tier, FirstPage = _bookPages.Count };
                    _bookChapters.Add(chapter);
                }
                chapter.Count++;
                if (isLocked) chapter.LockedCount++;
                _bookPages.Add(new BookPage
                {
                    Kind = BookPageKind.Recipe,
                    Chapter = tier,
                    Recipe = r,
                    Locked = isLocked,
                });
            }

            // The ribbon keeps the reader's place across opens; a book grown shorter just
            // cannot keep a place it no longer has.
            int spreads = Mathf.Max(1, (_bookPages.Count + 1) / 2);
            _bookSpread = Mathf.Clamp(_bookSpread, 0, spreads - 1);
            ShowRestingSpread();
        }

        /// <summary>The resting book: the open spread in the two full windows, the leaf
        /// down, the turn windows shut, and the arrow keys saying which ways remain.</summary>
        private void ShowRestingSpread()
        {
            FillBookPage(_bookPageRestL, _bookSpread * 2);
            FillBookPage(_bookPageRestR, _bookSpread * 2 + 1);
            FillBookPage(_bookPageInL, -1);
            FillBookPage(_bookPageInR, -1);
            SetBookWindow(_bookWinRestL, _bookPageRestL, -BkReach, -BkReach + 167f, -BkPageDX);
            SetBookWindow(_bookWinRestR, _bookPageRestR, BkReach - 167f, BkReach, BkPageDX);
            SetBookWindow(_bookWinInL, _bookPageInL, 0f, 0f, -BkPageDX);
            SetBookWindow(_bookWinInR, _bookPageInR, 0f, 0f, BkPageDX);
            if (_bookLeaf != null) _bookLeaf.enabled = false;
            int spreads = Mathf.Max(1, (_bookPages.Count + 1) / 2);
            if (_bookPrevKey != null) _bookPrevKey.gameObject.SetActive(_bookSpread > 0);
            if (_bookNextKey != null) _bookNextKey.gameObject.SetActive(_bookSpread + 1 < spreads);
            if (_bookHomeKey != null) _bookHomeKey.gameObject.SetActive(_bookSpread > 0);
        }

        /// <summary>Opens the book straight at a page — the contents' quick jump. A jump
        /// is a LOOKUP, not a gesture: it swaps spreads at once instead of riffling the
        /// whole distance frame by frame.</summary>
        private void JumpToPage(int pageIdx)
        {
            if (_bookTurning) return;
            int spread = Mathf.Clamp(pageIdx / 2, 0, Mathf.Max(0, (_bookPages.Count - 1) / 2));
            if (spread == _bookSpread) return;
            Sfx.Play("page_turn", 0.4f);
            _bookSpread = spread;
            ShowRestingSpread();
        }

        /// <summary>Prints one page into its container: the gold furniture (which must
        /// travel with the type — the author caught it staying behind once), then the
        /// page's own matter by its kind. Past the last page prints NOTHING: a blank
        /// leaf is bare paper, not an empty frame.</summary>
        private void FillBookPage(RectTransform print, int pageIdx)
        {
            for (int i = print.childCount - 1; i >= 0; i--)
                Destroy(print.GetChild(i).gameObject);
            if (pageIdx < 0 || pageIdx >= _bookPages.Count) return;
            var run = Run;
            if (run == null) return;
            var page = _bookPages[pageIdx];

            var frameArt = ItemArt.Load("menu_page_frame");
            if (frameArt != null)
            {
                var fr = NewRect("Frame", print);
                Place(fr, new Vector2(0.5f, 0.5f), new Vector2(BkPageW, BkPageH), Vector2.zero);
                var fi = fr.gameObject.AddComponent<Image>();
                fi.sprite = frameArt;
                fi.raycastTarget = false;
            }

            switch (page.Kind)
            {
                case BookPageKind.Title: FillTitlePage(print); break;
                case BookPageKind.Contents: FillContentsPage(print); break;
                default: FillRecipePage(print, page, run); break;
            }

            // The folio — every page but the title plate carries its number, so the
            // contents' numbers land on something.
            if (page.Kind != BookPageKind.Title)
            {
                var foot = NewText("Foot", print, _body, 16, TextAnchor.MiddleCenter,
                    new Color(0.52f, 0.44f, 0.36f));
                foot.rectTransform.anchorMin = foot.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                foot.rectTransform.pivot = new Vector2(0.5f, 0f);
                foot.rectTransform.sizeDelta = new Vector2(BkColW, 24f);
                foot.rectTransform.anchoredPosition = new Vector2(0, 16f);
                foot.text = "· " + (pageIdx + 1) + " ·";
            }
        }

        /// <summary>Turns the page: +1 forward, -1 back. The drawn peel plays between the
        /// spread below (lo) and the spread above it — one set of frames serves both
        /// directions, because the fold is the same fold. Reduced motion turns at once.</summary>
        private void TurnPage(int dir)
        {
            if (!_bookOpen || _bookTurning || _bookPanel == null) return;
            int spreads = Mathf.Max(1, (_bookPages.Count + 1) / 2);
            if (dir > 0 && _bookSpread + 1 >= spreads) return;
            if (dir < 0 && _bookSpread == 0) return;
            int lo = dir > 0 ? _bookSpread : _bookSpread - 1;
            if (Motion.Reduced || _bookLeafFrames == null || _bookLeafFrames[0] == null)
            {
                _bookSpread = lo + (dir > 0 ? 1 : 0);
                ShowRestingSpread();
                return;
            }
            Sfx.Play("page_turn", 0.55f);
            if (_bookPrevKey != null) _bookPrevKey.gameObject.SetActive(false);
            if (_bookNextKey != null) _bookNextKey.gameObject.SetActive(false);
            if (_bookHomeKey != null) _bookHomeKey.gameObject.SetActive(false);
            FillBookPage(_bookPageRestL, lo * 2);
            FillBookPage(_bookPageRestR, lo * 2 + 1);
            FillBookPage(_bookPageInL, lo * 2 + 2);
            FillBookPage(_bookPageInR, lo * 2 + 3);
            _bookTurnAnim = StartCoroutine(BookTurn(dir, lo));
        }

        private System.Collections.IEnumerator BookTurn(int dir, int lo)
        {
            _bookTurning = true;
            _bookLeaf.enabled = true;
            for (int s = 0; s < BkTurnFrames; s++)
            {
                ApplyTurnFrame(dir > 0 ? s : BkTurnFrames - 1 - s);
                for (float w = 0; w < BkFrameSec; w += Time.unscaledDeltaTime)
                    yield return null;
            }
            _bookSpread = lo + (dir > 0 ? 1 : 0);
            ShowRestingSpread();
            _bookTurning = false;
            _bookTurnAnim = null;
        }

        /// <summary>One frame of the peel, by the generator's own numbers (menu_booklet.py
        /// fold_params at t=(k+1)/16): the leaf sprite carries the paper; the three print
        /// windows are aimed at the mid-row fold. The fold is ANGLED — the bottom corner
        /// leads by up to 22 art px — so each window is clamped to the span true on EVERY
        /// row: a few pixels of bare cream at the fold for one 40 ms frame, never ink
        /// where the sheet is not.</summary>
        private void ApplyTurnFrame(int k)
        {
            _bookLeaf.sprite = _bookLeafFrames[k];
            float t = (k + 1) / (float)BkTurnFrames;
            float e = t * t * (3f - 2f * t);
            float a = BkReach * (1f - e);
            float lead = 22f * Mathf.Sin(Mathf.PI * t);
            float r = 1f + 7f * Mathf.Sin(Mathf.PI * t);
            float arc = Mathf.PI * r;
            bool flipped = BkReach - a >= arc;

            // The front print: flat from the spine to the fold, consumed column by column
            // — and once the back face has landed, cut at ITS creeping edge instead.
            float frontHi = flipped ? 2f * a + arc - BkReach - lead : a - lead * 0.5f;
            SetBookWindow(_bookWinRestR, _bookPageRestR,
                8f, Mathf.Clamp(frontHi, 8f, BkReach), BkPageDX);

            // The revealed page, right of the roll's silhouette. The leaf lies OVER this
            // window, so its angled edge and its cast shadow do the fine trimming.
            float roll = flipped ? Mathf.Round(r)
                : Mathf.Round(r * Mathf.Sin(Mathf.Min(Mathf.PI * 0.5f,
                    (BkReach - a) / Mathf.Max(0.001f, r))));
            SetBookWindow(_bookWinInR, _bookPageInR,
                Mathf.Min(a + roll, BkReach), BkReach, BkPageDX);

            // The back face riding the flipped half: the NEXT left page's print, shifted
            // right by an INTEGER of art pixels (a shifted column is crisp, a scaled one
            // is not) and creeping home to zero as the fold reaches the spine.
            if (flipped)
            {
                float shift = Mathf.Round(2f * a + arc);
                SetBookWindow(_bookWinInL, _bookPageInL,
                    shift - BkReach + lead, a - lead * 0.5f, -BkPageDX, shift);
            }
            else SetBookWindow(_bookWinInL, _bookPageInL, 0f, 0f, -BkPageDX);
        }

        /// <summary>The arrows leaf through the open book — the same page turn the
        /// corners give the mouse.</summary>
        private void UpdateBookKeys()
        {
            if (!_bookOpen || _bookTurning) return;
            // A hand on the search line owns the keyboard: turning pages under
            // somebody typing "margarita" would be the book fighting its own index.
            var sel = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && sel.GetComponent<InputField>() != null) return;
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null) return;
            if (keys.rightArrowKey.wasPressedThisFrame) TurnPage(+1);
            else if (keys.leftArrowKey.wasPressedThisFrame) TurnPage(-1);
        }

        /// <summary>A page-sized clipping window over the spread, and the print container
        /// inside it. A turn moves and resizes the WINDOW to the fold's numbers and
        /// counter-moves the print, so the page face never travels with its own mask.</summary>
        private RectTransform BookWindow(RectTransform sheet, string name, out RectTransform print)
        {
            var win = NewRect(name, sheet);
            Place(win, new Vector2(0.5f, 0.5f), new Vector2(0, BkPageH), new Vector2(0, BkLiftY));
            win.gameObject.AddComponent<RectMask2D>();
            print = NewRect("Print", win);
            Place(print, new Vector2(0.5f, 0.5f), new Vector2(BkPageW, BkPageH), Vector2.zero);
            return win;
        }

        /// <summary>Aims a window at the spine span [lo..hi] (art px, 0 at the stitched
        /// spine) and parks its print so the page face stays put at pageDX — plus the
        /// integer art-px shift for the back face riding the flipped half.</summary>
        private static void SetBookWindow(RectTransform win, RectTransform print,
            float lo, float hi, float pageDX, float shift = 0f)
        {
            float w = Mathf.Max(0f, hi - lo) * 2f;
            float cx = lo + hi;                     // (lo+hi)/2 art px × 2 HUD units
            win.sizeDelta = new Vector2(w, BkPageH);
            win.anchoredPosition = new Vector2(cx, BkLiftY);
            print.anchoredPosition = new Vector2(pageDX + shift * 2f - cx, 0f);
        }

        /// <summary>An invisible plate on a page's bottom outer corner — the corner the
        /// drawn peel lifts from. Right turns forward, left turns back; past the covers
        /// the press is simply not a page, and nothing happens.</summary>
        private void BookCorner(RectTransform sheet, string name, int dir)
        {
            var rt = NewRect(name, sheet);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(104, 104),
                new Vector2(dir * (BkPageDX + BkPageW * 0.5f - 52f),
                            BkLiftY - BkPageH * 0.5f + 52f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.001f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => TurnPage(dir));
        }

        /// <summary>The house's title plate — the page the book opens on. The menu
        /// itself starts when this page is turned.</summary>
        private void FillTitlePage(RectTransform print)
        {
            Color inkHead = new Color(0.30f, 0.16f, 0.05f);
            Color quiet = new Color(0.52f, 0.44f, 0.36f);

            var name = NewText("House", print, _display, 24, TextAnchor.MiddleCenter, inkHead);
            Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(BkColW, 60f),
                new Vector2(0, 168f));
            name.text = "LAST CALL";

            var rule = NewRect("Rule", print);
            Place(rule, new Vector2(0.5f, 0.5f), new Vector2(BkColW - 96f, 2f), new Vector2(0, 132f));
            var ri = rule.gameObject.AddComponent<Image>();
            ri.color = new Color(0.79f, 0.51f, 0.17f);
            ri.raycastTarget = false;

            var sub2 = NewText("Sub", print, _body, 16, TextAnchor.MiddleCenter,
                new Color(0.11f, 0.37f, 0.40f));
            Place(sub2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(BkColW, 24f),
                new Vector2(0, 108f));
            sub2.text = "HOUSE MENU";

            // The house's own drink, drawn for this page (2026-08-25, the author:
            // "kendin güzel bir kokteyl görseli oluştur") — generated at 64 art px by
            // Tools/menu_cover_drink_gen.py and quantized onto the 40-colour palette,
            // shown at exactly 2x. The serving-glass sprite stays as the fallback.
            var mark = NewRect("Mark", print);
            Place(mark, new Vector2(0.5f, 0.5f), new Vector2(128f, 128f), new Vector2(0, 8f));
            var mi = mark.gameObject.AddComponent<Image>();
            mi.sprite = ItemArt.Load("menu_cover_drink") ?? ItemArt.Load("glass3d_martini");
            mi.preserveAspect = true;
            mi.raycastTarget = false;
            mi.enabled = mi.sprite != null;
            mi.color = new Color(1f, 1f, 1f, 0.9f);

            var word = NewText("Word", print, _body, 16, TextAnchor.MiddleCenter, quiet);
            Place(word.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(BkColW, 24f),
                new Vector2(0, -64f));
            word.text = "POURS · PAGES · PROVENANCE";

            var hint = NewText("Hint", print, _body, 16, TextAnchor.MiddleCenter, quiet);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(BkColW, 24f),
                new Vector2(0, -262f));
            hint.text = "THE CONTENTS FACE THIS PAGE";

            // THE NEWS, ON THE PAGE THE BOOK OPENS ON. Each line is the way to the page
            // it is about, and opening it is what marks it read.
            if (_perfectNews.Count == 0) return;
            var newsHead = NewText("NewsHead", print, _body, 8, TextAnchor.MiddleCenter,
                new Color(0.42f, 0.46f, 0.55f));
            Place(newsHead.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(BkColW, 12f),
                new Vector2(0, -104f));
            newsHead.text = _perfectNews.Count == 1
                ? "NEW · A PERFECT RECIPE" : "NEW · " + _perfectNews.Count + " PERFECT RECIPES";

            float ny = 0f;
            for (int i = 0; i < _perfectNews.Count && i < 3; i++)
            {
                string id = _perfectNews[i];
                int page = -1;
                for (int p = 0; p < _bookPages.Count; p++)
                    if (_bookPages[p].Kind == BookPageKind.Recipe && _bookPages[p].Recipe.Id == id)
                    { page = p; break; }
                if (page < 0) continue;
                var pg = _bookPages[page];
                int target = page;
                var row = NewRect("News", print);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(BkColW, 26f);
                row.anchoredPosition = new Vector2(0, -118f - ny);
                var slab = row.gameObject.AddComponent<Image>();
                slab.color = BkPlatinum;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = slab;
                btn.transition = Selectable.Transition.ColorTint;
                var cb = btn.colors;
                cb.normalColor = new Color(1f, 1f, 1f, 0.55f);
                cb.highlightedColor = Color.white;
                cb.pressedColor = new Color(1f, 0.98f, 0.90f, 1f);
                cb.selectedColor = new Color(1f, 1f, 1f, 0.55f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
                btn.onClick.AddListener(() =>
                {
                    Sfx.Play("click", 0.4f);
                    _perfectNews.Remove(id);
                    RefreshBookBadge();
                    JumpToPage(target);
                });
                var nm = NewText("N", row, _body, 16, TextAnchor.MiddleLeft,
                    new Color(0.16f, 0.18f, 0.24f));
                Place(nm.rectTransform, new Vector2(0, 0.5f), new Vector2(BkColW - 60f, 22f),
                    Vector2.zero);
                nm.rectTransform.pivot = new Vector2(0, 0.5f);
                nm.rectTransform.anchoredPosition = new Vector2(8f, 0);
                nm.text = pg.Recipe.Name.ToUpperInvariant().Replace(" & ", " AND ");
                var fo = NewText("P", row, _body, 16, TextAnchor.MiddleRight,
                    new Color(0.16f, 0.18f, 0.24f));
                Place(fo.rectTransform, new Vector2(1, 0.5f), new Vector2(44f, 22f),
                    new Vector2(-8f, 0));
                fo.text = (page + 1).ToString();
                ny += 30f;
            }
        }

        /// <summary>The contents, grown into a browser (2026-08-25): a search line on
        /// top, and chapter rows that OPEN here — the chapter's every recipe listed with
        /// its folio, each line a shortcut to its page (the author: "starter'a
        /// tıklandığında o sayfa içerisinde içindekiler detaylanacak"). The search and
        /// the open chapter persist like the ribbon; the search line, while it holds
        /// letters, owns the list outright.</summary>
        private void FillContentsPage(RectTransform print)
        {
            var head = NewText("Head", print, _display, 16, TextAnchor.MiddleCenter,
                new Color(0.30f, 0.16f, 0.05f));
            head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            head.rectTransform.pivot = new Vector2(0.5f, 1f);
            head.rectTransform.sizeDelta = new Vector2(BkColW, 54f);
            head.rectTransform.anchoredPosition = new Vector2(0, -12f);
            head.text = "CONTENTS";

            // The search line (the author: "Contents'in üstünde arama kutusu olacak").
            var box = NewRect("Search", print);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(BkColW, 28f);
            box.anchoredPosition = new Vector2(0, -BkContentTop);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(0.94f, 0.90f, 0.80f);
            var boxEdge = new Color(0.36f, 0.22f, 0.08f, 0.55f);
            Hairline(box, new Vector2(0, 0), new Vector2(1, 0), boxEdge);
            Hairline(box, new Vector2(0, 1), new Vector2(1, 1), boxEdge);
            HairlineV(box, 0f, boxEdge);
            HairlineV(box, 1f, boxEdge);
            var st = NewText("T", box, _body, 16, TextAnchor.MiddleLeft, new Color(0.16f, 0.10f, 0.06f));
            Stretch(st.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            st.supportRichText = false;
            var ph = NewText("P", box, _body, 16, TextAnchor.MiddleLeft, new Color(0.5f, 0.42f, 0.32f));
            Stretch(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            ph.text = "SEARCH THE BOOK…";
            var input = box.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = st;
            input.placeholder = ph;
            input.text = _bookTocQuery;

            // The list below rebuilds alone, so typing never tears down its own box.
            var body = NewRect("Body", print);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.sizeDelta = new Vector2(BkColW, BkPageH - BkContentTop - 34f - 60f);
            body.anchoredPosition = new Vector2(0, -(BkContentTop + 34f));
            input.onValueChanged.AddListener(q => { _bookTocQuery = q; BuildTocBody(body); });
            BuildTocBody(body);
        }

        /// <summary>One clickable line of the contents, with the hover glow the author
        /// asked for ("hangi seçeneğin üstüne geliniyorsa parlamalı"): an amber wash
        /// that sleeps at a whisper and wakes under the pointer.</summary>
        private RectTransform TocRow(RectTransform body, float y, float h, Action onClick)
        {
            var row = NewRect("Row", body);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(BkColW, h);
            row.anchoredPosition = new Vector2(0, -y);
            var slab = row.gameObject.AddComponent<Image>();
            slab.color = new Color(0.79f, 0.51f, 0.17f, 0.30f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = slab;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0.22f);
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(1f, 0.92f, 0.75f, 1f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0.22f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(() => { Sfx.Play("click", 0.4f); onClick(); });
            return row;
        }

        /// <summary>The contents' list: search hits while the line holds letters, an
        /// opened chapter's own pages, or the chapter shelf.</summary>
        private void BuildTocBody(RectTransform body)
        {
            if (body == null) return;
            for (int i = body.childCount - 1; i >= 0; i--)
                Destroy(body.GetChild(i).gameObject);
            Color ink = new Color(0.20f, 0.13f, 0.07f);
            Color dim = new Color(0.45f, 0.36f, 0.28f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            string q = (_bookTocQuery ?? "").Trim();
            float y = 0f;

            void RecipeLine(BookPage pg, int pageIdx)
            {
                var row = TocRow(body, y, 24f, () => JumpToPage(pageIdx));
                var nm = NewText("N", row, _body, 16, TextAnchor.MiddleLeft, pg.Locked ? dim : ink);
                Place(nm.rectTransform, new Vector2(0, 0.5f), new Vector2(BkColW - 100f, 22f), Vector2.zero);
                nm.rectTransform.pivot = new Vector2(0, 0.5f);
                nm.rectTransform.anchoredPosition = new Vector2(8f, 0);
                // AND, NOT "&" (2026-08-25, seen in play). The body face's ampersand at
                // 16 is a vertical bar with two nubs — it reads as "GIN $ TONIC". The
                // recipe's own page prints the true name in the display face, which
                // draws the glyph properly; this line is the index, and an index that
                // cannot be read is not one.
                nm.text = pg.Recipe.Name.ToUpperInvariant().Replace(" & ", " AND ");
                if (pg.Locked)
                {
                    // THE INDEX SAYS HOW FAR, NOT JUST THAT IT IS SHUT (2026-08-25). A
                    // star gate draws its own row here at 8px; a lock that is not about
                    // stars keeps the word, because five sockets would promise a rung
                    // that no star ever opens.
                    var run2 = Run;
                    var lk2 = run2 != null ? run2.RecipeUnlock(pg.Recipe) : null;
                    double wants2 = lk2 != null ? lk2.StarsWanted : double.NaN;
                    if (!double.IsNaN(wants2))
                    {
                        StarRow(row, new Vector2(1, 0.5f), new Vector2(-46f, 0), 8f, wants2,
                            new Color(0.66f, 0.12f, 0.16f), new Color(0.36f, 0.22f, 0.08f, 0.20f));
                    }
                    else
                    {
                        var lk = NewText("L", row, _body, 8, TextAnchor.MiddleRight,
                            new Color(0.66f, 0.12f, 0.16f));
                        Place(lk.rectTransform, new Vector2(1, 0.5f), new Vector2(60f, 22f),
                            new Vector2(-46f, 0));
                        lk.text = "LOCKED";
                    }
                }
                var fo = NewText("P", row, _body, 16, TextAnchor.MiddleRight, figure);
                Place(fo.rectTransform, new Vector2(1, 0.5f), new Vector2(40f, 22f), new Vector2(-8f, 0));
                fo.text = (pageIdx + 1).ToString();
                y += 26f;
            }

            if (q.Length > 0)
            {
                int hits = 0;
                for (int p = 0; p < _bookPages.Count; p++)
                {
                    var pg = _bookPages[p];
                    if (pg.Kind != BookPageKind.Recipe) continue;
                    if (pg.Recipe.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (hits >= 15) break;
                    RecipeLine(pg, p);
                    hits++;
                }
                if (hits == 0)
                {
                    var none = NewText("None", body, _body, 16, TextAnchor.MiddleCenter, quiet);
                    Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(BkColW, 24f),
                        new Vector2(0, -8f));
                    none.rectTransform.pivot = new Vector2(0.5f, 1f);
                    none.text = "NOTHING BY THAT NAME";
                }
                return;
            }

            if (_bookTocChapter != null)
            {
                var back = TocRow(body, y, 24f, () => { _bookTocChapter = null; BuildTocBody(body); });
                var bt = NewText("T", back, _body, 16, TextAnchor.MiddleLeft, quiet);
                Stretch(bt.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-4, 0));
                bt.text = "< ALL CHAPTERS";
                y += 30f;
                for (int p = 0; p < _bookPages.Count; p++)
                {
                    var pg = _bookPages[p];
                    if (pg.Kind != BookPageKind.Recipe || pg.Chapter != _bookTocChapter) continue;
                    RecipeLine(pg, p);
                }
                return;
            }

            foreach (var ch in _bookChapters)
            {
                var chTitle = ch.Title;
                var row = TocRow(body, y, 50f, () => { _bookTocChapter = chTitle; BuildTocBody(body); });
                var nm = NewText("N", row, _display, 16, TextAnchor.UpperLeft, ink);
                Place(nm.rectTransform, new Vector2(0, 1), new Vector2(BkColW - 70f, 24f), Vector2.zero);
                nm.rectTransform.pivot = new Vector2(0, 1);
                nm.rectTransform.anchoredPosition = new Vector2(8f, -5f);
                nm.text = ch.Title;
                var fo = NewText("P", row, _display, 16, TextAnchor.MiddleRight, figure);
                Place(fo.rectTransform, new Vector2(1, 0.5f), new Vector2(56f, 24f), new Vector2(-8f, 0));
                fo.text = (ch.FirstPage + 1).ToString();
                var meta = NewText("M", row, _body, 16, TextAnchor.LowerLeft, quiet);
                Place(meta.rectTransform, new Vector2(0, 0), new Vector2(BkColW - 70f, 18f), Vector2.zero);
                meta.rectTransform.pivot = new Vector2(0, 0);
                meta.rectTransform.anchoredPosition = new Vector2(8f, 3f);
                meta.text = ch.Count + " POURS"
                    + (ch.LockedCount > 0 ? " · " + ch.LockedCount + " LOCKED" : "");
                y += 56f;
            }

            var note = NewText("Note", body, _body, 16, TextAnchor.MiddleCenter, quiet);
            Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(BkColW, 24f),
                new Vector2(0, -(y + 12f)));
            note.rectTransform.pivot = new Vector2(0.5f, 1f);
            note.text = "A CHAPTER OPENS ITS OWN LIST HERE";
        }

        /// <summary>One recipe, one page — the cookbook layout (2026-08-24). Top to
        /// bottom: tier and name in the heading zone, how it is worked and in what
        /// glass, the drink itself, the gauge's LEGEND (what the bar measures and which
        /// colour owns which fifth), the pours at full width, and the drink's own story
        /// pinned at the foot. A perfected page prints the exact share where its gauge
        /// used to stand, and wears platinum; a locked page keeps its gauges empty and
        /// says what it waits behind; a bottle the bar cannot pour SAYS SO under its
        /// name instead of colliding with the gauge (the overlap this layout retires).</summary>
        private void FillRecipePage(RectTransform print, BookPage page, TycoonRun run)
        {
            var r = page.Recipe;
            bool perfected = !page.Locked && r.HasAuthoredRatios && run.IsPerfected(r.Id);

            Color ink = new Color(0.20f, 0.13f, 0.07f);
            Color quiet = new Color(0.52f, 0.44f, 0.36f);
            Color figure = new Color(0.10f, 0.06f, 0.02f);
            Color prepInk = new Color(0.11f, 0.37f, 0.40f);
            Color goneInk = new Color(0.66f, 0.12f, 0.16f);
            Color miss = new Color(0.52f, 0.44f, 0.36f, 0.6f);
            Color have = new Color(0.36f, 0.22f, 0.08f, 0.09f);
            Color gone = new Color(0.74f, 0.16f, 0.20f, 0.13f);

            // ── the heading zone: the chapter above, the name on the rule ────────
            var eyebrow = NewText("Tier", print, _body, 16, TextAnchor.MiddleCenter, quiet);
            eyebrow.rectTransform.anchorMin = eyebrow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            eyebrow.rectTransform.pivot = new Vector2(0.5f, 1f);
            eyebrow.rectTransform.sizeDelta = new Vector2(BkColW, 20f);
            eyebrow.rectTransform.anchoredPosition = new Vector2(0, -8f);
            eyebrow.text = page.Chapter + (page.Locked ? " · LOCKED" : "");

            var head = NewText("Head", print, _display, 16, TextAnchor.MiddleCenter,
                page.Locked ? new Color(0.45f, 0.36f, 0.28f) : new Color(0.30f, 0.16f, 0.05f));
            head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            head.rectTransform.pivot = new Vector2(0.5f, 1f);
            head.rectTransform.sizeDelta = new Vector2(BkColW + 20f, 30f);
            head.rectTransform.anchoredPosition = new Vector2(0, -30f);
            head.text = r.Name.ToUpperInvariant();

            float y = BkContentTop;

            // ── how it is worked, and in what ────────────────────────────────────
            var way = NewText("Way", print, _body, 16, TextAnchor.MiddleCenter, prepInk);
            way.rectTransform.anchorMin = way.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            way.rectTransform.pivot = new Vector2(0.5f, 1f);
            way.rectTransform.sizeDelta = new Vector2(BkColW, 22f);
            way.rectTransform.anchoredPosition = new Vector2(0, -y);
            string glassWord = string.IsNullOrEmpty(r.GlassId)
                ? "HIGHBALL" : r.GlassId.Replace('_', ' ').ToUpperInvariant();
            way.text = PrepWord(r) + " · " + glassWord + " GLASS";
            y += 24f;

            var icon = NewRect("I", print);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 1f);
            icon.sizeDelta = new Vector2(48f, 48f);
            icon.anchoredPosition = new Vector2(0, -y);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = DrinkIcon.For(r, _bootstrap.Glassware);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = img.sprite != null;
            if (page.Locked) img.color = new Color(1, 1, 1, 0.4f);
            y += 56f;

            // ── the gauge's own legend (the author: the bar must SAY what it means
            // and which %-band each colour owns) ─────────────────────────────────
            var cap = NewText("Cap", print, _body, 8, TextAnchor.MiddleCenter, quiet);
            cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.rectTransform.sizeDelta = new Vector2(BkColW, 12f);
            cap.rectTransform.anchoredPosition = new Vector2(0, -y);
            cap.text = "THE POUR · EACH BOTTLE'S SHARE OF THE GLASS";
            y += 14f;
            float chipW = (BkColW - (RatioBox.Count - 1) * 4f) / RatioBox.Count;
            for (int i = 0; i < RatioBox.Count; i++)
            {
                float x = -BkColW * 0.5f + i * (chipW + 4f);
                var chip = NewRect("Lg" + i, print);
                chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
                chip.pivot = new Vector2(0, 1);
                chip.sizeDelta = new Vector2(chipW, 10f);
                chip.anchoredPosition = new Vector2(x, -y);
                var ci = chip.gameObject.AddComponent<Image>();
                ci.color = BandBoxColors[i];
                ci.raycastTarget = false;
                var lb = NewText("T", print, _body, 8, TextAnchor.UpperCenter, quiet);
                lb.rectTransform.anchorMin = lb.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                lb.rectTransform.pivot = new Vector2(0, 1);
                lb.rectTransform.sizeDelta = new Vector2(chipW, 12f);
                lb.rectTransform.anchoredPosition = new Vector2(x, -(y + 12f));
                lb.text = (int)(RatioBox.Lower(i) * 100) + "-" + (int)(RatioBox.Upper(i) * 100);
            }
            y += 30f;

            // ── the pours, one full-width row each ───────────────────────────────
            var specRows = RecipeSpecRows(r, poursOnly: true, locked: page.Locked);
            for (int i = 0; i < specRows.Count; i++)
            {
                var spec = specRows[i];
                // The prep word already stands over the icon; its row would say it twice.
                if (i == 0 && r.Id != "draught") continue;
                bool ingredient = spec.Style != null;
                bool stocked = !ingredient || InStock(spec.Style, spec.MinTier);

                if (spec.Hint)
                {
                    var hintT = NewText("H" + i, print, _body, 8, TextAnchor.MiddleLeft, quiet);
                    hintT.rectTransform.anchorMin = hintT.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    hintT.rectTransform.pivot = new Vector2(0.5f, 1f);
                    hintT.rectTransform.sizeDelta = new Vector2(BkColW - 8f, 14f);
                    hintT.rectTransform.anchoredPosition = new Vector2(0, -y);
                    hintT.text = spec.Label;
                    y += 16f;
                    continue;
                }

                var line = NewRect("S" + i, print);
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 1f);
                line.pivot = new Vector2(0.5f, 1f);
                line.sizeDelta = new Vector2(BkColW, 34f);
                line.anchoredPosition = new Vector2(0, -y);
                y += 36f;

                if (ingredient)
                {
                    var slab = line.gameObject.AddComponent<Image>();
                    slab.color = stocked ? have : gone;
                    slab.raycastTarget = false;
                }

                float textX = 4f;
                if (ingredient)
                {
                    var pour = new List<Sprite>();
                    foreach (var b in run.Shelf.Bottles)
                    {
                        var info = b.Ingredient?.Info;
                        if (info == null || info.Style != spec.Style) continue;
                        if (info.Tier < spec.MinTier) continue;
                        var a = ItemArt.Bottle(b.Ingredient);
                        if (a != null) pour.Add(a);
                    }
                    if (pour.Count == 0)
                    {
                        var fallback = ItemArt.Bottle(spec.Style);
                        if (fallback != null) pour.Add(fallback);
                    }
                    const float box = 28f;
                    float step = pour.Count > 1 ? Mathf.Min(box, 44f / pour.Count) : box;
                    for (int b = 0; b < pour.Count; b++)
                    {
                        var bi = NewRect("B" + b, line);
                        Place(bi, new Vector2(0, 0.5f), new Vector2(box, box),
                            new Vector2(3f + b * step, 0));
                        var bimg = bi.gameObject.AddComponent<Image>();
                        bimg.sprite = pour[b];
                        bimg.preserveAspect = true;
                        bimg.raycastTarget = false;
                        bimg.color = stocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    }
                    textX = box + 6f + Mathf.Max(0, pour.Count - 1) * step;
                }

                var label = NewText("L", line, _body, 16, TextAnchor.UpperLeft,
                    ingredient ? (stocked ? ink : miss) : prepInk);
                Place(label.rectTransform, new Vector2(0, 1),
                    new Vector2(BkColW - textX - BkGaugeW - 14f, 20f), Vector2.zero);
                label.rectTransform.pivot = new Vector2(0, 1);
                label.rectTransform.anchoredPosition = new Vector2(textX, -1f);
                label.raycastTarget = false;
                label.text = spec.Label + (spec.MinTier > 1 ? $"  T{spec.MinTier}+" : "");

                // A BOTTLE THE BAR CANNOT POUR SAYS SO (the author: "açık olmayan
                // alkoller kilitli gözükür") — under its own name, on its own line,
                // where it can never collide with the gauge.
                if (ingredient && !stocked)
                {
                    var lockT = NewText("X", line, _body, 8, TextAnchor.UpperLeft, goneInk);
                    Place(lockT.rectTransform, new Vector2(0, 1), new Vector2(200f, 12f), Vector2.zero);
                    lockT.rectTransform.pivot = new Vector2(0, 1);
                    lockT.rectTransform.anchoredPosition = new Vector2(textX, -20f);
                    lockT.raycastTarget = false;
                    lockT.text = "LOCKED · NOT IN THE WELL";
                }

                if (spec.Amount.Length > 0)
                {
                    // THE PERFECT SHARE PRINTS AS ITS NUMBER (the author: "perfect oran
                    // bulunduğunda o barın yerini perfect oranın sayısı alır") — the
                    // gauge stands down, the figure stands up, and the word under it
                    // says why the page may print it at all.
                    var amount = NewText("A", line, _display, 16, TextAnchor.UpperRight, figure);
                    Place(amount.rectTransform, new Vector2(1, 1), new Vector2(BkGaugeW, 20f),
                        new Vector2(-4f, -1f));
                    amount.rectTransform.pivot = new Vector2(1, 1);
                    amount.raycastTarget = false;
                    amount.text = spec.Amount;
                    var tag = NewText("PT", line, _body, 8, TextAnchor.UpperRight,
                        new Color(0.42f, 0.46f, 0.55f));
                    Place(tag.rectTransform, new Vector2(1, 1), new Vector2(BkGaugeW, 12f),
                        new Vector2(-4f, -21f));
                    tag.rectTransform.pivot = new Vector2(1, 1);
                    tag.raycastTarget = false;
                    tag.text = "PERFECT";
                }
                else if (spec.Box >= 0)
                {
                    var gauge = NewRect("Gauge", line);
                    Place(gauge, new Vector2(1, 0.5f), new Vector2(BkGaugeW, BkGaugeH),
                        new Vector2(-4f - BkGaugeW, 0));
                    gauge.pivot = new Vector2(0, 0.5f);

                    var tube = gauge.gameObject.AddComponent<Image>();
                    tube.sprite = ChromeArt.GaugeTube((int)BkGaugeW, (int)BkGaugeH);
                    tube.raycastTarget = false;
                    tube.color = new Color(0.80f, 0.74f, 0.62f, stocked ? 1f : 0.6f);

                    if (!page.Locked)
                    {
                        var fill = NewRect("Level", gauge);
                        Place(fill, new Vector2(0, 0.5f), new Vector2(BkGaugeW - 2f, BkGaugeH - 3f),
                            new Vector2(1f, -0.5f));
                        var lvl = fill.gameObject.AddComponent<Image>();
                        lvl.sprite = ChromeArt.GaugeLadder(BandBoxColors);
                        lvl.type = Image.Type.Filled;
                        lvl.fillMethod = Image.FillMethod.Horizontal;
                        lvl.fillOrigin = (int)Image.OriginHorizontal.Left;
                        lvl.fillAmount = (float)RatioBox.Upper(spec.Box);
                        lvl.raycastTarget = false;
                        lvl.color = stocked || !ingredient ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                    }

                    var glass = NewRect("Glass", gauge);
                    Stretch(glass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    var gimg = glass.gameObject.AddComponent<Image>();
                    gimg.sprite = ChromeArt.GaugeGlass((int)BkGaugeW, (int)BkGaugeH, RatioBox.Count);
                    gimg.raycastTarget = false;

                    if (spec.Best >= 0 && !page.Locked)
                    {
                        var mark = NewRect("Best", gauge);
                        Place(mark, new Vector2(0, 0.5f), new Vector2(1f, BkGaugeH + 5f),
                            new Vector2(1f + Mathf.Clamp01((float)spec.Best) * (BkGaugeW - 3f), 0));
                        var mimg = mark.gameObject.AddComponent<Image>();
                        mimg.raycastTarget = false;
                        mimg.color = new Color(0.20f, 0.13f, 0.07f, 0.85f);
                    }
                }
            }

            if (r.MinFill > 0)
            {
                var fillLine = NewText("Fill", print, _body, 16, TextAnchor.MiddleCenter, figure);
                fillLine.rectTransform.anchorMin = fillLine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                fillLine.rectTransform.pivot = new Vector2(0.5f, 1f);
                fillLine.rectTransform.sizeDelta = new Vector2(BkColW, 20f);
                fillLine.rectTransform.anchoredPosition = new Vector2(0, -y);
                fillLine.text = $"FILL {r.MinFill * 100:0}%+ OF THE GLASS";
                y += 22f;
            }

            var bestMake = page.Locked || !r.HasAuthoredRatios ? null : run.BestMakeFor(r.Id);
            if (!perfected && bestMake != null)
            {
                var best = NewText("YB", print, _body, 8, TextAnchor.MiddleCenter, quiet);
                best.rectTransform.anchorMin = best.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                best.rectTransform.pivot = new Vector2(0.5f, 1f);
                best.rectTransform.sizeDelta = new Vector2(BkColW, 12f);
                best.rectTransform.anchoredPosition = new Vector2(0, -y);
                best.text = $"YOUR BEST MAKE · {bestMake.Accuracy * 100:0}%";
                y += 16f;
            }

            if (page.Locked)
            {
                var gateLock = run.RecipeUnlock(r);
                double wants = gateLock != null ? gateLock.StarsWanted : double.NaN;
                bool starGate = !double.IsNaN(wants);
                var gate = NewRect("Gate", print);
                gate.anchorMin = gate.anchorMax = new Vector2(0.5f, 1f);
                gate.pivot = new Vector2(0.5f, 1f);
                // 46, not 68: the seven-pour page (Long Island) leaves exactly the foot's
                // width between its last row and the provenance rule, and a taller plate
                // has to eat one or the other. Two readings still fit — the rows just sit
                // closer together than they did on the roomy four-pour pages.
                float gateH = starGate ? 46f : 40f;
                gate.sizeDelta = new Vector2(BkColW, gateH);
                // THE FOOT IS SPOKEN FOR. The provenance is pinned to the bottom of the
                // page, so a plate that grows down the flow eventually prints over it —
                // a seven-pour page did exactly that the evening the gate learned to
                // draw stars. The plate stops above the foot rule instead.
                float gateTop = Mathf.Min(y + 4f, BkPageH - 158f - gateH);
                gate.anchoredPosition = new Vector2(0, -gateTop);
                var gi = gate.gameObject.AddComponent<Image>();
                gi.color = new Color(0.93f, 0.90f, 0.82f);
                gi.raycastTarget = false;
                var edge = new Color(0.66f, 0.12f, 0.16f, 0.45f);
                Hairline(gate, new Vector2(0, 0), new Vector2(1, 0), edge);
                Hairline(gate, new Vector2(0, 1), new Vector2(1, 1), edge);
                HairlineV(gate, 0f, edge);
                HairlineV(gate, 1f, edge);
                if (starGate)
                {
                    // TWO ROWS, THE SAME RULER: what the page wants, and where the bar
                    // stands tonight. Read together they are a distance — which is the
                    // thing a gate is actually telling you — and neither row needs its
                    // number to be believed.
                    void GateLine(string word, double stars, float top, Color lit, Color ink)
                    {
                        var w = NewText("W", gate, _body, 8, TextAnchor.MiddleLeft, ink);
                        Place(w.rectTransform, new Vector2(0, 1), new Vector2(72f, 14f),
                            new Vector2(10f, -top));
                        w.rectTransform.pivot = new Vector2(0, 1);
                        w.text = word;
                        StarRow(gate, new Vector2(0, 1), new Vector2(84f, -top - 6f), 11f,
                            stars, lit, new Color(0.36f, 0.22f, 0.08f, 0.18f));
                        var n = NewText("N", gate, _display, 16, TextAnchor.MiddleRight, ink);
                        // 60, and no wrapping: "4.0" is three display glyphs at 16 and a
                        // 44-unit box broke it onto three stacked lines (seen in play).
                        Place(n.rectTransform, new Vector2(1, 1), new Vector2(60f, 18f),
                            new Vector2(-8f, -top + 1f));
                        n.rectTransform.pivot = new Vector2(1, 1);
                        n.horizontalOverflow = HorizontalWrapMode.Overflow;
                        n.text = stars.ToString("0.0");
                    }
                    GateLine("OPENS AT", wants, 5f, UITheme.Amber[3], goneInk);
                    GateLine("YOU HAVE", run.Rating.Average, 25f,
                        new Color(0.60f, 0.52f, 0.40f), quiet);
                }
                else
                {
                    var gt = NewText("T", gate, _body, 16, TextAnchor.MiddleCenter, goneInk);
                    Stretch(gt.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 2), new Vector2(-6, -2));
                    gt.text = gateLock != null && !string.IsNullOrEmpty(gateLock.Sentence)
                        ? "OPENS: " + gateLock.Sentence
                        : "NOT ON THE HOUSE LIST YET";
                }
            }

            // ── the bottom matter, pinned to the foot so it can never collide with
            // the pours above: the story, then the ledger line ───────────────────
            var lore = RecipeLore.For(r.Id);
            var sep = NewRect("Sep", print);
            sep.anchorMin = sep.anchorMax = new Vector2(0.5f, 0f);
            sep.pivot = new Vector2(0.5f, 0f);
            sep.sizeDelta = new Vector2(BkColW - 40f, 1f);
            sep.anchoredPosition = new Vector2(0, 142f);
            var si = sep.gameObject.AddComponent<Image>();
            si.color = new Color(0.36f, 0.22f, 0.08f, 0.35f);
            si.raycastTarget = false;

            if (lore != null)
            {
                // Fine print, literally: at 16 the longer histories broke off mid-
                // sentence (three lines is ~84 characters); at 8 every note in the file
                // fits whole, and provenance reads as a book's bottom matter should.
                var note = NewText("Lore", print, _body, 8, TextAnchor.UpperLeft, quiet);
                note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                note.rectTransform.pivot = new Vector2(0.5f, 0f);
                note.rectTransform.sizeDelta = new Vector2(BkColW - 8f, 72f);
                note.rectTransform.anchoredPosition = new Vector2(0, 66f);
                note.horizontalOverflow = HorizontalWrapMode.Wrap;
                note.verticalOverflow = VerticalWrapMode.Truncate;
                note.text = lore.Note;

                var facts = NewText("Facts", print, _body, 8, TextAnchor.MiddleCenter, quiet);
                facts.rectTransform.anchorMin = facts.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                facts.rectTransform.pivot = new Vector2(0.5f, 0f);
                // 50, not 40: the gold foot rule crosses at 46, and a line that sits ON
                // it prints as struck-through provenance (seen in play, 2026-08-24).
                facts.rectTransform.sizeDelta = new Vector2(BkColW, 12f);
                facts.rectTransform.anchoredPosition = new Vector2(0, 50f);
                facts.text = lore.Origin + " · $" + DrinkOrder.MenuPrice(r);
            }

            if (perfected)
            {
                PlatinumFrame(print);
                // The angled ribbon over the top corner (the author: "perfect recipe
                // diye kartının üst köşesinde açılı bir şekilde belirtilir").
                var rib = NewRect("PerfectRib", print);
                rib.anchorMin = rib.anchorMax = new Vector2(1f, 1f);
                rib.pivot = new Vector2(0.5f, 0.5f);
                rib.sizeDelta = new Vector2(170f, 22f);
                rib.anchoredPosition = new Vector2(-52f, -52f);
                rib.localEulerAngles = new Vector3(0, 0, -45f);
                var rbi = rib.gameObject.AddComponent<Image>();
                rbi.color = BkPlatinum;
                rbi.raycastTarget = false;
                var rimEdge = new Color(0.42f, 0.46f, 0.55f, 0.9f);
                Hairline(rib, new Vector2(0, 0), new Vector2(1, 0), rimEdge);
                Hairline(rib, new Vector2(0, 1), new Vector2(1, 1), rimEdge);
                var rt = NewText("T", rib, _body, 8, TextAnchor.MiddleCenter,
                    new Color(0.16f, 0.18f, 0.24f));
                Stretch(rt.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                rt.text = "PERFECT RECIPE";
            }
        }

        /// <summary>The platinum binding a perfected page earns (the author: "kartların
        /// etrafı platinium rengi kaplanır"): a double platinum border laid over the
        /// gold one. Drawn as rects, not a tint — gold pixels cannot be multiplied into
        /// silver, and a frame that only pretends reads as a lighting bug.</summary>
        private void PlatinumFrame(RectTransform print)
        {
            Color pl = BkPlatinum;
            Color plDark = new Color(0.55f, 0.58f, 0.66f);
            void Bar(string bn, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos, Color c)
            {
                var b = NewRect(bn, print);
                b.anchorMin = b.anchorMax = anchor;
                b.pivot = pivot;
                b.sizeDelta = size;
                b.anchoredPosition = pos;
                var bi = b.gameObject.AddComponent<Image>();
                bi.color = c;
                bi.raycastTarget = false;
            }
            float w = BkPageW - 20f, h = BkPageH - 20f;
            Bar("PlT", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(w, 2f), new Vector2(0, -10f), pl);
            Bar("PlB", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(w, 2f), new Vector2(0, 10f), pl);
            Bar("PlL", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, h), new Vector2(10f, 0), pl);
            Bar("PlR", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(2f, h), new Vector2(-10f, 0), pl);
            float w2 = BkPageW - 32f, h2 = BkPageH - 32f;
            Bar("PlT2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(w2, 1f), new Vector2(0, -16f), plDark);
            Bar("PlB2", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(w2, 1f), new Vector2(0, 16f), plDark);
            Bar("PlL2", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1f, h2), new Vector2(16f, 0), plDark);
            Bar("PlR2", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, h2), new Vector2(-16f, 0), plDark);
        }

        /// <summary>A drawn paper key on a page's bottom outer corner — the visible half
        /// of the corner hotspot (the author: "sağ ve sol ok butonları koy").</summary>
        private RectTransform BookPaperKey(RectTransform sheet, string name, string word, int dir)
        {
            var rt = NewRect(name, sheet);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(40f, 30f),
                new Vector2(dir * (BkPageDX + BkPageW * 0.5f - 34f),
                            BkLiftY - BkPageH * 0.5f + 21f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.93f, 0.89f, 0.78f);
            var edge = new Color(0.36f, 0.22f, 0.08f, 0.55f);
            Hairline(rt, new Vector2(0, 0), new Vector2(1, 0), edge);
            Hairline(rt, new Vector2(0, 1), new Vector2(1, 1), edge);
            HairlineV(rt, 0f, edge);
            HairlineV(rt, 1f, edge);
            var t = NewText("T", rt, _display, 16, TextAnchor.MiddleCenter,
                new Color(0.30f, 0.16f, 0.05f));
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.text = word;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            // Paper, so it warms rather than presses: these are printed keys on a printed
            // page and a bevelled throw would make them the only machined things in the book.
            var keyGlow = rt.gameObject.AddComponent<HoverGlow>();
            keyGlow.Graphics = new UnityEngine.UI.Graphic[] { img };
            keyGlow.Gain = 1.10f;      // cream is already near white; 1.22 would blow it out
            // dir 0 is the home key: straight back to the title spread.
            btn.onClick.AddListener(() => { if (dir == 0) JumpToPage(0); else TurnPage(dir); });
            return rt;
        }
    }
}
