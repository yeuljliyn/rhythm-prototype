using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealthManager : MonoBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    const int   MaxHearts    = 5;
    const float HeartSize    = 36f;
    const float HeartSpacing = 10f;

    public int Hearts { get; private set; } = MaxHearts;

    Image[]    _heartImages = new Image[MaxHearts];
    Coroutine[] _pulseJobs  = new Coroutine[MaxHearts];

    static readonly Color ColFull  = new Color(1.00f, 0.20f, 0.25f, 1.00f);
    static readonly Color ColEmpty = new Color(0.25f, 0.08f, 0.08f, 0.45f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        BuildHearts();
        GameManager.Instance.OnJudgement += OnJudgement;
    }

    public void LoseHeart()
    {
        if (Hearts <= 0) return;
        Hearts--;
        int idx = Hearts;
        if (_pulseJobs[idx] != null) StopCoroutine(_pulseJobs[idx]);
        _pulseJobs[idx] = StartCoroutine(LoseHeartAnim(idx));
    }

    void OnJudgement(JudgementType type) { }

    void BuildHearts()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogWarning("PlayerHealthManager: Canvas not found"); return; }

        var uiRoot = canvasGO.transform.Find("UIRoot");
        var parent = uiRoot != null ? uiRoot : canvasGO.transform;

        var sprite = BuildHeartSprite();
        float totalW = MaxHearts * HeartSize + (MaxHearts - 1) * HeartSpacing;

        // Container — top center
        var containerGO   = new GameObject("HeartsContainer");
        containerGO.transform.SetParent(parent, false);
        var containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin        = new Vector2(0.5f, 1f);
        containerRect.anchorMax        = new Vector2(0.5f, 1f);
        containerRect.pivot            = new Vector2(0.5f, 1f);
        containerRect.anchoredPosition = new Vector2(0f, -14f);
        containerRect.sizeDelta        = new Vector2(totalW, HeartSize);

        for (int i = 0; i < MaxHearts; i++)
        {
            var go   = new GameObject("Heart_" + i);
            go.transform.SetParent(containerGO.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0f, 0f);
            rect.anchorMax        = new Vector2(0f, 1f);
            rect.pivot            = new Vector2(0f, 0.5f);
            rect.sizeDelta        = new Vector2(HeartSize, 0f);
            rect.anchoredPosition = new Vector2(i * (HeartSize + HeartSpacing), 0f);

            var img    = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color  = ColFull;
            img.preserveAspect = true;

            _heartImages[i] = img;
        }
    }

    IEnumerator LoseHeartAnim(int idx)
    {
        if (_heartImages[idx] == null) yield break;

        float elapsed = 0f, duration = 0.25f;
        var   rt      = _heartImages[idx].rectTransform;
        var   origin  = rt.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float f = elapsed / duration;
            rt.anchoredPosition = origin + Random.insideUnitCircle * 4f * (1f - f);
            _heartImages[idx].color = Color.Lerp(ColFull, ColEmpty, f);
            yield return null;
        }
        rt.anchoredPosition       = origin;
        _heartImages[idx].color   = ColEmpty;
        rt.localScale             = Vector3.one * 0.78f;
    }

    static Sprite BuildHeartSprite()
    {
        const int S = 48;
        var pixels  = new Color[S * S];

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float nx = (x - S / 2f + 0.5f) / (S / 2.4f);
            float ny = (y - S / 2f + 0.5f) / (S / 2.4f) + 0.15f;
            float v  = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * Mathf.Pow(ny, 3f);
            if (v <= 0f) pixels[y * S + x] = Color.white;
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
