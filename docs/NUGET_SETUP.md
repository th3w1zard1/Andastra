# NuGet API Key Setup

This guide explains how to configure your NuGet API key for automatic authentication when pushing packages.

## Option 1: Secure Configuration (Recommended)

Use the setup script to store your API key securely in your user NuGet.config:

```powershell
.\setup-nuget-key.ps1
```

This will:

- Prompt you for your API key (input is hidden)
- Store it securely in your user NuGet configuration
- Allow `dotnet nuget push` to work without specifying `--api-key`

After running this, you can simply use:

```powershell
.\build-nuget.ps1 --publish
```

## Option 2: Environment Variable

Set the `NUGET_API_KEY` environment variable:

**PowerShell:**

```powershell
$env:NUGET_API_KEY = "your_api_key_here"
```

**Windows (Permanent):**

```powershell
[Environment]::SetEnvironmentVariable("NUGET_API_KEY", "your_api_key_here", "User")
```

**Linux/macOS:**

```bash
export NUGET_API_KEY="your_api_key_here"
# Add to ~/.bashrc or ~/.zshrc for persistence
```

Then run:

```powershell
.\build-nuget.ps1 --publish
```

## Option 3: Command Line Parameter

Pass the API key directly:

```powershell
.\build-nuget.ps1 --publish --api-key YOUR_API_KEY
```

## Option 4: .env File (Local Development)

Create a `.env` file in the project root (already in .gitignore):

```
NUGET_API_KEY=your_api_key_here
NUGET_SOURCE=https://api.nuget.org/v3/index.json
```

The build script will automatically load this file.

## Getting Your NuGet API Key

1. Sign in to [nuget.org](https://www.nuget.org)
2. Go to Account Settings → API Keys
3. Create a new API key (or use an existing one)
4. Copy the key (you won't be able to see it again!)

## Priority Order

The build script checks for API key in this order:

1. `--api-key` parameter (highest priority)
2. `NUGET_API_KEY` environment variable
3. `.env` file (`NUGET_API_KEY=...`)
4. NuGet.config (if configured via `setup-nuget-key.ps1`)

## Verifying Configuration

After setup, test with:

```powershell
dotnet nuget push test.nupkg --source https://api.nuget.org/v3/index.json
```

If configured correctly, this should work without `--api-key`.
