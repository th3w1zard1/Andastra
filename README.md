# HoloPatcher.NET

A complete rewrite of HoloPatcher in C#/.NET with Avalonia UI framework.

## Project Structure

- **HoloPatcher** - Main Avalonia UI application
- **TSLPatcher.Core** - Core patching engine and logic (portable library)

## Features

### Implemented

- ✅ Basic UI structure with Avalonia
- ✅ Logger system (PatchLogger)
- ✅ Configuration models (PatcherConfig, LogLevel)
- ✅ Memory system (PatcherMemory)
- ✅ Token system (TokenUsage, NoTokenUsage, TokenUsage2DA, TokenUsageTLK)
- ✅ Namespace support (PatcherNamespace)
- ✅ Menu system (Tools, Help)
- ✅ Progress tracking
- ✅ Modification infrastructure (PatcherModification base class)
- ✅ 2DA modification system:
  - RowValue classes (Constant, 2DAMemory, TLKMemory, High, RowIndex, RowLabel, RowCell)
  - Target resolution (RowIndex, RowLabel, LabelColumn)
  - ChangeRow2DA, AddRow2DA, CopyRow2DA, AddColumn2DA
  - Modifications2DA container
- ✅ Comprehensive unit tests (xUnit + FluentAssertions)

### TODO

- ⏳ Permission fixing tools
- ⏳ iOS case sensitivity fixing
- ⏳ Auto-update system
- ⏳ RTF file handling
- ⏳ Complete test coverage

## Requirements

- .NET 8.0 SDK
- Avalonia 11.0.10

## Testing

The project includes comprehensive unit tests covering all core functionality. See [TESTING.md](TESTING.md) for detailed information.

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run specific test
dotnet test --filter "FullyQualifiedName~PatcherMemoryTests"
```

### Test Coverage

Current test coverage includes:

- ✅ PatcherMemory (token storage and retrieval)
- ✅ PatchLogger (logging with different levels)
- ✅ PatcherConfig (configuration management)
- ✅ LogLevel (enum behavior)
- ✅ PatcherNamespace (namespace handling)
- ✅ 2DA modifications
- ✅ GFF modifications
- ✅ TLK modifications
- ✅ SSF modifications
- 🚧 NSS/NCS modifications
- ✅ Config reader/INI parsing

## Building

```bash
cd Tools/HoloPatcher.NET
dotnet restore
dotnet build
```

## Running

```bash
dotnet run --project src/HoloPatcher/HoloPatcher.csproj
```

## Architecture

### TSLPatcher.Core

The core library contains all the patching logic independent of UI:

- **Config/** - Configuration models and parsing
- **Logger/** - Logging system
- **Memory/** - Token memory for patches
- **Namespaces/** - Namespace management
- **Mods/** - Modification operations (GFF, 2DA, TLK, etc.)
- **Patcher/** - Main installation engine

### HoloPatcher

The Avalonia UI application follows MVVM pattern:

- **Views/** - XAML views
- **ViewModels/** - View models with business logic
- **Services/** - Application services

## Migration Notes

This is a port from the original Python/Tkinter implementation. Key differences:

1. **UI Framework**: Tkinter → Avalonia (cross-platform, modern)
2. **Language**: Python → C# (.NET)
3. **Architecture**: Event-driven → MVVM pattern
4. **Threading**: Python threading → C# Tasks/async-await
5. **Logging**: Custom observable → Event-based logger

## Contributing

When contributing to the migration:

1. Reference the original Python code in `Libraries/PyKotor/src/pykotor/tslpatcher/`
2. Maintain functional equivalence
3. Follow C# coding conventions
4. Use async/await for I/O operations
5. Add XML documentation comments

## License

Same license as the original PyKotor project.
