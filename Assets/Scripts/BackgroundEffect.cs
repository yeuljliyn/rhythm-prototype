using UnityEngine;

public class BackgroundEffect : MonoBehaviour
{
    public Color baseColor = new Color(0.04f, 0.02f, 0.12f);
    public Color beatColor = new Color(0.12f, 0.04f, 0.28f);

    Camera      cam;
    float       beatInterval;
    float       lastBeat;
    float       pulseT;
    const float pulseDur = 0.15f;

    void Start()
    {
        cam          = Camera.main;
        beatInterval = 60f / RhythmManager.Instance.bpm;
        if (cam != null) cam.backgroundColor = baseColor;
    }

    void Update()
    {
        float song  = RhythmManager.Instance.SongTime;
        float beat  = Mathf.Floor(song / beatInterval);

        if (beat > lastBeat) { lastBeat = beat; pulseT = 0f; }

        pulseT += Time.deltaTime;
        float t = Mathf.Clamp01(pulseT / pulseDur);
        if (cam != null)
            cam.backgroundColor = Color.Lerp(beatColor, baseColor, t);
    }
}
