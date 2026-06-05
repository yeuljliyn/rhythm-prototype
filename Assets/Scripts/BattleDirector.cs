using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PatternData 기반 배틀 진행.
/// 보스 턴: 각 input.beat 타이밍에 ShowDirection 호출
/// 플레이어 턴: spaceBeat 이후, 같은 리듬 패턴을 입력 받아 PERFECT/GOOD/MISS 판정
/// </summary>
public class BattleDirector : MonoBehaviour
{
    [SerializeField] PatternData pattern;

    const float PerfectWindow = 0.10f;
    const float GoodWindow    = 0.20f;

    List<DirectionInputEntry> _playerExpected;
    List<float>               _playerTimes;
    int                       _playerIdx;
    bool                      _playerTurn;
    bool                      _playerTurnDone;

    void Start()
    {
        if (pattern == null)
            pattern = RhythmManager.Instance != null ? RhythmManager.Instance.Pattern : null;
        if (pattern == null || pattern.groups == null || pattern.groups.Count == 0) return;
        StartCoroutine(RunDirector());
    }

    void Update()
    {
        if (_playerTurn) HandlePlayerInput();
    }

    IEnumerator RunDirector()
    {
        var rm = RhythmManager.Instance;
        while (rm == null) { rm = RhythmManager.Instance; yield return null; }
        while (rm.SongTime < 0f) yield return null;

        foreach (var group in pattern.groups)
        {
            if (group.inputs == null || group.inputs.Count == 0) continue;
            if (group.spaceBeat < 0f) continue;

            // ── 보스 턴 ──────────────────────────────────
            StockChartBackground.SetTurn(false);

            foreach (var input in group.inputs)
            {
                float beatTime = pattern.BeatToTime(input.beat);
                while (rm.SongTime < beatTime) yield return null;
                BossController.Instance?.ShowDirection(input.direction);
            }

            // spaceBeat까지 대기
            float spaceTime = pattern.BeatToTime(group.spaceBeat);
            while (rm.SongTime < spaceTime) yield return null;

            // ── 플레이어 턴 ──────────────────────────────
            StockChartBackground.SetTurn(true);

            float firstInputTime = pattern.BeatToTime(group.inputs[0].beat);
            _playerExpected = new List<DirectionInputEntry>(group.inputs);
            _playerTimes    = new List<float>();
            foreach (var inp in group.inputs)
                _playerTimes.Add(spaceTime + (pattern.BeatToTime(inp.beat) - firstInputTime));

            _playerIdx      = 0;
            _playerTurnDone = false;
            _playerTurn     = true;

            while (!_playerTurnDone) yield return null;
        }
    }

    void HandlePlayerInput()
    {
        var rm = RhythmManager.Instance;
        var gm = GameManager.Instance;
        if (rm == null) return;

        // 타임아웃된 입력 자동 MISS 처리
        while (_playerIdx < _playerTimes.Count && rm.SongTime > _playerTimes[_playerIdx] + GoodWindow)
        {
            gm?.RegisterJudgement(JudgementType.Miss);
            _playerIdx++;
        }

        if (_playerIdx >= _playerTimes.Count)
        {
            _playerTurn     = false;
            _playerTurnDone = true;
            return;
        }

        ArrowDir? dir = ReadDirectionInput();
        if (dir == null) return;

        float expectedTime = _playerTimes[_playerIdx];
        float delta        = rm.SongTime - expectedTime;

        // 너무 이른 입력은 무시
        if (delta < -GoodWindow) return;

        ArrowDir      expectedDir = _playerExpected[_playerIdx].direction;
        JudgementType j;

        if (dir.Value != expectedDir)
            j = JudgementType.Miss;
        else if (Mathf.Abs(delta) <= PerfectWindow)
            j = JudgementType.Perfect;
        else
            j = JudgementType.Good;

        gm?.RegisterJudgement(j);
        if (j != JudgementType.Miss)
            BossController.Instance?.TakeDamage(j);

        _playerIdx++;
        if (_playerIdx >= _playerTimes.Count)
        {
            _playerTurn     = false;
            _playerTurnDone = true;
        }
    }

    static ArrowDir? ReadDirectionInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return null;
        if (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame) return ArrowDir.Up;
        if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame) return ArrowDir.Down;
        if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame) return ArrowDir.Left;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return ArrowDir.Right;
        return null;
    }
}
