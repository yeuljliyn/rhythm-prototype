using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives note spawning from a ChapterData asset.
/// Notes travel horizontally from spawnX (right/boss side) to hitZoneX (left/player side).
/// </summary>
public class RhythmManager : MonoBehaviour
{
    static RhythmManager _instance;
    public static RhythmManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<RhythmManager>(); return _instance; }
        private set { _instance = value; }
    }

    [Header("Chapter (overrides built-in beatmap when assigned)")]
    public ChapterData chapter;

    [Header("Fallback timing — used only when no Chapter assigned")]
    public float bpm        = 128f;
    public float introDelay = 2f;

    [Header("Note Lane")]
    public float spawnAheadTime = 2.2f;
    public float spawnX         =  8.0f;   // right side (off-screen)
    public float hitZoneX       = -5.0f;   // left side (player)
    public float noteY          = -2.5f;   // lane height

    public float SongTime     { get; private set; }
    public float BeatInterval { get; private set; }

    int         _nextIdx;
    bool        _isRunning;
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
    }

    void Start()
    {
        float activeBpm   = chapter != null ? chapter.bpm        : bpm;
        float activeIntro = chapter != null ? chapter.introDelay : introDelay;

        BeatInterval = 60f / Mathf.Max(1f, activeBpm);
        SongTime     = -activeIntro;

        BuildSchedule();
        PlayMusic(activeIntro);
        _isRunning = true;
    }

    void Update()
    {
        if (!_isRunning) return;
        SongTime += Time.deltaTime;

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
        _schedule = new List<ScheduledNote>();

        if (chapter != null && chapter.notes != null && chapter.notes.Count > 0)
        {
            foreach (NoteEntry e in chapter.notes)
                _schedule.Add(new ScheduledNote { hitTime = chapter.BeatToTime(e.beat) });
        }
        else
        {
            foreach (int beat in BuiltInBeats)
                _schedule.Add(new ScheduledNote { hitTime = beat * BeatInterval });
        }

        _schedule.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
    }

    void PlayMusic(float delay)
    {
        if (chapter == null || chapter.music == null) return;

        _audio = gameObject.GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.clip        = chapter.music;
        _audio.playOnAwake = false;
        _audio.loop        = false;
        _audio.PlayScheduled(AudioSettings.dspTime + delay);
    }

    void SpawnNote(float hitTime)
    {
        var go = new GameObject("Note");
        go.AddComponent<Note>().Initialize(hitTime, spawnX, hitZoneX, noteY, spawnAheadTime);
    }
}
