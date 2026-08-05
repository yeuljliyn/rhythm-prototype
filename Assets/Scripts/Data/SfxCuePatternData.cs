using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chart asset for decorative SFX cues (Stage1 and its sub-stages). A cue is a pure sound trigger
/// on a beat timeline — no player input, no judgement window, no Hit/Miss. Completely separate
/// from CallPatternData's call-and-response system.
/// </summary>
[CreateAssetMenu(menuName = "RhythmGame/Sfx Cue Pattern Data", fileName = "NewSfxCuePattern")]
public class SfxCuePatternData : ScriptableObject
{
    public string    chartName = "Segment 1";
    public AudioClip music;
    [Range(40f, 300f)] public float bpm = 128f;
    public float introDelay = 2f;

    [Tooltip("Q/W/E/R -> clip assignment used by the Sfx Cue Editor when stamping new notes " +
             "(index 0=Q, 1=W, 2=E, 3=R). Purely an authoring convenience saved with the chart — " +
             "changing a slot only affects notes stamped afterward, never notes already placed " +
             "(each SfxCueEntry keeps its own resolved clip).")]
    public AudioClip[] keySlots = new AudioClip[4];

    [Tooltip("Must stay sorted ascending by beat. The editor tool re-sorts after every add/move.")]
    public List<SfxCueEntry> cues = new List<SfxCueEntry>();

    public float BeatInterval               => 60f / Mathf.Max(1f, bpm);
    public float BeatToTime(float beat)     => beat * BeatInterval;
    public float TimeToBeat(float songTime) => songTime / BeatInterval;
}

/// <summary>
/// One instantaneous SFX trigger. 'clip' is copied in at authoring time (whatever the editor's
/// Q/W/E/R slot held when this note was stamped) — it is never a live reference to that slot, so
/// reassigning a key's slot later never retroactively changes notes already placed.
/// </summary>
[Serializable]
public class SfxCueEntry
{
    /// Which lane (0=Q, 1=W, 2=E, 3=R). Lanes are purely a visual/organizational split — cues on
    /// different lanes (or even the same lane) can freely overlap at the same beat.
    public int line;
    public float beat;
    public AudioClip clip;
}
