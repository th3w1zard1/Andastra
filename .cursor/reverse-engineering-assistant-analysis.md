# Reverse Engineering Assistant Analysis

## Summary

The `reverse-engineering-assistant` repository (cyberkaida/reverse-engineering-assistant) is a more advanced Ghidra MCP server compared to the basic `GhidraMCP` repository.

## Key Findings

### ✅ Has `list-open-programs` Tool

**Location**: `src/main/java/reva/tools/project/ProjectToolProvider.java`

**Tool Name**: `list-open-programs` (not `list_programs`)

**Functionality**:

- Lists all programs currently open in Ghidra across all tools
- Returns detailed information for each program:
  - `programPath` - Domain file pathname
  - `language` - Language ID
  - `compilerSpec` - Compiler spec ID
  - `creationDate` - Program creation date
  - `sizeBytes` - Memory size in bytes
  - `symbolCount` - Number of symbols
  - `functionCount` - Number of functions
  - `modificationDate` - Last modification time
  - `isReadOnly` - Read-only status
- Returns metadata with count of open programs

**Implementation**:

```java
List<Program> openPrograms = RevaProgramManager.getOpenPrograms();
```

### ✅ Has `get-current-program` Tool

**Location**: `src/main/java/reva/tools/project/ProjectToolProvider.java`

**Functionality**:

- Gets the currently active program
- **Note**: Currently just returns the first open program (not necessarily the active one)
- Returns same detailed information as `list-open-programs` but for single program

### ❌ No Program Switching Tool

**Finding**: There is NO tool to switch/select which program is active.

**However**:

- `McpServerManager` has a `setActiveProgram(Program program, PluginTool tool)` method
- This method exists but is NOT exposed as an MCP tool
- It's used internally by the server but not accessible via MCP API

**What exists**:

- Internal `setActiveProgram` method in `McpServerManager.java` (line 369)
- Used internally when programs are opened/activated
- Not exposed as a tool that can be called by MCP clients

## Architecture Comparison

### reverse-engineering-assistant

- More sophisticated architecture
- Uses `RevaProgramManager` singleton for program management
- Tracks programs across all Ghidra tools
- Has program caching and registry system
- Better structured with tool providers pattern
- Returns structured JSON data with metadata

### ghidra-mcp (LaurieWired)

- Simpler architecture
- Direct HTTP endpoints
- Basic program listing
- No program switching capability either

## Recommendations

### To Add Program Switching

1. **Add new tool to `ProjectToolProvider.java`**:

   ```java
   private void registerSetCurrentProgramTool() {
       // Tool that accepts programPath parameter
       // Uses ProgramManager.setCurrentProgram() or similar
       // Updates McpServerManager.setActiveProgram()
   }
   ```

2. **Ghidra API for switching**:
   - `ProgramManager.setCurrentProgram(Program program)` - if available
   - Or use `ProgramManager.openProgram(Program program)` to activate
   - May need to use `ProgramLocator` to find program by path

3. **Tool schema**:
   - Parameter: `programPath` (string) - path to the program to activate
   - Returns: Success/error message
   - Should validate program is open before switching

## Available Tools in reverse-engineering-assistant

From `ProjectToolProvider.java`:

1. `get-current-program` - Get current program
2. `list-project-files` - List files in project
3. `list-open-programs` - List all open programs ✅
4. `checkin-program` - Version control checkin
5. `analyze-program` - Run auto-analysis
6. `change-processor` - Change processor architecture
7. `import-file` - Import file into project

## Conclusion

The `reverse-engineering-assistant` repository has:

- ✅ `list-open-programs` tool (better than basic implementation)
- ✅ Better program management architecture
- ❌ No program switching tool (but infrastructure exists to add it)

The `setActiveProgram` method exists internally but needs to be exposed as an MCP tool to enable program switching functionality.
