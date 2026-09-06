using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a CallPatternData authored as "one practice cycle" (pattern.loopLengthBeats &gt; 0) into
/// a seamless, effectively-infinite repeat: the planner draws exactly one cycle in the Call Map
/// Editor, and this component keeps re-feeding CallDirector fresh copies of it, offset forward in
/// time, for as long as the practice segment stays active. There is no engine-level rewind —
/// CallClock.SongTime and the underlying AudioSource both just keep running forward, same as any
/// other chart; only the *note list* grows.
///
/// Cycle 0 is special: every entry that was authored as Response gets forced to Call (demo-only,
/// no input read — see CallRole) so the very first pass through the pattern is a pure
/// demonstration, matching "① 시범 1회". Cycle 1 onward reuse the entries exactly as authored
/// (Response stays Response), so every rep after the demo is judged normally — that's
/// "② 따라하기". Stage1PracticeGate counts real Hits via GameManager.OnJudgement and decides when
/// a segment is done; this component has no opinion on that, it just keeps the note stream fed.
///
/// Audio never restarts: AudioSource.loop is set true once in Start() and CallClock schedules
/// playback exactly the way it always does for any chart. For this to stay audibly in sync forever,
/// the music clip's length must equal loopLengthBeats * BeatInterval (see CallPatternData's
/// loopLengthBeats tooltip) — that's an authoring requirement on the music asset, not something
/// this script can enforce.
/// </summary>
[RequireComponent(typeof(CallDirector))]
[RequireComponent(typeof(CallClock))]
[RequireComponent(typeof(AudioSource))]
public class PracticeLoopExtender : MonoBehaviour
{
    [Tooltip("Update()마다 검사해서, 남은 버퍼가 이 사이클 수 미만으로 줄어들면 다음 사본을 이어붙임. " +
             "너무 작으면(1 미만) 프레임 드랍 시 노트가 바닥날 위험, 너무 크면 불필요한 재계산만 늘어남.")]
    [SerializeField] float refillThresholdCycles = 2f;

    [Tooltip("Start() 시점에 미리 얼마나 많은 판정용 사본을 깔아둘지(시범 사본 1개는 별도, 이건 그 뒤 " +
             "판정용 반복 개수). 이 이후로는 Update()가 알아서 계속 이어붙임.")]
    [SerializeField] int initialBufferReps = 4;

    /// Fires once, the instant SongTime crosses from cycle 0 (the demo) into cycle 1 (the first
    /// judged rep) — i.e. exactly when "따라하기" starts. Stage1PracticeGate uses this to show a
    /// segment's demoStartCaption ("해보십시오"), if it has one.
    public System.Action OnDemoFinished;

    CallDirector _director;
    CallClock    _clock;
    AudioSource  _audio;
    CallPatternData _pattern;

    int  _nextRepToAppend;   // next not-yet-appended repeat index (0 = demo, already seeded)
    bool _demoFinishedFired;

    void Awake()
    {
        _director = GetComponent<CallDirector>();
        _clock    = GetComponent<CallClock>();
        _audio    = GetComponent<AudioSource>();
    }

    void Start()
    {
        _pattern = _clock.Pattern;
        if (_pattern == null || _pattern.loopLengthBeats <= 0f)
        {
            Debug.LogWarning($"{name}: PracticeLoopExtender needs a CallPatternData with loopLengthBeats > 0 — disabling.");
            enabled = false;
            return;
        }

        _audio.loop = true;

        var seed = new List<CallEntry>();
        seed.AddRange(BuildCycle(0, forceDemo: true));
        for (int rep = 1; rep <= initialBufferReps; rep++)
            seed.AddRange(BuildCycle(rep, forceDemo: false));

        _director.ReplaceCalls(seed);
        _nextRepToAppend = initialBufferReps + 1;
    }

    void Update()
    {
        if (_pattern == null) return;

        float currentBeat  = _pattern.TimeToBeat(_clock.SongTime);
        float bufferedUpTo = _nextRepToAppend * _pattern.loopLengthBeats;

        if (bufferedUpTo - currentBeat < refillThresholdCycles * _pattern.loopLengthBeats)
        {
            _director.AppendCalls(BuildCycle(_nextRepToAppend, forceDemo: false));
            _nextRepToAppend++;
        }

        if (!_demoFinishedFired && currentBeat >= _pattern.loopLengthBeats)
        {
            _demoFinishedFired = true;
            OnDemoFinished?.Invoke();
        }
    }

    // Clones every authored entry, offset forward by rep * loopLengthBeats, with a fresh groupId
    // per rep (base groupId + rep * 1000) so CallDirector's one-Miss-per-groupId-per-turn tracking
    // (_failedGroups) treats every repeat as its own independent turn — without this, only the
    // very first repeat's Miss would ever register (HashSet.Add silently no-ops on a reused key),
    // and every later Miss in the loop would go visually/aurally silent. Hits always count
    // regardless (see CallDirector.Resolve), so this only matters for the Miss-reaction side.
    // forceDemo (rep 0 only) marks every entry's NEW CallEntry.forceDemo=true instead of coercing
    // role to Call or dropping Response entries. An earlier version tried both of those and each
    // broke Stage1WaveRideVisuals a different way: coercing role->Call made it fire an extra Call
    // cue (desyncing its "count Call cues mod 4" turn tracking for every rep afterward), and
    // dropping the Response entry fixed that but meant the demo cycle never visited the response
    // character at all. CallEntry.forceDemo (see its doc comment) + the matching CallDirector
    // changes solve both: role stays exactly as authored (so a Response entry still drives
    // OnPhaseChanged(true), moving the clown/boss to the response character) while forceDemo
    // alone keeps it unjudged and keeps it from firing OnCallCue (so the Call-cue counter never
    // sees an extra beat).
    List<CallEntry> BuildCycle(int rep, bool forceDemo)
    {
        var result = new List<CallEntry>(_pattern.calls.Count);
        float beatOffset = rep * _pattern.loopLengthBeats;

        foreach (var src in _pattern.calls)
        {
            result.Add(new CallEntry
            {
                type        = src.type,
                role        = src.role,
                groupId     = src.groupId + rep * 1000,
                cueBeat     = src.cueBeat + beatOffset,
                releaseBeat = src.releaseBeat + beatOffset,
                forceDemo   = forceDemo,
                clip        = src.clip, // 노트별 효과음(CallNoteSfxPlayer)도 반복마다 유지
            });
        }
        return result;
    }
}
