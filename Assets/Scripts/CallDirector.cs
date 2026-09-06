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

    /// Same moment as OnCallCue, additionally carrying the entry's groupId. Added alongside the
    /// original (parameterless) event rather than changing OnCallCue's signature, specifically so
    /// existing subscribers (Stage1WaveRideVisuals, Stage1WaterPourVisuals) don't need to change at
    /// all — they infer "which note" from firing order via a running counter, which only works when
    /// one group's calls always finish spawning before the next group's start (true for their
    /// charts). That assumption breaks for a chart where groups are deliberately interleaved
    /// call-by-call (e.g. Stage1QuickTapVisuals's cup relay wanting several relays running staggered
    /// one-ant-apart) — a plain firing-order counter can't tell which relay a given cue belongs to
    /// once two groups' individual calls start interleaving, not just overlapping as whole groups.
    /// Consumers that need real overlap safety should use this instead of OnCallCue.
    public System.Action<int> OnCallCueGroup;

    /// Fires on every player button press (Space/click) edge, regardless of whether it lands on a
    /// live Response window or not. Unlike OnCallCue, this is driven by the player's own input timing —
    /// minigame visuals (e.g. Ants) that should react to the player's action, not the chart clock, use this.
    public System.Action OnResponsePress;

    /// Same as OnResponsePress but for the release edge — fires on every physical release, regardless
    /// of whether anything was armed/judged. Added for Stage1SurviveVisuals: minigames where a
    /// character's on-screen position should be a literal, unconditional mirror of "is the button
    /// currently held" (not gated by whether that hold happened to land inside a judged window) need
    /// this instead of inferring release from OnHoldArmed/OnGroupJudged — those only fire for a
    /// *successful* arm/judgement, so a mistimed press that never claimed a live note left the
    /// character stuck in whatever pose its last successful event set, with no event ever firing to
    /// move it back (confirmed as a real reported bug — player stayed surfaced indefinitely after a
    /// mistimed release).
    public System.Action OnResponseRelease;

    /// Fires whenever a press or release happens that doesn't correspond to any live, claimable note
    /// at all — mash-cooldown presses, presses/releases nowhere near any note's window, and a
    /// press/release after this cycle's note is already judged/resolved. A near-miss press/an early
    /// Hold release still get their own Miss via OnGroupJudged (already fail-worthy on their own) so
    /// this deliberately does NOT also fire for those — it's specifically for input that CallDirector
    /// currently treats as complete no-ops. Added for Stage1SurviveVisuals: with OnResponsePress/
    /// OnResponseRelease driving the player's dive/surface pose unconditionally (see those events'
    /// doc comments), a genuinely off-beat press/release would otherwise still show a perfectly normal
    /// dive/surface pose — this lets that minigame fail-flash it instead, without CallDirector needing
    /// to know anything about "diving" or "surfacing".
    public System.Action OnResponseWasted;

    /// Fires once per Response-role entry actually resolved (Hit or Miss), carrying that entry's
    /// groupId — see Resolve(). Unlike GameManager.OnJudgement (global, no groupId, shared by every
    /// minigame), this comes straight from CallDirector at the exact LiveCall being resolved, so a
    /// minigame with several relays/groups in flight at once can tell which one this verdict belongs
    /// to without guessing (no FIFO-queue-of-pending-groups workaround needed).
    public System.Action<int, JudgementType> OnGroupJudged;

    /// Fires the instant a Response-role Hold is successfully claimed (armed) by a press — i.e. the
    /// player started holding a real, live Hold note. Distinct from OnResponsePress (fires on every
    /// press regardless of whether it claimed anything): this only fires for a genuine successful
    /// arm, so minigame visuals that need to show "the player is now actively holding" (e.g.
    /// Stage1SurviveVisuals diving the player character underwater) know exactly when that state
    /// actually starts, not just when a press happened.
    public System.Action<int> OnHoldArmed;

    /// Fires the instant an armed Hold is released too early (before its release window opens),
    /// right before the Resolve(Miss) call that immediately follows — carries that entry's groupId.
    /// A Hold can fail two different ways (see HandleInput's releaseEdge branch and ResolveTimeouts):
    /// released too early, or never released in time (timeout, including never pressed at all).
    /// OnGroupJudged alone can't tell these apart (both just arrive as groupId+Miss) — minigames that
    /// need to show a different visual depending on which kind of fail happened (e.g.
    /// Stage1SurviveVisuals: early release surfaces the character before showing its fail sprite,
    /// timeout leaves it submerged) listen for this to flag the upcoming Miss, then consume that
    /// flag when OnGroupJudged fires for the same groupId right after.
    public System.Action<int> OnHoldEarlyRelease;

    /// Fires for a group that's being torn down mid-flight without ever having a Response note to
    /// judge — see ForceResolvePending. Carries just the groupId, no JudgementType: unlike
    /// OnGroupJudged this was never a real Hit or Miss, so minigame visuals should react by silently
    /// hiding/resetting whatever they've got for this group (no success/fail animation), not by
    /// treating it as a verdict.
    public System.Action<int> OnGroupAbandoned;

    class LiveCall
    {
        public CallEntry data;
        public float windowOpenTime;     // Tap: cue-hw.      Hold: release-hw.
        public float deadline;           // Tap: cue+hw.      Hold: release+hw.
        public float claimableFromTime;  // Tap: cue-hw.      Hold: cue-hw (start-of-hold cue, same ± hw tolerance).
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

    /// Read-only view of the live, already-sorted-by-cueBeat spawn list — the actual schedule this
    /// director is running, INCLUDING whatever PracticeLoopExtender has synthesized/appended so far
    /// (unlike CallPatternData.calls, which for a looping practice chart only ever holds the one
    /// authored cycle). Added for Stage1SurviveVisuals: its bubble-anticipation cue needs each
    /// character's own real cueBeat/releaseBeat, several beats ahead of when that entry actually
    /// fires, to schedule "1 beat before" independently per entry instead of assuming a fixed gap
    /// between characters (see that class's doc comment) — this is the only way to see appended
    /// practice-loop reps far enough in advance for that to work.
    public IReadOnlyList<CallEntry> Calls => _calls;

    /// The CallClock this director reads SongTime from — exposed so visual glue scripts that get
    /// rebound to a different director at runtime (Stage1PracticeGate segment swaps, tutorial gates)
    /// can always reach the CURRENTLY bound director's clock via BindDirector(newDirector), instead
    /// of holding a separate, potentially-stale CallClock reference of their own.
    public CallClock Clock => clock;

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

    /// Force-resolves every still-live (spawned, not yet resolved), non-demo entry as a Hit, then
    /// drops it from `_live`. For a practice segment about to be torn down (Stage1PracticeGate,
    /// once requiredHits lands or skip is pressed): CallDirector's calls are explicitly allowed to
    /// overlap (see class doc), so it's entirely normal for a NEXT relay/cycle to already be
    /// mid-flight (some of its Call-role notes already spawned, cup already hopping) at the exact
    /// moment the player's Nth Hit satisfies practice completion — that in-flight cycle's own
    /// Response note is still in the future and would otherwise never get to fire OnGroupJudged
    /// once this CallDirector stops ticking (segment deactivated), stranding any visual glue keyed
    /// off that event mid-animation (e.g. Stage1QuickTapVisuals' cup, confirmed reported: "A 패턴
    /// 3번 다 맞췄는데 컵이 하나 더 나와서 B 시작하며 멈춤"). Calling this right before disabling
    /// the segment lets any such stranded relay finish its journey instead. Practice segments don't
    /// care about score (GameManager.ResetGame() wipes it before the main chart starts either way),
    /// so treating every leftover as a Hit is safe and simplest — demo entries are skipped since
    /// Resolve() doesn't know about isDemo and they're not judged by design.
    ///
    /// That first pass alone still leaves one hole: a group whose Call-role/demo notes already
    /// spawned (so its relay visuals already started reacting to OnCallCueGroup — e.g. the cup
    /// hopping ant-to-ant in Stage1QuickTapVisuals) but whose own Response note hasn't spawned into
    /// `_live` yet — it's still ahead of its cueBeat-hitWindow threshold. That group has no live
    /// non-demo entry for the loop below to Resolve(), so it never got an OnGroupJudged at all, and
    /// its relay visuals are left permanently stranded wherever they last hopped once this
    /// CallDirector goes disabled (confirmed reported: 3 hits already landed, yet one more cup
    /// appeared and froze mid-relay instead of ever reaching the player ant).
    ///
    /// First attempt fired a synthetic OnGroupJudged(Hit) for these leftovers so the visual would
    /// play out its full "success" journey (cup hops on to the player ant, then the clown) — but
    /// that note was never actually presented to the player as something to respond to, so playing
    /// its success animation reads as a whole extra, uncontrolled cup appearing right as the next
    /// segment is already spinning up (confirmed reported: the just-completed cup visibly reversing
    /// back to the first ant — that pooled Image getting reclaimed by the new segment's first relay
    /// while the forced "success" flight was still mid-air). A group that got torn down before ever
    /// having a real judgement isn't a Hit — it's abandoned — so this now fires the dedicated
    /// OnGroupAbandoned(groupId) instead, carrying no JudgementType, letting bound visuals silently
    /// hide/reset whatever they claimed for it instead of animating a verdict that never happened.
    public void ForceResolvePending()
    {
        var groupsInFlight = new HashSet<int>();
        foreach (var lc in _live)
            if (!lc.resolved) groupsInFlight.Add(lc.data.groupId);

        foreach (var lc in _live)
        {
            if (lc.resolved || lc.isDemo) continue;
            Resolve(lc, JudgementType.Hit); // fires OnGroupJudged itself
            groupsInFlight.Remove(lc.data.groupId);
        }
        _live.RemoveAll(lc => lc.resolved);

        foreach (var groupId in groupsInFlight)
            OnGroupAbandoned?.Invoke(groupId);
    }

    /// Appends more entries to the end of the live spawn list — for a practice loop that keeps
    /// re-feeding copies of its one authored cycle so the note stream never runs dry (see
    /// PracticeLoopExtender). Judgement state (_judgedEntries/_failedGroups) is keyed by reference/
    /// groupId, not by index, and _nextSpawnIdx only ever walks forward, so appending is safe —
    /// but the caller MUST pass entries already sorted ascending by cueBeat, all greater than every
    /// cueBeat already in the list, or SpawnDueCalls' forward walk breaks.
    public void AppendCalls(List<CallEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;
        _calls.AddRange(entries);
    }

    /// Wholesale-replaces the live spawn list and resets every piece of per-run state (spawn
    /// pointer, live calls, judged/failed tracking, song-end latch, phase-change tracking) —
    /// exactly what Awake() does, just callable after Awake has already run. For
    /// PracticeLoopExtender: Awake() already snapshotted the asset's `calls` as-authored (which
    /// would judge the very first cycle instead of demoing it), so the extender calls this once
    /// in its own Start() — guaranteed to run before this director's first Update() — to swap in
    /// its synthesized [demo cycle, then real judged cycles...] list before anything spawns.
    /// Never touches the CallPatternData asset itself.
    public void ReplaceCalls(List<CallEntry> entries)
    {
        _calls = entries != null ? new List<CallEntry>(entries) : new List<CallEntry>();
        _calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
        _nextSpawnIdx = 0;
        _live.Clear();
        _judgedEntries.Clear();
        _failedGroups.Clear();
        _songEndFired = false;
        _currentPhaseIsResponse = null;
    }

    void Update()
    {
        if (clock == null || pattern == null) return;
        if (clock.IsPaused) return; // frozen SongTime means no new spawns/timeouts, but input must be dropped too

        // Song's over and the result screen is up. Everything is already spawned and resolved so
        // there's nothing left to do here — and crucially HandleInput must stop, otherwise a
        // Space/click that dismisses the result screen also fires OnResponsePress/OnResponseRelease
        // and the player character visibly reacts behind the popup (reported on every stage).
        // CheckSongEnd sets this the same frame OnSongEnded fires.
        if (_songEndFired) return;

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

            // A stall between this segment activating and this Update() actually running (a frame
            // hitch, or CallClock's introDelay buffer getting eaten by activation cost — see
            // CallClock.Start()) can mean SongTime has already raced past this entry's ENTIRE
            // judged window by the moment the while loop above finally reaches it — every note the
            // stall skipped over gets spawned in one instantaneous burst. Left alone, a real
            // (non-demo) Response entry spawned already-dead like this would sail straight into
            // ResolveTimeouts (which runs right after this method, same Update() call) and get
            // timeout-failed before the player ever had one real frame to see or respond to it —
            // confirmed reported: intermittent instant-fail right at tutorial start, still inside
            // what's supposed to be the judgement-free preview.
            //
            // First attempt dropped it from `_live` entirely (never spawned at all) — but that also
            // silently skipped its OnPhaseChanged/OnCallCue below, so the visuals stopped matching
            // the chart mid-burst (confirmed reported: "미리보기 때도 데이터대로 움직이긴 해야 해,
            // 판정만 안 하면 되는 것" — the clown no longer reaching ant5 during preview at all).
            // Forcing this one LiveCall instance's own isDemo flag to true instead keeps it flowing
            // through the exact same spawn path as a real demo entry below (so every visual event —
            // OnPhaseChanged, OnCallCue, indicator — still fires exactly as authored), while
            // ResolveTimeouts's isDemo branch cleans it up as a harmless no-score Hit instead of a
            // real timeout-Miss. Only this LiveCall's flag changes — the underlying CallEntry
            // (role/forceDemo) is untouched, so it still reads as a genuine judged entry everywhere
            // else; _judgedEntries marks it spent so a later stray press can't near-miss-claim it via
            // FindNearMissEntry (which recomputes isDemo from the CallEntry itself, not this LiveCall).
            if (!lc.isDemo && t > lc.deadline)
            {
                lc.isDemo = true;
                _judgedEntries.Add(entry);
            }

            _live.Add(lc);

            // Driven by the entry's own authored role, not by isDemo — a Response entry that's
            // being demoed (PracticeLoopExtender's forceDemo) still visually reads as "the
            // response phase" (e.g. the clown/boss visiting the response character) even though
            // it isn't judged. For every non-practice chart today isDemo is only ever true when
            // role==Call anyway (liveInputStartBeat is 0 everywhere), so this is a no-op change
            // for existing charts — see CallEntry.forceDemo's doc comment.
            bool isResponsePhase = entry.role == CallRole.Response;
            if (_currentPhaseIsResponse != isResponsePhase)
            {
                _currentPhaseIsResponse = isResponsePhase;
                OnPhaseChanged?.Invoke(isResponsePhase);
            }

            var indicator = entry.type == CallType.Tap ? tapIndicator : holdIndicator;
            indicator?.ShowOpen(lc.isDemo);
            // Only a genuine Call-role demo fires OnCallCue/OnCallCueGroup — a demoed Response
            // entry (forceDemo) must NOT, or minigames that count Call cues to know whose turn it
            // is (Stage1WaveRideVisuals) would see one extra cue and desync every rep after.
            if (lc.isDemo && entry.role == CallRole.Call)
            {
                OnCallCue?.Invoke();
                OnCallCueGroup?.Invoke(entry.groupId);
                Debug.Log($"[CallDirector:{name}] Call cue — group {entry.groupId} (beat {entry.cueBeat})"); // 콘솔에서 진행 그룹 확인용
            }

            _nextSpawnIdx++;
        }
    }

    LiveCall BuildLiveCall(CallEntry entry)
    {
        float cueTime = pattern.BeatToTime(entry.cueBeat);
        bool  isDemo  = entry.role == CallRole.Call || entry.cueBeat < pattern.liveInputStartBeat || entry.forceDemo;

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
            // Armable from hitWindow BEFORE the start cue — symmetric with a Tap's claim window
            // (cueTime - hitWindow), and with how "on the beat" means "± hitWindow" everywhere
            // else. Previously this was bare `cueTime`, so a press even 1 ms early didn't arm, and
            // since the player is then holding the button (no fresh press edge) the hold could
            // never arm at all and died on timeout — a Miss for a hold the player pressed and held
            // right through. Only stage with Holds is 1-3, which is exactly where this was reported
            // as unfair. (The auto-arm in HandleInput covers pressing MORE than hitWindow early.)
            claimableFromTime = cueTime - pattern.hitWindow,
        };
    }

    void ResolveTimeouts(float t)
    {
        foreach (var lc in _live)
        {
            if (lc.resolved) continue;

            if (lc.isDemo)
            {
                if (t > lc.deadline)
                {
                    lc.resolved = true;

                    // 데모(role=Response, forceDemo)는 실제로 판정받지 않지만, 데모 자체가 "완벽한
                    // 성공"을 보여주는 연출이므로 그 성공을 시각적으로 완결할 신호가 필요하다 —
                    // Resolve()를 거치지 않으므로 점수/GameManager/_judgedEntries/_failedGroups엔
                    // 전혀 영향 없이 OnGroupJudged(Hit)만 쏴준다. 이게 없으면 이 이벤트로만 마지막
                    // 동작을 트리거하는 비주얼(예: Stage1QuickTapVisuals의 컵이 플레이어 개미->광대로
                    // 넘어가는 마지막 두 홉)은 데모 중 절대 완결되지 못하고 마지막 Call 위치에 멈춘다
                    // — 실제 겪은 버그("1-2 해보십시오 나오기 전 데모에서 컵이 개미4에서 멈춤").
                    if (lc.data.role == CallRole.Response)
                        OnGroupJudged?.Invoke(lc.data.groupId, JudgementType.Hit);
                }
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
                OnResponseWasted?.Invoke();
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
                    else
                    {
                        claim.isHoldArmed = true; // judged on release
                        OnHoldArmed?.Invoke(claim.data.groupId);
                    }
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

                        // A near-miss press never reaches Resolve() (that's the whole point — it
                        // must not double-register score), so it also never fired OnGroupJudged on
                        // its own. But ResolveTimeouts later sees this entry already in
                        // _judgedEntries and skips calling Resolve() for it too (see the "already
                        // used its one judgement" branch above) — meaning without this line,
                        // OnGroupJudged would NEVER fire for a near-missed group at all. Any visual
                        // glue keyed off groupId (e.g. Stage1QuickTapVisuals' cup relay, which parks
                        // the cup at the last Call-role ant until OnGroupJudged tells it to hop
                        // onward) would then strand its per-group state forever — this was a real
                        // reported bug ("cup stuck at ant5"). Fired unconditionally, matching
                        // Resolve() itself, which always fires OnGroupJudged regardless of whether
                        // this turn's Miss actually counted for score.
                        OnGroupJudged?.Invoke(groupId, JudgementType.Miss);

                        if (pattern.minInputIntervalSeconds > 0f)
                            _cooldownUntil = t + pattern.minInputIntervalSeconds;
                    }
                    else
                    {
                        // Nowhere near any note at all — pure noise, not judged, no cooldown. Still
                        // tell minigame visuals a press happened that claimed nothing real (see
                        // OnResponseWasted) — near-miss above already got its own Miss/fail via
                        // OnGroupJudged, so this branch is specifically the "not even close" case,
                        // including a press that arrives after this cycle's note is already fully
                        // judged/locked (_judgedEntries excludes it from FindNearMissEntry too).
                        OnResponseWasted?.Invoke();
                    }
                }
            }
        }

        // Auto-arm a Hold whose claim window is open while the button is already held down, with
        // nothing else armed. Covers the player pressing BEFORE this hold's window opened (natural
        // right after releasing the previous cycle's hold, or just anticipating the beat) and then
        // holding straight through — there's no fresh press edge inside the window for the pressEdge
        // path above to claim it, so without this the hold silently never arms and times out as a
        // Miss even though the player is physically holding through the whole thing (reported as
        // unfair on 1-3 — the only stage with Holds). This only ever fixes the ARM: the release is
        // still judged strictly against releaseBeat ± hitWindow, no verdict is softened. Respects
        // the mash cooldown exactly like every other input path.
        if (pressedNow && t >= _cooldownUntil)
        {
            bool anyHoldArmed = false;
            foreach (var lc in _live)
                if (!lc.resolved && lc.data.type == CallType.Hold && lc.isHoldArmed) { anyHoldArmed = true; break; }

            if (!anyHoldArmed)
            {
                LiveCall toArm = null;
                float bestDeadline = float.MaxValue;
                foreach (var lc in _live)
                {
                    if (lc.resolved || lc.isDemo || lc.data.type != CallType.Hold) continue;
                    if (lc.isHoldArmed) continue;
                    if (_judgedEntries.Contains(lc.data)) continue; // a hold already blown this turn never re-arms
                    if (t < lc.claimableFromTime || t > lc.deadline) continue;
                    if (lc.deadline < bestDeadline) { bestDeadline = lc.deadline; toArm = lc; } // earliest-deadline wins, same as pressEdge
                }
                if (toArm != null)
                {
                    toArm.isHoldArmed = true;
                    OnHoldArmed?.Invoke(toArm.data.groupId);
                }
            }
        }

        if (releaseEdge)
        {
            OnResponseRelease?.Invoke(); // unconditional, mirrors OnResponsePress — see its doc comment

            LiveCall armed = null;
            foreach (var lc in _live)
                if (!lc.resolved && lc.data.type == CallType.Hold && lc.isHoldArmed) { armed = lc; break; }

            if (armed != null)
            {
                if (t < armed.windowOpenTime)
                {
                    OnHoldEarlyRelease?.Invoke(armed.data.groupId); // fires just before the Resolve() below so
                                                                     // OnGroupJudged subscribers can tell this
                                                                     // Miss apart from a timeout Miss
                    Resolve(armed, JudgementType.Miss); // early release -> final Miss
                }
                else if (t <= armed.deadline)      Resolve(armed, JudgementType.Hit);
                // else: already handled by ResolveTimeouts as a timeout Miss
            }
            else
            {
                // Release with nothing currently armed (never pressed, or this cycle's note is
                // already judged/resolved) — previously just silently ignored. See OnResponseWasted.
                OnResponseWasted?.Invoke();
            }
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
            bool isDemo = entry.role == CallRole.Call || entry.cueBeat < pattern.liveInputStartBeat || entry.forceDemo;
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

        OnGroupJudged?.Invoke(lc.data.groupId, j);
        Debug.Log($"[CallDirector:{name}] Group {lc.data.groupId} judged: {j}"); // 콘솔에서 진행 그룹 확인용
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
