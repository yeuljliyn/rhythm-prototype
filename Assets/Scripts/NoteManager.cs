using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks active notes per lane (0=Left, 1=Down, 2=Up, 3=Right).
/// </summary>
public class NoteManager : MonoBehaviour
{
    static NoteManager _instance;
    public static NoteManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<NoteManager>(); return _instance; }
        private set { _instance = value; }
    }

    readonly List<Note>[] lanes = new List<Note>[4];

    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);

        for (int i = 0; i < 4; i++)
            lanes[i] = new List<Note>();
    }

    public void Register(Note note)
    {
        var list = lanes[note.Lane];
        list.Add(note);
        list.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
    }

    public void Unregister(Note note)
    {
        lanes[note.Lane].Remove(note);
    }

    public Note GetNearest(int lane, float songTime, float timeWindow)
    {
        Note  best     = null;
        float bestDiff = float.MaxValue;

        foreach (var note in lanes[lane])
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
