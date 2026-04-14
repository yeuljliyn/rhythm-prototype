using UnityEngine;

/// <summary>
/// Arena layout: draws a player sprite at the bottom-center,
/// a dividing line between player and boss zones, and ambient corner accents.
/// The boss is handled separately by BossController.
/// </summary>
public class LaneIndicator : MonoBehaviour
{
    [Header("Player Visual")]
    public SpriteRenderer playerRenderer;

    void Start()
    {
        var pixel = MakePixel();

        // Faint horizontal dividing line between player and boss zones
        CreateQuad("Divider", new Vector3(0f, 0f, 0f),
            new Vector3(30f, 0.03f, 1f), new Color(1f, 1f, 1f, 0.10f), pixel, 1);

        // Subtle vertical center guide
        CreateQuad("CenterGuide", Vector3.zero,
            new Vector3(0.03f, 20f, 1f), new Color(1f, 1f, 1f, 0.05f), pixel, 1);

        // Wire up PlayerController.bodyRenderer to the existing Player/Body if not set
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.bodyRenderer == null)
        {
            var bodyGO = GameObject.Find("Body");
            if (bodyGO != null)
            {
                var sr = bodyGO.GetComponent<SpriteRenderer>();
                if (sr != null) { pc.bodyRenderer = sr; playerRenderer = sr; }
            }
        }
    }

    void CreateQuad(string goName, Vector3 pos, Vector3 scale, Color color, Sprite pixel, int order)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = pixel;
        sr.color        = color;
        sr.sortingOrder = order;
    }

    static Sprite MakePixel()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    static Sprite BuildPlayerSprite()
    {
        const int S = 64;
        var pixels  = new Color[S * S];
        var center  = new Vector2((S - 1) / 2f, (S - 1) / 2f);

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = (x - center.x) / (S / 2f);
            float dy = (y - center.y) / (S / 2f);

            // Shield shape: rounded top, pointed bottom
            float halfW = 0.75f * (1f - Mathf.Pow(Mathf.Clamp01(-dy * 0.9f), 1.5f) * 0.4f);
            bool  inShield = Mathf.Abs(dx) < halfW && dy < 0.80f && dy > -0.90f;

            // Top arc
            bool topArc = Vector2.Distance(new Vector2(dx, dy), new Vector2(0f, 0.50f)) < 0.75f;

            if (inShield && topArc)
            {
                float edge = Mathf.Clamp01(1f - Mathf.Abs(dx) / halfW);
                Color body = Color.Lerp(new Color(0.20f, 0.55f, 1.00f),
                                        new Color(0.50f, 0.85f, 1.00f), edge * 0.6f + 0.2f);
                pixels[y * S + x] = body;
            }
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
