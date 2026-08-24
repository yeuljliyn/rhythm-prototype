using UnityEngine;

/// <summary>
/// 인트로 타이틀 카드가 떠 있는 동안, 빈 양동이가 파도 위를 왼쪽에서 오른쪽으로
/// 출렁이며 흘러가는 애니메이션(Stage1_1IntroScene 전용).
///
/// anchoredPosition(캔버스 디자인 좌표, CanvasScaler 기준 해상도 단위)만 사용 —
/// 이 좌표계는 CanvasScaler가 실제 해상도에 맞춰 알아서 스케일해주므로, 코드에서
/// canvas.scaleFactor를 따로 곱할 필요가 없다(project convention: 이게 필요한 경우는
/// RectTransform 계층 밖에서 world/screen 좌표에 직접 오프셋을 더할 때뿐).
/// </summary>
public class Stage1IntroBucketDrift : MonoBehaviour
{
    [SerializeField] RectTransform target;
    [SerializeField] float startX = -1000f;
    [SerializeField] float endX = 1000f;
    [SerializeField] float duration = 5f;
    [SerializeField] float baseY = 64f;
    [SerializeField] float bobAmplitude = 18f;
    [SerializeField] float bobFrequency = 1.6f;

    float _t;

    void Update()
    {
        _t += Time.deltaTime;
        float p = Mathf.Clamp01(_t / duration);
        float x = Mathf.Lerp(startX, endX, p);
        float y = baseY + Mathf.Sin(_t * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        target.anchoredPosition = new Vector2(x, y);
    }
}
