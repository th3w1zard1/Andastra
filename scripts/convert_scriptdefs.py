#!/usr/bin/env python3
"""Convert Python scriptdefs.py to C# ScriptDefs.cs"""

import sys
from typing import Any

sys.path.insert(0, 'vendor/PyKotor/Libraries/PyKotor/src')
sys.path.insert(0, 'vendor/Utility/src')

from pykotor.common.script import DataType  # type: ignore[import-not-found, note]  # pyright: ignore[reportMissingImports]
from pykotor.common.scriptdefs import (  # type: ignore[import-not-found, note]  # pyright: ignore[reportMissingImports]
    KOTOR_CONSTANTS,
    KOTOR_FUNCTIONS,
    TSL_CONSTANTS,
    TSL_FUNCTIONS,
)
from pykotor.common.script import ScriptConstant, ScriptParam, ScriptFunction  # type: ignore[import-not-found, note]  # pyright: ignore[reportMissingImports]


def convert_datatype(dt: DataType) -> str:
    """Convert Python DataType to C# DataType"""
    mapping: dict[str, str] = {
        'VOID': 'Void',
        'INT': 'Int',
        'FLOAT': 'Float',
        'STRING': 'String',
        'OBJECT': 'Object',
        'VECTOR': 'Vector',
        'LOCATION': 'Location',
        'EVENT': 'Event',
        'EFFECT': 'Effect',
        'ITEMPROPERTY': 'ItemProperty',
        'TALENT': 'Talent',
        'ACTION': 'Action',
        'STRUCT': 'Struct'
    }
    return mapping.get(dt.name, dt.name)

def convert_value(value: Any, dt: DataType) -> str:
    """Convert Python value to C# literal"""
    # Handle string constants that represent numeric values
    if isinstance(value, str):
        constant_map: dict[str, int] = {
            'OBJECT_SELF': 0,
            'OBJECT_INVALID': -1,
            'TRUE': 1,
            'FALSE': 0,
        }
        if value in constant_map:
            return str(constant_map[value])
        # Otherwise treat as string literal
        escaped = value.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\r', '\\r')
        return f'"{escaped}"'
    
    if dt == DataType.INT:
        return str(value)
    elif dt == DataType.FLOAT:
        if isinstance(value, float):
            # Handle special values
            if value == float('inf'):
                return "double.PositiveInfinity"
            elif value == float('-inf'):
                return "double.NegativeInfinity"
            elif value != value:  # NaN
                return "double.NaN"
            return f"{value}f"
        return str(value)
    elif dt == DataType.STRING:
        # Escape C# string
        escaped = value.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\r', '\\r')
        return f'"{escaped}"'
    elif dt == DataType.VECTOR:
        # Vector3 values
        from utility.common.geometry import Vector3  # type: ignore[import-not-found, note]  # pyright: ignore[reportMissingImports]
        if isinstance(value, Vector3):
            return f"new Vector3({value.x}f, {value.y}f, {value.z}f)"
        return str(value)
    else:
        return str(value)

def convert_constant(const: ScriptConstant) -> str:
    """Convert ScriptConstant to C#"""
    dt = convert_datatype(const.datatype)
    value = convert_value(const.value, const.datatype)
    return f'        new ScriptConstant(DataType.{dt}, "{const.name}", {value}),'

def convert_param(param: ScriptParam) -> str:
    """Convert ScriptParam to C#"""
    dt = convert_datatype(param.datatype)
    if param.default is not None:
        default_val = convert_value(param.default, param.datatype)
        return f'new ScriptParam(DataType.{dt}, "{param.name}", {default_val})'
    return f'new ScriptParam(DataType.{dt}, "{param.name}")'

def convert_function(func: ScriptFunction) -> str:
    """Convert ScriptFunction to C#"""
    ret_type = convert_datatype(func.returntype)
    params_str = ', '.join([convert_param(p) for p in func.params])
    # Escape description and raw strings for verbatim string literals (@""")
    # For verbatim strings, we only need to escape quotes by doubling them
    desc = func.description.replace('"', '""')
    raw = func.raw.replace('"', '""')
    return f"""        new ScriptFunction(
            DataType.{ret_type},
            "{func.name}",
            new List<ScriptParam> {{ {params_str} }},
            @"{desc}",
            @"{raw}"
        ),"""

# Generate C# file
output = """using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Common.Script;

namespace TSLPatcher.Core.Common.Script;

/// <summary>
/// NWScript constant and function definitions for KOTOR and TSL.
/// 1:1 port from pykotor.common.scriptdefs
/// </summary>
public static class ScriptDefs
{
    /// <summary>
    /// KOTOR (Knights of the Old Republic) script constants.
    /// </summary>
    public static readonly List<ScriptConstant> KOTOR_CONSTANTS = new()
    {
"""

# Add KOTOR constants
for const in KOTOR_CONSTANTS:
    output += convert_constant(const) + '\n'

output += """    };

    /// <summary>
    /// TSL (The Sith Lords) script constants.
    /// </summary>
    public static readonly List<ScriptConstant> TSL_CONSTANTS = new()
    {
"""

# Add TSL constants
for const in TSL_CONSTANTS:
    output += convert_constant(const) + '\n'

output += """    };

    /// <summary>
    /// KOTOR (Knights of the Old Republic) script functions.
    /// </summary>
    public static readonly List<ScriptFunction> KOTOR_FUNCTIONS = new()
    {
"""

# Add KOTOR functions
for func in KOTOR_FUNCTIONS:
    output += convert_function(func) + '\n'

output += """    };

    /// <summary>
    /// TSL (The Sith Lords) script functions.
    /// </summary>
    public static readonly List<ScriptFunction> TSL_FUNCTIONS = new()
    {
"""

# Add TSL functions
for func in TSL_FUNCTIONS:
    output += convert_function(func) + '\n'

output += """    };
}
"""

# Write to file
with open('src/TSLPatcher.Core/Common/Script/ScriptDefs.cs', 'w', encoding='utf-8') as f:
    f.write(output)

print("Generated ScriptDefs.cs successfully!")

