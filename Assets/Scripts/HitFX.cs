using UnityEngine;

/// <summary>Short-lived flash that scales up and fades out on note hit.</summary>
public class HitFX : MonoBehaviour
{
    float elapsed;
    const float duration = 0.25f;
    SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.5f, t);

        if (sr != null)
        {
            var c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f - t);
        }

        if (elapsed >= duration)
            Destroy(gameObject);
    }
}
