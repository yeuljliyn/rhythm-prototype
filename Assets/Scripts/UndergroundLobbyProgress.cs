using UnityEngine;

/// <summary>
/// 지하 로비(1-1~1-5) 전용 해금 진행도 저장 — 기존 GameProgressManager(무구/장비 경제)와는 완전히 별개의
/// 새 저장 시스템(계획 단계에서 사용자가 명시적으로 결정한 사항: 재사용 안 함).
///
/// "해금됨"의 기준은 0-based 인덱스 하나(highestUnlockedIndex) — 0이면 1-1만 입장 가능, 1이면 1-2까지,
/// ... 이런 식. 각 스테이지의 결과 화면(Stage1ResultScreen)이 랭크 A 이상으로 클리어했을 때
/// UnlockUpTo(그 스테이지의 0-based 인덱스 + 1)을 호출해서 다음 스테이지를 연다.
/// </summary>
public static class UndergroundLobbyProgress
{
    const string UnlockedIndexKey = "underground_unlocked_index";

    /// <summary>현재 입장 가능한 마지막 인덱스(0-based). 저장된 값이 없으면 0(1-1만 해금).</summary>
    public static int GetUnlockedIndex() => PlayerPrefs.GetInt(UnlockedIndexKey, 0);

    /// <summary>index까지 해금 — 이미 그보다 더 많이 해금돼 있으면 아무 것도 안 함(절대 되돌리지 않음).</summary>
    public static void UnlockUpTo(int index)
    {
        if (index <= GetUnlockedIndex()) return;
        PlayerPrefs.SetInt(UnlockedIndexKey, index);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("RhythmGame/지하 로비 진행도 초기화(1-1만 해금)")]
    static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(UnlockedIndexKey);
        Debug.Log("[UndergroundLobbyProgress] 초기화 완료 — 1-1만 해금된 상태로 되돌아감.");
    }
#endif
}
