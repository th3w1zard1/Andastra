using System.Collections.Generic;
using TSLPatcher.Core.Formats.TLK;

namespace TSLPatcher.Core.Diff;

public class TlkCompareResult
{
    public Dictionary<int, (string? Text, string? Sound)> ChangedEntries { get; } = new();
    public Dictionary<int, (string Text, string Sound)> AddedEntries { get; } = new();
}

public static class TlkDiff
{
    public static TlkCompareResult Compare(TLK original, TLK modified)
    {
        var result = new TlkCompareResult();
        int origCount = original.Count;
        int modCount = modified.Count;

        for (int i = 0; i < modCount; i++)
        {
            if (i >= origCount)
            {
                // Added entry
                TLKEntry entry = modified[i];
                result.AddedEntries[i] = (entry.Text, entry.Voiceover.ToString());
            }
            else
            {
                // Compare existing
                TLKEntry origEntry = original[i];
                TLKEntry modEntry = modified[i];

                string? changedText = null;
                string? changedSound = null;

                if (origEntry.Text != modEntry.Text)
                {
                    changedText = modEntry.Text;
                }

                // Assuming ResRef implements Equals correctly or use ToString()
                if (origEntry.Voiceover.ToString() != modEntry.Voiceover.ToString())
                {
                    changedSound = modEntry.Voiceover.ToString();
                }

                if (changedText != null || changedSound != null)
                {
                    result.ChangedEntries[i] = (changedText, changedSound);
                }
            }
        }

        return result;
    }
}

