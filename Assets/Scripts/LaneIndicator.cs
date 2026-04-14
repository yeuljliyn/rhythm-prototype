using UnityEngine;

/// <summary>
/// Cross-shaped track with 4 receptor markers.
/// Lane 0=Left(blue)  1=Down(green)  2=Up(yellow)  3=Right(red)
/// </summary>
public class LaneIndicator : MonoBehaviour
{
    SpriteRenderer[] receptorSR = new SpriteRenderer[4];

    static readonly Color[] LaneColors = {
        new Color(0.3f, 0.6f, 1f),     // Left  — blue
        new Color(0.3f, 1f,  0.45f),   // Down  — green
        new Color(1f,  0.9f, 0.2f),    // Up    — yellow
        new Color(1f,  0.35f,0.35f),   // Right — red
    };

    // Z rotation matching RhythmManager.LaneRotation so receptors show the same arrow direction
    static readonly float[] ReceptorRotation = { 180f, -90f, 90f, 0f };

    void Start()
    {
        var pixel = MakePixel();

        // Horizontal guide line
        CreateQuad("HTrack", Vector3.zero, new Vector3(30f, 0.04f, 1f),
                   new Color(1f, 1f, 1f, 0.12f), pixel, 1);
        // Vertical guide line
        CreateQuad("VTrack", Vector3.zero, new Vector3(0.04f, 20f, 1f),
                   new Color(1f, 1f, 1f, 0.12f), pixel, 1);

        // 4 receptor arrows
        var arrowSprite = ArrowSprite.Get();
        for (int i = 0; i < 4; i++)
        {
            var hz = new GameObject($"Receptor_{i}");
            hz.transform.SetParent(transform);
            hz.transform.position   = RhythmManager.ReceptorPos[i];
            hz.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            hz.transform.rotation   = Quaternion.Euler(0f, 0f, ReceptorRotation[i]);

            var sr          = hz.AddComponent<SpriteRenderer>();
            sr.sprite       = arrowSprite;
            sr.color        = new Color(LaneColors[i].r, LaneColors[i].g, LaneColors[i].b, 0.45f);
            sr.sortingOrder = 2;
            receptorSR[i]   = sr;
        }
    }

    void Update()
    {
        for (int i = 0; i < receptorSR.Length; i++)
        {
            if (receptorSR[i] == null) continue;
            float a = 0.35f + 0.25f * Mathf.Sin(Time.time * 6f);
            Color c = receptorSR[i].color;
            receptorSR[i].color = new Color(c.r, c.g, c.b, a);
        }
    }

    void CreateQuad(string goName, Vector3 pos, Vector3 scale, Color color, Sprite pixel, int order)
    {
        var go          = new GameObject(goName);
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
}
