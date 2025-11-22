using System.Collections.Generic;

namespace TSLPatcher.Core.Memory;

/// <summary>
/// Stores memory tokens used during patching
/// </summary>
public class PatcherMemory
{
    /// <summary>
    /// 2DAMemory# (token) -> string value
    /// </summary>
    public Dictionary<int, string> Memory2DA { get; } = new();

    /// <summary>
    /// StrRef# (token) -> dialog.tlk index
    /// </summary>
    public Dictionary<int, int> MemoryStr { get; } = new();

    public override string ToString()
    {
        return $"PatcherMemory(memory_2da={Memory2DA.Count} items, memory_str={MemoryStr.Count} items)";
    }
}

