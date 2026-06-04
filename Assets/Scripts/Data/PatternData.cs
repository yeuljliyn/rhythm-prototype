using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chart asset for the direction-pattern game mode.
/// A chart is a sequence of DirectionGroups; each group is a list of arrow inputs
/// followed by a Space confirmation beat.
/// </summary>
[CreateAssetMenu(menuName = "RhythmGame/Pattern Data", fileName = "NewPattern")]
public class PatternData : ScriptableObject
{
    public string    chartName  = "Pattern 1";
    public AudioClip music;
    [Range(40f, 300f)] public float bpm = 128f;
    public float     introDelay = 2f;
    public List<DirectionGroup> groups = new List<DirectionGroup>();

    public float BeatInterval               => 60f / Mathf.Max(1f, bpm);
    public float BeatToTime(float beat)     => beat * BeatInterval;
    public float TimeToBeat(float songTime) => songTime / BeatInterval;
}

/// <summary>
/// One input segment: a sequence of direction presses, confirmed by a Space press.
/// spaceBeat is -1 if not yet confirmed (should not exist in saved data).
/// </summary>
[Serializable]
public class DirectionGroup
{
    public float                     spaceBeat = -1f;
    public List<DirectionInputEntry> inputs    = new List<DirectionInputEntry>();
}

/// <summary>
/// A single arrow-key press with its beat timestamp.
/// </summary>
[Serializable]
public struct DirectionInputEntry
{
    public ArrowDir direction;
    public float    beat;

    public DirectionInputEntry(ArrowDir dir, float beat)
    {
        direction = dir;
        this.beat = beat;
    }
}
