using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime playback for SfxCuePatternData — fires each cue's AudioClip at its beat with no
/// judgement or input involved (unlike CallDirector). Piggybacks on an already-running CallClock's
/// SongTime instead of owning its own AudioSource/schedule, so it can never drift from whatever
/// song is actually playing the scene (the same clock driving that sub-stage's CallPatternData).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SfxCuePlayer : MonoBehaviour
{
    [SerializeField] CallClock clock;
    [SerializeField] SfxCuePatternData pattern;

    AudioSource _audio;
    List<SfxCueEntry> _cues;
    int _nextIdx;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _cues = pattern != null ? new List<SfxCueEntry>(pattern.cues) : new List<SfxCueEntry>();
        _cues.Sort((a, b) => a.beat.CompareTo(b.beat));
    }

    void Update()
    {
        if (clock == null || pattern == null) return;
        if (clock.IsPaused) return; // frozen SongTime means no cue should fire yet

        float t = clock.SongTime;
        while (_nextIdx < _cues.Count)
        {
            var cue = _cues[_nextIdx];
            if (t < pattern.BeatToTime(cue.beat)) break;

            if (cue.clip != null) _audio.PlayOneShot(cue.clip);
            _nextIdx++;
        }
    }
}
