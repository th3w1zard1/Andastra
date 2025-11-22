using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TSLPatcher.Core.Formats.TwoDA;

/// <summary>
/// Represents a 2DA file.
/// </summary>
public class TwoDA : IEnumerable<TwoDARow>
{
    private readonly List<Dictionary<string, string>> _rows = new();
    private readonly List<string> _headers = new();
    private readonly List<string> _labels = new();

    public TwoDA(List<string>? headers = null)
    {
        if (headers != null)
        {
            _headers.AddRange(headers);
        }
    }

    public int GetHeight() => _rows.Count;
    public int GetWidth() => _headers.Count;

    public List<string> GetHeaders() => new(_headers);
    public List<string> GetLabels() => new(_labels);

    public string GetLabel(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _labels.Count)
        {
            throw new IndexOutOfRangeException($"Row index {rowIndex} is out of range");
        }

        return _labels[rowIndex];
    }

    public int GetRowIndex(string rowLabel)
    {
        for (int i = 0; i < _labels.Count; i++)
        {
            if (_labels[i].Equals(rowLabel, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        throw new KeyNotFoundException($"Row label '{rowLabel}' not found");
    }

    public void SetLabel(int rowIndex, string value)
    {
        if (rowIndex < 0 || rowIndex >= _labels.Count)
        {
            throw new IndexOutOfRangeException($"Row index {rowIndex} is out of range");
        }

        _labels[rowIndex] = value;
    }

    public List<string> GetColumn(string header)
    {
        if (!_headers.Contains(header))
        {
            throw new KeyNotFoundException($"The header '{header}' does not exist.");
        }

        var result = new List<string>();
        for (int i = 0; i < GetHeight(); i++)
        {
            result.Add(_rows[i][header]);
        }
        return result;
    }

    public void AddColumn(string header)
    {
        if (_headers.Contains(header))
        {
            throw new InvalidOperationException($"The header '{header}' already exists.");
        }

        _headers.Add(header);
        foreach (var row in _rows)
        {
            row[header] = "";
        }
    }

    public void RemoveColumn(string header)
    {
        if (_headers.Contains(header))
        {
            foreach (var row in _rows)
            {
                row.Remove(header);
            }
            _headers.Remove(header);
        }
    }

    public TwoDARow GetRow(int rowIndex, string? context = null)
    {
        try
        {
            string label = GetLabel(rowIndex);
            return new TwoDARow(label, _rows[rowIndex]);
        }
        catch (IndexOutOfRangeException e)
        {
            throw new IndexOutOfRangeException(
                $"Row index {rowIndex} not found in the 2DA." +
                (context != null ? $" Context: {context}" : ""), e);
        }
    }

    public TwoDARow? FindRow(string rowLabel)
    {
        return this.FirstOrDefault(row => row.Label() == rowLabel);
    }

    public int? RowIndex(TwoDARow row)
    {
        int index = 0;
        foreach (var searching in this)
        {
            if (searching.Equals(row))
            {
                return index;
            }

            index++;
        }
        return null;
    }

    public int AddRow(string? rowLabel = null, Dictionary<string, object>? cells = null)
    {
        var newRow = new Dictionary<string, string>();
        _rows.Add(newRow);
        _labels.Add(rowLabel ?? _rows.Count.ToString());

        if (cells != null)
        {
            var convertedCells = new Dictionary<string, string>();
            foreach (var (key, value) in cells)
            {
                convertedCells[key] = value?.ToString() ?? "";
            }
            cells = convertedCells.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
        }

        foreach (string header in _headers)
        {
            newRow[header] = cells?.ContainsKey(header) == true
                ? cells[header]?.ToString() ?? ""
                : "";
        }

        return _rows.Count - 1;
    }

    public void RemoveRow(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < _rows.Count)
        {
            _rows.RemoveAt(rowIndex);
            _labels.RemoveAt(rowIndex);
        }
    }

    public int CopyRow(int rowIndex, string? newLabel = null)
    {
        var row = GetRow(rowIndex);
        var cellsCopy = new Dictionary<string, object>();
        foreach (var (header, value) in row.GetData())
        {
            cellsCopy[header] = value;
        }
        return AddRow(newLabel ?? row.Label(), cellsCopy);
    }

    public int CopyRow(TwoDARow sourceRow, string? rowLabel = null, Dictionary<string, object>? overrideCells = null)
    {
        int? sourceIndex = RowIndex(sourceRow);

        var newRow = new Dictionary<string, string>();
        _rows.Add(newRow);
        _labels.Add(rowLabel ?? _rows.Count.ToString());

        overrideCells ??= new Dictionary<string, object>();
        var convertedCells = new Dictionary<string, string>();
        foreach (var (key, value) in overrideCells)
        {
            convertedCells[key] = value?.ToString() ?? "";
        }

        foreach (string header in _headers)
        {
            newRow[header] = convertedCells.ContainsKey(header)
                ? convertedCells[header]
                : (sourceIndex.HasValue ? GetCellString(sourceIndex.Value, header) : "");
        }

        return _rows.Count - 1;
    }

    public string GetCellString(int rowIndex, string header, string? context = null)
    {
        try
        {
            return _rows[rowIndex][header];
        }
        catch (KeyNotFoundException)
        {
            throw new KeyNotFoundException(
                $"The header '{header}' does not exist in row {rowIndex}." +
                (context != null ? $" Context: {context}" : ""));
        }
    }

    public string GetCellString(string rowLabel, string header, string? context = null)
    {
        int rowIndex = GetRowIndex(rowLabel);
        return GetCellString(rowIndex, header, context);
    }

    public int? GetCellInt(int rowIndex, string header, int? defaultValue = null)
    {
        string cellValue = GetCellString(rowIndex, header);
        if (string.IsNullOrWhiteSpace(cellValue) || cellValue == "****")
        {
            return defaultValue;
        }

        if (int.TryParse(cellValue, out int result))
        {
            return result;
        }

        return defaultValue;
    }

    public float? GetCellFloat(int rowIndex, string header, float? defaultValue = null)
    {
        string cellValue = GetCellString(rowIndex, header);
        if (string.IsNullOrWhiteSpace(cellValue) || cellValue == "****")
        {
            return defaultValue;
        }

        if (float.TryParse(cellValue, out float result))
        {
            return result;
        }

        return defaultValue;
    }

    public void SetCellString(int rowIndex, string header, string value)
    {
        if (!_headers.Contains(header))
        {
            throw new KeyNotFoundException($"The header '{header}' does not exist.");
        }

        _rows[rowIndex][header] = value;
    }

    public void SetCellInt(int rowIndex, string header, int value)
    {
        SetCellString(rowIndex, header, value.ToString());
    }

    public void SetCellFloat(int rowIndex, string header, float value)
    {
        SetCellString(rowIndex, header, value.ToString());
    }

    public IEnumerator<TwoDARow> GetEnumerator()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            yield return new TwoDARow(GetLabel(i), _rows[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Serializes the 2DA to a byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var writer = new TwoDABinaryWriter(this);
        return writer.Write();
    }

    /// <summary>
    /// Deserializes a 2DA from a byte array.
    /// </summary>
    public static TwoDA FromBytes(byte[] data)
    {
        var reader = new TwoDABinaryReader(data);
        return reader.Load();
    }

    /// <summary>
    /// Saves the 2DA to a file.
    /// </summary>
    public void Save(string path)
    {
        var writer = new TwoDABinaryWriter(this);
        byte[] data = writer.Write();
        System.IO.File.WriteAllBytes(path, data);
    }
}

