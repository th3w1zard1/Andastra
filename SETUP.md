# Setup Guide for GitHub Actions Workflows

This guide will help you set up the GitHub Actions workflows for automated builds and releases.

## Prerequisites

1. GitHub repository with Actions enabled
2. .NET 9 SDK installed locally (for key generation)
3. Administrator access to repository settings

## Step 1: Generate NetSparkle Keys

### Windows (PowerShell)

```powershell
.\scripts\setup-netsparkle-keys.ps1 -Export
```

### Linux/macOS (Bash)

```bash
chmod +x scripts/setup-netsparkle-keys.sh
./scripts/setup-netsparkle-keys.sh --export
```

This will:

- Install NetSparkle tools if needed
- Generate Ed25519 key pair
- Display both keys
- Save keys to `keys/` directory (already in .gitignore)

## Step 2: Configure GitHub Secrets

1. Go to your repository on GitHub
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**

### Add NETSPARKLE_PRIVATE_KEY

- **Name**: `NETSPARKLE_PRIVATE_KEY`
- **Value**: The private key from Step 1 (base64 string)
- Click **Add secret**

### (Optional) Add NETSPARKLE_PUBLIC_KEY

- **Name**: `NETSPARKLE_PUBLIC_KEY`
- **Value**: The public key from Step 1 (base64 string)
- Click **Add secret**

⚠️ **Important**: Never commit the private key to the repository!

## Step 3: Update UpdateManager.cs

Open `src/HoloPatcher/UpdateManager.cs` and set the public key:

```csharp
Ed25519PublicKey = "your_base64_public_key_here"
```

Replace `your_base64_public_key_here` with the public key from Step 1.

## Step 4: Verify Workflow Files

The following workflow files should be in `.github/workflows/`:

- ✅ `ci.yml` - Continuous integration
- ✅ `test-builds.yml` - Test builds on PRs
- ✅ `build-all-platforms.yml` - Reusable build workflow
- ✅ `release-please.yml` - Automated releases
- ✅ `netsparkle-appcast.yml` - Appcast generation
- ✅ `validate-workflows.yml` - Workflow validation

## Step 5: Test the Setup

### Test CI Workflow

1. Create a test branch:

   ```bash
   git checkout -b test/workflow-setup
   ```

2. Make a small change and commit:

   ```bash
   git commit -m "test: verify workflows"
   ```

3. Push and create a PR:

   ```bash
   git push origin test/workflow-setup
   ```

4. Check the Actions tab to verify:
   - CI workflow runs
   - Test builds workflow runs
   - All checks pass

### Test Release Process

1. Make a commit with conventional commit message:

   ```bash
   git commit -m "feat: test release process"
   git push origin main
   ```

2. Release Please will create a PR with version bump

3. Review and merge the PR

4. Verify:
   - Build workflow triggers
   - All platforms build successfully
   - Release is created
   - Artifacts are uploaded
   - Appcast is generated

## Troubleshooting

### Keys Not Working

- Verify keys are base64-encoded
- Ensure no extra whitespace in secrets
- Check that public key matches private key

### Build Failures

- Check workflow logs in Actions tab
- Verify .NET SDK versions are correct
- Ensure all required tools are available

### Release Issues

- Verify release-please configuration
- Check that conventional commits are used
- Ensure secrets are properly configured

### Appcast Generation Fails

- Verify NETSPARKLE_PRIVATE_KEY secret is set
- Check that release assets are available
- Ensure NetSparkle tools are installed in workflow

## Platform Support

The workflows build for:

| Platform | Architectures | Framework | Self-Contained |
|----------|--------------|-----------|----------------|
| Windows 7 | x64, x86 | .NET Framework 4.8 | No |
| Windows 10/11 | x64, x86, arm64 | .NET 9 | Yes |
| Linux | x64, arm64 | .NET 9 | Yes |
| macOS | x64, arm64 | .NET 9 | Yes |

**Total: 10 build configurations**

## Next Steps

After setup is complete:

1. ✅ Workflows are configured
2. ✅ Secrets are set
3. ✅ Keys are generated
4. ✅ UpdateManager is configured

You're ready to use automated builds and releases!

For more information, see:

- [WORKFLOWS.md](WORKFLOWS.md) - Detailed workflow documentation
- [AUTOUPDATE.md](AUTOUPDATE.md) - NetSparkle auto-update guide
