using System.Collections;
using UnityEngine;

/// One-shot background audio for a fixed-length scene (e.g. Stage1TitleCard's IntroScene, which
/// hands off to SceneTransitionManager after `displaySeconds`) — plays `clip` starting at volume 0,
/// fades in over `fadeInSeconds`, holds at `targetVolume`, then fades out over `fadeOutSeconds` so it
/// reaches silence right as `totalDuration` elapses. Meant for a clip that's slightly longer than the
/// scene itself (fading out early hides the abrupt cut instead of the clip audibly restarting/looping
/// or getting yanked mid-note when the scene unloads).
///
/// `totalDuration` is independent from the actual clip length — set it to match whatever duration
/// actually drives the scene's transition (e.g. Stage1TitleCard.displaySeconds) so the audio and the
/// visual handoff land together.
[RequireComponent(typeof(AudioSource))]
public class AudioFadeInOut : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [SerializeField] float targetVolume  = 1f;
    [SerializeField] float fadeInSeconds  = 0.5f;
    [SerializeField] float fadeOutSeconds = 1.5f;
    [Tooltip("이 씬이 실제로 유지되는 시간(초) — 보통 Stage1TitleCard의 displaySeconds와 맞춤. " +
             "페이드아웃이 이 시점에 정확히 0으로 끝나도록 역산됨.")]
    [SerializeField] float totalDuration = 5f;

    AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop        = false;
        _source.clip        = clip;
    }

    void Start()
    {
        if (clip == null) return;
        _source.volume = 0f;
        _source.Play();
        StartCoroutine(FadeSequence());
    }

    IEnumerator FadeSequence()
    {
        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.deltaTime;
            _source.volume = Mathf.Lerp(0f, targetVolume, t / fadeInSeconds);
            yield return null;
        }
        _source.volume = targetVolume;

        float fadeOutStart = Mathf.Max(fadeInSeconds, totalDuration - fadeOutSeconds);
        float holdSeconds = fadeOutStart - fadeInSeconds;
        if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);

        t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.deltaTime;
            _source.volume = Mathf.Lerp(targetVolume, 0f, t / fadeOutSeconds);
            yield return null;
        }
        _source.volume = 0f;
        _source.Stop();
    }
}
