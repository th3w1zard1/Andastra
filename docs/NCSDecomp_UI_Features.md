# NCSDecomp UI Features Documentation

This document outlines the UI functionality from `vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java` that should be implemented in a separate UI project (e.g., using Avalonia for cross-platform support).

## Core UI Components

### Main Window Structure
- **Menu Bar**: File, View, Options, Help menus
- **Toolbar**: Quick access buttons for common actions
- **Split Panes**:
  - Main vertical split: Upper workspace (72%) / Status/log area (28%)
  - Upper horizontal split: Navigation tree (20%) / Tabbed workspace (80%)
- **Navigation Tree**: Left panel showing scripts/functions/variables with search filter
- **Tabbed Workspace**: Multiple file tabs with decompiled code and bytecode views
- **Status/Log Area**: Scrollable log output with log level filtering (TRACE, DEBUG, INFO, WARNING, ERROR)

### Tab Components
Each tab contains:
1. **Decompiled Code View** (editable):
   - Left: Original decompiled code (editable)
   - Right: Round-trip decompiled code (read-only, for comparison)
   - Syntax highlighting (NWScript)
   - Undo/redo support
   - Line numbers
   - Dirty file tracking (unsaved changes marked with `*`)

2. **Bytecode View** (read-only):
   - Left: Original bytecode
   - Right: Recompiled bytecode (for comparison)
   - Syntax highlighting (bytecode)
   - Linked scrollbars option

## File Operations

### Menu Items
- **File → Open** (Ctrl+O): Open .ncs or .nss files
- **File → Close** (Ctrl+W, Ctrl+F4): Close current tab
- **File → Close All** (Ctrl+Shift+W): Close all tabs
- **File → Save** (Ctrl+S): Save current file
- **File → Save All** (Ctrl+Shift+S): Save all modified files
- **File → Exit** (Ctrl+Q): Exit application

### Drag and Drop
- Support dropping .ncs or .nss files onto the workspace
- Support dropping files onto individual text panes

## View Operations

### Menu Items
- **View → View Decompiled Code** (F2): Switch to decompiled code view
- **View → View Byte Code** (F3): Switch to bytecode view
- **View → Link Scroll Bars**: Toggle synchronized scrolling between left/right panes

### Tab Navigation
- **Ctrl+Tab**: Next tab
- **Ctrl+Shift+Tab**: Previous tab
- **Ctrl+Page Down**: Next tab (alternative)
- **Ctrl+Page Up**: Previous tab (alternative)
- **Ctrl+1 through Ctrl+9**: Jump to specific tab number

## Text Editing Features

### Keyboard Shortcuts
- **Ctrl+A**: Select all
- **Ctrl+D**: Duplicate current line
- **Ctrl+/**: Toggle comment on current line/selection
- **Ctrl+Z**: Undo
- **Ctrl+Y**: Redo
- **Ctrl+Shift+Z**: Redo (alternative)
- **Ctrl+F**: Find (placeholder - not yet implemented)
- **Ctrl+H**: Replace (placeholder - not yet implemented)
- **Ctrl+G**: Go to Line (placeholder - not yet implemented)

### Font Controls
- **Ctrl++** or **Ctrl+=**: Zoom in
- **Ctrl+-**: Zoom out
- **Ctrl+0**: Reset zoom to default

### Other
- **F5**: Refresh/recompile current file
- **F11**: Toggle fullscreen

## Options Menu

- **Options → Settings** (Ctrl+P, Ctrl+,): Open settings dialog

## Help Menu

- **Help → About** (F1): Show about dialog
- **Help → bolabaden.org** (F2): Open website
- **Help → GitHub Repo** (F3): Open GitHub repository
- **Help → Sponsor NCSDecomp** (F4): Open sponsor page

## Settings Dialog

The settings dialog should include tabs for:
1. **Files & Directories**: Output directory, nwscript paths
2. **Game**: Game variant (K1/K2), compiler selection
3. **Decompilation**: Prefer switches, strict signatures
4. **Output**: File extension, encoding, filename prefix/suffix, overwrite files
5. **Interface**: Link scroll bars, other UI preferences

## Status/Log Area

### Features
- **Log Level Filter**: Dropdown to filter by severity (TRACE, DEBUG, INFO, WARNING, ERROR)
- **Color Coding**:
  - TRACE: Gray
  - DEBUG: Green
  - INFO: Black
  - WARNING: Orange
  - ERROR: Crimson red
- **Context Menu**: Right-click to clear log
- **Auto-scroll**: Automatically scrolls to bottom when new log entries are added
- **Status Bar**: Shows current status message (e.g., "Ready", "Loading...")

## Navigation Tree

### Features
- **Search Filter**: Text field to filter tree by function/variable names
- **Tree Structure**: Shows scripts → functions → variables hierarchy
- **Selection**: Clicking tree items should navigate to corresponding code location
- **Editable**: Tree items can be renamed (for variable/function names)

## Round-Trip Compilation

### Features
- When viewing decompiled code, automatically compile it and show:
  - Round-trip decompiled code (recompiled then decompiled again)
  - Recompiled bytecode (for comparison with original)
- Highlight differences between original and round-trip versions

## Implementation Notes

### Core Library Integration
The UI should use the following classes from `CSharpKOTOR.Formats.NCS.NCSDecomp`:
- `FileDecompiler`: Main decompilation logic
- `Settings`: Configuration management
- `TreeModelFactory`: Create tree data structures
- `RoundTripUtil`: Round-trip compilation utilities
- `CompilerUtil`: Compiler path resolution
- `CompilerExecutionWrapper`: External compiler execution (for round-trip)
- `RegistrySpoofer` / `NoOpRegistrySpoofer`: Registry spoofing for legacy compilers

### UI Framework Recommendations
- **Avalonia**: Cross-platform UI framework for .NET (recommended)
- **WPF**: Windows-only alternative
- **WinUI 3**: Modern Windows UI framework

### Key Implementation Points
1. **Stream Redirection**: Redirect `System.Console.Out` and `System.Console.Error` to the log area with color coding
2. **Tab Management**: Track open files, unsaved changes, and tab state
3. **Syntax Highlighting**: Implement NWScript and bytecode syntax highlighting
4. **File Watching**: Optionally watch for external file changes
5. **Error Handling**: Show user-friendly error dialogs for decompilation failures
6. **Progress Indicators**: Show progress for long-running operations (decompilation, compilation)

## Stream Redirection Implementation Details

The Java version uses inner classes `DualOutputPrintStream` and `DualOutputStream` to redirect `System.out` and `System.err` to the GUI log area. The C# UI should implement similar functionality.

### Required Components

1. **Dual Output Stream**: A custom stream that writes to both:
   - The original console output (for debugging)
   - The GUI log area (with color coding)

2. **Dual Output Print Stream**: A wrapper around the dual output stream that provides PrintStream-like functionality

3. **Helper Methods** (from Java `Decompiler.java:361-484`):

   - **`parseLogSeverity(text: string): LogSeverity`**: Parses log severity from log line text
     - Looks for markers like `[TRACE]`, `[DEBUG]`, `[INFO]`, `[WARN]`, `[ERROR]`
     - Handles special cases for decompiler control flow logs
     - Defaults to `INFO` if no severity marker found

   - **`shouldShowLog(severity: LogSeverity, filterLevel: string): bool`**: Determines if log should be shown based on filter
     - Compares log severity index with selected filter level index
     - Shows log if severity >= filter level

   - **`getSeverityIndex(level: string): int`**: Gets index of severity level for comparison
     - Maps level string to index in `Decompiler.LogLevels` array
     - Returns `Decompiler.DefaultLogLevelIndex` if not found

   - **`getColorForSeverity(severity: LogSeverity): Color`**: Gets color for log severity
     - TRACE: Gray (128, 128, 128)
     - DEBUG: Green (0, 128, 0)
     - INFO: Black (0, 0, 0)
     - WARNING: Orange (255, 140, 0)
     - ERROR: Crimson red (220, 20, 60)

### Implementation Pattern

```csharp
// Pseudo-code for C# implementation
public class DualOutputStream : Stream
{
    private readonly Stream original;
    private readonly TextBlock guiLog; // or equivalent UI control
    private readonly StringBuilder lineBuffer = new StringBuilder();
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Write to original stream
        original.Write(buffer, offset, count);
        
        // Buffer text for GUI
        string text = Encoding.UTF8.GetString(buffer, offset, count);
        lineBuffer.Append(text);
        
        // Flush line when newline found
        if (text.Contains("\n"))
        {
            FlushLine();
        }
    }
    
    private void FlushLine()
    {
        if (lineBuffer.Length > 0)
        {
            string line = lineBuffer.ToString();
            lineBuffer.Clear();
            AppendToGuiLog(line);
        }
    }
    
    private void AppendToGuiLog(string text)
    {
        // Parse severity and apply color
        LogSeverity severity = ParseLogSeverity(text);
        Color color = GetColorForSeverity(severity);
        
        // Check filter
        if (!ShouldShowLog(severity, currentFilterLevel))
            return;
        
        // Append to GUI with color (thread-safe, use UI dispatcher)
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Append styled text to log area
        });
    }
}
```

### Thread Safety

- All GUI updates must be performed on the UI thread (use `Dispatcher.Invoke` in Avalonia/WPF)
- Buffer writes can happen on any thread, but GUI appends must be synchronized

### Constants Available from Core Library

- `Decompiler.LogLevels`: Array of log level strings
- `Decompiler.DefaultLogLevelIndex`: Default log level index (2 = INFO)
- `Decompiler.LogSeverity`: Enum with TRACE, DEBUG, INFO, WARNING, ERROR values

## Missing Features (Placeholders in Java Version)

These features are planned but not yet implemented in the Java version:
- Find dialog (Ctrl+F)
- Replace dialog (Ctrl+H)
- Go to Line dialog (Ctrl+G)

These should be implemented in the C# UI version.

