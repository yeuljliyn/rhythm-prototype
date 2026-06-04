using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Direction-pattern beat map editor.
/// REC mode: arrow/WASD keys record direction inputs; Space confirms a group.
/// Edit mode: click to add/select; right-click or Del to delete; drag to move.
/// </summary>
public class BeatMapEditorWindow : EditorWindow
{
    // ── State ──────────────────────────────────────────────────────────────
    PatternData _pattern;
    bool   _isRecording;
    bool   _isPlaying;
    double _sessionStart;
    float  _currentSongTime;

    // Pending group accumulates direction inputs during recording until Space
    DirectionGroup _pendingGroup;

    // ── Timeline view ──────────────────────────────────────────────────────
    Vector2 _scrollPos;
    float   _zoom = 120f;

    // ── Selection / drag (edit mode) ───────────────────────────────────────
    int  _selGroupIdx = -1;
    int  _selInputIdx = -1;   // -1 = space marker selected, >=0 = direction input
    bool  _isDragging;
    float _dragOriginMouseX;
    float _dragOriginBeat;
    float _dragBeatDelta;

    // ── Snap ───────────────────────────────────────────────────────────────
    static readonly int[]    SnapDivs   = { 1, 2, 4, 8, 16 };
    static readonly string[] SnapLabels = { "1/1", "1/2", "1/4", "1/8", "1/16" };
    int _snapIndex = 2;

    // ── Layout constants ───────────────────────────────────────────────────
    const float LABEL_W    = 48f;
    const float RULER_H    = 22f;
    const float LANE_H     = 38f;
    const int   LANE_COUNT = 4;
    const float NOTE_W     = 10f;

    // ── Direction ↔ lane mapping ───────────────────────────────────────────
    static readonly ArrowDir[] LaneDirs   = { ArrowDir.Up, ArrowDir.Down, ArrowDir.Left, ArrowDir.Right };
    static readonly string[]   LaneLabels = { "↑", "↓", "←", "→" };
    static readonly Color[]    LaneColors =
    {
        new Color(1.00f, 0.90f, 0.20f),   // Up    – yellow
        new Color(0.30f, 1.00f, 0.45f),   // Down  – green
        new Color(0.30f, 0.60f, 1.00f),   // Left  – blue
        new Color(1.00f, 0.35f, 0.35f),   // Right – red
    };

    // Alternating group background tints
    static readonly Color[] GroupBgColors =
    {
        new Color(0.20f, 0.40f, 0.60f, 0.12f),
        new Color(0.50f, 0.20f, 0.60f, 0.12f),
    };

    // ── Audio reflection ───────────────────────────────────────────────────
    static MethodInfo _playMethod;
    static MethodInfo _stopMethod;

    // ── Menu ───────────────────────────────────────────────────────────────
    [MenuItem("RhythmGame/Beat Map Editor %#e")]
    static void Open()
    {
        var w = GetWindow<BeatMapEditorWindow>("Beat Map Editor");
        w.minSize = new Vector2(700f, 440f);
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

        // Auto-scroll: keep playhead at 25 % from the left
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
                "Select or create a Pattern Data asset above to get started.",
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
                var next = (PatternData)EditorGUILayout.ObjectField(
                    "Pattern Data", _pattern, typeof(PatternData), false);
                if (next != _pattern) { StopSession(); _pattern = next; }
                if (GUILayout.Button("New", GUILayout.Width(44))) CreateNewPattern();
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
            _pattern.music = (AudioClip)EditorGUILayout.ObjectField(
                "Music", _pattern.music, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_pattern);

            int groupCount = _pattern.groups?.Count ?? 0;
            int inputCount = 0;
            if (_pattern.groups != null)
                foreach (var g in _pattern.groups) inputCount += g.inputs.Count;

            float dur = _pattern.music != null ? _pattern.music.length : 0f;
            string durStr = dur > 0
                ? $"{Mathf.FloorToInt(dur / 60f):0}:{dur % 60f:00.#}"
                : "–";
            EditorGUILayout.LabelField(
                $"Groups: {groupCount}   Inputs: {inputCount}   Duration: {durStr}   " +
                "(drag audio into Music field, then hit ● REC)",
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
                    if (GUILayout.Button("■  STOP REC", EditorStyles.toolbarButton,
                        GUILayout.Width(96)))
                        StopSession();
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("●  REC", EditorStyles.toolbarButton,
                        GUILayout.Width(64)))
                        StartSession(record: true);
                    GUI.backgroundColor = Color.white;

                    if (_isPlaying)
                    {
                        if (GUILayout.Button("■  STOP", EditorStyles.toolbarButton,
                            GUILayout.Width(68)))
                            StopSession();
                    }
                    else
                    {
                        if (GUILayout.Button("▶  PREVIEW", EditorStyles.toolbarButton,
                            GUILayout.Width(84)))
                            StartSession(record: false);
                    }
                }

                GUI.enabled = _pattern != null;
                GUILayout.Space(10);
                if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(68)))
                {
                    if (EditorUtility.DisplayDialog("Clear Pattern",
                        $"Delete all {_pattern.groups?.Count ?? 0} groups?", "Clear", "Cancel"))
                    {
                        Undo.RecordObject(_pattern, "Clear Pattern");
                        _pattern.groups?.Clear();
                        EditorUtility.SetDirty(_pattern);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Zoom", EditorStyles.miniLabel, GUILayout.Width(36));
                _zoom = GUILayout.HorizontalSlider(_zoom, 30f, 500f, GUILayout.Width(90));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Snap", EditorStyles.miniLabel, GUILayout.Width(32));
                _snapIndex = EditorGUILayout.Popup(_snapIndex, SnapLabels,
                    EditorStyles.toolbarPopup, GUILayout.Width(50));
                GUILayout.Space(8);

                string timeStr = (_isRecording || _isPlaying) && _pattern != null
                    ? $"{Mathf.Max(0f, _currentSongTime) / 60f:0}:{Mathf.Max(0f, _currentSongTime) % 60f:00.0}  " +
                      $"Beat {Mathf.Max(0f, _pattern.TimeToBeat(_currentSongTime)):F1}"
                    : "--:--.--";
                EditorGUILayout.LabelField(timeStr, EditorStyles.miniLabel, GUILayout.Width(140));
            }

            // Hint bar
            if (_isRecording)
            {
                int pending = _pendingGroup?.inputs.Count ?? 0;
                GUI.color = new Color(1f, 0.9f, 0.2f);
                EditorGUILayout.LabelField(
                    $"● REC  —  ↑↓←→ / WASD = direction input   Space = confirm group   Esc = stop   " +
                    $"Pending: {pending} input(s)",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (!_isPlaying)
            {
                string selInfo = _selGroupIdx >= 0
                    ? (_selInputIdx >= 0
                        ? $"   |   Selected: Group {_selGroupIdx}, Input {_selInputIdx}"
                        : $"   |   Selected: Group {_selGroupIdx} (space marker)")
                    : "";
                EditorGUILayout.LabelField(
                    "Left-click = add / select   Drag = move   Right-click = delete   Del = delete selected" + selInfo,
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
        DrawGroupsAndInputs();
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

    void DrawGroupsAndInputs()
    {
        if (_pattern.groups == null) return;

        // Group background shading
        for (int g = 0; g < _pattern.groups.Count; g++)
        {
            var grp = _pattern.groups[g];
            if (grp.inputs.Count == 0) continue;

            float startBeat = grp.inputs[0].beat;
            float endBeat   = grp.spaceBeat >= 0f ? grp.spaceBeat : startBeat;
            float startX    = (_pattern.introDelay + _pattern.BeatToTime(startBeat)) * _zoom;
            float endX      = (_pattern.introDelay + _pattern.BeatToTime(endBeat)) * _zoom;

            EditorGUI.DrawRect(
                new Rect(startX, RULER_H, Mathf.Max(2f, endX - startX), LANE_H * LANE_COUNT),
                GroupBgColors[g % GroupBgColors.Length]);
        }

        // Confirmed groups: inputs + space markers
        for (int g = 0; g < _pattern.groups.Count; g++)
        {
            var grp = _pattern.groups[g];

            for (int i = 0; i < grp.inputs.Count; i++)
                DrawInputNote(grp.inputs[i], g, i, pending: false);

            if (grp.spaceBeat >= 0f)
                DrawSpaceMarker(grp.spaceBeat, g);
        }

        // Pending group (during recording) — shown lighter, not yet saved
        if (_pendingGroup != null)
        {
            foreach (var inp in _pendingGroup.inputs)
                DrawInputNote(inp, -1, -1, pending: true);
        }
    }

    void DrawInputNote(DirectionInputEntry entry, int groupIdx, int inputIdx, bool pending)
    {
        int   lane    = DirToLane(entry.direction);
        Color baseCol = LaneColors[lane];
        Color drawCol = pending ? new Color(baseCol.r, baseCol.g, baseCol.b, 0.40f) : baseCol;

        bool  selected = (!pending && groupIdx == _selGroupIdx && inputIdx == _selInputIdx);
        bool  dragging = selected && _isDragging;
        float beat     = dragging ? _dragOriginBeat + _dragBeatDelta : entry.beat;

        float x = (_pattern.introDelay + _pattern.BeatToTime(beat)) * _zoom - NOTE_W * 0.5f;
        float y = RULER_H + lane * LANE_H + 5f;
        float h = LANE_H - 10f;

        if (selected)
        {
            Color outline = dragging ? new Color(1f, 0.9f, 0.1f) : Color.white;
            EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, NOTE_W + 4f, h + 4f), outline);
        }

        EditorGUI.DrawRect(new Rect(x, y, NOTE_W, h), drawCol);
        // Shine
        EditorGUI.DrawRect(new Rect(x + 2f, y + 2f, NOTE_W - 4f, h * 0.4f),
            new Color(Mathf.Min(baseCol.r + 0.4f, 1f),
                      Mathf.Min(baseCol.g + 0.4f, 1f),
                      Mathf.Min(baseCol.b + 0.4f, 1f), 0.4f));

        if (dragging)
            GUI.Label(new Rect(x - 12f, y - 16f, 50f, 14f), $"B {beat:F2}",
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });

        if (!_isRecording && !pending && !_isDragging)
            EditorGUIUtility.AddCursorRect(new Rect(x - 6f, y, NOTE_W + 12f, h), MouseCursor.MoveArrow);
    }

    void DrawSpaceMarker(float beat, int groupIdx)
    {
        float x   = (_pattern.introDelay + _pattern.BeatToTime(beat)) * _zoom;
        bool  sel = (groupIdx == _selGroupIdx && _selInputIdx == -1);

        Color lineCol = sel ? new Color(1f, 1f, 0.1f) : new Color(1f, 1f, 0.3f, 0.75f);
        EditorGUI.DrawRect(new Rect(x - 1f, RULER_H, 2f, LANE_H * LANE_COUNT), lineCol);
        GUI.Label(new Rect(x + 3f, RULER_H + 2f, 28f, 14f), "SPC",
            new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = lineCol }, fontStyle = FontStyle.Bold });
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

            if (e.button == 0)
            {
                var (hg, hi) = FindInputNearPixel(mp, 12f);
                if (hg >= 0)
                {
                    // Select and start drag
                    _selGroupIdx      = hg;
                    _selInputIdx      = hi;
                    _isDragging       = true;
                    _dragOriginMouseX = mp.x;
                    _dragOriginBeat   = _pattern.groups[hg].inputs[hi].beat;
                    _dragBeatDelta    = 0f;
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                }
                else
                {
                    int sg = FindSpaceMarkerNearPixel(mp.x, 8f);
                    if (sg >= 0)
                    {
                        _selGroupIdx = sg;
                        _selInputIdx = -1;
                    }
                    else
                    {
                        // Add new direction input to the appropriate group
                        float beat = SnapBeat(_pattern.TimeToBeat(songTime));
                        if (beat >= 0f)
                        {
                            Undo.RecordObject(_pattern, "Add Input");
                            AddInputToGroup(beat, LaneDirs[lane]);
                            _selGroupIdx = -1;
                            _selInputIdx = -1;
                        }
                    }
                }
                e.Use();
            }
            else if (e.button == 1)
            {
                var (hg, hi) = FindInputNearPixel(mp, 12f);
                if (hg >= 0)
                    DeleteInput(hg, hi);
                else
                {
                    int sg = FindSpaceMarkerNearPixel(mp.x, 8f);
                    if (sg >= 0) DeleteGroup(sg);
                }
                e.Use();
            }

            Repaint();
        }

        else if (e.type == EventType.MouseDrag && _isDragging && _selGroupIdx >= 0 && _selInputIdx >= 0)
        {
            float deltaPixels = e.mousePosition.x - _dragOriginMouseX;
            float deltaBeat   = deltaPixels / (_zoom * _pattern.BeatInterval);
            _dragBeatDelta    = SnapBeat(Mathf.Max(0f, _dragOriginBeat + deltaBeat)) - _dragOriginBeat;
            e.Use();
            Repaint();
        }

        else if (e.type == EventType.MouseUp && _isDragging)
        {
            if (_selGroupIdx >= 0 && _selInputIdx >= 0 &&
                _selGroupIdx < _pattern.groups.Count)
            {
                var   grp  = _pattern.groups[_selGroupIdx];
                float fin  = Mathf.Max(0f, _dragOriginBeat + _dragBeatDelta);
                var   orig = grp.inputs[_selInputIdx];

                Undo.RecordObject(_pattern, "Move Input");
                grp.inputs[_selInputIdx] = new DirectionInputEntry(orig.direction, fin);
                grp.inputs.Sort((a, b) => a.beat.CompareTo(b.beat));
                _selInputIdx = grp.inputs.FindIndex(
                    x => x.direction == orig.direction && Mathf.Abs(x.beat - fin) < 0.001f);
                EditorUtility.SetDirty(_pattern);
            }

            _isDragging    = false;
            _dragBeatDelta = 0f;
            GUIUtility.hotControl = 0;
            e.Use();
            Repaint();
        }
    }

    // ── Global key handler (edit mode) ─────────────────────────────────────
    void HandleGlobalKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (_pattern == null || _selGroupIdx < 0) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            if (_selInputIdx >= 0) DeleteInput(_selGroupIdx, _selInputIdx);
            else                   DeleteGroup(_selGroupIdx);

            _selGroupIdx = -1;
            _selInputIdx = -1;
            e.Use();
            Repaint();
        }
    }

    // ── Recording key capture ──────────────────────────────────────────────
    void CaptureRecordingKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        if (e.keyCode == KeyCode.Escape) { StopSession(); e.Use(); return; }
        if (_currentSongTime < 0f) return;   // still in intro

        float beat = _pattern.TimeToBeat(_currentSongTime);

        // Space confirms the current pending group
        if (e.keyCode == KeyCode.Space)
        {
            if (_pendingGroup != null && _pendingGroup.inputs.Count > 0)
            {
                _pendingGroup.spaceBeat = beat;
                Undo.RecordObject(_pattern, "Confirm Group");
                _pattern.groups.Add(_pendingGroup);
                EditorUtility.SetDirty(_pattern);
            }
            // Always start fresh group after Space (even if pending was empty)
            _pendingGroup = new DirectionGroup();
            e.Use();
            return;
        }

        // Arrow / WASD: add to pending group
        ArrowDir? dir = e.keyCode switch
        {
            KeyCode.UpArrow    or KeyCode.W => ArrowDir.Up,
            KeyCode.DownArrow  or KeyCode.S => ArrowDir.Down,
            KeyCode.LeftArrow  or KeyCode.A => ArrowDir.Left,
            KeyCode.RightArrow or KeyCode.D => ArrowDir.Right,
            _                               => (ArrowDir?)null,
        };

        if (dir.HasValue)
        {
            if (_pendingGroup == null) _pendingGroup = new DirectionGroup();
            _pendingGroup.inputs.Add(new DirectionInputEntry(dir.Value, beat));
            e.Use();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    int DirToLane(ArrowDir dir) => dir switch
    {
        ArrowDir.Up    => 0,
        ArrowDir.Down  => 1,
        ArrowDir.Left  => 2,
        ArrowDir.Right => 3,
        _              => 0,
    };

    float SnapBeat(float rawBeat)
    {
        float grid = 1f / SnapDivs[_snapIndex];
        return Mathf.Round(rawBeat / grid) * grid;
    }

    // Returns (groupIdx, inputIdx) of the direction note closest to mp, or (-1,-1)
    (int g, int i) FindInputNearPixel(Vector2 mp, float tolerance)
    {
        if (_pattern.groups == null) return (-1, -1);
        int lane = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        if (lane < 0 || lane >= LANE_COUNT) return (-1, -1);

        ArrowDir targetDir = LaneDirs[lane];
        float bestDx = tolerance;
        int bestG = -1, bestI = -1;

        for (int g = 0; g < _pattern.groups.Count; g++)
        {
            var grp = _pattern.groups[g];
            for (int i = 0; i < grp.inputs.Count; i++)
            {
                if (grp.inputs[i].direction != targetDir) continue;
                float nx = (_pattern.introDelay + _pattern.BeatToTime(grp.inputs[i].beat)) * _zoom;
                float dx = Mathf.Abs(mp.x - nx);
                if (dx < bestDx) { bestDx = dx; bestG = g; bestI = i; }
            }
        }
        return (bestG, bestI);
    }

    // Returns groupIdx of the space marker closest to mouseX, or -1
    int FindSpaceMarkerNearPixel(float mouseX, float tolerance)
    {
        if (_pattern.groups == null) return -1;
        float best = tolerance;
        int   idx  = -1;
        for (int g = 0; g < _pattern.groups.Count; g++)
        {
            if (_pattern.groups[g].spaceBeat < 0f) continue;
            float sx = (_pattern.introDelay + _pattern.BeatToTime(_pattern.groups[g].spaceBeat)) * _zoom;
            float dx = Mathf.Abs(mouseX - sx);
            if (dx < best) { best = dx; idx = g; }
        }
        return idx;
    }

    // Adds a direction input to the group whose range contains beat, or a new group
    void AddInputToGroup(float beat, ArrowDir dir)
    {
        if (_pattern.groups == null) _pattern.groups = new List<DirectionGroup>();

        DirectionGroup target = null;
        foreach (var g in _pattern.groups)
        {
            if (g.inputs.Count == 0) continue;
            float groupStart = g.inputs[0].beat - 0.05f;
            float groupEnd   = g.spaceBeat >= 0f ? g.spaceBeat : float.MaxValue;
            if (beat >= groupStart && beat <= groupEnd)
            {
                target = g;
                break;
            }
        }

        if (target == null)
        {
            target = new DirectionGroup();
            _pattern.groups.Add(target);
        }

        target.inputs.Add(new DirectionInputEntry(dir, beat));
        target.inputs.Sort((a, b) => a.beat.CompareTo(b.beat));
        EditorUtility.SetDirty(_pattern);
    }

    void DeleteInput(int groupIdx, int inputIdx)
    {
        if (groupIdx < 0 || groupIdx >= _pattern.groups.Count) return;
        var grp = _pattern.groups[groupIdx];
        if (inputIdx < 0 || inputIdx >= grp.inputs.Count) return;

        Undo.RecordObject(_pattern, "Delete Input");
        grp.inputs.RemoveAt(inputIdx);
        // Remove group entirely if it has no inputs and no space marker
        if (grp.inputs.Count == 0 && grp.spaceBeat < 0f)
            _pattern.groups.RemoveAt(groupIdx);
        EditorUtility.SetDirty(_pattern);
    }

    void DeleteGroup(int groupIdx)
    {
        if (groupIdx < 0 || groupIdx >= _pattern.groups.Count) return;
        Undo.RecordObject(_pattern, "Delete Group");
        _pattern.groups.RemoveAt(groupIdx);
        EditorUtility.SetDirty(_pattern);
    }

    // ── Session control ────────────────────────────────────────────────────
    void StartSession(bool record)
    {
        if (_pattern?.music == null) return;
        _isRecording     = record;
        _isPlaying       = !record;
        _sessionStart    = EditorApplication.timeSinceStartup;
        _currentSongTime = -_pattern.introDelay;
        _pendingGroup    = record ? new DirectionGroup() : null;
        PlayAudio(_pattern.music);
    }

    void StopSession()
    {
        _pendingGroup = null;   // discard unconfirmed group

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

    void CreateNewPattern()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Pattern Data", "NewPattern", "asset",
            "Choose where to save the new pattern asset");
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<PatternData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        StopSession();
        _pattern = asset;
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
        catch (Exception ex) { Debug.LogWarning($"[BeatMapEditor] Audio preview error: {ex.Message}"); }
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
