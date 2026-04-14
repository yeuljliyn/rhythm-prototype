using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
// v2
public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI judgementText;
    public TextMeshProUGUI titleText;

    Coroutine judgeFadeRoutine;
    Coroutine comboPulseRoutine;

    void Start()
    {
        GameManager.Instance.OnScoreChanged += RefreshScoreUI;
        GameManager.Instance.OnJudgement    += ShowJudgement;
        RefreshScoreUI(0, 0);
        if (titleText != null)
            titleText.text = "CYBER SHAMAN\n<size=60%>SPIRIT CLEANSE</size>";
    }

    void RefreshScoreUI(int score, int combo)
    {
        if (scoreText != null)
            scoreText.text = score.ToString("D7");
        if (comboText != null)
        {
            comboText.text = combo >= 2 ? combo + "<size=60%>x</size>" : "";
            if (comboPulseRoutine != null) StopCoroutine(comboPulseRoutine);
            if (combo >= 2) comboPulseRoutine = StartCoroutine(PulseCombo());
        }
    }

    void ShowJudgement(JudgementType type)
    {
        if (judgementText == null) return;

        string txt;
        Color col;
        if (type == JudgementType.Perfect)      { txt = "PERFECT!"; col = new Color(1f, 0.95f, 0.1f); }
        else if (type == JudgementType.Good)    { txt = "GOOD";     col = new Color(0.2f, 1f, 0.5f); }
        else                                    { txt = "MISS";     col = new Color(1f, 0.2f, 0.2f); }

        judgementText.text  = txt;
        judgementText.color = col;
        if (judgeFadeRoutine != null) StopCoroutine(judgeFadeRoutine);
        judgeFadeRoutine = StartCoroutine(FadeJudgement(col));
    }

    IEnumerator FadeJudgement(Color startColor)
    {
        float duration = 0.7f;
        float elapsed  = 0f;
        Vector3 startPos = judgementText.transform.localPosition;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            judgementText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            judgementText.transform.localPosition = startPos + Vector3.up * 40f * (elapsed / duration);
            yield return null;
        }
        judgementText.text = "";
        judgementText.transform.localPosition = startPos;
    }

    IEnumerator PulseCombo()
    {
        comboText.transform.localScale = Vector3.one * 1.35f;
        float elapsed = 0f;
        float duration = 0.15f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(1.35f, 1f, elapsed / duration);
            comboText.transform.localScale = Vector3.one * s;
            yield return null;
        }
        comboText.transform.localScale = Vector3.one;
    }
}
