using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Input handler for the combat rhythm game.
/// SPACE / Z       → hit Normal (white) notes.
/// ← A / ↓ S / ↑ W / → D → hit the matching colored Special note.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer bodyRenderer;

    Coroutine _flashRoutine;

    static readonly Color ColHitNormal  = new Color(0.35f, 0.85f, 1.00f);
    static readonly Color ColHitSpecial = new Color(1.00f, 0.92f, 0.20f);
    static readonly Color ColMiss       = new Color(1.00f, 0.30f, 0.25f);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Normal attack
        if (kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
            TryHit(NoteType.Normal, ColHitNormal);

        // Special attacks — directional
        if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)
            TryHit(NoteType.ArrowLeft,  new Color(0.30f, 0.60f, 1.00f));
        if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame)
            TryHit(NoteType.ArrowDown,  new Color(0.30f, 1.00f, 0.45f));
        if (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame)
            TryHit(NoteType.ArrowUp,    new Color(1.00f, 0.90f, 0.20f));
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            TryHit(NoteType.ArrowRight, new Color(1.00f, 0.35f, 0.35f));
    }

    void TryHit(NoteType type, Color flashColor)
    {
        var note = NoteManager.Instance.GetNearest(
            RhythmManager.Instance.SongTime, Note.GoodWindow, type);

        if (note != null)
        {
            note.TryHit();
            Flash(flashColor);
        }
    }

    /// <summary>Called by BossController when a miss attack lands on the player.</summary>
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
}
