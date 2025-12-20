#!/usr/bin/env python3
# Fix YAML syntax errors in UTE.ksy

with open('src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTE/UTE.ksy', 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    
    # Fix doc string with colons (line ~161)
    if 'doc: If field_count = 1: Direct field index' in line:
        new_lines.append('        doc: |\n')
        new_lines.append('          If field_count = 1: Direct field index into field_array.\n')
        new_lines.append('          If field_count > 1: Byte offset into field_indices array.\n')
        new_lines.append('          If field_count = 0: Unused (empty struct).\n')
        i += 1
    # Fix doc string with colons (line ~190)
    elif 'doc: For simple types: inline data' in line:
        new_lines.append('        doc: |\n')
        new_lines.append('          For simple types (Byte, Char, UInt16, Int16, UInt32, Int32, UInt64, Int64, Single, Double): Inline data value.\n')
        new_lines.append('          For complex types (String, ResRef, LocalizedString, Binary, Vector3, Vector4): Byte offset into field_data section.\n')
        new_lines.append('          For Struct type: Struct index into struct_array.\n')
        new_lines.append('          For List type: Byte offset into list_indices array.\n')
        i += 1
    # Add if condition before pos for struct_array
    elif '  - id: struct_array' in line:
        new_lines.append(line)
        i += 1
        if i < len(lines) and 'type: struct_array' in lines[i]:
            new_lines.append(lines[i])
            i += 1
            if i < len(lines) and 'pos:' in lines[i]:
                # Insert if before pos
                new_lines.append('    if: gff_header.struct_count > 0\n')
                new_lines.append(lines[i])
                i += 1
            else:
                if i < len(lines):
                    new_lines.append(lines[i])
                    i += 1
    # Add if condition before pos for field_array
    elif '  - id: field_array' in line:
        new_lines.append(line)
        i += 1
        if i < len(lines) and 'type: field_array' in lines[i]:
            new_lines.append(lines[i])
            i += 1
            if i < len(lines) and 'pos:' in lines[i]:
                # Insert if before pos
                new_lines.append('    if: gff_header.field_count > 0\n')
                new_lines.append(lines[i])
                i += 1
            else:
                if i < len(lines):
                    new_lines.append(lines[i])
                    i += 1
    else:
        new_lines.append(line)
        i += 1

with open('src/Andastra/Parsing/Resource/Formats/GFF/Generics/UTE/UTE.ksy', 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("Fixed UTE.ksy YAML syntax errors")

