using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameProgressManager))]
public class GameProgressManagerEditor : Editor
{
    static readonly string[] RankOptions = { "--", "S", "A", "B", "C" };

    public override void OnInspectorGUI()
    {
        var mgr = (GameProgressManager)target;
        mgr.RefreshFromPrefs();

        var boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };

        // ── 전체 조작 ──────────────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("전체 조작", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("초기화 (Stage 1만 해금)", GUILayout.Height(32)))
        {
            if (EditorUtility.DisplayDialog("초기화 확인",
                "모든 진행 기록(해금, 랭크, 무구)이 삭제됩니다.\n계속하시겠습니까?", "초기화", "취소"))
                mgr.ResetAll();
        }

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
        if (GUILayout.Button("전체 해금", GUILayout.Height(32)))
            mgr.UnlockAll();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // ── 해금 수 슬라이더 ──────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("해금 스테이지 수", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        int newUnlocked = EditorGUILayout.IntSlider(mgr.unlockedStages, 1, 5);
        if (newUnlocked != mgr.unlockedStages)
            mgr.SetUnlocked(newUnlocked);

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("적용", GUILayout.Width(50), GUILayout.Height(20)))
            mgr.SetUnlocked(newUnlocked);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // ── 스테이지별 세부 ───────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("스테이지별 데이터", EditorStyles.boldLabel);

        var stageNames = GameProgressManager.GetStageNames();

        for (int i = 0; i < 5; i++)
        {
            bool isUnlocked = i < mgr.unlockedStages;

            // 잠김 스테이지는 흐리게
            GUI.enabled = true;
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = isUnlocked
                ? new Color(0.85f, 1f, 0.85f)
                : new Color(0.85f, 0.85f, 0.85f);

            EditorGUILayout.BeginVertical(boxStyle);
            GUI.backgroundColor = prevColor;

            // 헤더
            EditorGUILayout.BeginHorizontal();
            string lockIcon = isUnlocked ? "🔓" : "🔒";
            EditorGUILayout.LabelField($"{lockIcon}  {stageNames[i]}", EditorStyles.boldLabel);

            // 잠금/해금 토글 버튼
            if (isUnlocked && i > 0)
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("잠금", GUILayout.Width(48), GUILayout.Height(18)))
                    mgr.SetUnlocked(i);   // i까지만 해금 (i+1 잠금)
            }
            else if (!isUnlocked)
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.7f);
                if (GUILayout.Button("해금", GUILayout.Width(48), GUILayout.Height(18)))
                    mgr.SetUnlocked(i + 1);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // 랭크
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("베스트 랭크", GUILayout.Width(80));
            int rankIdx = System.Array.IndexOf(RankOptions, mgr.stageRanks[i]);
            if (rankIdx < 0) rankIdx = 0;
            int newRankIdx = EditorGUILayout.Popup(rankIdx, RankOptions, GUILayout.Width(60));
            if (newRankIdx != rankIdx)
                mgr.SetRank(i, RankOptions[newRankIdx]);

            // 랭크 색상 표시
            Color rankColor = mgr.stageRanks[i] == "S" ? new Color(1f, 0.85f, 0f)
                            : mgr.stageRanks[i] == "A" ? new Color(0.5f, 0.8f, 1f)
                            : mgr.stageRanks[i] == "B" ? new Color(0.5f, 1f, 0.5f)
                            : Color.gray;
            var oldColor = GUI.contentColor;
            GUI.contentColor = rankColor;
            EditorGUILayout.LabelField(mgr.stageRanks[i] == "--" ? "" : $"▶ {mgr.stageRanks[i]}",
                GUILayout.Width(40));
            GUI.contentColor = oldColor;

            EditorGUILayout.EndHorizontal();

            // 무구 획득 여부
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("무구 획득", GUILayout.Width(80));
            bool newMugu = EditorGUILayout.Toggle(mgr.muguObtained[i], GUILayout.Width(20));
            if (newMugu != mgr.muguObtained[i])
                mgr.SetMugu(i, newMugu);
            EditorGUILayout.LabelField(mgr.muguObtained[i] ? "획득" : "미획득",
                GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        GUI.enabled = true;

        // ── 새로고침 ──────────────────────────────────────────────────
        EditorGUILayout.Space(4);
        if (GUILayout.Button("🔄  새로고침", GUILayout.Height(24)))
            mgr.RefreshFromPrefs();

        // 변경사항 표시
        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
            Repaint();
        }
    }
}
