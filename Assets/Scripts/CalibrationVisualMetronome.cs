using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Shows direction cues synced to CalibrationSession's beat using dspTime.
/// No visual offset is stored or applied — timing is read raw from dspTime each frame.
/// </summary>
public class CalibrationVisualMetronome : MonoBehaviour
{
    CalibrationSession _session;

    TextMeshProUGUI _upLabel, _downLabel, _leftLabel, _rightLabel;
    Image           _beatPulse;

    static readonly ArrowDir[] AllDirs    = { ArrowDir.Up, ArrowDir.Down, ArrowDir.Left, ArrowDir.Right };
    static readonly Color      ColActive  = new Color(0.35f, 1.00f, 0.50f);
    static readonly Color      ColWarmup  = new Color(0.90f, 0.80f, 0.30f, 0.65f);
    static readonly Color      ColDim     = new Color(0.28f, 0.28f, 0.28f, 0.45f);

    public void Initialize(CalibrationSession session, Transform parent)
    {
        _session = session;
        BuildUI(parent);
    }

    void BuildUI(Transform parent)
    {
        // Beat pulse backdrop
        var pulseGO = MakeRect(parent, "BeatPulse",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(100f, 100f), new Vector2(0f, -250f));
        _beatPulse        = pulseGO.AddComponent<Image>();
        _beatPulse.sprite = BuildCircleSprite();
        _beatPulse.color  = Color.clear;

        // Arrow labels at cardinal positions around the circle
        _upLabel    = MakeArrow(parent, "↑", new Vector2(0f,    -180f));
        _downLabel  = MakeArrow(parent, "↓", new Vector2(0f,    -320f));
        _leftLabel  = MakeArrow(parent, "←", new Vector2(-90f,  -250f));
        _rightLabel = MakeArrow(parent, "→", new Vector2( 90f,  -250f));
    }

    void Update()
    {
        if (_session == null || !_session.IsActive) return;

        double elapsed = AudioSettings.dspTime - _session.SessionStartDspTime;
        if (elapsed < 0) { SetAllDim(); return; }

        double beatInterval = _session.BeatInterval;
        double phase        = elapsed % beatInterval;
        float  progress     = (float)(phase / beatInterval);   // 0 → 1 per beat

        // Flash at the start of each beat
        float pulseFade = Mathf.Clamp01(1f - progress / 0.25f);
        _beatPulse.color = new Color(0.25f, 0.85f, 1f, pulseFade * 0.50f);

        bool     isWarmup = _session.IsWarmup;
        ArrowDir target   = _session.CurrentTargetDir;
        TextMeshProUGUI[] labels = { _upLabel, _downLabel, _leftLabel, _rightLabel };

        for (int i = 0; i < AllDirs.Length; i++)
        {
            bool isTarget = !isWarmup && AllDirs[i] == target;
            float brightness = isTarget ? Mathf.Lerp(1f, 0.6f, progress) : 0f;

            labels[i].color = isWarmup
                ? Color.Lerp(ColDim, ColWarmup, pulseFade)
                : isTarget
                    ? Color.Lerp(ColDim, ColActive, Mathf.Clamp01(brightness + pulseFade))
                    : ColDim;

            labels[i].transform.localScale = isTarget
                ? Vector3.one * Mathf.Lerp(1.35f, 1.0f, progress)
                : Vector3.one;
        }
    }

    void SetAllDim()
    {
        foreach (var lbl in new[] { _upLabel, _downLabel, _leftLabel, _rightLabel })
            if (lbl != null) lbl.color = ColDim;
    }

    TextMeshProUGUI MakeArrow(Transform parent, string symbol, Vector2 pos)
    {
        var go   = new GameObject("Arrow_" + symbol);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(70f, 70f);
        rect.anchoredPosition = pos;
        var lbl           = go.AddComponent<TextMeshProUGUI>();
        lbl.text          = symbol;
        lbl.fontSize      = 56f;
        lbl.alignment     = TextAlignmentOptions.Center;
        lbl.color         = ColDim;
        lbl.raycastTarget = false;
        return lbl;
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
        const int S = 32;
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
