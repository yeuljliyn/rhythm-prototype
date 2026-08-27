using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Placeholder speech bubble for the 1-1~1-4 inline tutorials — a panel + tail anchored above a
/// target character (the on-screen Jackdo ant, not a DialogueScene portrait — see project docs).
/// The Bubble/Panel/Tail/Text hierarchy is authored directly in the scene (editor-visible, no Play
/// needed to see or move it — see project convention) rather than built procedurally in code; this
/// script only drives show/hide/positioning against whatever hierarchy is wired in the Inspector.
/// </summary>
public class Stage1SpeechBubble : MonoBehaviour
{
    [Tooltip("말풍선이 따라다닐 캐릭터(잭두 개미)의 RectTransform — 그 머리 위에 앵커됨.")]
    [SerializeField] RectTransform target;
    // target 기준 오프셋 — 고정값을 손으로 입력받는 대신, Awake 시점에 에디터에서 Bubble을
    // 배치해둔 실제 위치로부터 역산해서 캡처한다. 그래야 씬 뷰에서 Bubble을 드래그해 옮긴 위치가
    // Play를 눌러도 그대로 유지된다(예전엔 고정 offsetFromTarget이 매 프레임 위치를 덮어써서,
    // 에디터에서 옮긴 위치가 Play 시작과 동시에 무시되는 버그가 있었음).
    Vector2 _offsetFromTarget;

    [Header("씬에 미리 배치된 오브젝트 (Bubble/Panel/Tail/Text) — Play 없이 에디터에서 위치·크기 조정 가능")]
    [Tooltip("말풍선 전체(패널+꼬리)를 담는 최상위 RectTransform. Update()에서 이 오브젝트가 target을 따라 움직임.")]
    [SerializeField] RectTransform root;
    [Tooltip("배경 패널의 RectTransform — 크기는 에디터에서 배치한 그대로 고정. Show()가 더 이상 텍스트 " +
             "길이에 맞춰 sizeDelta를 자동 조정하지 않음(원래는 더미 리소스 시절 텍스트에 맞춰 패널을 " +
             "동적으로 늘리던 로직이었는데, 실제 말풍선 아트가 들어온 뒤로는 에디터에서 잡아둔 패널/텍스트 " +
             "크기·위치가 Play하면 이 계산값으로 덮어써져 반영이 안 되는 문제가 있었음 — 확인 리포트: " +
             "\"씬에서 크기 조정해도 Play하면 그대로 반영이 안 됨\").")]
    [SerializeField] RectTransform panelRect;
    [SerializeField] TMP_Text text;

    Coroutine _hideCr;
    Coroutine _waitCr;

    void Awake()
    {
        CaptureAuthoredOffset();
        Hide();
    }

    // 반드시 Hide()(비활성화)보다 먼저 호출 — 에디터에서 배치해둔 root.position은 비활성 상태여도
    // 유효하지만, 캡처 자체는 활성/비활성과 무관하게 지금 이 순간의 위치를 그대로 읽는다.
    void CaptureAuthoredOffset()
    {
        if (target == null || root == null) return;
        _offsetFromTarget = (Vector2)(root.position - target.position) / CanvasScale();
    }

    void Update()
    {
        if (target == null || root == null) return;
        root.position = target.position + (Vector3)ScaledOffset();
    }

    float CanvasScale()
    {
        var canvas = root.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.scaleFactor : 1f;
    }

    Vector2 ScaledOffset() => _offsetFromTarget * CanvasScale();

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (_hideCr != null) { StopCoroutine(_hideCr); _hideCr = null; }

        text.text = message;
        root.gameObject.SetActive(true);
        // panelRect/text keep whatever size and position were authored in the scene — see
        // panelRect's field doc comment for why this no longer auto-resizes.
    }

    public void Hide()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    /// Shows the message, then automatically hides after `seconds` — used for lines that aren't
    /// explicitly dismissed by a follow-up game event (e.g. a demo-transition caption).
    public void ShowFor(string message, float seconds)
    {
        Show(message);
        if (_hideCr != null) StopCoroutine(_hideCr);
        _hideCr = StartCoroutine(HideAfter(seconds));
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Hide();
        _hideCr = null;
    }

    /// Shows the message with no auto-hide timer — stays up until the player presses Space or
    /// clicks (same "either input, one logical button" convention CallDirector itself uses), then
    /// hides and calls `onConfirm`. Used to gate practice actually starting on the intro line
    /// being read, rather than a fixed timer racing the player.
    public void ShowUntilConfirm(string message, System.Action onConfirm)
    {
        Show(message);
        if (_waitCr != null) StopCoroutine(_waitCr);
        _waitCr = StartCoroutine(WaitForConfirm(onConfirm));
    }

    /// Cancels a pending ShowUntilConfirm wait (e.g. the player skipped before reading the line)
    /// without invoking its callback, and hides the bubble immediately.
    public void CancelWait()
    {
        if (_waitCr != null) { StopCoroutine(_waitCr); _waitCr = null; }
        Hide();
    }

    IEnumerator WaitForConfirm(System.Action onConfirm)
    {
        bool PressedNow() =>
            (Keyboard.current != null && Keyboard.current[Key.Space].isPressed) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);

        // Wait a frame first so the very same press that triggered this segment's entry (e.g. a
        // press that landed a Hit and advanced us here) can't also insta-confirm this new line.
        yield return null;
        bool prevPressed = PressedNow();

        while (true)
        {
            bool pressedNow = PressedNow();
            if (pressedNow && !prevPressed) break;
            prevPressed = pressedNow;
            yield return null;
        }

        Hide();
        _waitCr = null;
        onConfirm?.Invoke();
    }
}
