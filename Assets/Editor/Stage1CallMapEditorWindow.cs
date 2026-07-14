using System;
using System.Collections.Generic;
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

    // ── Layout constants ───────────────────────────────────────────────────
    const float LABEL_W    = 48f;
    const float RULER_H    = 22f;
    const float LANE_H     = 44f;
    const int   LANE_COUNT = 2;
    const float NOTE_W     = 10f;
    const float MIN_HOLD_BEATS = 0.1f;

    static readonly string[] LaneLabels = { "TAP", "HOLD" };
    static readonly Color[]  LaneColors =
    {
        new Color(1.00f, 0.85f, 0.20f),   // Tap  – yellow
        new Color(0.30f, 0.70f, 1.00f),   // Hold – blue
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
                if (next != _pattern) { StopSession(); _pattern = next; _selEntry = null; }
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
            int taps  = 0, holds = 0;
            if (_pattern.calls != null)
                foreach (var c in _pattern.calls) { if (c.type == CallType.Tap) taps++; else holds++; }

            float dur = _pattern.music != null ? _pattern.music.length : 0f;
            string durStr = dur > 0
                ? $"{Mathf.FloorToInt(dur / 60f):0}:{dur % 60f:00.#}"
                : "–";
            EditorGUILayout.LabelField(
                $"Calls: {total}  (Tap {taps} / Hold {holds})   Duration: {durStr}   " +
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
                string selInfo = _selEntry != null ? $"   |   Selected: {_selEntry.type} @ beat {_selEntry.cueBeat:F2}" : "";
                EditorGUILayout.LabelField(
                    "Tap lane: click = add, drag = move, right-click = delete.   " +
                    "Hold lane: drag empty space = create, drag handles/body = resize/move.   Del = delete selected" + selInfo,
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
        float contentH   = RULER_H + LANE_H * LANE_COUNT;

        Rect labelsRect = new Rect(full.x, full.y + RULER_H, LABEL_W, LANE_H * LANE_COUNT);
        DrawLaneLabels(labelsRect);

        Rect scrollRect  = new Rect(full.x + LABEL_W, full.y, full.width - LABEL_W, full.height);
        Rect contentRect = new Rect(0f, 0f, contentW, contentH);

        _scrollPos = GUI.BeginScrollView(scrollRect, _scrollPos, contentRect);

        EditorGUI.DrawRect(new Rect(0, 0, contentW, contentH), new Color(0.12f, 0.12f, 0.12f));
        DrawRulerAndGrid(contentW, totalAudio);
        DrawLaneBackgrounds(contentW);
        DrawCalls();
        if (_isRecording && _isPressed) DrawRecordingPreview();
        DrawPlayhead(contentH);

        if (!_isRecording) HandleTimelineMouseInput(contentRect);

        GUI.EndScrollView();
    }

    void DrawLaneLabels(Rect rect)
    {
        for (int i = 0; i < LANE_COUNT; i++)
        {
            Rect  r = new Rect(rect.x, rect.y + i * LANE_H, rect.width, LANE_H);
            Color c = LaneColors[i];
            EditorGUI.DrawRect(r, new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f));
            GUI.Label(r, LaneLabels[i], new GUIStyle(EditorStyles.boldLabel)
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
        EditorGUI.DrawRect(new Rect(0, 0, introW, RULER_H + LANE_H * LANE_COUNT),
            new Color(0f, 0f, 0f, 0.35f));
        if (introW > 20f)
            GUI.Label(new Rect(4f, 2f, 50f, RULER_H - 4f), "intro",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } });

        float liveX = (_pattern.introDelay + _pattern.BeatToTime(_pattern.liveInputStartBeat)) * _zoom;
        if (liveX > introW && liveX < totalW)
        {
            EditorGUI.DrawRect(new Rect(liveX, 0f, 2f, RULER_H + LANE_H * LANE_COUNT), new Color(0.6f, 1f, 0.6f, 0.6f));
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
            EditorGUI.DrawRect(new Rect(x, RULER_H, 1f, LANE_H * LANE_COUNT),
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
        for (int i = 0; i < LANE_COUNT; i++)
        {
            float y  = RULER_H + i * LANE_H;
            Color bg = i % 2 == 0 ? new Color(0.155f, 0.155f, 0.155f) : new Color(0.130f, 0.130f, 0.130f);
            EditorGUI.DrawRect(new Rect(0f, y, totalW, LANE_H), bg);
        }
    }

    void DrawCalls()
    {
        if (_pattern.calls == null) return;
        foreach (var entry in _pattern.calls)
        {
            if (entry.type == CallType.Tap) DrawTapNote(entry);
            else                            DrawHoldBar(entry);
        }
    }

    float BeatX(float beat) => (_pattern.introDelay + _pattern.BeatToTime(beat)) * _zoom;

    void DrawTapNote(CallEntry entry)
    {
        bool  selected = entry == _selEntry;
        bool  dragging = selected && _isDragging && _dragHandle == DragHandle.TapNote;
        float beat     = dragging ? _dragOriginCueBeat + _dragBeatDelta : entry.cueBeat;

        float x = BeatX(beat) - NOTE_W * 0.5f;
        float y = RULER_H + 0 * LANE_H + 6f;
        float h = LANE_H - 12f;
        Color col = LaneColors[0];

        if (selected)
        {
            Color outline = dragging ? new Color(1f, 0.9f, 0.1f) : Color.white;
            EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, NOTE_W + 4f, h + 4f), outline);
        }
        EditorGUI.DrawRect(new Rect(x, y, NOTE_W, h), col);
        EditorGUI.DrawRect(new Rect(x + 2f, y + 2f, NOTE_W - 4f, h * 0.4f),
            new Color(1f, 1f, 1f, 0.35f));

        if (dragging)
            GUI.Label(new Rect(x - 12f, y - 16f, 50f, 14f), $"B {beat:F2}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });

        if (!_isDragging)
            EditorGUIUtility.AddCursorRect(new Rect(x - 6f, y, NOTE_W + 12f, h), MouseCursor.MoveArrow);
    }

    void DrawHoldBar(CallEntry entry)
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

        float x0 = BeatX(cueBeat);
        float x1 = BeatX(releaseBeat);
        float y  = RULER_H + 1 * LANE_H + 6f;
        float h  = LANE_H - 12f;
        Color col = LaneColors[1];

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

    void DrawRecordingPreview()
    {
        float x0 = BeatX(_pressStartBeat);
        float x1 = (_currentSongTime + _pattern.introDelay) * _zoom;
        EditorGUI.DrawRect(new Rect(Mathf.Min(x0, x1), RULER_H, Mathf.Max(2f, Mathf.Abs(x1 - x0)), LANE_H * LANE_COUNT),
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

            int lane = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
            if (lane < 0 || lane >= LANE_COUNT) return;

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
                    else if (lane == 0)
                    {
                        Undo.RecordObject(_pattern, "Add Tap");
                        var entry = new CallEntry { type = CallType.Tap, cueBeat = clickBeat };
                        AddCall(entry);
                        _selEntry = entry;
                    }
                    else
                    {
                        // Start creating a Hold: begin at click, immediately drag the end handle
                        float step = 1f / SnapDivs[_snapIndex];
                        Undo.RecordObject(_pattern, "Add Hold");
                        var entry = new CallEntry { type = CallType.Hold, cueBeat = clickBeat, releaseBeat = clickBeat + step };
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
        if (_pattern == null || _selEntry == null) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            DeleteCall(_selEntry);
            _selEntry = null;
            e.Use();
            Repaint();
        }
    }

    // ── Recording key/mouse capture ────────────────────────────────────────
    void CaptureRecordingKeys()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopSession(); e.Use(); return; }
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
                ? new CallEntry { type = CallType.Tap, cueBeat = pressBeat }
                : new CallEntry { type = CallType.Hold, cueBeat = pressBeat, releaseBeat = Mathf.Max(releaseBeat, pressBeat + MIN_HOLD_BEATS) };
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
        int lane = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        if (lane != 0) return null;

        float bestDx = tolerance;
        CallEntry best = null;
        foreach (var entry in _pattern.calls)
        {
            if (entry.type != CallType.Tap) continue;
            float dx = Mathf.Abs(mp.x - BeatX(entry.cueBeat));
            if (dx < bestDx) { bestDx = dx; best = entry; }
        }
        return best;
    }

    (CallEntry entry, DragHandle handle) FindHoldNearPixel(Vector2 mp, float handleTolerance)
    {
        if (_pattern.calls == null) return (null, DragHandle.None);
        int lane = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        if (lane != 1) return (null, DragHandle.None);

        foreach (var entry in _pattern.calls)
        {
            if (entry.type != CallType.Hold) continue;
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
