using System.Collections;
using UnityEngine;

/// <summary>
/// 상승장/하락장 이미지를 순서대로 반복 재생해 배경 애니메이션을 만든다.
/// 빌딩 뒤, 구름 앞에 반투명하게 표시된다.
/// </summary>
public class StockChartBackground : MonoBehaviour
{
    static readonly string[] FrameNames =
    {
        "상승장1","상승장2","상승장3","상승장4","상승장5",
        "하락장1","하락장2","하락장3","하락장4","하락장5",
    };

    const float Fps         = 3f;    // 프레임 속도 (낮을수록 드라마틱)
    const float Alpha       = 0.35f; // 반투명도
    const int   SortOrder   = -9;    // 구름(-9)과 같은 깊이 — 빌딩 앞단 뒤

    SpriteRenderer _sr;
    Sprite[]       _frames;

    void Start()
    {
        _frames = new Sprite[FrameNames.Length];
        for (int i = 0; i < FrameNames.Length; i++)
        {
            _frames[i] = Resources.Load<Sprite>(FrameNames[i]);
            if (_frames[i] == null)
                Debug.LogWarning($"[StockChart] 로드 실패: {FrameNames[i]}");
        }

        var cam = Camera.main;
        var go  = new GameObject("StockChartAnim");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(
            cam != null ? cam.transform.position.x : 0f, 0f, 0f);

        _sr             = go.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = SortOrder;
        _sr.color        = new Color(1f, 1f, 1f, Alpha);

        // 카메라 화면 전체를 덮도록 스케일 조정
        if (cam != null && _frames[0] != null)
        {
            float camH    = cam.orthographicSize * 2f;
            float camW    = camH * cam.aspect;
            float sprH    = _frames[0].bounds.size.y;
            float sprW    = _frames[0].bounds.size.x;
            float scaleH  = camH / sprH;
            float scaleW  = camW / sprW;
            float scale   = Mathf.Max(scaleH, scaleW); // 꽉 채움
            go.transform.localScale = Vector3.one * scale;
        }

        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        int i = 0;
        float wait = 1f / Fps;
        while (true)
        {
            if (_frames[i] != null)
                _sr.sprite = _frames[i];
            i = (i + 1) % _frames.Length;
            yield return new WaitForSeconds(wait);
        }
    }
}
