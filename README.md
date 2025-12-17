# Andastra

A unified game engine runtime and development tooling suite for BioWare's engine families (Odyssey, Aurora, Eclipse, Infinity), built with .NET and MonoGame.

## Overview

Andastra is a .NET implementation providing a modern, cross-platform unified runtime for games built on BioWare's engine architectures. Similar in scope to projects like Xoreos, Andastra aims to create complete, faithful reimplementations of multiple BioWare engine families with a shared codebase and common abstractions.

The project unifies support for four major BioWare engine families:

- **Odyssey Engine**: Knights of the Old Republic (KOTOR), KOTOR II: The Sith Lords (TSL), Jade Empire
- **Aurora Engine**: Neverwinter Nights, Neverwinter Nights 2
- **Eclipse Engine**: Dragon Age series, Mass Effect series
- **Infinity Engine**: Baldur's Gate, Icewind Dale, Planescape: Torment

Currently, the Odyssey engine implementation is the most mature, with full support for KOTOR and TSL. The other engine families have foundational implementations with ongoing development. The project includes both the game engine runtime and a comprehensive suite of development tools for modding and content creation.

### Project Name

**Andastra** is named to reflect the project's foundational principles and its technical heritage:

- **Logical AND**: Symbolizes the intersection and integration of diverse systems, technologies, and approaches unified within this project.
- **Astraea**: In Greek mythology, Astraea is the daughter of Eos (goddess of the dawn). Similar to how Xoreos (which inspired our project's name) derives from XOR + EOS, the name "Andastra" draws inspiration from this mythological connection. This link emphasizes Andastra.NET's commitment to accuracy, justice, and the technical legacy of its predecessors.
- **Andraste**: References the Celtic war goddess, a symbol of determination and strength, and connects to BioWare lore as "The Maker" in the Dragon Age series.

This name tributes Xoreos and reflects the project's goal of bringing together modern .NET technologies with the precision and faithfulness required to recreate BioWare's classic game engines across multiple engine families.

### Supported Engine Families

Andastra provides a unified runtime architecture that supports multiple BioWare engine families through a common abstraction layer:

- **Odyssey Engine** (Primary Implementation): Full support for KOTOR and TSL with area rendering, navigation, scripting, dialogue, combat, and save/load systems
- **Aurora Engine** (In Development): Foundation for Neverwinter Nights and NWN2 support
- **Eclipse Engine** (In Development): Foundation for Dragon Age and Mass Effect series support
- **Infinity Engine** (In Development): Foundation for classic Infinity Engine games support

### Core Components

- **Unified Engine Runtime**: A multi-engine runtime architecture with engine-specific implementations sharing common abstractions
- **Development Tools**: A collection of utilities for modding, script compilation, file format manipulation, and content creation
- **File Format Support**: Complete parsing and manipulation support for all game file formats across engine families (GFF, 2DA, TLK, MDL, BWM, NCS, ERF, and more)

## Architecture

### Runtime Structure

The Andastra runtime is organized into a layered architecture with strict dependency rules:

```sh
┌─────────────────────────────────────────────────────────────┐
│                    Andastra.Game (Executable)                │
├─────────────────────────────────────────────────────────────┤
│  Runtime.Graphics   │  Runtime.Games      │  Runtime.Content│
│  (Rendering)         │  (Game Rules)       │  (Asset Pipeline)│
├─────────────────────────────────────────────────────────────┤
│                    Runtime.Scripting (NCS VM)               │
├─────────────────────────────────────────────────────────────┤
│                    Runtime.Core (Domain)                     │
├─────────────────────────────────────────────────────────────┤
│                    Parsing (File Formats)                    │
└─────────────────────────────────────────────────────────────┘
```

### Project Organization

**Runtime Projects:**

- `Andastra.Runtime.Core` - Pure domain logic, no external dependencies
- `Andastra.Runtime.Content` - Asset conversion and caching pipeline
- `Andastra.Runtime.Scripting` - NCS virtual machine and NWScript execution
- `Andastra.Runtime.Graphics` - Rendering backends (MonoGame, Stride)
- `Andastra.Runtime.Games.Common` - Common abstractions and base classes for all engine families
- `Andastra.Runtime.Games.Odyssey` - Odyssey engine implementation (KOTOR, TSL, Jade Empire)
- `Andastra.Runtime.Games.Aurora` - Aurora engine implementation (NWN, NWN2)
- `Andastra.Runtime.Games.Eclipse` - Eclipse engine implementation (Dragon Age, Mass Effect)
- `Andastra.Runtime.Games.Infinity` - Infinity engine implementation (Baldur's Gate, Icewind Dale, etc.)
- `Andastra.Game` - Main executable and game launcher

**Supporting Projects:**

- `Andastra.Parsing` - File format parsers and resource management
- `Andastra.Tests` - Unit and integration tests

**Development Tools:**

- `HoloPatcher.UI` - Mod installation and patching tool
- `NCSDecomp` - NWScript bytecode decompiler
- `KNSSComp.NET` - NWScript compiler
- `HolocronToolset.NET` - Content creation and editing tools
- `KotorDiff.NET` - File comparison and diff tool

## Features

### Engine Runtime

- **Area System**: Complete area loading with LYT layout, VIS visibility culling, and room mesh rendering
- **Navigation**: Walkmesh-based pathfinding with A* algorithm, surface material support, and dynamic obstacle handling
- **Entity System**: Component-based architecture supporting creatures, doors, placeables, triggers, waypoints, and more
- **Scripting**: Full NCS virtual machine implementation with NWScript engine API surface
- **Dialogue**: DLG conversation system with voice-over playback and lip-sync support
- **Combat**: Round-based combat system with d20 mechanics, damage calculation, and effect application
- **Save/Load**: Complete save game serialization compatible with original game formats
- **Mod Support**: Full resource precedence chain (override → module → save → chitin) matching original behavior

### Development Tools

- **HoloPatcher**: Comprehensive mod installation tool with support for 2DA, GFF, TLK, NSS/NCS, and SSF modifications
- **NCSDecomp**: Decompile NWScript bytecode back to source with full instruction analysis
- **Script Compiler**: Compile NWScript source files to bytecode with KOTOR 1/2 compatibility
- **Format Tools**: Read, write, and manipulate all game file formats

## Requirements

- **.NET 8.0 SDK** or later
- **MonoGame 3.8** or later (for runtime)
- **Visual Studio 2022** or **JetBrains Rider** (recommended for development)

## Building

### Quick Start

```bash
# Clone the repository
git clone <repository-url>
cd HoloPatcher.NET

# Restore dependencies
dotnet restore

# Build the solution
dotnet build Andastra.sln

# Run the game
dotnet run --project src/Andastra/Game/Andastra.Game.csproj
```

### Building Specific Components

```bash
# Build only the runtime
dotnet build src/Andastra/Runtime/

# Build only the tools
dotnet build src/Tools/

# Build with release configuration
dotnet build Andastra.sln --configuration Release
```

## Running

### Game Runtime

```bash
# Run the game (requires game installation)
dotnet run --project src/Andastra/Game/Andastra.Game.csproj

# Or specify game path
dotnet run --project src/Andastra/Game/Andastra.Game.csproj -- --game-path "C:\Games\KOTOR"
```

### Development Tools

```bash
# Run HoloPatcher
dotnet run --project src/Tools/HoloPatcher.UI/HoloPatcher.UI.csproj

# Run NCSDecomp
dotnet run --project src/Tools/NCSDecomp/NCSDecomp.csproj

# Run script compiler
dotnet run --project src/Tools/KNSSComp.NET/KNSSComp.NET.csproj
```

## Project Structure

```sh
Andastra/
├── src/
│   ├── Andastra/
│   │   ├── Game/              # Main executable
│   │   ├── Runtime/           # Engine runtime
│   │   │   ├── Core/          # Domain logic
│   │   │   ├── Content/       # Asset pipeline
│   │   │   ├── Scripting/     # NCS VM
│   │   │   ├── Graphics/      # Rendering backends
│   │   │   └── Games/          # Game implementations
│   │   ├── Parsing/           # File format parsers
│   │   ├── Tests/             # Unit tests
│   │   └── Utility/           # Shared utilities
│   └── Tools/                 # Development tools
│       ├── HoloPatcher.UI/
│       ├── NCSDecomp/
│       ├── KNSSComp.NET/
│       └── ...
├── docs/                      # Documentation
├── scripts/                   # Build and utility scripts
└── vendor/                    # Third-party dependencies
```

## Development

### Code Standards

- **C# Language Version**: Maximum C# 7.3 (for .NET Framework 4.x compatibility)
- **Architecture**: Layered architecture with strict dependency rules
- **Testing**: Comprehensive unit and integration tests
- **Documentation**: XML documentation comments for public APIs

### Key Design Principles

1. **Unified Multi-Engine Architecture**: Common abstractions enable support for multiple BioWare engine families with shared code
2. **Layered Architecture**: Core domain logic is independent of rendering and game-specific code
3. **Component-Based Entities**: Entity system uses composition over inheritance
4. **Resource Precedence**: Matches original game resource loading behavior exactly for each engine
5. **Script Compatibility**: NCS VM maintains bytecode compatibility with original engines
6. **Mod Support**: Full compatibility with existing mod formats and tools across all supported engines

### Adding New Features

When implementing new engine features:

1. **Core Domain First**: Implement pure domain logic in `Runtime.Core`
2. **Content Pipeline**: Add asset conversion in `Runtime.Content`
3. **Engine Abstraction**: Add common interfaces in `Runtime.Games.Common` if needed
4. **Engine Implementation**: Implement engine-specific behavior in the appropriate `Runtime.Games.{Engine}` project
5. **Rendering**: Add MonoGame adapters in `Runtime.Graphics.MonoGame`
6. **Tests**: Write comprehensive tests for deterministic logic

## Testing

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run specific test project
dotnet test src/Andastra/Tests/Andastra.Tests.csproj

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Documentation

- **[Quick Start Guide](docs/QUICKSTART.md)** - Getting started with development
- **[Engine Roadmap](docs/engine_roadmap.md)** - Implementation roadmap and status
- **[Architecture Documentation](docs/)** - Detailed architecture and design documents

## Contributing

When contributing to Andastra:

1. Follow the layered architecture and dependency rules
2. Maintain C# 7.3 compatibility
3. Write tests for new features
4. Document public APIs with XML comments
5. Match original engine behavior where applicable
6. Keep engine-specific logic in the appropriate `Runtime.Games.{Engine}` project
7. Use common abstractions in `Runtime.Games.Common` for shared functionality across engines

## License

This project is licensed under the Business Source License 1.1 (BSL-1.1). See the [LICENSE](LICENSE) file for details.

**Important**: The BSL is not an Open Source license. The Licensed Work will transition to the GNU General Public License v2.0 or later on 2029-12-31 (Change Date).

**Production Use**: Use of this software in a production environment, to provide services to third parties, or to generate revenue requires explicit authorization from the Licensor.

## Status

Andastra is under active development. The Odyssey engine implementation is the most mature, with core systems functional for KOTOR and TSL. The Aurora, Eclipse, and Infinity engine families have foundational implementations with ongoing development. See the [engine roadmap](docs/engine_roadmap.md) for detailed implementation status across all engine families.
