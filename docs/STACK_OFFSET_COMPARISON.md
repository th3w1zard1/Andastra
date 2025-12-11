# Stack Offset -8 Issue: File-by-File Comparison

## Files Involved in Stack Offset Calculation

### 1. VariableDeclarator.Compile (Statements.cs)

**C# Location:** `src/TSLPatcher.Core/Formats/NCS/Compiler/Statements.cs:25-64`
**Python Equivalent:** `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py` (VariableDeclaration.compile)

**C# Order:**

1. RSADDI (reserve stack space)
2. AddScoped (add to scope)
3. Compile initializer
4. GetScoped
5. CPDOWNSP

**Python Order:** (Need to verify)

### 2. CodeBlock.GetScoped (CodeBlock.cs)

**C# Location:** `src/TSLPatcher.Core/Formats/NCS/Compiler/NSS/AST/CodeBlock.cs:108-129`
**Python Equivalent:** `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:635-650`

**Status:** ✅ FIXED - Removed double TempStack subtraction

### 3. BinaryOperatorExpression.Compile

**C# Location:** `src/TSLPatcher.Core/Formats/NCS/Compiler/NSS/AST/Expressions/BinaryOperatorExpression.cs:32-59`
**Python Equivalent:** `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:1409-1442`

**Status:** ✅ FIXED - Now checks if temp_stack changed before adding, matches Python

### 4. Stack.CopyDown and StackIndex

**C# Location:** `src/TSLPatcher.Core/Formats/NCS/Compiler/Stack.cs:105-137, 869-923`
**Python Equivalent:** `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/interpreter.py:1183-1210, 1025-1078`

**Status:** ✅ MATCHES - Logic is identical

### 5. IntExpression.Compile

**C# Location:** `src/TSLPatcher.Core/Formats/NCS/Compiler/NSS/AST/Expressions/IntExpression.cs:20-24`
**Python Equivalent:** `vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:1127-1140`

**Status:** ⚠️ NEEDS VERIFICATION - Does it modify temp_stack?

## Current Issue

Stack offset -8 is calculated but stack only has 2 elements when CPDOWNSP executes.
Expected stack state: [variable=0, result=15] (2 elements)
Offset -8 means: 8 bytes down from top = 2 elements down
But we only have 2 elements, so -8 is out of range.

## Next Steps

1. Verify IntExpression doesn't modify temp_stack in Python
2. Check VariableDeclaration.compile order in Python
3. Trace exact temp_stack values at each step
