using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score    { get; private set; }
    public int Combo    { get; private set; }
    public int MaxCombo { get; private set; }

    public System.Action<int, int>      OnScoreChanged; // (score, combo)
    public System.Action<JudgementType> OnJudgement;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterJudgement(JudgementType type)
    {
        switch (type)
        {
            case JudgementType.Perfect:
                Combo++;
                Score += 300 + Combo * 10;
                break;
            case JudgementType.Good:
                Combo++;
                Score += 100 + Combo * 5;
                break;
            case JudgementType.Miss:
                Combo = 0;
                break;
        }

        if (Combo > MaxCombo) MaxCombo = Combo;

        OnScoreChanged?.Invoke(Score, Combo);
        OnJudgement?.Invoke(type);
    }

    public void ResetGame()
    {
        Score = Combo = MaxCombo = 0;
        OnScoreChanged?.Invoke(Score, Combo);
    }
}
