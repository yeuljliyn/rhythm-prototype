using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1-1 (파도타기) visual glue. Five ants stand in a row; the clown/wave rides across them
/// left to right. Ant1..Ant4 are Call-role notes (each shows the rhythm by raising its arm in turn),
/// Ant5 is the single Response-role note the player has to hit.
///
/// CallDirector's events carry no "which note" payload (see CallDirector.OnCallCue), so which ant
/// just cued is inferred purely from firing order: OnCallCue fires once per Call-role note in
/// chronological order, and this chart is authored (Call Map Editor, hand-placed — no Z echo, since
/// Z would mirror the call count 1:1 into an equal-count response instead of collapsing 4 calls into
/// a single response) with exactly 4 Call notes per group. So a running counter mod 4 recovers the
/// ant index (0..3) without CallDirector needing to know anything about ants.
///
/// The clown's horizontal position is driven directly off CallClock.SongTime against precomputed
/// cueBeat timestamps (BuildWaypoints/Update) instead of a fixed-duration coroutine kicked off by
/// CallDirector's OnCallCue/OnPhaseChanged events. Those events fire at spawn time — cueTime minus
/// the chart's hitWindow, i.e. slightly *before* the judged beat — so a short fixed-duration glide
/// from there sits idle for the rest of the beat (looks jerky/discrete), while a glide stretched to
/// fill the whole beat overshoots past the actual judged beat (arrives late, after the hit window has
/// already closed). Waypoint-based motion is always in transit and always lands exactly on the real
/// cueBeat (using the chart's own cueBeat->seconds conversion, not the event's fire time), so it's
/// both continuous and lands on the same beat the original snap-move used to land close to.
/// </summary>
public class Stage1WaveRideVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    // Source of truth for both SongTime (per-frame clown position) and the chart's cueBeats
    // (waypoint timestamps) — see BuildWaypoints/Update.
    [SerializeField] CallClock clock;

    [Header("Ant1..Ant4 = Call, Ant5 (last) = Response")]
    [SerializeField] Image[] antImages = new Image[5];
    [SerializeField] Sprite callAntIdleSprite;
    [SerializeField] Sprite callAntRaisedSprite;
    [SerializeField] Sprite responseAntIdleSprite;
    [SerializeField] Sprite responseAntRaisedSprite;

    [SerializeField] RectTransform clownRect;

    [SerializeField] float antPulseDuration = 0.25f;

    struct Waypoint { public float time; public int antIndex; }
    List<Waypoint> _waypoints;
    int _cachedSegment; // index of the last-known "next waypoint" — search resumes from here

    readonly Coroutine[] _antPulses = new Coroutine[5];
    int _callCueCount;

    void OnEnable()  { Bind(callDirector); }
    void OnDisable() { Unbind(callDirector); }

    void Start()
    {
        for (int i = 0; i < antImages.Length; i++)
            if (antImages[i] != null) antImages[i].sprite = IdleSpriteFor(i);

        BuildWaypoints();

        if (clownRect != null && _waypoints.Count > 0)
            SetClownX(AntX(_waypoints[0].antIndex));
        else if (clownRect != null && antImages.Length > 0 && antImages[0] != null)
            SetClownX(AntX(0));
    }

    void Update()
    {
        if (clock == null || clownRect == null || _waypoints == null || _waypoints.Count == 0) return;

        float t = clock.SongTime;

        // Advance the cached search forward only — SongTime only increases (except while paused,
        // where it's frozen and this is a harmless no-op).
        while (_cachedSegment < _waypoints.Count && _waypoints[_cachedSegment].time <= t)
            _cachedSegment++;

        if (_cachedSegment == 0) { SetClownX(AntX(_waypoints[0].antIndex)); return; }
        if (_cachedSegment >= _waypoints.Count) { SetClownX(AntX(_waypoints[_waypoints.Count - 1].antIndex)); return; }

        var prev = _waypoints[_cachedSegment - 1];
        var next = _waypoints[_cachedSegment];
        float frac = Mathf.Clamp01((t - prev.time) / Mathf.Max(0.0001f, next.time - prev.time));
        SetClownX(Mathf.SmoothStep(AntX(prev.antIndex), AntX(next.antIndex), frac));
    }

    /// Switches which CallDirector drives these visuals — same rebind pattern as
    /// Stage1WaterPourVisuals, kept for consistency if 1-1 ever grows a tutorial gate.
    public void BindDirector(CallDirector newDirector)
    {
        Unbind(callDirector);
        callDirector = newDirector;
        Bind(callDirector);
        BuildWaypoints();
    }

    void Bind(CallDirector d)
    {
        if (d == null) return;
        d.OnCallCue       += HandleCallCue;
        d.OnResponsePress += HandleResponsePress;
    }

    void Unbind(CallDirector d)
    {
        if (d == null) return;
        d.OnCallCue       -= HandleCallCue;
        d.OnResponsePress -= HandleResponsePress;
    }

    void HandleCallCue()
    {
        int antIndex = _callCueCount % 4; // relies on exactly 4 Call notes per group — see class doc
        _callCueCount++;
        PulseAnt(antIndex);
    }

    void HandleResponsePress()
    {
        PulseAnt(4);
    }

    /// Precomputes, in chart order, which ant the clown should be standing on at each cueBeat —
    /// Call-role entries cycle ant0..ant3 (same %4 logic as HandleCallCue), Response-role entries
    /// are always ant4. Rebuilt whenever the driving CallClock/pattern changes.
    void BuildWaypoints()
    {
        _waypoints = new List<Waypoint>();
        _cachedSegment = 0;
        _callCueCount = 0;

        var pattern = clock != null ? clock.Pattern : null;
        if (pattern == null || pattern.calls == null) return;

        var calls = new List<CallEntry>(pattern.calls);
        calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));

        int callCounter = 0;
        foreach (var c in calls)
        {
            int antIndex = c.role == CallRole.Call ? (callCounter++ % 4) : 4;
            _waypoints.Add(new Waypoint { time = pattern.BeatToTime(c.cueBeat), antIndex = antIndex });
        }
    }

    void PulseAnt(int index)
    {
        if (index < 0 || index >= antImages.Length || antImages[index] == null) return;
        if (_antPulses[index] != null) StopCoroutine(_antPulses[index]);
        _antPulses[index] = StartCoroutine(PulseAntRoutine(index));
    }

    IEnumerator PulseAntRoutine(int index)
    {
        var img = antImages[index];
        img.sprite = RaisedSpriteFor(index);
        yield return new WaitForSeconds(antPulseDuration);
        img.sprite = IdleSpriteFor(index);
        _antPulses[index] = null;
    }

    // Ant1..Ant4 (index 0-3, Call role) share one look; Ant5 (index 4, Response role) is the
    // player's own distinct character.
    Sprite IdleSpriteFor(int index)   => index == 4 ? responseAntIdleSprite   : callAntIdleSprite;
    Sprite RaisedSpriteFor(int index) => index == 4 ? responseAntRaisedSprite : callAntRaisedSprite;

    float AntX(int index) => antImages[index].rectTransform.anchoredPosition.x;

    void SetClownX(float x)
    {
        var pos = clownRect.anchoredPosition;
        pos.x = x;
        clownRect.anchoredPosition = pos;
    }
}
