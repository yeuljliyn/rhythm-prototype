using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer swordRenderer;

    Coroutine attackRoutine;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.zKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
        { Debug.Log("[PC] Ground attack"); Attack(0); }
        if (kb.xKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
        { Debug.Log("[PC] Air attack"); Attack(1); }
    }

    void Attack(int lane)
    {
        var note = NoteManager.Instance.GetNearest(lane, RhythmManager.Instance.SongTime, Note.GoodWindow);
        note?.TryHit();

        Color flashColor = lane == 0 ? new Color(0.2f, 1f, 0.9f) : new Color(1f, 0.2f, 0.9f);
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(SlashAnim(lane, flashColor));
    }

    IEnumerator SlashAnim(int lane, Color color)
    {
        if (bodyRenderer != null) { bodyRenderer.color = color; }

        if (swordRenderer != null)
        {
            float targetAngle = lane == 0 ? -40f : 40f;
            float elapsed = 0f;
            float duration = 0.12f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float angle = Mathf.Lerp(0f, targetAngle, Mathf.Sin(t * Mathf.PI));
                swordRenderer.transform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }
            swordRenderer.transform.localRotation = Quaternion.identity;
        }
        else { yield return new WaitForSeconds(0.08f); }

        if (bodyRenderer != null) { bodyRenderer.color = Color.white; }
    }
}
