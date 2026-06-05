using UnityEngine;
using System.Collections;

/// <summary>
/// 보스가 방향을 보여줄 때 (ShowDirection) 두 가지 이펙트 재생:
///   A. 방향 파티클 버스트 — 보스 위치에서 해당 방향으로 파티클 발사
///   B. 방향 팝 글로우    — 해당 방향 위치에 원형 글로우가 팝하고 페이드아웃
/// </summary>
public class BossDirectionEffect : MonoBehaviour
{
    [SerializeField] ParticleSystem _burstPS;
    [SerializeField] float _popOffset   = 1.6f;   // 보스 중심에서 팝 스프라이트까지 거리
    [SerializeField] float _popSize     = 0.9f;   // 팝 글로우 최대 크기 (world units)
    [SerializeField] float _popDuration = 0.12f;  // 팝인 시간
    [SerializeField] float _fadeDuration = 0.28f; // 페이드아웃 시간

    // ArrowDir: Left=0, Right=1, Up=2, Down=3  (DirAngles 순서와 동일)
    static readonly Color[] DirColors =
    {
        new Color(0.30f, 0.60f, 1.00f),  // Left  - 파랑
        new Color(1.00f, 0.35f, 0.35f),  // Right - 빨강
        new Color(1.00f, 0.90f, 0.20f),  // Up    - 노랑
        new Color(0.30f, 1.00f, 0.45f),  // Down  - 초록
    };

    static readonly Vector3[] DirOffsets =
    {
        new Vector3(-1f,  0f, 0f),  // Left
        new Vector3( 1f,  0f, 0f),  // Right
        new Vector3( 0f,  1f, 0f),  // Up
        new Vector3( 0f, -1f, 0f),  // Down
    };

    // Cone이 local +Y 방향으로 emit → Z축 회전으로 방향 전환
    static readonly float[] BurstAngles = { 90f, -90f, 0f, 180f };

    Sprite _circleSprite;

    void OnDestroy()
    {
        if (_circleSprite != null)
        {
            Destroy(_circleSprite.texture);
            Destroy(_circleSprite);
        }
    }

    public void PlayEffect(ArrowDir dir)
    {
        int idx = (int)dir;
        BurstParticle(idx);
        StartCoroutine(PopGlow(idx));
    }

    // ── A. 파티클 버스트 ─────────────────────────────────────────────────────

    void BurstParticle(int idx)
    {
        if (_burstPS == null) return;
        _burstPS.transform.position = transform.position;
        _burstPS.transform.rotation = Quaternion.Euler(0f, 0f, BurstAngles[idx]);

        var main = _burstPS.main;
        main.startColor = new ParticleSystem.MinMaxGradient(DirColors[idx]);
        _burstPS.Emit(18);
    }

    // ── B. 팝 글로우 ─────────────────────────────────────────────────────────

    IEnumerator PopGlow(int idx)
    {
        if (_circleSprite == null) _circleSprite = BuildCircleSprite(64);

        var go = new GameObject("BossPopGlow");
        go.transform.position = transform.position + DirOffsets[idx] * _popOffset;
        go.transform.localScale = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _circleSprite;
        sr.color        = DirColors[idx];
        sr.sortingOrder = 5;

        // Pop in
        for (float t = 0f; t < _popDuration; t += Time.deltaTime)
        {
            go.transform.localScale = Vector3.one * Mathf.Lerp(0f, _popSize, t / _popDuration);
            yield return null;
        }
        go.transform.localScale = Vector3.one * _popSize;

        // Fade out
        Color c = DirColors[idx];
        for (float t = 0f; t < _fadeDuration; t += Time.deltaTime)
        {
            sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / _fadeDuration));
            yield return null;
        }

        Destroy(go);
    }

    // ── 원형 소프트 글로우 스프라이트 동적 생성 ─────────────────────────────

    static Sprite BuildCircleSprite(int size)
    {
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half   = size * 0.5f;
        float softW  = half * 0.30f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float d     = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f) +
                                         (y - half + 0.5f) * (y - half + 0.5f));
                float alpha = Mathf.Clamp01((half - d) / softW);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }
}
