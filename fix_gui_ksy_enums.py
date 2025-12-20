#!/usr/bin/env python3
# Fix enum doc fields in GUI.ksy - remove doc fields that appear right after enum name

import sys

file_path = 'src/Andastra/Parsing/Resource/Formats/GFF/Generics/GUI/GUI.ksy'

with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    # Check if this is an enum name line (ends with :) and next line is a doc field
    if (i + 1 < len(lines) and 
        line.strip().endswith(':') and 
        not line.strip().startswith('#') and
        not line.strip().startswith('-') and
        lines[i+1].strip().startswith('doc:')):
        # Skip the doc line
        new_lines.append(line)
        i += 2  # Skip both the enum name line and the doc line
        continue
    new_lines.append(line)
    i += 1

with open(file_path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print(f"Fixed {file_path}")

