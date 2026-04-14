using UnityEngine;

/// <summary>
/// Generates circle and ring sprites procedurally at runtime.
/// </summary>
public static class CircleSprites
{
    static Sprite _circle;
    static Sprite _ring;

    public static Sprite GetCircle()
    {
        if (_circle == null) _circle = BuildCircle();
        return _circle;
    }

    public static Sprite GetRing()
    {
        if (_ring == null) _ring = BuildRing();
        return _ring;
    }

    /// <summary>Soft-edged filled circle, white center fades to transparent.</summary>
    static Sprite BuildCircle()
    {
        const int S = 64;
        var pixels  = new Color[S * S];
        var center  = new Vector2((S - 1) / 2f, (S - 1) / 2f);
        float outer = S / 2f - 1f;
        float inner = outer - 4f;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center);
            float alpha;
            if (dist <= inner)
                alpha = 1f;
            else if (dist <= outer)
                alpha = 1f - (dist - inner) / (outer - inner);
            else
                alpha = 0f;
            pixels[y * S + x] = new Color(1f, 1f, 1f, alpha);
        }

        return ToSprite(pixels, S);
    }

    /// <summary>Hollow ring — only the border is visible.</summary>
    static Sprite BuildRing()
    {
        const int S     = 64;
        const float Thickness = 5f;
        var pixels  = new Color[S * S];
        var center  = new Vector2((S - 1) / 2f, (S - 1) / 2f);
        float outer = S / 2f - 1f;
        float inner = outer - Thickness;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center);
            float alpha = 0f;
            if (dist >= inner && dist <= outer)
            {
                // Soft edges
                float edgeOuter = Mathf.Clamp01((outer - dist));
                float edgeInner = Mathf.Clamp01((dist - inner));
                alpha = Mathf.Min(edgeOuter, edgeInner);
                alpha = Mathf.Clamp01(alpha * 3f);
            }
            pixels[y * S + x] = new Color(1f, 1f, 1f, alpha);
        }

        return ToSprite(pixels, S);
    }

    static Sprite ToSprite(Color[] pixels, int S)
    {
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
