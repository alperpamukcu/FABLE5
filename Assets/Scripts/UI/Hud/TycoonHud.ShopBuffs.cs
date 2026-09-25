using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part ShopBuffs: what a thing in the MARKET does for the bar, said ON the thing (2026-09-23).
    //
    // The author: "Bufflar kısa ve net bir şekilde küçük iconuyla markette ürünlerde gözükmeli aynı şey açık
    // görünen tarifler içinde geçerli", and of the fittings: "Hangi geliştirme takılıysa o buff aktif olacak,
    // konfor gibi değil". So every listing that moves a number wears that number: an open page its character, a
    // rung of the room its kind and whether it is working, the stool and the bar top what they always give, a
    // better bottle what it adds to every drink it goes into. The market tile and the fitting card carry the LEAD
    // one as a badge on the picture; the reading card on the pointer says every one of them first, each with the
    // state it is in; the upgrade screen opens on a band of what the room gives as it stands.
    //
    // The badge, its plate, its mark, its figure and its word are the SHARED ones (TycoonHud.Buffs.cs:
    // BuffBadge / BuffIcon / Pct / BuffWord, ChromeArt.BuffPlate) - this file only decides WHAT a listing says
    // and WHERE it stands. Core decides what is live (TycoonRun.IsActive, FittingEffects, Buffs); nothing here
    // re-derives a rule it could ask for.
    //
    // A TileSpec carries its buffs in a side table rather than a field: TileSpec lives in TycoonHud.cs, which is
    // another session's file today. The table's keys are weak, so a spec dies with the tiles that captured it.
    public sealed partial class TycoonHud
    {
        /// <summary>One buff as the market says it: the shared badge's key, figure and plate, plus the two short
        /// lines the reading card sets beside it (what moves, and whether it is moving tonight).</summary>
        private sealed class ShopBuff
        {
            /// <summary>The shared icon/word key (a page's StatKey, a fitting kind's Id, "seats", "stock_premium").</summary>
            public string Key;
            /// <summary>Formatted and signed ("+12%", "+$4"); empty for a flag (FREE DRAIN), which prints its word.</summary>
            public string Figure;
            /// <summary>What the TILE prints when the reading card's figure will not fit on the picture (a bottle's
            /// "+4" beside its neck, where the card says "+$4"). Null: the same figure.</summary>
            public string TileFigure;
            /// <summary>The card's first line, in capitals: the kind's word, or a page character's name.</summary>
            public string Word;
            /// <summary>The card's second line: the state (ON / OFF / OFFER / LOCKED / BASE / ALWAYS), or a
            /// page's stat word.</summary>
            public string State;
            public Color StateInk;
            /// <summary>An optional third line: the caveat of a kind that waits for a free bartender.</summary>
            public string Note;
            /// <summary>A red plate. Only a page's nerf: a fitting is never red (GDD fittings spec §11.2).</summary>
            public bool Nerf;
            /// <summary>The Cream plate: the house standard, a piece owned but not worn, a premium the shelf already has.</summary>
            public bool Dim;
            /// <summary>0.55 for OFF and LOCKED, the rest at 1.</summary>
            public float Alpha = 1f;
            /// <summary>Not a badge but the medal pip: the comfort a piece adds for good. The rule the two keep
            /// (spec §11.4): a % always means a buff that works while the piece is in; a decimal beside the medal
            /// always means the comfort the room keeps.</summary>
            public bool IsComfort;
            public double Comfort;
        }

        private static readonly ConditionalWeakTable<TileSpec, List<ShopBuff>> s_shopBuffs =
            new ConditionalWeakTable<TileSpec, List<ShopBuff>>();

        /// <summary>Shared and never written to: a listing with nothing to say.</summary>
        private static readonly List<ShopBuff> NoShopBuffs = new List<ShopBuff>(0);

        /// <summary>The bar top's SERVICE per step, in the badge's points (0.03 satisfaction = +3%): the literal in
        /// <see cref="TycoonRun.Ambience"/>, which reaches its 0.06 cap exactly at the top rung, so every step
        /// gives all of it. Core names no constant for it; if it ever does, read that instead.</summary>
        private const int BarTopServicePct = 3;

        /// <summary>A glass step's SERVICE, in tenths of the same points (0.006 = +0.6%): the other literal in
        /// <see cref="TycoonRun.Ambience"/>. Five lines of five steps land exactly on its 0.15 cap, so no step is
        /// ever wasted against it.</summary>
        private const int GlassStepServiceTenths = 6;

        private static void SetShopBuffs(TileSpec spec, List<ShopBuff> buffs)
        {
            if (spec == null) return;
            s_shopBuffs.Remove(spec);
            if (buffs != null && buffs.Count > 0) s_shopBuffs.Add(spec, buffs);
        }

        /// <summary>
        /// What a listing says it does, lead first. What a builder set explicitly; otherwise an OPEN page's
        /// character, read off <see cref="TileSpec.Recipe"/> - which only an open recipe tile carries, so a sealed
        /// crate can never leak a character through here.
        /// </summary>
        private List<ShopBuff> ShopBuffsOf(TileSpec spec)
        {
            if (spec == null) return NoShopBuffs;
            if (s_shopBuffs.TryGetValue(spec, out var set)) return set;
            if (spec.Recipe != null && spec.State != TileState.Sealed)
            {
                var page = PageShopBuff(spec.Recipe);
                if (page != null) return new List<ShopBuff>(1) { page };
            }
            return NoShopBuffs;
        }

        private static ShopBuff LeadOf(List<ShopBuff> buffs)
        {
            foreach (var b in buffs) if (!b.IsComfort) return b;
            return null;
        }

        private static ShopBuff ComfortOf(List<ShopBuff> buffs)
        {
            foreach (var b in buffs) if (b.IsComfort) return b;
            return null;
        }

        // ── what each kind of listing says ──────────────────────────────────────────────────────────────────

        /// <summary>A page's character, as the book's strip says it: the stat's mark and figure on the sign's
        /// plate, the character's NAME and then the stat's word beside it on the card.</summary>
        private ShopBuff PageShopBuff(RecipeDefinition r)
        {
            var t = DrinkTraits.Of(r);
            if (ReferenceEquals(t, DrinkTrait.None) || !t.HasStat) return null;
            return new ShopBuff
            {
                Key = t.StatKey,
                Figure = Pct(t.StatPercent),
                Word = UIText.Caps(TraitName(t)),
                State = BuffWord(t.StatKey),
                StateInk = ShopInkSoft,
                Nerf = t.Sign == TraitSign.Nerf,
            };
        }

        /// <summary>
        /// ONE RUNG OF THE ROOM: each of its effects in Core's order (<see cref="TycoonRun.FittingEffects"/> - the
        /// kind, a tool's derived figure, FREE DRAIN as a flag), each in the state the fittings spec's table gives
        /// it, then the comfort it adds for good as the medal pip.
        ///   ON      installed (<see cref="TycoonRun.IsActive"/>) and not the house standard   Lime
        ///   OFF     owned, not installed                                                    Cream at 0.55
        ///   OFFER   buyable tonight                                                         Lime
        ///   LOCKED  behind its rung or its star                                             Lime at 0.55
        ///   BASE    the standard the room opens with, +0%                                   Cream
        /// The author: "Hangi geliştirme takılıysa o buff aktif olacak" - the one line on the card that says
        /// whether the number is working is the reason this exists.
        /// </summary>
        private List<ShopBuff> FittingShopBuffs(TycoonRun run, FixtureDefinition f, bool owned, bool locked)
        {
            var list = new List<ShopBuff>(3);
            if (run == null || f == null) return list;
            bool active = run.IsActive(f);
            foreach (var e in run.FittingEffects(f))
            {
                if (e.Kind == null) continue;
                var b = new ShopBuff
                {
                    Key = e.Kind.StatKey,
                    Figure = e.IsFlag ? "" : Pct(e.Percent),
                    Word = BuffWord(e.Kind.StatKey),
                    StateInk = ShopInkSoft,
                };
                if (e.IsBase) { b.Dim = true; b.State = UIText.T("buff.state.base"); }
                else if (active) { b.State = UIText.T("buff.state.on"); b.StateInk = UITheme.Lime[1]; }
                else if (owned) { b.Dim = true; b.Alpha = 0.55f; b.State = UIText.T("buff.state.off"); b.StateInk = ShopCost; }
                else if (locked) { b.Alpha = 0.55f; b.State = UIText.T("buff.state.locked"); }
                else b.State = UIText.T("buff.state.offer");
                // A TOOL SAYS ITS OWN NUMBER TOO (spec §11.4): the per cent is against the house tin or basin,
                // the seconds or the speed are what the hand will feel.
                string own = ToolFigure(f, e.Kind);
                if (own != null) b.State += " · " + own;
                if (e.Kind.WhileKeepingUp && !e.IsBase) b.Note = UIText.T("buff.state.keeping_up");
                list.Add(b);
            }
            if (f.Comfort > 0)
                list.Add(ComfortShopBuff(f.Comfort,
                    UIText.T(f.Level > 0 ? "decor.buff.comfort_ladder" : "decor.buff.comfort_kept"),
                    locked ? 0.55f : 1f));
            return list;
        }

        /// <summary>A tool's absolute number beside its per cent: "3.0 s" at the basin, "x1.5" on the tin and the
        /// tower. Null for anything else (and for FREE DRAIN, which is a flag with nothing to count).</summary>
        private static string ToolFigure(FixtureDefinition f, FittingBuff kind)
        {
            if (ReferenceEquals(kind, FittingBuffs.Wash))
                return UIText.T("buff.seconds", ("seconds",
                    (f.WashSeconds > 0 ? f.WashSeconds : Housekeeping.WashSeconds).ToString("0.0", CultureInfo.InvariantCulture)));
            if (ReferenceEquals(kind, FittingBuffs.ShakeSpeed) || ReferenceEquals(kind, FittingBuffs.PourSpeed))
                return UIText.T("decor.buff.speed", ("speed", f.WorkSpeed.ToString("0.0#", CultureInfo.InvariantCulture)));
            return null;
        }

        private ShopBuff ComfortShopBuff(double comfort, string line, float alpha = 1f) => new ShopBuff
        {
            Key = "comfort",
            Figure = "+" + comfort.ToString("0.0#", CultureInfo.InvariantCulture),
            Word = BuffWord("comfort"),
            State = line,
            StateInk = ShopInkSoft,
            Alpha = alpha,
            IsComfort = true,
            Comfort = comfort,
        };

        /// <summary>
        /// THE BAR'S OWN FITTINGS, WHICH ARE ALWAYS ON (2026-09-23): the stool, the bar top and a glass line are
        /// not worn and cannot be taken off, so their state is ALWAYS - and each says only what Core grants. The
        /// stool: one more seat, and <see cref="VenueComfort.StoolComfort"/> to the room. The bar top: +3 points of
        /// satisfaction on every served visit (SERVICE +3%). A glass step: +0.6. The old stool line promised
        /// "+0.25 stars", which Core never paid - it was the comfort, and it is said as comfort now.
        /// </summary>
        private List<ShopBuff> AlwaysShopBuffs(string key, string figure, double comfort)
        {
            var list = new List<ShopBuff>(2)
            {
                new ShopBuff
                {
                    Key = key,
                    Figure = figure,
                    Word = BuffWord(key),
                    State = UIText.T("buff.state.always"),
                    StateInk = UITheme.Lime[1],
                },
            };
            if (comfort > 0) list.Add(ComfortShopBuff(comfort, UIText.T("decor.buff.comfort_kept")));
            return list;
        }

        /// <summary>A glass step's SERVICE, which is less than a whole per cent: "+0.6%" ("+%0.6").</summary>
        private static string GlassStepServiceFigure() =>
            UIText.T("decor.buff.pct_tenths", ("pct",
                (GlassStepServiceTenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture)));

        /// <summary>
        /// A BETTER BOTTLE'S BUFF (2026-09-23, "markette ürünlerde gözükmeli"): every drink poured from a spirit
        /// above the well earns <see cref="TycoonConfig.StockPremiumPerTier"/> a rung on its price - but only from
        /// the BEST bottle of its style on the shelf (TycoonRun.PremiumFor), so the badge is green only where it
        /// would actually be earned: on the board, a bottle better than any of its style behind the bar; in the
        /// restock aisle, the one that is the best. A bottle the shelf already outdoes wears it Cream, and its line
        /// says why. Null for a bottle that never earns one: the well rung, a mixer, a keg, or a style no page on
        /// the book asks for by name.
        /// </summary>
        private ShopBuff StockPremiumBuff(IngredientCard card, ShelfBottle onShelf)
        {
            var run = Run;
            var info = card?.Info;
            if (run == null || info == null || info.Tier <= 1 || string.IsNullOrEmpty(info.Style)) return null;
            if (card.Type == IngredientType.Beer || !IngredientCategories.IsAlcoholic(info.Category, card.Type)) return null;
            int premium = (info.Tier - 1) * run.Config.StockPremiumPerTier;
            if (premium <= 0 || !AnyPageAsksForStyle(run, info.Style)) return null;
            int best = 0;
            foreach (var b in run.Shelf.Bottles)
            {
                var bi = b.Ingredient.Info;
                if (bi != null && bi.Style == info.Style && bi.Tier > best) best = bi.Tier;
            }
            bool live = onShelf != null ? info.Tier >= best : info.Tier > best;
            return new ShopBuff
            {
                Key = "stock_premium",
                Figure = "+$" + premium,
                TileFigure = "+" + premium,
                Word = BuffWord("stock_premium"),
                State = !live ? UIText.T("market.bottle.premium_held")
                    : onShelf != null ? UIText.T("market.bottle.premium_live")
                    : UIText.T("market.bottle.joins_shelf"),
                StateInk = live ? UITheme.Lime[1] : ShopInkSoft,
                Dim = !live,
            };
        }

        /// <summary>Whether any page on the book pours this style by name - the only band the premium is paid on.</summary>
        private static bool AnyPageAsksForStyle(TycoonRun run, string style)
        {
            if (run.AllRecipes == null) return false;
            foreach (var r in run.AllRecipes)
            {
                if (r?.RatioRequirements == null) continue;
                foreach (var band in r.RatioRequirements)
                    if (band.IsStyleBand && band.Style == style) return true;
            }
            return false;
        }

        /// <summary>What a ladder's head says the WORN rung does: "CROWD +4%", "WASH TIME -26% · FREE DRAIN".
        /// Null for the house standard, and for a rung that names no kind.</summary>
        private string LadderBuffWords(TycoonRun run, FixtureDefinition worn)
        {
            if (run == null || worn == null) return null;
            var said = new StringBuilder();
            foreach (var e in run.FittingEffects(worn))
            {
                if (e.Kind == null || e.IsBase) continue;
                if (said.Length > 0) said.Append(" · ");
                said.Append(BuffWord(e.Kind.StatKey));
                if (!e.IsFlag) said.Append(' ').Append(Pct(e.Percent));
            }
            return said.Length > 0 ? said.ToString() : null;
        }

        // ── how it is drawn ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The shared badge for one of these: the icon and the FIGURE as the loudest thing - no word, which is
        /// the reading card's to say (a tile's picture and a fitting card's row have 120-odd units, and a 16-unit
        /// figure face leaves no word room in them; measured 2026-09-23: "+22% SECOND ROUND" is 190). A flag has
        /// no figure and prints its word instead. OFF and LOCKED stand back at 0.55.
        /// </summary>
        private RectTransform ShopBadge(RectTransform host, ShopBuff b, Vector2 anchor, Vector2 pos,
            bool withWord = false, bool onTile = false)
        {
            string figure = onTile && b.TileFigure != null ? b.TileFigure : b.Figure;
            bool word = withWord || string.IsNullOrEmpty(figure);
            var badge = BuffBadge(host, b.Key, figure, b.Nerf, anchor, pos, word, b.Dim);
            StandBack(badge, b.Alpha);
            return badge;
        }

        private static void StandBack(RectTransform rt, float alpha)
        {
            if (rt == null || alpha >= 0.999f) return;
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            g.blocksRaycasts = false;
            g.interactable = false;
        }

        /// <summary>
        /// THE COMFORT PIP (2026-09-23): the medal and the comfort a piece adds for good, on the picture, on the
        /// stamps' own dark plate - so the card's one badge row is left to the buff, and a decimal beside the
        /// medal is never mistaken for a per cent. The figure is set in the figure face at 8.
        /// </summary>
        private RectTransform ComfortPip(RectTransform host, Vector2 anchor, Vector2 pos, double comfort, float alpha = 1f)
        {
            var pip = NewRect("Comfort", host);
            var bg = pip.gameObject.AddComponent<Image>();
            var ink = UITheme.Night[0];
            bg.color = new Color(ink.r, ink.g, ink.b, 0.82f);
            bg.raycastTarget = false;
            float x = 4f;
            var art = ItemArt.Medal(true, 16f);
            if (art != null)
            {
                // At its own pixels when that is 16 or less, as the shared fact row draws every house mark.
                float mw = Mathf.Min(16f, art.rect.width), mh = Mathf.Min(16f, art.rect.height);
                var m = NewRect("Mark", pip);
                Place(m, new Vector2(0, 0.5f), new Vector2(mw, mh), new Vector2(x + Mathf.Floor((16f - mw) * 0.5f), 0f));
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = art;
                mi.preserveAspect = true;
                mi.raycastTarget = false;
                mi.color = Color.white;          // the author's own drawing: never tinted
                x += 16f + 4f;
            }
            var fig = NewText("Figure", pip, _figures, 8, TextAnchor.MiddleLeft, UITheme.Cream[4]);
            fig.horizontalOverflow = HorizontalWrapMode.Overflow;
            fig.raycastTarget = false;
            fig.text = "+" + comfort.ToString("0.0#", CultureInfo.InvariantCulture);
            float fw = Mathf.Ceil(fig.preferredWidth);
            Place(fig.rectTransform, new Vector2(0, 0.5f), new Vector2(fw + 2f, 16f), new Vector2(x, 0f));
            Place(pip, anchor, new Vector2(x + fw + 6f, 20f), pos);
            StandBack(pip, alpha);
            return pip;
        }

        /// <summary>
        /// THE BUFFS, FIRST, ON THE READING CARD (2026-09-23): one row per buff under the card's rule - the badge,
        /// then its word in the shop's heavy face, then the state it is in (and a caveat, when the kind has one),
        /// each measured, so a long Turkish state line wraps under itself rather than into the next row. Built
        /// under the card, which already takes every raycast off whatever it holds. Returns what it spent.
        /// </summary>
        private float ShopBuffRows(List<ShopBuff> buffs, float y)
        {
            if (_shopCard == null) return 0f;
            if (_cardBuffRows == null)
            {
                _cardBuffRows = NewRect("BuffRows", _shopCard);
                _cardBuffRows.anchorMin = _cardBuffRows.anchorMax = _cardBuffRows.pivot = new Vector2(0, 1);
            }
            foreach (Transform old in _cardBuffRows)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            bool any = buffs != null && buffs.Count > 0;
            _cardBuffRows.gameObject.SetActive(any);
            if (!any) return 0f;

            const float W = ShopCardW - 2f * ShopCardGutter;
            float at = 0f;
            foreach (var b in buffs)
            {
                var row = NewRect("Row", _cardBuffRows);
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
                var mark = b.IsComfort
                    ? ComfortPip(row, new Vector2(0, 1), new Vector2(0f, -1f), b.Comfort, b.Alpha)
                    : ShopBadge(row, b, new Vector2(0, 1), Vector2.zero);
                float tx = Mathf.Ceil(mark.sizeDelta.x) + 8f, tw = Mathf.Max(40f, W - tx);
                float ty = 1f;
                // A flag's badge already carries its word (it has no figure to carry instead), so the row does
                // not say it twice.
                bool wordOnBadge = !b.IsComfort && string.IsNullOrEmpty(b.Figure);
                if (!wordOnBadge) ty += RowLine(row, "Word", _shop, ShopInk, UIText.Caps(b.Word ?? ""), tx, ty, tw);
                if (!string.IsNullOrEmpty(b.State)) ty += RowLine(row, "State", _body, b.StateInk, b.State, tx, ty, tw);
                if (!string.IsNullOrEmpty(b.Note)) ty += RowLine(row, "Note", _body, ShopInkSoft, b.Note, tx, ty, tw);
                float h = Mathf.Max(mark.sizeDelta.y, ty);
                row.sizeDelta = new Vector2(W, h);
                row.anchoredPosition = new Vector2(0f, -at);
                at += h + 4f;
            }
            _cardBuffRows.sizeDelta = new Vector2(W, at);
            _cardBuffRows.anchoredPosition = new Vector2(ShopCardGutter, -y);
            return at + 2f;
        }

        /// <summary>One 8-unit line of a buff row, wrapped in its column and cut to what it needs.</summary>
        private float RowLine(RectTransform row, string name, Font face, Color ink, string text, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var t = NewText(name, row, face, 8, TextAnchor.UpperLeft, ink);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(w, 400f), new Vector2(x, -y));
            t.text = text;
            float h = Mathf.Max(t.fontSize + 2f, Mathf.Ceil(t.preferredHeight));
            t.rectTransform.sizeDelta = new Vector2(w, h);
            return h;
        }

        /// <summary>The rows under the reading card. Declared here, not with the card's other fields in
        /// TycoonHud.cs, which is another session's file today; made on the first card that has a buff to say.</summary>
        private RectTransform _cardBuffRows;

        /// <summary>
        /// WHAT THE ROOM GIVES, AT THE TOP OF THE UPGRADE SCREEN (2026-09-23, the author: "oyuncu bu upgradelere
        /// biraz muhtaç edilmeli"): a dark band like the comfort band beside it, the caption, then one badge per
        /// kind the installed room moves - the data kinds as Core totals them (<see cref="HouseBuffs.Percent"/>,
        /// already clamped), the tools as the installed piece says them - in <see cref="FittingBuffs.All"/> order.
        /// A badge takes the pointer: the reading card names the pieces that make it. What does not fit is
        /// counted; the note that only what is on show works is said when there is room for it.
        /// </summary>
        private void RoomBuffBand(TycoonRun run)
        {
            if (_offerRow == null || run == null) return;
            var room = RoomShopBuffs(run);
            var h = NewRect("RoomBuffs", _offerRow);
            h.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var band = NewRect("Band", h);
            Stretch(band, Vector2.zero, Vector2.one, new Vector2(0, 3), new Vector2(0, -3));
            var bi = band.gameObject.AddComponent<Image>();
            bi.sprite = ChromeArt.Card();
            bi.type = Image.Type.Sliced;
            bi.color = UITheme.Night[1];
            bi.raycastTarget = false;

            var cap = NewText("Cap", band, _shop, 16, TextAnchor.MiddleLeft, UITheme.Cream[3]);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            cap.text = UIText.T("buff.band.title");
            float capW = Mathf.Ceil(cap.preferredWidth);
            Place(cap.rectTransform, new Vector2(0, 0.5f), new Vector2(capW + 2f, 20f), new Vector2(10f, 0f));

            // The aisle's own width, as ShopSection reads it: the viewport has already given the rail its side.
            float bandW = _offerRow.rect.width > 100f ? _offerRow.rect.width : 796f;
            float x = 10f + capW + 12f, right = bandW - 10f;
            if (room.Count == 0)
            {
                var none = NewText("None", band, _body, 8, TextAnchor.MiddleLeft, UITheme.Cream[2]);
                none.horizontalOverflow = HorizontalWrapMode.Wrap;
                none.verticalOverflow = VerticalWrapMode.Truncate;
                Place(none.rectTransform, new Vector2(0, 0.5f), new Vector2(Mathf.Max(40f, right - x), 24f), new Vector2(x, 0f));
                none.text = UIText.T("buff.band.none");
                return;
            }

            const float Gap = 6f, MoreW = 44f;
            int shown = 0;
            for (int i = 0; i < room.Count; i++)
            {
                var (buff, from) = room[i];
                var badge = ShopBadge(band, buff, new Vector2(0, 0.5f), new Vector2(x, 0f));
                float w = badge.sizeDelta.x;
                // The last one may run to the edge; any other must leave room for the count of the rest.
                float limit = i == room.Count - 1 ? right : right - MoreW;
                if (x + w > limit)
                {
                    badge.gameObject.SetActive(false);
                    Destroy(badge.gameObject);
                    break;
                }
                var plate = badge.GetComponent<Image>();
                if (plate != null) plate.raycastTarget = true;      // it has something to say on the pointer
                var card = new TileSpec
                {
                    Identity = UIText.Caps(buff.Word ?? ""),
                    Body = SourcesLine(from),
                };
                SetShopBuffs(card, new List<ShopBuff>(1) { buff });
                var relay = badge.gameObject.AddComponent<HoverRelay>();
                relay.Entered = () => ShowShopCard(card);
                relay.Exited = () => ShowShopCard(null);
                x += w + Gap;
                shown++;
            }
            if (shown < room.Count)
            {
                var more = NewText("More", band, _shop, 16, TextAnchor.MiddleLeft, UITheme.Cream[3]);
                more.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(more.rectTransform, new Vector2(0, 0.5f), new Vector2(MoreW, 20f), new Vector2(x, 0f));
                more.text = "+" + (room.Count - shown).ToString(CultureInfo.InvariantCulture);
                return;
            }

            var note = NewText("Note", band, _body, 8, TextAnchor.MiddleRight, UITheme.Cream[2]);
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.text = UIText.T("buff.band.note");
            float noteW = Mathf.Ceil(note.preferredWidth);
            if (x + 12f + noteW > right) { note.enabled = false; return; }
            Place(note.rectTransform, new Vector2(1, 0.5f), new Vector2(noteW + 2f, 14f), new Vector2(-10f, 0f));
        }

        /// <summary>
        /// The installed room's buffs, one per kind that moves: the twelve the data carries as Core totals them,
        /// then the tools as the piece in the slot says them (their figure is the tool's own, never summed).
        /// </summary>
        private List<(ShopBuff buff, List<FixtureDefinition> from)> RoomShopBuffs(TycoonRun run)
        {
            var list = new List<(ShopBuff, List<FixtureDefinition>)>();
            var house = run.Buffs;                  // read once per rebuild (spec §11.1)
            foreach (var kind in FittingBuffs.All)
            {
                if (!kind.IsTool)
                {
                    int pct = house.Percent(kind);
                    if (pct == 0) continue;
                    list.Add((RoomShopBuff(kind, Pct(pct)), new List<FixtureDefinition>(house.Sources(kind))));
                    continue;
                }
                foreach (var f in run.ActiveFittings)
                    foreach (var e in run.FittingEffects(f))
                        if (ReferenceEquals(e.Kind, kind) && !e.IsBase)
                            list.Add((RoomShopBuff(kind, e.IsFlag ? "" : Pct(e.Percent)), new List<FixtureDefinition>(1) { f }));
            }
            return list;
        }

        private ShopBuff RoomShopBuff(FittingBuff kind, string figure) => new ShopBuff
        {
            Key = kind.StatKey,
            Figure = figure,
            Word = BuffWord(kind.StatKey),
            State = UIText.T("buff.state.on"),
            StateInk = UITheme.Lime[1],
            Note = kind.WhileKeepingUp ? UIText.T("buff.state.keeping_up") : null,
        };

        /// <summary>"From: Neon Flamingo, Wall Lamps." - the pieces that make a band badge, by their own names.</summary>
        private static string SourcesLine(List<FixtureDefinition> from)
        {
            if (from == null || from.Count == 0) return "";
            var names = new List<string>(from.Count);
            foreach (var f in from) names.Add(UIText.Data("fixture", f.Id, "name", f.Name));
            return UIText.T("buff.band.from", ("pieces", string.Join(", ", names)));
        }
    }
}
