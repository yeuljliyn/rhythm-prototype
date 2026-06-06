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

    bool _triggered;

    void Update()
    {
        if (_triggered) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame) { Trigger("S"); return; }

        if (RhythmManager.Instance == null) return;
        if (RhythmManager.Instance.IsMusicDone) Trigger(null);
    }

    void Trigger(string devRank)
    {
        if (_triggered) return;
        _triggered = true;
        Time.timeScale = 0f;

        var audioSrc = FindObjectOfType<AudioSource>();
        if (audioSrc != null && audioSrc.isPlaying) audioSrc.Pause();

        var    gm   = GameManager.Instance;
        string rank = devRank ?? (gm != null ? gm.GetRank() : "C");

        int  stageIdx   = PlayerPrefs.GetInt("current_stage", 0);
        bool unlockNext = rank == "S" || rank == "A" || rank == "B";

        string prev = PlayerPrefs.GetString($"stage_{stageIdx}_rank", "");
        if (IsBetterRank(rank, prev))
            PlayerPrefs.SetString($"stage_{stageIdx}_rank", rank);

        if (unlockNext)
        {
            int unlocked = PlayerPrefs.GetInt("unlocked_stages", 1);
            if (stageIdx + 1 >= unlocked)
                PlayerPrefs.SetInt("unlocked_stages", stageIdx + 2);
            PlayerPrefs.SetInt($"mugu_stage_{stageIdx}", 1);
        }
        PlayerPrefs.Save();

        StartCoroutine(ShowResult(rank, stageIdx, unlockNext));
    }

    IEnumerator ShowResult(string rank, int stageIdx, bool unlockNext)
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
        sc.matchWidthOrHeight  = 0.5f;

        cvGO.AddComponent<GraphicRaycaster>();

        // 어두운 오버레이
        MakeImg(cvGO.transform, "Dim", null, new Color(0f, 0f, 0f, 0.6f), false);

        // ── Page 1 ──────────────────────────────────────────────────────────
        // Image를 먼저 추가해야 Transform → RectTransform으로 변환됨
        var page1GO = new GameObject("Page1");
        page1GO.transform.SetParent(cvGO.transform, false);
        var page1Img = page1GO.AddComponent<Image>();
        page1Img.color = Color.clear;
        page1Img.raycastTarget = false;
        FullStretch((RectTransform)page1GO.transform);

        MakeImg(page1GO.transform, "Popup",    _sprPopup,    Color.white, false);
        MakeImg(page1GO.transform, "RankText", _sprRankText, Color.white, false);

        Sprite rankSpr = rank == "S" ? _sprRankS
                       : rank == "A" ? _sprRankA
                       : rank == "B" ? _sprRankB
                       :               _sprRankC;
        MakeImg(page1GO.transform, "RankIcon", rankSpr, Color.white, false);

        if (unlockNext)
        {
            MakeImg(page1GO.transform, "UnlockText", _sprUnlockText, Color.white, false);

            var arrowGO = MakeImg(page1GO.transform, "Arrow", _sprArrow, Color.white, true);
            arrowGO.AddComponent<Button>().onClick.AddListener(() =>
            {
                page1GO.SetActive(false);
                ShowPage2(cvGO.transform, stageIdx);
            });
        }
        else
        {
            var retryGO = MakeImg(page1GO.transform, "Retry", _sprRetryText, Color.white, true);
            retryGO.AddComponent<Button>().onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                SceneTransitionManager.Instance.LoadSceneWithFade(SceneManager.GetActiveScene().name);
            });
        }

        yield return null;
    }

    void ShowPage2(Transform cvRoot, int stageIdx)
    {
        var page2GO = new GameObject("Page2");
        page2GO.transform.SetParent(cvRoot, false);
        var page2Img = page2GO.AddComponent<Image>();
        page2Img.color = Color.clear;
        page2Img.raycastTarget = false;
        FullStretch((RectTransform)page2GO.transform);

        MakeImg(page2GO.transform, "Popup",      _sprPopup,      Color.white, false);
        MakeImg(page2GO.transform, "EquipTitle", _sprEquipTitle, Color.white, false);
        MakeImg(page2GO.transform, "MuguText",   _sprMuguText,   Color.white, false);
        MakeImg(page2GO.transform, "MuguItem",   _sprMuguItem,   Color.white, false);

        // 화면 전체 클릭 → 로비로
        var hitGO  = new GameObject("HitArea");
        hitGO.transform.SetParent(page2GO.transform, false);
        var hitImg = hitGO.AddComponent<Image>();
        hitImg.color = Color.clear;
        hitImg.raycastTarget = true;
        FullStretch((RectTransform)hitGO.transform);
        hitGO.AddComponent<Button>().onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
        });
    }

    // Image를 먼저 추가해 RectTransform을 확보한 뒤 반환
    static GameObject MakeImg(Transform parent, string goName, Sprite sprite, Color color, bool raycast)
    {
        var go  = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); // Transform → RectTransform 자동 변환
        FullStretch((RectTransform)go.transform);
        img.sprite        = sprite;
        img.color         = color;
        img.raycastTarget = raycast;
        if (sprite != null) img.preserveAspect = false;
        return go;
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
