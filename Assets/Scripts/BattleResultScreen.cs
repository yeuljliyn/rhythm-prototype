using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 배틀 종료 감지 → 랭크 계산 → 클리어/실패 화면 표시.
/// </summary>
public class BattleResultScreen : MonoBehaviour
{
    static readonly (string name, string sprite)[] StageRewards =
    {
        ("Mugu: Water Ghost Hair",  "물귀신_배경제거"),
        ("Mugu: Spirit Shackle",    "지박령_배경제거"),
        ("Mugu: Ghost Robe",        "총각귀신_배경제거"),
        ("Mugu: Ghost Comb",        "처녀귀신_배경제거"),
        ("Mugu: Egg Ghost Eye",     "달걀귀신_배경제거"),
    };

    bool _triggered;
    bool _devClear;

    void Update()
    {
        if (_triggered) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.f1Key.wasPressedThisFrame) { _devClear = true;  Trigger(playerDied: false); return; }
            if (kb.f2Key.wasPressedThisFrame) { _devClear = false; Trigger(playerDied: true);  return; }
        }

        if (RhythmManager.Instance == null || NoteManager.Instance == null) return;

        if (PlayerHealthManager.Instance != null && PlayerHealthManager.Instance.Hearts <= 0)
        {
            Trigger(playerDied: true);
            return;
        }

        bool done = RhythmManager.Instance.IsScheduleDone
                 && NoteManager.Instance.Count == 0
                 && RhythmManager.Instance.SongTime > 0f;
        if (done) Trigger(playerDied: false);
    }

    void Trigger(bool playerDied)
    {
        if (_triggered) return;
        _triggered = true;

        Time.timeScale = 0f;

        var audioSrc = FindObjectOfType<AudioSource>();
        if (audioSrc != null && audioSrc.isPlaying) audioSrc.Pause();

        var    gm      = GameManager.Instance;
        bool   isClear = _devClear || (!playerDied && gm != null && gm.IsClear);
        string rank    = _devClear ? "S"    : (gm != null ? gm.GetRank()         : "C");
        float  acc     = _devClear ? 100f   : (gm != null ? gm.Accuracy * 100f   : 0f);

        if (isClear)
        {
            int stageIdx = PlayerPrefs.GetInt("current_stage", 0);
            int unlocked = PlayerPrefs.GetInt("unlocked_stages", 1);
            if (stageIdx + 1 >= unlocked)
                PlayerPrefs.SetInt("unlocked_stages", stageIdx + 2);
            string prev = PlayerPrefs.GetString($"stage_{stageIdx}_rank", "");
            if (IsBetterRank(rank, prev))
                PlayerPrefs.SetString($"stage_{stageIdx}_rank", rank);
            PlayerPrefs.SetInt($"mugu_stage_{stageIdx}", 1);
            PlayerPrefs.Save();
            StartCoroutine(ShowClearScreen(rank, acc, stageIdx));
        }
        else
        {
            BuildFailScreen(rank, acc);
        }
    }

    // ── 클리어 화면 ──────────────────────────────────────────────────────────

    IEnumerator ShowClearScreen(string rank, float acc, int stageIdx)
    {
        var p = BuildCanvas();

        MakeStretch(p, new Color(0f, 0f, 0f, 0.85f));
        MakeC(p, "STAGE CLEAR!", 68f, new Color(1f, 0.9f, 0.1f),  0f,  320f, 900f, 90f);

        Color rankCol = rank == "S" ? new Color(1f, 0.85f, 0f)
                      : rank == "A" ? new Color(0.4f, 0.8f, 1f)
                      : rank == "B" ? new Color(0.4f, 1f, 0.5f)
                      :               new Color(0.8f, 0.8f, 0.8f);
        MakeC(p, rank,                    110f, rankCol,       0f, 180f, 200f, 130f);
        MakeC(p, $"Accuracy  {acc:F1}%",   26f, Color.white,   0f,  70f, 500f,  50f);

        var ln = new GameObject("Line"); ln.transform.SetParent(p, false);
        var lr = ln.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.1f, 0.5f); lr.anchorMax = new Vector2(0.9f, 0.5f);
        lr.anchoredPosition = new Vector2(0f, 10f); lr.sizeDelta = new Vector2(0f, 2f);
        ln.AddComponent<Image>().color = new Color(0.5f, 0.4f, 0.7f, 0.5f);

        var (muguName, muguSprite) = StageRewards[Mathf.Clamp(stageIdx, 0, StageRewards.Length - 1)];
        MakeC(p, "MUGU OBTAINED", 20f, new Color(0.8f, 0.6f, 1f), 0f, -40f, 400f, 36f);

        var ig = MakeCImg(p, 0f, -150f, 150f, 150f);
        var ii = ig.AddComponent<Image>();
        var sp = Resources.Load<Sprite>(muguSprite);
        if (sp != null) { ii.sprite = sp; ii.preserveAspect = true; }
        else            { ii.color  = new Color(0.6f, 0.3f, 0.8f); }

        MakeC(p, muguName, 22f, new Color(1f, 0.85f, 0.4f), 0f, -265f, 500f, 40f);

        MakeBtn(p, "LOBBY", 0f, -350f, 320f, 62f, new Color(0.15f, 0.55f, 0.30f), () =>
        {
            SFXManager.PlayConfirm();
            Time.timeScale = 1f;
            SceneManager.LoadScene("LobbyScene");
        });

        yield return null;
    }

    // ── 실패 화면 ────────────────────────────────────────────────────────────

    void BuildFailScreen(string rank, float acc)
    {
        var p = BuildCanvas();
        MakeStretch(p, new Color(0f, 0f, 0f, 0.88f));

        MakeC(p, "STAGE FAILED", 68f, new Color(1f, 0.25f, 0.2f),  0f,  260f, 900f, 90f);
        MakeC(p, rank,            90f, new Color(0.8f, 0.3f, 0.3f), 0f,   90f, 180f, 120f);
        MakeC(p, $"Accuracy  {acc:F1}%  (B rank or above to clear)",
                                  22f, new Color(0.7f, 0.7f, 0.7f), 0f,  -10f, 750f,  50f);

        MakeBtn(p, "RETRY", -170f, -150f, 260f, 70f, new Color(0.15f, 0.45f, 0.70f), () =>
        {
            SFXManager.PlayClick();
            Time.timeScale = 1f;
            GameManager.Instance?.ResetGame();
            SceneManager.LoadScene("BattleScene");
        });
        MakeBtn(p, "LOBBY",  170f, -150f, 260f, 70f, new Color(0.25f, 0.15f, 0.35f), () =>
        {
            SFXManager.PlayBack();
            Time.timeScale = 1f;
            SceneManager.LoadScene("LobbyScene");
        });
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────────────

    static Transform BuildCanvas()
    {
        var old = GameObject.Find("ResultPanel");
        if (old != null) Object.Destroy(old);

        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO != null)
        {
            var panel = new GameObject("ResultPanel");
            panel.transform.SetParent(canvasGO.transform, false);
            var r = panel.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            panel.transform.SetAsLastSibling();
            return panel.transform;
        }

        // fallback
        var go = new GameObject("ResultCanvas");
        var cv = go.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 99;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    static void MakeStretch(Transform parent, Color color)
    {
        var go = new GameObject("BG");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
    }

    static void MakeC(Transform p, string text, float size, Color color,
                      float ox, float oy, float w, float h)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(p, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot     = new Vector2(0.5f, 0.5f);
        r.sizeDelta        = new Vector2(w, h);
        r.anchoredPosition = new Vector2(ox, oy);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color     = color;
        t.raycastTarget = false;
    }

    static GameObject MakeCImg(Transform p, float ox, float oy, float w, float h)
    {
        var go = new GameObject("Img");
        go.transform.SetParent(p, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot     = new Vector2(0.5f, 0.5f);
        r.sizeDelta        = new Vector2(w, h);
        r.anchoredPosition = new Vector2(ox, oy);
        return go;
    }

    static void MakeBtn(Transform p, string label, float ox, float oy, float w, float h,
                        Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(p, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot     = new Vector2(0.5f, 0.5f);
        r.sizeDelta        = new Vector2(w, h);
        r.anchoredPosition = new Vector2(ox, oy);
        go.AddComponent<Image>().color = color;
        go.AddComponent<Button>().onClick.AddListener(onClick);

        var lbl = new GameObject("L"); lbl.transform.SetParent(go.transform, false);
        var lr  = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 30f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color     = Color.white;
        t.raycastTarget = false;
    }

    static bool IsBetterRank(string newRank, string oldRank)
    {
        int Score(string rk) => rk == "S" ? 3 : rk == "A" ? 2 : rk == "B" ? 1 : 0;
        return Score(newRank) > Score(oldRank);
    }
}
