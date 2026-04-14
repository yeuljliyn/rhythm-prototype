using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 4-direction input: A/← = Left, S/↓ = Down, W/↑ = Up, D/→ = Right
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer bodyRenderer;

    Coroutine flashRoutine;

    static readonly Color[] LaneColors = {
        new Color(0.3f, 0.6f, 1f),     // Left  — blue
        new Color(0.3f, 1f,  0.45f),   // Down  — green
        new Color(1f,  0.9f, 0.2f),    // Up    — yellow
        new Color(1f,  0.35f,0.35f),   // Right — red
    };

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.aKey.wasPressedThisFrame     || kb.leftArrowKey.wasPressedThisFrame)  HitLane(0);
        if (kb.sKey.wasPressedThisFrame     || kb.downArrowKey.wasPressedThisFrame)  HitLane(1);
        if (kb.wKey.wasPressedThisFrame     || kb.upArrowKey.wasPressedThisFrame)    HitLane(2);
        if (kb.dKey.wasPressedThisFrame     || kb.rightArrowKey.wasPressedThisFrame) HitLane(3);
    }

    void HitLane(int lane)
    {
        var note = NoteManager.Instance.GetNearest(lane, RhythmManager.Instance.SongTime, Note.GoodWindow);
        note?.TryHit();

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash(LaneColors[lane]));
    }

    IEnumerator Flash(Color color)
    {
        if (bodyRenderer != null) bodyRenderer.color = color;
        yield return new WaitForSeconds(0.08f);
        if (bodyRenderer != null) bodyRenderer.color = Color.white;
    }
}
