# KNCSDecomp Tests

This directory contains comprehensive tests for the KNCSDecomp application.

## Test Structure

- **FileDecompilerTests.cs** - Unit tests for FileDecompiler class
- **Integration/FileDecompilerIntegrationTests.cs** - Integration tests with real NCS files
- **NWScriptLocatorTests.cs** - Tests for NWScriptLocator utility
- **SettingsTests.cs** - Tests for Settings class
- **UI/MainWindowHeadlessTests.cs** - Headless UI tests for MainWindow

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~FileDecompilerTests"
```

## Test Files

Test NCS files should be placed in `test_files/` directory. The test file `a_galaxymap.ncs` should be copied from:
`G:\GitHub\PyKotor\vendor\Kotor-Randomizer\kotor Randomizer 2\Resources\k2patch\a_galaxymap.ncs`

## Headless UI Testing

UI tests use Avalonia.Headless to run without a display server, making them suitable for CI/CD environments.

