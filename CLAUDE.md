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
- `CallClock`: `RhythmManager`와 별개의 dspTime 기반 클록. `RhythmManager`는 `PatternData` 타입에 결합돼 있어 재사용하지 않고 같은 공식을 새로 구현. `Pause()`/`Resume()`로 진짜 일시정지 지원 — dspTime은 `Time.timeScale`과 무관하게 실시간으로 계속 흐르므로, `Pause()`는 그 순간의 `SongTime`을 얼려서 반환하게 하고 `Resume()`은 정지해 있던 만큼 `_scheduledMusicStartDsp` 기준점을 밀어서 시간이 안 튀게 함. **`CallDirector.Update()`도 `clock.IsPaused`면 통째로 스킵해야 함** — 안 그러면 `Time.timeScale=0`으로 일시정지해도 판정 루프(`ResolveTimeouts`)가 계속 dspTime 기준으로 도는 걸 감지해서 미스 판정+효과음이 계속 발생하는 버그가 남(실제로 겪음). `Stage1PauseMenuManager`는 오디오만 `AudioSource.Pause()` 하지 말고 반드시 `CallClock.Pause()/Resume()`을 통해서 호출할 것.
- `CallDirector`: 겹치는 콜들의 판정 상태머신. 버튼 입력 시 열려있는 콜 중 **마감이 가장 임박한 콜**에 배정. Hold는 릴리즈 시점만 판정하고, 조기 이탈은 즉시 확정 미스(재시도 불가).
- `CallCueIndicator`: 플레이스홀더 큐 비주얼. `GameManager`/`ScreenVignetteFlash`/`BattleSFXController`는 방향(ArrowDir) 의존성이 없어 그대로 재사용.
- 새 에디터 툴 `RhythmGame → Call Map Editor` (`Assets/Editor/Stage1CallMapEditorWindow.cs`): 기존 `RhythmGame → Beat Map Editor`(`BeatMapEditorWindow.cs`)를 참고했지만 절대 수정하지 않고 새로 작성. 기존 툴의 `IsBeatInConfirmedGroup` 같은 겹침 잠금 로직이 없음 — Tap/Hold 두 레인에 언제든 겹쳐 찍을 수 있음. REC 모드는 Space/마우스 클릭을 누르고 있는 길이로 Tap/Hold를 자동 구분.
  - **콜/리스폰스 워크플로**: 직접 찍거나 REC로 녹음한 노트는 전부 기본값 `role=Call`, 현재 `groupId`(툴바 "Group #" 표시)가 자동으로 찍힘. 콜 그룹을 다 찍은 뒤 **`Z` 키**를 누르면 그 그룹의 콜 리듬(박자 간격, Tap/Hold 모양)을 그대로 복사해 `role=Response`로 자동 생성 — 리스폰스 노트는 손으로 안 찍음. 그 뒤 `_currentGroupId`가 1 증가해서, 리스폰스 구간에 겹쳐 찍는 룩어헤드 콜도 자동으로 "다음 그룹" 취급됨. 노트를 선택하고 `C`로 role을 수동 토글 가능(드문 예외용).
    - REC/PREVIEW로 음악이 도는 중에 Z를 누르면 **그 순간의 실제 박자**가 리스폰스 시작점(라이브 연주 그대로). 음악이 안 도는 순수 편집 모드에서 누르면 툴바 "Echo Gap"(기본 1박) 고정 오프셋으로 대체.
  - **레인 레이아웃**: 물리적 줄 순서는 TAP-HOLD-TAP-HOLD 4줄(TAP끼리/HOLD끼리 몰아서가 아님). 노트의 줄(0/1)과 콜/리스폰스 그룹 배경 밴드(0/1)는 **둘 다 같은 `groupId`→밴드 매핑**(`RecomputeGroupBands`, 시작 시간 순으로 그냥 번갈아 0,1,0,1...)을 따름 — 같은 그룹 노트는 항상 같은 줄, 다른 그룹은 항상 다른 줄. 실제 시간 겹침 여부로 줄을 나누던 이전 방식은 그룹 배경과 노트 줄이 어긋나는 버그가 있어서 폐기함.
  - 그룹 배경(파랑/보라 번갈아 wash), 리스폰스 구간(초록 톤 + "RESPONSE" 라벨), 콜→리스폰스 경계("Z" 마커)는 전부 `groupId` 기준으로 그려짐 — 세션 상태가 아니라 데이터에서 매번 재계산하므로 에디터 재시작/Undo에도 유지됨.
- Stage1 미니게임 1번째(물붓기, "왼쪽 큰손이 물 붓고(콜) → 오른쪽 개미가 따라 붓고(리스폰스)")의 비주얼 구성: `Assets/물붓는손1.png`(16컷)/`개미군단.png`(4컷)/`광대1.png`·`광대2.png`(각 16컷, 보스가 손 차례일 때/개미 차례일 때) 스프라이트시트를 Sprite/Multiple로 슬라이스하고, 플립북 `AnimationClip` 4개(`Assets/Animations/Stage1/`) + `AnimatorController` 3개(Hand/Ants/Boss, Boss는 `ResponsePhase` bool로 상태 전환) 생성. `Stage1Scene`의 `Canvas` 밑에 `Background`(네온시티 배경, RawImage)/`Boss`/`Hand`/`Ants` UI Image + Animator를 배치함.
  - **CallDirector 연동 완료**: `CallDirector`에 이벤트 3개 추가, 여전히 Hand/Ants/Boss를 전혀 모르는 범용 상태 유지:
    - `OnPhaseChanged`(`System.Action<bool>`, true=Response): `SpawnDueCalls`에서 새 콜이 스폰될 때마다 그 콜의 role(정확히는 `!lc.isDemo`)이 직전 상태와 다르면 1회 발화. Boss의 `ResponsePhase` bool 전환에만 사용(Boss1↔Boss2 — 누구 차례인지 표시).
    - `OnCallCue`(`System.Action`): Call-role(데모) 엔트리가 스폰되는 그 순간(=그 콜의 박자) 발화. 채보 시계가 트리거하는 큐.
    - `OnResponsePress`(`System.Action`): 플레이어가 Space/마우스 클릭을 누르는 순간(pressEdge) 판정 성공 여부와 무관하게 무조건 발화. 플레이어 입력이 트리거하는 큐.
    - Hand/Ants는 **자유 루프 금지, 이벤트 없인 정지**: 각각 `OnCallCue`/`OnResponsePress`가 올 때만 `Stage1WaterPourVisuals`(`Assets/Scripts/Stage1WaterPourVisuals.cs`, `Director`에 부착)가 해당 Animator를 프레임 0부터 1회 재생(`Animator.Play(현재stateHash, 0, 0f)` + `speed=1`)시키고, 그 상태의 `length`만큼 코루틴으로 대기한 뒤 다시 `speed=0`으로 멈춤 — 그래서 Hand는 매 콜 박자마다, Ants는 매 플레이어 클릭마다만 "펄스"하듯 한 번씩 움직인다(둘 다 항상 화면엔 보임, `SetActive` 토글 안 함). Boss도 같은 펄스를 `OnCallCue`/`OnResponsePress`에 맞춰 받음(현재 활성 state — Boss1 or Boss2 — 를 재생만 다시 트리거, 상태 자체는 `ResponsePhase` bool이 결정). `Start()`에서 전부 `speed=0`으로 시작하고 `pattern.calls[0].role`로 Boss 초기 bool만 세팅.
    - **알려진 이슈**: `Time.timeScale`이 이전 플레이 세션(다른 씬의 일시정지/결과화면 등)에서 0으로 남는 경우가 있음(`RhythmManager`에 이미 있는 방어 로직과 동일 문제) — `CallClock.Awake()`에서 `Time.timeScale = 1f`로 리셋해서 해결함. Animator는 스케일된 시간 기준이라 timeScale=0이면 `speed`를 얼마로 세팅해도 프레임이 안 넘어감(반면 `CallDirector`/`CallClock`은 dspTime 기반이라 timeScale 영향 안 받아서 판정 로직만 보면 멀쩡해 보이는 함정이 있었음).
    - MCP 스크립트로 헤드리스 Play 테스트할 때 에디터가 포커스 없으면 프레임 자체가 극도로 드물게 도는 경우가 있어(예: 4.6초 동안 `Time.frameCount`가 2만 증가) `WaitForSeconds` 기반 코루틴 타이밍 검증이 신뢰도가 낮음 — 실제 프레임 페이싱/느낌은 에디터에 포커스를 두고 직접 플레이해서 확인할 것.
- **아직 안 된 것**: 나머지 미니게임 4개(존버/초단타/리믹스 보스 등) 전용 캐릭터 비주얼·사운드(각각 `Stage1WaterPourVisuals` 같은 전용 글루 스크립트 필요, `CallDirector`는 그대로 재사용), 여러 `CallPatternData`를 이어 재생하는 시퀀스 매니저, 기존에 이미 있던 `CallEntry`(groupId=-1, 미지정) 데이터를 새 groupId 체계로 옮기는 마이그레이션.

### 결과창 / 일시정지 — Stage1Scene이 이제 BattleScene을 대체하는 배틀씬 역할

`BattleScene`은 그대로 두되(참고용, 삭제 안 함), 앞으로 실제로 플레이할 배틀씬은 `Stage1Scene`. `BattleResultScreen`/`PauseMenuManager`와 동일한 룩·동작을 갖는 Stage1 전용 버전을 새로 만듦(기존 파일 수정 안 함):

- `Stage1ResultScreen`(`Assets/Scripts/Stage1ResultScreen.cs`, `Stage1ResultScreen` GameObject): `BattleResultScreen`과 완전히 동일한 스프라이트 에셋(`_sprPopup`/`_sprRankText`/`_sprRankS~C`/`_sprUnlockText`/`_sprRetryText`/`_sprArrow` — 같은 GUID, `Assets/Image/`)을 재사용해 절차적으로 팝업을 생성. `CallDirector.OnSongEnded`를 구독해 트리거. **BattleResultScreen과 달리 단일 페이지만 있음** — Stage1Scene은 아직 Lobby의 스테이지 해금/무구 경제(Page2, `PlayerPrefs` stage index)에 편입 안 됐으므로 페이지1만 보여주고 화살표는 항상 LobbyScene으로 이동. 기존 `Stage1ResultUI.cs`(플레이스홀더, 텍스트만)는 완전히 대체되어 삭제함 — 씬의 `Canvas/ResultPanel`(RankLabel/ClearLabel 손으로 배치했던 것)도 같이 제거.
- `Stage1PauseMenuManager`(`Assets/Scripts/Stage1PauseMenuManager.cs`, `Stage1PauseMenuManager` GameObject): `PauseMenuManager`와 동일한 UI/동작(ESC 토글, 3-2-1 리줌 카운트다운, CONTINUE/SETTINGS/RETRY/LOBBY) — RETRY는 `Stage1Scene`을 리로드, 게임 오디오는 `RhythmManager` 대신 `CallClock`의 AudioSource를 찾음.
- 공유 인프라인 `SettingsManager.cs`만 최소 확장(BattleScene 파일 자체는 안 건드림): ESC 핸들러가 `PauseMenuManager.Instance`뿐 아니라 `Stage1PauseMenuManager.Instance`도 확인해서 넘겨줌, `FindGameAudio()`가 `RhythmManager` 없으면 `CallClock`으로 폴백, `Close()`의 "배틀씬이라 카운트다운 리줌 써야 하나" 판단에 씬 이름 `"Stage1Scene"`도 포함.

### 스테이지 내장 튜토리얼 (신규 — 기획서 기반, "물타기" 파트만 우선 구현)

기획서 반영: 별도 `TutorialIntroScene/TutorialBattleScene/TutorialClearScene` 3단계를 없애고 **스테이지 씬 하나 안에 튜토리얼(앞) → 본편(뒤)을 로딩·암전 없이 이어붙이는** 구조로 전환. **기존 튜토리얼 씬 3개는 삭제하지 않고 그대로 둠**(레거시 보존 — 참조하는 곳 6곳 이상 있음: `BattleResultScreen`/`StartSceneManager`/`StockChartBackground`/`BossController` 등, 안 건드림). 이번 작업은 **물타기(Stage1Scene)에만** 적용 — 파도타기는 신규 미니게임이라 별도 아트/차트 필요, 아직 미착수.

- `Stage1DialogueSceneManager`(`Assets/Scripts/Stage1DialogueSceneManager.cs`, 씬 `Assets/Scenes/Stage1DialogueScene.unity`): `DialogueScene`/`DialogueSceneManager`를 **복제**(원본 수정 안 함 — 나중에 되돌릴 수도 있어서)해서 만듦. §0 오프닝(사이버 신당, 잭두-신령님 첫 만남, 신령님은 선택지 전용이라 초상화 불필요) + §1 물귀신 등장/인트로를 새로 채움. 끝나면 `BattleScene` 대신 `Stage1Scene`으로 전환. 원본에 없던 `Node.BgKey` 필드를 추가해서 §0→§1 전환 시 배경을 `튜토_사이버신당` → `스테1_폐허사이버증권거래소`로 교체. **§2(물타기 콜 시작 대사, "이제 진짜입니다" 등 인게임 코칭 한두 줄)는 이번 범위에 포함 안 함** — Stage1Scene 안에서 라이브로 트리거되어야 하는데 별도 캡션 시스템이 없어서 보류.
- `Stage1_Tutorial.asset`(`Assets/CallPatterns/`): `Stage1_MVP_Test`를 복제해서 만든 튜토리얼 전용 차트. 같은 음악(`Stage1_Battle.wav`)·같은 BPM(120, **템포를 늦추지 않음** — 기획서 요구사항) 재사용, `hitWindow`만 살짝 넓힘(0.15). 단순한 Call→Response 페어를 8세트(groupId 0~7) 넉넉하게 반복 배치 — "무한 관대(성공할 때까지 반복)"를 엔진이 진짜로 되감아서 구현하는 대신, **데이터에 필요량보다 훨씬 많은 반복을 미리 깔아두고 3세트 성공하면 조기 종료**하는 방식으로 단순화(사용자 확인받음). 실제 콜/응답 리듬 디자인은 임시로 직접 짠 것 — 기획자가 나중에 `Call Map Editor`로 다시 짤 수 있음.
- `Stage1TutorialGate`(`Assets/Scripts/Stage1TutorialGate.cs`, `Stage1Scene`의 `Stage1TutorialGate` GameObject): 튜토리얼→본편 전환 오케스트레이터.
  - `Stage1Scene`에 `Director`(본편, `Stage1_MVP_Test`)와 `TutorialDirector`(`Director`를 복제, `pattern`만 `Stage1_Tutorial`로 교체 — **`CallClock.pattern`과 `CallDirector.pattern` 둘 다 명시적으로 와이어링돼 있어서 둘 다 바꿔야 함**, 안 그러면 `CallClock.Pattern` 통해 자동으로 잡히는 것 대신 `CallDirector`가 예전 값을 그대로 씀) 두 벌의 `AudioSource+CallClock+CallDirector`가 나란히 존재. 씬 저장 시 `Director`는 `activeSelf=false`, `TutorialDirector`는 `activeSelf=true`가 기본값 — `Stage1TutorialGate.Start()`가 실제로 어느 쪽을 켤지 결정.
  - `PlayerPrefs`(`stage1_tutorial_seen`)를 봐서: 이미 봤으면 튜토리얼 GameObject를 끄고 바로 본편 활성화. 처음이면 튜토리얼만 켜고, `GameManager.OnJudgement`를 구독해 Hit 3번 쌓이면(미스는 카운트 안 함, 그냥 무시) 튜토리얼 끄고 `GameManager.ResetGame()`으로 튜토리얼 중 쌓인 점수/미스를 지운 뒤 본편 활성화 + 플래그 저장.
  - **버그 하나 실제로 겪음**: 처음 구현 시 "이미 봤음" 분기에서 본편만 켜고 튜토리얼 GameObject를 끄는 걸 깜빡해서 둘 다 동시에 켜져 있었음(둘 다 같은 GameManager/판정에 간섭) — `SwitchToMain()`이 반드시 `tutorialDirectorGO.SetActive(false)`도 같이 해야 함.
- `Stage1WaterPourVisuals`가 `CallDirector`를 하나만 붙들고 있던 것에서 **런타임 재바인딩 가능**하게 확장(`BindDirector(CallDirector)` 메서드) — Hand/Ants/Boss는 이제 `Director`가 아니라 별도의 항상-활성 `PourVisuals` GameObject에 있고, `Stage1TutorialGate`가 처음엔 `TutorialDirector`에, 게이트 통과 후엔 `Director`에 바인딩을 갈아끼움. 이걸 위해 `CallDirector`에 `StartsInResponsePhase` 프로퍼티(정렬된 `_calls[0].role`로 계산) 추가해서 `Stage1WaterPourVisuals`가 자체 `CallPatternData` 참조 없이도 재바인딩 시 초기 페이즈를 알 수 있게 함.
- 랭크 클리어 기준은 **그대로 B 이상 유지**(기획서에 "클리어 = A 이상"이라 적혀 있었지만 사용자가 "기획자님이 착각한 것 같다"며 기존 B+ 유지로 확인 — 나중에 진짜 A로 올려야 하면 다시 알려주기로 함, 이 메모는 그 결정을 기록해두는 용도).

## 컨벤션

- **오브젝트는 에디터에서 바로 보이게, 실행(Play)해야 보이는 방식 지양.** 기존 씬 매니저 중 일부(`LobbySceneManager.BuildUI()` 등)는 UI 전체를 `Start()`에서 코드로 절차적 생성 — 그래서 Play해야만 Hierarchy에 오브젝트가 보임. 앞으로는 GameObject/프리팹을 MCP 도구(`gameobject-create`, `assets-prefab-create` 등)로 에디터에 직접 만들어서, Play 없이도 Hierarchy에서 보고 위치 등을 직접 수정할 수 있게 할 것.

## 참고

- 이 컴퓨터엔 독립 git이 PATH에 없음. GitHub Desktop 번들 git 사용: `$env:LocalAppData\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`
- MCP 브릿지(`com.ivanmurzak.unity.mcp`)가 컴파일 에러(`CS0115` 등)로 안 뜨면 `Assets/Plugins/NuGet`의 의존 DLL 버전 불일치일 가능성이 높음. `Tools → AI Game Developer → Dependencies → Force Resolve NuGet DLLs`로 재해결 시도. 그래도 안 바뀌면 Unity 프로세스가 그 DLL을 물고 있어서(Windows 파일 잠금) 그런 것이니 에디터를 완전히 껐다 켠 뒤 다시 시도.
