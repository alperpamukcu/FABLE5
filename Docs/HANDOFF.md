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

**THE PLAYMODE SUITE GOES INTERMITTENTLY RED, AND THE CAUSE IS GHOST INPUT.** Read this
before you spend an afternoon on it, as this session did — twice, down two wrong paths.

**The symptom** is a different test failing each run with the same shape: *the pointer never
reached the seat*, or *pressing "MENU — MAKE A DRINK" 6 times never opened MenuPanel*. The
moving target is what makes it look like a code bug. It is not one.

**The tell is in the console**, not in the test output:

```
ArgumentException: State format KEYS from event does not match state format MOUS of device Mouse:/Mouse
ArgumentException during event processing of Editor update; resetting event buffer
```

A keyboard event is being delivered to the virtual mouse and the Input System resets its
event buffer mid-test, so presses land nowhere. `InputTestFixture` leaves this behind, and
it ACCUMULATES across repeated PlayMode runs — which is exactly what a session that runs
the suite over and over does.

**The fix is the project's own menu item**: **LastCall → Clear Ghost Input (after a killed
PlayMode run)** — note the full title; `ExecuteMenuItem("LastCall/Clear Ghost Input")` on
its own returns false and does nothing. Clear it, then run. Two clean 8/8 runs followed it
here. If the pointer is still dead, restart the editor.

**A wedged job looks similar and is not the same thing.** `get_test_job` sitting at
`running 0/None` while the editor reports no tests running is the job tracker stuck, not the
input. `TestJobManager.ClearStuckJob()` via `execute_code` frees it — the type is at
`MCPForUnity.Editor.Services.TestJobManager` and the method is static.

**TWO CORRECTIONS TO WHAT THIS FILE SAID EARLIER**, both worth reading because both cost
real time:

1. **Disable Domain Reload was blamed and is not the cause.** Turning it off gave three
   green runs in a row and that was taken as proof; the suite went red again later with the
   setting confirmed OFF, and red at a commit with the whole session's work stashed. It is
   still worth leaving off — statics not resetting between play sessions is a real hazard for
   a suite that enters play eight times — but it does not explain these failures and fixing
   it will not stop them.
2. **`ProjectSettings/EditorSettings.asset` fights being changed.** Setting
   `EditorSettings.enterPlayModeOptionsEnabled = false` updates the live editor but does NOT
   reach the file, even through `SaveAssets`, `File/Save Project` or a `SerializedObject`
   write — and Unity rewrites the file back to the old value after play. It has to be edited
   in the Editor UI (Edit → Project Settings → Editor → Enter Play Mode Settings) to stick.

**A test-side fix went in with this**: both suites' `OpenTheBar()` now waits for
`Phase == DayOpen`, not just for the curtain to lift. Every door in the flow guards on that
phase and refuses WITHOUT A SOUND, so a press one phase early was being swallowed and
reported as "the button never opened the panel". That was a real hole even if it was not
the whole story.

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
