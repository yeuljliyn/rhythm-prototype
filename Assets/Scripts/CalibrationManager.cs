using UnityEngine;

/// <summary>
/// Global judgement offset. DontDestroyOnLoad singleton.
/// judgementOffsetMs is the only persisted calibration value.
/// Use GetAdjustedInputTimeS() at the moment of hit judgement.
/// </summary>
public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance { get; private set; }

    const string PrefKey = "JudgementOffsetMs";
    const float  MinMs   = -200f;
    const float  MaxMs   =  200f;

    [SerializeField] float _correctionStrength = 0.75f;

    public float JudgementOffsetMs { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[CalibrationManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<CalibrationManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void Load() =>
        JudgementOffsetMs = PlayerPrefs.GetFloat(PrefKey, 0f);

    public void Save()
    {
        PlayerPrefs.SetFloat(PrefKey, JudgementOffsetMs);
        PlayerPrefs.Save();
    }

    public void ResetCalibration()
    {
        JudgementOffsetMs = 0f;
        Save();
    }

    /// <summary>
    /// averageErrorMs > 0 means player tapped late → offset shifts negative.
    /// Partial convergence via _correctionStrength prevents over-correction.
    /// </summary>
    public void ApplyJudgementCalibrationResult(float averageErrorMs)
    {
        JudgementOffsetMs -= averageErrorMs * _correctionStrength;
        JudgementOffsetMs  = Mathf.Clamp(JudgementOffsetMs, MinMs, MaxMs);
        Save();
    }

    /// <summary>Manual ±N ms adjustment (e.g. from +5/-5 buttons).</summary>
    public void AdjustJudgementOffset(float deltaMsHuman)
    {
        JudgementOffsetMs  = Mathf.Clamp(JudgementOffsetMs + deltaMsHuman, MinMs, MaxMs);
        Save();
    }

    /// <summary>
    /// Returns adjusted input time in seconds for hit judgement.
    /// rawSongTimeS: seconds since song start (dspTime-based).
    /// </summary>
    public double GetAdjustedInputTimeS(double rawSongTimeS) =>
        rawSongTimeS + JudgementOffsetMs / 1000.0;
}
