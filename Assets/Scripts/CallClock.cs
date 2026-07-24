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
    bool   _isPaused;
    double _pauseStartDsp;
    float  _frozenSongTime;

    public CallPatternData Pattern => pattern;
    public bool  IsPaused => _isPaused;

    // dspTime keeps advancing in real time regardless of Time.timeScale, so a plain
    // (dspTime - epoch) formula would let SongTime jump forward across a pause. While paused,
    // report the frozen value instead; Resume() shifts the epoch so it picks up right where it left off.
    public float SongTime => _isRunning
        ? (_isPaused ? _frozenSongTime : (float)(AudioSettings.dspTime - _scheduledMusicStartDsp))
        : 0f;

    public bool IsMusicDone =>
        _isRunning && _audio != null && _audio.clip != null && SongTime >= _audio.clip.length;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop        = false;

        Time.timeScale = 1f; // 이전 플레이 세션에서 timeScale=0이 남아있을 경우 초기화 (RhythmManager와 동일한 방어 로직)
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

    public void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _frozenSongTime = SongTime;
        _isPaused       = true;
        _pauseStartDsp  = AudioSettings.dspTime;
        if (_audio != null && _audio.isPlaying) _audio.Pause();
    }

    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;
        _scheduledMusicStartDsp += AudioSettings.dspTime - _pauseStartDsp; // shift epoch by the paused duration
        _isPaused = false;
        if (_audio != null) _audio.UnPause();
    }
}
