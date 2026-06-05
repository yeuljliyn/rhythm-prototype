using System.Collections;
using UnityEngine;

/// <summary>
/// PatternData 기반으로 보스/플레이어 턴을 관리.
/// 보스 턴: 보스가 방향 스프라이트를 순서대로 보여줌 + 하락장 배경
/// 플레이어 턴: 플레이어 입력 대기 + 상승장 배경
/// </summary>
public class BattleDirector : MonoBehaviour
{
    [SerializeField] PatternData pattern;

    void Start()
    {
        if (pattern == null)
            pattern = RhythmManager.Instance != null ? RhythmManager.Instance.Pattern : null;
        if (pattern == null || pattern.groups == null || pattern.groups.Count == 0) return;
        StartCoroutine(RunDirector());
    }

    IEnumerator RunDirector()
    {
        while (RhythmManager.Instance == null) yield return null;
        while (RhythmManager.Instance.SongTime < 0f) yield return null;

        foreach (var group in pattern.groups)
        {
            if (group.inputs == null || group.inputs.Count == 0) continue;

            // 보스 차례 → 하락장
            StockChartBackground.SetTurn(false);

            foreach (var input in group.inputs)
            {
                float beatTime = pattern.BeatToTime(input.beat);
                while (RhythmManager.Instance.SongTime < beatTime)
                    yield return null;
                BossController.Instance?.ShowDirection(input.direction);
            }

            // 플레이어 차례 → 상승장
            StockChartBackground.SetTurn(true);

            if (group.spaceBeat > 0f)
            {
                float spaceTime = pattern.BeatToTime(group.spaceBeat);
                while (RhythmManager.Instance.SongTime < spaceTime)
                    yield return null;
            }
        }
    }
}
