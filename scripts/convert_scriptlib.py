#!/usr/bin/env python3
"""Convert Python scriptlib.py to C# ScriptLib.cs"""

import sys
import os
sys.path.insert(0, 'vendor/PyKotor/Libraries/PyKotor/src')

from pykotor.common.scriptlib import KOTOR_LIBRARY, TSL_LIBRARY

def escape_csharp_string(s):
    """Escape string for C# verbatim string literal"""
    # For verbatim strings (@""), we only need to escape quotes by doubling them
    # Also handle newlines and other special characters
    return s.replace('"', '""')

# Generate C# file
output = """using System.Collections.Generic;
using System.Text;

namespace TSLPatcher.Core.Common.Script;

/// <summary>
/// NWScript library include files for KOTOR and TSL.
/// 1:1 port from pykotor.common.scriptlib
/// </summary>
public static class ScriptLib
{
    /// <summary>
    /// KOTOR (Knights of the Old Republic) script library includes.
    /// Maps include file names to their source code content.
    /// </summary>
    public static readonly Dictionary<string, byte[]> KOTOR_LIBRARY = new()
    {
"""

# Add KOTOR library entries
for key, value in sorted(KOTOR_LIBRARY.items()):
    # Convert bytes to C# verbatim string literal
    content = value.decode('utf-8', errors='replace')
    escaped_content = escape_csharp_string(content)
    # Use verbatim string literal with @"" syntax - need to handle multi-line properly
    # Split into lines and join with newlines
    output += f'        {{ "{key}", Encoding.UTF8.GetBytes(@"{escaped_content}") }},\n'

output += """    };

    /// <summary>
    /// TSL (The Sith Lords) script library includes.
    /// Maps include file names to their source code content.
    /// </summary>
    public static readonly Dictionary<string, byte[]> TSL_LIBRARY = new()
    {
"""

# Add TSL library entries
for key, value in sorted(TSL_LIBRARY.items()):
    # Convert bytes to C# verbatim string literal
    content = value.decode('utf-8', errors='replace')
    escaped_content = escape_csharp_string(content)
    # Use verbatim string literal with @"" syntax
    output += f'        {{ "{key}", Encoding.UTF8.GetBytes(@"{escaped_content}") }},\n'

output += """    };
}
"""

# Write to file
with open('src/TSLPatcher.Core/Common/Script/ScriptLib.cs', 'w', encoding='utf-8') as f:
    f.write(output)

print("Generated ScriptLib.cs successfully!")
print(f"KOTOR_LIBRARY: {len(KOTOR_LIBRARY)} entries")
print(f"TSL_LIBRARY: {len(TSL_LIBRARY)} entries")

