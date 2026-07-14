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

    [Tooltip("Calls whose cueBeat is before this beat are demo-only: shown, never judged, no input read.")]
    public float liveInputStartBeat = 0f;

    [Tooltip("Must stay sorted ascending by cueBeat. The editor tool re-sorts after every add/move.")]
    public List<CallEntry> calls = new List<CallEntry>();

    public float BeatInterval               => 60f / Mathf.Max(1f, bpm);
    public float BeatToTime(float beat)     => beat * BeatInterval;
    public float TimeToBeat(float songTime) => songTime / BeatInterval;
}

public enum CallType { Tap, Hold }

[Serializable]
public class CallEntry
{
    public CallType type;

    /// Tap: the single judged beat (window = cueBeat ± hitWindow).
    /// Hold: the beat at which the "start holding" cue is shown (not itself judged).
    public float cueBeat;

    /// Hold only. The judged release beat (window = releaseBeat ± hitWindow). Must be > cueBeat.
    public float releaseBeat;
}
