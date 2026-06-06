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
        // 캔버스 생성 (기준해상도 1672×941)
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
        var page1 = new GameObject("Page1");
        page1.transform.SetParent(cvGO.transform, false);
        Stretch(page1);

        MakeImg(page1.transform, "Popup",    _sprPopup,  Color.white, false);
        MakeImg(page1.transform, "RankText", _sprRankText, Color.white, false);

        Sprite rankSpr = rank == "S" ? _sprRankS
                       : rank == "A" ? _sprRankA
                       : rank == "B" ? _sprRankB
                       :               _sprRankC;
        MakeImg(page1.transform, "RankIcon", rankSpr, Color.white, false);

        if (unlockNext)
        {
            MakeImg(page1.transform, "UnlockText", _sprUnlockText, Color.white, false);

            // 화살표 버튼 → Page 2로 전환
            var arrowGO = MakeImg(page1.transform, "Arrow", _sprArrow, Color.white, true);
            arrowGO.AddComponent<Button>().onClick.AddListener(() =>
            {
                page1.SetActive(false);
                ShowPage2(cvGO.transform, stageIdx);
            });
        }
        else
        {
            // 재도전 버튼 → 현재 씬 재시작
            var retryGO = MakeImg(page1.transform, "Retry", _sprRetryText, Color.white, true);
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
        var page2 = new GameObject("Page2");
        page2.transform.SetParent(cvRoot, false);
        Stretch(page2);

        MakeImg(page2.transform, "Popup",     _sprPopup,     Color.white, false);
        MakeImg(page2.transform, "EquipTitle",_sprEquipTitle, Color.white, false);
        MakeImg(page2.transform, "MuguText",  _sprMuguText,  Color.white, false);
        MakeImg(page2.transform, "MuguItem",  _sprMuguItem,  Color.white, false);

        // 전체 클릭 → 로비로
        var hitGO = new GameObject("HitArea");
        hitGO.transform.SetParent(page2.transform, false);
        Stretch(hitGO);
        var hitImg = hitGO.AddComponent<Image>();
        hitImg.color = Color.clear;
        hitGO.AddComponent<Button>().onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
        });
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────

    static GameObject MakeImg(Transform parent, string goName, Sprite sprite, Color color, bool raycast)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        Stretch(go);
        var img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.color          = color;
        img.raycastTarget  = raycast;
        if (sprite != null) img.preserveAspect = false;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var r = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
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
