using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BattleResultScreen : MonoBehaviour
{
    [Header("Page 1")]
    [SerializeField] Sprite _sprPopup;
    [SerializeField] Sprite _sprRankText;
    [SerializeField] Sprite _sprRankS;
    [SerializeField] Sprite _sprRankA;
    [SerializeField] Sprite _sprRankB;
    [SerializeField] Sprite _sprRankC;
    [SerializeField] Sprite _sprUnlockText;
    [SerializeField] Sprite _sprRetryText;
    [SerializeField] Sprite _sprArrow;

    [Header("Page 2")]
    [SerializeField] Sprite _sprEquipTitle;
    [SerializeField] Sprite _sprMuguText;
    [SerializeField] Sprite _sprMuguItem;

    bool        _triggered;
    bool        _unlockNext;
    bool        _isTutorial;
    int         _stageIdx;
    Transform   _cvRoot;
    GameObject  _page1GO;

    enum Page { None, Page1, Page2 }
    Page _currentPage;

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (!_triggered)
        {
            if (kb.f1Key.wasPressedThisFrame) { Trigger("S", dev: true);  return; }
            if (kb.f2Key.wasPressedThisFrame) { Trigger("C", dev: true);  return; }
            if (RhythmManager.Instance != null && RhythmManager.Instance.IsMusicDone)
                Trigger(null, dev: false);
            return;
        }

        // 결과창 표시 중 Space → 다음 페이지
        if (kb.spaceKey.wasPressedThisFrame) AdvancePage();
    }

    void Trigger(string devRank, bool dev)
    {
        if (_triggered) return;
        _triggered = true;
        Time.timeScale = 0f;

        RhythmManager.Instance?.StopMusic();
        foreach (var src in FindObjectsOfType<AudioSource>())
            if (src.isPlaying) src.Pause();

        var    gm   = GameManager.Instance;
        string rank = devRank ?? (gm != null ? gm.GetRank() : "C");

        _stageIdx   = PlayerPrefs.GetInt("current_stage", 0);
        _unlockNext = rank == "S" || rank == "A" || rank == "B";
        _isTutorial = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TutorialBattleScene";

        if (!_isTutorial)
        {
            string prev = PlayerPrefs.GetString($"stage_{_stageIdx}_rank", "");
            if (IsBetterRank(rank, prev))
                PlayerPrefs.SetString($"stage_{_stageIdx}_rank", rank);

            if (_unlockNext)
            {
                int unlocked = PlayerPrefs.GetInt("unlocked_stages", 1);
                if (_stageIdx + 1 >= unlocked)
                    PlayerPrefs.SetInt("unlocked_stages", _stageIdx + 2);
                PlayerPrefs.SetInt($"mugu_stage_{_stageIdx}", 1);
            }
            PlayerPrefs.Save();
        }

        StartCoroutine(ShowResult(rank));
    }

    void AdvancePage()
    {
        if (_currentPage == Page.Page1)
        {
            if (_unlockNext)
            {
                _page1GO?.SetActive(false);
                if (_isTutorial) GoTutorialClear();
                else ShowPage2();
            }
            else
            {
                if (_isTutorial) GoRetry();
                else GoLobby();
            }
        }
        else if (_currentPage == Page.Page2)
        {
            GoLobby();
        }
    }

    IEnumerator ShowResult(string rank)
    {
        var old = GameObject.Find("ResultCanvas");
        if (old != null) Destroy(old);

        var cvGO = new GameObject("ResultCanvas");
        var cv   = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 99;

        var sc = cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1672f, 941f);
        sc.matchWidthOrHeight  = 1f; // 세로(높이) 기준 고정 — 와이드/울트라와이드에서 위아래 잘림 방지

        cvGO.AddComponent<GraphicRaycaster>();
        _cvRoot = cvGO.transform;

        MakeImg(_cvRoot, "Dim", null, new Color(0f, 0f, 0f, 0.6f));

        // Page 1
        _page1GO = new GameObject("Page1");
        _page1GO.transform.SetParent(_cvRoot, false);
        var p1Bg = _page1GO.AddComponent<Image>();
        p1Bg.color = Color.clear;
        p1Bg.raycastTarget = false;
        FullStretch((RectTransform)_page1GO.transform);

        MakeImg(_page1GO.transform, "Popup",    _sprPopup);
        MakeImg(_page1GO.transform, "RankText", _sprRankText);

        Sprite rankSpr = rank == "S" ? _sprRankS
                       : rank == "A" ? _sprRankA
                       : rank == "B" ? _sprRankB
                       :               _sprRankC;
        MakeImg(_page1GO.transform, "RankIcon", rankSpr);

        if (_unlockNext)
        {
            if (!_isTutorial)
                MakeImg(_page1GO.transform, "UnlockText", _sprUnlockText);
            MakeArrowBtn(_page1GO.transform, () =>
            {
                _page1GO.SetActive(false);
                if (_isTutorial) GoTutorialClear();
                else ShowPage2();
            });
        }
        else
        {
            MakeImg(_page1GO.transform, "RetryText", _sprRetryText);
            MakeArrowBtn(_page1GO.transform, _isTutorial ? (UnityEngine.Events.UnityAction)GoRetry : GoLobby);
        }

        _currentPage = Page.Page1;
        yield return null;
    }

    void ShowPage2()
    {
        var page2GO = new GameObject("Page2");
        page2GO.transform.SetParent(_cvRoot, false);
        var p2Bg = page2GO.AddComponent<Image>();
        p2Bg.color = Color.clear;
        p2Bg.raycastTarget = false;
        FullStretch((RectTransform)page2GO.transform);

        MakeImg(page2GO.transform, "Popup",      _sprPopup);
        MakeImg(page2GO.transform, "EquipTitle", _sprEquipTitle);
        MakeImg(page2GO.transform, "MuguText",   _sprMuguText);
        MakeImg(page2GO.transform, "MuguItem",   _sprMuguItem);
        MakeArrowBtn(page2GO.transform, GoLobby);

        _currentPage = Page.Page2;
    }

    void GoLobby()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
    }

    void GoRetry()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("TutorialBattleScene");
    }

    void GoTutorialClear()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("TutorialClearScene");
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
        img.alphaHitTestMinimumThreshold = 0.1f; // 투명 영역 클릭 차단

        go.AddComponent<Button>().onClick.AddListener(onClick);
    }

    static void FullStretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static bool IsBetterRank(string newRank, string oldRank)
    {
        int Score(string rk) => rk == "S" ? 3 : rk == "A" ? 2 : rk == "B" ? 1 : 0;
        return Score(newRank) > Score(oldRank);
    }
}
