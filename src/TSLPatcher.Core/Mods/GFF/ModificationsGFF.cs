using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.GFF;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.GFF;

/// <summary>
/// Container for GFF file modifications.
/// 1:1 port from Python ModificationsGFF in pykotor/tslpatcher/mods/gff.py
/// </summary>
public class ModificationsGFF : PatcherModifications
{
    public new const string DEFAULT_DESTINATION = "Override";
    public static string DefaultDestination => DEFAULT_DESTINATION;

    public List<ModifyGFF> Modifiers { get; set; } = new();

    public ModificationsGFF(string filename, bool replace = false, List<ModifyGFF>? modifiers = null)
        : base(filename, replace)
    {
        Modifiers = modifiers ?? new List<ModifyGFF>();
    }

    public override object PatchResource(
        byte[] source,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        var reader = new GFFBinaryReader(source);
        var gff = reader.Load();
        Apply(gff, memory, logger, game);
        var writer = new GFFBinaryWriter(gff);
        return writer.Write();
    }

    public override void Apply(
        object mutableData,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        if (mutableData is Formats.GFF.GFF gff)
        {
            foreach (var modifier in Modifiers)
            {
                modifier.Apply(gff.Root, memory, logger, game);
            }
        }
        else
        {
            logger.AddError($"Expected GFF object for ModificationsGFF, but got {mutableData.GetType().Name}");
        }
    }
}

