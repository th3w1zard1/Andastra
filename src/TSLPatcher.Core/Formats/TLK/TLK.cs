using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TSLPatcher.Core.Common;

namespace TSLPatcher.Core.Formats.TLK;

/// <summary>
/// Represents a TLK (talk table) file.
/// </summary>
public class TLK(Common.Language language = Common.Language.English) : IEnumerable<(int stringref, TLKEntry entry)>
{
    public List<TLKEntry> Entries { get; set; } = new();
    public Common.Language Language { get; set; } = language;

    public int Count => Entries.Count;

    public TLKEntry? Get(int stringref)
    {
        return stringref >= 0 && stringref < Entries.Count ? Entries[stringref] : null;
    }

    public int Add(string text, string soundResref = "")
    {
        var entry = new TLKEntry(text, new ResRef(soundResref));
        Entries.Add(entry);
        return Entries.Count - 1;
    }

    public void Replace(int stringref, string? text, string soundResref = "")
    {
        if (stringref < 0 || stringref >= Entries.Count)
        {
            throw new IndexOutOfRangeException($"Cannot replace nonexistent stringref in dialog.tlk: '{stringref}'");
        }

        string oldText = Entries[stringref].Text;
        ResRef oldSound = Entries[stringref].Voiceover;

        // Python line 176: text or old_text - if text is falsy (None, "", etc.), use old_text
        // Python line 176: ResRef(sound_resref) if sound_resref else old_sound
        string newText = string.IsNullOrEmpty(text) ? oldText : text;
        ResRef newSound = string.IsNullOrEmpty(soundResref) ? oldSound : new ResRef(soundResref);

        Entries[stringref] = new TLKEntry(newText, newSound);
    }

    public void Resize(int size)
    {
        if (Entries.Count > size)
        {
            Entries = Entries.GetRange(0, size);
        }
        else
        {
            while (Entries.Count < size)
            {
                Entries.Add(new TLKEntry("", ResRef.FromBlank()));
            }
        }
    }

    public TLKEntry this[int stringref]
    {
        get
        {
            if (stringref < 0 || stringref >= Entries.Count)
            {
                throw new IndexOutOfRangeException($"Stringref {stringref} is out of range");
            }

            return Entries[stringref];
        }
        set
        {
            if (stringref < 0 || stringref >= Entries.Count)
            {
                throw new IndexOutOfRangeException($"Stringref {stringref} is out of range");
            }

            Entries[stringref] = value;
        }
    }

    public IEnumerator<(int stringref, TLKEntry entry)> GetEnumerator()
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            yield return (i, Entries[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the text of a stringref entry.
    /// </summary>
    public string String(int stringref)
    {
        TLKEntry? entry = Get(stringref);
        return entry?.Text ?? "";
    }

    /// <summary>
    /// Saves the TLK to a file.
    /// </summary>
    public void Save(string path)
    {
        var writer = new TLKBinaryWriter(this);
        byte[] data = writer.Write();
        File.WriteAllBytes(path, data);
    }
}

