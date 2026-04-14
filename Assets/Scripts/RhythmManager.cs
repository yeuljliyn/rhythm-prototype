using UnityEngine;

/// <summary>
/// Combat rhythm engine.
/// Notes (beat circles) spawn at random positions near the boss and shrink on approach.
/// The player presses SPACE / Z to hit each circle in time.
/// </summary>
public class RhythmManager : MonoBehaviour
{
    static RhythmManager _instance;
    public static RhythmManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<RhythmManager>(); return _instance; }
        private set { _instance = value; }
    }

    [Header("Timing")]
    public float bpm            = 128f;
    public float spawnAheadTime = 2.2f;

    [Header("Note Spawn Area — near the boss")]
    public Vector2 spawnCenter  = new Vector2(0f, 2.8f);
    public float   spawnRadius  = 1.6f;

    [Header("Intro")]
    public float introDelay = 2f;

    // ── Runtime ───────────────────────────────────────────────────────
    public float SongTime  { get; private set; }
    float beatInterval;
    int   nextNoteIndex;
    bool  isRunning;

    // ── Beatmap: beat numbers (one note per entry) ────────────────────
    static readonly int[] Beatmap =
    {
         1,  2,  3,  4,
         5,  6,  7,  8,
         9, 10, 11, 12,
        13, 14, 15, 16,
        17, 18, 19, 20,
        21, 22, 23, 24,
        25, 26, 27, 28,
        29, 30, 31, 32,
    };

    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);
    }

    void Start()
    {
        beatInterval = 60f / bpm;
        SongTime     = -introDelay;
        isRunning    = true;
    }

    void Update()
    {
        if (!isRunning) return;
        SongTime += Time.deltaTime;

        while (nextNoteIndex < Beatmap.Length)
        {
            float hitTime   = Beatmap[nextNoteIndex] * beatInterval;
            float spawnTime = hitTime - spawnAheadTime;

            if (SongTime >= spawnTime)
            {
                SpawnNote(hitTime);
                nextNoteIndex++;
            }
            else break;
        }
    }

    void SpawnNote(float hitTime)
    {
        // Random position within the spawn area around the boss
        Vector2 offset   = Random.insideUnitCircle * spawnRadius;
        var     spawnPos = new Vector3(spawnCenter.x + offset.x, spawnCenter.y + offset.y, 0f);

        var go = new GameObject("BeatCircle");
        go.transform.position = spawnPos;
        go.AddComponent<Note>().Initialize(hitTime, SongTime);
    }
}
