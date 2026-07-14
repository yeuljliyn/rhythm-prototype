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

## Stage1 콜-리스폰스 배틀 (신규 — `Stage1Scene`, 진행 중)

배틀씬을 완전히 다른 방식으로 새로 만드는 중. **기존 `BattleScene`/`BattleDirector`는 그대로 두고, `Assets/Scenes/Stage1Scene.unity`에서 독립적으로 작업.** 기존 시스템을 "고친다"는 가정으로 수정하지 말 것.

리듬천국 스타일 콜(Call, 게임이 박자를 알려줌) + 리스폰스(Response, 탭/홀드릴리즈 입력) 엔진. 버튼 1개(Space·마우스 좌클릭을 같은 응답으로 처리), 판정은 Hit/Miss 2단뿐, 플레이 중 점수·콤보·%는 화면에 안 띄우고 콜마다 즉시 반응만 보여줌. 곡 끝에만 랭크(B 이상 클리어) 표시, 미스해도 곡은 끝까지 진행.

- `CallPatternData` (ScriptableObject, `Assets/Scripts/Data/CallPatternData.cs`, 에셋은 `Assets/CallPatterns/`): 플랫한 `CallEntry`(`Tap`/`Hold`, `cueBeat`/`releaseBeat`) 리스트. 기존 `PatternData`의 `DirectionGroup`(콜 구간→응답 구간 순차 구조)과 달리 **구조적으로 콜끼리 겹칠 수 있음** — 이게 이번 재작업의 핵심 요구사항.
- `CallClock`: `RhythmManager`와 별개의 dspTime 기반 클록. `RhythmManager`는 `PatternData` 타입에 결합돼 있어 재사용하지 않고 같은 공식을 새로 구현.
- `CallDirector`: 겹치는 콜들의 판정 상태머신. 버튼 입력 시 열려있는 콜 중 **마감이 가장 임박한 콜**에 배정. Hold는 릴리즈 시점만 판정하고, 조기 이탈은 즉시 확정 미스(재시도 불가).
- `CallCueIndicator` / `Stage1ResultUI`: 각각 플레이스홀더 큐 비주얼, 곡 끝 랭크 표시. `GameManager`/`ScreenVignetteFlash`/`BattleSFXController`는 방향(ArrowDir) 의존성이 없어 그대로 재사용.
- 새 에디터 툴 `RhythmGame → Call Map Editor` (`Assets/Editor/Stage1CallMapEditorWindow.cs`): 기존 `RhythmGame → Beat Map Editor`(`BeatMapEditorWindow.cs`)를 참고했지만 절대 수정하지 않고 새로 작성. 기존 툴의 `IsBeatInConfirmedGroup` 같은 겹침 잠금 로직이 없음 — Tap/Hold 두 레인에 언제든 겹쳐 찍을 수 있음. REC 모드는 Space/마우스 클릭을 누르고 있는 길이로 Tap/Hold를 자동 구분.
- **아직 안 된 것**: 스테이지1의 실제 미니게임 5개(개미 파도타기/물타기/존버/초단타/리믹스 보스) 각각의 전용 캐릭터 비주얼·사운드, 여러 `CallPatternData`를 이어 재생하는 시퀀스 매니저. 기획 비주얼 에셋 확보 후 진행 — 그 전까지는 위 엔진 + 에디터 툴 + 최소 재생 가능한 씬 골격까지만 완성된 상태.

## 컨벤션

- **오브젝트는 에디터에서 바로 보이게, 실행(Play)해야 보이는 방식 지양.** 기존 씬 매니저 중 일부(`LobbySceneManager.BuildUI()` 등)는 UI 전체를 `Start()`에서 코드로 절차적 생성 — 그래서 Play해야만 Hierarchy에 오브젝트가 보임. 앞으로는 GameObject/프리팹을 MCP 도구(`gameobject-create`, `assets-prefab-create` 등)로 에디터에 직접 만들어서, Play 없이도 Hierarchy에서 보고 위치 등을 직접 수정할 수 있게 할 것.

## 참고

- 이 컴퓨터엔 독립 git이 PATH에 없음. GitHub Desktop 번들 git 사용: `$env:LocalAppData\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`
- MCP 브릿지(`com.ivanmurzak.unity.mcp`)가 컴파일 에러(`CS0115` 등)로 안 뜨면 `Assets/Plugins/NuGet`의 의존 DLL 버전 불일치일 가능성이 높음. `Tools → AI Game Developer → Dependencies → Force Resolve NuGet DLLs`로 재해결 시도. 그래도 안 바뀌면 Unity 프로세스가 그 DLL을 물고 있어서(Windows 파일 잠금) 그런 것이니 에디터를 완전히 껐다 켠 뒤 다시 시도.
