using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks active notes per lane for efficient hit detection.
/// </summary>
public class NoteManager : MonoBehaviour
{
    public static NoteManager Instance { get; private set; }

    // Sorted by HitTime ascending — closest-to-hit is index 0
    private List<Note> groundNotes = new List<Note>();
    private List<Note> airNotes    = new List<Note>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Register(Note note)
    {
        var list = GetList(note.Lane);
        list.Add(note);
        list.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
    }

    public void Unregister(Note note)
    {
        GetList(note.Lane).Remove(note);
    }

    /// <summary>Returns the nearest note in the lane within timeWindow, or null.</summary>
    public Note GetNearest(int lane, float songTime, float timeWindow)
    {
        var list = GetList(lane);
        Note best     = null;
        float bestDiff = float.MaxValue;

        foreach (var note in list)
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

    private List<Note> GetList(int lane) => lane == 0 ? groundNotes : airNotes;
}
