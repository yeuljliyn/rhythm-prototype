using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class BeatMapEditorWindow : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────
    ChapterData _chapter;
    bool  _isRecording;
    bool  _isPlaying;
    double _sessionStart;
    float _currentSongTime;   // seconds relative to SongTime=0 (negative during intro)

    // ── Timeline view ─────────────────────────────────────────────────
    Vector2 _scrollPos;
    float   _zoom        = 120f;   // pixels per second of audio
    int     _selectedIdx = -1;

    // ── Drag state ────────────────────────────────────────────────────
    bool  _isDragging;
    float _dragOriginMouseX;  // content-space X when drag started
    float _dragOriginBeat;    // note's beat before drag
    float _dragBeatDelta;     // snapped beat offset during drag (applied on release)

    // ── Snap ──────────────────────────────────────────────────────────
    static readonly int[]    SnapDivs   = { 1, 2, 4, 8, 16 };
    static readonly string[] SnapLabels = { "1/1", "1/2", "1/4", "1/8", "1/16" };
    int _snapIndex = 2;

    // ── Layout constants ──────────────────────────────────────────────
    const float LABEL_W      = 64f;
    const float RULER_H      = 22f;
    const float LANE_H       = 38f;
    const int   LANE_COUNT   = 5;
    const float NOTE_W       = 10f;

    // ── Colours & labels ──────────────────────────────────────────────
    static readonly Color[] NoteColors =
    {
        new Color(0.40f, 0.75f, 1.00f),  // Normal  – steel blue
        new Color(0.30f, 0.60f, 1.00f),  // Left    – blue
        new Color(0.30f, 1.00f, 0.45f),  // Down    – green
        new Color(1.00f, 0.90f, 0.20f),  // Up      – yellow
        new Color(1.00f, 0.35f, 0.35f),  // Right   – red
    };
    static readonly string[] LaneLabels = { "SPACE", "←", "↓", "↑", "→" };

    // ── Audio reflection cache ────────────────────────────────────────
    static MethodInfo _playMethod;
    static MethodInfo _stopMethod;

    // ── Menu ──────────────────────────────────────────────────────────
    [MenuItem("RhythmGame/Beat Map Editor %#e")]
    static void Open()
    {
        var w = GetWindow<BeatMapEditorWindow>("Beat Map Editor");
        w.minSize = new Vector2(700f, 440f);
        w.Show();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────
    void OnEnable()  => EditorApplication.update += Tick;
    void OnDisable() { EditorApplication.update -= Tick; StopAudio(); }

    void Tick()
    {
        if (!_isRecording && !_isPlaying) return;
        if (_chapter == null) { StopSession(); return; }

        _currentSongTime = (float)(EditorApplication.timeSinceStartup - _sessionStart)
                           - _chapter.introDelay;

        float musicLen = _chapter.music != null ? _chapter.music.length : 0f;
        if (musicLen > 0f && _currentSongTime > musicLen)
            StopSession();

        // Auto-scroll: keep playhead at 25 % from the left
        float absX      = (_currentSongTime + _chapter.introDelay) * _zoom;
        float viewW     = position.width - LABEL_W - 20f;
        float target    = absX - viewW * 0.25f;
        if (Mathf.Abs(target - _scrollPos.x) > 8f)
            _scrollPos.x = Mathf.Max(0f, target);

        Repaint();
    }

    // ── OnGUI ─────────────────────────────────────────────────────────
    void OnGUI()
    {
        DrawHeader();

        if (_chapter == null)
        {
            EditorGUILayout.HelpBox("Select or create a Chapter Data asset above to get started.", MessageType.Info);
            return;
        }

        DrawToolbar();
        DrawTimeline();

        if (_isRecording)
            CaptureRecordingKeys();
        else
            HandleGlobalKeys();
    }

    void HandleGlobalKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (_chapter == null || _selectedIdx < 0) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            Undo.RecordObject(_chapter, "Delete Note");
            _chapter.notes.RemoveAt(_selectedIdx);
            _selectedIdx = -1;
            _isDragging  = false;
            EditorUtility.SetDirty(_chapter);
            e.Use();
            Repaint();
        }
    }

    // ── Header ────────────────────────────────────────────────────────
    void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // Chapter picker row
            using (new EditorGUILayout.HorizontalScope())
            {
                var next = (ChapterData)EditorGUILayout.ObjectField(
                    "Chapter Data", _chapter, typeof(ChapterData), false);
                if (next != _chapter) { StopSession(); _chapter = next; }

                if (GUILayout.Button("New", GUILayout.Width(44)))
                    CreateNewChapter();
            }

            if (_chapter == null) return;

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                _chapter.chapterName = EditorGUILayout.TextField("Name", _chapter.chapterName);
                GUILayout.Space(8);
                EditorGUILayout.LabelField("BPM", GUILayout.Width(30));
                _chapter.bpm = EditorGUILayout.FloatField(_chapter.bpm, GUILayout.Width(56));
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Intro Delay", GUILayout.Width(72));
                _chapter.introDelay = EditorGUILayout.FloatField(_chapter.introDelay, GUILayout.Width(46));
            }

            _chapter.music = (AudioClip)EditorGUILayout.ObjectField(
                "Music", _chapter.music, typeof(AudioClip), false);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_chapter);

            int   noteCount = _chapter.notes?.Count ?? 0;
            float dur       = _chapter.music != null ? _chapter.music.length : 0f;
            string durStr   = dur > 0
                ? $"{Mathf.FloorToInt(dur / 60f):0}:{dur % 60f:00.#}"
                : "–";
            EditorGUILayout.LabelField(
                $"Notes: {noteCount}   Duration: {durStr}   " +
                $"(drag audio into Music field, then hit ● REC)",
                EditorStyles.miniLabel);
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────────
    void DrawToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.toolbar))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool canPlay = _chapter?.music != null;
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

                GUI.enabled = _chapter != null;
                GUILayout.Space(10);

                if (GUILayout.Button("Clear All", EditorStyles.toolbarButton,
                    GUILayout.Width(68)))
                {
                    if (EditorUtility.DisplayDialog("Clear Notes",
                        $"Delete all {_chapter.notes?.Count ?? 0} notes?", "Clear", "Cancel"))
                    {
                        Undo.RecordObject(_chapter, "Clear Notes");
                        _chapter.notes?.Clear();
                        EditorUtility.SetDirty(_chapter);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();

                // Zoom slider
                EditorGUILayout.LabelField("Zoom", EditorStyles.miniLabel, GUILayout.Width(36));
                _zoom = GUILayout.HorizontalSlider(_zoom, 30f, 500f, GUILayout.Width(90));

                GUILayout.Space(8);
                EditorGUILayout.LabelField("Snap", EditorStyles.miniLabel, GUILayout.Width(32));
                _snapIndex = EditorGUILayout.Popup(_snapIndex, SnapLabels,
                    EditorStyles.toolbarPopup, GUILayout.Width(50));

                GUILayout.Space(8);

                // Playhead time display
                string timeStr = (_isRecording || _isPlaying) && _chapter != null
                    ? $"{Mathf.Max(0, _currentSongTime) / 60f:0}:{Mathf.Max(0, _currentSongTime) % 60f:00.0}  " +
                      $"Beat {Mathf.Max(0f, _chapter.TimeToBeat(_currentSongTime)):F1}"
                    : "--:--.--";
                EditorGUILayout.LabelField(timeStr, EditorStyles.miniLabel,
                    GUILayout.Width(140));
            }

            // Recording hint bar
            if (_isRecording)
            {
                GUI.color = new Color(1f, 0.9f, 0.2f);
                EditorGUILayout.LabelField(
                    "● REC  —  SPACE / Z = normal   ← ↓ ↑ → = arrow notes   Esc = stop   " +
                    "Keep this window focused!",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (!_isPlaying)
            {
                string selInfo = _selectedIdx >= 0 && _chapter?.notes != null
                    ? $"   |   Selected: Beat {_chapter.notes[_selectedIdx].beat:F2}"
                    : "";
                EditorGUILayout.LabelField(
                    "Left-click = add note   Drag note = move   Right-click = delete   " +
                    "Del = delete selected   Ctrl+Z = undo" + selInfo,
                    EditorStyles.miniLabel);
            }
        }
    }

    // ── Timeline ──────────────────────────────────────────────────────
    void DrawTimeline()
    {
        if (_chapter == null) return;

        // Reserve remaining window area for the timeline
        Rect full = GUILayoutUtility.GetRect(
            GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (full.height < 20f) return;

        float totalAudio  = Mathf.Max(10f, _chapter.music != null ? _chapter.music.length : 60f);
        float contentW    = (_chapter.introDelay + totalAudio) * _zoom + 120f;
        float contentH    = RULER_H + LANE_H * LANE_COUNT;

        // Fixed lane labels (left strip, below ruler)
        Rect labelsRect = new Rect(full.x, full.y + RULER_H, LABEL_W, LANE_H * LANE_COUNT);
        DrawLaneLabels(labelsRect);

        // Scrollable area to the right of the labels
        Rect scrollRect  = new Rect(full.x + LABEL_W, full.y,
                                    full.width - LABEL_W, full.height);
        Rect contentRect = new Rect(0f, 0f, contentW, contentH);

        _scrollPos = GUI.BeginScrollView(scrollRect, _scrollPos, contentRect);

        // Background
        EditorGUI.DrawRect(new Rect(0, 0, contentW, contentH),
            new Color(0.12f, 0.12f, 0.12f));

        DrawRulerAndGrid(contentW, totalAudio);
        DrawLaneBackgrounds(contentW);
        DrawNotes();
        DrawPlayhead(contentH);
        HandleTimelineMouseInput(contentRect);

        GUI.EndScrollView();
    }

    void DrawLaneLabels(Rect rect)
    {
        for (int i = 0; i < LANE_COUNT; i++)
        {
            Rect r = new Rect(rect.x, rect.y + i * LANE_H, rect.width, LANE_H);
            Color c = NoteColors[i];
            EditorGUI.DrawRect(r, new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f));

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = c }
            };
            GUI.Label(r, LaneLabels[i], style);
        }
    }

    void DrawRulerAndGrid(float totalW, float totalAudio)
    {
        // Ruler background
        EditorGUI.DrawRect(new Rect(0, 0, totalW, RULER_H),
            new Color(0.08f, 0.08f, 0.08f));

        // Intro shading
        float introW = _chapter.introDelay * _zoom;
        EditorGUI.DrawRect(new Rect(0, 0, introW, RULER_H + LANE_H * LANE_COUNT),
            new Color(0f, 0f, 0f, 0.35f));
        if (introW > 20f)
            GUI.Label(new Rect(4f, 2f, 50f, RULER_H - 4f), "intro",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } });

        if (_chapter.bpm <= 0f) return;

        float beatInterval = _chapter.BeatInterval;
        int   totalBeats   = Mathf.CeilToInt(totalAudio / beatInterval) + 2;

        for (int i = 0; i <= totalBeats; i++)
        {
            float x = (_chapter.introDelay + i * beatInterval) * _zoom;
            if (x > totalW) break;

            bool isMeasure = (i % 4 == 0);

            // Vertical grid line
            float lineAlpha = isMeasure ? 0.55f : 0.20f;
            EditorGUI.DrawRect(new Rect(x, RULER_H, 1f, LANE_H * LANE_COUNT),
                new Color(0.6f, 0.6f, 0.6f, lineAlpha));

            // Ruler tick
            if (isMeasure)
            {
                EditorGUI.DrawRect(new Rect(x, 0f, 1f, RULER_H),
                    new Color(0.65f, 0.65f, 0.65f));
                GUI.Label(new Rect(x + 3f, 2f, 44f, RULER_H - 4f),
                    $"M{i / 4 + 1}",
                    new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } });
            }
            else
            {
                EditorGUI.DrawRect(new Rect(x, RULER_H - 6f, 1f, 6f),
                    new Color(0.40f, 0.40f, 0.40f));
            }
        }
    }

    void DrawLaneBackgrounds(float totalW)
    {
        for (int i = 0; i < LANE_COUNT; i++)
        {
            float y  = RULER_H + i * LANE_H;
            Color bg = i % 2 == 0
                ? new Color(0.155f, 0.155f, 0.155f)
                : new Color(0.130f, 0.130f, 0.130f);
            EditorGUI.DrawRect(new Rect(0f, y, totalW, LANE_H), bg);
        }
    }

    void DrawNotes()
    {
        if (_chapter.notes == null) return;

        for (int i = 0; i < _chapter.notes.Count; i++)
        {
            NoteEntry note     = _chapter.notes[i];
            int       lane     = (int)note.type;
            bool      dragging = (i == _selectedIdx && _isDragging);

            float displayBeat = dragging ? _dragOriginBeat + _dragBeatDelta : note.beat;
            float absTime     = (_chapter.introDelay + _chapter.BeatToTime(displayBeat)) * _zoom;
            float x           = absTime - NOTE_W * 0.5f;
            float y           = RULER_H + lane * LANE_H + 5f;
            float h           = LANE_H - 10f;
            Color c           = NoteColors[lane];

            // Selection / drag outline
            if (i == _selectedIdx)
            {
                Color outlineColor = dragging ? new Color(1f, 0.9f, 0.1f) : Color.white;
                EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, NOTE_W + 4f, h + 4f), outlineColor);
            }

            // Note body (slightly transparent while dragging)
            Color bodyColor = dragging ? new Color(c.r, c.g, c.b, 0.65f) : c;
            EditorGUI.DrawRect(new Rect(x, y, NOTE_W, h), bodyColor);

            // Shine
            EditorGUI.DrawRect(new Rect(x + 2f, y + 2f, NOTE_W - 4f, h * 0.4f),
                new Color(Mathf.Min(c.r + 0.4f, 1f),
                          Mathf.Min(c.g + 0.4f, 1f),
                          Mathf.Min(c.b + 0.4f, 1f), 0.45f));

            // Beat label while dragging
            if (dragging)
            {
                string label = $"B {displayBeat:F2}";
                GUI.Label(new Rect(x - 12f, y - 16f, 50f, 14f), label,
                    new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });
            }

            // Cursor hint: move arrow when hovering over a note
            if (!_isRecording && !_isDragging)
                EditorGUIUtility.AddCursorRect(
                    new Rect(x - 6f, y, NOTE_W + 12f, h), MouseCursor.MoveArrow);
        }
    }

    void DrawPlayhead(float contentH)
    {
        if (!_isRecording && !_isPlaying) return;
        float x = (_currentSongTime + _chapter.introDelay) * _zoom;
        EditorGUI.DrawRect(new Rect(x - 1f, 0f, 2f, contentH),
            new Color(1f, 0.92f, 0.15f, 0.90f));
        // Small triangle / notch at top
        EditorGUI.DrawRect(new Rect(x - 4f, 0f, 8f, 4f),
            new Color(1f, 0.92f, 0.15f));
    }

    // ── Timeline mouse interaction ─────────────────────────────────────
    void HandleTimelineMouseInput(Rect contentRect)
    {
        Event e = Event.current;

        // ── Mouse Down ────────────────────────────────────────────────
        if (e.type == EventType.MouseDown)
        {
            Vector2 mp = e.mousePosition;
            if (!contentRect.Contains(mp) || mp.y < RULER_H) return;

            int lane = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
            if (lane < 0 || lane >= LANE_COUNT) return;

            float songTime = mp.x / _zoom - _chapter.introDelay;

            if (e.button == 0 && !_isRecording)
            {
                // Check if clicking on an existing note (12 px tolerance)
                int hitIdx = FindNoteNearPixel(mp, 12f);

                if (hitIdx >= 0)
                {
                    // Select + start drag
                    _selectedIdx       = hitIdx;
                    _isDragging        = true;
                    _dragOriginMouseX  = mp.x;
                    _dragOriginBeat    = _chapter.notes[hitIdx].beat;
                    _dragBeatDelta     = 0f;
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                }
                else
                {
                    // Empty space → add note
                    float beat = SnapBeat(_chapter.TimeToBeat(songTime));
                    if (beat >= 0f)
                    {
                        Undo.RecordObject(_chapter, "Add Note");
                        AddNote(beat, (NoteType)lane);
                        // Select newly added note
                        _selectedIdx = _chapter.notes.FindIndex(
                            n => n.type == (NoteType)lane && Mathf.Abs(n.beat - beat) < 0.001f);
                    }
                }
                e.Use();
            }
            else if (e.button == 1)
            {
                // Right-click → delete
                int idx = FindNoteNearPixel(mp, 12f);
                if (idx >= 0)
                {
                    Undo.RecordObject(_chapter, "Delete Note");
                    _chapter.notes.RemoveAt(idx);
                    EditorUtility.SetDirty(_chapter);
                    if (_selectedIdx == idx) _selectedIdx = -1;
                    else if (_selectedIdx > idx) _selectedIdx--;
                    _isDragging = false;
                }
                e.Use();
            }

            Repaint();
        }

        // ── Mouse Drag ────────────────────────────────────────────────
        else if (e.type == EventType.MouseDrag && _isDragging && _selectedIdx >= 0)
        {
            float deltaPixels  = e.mousePosition.x - _dragOriginMouseX;
            float deltaBeat    = deltaPixels / (_zoom * _chapter.BeatInterval);
            float rawBeat      = _dragOriginBeat + deltaBeat;
            float snappedBeat  = SnapBeat(Mathf.Max(0f, rawBeat));
            _dragBeatDelta     = snappedBeat - _dragOriginBeat;
            e.Use();
            Repaint();
        }

        // ── Mouse Up ──────────────────────────────────────────────────
        else if (e.type == EventType.MouseUp && _isDragging)
        {
            if (_selectedIdx >= 0 && _chapter.notes != null)
            {
                float finalBeat  = Mathf.Max(0f, _dragOriginBeat + _dragBeatDelta);
                NoteType type    = _chapter.notes[_selectedIdx].type;

                Undo.RecordObject(_chapter, "Move Note");
                var entry = _chapter.notes[_selectedIdx];
                entry.beat = finalBeat;
                _chapter.notes[_selectedIdx] = entry;
                _chapter.notes.Sort((a, b) => a.beat.CompareTo(b.beat));

                // Restore selection to the moved note
                _selectedIdx = _chapter.notes.FindIndex(
                    n => n.type == type && Mathf.Abs(n.beat - finalBeat) < 0.001f);

                EditorUtility.SetDirty(_chapter);
            }

            _isDragging   = false;
            _dragBeatDelta = 0f;
            GUIUtility.hotControl = 0;
            e.Use();
            Repaint();
        }
    }

    // Pixel-distance note search (ignores lane if mp.y is on top of note)
    int FindNoteNearPixel(Vector2 mp, float pixelTolerance)
    {
        if (_chapter.notes == null) return -1;

        int   lane    = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        int   best    = -1;
        float bestDx  = pixelTolerance;

        for (int i = 0; i < _chapter.notes.Count; i++)
        {
            NoteEntry n = _chapter.notes[i];
            if ((int)n.type != lane) continue;
            float noteX = (_chapter.introDelay + _chapter.BeatToTime(n.beat)) * _zoom;
            float dx    = Mathf.Abs(mp.x - noteX);
            if (dx < bestDx) { bestDx = dx; best = i; }
        }
        return best;
    }

    // ── Recording key capture ─────────────────────────────────────────
    void CaptureRecordingKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        if (e.keyCode == KeyCode.Escape) { StopSession(); e.Use(); return; }
        if (_currentSongTime < 0f) return;  // still in intro

        NoteType? t = null;
        switch (e.keyCode)
        {
            case KeyCode.Space:      case KeyCode.Z:          t = NoteType.Normal;     break;
            case KeyCode.LeftArrow:  case KeyCode.A:          t = NoteType.ArrowLeft;  break;
            case KeyCode.DownArrow:  case KeyCode.S:          t = NoteType.ArrowDown;  break;
            case KeyCode.UpArrow:    case KeyCode.W:          t = NoteType.ArrowUp;    break;
            case KeyCode.RightArrow: case KeyCode.D:          t = NoteType.ArrowRight; break;
        }

        if (t.HasValue)
        {
            float beat = SnapBeat(_chapter.TimeToBeat(_currentSongTime));
            if (beat >= 0f)
            {
                Undo.RecordObject(_chapter, "Record Note");
                AddNote(beat, t.Value);
            }
            e.Use();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    float SnapBeat(float rawBeat)
    {
        float grid = 1f / SnapDivs[_snapIndex];
        return Mathf.Round(rawBeat / grid) * grid;
    }

    void AddNote(float beat, NoteType type)
    {
        if (_chapter?.notes == null) return;
        _chapter.notes.RemoveAll(n => n.type == type && Mathf.Abs(n.beat - beat) < 0.001f);
        _chapter.notes.Add(new NoteEntry(beat, type));
        _chapter.notes.Sort((a, b) => a.beat.CompareTo(b.beat));
        EditorUtility.SetDirty(_chapter);
    }

    void StartSession(bool record)
    {
        if (_chapter?.music == null) return;
        _isRecording     = record;
        _isPlaying       = !record;
        _sessionStart    = EditorApplication.timeSinceStartup;
        _currentSongTime = -_chapter.introDelay;
        PlayAudio(_chapter.music);
    }

    void StopSession()
    {
        bool wasDirty = _isRecording || _isPlaying;
        _isRecording = false;
        _isPlaying   = false;
        StopAudio();
        if (wasDirty && _chapter != null)
        {
            EditorUtility.SetDirty(_chapter);
            AssetDatabase.SaveAssets();
        }
    }

    void CreateNewChapter()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Chapter Data", "NewChapter", "asset",
            "Choose where to save the new chapter asset");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<ChapterData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        StopSession();
        _chapter = asset;
    }

    // ── Audio preview via reflection ──────────────────────────────────
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
                // Unity 2020+
                _playMethod = util.GetMethod("PlayPreviewClip",
                    BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                // Older fallback
                if (_playMethod == null)
                    _playMethod = util.GetMethod("PlayClip",
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
