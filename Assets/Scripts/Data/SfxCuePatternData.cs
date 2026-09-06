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

    [Tooltip("Sfx Cue Editor에서 새 노트를 찍을 때 쓰이는 Q/W/E/R -> 클립 배정 " +
             "(0=Q, 1=W, 2=E, 3=R). 차트와 함께 저장되는 순수 작업 편의용 설정 — " +
             "슬롯을 바꿔도 그 이후에 찍는 노트에만 적용되고, 이미 찍어둔 노트는 " +
             "영향받지 않음(SfxCueEntry마다 확정된 클립을 각자 따로 들고 있음).")]
    public AudioClip[] keySlots = new AudioClip[4];

    [Tooltip("반드시 beat 오름차순으로 정렬된 상태를 유지해야 함. 추가/이동할 때마다 에디터 " +
             "툴이 자동으로 재정렬함.")]
    public List<SfxCueEntry> cues = new List<SfxCueEntry>();

    public float BeatInterval               => 60f / Mathf.Max(1f, bpm);
    public float BeatToTime(float beat)     => beat * BeatInterval;
    public float TimeToBeat(float songTime) => songTime / BeatInterval;
}

/// <summary>
/// One instantaneous SFX trigger. 'clip' is copied in at authoring time (whatever the editor's
/// Q/W/E/R slot held when this note was stamped) — it is never a live reference to that slot, so
/// reassigning a key's slot later never retroactively changes notes already placed.
///
/// Also reused by CallPatternData.sfxCues (the Chart Editor's SFX lane) — there it's a single
/// lane so 'line' stays 0, and CallNoteSfxPlayer is the runtime consumer instead of SfxCuePlayer.
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
