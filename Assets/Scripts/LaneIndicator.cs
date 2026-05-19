using UnityEngine;

/// <summary>
/// Draws the hit zone vertical bar on the left (player side) and a subtle horizontal lane guide.
/// Pulses the hit zone on each beat.
/// </summary>
public class LaneIndicator : MonoBehaviour
{
    SpriteRenderer _hitZoneSR;
    float          _pulseTimer;

    void Start()
    {
        var pixel = MakePixel();

        float hz  = RhythmManager.Instance != null ? RhythmManager.Instance.hitZoneX : -5f;
        float ny  = RhythmManager.Instance != null ? RhythmManager.Instance.noteY    : -2.5f;

        // Horizontal lane guide
        CreateQuad("LaneGuide",
            new Vector3(0f, ny, 0f),
            new Vector3(30f, 0.04f, 1f),
            new Color(1f, 1f, 1f, 0.07f), pixel, 1);

        // Hit zone vertical bar
        var hzGO = CreateQuad("HitZone",
            new Vector3(hz, ny, 0f),
            new Vector3(0.08f, 5f, 1f),
            new Color(0.30f, 0.85f, 1.00f, 0.60f), pixel, 2);
        _hitZoneSR = hzGO.GetComponent<SpriteRenderer>();

        // Hit zone circle indicator
        var cirGO = new GameObject("HitZoneCircle");
        cirGO.transform.position   = new Vector3(hz, ny, 0f);
        cirGO.transform.localScale = Vector3.one * 0.7f;
        var cirSR          = cirGO.AddComponent<SpriteRenderer>();
        cirSR.sprite       = CircleSprites.GetRing();
        cirSR.color        = new Color(0.30f, 0.85f, 1.00f, 0.50f);
        cirSR.sortingOrder = 2;

        // Wire PlayerController.bodyRenderer if not yet set
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.bodyRenderer == null)
        {
            var bodyGO = GameObject.Find("Body");
            if (bodyGO != null)
                pc.bodyRenderer = bodyGO.GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (_hitZoneSR == null || RhythmManager.Instance == null) return;

        float songTime = RhythmManager.Instance.SongTime;
        if (songTime < 0f) return;

        float beat  = RhythmManager.Instance.BeatInterval;
        float phase = ((songTime % beat) + beat) % beat;
        float dist  = Mathf.Min(phase, beat - phase);
        float pulse = Mathf.Clamp01(1f - dist / (beat * 0.18f));

        float alpha = Mathf.Lerp(0.40f, 0.90f, pulse);
        _hitZoneSR.color = new Color(0.30f, 0.85f, 1.00f, alpha);
    }

    GameObject CreateQuad(string goName, Vector3 pos, Vector3 scale, Color color, Sprite pixel, int order)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = pixel;
        sr.color        = color;
        sr.sortingOrder = order;
        return go;
    }

    static Sprite MakePixel()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
