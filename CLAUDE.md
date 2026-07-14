# rhythm-prototype

Unity 리듬 배틀 게임 프로토타입. 플레이어가 한국 귀신 테마 보스(물귀신/지박령/총각귀신/처녀귀신/달걀귀신) 5명과 턴제 리듬 배틀을 벌이는 구조.

Unity Editor는 MCP 도구로 조작합니다 (`.claude/skills/` 참고 — `gameobject-*`, `scene-*`, `assets-*` 등). 씬 YAML을 직접 손으로 편집하지 말고 이 도구들을 사용하세요.

## 씬 흐름

```
StartScene → LobbyScene(스테이지 선택) → DialogueScene
           → BattleScene (또는 TutorialIntroScene → TutorialBattleScene → TutorialClearScene)
           → BattleResultScreen
```

`SceneTransitionManager` (싱글톤, DontDestroyOnLoad)가 페이드 인/아웃 전환을 전담. 씬 전환은 항상 `SceneTransitionManager.Instance.LoadSceneWithFade("SceneName")`을 통해서.

## 배틀 시스템 (현재 사용 중 — "direction-pattern" 방식)

- `PatternData` (ScriptableObject, `Assets/Chapter/*.asset`): 실제 사용되는 차트 포맷. `DirectionGroup` 목록 = [화살표 입력 시퀀스(`DirectionInputEntry`: ArrowDir + beat)] + 확인용 `spaceBeat`.
- `RhythmManager`: dspTime 기반으로 `SongTime` 계산해 오디오 동기화. 노트 레인 스폰(`BuildSchedule`/`SpawnNote`)은 **비활성화됨** — 레거시 호환용으로만 남아있음.
- `BattleDirector`: 배틀 진행의 실제 주체. 코루틴으로 그룹을 순회:
  1. 보스 턴 — `BossController.ShowDirection()`으로 방향 순차 표시
  2. 플레이어 턴 — 같은 리듬으로 화살표 입력 받아 판정 (Hit/Miss, 0.2초 윈도우) → `GameManager.RegisterJudgement()`
- `GameManager` (싱글톤): 점수/콤보/Hit/Miss 집계, S/A/B/C 랭크 산출.
- `BossController`: 방향/아이들(둠칫둠칫) 스프라이트 전환. `Resources.LoadAll`로 스프라이트 로드.
- `GameProgressManager`: PlayerPrefs로 스테이지 해금/랭크/무구(장비) 저장.

## 레거시 / 미사용

`NoteManager`, `Note`, `ArrowSprite`, `CircleSprites`, `LaneIndicator` 등은 예전 "노트 레인" 방식의 잔재. direction-pattern 방식으로 대체되어 현재 사용 안 함 — 이쪽을 되살릴 게 아니라면 건드릴 필요 없음.

## 진행 중인 방향 (2026-07-14 기준)

배틀씬을 완전히 다른 방식으로 새로 만들 예정. **기존 `BattleScene`/`BattleDirector`는 그대로 두고, 새 씬을 따로 만들어서 작업.** 기존 시스템을 "고친다"는 가정으로 수정하지 말 것 — 새 방향에 대한 구체적인 설계는 아직 미정이니 작업 전 사용자에게 확인.

## 컨벤션

- **오브젝트는 에디터에서 바로 보이게, 실행(Play)해야 보이는 방식 지양.** 기존 씬 매니저 중 일부(`LobbySceneManager.BuildUI()` 등)는 UI 전체를 `Start()`에서 코드로 절차적 생성 — 그래서 Play해야만 Hierarchy에 오브젝트가 보임. 앞으로는 GameObject/프리팹을 MCP 도구(`gameobject-create`, `assets-prefab-create` 등)로 에디터에 직접 만들어서, Play 없이도 Hierarchy에서 보고 위치 등을 직접 수정할 수 있게 할 것.

## 참고

- 이 컴퓨터엔 독립 git이 PATH에 없음. GitHub Desktop 번들 git 사용: `$env:LocalAppData\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`
