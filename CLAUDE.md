# rhythm-prototype

Unity 리듬 게임 프로토타입. 기계 무당 **잭두**가 플레이어(**신령님**, 화면 밖 존재로 초상화 없음, 대사는 선택지로만)의 박자를 빌려 한 맺힌 귀신을 "놀아드리고 보내는" 굿을 벌인다. 스테이지 1의 손님은 주식에 물려 죽은 **물귀신**(광대 분장 = 도깨비/영감이 씌인 것). 리듬천국식 **콜(게임이 리듬을 보여줌) → 리스폰스(플레이어가 따라 침)** 구조, 버튼은 Space/마우스 좌클릭 하나, 판정은 Hit/Miss만.

Unity Editor는 **Unity 공식 CLI MCP 서버**(`.mcp.json`의 `unity`, 도구 이름 `mcp__unity__*`: `editor_status`, `get_scene_hierarchy`, `find_gameobjects`, `open_scene`, `set_component_properties`, `set_serialized_field`, `editor_play` 등)로 조작한다. 씬 YAML을 손으로 편집하지 말 것. `.claude/skills`에 남아 있는 `gameobject-*`, `scene-*`, `assets-*`, `ping` 같은 스킬은 ivanmurzak 브릿지(`unity-mcp-cli`)용이라 **쓰지 않는다** — 로그인을 요구하고 이 프로젝트에 MCP 서버로 등록돼 있지도 않다. 세션 시작 시 연결 확인은 `mcp__unity__editor_status` 한 번이면 충분.

## 씬 흐름 (빌드에 포함된 것만)

```
첫 실행 (opening_seen 없음):
StartScene
 → DialogueScene            §0 오프닝(옥탑방, 잭두 혼자). 게임 통틀어 1회 — PlayerPrefs "opening_seen". 끝나면 로비 없이 §1로 직행
 → Stage1DialogueScene      §1 물귀신 등장. 1회 — "stage1_underground_intro_seen". 끝에 잭두가 연기 속에서 개미로 변신,
                            그 화면이 연기 와이프(LoadSceneWithSmoke)로 1-1 인트로에 이어짐
 → Stage1_1IntroScene → Stage1_1Scene → 결과창 → 판 후 대사 → UndergroundLobbyScene(첫 등장)

두 번째 실행부터:
StartScene
 → SurfaceLobbyScene        지상 로비. 스테이지 슬롯 5개 가로 스크롤, 지금은 스테이지1만 입장 가능
 → UndergroundLobbyScene    지하 로비. 1-1~1-5 슬롯, 벽 구멍으로 각 판 배경이 보임, 미해금은 가림벽. 판 선택 시 연기 와이프
 → Stage1_NIntroScene       판별 인트로(타이틀 이미지 0.7초 지연 페이드인 + 짧은 연출 + IntroBGM, ~5초, 입력 없음, 글자 없음)
 → Stage1_NScene            연습 세그먼트(Stage1PracticeGate) → 본편 → Stage1ResultScreen → (S/A 첫 클리어면) 판 후 대사 오버레이
 → UndergroundLobbyScene    (돌아오면 마지막에 있던 슬롯 그대로)
```

- 씬 전환은 `SceneTransitionManager.Instance.LoadSceneWithFade("SceneName")`(검정 페이드). **판에 들어갈 때**(지하 로비→인트로, §1 변신→1-1)만 `LoadSceneWithSmoke` — 연기 퍼프(`Assets/Resources/Smoke/puff_*.png`, 파이썬 절차 생성)가 덮었다 걷힘. `SmokeCoverOnly/SmokeClearNow`는 씬 전환 없이 덮기만 할 때. 둘 다 unscaled 시간.
- "1회만" 게이팅은 **대상 씬 안에서 하지 않고 호출부에서** 한다(`StartSceneManager`, `SurfaceLobbyManager`, `UndergroundLobbyManager.TrySelect`, `Stage1ResultScreen`). 대사씬 안에서 체크하면 그 씬을 직접 열어 Play 테스트할 때마다 튕겨나간다(실제로 겪음). 대사씬은 완주 시 플래그만 세팅.
- PlayerPrefs 키: `opening_seen`, `stage1_underground_intro_seen`, `stage1_post_dialogue_seen_N`(N=0..4, 판 후 대사 1회), `underground_unlocked_index`(0-based, 해금된 마지막 판), `underground_last_index`(지하 로비 커서 위치). 초기화 ContextMenu가 `StartSceneManager`/`Stage1DialogueSceneManager`에 있음.

## 스테이지 1 — 5판

| 판 | 이름 | 미니게임 | 비주얼 글루 | 인트로 씬 요소 |
|---|---|---|---|---|
| 1-1 | 파도타기 | 개미 4마리가 파도 올 때 차례로 손 들고(콜), 5번째 개미(나)가 친다 | `Stage1WaveRideVisuals` | 바다·모래·떠가는 양동이(`Stage1IntroBucketDrift`)·잭두·개미 |
| 1-2 | 넘기기 | 목 마른 광대에게 잔을 개미 손에서 손으로 릴레이, 마지막이 나. 여러 잔이 겹쳐 돈다(컵 풀) | `Stage1QuickTapVisuals` | 광대, 말풍선 이미지 2개(흔들림) |
| 1-3 | 버티기 | 위에서 물을 퍼붓는 중 꾹 누르고(Hold) 숨 쉴 때 떼기. 광대→개미→나 순서로 물 밖에 나옴 | `Stage1SurviveVisuals` | 수조, 그림자 리빌 |
| 1-4 | 물타기 | 왼쪽 큰손이 붓는 대로(콜) 내 개미가 따라 붓기 | `Stage1AntPourVisuals` | 손, 풍선, 광대, 잭두 |
| 1-5 | 이별이여 작별이여 배 놓아가자 | 1-1~1-4 리믹스. 영감 굿노래에서 발췌한 이름 | `Stage1RemixPhaseSwitcher`가 4개 화면 중 하나만 켬 | 바다, 광대(양동이 타고 출렁), 잭두 |

- 판 이름 흐름이 물귀신의 주식 인생(추격매수 → 물량 넘기기 → 존버 → 물타기 → 손절/보내기)과 겹치도록 설계됨.
- 인트로 씬 타이틀은 5판 전부 이미지(`TitleImage`, `Stage1TitleCard.titleGraphic`에 배선 → 0.7초 뒤 페이드인하며 0.92→1 스케일). `TitleText`는 1-3~1-5에 있지만 비활성이라 `titleString`은 화면에 안 나옴. **인트로 씬에 글자를 넣지 않는다**(한 줄 캡션과 판 전 대사 씬을 넣었다가 둘 다 철회 — 설명은 §1 변신 장면과 판 후 대사가 맡음). 코드로 TMP를 새로 만들 땐 폰트를 `Assets/Resources/Fonts/NeoDunggeunmoProSDF`로 명시(기본 LiberationSans가 물려 한글이 깨짐 — 실제로 겪음).
- 1-5 리믹스는 같은 스타일이 한 곡 안에 여러 번 나온다. 각 Visuals는 `OnEnable/OnDisable`로 `CallDirector` 이벤트를 구독/해제하고, 꺼질 때 코루틴·상태를 반드시 초기화해야 한다(안 하면 "1-1 끝나고 1-2로 넘어가면 1-1 상태 그대로" 같은 버그가 남 — 실제로 겪음).

## 리듬 엔진 (Stage1 계열 전부 공용)

- `CallPatternData` (`Assets/Scripts/Data/CallPatternData.cs`, 에셋 `Assets/CallPatterns/`): `music`, `bpm`, `introDelay`, `hitWindow`/`missWindow`, `minInputIntervalSeconds`(마구누르기 쿨다운), 플랫한 `calls`(`CallEntry`: `Tap`/`Hold`, `role` = `Call`(데모, 판정·입력 없음)/`Response`(판정 대상), `groupId`, `cueBeat`/`releaseBeat`, 노트별 효과음 `clip`), `loopLengthBeats`(>0이면 연습용 1사이클 차트), `phases`(리믹스용 `styleId` 구간), `sfxCues`(판정 무관 효과음 큐). **콜은 시간상 겹칠 수 있다** — 이게 이 엔진의 핵심 요구사항.
  - `groupId`는 에디터가 배치 시점에 명시적으로 찍는 값. 시간/순서로 추론하지 않음. 이벤트 소비자가 "어느 그룹의 몇 번째 콜인지"를 알아야 할 때(1-2 컵 릴레이처럼 그룹이 진짜로 인터리브될 때)는 `OnCallCueGroup`/`OnGroupJudged`(groupId 실림)를 쓰고, 단순 순차면 `OnCallCue` 발화 횟수 mod N으로 충분.
- `CallClock`: dspTime 기반 `SongTime`. `Pause()/Resume()`로 진짜 일시정지(dspTime은 `Time.timeScale`과 무관하게 흐르므로 기준점을 밀어서 보정). **`CallDirector.Update()`는 `clock.IsPaused`면 통째로 스킵** — 안 그러면 일시정지 중에도 미스 판정+효과음이 계속 남(실제로 겪음). `Awake()`에서 `Time.timeScale = 1f` 리셋(이전 씬의 일시정지/결과창이 0으로 남기는 문제).
- `CallDirector`: 겹치는 콜들의 판정 상태머신. 입력은 열려 있는 Response 콜 중 **마감이 가장 임박한 것**에 배정. Hold는 릴리즈 시점만 판정, 조기 이탈은 즉시 미스. 이벤트: `OnCallCue`, `OnCallCueGroup(groupId)`, `OnResponsePress`(판정 무관, 누르는 순간), `OnResponseRelease`, `OnGroupJudged`, `OnPhaseChanged(bool isResponse)`, `OnSongEnded`. **Hand/Ants/Clown 같은 구체 오브젝트는 전혀 모름** — 그건 판별 Visuals 글루의 일. AudioSource도 모름(효과음은 `CallNoteSfxPlayer`).
- `GameManager`(싱글톤): Hit/Miss 집계 → `GetRank()` S/A/B/C. `IsClear`(B 이상)는 구 무구 경제 기준으로 **Stage1 결과창은 안 씀**.
- Visuals 글루 공통 규칙: 캐릭터는 **자유 루프 금지, 이벤트 없인 정지**. 콜 큐/플레이어 입력 이벤트가 올 때만 스프라이트 스왑 또는 1회 펄스. `SetActive` 토글로 숨기지 않음.

## 스테이지 씬 안의 구성

- `Stage1PracticeGate`: 연습 세그먼트 N개(각각 `AudioSource+CallClock+CallDirector+PracticeLoopExtender`) → 본편 `Director`. 세그먼트 차트는 1사이클만 그리고 `PracticeLoopExtender`가 무한 반복(0회차는 전부 Call로 강제 = 시범, 1회차부터 판정). Hit 3개(`requiredHits`)면 다음으로. 세그먼트/본편 전환은 항상 "옛 것 비활성 → 새 것 활성 + Visuals 재바인딩(`IPracticeRebindable.BindDirector`)" 관용구. 캡션(`introCaption`/`demoStartCaption`/`mainTransitionCaption`)은 인스펙터 문자열 → `Stage1SpeechBubble`(잭두 개미 머리 위, 3초 후 자동 숨김).
- `Stage1SkipHold`: E 2초 홀드로 연습 스킵. `Stage1PracticeCounter`: N/3 점 표시. 둘 다 씬에 배치된 오브젝트만 구동.
- `Stage1ResultScreen`: `OnSongEnded`에 팝업. **S/A면 다음 판 해금**(`UndergroundLobbyProgress.UnlockUpTo(stageIndex+1)`) + UnlockText, B/C면 RetryText. 이후 지하 로비로 — 단 S/A로 그 판을 **처음** 클리어했으면 팝업을 걷고 `Stage1PostDialogue`(판 후 대사)를 먼저 재생한 뒤 로비로.
- `Stage1PostDialogue`: 판 후 대사 오버레이. 프리팹 `Assets/Prefabs/Stage1PostDialoguePanel`(Canvas 정렬 100, Dim + 잭두/물귀신 초상화 + 대사창)이 Stage1_1~1_5 씬마다 비활성으로 하나씩 있고, 결과창의 `postDialogue` 필드에 배선(비면 자동 검색). 노드는 `stageIndex`별로 스크립트 안에 하드코딩, `NextByChoice`로 선택지 반응 분기 후 합류. timeScale 0인 채로 실시간 기준 동작. 1회 게이팅은 호출부(결과창)가 `HasSeen()`으로, 플래그 `stage1_post_dialogue_seen_N`은 완주 시 세팅. 리셋 ContextMenu 있음. 새 대사 씬 만들 때는 `Stage1DialogueSceneManager`를 복제하는 기존 패턴 유지. 팝업이 뜨면 모든 `CallDirector`를 정적 kill switch로 정지(안 하면 팝업 뒤 캐릭터가 입력에 반응). F1/F2는 테스트용 S/C 강제.
- `Stage1PauseMenuManager`: ESC 토글, 3-2-1 리줌, CONTINUE/SETTINGS/RETRY/LOBBY. 반드시 `CallClock.Pause()/Resume()` 경유. `SettingsManager`가 ESC를 여기로 넘겨줌.
- Hit/Miss 비네트(`ScreenVignetteFlash`)는 씬 Canvas에 **1개만** (중복 시 두 번 깜빡임, 21:9에서 위아래만 보이는 버그 수정됨).

## 에디터 툴 (`RhythmGame` 메뉴)

- **Chart Editor** (`Assets/Editor/Stage1ChartEditorWindow.cs`) — 현행. 겹치는 그룹을 그리디 인터벌 스케줄링으로 레인에 배치(3그룹 이상 동시 가능), 다중 선택/마키, SFX 레인, PHASE 레인, 노트 클릭 시 인라인 효과음 클립, 재생바가 실제 오디오 위치를 따름, 그리드 메트로놈, introDelay 미세조정.
- **Call Map Editor** (`Stage1CallMapEditorWindow.cs`) — 1세대. 레인 2개 고정. 1-1/1-4처럼 단순한 차트엔 여전히 동작. 수정하지 않음.
  - 공통 워크플로: 손으로 찍는 노트는 전부 `role=Call` + 현재 groupId. 콜 그룹 완성 후 **Z**로 같은 리듬의 Response 그룹 자동 생성(리스폰스는 손으로 안 찍음), groupId +1. **C**로 role 수동 토글. REC 모드는 Space/클릭 누른 길이로 Tap/Hold 구분. 1-1 같은 "콜 4개 → 리스폰스 1개" 형태는 Z 안 쓰고 손으로.
- **Sfx Cue Editor** (`Stage1SfxCueEditorWindow.cs`): 별도 `SfxCuePatternData`용. Q/W/E/R 레인에 판정 무관 효과음 큐. 지금은 대부분 Chart Editor의 SFX 레인(`CallPatternData.sfxCues`)으로 흡수됨.
- `BeatMapEditorWindow.cs`는 레거시 `PatternData`용. 안 건드림.

## 사운드

- `CallNoteSfxPlayer`: `CallEntry.clip`과 `sfxCues`를 그 박자에 재생. `CallDirector`와 같은 오브젝트, `Clock.SongTime` 폴링. 연습 루프면 `loopLengthBeats` 단위로 스스로 반복. 씬 로드 시 자동 부착(손 배선 불필요).
- `SFXManager`: UI 효과음(클릭/이동/선택/뒤로). wav 로드 후 피크 정규화. RuntimeInitialize 자동 부트스트랩.
- `LobbyBGMPlayer`: 지상/지하 로비 공용 BGM, DontDestroyOnLoad. 로비 매니저 `Awake()`에서 `Ensure()`만 호출. 로비 아닌 씬으로 가면 스스로 정리.
- 인트로 씬: `IntroBGM`(`Assets/인트로/1-N_intro.wav`) + `BackgroundAudio`(`AudioFadeInOut`) 레이어 분리. `Stage1TitleCard.oneShotSfx`는 별개.
- `StartSceneManager`는 `Resources/Music/StartBGM`을 씬마다 새로 만듦.

## 화면 / 종횡비

- 모든 Canvas는 Overlay + CanvasScaler(1920×1080, `matchWidthOrHeight=1`). 21:9 대응은 **SafeFrame**(16:9 고정 영역) 안에 UI를 두고 배경만 밖으로 넘치게 함(`AspectRatioLetterbox`). 결과창처럼 코드로 만드는 UI도 SafeFrame 밑에 붙일 것.
- `CameraBackgroundEnforcer`: 씬 로드마다 카메라 클리어를 검정 단색으로 강제(21:9 화면 밖이 유니티 기본 파랑으로 뜨던 문제).

## 대사 씬

- `DialogueSceneManager`(§0 오프닝, 7노드) / `Stage1DialogueSceneManager`(§1 물귀신, 5노드). 노드 배열이 C# 안에 하드코딩(`Nodes`), `Line`/`Choice`/`End`. 선택지는 신령님의 태도(냉소/호기심) 두 축이고, `NextByChoice[i]`로 잭두 반응 노드가 갈린 뒤 공통 노드로 합류. "저게 뭐야?"형 질문 선택지는 쓰지 않는다(`docs/design.md` 서사 전달 규칙). UI 오브젝트는 씬에 직접 배치(두 씬 Hierarchy 동일, 필드명도 동일하게 유지). 표정 스프라이트는 `EmotionKey` → `Assets/대사씬시작/`, ContextMenu "Auto-Load Emotions"로 재수집.
- 캐릭터 강조는 색 디밍만. `localScale`은 코드에서 안 건드림(에디터 값을 덮어써서 Play 시 초상화가 커지던 버그).
- 서사 전달 구조(2026-09-30 방향 B·변신판): 짧은 §0·§1 → §1 끝 **변신**(잭두 초상화 주변 연기 → 개미 잭두 스프라이트, `Stage1DialogueSceneManager.TransformIntoAnt`, 개미 크기·위치는 `_antDisplayHeight/_antOffsetX`) → 1-1 직행 → 판 후 대사(`Stage1PostDialogue`)로 물귀신이 한 단계씩 풀림. 세계관 규칙: 영감놀이 = 무리로 변해서 같이 놀아줌, 개미 몸은 신령님 장단에만 움직임, 개미 잭두는 말풍선으로만 말함. 상세는 `docs/design.md`.

## 컨벤션

- **오브젝트는 에디터에서 바로 보이게.** `Start()`에서 UI를 절차 생성하는 방식(`LobbySceneManager.BuildUI()` 등 레거시) 지양. 스크립트는 인스펙터에 배선된 오브젝트를 "구동"만 한다. 에디터에서 잡은 위치/크기를 코드가 매 프레임 덮어쓰지 않도록 주의(`Stage1SpeechBubble`은 Awake에서 배치 위치를 역산해 오프셋으로 씀).
- **RectTransform 계층 밖에서 world/screen 좌표에 오프셋을 더할 때는 `canvas.scaleFactor`를 곱한다.** `anchoredPosition`만 쓰면 불필요.
- 기존 시스템을 "고친다"는 가정으로 바꾸지 말고, 필요하면 **복제해서 새 파일**로 만들고 원본은 그대로 둔다(`Stage1ResultScreen`↔`BattleResultScreen`, `Stage1DialogueSceneManager`↔`DialogueSceneManager`, Chart Editor↔Call Map Editor 모두 이 패턴). 공유 인프라(`SettingsManager`)만 최소 확장.
- 새 씬을 만들면 `Awake()`에서 `Time.timeScale = 1f` 방어. 코루틴/애니메이터는 스케일된 시간이라 0이 남아 있으면 멈춰 보인다.
- MCP 헤드리스 Play 테스트는 에디터 포커스가 없으면 프레임이 거의 안 돌아 타이밍 검증 신뢰도가 낮다. 느낌 확인은 직접 플레이.

## 레거시 (빌드 미포함, 삭제 안 함, 건드리지 않음)

- 구 배틀: `BattleScene`, `BattleDirector`, `RhythmManager`, `PatternData`(`Assets/Chapter/`), `BossController`, `BattleResultScreen`, `PauseMenuManager`, `GameProgressManager`(무구 경제), `LobbyScene`/`LobbySceneManager`.
- 더 옛 노트 레인: `NoteManager`, `Note`, `ArrowSprite`, `CircleSprites`, `LaneIndicator`.
- 구 튜토리얼 씬 3개: `TutorialIntroScene`/`TutorialBattleScene`/`TutorialClearScene`. 참조하는 곳이 많아 남겨둠.
- 물붓기 프로토: `Stage1Scene` + `Stage1TutorialGate` + `Stage1WaterPourVisuals`(Animator 플립북 방식). 1-4 물타기의 전신. `Stage1PracticeGate`가 이걸 일반화한 것.
- `Stage1_1Scene_NewArt` + `Stage1WaveRideVisuals_NewArt`: 1-1 새 아트 비교용. 방향 정해지면 하나 삭제.
- `SpiritOrb`, `GaugeManager`, `UIManager`, `StockChartBackground`, `TurnSpotlightEffect`, `PlayerDanceAnimator` 등은 구 배틀 연출.
- `Calibration*`은 레거시 아님 — StartScene/SettingsManager에서 쓰는 입력 보정.

## 참고

- 이 컴퓨터엔 독립 git이 PATH에 없음. GitHub Desktop 번들 git 사용: `$env:LocalAppData\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`
- 공식 MCP 서버(`unity.exe mcp --project-path ...`)가 응답하지 않으면 먼저 Unity Editor가 이 프로젝트를 열고 있는지, 컴파일 에러로 멈춰 있지 않은지 확인(`editor_status`의 `compiling`/`status`). 에디터를 완전 재시작하면 대부분 복구됨.
- 프로젝트에는 예전 ivanmurzak 브릿지(`com.ivanmurzak.unity.mcp`, `Assets/Plugins/NuGet`)가 아직 남아 있다. 이 패키지 때문에 컴파일 에러(`CS0115` 등)가 나면 `Tools → AI Game Developer → Dependencies → Force Resolve NuGet DLLs`로 DLL 버전을 맞춘다. 조작 경로로는 쓰지 않는다.
