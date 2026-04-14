using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks all active beat circles on screen.
/// No lanes — notes are positioned randomly near the boss.
/// </summary>
public class NoteManager : MonoBehaviour
{
    static NoteManager _instance;
    public static NoteManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<NoteManager>(); return _instance; }
        private set { _instance = value; }
    }

    readonly List<Note> _notes = new List<Note>();

    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);
    }

    public void Register(Note note)
    {
        _notes.Add(note);
        _notes.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
    }

    public void Unregister(Note note) => _notes.Remove(note);

    /// <summary>Returns the note closest in time to songTime within the given window.</summary>
    public Note GetNearest(float songTime, float timeWindow)
    {
        Note  best     = null;
        float bestDiff = float.MaxValue;

        foreach (var note in _notes)
        {
            float diff = Mathf.Abs(songTime - note.HitTime);
            if (diff <= timeWindow && diff < bestDiff)
            {
                bestDiff = diff;
                best     = note;
            }
        }
        return best;
    }
}
