# GitHub Actions Workflows

This directory contains GitHub Actions workflows for building, testing, and releasing HoloPatcher.

## Workflows

### `ci.yml`

Runs on every push and pull request to main/master:

- Runs unit tests
- Validates code with analyzers
- Ensures code quality

### `test-builds.yml`

Tests builds on a subset of platforms for PRs:

- Windows 7 (x64) - .NET Framework 4.8
- Windows 10/11 (x64) - .NET 9
- Linux (x64) - .NET 9
- macOS (arm64) - .NET 9

### `build-all-platforms.yml`

Reusable workflow that builds all platform/architecture combinations:

- **Windows 7**: x64, x86 (NET Framework 4.8)
- **Windows 10/11**: x64, x86, arm64 (.NET 9)
- **Linux**: x64, arm64 (.NET 9)
- **macOS**: x64, arm64 (.NET 9)

**Total: 10 build configurations**

### `release-please.yml`

Automated release management:

- Creates releases based on conventional commits
- Triggers build workflow
- Uploads artifacts to releases
- Generates NetSparkle appcast

### `netsparkle-appcast.yml`

Generates appcast.xml for NetSparkle auto-updates when a release is published.

### `validate-workflows.yml`

Validates workflow YAML syntax and checks for required secrets.

## Required Secrets

Configure these secrets in your repository settings:

- `NETSPARKLE_PRIVATE_KEY`: Ed25519 private key for signing appcast (base64)
- `NETSPARKLE_PUBLIC_KEY`: Ed25519 public key (for reference, used in code)

## Release Process

1. **Conventional Commits**: Use conventional commit messages (feat:, fix:, etc.)
2. **Release Please**: Automatically creates PRs with version bumps
3. **Merge PR**: Merging the release PR triggers the build workflow
4. **Build**: All platforms are built in parallel
5. **Release**: Artifacts are uploaded to GitHub Release
6. **Appcast**: NetSparkle appcast is generated and uploaded

## Manual Release

To manually trigger a release build:

```bash
gh workflow run build-all-platforms.yml -f version=2.0.0 -f tag_name=v2.0.0
```

## Platform Support Matrix

| Platform | Architectures | Framework | Self-Contained |
|----------|--------------|-----------|---------------|
| Windows 7 | x64, x86 | .NET Framework 4.8 | No |
| Windows 10/11 | x64, x86, arm64 | .NET 9 | Yes |
| Linux | x64, arm64 | .NET 9 | Yes |
| macOS | x64, arm64 | .NET 9 | Yes |

## Testing

The `test-builds.yml` workflow validates that:

- Code compiles on all target platforms
- Tests pass
- Build artifacts are created correctly
- Executables are non-empty

Run before merging PRs to ensure compatibility.
