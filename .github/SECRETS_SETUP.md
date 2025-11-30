# GitHub Secrets Setup

## Required Secrets

You need to add the following secrets to your GitHub repository for the workflows to function properly.

### 1. NETSPARKLE_PRIVATE_KEY

**Purpose**: Signs appcast.xml files for secure auto-updates

**Value**: 
```
+/29dcynDIGndUh+50B0QLGGCzALNgPeT6ZKm3jeeDk=
```

**How to add**:
1. Go to: https://github.com/th3w1zard1/HoloPatcher.NET/settings/secrets/actions
2. Click **"New repository secret"**
3. Name: `NETSPARKLE_PRIVATE_KEY`
4. Value: `+/29dcynDIGndUh+50B0QLGGCzALNgPeT6ZKm3jeeDk=`
5. Click **"Add secret"**

### 2. NETSPARKLE_PUBLIC_KEY (Optional)

**Purpose**: Reference only - used in UpdateManager.cs

**Value**:
```
7ufoyE9utWpTIGQ6G5zqt3gOyzfcXniDPdlLJLun4tw=
```

**Note**: This is already configured in `UpdateManager.cs`. You can add it as a secret for reference, but it's not required for workflows to run.

## Verification

After adding secrets, you can verify they're set by:
1. Going to the repository Settings → Secrets and variables → Actions
2. You should see `NETSPARKLE_PRIVATE_KEY` listed (value is hidden)

## Security Notes

⚠️ **IMPORTANT**:
- Never commit the private key to the repository
- Never share the private key publicly
- The private key is used to sign appcast files - if compromised, attackers could create fake update files
- The public key is safe to include in code (it's already in UpdateManager.cs)

## Regenerating Keys

If you need to regenerate keys:

```bash
netsparkle-generate-appcast --generate-keys --export true
```

Then:
1. Update `UpdateManager.cs` with the new public key
2. Update the `NETSPARKLE_PRIVATE_KEY` secret with the new private key
3. Regenerate all existing appcast files with the new key

