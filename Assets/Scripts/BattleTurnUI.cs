using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스/플레이어 턴 전환을 화면에 명확히 표시.
/// BattleDirector가 static 메서드(Fire*)를 호출하면 UI가 반응함.
/// BattleDirector.Start()에서 자동으로 AddComponent됨.
/// </summary>
public class BattleTurnUI : MonoBehaviour
{
    public static event Action                             OnBossTurnBegin;
    public static event Action<List<DirectionInputEntry>> OnPlayerTurnBegin;
    public static event Action<int, JudgementType>        OnPlayerStep;

    // ArrowDir: Left=0, Right=1, Up=2, Down=3
    static readonly string[] DirChar = { "←", "→", "↑", "↓" };

    TextMeshProUGUI   _bannerText;
    Image             _bannerBG;
    TextMeshProUGUI[] _arrowLabels;
    GameObject        _arrowRow;
    Coroutine         _bannerCr;

    void Awake()
    {
        BuildUI();
        OnBossTurnBegin   += HandleBossTurn;
        OnPlayerTurnBegin += HandlePlayerTurn;
        OnPlayerStep      += HandlePlayerStep;
    }

    void OnDestroy()
    {
        OnBossTurnBegin   -= HandleBossTurn;
        OnPlayerTurnBegin -= HandlePlayerTurn;
        OnPlayerStep      -= HandlePlayerStep;
    }

    // ── UI 생성 ──────────────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = GameObject.Find("Canvas");
        Transform parent;
        if (canvasGO != null)
        {
            parent = canvasGO.transform;
        }
        else
        {
            var go = new GameObject("TurnUICanvas");
            var cv = go.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 50;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight  = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            parent = go.transform;
        }

        // 배너 배경
        var bgGO = new GameObject("TurnBannerBG");
        bgGO.transform.SetParent(parent, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = new Vector2(0f, 270f);
        bgRT.sizeDelta = new Vector2(600f, 110f);
        _bannerBG = bgGO.AddComponent<Image>();
        _bannerBG.color = Color.clear;

        // 배너 텍스트
        var textGO = new GameObject("TurnBannerText");
        textGO.transform.SetParent(bgGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;
        _bannerText = textGO.AddComponent<TextMeshProUGUI>();
        _bannerText.fontSize   = 82f;
        _bannerText.fontStyle  = FontStyles.Bold;
        _bannerText.alignment  = TextAlignmentOptions.Center;
        _bannerText.text       = "";
        _bannerText.raycastTarget = false;

        // 방향키 시퀀스 행
        _arrowRow = new GameObject("ArrowRow");
        _arrowRow.transform.SetParent(parent, false);
        var rowRT = _arrowRow.AddComponent<RectTransform>();
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, 155f);
        rowRT.sizeDelta = new Vector2(800f, 90f);
        _arrowRow.SetActive(false);
        _arrowLabels = new TextMeshProUGUI[0];
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────────

    void HandleBossTurn()
    {
        _arrowRow.SetActive(false);
        if (_bannerCr != null) StopCoroutine(_bannerCr);
        _bannerCr = StartCoroutine(ShowBanner("BOSS TURN", new Color(1f, 0.22f, 0.12f), new Color(0.5f, 0.05f, 0.05f, 0.75f)));
    }

    void HandlePlayerTurn(List<DirectionInputEntry> inputs)
    {
        if (_bannerCr != null) StopCoroutine(_bannerCr);
        _bannerCr = StartCoroutine(ShowBanner("YOUR TURN!", new Color(0.25f, 1f, 0.45f), new Color(0.05f, 0.35f, 0.12f, 0.75f)));
        RebuildArrows(inputs);
        _arrowRow.SetActive(true);
    }

    void HandlePlayerStep(int idx, JudgementType j)
    {
        if (_arrowLabels == null || idx >= _arrowLabels.Length) return;

        Color doneCol = j == JudgementType.Hit
            ? new Color(0.10f, 0.60f, 1.00f, 0.65f)
            : new Color(1f, 0.18f, 0.12f, 0.65f);
        _arrowLabels[idx].color    = doneCol;
        _arrowLabels[idx].fontSize = 48f;

        int next = idx + 1;
        if (next < _arrowLabels.Length)
            HighlightArrow(next);
        else
            StartCoroutine(HideArrowRowDelayed(0.5f));
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────

    void RebuildArrows(List<DirectionInputEntry> inputs)
    {
        foreach (Transform t in _arrowRow.transform) Destroy(t.gameObject);
        _arrowLabels = new TextMeshProUGUI[inputs.Count];

        float cell   = 78f;
        float gap    = 14f;
        float total  = inputs.Count * cell + (inputs.Count - 1) * gap;
        float startX = -total / 2f + cell / 2f;

        for (int i = 0; i < inputs.Count; i++)
        {
            var go = new GameObject("A" + i);
            go.transform.SetParent(_arrowRow.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(cell, 90f);
            rt.anchoredPosition = new Vector2(startX + i * (cell + gap), 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize      = 54f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.text          = DirChar[(int)inputs[i].direction];
            tmp.color         = new Color(0.55f, 0.55f, 0.55f, 0.65f);
            tmp.raycastTarget = false;
            _arrowLabels[i]   = tmp;
        }

        if (_arrowLabels.Length > 0) HighlightArrow(0);
    }

    void HighlightArrow(int idx)
    {
        if (idx >= _arrowLabels.Length) return;
        _arrowLabels[idx].color    = new Color(1f, 0.95f, 0.15f);
        _arrowLabels[idx].fontSize = 74f;
    }

    IEnumerator ShowBanner(string text, Color textCol, Color bgCol)
    {
        _bannerText.text  = text;
        _bannerText.color = textCol;
        _bannerBG.color   = bgCol;
        _bannerBG.transform.localScale = Vector3.one * 1.4f;

        // 팝인
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.15f;
            float s = Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            _bannerBG.transform.localScale = Vector3.one * s;
            yield return null;
        }
        _bannerBG.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(0.7f);

        // 페이드 아웃
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.3f;
            float a = 1f - Mathf.Clamp01(t);
            _bannerText.color = new Color(textCol.r, textCol.g, textCol.b, a);
            _bannerBG.color   = new Color(bgCol.r,   bgCol.g,   bgCol.b,   bgCol.a * a);
            yield return null;
        }
        _bannerText.text = "";
        _bannerBG.color  = Color.clear;
    }

    IEnumerator HideArrowRowDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        _arrowRow.SetActive(false);
    }

    // ── Static fire (BattleDirector에서 호출) ────────────────────────────────

    public static void FireBossTurn()
        => OnBossTurnBegin?.Invoke();

    public static void FirePlayerTurn(List<DirectionInputEntry> inputs)
        => OnPlayerTurnBegin?.Invoke(inputs);

    public static void FirePlayerStep(int idx, JudgementType j)
        => OnPlayerStep?.Invoke(idx, j);
}
