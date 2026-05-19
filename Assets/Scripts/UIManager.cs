using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI judgementText;
    public TextMeshProUGUI hintText;

    Coroutine _judgeFadeRoutine;
    Coroutine _comboPulseRoutine;
    Vector3   _judgementOrigin;

    void Start()
    {
        GameManager.Instance.OnScoreChanged += RefreshComboUI;
        GameManager.Instance.OnJudgement    += ShowJudgement;
        RefreshComboUI(0, 0);

        if (judgementText != null)
            _judgementOrigin = judgementText.transform.localPosition;

        if (hintText != null)
            hintText.text = "Any key / click → HIT  |  ← ↓ ↑ → → follow BONUS COMMAND";
    }

    void RefreshComboUI(int score, int combo)
    {
        if (comboText == null) return;
        comboText.text = combo >= 2 ? $"{combo}<size=60%>x</size>" : "";
        if (combo >= 2)
        {
            if (_comboPulseRoutine != null) StopCoroutine(_comboPulseRoutine);
            _comboPulseRoutine = StartCoroutine(PulseCombo());
        }
    }

    void ShowJudgement(JudgementType type)
    {
        if (judgementText == null) return;

        string txt;
        Color  col;
        if      (type == JudgementType.Perfect) { txt = "PERFECT"; col = new Color(1f, 0.92f, 0.10f); }
        else if (type == JudgementType.Good)    { txt = "GOOD";    col = new Color(0.20f, 1f, 0.50f); }
        else                                    { txt = "MISS";    col = new Color(1f, 0.22f, 0.18f); }

        judgementText.text  = txt;
        judgementText.color = col;
        if (_judgeFadeRoutine != null) StopCoroutine(_judgeFadeRoutine);
        _judgeFadeRoutine = StartCoroutine(FadeJudgement(col));
    }

    IEnumerator FadeJudgement(Color startColor)
    {
        float duration = 0.7f, elapsed = 0f;
        judgementText.transform.localPosition = _judgementOrigin;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            judgementText.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            judgementText.transform.localPosition = _judgementOrigin + Vector3.up * 40f * t;
            yield return null;
        }
        judgementText.text = "";
        judgementText.transform.localPosition = _judgementOrigin;
    }

    IEnumerator PulseCombo()
    {
        comboText.transform.localScale = Vector3.one * 1.35f;
        float elapsed = 0f, duration = 0.15f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            comboText.transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, elapsed / duration);
            yield return null;
        }
        comboText.transform.localScale = Vector3.one;
    }
}
