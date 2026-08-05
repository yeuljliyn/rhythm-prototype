using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Decorative SFX cue editor for SfxCuePatternData. Unlike Stage1CallMapEditorWindow, notes carry
/// no Tap/Hold shape and no judgement role — a note is just "play this clip at this beat, on this
/// lane." Four fixed lanes (Q/W/E/R) exist purely so multiple cues can be stamped at/near the same
/// beat without colliding on the timeline; overlap across (or within) lanes is always allowed.
///
/// REC mode: hold nothing, just tap Q/W/E/R while the song plays — each press instantly stamps a
/// note on that lane at the live beat, using whatever clip is currently assigned to that key in the
/// header. Reassigning a key's clip mid-session only affects notes stamped afterward; notes already
/// placed keep the clip they were stamped with (SfxCueEntry.clip is a copy, not a live reference).
/// Edit mode: click a lane = add note (using that lane's current slot clip) / drag = move /
/// right-click = delete. Del = delete selected.
///
/// No audio feedback on stamp: the Editor's preview-audio API (UnityEditor.AudioUtil) only has ONE
/// global playback slot, so playing the stamped clip would have to interrupt the song currently
/// playing (confirmed by reflection — there's no way to layer a second clip on top of it outside
/// Play Mode). That interruption reads as the music glitching, so REC stays silent on stamp; use
/// the "▶ Play" button next to a selected note (edit mode only) to audition one clip at a time.
/// </summary>
public class Stage1SfxCueEditorWindow : EditorWindow
{
    // ── State ──────────────────────────────────────────────────────────────
    SfxCuePatternData _pattern;
    bool   _isRecording;
    bool   _isPlaying;
    double _sessionStart;
    float  _currentSongTime;

    // REC-mode key state (prevents OS auto-repeat from spamming stamps while a key is held)
    readonly bool[] _lineKeyDown = new bool[LANE_COUNT];

    // ── Timeline view ──────────────────────────────────────────────────────
    Vector2 _scrollPos;
    float   _zoom = 120f;

    // ── Selection / drag (edit mode) ───────────────────────────────────────
    SfxCueEntry _selEntry;
    bool   _isDragging;
    float  _dragOriginMouseX;
    float  _dragOriginBeat;
    float  _dragBeatDelta;

    // ── Snap ───────────────────────────────────────────────────────────────
    static readonly int[]    SnapDivs   = { 1, 2, 4, 8, 16 };
    static readonly string[] SnapLabels = { "1/1", "1/2", "1/4", "1/8", "1/16" };
    int _snapIndex = 2;

    // ── Layout constants ───────────────────────────────────────────────────
    const float LABEL_W    = 48f;
    const float RULER_H    = 22f;
    const float LANE_H     = 44f;
    const int   LANE_COUNT = 4; // Q, W, E, R
    const float NOTE_W     = 10f;

    static readonly string[] LaneLabels = { "Q", "W", "E", "R" };
    static readonly KeyCode[] LaneKeys  = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
    static readonly Color[]  LaneColors =
    {
        new Color(1.00f, 0.35f, 0.35f),   // Q – red
        new Color(1.00f, 0.75f, 0.20f),   // W – orange
        new Color(0.35f, 0.85f, 0.45f),   // E – green
        new Color(0.35f, 0.65f, 1.00f),   // R – blue
    };

    // ── Audio reflection ───────────────────────────────────────────────────
    static MethodInfo _playMethod;
    static MethodInfo _stopMethod;

    // ── Menu ───────────────────────────────────────────────────────────────
    [MenuItem("RhythmGame/Sfx Cue Editor")]
    static void Open()
    {
        var w = GetWindow<Stage1SfxCueEditorWindow>("Sfx Cue Editor");
        w.minSize = new Vector2(700f, 340f);
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
                "Select or create a Sfx Cue Pattern Data asset above to get started.",
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
                var next = (SfxCuePatternData)EditorGUILayout.ObjectField(
                    "Sfx Cue Pattern Data", _pattern, typeof(SfxCuePatternData), false);
                if (next != _pattern) { StopSession(); _pattern = next; _selEntry = null; }
                if (GUILayout.Button("New", GUILayout.Width(44))) CreateNewSfxCuePattern();
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

            if (_pattern.keySlots == null || _pattern.keySlots.Length != LANE_COUNT)
                _pattern.keySlots = new AudioClip[LANE_COUNT];

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Key Slots", GUILayout.Width(60));
                for (int i = 0; i < LANE_COUNT; i++)
                {
                    GUI.color = LaneColors[i];
                    EditorGUILayout.LabelField(LaneLabels[i], GUILayout.Width(14));
                    GUI.color = Color.white;
                    _pattern.keySlots[i] = (AudioClip)EditorGUILayout.ObjectField(
                        _pattern.keySlots[i], typeof(AudioClip), false, GUILayout.Width(120));
                }
            }
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_pattern);

            int total = _pattern.cues?.Count ?? 0;
            int[] perLine = new int[LANE_COUNT];
            if (_pattern.cues != null)
                foreach (var c in _pattern.cues)
                    if (c.line >= 0 && c.line < LANE_COUNT) perLine[c.line]++;

            float dur = _pattern.music != null ? _pattern.music.length : 0f;
            string durStr = dur > 0
                ? $"{Mathf.FloorToInt(dur / 60f):0}:{dur % 60f:00.#}"
                : "–";
            EditorGUILayout.LabelField(
                $"Cues: {total}  (Q {perLine[0]} / W {perLine[1]} / E {perLine[2]} / R {perLine[3]})   " +
                $"Duration: {durStr}   Overlap is always allowed.",
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
                        $"Delete all {_pattern.cues?.Count ?? 0} cues?", "Clear", "Cancel"))
                    {
                        Undo.RecordObject(_pattern, "Clear Pattern");
                        _pattern.cues?.Clear();
                        _selEntry = null;
                        EditorUtility.SetDirty(_pattern);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();

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
                    "● REC  —  Q / W / E / R = stamp a cue on that lane using its current Key Slot clip.   Esc = stop",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (!_isPlaying)
            {
                string selInfo = _selEntry != null
                    ? $"   |   Selected: lane {LaneLabels[Mathf.Clamp(_selEntry.line, 0, LANE_COUNT - 1)]} @ beat {_selEntry.beat:F2}  " +
                      $"clip: {(_selEntry.clip != null ? _selEntry.clip.name : "(none)")}"
                    : "";
                EditorGUILayout.LabelField(
                    "Click a lane = add (uses that lane's Key Slot clip)   Drag = move   Right-click = delete   " +
                    "Del = delete selected" + selInfo,
                    EditorStyles.miniLabel);

                if (_selEntry != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Selected note's clip", GUILayout.Width(110));
                        EditorGUI.BeginChangeCheck();
                        var newClip = (AudioClip)EditorGUILayout.ObjectField(
                            _selEntry.clip, typeof(AudioClip), false, GUILayout.Width(160));
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(_pattern, "Change Cue Clip");
                            _selEntry.clip = newClip;
                            EditorUtility.SetDirty(_pattern);
                        }
                        if (GUILayout.Button("▶ Play", GUILayout.Width(50)) && _selEntry.clip != null)
                            PlayAudio(_selEntry.clip);
                    }
                }
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
        DrawNotes();
        DrawPlayhead(contentH);

        if (!_isRecording) HandleTimelineMouseInput(contentRect);

        GUI.EndScrollView();
    }

    void DrawLaneLabels(Rect rect)
    {
        for (int row = 0; row < LANE_COUNT; row++)
        {
            Rect  r = new Rect(rect.x, rect.y + row * LANE_H, rect.width, LANE_H);
            Color c = LaneColors[row];
            EditorGUI.DrawRect(r, new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f));
            GUI.Label(r, LaneLabels[row], new GUIStyle(EditorStyles.boldLabel)
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
        for (int row = 0; row < LANE_COUNT; row++)
        {
            float y  = RULER_H + row * LANE_H;
            Color bg = row % 2 == 0 ? new Color(0.155f, 0.155f, 0.155f) : new Color(0.130f, 0.130f, 0.130f);
            EditorGUI.DrawRect(new Rect(0f, y, totalW, LANE_H), bg);

            if (row < LANE_COUNT - 1)
                EditorGUI.DrawRect(new Rect(0f, y + LANE_H, totalW, 1f), new Color(0f, 0f, 0f, 0.35f));
        }
    }

    float BeatX(float beat) => (_pattern.introDelay + _pattern.BeatToTime(beat)) * _zoom;

    void DrawNotes()
    {
        if (_pattern.cues == null) return;
        foreach (var entry in _pattern.cues)
            DrawNote(entry);
    }

    void DrawNote(SfxCueEntry entry)
    {
        bool  selected = entry == _selEntry;
        bool  dragging = selected && _isDragging;
        float beat     = dragging ? _dragOriginBeat + _dragBeatDelta : entry.beat;

        int   line = Mathf.Clamp(entry.line, 0, LANE_COUNT - 1);
        float y = RULER_H + line * LANE_H + 6f;
        float h = LANE_H - 12f;
        float x = BeatX(beat) - NOTE_W * 0.5f;
        Color col = LaneColors[line];

        if (selected)
        {
            Color outline = dragging ? new Color(1f, 0.9f, 0.1f) : Color.white;
            EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, NOTE_W + 4f, h + 4f), outline);
        }
        EditorGUI.DrawRect(new Rect(x, y, NOTE_W, h), col);
        EditorGUI.DrawRect(new Rect(x + 2f, y + 2f, NOTE_W - 4f, h * 0.4f), new Color(1f, 1f, 1f, 0.35f));

        if (dragging)
            GUI.Label(new Rect(x - 12f, y - 16f, 50f, 14f), $"B {beat:F2}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.9f, 0.1f) }, fontStyle = FontStyle.Bold });
        else if (_zoom > 80f)
        {
            string label = entry.clip != null ? entry.clip.name : "(none)";
            GUI.Label(new Rect(x - 34f, y + h * 0.5f - 7f, 96f, 14f), label,
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } });
        }

        if (!_isDragging)
            EditorGUIUtility.AddCursorRect(new Rect(x - 6f, y, NOTE_W + 12f, h), MouseCursor.MoveArrow);
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

            GUI.FocusControl(null);

            int row = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
            if (row < 0 || row >= LANE_COUNT) return;

            float songTime  = mp.x / _zoom - _pattern.introDelay;
            float clickBeat = SnapBeat(_pattern.TimeToBeat(songTime));

            if (e.button == 0)
            {
                var hit = FindNoteNearPixel(mp, 12f);
                if (hit != null)
                {
                    BeginDrag(hit, mp.x);
                }
                else
                {
                    Undo.RecordObject(_pattern, "Add Sfx Cue");
                    var clip = _pattern.keySlots != null && row < _pattern.keySlots.Length ? _pattern.keySlots[row] : null;
                    var entry = new SfxCueEntry { line = row, beat = clickBeat, clip = clip };
                    AddCue(entry);
                    _selEntry = entry;
                }
                e.Use();
            }
            else if (e.button == 1)
            {
                var hit = FindNoteNearPixel(mp, 12f);
                if (hit != null) DeleteCue(hit);
                e.Use();
            }

            Repaint();
        }
        else if (e.type == EventType.MouseDrag && _isDragging && _selEntry != null)
        {
            float deltaPixels = e.mousePosition.x - _dragOriginMouseX;
            float deltaBeat   = deltaPixels / (_zoom * _pattern.BeatInterval);
            float rawTarget   = _dragOriginBeat + deltaBeat;
            _dragBeatDelta    = SnapBeat(Mathf.Max(0f, rawTarget)) - _dragOriginBeat;
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

    void BeginDrag(SfxCueEntry entry, float mouseX)
    {
        _selEntry         = entry;
        _isDragging        = true;
        _dragOriginMouseX  = mouseX;
        _dragOriginBeat    = entry.beat;
        _dragBeatDelta     = 0f;
        GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
    }

    void CommitDrag()
    {
        if (_selEntry != null)
        {
            Undo.RecordObject(_pattern, "Move Sfx Cue");
            _selEntry.beat = Mathf.Max(0f, _dragOriginBeat + _dragBeatDelta);
            _pattern.cues.Sort((a, b) => a.beat.CompareTo(b.beat));
            EditorUtility.SetDirty(_pattern);
        }

        _isDragging    = false;
        _dragBeatDelta = 0f;
        GUIUtility.hotControl = 0;
    }

    // ── Global key handler (edit mode) ─────────────────────────────────────
    void HandleGlobalKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (_pattern == null) return;
        if (EditorGUIUtility.editingTextField) return;
        if (_selEntry == null) return;

        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            DeleteCue(_selEntry);
            _selEntry = null;
            e.Use();
            Repaint();
        }
    }

    // ── Recording key capture ───────────────────────────────────────────────
    void CaptureRecordingKeys()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopSession(); e.Use(); return; }
        if (_currentSongTime < 0f) return; // still in intro

        int line = Array.IndexOf(LaneKeys, e.keyCode);
        if (line < 0) return;

        if (e.type == EventType.KeyDown)
        {
            if (_lineKeyDown[line]) { e.Use(); return; } // ignore OS auto-repeat while held
            _lineKeyDown[line] = true;
            StampCue(line);
            e.Use();
        }
        else if (e.type == EventType.KeyUp)
        {
            _lineKeyDown[line] = false;
            e.Use();
        }
    }

    void StampCue(int line)
    {
        float beat = SnapBeat(_pattern.TimeToBeat(_currentSongTime));
        var   clip = _pattern.keySlots != null && line < _pattern.keySlots.Length ? _pattern.keySlots[line] : null;
        Undo.RecordObject(_pattern, "Record Sfx Cue");
        AddCue(new SfxCueEntry { line = line, beat = beat, clip = clip });
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    float SnapBeat(float rawBeat)
    {
        float grid = 1f / SnapDivs[_snapIndex];
        return Mathf.Round(rawBeat / grid) * grid;
    }

    void AddCue(SfxCueEntry entry)
    {
        if (_pattern.cues == null) _pattern.cues = new List<SfxCueEntry>();
        _pattern.cues.Add(entry);
        _pattern.cues.Sort((a, b) => a.beat.CompareTo(b.beat));
        EditorUtility.SetDirty(_pattern);
    }

    void DeleteCue(SfxCueEntry entry)
    {
        if (_pattern.cues == null) return;
        Undo.RecordObject(_pattern, "Delete Sfx Cue");
        _pattern.cues.Remove(entry);
        if (_selEntry == entry) _selEntry = null;
        EditorUtility.SetDirty(_pattern);
    }

    SfxCueEntry FindNoteNearPixel(Vector2 mp, float tolerance)
    {
        if (_pattern.cues == null) return null;
        int row = Mathf.FloorToInt((mp.y - RULER_H) / LANE_H);
        if (row < 0 || row >= LANE_COUNT) return null;

        float bestDx = tolerance;
        SfxCueEntry best = null;
        foreach (var entry in _pattern.cues)
        {
            if (entry.line != row) continue;
            float dx = Mathf.Abs(mp.x - BeatX(entry.beat));
            if (dx < bestDx) { bestDx = dx; best = entry; }
        }
        return best;
    }

    // ── Session control ────────────────────────────────────────────────────
    void StartSession(bool record)
    {
        if (_pattern?.music == null) return;
        _isRecording     = record;
        _isPlaying       = !record;
        _sessionStart    = EditorApplication.timeSinceStartup;
        _currentSongTime = -_pattern.introDelay;
        Array.Clear(_lineKeyDown, 0, _lineKeyDown.Length);
        PlayAudio(_pattern.music);
    }

    void StopSession()
    {
        Array.Clear(_lineKeyDown, 0, _lineKeyDown.Length);

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

    void CreateNewSfxCuePattern()
    {
        const string folder = "Assets/SfxCuePatterns";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "SfxCuePatterns");

        string path = EditorUtility.SaveFilePanelInProject(
            "Create Sfx Cue Pattern Data", "NewSfxCuePattern", "asset",
            "Choose where to save the new sfx cue pattern asset", folder);
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<SfxCuePatternData>();
        asset.keySlots = new AudioClip[LANE_COUNT];
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
        catch (Exception ex) { Debug.LogWarning($"[Stage1SfxCueEditor] Audio preview error: {ex.Message}"); }
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
