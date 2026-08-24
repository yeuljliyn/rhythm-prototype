using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// "Hold E for 2 seconds to skip the practice" — always available from the moment practice starts
/// (no PlayerPrefs "already seen" gate; see project tutorial redesign). Releasing E before the hold
/// completes resets progress to zero (standard hold-button behavior). Uses the new Input System
/// (Keyboard.current), matching CallDirector's own input handling, rather than the legacy
/// UnityEngine.Input class.
///
/// The Root/GaugeTrack/GaugeFill/HintText hierarchy is authored directly in the scene (editor-
/// visible, no Play needed to see or move it — see project convention) rather than built
/// procedurally in code; this script only drives the fill amount and hint text against whatever
/// hierarchy is wired in the Inspector. GaugeTrack/GaugeFill use a placeholder circle sprite baked
/// once to a real asset (Assets/Generated/UI/CircleGaugeSprite.png) — no real art yet.
/// </summary>
public class Stage1SkipHold : MonoBehaviour
{
    [SerializeField] Key skipKey = Key.E;
    [SerializeField] float holdSeconds = 2f;

    [Header("씬에 미리 배치된 오브젝트 — Play 없이 에디터에서 위치·크기 조정 가능")]
    [SerializeField] RectTransform root;
    [Tooltip("원형 게이지 채우기용 Image (Type=Filled, Radial360).")]
    [SerializeField] Image fillImage;
    [SerializeField] TMP_Text hintText;

    public System.Action OnSkip;

    float _heldFor;
    bool  _fired;

    void Awake()
    {
        if (hintText != null) hintText.text = $"스킵 {skipKey}";
    }

    void Update()
    {
        if (_fired) return;

        bool held = Keyboard.current != null && Keyboard.current[skipKey].isPressed;
        _heldFor = held ? _heldFor + Time.deltaTime : 0f;

        float progress = Mathf.Clamp01(_heldFor / holdSeconds);
        fillImage.fillAmount = progress;

        if (progress >= 1f)
        {
            _fired = true;
            OnSkip?.Invoke();
        }
    }

    /// Practice segments that finish naturally (3 hits) should stop listening for E — no point
    /// showing/racing a skip prompt once there's nothing left to skip.
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
        if (active) { _heldFor = 0f; _fired = false; fillImage.fillAmount = 0f; }
    }
}
