using System;
using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods;

/// <summary>
/// Possible actions for how the patcher should behave when patching a file to a ERF/MOD/RIM
/// while that filename already exists in the Override folder.
/// </summary>
public static class OverrideType
{
    /// <summary>Do nothing: don't even check (TSLPatcher default)</summary>
    public const string IGNORE = "ignore";

    /// <summary>Log a warning (HoloPatcher default)</summary>
    public const string WARN = "warn";

    /// <summary>Rename the file in the Override folder with the 'old_' prefix. Also logs a warning.</summary>
    public const string RENAME = "rename";
}

/// <summary>
/// Abstract base class for TSLPatcher modifications.
/// </summary>
public abstract class PatcherModifications
{
    public const string DEFAULT_DESTINATION = "Override";

    public string SourceFile { get; set; }
    public string SourceFolder { get; set; } = ".";
    public string SaveAs { get; set; }
    public bool ReplaceFile { get; set; }
    public string Destination { get; set; } = DEFAULT_DESTINATION;
    public string Action { get; set; } = "Patch ";
    public string OverrideTypeValue { get; set; } = OverrideType.WARN;

    /// <summary>
    /// Determines how !ReplaceFile will be handled.
    /// TSLPatcher's InstallList and CompileList are the only patchlists that handle replace behavior differently.
    /// In InstallList/CompileList, if this is True and !ReplaceFile is False or File#=file_to_install.ext,
    /// the resource will be skipped if the resource already exists.
    /// </summary>
    public bool SkipIfNotReplace { get; set; }

    protected PatcherModifications(string sourcefile, bool? replace = null)
    {
        SourceFile = sourcefile;
        SaveAs = sourcefile;
        ReplaceFile = replace ?? false;
    }

    /// <summary>
    /// Patch the resource defined by the 'source' arg. Returns the bytes data of the result.
    /// If true is returned as an object, skip this resource.
    /// </summary>
    public abstract object PatchResource(
        byte[] source,
        PatcherMemory memory,
        PatchLogger logger,
        Game game);

    /// <summary>
    /// Apply this patch's modifications to the mutable_data object argument passed.
    /// </summary>
    public abstract void Apply(
        object mutableData,
        PatcherMemory memory,
        PatchLogger logger,
        Game game);

    /// <summary>
    /// All optional TSLPatcher vars that can be parsed for a given patch list.
    /// </summary>
    public virtual void PopTslPatcherVars(
        Dictionary<string, string> fileSectionDict,
        string? defaultDestination = null,
        string defaultSourceFolder = ".")
    {
        // Note: The second argument passed to the 'pop' function is the default.

        if (fileSectionDict.TryGetValue("!SourceFile", out string? sourceFile))
        {
            SourceFile = sourceFile;
            fileSectionDict.Remove("!SourceFile");
        }

        // !SaveAs and !Filename are the same
        if (fileSectionDict.TryGetValue("!Filename", out string? filename))
        {
            SaveAs = filename;
            fileSectionDict.Remove("!Filename");
        }
        else if (fileSectionDict.TryGetValue("!SaveAs", out string? saveAs))
        {
            SaveAs = saveAs;
            fileSectionDict.Remove("!SaveAs");
        }

        string destinationFallback = defaultDestination ?? DEFAULT_DESTINATION;
        if (fileSectionDict.TryGetValue("!Destination", out string? destination))
        {
            Destination = destination;
            fileSectionDict.Remove("!Destination");
        }
        else
        {
            Destination = destinationFallback;
        }

        // !ReplaceFile=1 is prioritized, see Stoffe's HLFP mod v2.1 for reference
        if (fileSectionDict.TryGetValue("!ReplaceFile", out string? replaceFile))
        {
            ReplaceFile = ConvertToBool(replaceFile);
            fileSectionDict.Remove("!ReplaceFile");
        }

        // TSLPatcher defaults to "ignore". However realistically, Override file shadowing is
        // a major problem, so HoloPatcher defaults to "warn"
        if (fileSectionDict.TryGetValue("!OverrideType", out string? overrideType))
        {
            OverrideTypeValue = overrideType.ToLowerInvariant();
            fileSectionDict.Remove("!OverrideType");
        }

        if (fileSectionDict.TryGetValue("!SourceFolder", out string? sourceFolder))
        {
            SourceFolder = sourceFolder;
            fileSectionDict.Remove("!SourceFolder");
        }
        else
        {
            SourceFolder = defaultSourceFolder;
        }
    }

    protected static bool ConvertToBool(object value)
    {
        if (value is string str)
        {
            if (str == "1")
            {
                return true;
            }

            if (str == "0")
            {
                return false;
            }

            // Also try parsing as boolean or integer
            if (bool.TryParse(str, out bool boolResult))
            {
                return boolResult;
            }

            if (int.TryParse(str, out int intValue))
            {
                return intValue != 0;
            }

            return str.Trim().ToLower() == "true";
        }
        if (value is bool b)
        {
            return b;
        }

        return false;
    }
}

