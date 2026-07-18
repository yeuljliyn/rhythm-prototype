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
  - `CallEntry.role` (`CallRole`: `Call`/`Response`, 엔트리별): `Call`은 보스/손이 보여주기만 하는 데모 큐(판정 안 됨, 입력 안 읽음), `Response`가 실제 판정 대상. 기존 `liveInputStartBeat`(전역 컷오프, 한 번만 전환)와 달리 곡 전체에서 콜↔리스폰스가 반복되는 구조를 표현 가능. `CallDirector`는 `role==Call || cueBeat<liveInputStartBeat`를 데모 판정 기준으로 씀(`CallDirector.BuildLiveCall`).
  - `CallEntry.groupId` (에디터 전용, 런타임 `CallDirector`는 무시): Z-에코로 묶인 콜/리스폰스 그룹 식별자. **시간/순서로 추론하지 않고 에디터가 배치 시점에 명시적으로 찍음** — 그래서 리스폰스 구간에 다음 콜을 겹쳐(룩어헤드) 찍어도 그룹이 안 섞임.
- `CallClock`: `RhythmManager`와 별개의 dspTime 기반 클록. `RhythmManager`는 `PatternData` 타입에 결합돼 있어 재사용하지 않고 같은 공식을 새로 구현.
- `CallDirector`: 겹치는 콜들의 판정 상태머신. 버튼 입력 시 열려있는 콜 중 **마감이 가장 임박한 콜**에 배정. Hold는 릴리즈 시점만 판정하고, 조기 이탈은 즉시 확정 미스(재시도 불가).
- `CallCueIndicator` / `Stage1ResultUI`: 각각 플레이스홀더 큐 비주얼, 곡 끝 랭크 표시. `GameManager`/`ScreenVignetteFlash`/`BattleSFXController`는 방향(ArrowDir) 의존성이 없어 그대로 재사용.
- 새 에디터 툴 `RhythmGame → Call Map Editor` (`Assets/Editor/Stage1CallMapEditorWindow.cs`): 기존 `RhythmGame → Beat Map Editor`(`BeatMapEditorWindow.cs`)를 참고했지만 절대 수정하지 않고 새로 작성. 기존 툴의 `IsBeatInConfirmedGroup` 같은 겹침 잠금 로직이 없음 — Tap/Hold 두 레인에 언제든 겹쳐 찍을 수 있음. REC 모드는 Space/마우스 클릭을 누르고 있는 길이로 Tap/Hold를 자동 구분.
  - **콜/리스폰스 워크플로**: 직접 찍거나 REC로 녹음한 노트는 전부 기본값 `role=Call`, 현재 `groupId`(툴바 "Group #" 표시)가 자동으로 찍힘. 콜 그룹을 다 찍은 뒤 **`Z` 키**를 누르면 그 그룹의 콜 리듬(박자 간격, Tap/Hold 모양)을 그대로 복사해 `role=Response`로 자동 생성 — 리스폰스 노트는 손으로 안 찍음. 그 뒤 `_currentGroupId`가 1 증가해서, 리스폰스 구간에 겹쳐 찍는 룩어헤드 콜도 자동으로 "다음 그룹" 취급됨. 노트를 선택하고 `C`로 role을 수동 토글 가능(드문 예외용).
    - REC/PREVIEW로 음악이 도는 중에 Z를 누르면 **그 순간의 실제 박자**가 리스폰스 시작점(라이브 연주 그대로). 음악이 안 도는 순수 편집 모드에서 누르면 툴바 "Echo Gap"(기본 1박) 고정 오프셋으로 대체.
  - **레인 레이아웃**: 물리적 줄 순서는 TAP-HOLD-TAP-HOLD 4줄(TAP끼리/HOLD끼리 몰아서가 아님). 노트의 줄(0/1)과 콜/리스폰스 그룹 배경 밴드(0/1)는 **둘 다 같은 `groupId`→밴드 매핑**(`RecomputeGroupBands`, 시작 시간 순으로 그냥 번갈아 0,1,0,1...)을 따름 — 같은 그룹 노트는 항상 같은 줄, 다른 그룹은 항상 다른 줄. 실제 시간 겹침 여부로 줄을 나누던 이전 방식은 그룹 배경과 노트 줄이 어긋나는 버그가 있어서 폐기함.
  - 그룹 배경(파랑/보라 번갈아 wash), 리스폰스 구간(초록 톤 + "RESPONSE" 라벨), 콜→리스폰스 경계("Z" 마커)는 전부 `groupId` 기준으로 그려짐 — 세션 상태가 아니라 데이터에서 매번 재계산하므로 에디터 재시작/Undo에도 유지됨.
- Stage1 미니게임 1번째(물붓기, "왼쪽 큰손이 물 붓고(콜) → 오른쪽 개미가 따라 붓고(리스폰스)")의 **비주얼만 우선 구성**: `Assets/물붓는손1.png`(16컷)/`개미군단.png`(4컷)/`광대1.png`·`광대2.png`(각 16컷, 보스가 손 차례일 때/개미 차례일 때) 스프라이트시트를 Sprite/Multiple로 슬라이스하고, 플립북 `AnimationClip` 4개(`Assets/Animations/Stage1/`) + `AnimatorController` 3개(Hand/Ants/Boss, Boss는 `ResponsePhase` bool로 상태 전환) 생성. `Stage1Scene`의 `Canvas` 밑에 `Background`(네온시티 배경, RawImage)/`Boss`/`Hand`/`Ants` UI Image + Animator를 배치함. **아직 CallDirector 이벤트와 연결 안 됨** — 지금은 그냥 계속 반복 재생만 함, 실제 콜/리스폰스 타이밍에 맞춰 손/개미 애니메이션 트리거하고 Boss1/Boss2 전환하는 로직은 다음 작업.
- **아직 안 된 것**: 물붓기 미니게임의 CallDirector 연동(위 항목), 나머지 미니게임 4개(존버/초단타/리믹스 보스 등) 전용 캐릭터 비주얼·사운드, 여러 `CallPatternData`를 이어 재생하는 시퀀스 매니저, 기존에 이미 있던 `CallEntry`(groupId=-1, 미지정) 데이터를 새 groupId 체계로 옮기는 마이그레이션.

## 컨벤션

- **오브젝트는 에디터에서 바로 보이게, 실행(Play)해야 보이는 방식 지양.** 기존 씬 매니저 중 일부(`LobbySceneManager.BuildUI()` 등)는 UI 전체를 `Start()`에서 코드로 절차적 생성 — 그래서 Play해야만 Hierarchy에 오브젝트가 보임. 앞으로는 GameObject/프리팹을 MCP 도구(`gameobject-create`, `assets-prefab-create` 등)로 에디터에 직접 만들어서, Play 없이도 Hierarchy에서 보고 위치 등을 직접 수정할 수 있게 할 것.

## 참고

- 이 컴퓨터엔 독립 git이 PATH에 없음. GitHub Desktop 번들 git 사용: `$env:LocalAppData\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`
- MCP 브릿지(`com.ivanmurzak.unity.mcp`)가 컴파일 에러(`CS0115` 등)로 안 뜨면 `Assets/Plugins/NuGet`의 의존 DLL 버전 불일치일 가능성이 높음. `Tools → AI Game Developer → Dependencies → Force Resolve NuGet DLLs`로 재해결 시도. 그래도 안 바뀌면 Unity 프로세스가 그 DLL을 물고 있어서(Windows 파일 잠금) 그런 것이니 에디터를 완전히 껐다 켠 뒤 다시 시도.
