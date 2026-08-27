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

    // Scheduling lives in OnEnable (fires every activation), not Start (fires exactly once, ever) —
    // this matters specifically for Stage1PracticeGate's practice segments: PracticeSegment1/2 are
    // authored ACTIVE in the scene, so their Awake/OnEnable/Start all fire the instant the scene
    // loads, before Stage1PracticeGate.Start() gets a chance to run its own SetActive(false) pass
    // on every segment. If scheduling were still in Start(), _scheduledMusicStartDsp would get
    // permanently anchored to that very first (bogus, pre-gate) moment — then however many real
    // seconds the player spends reading the segment's intro caption (StartSegmentPlay only
    // SetActive(true)s the segment again once they confirm it) would ALL silently count as elapsed
    // SongTime by the time this segment is reactivated for real, since SetActive(true) on an
    // already-Started object only re-fires OnEnable, never Start again. The very next Update() would
    // then see SongTime already far advanced and SpawnDueCalls would fire an entire burst of
    // already-due notes in one frame — confirmed reported: "미리보기 로직이 이미 진행되다가 [인트로
    // 대사] 클릭하면 갑자기 개미5로 와버림". Rescheduling in OnEnable instead means every
    // reactivation — including this first bogus one — re-anchors from "right now", so only the LAST
    // OnEnable before real gameplay (the confirmed one) ends up mattering. OnDisable cancels
    // whatever got scheduled so a stray PlayScheduled from that bogus first activation can't still
    // fire audio later while the segment sits inactive.
    void OnEnable()
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

    void OnDisable()
    {
        _isRunning = false;
        if (_audio != null) { _audio.Stop(); _audio.clip = null; }
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
