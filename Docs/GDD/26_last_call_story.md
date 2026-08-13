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

## 1b. Ece, and whose bar this is

**The bar opened this week.** That is the premise, and it is the one the game already tells:
six bottles in the well, no reputation, nobody who knows you, and three red days from being
gone. Nothing in the arc may lean on the room having a past — it does not have one yet.

**The player owns it.** Not because it is the more flattering story, but because it is the only
one that does not fight the screens: this game hands the player the till, the rent, the
shopping list and the star rating, and those are an owner's decisions. A story in which
somebody else owns the room would be arguing with every button.

**Ece is the bartender who works the shift.** Turkish, thirty-one, and has done this before —
in rooms that did not make it. She is the player's second pair of hands and the arc's voice:
the owner has the money and the licence to sign, she has the trade. That balance is what lets
her teach without being the boss and comment without being a customer.

She has **two jobs**, and they are different content:

| | **The teacher** | **The frame** |
|---|---|---|
| what | says the first time each thing happens, as a person | introduces the last customer before they sit, reads the room after they leave |
| keyed to | STATE ("there are two spirits in that tin") | the beat (§2) |
| data | `lessons` (§10) | the beat's own `hostBefore` / `hostAfter` lines |
| when | once per run, the moment the condition is first true | at the last call |

The teaching half matters more than it sounds. Everything this game explains today, it explains
in system voice — "SHAKER EMPTY — TAP A BOTTLE", "IT WANTS A MIX — BACK TO THE SHAKER". Those
lines are good and they stay; they are the bar talking to itself. Ece is the first time anyone
says it to *you*, and the tutorial module this project deleted in the 2026-08-07 sweep comes
back through her rather than as a mode.

**She is never in the CROWD** — her look is reserved out of the arrivals pool forever, so she
can never walk in as a stranger with someone else's order. But she is not sealed behind the
bar either: **the first last call is hers.** When the door shuts on night one she crosses to
the other side of it, sits down, and asks the player for a drink — which is exactly what a
bartender does when the shift ends, and it teaches the whole beat on a night where nothing can
be lost. She takes the stool as a `guest` for that beat and goes back to being the host after
it; the role decides the arrivals pool, not the furniture.

That first beat is also where the arc is introduced: after the drink she says, in her own
words, that people will start coming in asking for things the shelf does not have, and that
this is how a room gets a name. Every later beat then arrives already framed (`hostBefore`).

**What she costs in art:** one 72×72 face to speak with — the author is drawing her
specially, and until it lands the data names a stand-in face (`placeholderLook`) so the plate
is never blank and never borrows a face that means somebody else. The field is deleted the day
the portrait ships, which is the whole point of it being a field.

The dependency, precisely: — every look in `Resources/Patron`
carries one, cropped from its own idle frame. That is the whole dependency for the plate. A
standing sprite behind the bar is a *later* want, not a blocker, and until it exists the plate
carries her name with no portrait rather than borrowing somebody else's.

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

## 2b. Which night they come on (the author, 2026-08-13)

**A guest comes on a Friday or a Saturday.** Not "on day 9" — on a night of the week, in a
week of the run.

The calendar was already there and already on the screen: six open nights, Tuesday through
Sunday, Mondays dark, and the plaque has been printing `WEEK 2 · FRIDAY` since long before it
meant anything. It meant nothing — any night was like any other night. Hanging the story on it
costs no new vocabulary and turns a caption into a rule the player can read from across the
room.

What it buys, in order of how much it matters:

1. **The days between become the shopping days.** The whole arc runs on *they asked for
   something you cannot pour yet* (§4). Under a day number, "come back in two days" is an
   arbitrary integer in a file. Under the calendar it is a **deadline with a name on it**: he
   asked on Friday, the market opens after every night, and he is back next Friday. The bar's
   quiet nights stop being filler and become preparation.
2. **Rarity keeps the beat from becoming routine.** The plaque lights magenta every night; if
   somebody walked in under it every night, the beat would be wallpaper by week two. Two
   candidate nights a week, at most one person on each.
3. **It is what the fiction already says.** A man who collects for the building, a critic who
   writes about rooms — these people come when the room is worth being seen in. Nobody
   important turns up on a Wednesday.

**The house is not a guest.** Ece works the shift; she does not *come to the bar*. Her beat
zero is the opening Tuesday, and Core allows a quiet night **only** for a character with
`role: host`. That exception is what keeps the rule sharp rather than blurring it: the weekend
is what makes a guest a guest.

**Each beat names its own night, in data.** Not rolled — a scripted night must play the same
way twice, and a character who is a Friday man should be a Friday man every time he comes back.

**The failure this rule can have, and the guard against it.** A missed beat is pushed back by
its return clock, and under a weekend gate that push must land ON the character's night. "Today
plus two" would put a Friday guest on a Sunday, where the gate can never open — and nothing
would throw, nothing would go red; the arc would simply stop for the rest of the run. So the
return is measured in **weeks on that night** (`returnsAfterWeeks`), and the test that pins it
is the one to keep: *a beat missed on a Friday comes back on a Friday, never on a Wednesday,
and never never.*

**Deliberately not in scope:** the weekend does not (yet) mean a bigger crowd, a better tier or
a different rent. That is the economy's business (GDD 23) and a separate decision; this section
only says who comes to the door after it has shut.

---

## 3. The last customer — a guest of the house (reworked 2026-08-13, the author)

A scripted `CustomerVisit`, built from data rather than rolled — and **not a customer in any
way the books notice**. The author's ruling: the story's people are outside the night's
economy entirely.

| Rolled crowd | The guest of the house |
|---|---|
| `NextArrival()` rolls order, patience, decide delay, and a regular from the pool | every field comes from the beat's data |
| the order hides behind the ID card | **no licence.** They say who they are on the way in; the ask lives in the dialogue |
| pays at the tab, tips on quality | **pays nothing.** No bill, no tip, no line in SALES |
| files a rating on the way out; the slip counts them | **files nothing.** Not in the night's stars, not on the slip, not a served/walked count |
| patience is a roll; they storm off | one written clock for the whole visit; when it runs out they *leave a line*, they do not storm |
| any stool | the stool nearest the till (they sit where they can be talked to) |

**Why the books look away, in one flag.** `CustomerVisit.OnTheHouse`, read in exactly one
place per ledger (`BarDay.FinishedCounted()` for everything that counts the night). Both
directions are exploits otherwise: a passed trial must not lift a dreadful night's stars, and
a failed one must not stain a good bar — the trial's stake is the ARC, and the arc only.

**The licence exception is written, not eroded.** "Hidden information stays hidden" is the
house's hardest rule and it stands for the crowd untouched. The guest is the one written
exception — their `InspectId()` is called by Core at the seat, because introducing yourself
is what a licence is for — and any future erosion of the crowd's rule cannot cite this line.

**§3.1 The conversation holds the clock.** They walk in like anyone else, sit like anyone
else — then the dialogue begins, and NOTHING ticks while they talk (`ClockHeld`). The trial's
clock starts when the talking ends (`BeginLastCallTrial()`), so a slow reader is never
punished for reading. Core does not trust the UI to end a conversation: after
`StoryTrial.TalkingGrace` (120s, far past any real dialogue) the trial starts itself, because
a held clock is a night that can never close.

**§3.2 They are a `RegularState` like anyone else** — name, age, hometown, visits,
relationship — so the second visit already knows it is the second. The face is a reserved
look (§8), the same person the same picture every night of the run.

**They are still not a boss fight** — the trial (§4) is pressure, but there is no failure
state beyond the beat re-arming, no combat framing, and nothing the run can lose that it had
before the door opened. The only thing at stake is the story.

---

## 4. The ask: a trial, not an order (reworked 2026-08-13, the author)

**The shape.** After the talk, the guest asks for a RUN of drinks — several, against ONE
clock, to a standard nothing else in this game asks for. An inspector's visit: the model the
author named is Dave the Diver's service nights, one demanding person instead of a full room.

- **One at a time.** The post-it on the screen shows the drink in hand and the clock — never
  the list. What comes next is the guest's to say (`StoryTrialRun.Current`).
- **One clock.** `StoryTrial.Seconds` for the whole visit, started when the talking stops.
  Landing a drink does not refresh it — a trial is a deadline, not the extra round a good
  serve earns (that path deliberately stays untouched).
- **The standard.** Exactly the drink (`OrderMatch.Exact`), garnished exactly as asked
  (spec 1.0), worked the way the book says (method 1.0). The ONE forgiving edge is the fill:
  ≥ 0.90 of the glass is a poured glass — strict, not cruel, and the author's own number.
- **Mistakes.** A wrong drink is a fumble: the ask STAYS (they still want it), and it costs
  the allowance and the time to build another. Past `allowedMistakes`, the night is failed.
  The early beats are written kind (2 allowed, then 1); the gourmet allows none.
- **The ordinary verbs, the ordinary judge.** No special mode: the player builds and serves
  exactly as for anyone, `ServiceJudge` measures exactly as for anyone. Only the PASS MARK
  is the trial's, and only the arc is at stake — the verdict that comes back is a reaction,
  not a receipt (§3).

**The shopping week survives the reveal.** Since asks come one at a time, the post-it cannot
be the thing that names what is missing IN ADVANCE — so that job moves to where it now
belongs: `needStyle` is what the HOST warns about, days early, in her own lines ("have
something brown by Friday"). The quiet nights stay the preparation nights (§2b); the warning
just has a voice now instead of a tile.

The game still knows three separate ways a drink can be out of reach, and the arc should use
all three in turn, because each one teaches a different part of the shop:

1. **The shelf cannot pour it.** `MissingStyles(recipe)` already computes this and the market
   already says it in those words ("Nothing on the shelf pours tequila"). → *go buy a style.*
2. **The book does not have it.** Recipes unlock on star gates (five bands). → *earn the
   standing.*
3. **The bottle is too plain.** A band with `MinTier` refuses the well pour (`CanMake` honours
   it). → *upgrade the brand.*

**The rule that makes this a door and not a wall:** the ask must always name what is missing —
the host days early (`needStyle`, her warning lines), the guest's own nudge on a return, and
the market's words on the tile. A player who will be asked for a Margarita and has no tequila
must be told *tequila*, before the night it costs them — never left to guess. This is the
difference between a quest and a tease.

**A beat may bring its own page** (`grantsRecipeOnAsk`) — live since 2026-08-13
(`TycoonRun.GrantRecipe`, called when the guest sits: no star gate, no price, no line on the
slip, and the only door in the game that opens the book for free). Some asks are for a drink the book
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

**Return schedule.** `returnsAfterWeeks` in data (default 1), landing on the beat's own night
(§2b). Between visits the ask stands: the book shows it as an open tab. A beat may also carry
`nudge` lines used on the second and third returns so the person notices the player is closer
("I saw the crate come in").

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

**Done, 2026-08-13 (S2), and it found things.** 200/200 arcs finish inside thirty nights, and
the night around them is unchanged — measured by running the same seeds with the arc switched
off: standing 2.82 → 2.83, storm-offs level, customers a night level. The till rises ($161 →
$208 median) because a granted page is a page nobody buys. The report also carries **what came
back and why**, with the delivered glass in the line, which is what turned three silent
failures into three named causes — see PLAN_last_call S2. The one that matters to this module:
**a fizzy Built drink cannot be poured to a high fill in big confident measures**, because the
glass is re-chosen mid-build and an overfilled glass is a permanently wrong ratio. A player
meets that trap too. If a beat ever wants a full highball of something fizzy, this is the
thing to test it against.

**Tests.** Core (EditMode): a beat arms on its day, the order is the scripted one, a right
serve advances, a wrong serve does not, a decline re-arms, and a story file naming a recipe
that does not exist fails at load with a loud message. UI (PlayMode): the last customer walks
in after the door shuts and the dialogue plate carries their line; the closing light beat gets
its own blessed picture (`LookTests`).

**As built (2026-08-13, S1).** The Core half is done and green — 23 tests in
`LastCustomerTests`, the whole EditMode suite at 238. Two of them are worth naming because
they are the ones a later change is likeliest to break: *the night cannot end while the guest
is on the stool* (the day's end condition was not extended to make this true — a guest on a
stool already fails it) and *a run without a story never hears a last call* (the arc is opt-in
exactly like the regulars, which is what keeps every older test honest). The load-time
validation is the arc's constructor for now — a circle, an orphan beat, two beats on one
night, a night that leads nowhere — and grows the recipe/style/reward checks when the file is
actually parsed in S5.

The classes: **`StoryArc`** is content, built once and shared (the sim will hand one arc to
two hundred bars); **`StoryProgress`** is where a run has got to. Nothing scripted touches the
RNG, so the `"story"` stream is still unspent.

---

## 10. The data

One file, `Assets/Data/story/story.json`, parsed by `DataLoader` into Core types. JsonUtility
rules apply: public fields, no dictionaries, no nullable types, `""`/`0` for "none".

```json
{
  "version": 1,
  "characters": [
    {
      "id": "ece",
      "look": "ece",
      "role": "host",
      "blurb": "Works the shift with you. Has done this before, in rooms that did not make it."
    },
    {
      "id": "collector",
      "look": "execman",
      "role": "guest",
      "blurb": "Collects for the building. Never raises his voice."
    }
  ],

  "lessons": [
    {
      "id": "first_night",
      "when": "first_night",
      "say": [ "Six bottles and a keg. That is a bar, technically.", "Read them before you pour. The licence says what they came for." ]
    }
  ],
  "beats": [
    {
      "id": "collector_1",
      "character": "collector",
      "week": 1,
      "night": "friday",
      "asks": ["neat_pour", "neat_pour"],
      "seconds": 100,
      "allowedMistakes": 1,
      "needStyle": "bourbon",
      "needTier": 0,
      "grantsRecipeOnAsk": "",
      "ask": [
        "You are the new one.",
        "A whiskey, neat. Then another. I drink the second one slower."
      ],
      "nudge": [ "Still nothing brown on that shelf." ],
      "servedRight": [ "That is the one.", "The building can wait a week." ],
      "servedWrong": [ "That is not whiskey." ],
      "declined": [ "Then I will come back." ],
      "hostBefore": [ "That one is here about the building, not the whiskey." ],
      "hostAfter": [ "He will be back. They always are." ],
      "rewardMoney": 40,
      "rewardStars": 0.0,
      "rewardRecipe": "",
      "rewardBottle": "",
      "returnsAfterWeeks": 1,
      "next": "collector_2"
    }
  ]
}
```

Field notes:

- `week` / `night` — WHEN, on the bar's own calendar (§2b): the week counting from the one the
  bar opened in, and one of `tuesday`…`sunday` (Mondays the bar is dark). A guest's night must
  be `friday` or `saturday`; only a `host` may be written for a quiet night. The day number is
  the calendar's business, never the file's.
- `returnsAfterWeeks` — how long after a miss they try again, **on the same night**. Weeks, not
  days, because a day count would push a Friday guest onto a night that never comes (§2b).
- `asks` — the trial's drinks IN ORDER (§4), every one an id in `recipes.json`. Revealed one
  at a time; the first is the night's headline, the thing the host warns about.
- `seconds` / `allowedMistakes` — the trial's one clock and its allowance (§4). Difficulty is
  DATA: tightening a beat is an edit, not a commit.
- `needStyle` / `needTier` — what the beat is *about*, so the HOST can name what is missing
  days early (§4). Empty/0 = "nothing in particular; you should already be able to".
- `grantsRecipeOnAsk` — a recipe id handed over with the ask, gate and price waived (§4). `""`
  for the ordinary case where the drink is already in the book or buyable.
- `characters[].role` — `guest` (sits, orders, can be served) or `host` (Ece: behind the bar,
  never seated, never rolled into the crowd). Exactly one host.
- `lessons[].when` — the NAME of a condition the code owns. This is deliberately not a
  scripting language: the game keeps a small table of predicates it can actually observe, the
  data picks one by name and supplies the words, and an unknown name is refused at load like
  any other bad reference. The starting vocabulary, all of it already computable:
  `first_night`, `first_licence` (nobody's card read yet), `two_spirits_in_the_tin`
  (Core's own `MixRequired && !IsMixed`), `first_keg`, `first_market`, `cannot_pour_the_ask`
  (a beat is due and the shelf is missing its style), `red_night`, `first_extra_order`.
  Each fires once per run, the moment its condition is first true.
- `ask` / `servedRight` / `servedWrong` / `declined` — 1–2 lines each (§7). `nudge` is used on
  returns.
- `hostBefore` / `hostAfter` — Ece's frame around the beat (§1b): one line before they sit, one
  after they leave. Empty is allowed; a beat she has nothing to say about is a beat she watches.
- `reward*` — all optional; every non-empty one must resolve at load or the file is rejected.
- `next` — the beat that arms when this one is served right. `""` ends the arc.

---

## 11. A proposed arc (placeholder cast, real shape)

Seven beats over roughly three weeks, each teaching one system through one person, with a
through-line about the bar itself. **The names and lines here are placeholders** — the shape is
the design; the writing is the author's.

The first FOUR are written as real data in `Assets/Data/story/story.json` — real recipe ids,
real styles, and three faces the cast already owns (`execman`, `teal`, `profess`), which are
reserved from the crowd from S1 on. (The sister was recast 2026-08-13, the author: the guests
are public people now — an influencer, a gourmet inspector, a collector — because a trial
needs somebody with the standing to hold one; `ember` went back to the crowd.)

**ONLY BEAT ZERO IS LIVE (2026-08-13, the author: "şimdilik sadece ece olsun").** The arc the
game plays is Ece's night and nothing else; the three guest beats below are written, validated
and parked in `Docs/story_guests_drafted.json`, to be put back one at a time once the story
around her is built. Nothing loads that file, but an EditMode test still builds it against the
real cast and the real book, so a drafted beat cannot rot in the drawer.

Every guest comes at the weekend (§2b); the host takes the quiet opening night. The day number
is the calendar's, not the author's — it falls out of the week and the night.

| # | Night | Who | Asks for | Teaches | Gate |
|---|---|---|---|---|---|
| 0 | W1 · TUE *(1)* | **Ece** — she works here | `neat_pour` of whatever is on the shelf | the beat itself, on a night nothing can be lost — and the arc, in her own words | none |
| 1 | W1 · FRI *(4)* | **The collector** — takes the rent for the building | `neat_pour` — a whiskey, neat | the market: buy a style you lack | shelf (no bourbon in the opening well) |
| 2 | W2 · SAT *(11)* | **The influencer** — four hundred thousand people watch her drink | `moscow_mule` + `vodka_soda`, 90s | the market again, and the book: the reward is a page (`gimlet`) the 2★ gate has not opened | shelf (ginger) |
| 3 | W3 · FRI *(16)* | **The gourmet** — inspects rooms like this, counts the seconds | `manhattan` + `gimlet` + `whiskey_ginger`, 150s, no mistakes | method: stirred is not shaken — and he hands over the page himself | method (+ vermouth) |
| 4 | W3 · SAT *(17)* | **The brewer** — sells the kegs two valleys over | a pint with a proper head | draught: the head band | craft |
| 5 | W4 · FRI *(22)* | **The old regular** — has not missed a weekend yet | "the usual" | reading: the licence, the visits, the person | reading |
| 6 | W4 · SAT *(23)* | **The high roller** — spends what the room is worth | a top-shelf brand, by name | tiers: the well pour will not do | tier |
| 7 | W5 · FRI *(28)* | **Last call** — the drink the room gets known for | everything above, in one glass | the closing beat | all |

Through-line: the room is new, and what it becomes is what these nights make it. Each visitor
leaves it with something — a bottle, a page, a paragraph, a regular — and the last one asks for
the drink the bar has by then become known for. Nobody explains the bar to the player; the bar
is what they have been building.

**The tail has slack, and it is not free.** Eight beats over five weekends leaves a run of
about thirty nights a few spare nights, and every miss costs a whole week. That is the price
the weekend rule charges for its deadline, and it is deliberate — but it means the last beats
can slide off the end of a badly-run bar. The sim's report is where that shows up (S2: the day
each arc stalled on), not a guess.

---

## 12. Out of scope (fenced deliberately)

- Drawn cutscene frames or animated portraits (§7 — the light beat replaces them).
- Branching plot, reputation trees, multiple endings.
- Voice acting, localisation of story text (the string table question is separate).
- Any change to how ordinary customers behave. The crowd is not part of this module.
