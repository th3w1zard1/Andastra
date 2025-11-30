# GitHub Actions Workflows Summary

## Overview

This repository uses GitHub Actions for:
- ✅ Continuous Integration (CI)
- ✅ Multi-platform builds (10 configurations)
- ✅ Automated releases with release-please
- ✅ NetSparkle auto-update appcast generation

## Workflow Architecture

```
┌─────────────────┐
│  Push/PR        │
└────────┬────────┘
         │
         ├─→ ci.yml (tests, linting)
         └─→ test-builds.yml (build validation)
         
┌─────────────────┐
│  Release Please  │
│  (conventional  │
│   commits)      │
└────────┬────────┘
         │
         ├─→ Creates release PR
         │
         └─→ On merge:
             ├─→ build-all-platforms.yml (10 builds)
             ├─→ Upload artifacts to release
             └─→ Generate appcast.xml
```

## Build Matrix

| Platform | Arch | Framework | Self-Contained | RID |
|----------|------|-----------|----------------|-----|
| Windows 7 | x64 | .NET Framework 4.8 | No | win7-x64 |
| Windows 7 | x86 | .NET Framework 4.8 | No | win7-x86 |
| Windows 10/11 | x64 | .NET 9 | Yes | win-x64 |
| Windows 10/11 | x86 | .NET 9 | Yes | win-x86 |
| Windows 10/11 | arm64 | .NET 9 | Yes | win-arm64 |
| Linux | x64 | .NET 9 | Yes | linux-x64 |
| Linux | arm64 | .NET 9 | Yes | linux-arm64 |
| macOS | x64 | .NET 9 | Yes | osx-x64 |
| macOS | arm64 | .NET 9 | Yes | osx-arm64 |

**Total: 10 build configurations**

## File Structure

```
.github/
├── workflows/
│   ├── ci.yml                    # CI on every push/PR
│   ├── test-builds.yml           # Test builds on PRs
│   ├── build-all-platforms.yml  # Reusable build workflow
│   ├── release-please.yml       # Automated releases
│   ├── netsparkle-appcast.yml   # Appcast generation
│   └── validate-workflows.yml   # Workflow validation
├── release-please-config.json   # Release-please config
├── release-please-manifest.json  # Current version
├── RELEASE_TEMPLATE.md          # Release notes template
└── release-notes/               # Release notes for appcast
```

## Quick Start

1. **Generate keys**: `.\scripts\setup-netsparkle-keys.ps1 -Export`
2. **Add secrets**: GitHub Settings → Secrets → Actions
3. **Update code**: Set `Ed25519PublicKey` in `UpdateManager.cs`
4. **Test**: Create a PR to trigger test builds
5. **Release**: Use conventional commits, merge release PR

## Testing

Run validation script before pushing:
```powershell
.\scripts\test-workflows.ps1 -All
```

Or on Linux/macOS:
```bash
./scripts/test-workflows.sh --all
```

## Release Flow

1. Developer makes commits with conventional messages
2. Release-please creates PR with version bump
3. PR is reviewed and merged
4. Build workflow triggers automatically
5. All 10 platforms build in parallel
6. Artifacts uploaded to GitHub Release
7. Appcast generated and uploaded
8. Users receive auto-update notifications

## Troubleshooting

See [WORKFLOWS.md](../WORKFLOWS.md) for detailed troubleshooting guide.

