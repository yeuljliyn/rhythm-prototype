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

    [Tooltip("Judgement half-window in seconds. Total window = 2x this.")]
    public float hitWindow = 0.12f;

    [Tooltip("Extra global cutoff: calls whose cueBeat is before this beat are always demo-only " +
             "(shown, never judged), regardless of their per-entry CallRole. Use 0 for intro-less charts " +
             "where Call/Response is fully controlled per-entry via CallEntry.role.")]
    public float liveInputStartBeat = 0f;

    [Tooltip("Must stay sorted ascending by cueBeat. The editor tool re-sorts after every add/move.")]
    public List<CallEntry> calls = new List<CallEntry>();

    public float BeatInterval               => 60f / Mathf.Max(1f, bpm);
    public float BeatToTime(float beat)     => beat * BeatInterval;
    public float TimeToBeat(float songTime) => songTime / BeatInterval;
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
}
