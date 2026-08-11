using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime judgement engine for the call-and-response system. Unlike BattleDirector,
/// calls are not grouped into sequential call/response phases — the full chart is a flat,
/// time-sorted list and multiple calls may have open judgement windows simultaneously.
/// Single physical response (Space key or mouse left click, both treated as one logical button).
/// </summary>
public class CallDirector : MonoBehaviour
{
    [SerializeField] CallClock clock;
    [SerializeField] CallPatternData pattern;
    [SerializeField] Key responseKey = Key.Space;

    [Header("Placeholder visuals — swap for real minigame art later")]
    [SerializeField] CallCueIndicator tapIndicator;
    [SerializeField] CallCueIndicator holdIndicator;

    public System.Action OnSongEnded;

    /// Fires whenever the chart transitions from a Call-role entry to a Response-role entry
    /// (or back), in chronological spawn order. Minigame visual scripts (e.g. Stage1WaterPourVisuals)
    /// listen to this to switch which character is on screen — CallDirector itself stays
    /// minigame-agnostic and knows nothing about Hand/Ants/Boss.
    public System.Action<bool> OnPhaseChanged; // true = Response phase, false = Call phase

    /// Fires exactly when a Call-role (demo) entry becomes live — i.e. on the beat the boss/hand
    /// is demonstrating. This is a scripted cue driven by chart timing, not player input.
    public System.Action OnCallCue;

    /// Fires on every player button press (Space/click) edge, regardless of whether it lands on a
    /// live Response window or not. Unlike OnCallCue, this is driven by the player's own input timing —
    /// minigame visuals (e.g. Ants) that should react to the player's action, not the chart clock, use this.
    public System.Action OnResponsePress;

    class LiveCall
    {
        public CallEntry data;
        public float windowOpenTime;     // Tap: cue-hw.      Hold: release-hw.
        public float deadline;           // Tap: cue+hw.      Hold: release+hw.
        public float claimableFromTime;  // Tap: windowOpenTime. Hold: cueTime (start-of-hold cue).
        public bool  isDemo;
        public bool  isHoldArmed;
        public bool  resolved;
    }

    List<CallEntry> _calls;     // local sorted copy — never mutates the asset
    readonly List<LiveCall> _live = new List<LiveCall>();

    /// Whether this chart's first entry (by cueBeat) is a Response — lets visual glue scripts
    /// (e.g. Stage1WaterPourVisuals) initialize the right idle state without needing their own
    /// CallPatternData reference, and without waiting for the first spawn/OnPhaseChanged event.
    public bool StartsInResponsePhase => _calls != null && _calls.Count > 0 && _calls[0].role == CallRole.Response;

    int  _nextSpawnIdx;
    bool _songEndFired;
    bool _prevPressed; // Space + mouse left click combined, previous-frame state
    bool? _currentPhaseIsResponse;
    // SongTime until which ALL input is ignored — only ever set by an "empty air" press (see
    // HandleInput). A press that lands on a real note never sets this, so legitimate fast
    // back-to-back hits are never throttled, only wasted/mistimed presses cost you a cooldown.
    float _cooldownUntil = float.NegativeInfinity;

    // groupId -> already took a Miss this turn. Caps a turn at one Miss no matter how it happens
    // (a mistimed press now, the note's own timeout later, a second Response note in the same
    // group, ...) — one blown turn shouldn't cascade into several. groupId is unique per turn for
    // a whole chart (Call Map Editor's group counter only ever increments), so this never needs
    // clearing mid-song. -1 (ungrouped/legacy entries) is intentionally never tracked here.
    readonly HashSet<int> _failedGroups = new HashSet<int>();

    // A note gets exactly one judgement attempt across its whole span (front Miss Window + Hit
    // Window + back Miss Window), whichever comes first. Without this, an early near-miss press
    // (which doesn't resolve the LiveCall — see FindNearMissEntry) left the note's Hit Window still
    // claimable, so a second, later press could still land a Hit on a note you already fumbled —
    // and conversely a press in the *back* Miss Window after an already-resolved Hit had nothing
    // stopping FindNearMissEntry from matching it again and firing a phantom extra Miss. Reference
    // equality is fine: entries in _calls are the same CallEntry instances referenced by every
    // LiveCall.data built from them.
    readonly HashSet<CallEntry> _judgedEntries = new HashSet<CallEntry>();

    void Awake()
    {
        if (pattern == null && clock != null) pattern = clock.Pattern;
        _calls = pattern != null ? new List<CallEntry>(pattern.calls) : new List<CallEntry>();
        _calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
    }

    void Update()
    {
        if (clock == null || pattern == null) return;
        if (clock.IsPaused) return; // frozen SongTime means no new spawns/timeouts, but input must be dropped too

        float t = clock.SongTime;

        SpawnDueCalls(t);
        ResolveTimeouts(t);
        HandleInput(t);
        _live.RemoveAll(lc => lc.resolved);
        CheckSongEnd(t);
    }

    void SpawnDueCalls(float t)
    {
        while (_nextSpawnIdx < _calls.Count)
        {
            var entry     = _calls[_nextSpawnIdx];
            float cueTime = pattern.BeatToTime(entry.cueBeat);
            if (t < cueTime - pattern.hitWindow) break;

            var lc = BuildLiveCall(entry);
            _live.Add(lc);

            bool isResponsePhase = !lc.isDemo;
            if (_currentPhaseIsResponse != isResponsePhase)
            {
                _currentPhaseIsResponse = isResponsePhase;
                OnPhaseChanged?.Invoke(isResponsePhase);
            }

            var indicator = entry.type == CallType.Tap ? tapIndicator : holdIndicator;
            indicator?.ShowOpen(lc.isDemo);
            if (lc.isDemo) OnCallCue?.Invoke();

            _nextSpawnIdx++;
        }
    }

    LiveCall BuildLiveCall(CallEntry entry)
    {
        float cueTime = pattern.BeatToTime(entry.cueBeat);
        bool  isDemo  = entry.role == CallRole.Call || entry.cueBeat < pattern.liveInputStartBeat;

        if (entry.type == CallType.Tap)
        {
            return new LiveCall
            {
                data = entry, isDemo = isDemo,
                windowOpenTime    = cueTime - pattern.hitWindow,
                deadline          = cueTime + pattern.hitWindow,
                claimableFromTime = cueTime - pattern.hitWindow,
            };
        }

        float releaseTime = pattern.BeatToTime(entry.releaseBeat);
        return new LiveCall
        {
            data = entry, isDemo = isDemo,
            windowOpenTime    = releaseTime - pattern.hitWindow,
            deadline          = releaseTime + pattern.hitWindow,
            claimableFromTime = cueTime,   // armable any time from its own start cue
        };
    }

    void ResolveTimeouts(float t)
    {
        foreach (var lc in _live)
        {
            if (lc.resolved) continue;

            if (lc.isDemo)
            {
                if (t > lc.deadline) lc.resolved = true; // just cleanup, no judgement
                continue;
            }

            if (t <= lc.deadline) continue;

            if (_judgedEntries.Contains(lc.data))
            {
                // Already used its one judgement via an earlier near-miss press — just clean up,
                // no second Resolve() call (no double SFX/indicator/score).
                lc.resolved = true;
                continue;
            }

            // Deadline passed without a valid resolution:
            //  - Tap: never pressed in [windowOpenTime, deadline]
            //  - Hold: never pressed at all, OR held past the window without releasing
            Resolve(lc, JudgementType.Miss);
        }
    }

    void HandleInput(float t)
    {
        bool spaceDown = Keyboard.current != null && Keyboard.current[responseKey].isPressed;
        bool mouseDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool pressedNow = spaceDown || mouseDown;
        bool pressEdge   = pressedNow && !_prevPressed;
        bool releaseEdge = !pressedNow && _prevPressed;
        _prevPressed = pressedNow;

        if (pressEdge)
        {
            // OnResponsePress always fires, even inside the mash-guard cooldown below — reactive
            // visuals (a normal ant's hand-raise, a cup pass) should still play on every physical
            // press so the game feels responsive, even when that press doesn't count for judgement.
            OnResponsePress?.Invoke();

            if (t < _cooldownUntil)
            {
                // Still cooling down from an earlier empty-air press — every input is ignored
                // outright during this window, including one that would otherwise land on a real
                // note (it'll simply time out on its own later, same as never pressing at all).
            }
            else
            {
                LiveCall claim = null;
                float bestDeadline = float.MaxValue;

                foreach (var lc in _live)
                {
                    if (lc.resolved || lc.isDemo) continue;
                    if (_judgedEntries.Contains(lc.data)) continue; // already used its one judgement (an earlier near-miss)
                    if (lc.data.type == CallType.Hold && lc.isHoldArmed) continue; // already holding this one
                    if (t < lc.claimableFromTime || t > lc.deadline) continue;

                    if (lc.deadline < bestDeadline) { bestDeadline = lc.deadline; claim = lc; } // earliest-deadline wins
                }

                if (claim != null)
                {
                    // Landed inside a real note's window — judge normally. Deliberately does NOT
                    // start the mash cooldown, so a fast run of genuinely-on-time hits is never
                    // throttled by it.
                    if (claim.data.type == CallType.Tap) Resolve(claim, JudgementType.Hit);
                    else                                  claim.isHoldArmed = true; // judged on release
                }
                else
                {
                    // Missed the Hit window, but check whether it's still close enough to some
                    // note (within hitWindow+missWindow) to read as a real wrong-timing attempt
                    // rather than random noise.
                    CallEntry nearMiss = FindNearMissEntry(t);
                    if (nearMiss != null)
                    {
                        // This note's one judgement attempt is now spent — a later press anywhere
                        // else in its span (front/back Miss Window, or even its own Hit Window once
                        // it opens) can no longer claim a Hit or register another Miss for it.
                        _judgedEntries.Add(nearMiss);

                        // Close but wrong: register a Miss so mashing near a note can't fish for
                        // hits across its whole window for free — unless this turn already took its
                        // one Miss (see _failedGroups), in which case this press is just noise on
                        // top of an already-failed turn. Either way, this is what starts the mash
                        // cooldown.
                        int groupId = nearMiss.groupId;
                        if (groupId < 0 || _failedGroups.Add(groupId))
                            GameManager.Instance?.RegisterJudgement(JudgementType.Miss);

                        if (pattern.minInputIntervalSeconds > 0f)
                            _cooldownUntil = t + pattern.minInputIntervalSeconds;
                    }
                    // else: nowhere near any note at all — pure noise, not judged, no cooldown.
                }
            }
        }

        if (releaseEdge)
        {
            LiveCall armed = null;
            foreach (var lc in _live)
                if (!lc.resolved && lc.data.type == CallType.Hold && lc.isHoldArmed) { armed = lc; break; }

            if (armed != null)
            {
                if (t < armed.windowOpenTime)      Resolve(armed, JudgementType.Miss); // early release -> final Miss
                else if (t <= armed.deadline)      Resolve(armed, JudgementType.Hit);
                // else: already handled by ResolveTimeouts as a timeout Miss
            }
            // else: release with nothing armed — ignored
        }
    }

    // Scans the *full* chart (not just _live) for a Response entry whose judged instant (cueBeat
    // for Tap, releaseBeat for Hold) is within hitWindow+missWindow of t. Needs the full list rather
    // than _live because the fail band can reach earlier than a note's own spawn time
    // (cueTime - hitWindow, see SpawnDueCalls), so the note may not be live yet when an early
    // near-miss press happens.
    CallEntry FindNearMissEntry(float t)
    {
        float failRadius = pattern.hitWindow + pattern.missWindow;
        foreach (var entry in _calls)
        {
            bool isDemo = entry.role == CallRole.Call || entry.cueBeat < pattern.liveInputStartBeat;
            if (isDemo) continue;
            if (_judgedEntries.Contains(entry)) continue; // already used its one judgement attempt

            float judgedTime = entry.type == CallType.Tap
                ? pattern.BeatToTime(entry.cueBeat)
                : pattern.BeatToTime(entry.releaseBeat);

            if (Mathf.Abs(t - judgedTime) <= failRadius) return entry;
        }
        return null;
    }

    void Resolve(LiveCall lc, JudgementType j)
    {
        lc.resolved = true;
        _judgedEntries.Add(lc.data); // this note's one judgement attempt is spent

        // A Miss only counts against score/combo the first time its turn fails — see
        // _failedGroups. Hits always count normally, even in an already-failed turn, so a player
        // who recovers mid-turn still gets credit for the notes they do land.
        bool countsForScore = j == JudgementType.Hit ||
                               lc.data.groupId < 0 || _failedGroups.Add(lc.data.groupId);
        if (countsForScore)
            GameManager.Instance?.RegisterJudgement(j); // fires OnJudgement -> ScreenVignetteFlash, BattleSFXController.PlayHit/Miss

        var indicator = lc.data.type == CallType.Tap ? tapIndicator : holdIndicator;
        if (j == JudgementType.Hit) indicator?.ShowHit();
        else                        indicator?.ShowMiss();
    }

    void CheckSongEnd(float t)
    {
        if (_songEndFired) return;
        bool allSpawned  = _nextSpawnIdx >= _calls.Count;
        bool allResolved = _live.Count == 0;
        if (allSpawned && allResolved && clock.IsMusicDone)
        {
            _songEndFired = true;
            OnSongEnded?.Invoke();
        }
    }
}
