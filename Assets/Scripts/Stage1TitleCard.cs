using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Glue for a stage's IntroScene (Stage1_1IntroScene ~ Stage1_5IntroScene) — a short "episode title
/// card" with no music: shows the stage title (+ an optional one-shot SFX) for a fixed duration,
/// then hands off to SceneTransitionManager, which already owns its own fade-to-black/fade-in
/// (see SceneTransitionManager.LoadSceneWithFade) — this script does not build a separate fade
/// panel of its own.
///
/// Each IntroScene is its own small scene (see project decision: intro kept separate from the
/// tutorial+main scene for maintainability) with a Canvas holding this component plus whatever
/// stage-specific flourish objects (e.g. 1-1's wave visual/SFX) that scene wants to show alongside
/// the shared title text.
/// </summary>
public class Stage1TitleCard : MonoBehaviour
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] string   titleString = "1-1. 타기";
    [SerializeField] float    displaySeconds = 5f;
    [SerializeField] AudioClip oneShotSfx;
    [Tooltip("SFX가 인트로 시작 후 몇 초 뒤에 재생될지 (0 = 즉시).")]
    [SerializeField] float    sfxDelay = 0f;
    [SerializeField] string   nextSceneName = "Stage1_1Scene";

    AudioSource _sfxSource;

    void Awake()
    {
        // 이전 플레이 세션(다른 씬의 일시정지 등)에서 Time.timeScale이 0으로 남아있으면
        // Sequence()의 WaitForSeconds가 영원히 멈춤 — CallClock.Awake()의 방어 로직과 동일한 이유.
        Time.timeScale = 1f;

        if (titleText != null) titleText.text = titleString;

        if (oneShotSfx != null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
        }
    }

    void Start()
    {
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        // sfxDelay is a moment WITHIN the display window, not additional time on top of it.
        if (_sfxSource != null && sfxDelay > 0f)
            yield return new WaitForSeconds(Mathf.Min(sfxDelay, displaySeconds));

        if (_sfxSource != null)
            _sfxSource.PlayOneShot(oneShotSfx);

        float remaining = displaySeconds - Mathf.Min(sfxDelay, displaySeconds);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        SceneTransitionManager.Instance.LoadSceneWithFade(nextSceneName);
    }
}
