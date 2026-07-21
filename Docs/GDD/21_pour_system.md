# LAST CALL — GDD Module 21: The Pour System

> v4 NOTE (2026-07-22): pouring/ratio/recipe rules here remain the craft subsystem, but
> the drink is now built in the SHAKER flow (module 24) and judged as a named ORDER
> (module 23 §4). Score/Mult language is retired with the old loop.

> **v3.0 — the pour pivot.** Replaces card-drawing with pouring. The shelf is always in front
> of you; you hold a bottle to pour, watch the glass fill, and serve when the mix reads right.
> This module owns the pour, the glass and the shelf economy. Module 19 still owns emotions
> and the read; module 20 still owns regulars and the week.
>
> **Supersedes:** the Cabinet/deck, the 8-card rail, Restocks, and recipe pattern-matching by
> card count. See §8 for what happens to each.

## 1. The pitch in one line

You are not playing cards. You are **pouring a drink for a specific person**, and how much of
each bottle goes in is the whole decision.

## 2. Why this replaces the deck

The deckbuilder draw asked "what did I get?". The pour asks "what does this person need, and
can my hand hold steady?". The second question is the one this game has been growing toward
since the emotion pivot — the draw was leftover genre scaffolding, and it fought the fiction:
a bartender does not draw a random gin.

What replaces the draw as a source of tension:

- **Precision.** Ratios are continuous. Getting 70/30 is a skill, not a lookup.
- **Scarcity.** Bottles run dry and refills cost money (§6).
- **Progression.** Bottles, glassware and the bar itself are upgradeable (§7).

## 3. Pouring

Hold a bottle → it pours into the glass at its `PourRate` until you let go or the glass
overflows. Release to stop.

```
Glass capacity          C        (upgradeable, §7.2)
Poured volume of i      v_i
Total volume            V = Σ v_i
Fill fraction           F = V / C          (0…1)
Ratio of ingredient i   r_i = v_i / V      (0…1, sums to 1)
```

**The glass cannot overflow (final ruling 2026-07-20).** Pouring past `C` simply stops at
the brim: the glass takes what fits, the bottle keeps the rest, and the hold releases.
Two earlier rulings ("a spill ruins the drink", then "a spill still serves at a penalty")
both punished a heavy hand with drama; playtesting said the drama wasn't fun — a
held-too-long pour now costs *precision* (your ratios and fill drift from what you wanted),
which is punishment enough. With nothing left to spill, `Spills`/`NoSpillsThisCustomer`
now count deliberate waste: binning a glass with anything in it.

**Garnishes go in by the pinch (2026-07-20).** One tap of a garnish jar drops a fixed
**5% of the glass** (`GarnishClickFraction`), however long the button was held; taps stack.
Trickling out 1% slivers of mint by timing a held jar was busywork with no read behind it —
a pinch is a deliberate, countable act, and two pinches of anything stay under the 15%
stray allowance a recipe tolerates (§9).

### 3.1 The glass readout

Top-left of the screen, diegetic: a stemmed cocktail glass that fills as you pour,
**layered and coloured by what went in** — each band in its style's signature colour
(GDD 22 §1) — in pour order, bottom-up. The liquid is clipped to the bowl by a stencil
mask baked from the sprite's own interior, so the fill hugs the glass instead of floating
on it as a square. The fill % prints inside the bowl; the live ratio list sits under the
foot. This is the primary feedback channel — the numbers are secondary.

## 4. Emotions scale with volume

Every ingredient already carries emotion charges (GDD 19 §4). They are now **per full glass**,
and what you actually poured scales them:

```
applied(e) = round( Σ_i  charge_i(e) × (v_i / C) × chargeMultiplier )
```

So a full glass of pure vodka delivers vodka's printed charges. Half a glass delivers half.
A full glass of 70% vodka / 30% lemon delivers 0.7 × vodka + 0.3 × lemon.

**This is the core decision the game is now about.** Vodka is energising, lemon is sharpening,
bourbon settles anger and opens sadness — you read who is in front of you and pour the
proportions that move *their* stats where *they* asked. The example that drove this design:
someone who needs energy without the edge gets 70% vodka, 30% lemon.

Because ratios are continuous, the fine-adjustment problem from GDD 19 §4 mostly dissolves:
you no longer need a Garnish card printed at exactly −4 to land on zero, you pour a smaller
measure. Garnishes remain for their charges, not as precision tools.

## 5. The glass length axis

The customer wants more than the right emotional balance — they want a particular *kind* of
drink in front of them. The ID carries a second row:

| Preference | Target fill | Reads as |
|---|---|---|
| **Long** | 0.75 – 1.00 | something to nurse, to sit with |
| **Regular** | 0.45 – 0.75 | an ordinary drink |
| **Short** | 0.15 – 0.45 | one sharp thing, then out |

Landing inside the band moves a **named second emotion**, shown on the ID next to the
preference. Missing it does nothing — it is a bonus objective, never a penalty, because
punishing both axes at once would make every serve a coin flip.

> **Tone ruling, non-negotiable.** Volume comes from **every** ingredient, mixers included. A
> Long drink can be 20% gin and 55% soda; a Short one can be neat spirit. So the fill axis is
> the drink's *length*, never its strength, and the game never rewards pouring more alcohol.
> This is what keeps GDD 19's guardrail intact under a mechanic that would otherwise violate
> it outright: the bartender is still reading what someone needs, and "a tall glass of soda
> and bitters to hold for an hour" is a complete, correct answer.

## 6. Bottles run dry

Each bottle holds a finite volume, tracked across the whole run. Pouring spends it. When a
bottle empties it is unavailable until refilled in the Back Room.

- Refill cost scales with the bottle's tier (§7.1) — the good stuff is expensive to keep in.
- This is the economy pressure that replaces Restocks: *when* to spend the expensive bottle is
  a real decision, and a night that goes badly leaves you dry for the next one.
- **Ruling:** a bottle with some volume left can always be poured; it simply runs out
  mid-pour. Running dry mid-pour is not a spill — you get what was left.

## 7. Progression

Money now buys three things instead of one.

### 7.1 Bottles
Upgrade an ingredient's tier: stronger charges, larger capacity, higher refill cost. This is
the deckbuilder's "improve your deck" moved onto the shelf.

### 7.2 Glassware
Bigger capacity (more room for precision, higher overflow ceiling), and later, **multiple
glass shapes** with different capacities that suit different length preferences.

### 7.3 The bar
Room-level upgrades that change the whole run: a faster well, a speed pourer (finer control),
better lighting (narrows RANGE bands), a back-bar mirror (see one extra reading). This is
where the information economy from GDD 19 §8 relocates.

## 8. What happens to the old systems

| System | Fate |
|---|---|
| Cabinet / deck / draw | **Deleted.** The shelf is always available. |
| 8-card rail | **Deleted.** Replaced by the shelf and the glass. |
| Restock | **Deleted.** Bottle volume is the new scarcity. |
| Mixes per customer | **Kept**, renamed: how many drinks a customer will accept. |
| Chat | **Kept.** Costs a drink slot instead of a Restock. |
| Recipes | **Kept, re-specified.** See §9. |
| Emotion charges | **Kept**, now scaled by poured volume (§4). |
| Regulars, drift, relationship, quota, demand | **Untouched.** |
| Patrons, VIPs, Tools, vouchers | **Kept**; the ones keyed on rail/restock behaviour need an audit. |

## 9. Recipes, re-specified

Recipes are not deleted and they are not the primary decision. They become **ratio bands you
can hit on top of serving the right emotions**:

```
MARTINI    gin 55-75%,  vermouth 10-25%,  fill ≥ 0.70
SPRITZ     aperol 25-40%, prosecco 40-60%, soda 10-25%
```

Hitting a recipe's bands pays its Flavor and Mult exactly as before, and applies its
`chargeMultiplier` so a well-made drink carries its emotional charges further. Missing every
recipe still produces a real drink that moves real emotions — at ×0.5 charge, exactly the
existing no-recipe rule from GDD 19 §5, now generalised.

**The house pour (2026-07-20):** a drink that matches no recipe is no longer a hard zero —
it pays its **volume-weighted Flavor at ×1** (a full glass of a Flavor-7 bourbon ≈ 7
points), while a recipe pays `Flavor × Mult` (a Martini ≈ 160). Pouring *something* always
beats pouring nothing, experimenting reads as practice instead of punishment, and the
order-of-magnitude gap to real recipes is the reward ladder. Emotion charges still land at
×0.5 without a recipe — the house pour changes money, not feelings.

**Generosity pass (2026-07-20):** derived bands widened from ±15% to **±20%**, and the
unnamed-stray allowance from 10% to **15%** of the glass. Free-hand pouring on a held
button lands within ~10% at best, so the old bands made recipes a precision test rather
than a judgement call — and a garnish pinch (5%) plus a splash could knock a drink out of
its recipe. Wider bands overlap more between neighbouring recipes; rank order arbitrates,
which is what rank is for. The recipe book in the HUD now teaches exactly this: each
pourable recipe as its bands, unpourable ones counted as "house secrets".

**Ruling:** the emotional ratio comes first and the recipe is a bonus, not a gate. A player
who serves the perfect emotional mix and matches no recipe should still satisfy the customer;
they just earn fewer points doing it. Inverting that — forcing recipe compliance — would put
the craft layer back in charge of a game that is now about reading people.

## 10. Open questions

- **Round limiter.** "Drinks a customer will accept" is inherited from Mixes and untested
  under pouring. If a drink takes 10 seconds to build, 4 per customer may be far too many.
- ~~**Overflow harshness.**~~ **Resolved 2026-07-20** (§3, twice in one day): first spills
  became servable-at-a-penalty, then playtesting removed spilling entirely — the glass
  stops at the brim and "spills" now mean binned drinks.
- **Input.** Hold-to-pour needs a controller answer and an accessibility answer (hold is bad
  for some players). A tap-to-set-measure fallback is probably required.
- **Does precision survive a pixel glass?** At 640×360 the glass is maybe 40px tall, so a
  band is ~2px per 5%. The readout may need numeric support after all.
