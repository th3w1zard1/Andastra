# KotorCLI for .NET

A comprehensive build tool for KOTOR projects with cli-compatible syntax, ported from PyKotor's KotorCLI to C#/.NET.

## Status

This is an **in-progress implementation**. The project structure and command stubs are in place, but many commands still need full implementation.

## Project Structure

- `Program.cs` - Main entry point with root command setup
- `Commands/` - All command implementations
- `Configuration/` - TOML configuration file parser (KotorCLIConfig)
- `Logging/` - Logger implementations (Standard, Verbose, Debug, Quiet)

## Commands

### Core Build Commands
- `config` - Configuration management (stub)
- `init` - Project initialization (stub)
- `list` - List targets (stub)
- `unpack` - Unpack archives (stub)
- `convert` - Convert JSON to GFF (stub)
- `compile` - Compile NSS scripts (stub)
- `pack` - Pack sources into modules (stub)
- `install` - Install to KOTOR directory (stub)
- `launch` - Launch game (stub)

### Archive Commands
- `extract` - Extract from archives (stub)
- `list-archive` - List archive contents (stub)
- `create-archive` - Create archives (stub)
- `search-archive` - Search archives (stub)
- `key-pack` - Create KEY files (stub)

### Format Conversion Commands
- `gff2json`, `json2gff` - GFF ↔ JSON (stub)
- `gff2xml`, `xml2gff` - GFF ↔ XML (stub)
- `tlk2xml`, `xml2tlk` - TLK ↔ XML (stub)
- `ssf2xml`, `xml2ssf` - SSF ↔ XML (stub)
- `2da2csv`, `csv22da` - 2DA ↔ CSV (stub)

### Script Tools
- `decompile` - Decompile NCS to NSS (stub)
- `disassemble` - Disassemble NCS (stub)
- `assemble` - Compile NSS to NCS (stub)

### Resource Tools
- `texture-convert` - Convert textures (stub)
- `sound-convert` - Convert sounds (stub)
- `model-convert` - Convert models (stub)

### Utilities
- `diff`, `grep`, `stats`, `validate`, `merge`, `cat` - Utility commands (stub)

### Validation
- `check-txi`, `check-2da` - Validation commands (stub)

## Known Issues

1. **System.CommandLine API Usage**: Many commands need their API usage corrected (use `.Options.Add()` instead of `.AddOption()`, etc.)
2. **Project Reference**: Path to Andastra.Parsing needs verification
3. **Implementation**: All commands are currently stubs and need full implementation

## Next Steps

1. Fix System.CommandLine API usage across all command files
2. Implement core commands (init, unpack, convert, compile, pack, install)
3. Integrate with Andastra.Parsing library for file operations
4. Implement format conversion commands
5. Add comprehensive testing

## References

Original Python implementation: `vendor/PyKotor/Tools/KotorCLI/`

