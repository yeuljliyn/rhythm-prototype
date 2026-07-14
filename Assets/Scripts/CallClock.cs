using UnityEngine;

/// <summary>
/// Minimal dspTime-based song clock for the call-and-response engine.
/// SongTime == 0 when music starts; negative during intro delay.
/// Deliberately duplicates RhythmManager's formula instead of depending on it,
/// since RhythmManager is hard-typed to PatternData (the 4-direction chart asset).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CallClock : MonoBehaviour
{
    [SerializeField] CallPatternData pattern;

    AudioSource _audio;
    double _scheduledMusicStartDsp;
    bool   _isRunning;

    public CallPatternData Pattern => pattern;
    public float SongTime => _isRunning ? (float)(AudioSettings.dspTime - _scheduledMusicStartDsp) : 0f;

    public bool IsMusicDone =>
        _isRunning && _audio != null && _audio.clip != null && SongTime >= _audio.clip.length;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop        = false;
    }

    void Start()
    {
        float introDelay = pattern != null ? pattern.introDelay : 2f;
        _scheduledMusicStartDsp = AudioSettings.dspTime + introDelay;

        if (pattern != null && pattern.music != null)
        {
            _audio.clip = pattern.music;
            _audio.PlayScheduled(_scheduledMusicStartDsp);
        }
        _isRunning = true;
    }

    public void StopMusic()
    {
        _isRunning = false;
        if (_audio != null && _audio.isPlaying) _audio.Stop();
    }
}
