using UnityEngine;

/// <summary>
/// 4-direction arrow rhythm engine.
/// Lane 0 = Left  (A / ←)  note travels RIGHT toward receptor
/// Lane 1 = Down  (S / ↓)  note travels UP    toward receptor
/// Lane 2 = Up    (W / ↑)  note travels DOWN  toward receptor
/// Lane 3 = Right (D / →)  note travels LEFT  toward receptor
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
    public float bpm           = 128f;
    public float noteSpeed     = 3.5f;
    public float spawnAheadTime = 2.5f;

    [Header("Prefab")]
    public GameObject notePrefab;

    [Header("Intro")]
    public float introDelay = 2f;

    // ── Lane definitions ─────────────────────────────────────────────
    public static readonly Vector3[] ReceptorPos = {
        new Vector3(-2.5f,  0f,   0f),   // 0  Left
        new Vector3( 0f,   -2.5f, 0f),   // 1  Down
        new Vector3( 0f,    2.5f, 0f),   // 2  Up
        new Vector3( 2.5f,  0f,   0f),   // 3  Right
    };

    public static readonly Vector3[] NoteDir = {
        Vector3.right,   // 0 Left  lane — travels right
        Vector3.up,      // 1 Down  lane — travels up
        Vector3.down,    // 2 Up    lane — travels down
        Vector3.left,    // 3 Right lane — travels left
    };

    // Z rotation: arrow points toward the KEY the player must press (right-facing sprite as base)
    // Lane 0 = Left  (A/←) → 180°  Lane 1 = Down (S/↓) → -90°
    // Lane 2 = Up   (W/↑) →  90°  Lane 3 = Right(D/→) →   0°
    static readonly float[] LaneRotation = { 180f, -90f, 90f, 0f };

    static readonly Color[] LaneColors = {
        new Color(0.3f, 0.6f, 1f),     // Left  — blue
        new Color(0.3f, 1f,  0.45f),   // Down  — green
        new Color(1f,  0.9f, 0.2f),    // Up    — yellow
        new Color(1f,  0.35f,0.35f),   // Right — red
    };

    // ── Runtime ──────────────────────────────────────────────────────
    public float SongTime { get; private set; }

    float beatInterval;
    int   nextNoteIndex;
    bool  isRunning;

    // ── Beatmap: { beat, lane }  lane: 0=Left 1=Down 2=Up 3=Right ───
    static readonly int[,] Beatmap =
    {
        {1,0},{2,3},{3,2},{4,1},
        {5,0},{6,2},{7,3},{8,1},
        {9,0},{10,0},{11,2},{12,3},
        {13,1},{14,3},{15,2},{16,0},
        {17,0},{17,3},{18,1},{18,2},
        {19,0},{20,3},{21,2},{22,1},
        {23,0},{23,2},{24,1},{24,3},
        {25,0},{26,2},{27,1},{28,3},
        {29,0},{29,2},{30,1},{30,3},
        {31,0},{31,3},{32,1},{32,2},
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
        if (notePrefab == null)
        {
            Debug.LogWarning("notePrefab not assigned on RhythmManager!");
            return;
        }

        Vector3 spawnPos = ReceptorPos[lane] + (-NoteDir[lane]) * noteSpeed * spawnAheadTime;
        spawnPos.z = 0f;

        var go  = Instantiate(notePrefab, spawnPos, Quaternion.Euler(0f, 0f, LaneRotation[lane]));
        var sr  = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = LaneColors[lane];

        go.GetComponent<Note>().Initialize(lane, hitTime, noteSpeed, NoteDir[lane]);
    }
}
