using System;
using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.TwoDA;

/// <summary>
/// 2DA modification algorithms for TSLPatcher/HoloPatcher.
/// 
/// This module implements 2DA modification logic for applying patches from changes.ini files.
/// Handles row/column additions, cell modifications, and memory token resolution.
/// 
/// References:
/// ----------
///     vendor/TSLPatcher/TSLPatcher.pl - Perl 2DA modification logic (likely unfinished)
///     vendor/Kotor.NET/Kotor.NET.Patcher/ - Incomplete C# patcher
/// </summary>

/// <summary>
/// Container for 2DA file modifications.
/// 1:1 port from Python Modifications2DA in pykotor/tslpatcher/mods/twoda.py
/// </summary>
public class Modifications2DA : PatcherModifications
{
    public new const string DEFAULT_DESTINATION = PatcherModifications.DEFAULT_DESTINATION;
    public static string DefaultDestination => DEFAULT_DESTINATION;

    public static readonly Dictionary<string, int> HardcappedRowLimits = new()
    {
        { "placeables.2da", 256 },
        { "upcrystals.2da", 256 },
        { "upgrade.2da", 32 }
    };

    public List<Modify2DA> Modifiers { get; set; } = new();

    public Modifications2DA(string filename)
        : base(filename)
    {
        Modifiers = new List<Modify2DA>();
    }

    public override object PatchResource(
        byte[] source,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        Formats.TwoDA.TwoDA twoda = new TwoDABinaryReader(source).Load();
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

        foreach (Modify2DA row in Modifiers)
        {
            try
            {
                row.Apply(twoda, memory);
            }
            catch (Exception e)
            {
                string msg = $"{e.Message} when patching the file '{SaveAs}'";
                if (e is WarningError)
                {
                    logger.AddWarning(msg);
                }
                else
                {
                    logger.AddError(msg);
                    break;
                }
            }
        }
        if (game.IsK2())
        {
            return;
        }

        // Exceeding row count maximums will break the game.
        if (HardcappedRowLimits.TryGetValue(SaveAs.ToLowerInvariant(), out int twodaRowLimit))
        {
            int curRowCount = twoda.GetHeight();
            int rowsAdded = curRowCount - twodaRowLimit;
            if (curRowCount > twodaRowLimit)
            {
                throw new InvalidOperationException($"{SaveAs} has a max row count of {twodaRowLimit}. Adding more will break the game. This mod attempted to add {rowsAdded} rows and have not been applied.");
            }
        }
    }
}
