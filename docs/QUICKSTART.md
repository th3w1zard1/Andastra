# HoloPatcher.NET Quick Start Guide

## Prerequisites

1. **Install .NET 8.0 SDK**
   - Download from: <https://dotnet.microsoft.com/download/dotnet/8.0>
   - Verify installation: `dotnet --version`

2. **IDE (Optional but Recommended)**
   - Visual Studio 2022 (Windows)
   - JetBrains Rider (Cross-platform)
   - VS Code with C# extension (Cross-platform)

## Building the Project

### Option 1: Using Scripts

**Windows (PowerShell)**:

```powershell
cd Tools\HoloPatcher.NET
.\build.ps1
```

**Linux/macOS (Bash)**:

```bash
cd Tools/HoloPatcher.NET
chmod +x build.sh
./build.sh
```

### Option 2: Using .NET CLI

```bash
cd Tools/HoloPatcher.NET
dotnet restore
dotnet build
```

## Running the Application

```bash
cd Tools/HoloPatcher.NET
dotnet run --project src/HoloPatcher/HoloPatcher.csproj
```

## Project Structure Overview

```sh
Tools/HoloPatcher.NET/
├── HoloPatcher.sln              # Solution file
├── src/
│   ├── HoloPatcher/             # Main UI application (Avalonia)
│   │   ├── Views/               # XAML views
│   │   ├── ViewModels/          # View models (MVVM)
│   │   ├── App.axaml            # Application definition
│   │   └── Program.cs           # Entry point
│   │
│   └── TSLPatcher.Core/         # Core patching library
│       ├── Config/              # Configuration models
│       ├── Logger/              # Logging system
│       ├── Memory/              # Token memory
│       ├── Namespaces/          # Namespace management
│       └── Mods/                # Modification operations (TODO)
│
├── README.md                    # Project documentation
├── MIGRATION_GUIDE.md           # Python → C# migration guide
└── QUICKSTART.md                # This file
```

## Development Workflow

### 1. Open in IDE

**Visual Studio 2022**:

- Open `HoloPatcher.sln`
- Press F5 to build and run

**JetBrains Rider**:

- Open `HoloPatcher.sln`
- Click the Run button or press Shift+F10

**VS Code**:

```bash
cd Tools/HoloPatcher.NET
code .
```

- Install C# extension
- Open Command Palette (Ctrl+Shift+P)
- Select ".NET: Generate Assets for Build and Debug"
- Press F5

### 2. Make Changes

The codebase follows MVVM (Model-View-ViewModel) pattern:

**To add new UI functionality**:

1. Add properties/commands to ViewModel
2. Update XAML to bind to new properties
3. Implement command logic

**To add patching features**:

1. Add models to `TSLPatcher.Core`
2. Implement business logic
3. Expose through ViewModels

### 3. Build and Test

```bash
# Build
dotnet build

# Run
dotnet run --project src/HoloPatcher/HoloPatcher.csproj

# (Future) Run tests
dotnet test
```

## Current Status

### ✅ Working Features

- Application launches with Avalonia UI
- Basic window layout (menu, namespace selection, log area)
- Logger system functional
- Configuration models in place
- Browse for mod directory (UI only)
- Browse for game path (UI only)

### 🚧 In Progress

- INI file parsing
- Mod loading logic
- Installation engine

### ⏳ TODO

- All patching operations (GFF, 2DA, TLK, NSS, NCS, SSF)
- Backup/restore functionality
- Tools menu implementations
- RTF file display

## Next Steps for Development

1. **Implement ConfigReader** (`TSLPatcher.Core/Config/ConfigReader.cs`)
   - Port INI parsing logic from Python
   - Reference: `Libraries/PyKotor/src/pykotor/tslpatcher/reader.py`

2. **Implement Namespace Loading**
   - Parse `namespaces.ini`
   - Parse `changes.ini`
   - Load mod information

3. **Port Modification Classes**
   - Start with simplest: InstallFile
   - Then: 2DA modifications
   - Then: GFF modifications
   - Finally: Script compilation

4. **Implement ModInstaller**
   - Resource lookup
   - Patch application
   - Progress tracking

See `MIGRATION_GUIDE.md` for detailed implementation guidance.

## Troubleshooting

### Build Errors

**"SDK not found"**:

- Ensure .NET 8.0 SDK is installed
- Run `dotnet --version` to verify

**"Package restore failed"**:

```bash
dotnet restore --force
dotnet build
```

### Runtime Errors

**"Application doesn't start"**:

- Check Program.cs entry point
- Verify App.axaml is set as AvaloniaResource

**"Window doesn't appear"**:

- Check MainWindow initialization in App.axaml.cs
- Verify XAML syntax in MainWindow.axaml

## Resources

- **Avalonia Docs**: <https://docs.avaloniaui.net/>
- **MVVM Toolkit**: <https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/>
- **.NET Docs**: <https://learn.microsoft.com/en-us/dotnet/>

## Getting Help

For issues or questions:

1. Check `MIGRATION_GUIDE.md` for implementation patterns
2. Review original Python source in `Libraries/PyKotor/src/pykotor/tslpatcher/`
3. Consult Avalonia documentation
4. Check .NET API documentation

## Contributing

When adding new features:

1. Follow existing code structure
2. Use MVVM pattern for UI code
3. Keep business logic in TSLPatcher.Core
4. Add XML documentation comments
5. Test with actual KOTOR mods when possible
