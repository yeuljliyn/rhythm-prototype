using UnityEngine;

public class LaneIndicator : MonoBehaviour
{
    SpriteRenderer hitZoneSR;

    public void Init(float yPos, Color trackColor, Color hitColor, Sprite pixel)
    {
        transform.position = new Vector3(0f, yPos, 0f);

        // Track line
        var line = new GameObject("TrackLine");
        line.transform.SetParent(transform);
        line.transform.localPosition = Vector3.zero;
        line.transform.localScale    = new Vector3(30f, 0.05f, 1f);
        var lineSR          = line.AddComponent<SpriteRenderer>();
        lineSR.sprite       = pixel;
        lineSR.color        = trackColor;
        lineSR.sortingOrder = 1;

        // Hit-zone marker at x = -5
        var hz = new GameObject("HitZone");
        hz.transform.SetParent(transform);
        hz.transform.localPosition = new Vector3(-5f, 0f, 0f);
        hz.transform.localScale    = new Vector3(0.12f, 1.4f, 1f);
        hitZoneSR          = hz.AddComponent<SpriteRenderer>();
        hitZoneSR.sprite   = pixel;
        hitZoneSR.color    = hitColor;
        hitZoneSR.sortingOrder = 2;
    }

    void Update()
    {
        if (hitZoneSR == null) return;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
        Color c = hitZoneSR.color;
        hitZoneSR.color = new Color(c.r, c.g, c.b, pulse);
    }
}
