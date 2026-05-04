using System;
using UnityEngine;

[Serializable]
public struct NoteEntry
{
    public float    beat;
    public NoteType type;

    public NoteEntry(float beat, NoteType type)
    {
        this.beat = beat;
        this.type = type;
    }
}
