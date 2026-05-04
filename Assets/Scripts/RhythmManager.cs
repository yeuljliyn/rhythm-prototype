using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives note spawning from a ChapterData asset.
/// Falls back to the built-in beatmap when no ChapterData is assigned,
/// so the existing SampleScene still works without any setup.
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
    public float bpm            = 128f;
    public float spawnAheadTime = 2.2f;
    public float introDelay     = 2f;

    [Header("Note spawn area")]
    public Vector2 spawnCenter = new Vector2(0f, 2.8f);
    public float   spawnRadius = 1.6f;

    // ── Runtime ───────────────────────────────────────────────────────
    public float SongTime { get; private set; }

    float       _beatInterval;
    int         _nextIdx;
    bool        _isRunning;
    AudioSource _audio;

    struct ScheduledNote { public float hitTime; public NoteType type; }
    List<ScheduledNote> _schedule;

    // ── Built-in beatmap (fallback) ───────────────────────────────────
    static readonly int[,] BuiltInBeatmap =
    {
        { 1, 0},{ 2, 0},{ 3, 0},{ 4, 1},
        { 5, 0},{ 6, 0},{ 7, 0},{ 8, 3},
        { 9, 0},{10, 0},{11, 0},{12, 4},
        {13, 0},{14, 0},{15, 0},{16, 2},
        {17, 0},{18, 0},{19, 1},{20, 0},
        {21, 0},{22, 0},{23, 3},{24, 0},
        {25, 0},{26, 4},{27, 0},{28, 0},
        {29, 0},{30, 2},{31, 0},{32, 0},
    };

    // ── Unity ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);
    }

    void Start()
    {
        float activeBpm   = chapter != null ? chapter.bpm        : bpm;
        float activeIntro = chapter != null ? chapter.introDelay : introDelay;

        _beatInterval = 60f / Mathf.Max(1f, activeBpm);
        SongTime      = -activeIntro;
        _isRunning    = true;

        BuildSchedule();
        PlayMusic(activeIntro);
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

            SpawnNote(hitTime, _schedule[_nextIdx].type);
            _nextIdx++;
        }
    }

    // ── Internals ─────────────────────────────────────────────────────
    void BuildSchedule()
    {
        _schedule = new List<ScheduledNote>();

        if (chapter != null && chapter.notes != null && chapter.notes.Count > 0)
        {
            foreach (NoteEntry e in chapter.notes)
                _schedule.Add(new ScheduledNote
                {
                    hitTime = chapter.BeatToTime(e.beat),
                    type    = e.type
                });
        }
        else
        {
            int len = BuiltInBeatmap.GetLength(0);
            for (int i = 0; i < len; i++)
                _schedule.Add(new ScheduledNote
                {
                    hitTime = BuiltInBeatmap[i, 0] * _beatInterval,
                    type    = (NoteType)BuiltInBeatmap[i, 1]
                });
        }

        _schedule.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
    }

    void PlayMusic(float delay)
    {
        if (chapter == null || chapter.music == null) return;

        _audio      = gameObject.GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        _audio.clip        = chapter.music;
        _audio.playOnAwake = false;
        _audio.loop        = false;
        _audio.PlayScheduled(AudioSettings.dspTime + delay);
    }

    void SpawnNote(float hitTime, NoteType type)
    {
        Vector2 offset   = Random.insideUnitCircle * spawnRadius;
        var     spawnPos = new Vector3(spawnCenter.x + offset.x,
                                       spawnCenter.y + offset.y, 0f);
        var go = new GameObject("BeatCircle");
        go.transform.position = spawnPos;
        go.AddComponent<Note>().Initialize(hitTime, SongTime, type);
    }
}
