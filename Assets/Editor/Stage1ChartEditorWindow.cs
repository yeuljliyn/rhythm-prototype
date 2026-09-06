using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Second-generation chart editor for CallPatternData (Stage1 engine) — lives entirely alongside
/// Stage1CallMapEditorWindow.cs, which is left completely untouched. 1-1/1-4 already work fine on
/// the old tool; this file exists to fix the two things that don't scale there:
///
/// 1) FIXED 2-LANE LAYOUT. The old tool always alternates overlapping groups between exactly 2
///    bands (GROUP_BANDS = 2, RecomputeGroupBands). That's fine for 1-1/1-4's "one call group, one
///    look-ahead at most" shape, but 1-2's cup relay needs 3+ genuinely concurrent groups on screen,
///    which the old tool has no room for — notes from a 3rd overlapping group just land back on top
///    of band 0 or 1. This file replaces the fixed alternation with a real greedy interval-scheduling
///    lane allocator (RecomputeGroupBands below): sort groups by start beat, assign each to the
///    lowest-numbered lane whose previous occupant has already ended, allocate a new lane only when
///    none is free. Recomputed from scratch on every single repaint (never cached across frames) —
///    a previous attempt at dynamic lanes reportedly got stuck always reusing the same lane instead
///    of ever allocating a new one, which smells exactly like a stale cache or an inverted "is this
///    lane free" comparison; recomputing fresh every draw and keeping the algorithm to the textbook
///    "earliest-freed lane" form sidesteps both.
/// 2) SINGLE-ITEM EVERYTHING. The old tool has exactly one selected CallEntry, one drag, no marquee-
///    select, no copy/paste. This file uses a HashSet<CallEntry> selection instead, adds Shift-drag
///    marquee select, Shift-click to toggle, double-click to select a whole group, multi-note drag
///    (whole selection moves together — but only for a "move" grab, i.e. a Tap note or a Hold body;
///    grabbing a Hold's start/end handle only resizes that one note, never the rest of the
///    selection), Ctrl+C/Ctrl+V (paste at the mouse's current beat, fresh groupId), and a "duplicate
///    selection N beats later" toolbar action built on the same copy/paste primitive.
///
/// Also adds scrub/seek: click the ruler to set a scrub beat, then Preview/REC can start from there
/// instead of always from the very beginning — useful once a chart is long enough that re-listening
/// from beat 0 every time gets painful. See StartSession's startBeat parameter and the
/// _sessionStartSongTime field (replaces the old hardcoded "-introDelay" starting point).
///
/// Everything else (header fields, REC/Echo workflow, audio preview reflection helpers, snap) is
/// carried over near-verbatim from Stage1CallMapEditorWindow.cs — those parts aren't what's broken.
/// Both tools read/write the exact same CallPatternData asset format, so any chart openable in one
/// is openable in the other; nothing here changes the data model.
/// </summary>
public class Stage1ChartEditorWindow : EditorWindow
{
    // ── State ──────────────────────────────────────────────────────────────
    CallPatternData _pattern;
    bool   _isRecording;
    bool   _isPlaying;
    double _sessionStart;
    float  _sessionStartSongTime; // chart-time (seconds relative to beat 0) audio began playing from
    float  _currentSongTime;

    // REC-mode press tracking (Space key or mouse-left, whichever starts first)
    bool   _isPressed;
    float  _pressStartBeat;
    double _pressStartRealTime;
    [SerializeField] float _holdThresholdSec = 0.15f;

    // ── Scrub / seek ───────────────────────────────────────────────────────
    float _scrubBeat = 0f; // set by clicking the ruler; Preview/REC "from here" starts from this beat

    // ── Timeline view ──────────────────────────────────────────────────────
    Vector2 _scrollPos;
    float   _zoom = 120f;

    // ── Selection / drag (edit mode) ───────────────────────────────────────
    enum DragHandle { None, TapNote, HoldStart, HoldEnd, HoldBody }
    readonly HashSet<CallEntry> _selection = new HashSet<CallEntry>();
    CallEntry  _primaryEntry; // last entry explicitly clicked — drives the numeric-edit panel/status line
    DragHandle _dragHandle = DragHandle.None;
    CallEntry  _dragAnchor;   // the specific entry actually grabbed; determines Tap/Hold delta math
    bool   _isDragging;
    float  _dragOriginMouseX;
    float  _dragBeatDelta;
    // Per-entry snapshot of cueBeat/releaseBeat at drag start, for every entry the drag will move
    // (just _dragAnchor for a resize, the whole _selection for a move — see class doc point 2).
    readonly Dictionary<CallEntry, Vector2> _dragOrigins = new Dictionary<CallEntry, Vector2>();

    // ── Marquee (Shift-drag on empty space) ────────────────────────────────
    bool    _isMarqueeSelecting;
    Vector2 _marqueeStart;
    Vector2 _marqueeCurrent;
    // Screen rects of everything drawn this frame, for marquee hit-testing — populated by DrawCalls.
    readonly Dictionary<CallEntry, Rect> _entryScreenRects = new Dictionary<CallEntry, Rect>();

    // ── Clipboard ──────────────────────────────────────────────────────────
    readonly List<CallEntry> _clipboard = new List<CallEntry>();
    float _duplicateOffsetBeats = 4f;

    // ── Snap ───────────────────────────────────────────────────────────────
    static readonly int[]    SnapDivs   = { 1, 2, 4, 8, 16 };
    static readonly string[] SnapLabels = { "1/1", "1/2", "1/4", "1/8", "1/16" };
    int _snapIndex = 2;

    // ── Echo (Z key) — identical workflow/semantics to Stage1CallMapEditorWindow ───────────────
    int _currentGroupId;
    [SerializeField] float _echoGapBeats = 1f;

    // ── Survive-cycle batch placement (존버 사이클 한번에 찍기) — see DrawToolbar's row for this ──
    [SerializeField] float _surviveClownOffset = 1f;
    [SerializeField] float _surviveAntOffset = 2f;
    [SerializeField] float _surviveReleaseOffset = 3f;
    [SerializeField] float _surviveResubmergeOffset = 4f;

    // ── Phases (1-5-style multi-stage charts — see CallPatternData.phases) ─────────────────────
    // A separate strip below the note lanes, not another note type: phases tag beat *ranges* with
    // which minigame style owns them, independent of the Tap/Hold/Call/Response notes placed inside
    // that range. Same create/resize/move/delete-by-dragging interaction shape as a Hold note, just
    // applied to ChartPhase instead of CallEntry, with its own single-selection state (kept separate
    // from _selection/_selectedPhase's CallEntry HashSet on purpose — a phase isn't a note and
    // shouldn't accidentally get swept into copy/paste/duplicate/Ctrl+A).
    enum PhaseDragHandle { None, Start, End, Body }
    const float PHASE_LANE_H = 28f;
    ChartPhase      _selectedPhase;
    PhaseDragHandle _phaseDragHandle = PhaseDragHandle.None;
    bool   _isPhaseDragging;
    float  _phaseDragOriginMouseX;
    float  _phaseDragOriginStart;
    float  _phaseDragOriginEnd;
    float  _phaseDragBeatDelta;
    [SerializeField] string _newPhaseStyleId = "1-1"; // remembered default for the next phase created

    // ── SFX cue lane (CallPatternData.sfxCues — pure sound triggers, no judgement) ─────────────
    // A second strip below the PHASE lane. Each cue is a point on the beat timeline carrying one
    // AudioClip; CallNoteSfxPlayer plays it at that beat in game. Functionally nothing to do with
    // Tap/Hold — it's the phase-lane interaction shape (click = add, drag = move, right-click =
    // delete) minus the length. Own single-selection state, kept out of _selection like _selectedPhase.
    const float SFX_LANE_H = 24f;
    SfxCueEntry _selectedSfx;
    bool   _isSfxDragging;
    float  _sfxDragOriginMouseX;
    float  _sfxDragOriginBeat;
    float  _sfxDragBeatDelta;

    static readonly Color SfxCueColor = new Color(0.35f, 0.80f, 1.00f);

    static readonly Color[] PhasePalette =
    {
        new Color(0.95f, 0.55f, 0.55f),
        new Color(0.55f, 0.85f, 0.55f),
        new Color(0.55f, 0.65f, 0.95f),
        new Color(0.95f, 0.85f, 0.45f),
        new Color(0.80f, 0.55f, 0.95f),
        new Color(0.55f, 0.90f, 0.90f),
    };

    // Stable color per styleId (hash-based), so the same style always reads as the same color
    // across the whole timeline and across editor sessions, without needing a hand-maintained
    // style->color table that would need updating every time a new minigame style is added.
    static Color PhaseColor(string styleId)
    {
        if (string.IsNullOrEmpty(styleId)) return new Color(0.45f, 0.45f, 0.45f);
        int hash = 0;
        foreach (char c in styleId) hash = hash * 31 + c;
        return PhasePalette[Mathf.Abs(hash) % PhasePalette.Length];
    }

    // ── Layout constants ───────────────────────────────────────────────────
    const float LABEL_W    = 48f;
    const float RULER_H    = 22f;
    const float LANE_H     = 44f;
    const int   LANE_COUNT = 2;   // sections: Tap, Hold — unlike SUBROWS, this is genuinely fixed
    const float NOTE_W     = 10f;
    const float MIN_HOLD_BEATS = 0.1f;

    // The SFX cue strip sits directly under the ruler, above the Tap/Hold note lanes; the PHASE
    // strip stays pinned to the very bottom. So the note lanes start below ruler + SFX strip.
    const float NOTE_TOP = RULER_H + SFX_LANE_H;

    // Dynamic band count — how many concurrent lanes the busiest moment of the chart currently
    // needs. Recomputed every DrawTimeline() call by RecomputeGroupBands (see class doc point 1).
    // TOTAL_ROWS = LANE_COUNT * SUBROWS, interleaved Tap/Hold/Tap/Hold/... same physical ordering
    // rationale as the old tool (each band gets one full Tap+Hold pass).
    int SUBROWS    = 1;
    int TOTAL_ROWS => LANE_COUNT * Mathf.Max(1, SUBROWS);
    static int SectionOfRow(int row, int subrows) => row % LANE_COUNT;
    static int SubRowOfRow(int row, int subrows)  => row / LANE_COUNT;
    static int RowOf(int section, int subRow)     => subRow * LANE_COUNT + section;

    static readonly string[] LaneLabels = { "TAP", "HOLD" };
    static readonly Color[]  LaneColors =
    {
        new Color(1.00f, 0.85f, 0.20f),   // Tap  – yellow
        new Color(0.30f, 0.70f, 1.00f),   // Hold – blue
    };

    static readonly Color[] GroupBgColors =
    {
        new Color(0.20f, 0.40f, 0.60f, 0.12f),
        new Color(0.50f, 0.20f, 0.60f, 0.12f),
        new Color(0.60f, 0.45f, 0.15f, 0.12f),
        new Color(0.20f, 0.55f, 0.35f, 0.12f),
        new Color(0.55f, 0.20f, 0.20f, 0.12f),
        new Color(0.30f, 0.30f, 0.60f, 0.12f),
    };

    // ── Audio reflection ───────────────────────────────────────────────────
    static MethodInfo _playMethod;
    static MethodInfo _stopMethod;

    // ── Menu ───────────────────────────────────────────────────────────────
    [MenuItem("RhythmGame/Chart Editor")]
    static void Open()
    {
        var w = GetWindow<Stage1ChartEditorWindow>("채보 에디터");
        w.minSize = new Vector2(700f, 380f);
        w.Show();
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────
    void OnEnable()  => EditorApplication.update += Tick;
    void OnDisable() { EditorApplication.update -= Tick; StopAudio(); }

    void Tick()
    {
        if (!_isRecording && !_isPlaying) return;
        if (_pattern == null) { StopSession(); return; }

        _currentSongTime = _sessionStartSongTime + (float)(EditorApplication.timeSinceStartup - _sessionStart);

        // PlayAudio() fires immediately in StartSession() (no artificial wait) and
        // _sessionStartSongTime starts at 0 for that same instant, so _currentSongTime is always true
        // audio content time — 0 at the real content start, counting up in lockstep with what's
        // actually playing. That's the same quantity CallPatternData.BeatToTime's output represents,
        // so every pixel-from-time conversion below multiplies by _zoom directly, no manual
        // "+ introDelay" (introDelay is baked into BeatToTime/TimeToBeat themselves — see
        // CallPatternData.cs — so notes captured here via TimeToBeat(_currentSongTime) round-trip
        // correctly without needing any real silent lead-in here to match anything at runtime;
        // CallClock's own real introDelay-second wait before its AudioSource starts is a separate,
        // unrelated real-time scheduling detail — see CallClock.OnEnable — that this tool doesn't
        // need to reproduce).
        float musicLen = _pattern.music != null ? _pattern.music.length : 0f;
        if (musicLen > 0f && _currentSongTime > musicLen)
            StopSession();

        float absX   = _currentSongTime * _zoom;
        float viewW  = position.width - LABEL_W - 20f;
        float target = absX - viewW * 0.25f;
        if (Mathf.Abs(target - _scrollPos.x) > 8f)
            _scrollPos.x = Mathf.Max(0f, target);

        Repaint();
    }

    // ── OnGUI ──────────────────────────────────────────────────────────────
    void OnGUI()
    {
        DrawHeader();

        if (_pattern == null)
        {
            EditorGUILayout.HelpBox(
                "위에서 Call Pattern Data 에셋을 선택하거나 새로 만들면 시작할 수 있습니다.",
                MessageType.Info);
            return;
        }

        DrawToolbar();
        DrawTimeline();

        if (_isRecording) CaptureRecordingKeys();
        else              HandleGlobalKeys();
    }

    // ── Header ─────────────────────────────────────────────────────────────
    void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var next = (CallPatternData)EditorGUILayout.ObjectField(
                    "채보 데이터", _pattern, typeof(CallPatternData), false);
                if (next != _pattern) { StopSession(); _pattern = next; ClearSelection(); _selectedPhase = null; _selectedSfx = null; _currentGroupId = NextFreeGroupId(next); }
                if (GUILayout.Button("새로 만들기", GUILayout.Width(80))) CreateNewCallPattern();
            }

            if (_pattern == null) return;

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _pattern.chartName  = EditorGUILayout.TextField("이름", _pattern.chartName);
                GUILayout.Space(8);
                EditorGUILayout.LabelField("BPM", GUILayout.Width(30));
                _pattern.bpm = EditorGUILayout.FloatField(_pattern.bpm, GUILayout.Width(56));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("인트로 딜레이", "음악 파일 시작부터 첫 박자(beat 0)까지 걸리는 시간(초)."),
                    GUILayout.Width(72));
                _pattern.introDelay = EditorGUILayout.FloatField(_pattern.introDelay, GUILayout.Width(46));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Hit 윈도우 (±초)", "정타로 인정되는 판정 반폭(초). cueBeat(홀드는 releaseBeat) ± 이 값 이내면 Hit."),
                    GUILayout.Width(96));
                _pattern.hitWindow = EditorGUILayout.FloatField(_pattern.hitWindow, GUILayout.Width(56));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("Miss 윈도우 (±초)", "Hit 윈도우 바로 바깥쪽에 붙는 '아깝게 놓침' 판정용 여유 반폭."),
                    GUILayout.Width(102));
                _pattern.missWindow = EditorGUILayout.FloatField(_pattern.missWindow, GUILayout.Width(56));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("라이브 입력 시작 박자", "이 박자보다 이전인 콜은 role과 무관하게 항상 데모 전용(판정 안 함)으로 취급됨."),
                    GUILayout.Width(120));
                _pattern.liveInputStartBeat = EditorGUILayout.FloatField(_pattern.liveInputStartBeat, GUILayout.Width(56));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("마구 누르기 쿨다운 (초)", "곡 전체에 적용되는 마구 누르기 방지 쿨다운. 0이면 쿨다운 없음."),
                    GUILayout.Width(104));
                _pattern.minInputIntervalSeconds = EditorGUILayout.FloatField(_pattern.minInputIntervalSeconds, GUILayout.Width(56));
            }
            _pattern.music = (AudioClip)EditorGUILayout.ObjectField(
                "음악", _pattern.music, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_pattern);

            int total = _pattern.calls?.Count ?? 0;
            int taps  = 0, holds = 0, calls = 0, responses = 0;
            if (_pattern.calls != null)
                foreach (var c in _pattern.calls)
                {
                    if (c.type == CallType.Tap) taps++; else holds++;
                    if (c.role == CallRole.Call) calls++; else responses++;
                }

            float dur = _pattern.music != null ? _pattern.music.length : 0f;
            string durStr = dur > 0
                ? $"{Mathf.FloorToInt(dur / 60f):0}:{dur % 60f:00.#}"
                : "–";
            EditorGUILayout.LabelField(
                $"전체 노트: {total}개  (Tap {taps} / Hold {holds})   Call {calls} / Response {responses}   재생 시간: {durStr}   " +
                $"사용 중인 레인: {SUBROWS}   선택됨: {_selection.Count}개   Phase: {_pattern.phases?.Count ?? 0}개   " +
                $"SFX: {_pattern.sfxCues?.Count ?? 0}개",
                EditorStyles.miniLabel);
        }
    }

    // ── Toolbar ────────────────────────────────────────────────────────────
    void DrawToolbar()
    {
        // NOTE: the toolbar style goes on each ROW's HorizontalScope, not on an outer
        // VerticalScope — EditorStyles.toolbar pins a fixed height and doesn't stretch, so a
        // multi-row VerticalScope with that style under-reserves its layout height and the
        // lower rows spill down over the timeline's top-left label cell ("SFX"/"PHASE").
        using (new EditorGUILayout.VerticalScope())
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool canPlay = _pattern?.music != null;
                GUI.enabled = canPlay;

                if (_isRecording)
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("■  녹음 중지", EditorStyles.toolbarButton, GUILayout.Width(96)))
                        StopSession();
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("●  녹음", EditorStyles.toolbarButton, GUILayout.Width(56)))
                        StartSession(record: true, startBeat: -1f);
                    if (GUILayout.Button("● 여기서 ▸", EditorStyles.toolbarButton, GUILayout.Width(70)))
                        StartSession(record: true, startBeat: _scrubBeat);
                    GUI.backgroundColor = Color.white;

                    if (_isPlaying)
                    {
                        if (GUILayout.Button("■  정지", EditorStyles.toolbarButton, GUILayout.Width(68)))
                            StopSession();
                    }
                    else
                    {
                        if (GUILayout.Button("▶  미리듣기", EditorStyles.toolbarButton, GUILayout.Width(80)))
                            StartSession(record: false, startBeat: -1f);
                        if (GUILayout.Button("▶ 여기서 ▸", EditorStyles.toolbarButton, GUILayout.Width(76)))
                            StartSession(record: false, startBeat: _scrubBeat);
                    }
                }

                GUI.enabled = _pattern != null;
                GUILayout.Space(6);
                EditorGUILayout.LabelField(
                    new GUIContent("탐색 박자", "타임라인 눈금자를 클릭하면 여기에 설정됨 — '여기서 ▸' 버튼이 처음부터가 아니라 이 지점부터 재생/녹음을 시작함."),
                    EditorStyles.miniLabel, GUILayout.Width(62));
                _scrubBeat = Mathf.Max(0f, EditorGUILayout.FloatField(_scrubBeat, GUILayout.Width(44)));

                GUILayout.Space(10);
                EditorGUILayout.LabelField(
                    new GUIContent("존버", "'광대 Tap → 개미 Tap → 플레이어 Hold → 다같이 재잠수 Tap' 4노트를 탐색 박자를 " +
                        "시작점 삼아 한 번에 찍음. 그룹 #은 지금 Group #을 쓰고 찍은 뒤 Z처럼 자동 1 증가, " +
                        "탐색 박자도 재잠수 시점으로 이동해서 바로 이어 찍기 편함. 1-5 리믹스 같은 다른 " +
                        "CallPatternData에서도 그대로 쓸 수 있음(이 채보 전용 기능 아님)."),
                    EditorStyles.miniBoldLabel, GUILayout.Width(30));
                EditorGUILayout.LabelField("광대+", EditorStyles.miniLabel, GUILayout.Width(28));
                _surviveClownOffset = EditorGUILayout.FloatField(_surviveClownOffset, GUILayout.Width(26));
                EditorGUILayout.LabelField("개미+", EditorStyles.miniLabel, GUILayout.Width(28));
                _surviveAntOffset = EditorGUILayout.FloatField(_surviveAntOffset, GUILayout.Width(26));
                EditorGUILayout.LabelField("릴리즈+", EditorStyles.miniLabel, GUILayout.Width(34));
                _surviveReleaseOffset = EditorGUILayout.FloatField(_surviveReleaseOffset, GUILayout.Width(26));
                EditorGUILayout.LabelField("재잠수+", EditorStyles.miniLabel, GUILayout.Width(34));
                _surviveResubmergeOffset = EditorGUILayout.FloatField(_surviveResubmergeOffset, GUILayout.Width(26));
                if (GUILayout.Button("찍기", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    PlaceSurviveCycle();

                GUILayout.Space(10);
                if (GUILayout.Button("전체 삭제", EditorStyles.toolbarButton, GUILayout.Width(68)))
                {
                    if (EditorUtility.DisplayDialog("채보 전체 삭제",
                        $"노트 {_pattern.calls?.Count ?? 0}개를 전부 삭제할까요?", "삭제", "취소"))
                    {
                        Undo.RecordObject(_pattern, "Clear Pattern");
                        _pattern.calls?.Clear();
                        ClearSelection();
                        _currentGroupId = 0;
                        EditorUtility.SetDirty(_pattern);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(
                    new GUIContent("Hold≥", "이 길이(초) 이상 누르고 있으면 REC 중 자동으로 Hold 노트로 기록됨 (짧으면 Tap)."),
                    EditorStyles.miniLabel, GUILayout.Width(36));
                _holdThresholdSec = EditorGUILayout.FloatField(_holdThresholdSec, GUILayout.Width(36));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("확대", EditorStyles.miniLabel, GUILayout.Width(36));
                _zoom = GUILayout.HorizontalSlider(_zoom, 30f, 500f, GUILayout.Width(90));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("스냅", "노트를 찍거나 옮길 때 박자를 이 단위로 맞춤."),
                    EditorStyles.miniLabel, GUILayout.Width(32));
                _snapIndex = EditorGUILayout.Popup(_snapIndex, SnapLabels, EditorStyles.toolbarPopup, GUILayout.Width(50));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("에코 간격", "음악이 재생 중이 아닐 때 Z를 눌렀을 때만 쓰이는 대체 간격."),
                    EditorStyles.miniLabel, GUILayout.Width(56));
                _echoGapBeats = EditorGUILayout.FloatField(_echoGapBeats, GUILayout.Width(36));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("그룹 #", "새로 찍는 노트에 붙는 그룹 번호. Z를 누르면 1 증가함."),
                    EditorStyles.miniLabel, GUILayout.Width(48));
                _currentGroupId = Mathf.Max(0, EditorGUILayout.IntField(_currentGroupId, GUILayout.Width(30)));
                GUILayout.Space(8);

                string timeStr = (_isRecording || _isPlaying) && _pattern != null
                    ? $"{Mathf.Max(0f, _currentSongTime) / 60f:0}:{Mathf.Max(0f, _currentSongTime) % 60f:00.0}  " +
                      $"박자 {Mathf.Max(0f, _pattern.TimeToBeat(_currentSongTime)):F1}"
                    : "--:--.--";
                EditorGUILayout.LabelField(timeStr, EditorStyles.miniLabel, GUILayout.Width(140));
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.enabled = _selection.Count > 0 && _pattern != null;
                EditorGUILayout.LabelField(
                    new GUIContent("복제 간격(+박자)", "복제본을 원본에서 몇 박자 뒤에 붙여넣을지."),
                    EditorStyles.miniLabel, GUILayout.Width(92));
                _duplicateOffsetBeats = EditorGUILayout.FloatField(_duplicateOffsetBeats, GUILayout.Width(44));
                if (GUILayout.Button("선택 항목 복제", EditorStyles.toolbarButton, GUILayout.Width(120)))
                    DuplicateSelection(_duplicateOffsetBeats);
                GUI.enabled = true;

                GUILayout.Space(12);
                if (_primaryEntry != null && _selection.Contains(_primaryEntry))
                {
                    EditorGUILayout.LabelField("큐 박자", EditorStyles.miniLabel, GUILayout.Width(48));
                    float newCue = EditorGUILayout.FloatField(_primaryEntry.cueBeat, GUILayout.Width(52));
                    if (!Mathf.Approximately(newCue, _primaryEntry.cueBeat))
                    {
                        Undo.RecordObject(_pattern, "Edit Cue Beat");
                        _primaryEntry.cueBeat = Mathf.Max(0f, newCue);
                        SortAndDirty();
                    }
                    if (_primaryEntry.type == CallType.Hold)
                    {
                        EditorGUILayout.LabelField("릴리즈 박자", EditorStyles.miniLabel, GUILayout.Width(70));
                        float newRel = EditorGUILayout.FloatField(_primaryEntry.releaseBeat, GUILayout.Width(52));
                        if (!Mathf.Approximately(newRel, _primaryEntry.releaseBeat))
                        {
                            Undo.RecordObject(_pattern, "Edit Release Beat");
                            _primaryEntry.releaseBeat = Mathf.Max(_primaryEntry.cueBeat + MIN_HOLD_BEATS, newRel);
                            SortAndDirty();
                        }
                    }
                }
            }

            // Own dedicated row (not sharing a line with Duplicate/cue-beat) so it can never get
            // pushed off the right edge of the window and silently disappear regardless of how
            // narrow the window is — that's what was happening when this lived at the end of the
            // row above. Only "new phase" default lives here now; editing an EXISTING phase's style
            // is done via an inline field drawn directly above that phase in the PHASE lane itself
            // (DrawPhases) — same reasoning, doesn't compete for toolbar space at all.
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField(
                    new GUIContent("새 Phase 스타일", "아래 PHASE 레인의 빈 공간을 클릭해 새 phase를 만들 때 찍히는 스타일 태그."),
                    EditorStyles.miniLabel, GUILayout.Width(90));
                _newPhaseStyleId = EditorGUILayout.TextField(_newPhaseStyleId, GUILayout.Width(70));
                GUILayout.Space(16);
                EditorGUILayout.LabelField(
                    new GUIContent("새 SFX 클립", "맨 아래 SFX 레인에 새 큐를 찍을 때 자동 배정되는 클립. " +
                        "큐마다 개별 변경 가능(큐 클릭 → 바로 위 칸). 비워두면 클립 없이 찍힘."),
                    EditorStyles.miniLabel, GUILayout.Width(72));
                EditorGUI.BeginChangeCheck();
                var ndc = (AudioClip)EditorGUILayout.ObjectField(_pattern.sfxCueDefaultClip, typeof(AudioClip), false, GUILayout.Width(130));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_pattern, "Set New Sfx Cue Clip");
                    _pattern.sfxCueDefaultClip = ndc;
                    EditorUtility.SetDirty(_pattern);
                }
                GUILayout.Space(12);
                EditorGUILayout.LabelField(
                    "기존 phase/SFX 큐 수정: 아래 레인에서 해당 항목 클릭 — 바로 위에 수정 칸이 나타남.",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            if (_isRecording)
            {
                GUI.color = new Color(1f, 0.9f, 0.2f);
                EditorGUILayout.LabelField(
                    _isPressed
                        ? "● 녹음 중  —  누르고 있는 중... 떼면 기록됨 (짧게 = Tap, 길게 = Hold)   Esc = 중지"
                        : "● 녹음 중  —  Space 또는 마우스 왼쪽 클릭 = 누르고 있으면 콜 기록   Esc = 중지",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (!_isPlaying)
            {
                EditorGUILayout.LabelField(
                    "빈 공간 클릭 = 노트 추가.   눈금자 클릭 = 탐색 박자 설정.   Shift+드래그(빈 공간) = 박스 선택.   " +
                    "Shift+클릭(노트) = 선택 추가/제거.   더블클릭 = 그룹 전체 선택.   " +
                    "Tap/Hold 몸통 드래그 = 선택된 것 전체를 함께 이동(리사이즈 핸들은 그 노트만).   " +
                    "Ctrl+C = 복사, Ctrl+V = 탐색 박자 위치에 붙여넣기(새 그룹 #).   Ctrl+A = 전체 선택.   Del = 선택 삭제.   Esc = 선택 해제.   " +
                    $"Z = 그룹 #{_currentGroupId}의 Call을 Response로 복사, 이후 #{_currentGroupId + 1}로 이동.   " +
                    "PHASE 레인(1-5같이 여러 스테이지가 섞인 곡용): 빈 공간 클릭 = " +
                    "'새 Phase 스타일' 태그로 phase 추가, 가장자리 드래그 = 크기 조절, 몸통 드래그 = 이동, 우클릭 = 삭제.   " +
                    "SFX 레인(맨 아래, 판정과 무관한 순수 효과음): 빈 공간 클릭 = '새 SFX 클립'으로 큐 추가, " +
                    "큐 클릭 = 선택(바로 위 칸에서 클립 지정/▶ 미리듣기), 드래그 = 이동, 우클릭 = 삭제, Del = 선택 삭제.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    // ── Timeline ───────────────────────────────────────────────────────────
    void DrawTimeline()
    {
        if (_pattern == null) return;

        // Band count must be known BEFORE laying out rects (labels/content height all depend on
        // it) — recomputing here, first thing, every repaint, is what keeps this correct even as
        // notes are added/moved/deleted; there is no cross-frame cache to go stale.
        RecomputeGroupBands();

        Rect full = GUILayoutUtility.GetRect(
            GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (full.height < 20f) return;

        float totalAudio = Mathf.Max(10f, _pattern.music != null ? _pattern.music.length : 60f);
        // totalAudio is raw content time (the clip's own file duration, content-time 0 = file
        // sample 0) — same kind of quantity as _currentSongTime, so no manual "+ introDelay" here
        // either now that introDelay is baked into BeatToTime/TimeToBeat rather than applied
        // per-call-site (the "intro" shaded region drawn at the left, see below, is the FIRST
        // introDelay seconds of this same span, not extra width tacked on before it).
        float contentW   = totalAudio * _zoom + 120f;
        // Strip order top-to-bottom: ruler, SFX strip, Tap/Hold note lanes, PHASE strip. The note
        // lanes start at NOTE_TOP (= RULER_H + SFX_LANE_H); the PHASE strip stays pinned last.
        float noteLanesH = LANE_H * TOTAL_ROWS;
        float contentH   = RULER_H + SFX_LANE_H + noteLanesH + PHASE_LANE_H;

        Rect labelsRect = new Rect(full.x, full.y + NOTE_TOP, LABEL_W, noteLanesH);
        DrawLaneLabels(labelsRect);
        var stripLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        Rect phaseLabelRect = new Rect(full.x, full.y + NOTE_TOP + noteLanesH, LABEL_W, PHASE_LANE_H);
        GUI.Label(phaseLabelRect, "PHASE", stripLabelStyle);
        Rect sfxLabelRect = new Rect(full.x, full.y + RULER_H, LABEL_W, SFX_LANE_H);
        GUI.Label(sfxLabelRect, "SFX", stripLabelStyle);

        Rect scrollRect  = new Rect(full.x + LABEL_W, full.y, full.width - LABEL_W, full.height);
        Rect contentRect = new Rect(0f, 0f, contentW, contentH);

        _scrollPos = GUI.BeginScrollView(scrollRect, _scrollPos, contentRect);

        EditorGUI.DrawRect(new Rect(0, 0, contentW, contentH), new Color(0.12f, 0.12f, 0.12f));
        DrawRulerAndGrid(contentW, totalAudio, contentH);
        DrawLaneBackgrounds(contentW);
        DrawCallResponseGroups(contentH);
        DrawCalls();
        DrawPhases(contentW, noteLanesH);
        DrawSfxCues(contentW, noteLanesH);
        if (_isMarqueeSelecting) DrawMarquee();
        if (_isRecording && _isPressed) DrawRecordingPreview();
        DrawScrubMarker(contentH);
        DrawPlayhead(contentH);

        if (!_isRecording) HandleTimelineMouseInput(contentRect, noteLanesH);

        GUI.EndScrollView();
    }

    void DrawLaneLabels(Rect rect)
    {
        for (int row = 0; row < TOTAL_ROWS; row++)
        {
            int   section = SectionOfRow(row, SUBROWS);
            Rect  r = new Rect(rect.x, rect.y + row * LANE_H, rect.width, LANE_H);
            Color c = LaneColors[section];
            EditorGUI.DrawRect(r, new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f));
            GUI.Label(r, LaneLabels[section], new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = c }
            });
        }
    }

    void DrawRulerAndGrid(float totalW, float totalAudio, float contentH)
    {
        EditorGUI.DrawRect(new Rect(0, 0, totalW, RULER_H), new Color(0.08f, 0.08f, 0.08f));

        float introW = _pattern.introDelay * _zoom;
        EditorGUI.DrawRect(new Rect(0, 0, introW, contentH), new Color(0f, 0f, 0f, 0.35f));
        if (introW > 20f)
            GUI.Label(new Rect(4f, 2f, 50f, RULER_H - 4f), "intro",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } });

        float liveX = _pattern.BeatToTime(_pattern.liveInputStartBeat) * _zoom; // introDelay is already baked into BeatToTime
        if (liveX > introW && liveX < totalW)
        {
            EditorGUI.DrawRect(new Rect(liveX, 0f, 2f, contentH), new Color(0.6f, 1f, 0.6f, 0.6f));
            GUI.Label(new Rect(liveX + 3f, 2f, 40f, RULER_H - 4f), "LIVE",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 1f, 0.6f) } });
        }

        if (_pattern.bpm <= 0f) return;

        float beatInterval = _pattern.BeatInterval;
        int   totalBeats   = Mathf.CeilToInt(totalAudio / beatInterval) + 2;

        for (int i = 0; i <= totalBeats; i++)
        {
            float x = (_pattern.introDelay + i * beatInterval) * _zoom;
            if (x > totalW) break;

            bool isMeasure = (i % 4 == 0);
            EditorGUI.DrawRect(new Rect(x, NOTE_TOP, 1f, LANE_H * TOTAL_ROWS),
                new Color(0.6f, 0.6f, 0.6f, isMeasure ? 0.55f : 0.20f));

            if (isMeasure)
            {
                EditorGUI.DrawRect(new Rect(x, 0f, 1f, RULER_H), new Color(0.65f, 0.65f, 0.65f));
                GUI.Label(new Rect(x + 3f, 2f, 44f, RULER_H - 4f), $"M{i / 4 + 1}",
                    new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } });
            }
            else
            {
                EditorGUI.DrawRect(new Rect(x, RULER_H - 6f, 1f, 6f), new Color(0.40f, 0.40f, 0.40f));
            }
        }
    }

    void DrawLaneBackgrounds(float totalW)
    {
        for (int row = 0; row < TOTAL_ROWS; row++)
        {
            int   section = SectionOfRow(row, SUBROWS);
            float y  = NOTE_TOP + row * LANE_H;
            Color bg = section % 2 == 0 ? new Color(0.155f, 0.155f, 0.155f) : new Color(0.130f, 0.130f, 0.130f);
            EditorGUI.DrawRect(new Rect(0f, y, totalW, LANE_H), bg);

            if (row < TOTAL_ROWS - 1)
                EditorGUI.DrawRect(new Rect(0f, y + LANE_H, totalW, 1f), new Color(0f, 0f, 0f, 0.35f));
        }
    }

    // ── Dynamic lane allocation (see class doc point 1) ────────────────────
    readonly Dictionary<CallEntry, int> _tapRowCache  = new Dictionary<CallEntry, int>();
    readonly Dictionary<CallEntry, int> _holdRowCache = new Dictionary<CallEntry, int>();
    readonly Dictionary<int, int> _groupBandCache = new Dictionary<int, int>();

    static float GroupEnd(CallEntry c) => c.type == CallType.Hold ? c.releaseBeat : c.cueBeat;

    List<(int groupId, float start, float end, List<CallEntry> resps)> BuildGroupSpans()
    {
        var spans = new List<(int, float, float, List<CallEntry>)>();
        if (_pattern.calls == null) return spans;

        foreach (var grp in _pattern.calls.Where(c => c.groupId >= 0).GroupBy(c => c.groupId))
        {
            var callEntries = grp.Where(c => c.role == CallRole.Call).ToList();
            var respEntries = grp.Where(c => c.role == CallRole.Response).ToList();
            if (callEntries.Count == 0 && respEntries.Count == 0) continue;

            float start = callEntries.Count > 0 ? callEntries.Min(c => c.cueBeat) : respEntries.Min(c => c.cueBeat);
            float end   = respEntries.Count > 0 ? respEntries.Max(GroupEnd)
                        : callEntries.Count > 0 ? callEntries.Max(GroupEnd) : start;
            spans.Add((grp.Key, start, end, respEntries));
        }
        return spans;
    }

    /// Greedy interval-scheduling lane allocation ("minimum meeting rooms"): sort groups by start
    /// beat; for each, reuse the lowest-numbered lane whose current occupant already ended by this
    /// group's start, or open a new lane if none qualify. Fully recomputed every call — no state
    /// carried between repaints, so a stale cache can't cause the "notes only ever land on the same
    /// lane" bug this replaces.
    void RecomputeGroupBands()
    {
        _groupBandCache.Clear();

        var ordered = BuildGroupSpans().OrderBy(s => s.start).ToList();
        var laneEndBeat = new List<float>();

        foreach (var s in ordered)
        {
            int assignedLane = -1;
            for (int i = 0; i < laneEndBeat.Count; i++)
            {
                if (laneEndBeat[i] <= s.start) { assignedLane = i; break; }
            }
            if (assignedLane == -1)
            {
                assignedLane = laneEndBeat.Count;
                laneEndBeat.Add(s.end);
            }
            else
            {
                laneEndBeat[assignedLane] = s.end;
            }
            _groupBandCache[s.groupId] = assignedLane;
        }

        SUBROWS = Mathf.Max(1, laneEndBeat.Count);
    }

    void DrawCalls()
    {
        _tapRowCache.Clear();
        _holdRowCache.Clear();
        _entryScreenRects.Clear();
        if (_pattern.calls == null) return;

        foreach (var entry in _pattern.calls)
        {
            int band = entry.groupId >= 0 && _groupBandCache.TryGetValue(entry.groupId, out int b) ? b : 0;
            if (entry.type == CallType.Tap) { _tapRowCache[entry] = band; DrawTapNote(entry, band); }
            else                            { _holdRowCache[entry] = band; DrawHoldBar(entry, band); }
        }
    }

    float BeatX(float beat) => _pattern.BeatToTime(beat) * _zoom; // introDelay is already baked into BeatToTime

    static (float y, float h) FixedSubRowRect(int sectionIndex, int subRow)
    {
        float rowTop = NOTE_TOP + RowOf(sectionIndex, subRow) * LANE_H;
        return (rowTop + 6f, LANE_H - 12f);
    }

    void DrawTapNote(CallEntry entry, int subRow)
    {
        bool  selected = _selection.Contains(entry);
        bool  dragging = selected && _isDragging && _dragOrigins.ContainsKey(entry);
        float beat     = dragging ? _dragOrigins[entry].x + _dragBeatDelta : entry.cueBeat;

        var (y, h) = FixedSubRowRect(0, subRow);
        float x = BeatX(beat) - NOTE_W * 0.5f;
        Color col = LaneColors[0];
        if (entry.role == CallRole.Call) col.a = 0.4f;

        if (selected)
        {
            Color outline = dragging ? new Color(1f, 0.9f, 0.1f) : Color.white;
            EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, NOTE_W + 4f, h + 4f), outline);
        }
        EditorGUI.DrawRect(new Rect(x, y, NOTE_W, h), col);
        EditorGUI.DrawRect(new Rect(x + 2f, y + 2f, NOTE_W - 4f, h * 0.4f),
            new Color(1f, 1f, 1f, entry.role == CallRole.Call ? 0.2f : 0.35f));

        if (dragging)
            GUI.Label(new Rect(x - 12f, y - 16f, 50f, 14f), $"B {beat:F2}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });

        _entryScreenRects[entry] = new Rect(x, y, NOTE_W, h);

        if (!_isDragging)
            EditorGUIUtility.AddCursorRect(new Rect(x - 6f, y, NOTE_W + 12f, h), MouseCursor.MoveArrow);
    }

    void DrawHoldBar(CallEntry entry, int subRow)
    {
        bool selected = _selection.Contains(entry);
        float cueBeat     = entry.cueBeat;
        float releaseBeat = entry.releaseBeat;

        if (selected && _isDragging && _dragOrigins.TryGetValue(entry, out Vector2 origin))
        {
            if (entry == _dragAnchor && _dragHandle == DragHandle.HoldStart)
                cueBeat = Mathf.Min(origin.x + _dragBeatDelta, origin.y - MIN_HOLD_BEATS);
            else if (entry == _dragAnchor && _dragHandle == DragHandle.HoldEnd)
                releaseBeat = Mathf.Max(origin.y + _dragBeatDelta, origin.x + MIN_HOLD_BEATS);
            else // whole-selection move (Tap/HoldBody grab)
            {
                cueBeat     = origin.x + _dragBeatDelta;
                releaseBeat = origin.y + _dragBeatDelta;
            }
        }

        var (y, h) = FixedSubRowRect(1, subRow);
        float x0 = BeatX(cueBeat);
        float x1 = BeatX(releaseBeat);
        Color col = LaneColors[1];
        if (entry.role == CallRole.Call) col.a = 0.4f;

        if (selected)
        {
            Color outline = _isDragging ? new Color(1f, 0.9f, 0.1f) : Color.white;
            EditorGUI.DrawRect(new Rect(x0 - 2f, y - 2f, (x1 - x0) + 4f, h + 4f), outline);
        }
        EditorGUI.DrawRect(new Rect(x0, y, Mathf.Max(2f, x1 - x0), h), col);
        EditorGUI.DrawRect(new Rect(x0 - 2f, y, 4f, h), new Color(1f, 1f, 1f, 0.8f));
        EditorGUI.DrawRect(new Rect(x1 - 2f, y, 4f, h), new Color(1f, 1f, 1f, 0.8f));

        if (selected && _isDragging)
            GUI.Label(new Rect(x0, y - 16f, 100f, 14f), $"B {cueBeat:F2} → {releaseBeat:F2}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });

        _entryScreenRects[entry] = new Rect(x0, y, Mathf.Max(2f, x1 - x0), h);

        if (!_isDragging)
        {
            EditorGUIUtility.AddCursorRect(new Rect(x0 - 6f, y, 12f, h), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(x1 - 6f, y, 12f, h), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(x0 + 6f, y, Mathf.Max(0f, (x1 - x0) - 12f), h), MouseCursor.MoveArrow);
        }
    }

    void DrawCallResponseGroups(float contentH)
    {
        var spans = BuildGroupSpans();
        if (spans.Count == 0) return;

        var ordered = spans.OrderBy(s => s.start).ToList();
        // Bands must line up with the note rows only — divide the note-lane height (not everything
        // below the ruler, which would fold in the SFX + PHASE strips) by the sub-row count.
        float bandH = (LANE_H * TOTAL_ROWS) / Mathf.Max(1, SUBROWS);

        int colorIndex = 0;
        foreach (var s in ordered)
        {
            float bandY = NOTE_TOP + (_groupBandCache.TryGetValue(s.groupId, out int bnd) ? bnd : 0) * bandH;

            float x0 = BeatX(s.start);
            float x1 = BeatX(s.end);
            EditorGUI.DrawRect(new Rect(x0, bandY, Mathf.Max(1f, x1 - x0), bandH),
                GroupBgColors[colorIndex % GroupBgColors.Length]);
            colorIndex++;

            if (s.resps.Count == 0) continue;

            float rStart = s.resps.Min(c => c.cueBeat);
            float rEnd   = s.resps.Max(GroupEnd);
            float rx0 = BeatX(rStart);
            float rx1 = BeatX(rEnd);
            EditorGUI.DrawRect(new Rect(rx0, bandY, Mathf.Max(1f, rx1 - rx0), bandH),
                new Color(0.1f, 0.55f, 0.35f, 0.18f));
            GUI.Label(new Rect(rx0 + 4f, bandY + bandH - 16f, 80f, 14f), "RESPONSE",
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.4f, 1f, 0.7f, 0.9f) }, fontStyle = FontStyle.Bold });

            Color lineCol = new Color(0.3f, 1f, 0.55f, 0.95f);
            EditorGUI.DrawRect(new Rect(rx0 - 1f, bandY, 2f, bandH), lineCol);
            GUI.Label(new Rect(rx0 + 3f, bandY + 2f, 28f, 14f), "Z",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = lineCol }, fontStyle = FontStyle.Bold });
        }
    }

    void DrawPhases(float totalW, float noteLanesH)
    {
        float y = NOTE_TOP + noteLanesH;
        EditorGUI.DrawRect(new Rect(0f, y, totalW, PHASE_LANE_H), new Color(0.09f, 0.09f, 0.11f));

        if (_pattern.phases == null) return;

        foreach (var p in _pattern.phases)
        {
            float start = p.startBeat, end = p.endBeat;
            if (p == _selectedPhase && _isPhaseDragging)
            {
                if (_phaseDragHandle == PhaseDragHandle.Start)
                    start = Mathf.Min(_phaseDragOriginStart + _phaseDragBeatDelta, _phaseDragOriginEnd - 0.1f);
                else if (_phaseDragHandle == PhaseDragHandle.End)
                    end = Mathf.Max(_phaseDragOriginEnd + _phaseDragBeatDelta, _phaseDragOriginStart + 0.1f);
                else
                {
                    start = _phaseDragOriginStart + _phaseDragBeatDelta;
                    end   = _phaseDragOriginEnd + _phaseDragBeatDelta;
                }
            }

            float x0 = BeatX(start);
            float x1 = BeatX(end);
            bool selected = p == _selectedPhase;
            Color col = PhaseColor(p.styleId);

            var rect = new Rect(x0, y + 2f, Mathf.Max(2f, x1 - x0), PHASE_LANE_H - 4f);
            EditorGUI.DrawRect(rect, col);
            if (selected) DrawRectOutline(rect, Color.white);

            string label = string.IsNullOrEmpty(p.styleId) ? "(스타일 없음)" : p.styleId;
            GUI.Label(new Rect(x0 + 4f, y + 3f, Mathf.Max(10f, x1 - x0 - 8f), PHASE_LANE_H - 6f), label,
                new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.black } });

            // Editable right on the phase itself, not in the toolbar — a toolbar row fighting for
            // horizontal space with everything else was going invisible off the right edge on
            // narrower/shorter windows. Drawn directly above the band it belongs to, so it's always
            // exactly where the user is already looking, regardless of window size.
            if (selected && !_isPhaseDragging)
            {
                var editRect = new Rect(x0, y - 20f, Mathf.Max(70f, x1 - x0), 18f);
                EditorGUI.DrawRect(new Rect(editRect.x - 2f, editRect.y - 2f, editRect.width + 4f, editRect.height + 4f),
                    new Color(0f, 0f, 0f, 0.75f));
                string newStyle = EditorGUI.TextField(editRect, p.styleId);
                if (newStyle != p.styleId)
                {
                    Undo.RecordObject(_pattern, "Edit Phase Style");
                    p.styleId = newStyle;
                    EditorUtility.SetDirty(_pattern);
                }
                // Not "Del/Backspace = delete" here on purpose: while this text field has focus,
                // Del/Backspace naturally edit its text (EditorGUIUtility.editingTextField gates
                // HandleGlobalKeys' shortcuts off while typing) — right-click is the one delete
                // gesture that's unambiguous regardless of focus state, so that's what's advertised.
                GUI.Label(new Rect(x0, y - 20f - 14f, 220f, 14f), $"[{p.startBeat:F1} – {p.endBeat:F1}]  (우클릭 = 삭제)",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
            }

            if (!_isPhaseDragging)
            {
                EditorGUIUtility.AddCursorRect(new Rect(x0 - 5f, y, 10f, PHASE_LANE_H), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(new Rect(x1 - 5f, y, 10f, PHASE_LANE_H), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(new Rect(x0 + 5f, y, Mathf.Max(0f, (x1 - x0) - 10f), PHASE_LANE_H), MouseCursor.MoveArrow);
            }
        }
    }

    // ── SFX cue lane ────────────────────────────────────────────────────────
    void DrawSfxCues(float totalW, float noteLanesH)
    {
        // SFX strip sits directly under the ruler, above the note lanes (noteLanesH unused now that
        // it's not stacked below them — kept in the signature to match DrawPhases).
        float y = RULER_H;
        EditorGUI.DrawRect(new Rect(0f, y, totalW, SFX_LANE_H), new Color(0.08f, 0.11f, 0.13f));

        if (_pattern.sfxCues == null) return;

        float noteY = y + 3f;
        float noteH = SFX_LANE_H - 6f;

        foreach (var c in _pattern.sfxCues)
        {
            float beat = (c == _selectedSfx && _isSfxDragging) ? _sfxDragOriginBeat + _sfxDragBeatDelta : c.beat;
            float x    = BeatX(beat) - NOTE_W * 0.5f;
            bool  selected = c == _selectedSfx;

            if (selected)
                DrawRectOutline(new Rect(x - 2f, noteY - 2f, NOTE_W + 4f, noteH + 4f),
                    _isSfxDragging ? new Color(1f, 0.9f, 0.1f) : Color.white);
            EditorGUI.DrawRect(new Rect(x, noteY, NOTE_W, noteH), SfxCueColor);
            EditorGUI.DrawRect(new Rect(x + 2f, noteY + 2f, NOTE_W - 4f, noteH * 0.4f), new Color(1f, 1f, 1f, 0.35f));

            if (_isSfxDragging && selected)
                GUI.Label(new Rect(x - 12f, y + SFX_LANE_H + 1f, 60f, 14f), $"B {beat:F2}",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });
            else if (_zoom > 80f)
                GUI.Label(new Rect(x + NOTE_W + 3f, noteY - 1f, 120f, noteH),
                    c.clip != null ? c.clip.name : "(none)",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } });

            // Clip picker inline just BELOW the selected cue (the SFX strip now sits directly under
            // the ruler, so there's no room above it — drawing below keeps it off the ruler while
            // still never fighting the toolbar for horizontal space).
            if (selected && !_isSfxDragging)
            {
                var editRect = new Rect(x - 60f, y + SFX_LANE_H + 18f, 150f, 18f);
                EditorGUI.DrawRect(new Rect(editRect.x - 2f, editRect.y - 2f, editRect.width + 4f + 24f, editRect.height + 4f),
                    new Color(0f, 0f, 0f, 0.8f));
                EditorGUI.BeginChangeCheck();
                var newClip = (AudioClip)EditorGUI.ObjectField(editRect, c.clip, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_pattern, "Change Sfx Cue Clip");
                    c.clip = newClip;
                    EditorUtility.SetDirty(_pattern);
                }
                if (GUI.Button(new Rect(editRect.xMax + 2f, editRect.y, 20f, 18f), "▶") && c.clip != null)
                    PlayAudio(c.clip, 0);
                GUI.Label(new Rect(editRect.x, editRect.y - 14f, 240f, 14f), $"beat {c.beat:F2}   (우클릭 = 삭제, Del = 삭제)",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
            }

            if (!_isSfxDragging)
                EditorGUIUtility.AddCursorRect(new Rect(x - 6f, noteY, NOTE_W + 12f, noteH), MouseCursor.MoveArrow);
        }
    }

    void DrawRecordingPreview()
    {
        float x0 = BeatX(_pressStartBeat);
        float x1 = _currentSongTime * _zoom; // introDelay already baked into BeatX's own formula; _currentSongTime is raw content time
        EditorGUI.DrawRect(new Rect(Mathf.Min(x0, x1), NOTE_TOP, Mathf.Max(2f, Mathf.Abs(x1 - x0)), LANE_H * TOTAL_ROWS),
            new Color(1f, 1f, 0.3f, 0.18f));
    }

    void DrawScrubMarker(float contentH)
    {
        if (_isPlaying || _isRecording) return; // playhead takes over while running
        float x = BeatX(_scrubBeat);
        EditorGUI.DrawRect(new Rect(x - 1f, 0f, 2f, contentH), new Color(0.4f, 0.75f, 1f, 0.75f));
        EditorGUI.DrawRect(new Rect(x - 4f, 0f, 8f, 4f), new Color(0.4f, 0.75f, 1f));
    }

    void DrawPlayhead(float contentH)
    {
        if (!_isRecording && !_isPlaying) return;
        float x = _currentSongTime * _zoom; // introDelay already baked into BeatX's own formula; _currentSongTime is raw content time
        EditorGUI.DrawRect(new Rect(x - 1f, 0f, 2f, contentH), new Color(1f, 0.92f, 0.15f, 0.90f));
        EditorGUI.DrawRect(new Rect(x - 4f, 0f, 8f, 4f), new Color(1f, 0.92f, 0.15f));
    }

    void DrawMarquee()
    {
        Rect r = MarqueeRect();
        EditorGUI.DrawRect(r, new Color(0.4f, 0.75f, 1f, 0.12f));
        DrawRectOutline(r, new Color(0.4f, 0.75f, 1f, 0.9f));
    }

    static void DrawRectOutline(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
    }

    Rect MarqueeRect()
    {
        float x0 = Mathf.Min(_marqueeStart.x, _marqueeCurrent.x);
        float y0 = Mathf.Min(_marqueeStart.y, _marqueeCurrent.y);
        float x1 = Mathf.Max(_marqueeStart.x, _marqueeCurrent.x);
        float y1 = Mathf.Max(_marqueeStart.y, _marqueeCurrent.y);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    // ── Timeline mouse interaction (edit mode only) ────────────────────────
    void HandleTimelineMouseInput(Rect contentRect, float noteLanesH)
    {
        Event e = Event.current;
        // Strip order: ruler [0, RULER_H) → SFX [RULER_H, NOTE_TOP) → note lanes → PHASE (bottom).
        float phaseLaneTop = NOTE_TOP + noteLanesH;

        if (e.type == EventType.MouseDown)
        {
            Vector2 mp = e.mousePosition;
            if (!contentRect.Contains(mp)) return;

            GUI.FocusControl(null);

            if (mp.y < RULER_H)
            {
                // Ruler click sets the scrub beat (only when idle — while playing/recording the
                // playhead already occupies this role).
                if (!_isPlaying && !_isRecording && e.button == 0)
                {
                    // introDelay is already baked into TimeToBeat — no manual subtraction here.
                    _scrubBeat = Mathf.Max(0f, SnapBeat(_pattern.TimeToBeat(mp.x / _zoom)));
                    e.Use();
                    Repaint();
                }
                return;
            }

            if (mp.y < NOTE_TOP)
            {
                // SFX strip — directly under the ruler (ruler case already returned above).
                HandleSfxLaneMouseDown(mp, e.button);
                e.Use();
                Repaint();
                return;
            }

            if (mp.y >= phaseLaneTop)
            {
                HandlePhaseLaneMouseDown(mp, e.button);
                e.Use();
                Repaint();
                return;
            }

            int row     = Mathf.FloorToInt((mp.y - NOTE_TOP) / LANE_H);
            int section = row >= 0 && row < TOTAL_ROWS ? SectionOfRow(row, SUBROWS) : -1;
            if (section < 0) return;

            // introDelay is already baked into TimeToBeat — no manual subtraction here.
            float clickBeat = SnapBeat(_pattern.TimeToBeat(mp.x / _zoom));

            if (e.button == 0)
            {
                var hitTap = FindTapNearPixel(mp, 12f);
                CallEntry hitEntry = hitTap;
                DragHandle hitHandle = DragHandle.TapNote;
                if (hitEntry == null)
                {
                    var (hitHold, handle) = FindHoldNearPixel(mp, 10f);
                    hitEntry = hitHold;
                    hitHandle = handle;
                }

                if (hitEntry != null)
                {
                    if (e.clickCount == 2)
                    {
                        SelectWholeGroup(hitEntry);
                        e.Use(); Repaint();
                        return;
                    }

                    if (e.shift)
                    {
                        ToggleSelection(hitEntry);
                        e.Use(); Repaint();
                        return;
                    }

                    if (!_selection.Contains(hitEntry))
                    {
                        _selection.Clear();
                        _selection.Add(hitEntry);
                    }
                    _primaryEntry = hitEntry;
                    BeginDrag(hitEntry, hitHandle, mp.x);
                }
                else if (e.shift)
                {
                    // Shift-drag on empty space = start a marquee box-select (additive to current
                    // selection on release — see class doc point 2).
                    _isMarqueeSelecting = true;
                    _marqueeStart = mp;
                    _marqueeCurrent = mp;
                }
                else if (section == 0)
                {
                    Undo.RecordObject(_pattern, "Add Tap");
                    var entry = new CallEntry { type = CallType.Tap, role = CallRole.Call, groupId = _currentGroupId, cueBeat = clickBeat };
                    AddCall(entry);
                    _selection.Clear();
                    _selection.Add(entry);
                    _primaryEntry = entry;
                }
                else
                {
                    float step = 1f / SnapDivs[_snapIndex];
                    Undo.RecordObject(_pattern, "Add Hold");
                    var entry = new CallEntry { type = CallType.Hold, role = CallRole.Call, groupId = _currentGroupId, cueBeat = clickBeat, releaseBeat = clickBeat + step };
                    AddCall(entry);
                    _selection.Clear();
                    _selection.Add(entry);
                    _primaryEntry = entry;
                    BeginDrag(entry, DragHandle.HoldEnd, mp.x);
                }
                e.Use();
            }
            else if (e.button == 1)
            {
                var hitTap = FindTapNearPixel(mp, 12f);
                CallEntry hitEntry = hitTap;
                if (hitEntry == null)
                {
                    var (hitHold, _) = FindHoldNearPixel(mp, 10f);
                    hitEntry = hitHold;
                }

                if (hitEntry != null) DeleteEntries(new[] { hitEntry });
                else                  ClearSelection(); // right-click empty space = deselect

                e.Use();
            }

            Repaint();
        }
        else if (e.type == EventType.MouseDrag)
        {
            if (_isMarqueeSelecting)
            {
                _marqueeCurrent = e.mousePosition;
                e.Use();
                Repaint();
            }
            else if (_isDragging && _dragAnchor != null)
            {
                float deltaPixels = e.mousePosition.x - _dragOriginMouseX;
                float deltaBeat   = deltaPixels / (_zoom * _pattern.BeatInterval);
                Vector2 anchorOrigin = _dragOrigins[_dragAnchor];
                float rawTarget = _dragHandle == DragHandle.HoldEnd ? anchorOrigin.y + deltaBeat : anchorOrigin.x + deltaBeat;
                float anchorRef = _dragHandle == DragHandle.HoldEnd ? anchorOrigin.y : anchorOrigin.x;
                _dragBeatDelta = SnapBeat(Mathf.Max(0f, rawTarget)) - anchorRef;
                e.Use();
                Repaint();
            }
            else if (_isPhaseDragging && _selectedPhase != null)
            {
                float deltaPixels = e.mousePosition.x - _phaseDragOriginMouseX;
                float deltaBeat   = deltaPixels / (_zoom * _pattern.BeatInterval);
                float rawTarget = _phaseDragHandle == PhaseDragHandle.End ? _phaseDragOriginEnd + deltaBeat : _phaseDragOriginStart + deltaBeat;
                float anchorRef = _phaseDragHandle == PhaseDragHandle.End ? _phaseDragOriginEnd : _phaseDragOriginStart;
                // No floor at 0 here — phases must be draggable into the intro-delay region (negative
                // beats), same as they can already be created there by clicking.
                _phaseDragBeatDelta = SnapBeat(rawTarget) - anchorRef;
                e.Use();
                Repaint();
            }
            else if (_isSfxDragging && _selectedSfx != null)
            {
                float deltaPixels = e.mousePosition.x - _sfxDragOriginMouseX;
                float deltaBeat   = deltaPixels / (_zoom * _pattern.BeatInterval);
                _sfxDragBeatDelta = SnapBeat(Mathf.Max(0f, _sfxDragOriginBeat + deltaBeat)) - _sfxDragOriginBeat;
                e.Use();
                Repaint();
            }
        }
        else if (e.type == EventType.MouseUp)
        {
            if (_isMarqueeSelecting)
            {
                CommitMarquee();
                e.Use();
                Repaint();
            }
            else if (_isDragging)
            {
                CommitDrag();
                e.Use();
                Repaint();
            }
            else if (_isPhaseDragging)
            {
                CommitPhaseDrag();
                e.Use();
                Repaint();
            }
            else if (_isSfxDragging)
            {
                CommitSfxDrag();
                e.Use();
                Repaint();
            }
        }
    }

    // ── Phase lane interaction ──────────────────────────────────────────────
    void HandlePhaseLaneMouseDown(Vector2 mp, int button)
    {
        if (button == 0)
        {
            var (hitPhase, handle) = FindPhaseNearPixel(mp);
            if (hitPhase != null)
            {
                _selectedPhase = hitPhase;
                BeginPhaseDrag(hitPhase, handle, mp.x);
            }
            else
            {
                // Create-by-dragging, same shape as a Hold note: place it at the click beat with a
                // default 4-beat length, then immediately start dragging its end handle to size it.
                // introDelay is already baked into TimeToBeat — no manual subtraction here.
                float clickBeat = SnapBeat(_pattern.TimeToBeat(mp.x / _zoom));
                Undo.RecordObject(_pattern, "Add Phase");
                var phase = new ChartPhase { styleId = _newPhaseStyleId, startBeat = clickBeat, endBeat = clickBeat + 4f };
                if (_pattern.phases == null) _pattern.phases = new List<ChartPhase>();
                _pattern.phases.Add(phase);
                EditorUtility.SetDirty(_pattern);
                _selectedPhase = phase;
                BeginPhaseDrag(phase, PhaseDragHandle.End, mp.x);
            }
        }
        else if (button == 1)
        {
            var (hitPhase, _) = FindPhaseNearPixel(mp);
            if (hitPhase != null)
            {
                Undo.RecordObject(_pattern, "Delete Phase");
                _pattern.phases.Remove(hitPhase);
                if (_selectedPhase == hitPhase) _selectedPhase = null;
                EditorUtility.SetDirty(_pattern);
            }
            else
            {
                _selectedPhase = null; // right-click empty phase lane = deselect
            }
        }
    }

    (ChartPhase phase, PhaseDragHandle handle) FindPhaseNearPixel(Vector2 mp)
    {
        if (_pattern.phases == null) return (null, PhaseDragHandle.None);
        const float tol = 8f;
        foreach (var p in _pattern.phases)
        {
            float x0 = BeatX(p.startBeat);
            float x1 = BeatX(p.endBeat);
            if (Mathf.Abs(mp.x - x0) < tol) return (p, PhaseDragHandle.Start);
            if (Mathf.Abs(mp.x - x1) < tol) return (p, PhaseDragHandle.End);
            if (mp.x > x0 && mp.x < x1)     return (p, PhaseDragHandle.Body);
        }
        return (null, PhaseDragHandle.None);
    }

    void BeginPhaseDrag(ChartPhase phase, PhaseDragHandle handle, float mouseX)
    {
        _phaseDragHandle       = handle;
        _isPhaseDragging       = true;
        _phaseDragOriginMouseX = mouseX;
        _phaseDragOriginStart  = phase.startBeat;
        _phaseDragOriginEnd    = phase.endBeat;
        _phaseDragBeatDelta    = 0f;
        GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
    }

    void CommitPhaseDrag()
    {
        if (_selectedPhase != null)
        {
            Undo.RecordObject(_pattern, "Move/Resize Phase");
            switch (_phaseDragHandle)
            {
                case PhaseDragHandle.Start:
                    // No floor at 0 — dragging a phase's start earlier than beat 0 (into the intro
                    // delay) must be allowed, not silently clamped back to M1.
                    _selectedPhase.startBeat = Mathf.Min(_phaseDragOriginStart + _phaseDragBeatDelta, _phaseDragOriginEnd - 0.1f);
                    break;
                case PhaseDragHandle.End:
                    _selectedPhase.endBeat = Mathf.Max(_phaseDragOriginEnd + _phaseDragBeatDelta, _phaseDragOriginStart + 0.1f);
                    break;
                case PhaseDragHandle.Body:
                    _selectedPhase.startBeat = _phaseDragOriginStart + _phaseDragBeatDelta;
                    _selectedPhase.endBeat   = _phaseDragOriginEnd + _phaseDragBeatDelta;
                    break;
            }
            EditorUtility.SetDirty(_pattern);
        }

        _isPhaseDragging    = false;
        _phaseDragHandle    = PhaseDragHandle.None;
        _phaseDragBeatDelta = 0f;
        GUIUtility.hotControl = 0;
    }

    // ── SFX cue lane interaction ────────────────────────────────────────────
    void HandleSfxLaneMouseDown(Vector2 mp, int button)
    {
        if (button == 0)
        {
            var hit = FindSfxNearPixel(mp, 12f);
            if (hit != null)
            {
                _selectedSfx = hit;
                BeginSfxDrag(hit, mp.x);
            }
            else
            {
                // introDelay is already baked into TimeToBeat — no manual subtraction.
                float clickBeat = SnapBeat(_pattern.TimeToBeat(mp.x / _zoom));
                Undo.RecordObject(_pattern, "Add Sfx Cue");
                var cue = new SfxCueEntry { line = 0, beat = clickBeat, clip = _pattern.sfxCueDefaultClip };
                if (_pattern.sfxCues == null) _pattern.sfxCues = new List<SfxCueEntry>();
                _pattern.sfxCues.Add(cue);
                _pattern.sfxCues.Sort((a, b) => a.beat.CompareTo(b.beat));
                EditorUtility.SetDirty(_pattern);
                _selectedSfx = cue;
                // No drag started — an SFX cue is a point (no length), like a Tap.
            }
        }
        else if (button == 1)
        {
            var hit = FindSfxNearPixel(mp, 12f);
            if (hit != null)
            {
                Undo.RecordObject(_pattern, "Delete Sfx Cue");
                _pattern.sfxCues.Remove(hit);
                if (_selectedSfx == hit) _selectedSfx = null;
                EditorUtility.SetDirty(_pattern);
            }
            else
            {
                _selectedSfx = null;
            }
        }
    }

    SfxCueEntry FindSfxNearPixel(Vector2 mp, float tol)
    {
        if (_pattern.sfxCues == null) return null;
        SfxCueEntry best = null;
        float bestDist = tol;
        foreach (var c in _pattern.sfxCues)
        {
            float d = Mathf.Abs(mp.x - BeatX(c.beat));
            if (d <= bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    void BeginSfxDrag(SfxCueEntry cue, float mouseX)
    {
        _isSfxDragging       = true;
        _sfxDragOriginMouseX = mouseX;
        _sfxDragOriginBeat   = cue.beat;
        _sfxDragBeatDelta    = 0f;
        GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
    }

    void CommitSfxDrag()
    {
        if (_selectedSfx != null)
        {
            Undo.RecordObject(_pattern, "Move Sfx Cue");
            _selectedSfx.beat = Mathf.Max(0f, _sfxDragOriginBeat + _sfxDragBeatDelta);
            _pattern.sfxCues.Sort((a, b) => a.beat.CompareTo(b.beat));
            EditorUtility.SetDirty(_pattern);
        }

        _isSfxDragging    = false;
        _sfxDragBeatDelta = 0f;
        GUIUtility.hotControl = 0;
    }

    void CommitMarquee()
    {
        Rect box = MarqueeRect();
        foreach (var kv in _entryScreenRects)
            if (box.Overlaps(kv.Value)) _selection.Add(kv.Key);
        _isMarqueeSelecting = false;
    }

    void BeginDrag(CallEntry anchor, DragHandle handle, float mouseX)
    {
        _dragAnchor       = anchor;
        _dragHandle       = handle;
        _isDragging       = true;
        _dragOriginMouseX = mouseX;
        _dragBeatDelta    = 0f;

        _dragOrigins.Clear();
        // Resizing a handle only ever affects the one note grabbed; moving (Tap or Hold body)
        // carries the whole current selection along together.
        bool wholeSelectionMoves = handle == DragHandle.TapNote || handle == DragHandle.HoldBody;
        if (wholeSelectionMoves)
            foreach (var s in _selection) _dragOrigins[s] = new Vector2(s.cueBeat, s.releaseBeat);
        else
            _dragOrigins[anchor] = new Vector2(anchor.cueBeat, anchor.releaseBeat);

        GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
    }

    void CommitDrag()
    {
        if (_dragOrigins.Count > 0)
        {
            Undo.RecordObject(_pattern, "Move/Resize Call");
            foreach (var kv in _dragOrigins)
            {
                CallEntry entry  = kv.Key;
                Vector2   origin = kv.Value;

                if (entry == _dragAnchor && _dragHandle == DragHandle.HoldStart)
                    entry.cueBeat = Mathf.Min(Mathf.Max(0f, origin.x + _dragBeatDelta), origin.y - MIN_HOLD_BEATS);
                else if (entry == _dragAnchor && _dragHandle == DragHandle.HoldEnd)
                    entry.releaseBeat = Mathf.Max(origin.y + _dragBeatDelta, origin.x + MIN_HOLD_BEATS);
                else
                {
                    entry.cueBeat = Mathf.Max(0f, origin.x + _dragBeatDelta);
                    if (entry.type == CallType.Hold)
                        entry.releaseBeat = Mathf.Max(entry.cueBeat + MIN_HOLD_BEATS, origin.y + _dragBeatDelta);
                }
            }
            SortAndDirty();
        }

        _isDragging    = false;
        _dragHandle    = DragHandle.None;
        _dragAnchor    = null;
        _dragBeatDelta = 0f;
        _dragOrigins.Clear();
        GUIUtility.hotControl = 0;
    }

    // ── Selection helpers ───────────────────────────────────────────────────
    void ClearSelection() { _selection.Clear(); _primaryEntry = null; }

    void ToggleSelection(CallEntry entry)
    {
        if (_selection.Contains(entry)) _selection.Remove(entry);
        else { _selection.Add(entry); _primaryEntry = entry; }
    }

    void SelectWholeGroup(CallEntry clicked)
    {
        _selection.Clear();
        if (clicked.groupId < 0) { _selection.Add(clicked); _primaryEntry = clicked; return; }
        foreach (var c in _pattern.calls)
            if (c.groupId == clicked.groupId) _selection.Add(c);
        _primaryEntry = clicked;
    }

    // ── Global key handler (edit mode) ─────────────────────────────────────
    void HandleGlobalKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (_pattern == null) return;
        if (EditorGUIUtility.editingTextField) return;

        if (e.keyCode == KeyCode.Z)
        {
            EchoCallGroupToResponse();
            e.Use(); Repaint();
            return;
        }

        if (e.control || e.command)
        {
            if (e.keyCode == KeyCode.C) { CopySelection(); e.Use(); return; }
            if (e.keyCode == KeyCode.V) { PasteClipboard(); e.Use(); Repaint(); return; }
            if (e.keyCode == KeyCode.A) { SelectAll(); e.Use(); Repaint(); return; }
        }

        if (e.keyCode == KeyCode.Escape) { ClearSelection(); _selectedSfx = null; e.Use(); Repaint(); return; }

        // SFX cue delete — separate from the CallEntry selection (an SFX cue is never in _selection).
        if (_selectedSfx != null && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
        {
            Undo.RecordObject(_pattern, "Delete Sfx Cue");
            _pattern.sfxCues?.Remove(_selectedSfx);
            _selectedSfx = null;
            EditorUtility.SetDirty(_pattern);
            e.Use(); Repaint();
            return;
        }

        if (_selection.Count == 0) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            DeleteEntries(_selection.ToList());
            e.Use(); Repaint();
        }
        else if (e.keyCode == KeyCode.C)
        {
            Undo.RecordObject(_pattern, "Toggle Call/Response");
            foreach (var s in _selection) s.role = s.role == CallRole.Call ? CallRole.Response : CallRole.Call;
            EditorUtility.SetDirty(_pattern);
            e.Use(); Repaint();
        }
    }

    void SelectAll()
    {
        _selection.Clear();
        if (_pattern.calls == null) return;
        foreach (var c in _pattern.calls) _selection.Add(c);
    }

    void CopySelection()
    {
        _clipboard.Clear();
        foreach (var s in _selection)
            _clipboard.Add(new CallEntry { type = s.type, role = s.role, groupId = s.groupId, cueBeat = s.cueBeat, releaseBeat = s.releaseBeat });
    }

    /// Pastes the clipboard so its earliest cueBeat lands on targetBeat, all under a fresh groupId
    /// (one new id for the whole paste, preserving relative offsets/shape of what was copied).
    void PasteAt(float targetBeat)
    {
        if (_clipboard.Count == 0 || _pattern == null) return;

        float anchor = _clipboard.Min(c => c.cueBeat);
        int newGroupId = NextFreeGroupId(_pattern);

        Undo.RecordObject(_pattern, "Paste Calls");
        var pasted = new List<CallEntry>();
        foreach (var c in _clipboard)
        {
            var e = new CallEntry
            {
                type    = c.type,
                role    = c.role,
                groupId = newGroupId,
                cueBeat = Mathf.Max(0f, targetBeat + (c.cueBeat - anchor)),
            };
            if (c.type == CallType.Hold)
                e.releaseBeat = e.cueBeat + (c.releaseBeat - c.cueBeat);
            pasted.Add(e);
        }

        if (_pattern.calls == null) _pattern.calls = new List<CallEntry>();
        _pattern.calls.AddRange(pasted);
        SortAndDirty();

        _selection.Clear();
        foreach (var p in pasted) _selection.Add(p);
        _primaryEntry = pasted[0];
        _currentGroupId = newGroupId + 1;
    }

    // Ctrl+V pastes at the scrub beat — simpler and more predictable than trying to infer whether
    // the mouse happens to be over the timeline at the moment the keyboard shortcut fires. Click
    // the ruler first to choose where, same control used for "preview/REC from here".
    void PasteClipboard() => PasteAt(SnapBeat(_scrubBeat));

    void DuplicateSelection(float offsetBeats)
    {
        if (_selection.Count == 0) return;
        float anchor = _selection.Min(s => s.cueBeat);
        CopySelection();
        PasteAt(SnapBeat(anchor + offsetBeats));
    }

    /// Z: mirrors the current group's Call-role entries into a matching Response group. Identical
    /// workflow/semantics to Stage1CallMapEditorWindow's EchoCallGroupToResponse.
    void EchoCallGroupToResponse()
    {
        if (_pattern.calls == null || _pattern.calls.Count == 0) return;

        var group = _pattern.calls
            .Where(c => c.role == CallRole.Call && c.groupId == _currentGroupId)
            .OrderBy(c => c.cueBeat)
            .ToList();

        if (group.Count == 0)
        {
            EditorUtility.DisplayDialog("복사할 콜이 없음",
                $"현재 그룹(#{_currentGroupId})에 Call 노트가 없습니다. Tap/Hold 노트를 먼저 하나 이상 찍고, " +
                "Z를 눌러 Response 그룹으로 복사하세요.", "확인");
            return;
        }

        float groupStart = group[0].cueBeat;
        float groupEnd   = group.Max(GroupEnd);

        float responseStart;
        if (_isRecording || _isPlaying)
        {
            float pressedBeat = SnapBeat(_pattern.TimeToBeat(_currentSongTime));
            responseStart = Mathf.Max(groupEnd, pressedBeat);
        }
        else
        {
            responseStart = groupEnd + _echoGapBeats;
        }

        Undo.RecordObject(_pattern, "Echo Call Group to Response");

        var newEntries = new List<CallEntry>();
        foreach (var c in group)
        {
            float delta = c.cueBeat - groupStart;
            var echoed = new CallEntry { type = c.type, role = CallRole.Response, groupId = _currentGroupId, cueBeat = responseStart + delta };
            if (c.type == CallType.Hold)
                echoed.releaseBeat = responseStart + (c.releaseBeat - groupStart);
            newEntries.Add(echoed);
        }

        _pattern.calls.AddRange(newEntries);
        SortAndDirty();

        _selection.Clear();
        foreach (var n in newEntries) _selection.Add(n);
        _primaryEntry = newEntries[0];

        float targetX = BeatX(responseStart) - (position.width - LABEL_W - 20f) * 0.25f;
        _scrollPos.x  = Mathf.Max(0f, targetX);

        Debug.Log($"[Chart Editor] Echoed group #{_currentGroupId}: {newEntries.Count} call(s) -> response starting at beat {responseStart:F2}.");
        _currentGroupId++;
    }

    static int NextFreeGroupId(CallPatternData p)
    {
        if (p?.calls == null || p.calls.Count == 0) return 0;
        int max = -1;
        foreach (var c in p.calls) max = Mathf.Max(max, c.groupId);
        return max + 1;
    }

    // ── Recording key/mouse capture ─────────────────────────────────────────
    void CaptureRecordingKeys()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopSession(); e.Use(); return; }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Z && !_isPressed && !EditorGUIUtility.editingTextField)
        {
            EchoCallGroupToResponse();
            e.Use();
            return;
        }

        if (_currentSongTime < 0f) return;

        bool isPressEvent   = (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) ||
                               (e.type == EventType.MouseDown && e.button == 0);
        bool isReleaseEvent = (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space) ||
                               (e.type == EventType.MouseUp && e.button == 0);

        if (isPressEvent && !_isPressed)
        {
            _isPressed          = true;
            _pressStartBeat     = _pattern.TimeToBeat(_currentSongTime);
            _pressStartRealTime = EditorApplication.timeSinceStartup;
            e.Use();
            return;
        }

        if (isReleaseEvent && _isPressed)
        {
            _isPressed = false;
            float  releaseBeat = SnapBeat(_pattern.TimeToBeat(_currentSongTime));
            float  pressBeat   = SnapBeat(_pressStartBeat);
            double durationSec = EditorApplication.timeSinceStartup - _pressStartRealTime;

            Undo.RecordObject(_pattern, "Record Call");
            CallEntry entry = durationSec < _holdThresholdSec
                ? new CallEntry { type = CallType.Tap, role = CallRole.Call, groupId = _currentGroupId, cueBeat = pressBeat }
                : new CallEntry { type = CallType.Hold, role = CallRole.Call, groupId = _currentGroupId, cueBeat = pressBeat, releaseBeat = Mathf.Max(releaseBeat, pressBeat + MIN_HOLD_BEATS) };
            AddCall(entry);
            e.Use();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    float SnapBeat(float rawBeat)
    {
        float grid = 1f / SnapDivs[_snapIndex];
        return Mathf.Round(rawBeat / grid) * grid;
    }

    void SortAndDirty()
    {
        _pattern.calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
        EditorUtility.SetDirty(_pattern);
    }

    void AddCall(CallEntry entry)
    {
        if (_pattern.calls == null) _pattern.calls = new List<CallEntry>();
        _pattern.calls.Add(entry);
        SortAndDirty();
    }

    // Places one Stage1SurviveVisuals-shaped cycle (Clown Tap, NormalAnt Tap, player Hold, resubmerge
    // Tap — see that script's class doc) starting at the scrub beat, using the 4 offset fields above.
    // Pure CallEntry data manipulation, no scene/stage dependency — works on whatever CallPatternData
    // is currently open, including a future 1-5 remix chart. Only saves the per-note beat arithmetic;
    // the resulting entries are ordinary notes you can still drag individually afterward, so different
    // cycles are free to use different pacing (the offset fields are just this click's starting point,
    // not a constraint baked into the data).
    void PlaceSurviveCycle()
    {
        float start = _scrubBeat;

        Undo.RecordObject(_pattern, "Add Survive Cycle");
        if (_pattern.calls == null) _pattern.calls = new List<CallEntry>();

        var clown = new CallEntry { type = CallType.Tap, role = CallRole.Call, groupId = _currentGroupId, cueBeat = start + _surviveClownOffset };
        var ant = new CallEntry { type = CallType.Tap, role = CallRole.Call, groupId = _currentGroupId, cueBeat = start + _surviveAntOffset };
        var player = new CallEntry { type = CallType.Hold, role = CallRole.Response, groupId = _currentGroupId, cueBeat = start, releaseBeat = start + _surviveReleaseOffset };
        var resubmerge = new CallEntry { type = CallType.Tap, role = CallRole.Call, groupId = _currentGroupId, cueBeat = start + _surviveResubmergeOffset };

        _pattern.calls.Add(clown);
        _pattern.calls.Add(ant);
        _pattern.calls.Add(player);
        _pattern.calls.Add(resubmerge);
        SortAndDirty();

        _selection.Clear();
        foreach (var n in new[] { clown, ant, player, resubmerge }) _selection.Add(n);
        _primaryEntry = player;

        Debug.Log($"[Chart Editor] 존버 사이클 #{_currentGroupId} 찍음 — 시작 {start:F2}, 릴리즈 {start + _surviveReleaseOffset:F2}, 재잠수 {start + _surviveResubmergeOffset:F2}.");

        _currentGroupId++;
        _scrubBeat = start + _surviveResubmergeOffset; // 바로 이어서 다음 사이클 찍기 편하게
    }

    void DeleteEntries(IList<CallEntry> entries)
    {
        if (_pattern.calls == null || entries.Count == 0) return;
        Undo.RecordObject(_pattern, entries.Count > 1 ? "Delete Calls" : "Delete Call");
        foreach (var e in entries)
        {
            _pattern.calls.Remove(e);
            _selection.Remove(e);
            if (_primaryEntry == e) _primaryEntry = null;
        }
        EditorUtility.SetDirty(_pattern);
    }

    CallEntry FindTapNearPixel(Vector2 mp, float tolerance)
    {
        if (_pattern.calls == null) return null;
        int row = Mathf.FloorToInt((mp.y - NOTE_TOP) / LANE_H);
        if (row < 0 || row >= TOTAL_ROWS || SectionOfRow(row, SUBROWS) != 0) return null;
        int clickedSubRow = SubRowOfRow(row, SUBROWS);

        float bestDx = tolerance;
        CallEntry best = null;
        foreach (var entry in _pattern.calls)
        {
            if (entry.type != CallType.Tap) continue;
            if (!_tapRowCache.TryGetValue(entry, out int er) || er != clickedSubRow) continue;
            float dx = Mathf.Abs(mp.x - BeatX(entry.cueBeat));
            if (dx < bestDx) { bestDx = dx; best = entry; }
        }
        return best;
    }

    (CallEntry entry, DragHandle handle) FindHoldNearPixel(Vector2 mp, float handleTolerance)
    {
        if (_pattern.calls == null) return (null, DragHandle.None);
        int row = Mathf.FloorToInt((mp.y - NOTE_TOP) / LANE_H);
        if (row < 0 || row >= TOTAL_ROWS || SectionOfRow(row, SUBROWS) != 1) return (null, DragHandle.None);
        int clickedSubRow = SubRowOfRow(row, SUBROWS);

        foreach (var entry in _pattern.calls)
        {
            if (entry.type != CallType.Hold) continue;
            if (!_holdRowCache.TryGetValue(entry, out int er) || er != clickedSubRow) continue;

            float x0 = BeatX(entry.cueBeat);
            float x1 = BeatX(entry.releaseBeat);

            if (Mathf.Abs(mp.x - x0) < handleTolerance) return (entry, DragHandle.HoldStart);
            if (Mathf.Abs(mp.x - x1) < handleTolerance) return (entry, DragHandle.HoldEnd);
            if (mp.x > x0 && mp.x < x1)                 return (entry, DragHandle.HoldBody);
        }
        return (null, DragHandle.None);
    }

    // ── Session control ─────────────────────────────────────────────────────
    /// startBeat < 0 means "start from the very beginning of the audio file" — chart-time starts at
    /// 0 and audio plays from sample 0 immediately, no artificial pre-roll wait (introDelay only
    /// affects BeatToTime/TimeToBeat's math, not when playback actually starts here). startBeat >= 0
    /// seeks both the chart-time bookkeeping and the actual audio playback to that beat.
    void StartSession(bool record, float startBeat)
    {
        if (_pattern?.music == null) return;
        _isRecording = record;
        _isPlaying   = !record;
        _sessionStart = EditorApplication.timeSinceStartup;

        if (startBeat < 0f)
        {
            // _currentSongTime starts at 0 the instant PlayAudio fires here — no artificial wait.
            // introDelay lives purely in BeatToTime/TimeToBeat's math (CallPatternData.cs), not in
            // this session clock's epoch, so REC capture (_pattern.TimeToBeat(_currentSongTime),
            // unwrapped, elsewhere in this file) round-trips correctly through BeatToTime without
            // needing real silence here to match anything at runtime.
            _sessionStartSongTime = 0f;
            _currentSongTime      = 0f;
            _isPressed = false;
            PlayAudio(_pattern.music, 0);
        }
        else
        {
            // chartTime is already true audio content time (introDelay baked into BeatToTime), and
            // the raw clip file has no silence of its own baked in — its sample 0 IS content time 0
            // (CallClock's real silent lead-in comes from delaying when PlayScheduled fires, not
            // from anything inside the clip) — so the seek sample is chartTime directly, no further
            // +introDelay.
            float chartTime = _pattern.BeatToTime(startBeat);
            int   startSample = Mathf.Max(0, Mathf.RoundToInt(chartTime * _pattern.music.frequency));
            _sessionStartSongTime = chartTime;
            _currentSongTime      = chartTime;
            _isPressed = false;
            PlayAudio(_pattern.music, startSample);
        }
    }

    void StopSession()
    {
        _isPressed = false;

        bool wasDirty = _isRecording || _isPlaying;
        _isRecording = false;
        _isPlaying   = false;
        StopAudio();
        if (wasDirty && _pattern != null)
        {
            EditorUtility.SetDirty(_pattern);
            AssetDatabase.SaveAssets();
        }
    }

    void CreateNewCallPattern()
    {
        const string folder = "Assets/CallPatterns";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "CallPatterns");

        string path = EditorUtility.SaveFilePanelInProject(
            "채보 데이터 만들기", "NewCallPattern", "asset",
            "새 채보 에셋을 저장할 위치를 선택하세요", folder);
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<CallPatternData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        StopSession();
        _pattern = asset;
        ClearSelection();
    }

    // ── Audio preview via reflection ────────────────────────────────────────
    static Type GetAudioUtil() =>
        typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    void PlayAudio(AudioClip clip, int startSample)
    {
        if (clip == null) return;
        StopAudio();
        try
        {
            Type util = GetAudioUtil();
            if (_playMethod == null)
            {
                _playMethod =
                    util.GetMethod("PlayPreviewClip",
                        BindingFlags.Static | BindingFlags.Public, null,
                        new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    ?? util.GetMethod("PlayClip",
                        BindingFlags.Static | BindingFlags.Public, null,
                        new[] { typeof(AudioClip) }, null);
            }
            if (_playMethod != null)
            {
                object[] args = _playMethod.GetParameters().Length == 3
                    ? new object[] { clip, startSample, false }
                    : new object[] { clip }; // very old Unity fallback with no seek support
                _playMethod.Invoke(null, args);
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[Stage1ChartEditor] Audio preview error: {ex.Message}"); }
    }

    void StopAudio()
    {
        try
        {
            Type util = GetAudioUtil();
            if (_stopMethod == null)
                _stopMethod = util.GetMethod("StopAllPreviewClips",
                    BindingFlags.Static | BindingFlags.Public);
            _stopMethod?.Invoke(null, null);
        }
        catch { }
    }
}
