# GitHub Actions Workflows Documentation

This document describes the GitHub Actions workflows for HoloPatcher.NET.

## Overview

The project uses GitHub Actions for:
- **Continuous Integration**: Testing and validation on every PR
- **Automated Releases**: Using release-please for version management
- **Multi-Platform Builds**: Building for Windows, Linux, and macOS
- **Auto-Updates**: NetSparkle appcast generation

## Workflow Files

### CI Workflow (`ci.yml`)

Runs on every push and pull request:
- Restores dependencies
- Builds the solution
- Runs unit tests
- Validates code with analyzers

### Test Builds (`test-builds.yml`)

Tests builds on a representative subset of platforms:
- Windows 7 x64 (.NET Framework 4.8)
- Windows 10/11 x64 (.NET 9)
- Linux x64 (.NET 9)
- macOS arm64 (.NET 9)

Validates that:
- Code compiles successfully
- Tests pass
- Build artifacts are created
- Executables are valid

### Build All Platforms (`build-all-platforms.yml`)

Reusable workflow that builds all platform/architecture combinations:

**Windows 7** (requires .NET Framework 4.8):
- x64
- x86

**Windows 10/11** (.NET 9, self-contained):
- x64
- x86
- arm64

**Linux** (.NET 9, self-contained):
- x64
- arm64

**macOS** (.NET 9, self-contained):
- x64
- arm64

**Total: 10 build configurations**

### Release Please (`release-please.yml`)

Automated release management:
1. Monitors conventional commits (feat:, fix:, etc.)
2. Creates release PRs with version bumps
3. When PR is merged, triggers build workflow
4. Waits for all builds to complete
5. Downloads all artifacts
6. Creates GitHub Release with artifacts
7. Generates NetSparkle appcast
8. Uploads appcast to release

### NetSparkle Appcast (`netsparkle-appcast.yml`)

Generates `appcast.xml` for auto-updates:
- Runs when a release is published
- Uses NetSparkle tools to generate signed appcast
- Uploads appcast to release assets

## Setup Instructions

### 1. Generate NetSparkle Keys

```bash
dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator
netsparkle-generate-appcast --generate-keys --export true
```

### 2. Configure GitHub Secrets

Go to Repository Settings → Secrets and variables → Actions, and add:

- **NETSPARKLE_PRIVATE_KEY**: Base64-encoded Ed25519 private key
- **NETSPARKLE_PUBLIC_KEY**: Base64-encoded Ed25519 public key (for reference)

### 3. Update UpdateManager

In `src/HoloPatcher/UpdateManager.cs`, set:

```csharp
Ed25519PublicKey = "your_base64_public_key_here"
```

Or use an environment variable or configuration file.

### 4. Configure Release Please

The `release-please-config.json` is already configured. It will:
- Monitor commits for conventional commit messages
- Update version in `HoloPatcher.csproj`
- Create release PRs automatically

## Release Process

### Automatic Release (Recommended)

1. **Make changes** with conventional commit messages:
   ```bash
   git commit -m "feat: add new feature"
   git commit -m "fix: fix bug"
   ```

2. **Push to main/master**:
   ```bash
   git push origin main
   ```

3. **Release Please creates PR**: A PR will be created with version bump

4. **Review and merge**: Merge the PR to trigger release

5. **Build and release**: Workflows automatically:
   - Build all platforms
   - Create GitHub Release
   - Upload artifacts
   - Generate appcast

### Manual Release

1. **Update version** in `src/HoloPatcher/HoloPatcher.csproj`:
   ```xml
   <Version>2.0.0</Version>
   ```

2. **Create and push tag**:
   ```bash
   git tag v2.0.0
   git push origin v2.0.0
   ```

3. **Trigger build workflow**:
   ```bash
   gh workflow run build-all-platforms.yml -f version=2.0.0 -f tag_name=v2.0.0
   ```

4. **Create release manually** on GitHub with artifacts

## Conventional Commits

Release Please uses conventional commits to determine version bumps:

- `feat:` → Minor version bump (1.0.0 → 1.1.0)
- `fix:` → Patch version bump (1.0.0 → 1.0.1)
- `BREAKING CHANGE:` → Major version bump (1.0.0 → 2.0.0)

Examples:
```bash
git commit -m "feat: add auto-update support"
git commit -m "fix: resolve crash on startup"
git commit -m "feat!: redesign UI"  # Breaking change
```

## Platform-Specific Notes

### Windows 7

- Uses .NET Framework 4.8
- Requires MSBuild (automatically set up in workflow)
- Not self-contained (requires .NET Framework installed)

### Windows 10/11

- Uses .NET 9
- Self-contained (includes .NET runtime)
- Supports x64, x86, and arm64

### Linux

- Uses .NET 9
- Self-contained
- Supports x64 and arm64
- Requires glibc 2.17+

### macOS

- Uses .NET 9
- Self-contained
- Supports x64 (Intel) and arm64 (Apple Silicon)
- Requires macOS 10.13+

## Troubleshooting

### Build Failures

1. **Check workflow logs**: Go to Actions tab in GitHub
2. **Verify .NET SDK version**: Ensure workflow uses correct version
3. **Check platform compatibility**: Some features may not work on all platforms

### Release Issues

1. **Verify secrets**: Ensure NETSPARKLE_PRIVATE_KEY is set
2. **Check tag format**: Tags must start with 'v' (e.g., v2.0.0)
3. **Verify artifacts**: Ensure all builds completed successfully

### Appcast Generation

1. **Verify private key**: Must match public key in code
2. **Check release assets**: Artifacts must be available
3. **Validate appcast.xml**: Check syntax and signatures

## Testing Workflows

The `validate-workflows.yml` workflow:
- Validates YAML syntax
- Checks for required secrets
- Runs on workflow file changes

Run manually to validate:
```bash
gh workflow run validate-workflows.yml
```

## Artifact Retention

- **Build artifacts**: Retained for 30 days
- **Test artifacts**: Retained for 7 days
- **Appcast**: Retained for 90 days

## Performance

- **Build time**: ~10-15 minutes per platform
- **Total build time**: ~2-3 hours for all platforms (parallel)
- **Release time**: ~5 minutes after builds complete

## Security

- **Private keys**: Stored as GitHub Secrets (encrypted)
- **Signatures**: All appcast files are signed with Ed25519
- **Verification**: NetSparkle verifies signatures before updates

## Support

For issues with workflows:
1. Check workflow logs in GitHub Actions
2. Validate YAML syntax
3. Verify secrets are configured
4. Check platform-specific requirements

