using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이어 턴 화살표 시퀀스 UI.
/// 보스 턴 시각 연출은 TurnSpotlightEffect가 담당.
/// </summary>
public class BattleTurnUI : MonoBehaviour
{
    public static event Action                             OnBossTurnBegin;
    public static event Action<List<DirectionInputEntry>> OnPlayerTurnBegin;
    public static event Action<int, JudgementType>        OnPlayerStep;

    // ArrowDir: Left=0, Right=1, Up=2, Down=3
    static readonly string[] DirChar = { "←", "→", "↑", "↓" };

    TextMeshProUGUI[] _arrowLabels;
    GameObject        _arrowRow;

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
            sc.matchWidthOrHeight  = 1f; // 세로(높이) 기준 고정 — 와이드/울트라와이드에서 위아래 잘림 방지
            go.AddComponent<GraphicRaycaster>();
            parent = go.transform;
        }

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
    }

    void HandlePlayerTurn(List<DirectionInputEntry> inputs)
    {
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
