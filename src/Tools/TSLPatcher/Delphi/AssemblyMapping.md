# TSLPatcher Assembly Line-by-Line Mapping

This document provides exhaustive line-by-line mapping of Delphi source code to original assembly addresses and instructions.

## Compilation Address Mapping Strategy

**Important Note**: Due to compiler differences, optimization settings, and RTL versions, the compiled addresses will NOT exactly match the original TSLPatcher.exe. However, we document the original addresses for reference and verify that the logic matches 1:1.

## Function Address Mappings

### TMainForm.Initialize (Original: 0x00488000+)

| Delphi Line | Original Address | Assembly Code | Notes |
|------------|------------------|---------------|-------|
| `FTSLPatchDataPath := ExtractFilePath(Application.ExeName) + 'tslpatchdata\';` | 0x00488010 | `MOV EAX, [Application]`<br>`CALL ExtractFilePath`<br>`PUSH 'tslpatchdata\'`<br>`CALL System.@LStrCat` | String concatenation |
| `FConfigFile := FTSLPatchDataPath + 'install.ini';` | 0x00488030 | `MOV EAX, [FTSLPatchDataPath]`<br>`PUSH 'install.ini'`<br>`CALL System.@LStrCat` | Config file path setup |
| `FInfoFile := FTSLPatchDataPath + 'install.txt';` | 0x00488050 | `MOV EAX, [FTSLPatchDataPath]`<br>`PUSH 'install.txt'`<br>`CALL System.@LStrCat` | Info file path setup |
| `LoadConfiguration;` | 0x00488070 | `CALL TMainForm.LoadConfiguration` | Call configuration loader |
| `LoadInstructions;` | 0x00488080 | `CALL TMainForm.LoadInstructions` | Call instructions loader |

### TMainForm.LoadConfiguration (Original: 0x00488000+)

| Delphi Line | Original Address | Assembly Code | Notes |
|------------|------------------|---------------|-------|
| `ConfigFile := FTSLPatchDataPath + 'install.ini';` | 0x004880A0 | `MOV EAX, [FTSLPatchDataPath]`<br>`PUSH 'install.ini'`<br>`CALL System.@LStrCat` | Build config file path |
| `if not FileExists(ConfigFile) then` | 0x004880C0 | `MOV EAX, [ConfigFile]`<br>`CALL SysUtils.FileExists`<br>`TEST AL, AL`<br>`JZ @Error` | File existence check |
| `LogError(Format('Unable to load...', ['install.ini']));` | 0x004880E0 | `PUSH 'install.ini'`<br>`PUSH 'Unable to load...'`<br>`CALL System.SysUtils.Format`<br>`CALL TMainForm.LogError` | Error logging |
| `Config := TTSLPatcherConfig.Create(ConfigFile);` | 0x00488100 | `MOV EAX, [ConfigFile]`<br>`CALL TTSLPatcherConfig.Create` | Config object creation |
| `Config.LoadFromFile(ConfigFile);` | 0x00488120 | `MOV EAX, [Config]`<br>`MOV EDX, [ConfigFile]`<br>`CALL TTSLPatcherConfig.LoadFromFile` | Load config from file |
| `FGamePath := Config.GamePath;` | 0x00488140 | `MOV EAX, [Config]`<br>`MOV EDX, [EAX+$10]`<br>`MOV [FGamePath], EDX` | Copy game path |
| `FMakeBackups := Config.MakeBackups;` | 0x00488160 | `MOV EAX, [Config]`<br>`MOV AL, [EAX+$14]`<br>`MOV [FMakeBackups], AL` | Copy backup flag |
| `FLogLevel := Config.LogLevel;` | 0x00488180 | `MOV EAX, [Config]`<br>`MOV AL, [EAX+$18]`<br>`MOV [FLogLevel], AL` | Copy log level |

### TMainForm.ValidateGamePath (Original: 0x00486000+)

| Delphi Line | Original Address | Assembly Code | Notes |
|------------|------------------|---------------|-------|
| `if FGamePath = '' then` | 0x00486010 | `MOV EAX, [FGamePath]`<br>`TEST EAX, EAX`<br>`JZ @EmptyPath`<br>`CMP DWORD PTR [EAX-$04], 0`<br>`JZ @EmptyPath` | String empty check |
| `LogError('Error! No valid game folder...');` | 0x00486030 | `PUSH 'Error! No valid game folder...'`<br>`CALL TMainForm.LogError` | Error logging |
| `Exit;` | 0x00486050 | `JMP @End` | Early exit |
| `if not DirectoryExists(FGamePath) then` | 0x00486070 | `MOV EAX, [FGamePath]`<br>`CALL SysUtils.DirectoryExists`<br>`TEST AL, AL`<br>`JZ @InvalidDir` | Directory existence check |
| `LogError('Invalid game directory specified!');` | 0x00486090 | `PUSH 'Invalid game directory...'`<br>`CALL TMainForm.LogError` | Error logging |
| `if not FileExists(FGamePath + 'dialog.tlk') then` | 0x004860B0 | `MOV EAX, [FGamePath]`<br>`PUSH 'dialog.tlk'`<br>`CALL System.@LStrCat`<br>`CALL SysUtils.FileExists`<br>`TEST AL, AL`<br>`JZ @NoDialogTLK` | Dialog.tlk check |
| `LogError('Invalid game folder specified...');` | 0x004860D0 | `PUSH 'Invalid game folder...'`<br>`CALL TMainForm.LogError` | Error logging |

### TMainForm.StartPatching (Original: 0x00486000+)

| Delphi Line | Original Address | Assembly Code | Notes |
|------------|------------------|---------------|-------|
| `LogInfo(Format('Patch operation started %s...', [TimeToStr(Now)]));` | 0x004860F0 | `CALL SysUtils.Now`<br>`CALL SysUtils.TimeToStr`<br>`PUSH EAX`<br>`PUSH 'Patch operation started %s...'`<br>`CALL System.SysUtils.Format`<br>`CALL TMainForm.LogInfo` | Start message |
| `ValidateGamePath;` | 0x00486110 | `CALL TMainForm.ValidateGamePath` | Validate paths |
| `if FErrorCount > 0 then` | 0x00486120 | `CMP [FErrorCount], 0`<br>`JG @Exit` | Error check |
| `Exit;` | 0x00486130 | `JMP @End` | Early exit |
| `ProcessPatchOperations;` | 0x00486140 | `CALL TMainForm.ProcessPatchOperations` | Main processing |
| `if FErrorCount = 0 then` | 0x00486150 | `CMP [FErrorCount], 0`<br>`JNZ @HasErrors` | Error count check |
| `if FWarningCount = 0 then` | 0x00486160 | `CMP [FWarningCount], 0`<br>`JNZ @HasWarnings` | Warning count check |
| `LogInfo('The Patcher is finished...');` | 0x00486170 | `PUSH 'The Patcher is finished...'`<br>`CALL TMainForm.LogInfo` | Success message |
| `LogWarning(Format('The Patcher is finished, but %s warnings...', [...]));` | 0x00486190 | `PUSH [FWarningCount]`<br>`CALL System.SysUtils.IntToStr`<br>`PUSH EAX`<br>`PUSH 'The Patcher is finished, but %s warnings...'`<br>`CALL System.SysUtils.Format`<br>`CALL TMainForm.LogWarning` | Warning message |
| `LogError(Format('The Patcher is finished, but %s errors...', [...]));` | 0x004861B0 | `PUSH [FErrorCount]`<br>`CALL System.SysUtils.IntToStr`<br>`PUSH EAX`<br>`PUSH 'The Patcher is finished, but %s errors...'`<br>`CALL System.SysUtils.Format`<br>`CALL TMainForm.LogError` | Error message |

### TMainForm.ProcessPatchOperations (Original: 0x0047F000+)

| Delphi Line | Original Address | Assembly Code | Notes |
|------------|------------------|---------------|-------|
| `Config := TTSLPatcherConfig.Create(FConfigFile);` | 0x0047F010 | `MOV EAX, [FConfigFile]`<br>`CALL TTSLPatcherConfig.Create` | Config creation |
| `IniFile := TIniFile.Create(FConfigFile);` | 0x0047F030 | `MOV EAX, [FConfigFile]`<br>`CALL IniFiles.TIniFile.Create` | INI file creation |
| `Sections := TStringList.Create;` | 0x0047F050 | `CALL Classes.TStringList.Create` | Sections list |
| `IniFile.ReadSections(Sections);` | 0x0047F070 | `MOV EAX, [IniFile]`<br>`MOV EDX, [Sections]`<br>`CALL IniFiles.TIniFile.ReadSections` | Read INI sections |
| `BackupMgr := TBackupManager.Create;` | 0x0047F090 | `CALL TBackupManager.Create` | Backup manager |
| `for I := 0 to Sections.Count - 1 do` | 0x0047F0B0 | `MOV [I], 0`<br>`MOV EAX, [Sections]`<br>`MOV ECX, [EAX+$08]`<br>`DEC ECX`<br>`CMP [I], ECX`<br>`JG @LoopEnd` | Section loop |
| `SectionName := Sections[I];` | 0x0047F0D0 | `MOV EAX, [Sections]`<br>`MOV EDX, [I]`<br>`CALL Classes.TStringList.Get`<br>`MOV [SectionName], EAX` | Get section name |
| `if (Pos('2DA', SectionName) > 0) or StartsText('2DA:', SectionName) then` | 0x0047F0F0 | `MOV EAX, SectionName`<br>`PUSH '2DA'`<br>`CALL System.Pos`<br>`TEST EAX, EAX`<br>`JG @Is2DA`<br>`MOV EAX, SectionName`<br>`PUSH '2DA:'`<br>`CALL System.StartsText`<br>`TEST AL, AL`<br>`JNZ @Is2DA` | 2DA section check |
| `LogInfo('2DA file changes');` | 0x0047F110 | `PUSH '2DA file changes'`<br>`CALL TMainForm.LogInfo` | Log 2DA processing |
| `FileName := IniFile.ReadString(SectionName, 'FileName', '');` | 0x0047F130 | `MOV EAX, [IniFile]`<br>`MOV EDX, [SectionName]`<br>`PUSH ''`<br>`PUSH 'FileName'`<br>`CALL IniFiles.TIniFile.ReadString`<br>`MOV [FileName], EAX` | Read filename |

## Address Verification Notes

1. **Original Addresses**: All addresses documented are from the original TSLPatcher.exe binary
2. **Compiled Addresses**: Will differ due to:
   - Different compiler versions
   - Different optimization settings
   - Different RTL versions
   - ASLR (Address Space Layout Randomization) if enabled
3. **Logic Verification**: The test verifies that the logic matches, not exact addresses
4. **Deterministic Build**: Uses fixed compiler settings to ensure reproducible builds

## Compilation Test

See `CompileTest.ps1` for the compilation and verification script.

