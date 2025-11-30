# Auto-Update Configuration with NetSparkle

HoloPatcher uses [NetSparkle](https://github.com/netsparkleupdater/netsparkle) for industry-standard automatic updates. This document explains how to set up and configure the auto-update system.

## Overview

NetSparkle provides:

- Automatic update checking in the background
- Secure signature verification (Ed25519)
- Cross-platform support (Windows, macOS, Linux)
- User-friendly update dialogs
- Support for stable and beta release channels

## Setup

### 1. Generate Ed25519 Keys

First, install the NetSparkle tools:

```bash
dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator
```

Generate a key pair for signing updates:

```bash
netsparkle-generate-appcast --generate-keys
```

This will create:

- **Public key**: Use this in your application (store in `UpdateManager.Ed25519PublicKey`)
- **Private key**: Keep this secret and use it to sign appcast files

To export keys to console:

```bash
netsparkle-generate-appcast --generate-keys --export true
```

### 2. Create Appcast File

The appcast is an XML file that describes available updates. Generate it using:

```bash
netsparkle-generate-appcast \
  -b path/to/release/binaries \
  -p path/to/release/notes \
  -k path/to/private_key.txt \
  -o appcast.xml
```

**Parameters:**

- `-b` or `--binary-path`: Directory containing release binaries
- `-p` or `--changelog-path`: Directory containing release notes/changelogs
- `-k` or `--private-key`: Path to Ed25519 private key file
- `-o` or `--output`: Output appcast.xml file path
- `--human-readable`: Format XML with indentation (optional)

**Example:**

```bash
netsparkle-generate-appcast \
  -b dist/win-x64 \
  -p docs/releases \
  -k keys/private_key.txt \
  -o appcast.xml \
  --human-readable true
```

### 3. Host Appcast File

Upload the `appcast.xml` file to a publicly accessible URL. Common options:

- **GitHub Releases**: Upload as a release asset
- **GitHub Pages**: Host in a `gh-pages` branch
- **CDN/Web Server**: Host on your own infrastructure

**Recommended GitHub Setup:**

1. Create a release on GitHub
2. Upload `appcast.xml` as a release asset
3. Use the direct download URL: `https://github.com/user/repo/releases/download/tag/appcast.xml`

### 4. Configure Application

The `UpdateManager` is automatically initialized in `App.axaml.cs`. To customize:

```csharp
_updateManager = new UpdateManager
{
    AppcastUrl = "https://your-domain.com/appcast.xml",
    BetaAppcastUrl = "https://your-domain.com/appcast-beta.xml",
    UseBetaChannel = false, // Set to true for beta channel
    CheckOnStartup = true,
    SilentCheck = true,
    Ed25519PublicKey = "your_base64_public_key_here"
};
```

### 5. Appcast XML Structure

The generated appcast.xml follows this structure:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
  <channel>
    <title>HoloPatcher Updates</title>
    <link>https://github.com/th3w1zard1/HoloPatcher.NET</link>
    <description>HoloPatcher update feed</description>
    <language>en</language>
    <item>
      <title>Version 2.0.0</title>
      <pubDate>Mon, 01 Jan 2024 00:00:00 +0000</pubDate>
      <sparkle:version>2.0.0</sparkle:version>
      <sparkle:shortVersionString>2.0.0</sparkle:shortVersionString>
      <sparkle:minimumSystemVersion>Windows 10</sparkle:minimumSystemVersion>
      <enclosure 
        url="https://github.com/user/repo/releases/download/v2.0.0/HoloPatcher-2.0.0-win-x64.zip"
        sparkle:version="2.0.0"
        sparkle:shortVersionString="2.0.0"
        sparkle:os="windows"
        sparkle:edSignature="signature_here"
        length="12345678"
        type="application/zip" />
      <sparkle:releaseNotesLink>https://github.com/user/repo/releases/tag/v2.0.0</sparkle:releaseNotesLink>
    </item>
  </channel>
</rss>
```

## Release Channels

### Stable Channel

- **Appcast URL**: `AppcastUrl` property
- **Default**: Used when `UseBetaChannel = false`
- **Recommended for**: Production releases

### Beta Channel

- **Appcast URL**: `BetaAppcastUrl` property
- **Enable**: Set `UseBetaChannel = true`
- **Recommended for**: Pre-release testing

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Generate Appcast

on:
  release:
    types: [published]

jobs:
  generate-appcast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Install NetSparkle Tools
        run: dotnet tool install -g NetSparkleUpdater.Tools.AppCastGenerator
      
      - name: Generate Appcast
        run: |
          netsparkle-generate-appcast \
            -b dist/win-x64 \
            -p docs/releases \
            -k ${{ secrets.NETSPARKLE_PRIVATE_KEY }} \
            -o appcast.xml
      
      - name: Upload Appcast
        uses: actions/upload-release-asset@v1
        with:
          upload_url: ${{ github.event.release.upload_url }}
          asset_path: ./appcast.xml
          asset_name: appcast.xml
          asset_content_type: application/xml
```

### Secrets Management

Store sensitive keys in GitHub Secrets:

- `NETSPARKLE_PRIVATE_KEY`: Base64-encoded Ed25519 private key
- `NETSPARKLE_PUBLIC_KEY`: Base64-encoded Ed25519 public key (for reference)

## Security Best Practices

1. **Always use signature verification in production**:
   - Set `Ed25519PublicKey` with your public key
   - Use `SecurityMode.Strict` (default when key is provided)

2. **Keep private key secure**:
   - Never commit private keys to repository
   - Use secrets management in CI/CD
   - Rotate keys if compromised

3. **Use HTTPS for appcast URLs**:
   - Prevents man-in-the-middle attacks
   - Ensures appcast integrity

4. **Test updates before release**:
   - Test update process in staging environment
   - Verify signature verification works
   - Ensure installer works correctly

## Manual Update Check

Users can manually check for updates by calling:

```csharp
_updateManager.CheckForUpdates();
```

This can be triggered from a menu item or button in the UI.

## Troubleshooting

### Updates Not Detected

1. **Check appcast URL**: Verify the URL is accessible
2. **Verify version format**: Use semantic versioning (e.g., `2.0.0`)
3. **Check signature**: Ensure appcast is properly signed
4. **Review logs**: Check application logs for update check errors

### Signature Verification Fails

1. **Verify public key**: Ensure public key matches private key used for signing
2. **Check key format**: Public key must be base64-encoded
3. **Regenerate keys**: If keys don't match, regenerate and update both

### UI Not Showing

1. **Check UI thread**: Ensure `UpdateManager` is initialized on UI thread
2. **Verify UIFactory**: If using custom UI, ensure factory is properly implemented
3. **Check silent mode**: `SilentCheck = true` may suppress UI

## Additional Resources

- [NetSparkle Documentation](https://github.com/netsparkleupdater/netsparkle)
- [Appcast Generator Tool](https://www.nuget.org/packages/NetSparkleUpdater.Tools.AppCastGenerator)
- [Sparkle Project (macOS)](https://sparkle-project.org/) - Original inspiration

## Configuration in Code

The update manager is configured in `App.axaml.cs`:

```csharp
private void InitializeUpdateManager()
{
    _updateManager = new UpdateManager
    {
        AppcastUrl = "https://github.com/th3w1zard1/HoloPatcher.NET/releases/latest/download/appcast.xml",
        BetaAppcastUrl = "https://github.com/th3w1zard1/HoloPatcher.NET/releases/download/bleeding-edge/appcast-beta.xml",
        CheckOnStartup = true,
        SilentCheck = true,
        UseBetaChannel = false,
        Ed25519PublicKey = "" // Set your public key here
    };
    _updateManager.Start();
}
```

For production, set `Ed25519PublicKey` with your actual public key for secure signature verification.
