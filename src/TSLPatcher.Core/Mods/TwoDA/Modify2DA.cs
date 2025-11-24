using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.TwoDA;

/// <summary>
/// Abstract base for 2DA modifications.
/// 1:1 port from Python Modify2DA in pykotor/tslpatcher/mods/twoda.py
/// </summary>
public abstract class Modify2DA
{
    protected static Dictionary<string, string> Unpack(
        Dictionary<string, RowValue> cells,
        PatcherMemory memory,
        Core.Formats.TwoDA.TwoDA twoda,
        TwoDARow row)
    {
        var result = new Dictionary<string, string>();
        foreach ((string column, RowValue value) in cells)
        {
            result[column] = value.Value(memory, twoda, row);
        }
        return result;
    }

    public abstract void Apply(Core.Formats.TwoDA.TwoDA twoda, PatcherMemory memory);
}

/// <summary>
/// Changes an existing row in a 2DA.
/// 1:1 port from Python ChangeRow2DA
/// </summary>
public class ChangeRow2DA : Modify2DA
{
    public string Identifier { get; }
    public Target Target { get; }
    public Dictionary<string, RowValue> Cells { get; }
    public Dictionary<int, RowValue> Store2DA { get; }
    public Dictionary<int, RowValue> StoreTLK { get; }

    public ChangeRow2DA(
        string identifier,
        Target target,
        Dictionary<string, RowValue> cells,
        Dictionary<int, RowValue>? store2da = null,
        Dictionary<int, RowValue>? storeTlk = null)
    {
        Identifier = identifier;
        Target = target;
        Cells = cells;
        Store2DA = store2da ?? new Dictionary<int, RowValue>();
        StoreTLK = storeTlk ?? new Dictionary<int, RowValue>();
    }

    public override void Apply(Core.Formats.TwoDA.TwoDA twoda, PatcherMemory memory)
    {
        TwoDARow? sourceRow = Target.Search(twoda, memory);

        if (sourceRow == null)
        {
            throw new WarningError($"The source row was not found during the search: ({Target.TargetType}, {Target.Value})");
        }

        Dictionary<string, string> cells = Unpack(Cells, memory, twoda, sourceRow);
        sourceRow.UpdateValues(cells);

        foreach ((int tokenId, RowValue value) in Store2DA)
        {
            memory.Memory2DA[tokenId] = value.Value(memory, twoda, sourceRow);
        }

        foreach ((int tokenId, RowValue value) in StoreTLK)
        {
            memory.MemoryStr[tokenId] = int.Parse(value.Value(memory, twoda, sourceRow));
        }
    }

    public override string ToString() =>
        $"ChangeRow2DA(identifier='{Identifier}', target={Target}, cells=[{Cells.Count} items], store_2da=[{Store2DA.Count} items], store_tlk=[{StoreTLK.Count} items])";
}

/// <summary>
/// Adds a new row to a 2DA.
/// 1:1 port from Python AddRow2DA
/// </summary>
public class AddRow2DA : Modify2DA
{
    public string Identifier { get; }
    public string? ExclusiveColumn { get; }
    public string? RowLabel { get; }
    public Dictionary<string, RowValue> Cells { get; }
    public Dictionary<int, RowValue> Store2DA { get; }
    public Dictionary<int, RowValue> StoreTLK { get; }

    public AddRow2DA(
        string identifier,
        string? exclusiveColumn,
        string? rowLabel,
        Dictionary<string, RowValue> cells,
        Dictionary<int, RowValue>? store2da = null,
        Dictionary<int, RowValue>? storeTlk = null)
    {
        Identifier = identifier;
        ExclusiveColumn = exclusiveColumn;
        RowLabel = rowLabel;
        Cells = cells;
        Store2DA = store2da ?? new Dictionary<int, RowValue>();
        StoreTLK = storeTlk ?? new Dictionary<int, RowValue>();
    }

    public override void Apply(Core.Formats.TwoDA.TwoDA twoda, PatcherMemory memory)
    {
        TwoDARow? targetRow = null;

        if (!string.IsNullOrEmpty(ExclusiveColumn))
        {
            if (!Cells.ContainsKey(ExclusiveColumn))
            {
                throw new WarningError($"Exclusive column {ExclusiveColumn} does not exists");
            }

            string exclusiveValue = Cells[ExclusiveColumn].Value(memory, twoda, null);
            foreach (TwoDARow row in twoda)
            {
                if (row.GetString(ExclusiveColumn) == exclusiveValue)
                {
                    targetRow = row;
                    break;
                }
            }
        }

        if (targetRow == null)
        {
            string rowLabel = RowLabel ?? twoda.LabelMax().ToString();
            int index = twoda.AddRow(rowLabel, new Dictionary<string, object>());
            targetRow = twoda.GetRow(index);
            targetRow.UpdateValues(Unpack(Cells, memory, twoda, targetRow));
        }
        else
        {
            // Exclusive column match found - update existing row instead of adding new one
            Dictionary<string, string> cells = Unpack(Cells, memory, twoda, targetRow);
            targetRow.UpdateValues(cells);
        }

        foreach ((int tokenId, RowValue value) in Store2DA)
        {
            memory.Memory2DA[tokenId] = value.Value(memory, twoda, targetRow);
        }

        foreach ((int tokenId, RowValue value) in StoreTLK)
        {
            memory.MemoryStr[tokenId] = int.Parse(value.Value(memory, twoda, targetRow));
        }
    }

    public override string ToString() =>
        $"AddRow2DA(identifier='{Identifier}', exclusive_column='{ExclusiveColumn}', row_label='{RowLabel}', cells=[{Cells.Count} items], store_2da=[{Store2DA.Count} items], store_tlk=[{StoreTLK.Count} items])";
}

/// <summary>
/// Copies an existing row in a 2DA.
/// 1:1 port from Python CopyRow2DA
/// </summary>
public class CopyRow2DA : Modify2DA
{
    public string Identifier { get; }
    public Target Target { get; }
    public string? ExclusiveColumn { get; }
    public string? RowLabel { get; }
    public Dictionary<string, RowValue> Cells { get; }
    public Dictionary<int, RowValue> Store2DA { get; }
    public Dictionary<int, RowValue> StoreTLK { get; }

    public CopyRow2DA(
        string identifier,
        Target target,
        string? exclusiveColumn,
        string? rowLabel,
        Dictionary<string, RowValue> cells,
        Dictionary<int, RowValue>? store2da = null,
        Dictionary<int, RowValue>? storeTlk = null)
    {
        Identifier = identifier;
        Target = target;
        ExclusiveColumn = exclusiveColumn;
        RowLabel = rowLabel;
        Cells = cells;
        Store2DA = store2da ?? new Dictionary<int, RowValue>();
        StoreTLK = storeTlk ?? new Dictionary<int, RowValue>();
    }

    public override void Apply(Core.Formats.TwoDA.TwoDA twoda, PatcherMemory memory)
    {
        TwoDARow? sourceRow = Target.Search(twoda, memory);
        string rowLabel = RowLabel ?? twoda.LabelMax().ToString();

        if (sourceRow == null)
        {
            throw new WarningError($"Source row cannot be None. row_label was '{rowLabel}'");
        }

        TwoDARow? targetRow = null;

        if (!string.IsNullOrEmpty(ExclusiveColumn))
        {
            if (!Cells.ContainsKey(ExclusiveColumn))
            {
                throw new WarningError($"Exclusive column {ExclusiveColumn} does not exists");
            }

            string exclusiveValue = Cells[ExclusiveColumn].Value(memory, twoda, null);
            foreach (TwoDARow row in twoda)
            {
                if (row.GetString(ExclusiveColumn) == exclusiveValue)
                {
                    targetRow = row;
                    break;
                }
            }
        }

        if (targetRow != null)
        {
            // If the row already exists (based on exclusive_column) then we update the cells
            Dictionary<string, string> cells = Unpack(Cells, memory, twoda, targetRow);
            targetRow.UpdateValues(cells);
        }
        else
        {
            // Otherwise, we add the new row instead
            int index = twoda.CopyRow(sourceRow, rowLabel, new Dictionary<string, object>());
            targetRow = twoda.GetRow(index);
            Dictionary<string, string> cells = Unpack(Cells, memory, twoda, targetRow);
            targetRow.UpdateValues(cells);
        }

        foreach ((int tokenId, RowValue value) in Store2DA)
        {
            memory.Memory2DA[tokenId] = value.Value(memory, twoda, targetRow);
        }

        foreach ((int tokenId, RowValue value) in StoreTLK)
        {
            memory.MemoryStr[tokenId] = int.Parse(value.Value(memory, twoda, targetRow));
        }
    }

    public override string ToString() =>
        $"CopyRow2DA(identifier='{Identifier}', target={Target}, exclusive_column='{ExclusiveColumn}', row_label='{RowLabel}', cells=[{Cells.Count} items], store_2da=[{Store2DA.Count} items], store_tlk=[{StoreTLK.Count} items])";
}

/// <summary>
/// Adds a new column to a 2DA.
/// 1:1 port from Python AddColumn2DA
/// </summary>
public class AddColumn2DA : Modify2DA
{
    public string Identifier { get; }
    public string Header { get; }
    public string Default { get; }
    public Dictionary<int, RowValue> IndexInsert { get; }
    public Dictionary<string, RowValue> LabelInsert { get; }
    public Dictionary<int, string> Store2DA { get; }

    public AddColumn2DA(
        string identifier,
        string header,
        string defaultValue,
        Dictionary<int, RowValue> indexInsert,
        Dictionary<string, RowValue> labelInsert,
        Dictionary<int, string>? store2da = null)
    {
        Identifier = identifier;
        Header = header;
        Default = defaultValue;
        IndexInsert = indexInsert;
        LabelInsert = labelInsert;
        Store2DA = store2da ?? new Dictionary<int, string>();
    }

    public override void Apply(Core.Formats.TwoDA.TwoDA twoda, PatcherMemory memory)
    {
        if (twoda.GetHeaders().Contains(Header))
        {
            throw new WarningError($"Column '{Header}' already exists in the 2DA");
        }

        twoda.AddColumn(Header);

        for (int i = 0; i < twoda.GetHeight(); i++)
        {
            TwoDARow row = twoda.GetRow(i);
            string cellValue = Default;

            // Check if there's an index-specific value
            if (IndexInsert.ContainsKey(i))
            {
                cellValue = IndexInsert[i].Value(memory, twoda, row);
            }
            // Check if there's a label-specific value
            else if (LabelInsert.ContainsKey(row.Label()))
            {
                cellValue = LabelInsert[row.Label()].Value(memory, twoda, row);
            }

            twoda.SetCellString(i, Header, cellValue);
        }

        foreach ((int tokenId, string value) in Store2DA)
        {
            // value should be a string like "I0", "L1", or just "0" (numeric string = index)
            if (value.StartsWith("I"))
            {
                int rowIndex = int.Parse(value.Substring(1));
                TwoDARow row = twoda.GetRow(rowIndex);
                memory.Memory2DA[tokenId] = row.GetString(Header);
            }
            else if (value.StartsWith("L"))
            {
                string rowLabel = value.Substring(1);
                TwoDARow? row = twoda.FindRow(rowLabel);
                if (row == null)
                {
                    throw new WarningError($"Could not find row with label '{rowLabel}' when storing 2DA memory");
                }
                memory.Memory2DA[tokenId] = row.GetString(Header);
            }
            else if (int.TryParse(value, out int rowIndex))
            {
                // Numeric string without prefix: try as label first, then as index
                TwoDARow? row = twoda.FindRow(value);
                if (row != null)
                {
                    // Matches a row label
                    memory.Memory2DA[tokenId] = row.GetString(Header);
                }
                else
                {
                    // Treat as row index
                    row = twoda.GetRow(rowIndex);
                    memory.Memory2DA[tokenId] = row.GetString(Header);
                }
            }
            else
            {
                throw new WarningError($"store_2da dict has an invalid value at {tokenId}: '{value}'");
            }
        }
    }

    public override string ToString() =>
        $"AddColumn2DA(identifier='{Identifier}', header='{Header}', default='{Default}', index_insert=[{IndexInsert.Count} items], label_insert=[{LabelInsert.Count} items], store_2da=[{Store2DA.Count} items])";
}

