using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Simple beat-pulse visual for CalibrationSession (spacebar mode).
/// Flashes a circle on each beat using dspTime — no direction cues.
/// </summary>
public class CalibrationVisualMetronome : MonoBehaviour
{
    CalibrationSession _session;

    Image           _beatPulse;
    TextMeshProUGUI _spaceLabel;

    public void Initialize(CalibrationSession session, Transform parent)
    {
        _session = session;
        BuildUI(parent);
    }

    void BuildUI(Transform parent)
    {
        var pulseGO = MakeRect(parent, "BeatPulse",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(120f, 120f), new Vector2(0f, -340f));
        _beatPulse        = pulseGO.AddComponent<Image>();
        _beatPulse.sprite = BuildCircleSprite();
        _beatPulse.color  = Color.clear;

        // "SPACE" label inside the circle
        var lblGO = new GameObject("SpaceLbl");
        lblGO.transform.SetParent(pulseGO.transform, false);
        var lblRect       = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
        _spaceLabel           = lblGO.AddComponent<TextMeshProUGUI>();
        _spaceLabel.text      = "SPACE";
        _spaceLabel.fontSize  = 18f;
        _spaceLabel.alignment = TextAlignmentOptions.Center;
        _spaceLabel.color     = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        _spaceLabel.raycastTarget = false;
    }

    void Update()
    {
        if (_session == null || !_session.IsActive)
        {
            if (_beatPulse != null) _beatPulse.color = Color.clear;
            return;
        }

        double elapsed = AudioSettings.dspTime - _session.SessionStartDspTime;
        if (elapsed < 0) { _beatPulse.color = Color.clear; return; }

        double phase    = elapsed % _session.BeatInterval;
        float  progress = (float)(phase / _session.BeatInterval);  // 0 → 1 per beat

        float pulseFade = Mathf.Clamp01(1f - progress / 0.25f);

        Color pulseCol = _session.IsWarmup
            ? new Color(0.90f, 0.80f, 0.30f, pulseFade * 0.60f)
            : new Color(0.25f, 0.85f, 1.00f, pulseFade * 0.70f);
        _beatPulse.color = pulseCol;

        float scale = Mathf.Lerp(1.15f, 1.0f, progress);
        _beatPulse.transform.localScale = Vector3.one * scale;
    }

    static GameObject MakeRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        return go;
    }

    static Sprite BuildCircleSprite()
    {
        const int S = 64;
        float cx = (S - 1) / 2f, cy = (S - 1) / 2f, r = S / 2f - 1f;
        var pixels = new Color[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            pixels[y * S + x] = new Color(1f, 1f, 1f, d <= r ? 1f : 0f);
        }
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
