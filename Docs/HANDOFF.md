# HANDOFF — picking LAST CALL up on another machine

Written 2026-08-20, at the end of the dead-weight sweep. This is the ONLY file you need
before the project runs on a second computer. It says what git does not carry, what order
to do things in, and which traps this project has already paid for once.

---

## 1 · What the repo does NOT carry

Everything here is deliberately gitignored. The clone will look complete and will not be.

| Missing | What it means | What to do |
|---|---|---|
| **`Tools/pixellab_token.txt`** | The PixelLab Bearer token (36 bytes). Every art generator reads it. Without it `Tools/pixellab.py` fails on auth, not on a clear message. | Copy the file across by hand, or paste a fresh token from the PixelLab account. **It is a secret — it does not belong in a commit.** |
| **`Library/`** (~3 GB) | Unity's import cache. | Do not copy it. Unity rebuilds it on first open — that import is LONG (5,000+ sprites). Start it and leave it alone. |
| `Temp/`, `obj/`, `Logs/`, `UserSettings/` | Per-machine scratch. | Nothing. |
| `Tools/*_raw/`, `Tools/AssetPipeline/staging/` | Raw generator output, ~2 MB, already processed into `Assets/`. Untracked on 2026-08-20. | Nothing. Re-generate if a pipeline is ever re-run. |
| `.agents/` | IDE agent rules for another tool. | Nothing. |

## 2 · Order of operations on the new machine

1. `git clone https://github.com/alperpamukcu/FABLE5.git`
2. Open with **Unity 6000.3.10f1** exactly (`ProjectSettings/ProjectVersion.txt` pins the
   revision). A different patch version will re-serialise assets and dirty the diff.
3. Let the first import finish completely.
4. Drop `Tools/pixellab_token.txt` in, if there is any art work coming.
5. Bring UnityMCP up — §3.
6. Run EditMode. Expect green. Run PlayMode. **Expect `LookTests` to fail** — §4.

## 3 · UnityMCP, the short version

The package is `com.coplaydev.unity-mcp`; Claude Code talks to it over HTTP at
`http://127.0.0.1:8080/mcp`.

- The server is a SEPARATE process, started from Unity: **Window → MCP For Unity →
  Start Server**, then **Connect**. Auto-start is off by default.
- Unity connects OUT to the server. Server up but editor not connected gives every tool
  `{"reason":"no_unity_session"}`.
- If the Claude Code session began while the server was down, the tools are not loaded
  into that session at all. Restart the session after connecting.
- Loop for verifying a change: `refresh_unity` (compile) → `read_console` (errors) →
  `run_tests`.
- `run_tests` returns a JOB ID; wait on it with `get_test_job {job_id, wait_timeout}`.
  There is no `run_tests {action:"status"}`.
- `execute_code` needs `action:"execute"` and the body MUST return a value; string
  constants cannot contain newlines (use `(char)10`).
- **`run_tests` timing out at init almost always means the editor was left in play mode.**
  Stop play and it runs immediately. Second suspect: after an OS sleep the TestRunnerApi
  can wedge — only an editor restart clears that.

## 4 · The trap that WILL bite on a new machine

`LookTests` (PlayMode) compares three screens pixel for pixel against
`Assets/Tests/PlayMode/Baselines~/`. **Those pictures were blessed on the first machine's
GPU.** A different GPU can differ by a pixel and fail all three, and that failure means
nothing about the code.

The fix is deliberate, not automatic:

1. **LastCall → Re-bless UI Baselines**
2. Run the PlayMode suite **twice**. The first run redraws the pictures and *fails on
   purpose* — that is the gate that stops anyone blessing a screen without looking at it.
3. **LOOK at the new pictures** before the second run. On failure the current shot and the
   diff land in `Temp/UiLooks` (**LastCall → Show Last UI Look Failures**).
4. Commit the re-blessed baselines with a message that says which machine drew them.

Two more PlayMode facts the suite already paid for: a killed run leaves a virtual mouse as
the editor's only pointer (**LastCall → Clear Ghost Input**, or restart), and a wedged job
blocks every later `run_tests` with `tests_running` (`TestJobManager.ClearStuckJob()` via
`execute_code`).

## 5 · Read these, in this order — and stop

The docs are large. To get current without reading all of them:

1. **`CLAUDE.md`** (repo root) — the architecture, the hard rules, how to verify.
2. **`Docs/GDD_MEVCUT.md`** — the as-built rulebook. It wins over every other doc.
3. **`Docs/PLAN_service_depth.md`** — the live staging document and conflict ledger.

`Docs/GDD/` holds the design specs; open a module only when you are working on it. Modules
21 (pour), 23/24 (loop and service), 26 (the last customer), 16 (UI style) are the ones in
play. `Docs/GDD/_CHANGELOG.md` is where each ruling and its reversal is dated — read the
top entry, not the file.

## 6 · Where the work actually stood on 2026-08-20

**Just shipped (commit `d147d117`, the author's own hand):** the market storefront in the
desktop's dialect (`ChromeArt.Win98Key`/`Isle`, the 26-band `ViceFade`, `Win98Press`), the
hour/week instrument panel (`ChromeArt.Well` + a rewritten `SegmentClock`), fixtures with
unlock conditions, and the room keeping time (`window_cycle`, `star3d`). None of it is
finished work — it is where the pen was put down.

**DISABLE DOMAIN RELOAD BREAKS THE PLAYMODE SUITE — found and fixed 2026-08-20.** For a
few hours the suite was red and non-deterministically so: a different test failed each run,
always with the same shape — *the pointer never reached* the seat, or six presses of
"MENU — MAKE A DRINK" never opened `MenuPanel`. It was never a code bug.

`ProjectSettings/EditorSettings.asset` had picked up `m_EnterPlayModeOptions: 1`
(**Disable Domain Reload**) in `d147d117`. With domain reload off, statics are NOT reset
between play sessions, so eight tests that each enter play mode inherit the previous one's
leftovers — and `InputTestFixture`'s virtual mouse is exactly the kind of state that does
not survive that. The proof is clean: setting is on → red, non-deterministic; setting off →
**8/8 green, including all three pixel-compared screens**. Nothing else changed.

Two facts worth keeping:

- **That setting is TRACKED**, so it travels with the clone. If the suite on the new machine
  is red with "pointer never reached" anywhere in the message, check this FIRST — before
  reading a line of UI code.
- The setting exists for a real reason: with it on, play mode starts almost instantly, which
  is a genuine win when hand-testing. The trade is that the test floor stops working. If you
  want it back, turn it on for hand-play and **off before running the suite** — or do the
  proper fix and reset the UI layer's statics from
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`, which
  is a real job across the sprite caches.

Diagnostics that were run before the cause was found, kept because they rule things out:
nothing covers the menu button (an `EventSystem.RaycastAll` at its centre returns it alone),
and `btn.onClick.Invoke()` opens the panel in the same frame — the wiring was always sound.

**A second dead-but-suspicious one:** `TycoonHud.RefundArt` has no caller. It builds the
picture for a refund row, so this reads more like a wire that was never connected than like
something retired — worth a look before deleting it.

**Closed since:** `ItemArt.Glass` and its one call site are gone (the author: "glass.png çok
eski sürüme ait artık kullanılmıyor"). It was a pre-v3 leftover, and the fallback it fed —
the dirty glass on the counter — sat directly under the rule that forbids it ("the empty on
the counter is the drawn vessel the drink was served in, not a stock photo of some other
glass"). An unknown line now leaves the prop undrawn, which the colour it already carries
was written for.

**Left alone on purpose by the sweep** (dead today, but the author is mid-build):
`TycoonRun.CanUnlock` — the unlock feature grew a `Kept` condition the same day;
`StoryArc.BeatNamed` — PLAN_last_call S4/S5 are not built yet;
`BackBarArt.KegCrown` — kept under its own written ruling, hand-drawn art rather than logic.

**Four faces the story names but the game cannot draw:** `story.json` gives its guest `ece`
the placeholder `glam`, and `execman` / `profess` / `teal` are written as looks. None of
them are in `PatronCast`, so `LookNamed` never found them even before the sweep deleted the
old-rig art. Whichever of them the story needs must be REDRAWN to the 2026-08-19 rig and
added to the cast — that was already the plan for all 33.

**Not started, documented:** PLAN Faz C, the same-seed economy sweep at several hand levels,
to tune `PerfectWindow` and the pay curve. Best run after playing enough to feel the reveal
pacing.
