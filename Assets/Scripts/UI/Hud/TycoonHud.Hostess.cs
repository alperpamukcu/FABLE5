using System;
using System.Collections.Generic;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;

namespace LastCall.UI
{
    // TycoonHud, part Hostess: she walks in at the close with a job (2026-09-28, spec §C).
    //
    // The author, 2026-09-27: the hostess is the game's lead and comes by only for the jobs and the teaching; at the
    // close of the night after a job is done she comes in "between closing time and the last customer leaving", says
    // her piece on the same plate everybody talks on, hands over the next one and goes. While she talks the night
    // stands still. Her story scenes are carried by her ANIMATIONS - her own clips are being drawn - so the talk beat
    // below is a hook: a clip a line, played once, then back to standing.
    //
    // Core decides everything (TycoonRun.Quests: whether she comes, what she brings, when it is heard); this file is
    // the scene. The one signal is run.HostessVisit, and the one verb it calls is run.HearHostess(), once, on the key
    // under her last line.
    public sealed partial class TycoonHud
    {
        /// <summary>Off, walking in (and the arrive beat), talking on the plate, and leaving (the leave beat and the
        /// walk back out). Only Talk puts anything on the plate.</summary>
        private enum HostessBeat { Off, Enter, Talk, Leave }

        private HostessBeat _hostessBeat = HostessBeat.Off;

        /// <summary>She is in the room - walking, talking or leaving. The talk hook (TycoonHud.Update) holds the
        /// night for as long as this is true, so nobody walks in, waits or leaves around her.</summary>
        private bool _hostessOnStage;

        /// <summary>Where she is along the counter, in HUD units - the closing beat's lamp follows it.</summary>
        private float _hostessX;

        /// <summary>Her figure: a stool's view with no stool, so she is drawn, lit and cropped by the bar exactly as
        /// every drinker is (SyncPatronBody) - a world sprite, not an overlay Image floating over the counter.</summary>
        private SeatView _hostessView;

        private float _hostessMarkX;
        private float _hostessClock;                          // the walk's clock, and the leave's
        private float _hostessBeatLeft, _hostessBeatSeconds;  // the one-shot playing now (arrive or leave)
        private Sprite[] _hostessTalk;                        // the talk clip for the line on the plate, or null
        private float _hostessTalkT;
        private int _hostessSaidAt = -1;                      // the plate line the talk clip was started for

        /// <summary>Her talk clips by look and folder, the empty answer kept too (a look without them is asked once).</summary>
        private readonly Dictionary<string, Sprite[]> _hostessClips = new Dictionary<string, Sprite[]>();

        /// <summary>The scene's clock: stopped under the pause menu, and at the ceremonies' pace (1 in the game; the
        /// PlayMode suite runs it at 8). Unscaled, because the room's own clock is held at 0 while she talks.</summary>
        private static float HostessDelta(bool paused) =>
            paused ? 0f : Time.unscaledDeltaTime * Ceremony.Pace;

        /// <summary>
        /// Runs the scene off the run, once a frame, right after SyncLastCall (which puts her lines on the plate).
        /// </summary>
        private void SyncHostess(TycoonRun run)
        {
            // Nothing of her survives the night: a closed night, a new run or a dev verb sends her off at once.
            if (run.Phase != TycoonPhase.DayOpen)
            {
                if (_hostessBeat != HostessBeat.Off) HostessOff();
                return;
            }
            var visit = run.HostessVisit;
            float dt = HostessDelta(Paused);

            switch (_hostessBeat)
            {
                case HostessBeat.Off:
                    if (visit != null && !MenuUp) HostessEnter(run, visit);
                    break;

                case HostessBeat.Enter:
                    // HEARD WITHOUT HER (Core's backstop, a dev verb): whatever she came to say is on the bar already.
                    if (visit == null) { HostessBeginLeave(); break; }
                    StepHostessEnter(dt);
                    break;

                case HostessBeat.Talk:
                    if (visit == null) { HostessBeginLeave(); break; }
                    StepHostessTalk(dt);
                    break;

                case HostessBeat.Leave:
                    StepHostessLeave(dt);
                    break;
            }
        }

        /// <summary>
        /// She comes in. The counter is cleared for her the way a person walking up to the bar clears it - the
        /// licence put down, the bench shut, the book closed, the cellar rolled back - and none of it is lost: the
        /// drink stays in Core's tin and glass. The pause, the settings and the menu are left alone.
        /// </summary>
        private void HostessEnter(TycoonRun run, HostessVisit visit)
        {
            // NOBODY TO SAY IT (a story with no host, or nothing in her book to say): the moment is spent silently,
            // as SyncLesson spends a lesson - the job still goes on the bar.
            var host = Hostess;
            if (host == null || HostessLines(run, visit, host).Count == 0)
            {
                run.HearHostess();
                return;
            }

            ClearTheCounterForHer();
            _hostessOnStage = true;
            _hostessSaidAt = -1;
            _hostessTalk = null;
            EnsureHostessView();
            _hostessView.Look = LookForStory(host);
            _hostessMarkX = HostessMark(run);
            _hostessView.SeatX = _hostessMarkX;
            _hostessView.Exiting = false;
            _hostessView.Root.gameObject.SetActive(true);
            if (_hostessView.Body != null) _hostessView.Body.flipX = false;

            // No figure to draw (her look and its stand-in both missing) or less movement asked for: she is simply
            // there, at her mark, and the plate is up.
            if (_hostessView.Look == null || Motion.Reduced)
            {
                _hostessX = _hostessMarkX;
                _hostessView.WalkT = 1f;
                HostessTalkBegins();
                return;
            }
            // In from the room's right edge at the drinkers' own pace, the walk's clock phased so the step that
            // lands on her mark is the cycle's first frame - the pose ARRIVE was drawn from (SeatWalkClock).
            _hostessX = _hudRoot.rect.width + OffscreenMargin;
            _hostessView.WalkT = 0f;
            float travel = Mathf.Max(1f, _hostessX - _hostessMarkX) / WalkSpeed;
            _hostessClock = WalkCycleSeconds - Mathf.Repeat(travel, WalkCycleSeconds);
            _hostessBeatLeft = _hostessBeatSeconds = 0f;
            _hostessBeat = HostessBeat.Enter;
            DrawHostess(PatronClip.Walk, _hostessClock, 1);
        }

        private void StepHostessEnter(float dt)
        {
            if (_hostessBeatSeconds <= 0f)
            {
                // the walk to her mark
                _hostessX = Mathf.MoveTowards(_hostessX, _hostessMarkX, dt * WalkSpeed);
                _hostessClock += dt;
                if (_hostessX > _hostessMarkX + 0.01f) { DrawHostess(PatronClip.Walk, _hostessClock, 1); return; }
                _hostessView.WalkT = 1f;
                _hostessBeatSeconds = _hostessBeatLeft = Mathf.Max(0.0001f, OneShotSeconds(_hostessView, PatronClip.Arrive));
            }
            // ...then she stops and turns to the room (ARRIVE), and the plate comes up
            _hostessBeatLeft -= dt;
            if (_hostessBeatLeft > 0f) { DrawHostess(PatronClip.Arrive, _hostessBeatSeconds - _hostessBeatLeft, 1); return; }
            HostessTalkBegins();
        }

        private void HostessTalkBegins()
        {
            _hostessBeat = HostessBeat.Talk;
            _hostessBeatLeft = _hostessBeatSeconds = 0f;
            DrawHostess(PatronClip.Idle, 0f, 1);
        }

        /// <summary>
        /// THE TALK HOOK. Every time a line comes up on the plate she plays a clip for it, once, and comes back to
        /// standing - her own TALK clips in turn when they ship (talk_1..talk_3, or one "talk"), and until then the
        /// order beat every drinker speaks with. Her story scenes are acted by these; nothing else has to change
        /// when they land.
        /// </summary>
        private void StepHostessTalk(float dt)
        {
            if (_plateAt != _hostessSaidAt && _plateAt < _plateScript.Count)
            {
                _hostessSaidAt = _plateAt;
                _hostessTalk = HostessTalkClip(_plateAt);
                _hostessTalkT = 0f;
            }
            if (_hostessTalk != null && _hostessTalk.Length > 0 && _hostessTalkT < _hostessTalk.Length / PatronFps)
            {
                DrawHostessFrames(_hostessTalk, _hostessTalkT, 1);
                _hostessTalkT += dt;
                return;
            }
            DrawHostess(PatronClip.Idle, 0f, 1);
        }

        private Sprite[] HostessTalkClip(int line)
        {
            var look = _hostessView?.Look;
            if (look == null) return null;
            var frames = HostessClip(look.Slug, "talk_" + (line % 3 + 1));
            if (frames.Length == 0) frames = HostessClip(look.Slug, "talk");
            if (frames.Length == 0) frames = look.ClipFor(PatronClip.Order);   // the stand-in's own speaking beat
            return frames;
        }

        private Sprite[] HostessClip(string slug, string folder)
        {
            string key = slug + "/" + folder;
            if (!_hostessClips.TryGetValue(key, out var frames))
                _hostessClips[key] = frames = LoadPatronClip(slug, folder);
            return frames;
        }

        /// <summary>She has been heard - or the visit went without her. The plate is already down; she turns to
        /// the door (LEAVE) and walks out the way she came, and the night is held until she is through it.</summary>
        private void HostessBeginLeave()
        {
            if (Motion.Reduced || _hostessView == null || _hostessView.Look == null) { HostessOff(); return; }
            _hostessBeat = HostessBeat.Leave;
            _hostessTalk = null;
            _hostessView.Exiting = true;
            _hostessBeatSeconds = _hostessBeatLeft = OneShotSeconds(_hostessView, PatronClip.Leave);
            _hostessClock = 0f;
        }

        private void StepHostessLeave(float dt)
        {
            if (_hostessBeatLeft > 0f)
            {
                _hostessBeatLeft -= dt;
                DrawHostess(PatronClip.Leave, _hostessBeatSeconds - _hostessBeatLeft, 1);
                return;
            }
            float exitX = _hudRoot.rect.width + OffscreenMargin;
            _hostessX = Mathf.MoveTowards(_hostessX, exitX, dt * WalkSpeed);
            _hostessClock += dt;
            if (_hostessX >= exitX - 0.01f) { HostessOff(); return; }
            DrawHostess(PatronClip.Walk, _hostessClock, -1);   // the entrance mirrored, to the right
        }

        /// <summary>Gone: the figure hidden, the lamp back to the guest-only rule, and the hold let go.</summary>
        private void HostessOff()
        {
            _hostessBeat = HostessBeat.Off;
            _hostessOnStage = false;
            _hostessTalk = null;
            _hostessSaidAt = -1;
            if (_hostessView == null) return;
            _hostessView.Exiting = false;
            if (_hostessView.Body != null)
            {
                _hostessView.Body.flipX = false;
                _hostessView.Body.gameObject.SetActive(false);
            }
            if (_hostessView.Root != null) _hostessView.Root.gameObject.SetActive(false);
        }

        /// <summary>The key under her last line (OnPlateKey): Core is told she was heard - the job is on the bar from
        /// this instant - and she goes.</summary>
        private void HostessHeard(TycoonRun run)
        {
            if (_hostessBeat != HostessBeat.Talk) return;
            run.HearHostess();
            HostessBeginLeave();
        }

        // ── her figure ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A hidden mark on the HUD at the stools' own line, and a world sprite that follows it - built the
        /// first time she comes in. "Hostess" is the name the PlayMode suite looks for.</summary>
        private void EnsureHostessView()
        {
            if (_hostessView != null) return;
            var mark = NewRect("HostessMark", _hudRoot);
            mark.anchorMin = mark.anchorMax = new Vector2(0, 0);
            mark.pivot = new Vector2(0.5f, 0);
            mark.sizeDelta = new Vector2(150f, CharWinH);
            _hostessView = new SeatView { Root = mark, Index = -1 };
            var stageForBody = stage != null ? stage : FindFirstObjectByType<DiegeticStage>();
            if (stageForBody != null)
            {
                // A drinker's order and shadow: over the room, under the counter, which takes her legs.
                _hostessView.Body = stageForBody.NewStageSprite("Hostess", 25, castsShadow: true);
                _hostessView.Body.gameObject.SetActive(false);
            }
        }

        private void DrawHostess(PatronClip clip, float t, int facing)
        {
            if (_hostessView == null) return;
            _hostessView.Root.anchoredPosition = new Vector2(_hostessX, SeatLineY);   // she rides the cellar's lift too
            if (_hostessView.Look == null) return;
            UpdatePatronFrame(_hostessView, clip, t, facing);
        }

        private void DrawHostessFrames(Sprite[] frames, float t, int facing)
        {
            if (_hostessView == null || _hostessView.Body == null) return;
            _hostessView.Root.anchoredPosition = new Vector2(_hostessX, SeatLineY);
            _hostessView.Body.sprite = frames[PatronFrameIndex(PatronClip.Order, t, frames.Length)];
            _hostessView.Body.flipX = facing < 0;
            SyncPatronBody(_hostessView);
        }

        /// <summary>
        /// HER MARK: the stool nearest the door that the bar does not own - so she never stands where somebody is
        /// sitting. The bar fills from the middle out (SeatFillOrder), so that is an END stool; with all six owned,
        /// she stands just inside the door, a stool's pitch past the last one - or, where the field is too narrow for
        /// that, half a pitch in from its edge, the room a drinker takes either side of a stool (2026-09-28, the
        /// review: at the edge less 150 she stood 112 from the sixth stool, both bodies at order 25). At 1280 that
        /// is x 1190, 172 from the sixth stool: the placeholder's widest standing frame (the order beat she talks
        /// with, 88 units either side of her mark) ends at 1278 and starts about where the sixth drinker's
        /// gesture ends. To be measured in play once her own art lands.
        /// </summary>
        private float HostessMark(TycoonRun run)
        {
            var order = SeatOrderFor(run);
            int owned = Math.Min(run.Seats, order.Length);
            for (int i = _seats.Count - 1; i >= 0; i--)
            {
                bool mine = false;
                for (int k = 0; k < owned; k++) if (order[k] == i) { mine = true; break; }
                if (!mine) return _seats[i].SeatX;
            }
            float past = _seats.Count > 0 ? _seats[_seats.Count - 1].SeatX + SeatGap : _hudRoot.rect.width * 0.5f;
            return Mathf.Min(past, _hudRoot.rect.width - SeatGap * 0.5f);
        }

        /// <summary>Everything that stands between her and the bar goes, as a hand clearing a counter would.</summary>
        private void ClearTheCounterForHer()
        {
            CloseId();
            _flow?.CloseFlow();
            ShutTheBookHard();
            if (stage != null) stage.SetDrawerOpen(false, instant: false);
        }

        // ── her words ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// What she says tonight, in order, in the player's language: the thanks for the job just done, then the
        /// next job - or, with nothing to hand over, the finale or "not yet". The slots are filled after the table
        /// has spoken, as VoiceBook fills a drinker's line: {name} her whole name, {drink} the job's drink, {n} its
        /// count, {rung} the rung the next job waits on.
        /// </summary>
        private List<string> HostessLines(TycoonRun run, HostessVisit visit, StoryCharacter host)
        {
            var said = new List<string>();
            if (visit == null || host == null) return said;
            if (visit.Finished != null)
                Add(UIText.DataLines("quest", visit.Finished.Id, "done", visit.Finished.Definition.Done), visit.Finished);
            if (visit.Offered != null)
                Add(UIText.DataLines("quest", visit.Offered.Id, "hand_over", visit.Offered.Definition.HandOver), visit.Offered);
            else if (visit.Finale && run.Quests != null)
                Add(UIText.DataLines("hostess", "finale", "say", run.Quests.Finale), null);
            else if (visit.WaitingFor != null && run.Quests != null)
                Add(UIText.DataLines("hostess", "not_yet", "say", new[] { run.Quests.NotYet }), null);
            return said;

            void Add(List<string> lines, ActiveQuest quest)
            {
                foreach (var line in lines)
                    if (!string.IsNullOrWhiteSpace(line)) said.Add(FillHostessLine(line, quest, host, visit));
            }
        }

        private static string FillHostessLine(string line, ActiveQuest quest, StoryCharacter host, HostessVisit visit)
        {
            string s = line.Replace("{name}", host != null ? host.Name : "");
            if (quest != null)
            {
                if (quest.Kind == QuestKind.Serve)
                    s = s.Replace("{drink}", UIText.Data("recipe", quest.RecipeId, "name", quest.RecipeName));
                s = s.Replace("{n}", quest.Target.ToString());
            }
            if (visit != null && visit.WaitingFor != null) s = s.Replace("{rung}", UIText.T(visit.WaitingFor.Title));
            return s;
        }

        /// <summary>The line the message quotes: the last thing she said handing the job over, slots filled.</summary>
        private string HostessQuotedLine(ActiveQuest quest, StoryCharacter host)
        {
            var handOver = quest.Definition.HandOver;
            int last = handOver.Count - 1;
            string line = UIText.Data("quest", quest.Id, "hand_over." + last, handOver[last]);
            return FillHostessLine(line, quest, host, null);
        }

        /// <summary>
        /// Her lines on the plate (SyncLastCall's no-beat path, while she is talking): her face, her name, one line a
        /// key. The last key is ON IT when she brings a job and GOOD NIGHT when she does not; there is nothing to say
        /// no to, and no rung row or post-it.
        /// </summary>
        private void SyncHostessPlate(TycoonRun run, HostessVisit visit)
        {
            string part = "hostess:" + visit.Key;
            var host = Hostess;
            if (part != _plateStage)
            {
                _plateStage = part;
                _plateAt = 0;
                _plateScript.Clear();
                _hostessSaidAt = -1;
                foreach (var line in HostessLines(run, visit, host))
                    _plateScript.Add((host.Name, LookForStory(host)?.Slug, line));
                if (_plateScript.Count == 0) { _plateStage = ""; HostessHeard(run); return; }
            }
            bool talking = _plateAt < _plateScript.Count;
            if (talking != _plate.gameObject.activeSelf) _plate.gameObject.SetActive(talking);
            if (talking)
            {
                var (who, look, line) = _plateScript[_plateAt];
                _plateName.text = UIText.Caps(who);
                _plateLine.text = line;   // already in the player's language: HostessLines built the script
                var face = LookNamed(look);
                _plateFace.sprite = face?.Face;
                _plateFace.enabled = _plateFace.sprite != null;
                bool last = _plateAt == _plateScript.Count - 1;
                _plateKeyLabel.text = !last ? UIText.T("chrome.story.go_on")
                    : visit.Offered != null ? UIText.T("chrome.hostess.on_it") : UIText.T("chrome.story.good_night");
                if (_plateNoKey.gameObject.activeSelf) _plateNoKey.gameObject.SetActive(false);
            }
            if (_gateRow.gameObject.activeSelf) _gateRow.gameObject.SetActive(false);
            if (_postIt.gameObject.activeSelf) _postIt.gameObject.SetActive(false);
        }
    }
}
