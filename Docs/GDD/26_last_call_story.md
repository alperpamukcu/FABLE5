# LAST CALL — GDD Module 26: The Last Customer (story, dialogue, the closing beat)

**Status:** design, not built. Written 2026-08-12 from the author's own brief: the last call
should be *scripted*, not sandbox — a boss-like final customer with dialogue, who asks for a
drink the bar cannot pour yet, so the ask becomes the reason to grow. Small story, not a deep
one. Staging lives in `PLAN_last_call.md`; this module is the rulebook.

---

## 1. Why this exists

Two measured gaps, one answer.

**The night has no shape.** The floor is a flat stream: arrivals on a gap timer until the
clock runs out (`BarDay`, `NightSeconds = 95`), then the till counts up. Nothing rises,
nothing closes. The game is called LAST CALL and the phase model — `DayOpen → DayEnd →
Closed` — has no last call in it.

**Growing has no face.** The market, the star gates and the tiers all work, and nothing ever
*asks* the player for a specific thing. The 200-run sim reaches day 30 in every seed; the only
pressure is arithmetic. A named person who wants one drink you cannot make turns the shopping
list into a debt of honour.

The last customer answers both at once, and it costs no new subsystem: the night already stops
letting people in (`IsClosingTime`), and already refuses to end while somebody is still on a
stool (`IsComplete => IsClosingTime && _seated.Count == 0`). The last customer is the one
arrival that comes through a shut door.

---

## 2. The beat

```
18:00 ──────────────── the ordinary night ────────────────► 02:00        (NightSeconds)
                                                            │
                             the door shuts, the room drains │
                                                            ▼
                                                   ┌──────────────────┐
                                                   │  THE LAST CALL   │  one person, one ask
                                                   └──────────────────┘
                                                            │
                                              served / declined / got it wrong
                                                            ▼
                                                       DayEnd, the slip
```

**Trigger.** The night closes its door on the clock, exactly as it does today. When the last
ordinary drinker leaves — the moment `IsClosingTime && Seated.Count == 0` — the run asks the
story whether a beat is due tonight. If one is, the guest is seated and the night does *not*
complete until they are gone. If none is due, the night ends as it does today.

**Why after the room empties, not during the rush.** The scene needs the bar to itself: it is
a conversation, the light drops, and the player is not juggling four stools while somebody
tells them something. It also means the beat can never be lost to a bad night — it waits.

**The sign is already up.** At `IsClosingTime` the HUD's plaque already turns magenta and reads
**LAST CALL** instead of the date (`TycoonHud`, the clock row). The game has been announcing
this moment for weeks and putting nobody under it; the beat hangs off exactly that flag, so the
announcement and the person arrive together.

**What changes on screen** (§7): the room dims to the lamp over their stool, the neon LAST
CALL sign ignites, the ambience thins. No drawn cutscene.

---

## 3. The last customer

A scripted `CustomerVisit`, built from data rather than rolled:

| Rolled crowd | The last customer |
|---|---|
| `NextArrival()` rolls order, patience, decide delay, and a regular from the pool | every field comes from the beat's data |
| patience is a roll; they storm off | patience is long and finite; when it runs out they *leave a line*, they do not storm |
| the ID card gives the drink outright | the card gives the drink **and** what the bar is missing for it |
| any stool | the stool nearest the till (they sit where they can be talked to) |

They are a `RegularState` like anyone else — name, age, hometown, visits, relationship — so
the second visit already knows it is the second, and `Relationship` moves on the outcome. The
face is a reserved look (§8) so the same person is the same picture every night of the run.

**They are not a boss fight.** No timer pressure beyond the ordinary patience, no failure
state, no combat framing. The only thing at stake is the answer.

---

## 4. The ask: an order the bar cannot fill yet

The game already knows three separate ways a drink can be out of reach, and the arc should use
all three in turn, because each one teaches a different part of the shop:

1. **The shelf cannot pour it.** `MissingStyles(recipe)` already computes this and the market
   already says it in those words ("Nothing on the shelf pours tequila"). → *go buy a style.*
2. **The book does not have it.** Recipes unlock on star gates (five bands). → *earn the
   standing.*
3. **The bottle is too plain.** A band with `MinTier` refuses the well pour (`CanMake` honours
   it). → *upgrade the brand.*

**The rule that makes this a door and not a wall:** the ask must always name what is missing,
in the customer's own words on the licence and in the market's words on the tile. A player who
hears "I want a Margarita" and has no tequila must be told *tequila*, tonight, by the game —
never left to guess. This is the difference between a quest and a tease.

**A beat may bring its own page** (`grantsRecipeOnAsk`). Some asks are for a drink the book
does not contain and the star gate will not open for weeks; the person can simply hand it over
— "here is how it is built" — and the ask becomes *make this*, not *earn the right to know it*.
This exists because of a measured hole: **every stirred recipe in the catalogue is rank 22 or
higher, which is the 4★ band**, while the shake is taught at rank 3 (`gin_sour`, no gate). The
stir verb the bench grew on 2026-08-11 is therefore invisible for most of a run. Until the
recipe ladder grows an early stirred drink, the critic's beat is the only thing that teaches
the verb — and it teaches it by handing the player the page.

**Serving is the ordinary verb.** No special mode: the player builds the drink the way they
build any drink, and `ServiceJudge` grades it the way it grades any drink. The story listens
to the verdict; it does not replace it.

---

## 5. Promise, return, and never a dead end

The first ask is nearly always unfillable — that is the point. So the beat has three exits and
none of them is a loss:

- **Served right** — the arc advances to the next beat, the reward lands (§6), the
  relationship moves up a notch, the night's slip carries their line.
- **Served wrong** — the drink is graded as any drink; they say the wrong-serve line; the arc
  does *not* advance and re-arms on the return schedule. A wrong answer costs a night, not the
  arc.
- **Declined** — the player tells them the bar cannot do it tonight (an explicit verb, §7).
  They leave a line and come back. Declining is honest and cheap; it exists so the player is
  never stuck staring at an order they cannot build.

**Return schedule.** `returnsAfterDays` in data (default 2). Between visits the ask stands: the
book shows it as an open tab. A beat may also carry `nudge` lines used on the second and third
returns so the person notices the player is closer ("I saw the crate come in").

**Nothing is missable.** The arc waits. A player who never buys tequila simply never advances
past that beat, and the ordinary game continues around it.

---

## 6. What it pays

Money is not the reward — the bar makes money every night. The reward is the thing the ordinary
loop cannot give:

- **the next beat** (the story is the payment),
- **a gift with a mechanical edge**: a recipe page the star gate would not have opened yet, a
  bottle left on the counter, a fixture for the room, a standing discount at one aisle, or a
  regular who now comes back on their own,
- **standing**: a star bump, which is the arc feeding the tycoon rather than sitting beside it.

Rule: every reward must be visible in a system the player already reads (the book, the shelf,
the market, the star row). No invisible flags.

---

## 7. Dialogue and the verbs around it

**The voice.** This bar is terse. Every line the game speaks today is short and flat —
"SHAKER EMPTY — TAP A BOTTLE", "IT WANTS A MIX — BACK TO THE SHAKER". Story lines must live in
that register: **two lines per beat, one sentence each, no monologue.** If a character needs a
paragraph, the character is wrong for this game.

**The surface.** A dialogue plate at the counter, in the licence's own paper-and-ink language:
the face on the left, the name above, one or two lines set at 16, and a single key to answer.
It is not a JRPG box and it does not cover the bar.

**The verbs the player has during a last call:**

| Verb | What it is |
|---|---|
| **Listen** | advance the lines (the single key) |
| **Read the licence** | the same click as any customer — the card carries the ask *and* what is missing |
| **Serve** | the ordinary serve; the judge grades it |
| **Say no tonight** | the decline (§5) — a small key on the dialogue plate, never a hidden one |

**Choices are a later question.** If they arrive, they are *tone*, not plot: two ways to say
the same thing (curt / kind) that move the relationship, not the arc. Branching plot is out of
scope for a bar tycoon whose story is meant to be thin.

**The "cutscene" is light, not frames.** The project has a URP 2D light rig, a neon sign, and a
palette that already moves. The closing beat is: ceiling lamps down, one lamp over their stool,
the LAST CALL sign ignites, ambience thins, and the HUD's chrome quiets. Drawn cutscene frames
are explicitly *not* in this design — they cost art the project does not have and would fight
the diegetic style the room was rebuilt for (GDD 18).

---

## 8. Characters as data (a prerequisite)

Today the cast's papers — 30 names, ages, countries, flags — are a `Dictionary` **hardcoded in
`TycoonHud`**, keyed by look slug. That breaks the house rule ("content is data") and the story
cannot live on top of it: a story character needs the same papers the licence prints, and the
writer must be able to add one without touching C#.

**Prerequisite work:** move the papers table to `Assets/Data/customers/papers.json`, parsed by
`DataLoader` with loud validation, keyed by look slug, with the story's characters written in
the same file. The look (`PatronLook`) stays what it is — a face and a star gate.

A story character then is: a slug (its face), papers (what the licence prints), and a role in
the arc. Reserve its look so the crowd never wears that face.

---

## 9. Determinism, the sim, and the tests

**Determinism.** The last customer is scripted: no rolls, so no drift. Anything that must vary
draws from a new named stream (`"story"`), which by construction cannot shift the existing
streams — the same trick the read stream already uses to keep old seeds close.

**The sim bot must know.** `TycoonSimulator` plays real runs; if it meets a last customer it
cannot parse, the balance report lies. The bot serves the ask when it can pour it and declines
when it cannot, and the report gains three lines: asks / served / declined. Until that lands,
every balance number in `tycoon_sim_report.md` is suspect on any day a beat is due.

**Tests.** Core (EditMode): a beat arms on its day, the order is the scripted one, a right
serve advances, a wrong serve does not, a decline re-arms, and a story file naming a recipe
that does not exist fails at load with a loud message. UI (PlayMode): the last customer walks
in after the door shuts and the dialogue plate carries their line; the closing light beat gets
its own blessed picture (`LookTests`).

---

## 10. The data

One file, `Assets/Data/story/story.json`, parsed by `DataLoader` into Core types. JsonUtility
rules apply: public fields, no dictionaries, no nullable types, `""`/`0` for "none".

```json
{
  "version": 1,
  "characters": [
    {
      "id": "collector",
      "look": "execman",
      "name": "Graham Sedgwick",
      "age": 54,
      "hometown": "London",
      "iso": "gb",
      "blurb": "Collects for the building. Never raises his voice."
    }
  ],
  "beats": [
    {
      "id": "collector_1",
      "character": "collector",
      "day": 2,
      "recipe": "neat_pour",
      "needStyle": "bourbon",
      "needTier": 0,
      "grantsRecipeOnAsk": "",
      "patienceSeconds": 40,
      "ask": [
        "You are the new one.",
        "A whiskey, neat. I will wait."
      ],
      "nudge": [ "Still nothing brown on that shelf." ],
      "servedRight": [ "That is the one.", "The building can wait a week." ],
      "servedWrong": [ "That is not whiskey." ],
      "declined": [ "Then I will come back." ],
      "rewardMoney": 40,
      "rewardStars": 0.0,
      "rewardRecipe": "",
      "rewardBottle": "",
      "returnsAfterDays": 2,
      "next": "collector_2"
    }
  ]
}
```

Field notes:

- `recipe` — the recipe id the ask is graded against; it must exist in `recipes.json`.
- `needStyle` / `needTier` — what the beat is *about*, so the card can name what is missing
  without re-deriving it. Empty/0 = "nothing in particular; you should already be able to".
- `grantsRecipeOnAsk` — a recipe id handed over with the ask, gate and price waived (§4). `""`
  for the ordinary case where the drink is already in the book or buyable.
- `ask` / `servedRight` / `servedWrong` / `declined` — 1–2 lines each (§7). `nudge` is used on
  returns.
- `reward*` — all optional; every non-empty one must resolve at load or the file is rejected.
- `next` — the beat that arms when this one is served right. `""` ends the arc.

---

## 11. A proposed arc (placeholder cast, real shape)

Seven beats over roughly three weeks, each teaching one system through one person, with a
through-line about the bar itself. **The names and lines here are placeholders** — the shape is
the design; the writing is the author's.

The first three are written as real data in `Assets/Data/story/story.json` — real recipe ids,
real styles, and three faces the cast already owns (`execman`, `ember`, `profess`), which are
reserved from the crowd from S1 on.

| # | Night | Who | Asks for | Teaches | Gate |
|---|---|---|---|---|---|
| 1 | 2 | **The collector** — takes the rent for the building | `neat_pour` — a whiskey, neat | the market: buy a style you lack | shelf (no bourbon in the opening well) |
| 2 | 5 | **The sister** — drank here before it was yours | `moscow_mule` | the market again, and the book: the reward is a page (`gimlet`) the 2★ gate has not opened | shelf (ginger) |
| 3 | 9 | **The critic** — writes about rooms like this | `manhattan`, *stirred* | method: stirred is not shaken — and he hands over the page himself | method (+ vermouth) |
| 4 | 10 | **The brewer** — sells the kegs two valleys over | a pint with a proper head | draught: the head band | craft |
| 5 | 13 | **The old regular** — was coming here before | "the usual" | reading: the licence, the visits, the person | reading |
| 6 | 17 | **The high roller** — spends what the room is worth | a top-shelf brand, by name | tiers: the well pour will not do | tier |
| 7 | 21 | **Last call** — the drink the bar was known for | everything above, in one glass | the closing beat | all |

Through-line: the bar came with a past. Each visitor knows a piece of it; the last one asks for
the drink that past was famous for. The player never learns more than the bar would tell them.

---

## 12. Out of scope (fenced deliberately)

- Drawn cutscene frames or animated portraits (§7 — the light beat replaces them).
- Branching plot, reputation trees, multiple endings.
- Voice acting, localisation of story text (the string table question is separate).
- Any change to how ordinary customers behave. The crowd is not part of this module.
