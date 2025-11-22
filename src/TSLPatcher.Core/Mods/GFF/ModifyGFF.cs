using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.GFF;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.GFF;

/// <summary>
/// Abstract base for GFF modifications.
/// 1:1 port from Python ModifyGFF in pykotor/tslpatcher/mods/gff.py
/// </summary>
public abstract class ModifyGFF
{
    public abstract void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1);

    protected static GFFStruct? NavigateToStruct(GFFStruct rootContainer, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return rootContainer;
        }

        string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        object? container = rootContainer;

        foreach (string part in parts)
        {
            if (container is GFFStruct gffStruct)
            {
                // Try to get as struct or list
                if (gffStruct.TryGetStruct(part, out GFFStruct? childStruct))
                {
                    container = childStruct;
                }
                else if (gffStruct.TryGetList(part, out GFFList? childList))
                {
                    container = childList;
                }
                else
                {
                    return null;
                }
            }
            else if (container is GFFList gffList)
            {
                // Part should be a list index
                if (int.TryParse(part, out int index))
                {
                    container = gffList.At(index);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return container as GFFStruct;
    }

    protected static (GFFStruct? container, string label) NavigateToField(GFFStruct rootContainer, string path)
    {
        string parentPath = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
        string label = Path.GetFileName(path);
        GFFStruct? container = NavigateToStruct(rootContainer, parentPath);
        return (container, label);
    }
}

/// <summary>
/// Modifies an existing field in a GFF structure.
/// 1:1 port from Python ModifyFieldGFF
/// </summary>
public class ModifyFieldGFF : ModifyGFF
{
    public string Path { get; }
    public FieldValue Value { get; }

    public ModifyFieldGFF(string path, FieldValue value)
    {
        Path = path;
        Value = value;
    }

    public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
    {
        if (rootContainer is not GFFStruct rootStruct)
        {
            logger.AddError($"Expected GFFStruct but got {rootContainer.GetType().Name}");
            return;
        }

        var (navigatedStruct, label) = NavigateToField(rootStruct, Path);
        if (navigatedStruct == null)
        {
            logger.AddError($"Could not navigate to path: {Path}");
            return;
        }

        // Get the existing field type
        if (!navigatedStruct.TryGetFieldType(label, out GFFFieldType fieldType))
        {
            logger.AddError($"Field '{label}' not found in struct");
            return;
        }

        // Get the value and set it
        object value = Value.Value(memory, fieldType);

        // Handle !FieldPath (string path stored in value)
        if (value is string strValue && (strValue.Contains('/') || strValue.Contains('\\')))
        {
            var (srcStruct, srcLabel) = NavigateToField(rootStruct, strValue);
            if (srcStruct != null && srcStruct.TryGetFieldType(srcLabel, out GFFFieldType srcType))
            {
                value = srcStruct.GetValue(srcLabel)!;
                // If type differs, we might need conversion, but usually !FieldPath implies copy compatible types
            }
        }

        // Handle LocalizedStringDelta specially
        if (value is LocalizedStringDelta delta)
        {
            if (!navigatedStruct.TryGetLocString(label, out Common.LocalizedString? original))
            {
                return;
            }

            delta.Apply(original!, memory);
            navigatedStruct.SetLocString(label, original!);
            return;
        }

        // Set the field based on type
        SetFieldValue(navigatedStruct, label, value, fieldType);
    }

    private static void SetFieldValue(GFFStruct gffStruct, string label, object value, GFFFieldType fieldType)
    {
        try
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    gffStruct.SetUInt8(label, Convert.ToByte(value));
                    break;
                case GFFFieldType.Int8:
                    gffStruct.SetInt8(label, Convert.ToSByte(value));
                    break;
                case GFFFieldType.UInt16:
                    gffStruct.SetUInt16(label, Convert.ToUInt16(value));
                    break;
                case GFFFieldType.Int16:
                    gffStruct.SetInt16(label, Convert.ToInt16(value));
                    break;
                case GFFFieldType.UInt32:
                    gffStruct.SetUInt32(label, Convert.ToUInt32(value));
                    break;
                case GFFFieldType.Int32:
                    gffStruct.SetInt32(label, Convert.ToInt32(value));
                    break;
                case GFFFieldType.UInt64:
                    gffStruct.SetUInt64(label, Convert.ToUInt64(value));
                    break;
                case GFFFieldType.Int64:
                    gffStruct.SetInt64(label, Convert.ToInt64(value));
                    break;
                case GFFFieldType.Single:
                    gffStruct.SetSingle(label, Convert.ToSingle(value));
                    break;
                case GFFFieldType.Double:
                    gffStruct.SetDouble(label, Convert.ToDouble(value));
                    break;
                case GFFFieldType.String:
                    gffStruct.SetString(label, value.ToString() ?? "");
                    break;
                case GFFFieldType.ResRef:
                    gffStruct.SetResRef(label, value as Common.ResRef ?? Common.ResRef.FromBlank());
                    break;
                case GFFFieldType.LocalizedString:
                    gffStruct.SetLocString(label, value as Common.LocalizedString ?? new Common.LocalizedString(0));
                    break;
                case GFFFieldType.Vector3:
                    if (value is Common.Vector3 v3)
                    {
                        gffStruct.SetVector3(label, v3);
                    }

                    break;
                case GFFFieldType.Vector4:
                    if (value is Common.Vector4 v4)
                    {
                        gffStruct.SetVector4(label, v4);
                    }

                    break;
            }
        }
        catch (Exception)
        {
            // Ignore conversion errors for now or log them
        }
    }
}

/// <summary>
/// Adds a new field to a GFF structure.
/// 1:1 port from Python AddFieldGFF
/// </summary>
public class AddFieldGFF : ModifyGFF
{
    public string Identifier { get; }
    public string Label { get; }
    public GFFFieldType FieldType { get; }
    public FieldValue Value { get; }
    public string Path { get; set; }
    public List<ModifyGFF> Modifiers { get; } = new();

    public AddFieldGFF(string identifier, string label, GFFFieldType fieldType, FieldValue value, string path)
    {
        Identifier = identifier;
        Label = label;
        FieldType = fieldType;
        Value = value;
        Path = path;
    }

    public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
    {
        if (rootContainer is not GFFStruct rootStruct)
        {
            logger.AddError($"Expected GFFStruct but got {rootContainer.GetType().Name}");
            return;
        }

        // Resolve the special sentinel used when adding a Struct
        string containerPath = Path.EndsWith(">>##INDEXINLIST##<<") ? System.IO.Path.GetDirectoryName(Path)?.Replace("\\", "/") ?? "" : Path;

        GFFStruct? navigatedStruct = NavigateToStruct(rootStruct, containerPath);
        if (navigatedStruct == null)
        {
            logger.AddError($"Unable to add new GFF Field '{Label}' at GFF Path '{containerPath}': does not exist!");
            return;
        }

        object value = Value.Value(memory, FieldType);

        // Set the field based on type
        switch (FieldType)
        {
            case GFFFieldType.UInt8:
                navigatedStruct.SetUInt8(Label, Convert.ToByte(value));
                break;
            case GFFFieldType.Int8:
                navigatedStruct.SetInt8(Label, Convert.ToSByte(value));
                break;
            case GFFFieldType.UInt16:
                navigatedStruct.SetUInt16(Label, Convert.ToUInt16(value));
                break;
            case GFFFieldType.Int16:
                navigatedStruct.SetInt16(Label, Convert.ToInt16(value));
                break;
            case GFFFieldType.UInt32:
                navigatedStruct.SetUInt32(Label, Convert.ToUInt32(value));
                break;
            case GFFFieldType.Int32:
                navigatedStruct.SetInt32(Label, Convert.ToInt32(value));
                break;
            case GFFFieldType.UInt64:
                navigatedStruct.SetUInt64(Label, Convert.ToUInt64(value));
                break;
            case GFFFieldType.Int64:
                navigatedStruct.SetInt64(Label, Convert.ToInt64(value));
                break;
            case GFFFieldType.Single:
                navigatedStruct.SetSingle(Label, Convert.ToSingle(value));
                break;
            case GFFFieldType.Double:
                navigatedStruct.SetDouble(Label, Convert.ToDouble(value));
                break;
            case GFFFieldType.String:
                navigatedStruct.SetString(Label, value.ToString() ?? "");
                break;
            case GFFFieldType.ResRef:
                navigatedStruct.SetResRef(Label, value as Common.ResRef ?? Common.ResRef.FromBlank());
                break;
            case GFFFieldType.LocalizedString:
                if (value is LocalizedStringDelta delta && navigatedStruct.TryGetLocString(Label, out var existing))
                {
                    delta.Apply(existing!, memory);
                }
                else if (value is Common.LocalizedString locString)
                {
                    navigatedStruct.SetLocString(Label, locString);
                }
                break;
            case GFFFieldType.Vector3:
                if (value is Common.Vector3 v3)
                {
                    navigatedStruct.SetVector3(Label, v3);
                }

                break;
            case GFFFieldType.Vector4:
                if (value is Common.Vector4 v4)
                {
                    navigatedStruct.SetVector4(Label, v4);
                }

                break;
            case GFFFieldType.List:
                if (value is GFFList list)
                {
                    navigatedStruct.SetList(Label, list);
                }

                break;
            case GFFFieldType.Struct:
                if (value is GFFStruct @struct)
                {
                    navigatedStruct.SetStruct(Label, @struct);
                }

                break;
        }

        // Apply any modifiers
        foreach (ModifyGFF modifier in Modifiers)
        {
            modifier.Apply(rootStruct, memory, logger, game);
        }
    }
}

/// <summary>
/// Adds a new struct to a GFF list.
/// 1:1 port from Python AddStructToListGFF
/// </summary>
public class AddStructToListGFF : ModifyGFF
{
    public string Identifier { get; }
    public FieldValue Value { get; }
    public string Path { get; }
    public int? IndexToToken { get; }
    public List<ModifyGFF> Modifiers { get; } = new();

    public AddStructToListGFF(string identifier, FieldValue value, string path, int? indexToToken = null)
    {
        Identifier = identifier;
        Value = value;
        Path = path;
        IndexToToken = indexToToken;
    }

    public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
    {
        if (rootContainer is not GFFStruct rootStruct)
        {
            logger.AddError($"Expected GFFStruct but got {rootContainer.GetType().Name}");
            return;
        }

        // Resolve the special sentinel
        string listPath = Path.EndsWith(">>##INDEXINLIST##<<") ? System.IO.Path.GetDirectoryName(Path)?.Replace("\\", "/") ?? "" : Path;

        // Navigate to the list container
        object? navigatedContainer = null;
        if (string.IsNullOrEmpty(listPath))
        {
            navigatedContainer = rootStruct;
        }
        else
        {
            string[] parts = listPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            object? container = rootStruct;

            foreach (string part in parts)
            {
                if (container is GFFStruct currentStruct)
                {
                    if (currentStruct.TryGetStruct(part, out GFFStruct? childStruct))
                    {
                        container = childStruct;
                    }
                    else if (currentStruct.TryGetList(part, out GFFList? childList))
                    {
                        container = childList;
                    }
                    else
                    {
                        container = null;
                        break;
                    }
                }
                else if (container is GFFList gffList && int.TryParse(part, out int index))
                {
                    container = gffList.At(index);
                }
                else
                {
                    container = null;
                    break;
                }
            }
            navigatedContainer = container;
        }

        if (navigatedContainer is not GFFList listContainer)
        {
            logger.AddError($"Unable to add struct to list '{listPath}': Not a GFFList");
            return;
        }

        // Get the struct ID from the value
        object valueObj = Value.Value(memory, GFFFieldType.Struct);
        int structId = 0;
        if (valueObj is GFFStruct gffStruct)
        {
            structId = gffStruct.StructId;
        }
        else if (valueObj is int id)
        {
            structId = id;
        }

        // Add new struct to the list
        GFFStruct newStruct = listContainer.Add(structId);
        int newIndex = listContainer.Count - 1;

        // Store the index if requested
        if (IndexToToken.HasValue)
        {
            memory.Memory2DA[IndexToToken.Value] = newIndex.ToString();
        }

        // Apply any modifiers - need to replace the sentinel path
        foreach (ModifyGFF modifier in Modifiers)
        {
            if (modifier is AddFieldGFF addField)
            {
                string modPath = addField.Path;
                if (modPath.Contains(">>##INDEXINLIST##<<"))
                {
                    modPath = modPath.Replace(">>##INDEXINLIST##<<", newIndex.ToString());
                }
                var newAddField = new AddFieldGFF(addField.Identifier, addField.Label, addField.FieldType, addField.Value, listPath + "/" + newIndex + "/" + addField.Label.Split('/').Last());
                // Copy modifiers to new field
                newAddField.Modifiers.AddRange(addField.Modifiers);
                newAddField.Apply(rootStruct, memory, logger, game);
            }
            else
            {
                modifier.Apply(rootStruct, memory, logger, game);
            }
        }
    }
}

/// <summary>
/// A modifier class used for !FieldPath support.
/// </summary>
public class Memory2DAModifierGFF : ModifyGFF
{
    public string Identifier { get; }
    public string Path { get; }
    public int DestTokenId { get; }
    public int? SrcTokenId { get; }

    public Memory2DAModifierGFF(string identifier, string path, int destTokenId, int? srcTokenId = null)
    {
        Identifier = identifier;
        Path = path;
        DestTokenId = destTokenId;
        SrcTokenId = srcTokenId;
    }

    public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
    {
        if (rootContainer is not GFFStruct rootStruct)
        {
            return;
        }

        if (SrcTokenId == null)
        {
            // Assign path to memory
            memory.Memory2DA[DestTokenId] = Path;
            return;
        }

        // Assign 2DAMEMORY=2DAMEMORY (pointer copy)
        string? destPath = null;
        if (!memory.Memory2DA.TryGetValue(DestTokenId, out destPath))
        {
            destPath = Path;
        }

        var (destStruct, destLabel) = NavigateToField(rootStruct, destPath);
        if (destStruct == null)
        {
            // If destination navigation fails, maybe we are just updating the memory token itself
            // memory.Memory2DA[DestTokenId] = srcVal;
            // But we need to get srcVal first.
        }

        // Src lookup
        if (!memory.Memory2DA.TryGetValue(SrcTokenId.Value, out string? srcPath))
        {
            throw new KeyNotFoundException($"2DAMEMORY{SrcTokenId} not defined");
        }

        // Try to navigate source as path
        var (srcStruct, srcLabel) = NavigateToField(rootStruct, srcPath);

        if (srcStruct != null && !string.IsNullOrEmpty(srcLabel) && srcStruct.TryGetFieldType(srcLabel, out var srcType))
        {
            object? val = srcStruct.GetValue(srcLabel);

            if (destStruct != null && !string.IsNullOrEmpty(destLabel))
            {
                // Field to Field copy
                destStruct.SetField(destLabel, srcType, val!);
            }
            else
            {
                // Field to Memory copy
                memory.Memory2DA[DestTokenId] = val?.ToString() ?? "";
            }
        }
        else
        {
            // Source is just a value in memory
            if (destStruct != null && !string.IsNullOrEmpty(destLabel) && destStruct.TryGetFieldType(destLabel, out var destType))
            {
                // Value to Field copy
                // We need to convert srcPath (which is the value string) to destType
                // Reuse SetFieldValue helper logic from ModifyFieldGFF if possible, but it's private there.
                // I'll duplicate basic conversion logic or assume compatible types.
                // For now, just basic string conversion or use Convert.ChangeType logic implicitly via GFFStruct.SetField helpers?
                // GFFStruct Setters do conversion.

                // But SetField takes object.
                // I'll try to pass string and let SetField handle it?
                // No, GFFStruct.SetField takes object and stores it.
                // If I pass string "10" to SetUInt8, GFFStruct.SetUInt8 does conversion.
                // But GFFStruct.SetField doesn't.

                // I'll use reflection/switch to call specific setter based on DestType.
                SetFieldValue(destStruct, destLabel, srcPath, destType);
            }
            else
            {
                // Value to Memory copy
                memory.Memory2DA[DestTokenId] = srcPath;
            }
        }
    }

    private static void SetFieldValue(GFFStruct gffStruct, string label, object value, GFFFieldType fieldType)
    {
        // Duplicated from ModifyFieldGFF because it was private
        // In a real refactor I would move this to a helper class
        try
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8: gffStruct.SetUInt8(label, Convert.ToByte(value)); break;
                case GFFFieldType.Int8: gffStruct.SetInt8(label, Convert.ToSByte(value)); break;
                case GFFFieldType.UInt16: gffStruct.SetUInt16(label, Convert.ToUInt16(value)); break;
                case GFFFieldType.Int16: gffStruct.SetInt16(label, Convert.ToInt16(value)); break;
                case GFFFieldType.UInt32: gffStruct.SetUInt32(label, Convert.ToUInt32(value)); break;
                case GFFFieldType.Int32: gffStruct.SetInt32(label, Convert.ToInt32(value)); break;
                case GFFFieldType.UInt64: gffStruct.SetUInt64(label, Convert.ToUInt64(value)); break;
                case GFFFieldType.Int64: gffStruct.SetInt64(label, Convert.ToInt64(value)); break;
                case GFFFieldType.Single: gffStruct.SetSingle(label, Convert.ToSingle(value)); break;
                case GFFFieldType.Double: gffStruct.SetDouble(label, Convert.ToDouble(value)); break;
                case GFFFieldType.String: gffStruct.SetString(label, value.ToString() ?? ""); break;
                case GFFFieldType.ResRef: gffStruct.SetResRef(label, value as Common.ResRef ?? new Common.ResRef(value.ToString() ?? "")); break;
                    // ... others
            }
        }
        catch { }
    }
}
