using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 차례: 하락장 프레임 애니메이션
/// 플레이어 차례: 상승장 프레임 애니메이션
/// BattleDirector가 없을 때는 beatsPerTurn 주기로 자동 교대.
/// </summary>
public class StockChartBackground : MonoBehaviour
{
    static readonly string[] RisingNames  = { "상승장1","상승장2","상승장3","상승장4","상승장5" };
    static readonly string[] FallingNames = { "하락장1","하락장2","하락장3","하락장4","하락장5" };

    [Tooltip("BattleDirector 없을 때 자동 교대할 박자 수")]
    public int beatsPerTurn = 4;

    const float Fps       = 3f;
    const float Alpha     = 0.35f;
    const int   SortOrder = -9;

    // false = 보스 차례 → 하락장 / true = 플레이어 차례 → 상승장
    static bool _isPlayerTurn       = false;
    static bool _externallyControlled = false;

    public static void SetTurn(bool isPlayerTurn)
    {
        _externallyControlled = true;
        _isPlayerTurn = isPlayerTurn;
    }

    SpriteRenderer _sr;
    Sprite[]       _risingFrames;
    Sprite[]       _fallingFrames;

    void Start()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TutorialBattleScene") return;

        _isPlayerTurn        = false;
        _externallyControlled = false;

        _risingFrames  = LoadFrames(RisingNames);
        _fallingFrames = LoadFrames(FallingNames);

        var cam = Camera.main;
        var go  = new GameObject("StockChartAnim");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(cam != null ? cam.transform.position.x : 0f, 0f, 0f);

        _sr              = go.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = SortOrder;
        _sr.color        = new Color(1f, 1f, 1f, Alpha);

        var refFrames = (_fallingFrames != null && _fallingFrames.Length > 0) ? _fallingFrames : _risingFrames;
        if (cam != null && refFrames != null && refFrames.Length > 0 && refFrames[0] != null)
        {
            float camH  = cam.orthographicSize * 2f;
            float camW  = camH * cam.aspect;
            float sprH  = refFrames[0].bounds.size.y;
            float sprW  = refFrames[0].bounds.size.x;
            float scale = Mathf.Max(camH / sprH, camW / sprW);
            go.transform.localScale = Vector3.one * scale;
        }

        StartCoroutine(Animate());
        StartCoroutine(AutoSwitchTurns());
    }

    Sprite[] LoadFrames(string[] names)
    {
        var frames = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            frames[i] = Resources.Load<Sprite>(names[i]);
            if (frames[i] == null)
                Debug.LogWarning($"[StockChart] 로드 실패: {names[i]}");
        }
        return frames;
    }

    IEnumerator Animate()
    {
        int i = 0;
        float wait = 1f / Fps;
        while (true)
        {
            var frames = _isPlayerTurn ? _risingFrames : _fallingFrames;
            if (frames != null && _sr != null)
            {
                int fi = i % frames.Length;
                if (frames[fi] != null) _sr.sprite = frames[fi];
            }
            i++;
            yield return new WaitForSeconds(wait);
        }
    }

    IEnumerator AutoSwitchTurns()
    {
        while (RhythmManager.Instance == null) yield return null;
        float beat         = RhythmManager.Instance.BeatInterval;
        float turnDuration = beat * Mathf.Max(1, beatsPerTurn);

        while (true)
        {
            if (!_externallyControlled) _isPlayerTurn = false;  // 보스 차례 → 하락장
            yield return new WaitForSeconds(turnDuration);
            if (!_externallyControlled) _isPlayerTurn = true;   // 플레이어 차례 → 상승장
            yield return new WaitForSeconds(turnDuration);
        }
    }
}
