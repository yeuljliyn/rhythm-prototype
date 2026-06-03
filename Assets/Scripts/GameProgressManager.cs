using UnityEngine;

/// <summary>
/// 스테이지 해금/랭크 데이터를 인스펙터에서 관리한다.
/// BattleScene이나 LobbyScene에 하나만 배치하면 됩니다.
/// </summary>
public class GameProgressManager : MonoBehaviour
{
    [Header("현재 저장된 데이터 (읽기 전용)")]
    [SerializeField, HideInInspector] public int   unlockedStages;
    [SerializeField, HideInInspector] public string[] stageRanks = new string[5];
    [SerializeField, HideInInspector] public bool[]   muguObtained = new bool[5];

    static readonly string[] StageNames =
        { "Stage 1 (물귀신)", "Stage 2 (지박령)", "Stage 3 (총각귀신)", "Stage 4 (처녀귀신)", "Stage 5 (달걀귀신)" };

    public static string[] GetStageNames() => StageNames;

    /// <summary>PlayerPrefs에서 현재 값을 읽어 필드를 갱신한다.</summary>
    public void RefreshFromPrefs()
    {
        unlockedStages = PlayerPrefs.GetInt("unlocked_stages", 1);
        for (int i = 0; i < 5; i++)
        {
            stageRanks[i]   = PlayerPrefs.GetString($"stage_{i}_rank", "--");
            muguObtained[i] = PlayerPrefs.GetInt($"mugu_stage_{i}", 0) == 1;
        }
    }

    public void SetUnlocked(int count)
    {
        count = Mathf.Clamp(count, 1, 5);
        PlayerPrefs.SetInt("unlocked_stages", count);
        PlayerPrefs.Save();
        RefreshFromPrefs();
        Debug.Log($"[GameProgress] 해금 스테이지 수 → {count}");
    }

    public void SetRank(int stageIdx, string rank)
    {
        PlayerPrefs.SetString($"stage_{stageIdx}_rank", rank);
        PlayerPrefs.Save();
        RefreshFromPrefs();
    }

    public void SetMugu(int stageIdx, bool obtained)
    {
        PlayerPrefs.SetInt($"mugu_stage_{stageIdx}", obtained ? 1 : 0);
        PlayerPrefs.Save();
        RefreshFromPrefs();
    }

    public void ResetAll()
    {
        PlayerPrefs.SetInt("unlocked_stages", 1);
        for (int i = 0; i < 5; i++)
        {
            PlayerPrefs.DeleteKey($"stage_{i}_rank");
            PlayerPrefs.DeleteKey($"mugu_stage_{i}");
        }
        PlayerPrefs.Save();
        RefreshFromPrefs();
        Debug.Log("[GameProgress] 전체 초기화 완료");
    }

    public void UnlockAll()
    {
        PlayerPrefs.SetInt("unlocked_stages", 5);
        PlayerPrefs.Save();
        RefreshFromPrefs();
        Debug.Log("[GameProgress] 전체 해금 완료");
    }

    void OnEnable() => RefreshFromPrefs();
}
