using UnityEngine;

/// <summary>
/// Generates a right-pointing arrow sprite at runtime (64×64, white fill + dark outline).
/// SpriteRenderer.color tints the white fill to the lane colour; the outline stays dark.
/// </summary>
public static class ArrowSprite
{
    static Sprite _cached;

    public static Sprite Get()
    {
        if (_cached == null) _cached = Build();
        return _cached;
    }

    static Sprite Build()
    {
        const int S = 64;
        var shape = new bool[S, S];

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // Body: thin horizontal rectangle
                bool body = x >= 2 && x <= 40 && y >= 28 && y <= 36;

                // Head: isoceles triangle pointing right
                bool head = false;
                if (x >= 40 && x <= 62)
                {
                    float halfH = (62f - x) * 20f / 22f;
                    head = y >= 32f - halfH && y <= 32f + halfH;
                }

                shape[x, y] = body || head;
            }
        }

        var pixels = new Color[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                if (shape[x, y])
                {
                    pixels[y * S + x] = Color.white;
                }
                else if (HasArrowNeighbor(shape, x, y, S))
                {
                    // Dark outline so the arrow pops against any background
                    pixels[y * S + x] = new Color(0f, 0f, 0f, 0.75f);
                }
                // else stays Color.clear (default)
            }
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply();

        // pixelsPerUnit = S so the sprite is exactly 1 Unity unit wide/tall
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    static bool HasArrowNeighbor(bool[,] shape, int x, int y, int S)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            int nx = x + dx, ny = y + dy;
            if (nx >= 0 && nx < S && ny >= 0 && ny < S && shape[nx, ny])
                return true;
        }
        return false;
    }
}
