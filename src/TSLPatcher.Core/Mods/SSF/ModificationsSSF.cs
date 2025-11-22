using System;
using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.SSF;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.SSF;

/// <summary>
/// Represents a single SSF sound modification.
/// Matches Python ModifySSF class.
/// </summary>
public class ModifySSF
{
    public SSFSound Sound { get; set; }
    public TokenUsage Stringref { get; set; }

    public ModifySSF(SSFSound sound, TokenUsage stringref)
    {
        Sound = sound;
        Stringref = stringref;
    }

    public void Apply(Formats.SSF.SSF ssf, PatcherMemory memory)
    {
        ssf.SetData(Sound, int.Parse(Stringref.Value(memory)));
    }
}

/// <summary>
/// Container for SSF (sound set file) modifications.
/// Matches Python ModificationsSSF class.
/// </summary>
public class ModificationsSSF(string filename, bool replaceFile, bool _noReplacefileCheck = false, List<ModifySSF>? modifiers = null) : PatcherModifications(filename, replaceFile)
{
    public const string DEFAULTDESTINATION = "Override";
    public static string DefaultDestination => DEFAULTDESTINATION;

    public List<ModifySSF> Modifiers { get; set; } = modifiers ?? new List<ModifySSF>();

    public override object PatchResource(
        byte[] source,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        var reader = new SSFBinaryReader(source);
        var ssf = reader.Load();
        Apply(ssf, memory, logger, game);

        var writer = new SSFBinaryWriter(ssf);
        return writer.Write();
    }

    public override void Apply(
        object mutableData,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        if (mutableData is not Formats.SSF.SSF ssf)
        {
            throw new ArgumentException($"Expected SSF object, got {mutableData.GetType().Name}");
        }

        foreach (var modifier in Modifiers)
        {
            modifier.Apply(ssf, memory);
        }
    }
}
