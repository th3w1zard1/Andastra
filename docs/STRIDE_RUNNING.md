# Running Odyssey Game (Stride)

## Quick Start

The correct command to run the Odyssey game is:

```powershell
dotnet run --project src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj -- --k1 --path "C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic" --module end_m01aa
```

## Important Notes

### Correct Project File

- ✅ **Use**: `src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj` (the executable)
- ❌ **Don't use**: `src\OdysseyRuntime\Odyssey.Stride\Odyssey.Stride.csproj` (it's a library)

`Odyssey.Game` is the entry point application that contains `Program.cs` and command-line argument parsing. `Odyssey.Stride` is a library project that provides Stride engine adapters.

### Command-Line Arguments

The game supports the following command-line arguments:

#### Game Selection
- `--k1`, `-k1` - Run KOTOR 1 (default)
- `--k2`, `-k2`, `--tsl` - Run KOTOR 2 (TSL)

#### Paths
- `--path <path>`, `-p <path>` - Path to KOTOR installation directory
- `--module <name>`, `-m <name>` - Start at specific module (e.g., `end_m01aa`)
- `--load <save>`, `-l <save>` - Load save game file

#### Display
- `--width <n>`, `-w <n>` - Window width (default: 1280)
- `--height <n>`, `-h <n>` - Window height (default: 720)
- `--fullscreen`, `-f` - Run in fullscreen mode

#### Other Options
- `--debug`, `-d` - Enable debug rendering
- `--no-intro` - Skip intro videos (default: enabled)
- `--help`, `-?` - Show help message

### Examples

```powershell
# Run KOTOR 1 with default settings (auto-detects installation)
dotnet run --project src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj -- --k1

# Run KOTOR 2 with custom path and module
dotnet run --project src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj -- --k2 --path "C:\Games\KOTOR2" --module endar_spire

# Run in fullscreen with debug rendering
dotnet run --project src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj -- --k1 --fullscreen --debug

# Show help
dotnet run --project src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj -- --help
```

### Path Detection

If you don't specify `--path`, the game will attempt to auto-detect the KOTOR installation path by checking common Steam and retail installation locations.

### Building First

Make sure to build the project before running:

```powershell
dotnet build src\OdysseyRuntime\Odyssey.Game\Odyssey.Game.csproj
```

Or use the packaging scripts which handle building:

```powershell
.\scripts\Build-StrideGame.ps1
```

