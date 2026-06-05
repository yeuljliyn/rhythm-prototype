using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives note spawning. SongTime is dspTime-based for accurate audio synchronization.
/// SongTime == 0 when the music starts; negative during the intro delay.
/// </summary>
public class RhythmManager : MonoBehaviour
{
    static RhythmManager _instance;
    public static RhythmManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<RhythmManager>(); return _instance; }
        private set { _instance = value; }
    }

    [Header("Pattern (direction-pattern chart)")]
    public PatternData chapter;   // field name kept so scene reference survives

    [Header("Fallback timing — used only when no Pattern assigned")]
    public float bpm        = 128f;
    public float introDelay = 2f;

    [Header("Note Lane (unused — direction-pattern mode)")]
    public float spawnAheadTime = 2.2f;
    public float spawnX         =  8.0f;
    public float hitZoneX       = -5.0f;
    public float noteY          = -2.5f;

    public PatternData Pattern => chapter;

    // SongTime == 0 when music starts; negative during intro
    public float  SongTime  => _isRunning ? (float)(AudioSettings.dspTime - _scheduledMusicStartDsp) : 0f;
    // Double-precision version for offset calculations in Note.TryHit
    public double SongTimeD => _isRunning ? AudioSettings.dspTime - _scheduledMusicStartDsp : 0.0;

    public float BeatInterval   { get; private set; }
    public bool  IsScheduleDone => _nextIdx >= _schedule.Count;

    // 음악 클립이 끝까지 재생됐는지 여부
    public bool IsMusicDone
    {
        get
        {
            if (!_isRunning) return false;
            if (_audio == null || _audio.clip == null) return IsScheduleDone;
            return SongTime >= _audio.clip.length;
        }
    }

    int    _nextIdx;
    bool   _isRunning;
    double _scheduledMusicStartDsp;
    AudioSource _audio;

    struct ScheduledNote { public float hitTime; }
    List<ScheduledNote> _schedule = new List<ScheduledNote>();

    static readonly int[] BuiltInBeats =
    {
         1,  2,  3,  4,  5,  6,  7,  8,
         9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24,
        25, 26, 27, 28, 29, 30, 31, 32,
    };

    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);

        Time.timeScale = 1f;  // 이전 플레이 세션에서 timeScale=0이 남아있을 경우 초기화
    }

    void Start()
    {
        float activeBpm   = chapter != null ? chapter.bpm        : bpm;
        float activeIntro = chapter != null ? chapter.introDelay : introDelay;

        BeatInterval = 60f / Mathf.Max(1f, activeBpm);

        // Music is scheduled activeIntro seconds from now.
        // SongTime = dspTime - _scheduledMusicStartDsp, so:
        //   at Start():   SongTime = -activeIntro  (still in intro)
        //   when music plays: SongTime = 0
        _scheduledMusicStartDsp = AudioSettings.dspTime + activeIntro;

        BuildSchedule();
        PlayMusic(activeIntro);
        _isRunning = true;
    }

    void Update()
    {
        if (!_isRunning) return;

        while (_nextIdx < _schedule.Count)
        {
            float hitTime   = _schedule[_nextIdx].hitTime;
            float spawnTime = hitTime - spawnAheadTime;
            if (SongTime < spawnTime) break;
            SpawnNote(hitTime);
            _nextIdx++;
        }
    }

    void BuildSchedule()
    {
        _schedule = new List<ScheduledNote>(); // notes disabled — direction-pattern mode
    }

    void PlayMusic(float delay)
    {
        if (chapter == null || chapter.music == null) return;

        _audio = gameObject.GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.clip        = chapter.music;
        _audio.playOnAwake = false;
        _audio.loop        = false;
        _audio.PlayScheduled(_scheduledMusicStartDsp);
    }

    void SpawnNote(float hitTime)
    {
        // Note spawning disabled — game uses direction-pattern mode
    }
}
