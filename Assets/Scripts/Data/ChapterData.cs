using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RhythmGame/Chapter Data", fileName = "NewChapter")]
public class ChapterData : ScriptableObject
{
    public string    chapterName = "Chapter 1";
    public AudioClip music;
    [Range(40f, 300f)] public float bpm         = 128f;
    public float     introDelay  = 2f;
    public List<NoteEntry> notes = new List<NoteEntry>();

    public float BeatInterval              => 60f / Mathf.Max(1f, bpm);
    public float BeatToTime(float beat)    => beat * BeatInterval;
    public float TimeToBeat(float songTime) => songTime / BeatInterval;
}
