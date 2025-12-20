# Kaitai Struct Compiler Installation Guide

This guide explains how to install the Kaitai Struct compiler to enable the Kaitai Struct tests in the Andastra project.

## Prerequisites

The Kaitai Struct compiler requires **Java Runtime Environment (JRE) 8 or later** to run.

- **Windows**: Download JRE from [Oracle](https://www.oracle.com/java/technologies/downloads/) or use [Adoptium](https://adoptium.net/)
- **Linux**: `sudo apt install default-jre` (Debian/Ubuntu) or `sudo yum install java-11-openjdk` (RHEL/CentOS)
- **macOS**: `brew install openjdk` or download from Adoptium

Verify Java is installed:
```bash
java -version
```

## Installation Methods

### Method 1: Download JAR File (Recommended)

1. Download the latest compiler JAR from [GitHub Releases](https://github.com/kaitai-io/kaitai_struct_compiler/releases/latest)
   - Look for `kaitai-struct-compiler-X.X.X.jar` (where X.X.X is the version number)

2. Place the JAR file in one of these locations:
   - `%USERPROFILE%\.kaitai\kaitai-struct-compiler.jar` (Windows)
   - `~/.kaitai/kaitai-struct-compiler.jar` (Linux/macOS)
   - Or set the `KAITAI_COMPILER_JAR` environment variable to point to the JAR file

3. (Optional) Create a wrapper script for easier use:

   **Windows (`kaitai-struct-compiler.bat`)**:
   ```batch
   @echo off
   java -jar "%~dp0kaitai-struct-compiler.jar" %*
   ```

   **Linux/macOS (`kaitai-struct-compiler`)**:
   ```bash
   #!/bin/bash
   java -jar "$(dirname "$0")/kaitai-struct-compiler.jar" "$@"
   ```
   Make it executable: `chmod +x kaitai-struct-compiler`

### Method 2: Package Manager Installation

#### Windows (via Chocolatey)
```powershell
choco install kaitai-struct-compiler
```

#### Windows (via Scoop)
```powershell
scoop install kaitai-struct-compiler
```

#### Linux (via Package Manager)

**Debian/Ubuntu** (if available in repositories):
```bash
sudo apt update
sudo apt install kaitai-struct-compiler
```

**Manual Debian Package Installation**:
```bash
wget https://packages.kaitai.io/dists/unstable/main/binary-amd64/kaitai-struct-compiler_0.10_all.deb
sudo dpkg -i kaitai-struct-compiler_0.10_all.deb
```

#### macOS (via Homebrew)
```bash
brew install kaitai-struct-compiler
```

### Method 3: Build from Source

If you need the latest development version:

1. Clone the repository:
   ```bash
   git clone https://github.com/kaitai-io/kaitai_struct_compiler.git
   cd kaitai_struct_compiler
   ```

2. Build using Gradle:
   ```bash
   ./gradlew build
   ```

3. The compiled JAR will be in `build/libs/kaitai-struct-compiler-*.jar`

## Verification

After installation, verify the compiler is accessible:

```bash
kaitai-struct-compiler --version
```

Or if using the JAR directly:
```bash
java -jar kaitai-struct-compiler.jar --version
```

Expected output should show the compiler version (e.g., `0.10`).

## Environment Variable (Optional)

If you installed the JAR in a custom location, you can set an environment variable:

**Windows**:
```powershell
[System.Environment]::SetEnvironmentVariable('KAITAI_COMPILER_JAR', 'C:\path\to\kaitai-struct-compiler.jar', 'User')
```

**Linux/macOS**:
```bash
export KAITAI_COMPILER_JAR=/path/to/kaitai-struct-compiler.jar
echo 'export KAITAI_COMPILER_JAR=/path/to/kaitai-struct-compiler.jar' >> ~/.bashrc  # or ~/.zshrc
```

## Running Tests

Once installed, the Kaitai Struct compiler tests will automatically detect and use it:

```bash
dotnet test src/Andastra/Tests/TSLPatcher.Tests.csproj --filter "FullyQualifiedName~KaitaiCompilerTests"
```

The tests check for the compiler in these locations (in order):
1. `kaitai-struct-compiler` command in PATH
2. `KAITAI_COMPILER_JAR` environment variable
3. Common installation locations:
   - `%USERPROFILE%\.kaitai\kaitai-struct-compiler.jar` (Windows)
   - `~/.kaitai/kaitai-struct-compiler.jar` (Linux/macOS)
   - `%USERPROFILE%\.local\bin\kaitai-struct-compiler` (Windows)
   - `~/.local/bin/kaitai-struct-compiler` (Linux/macOS)
   - `C:\Program Files\kaitai-struct-compiler\kaitai-struct-compiler.jar` (Windows)
   - Test project directory and parent directories

## Troubleshooting

### "kaitai-struct-compiler not found"

- Ensure Java is installed and in PATH: `java -version`
- Check that the JAR file exists in one of the expected locations
- Verify file permissions (Linux/macOS): `chmod +x kaitai-struct-compiler.jar` (if using as executable)
- Try running directly: `java -jar path/to/kaitai-struct-compiler.jar --version`

### "Java not found" or version errors

- Ensure Java 8 or later is installed
- Verify JAVA_HOME is set correctly (for some installations)
- On Windows, restart your terminal after installing Java

### Tests skip even with compiler installed

- The tests gracefully skip if the compiler isn't found (this is expected behavior)
- Check the test output for compiler detection messages
- Verify the compiler works manually before running tests

## Additional Resources

- [Kaitai Struct Documentation](https://doc.kaitai.io/)
- [Kaitai Struct Compiler GitHub](https://github.com/kaitai-io/kaitai_struct_compiler)
- [Kaitai Struct Web IDE](https://ide.kaitai.io/) - Test .ksy files online without installation

