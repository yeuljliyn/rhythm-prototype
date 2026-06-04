using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer     bodyRenderer;
    public Animator           bodyAnimator;
    public PlayerDanceAnimator danceAnimator;

    Coroutine _flashRoutine;

    static readonly Color ColHit = new Color(0.35f, 0.85f, 1.00f);

    void Update()
    {
        if (SettingsManager.Instance != null && SettingsManager.Instance.IsPanelOpen) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if      (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)
                danceAnimator?.ShowDirection(ArrowDir.Left);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                danceAnimator?.ShowDirection(ArrowDir.Right);
            else if (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame)
                danceAnimator?.ShowDirection(ArrowDir.Up);
            else if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame)
                danceAnimator?.ShowDirection(ArrowDir.Down);
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
        if (kb == null) return false;

        if (kb.spaceKey.wasPressedThisFrame) return true;
        if (!kb.anyKey.wasPressedThisFrame)  return false;

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
