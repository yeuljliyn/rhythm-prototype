using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages one calibration session.
/// Pre-generates a click-track AudioClip so metronome beats are perfectly
/// DSP-scheduled. Collects direction-matched timing samples, filters outliers,
/// and applies a trimmed-mean error to CalibrationManager on completion.
/// </summary>
public class CalibrationSession : MonoBehaviour
{
    [SerializeField] float _bpm           = 100f;
    [SerializeField] int   _warmupBeats   = 4;
    [SerializeField] int   _targetSamples = 14;
    [SerializeField] float _maxErrorMs    = 180f;
    [SerializeField] float _trimFraction  = 0.15f;  // cut top/bottom 15% before mean

    static readonly ArrowDir[] DirPattern =
        { ArrowDir.Up, ArrowDir.Down, ArrowDir.Left, ArrowDir.Right };

    double _sessionStartDspTime;
    double _beatInterval;
    bool   _active;
    int    _lastInputBeat = -1;   // one sample per beat

    readonly List<float> _validSamples = new List<float>();
    int _earlyCount;
    int _lateCount;
    int _excludedCount;

    AudioSource _audio;

    // ── Read-only state (for UI / visual) ────────────────────────────────
    public bool     IsActive     => _active;
    public bool     IsComplete   { get; private set; }
    public double   SessionStartDspTime => _sessionStartDspTime;
    public double   BeatInterval        => _beatInterval;
    public int      WarmupBeats         => _warmupBeats;
    public int      TargetSamples       => _targetSamples;

    public int CurrentBeatIndex =>
        _active ? Mathf.Max(0, (int)((AudioSettings.dspTime - _sessionStartDspTime) / _beatInterval)) : 0;

    public bool     IsWarmup       => CurrentBeatIndex < _warmupBeats;
    public ArrowDir CurrentTargetDir =>
        DirPattern[Mathf.Max(0, CurrentBeatIndex - _warmupBeats) % DirPattern.Length];

    public int ValidSampleCount  => _validSamples.Count;
    public int EarlyCount        => _earlyCount;
    public int LateCount         => _lateCount;
    public int ExcludedCount     => _excludedCount;

    // ── Results set after completion ─────────────────────────────────────
    public float LastAverageErrorMs { get; private set; }
    public float FinalOffsetMs      { get; private set; }

    public System.Action OnSessionComplete;

    void Awake()
    {
        _audio             = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.volume      = 0.85f;
    }

    public void Begin()
    {
        IsComplete     = false;
        _active        = true;
        _lastInputBeat = -1;
        _validSamples.Clear();
        _earlyCount = _lateCount = _excludedCount = 0;

        _beatInterval = 60.0 / Mathf.Max(1f, _bpm);

        // Generate full click track so all beats are perfectly DSP-scheduled
        int totalBeats = _warmupBeats + _targetSamples + 4;
        _audio.clip    = GenerateClickTrack(totalBeats, (float)_beatInterval);

        // 200 ms lead-in before beat 0 so the first click isn't clipped
        _sessionStartDspTime = AudioSettings.dspTime + 0.2;
        _audio.PlayScheduled(_sessionStartDspTime);
    }

    public void Stop()
    {
        _active = false;
        _audio?.Stop();
    }

    void Update()
    {
        if (!_active) return;

        // Auto-finish when enough samples collected or track runs out
        if (_validSamples.Count >= _targetSamples ||
            CurrentBeatIndex >= _warmupBeats + _targetSamples + 2)
        {
            Finish();
            return;
        }

        HandleInput();
    }

    void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        ArrowDir? pressed = null;
        if      (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame)    pressed = ArrowDir.Up;
        else if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame)    pressed = ArrowDir.Down;
        else if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)    pressed = ArrowDir.Left;
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)    pressed = ArrowDir.Right;

        if (!pressed.HasValue) return;

        int beat = CurrentBeatIndex;
        if (beat < _warmupBeats)    return;  // warmup: ignore
        if (beat == _lastInputBeat) return;  // one sample per beat

        // Wrong direction: skip, mark beat consumed
        if (pressed.Value != CurrentTargetDir)
        {
            _lastInputBeat = beat;
            return;
        }

        _lastInputBeat = beat;
        RecordSample(beat);
    }

    void RecordSample(int nearestBeat)
    {
        double rawInputTime      = AudioSettings.dspTime - _sessionStartDspTime;
        // adjustedInputTime accounts for the current offset so that
        // errorMs reflects REMAINING error, enabling cumulative convergence
        double adjustedInputTime = rawInputTime +
            (CalibrationManager.Instance?.JudgementOffsetMs ?? 0f) / 1000.0;
        double targetBeatTime    = nearestBeat * _beatInterval;
        float  errorMs           = (float)((adjustedInputTime - targetBeatTime) * 1000.0);

        if (Mathf.Abs(errorMs) > _maxErrorMs) { _excludedCount++; return; }

        _validSamples.Add(errorMs);
        if (errorMs > 0f) _lateCount++;
        else              _earlyCount++;
    }

    void Finish()
    {
        if (!_active) return;
        _active    = false;
        IsComplete = true;
        _audio.Stop();

        if (_validSamples.Count == 0)
        {
            LastAverageErrorMs = 0f;
            FinalOffsetMs      = CalibrationManager.Instance?.JudgementOffsetMs ?? 0f;
            OnSessionComplete?.Invoke();
            return;
        }

        LastAverageErrorMs = CalcTrimmedMean(_validSamples, _trimFraction);
        // judgementOffsetMs -= averageErrorMs * correctionStrength
        CalibrationManager.Instance?.ApplyJudgementCalibrationResult(LastAverageErrorMs);
        FinalOffsetMs = CalibrationManager.Instance?.JudgementOffsetMs ?? 0f;
        OnSessionComplete?.Invoke();
    }

    static float CalcTrimmedMean(List<float> values, float trimFraction)
    {
        var sorted = new List<float>(values);
        sorted.Sort();
        int   cut = Mathf.FloorToInt(sorted.Count * trimFraction);
        int   end = sorted.Count - cut;
        float sum = 0f;
        for (int i = cut; i < end; i++) sum += sorted[i];
        int cnt = end - cut;
        return cnt > 0 ? sum / cnt : 0f;
    }

    // Pre-generates N click beats into a single AudioClip for accurate DSP scheduling
    static AudioClip GenerateClickTrack(int beats, float beatIntervalSec)
    {
        const int   sampleRate = 44100;
        const float clickDur   = 0.06f;
        const float clickFreq  = 880f;
        int clickSamples = (int)(clickDur * sampleRate);
        int totalSamples = (int)(beats * beatIntervalSec * sampleRate) + sampleRate;
        var data         = new float[totalSamples];

        for (int b = 0; b < beats; b++)
        {
            int offset = (int)(b * beatIntervalSec * sampleRate);
            for (int i = 0; i < clickSamples; i++)
            {
                int s = offset + i;
                if (s >= totalSamples) break;
                float t   = (float)i / sampleRate;
                float env = 1f - t / clickDur;
                data[s]  += Mathf.Sin(2 * Mathf.PI * clickFreq * t) * env * 0.8f;
            }
        }

        var clip = AudioClip.Create("CalibClickTrack", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
