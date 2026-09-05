# LAST CALL — GDD Module 28: The Door (age, papers, the kick)

**Status:** built — Core (H2b), the card (H3) and the altered card (H6) shipped 2026-09-05; as-built in GDD_MEVCUT §9.24. Written 2026-09-04 from the author's
brief: *"20 yaş altı kişiler alkol alamayacak, oyuna 20 yaş altı müşteriler eklenecek. 20 yaş altı
birisi geldiğinde kimliğinden kontrol ederken kimliğin üstündeki butondan 'kick'leyebileceksin,
aynı zamanda sahte kimlik de işin içerisine eklenecek. Sahte kimlikli birisine alkol vermenin büyük
para cezası olacak; oyuncu başta yaptığı hatalardan çok ağır cezalandırılmaması için oyundaki
gelişmişlik seviyesine göre ceza belirlenecek. Doğru şekilde kovması ise gün sonunda küçük bonus
paralar verecek — hükümetin takdir etmesi mantığında bir içki ücreti ödeyecek, örneğin 10 dolar."*
Reviewed the same night by five adversarial passes against the working tree; what they found is
folded in and the reversed calls are listed in §10. Staging lives in `Docs/PLAN_house_and_law.md`.
Module 27 is the other half of the same brief (the room). Where a line here disagrees with
`Docs/GDD_MEVCUT.md` after a phase ships, GDD_MEVCUT wins.

---

## 1. Why this exists

The ID card is the game's hidden-information mechanic: the order lives behind it, and reading it is
taking the order (GDD 23 §3, CLAUDE.md hard rule). But nothing on the card has ever been a
*question*. It prints a name, an age, a country, a flag and a photo, and the only thing the player
does with it is close it. The author's ruling gives the card its second job: **some of the people
at the bar should not be served, and the card is how you know.**

- Some customers are **under twenty**. Some of them say so on the card. Some carry a **borrowed
  card** — somebody else's licence, somebody else's face on it.
- Serving alcohol to any of them costs a **fine**, sized to how far the bar has come, so the first
  week's mistakes are cheap and a four-star bar's are not.
- Showing the right person the door earns the **state's thanks**: a small line on the slip, the
  price of a well drink, for every face shown it.
- Showing the wrong person the door is a walk-out — a review of zero, a regular who remembers.

## 2. The law

- **Drinking age is 20** (`IdPapers.DrinkingAge`). Nineteen is a minor; twenty is served. (The
  registry rolls the crowd at 21–67 and papers.json carries one twenty-year-old — both stay legal.)
- Every recipe in the book is alcoholic. There is no soft order to give a minor instead; the
  right answer to a minor is the door. (A non-alcoholic page is fenced, §11.)
- **A minor is a PERSON, not a visit.** Their papers ride on the person (`RegularState`); a minor
  who comes back — a minor who was SERVED and got away with it — is still a minor, with the same
  card. A minor who was shown the door does not come back at all (§4).
- The guest of the house (GDD 26 §3) is never a minor and cannot be kicked — outside the books,
  outside the law.

### 2.1 Three kinds of paper

| Kind | What the card shows | The tell | Serve them → | Kick them → |
|---|---|---|---|---|
| **Honest adult** (the crowd today) | their own papers | none | as today | WRONG kick: a zero review, they walk |
| **Honest minor** | their own papers, age **18 or 19** | the number | fine | RIGHT kick: thanks |
| **Borrowed card** | somebody else's papers: another face's photo, name, age (21+), country, flag | **the photo is not the person on the stool** | fine | RIGHT kick: thanks |
| **Altered card** *(shipped 2026-09-05, H6)* | their own photo and name, age bumped to 21–24 | **the flag does not match the country** | fine | RIGHT kick: thanks |

The borrowed card is the first forgery to ship: its tell is the strongest thing on the card (the
portrait is 144 px and the person is sitting right there), it needs no new art, and it is the
forgery a bar actually sees. The altered card is specified so the second forgery does not have to
be designed twice; it ships when the first one has been played.

## 3. Papers are hidden information

- **Core owns the truth, and only Core can see it.** `IdPapers { TrueAge, PrintedAge, Forgery,
  LooksYoung }` hangs off the person as `RegularState.Papers` — an INTERNAL getter, exactly like
  the order's `OrderTruth`. The one public door is `CustomerVisit.Papers`, which **throws until
  `InspectId()`** (the C3 pattern). The review found the first draft had left the getter public,
  reachable through `visit.Regular` before any card was read; that is the hole CLAUDE.md says has
  opened twice, and it is closed at the language level (no `InternalsVisibleTo` exists, so the UI
  assembly cannot compile a read).
- **Everyone carries papers.** An honest adult's card says what the registry rolled and nothing
  else (`IsHonestAdult`); "null" means only "not asked yet". Rolled ONCE per person, at their
  first arrival (`TycoonRun.NextArrival`, when `regular.PapersRolled` is false), on a **new named
  stream, `"papers"`**. No existing stream draws one more number, so every seed's crowd, orders
  and patience stay what they were UNTIL the first minor is met — from there the night diverges
  (a kick frees a stool early, a fined minor asks for no round), which is what the A/B measures.
- **One fact is not hidden: `LooksYoung`.** You can see the person. Every minor looks young, and
  so does a share of honest adults (`YoungAdultShare` 0.25, rolled on the same stream), so a young
  face is a reason to read the card carefully and never the verdict. This is what lets the room
  pick a face BEFORE the card is read (§3.1) without the face becoming the tell.
- **Opt-in with the regulars**: a run built without `archetypes` has no people, so it has no
  papers and no minors — every bench setup and older test is untouched. The sim and the real
  bootstrap both build with regulars, so both meet the door.
- Who is under twenty among the NEW people who walk in, by day: `MinorChance(day) = day < 2 ? 0 :
  min(0.12, 0.03 + 0.01 × day)` — nobody on opening night, one in twenty on the second, one in
  eight from the ninth. Returns run at 55% from the second night and a bounced minor never
  returns, so the share of SEATS that are minors is roughly half of that. Of minors, `ForgedShare
  = 0.5` carry a borrowed card; the rest print their own age. Starting stakes (§9).

### 3.1 Faces and papers agree (the HUD's side of the contract)

The card prints the FACE's papers (`papers.json`, per look), and the face is chosen by the HUD
when the customer sits, before any card is read. So:

- `papers.json` rows gain `"young": true` on the looks that can pass for nineteen. A visit whose
  `LooksYoung` is true draws its face from that pool (the same longest-unseen, no-doubling rules);
  adults who look young draw from it too, which is the point.
- An **honest minor** prints their face's papers with the AGE replaced by `PrintedAge` (18–19).
- A **borrowed card** prints ANOTHER look's papers — photo, name, age, country, flag. The lender is
  booked PER PERSON, once, when the person's own face is booked (`_lenderOfPerson` beside
  `_faceOfPerson`), hashed from the person's id the way the licence number already is — no stream
  is touched, and a returning minor shows the same stranger's card. Excluded: the person's own
  look, looks with no papers, and looks seated on another stool at that moment.
- **The ticket over the head and the log print the CARD's name once it is read** — for a borrowed
  card, the lender's — so the name is not a second, free tell (the card/ticket disagreement of
  2026-08-11 would otherwise return).
- The fine and the thanks are decided on the TRUTH, never on what was printed, so nothing the HUD
  draws can change a verdict.

## 4. The kick

`TycoonRun.Kick(visit)` — the door's only verb.

- **Guards:** day open; the visit is seated and waiting; **`IdInspected`** (hidden information: you
  read the card, then you decide); **not yet served a round** (`Paid == 0` — *"you cannot show the
  door to someone you have already served; the card was your moment"*; a customer on an extra
  round is always an adult, minors get no round); not the guest of the house (throws — module 26
  owns them).
- **Effect:** the visit ends in a new state, `VisitState.Kicked`. They leave nothing on the
  counter (`DrinkServed` is false — module 27 §4.1). The stool is free at once.
- **Right kick** (the truth was a minor or a forgery): the visit is marked `OffTheBooks` and
  `BarDay.FinishedCounted()` / `AverageSatisfaction` — the ONE place that decides who counts —
  skip it exactly as they skip the guest: no review, no seat in the night's mean, not SERVED and
  not WALKED on the slip. The person is `Barred`: the registry's return roll passes over them
  (the draw is spent either way, so the stream does not learn who was barred; a stranger walks in
  instead). `RightKicks++` for the night.
- **Wrong kick** (an honest adult): the visit counts, at satisfaction **0** — it is a walk-out
  (they were refused a drink they were entitled to): the regular records the visit at 0 and the
  slip counts them as WALKED (the people count reads `StormedOff || Kicked && !OffTheBooks`).
  There is no fine for it — the cost is the review and the lost tab. `WrongKicks++`.
- **The button lives ON the card** (*"kimliğin üstündeki butondan"*): a KICK key in the licence's
  header band, right of `PATRON LICENCE · CLASS B` and left of the flag (≈ 160 × 30, `KeyPlate`
  language, ViceRed). Not on the scrim — the scrim's one meaning is *close*, and the review's
  first draft would have put the irreversible verb where the habitual close-click lands. Hidden
  (not merely disabled) for the guest of the house. Sequence: read `_idVisit` into a local, call
  the verb, `CloseId`, then the toast — the card auto-closes when a visit stops waiting, so the
  local is what survives.

## 5. Serving a minor

The serve goes through. They pay and they tip like anyone else, and the fine lands right after:

- `Fine = FineBase + FinePerStar × floor(Rating.Average)` — **$20 + $20 per whole star of the
  standing** (`IdPapers.FineFor`): $20 on a no-name bar, $60 at two stars, $100 at four. The
  standing is the game's own measure of *"gelişmişlik"*; a bar that has climbed is held to more.
- **Charged when they get up, after the tab** — in `SettleDepartures`, right after `Money +=
  visit.Paid`, settled once under `TabSettled` (the visit carries `FineOwed`). `ServeTo` moves no
  money today and still moves none; *pays first, then is fined* is true to the second, and the
  bin fee's till clamp is never fooled by a mid-night dip. `DayFines += fine`; the till MAY go
  below zero (a fine is a debt that happens to you, like rent — GDD 23 §6's one landlord gains a
  second) and it counts toward the three red strikes like anything else in the red.
- **Once per VISIT** (`visit.Fined`). A person can sit twice in a night (the return roll has no
  seated check), and each sitting is its own offence.
- A fined minor asks for no extra round: `ServeTo` passes no next order AND re-issues the verdict
  with `OrdersAgain = false`, so the HUD cannot announce a round Core refused. They leave after
  the one drink.
- `MinorsServed++` for the night. The FINES row's label says why (§7).

## 6. The state's thanks

`KickBonus = 5` per right kick — *"bir içki ücreti"* taken literally: the starter menu prices a
well drink at $4–5, and the 200-run floor bot nets about $8 on a served customer, so the author's
example of $10 was more than a drink and, at one minor a night by week two, more than the bot's
whole nightly margin. Five is one drink. With the bounced never returning, it is paid once per
face and the dice cannot farm it.

Paid **when the floor closes, with the rent** (`Floor.IsComplete` in `Tick`), never in
`ContinueToNextDay` — the slip prints TILL and NET from the till as it stands, and a bonus paid
after the slip is drawn would make the paper wrong by $5 × n. Paid once: the same block flips the
phase to DayEnd and `EnsurePhase` refuses further ticks; `DevJumpToNight` plays no night and pays
nothing. It is INCOME (`DayIncome = Sales + Tips + Bonus`), printed as its own row only when
non-zero.

## 7. What it does to the books

| Line | Where | When it prints |
|---|---|---|
| `FINES · under age` / `FINES · borrowed card` | expense, red, `DayFines` (the label carries the reason; BillRow's label column has the width, the people line does not) | only when > 0 (GDD_MEVCUT §9.4: routine zeros were cut) |
| `STATE'S THANKS · 2 SHOWN THE DOOR` | income, `DayBonus` | only when > 0 |
| `DayDetail`/`DayResult` | `Fines`, `Bonus`, `RightKicks`, `WrongKicks`, `MinorsServed` | always carried, so the week board, the register's book and the sim read one record |

- The slip's people line and its two critics read the FILED record, not the floor: a right kick is
  neither SERVED nor WALKED and is skipped by the critic search (it does not rate the bar); a wrong
  kick is WALKED and reaches the low critic by its 0 like any walk-out. Today's `served++` else
  branch (anything not StormedOff) is the line that changes.
- **Every night counter is zeroed at BOTH reset points** — `ContinueToNextDay` and `DevJumpToNight`:
  `DayFines`, `DayBonus`, `RightKicks`, `WrongKicks`, `MinorsServed` (`Housekeeping` is reset by
  the new `BarDay`). The register's book line gains `· fines $n` / `· thanks $n`.
- `DayLedger.CloseDay` refuses negative income or expenses; fines ride expenses and the bonus rides
  income, both ≥ 0, so nothing there moves.

## 8. Where it shows (module 24 owns the presentation; the beats are named here)

- **The card:** PHOTO (144 px, the look's own face — or the lender's), NAME, AGE, CITIZEN OF, the
  flag — unchanged fields, now sometimes lying. The KICK key in the header band (§4).
- **The stool:** on a kick, no reaction motes, no cheer; the log line reads `SHOWN THE DOOR ·
  under age` / `· borrowed card` / `· they were of age` in the house's inks; the customer walks
  out on the exit walk the storm-off uses.
- **The slip:** the FINES and STATE'S THANKS rows (§7); two new 16×16 marks in `ChromeArt.Masks`
  (`fine`, `thanks`) so the rows carry a gutter like every other line.
- **The sim report:** minors met (from `DayDetail`, since `FinishedCounted` no longer holds
  them), right kicks, wrong kicks, minors served, fines paid (and fines by whole star of the
  standing, so *"money per night at each standing"* is a row), thanks earned.

## 9. Numbers (balance v0 — starting stakes)

| Constant | Value | Where (as built) |
|---|---|---|
| `DrinkingAge` | 20 | `IdPapers` |
| `MinorChance(day)` | `day < 2 ? 0 : min(0.12, 0.03 + 0.01 × day)` — among NEW people | `IdPapers` |
| `ForgedShare` | 0.5 | `IdPapers` |
| `YoungAdultShare` | 0.25 | `IdPapers` |
| honest minor `PrintedAge` | 18–19 (`"papers"` stream) | `IdPapers.Roll` |
| `FineBase` / `FinePerStar` | $20 / $20 per whole star | `IdPapers.FineFor` |
| `KickBonus` | $5 | `IdPapers` |

**What the sim must show:** the floor bot reads the card perfectly and kicks every minor — fines
0, thanks ≈ $5 × minors met; the shape (bankruptcies, median till) should move only by the seats
minors took, and thanks must stay under a quarter of the night's net. `Hands.MisreadId` — a
chance drawn once per visit at the bot's InspectId pass from a bot-only named stream
(`rng.GetStream("door")`, the same free-stream rule the hands use; NOT a slot in the twelve-slot
hand, which is dealt once per drink after the build gate) — makes a bot that misses a share of
forgeries, so the fine curve is measured as money per night at each standing before anyone plays it.

## 10. Decisions (flagged, reversible)

- **D1 — one tell first (the photo).** The borrowed card ships alone; the altered card (flag) is
  designed here and built second. One strong tell the player learns, then a second that makes
  the first not enough.
- **D2 — the fine scales with the STANDING**, not the day. Days pass whether or not the bar grew;
  the standing is what the author means by *gelişmişlik*, and it is already the game's spine.
- **D3 — the fine may go below zero.** The loop's one written debt rule (GDD 23 §6: only rent)
  gains a second landlord. Documented in GDD_MEVCUT when it ships.
- **D4 — a wrong kick is a walk-out, not a fine.** The author priced the mistake in the other
  direction only; a refused adult already costs the tab, the tip and a zero review.
- **D5 — papers are per PERSON, and the bounced do not return.** Reversed in part from the first
  draft ("a returning minor is the same minor; the memory is the skill"): with returns at 55% the
  same face would have been a free bonus every time the dice sent it back. A minor who was
  SERVED does return, with the same card — that memory is still the skill.
- **D6 — the served minor pays first, then is fined — when they get up.** The bar took the money;
  the law takes more. (Voiding the sale would hide the fine inside a missing tab.)
- **D7 — no blind kick.** Core throws before `InspectId`; the card is the mechanic.
- **D8 — the thanks is five dollars, not ten.** The author's number was an example ("örneğin");
  the review priced it against the till and it was more than a served customer nets.
- **D9 — the young face is shared with adults** (`YoungAdultShare`), so the room can choose a
  face before the card is read without the face becoming the verdict.
- **D10 — a right kick is off the books, decided in `BarDay`**, the one gate that already
  answers "does this person count", never in `ContinueToNextDay`.

## 11. Out of scope (fenced deliberately)

- A non-alcoholic page in the book (a soft order for a minor). Their answer is the door.
- New drawn faces for minors: the young pool is chosen from the rig the bar has; new characters
  go through the 2026-08-19 rig and the author's HTML review, not through this module.
- A bouncer, an inspector visit, a licence to lose. The fine is the whole of the law.
- Kicking a drunk, a fight, a ban list beyond "the bounced do not return".
