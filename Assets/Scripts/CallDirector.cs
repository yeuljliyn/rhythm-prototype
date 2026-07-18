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
    int  _nextSpawnIdx;
    bool _songEndFired;
    bool _prevPressed; // Space + mouse left click combined, previous-frame state

    void Awake()
    {
        if (pattern == null && clock != null) pattern = clock.Pattern;
        _calls = pattern != null ? new List<CallEntry>(pattern.calls) : new List<CallEntry>();
        _calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
    }

    void Update()
    {
        if (clock == null || pattern == null) return;
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

            var indicator = entry.type == CallType.Tap ? tapIndicator : holdIndicator;
            indicator?.ShowOpen(lc.isDemo);

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
            LiveCall claim = null;
            float bestDeadline = float.MaxValue;

            foreach (var lc in _live)
            {
                if (lc.resolved || lc.isDemo) continue;
                if (lc.data.type == CallType.Hold && lc.isHoldArmed) continue; // already holding this one
                if (t < lc.claimableFromTime || t > lc.deadline) continue;

                if (lc.deadline < bestDeadline) { bestDeadline = lc.deadline; claim = lc; } // earliest-deadline wins
            }

            if (claim != null)
            {
                if (claim.data.type == CallType.Tap) Resolve(claim, JudgementType.Hit);
                else                                  claim.isHoldArmed = true; // judged on release
            }
            // else: unmatched press — ignored, no penalty
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

    void Resolve(LiveCall lc, JudgementType j)
    {
        lc.resolved = true;
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
