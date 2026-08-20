using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage1_4 glue: Hand pours as a Call demo, PlayerAnt pours in Response, Clown just watches and
/// reacts to the outcome (Hit/Miss). Static Image sprite-swaps only (no Animator/AnimationClip) —
/// same style as Stage1SurviveVisuals. CallDirector stays completely unaware of
/// Hand/PlayerAnt/Clown, same as every other Stage1 minigame glue script.
/// </summary>
public class Stage1AntPourVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("Hand (Call)")]
    [SerializeField] Image handImage;
    [SerializeField] Sprite handIdleSprite;
    [SerializeField] Sprite handPourSprite;
    [SerializeField] float handPourHoldTime = 0.35f;

    [Header("Player Ant (Response)")]
    [SerializeField] Image playerAntImage;
    [SerializeField] Sprite playerAntIdleSprite;
    [SerializeField] Sprite playerAntPourSprite;
    [SerializeField] Sprite playerAntFailSprite;
    [SerializeField] float playerReactHoldTime = 0.35f;

    [Header("Player Ant — BPM 둠칫둠칫 (1-1의 Stage1WaveRideVisuals.AnimateAntsWithBpm()와 동일한 " +
            "방식: 피벗을 바닥에 고정해두고 Scale Y만 코사인 파형으로 오르내림 — clock.Pattern에서 " +
            "매 프레임 BPM을 읽어오므로 곡이 바뀌어도 그대로 맞음. 스프라이트 교체(붓기/실패)와는 " +
            "완전히 별개 축(Scale)이라 서로 안 건드림)")]
    [SerializeField] CallClock clock;
    [Tooltip("PlayerAnt의 RectTransform pivot.y가 0(바닥)이어야 통통 튀는 느낌이 남 — 중앙(0.5)이면 " +
             "위아래로 같이 흔들려서 그냥 커졌다 작아지는 것처럼 보임.")]
    [SerializeField] float playerBopScaleMultiplier = 1.08f;
    [Tooltip("한 박에 몇 번 출렁이는지. 1이면 한 박에 한 번 오르내림.")]
    [SerializeField] float playerBopCyclesPerBeat = 1f;

    Vector3 _playerBaseScale;

    [Header("Clown (reacts to outcome)")]
    [SerializeField] Image clownImage;
    [SerializeField] Sprite clownIdleSprite;
    [SerializeField] Sprite clownFailSprite;
    [SerializeField] Sprite clownSuccessSprite;
    [SerializeField] float clownReactHoldTime = 0.45f;

    [Header("Success balloons — float up on Hit")]
    [Tooltip("풍선이 스폰되는 부모 RectTransform. 캐릭터들(Clown/Hand/PlayerAnt) 뒤에서 떠오르게 " +
             "하려면 씬 하이러키에서 이 오브젝트를 그 캐릭터들보다 위(먼저 그려지는 순서)에 둘 것.")]
    [SerializeField] RectTransform balloonSpawnParent;
    [SerializeField] Sprite redBalloonSprite;
    [SerializeField] Sprite blueBalloonSprite;
    [Tooltip("풍선이 스폰되는 시작 Y(spawnParent 기준 anchoredPosition) — 화면 아래쪽에서 떠오르기 " +
             "시작하도록 보통 음수.")]
    [SerializeField] float balloonSpawnY = -450f;
    [Tooltip("화면 중앙(광대가 있는 자리)에서 좌/우로 이만큼 떨어진 곳에서 스폰 — 캐릭터 뒤가 아니라 " +
             "양옆에서 떠오르게 함. 매번 좌/우 중 랜덤으로 고름.")]
    [SerializeField] float balloonSideDistance = 700f;
    [Tooltip("좌/우 스폰 지점 기준 추가 랜덤 흔들림 폭.")]
    [SerializeField] float balloonSpreadX = 120f;
    [SerializeField] float balloonScale = 0.5f;
    [SerializeField] float balloonRiseHeight = 900f;
    [SerializeField] float balloonRiseDuration = 2.5f;
    [SerializeField] float balloonSwayAmplitude = 30f;
    [SerializeField] float balloonSwaySpeed = 2f;

    Coroutine _handCr, _playerCr, _clownCr;

    void OnEnable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup += HandleCallCue;
        callDirector.OnGroupJudged  += HandleGroupJudged;
        callDirector.OnResponsePress += HandlePlayerPress;
    }

    void OnDisable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup -= HandleCallCue;
        callDirector.OnGroupJudged  -= HandleGroupJudged;
        callDirector.OnResponsePress -= HandlePlayerPress;
    }

    void Start()
    {
        if (handImage != null)      handImage.sprite      = handIdleSprite;
        if (playerAntImage != null)
        {
            playerAntImage.sprite = playerAntIdleSprite;
            _playerBaseScale = playerAntImage.rectTransform.localScale;
        }
        if (clownImage != null)     clownImage.sprite     = clownIdleSprite;
    }

    void Update()
    {
        AnimatePlayerBop();
    }

    // Reads bpm live off clock.Pattern.BeatInterval every frame (not cached) so a chart/song swap is
    // picked up automatically — same reasoning as Stage1WaveRideVisuals. Only Scale.y is touched.
    void AnimatePlayerBop()
    {
        if (clock == null || playerAntImage == null) return;

        float beatInterval = clock.Pattern != null ? clock.Pattern.BeatInterval : 60f / 128f;
        float phase = clock.SongTime / beatInterval * playerBopCyclesPerBeat;
        float wave01 = (Mathf.Cos(phase * Mathf.PI * 2f) + 1f) * 0.5f; // phase 0 -> 1 (matches resting scale, no pop on song start)
        float scaleY = Mathf.Lerp(_playerBaseScale.y, _playerBaseScale.y * playerBopScaleMultiplier, wave01);

        var rect = playerAntImage.rectTransform;
        var scale = rect.localScale;
        scale.y = scaleY;
        rect.localScale = scale;
    }

    void HandleCallCue(int groupId)
    {
        if (_handCr != null) StopCoroutine(_handCr);
        _handCr = StartCoroutine(HandPulse());
    }

    void HandleGroupJudged(int groupId, JudgementType j)
    {
        bool hit = j == JudgementType.Hit;

        if (_playerCr != null) StopCoroutine(_playerCr);
        _playerCr = StartCoroutine(PlayerReact(hit));

        if (_clownCr != null) StopCoroutine(_clownCr);
        _clownCr = StartCoroutine(ClownReact(hit));

        // Fire-and-forget, not busy-guarded like the pulses above — each Hit spawns its own balloon
        // that lives out its own rise+fade independently, so back-to-back Hits can have several
        // balloons on screen at once instead of one replacing another.
        if (hit) StartCoroutine(SpawnBalloon());
    }

    // Fires on every physical press, judged or not (hit/miss-window 밖의 "그냥 눌러본" 탭 포함) — the
    // player ant should still visibly pour so tapping always feels responsive, even when nothing
    // is being judged. Busy-guard (skip while a pulse is already playing), NOT cancel-and-restart —
    // CallDirector's own mash cooldown only blocks *judgement*, OnResponsePress still fires on every
    // single press regardless, so cancel-and-restart here let spamming keep resetting the pulse back
    // to "pouring" before it ever got to revert, making the ant look permanently stuck pouring
    // instead of toggling normal/pour per tap. A press that DOES land in a judged window still fires
    // this first, then HandleGroupJudged immediately after (same Update tick) — that one still
    // cancels-and-restarts, so a judged Miss always shows correctly even if this just started a
    // generic pour pulse a moment earlier.
    void HandlePlayerPress()
    {
        if (_playerCr != null) return;
        _playerCr = StartCoroutine(PlayerReact(hit: true));
    }

    IEnumerator HandPulse()
    {
        if (handImage == null) yield break;
        handImage.sprite = handPourSprite;
        yield return new WaitForSeconds(handPourHoldTime);
        handImage.sprite = handIdleSprite;
        _handCr = null;
    }

    IEnumerator PlayerReact(bool hit)
    {
        if (playerAntImage == null) yield break;
        playerAntImage.sprite = hit ? playerAntPourSprite : playerAntFailSprite;
        yield return new WaitForSeconds(playerReactHoldTime);
        playerAntImage.sprite = playerAntIdleSprite;
        _playerCr = null;
    }

    IEnumerator ClownReact(bool hit)
    {
        if (clownImage == null) yield break;
        clownImage.sprite = hit ? clownSuccessSprite : clownFailSprite;
        yield return new WaitForSeconds(clownReactHoldTime);
        clownImage.sprite = clownIdleSprite;
        _clownCr = null;
    }

    // Spawns one procedurally-animated balloon (red or blue, random) that rises with a gentle side-
    // to-side sway and fades out near the top — same "spawn a temp Image, animate, self-destroy"
    // pattern as Stage1SurviveVisuals' bubble FX, just for a celebratory Hit flourish instead of an
    // anticipation cue.
    IEnumerator SpawnBalloon()
    {
        if (balloonSpawnParent == null) yield break;
        var sprite = Random.value < 0.5f ? redBalloonSprite : blueBalloonSprite;
        if (sprite == null) yield break;

        var go = new GameObject("Balloon(Temp)", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(balloonSpawnParent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        rt.sizeDelta = sprite.rect.size * balloonScale;

        float side = Random.value < 0.5f ? -1f : 1f;
        float x = side * balloonSideDistance + Random.Range(-balloonSpreadX, balloonSpreadX);
        Vector2 start = new Vector2(x, balloonSpawnY);
        rt.anchoredPosition = start;

        float t = 0f;
        Color c = img.color;
        while (t < balloonRiseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / balloonRiseDuration);
            float sway = Mathf.Sin(t * balloonSwaySpeed) * balloonSwayAmplitude;
            rt.anchoredPosition = new Vector2(start.x + sway, start.y + p * balloonRiseHeight);

            const float fadeStart = 0.75f; // begin fading in the last quarter of the rise
            c.a = p > fadeStart ? Mathf.Lerp(1f, 0f, (p - fadeStart) / (1f - fadeStart)) : 1f;
            img.color = c;

            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }
}
