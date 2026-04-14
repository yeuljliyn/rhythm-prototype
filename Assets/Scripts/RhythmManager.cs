using UnityEngine;

/// <summary>
/// Combat rhythm engine.
/// Normal notes (white) → SPACE / Z.
/// Special notes (colored + arrow) → matching direction key.
/// About 25 % of notes are specials, giving a "special attack" feel.
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
    public Vector2 spawnCenter = new Vector2(0f, 2.8f);
    public float   spawnRadius = 1.6f;

    [Header("Intro")]
    public float introDelay = 2f;

    // ── Runtime ───────────────────────────────────────────────────────
    public float SongTime  { get; private set; }
    float beatInterval;
    int   nextNoteIndex;
    bool  isRunning;

    // ── Beatmap: { beat, noteType }
    // noteType: 0=Normal  1=ArrowLeft  2=ArrowDown  3=ArrowUp  4=ArrowRight
    // Special notes appear roughly every 4 beats (~25 %) for a "special attack" cadence.
    static readonly int[,] Beatmap =
    {
        { 1, 0},{ 2, 0},{ 3, 0},{ 4, 1},   // beat 4  → Left  (special)
        { 5, 0},{ 6, 0},{ 7, 0},{ 8, 3},   // beat 8  → Up    (special)
        { 9, 0},{10, 0},{11, 0},{12, 4},    // beat 12 → Right (special)
        {13, 0},{14, 0},{15, 0},{16, 2},    // beat 16 → Down  (special)
        {17, 0},{18, 0},{19, 1},{20, 0},    // beat 19 → Left  (special mid-phrase)
        {21, 0},{22, 0},{23, 3},{24, 0},    // beat 23 → Up    (special)
        {25, 0},{26, 4},{27, 0},{28, 0},    // beat 26 → Right (special)
        {29, 0},{30, 2},{31, 0},{32, 0},    // beat 30 → Down  (special)
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
                SpawnNote(hitTime, (NoteType)Beatmap[nextNoteIndex, 1]);
                nextNoteIndex++;
            }
            else break;
        }
    }

    void SpawnNote(float hitTime, NoteType type)
    {
        Vector2 offset   = Random.insideUnitCircle * spawnRadius;
        var     spawnPos = new Vector3(spawnCenter.x + offset.x, spawnCenter.y + offset.y, 0f);

        var go = new GameObject("BeatCircle");
        go.transform.position = spawnPos;
        go.AddComponent<Note>().Initialize(hitTime, SongTime, type);
    }
}
