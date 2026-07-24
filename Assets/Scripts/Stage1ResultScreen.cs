using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage1 end-of-song result popup — same visual language as BattleResultScreen (same sprite
/// assets, same Dim/Popup/RankText/RankIcon composition), but simplified: Stage1Scene isn't wired
/// into the Lobby stage-unlock economy yet, so there's no Page2/mugu-equip step and no tutorial
/// branch — just the rank icon and a single button back to Lobby.
/// </summary>
public class Stage1ResultScreen : MonoBehaviour
{
    [SerializeField] CallDirector director;

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

    void Start()      { if (director != null) director.OnSongEnded += Trigger; }
    void OnDestroy()  { if (director != null) director.OnSongEnded -= Trigger; }

    void Trigger()
    {
        if (_triggered) return;
        _triggered = true;
        Time.timeScale = 0f;

        foreach (var src in FindObjectsOfType<AudioSource>())
            if (src.isPlaying) src.Pause();

        var    gm    = GameManager.Instance;
        string rank  = gm != null ? gm.GetRank() : "C";
        bool   clear = gm != null && gm.IsClear;

        StartCoroutine(ShowResult(rank, clear));
    }

    IEnumerator ShowResult(string rank, bool clear)
    {
        var cvGO = new GameObject("Stage1ResultCanvas");
        var cv   = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 99;

        var sc = cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1672f, 941f);
        sc.matchWidthOrHeight  = 0.5f;

        cvGO.AddComponent<GraphicRaycaster>();
        var root = cvGO.transform;

        MakeImg(root, "Dim", null, new Color(0f, 0f, 0f, 0.6f));
        MakeImg(root, "Popup",    _sprPopup);
        MakeImg(root, "RankText", _sprRankText);

        Sprite rankSpr = rank == "S" ? _sprRankS
                       : rank == "A" ? _sprRankA
                       : rank == "B" ? _sprRankB
                       :               _sprRankC;
        MakeImg(root, "RankIcon", rankSpr);

        MakeImg(root, clear ? "UnlockText" : "RetryText", clear ? _sprUnlockText : _sprRetryText);
        MakeArrowBtn(root, GoLobby);

        yield return null;
    }

    void GoLobby()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
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
