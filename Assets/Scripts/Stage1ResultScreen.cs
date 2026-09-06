using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Stage1 end-of-song result popup — same visual language as BattleResultScreen (same sprite
/// assets, same Dim/Popup/RankText/RankIcon composition), but simplified: no Page2/mugu-equip step,
/// no tutorial branch — just the rank icon and a single button back to the 지하 로비
/// (UndergroundLobbyScene). 이 씬 하나짜리 컴포넌트를 Stage1_1Scene~Stage1_5Scene 각각에 그대로
/// 복제해서 씀 — 그래서 stageIndex(0-based, 1-1=0)로 "내가 몇 번째 스테이지인지"만 인스펙터에서
/// 지정해주면 됨. 랭크 A 이상으로 클리어하면 UndergroundLobbyProgress를 통해 다음 스테이지를 해금.
/// </summary>
public class Stage1ResultScreen : MonoBehaviour
{
    [SerializeField] CallDirector director;

    [Tooltip("이 스테이지의 지하 로비 슬롯 인덱스(0-based, 1-1=0, 1-2=1 ...) — 랭크 A 이상 클리어 시 " +
             "이 값+1을 해금 처리하는 데 씀.")]
    [SerializeField] int stageIndex = 0;

    [Header("Same sprite assets as BattleResultScreen")]
    [SerializeField] Sprite _sprPopup;
    [SerializeField] Sprite _sprRankText;
    [SerializeField] Sprite _sprRankS;
    [SerializeField] Sprite _sprRankA;
    [SerializeField] Sprite _sprRankB;
    [SerializeField] Sprite _sprRankC;
    [SerializeField] Sprite _sprUnlockText;
    [SerializeField] Sprite _sprRetryText;
    [SerializeField] Sprite _sprArrow;

    bool _triggered;
    bool _advanced;
    float _shownRealtime;

    // 결과창이 뜬 직후 짧은 유예 — 곡이 끝나는 순간 마침 누르고 있던 Space/클릭이 결과창을 보기도
    // 전에 그대로 "다음으로" 스킵돼버리지 않도록.
    const float AdvanceArmDelay = 0.4f;

    void Start()      { if (director != null) director.OnSongEnded += HandleSongEnded; }
    void OnDestroy()  { if (director != null) director.OnSongEnded -= HandleSongEnded; }

    void Update()
    {
        var kb = Keyboard.current;

        if (!_triggered)
        {
            // 테스트용 강제 랭크 치트 — BattleResultScreen(구)의 F1(S강제)/F2(C강제)와 동일한 패턴.
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) { Trigger("S"); return; }
            if (kb.f2Key.wasPressedThisFrame) { Trigger("C"); return; }
            return;
        }

        // 결과창 표시 중 — Space/좌클릭으로도 다음으로 넘어감(화살표 버튼은 그대로 둠).
        if (_advanced) return;
        if (Time.realtimeSinceStartup - _shownRealtime < AdvanceArmDelay) return;

        bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (space || click) GoUndergroundLobby();
    }

    void HandleSongEnded() => Trigger(null);

    void Trigger(string forcedRank)
    {
        if (_triggered) return;
        _triggered = true;
        _shownRealtime = Time.realtimeSinceStartup;
        Time.timeScale = 0f;

        foreach (var src in FindObjectsOfType<AudioSource>())
            if (src.isPlaying) src.Pause();

        // 결과창이 뜨면 모든 CallDirector를 한 번에 정지 — 안 그러면 CallDirector.Update()가 계속
        // 돌면서 결과창을 넘기려고 누른 Space/클릭이 OnResponsePress/OnResponseRelease까지 발화해서
        // 팝업 뒤 캐릭터가 움직이고 효과음도 남(신고받음). CallDirector 자체의 _songEndFired 가드는
        // 곡을 끝까지 재생한 경우에만 걸리고 F1/F2 치트·연습 중 조기 종료 등에선 안 켜짐. 컴포넌트를
        // 끄는 것도 Stage1PracticeGate가 세그먼트를 도로 SetActive하면 되살아나서 불안정 —
        // 정적 플래그가 확실. CallDirector.Awake()에서 씬 로드마다 false로 리셋됨.
        CallDirector.SuppressAll = true;

        var    gm   = GameManager.Instance;
        string rank = forcedRank ?? (gm != null ? gm.GetRank() : "C");
        // UnlockText/RetryText 표시 기준은 gm.IsClear(기존 무구 경제의 B 이상 "클리어")가 아니라
        // 실제 다음 스테이지 해금 기준(A 이상)과 정확히 맞춤 — B일 때 "해금됨" 텍스트가 뜨는데 실제로는
        // 아무 것도 안 열리는 불일치가 있었음(신고받은 버그). S/A일 때만 UnlockText, B/C는 RetryText.
        bool unlockedNext = rank == "S" || rank == "A";

        if (unlockedNext)
            UndergroundLobbyProgress.UnlockUpTo(stageIndex + 1);

        StartCoroutine(ShowResult(rank, unlockedNext));
    }

    IEnumerator ShowResult(string rank, bool unlockedNext)
    {
        var cvGO = new GameObject("Stage1ResultCanvas");
        var cv   = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 99;

        var sc = cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1672f, 941f);
        sc.matchWidthOrHeight  = 1f; // 세로(높이) 기준 고정 — 와이드/울트라와이드에서 위아래 잘림 방지

        cvGO.AddComponent<GraphicRaycaster>();
        var root = cvGO.transform;

        // Dim은 화면 전체를 덮어야 하니 풀스트레치 그대로 — 팝업 그림들만 SafeFrame(고정
        // 1672x941, 항상 캔버스 중앙)에 넣어서, 와이드/울트라와이드에서 캔버스 자체가 기준
        // 해상도보다 넓어져도(matchWidthOrHeight=1이라 세로 기준 고정) 늘어나지 않고 원래 비율
        // 그대로 중앙에 표시되게 함 — 다른 씬들의 SafeFrame과 동일한 이유/방식.
        MakeImg(root, "Dim", null, new Color(0f, 0f, 0f, 0.6f));

        var safeFrameGO = new GameObject("SafeFrame", typeof(RectTransform));
        safeFrameGO.transform.SetParent(root, false);
        var safeFrameRt = (RectTransform)safeFrameGO.transform;
        safeFrameRt.anchorMin = safeFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
        safeFrameRt.pivot = new Vector2(0.5f, 0.5f);
        safeFrameRt.sizeDelta = new Vector2(1672f, 941f);
        safeFrameRt.anchoredPosition = Vector2.zero;

        MakeImg(safeFrameRt, "Popup",    _sprPopup);
        MakeImg(safeFrameRt, "RankText", _sprRankText);

        Sprite rankSpr = rank == "S" ? _sprRankS
                       : rank == "A" ? _sprRankA
                       : rank == "B" ? _sprRankB
                       :               _sprRankC;
        MakeImg(safeFrameRt, "RankIcon", rankSpr);

        MakeImg(safeFrameRt, unlockedNext ? "UnlockText" : "RetryText", unlockedNext ? _sprUnlockText : _sprRetryText);
        MakeArrowBtn(safeFrameRt, GoUndergroundLobby);

        yield return null;
    }

    void GoUndergroundLobby()
    {
        if (_advanced) return;   // 화살표 버튼 클릭 / Space / 좌클릭 어느 쪽으로 들어와도 전환은 한 번만
        _advanced = true;
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("UndergroundLobbyScene");
    }

    // 장식용 이미지 (클릭 불가)
    static void MakeImg(Transform parent, string goName, Sprite sprite, Color? tint = null)
    {
        var go  = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        FullStretch((RectTransform)go.transform);
        img.sprite        = sprite;
        img.color         = tint ?? Color.white;
        img.raycastTarget = false;
        if (sprite != null) img.preserveAspect = false;
    }

    // 화살표 버튼 — 투명 픽셀 클릭 안 되게 alphaHitTest 적용
    void MakeArrowBtn(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        var go  = new GameObject("ArrowBtn");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        FullStretch((RectTransform)go.transform);
        img.sprite        = _sprArrow;
        img.color         = Color.white;
        img.raycastTarget = true;
        img.preserveAspect = false;
        img.alphaHitTestMinimumThreshold = 0.1f;

        go.AddComponent<Button>().onClick.AddListener(onClick);
    }

    static void FullStretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
