using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Detects battle end, calculates rank, and shows result screen.
/// No fail state. Rank B or above unlocks the next stage.
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

    void Update()
    {
        if (_triggered) return;

        // F1: dev shortcut — instant complete with S rank
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame)
        {
            Trigger(devRank: "S");
            return;
        }

        if (RhythmManager.Instance == null) return;

        if (RhythmManager.Instance.IsMusicDone) Trigger(devRank: null);
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
        float  acc  = devRank != null ? 100f : (gm != null ? gm.Accuracy * 100f : 0f);

        int  stageIdx  = PlayerPrefs.GetInt("current_stage", 0);
        bool unlockNext = rank == "S" || rank == "A" || rank == "B";

        // Save best rank
        string prev = PlayerPrefs.GetString($"stage_{stageIdx}_rank", "");
        if (IsBetterRank(rank, prev))
            PlayerPrefs.SetString($"stage_{stageIdx}_rank", rank);

        // Unlock next stage (rank B or above)
        if (unlockNext)
        {
            int unlocked = PlayerPrefs.GetInt("unlocked_stages", 1);
            if (stageIdx + 1 >= unlocked)
                PlayerPrefs.SetInt("unlocked_stages", stageIdx + 2);
            PlayerPrefs.SetInt($"mugu_stage_{stageIdx}", 1);
        }

        PlayerPrefs.Save();
        StartCoroutine(ShowResultScreen(rank, acc, stageIdx, unlockNext));
    }

    IEnumerator ShowResultScreen(string rank, float acc, int stageIdx, bool unlockNext)
    {
        var p = BuildCanvas();

        MakeStretch(p, new Color(0f, 0f, 0f, 0.85f));

        // Title color depends on unlock result
        Color titleCol = unlockNext
            ? new Color(1f, 0.9f, 0.1f)
            : new Color(0.7f, 0.7f, 0.7f);
        string titleText = unlockNext ? "STAGE CLEAR!" : "STAGE COMPLETE";
        MakeC(p, titleText, 68f, titleCol, 0f, 320f, 900f, 90f);

        Color rankCol = rank == "S" ? new Color(1f, 0.85f, 0f)
                      : rank == "A" ? new Color(0.4f, 0.8f, 1f)
                      : rank == "B" ? new Color(0.4f, 1f, 0.5f)
                      :               new Color(0.8f, 0.8f, 0.8f);
        MakeC(p, rank,                    110f, rankCol,     0f, 180f, 200f, 130f);
        MakeC(p, $"Accuracy  {acc:F1}%",   26f, Color.white, 0f,  70f, 500f,  50f);

        var ln = new GameObject("Line"); ln.transform.SetParent(p, false);
        var lr = ln.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.1f, 0.5f); lr.anchorMax = new Vector2(0.9f, 0.5f);
        lr.anchoredPosition = new Vector2(0f, 10f); lr.sizeDelta = new Vector2(0f, 2f);
        ln.AddComponent<Image>().color = new Color(0.5f, 0.4f, 0.7f, 0.5f);

        if (unlockNext)
        {
            var (muguName, muguSprite) = StageRewards[Mathf.Clamp(stageIdx, 0, StageRewards.Length - 1)];
            MakeC(p, "MUGU OBTAINED", 20f, new Color(0.8f, 0.6f, 1f), 0f, -40f, 400f, 36f);

            var ig = MakeCImg(p, 0f, -150f, 150f, 150f);
            var ii = ig.AddComponent<Image>();
            var sp = Resources.Load<Sprite>(muguSprite);
            if (sp != null) { ii.sprite = sp; ii.preserveAspect = true; }
            else            { ii.color  = new Color(0.6f, 0.3f, 0.8f); }

            MakeC(p, muguName, 22f, new Color(1f, 0.85f, 0.4f), 0f, -265f, 500f, 40f);
        }
        else
        {
            MakeC(p, "Reach rank B or above to unlock the next stage", 22f,
                  new Color(0.7f, 0.5f, 0.5f), 0f, -120f, 700f, 40f);
        }

        MakeBtn(p, "LOBBY", 0f, -370f, 320f, 62f, new Color(0.15f, 0.45f, 0.70f), () =>
        {
            SFXManager.PlayConfirm();
            Time.timeScale = 1f;
            SceneManager.LoadScene("LobbyScene");
        });

        yield return null;
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

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
        r.pivot            = new Vector2(0.5f, 0.5f);
        r.sizeDelta        = new Vector2(w, h);
        r.anchoredPosition = new Vector2(ox, oy);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text          = text;
        t.fontSize      = size;
        t.alignment     = TextAlignmentOptions.Center;
        t.color         = color;
        t.raycastTarget = false;
    }

    static GameObject MakeCImg(Transform p, float ox, float oy, float w, float h)
    {
        var go = new GameObject("Img");
        go.transform.SetParent(p, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot            = new Vector2(0.5f, 0.5f);
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
        r.pivot            = new Vector2(0.5f, 0.5f);
        r.sizeDelta        = new Vector2(w, h);
        r.anchoredPosition = new Vector2(ox, oy);
        go.AddComponent<Image>().color = color;
        go.AddComponent<Button>().onClick.AddListener(onClick);

        var lbl = new GameObject("L"); lbl.transform.SetParent(go.transform, false);
        var lr  = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.text          = label;
        t.fontSize      = 30f;
        t.fontStyle     = FontStyles.Bold;
        t.alignment     = TextAlignmentOptions.Center;
        t.color         = Color.white;
        t.raycastTarget = false;
    }

    static bool IsBetterRank(string newRank, string oldRank)
    {
        int Score(string rk) => rk == "S" ? 3 : rk == "A" ? 2 : rk == "B" ? 1 : 0;
        return Score(newRank) > Score(oldRank);
    }
}
