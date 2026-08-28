using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chart asset for the call-and-response engine (Stage1). Unlike PatternData/DirectionGroup,
/// this is a flat list of calls with no phase-separation between "call" and "response" ranges —
/// calls may freely overlap in time on the timeline.
/// </summary>
[CreateAssetMenu(menuName = "RhythmGame/Call Pattern Data", fileName = "NewCallPattern")]
public class CallPatternData : ScriptableObject
{
    public string    chartName = "Segment 1";
    public AudioClip music;
    [Range(40f, 300f)] public float bpm = 128f;
    public float introDelay = 2f;

    [Tooltip("판정 반폭(초 단위) — cueBeat(홀드는 releaseBeat) ± 이 값 이내에 입력하면 Hit. " +
             "실제 Hit 판정 구간 전체 폭은 이 값의 2배.")]
    public float hitWindow = 0.12f;

    [Tooltip("hitWindow 바로 바깥쪽에 붙는 '아깝게 놓침' 판정용 여유 반폭(초 단위). Hit 구간은 " +
             "벗어났지만 hitWindow + missWindow 이내에 들어온 입력은 Miss로 판정(랜덤 노이즈가 " +
             "아니라 실제로 타이밍을 노린 시도로 볼 수 있을 만큼 가까움)되고 마구 누르기 방지 " +
             "쿨다운이 시작됨. 이보다 더 멀리 벗어난 입력은 아예 판정 대상이 아님 — Hit도 Miss도 " +
             "쿨다운도 없고, 눌렀을 때 나오는 리액션 연출만 그대로 재생됨. 0이면 별도 실패 구간 " +
             "없이 hitWindow 밖은 전부 판정 안 함.")]
    public float missWindow = 0.12f;

    [Tooltip("마구 누르기 방지 쿨다운(초 단위, 곡마다 다르게 튜닝). 노트가 없는 '허공'에 입력했을 " +
             "때만 시작됨 — 그 입력은 즉시 Miss로 판정되고, 이후 이 시간 동안은 실제 노트 판정 " +
             "구간 안에 들어오는 입력이라도 포함해서 모든 입력이 조용히 무시됨. 실제 노트를 " +
             "제대로 맞춘 입력은 정상 판정되고 이 쿨다운을 절대 시작시키지 않으므로, 정당하게 " +
             "빠르게 연속으로 맞추는 건 절대 불이익을 받지 않음. 0이면 마구 누르기 방지 없음.")]
    public float minInputIntervalSeconds = 0f;

    [Tooltip("추가 전역 컷오프: cueBeat가 이 박자보다 이전인 콜은 각 엔트리의 CallRole과 " +
             "무관하게 항상 데모 전용(보여주기만 하고 판정 안 함)으로 취급됨. 인트로 없이 " +
             "CallEntry.role로 콜/리스폰스를 엔트리별로 완전히 제어하는 차트라면 0으로 둘 것.")]
    public float liveInputStartBeat = 0f;

    [Tooltip("반드시 cueBeat 오름차순으로 정렬된 상태를 유지해야 함. 추가/이동할 때마다 에디터 " +
             "툴이 자동으로 재정렬함.")]
    public List<CallEntry> calls = new List<CallEntry>();

    [Tooltip("0보다 크면 이 차트는 '연습 루프 한 사이클'이다 — PracticeLoopExtender가 calls 전체를 " +
             "이 박자 길이만큼씩 뒤로 밀어서 계속 이어붙여 무한 반복시킨다. 실제 음악 파일의 길이(초) " +
             "== loopLengthBeats * BeatInterval이어야 AudioSource.loop가 노트 반복과 어긋나지 않는다. " +
             "0(기본값)이면 일반 차트 — 아무 영향 없음.")]
    public float loopLengthBeats = 0f;

    [Tooltip("1-5(리믹스)처럼 곡 하나 안에서 여러 미니게임 스타일을 오갈 때, 어느 구간이 어느 " +
             "스타일인지 표시하는 목록. 비어있으면(기본값) '파일 전체가 하나의 스타일'이라는 " +
             "뜻이라 1-1/1-2/1-4처럼 phase 개념이 필요 없는 기존 채보는 전혀 영향 없음. 같은 " +
             "styleId가 곡 안에서 여러 번(연속 안 해도) 나와도 됨 — 예: 1-1 구간 -> 1-4 구간 -> " +
             "다시 1-1 구간.")]
    public List<ChartPhase> phases = new List<ChartPhase>();

    public float BeatInterval               => 60f / Mathf.Max(1f, bpm);

    // introDelay is folded directly into this conversion (not just used as CallClock's real-world
    // playback-start delay) — see class doc note below for why. BeatToTime(beat) is what every
    // consumer (CallDirector's judgement, Stage1SurviveVisuals, Stage1RemixPhaseSwitcher,
    // PracticeLoopExtender, the chart editor's own timeline/REC math) treats as "true audio content
    // time for this beat"; TimeToBeat is its exact inverse (BeatToTime(TimeToBeat(x)) == x).
    //
    // Why introDelay lives here instead of purely in CallClock's scheduling: the chart editor tool
    // (Stage1ChartEditorWindow) captures a note's cueBeat from its own internal chart-clock
    // (_currentSongTime), which — once its own PlayAudio-timing bug was fixed (audio now genuinely
    // starts only after a real introDelay-second silent lead-in, matching CallClock's runtime
    // scheduling exactly) — equals true content time C exactly. Storing cueBeat as a bare
    // C/BeatInterval would require migrating every already-authored note (BPM differs per chart,
    // so the fix isn't even a single constant) to match; baking the SAME introDelay this chart
    // already carries into the conversion instead means EVERY existing chart's already-recorded
    // notes are correct as-is, no data migration needed, no editing decimals-y beat numbers by
    // hand — confirmed via QA (planner + dev) that this fixes the "notes fire introDelay seconds
    // early relative to the music" bug across every stage without touching a single stored beat.
    public float BeatToTime(float beat)     => beat * BeatInterval + introDelay;
    public float TimeToBeat(float songTime) => (songTime - introDelay) / BeatInterval;

    /// First phase whose [startBeat, endBeat) contains the given beat, or null if phases is empty
    /// or the beat falls in a gap between phases. Overlapping phases (shouldn't normally happen,
    /// but nothing stops a designer from authoring it) resolve to whichever comes first in the list.
    public ChartPhase GetPhaseAt(float beat)
    {
        if (phases == null) return null;
        foreach (var p in phases)
            if (beat >= p.startBeat && beat < p.endBeat) return p;
        return null;
    }

    /// True if the given beat falls inside a phase tagged with this exact styleId. Minigame visuals
    /// scripts sharing a single multi-phase chart (1-5) use this as a cheap per-event guard — see
    /// CallPatternData.phases' tooltip. Always true if this chart has no phases at all (single-style
    /// chart, e.g. any of today's 1-1/1-2/1-4 charts), so existing single-style scripts don't need
    /// any special-casing to keep working once this field exists.
    public bool IsBeatInStyle(float beat, string styleId)
    {
        if (phases == null || phases.Count == 0) return true;
        var p = GetPhaseAt(beat);
        return p != null && p.styleId == styleId;
    }
}

/// One labeled beat range within a multi-phase (1-5-style) chart — see CallPatternData.phases.
[Serializable]
public class ChartPhase
{
    [Tooltip("어느 미니게임 스타일인지 (예: \"1-1\", \"1-2\", \"1-3\", \"1-4\"). 자유 문자열 — " +
             "미니게임 비주얼 스크립트가 CallPatternData.IsBeatInStyle(beat, styleId)로 자기 " +
             "구간인지 확인할 때 쓰는 값과 정확히 일치해야 함.")]
    public string styleId = "";
    public float  startBeat;
    public float  endBeat;
}

public enum CallType { Tap, Hold }

/// Response (default): judged normally against player input.
/// Call: a demonstration cue (e.g. the boss/hand showing the rhythm) — shown, never judged, no input read.
/// This is per-entry so Call/Response can alternate repeatedly through a chart, unlike the
/// single global liveInputStartBeat cutoff (which only supports one demo-then-live split).
public enum CallRole { Response, Call }

[Serializable]
public class CallEntry
{
    public CallType type;
    public CallRole role = CallRole.Response;

    /// Which Call Map Editor Z-echo group this entry belongs to — a Call run and its mirrored
    /// Response run share the same id. Assigned explicitly by the editor tool at authoring time
    /// (not inferred from time/order), so overlapping look-ahead Calls dropped inside an already-
    /// generated Response range don't get confused with that Response's own group. Purely an
    /// authoring/visualization aid — CallDirector ignores this at runtime. -1 = ungrouped
    /// (entries authored before this field existed).
    public int groupId = -1;

    /// Tap: the single judged beat (window = cueBeat ± hitWindow).
    /// Hold: the beat at which the "start holding" cue is shown (not itself judged).
    public float cueBeat;

    /// Hold only. The judged release beat (window = releaseBeat ± hitWindow). Must be > cueBeat.
    public float releaseBeat;

    /// Runtime-only (never authored, never serialized — deliberately [NonSerialized] so it never
    /// clutters the Call Map Editor's Inspector for real chart entries). Set true only by
    /// PracticeLoopExtender's synthesized demo cycle (rep 0): marks an entry as shown-but-not-
    /// judged the same way CallRole.Call already does, WITHOUT changing its role — so a Response
    /// entry demoed this way still drives CallDirector.OnPhaseChanged(true) (the clown/boss
    /// visiting the response character) while still not reading input or firing OnCallCue (which
    /// would desync minigames that count Call cues to know whose turn it is — see
    /// Stage1WaveRideVisuals). See CallDirector.BuildLiveCall/SpawnDueCalls.
    [System.NonSerialized] public bool forceDemo;
}
