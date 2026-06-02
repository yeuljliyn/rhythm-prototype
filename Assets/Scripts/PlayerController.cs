using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer bodyRenderer;
    public Animator       bodyAnimator;

    Coroutine _flashRoutine;

    static readonly Color ColHit    = new Color(0.35f, 0.85f, 1.00f);
    static readonly Color ColMiss   = new Color(1.00f, 0.30f, 0.25f);

    void Update()
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.IsPanelOpen) return;

        // Arrow keys — forward to command system first, then also count as note hit
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)
                CommandSystem.Instance?.TryInput(ArrowDir.Left);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                CommandSystem.Instance?.TryInput(ArrowDir.Right);
            else if (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame)
                CommandSystem.Instance?.TryInput(ArrowDir.Up);
            else if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame)
                CommandSystem.Instance?.TryInput(ArrowDir.Down);
        }

        if (IsValidHitInput())
            TryHitNote();
    }

    void TryHitNote()
    {
        var note = NoteManager.Instance.GetNearest(
            RhythmManager.Instance.SongTime, Note.GoodWindow);

        if (note != null)
        {
            note.TryHit();
            Flash(ColHit);
            bodyAnimator?.SetTrigger("Attack");
        }
    }

    public void FlashDamage() => Flash(ColMiss);

    void Flash(Color color)
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(color));
    }

    IEnumerator FlashRoutine(Color color)
    {
        if (bodyRenderer != null) bodyRenderer.color = color;
        yield return new WaitForSeconds(0.10f);
        if (bodyRenderer != null) bodyRenderer.color = Color.white;
    }

    static bool IsValidHitInput()
    {
        var ms = Mouse.current;
        if (ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame))
            return true;

        var kb = Keyboard.current;
        if (kb == null || !kb.anyKey.wasPressedThisFrame) return false;

        // Exclude pause/modifier/system keys
        if (kb.escapeKey.wasPressedThisFrame)      return false;
        if (kb.leftCtrlKey.wasPressedThisFrame  || kb.rightCtrlKey.wasPressedThisFrame)  return false;
        if (kb.leftAltKey.wasPressedThisFrame   || kb.rightAltKey.wasPressedThisFrame)   return false;
        if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame) return false;
        if (kb.leftMetaKey.wasPressedThisFrame  || kb.rightMetaKey.wasPressedThisFrame)  return false;
        if (kb.f1Key.wasPressedThisFrame  || kb.f2Key.wasPressedThisFrame  ||
            kb.f3Key.wasPressedThisFrame  || kb.f4Key.wasPressedThisFrame  ||
            kb.f5Key.wasPressedThisFrame  || kb.f6Key.wasPressedThisFrame  ||
            kb.f7Key.wasPressedThisFrame  || kb.f8Key.wasPressedThisFrame  ||
            kb.f9Key.wasPressedThisFrame  || kb.f10Key.wasPressedThisFrame ||
            kb.f11Key.wasPressedThisFrame || kb.f12Key.wasPressedThisFrame) return false;
        if (kb.printScreenKey.wasPressedThisFrame) return false;
        if (kb.scrollLockKey.wasPressedThisFrame)  return false;
        if (kb.pauseKey.wasPressedThisFrame)       return false;

        return true;
    }
}
