using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Single-button input: SPACE or Z hits the nearest beat circle.
/// The player visual sits at the bottom-center of the screen.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer bodyRenderer;

    Coroutine _flashRoutine;

    static readonly Color ColHit  = new Color(0.35f, 0.85f, 1.00f);
    static readonly Color ColMiss = new Color(1.00f, 0.30f, 0.25f);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
            HitNote();
    }

    void HitNote()
    {
        var note = NoteManager.Instance.GetNearest(
            RhythmManager.Instance.SongTime, Note.GoodWindow);

        if (note != null)
        {
            note.TryHit();
            Flash(ColHit);
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
