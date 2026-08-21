using UnityEngine;

/// <summary>
/// Stage1_5 (리믹스) glue: shows exactly one of the 1-1~1-4 "screen" GameObjects at a time, based on
/// which phase (CallPatternData.phases) the song is currently in. Polled every frame off the shared
/// CallClock rather than event-driven, so it's always correct regardless of whether any note happens
/// to fire near a phase boundary — the planner authors phases directly in the Chart Editor, this
/// script never needs to know the order in advance.
///
/// Switching is instant (plain SetActive, no fade) per explicit request ("바로바로 전환되야 해").
/// Deactivating a screen's root also deactivates its minigame's own Visuals script component (e.g.
/// Stage1WaveRideVisuals), which unsubscribes it from CallDirector via that script's own OnDisable() —
/// every existing Stage1 visuals script already follows that OnEnable/OnDisable-subscribe pattern, so
/// an inactive phase's minigame genuinely stops listening the moment its screen turns off, not just
/// stops rendering. That's what keeps 4 minigames sharing one CallDirector from cross-talking, with
/// zero changes needed to any of the 4 existing scripts.
/// </summary>
public class Stage1RemixPhaseSwitcher : MonoBehaviour
{
    [SerializeField] CallClock clock;

    [System.Serializable]
    public class ScreenEntry
    {
        [Tooltip("CallPatternData.phases에 찍히는 styleId와 정확히 일치해야 함 (예: \"1-1\", \"1-2\").")]
        public string styleId;
        public GameObject screenRoot;
    }

    [SerializeField] ScreenEntry[] screens;

    // Null until the first phase is entered — every screen starts (and stays) off until then, since
    // an empty/not-yet-authored phases list has no phase covering any beat.
    string _currentStyleId;

    void Update()
    {
        if (clock == null || clock.Pattern == null || screens == null) return;

        float beat = clock.Pattern.TimeToBeat(clock.SongTime);
        var phase = clock.Pattern.GetPhaseAt(beat);
        string wantedStyleId = phase != null ? phase.styleId : null;

        if (wantedStyleId == _currentStyleId) return;
        _currentStyleId = wantedStyleId;

        foreach (var s in screens)
            if (s.screenRoot != null) s.screenRoot.SetActive(s.styleId == wantedStyleId);
    }
}
