using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Mods;
using TSLPatcher.Core.Mods.GFF;
using TSLPatcher.Core.Mods.NSS;
using TSLPatcher.Core.Mods.SSF;
using TSLPatcher.Core.Mods.TLK;
using TSLPatcher.Core.Mods.TwoDA;

namespace TSLPatcher.Core.Config;

/// <summary>
/// Configuration for TSLPatcher operations
/// </summary>
public class PatcherConfig
{
    public string WindowTitle { get; set; } = string.Empty;
    public string ConfirmMessage { get; set; } = string.Empty;
    public int? GameNumber { get; set; }

    public List<string[]> RequiredFiles { get; set; } = new();
    public List<string> RequiredMessages { get; set; } = new();
    public int SaveProcessedScripts { get; set; }
    public LogLevel LogLevel { get; set; } = LogLevel.Warnings;

    // Optional HoloPatcher features
    public bool IgnoreFileExtensions { get; set; }

    // Patch lists
    public List<InstallFile> InstallList { get; set; } = new();
    public List<Modifications2DA> Patches2DA { get; set; } = new();
    public List<ModificationsGFF> PatchesGFF { get; set; } = new();
    public List<ModificationsSSF> PatchesSSF { get; set; } = new();
    public List<ModificationsNSS> PatchesNSS { get; set; } = new();
    public List<ModificationsNCS> PatchesNCS { get; set; } = new();
    public ModificationsTLK PatchesTLK { get; set; } = new();

    public int PatchCount()
    {
        int num2DAPatches = Patches2DA.Sum(twodaPatch => twodaPatch.Modifiers.Count);
        int numGffPatches = FlattenGffPatches().Count;
        int numSsfPatches = PatchesSSF.Sum(ssfPatch => ssfPatch.Modifiers.Count);
        int numTlkPatches = PatchesTLK.Modifiers.Count;
        int numInstallListPatches = InstallList.Count;
        int numNssPatches = PatchesNSS.Count;
        int numNcsPatches = PatchesNCS.Count;

        return num2DAPatches +
               numGffPatches +
               numSsfPatches +
               numTlkPatches +
               numInstallListPatches +
               numNssPatches +
               numNcsPatches;
    }

    /// <summary>
    /// Gets nested GFF patches recursively from a modifier that supports nested modifiers.
    /// </summary>
    public List<ModifyGFF> GetNestedGffPatches(ModifyGFF argGffModifier)
    {
        // Create a copy of modifiers (shallow copy)
        var nestedModifiers = new List<ModifyGFF>();

        // Check if this modifier has nested modifiers
        if (argGffModifier is AddFieldGFF addField && addField.Modifiers != null)
        {
            nestedModifiers.AddRange(addField.Modifiers);
        }
        else if (argGffModifier is AddStructToListGFF addStructToList && addStructToList.Modifiers != null)
        {
            nestedModifiers.AddRange(addStructToList.Modifiers);
        }

        // Recursively get nested modifiers
        var allNested = new List<ModifyGFF>(nestedModifiers);
        foreach (var gffModifier in nestedModifiers.ToList())
        {
            if (gffModifier is AddFieldGFF || gffModifier is AddStructToListGFF)
            {
                allNested.AddRange(GetNestedGffPatches(gffModifier));
            }
        }

        return allNested;
    }

    /// <summary>
    /// Flattens all GFF patches into a single list, resolving nested structures.
    /// </summary>
    public List<ModifyGFF> FlattenGffPatches()
    {
        var flattenedGffPatches = new List<ModifyGFF>();

        foreach (var gffPatch in PatchesGFF)
        {
            foreach (var gffModifier in gffPatch.Modifiers)
            {
                // Skip memory modifiers (they don't count as patches)
                bool isMemoryModifier = gffModifier is Memory2DAModifierGFF;
                if (!isMemoryModifier)
                {
                    flattenedGffPatches.Add(gffModifier);
                }

                // Only AddFieldGFF and AddStructToListGFF have modifiers attribute
                if (gffModifier is AddFieldGFF addField && addField.Modifiers.Count > 0)
                {
                    var nestedModifiers = GetNestedGffPatches(addField);
                    // Nested modifiers will reference the item from the flattened list
                    // Clear and re-add to update the modifier's nested modifiers
                    addField.Modifiers.Clear();
                    addField.Modifiers.AddRange(nestedModifiers);
                    flattenedGffPatches.AddRange(nestedModifiers);
                }
                else if (gffModifier is AddStructToListGFF addStruct && addStruct.Modifiers.Count > 0)
                {
                    var nestedModifiers = GetNestedGffPatches(addStruct);
                    // Clear and re-add to update the modifier's nested modifiers
                    addStruct.Modifiers.Clear();
                    addStruct.Modifiers.AddRange(nestedModifiers);
                    flattenedGffPatches.AddRange(nestedModifiers);
                }
            }
        }

        return flattenedGffPatches;
    }
}

