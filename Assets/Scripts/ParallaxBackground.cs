using UnityEngine;

/// <summary>
/// 각 레이어를 사인파로 미세하게 진동시켜 입체감을 준다.
/// 앞쪽 레이어일수록 진폭이 커서 원근감이 느껴진다.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    struct Layer
    {
        public SpriteRenderer sr;
        public float          baseX;
        public float          baseY;
        public float          amplitude;
        public float          frequency;
        public float          phase;
    }

    // (스프라이트 이름, 소팅오더, 진폭, 주파수, 높이비율, Y오프셋배율, 색상)
    // heightFrac: 화면 높이 대비 스프라이트 높이 비율 (1=풀화면, 0.5=절반)
    // yBias: -1=하단정렬, 0=중앙
    static readonly (string name, int order, float amplitude, float frequency, float heightFrac, float yBias, Color color)[] LayerDefs =
    {
        ("sky-stars",       -10, 0.00f, 0.10f, 1.00f,  0.0f, new Color(0.585f, 0.585f, 0.585f, 1f)),
        ("sky-clouds",       -9, 0.04f, 0.08f, 1.00f,  0.0f, Color.white),
        ("sky-buildings-3",  -8, 0.10f, 0.10f, 0.50f, -1.0f, Color.white),
        ("sky-buildings-2",  -7, 0.22f, 0.12f, 0.55f, -1.0f, Color.white),
        ("sky-buildings-1",  -6, 0.40f, 0.14f, 0.60f, -1.0f, Color.white),
    };

    Layer[] _layers;
    Camera  _cam;

    void Start()
    {
        _cam    = Camera.main;
        _layers = new Layer[LayerDefs.Length];

        for (int i = 0; i < LayerDefs.Length; i++)
        {
            var (spriteName, order, amplitude, frequency, heightFrac, yBias, color) = LayerDefs[i];
            var sprite = Resources.Load<Sprite>(spriteName);
            if (sprite == null)
            {
                Debug.LogWarning($"[Parallax] 로드 실패: {spriteName}");
                continue;
            }

            float camH = _cam != null ? _cam.orthographicSize : 5f;
            float camX = _cam != null ? _cam.transform.position.x : 0f;

            float targetH = camH * 2f * heightFrac;  // 목표 표시 높이 (월드 단위)
            float sprH    = sprite.bounds.size.y;
            float scale   = sprH > 0f ? targetH / sprH : 1f;

            // yBias=-1이면 하단 정렬: y = -camH + targetH/2
            float posY = yBias < 0f ? -camH + targetH / 2f : 0f;

            var go = new GameObject(spriteName);
            go.transform.SetParent(transform);
            go.transform.position   = new Vector3(camX, posY, 0f);
            go.transform.localScale = Vector3.one * scale;

            var sr         = go.AddComponent<SpriteRenderer>();
            sr.sprite      = sprite;
            sr.color       = color;
            sr.sortingOrder = order;

            _layers[i] = new Layer
            {
                sr        = sr,
                baseX     = camX,
                baseY     = posY,
                amplitude = amplitude,
                frequency = frequency,
                phase     = i * 0.7f,
            };
        }
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < _layers.Length; i++)
        {
            var l = _layers[i];
            if (l.sr == null) continue;

            float offsetX = Mathf.Sin(t * l.frequency + l.phase) * l.amplitude;
            l.sr.transform.position = new Vector3(l.baseX + offsetX, l.baseY, 0f);
        }
    }
}
