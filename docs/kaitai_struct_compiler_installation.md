# Kaitai Struct Compiler Installation Guide

This guide explains how to install the Kaitai Struct compiler to enable the Kaitai Struct tests in the Andastra project.

## Prerequisites

The Kaitai Struct compiler requires **Java Runtime Environment (JRE) 8 or later** to run (Java 21 LTS recommended).

- **Windows**: Download JRE from [Oracle](https://www.oracle.com/java/technologies/downloads/) or use [Adoptium](https://adoptium.net/)
- **Linux**: `sudo apt install default-jre` (Debian/Ubuntu) or `sudo yum install java-11-openjdk` (RHEL/CentOS)
- **macOS**: `brew install openjdk` or download from Adoptium

Verify Java is installed:

```bash
java -version
```

## Installation Methods

### Method 1: Windows MSI Installer (Recommended for Windows)

1. Download the MSI installer from [GitHub Releases](https://github.com/kaitai-io/kaitai_struct_compiler/releases/latest):
   - **Direct link for v0.11**: <https://github.com/kaitai-io/kaitai_struct_compiler/releases/download/0.11/kaitai-struct-compiler-0.11.msi>
   - Or browse all releases: <https://github.com/kaitai-io/kaitai_struct_compiler/releases>

2. Run the MSI installer and follow the installation wizard

3. The compiler will be installed and added to your PATH automatically

**Note**: The compiler is distributed via GitHub Releases (moved from Bintray in May 2021). Package managers like Chocolatey and Scoop do not currently provide the Kaitai Struct compiler package.

### Method 2: Universal ZIP File (Portable, All Platforms)

This method works on Windows, Linux, and macOS and requires no installation:

1. Download the universal ZIP from [GitHub Releases](https://github.com/kaitai-io/kaitai_struct_compiler/releases/latest):
   - **Direct link for v0.11**: <https://github.com/kaitai-io/kaitai_struct_compiler/releases/download/0.11/kaitai-struct-compiler-0.11.zip>
   - Or browse all releases: <https://github.com/kaitai-io/kaitai_struct_compiler/releases>

2. Extract the ZIP file to a location of your choice:
   - Windows: `C:\Tools\kaitai-struct-compiler` or `%USERPROFILE%\.local\kaitai-struct-compiler`
   - Linux/macOS: `~/.local/kaitai-struct-compiler` or `/usr/local/kaitai-struct-compiler`

3. (Optional) Add the extracted directory to your PATH:
   - **Windows**: Add the directory to System Environment Variables (PATH)
   - **Linux/macOS**: Add to PATH in `~/.bashrc` or `~/.zshrc`:

     ```bash
     export PATH="$HOME/.local/kaitai-struct-compiler:$PATH"
     ```

The ZIP contains launcher scripts (`kaitai-struct-compiler.bat` on Windows, `kaitai-struct-compiler` on Linux/macOS) that work out of the box.

### Method 3: Linux Debian Package (.deb)

For Debian/Ubuntu systems:

```bash
# Download the latest .deb package (v0.11 example)
curl -fsSLO https://github.com/kaitai-io/kaitai_struct_compiler/releases/download/0.11/kaitai-struct-compiler_0.11_all.deb

# Install it
sudo apt-get install ./kaitai-struct-compiler_0.11_all.deb
```

**Note**: As of May 2021, packages are distributed via GitHub Releases, not traditional package repositories.

### Method 4: macOS Homebrew

```bash
brew install kaitai-struct-compiler
```

### Method 5: JAR File (Manual Setup)

If you prefer to use just the JAR file:

1. Download the JAR from [GitHub Releases](https://github.com/kaitai-io/kaitai_struct_compiler/releases/latest):
   - Look for `kaitai-struct-compiler-X.X.X.jar` (where X.X.X is the version number)
   - Note: The JAR may be inside the universal ZIP file

2. Place the JAR file in one of these locations:
   - `%USERPROFILE%\.kaitai\kaitai-struct-compiler.jar` (Windows)
   - `~/.kaitai/kaitai-struct-compiler.jar` (Linux/macOS)
   - Or set the `KAITAI_COMPILER_JAR` environment variable to point to the JAR file

3. (Optional) Create a wrapper script for easier use:

   **Windows (`kaitai-struct-compiler.bat`)**:

   ```batch
   @echo off
   java -jar "%USERPROFILE%\.kaitai\kaitai-struct-compiler.jar" %*
   ```

   **Linux/macOS (`kaitai-struct-compiler`)**:

   ```bash
   #!/bin/bash
   java -jar "$HOME/.kaitai/kaitai-struct-compiler.jar" "$@"
   ```

   Make it executable: `chmod +x kaitai-struct-compiler`

## Verification

After installation, verify the compiler is accessible:

```bash
kaitai-struct-compiler --version
```

Or if using the JAR directly:

```bash
java -jar kaitai-struct-compiler.jar --version
```

Expected output should show the compiler version (e.g., `0.11`).

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
- Check that the compiler exists in one of the expected locations
- Verify file permissions (Linux/macOS): `chmod +x kaitai-struct-compiler` (if using script)
- Try running directly: `java -jar path/to/kaitai-struct-compiler.jar --version`

### "Java not found" or version errors

- Ensure Java 8 or later is installed (Java 21 LTS recommended)
- Verify JAVA_HOME is set correctly (for some installations)
- On Windows, restart your terminal after installing Java

### Tests skip even with compiler installed

- The tests gracefully skip if the compiler isn't found (this is expected behavior)
- Check the test output for compiler detection messages
- Verify the compiler works manually before running tests

## Additional Resources

- [Kaitai Struct Official Website](https://kaitai.io/) - Main site with download links
- [Kaitai Struct Documentation](https://doc.kaitai.io/)
- [Kaitai Struct Compiler GitHub](https://github.com/kaitai-io/kaitai_struct_compiler)
- [GitHub Releases](https://github.com/kaitai-io/kaitai_struct_compiler/releases) - Download latest version
- [Kaitai Struct Web IDE](https://ide.kaitai.io/) - Test .ksy files online without installation
