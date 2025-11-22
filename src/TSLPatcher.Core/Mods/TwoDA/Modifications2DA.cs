using System;
using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.TwoDA;

/// <summary>
/// Container for 2DA file modifications.
/// 1:1 port from Python Modifications2DA in pykotor/tslpatcher/mods/twoda.py
/// </summary>
public class Modifications2DA : PatcherModifications
{
    public const string DEFAULTDESTINATION = "Override";
    public static string DefaultDestination => DEFAULTDESTINATION;

    public List<Modify2DA> Modifiers { get; set; } = new();

    public Modifications2DA(string filename, List<Modify2DA>? modifiers = null)
        : base(filename, false)
    {
        Modifiers = modifiers ?? new List<Modify2DA>();
    }

    public override object PatchResource(
        byte[] source,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        var twoda = new TwoDABinaryReader(source).Load();
        Apply(twoda, memory, logger, game);
        return new TwoDABinaryWriter(twoda).Write();
    }

    public override void Apply(
        object mutableData,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        if (mutableData is not Core.Formats.TwoDA.TwoDA twoda)
        {
            logger.AddError($"Expected TwoDA object for Modifications2DA, but got {mutableData.GetType().Name}");
            return;
        }

        foreach (var modifier in Modifiers)
        {
            try
            {
                modifier.Apply(twoda, memory);
            }
            catch (WarningError e)
            {
                string msg = $"{e.Message} when patching the file '{SaveAs}'";
                logger.AddWarning(msg);
            }
            catch (Exception e)
            {
                string msg = $"{e.Message} when patching the file '{SaveAs}'";
                logger.AddError(msg);
                break;
            }
        }
    }
}
