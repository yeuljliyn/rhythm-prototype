using UnityEngine;

/// <summary>
/// Core BPM engine.  Spawns notes from a hardcoded beatmap.
/// Lane 0 = ground (Z key / Down Arrow)
/// Lane 1 = air    (X key / Up Arrow)
/// </summary>
public class RhythmManager : MonoBehaviour
{
    public static RhythmManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Timing")]
    public float bpm       = 128f;
    public float noteSpeed = 9f;      // units per second

    [Header("Scene References")]
    public Transform noteSpawnPoint;  // right edge (X ≈ +11)
    public Transform hitLine;         // player hit zone (X ≈ -5)

    [Header("Prefabs")]
    public GameObject groundNotePrefab;
    public GameObject airNotePrefab;

    [Header("Intro")]
    public float introDelay = 2f;   // seconds of silence before first beat

    // ── Runtime ────────────────────────────────────────────────────
    public float SongTime { get; private set; }

    float beatInterval;
    float spawnAheadTime;
    int   nextNoteIndex;
    bool  isRunning;

    // ── Beatmap: { beat, lane }  (beat 1-based, lane 0=ground 1=air) ──
    // 32-beat pattern — cyberpunk battle rhythm
    static readonly int[,] Beatmap =
    {
        {1,0},{2,0},{3,1},{4,0},
        {5,0},{6,1},{7,0},{8,1},
        {9,0},{10,0},{11,1},{12,0},
        {13,1},{14,0},{15,0},{16,1},
        {17,0},{18,1},{19,0},{20,0},
        {21,1},{22,0},{23,1},{24,0},
        {25,0},{25,1},{26,0},{27,1},
        {28,0},{29,0},{30,1},{31,0},
        {32,0},{32,1},
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        beatInterval    = 60f / bpm;
        float distance  = noteSpawnPoint.position.x - hitLine.position.x;
        spawnAheadTime  = distance / noteSpeed;
        SongTime        = -introDelay;   // count up from negative → notes have lead time
        isRunning       = true;
    }

    void Update()
    {
        if (!isRunning) return;

        SongTime += Time.deltaTime;

        // Spawn all notes whose spawn-time has arrived
        int total = Beatmap.GetLength(0);
        while (nextNoteIndex < total)
        {
            float hitTime   = Beatmap[nextNoteIndex, 0] * beatInterval;
            float spawnTime = hitTime - spawnAheadTime;

            if (SongTime >= spawnTime)
            {
                SpawnNote(Beatmap[nextNoteIndex, 1], hitTime);
                nextNoteIndex++;
            }
            else break;
        }
    }

    void SpawnNote(int lane, float hitTime)
    {
        // Y position: ground lane = -2.2, air lane = 0.8
        float y = lane == 0 ? -2.2f : 0.8f;
        var   pos    = new Vector3(noteSpawnPoint.position.x, y, 0f);
        var   prefab = lane == 0 ? groundNotePrefab : airNotePrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Note prefab not assigned on RhythmManager!");
            return;
        }

        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.GetComponent<Note>().Initialize(lane, hitTime, noteSpeed);
    }
}
