using UnityEngine;

public class GameManager : MonoBehaviour
{
    static GameManager _instance;
    public static GameManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<GameManager>(); return _instance; }
        private set { _instance = value; }
    }

    public int Score    { get; private set; }
    public int Combo    { get; private set; }
    public int MaxCombo { get; private set; }

    public int Hits   { get; private set; }
    public int Misses { get; private set; }
    public int Total  => Hits + Misses;
    public float Accuracy => Total > 0 ? (float)Hits / Total : 0f;

    public string GetRank()
    {
        float acc = Accuracy * 100f;
        if (acc >= 95f) return "S";
        if (acc >= 85f) return "A";
        if (acc >= 70f) return "B";
        return "C";
    }
    public bool IsClear => GetRank() != "C";

    public System.Action<int, int>      OnScoreChanged;
    public System.Action<JudgementType> OnJudgement;

    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);
    }

    public void RegisterJudgement(JudgementType type)
    {
        switch (type)
        {
            case JudgementType.Hit:
                Hits++;
                Combo++;
                Score += 200 + Combo * 10;
                break;
            case JudgementType.Miss:
                Misses++;
                Combo = 0;
                break;
        }

        if (Combo > MaxCombo) MaxCombo = Combo;

        OnScoreChanged?.Invoke(Score, Combo);
        OnJudgement?.Invoke(type);

        if (type == JudgementType.Hit) BattleSFXController.Instance?.PlayHit();
        else                           BattleSFXController.Instance?.PlayMiss();
    }

    public void ResetGame()
    {
        Score = Combo = MaxCombo = Hits = Misses = 0;
        OnScoreChanged?.Invoke(Score, Combo);
    }
}
