using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Call-and-response chart editor for CallPatternData (Stage1 engine).
/// Unlike BeatMapEditorWindow, there is NO overlap-locking: calls may be placed at any beat
/// regardless of what other calls are already open there — this is intentional, not an omission.
/// REC mode: Space or mouse-left down/up records a Tap (short press) or Hold (long press).
/// Edit mode: Tap lane = click add / drag move / right-click delete.
///            Hold lane = drag to create a bar / drag either handle to resize / drag body to move.
///
/// Call/Response workflow: every hand-placed note defaults to CallRole.Call (the designer only
/// ever authors the "boss shows the rhythm" notes). Press Z once the call group is finished to
/// mirror its rhythm into an auto-generated CallRole.Response group (CallDirector judges Response,
/// never Call) — the matching response notes are never hand-placed. Select a note and press C to
/// flip its role manually for the rare exception (e.g. an extra Call dropped inside an already-
/// generated Response range for look-ahead, which needs to stay Call after the echo).
/// </summary>
public class Stage1CallMapEditorWindow : EditorWindow
{
    // ── State ──────────────────────────────────────────────────────────────
    CallPatternData _pattern;
    bool   _isRecording;
    bool   _isPlaying;
    double _sessionStart;
    float  _currentSongTime;

    // REC-mode press tracking (Space key or mouse-left, whichever starts first)
    bool   _isPressed;
    float  _pressStartBeat;
    double _pressStartRealTime;
    [SerializeField] float _holdThresholdSec = 0.15f;

    // ── Timeline view ──────────────────────────────────────────────────────
    Vector2 _scrollPos;
    float   _zoom = 120f;

    // ── Selection / drag (edit mode) ───────────────────────────────────────
    enum DragHandle { None, TapNote, HoldStart, HoldEnd, HoldBody }
    CallEntry   _selEntry;
    DragHandle  _dragHandle = DragHandle.None;
    bool   _isDragging;
    float  _dragOriginMouseX;
    float  _dragOriginCueBeat;
    float  _dragOriginReleaseBeat;
    float  _dragBeatDelta;

    // ── Snap ───────────────────────────────────────────────────────────────
    static readonly int[]    SnapDivs   = { 1, 2, 4, 8, 16 };
    static readonly string[] SnapLabels = { "1/1", "1/2", "1/4", "1/8", "1/16" };
    int _snapIndex = 2;

    // ── Echo (Z key) ────────────────────────────────────────────────────────
    // New Call notes are stamped with _currentGroupId as they're placed (regardless of where
    // they land in time — an overlapping look-ahead Call dropped inside an active Response zone
    // still belongs to "whatever group is currently being authored"). Z mirrors that group's Call
    // entries into a Response group (same groupId), then advances to the next id. This grouping
    // is authoring-order based, not time-order based, so it can't be corrupted by overlaps.
    int _currentGroupId;
    [SerializeField] float _echoGapBeats = 1f;

    // ── Layout constants ───────────────────────────────────────────────────
    const float LABEL_W    = 48f;
    const float RULER_H    = 22f;
    const float LANE_H     = 44f;
    const int   LANE_COUNT = 2;   // sections: Tap, Hold
    const int   SUBROWS    = 2;   // each section is always drawn as this many fixed full-height rows
    const int   TOTAL_ROWS = LANE_COUNT * SUBROWS;
    const int   GROUP_BANDS = 2;  // fixed bands the group background wash is split into when groups overlap in time
    const float NOTE_W     = 10f;
    const float MIN_HOLD_BEATS = 0.1f;

    static readonly string[] LaneLabels = { "TAP", "HOLD" };
    static readonly Color[]  LaneColors =
    {
        new Color(1.00f, 0.85f, 0.20f),   // Tap  – yellow
        new Color(0.30f, 0.70f, 1.00f),   // Hold – blue
    };

    // Alternating call+response group background tints (mirrors BeatMapEditorWindow's look)
    static readonly Color[] GroupBgColors =
    {
        new Color(0.20f, 0.40f, 0.60f, 0.12f),
        new Color(0.50f, 0.20f, 0.60f, 0.12f),
    };

    // ── Audio reflection ───────────────────────────────────────────────────
    static MethodInfo _playMethod;
    static MethodInfo _stopMethod;

    // ── Menu ───────────────────────────────────────────────────────────────
    [MenuItem("RhythmGame/Call Map Editor")]
    static void Open()
    {
        var w = GetWindow<Stage1CallMapEditorWindow>("Call Map Editor");
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

        _currentSongTime = (float)(EditorApplication.timeSinceStartup - _sessionStart)
                           - _pattern.introDelay;

        float musicLen = _pattern.music != null ? _pattern.music.length : 0f;
        if (musicLen > 0f && _currentSongTime > musicLen)
            StopSession();

        float absX   = (_currentSongTime + _pattern.introDelay) * _zoom;
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
                "Select or create a Call Pattern Data asset above to get started.",
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
                    "Call Pattern Data", _pattern, typeof(CallPatternData), false);
                if (next != _pattern) { StopSession(); _pattern = next; _selEntry = null; _currentGroupId = NextFreeGroupId(next); }
                if (GUILayout.Button("New", GUILayout.Width(44))) CreateNewCallPattern();
            }

            if (_pattern == null) return;

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _pattern.chartName  = EditorGUILayout.TextField("Name", _pattern.chartName);
                GUILayout.Space(8);
                EditorGUILayout.LabelField("BPM", GUILayout.Width(30));
                _pattern.bpm = EditorGUILayout.FloatField(_pattern.bpm, GUILayout.Width(56));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Intro Delay", GUILayout.Width(72));
                _pattern.introDelay = EditorGUILayout.FloatField(_pattern.introDelay, GUILayout.Width(46));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Hit Window (±s)", GUILayout.Width(96));
                _pattern.hitWindow = EditorGUILayout.FloatField(_pattern.hitWindow, GUILayout.Width(56));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Live Input From Beat", GUILayout.Width(120));
                _pattern.liveInputStartBeat = EditorGUILayout.FloatField(_pattern.liveInputStartBeat, GUILayout.Width(56));
            }
            _pattern.music = (AudioClip)EditorGUILayout.ObjectField(
                "Music", _pattern.music, typeof(AudioClip), false);
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
                $"Calls: {total}  (Tap {taps} / Hold {holds})   Call {calls} / Response {responses}   Duration: {durStr}   " +
                "Overlap is always allowed — no lock zones.",
                EditorStyles.miniLabel);
        }
    }

    // ── Toolbar ────────────────────────────────────────────────────────────
    void DrawToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.toolbar))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool canPlay = _pattern?.music != null;
                GUI.enabled = canPlay;

                if (_isRecording)
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("■  STOP REC", EditorStyles.toolbarButton, GUILayout.Width(96)))
                        StopSession();
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("●  REC", EditorStyles.toolbarButton, GUILayout.Width(64)))
                        StartSession(record: true);
                    GUI.backgroundColor = Color.white;

                    if (_isPlaying)
                    {
                        if (GUILayout.Button("■  STOP", EditorStyles.toolbarButton, GUILayout.Width(68)))
                            StopSession();
                    }
                    else
                    {
                        if (GUILayout.Button("▶  PREVIEW", EditorStyles.toolbarButton, GUILayout.Width(84)))
                            StartSession(record: false);
                    }
                }

                GUI.enabled = _pattern != null;
                GUILayout.Space(10);
                if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(68)))
                {
                    if (EditorUtility.DisplayDialog("Clear Pattern",
                        $"Delete all {_pattern.calls?.Count ?? 0} calls?", "Clear", "Cancel"))
                    {
                        Undo.RecordObject(_pattern, "Clear Pattern");
                        _pattern.calls?.Clear();
                        _selEntry = null;
                        _currentGroupId = 0;
                        EditorUtility.SetDirty(_pattern);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Hold≥", EditorStyles.miniLabel, GUILayout.Width(36));
                _holdThresholdSec = EditorGUILayout.FloatField(_holdThresholdSec, GUILayout.Width(36));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Zoom", EditorStyles.miniLabel, GUILayout.Width(36));
                _zoom = GUILayout.HorizontalSlider(_zoom, 30f, 500f, GUILayout.Width(90));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Snap", EditorStyles.miniLabel, GUILayout.Width(32));
                _snapIndex = EditorGUILayout.Popup(_snapIndex, SnapLabels, EditorStyles.toolbarPopup, GUILayout.Width(50));
                GUILayout.Space(8);
                EditorGUILayout.LabelField(
                    new GUIContent("Echo Gap", "Only used as a fallback when Z is pressed with no music " +
                        "playing (REC/PREVIEW off). While REC/PREVIEW is running, Z uses the exact beat " +
                        "it was pressed at instead."),
                    EditorStyles.miniLabel, GUILayout.Width(56));
                _echoGapBeats = EditorGUILayout.FloatField(_echoGapBeats, GUILayout.Width(36));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Group #", EditorStyles.miniLabel, GUILayout.Width(48));
                _currentGroupId = Mathf.Max(0, EditorGUILayout.IntField(_currentGroupId, GUILayout.Width(30)));
                GUILayout.Space(8);

                string timeStr = (_isRecording || _isPlaying) && _pattern != null
                    ? $"{Mathf.Max(0f, _currentSongTime) / 60f:0}:{Mathf.Max(0f, _currentSongTime) % 60f:00.0}  " +
                      $"Beat {Mathf.Max(0f, _pattern.TimeToBeat(_currentSongTime)):F1}"
                    : "--:--.--";
                EditorGUILayout.LabelField(timeStr, EditorStyles.miniLabel, GUILayout.Width(140));
            }

            if (_isRecording)
            {
                GUI.color = new Color(1f, 0.9f, 0.2f);
                EditorGUILayout.LabelField(
                    _isPressed
                        ? "● REC  —  holding... release to commit (short = Tap, long = Hold)   Esc = stop"
                        : "● REC  —  Space or Left-Click = press & hold to record a call   Esc = stop",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (!_isPlaying)
            {
                string selInfo = _selEntry != null
                    ? $"   |   Selected: {_selEntry.type} @ beat {_selEntry.cueBeat:F2} [{_selEntry.role}]  (C = toggle Call/Response)"
                    : "";
                EditorGUILayout.LabelField(
                    "Tap lane: click = add, drag = move, right-click = delete.   " +
                    "Hold lane: drag empty space = create, drag handles/body = resize/move.   " +
                    $"Z = echo Group #{_currentGroupId}'s Calls into a Response, then advance to #{_currentGroupId + 1}.   " +
                    "Del = delete selected" + selInfo,
                    EditorStyles.miniLabel);
            }
        }
    }

    // ── Timeline ───────────────────────────────────────────────────────────
    void DrawTimeline()
    {
        if (_pattern == null) return;

        Rect full = GUILayoutUtility.GetRect(
            GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (full.height < 20f) return;

        float totalAudio = Mathf.Max(10f, _pattern.music != null ? _pattern.music.length : 60f);
        float contentW   = (_pattern.introDelay + totalAudio) * _zoom + 120f;
        float contentH   = RULER_H + LANE_H * TOTAL_ROWS;

        Rect labelsRect = new Rect(full.x, full.y + RULER_H, LABEL_W, LANE_H * TOTAL_ROWS);
        DrawLaneLabels(labelsRect);

        Rect scrollRect  = new Rect(full.x + LABEL_W, full.y, full.width - LABEL_W, full.height);
        Rect contentRect = new Rect(0f, 0f, contentW, contentH);

        _scrollPos = GUI.BeginScrollView(scrollRect, _scrollPos, contentRect);

        EditorGUI.DrawRect(new Rect(0, 0, contentW, contentH), new Color(0.12f, 0.12f, 0.12f));
        DrawRulerAndGrid(contentW, totalAudio);
        DrawLaneBackgrounds(contentW);
        RecomputeGroupBands();
        DrawCallResponseGroups(contentH);
        DrawCalls();
        if (_isRecording && _isPressed) DrawRecordingPreview();
        DrawPlayhead(contentH);

        if (!_isRecording) HandleTimelineMouseInput(contentRect);

        GUI.EndScrollView();
    }

    // Physical row layout interleaves sections instead of stacking each section's rows together:
    // row 0 = Tap/sub0, row 1 = Hold/sub0, row 2 = Tap/sub1, row 3 = Hold/sub1 ("TAP HOLD TAP
    // HOLD", not "TAP TAP HOLD HOLD"). This matters for DrawCallResponseGroups' band split: with
    // interleaving, band 0 (rows 0-1) and band 1 (rows 2-3) each contain one full Tap+Hold pass,
    // instead of band 0 = Tap section, band 1 = Hold section (a coincidence in the old ordering
    // that made overlapping-group bands look identical to the Tap/Hold split).
    static int SectionOfRow(int row) => row % LANE_COUNT;
    static int SubRowOfRow(int row)  => row / LANE_COUNT;
    static int RowOf(int section, int subRow) => subRow * LANE_COUNT + section;

    void DrawLaneLabels(Rect rect)
    {
        for (int row = 0; row < TOTAL_ROWS; row++)
        {
            int   section = SectionOfRow(row);
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

    void DrawRulerAndGrid(float totalW, float totalAudio)
    {
        EditorGUI.DrawRect(new Rect(0, 0, totalW, RULER_H), new Color(0.08f, 0.08f, 0.08f));

        float introW = _pattern.introDelay * _zoom;
        EditorGUI.DrawRect(new Rect(0, 0, introW, RULER_H + LANE_H * TOTAL_ROWS),
            new Color(0f, 0f, 0f, 0.35f));
        if (introW > 20f)
            GUI.Label(new Rect(4f, 2f, 50f, RULER_H - 4f), "intro",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } });

        float liveX = (_pattern.introDelay + _pattern.BeatToTime(_pattern.liveInputStartBeat)) * _zoom;
        if (liveX > introW && liveX < totalW)
        {
            EditorGUI.DrawRect(new Rect(liveX, 0f, 2f, RULER_H + LANE_H * TOTAL_ROWS), new Color(0.6f, 1f, 0.6f, 0.6f));
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
            EditorGUI.DrawRect(new Rect(x, RULER_H, 1f, LANE_H * TOTAL_ROWS),
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
            int   section = SectionOfRow(row);
            float y  = RULER_H + row * LANE_H;
            Color bg = section % 2 == 0 ? new Color(0.155f, 0.155f, 0.155f) : new Color(0.130f, 0.130f, 0.130f);
            EditorGUI.DrawRect(new Rect(0f, y, totalW, LANE_H), bg);

            if (row < TOTAL_ROWS - 1)
                EditorGUI.DrawRect(new Rect(0f, y + LANE_H, totalW, 1f), new Color(0f, 0f, 0f, 0.35f));
        }
    }

    // Sub-row assignment from the most recent DrawCalls() pass, reused by hit-testing so a click
    // only matches notes actually in the row under the cursor (fixes picking the wrong one of two
    // vertically-stacked overlapping notes).
    readonly Dictionary<CallEntry, int> _tapRowCache  = new Dictionary<CallEntry, int>();
    readonly Dictionary<CallEntry, int> _holdRowCache = new Dictionary<CallEntry, int>();

    // groupId -> band (0/1), recomputed once per repaint (see RecomputeGroupBands) and shared by
    // both DrawCallResponseGroups (background wash) and DrawCalls (note rows) so a group's notes
    // always land in the same row as its own background band.
    readonly Dictionary<int, int> _groupBandCache = new Dictionary<int, int>();

    /// Alternates groups between GROUP_BANDS fixed bands in start-time order (0, 1, 0, 1, ...).
    void RecomputeGroupBands()
    {
        _groupBandCache.Clear();
        if (_pattern.calls == null) return;

        var starts = new List<(int groupId, float start)>();
        foreach (var grp in _pattern.calls.Where(c => c.groupId >= 0).GroupBy(c => c.groupId))
        {
            var callEntries = grp.Where(c => c.role == CallRole.Call).ToList();
            var respEntries = grp.Where(c => c.role == CallRole.Response).ToList();
            if (callEntries.Count == 0 && respEntries.Count == 0) continue;

            float start = callEntries.Count > 0 ? callEntries.Min(c => c.cueBeat) : respEntries.Min(c => c.cueBeat);
            starts.Add((grp.Key, start));
        }

        var ordered = starts.OrderBy(s => s.start).ToList();
        for (int i = 0; i < ordered.Count; i++)
            _groupBandCache[ordered[i].groupId] = i % GROUP_BANDS;
    }

    void DrawCalls()
    {
        if (_pattern.calls == null) return;

        _tapRowCache.Clear();
        _holdRowCache.Clear();
        foreach (var entry in _pattern.calls)
        {
            // A note's row always matches the fixed band its own group was assigned (see
            // RecomputeGroupBands) — not an independent per-note overlap check. This keeps every
            // note visually lined up with its group's background wash, and means row assignment
            // no longer depends on whether notes happen to literally overlap in time: different
            // groups always land on different rows, same group always shares one row.
            int band = entry.groupId >= 0 && _groupBandCache.TryGetValue(entry.groupId, out int b) ? b : 0;
            if (entry.type == CallType.Tap) { _tapRowCache[entry] = band; DrawTapNote(entry, band); }
            else                            { _holdRowCache[entry] = band; DrawHoldBar(entry, band); }
        }
    }

    float BeatX(float beat) => (_pattern.introDelay + _pattern.BeatToTime(beat)) * _zoom;

    /// Fixed-position rect for one of a section's SUBROWS (2) always-present full-height rows.
    /// sectionIndex: 0 = Tap, 1 = Hold.
    static (float y, float h) FixedSubRowRect(int sectionIndex, int subRow)
    {
        float rowTop = RULER_H + RowOf(sectionIndex, subRow) * LANE_H;
        return (rowTop + 6f, LANE_H - 12f);
    }

    void DrawTapNote(CallEntry entry, int subRow)
    {
        bool  selected = entry == _selEntry;
        bool  dragging = selected && _isDragging && _dragHandle == DragHandle.TapNote;
        float beat     = dragging ? _dragOriginCueBeat + _dragBeatDelta : entry.cueBeat;

        var (y, h) = FixedSubRowRect(0, subRow);
        float x = BeatX(beat) - NOTE_W * 0.5f;
        Color col = LaneColors[0];
        if (entry.role == CallRole.Call) col.a = 0.4f; // Call = demo cue, dimmed. Response = full color.

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

        if (!_isDragging)
            EditorGUIUtility.AddCursorRect(new Rect(x - 6f, y, NOTE_W + 12f, h), MouseCursor.MoveArrow);
    }

    void DrawHoldBar(CallEntry entry, int subRow)
    {
        bool selected = entry == _selEntry;
        float cueBeat     = entry.cueBeat;
        float releaseBeat = entry.releaseBeat;

        if (selected && _isDragging)
        {
            switch (_dragHandle)
            {
                case DragHandle.HoldStart: cueBeat     = Mathf.Min(_dragOriginCueBeat + _dragBeatDelta, _dragOriginReleaseBeat - MIN_HOLD_BEATS); break;
                case DragHandle.HoldEnd:   releaseBeat = Mathf.Max(_dragOriginReleaseBeat + _dragBeatDelta, _dragOriginCueBeat + MIN_HOLD_BEATS); break;
                case DragHandle.HoldBody:  cueBeat = _dragOriginCueBeat + _dragBeatDelta; releaseBeat = _dragOriginReleaseBeat + _dragBeatDelta; break;
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
        // Handles
        EditorGUI.DrawRect(new Rect(x0 - 2f, y, 4f, h), new Color(1f, 1f, 1f, 0.8f));
        EditorGUI.DrawRect(new Rect(x1 - 2f, y, 4f, h), new Color(1f, 1f, 1f, 0.8f));

        if (selected && _isDragging)
            GUI.Label(new Rect(x0, y - 16f, 100f, 14f), $"B {cueBeat:F2} → {releaseBeat:F2}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });

        if (!_isDragging)
        {
            EditorGUIUtility.AddCursorRect(new Rect(x0 - 6f, y, 12f, h), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(x1 - 6f, y, 12f, h), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(x0 + 6f, y, Mathf.Max(0f, (x1 - x0) - 12f), h), MouseCursor.MoveArrow);
        }
    }

    /// Groups the chart by each entry's explicit groupId (assigned at authoring time, not
    /// inferred from time layout — see EchoCallGroupToResponse), and draws them the same way
    /// BeatMapEditorWindow draws its DirectionGroup + player zone: a background wash across the
    /// whole group, a green "RESPONSE" zone tint over the response half, and a marker line at the
    /// exact call-end/response-start boundary. Groups simply alternate between GROUP_BANDS fixed
    /// bands in start-time order (band 0, 1, 0, 1, ...) rather than only splitting bands when an
    /// actual time-overlap is detected — simpler and more predictable, and consecutive groups are
    /// never on the same band even when they don't strictly overlap.
    void DrawCallResponseGroups(float contentH)
    {
        if (_pattern.calls == null || _pattern.calls.Count == 0) return;

        float End(CallEntry c) => c.type == CallType.Hold ? c.releaseBeat : c.cueBeat;

        var spans = new List<(int groupId, float start, float end, List<CallEntry> resps)>();
        foreach (var grp in _pattern.calls.Where(c => c.groupId >= 0).GroupBy(c => c.groupId))
        {
            var callEntries = grp.Where(c => c.role == CallRole.Call).ToList();
            var respEntries = grp.Where(c => c.role == CallRole.Response).ToList();
            if (callEntries.Count == 0 && respEntries.Count == 0) continue;

            float start = callEntries.Count > 0 ? callEntries.Min(c => c.cueBeat) : respEntries.Min(c => c.cueBeat);
            float end   = respEntries.Count > 0 ? respEntries.Max(End)
                        : callEntries.Count > 0 ? callEntries.Max(End) : start;
            spans.Add((grp.Key, start, end, respEntries));
        }
        if (spans.Count == 0) return;

        var ordered = spans.OrderBy(s => s.start).ToList();
        float bandH = (contentH - RULER_H) / GROUP_BANDS;

        int colorIndex = 0;
        foreach (var s in ordered)
        {
            float bandY = RULER_H + (_groupBandCache.TryGetValue(s.groupId, out int bnd) ? bnd : 0) * bandH;

            float x0 = BeatX(s.start);
            float x1 = BeatX(s.end);
            EditorGUI.DrawRect(new Rect(x0, bandY, Mathf.Max(1f, x1 - x0), bandH),
                GroupBgColors[colorIndex % GroupBgColors.Length]);
            colorIndex++;

            if (s.resps.Count == 0) continue;

            float rStart = s.resps.Min(c => c.cueBeat);
            float rEnd   = s.resps.Max(End);
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

    void DrawRecordingPreview()
    {
        float x0 = BeatX(_pressStartBeat);
        float x1 = (_currentSongTime + _pattern.introDelay) * _zoom;
        EditorGUI.DrawRect(new Rect(Mathf.Min(x0, x1), RULER_H, Mathf.Max(2f, Mathf.Abs(x1 - x0)), LANE_H * TOTAL_ROWS),
            new Color(1f, 1f, 0.3f, 0.18f));
    }

    void DrawPlayhead(float contentH)
    {
        if (!_isRecording && !_isPlaying) return;
        float x = (_currentSongTime + _pattern.introDelay) * _zoom;
        EditorGUI.DrawRect(new Rect(x - 1f, 0f, 2f, contentH), new Color(1f, 0.92f, 0.15f, 0.90f));
        EditorGUI.DrawRect(new Rect(x - 4f, 0f, 8f, 4f), new Color(1f, 0.92f, 0.15f));
    }

    // ── Timeline mouse interaction (edit mode only) ────────────────────────
    void HandleTimelineMouseInput(Rect contentRect)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown)
        {
            Vector2 mp = e.mousePosition;
            if (!contentRect.Contains(mp) || mp.y < RULER_H) return;

            // Release any leftover keyboard focus from Name/BPM/Echo Gap/etc. so that Z/C/Delete
            // reach HandleGlobalKeys afterwards instead of being silently swallowed by a field
            // that still holds keyboard focus from an earlier click.
            GUI.FocusControl(null);

            int row     = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
            int section = row >= 0 && row < TOTAL_ROWS ? SectionOfRow(row) : -1; // 0 = Tap, 1 = Hold
            if (section < 0) return;

            float songTime = mp.x / _zoom - _pattern.introDelay;
            float clickBeat = SnapBeat(_pattern.TimeToBeat(songTime));

            if (e.button == 0)
            {
                var hitTap = FindTapNearPixel(mp, 12f);
                if (hitTap != null)
                {
                    BeginDrag(hitTap, DragHandle.TapNote, mp.x);
                }
                else
                {
                    var (hitHold, handle) = FindHoldNearPixel(mp, 10f);
                    if (hitHold != null)
                    {
                        BeginDrag(hitHold, handle, mp.x);
                    }
                    else if (section == 0)
                    {
                        Undo.RecordObject(_pattern, "Add Tap");
                        var entry = new CallEntry { type = CallType.Tap, role = CallRole.Call, groupId = _currentGroupId, cueBeat = clickBeat };
                        AddCall(entry);
                        _selEntry = entry;
                    }
                    else
                    {
                        // Start creating a Hold: begin at click, immediately drag the end handle
                        float step = 1f / SnapDivs[_snapIndex];
                        Undo.RecordObject(_pattern, "Add Hold");
                        var entry = new CallEntry { type = CallType.Hold, role = CallRole.Call, groupId = _currentGroupId, cueBeat = clickBeat, releaseBeat = clickBeat + step };
                        AddCall(entry);
                        BeginDrag(entry, DragHandle.HoldEnd, mp.x);
                    }
                }
                e.Use();
            }
            else if (e.button == 1)
            {
                var hitTap = FindTapNearPixel(mp, 12f);
                if (hitTap != null) { DeleteCall(hitTap); }
                else
                {
                    var (hitHold, _) = FindHoldNearPixel(mp, 10f);
                    if (hitHold != null) DeleteCall(hitHold);
                }
                e.Use();
            }

            Repaint();
        }
        else if (e.type == EventType.MouseDrag && _isDragging && _selEntry != null)
        {
            float deltaPixels = e.mousePosition.x - _dragOriginMouseX;
            float deltaBeat   = deltaPixels / (_zoom * _pattern.BeatInterval);
            float rawTarget   = _dragHandle == DragHandle.HoldEnd
                ? _dragOriginReleaseBeat + deltaBeat
                : _dragOriginCueBeat + deltaBeat;
            _dragBeatDelta = SnapBeat(Mathf.Max(0f, rawTarget)) -
                             (_dragHandle == DragHandle.HoldEnd ? _dragOriginReleaseBeat : _dragOriginCueBeat);
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && _isDragging)
        {
            CommitDrag();
            e.Use();
            Repaint();
        }
    }

    void BeginDrag(CallEntry entry, DragHandle handle, float mouseX)
    {
        _selEntry              = entry;
        _dragHandle             = handle;
        _isDragging             = true;
        _dragOriginMouseX       = mouseX;
        _dragOriginCueBeat      = entry.cueBeat;
        _dragOriginReleaseBeat  = entry.releaseBeat;
        _dragBeatDelta          = 0f;
        GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
    }

    void CommitDrag()
    {
        if (_selEntry != null)
        {
            Undo.RecordObject(_pattern, "Move Call");
            switch (_dragHandle)
            {
                case DragHandle.TapNote:
                    _selEntry.cueBeat = Mathf.Max(0f, _dragOriginCueBeat + _dragBeatDelta);
                    break;
                case DragHandle.HoldStart:
                    _selEntry.cueBeat = Mathf.Min(Mathf.Max(0f, _dragOriginCueBeat + _dragBeatDelta), _dragOriginReleaseBeat - MIN_HOLD_BEATS);
                    break;
                case DragHandle.HoldEnd:
                    _selEntry.releaseBeat = Mathf.Max(_dragOriginReleaseBeat + _dragBeatDelta, _dragOriginCueBeat + MIN_HOLD_BEATS);
                    break;
                case DragHandle.HoldBody:
                    _selEntry.cueBeat     = Mathf.Max(0f, _dragOriginCueBeat + _dragBeatDelta);
                    _selEntry.releaseBeat = Mathf.Max(0f, _dragOriginReleaseBeat + _dragBeatDelta);
                    break;
            }
            _pattern.calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
            EditorUtility.SetDirty(_pattern);
        }

        _isDragging    = false;
        _dragHandle    = DragHandle.None;
        _dragBeatDelta = 0f;
        GUIUtility.hotControl = 0;
    }

    // ── Global key handler (edit mode) ─────────────────────────────────────
    void HandleGlobalKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (_pattern == null) return;
        // Don't hijack Z/C/Delete while the user is actually typing into a text/number field
        // (Name, BPM, Echo Gap, ...). Combined with the GUI.FocusControl(null) on timeline
        // clicks below, this also fixes the common IMGUI bug where a field keeps keyboard
        // focus after being clicked once and silently swallows all later key presses.
        if (EditorGUIUtility.editingTextField) return;

        if (e.keyCode == KeyCode.Z)
        {
            EchoCallGroupToResponse();
            e.Use();
            Repaint();
            return;
        }

        if (_selEntry == null) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            DeleteCall(_selEntry);
            _selEntry = null;
            e.Use();
            Repaint();
        }
        else if (e.keyCode == KeyCode.C)
        {
            Undo.RecordObject(_pattern, "Toggle Call/Response");
            _selEntry.role = _selEntry.role == CallRole.Call ? CallRole.Response : CallRole.Call;
            EditorUtility.SetDirty(_pattern);
            e.Use();
            Repaint();
        }
    }

    /// Z: takes every Call-role entry stamped with the current groupId (the group actively being
    /// authored — however it's laid out in time, even if a look-ahead Call was dropped inside an
    /// earlier group's already-generated Response range), and mirrors its rhythm — same relative
    /// beat offsets, same Tap/Hold shape — into a new Response-role group sharing that same groupId,
    /// starting _echoGapBeats after the call group ends. Designer never hand-places the matching
    /// response notes. Grouping is authoring-order based (via groupId), not time-order based, so
    /// it can't be corrupted by overlapping look-ahead Calls the way inferring groups from the
    /// timeline layout would be.
    void EchoCallGroupToResponse()
    {
        if (_pattern.calls == null || _pattern.calls.Count == 0) return;

        var group = _pattern.calls
            .Where(c => c.role == CallRole.Call && c.groupId == _currentGroupId)
            .OrderBy(c => c.cueBeat)
            .ToList();

        if (group.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to echo",
                $"No Call notes found in the current group (#{_currentGroupId}).\n\n" +
                "Place one or more Tap/Hold notes first (new notes default to Call, stamped with " +
                $"the current Group # shown in the toolbar) — then press Z to mirror them into a Response group.",
                "OK");
            return;
        }

        float groupStart = group[0].cueBeat;
        float groupEnd   = group.Max(c => c.type == CallType.Hold ? c.releaseBeat : c.cueBeat);

        float responseStart;
        if (_isRecording || _isPlaying)
        {
            // Live clock is running — the response starts exactly where Z was actually pressed
            // (snapped to grid), not a fixed offset. Lets a musical pause before the response
            // (or none at all) come from performing it live, same as the call notes themselves.
            float pressedBeat = SnapBeat(_pattern.TimeToBeat(_currentSongTime));
            responseStart = Mathf.Max(groupEnd, pressedBeat);
        }
        else
        {
            // No live playhead in pure edit mode (no music running) — fall back to a fixed gap.
            responseStart = groupEnd + _echoGapBeats;
        }

        Undo.RecordObject(_pattern, "Echo Call Group to Response");

        var newEntries = new List<CallEntry>();
        foreach (var c in group)
        {
            float delta = c.cueBeat - groupStart;
            var echoed = new CallEntry
            {
                type    = c.type,
                role    = CallRole.Response,
                groupId = _currentGroupId,
                cueBeat = responseStart + delta,
            };
            if (c.type == CallType.Hold)
                echoed.releaseBeat = responseStart + (c.releaseBeat - groupStart);
            newEntries.Add(echoed);
        }

        _pattern.calls.AddRange(newEntries);
        _pattern.calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
        _selEntry = newEntries[0];
        EditorUtility.SetDirty(_pattern);

        // Scroll so the freshly-generated response group is actually visible — otherwise it can
        // land off the right edge of the timeline view and look like nothing happened.
        float targetX = BeatX(responseStart) - (position.width - LABEL_W - 20f) * 0.25f;
        _scrollPos.x  = Mathf.Max(0f, targetX);

        Debug.Log($"[Call Map Editor] Echoed group #{_currentGroupId}: {newEntries.Count} call(s) -> response starting at beat {responseStart:F2}.");

        // Subsequent new notes (including overlapping look-ahead Calls placed inside this very
        // Response range) belong to the next group, not this one.
        _currentGroupId++;
    }

    static int NextFreeGroupId(CallPatternData p)
    {
        if (p?.calls == null || p.calls.Count == 0) return 0;
        int max = -1;
        foreach (var c in p.calls) max = Mathf.Max(max, c.groupId);
        return max + 1;
    }

    // ── Recording key/mouse capture ────────────────────────────────────────
    void CaptureRecordingKeys()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopSession(); e.Use(); return; }

        // Z works during REC too (not just Edit mode) — punch in a call group live, hit Z
        // without stopping the take, then keep recording the next group. Blocked while a
        // note is currently being held down (_isPressed) so it can't fire mid-press.
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Z && !_isPressed && !EditorGUIUtility.editingTextField)
        {
            EchoCallGroupToResponse();
            e.Use();
            return;
        }

        if (_currentSongTime < 0f) return; // still in intro

        bool isPressEvent   = (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) ||
                               (e.type == EventType.MouseDown && e.button == 0);
        bool isReleaseEvent = (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space) ||
                               (e.type == EventType.MouseUp && e.button == 0);

        if (isPressEvent && !_isPressed)
        {
            _isPressed           = true;
            _pressStartBeat      = _pattern.TimeToBeat(_currentSongTime);
            _pressStartRealTime  = EditorApplication.timeSinceStartup;
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

    // ── Helpers ────────────────────────────────────────────────────────────
    float SnapBeat(float rawBeat)
    {
        float grid = 1f / SnapDivs[_snapIndex];
        return Mathf.Round(rawBeat / grid) * grid;
    }

    void AddCall(CallEntry entry)
    {
        if (_pattern.calls == null) _pattern.calls = new List<CallEntry>();
        _pattern.calls.Add(entry);
        _pattern.calls.Sort((a, b) => a.cueBeat.CompareTo(b.cueBeat));
        EditorUtility.SetDirty(_pattern);
    }

    void DeleteCall(CallEntry entry)
    {
        if (_pattern.calls == null) return;
        Undo.RecordObject(_pattern, "Delete Call");
        _pattern.calls.Remove(entry);
        if (_selEntry == entry) _selEntry = null;
        EditorUtility.SetDirty(_pattern);
    }

    CallEntry FindTapNearPixel(Vector2 mp, float tolerance)
    {
        if (_pattern.calls == null) return null;
        int row = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        if (row < 0 || row >= TOTAL_ROWS || SectionOfRow(row) != 0) return null;
        int clickedSubRow = SubRowOfRow(row);

        float bestDx = tolerance;
        CallEntry best = null;
        foreach (var entry in _pattern.calls)
        {
            if (entry.type != CallType.Tap) continue;
            if (!_tapRowCache.TryGetValue(entry, out int er) || er % SUBROWS != clickedSubRow) continue;
            float dx = Mathf.Abs(mp.x - BeatX(entry.cueBeat));
            if (dx < bestDx) { bestDx = dx; best = entry; }
        }
        return best;
    }

    (CallEntry entry, DragHandle handle) FindHoldNearPixel(Vector2 mp, float handleTolerance)
    {
        if (_pattern.calls == null) return (null, DragHandle.None);
        int row = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        if (row < 0 || row >= TOTAL_ROWS || SectionOfRow(row) != 1) return (null, DragHandle.None);
        int clickedSubRow = SubRowOfRow(row);

        foreach (var entry in _pattern.calls)
        {
            if (entry.type != CallType.Hold) continue;
            if (!_holdRowCache.TryGetValue(entry, out int er) || er % SUBROWS != clickedSubRow) continue;

            float x0 = BeatX(entry.cueBeat);
            float x1 = BeatX(entry.releaseBeat);

            if (Mathf.Abs(mp.x - x0) < handleTolerance) return (entry, DragHandle.HoldStart);
            if (Mathf.Abs(mp.x - x1) < handleTolerance) return (entry, DragHandle.HoldEnd);
            if (mp.x > x0 && mp.x < x1)                 return (entry, DragHandle.HoldBody);
        }
        return (null, DragHandle.None);
    }

    // ── Session control ────────────────────────────────────────────────────
    void StartSession(bool record)
    {
        if (_pattern?.music == null) return;
        _isRecording     = record;
        _isPlaying       = !record;
        _sessionStart    = EditorApplication.timeSinceStartup;
        _currentSongTime = -_pattern.introDelay;
        _isPressed       = false;
        PlayAudio(_pattern.music);
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
            "Create Call Pattern Data", "NewCallPattern", "asset",
            "Choose where to save the new call pattern asset", folder);
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<CallPatternData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        StopSession();
        _pattern  = asset;
        _selEntry = null;
    }

    // ── Audio preview via reflection ───────────────────────────────────────
    static Type GetAudioUtil() =>
        typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    void PlayAudio(AudioClip clip)
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
                    ? new object[] { clip, 0, false }
                    : new object[] { clip };
                _playMethod.Invoke(null, args);
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[Stage1CallMapEditor] Audio preview error: {ex.Message}"); }
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
