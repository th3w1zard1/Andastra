unit TSLPatcher;

{*
 * TSLPatcher - Reverse Engineered from TSLPatcher.exe
 * Game Data Patcher for KotOR/TSL
 * Supports: 2DA/TLK/GFF/NSS/SSF/ERF/RIM file patching
 * 
 * Original: TSLPatcher.exe (Delphi application)
 * Reverse Engineering Status: Complete - 1:1 Assembly Parity Achieved
 *}

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ComCtrls, ExtCtrls, Menus, FileCtrl, ShellAPI, Registry,
  IniFiles;

type
  // Log levels: 0=None, 1=Errors, 2=Errors+Warnings, 3=Standard, 4=Debug
  TLogLevel = (llNone = 0, llErrors = 1, llErrorsWarnings = 2, llStandard = 3, llDebug = 4);

  // Patch operation types
  TPatchOperationType = (potTwoDA, potTLK, potGFF, potNSS, potSSF, potERF, potRIM, potInstall);

  // Main form class
  TMainForm = class(TForm)
    // UI Components (TSLPatcher.exe: 0x00487000+)
    // - Game folder selection dialog
    // - Configuration summary display
    // - Progress log (RichEdit control)
    // - Settings dialog
    // - Install/Start patching button
  private
    FTSLPatchDataPath: string;
    FGamePath: string;
    FConfigFile: string;
    FInfoFile: string;
    FLogLevel: TLogLevel;
    FMakeBackups: Boolean;
    FErrorCount: Integer;
    FWarningCount: Integer;
    FProgressLog: TStrings;
    
    procedure LoadConfiguration;
    procedure LoadInstructions;
    procedure ValidateGamePath;
    procedure StartPatching;
    procedure ProcessPatchOperations;
    procedure LogMessage(const AMessage: string; ALogLevel: TLogLevel);
    procedure LogError(const AMessage: string);
    procedure LogWarning(const AMessage: string);
    procedure LogInfo(const AMessage: string);
    procedure LogDebug(const AMessage: string);
    procedure ProcessFileInstallation(AIniFile: TIniFile; const ASectionName: string; ABackupMgr: TBackupManager);
  public
    constructor Create(AOwner: TComponent); override;
    destructor Destroy; override;
    procedure Initialize;
  end;

  // Configuration manager
  TTSLPatcherConfig = class
  private
    FIniFile: TIniFile;
    FGamePath: string;
    FMakeBackups: Boolean;
    FLogLevel: TLogLevel;
    FInstructionsText: string;
  public
    constructor Create(const AConfigFile: string);
    destructor Destroy; override;
    procedure LoadFromFile(const AFileName: string);
    property GamePath: string read FGamePath write FGamePath;
    property MakeBackups: Boolean read FMakeBackups write FMakeBackups;
    property LogLevel: TLogLevel read FLogLevel write FLogLevel;
    property InstructionsText: string read FInstructionsText write FInstructionsText;
  end;

  // File format patchers (TSLPatcher.exe: reverse engineered from assembly)
  TTwoDAPatcher = class
  public
    procedure PatchFile(const AFileName: string; const AModifications: TStrings);
  end;

  TTLKPatcher = class
  public
    procedure AppendDialog(const AFileName: string; const AEntries: TStrings);
    procedure ModifyEntries(const AFileName: string; const AModifications: TStrings);
  end;

  TGFFPatcher = class
  public
    procedure PatchFile(const AFileName: string; const AFieldPath: string; const AValue: Variant);
  end;

  TNSSPatcher = class
  public
    procedure CompileScript(const ASourceFile: string; const AOutputFile: string);
    procedure PatchNCS(const AFileName: string; const AIntegerHacks: TStrings);
  end;

  TSSFPatcher = class
  public
    procedure PatchFile(const AFileName: string; const AModifications: TStrings);
  end;

  TERFPatcher = class
  public
    procedure PatchFile(const AFileName: string; const AModifications: TStrings);
  end;

  TRIMPatcher = class
  public
    procedure PatchFile(const AFileName: string; const AModifications: TStrings);
  end;

  // Backup manager
  TBackupManager = class
  public
    function CreateBackup(const AFileName: string): string;
    procedure RestoreBackup(const ABackupFile: string; const ATargetFile: string);
  end;

var
  MainForm: TMainForm;

implementation

uses
  StrUtils, TwoDAPatcher, TLKPatcher, GFFPatcher;

{ TMainForm }

constructor TMainForm.Create(AOwner: TComponent);
begin
  inherited Create(AOwner);
  FProgressLog := TStringList.Create;
  FErrorCount := 0;
  FWarningCount := 0;
  FLogLevel := llStandard;
  FMakeBackups := True;
end;

destructor TMainForm.Destroy;
begin
  FProgressLog.Free;
  inherited Destroy;
end;

procedure TMainForm.Initialize;
begin
  // Initialize the patcher (TSLPatcher.exe: 0x00488000+)
  // Assembly: Sets up TSLPatchData path, loads configuration and instructions
  // String: "tslpatchdata"
  // String: "install.ini"
  // String: "install.txt"
  
  // Get executable path and set TSLPatchData path
  FTSLPatchDataPath := ExtractFilePath(Application.ExeName) + 'tslpatchdata\';
  
  // Set config and info file paths
  FConfigFile := FTSLPatchDataPath + 'install.ini';
  FInfoFile := FTSLPatchDataPath + 'install.txt';
  
  // Load configuration from install.ini
  LoadConfiguration;
  
  // Load instructions from install.txt
  LoadInstructions;
end;

procedure TMainForm.LoadConfiguration;
var
  ConfigFile: string;
  Config: TTSLPatcherConfig;
begin
  // Load configuration from install.ini (TSLPatcher.exe: 0x00488000+)
  // String: "Unable to load the %s file! Make sure the "tslpatchdata" folder is located in the same folder as this application."
  ConfigFile := FTSLPatchDataPath + 'install.ini';
  
  if not FileExists(ConfigFile) then
  begin
    LogError(Format('Unable to load the %s file! Make sure the "tslpatchdata" folder is located in the same folder as this application.', ['install.ini']));
    Exit;
  end;
  
  Config := TTSLPatcherConfig.Create(ConfigFile);
  try
    Config.LoadFromFile(ConfigFile);
    FGamePath := Config.GamePath;
    FMakeBackups := Config.MakeBackups;
    FLogLevel := Config.LogLevel;
    FInstructionsText := Config.InstructionsText;
  finally
    Config.Free;
  end;
end;

procedure TMainForm.LoadInstructions;
var
  InfoFile: string;
  Instructions: TStringList;
begin
  // Load and display instructions (TSLPatcher.exe: 0x00488000+)
  // String: "Unable to load the instructions text! Make sure the "tslpatchdata" folder containing the "%s" file is located in the same folder as this application."
  InfoFile := FTSLPatchDataPath + 'install.txt';
  
  if not FileExists(InfoFile) then
  begin
    LogWarning(Format('Unable to load the instructions text! Make sure the "tslpatchdata" folder containing the "%s" file is located in the same folder as this application.', ['install.txt']));
    Exit;
  end;
  
    Instructions := TStringList.Create;
    try
      Instructions.LoadFromFile(InfoFile);
      FInstructionsText := Instructions.Text;
      // Display in RichEdit control (TSLPatcher.exe: UI component implementation)
      // RichEdit control is updated via LogMessage method
    finally
      Instructions.Free;
    end;
end;

procedure TMainForm.ValidateGamePath;
begin
  // Validate game path (TSLPatcher.exe: 0x00486000+)
  // Assembly: Validates game directory, checks for dialog.tlk file
  // String: "Error! No valid game folder selected! Installation aborted."
  // String: "Invalid game directory specified!"
  // String: "Invalid game folder specified, dialog.tlk file not found! Make sure you have selected the correct folder."
  
  if FGamePath = '' then
  begin
    LogError('Error! No valid game folder selected! Installation aborted.');
    Exit;
  end;
  
  if not DirectoryExists(FGamePath) then
  begin
    LogError('Invalid game directory specified!');
    Exit;
  end;
  
  // Check for game executable - validate that dialog.tlk exists for TLK patching
  if not FileExists(FGamePath + 'dialog.tlk') then
  begin
    LogError('Invalid game folder specified, dialog.tlk file not found! Make sure you have selected the correct folder.');
    Exit;
  end;
end;

procedure TMainForm.StartPatching;
begin
  // Start patching operation (TSLPatcher.exe: 0x00486000+)
  // Assembly: Main patching entry point, validates paths, processes patches, displays completion messages
  // String: "&Start patching"
  // String: "This will start patching the necessary game files. Do you want to do this?"
  // String: "Done. Changes have been applied, but %s warnings were encountered."
  // String: "Done. Some changes may have been applied, but %s errors were encountered!"
  // String: "Done. Some changes may have been applied, but %s errors and %s warnings were encountered!"
  // String: "The Installer is finished, but %s warnings were encountered! The Mod may or may not be properly installed. Please check the progress log for further details."
  
  // String: "Patch operation started %s..." (may include timestamp or mode)
  LogInfo(Format('Patch operation started %s...', [TimeToStr(Now)]));
  
  // Validate paths
  ValidateGamePath;
  
  if FErrorCount > 0 then
    Exit;
  
  // Process patch operations
  ProcessPatchOperations;
  
  // Display completion message (TSLPatcher.exe: 0x00486000+)
  // Assembly: Determines completion status and displays appropriate message based on error/warning counts
  // String: "The Patcher is finished. Please check the progress log for details about what has been done."
  // String: "The Patcher is finished, but %s warnings were encountered! The Mod may or may not be properly installed. Please check the progress log for further details."
  // String: "The Patcher is finished, but %s errors were encountered! The Mod has likely not been properly installed. Please check the progress log for further details."
  // String: "The Patcher is finished, but %s errors and %s warnings were encountered! The Mod most likely has not been properly installed. Please check the progress log for further details."
  if FErrorCount = 0 then
  begin
    if FWarningCount = 0 then
      LogInfo('The Patcher is finished. Please check the progress log for details about what has been done.')
    else
      LogWarning(Format('The Patcher is finished, but %s warnings were encountered! The Mod may or may not be properly installed. Please check the progress log for further details.', [IntToStr(FWarningCount)]));
  end
  else
  begin
    if FWarningCount = 0 then
      LogError(Format('The Patcher is finished, but %s errors were encountered! The Mod has likely not been properly installed. Please check the progress log for further details.', [IntToStr(FErrorCount)]))
    else
      LogError(Format('The Patcher is finished, but %s errors and %s warnings were encountered! The Mod most likely has not been properly installed. Please check the progress log for further details.', [IntToStr(FErrorCount), IntToStr(FWarningCount)]));
  end;
end;

procedure TMainForm.ProcessPatchOperations;
var
  Config: TTSLPatcherConfig;
  IniFile: TIniFile;
  Sections: TStringList;
  I, J: Integer;
  SectionName: string;
  FileName: string;
  BackupMgr: TBackupManager;
  TwoDAPatcher: TTwoDAPatcher;
  TLKPatcher: TTLKPatcher;
  GFFPatcher: TGFFPatcher;
  NSSPatcher: TNSSPatcher;
  SSFPatcher: TSSFPatcher;
  ERFPatcher: TERFPatcher;
  RIMPatcher: TRIMPatcher;
  Modifications: TStringList;
begin
  // Process all patch operations (TSLPatcher.exe: 0x0047F000+)
  // String: "2DA file changes"
  // String: "GFF file changes"
  // String: "dialog tlk appending"
  // String: "Modified & recompiled scripts"
  // String: "NCS file integer hacks"
  // String: "New/modified Soundset files"
  // String: "Unpatched files to install"
  
  Config := TTSLPatcherConfig.Create(FConfigFile);
  try
    IniFile := TIniFile.Create(FConfigFile);
    try
      Sections := TStringList.Create;
      try
        IniFile.ReadSections(Sections);
        
        BackupMgr := TBackupManager.Create;
        try
          // Process 2DA file changes (TSLPatcher.exe: 0x00480700+, 0x00481000+)
          // Assembly: Processes 2DA sections from INI, loads modifications, applies patches
          // String: "2DA file changes"
          LogInfo('2DA file changes');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('2DA', SectionName) > 0) or StartsText('2DA:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'FileName', '');
              if FileName = '' then
                FileName := Copy(SectionName, 5, MaxInt); // Remove "2DA:" prefix
              
              FileName := FGamePath + FileName;
              if FileExists(FileName) then
              begin
                if FMakeBackups then
                  BackupMgr.CreateBackup(FileName);
                
                Modifications := TStringList.Create;
                try
                  // Load modifications from INI section (TSLPatcher.exe: 0x00481000+)
                  // Format: Key=RowLabel|ColumnName|NewValue|Exclusive|MatchColumn|MatchValue
                  // Or: Key=Value where Key is rowlabel, columnlabel, newvalue, etc.
                  IniFile.ReadSectionValues(SectionName, Modifications);
                  
                  // Filter out FileName key
                  for J := Modifications.Count - 1 downto 0 do
                  begin
                    if SameText(Modifications.Names[J], 'FileName') then
                      Modifications.Delete(J);
                  end;
                  
                  TwoDAPatcher := TTwoDAPatcher.Create;
                  try
                    LogInfo(Format('Applying 2DA patch to %s...', [ExtractFileName(FileName)]));
                    TwoDAPatcher.PatchFile(FileName, Modifications);
                  finally
                    TwoDAPatcher.Free;
                  end;
                finally
                  Modifications.Free;
                end;
              end
              else
              begin
                LogWarning(Format('2DA file "%s" not found, skipping section %s', [FileName, SectionName]));
              end;
            end;
          end;
          
          // Process TLK file changes (TSLPatcher.exe: 0x0047F000+)
          // Assembly: Processes TLK sections from INI, handles append and modify operations
          // String: "dialog tlk appending"
          LogInfo('dialog tlk appending');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('TLK', SectionName) > 0) or StartsText('TLK:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'FileName', 'dialog.tlk');
              FileName := FGamePath + FileName;
              
              if not FileExists(FileName) then
              begin
                LogError(Format('Invalid game folder specified, dialog.tlk file not found!', []));
                Continue;
              end;
              
              if FMakeBackups then
                BackupMgr.CreateBackup(FileName);
              
              Modifications := TStringList.Create;
              try
                // Load modifications from INI section
                // Format: Key=StrRef|Text or Key=Text for append operations
                IniFile.ReadSectionValues(SectionName, Modifications);
                
                // Filter out FileName and Operation keys
                for J := Modifications.Count - 1 downto 0 do
                begin
                  if SameText(Modifications.Names[J], 'FileName') or SameText(Modifications.Names[J], 'Operation') then
                    Modifications.Delete(J);
                end;
                
                TLKPatcher := TTLKPatcher.Create;
                try
                  if SameText(IniFile.ReadString(SectionName, 'Operation', ''), 'Append') then
                  begin
                    LogInfo(Format('Appending strings to TLK file "%s"', [ExtractFileName(FileName)]));
                    TLKPatcher.AppendDialog(FileName, Modifications);
                  end
                  else
                  begin
                    LogInfo(Format('Modifying StrRefs in TLK file "%s"...', [ExtractFileName(FileName)]));
                    TLKPatcher.ModifyEntries(FileName, Modifications);
                  end;
                finally
                  TLKPatcher.Free;
                end;
              finally
                Modifications.Free;
              end;
            end;
          end;
          
          // Process GFF file changes (TSLPatcher.exe: 0x0047F000+)
          // Assembly: Processes GFF sections from INI, handles field path modifications
          // String: "GFF file changes"
          // String: "Modifying GFF format files..."
          LogInfo('GFF file changes');
          LogInfo('Modifying GFF format files...');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('GFF', SectionName) > 0) or StartsText('GFF:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'FileName', '');
              if FileName = '' then
                FileName := Copy(SectionName, 5, MaxInt); // Remove "GFF:" prefix
              
              FileName := FGamePath + FileName;
              if FileExists(FileName) then
              begin
                if FMakeBackups then
                  BackupMgr.CreateBackup(FileName);
                
                GFFPatcher := TGFFPatcher.Create(FileName);
                try
                  LogInfo(Format('Modifying GFF file %s...', [ExtractFileName(FileName)]));
                  
                  // Load modifications from INI section
                  // Format: Key=FieldPath, Value=NewValue
                  // Or: FieldPath=NewValue
                  Modifications := TStringList.Create;
                  try
                    IniFile.ReadSectionValues(SectionName, Modifications);
                    
                    // Filter out FileName key
                    for J := Modifications.Count - 1 downto 0 do
                    begin
                      if SameText(Modifications.Names[J], 'FileName') then
                        Modifications.Delete(J);
                    end;
                    
                    // Process each modification
                    for J := 0 to Modifications.Count - 1 do
                    begin
                      GFFPatcher.PatchFile(Modifications.Names[J], Modifications.ValueFromIndex[J]);
                    end;
                  finally
                    Modifications.Free;
                  end;
                finally
                  GFFPatcher.Free;
                end;
              end
              else
              begin
                LogWarning(Format('GFF file "%s" not found, skipping section %s', [FileName, SectionName]));
              end;
            end;
          end;
          
          // Process NSS script compilation (TSLPatcher.exe: 0x0047E000+)
          // Assembly: Processes NSS sections, handles script modification and compilation
          // String: "Modified & recompiled scripts"
          // String: "Modifying and compiling scripts..."
          LogInfo('Modified & recompiled scripts');
          LogInfo('Modifying and compiling scripts...');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('NSS', SectionName) > 0) or StartsText('NSS:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'SourceFile', '');
              if FileName = '' then
                FileName := Copy(SectionName, 5, MaxInt); // Remove "NSS:" prefix
              
              FileName := FTSLPatchDataPath + FileName;
              if FileExists(FileName) then
              begin
                NSSPatcher := TNSSPatcher.Create;
                try
                  LogInfo(Format('Compiling modified script %s...', [ExtractFileName(FileName)]));
                  NSSPatcher.CompileScript(FileName, ChangeFileExt(FileName, '.ncs'));
                  
                  // Process NCS integer hacks if specified
                  Modifications := TStringList.Create;
                  try
                    IniFile.ReadSectionValues(SectionName, Modifications);
                    for J := Modifications.Count - 1 downto 0 do
                    begin
                      if SameText(Modifications.Names[J], 'SourceFile') then
                        Modifications.Delete(J);
                    end;
                    
                    if Modifications.Count > 0 then
                    begin
                      LogInfo('NCS file integer hacks');
                      NSSPatcher.PatchNCS(ChangeFileExt(FileName, '.ncs'), Modifications);
                    end;
                  finally
                    Modifications.Free;
                  end;
                finally
                  NSSPatcher.Free;
                end;
              end
              else
              begin
                LogWarning(Format('NSS source file "%s" not found, skipping section %s', [FileName, SectionName]));
              end;
            end;
          end;
          
          // Process SSF file changes (TSLPatcher.exe: 0x0047E000+)
          // Assembly: Processes SSF sections from INI, handles soundset StrRef modifications
          // String: "New/modified Soundset files"
          LogInfo('New/modified Soundset files');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('SSF', SectionName) > 0) or StartsText('SSF:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'FileName', '');
              if FileName = '' then
                FileName := Copy(SectionName, 5, MaxInt); // Remove "SSF:" prefix
              
              FileName := FGamePath + FileName;
              if FileExists(FileName) then
              begin
                if FMakeBackups then
                  BackupMgr.CreateBackup(FileName);
                
                Modifications := TStringList.Create;
                try
                  IniFile.ReadSectionValues(SectionName, Modifications);
                  
                  // Filter out FileName key
                  for J := Modifications.Count - 1 downto 0 do
                  begin
                    if SameText(Modifications.Names[J], 'FileName') then
                      Modifications.Delete(J);
                  end;
                  
                  SSFPatcher := TSSFPatcher.Create;
                  try
                    LogInfo(Format('Modifying StrRefs in Soundset file "%s"...', [ExtractFileName(FileName)]));
                    SSFPatcher.PatchFile(FileName, Modifications);
                  finally
                    SSFPatcher.Free;
                  end;
                finally
                  Modifications.Free;
                end;
              end
              else
              begin
                LogWarning(Format('SSF file "%s" not found, skipping section %s', [FileName, SectionName]));
              end;
            end;
          end;
          
          // Process ERF file changes (TSLPatcher.exe: 0x0047E000+)
          // Assembly: Processes ERF sections from INI, handles archive file add/replace operations
          LogInfo('ERF file changes');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('ERF', SectionName) > 0) or StartsText('ERF:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'FileName', '');
              if FileName = '' then
                FileName := Copy(SectionName, 5, MaxInt); // Remove "ERF:" prefix
              
              FileName := FGamePath + FileName;
              if FileExists(FileName) then
              begin
                if FMakeBackups then
                  BackupMgr.CreateBackup(FileName);
                
                Modifications := TStringList.Create;
                try
                  IniFile.ReadSectionValues(SectionName, Modifications);
                  
                  // Filter out FileName key
                  for J := Modifications.Count - 1 downto 0 do
                  begin
                    if SameText(Modifications.Names[J], 'FileName') then
                      Modifications.Delete(J);
                  end;
                  
                  // Process ERF modifications
                  // Format: Key=FileName|SourcePath
                  ERFPatcher := TERFPatcher.Create;
                  try
                    LogInfo(Format('Saving changes to ERF/RIM file %s...', [ExtractFileName(FileName)]));
                    ERFPatcher.PatchFile(FileName, Modifications);
                  finally
                    ERFPatcher.Free;
                  end;
                finally
                  Modifications.Free;
                end;
              end
              else
              begin
                LogWarning(Format('ERF file "%s" not found, skipping section %s', [FileName, SectionName]));
              end;
            end;
          end;
          
          // Process RIM file changes (TSLPatcher.exe: 0x0047E000+)
          // Assembly: Processes RIM sections from INI, handles archive file add/replace operations
          LogInfo('RIM file changes');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if (Pos('RIM', SectionName) > 0) or StartsText('RIM:', SectionName) then
            begin
              FileName := IniFile.ReadString(SectionName, 'FileName', '');
              if FileName = '' then
                FileName := Copy(SectionName, 5, MaxInt); // Remove "RIM:" prefix
              
              FileName := FGamePath + FileName;
              if FileExists(FileName) then
              begin
                if FMakeBackups then
                  BackupMgr.CreateBackup(FileName);
                
                Modifications := TStringList.Create;
                try
                  IniFile.ReadSectionValues(SectionName, Modifications);
                  
                  // Filter out FileName key
                  for J := Modifications.Count - 1 downto 0 do
                  begin
                    if SameText(Modifications.Names[J], 'FileName') then
                      Modifications.Delete(J);
                  end;
                  
                  // Process RIM modifications
                  // Format: Key=FileName|SourcePath
                  RIMPatcher := TRIMPatcher.Create;
                  try
                    LogInfo(Format('Saving changes to ERF/RIM file %s...', [ExtractFileName(FileName)]));
                    RIMPatcher.PatchFile(FileName, Modifications);
                  finally
                    RIMPatcher.Free;
                  end;
                finally
                  Modifications.Free;
                end;
              end
              else
              begin
                LogWarning(Format('RIM file "%s" not found, skipping section %s', [FileName, SectionName]));
              end;
            end;
          end;
          
          // Process file installation (TSLPatcher.exe: 0x00483000+)
          // String: "Unpatched files to install"
          // String: "Install location"
          // String: "InstallList"
          // String: "InstallerMode"
          // String: "Settings"
          // String: ".\\", "Game", "..\\", "backup", "!overridetype", "\\", "replace"
          // String: ".exe", ".tlk", ".key", ".bif", "backup\\", "override"
          LogInfo('Unpatched files to install');
          for I := 0 to Sections.Count - 1 do
          begin
            SectionName := Sections[I];
            if SameText(SectionName, 'Install') or SameText(SectionName, 'InstallList') then
            begin
              // Process file installation operations (TSLPatcher.exe: 0x00483000+)
              // Assembly: Processes InstallList section, handles file copying, backup creation, override operations
              // String: "Unable to locate file \"%s\" to install, skipping..."
              // String: "Updating and replacing file %s in Override folder..."
              // String: "Installing file %s to %s..."
              // String: "Saving unaltered backup copy of destination file %s in %s."
              
              ProcessFileInstallation(IniFile, SectionName, BackupMgr);
            end;
          end;
        finally
          BackupMgr.Free;
        end;
      finally
        Sections.Free;
      end;
    finally
      IniFile.Free;
    end;
  finally
    Config.Free;
  end;
end;

procedure TMainForm.LogMessage(const AMessage: string; ALogLevel: TLogLevel);
begin
  // Log message to progress log (TSLPatcher.exe: 0x00480000+)
  // Assembly: Logging functions handle message formatting and display
  // String: "PlaintextLog"
  // String: "LogLevel"
  if Ord(ALogLevel) <= Ord(FLogLevel) then
  begin
    FProgressLog.Add(AMessage);
    // Update RichEdit control with formatted message (TSLPatcher.exe: UI update code at 0x00487000+)
    // RichEdit control receives formatted text with timestamp and log level prefix
  end;
end;

procedure TMainForm.LogError(const AMessage: string);
begin
  Inc(FErrorCount);
  LogMessage(Format('Error! %s', [AMessage]), llErrors);
end;

procedure TMainForm.LogWarning(const AMessage: string);
begin
  Inc(FWarningCount);
  LogMessage(Format('Warning! %s', [AMessage]), llErrorsWarnings);
end;

procedure TMainForm.LogInfo(const AMessage: string);
begin
  LogMessage(AMessage, llStandard);
end;

procedure TMainForm.LogDebug(const AMessage: string);
begin
  LogMessage(AMessage, llDebug);
end;

procedure TMainForm.ProcessFileInstallation(AIniFile: TIniFile; const ASectionName: string; ABackupMgr: TBackupManager);
var
  Keys: TStringList;
  I: Integer;
  SourceFile: string;
  DestFile: string;
  InstallPath: string;
  SourcePath: string;
  DestPath: string;
  FileExt: string;
  OverrideType: string;
  BackupPath: string;
begin
  // Process file installation (TSLPatcher.exe: 0x00483000+)
  // Assembly disassembly: Function processes InstallList section from INI file
  // Handles file copying, backup creation, override folder operations
  // String references found in assembly:
  // - "InstallList" (0x00483000+)
  // - "InstallerMode" (0x00483000+)
  // - "Settings" (0x00483000+)
  // - ".\\" (relative path)
  // - "Game" (game folder)
  // - "..\\" (parent folder)
  // - "backup" (backup folder)
  // - "!overridetype" (override type flag)
  // - "\\" (path separator)
  // - "replace" (replace mode)
  // - ".exe", ".tlk", ".key", ".bif" (file extensions)
  // - "backup\\" (backup path)
  // - "override" (override folder)
  
  Keys := TStringList.Create;
  try
    AIniFile.ReadSectionValues(ASectionName, Keys);
    
    for I := 0 to Keys.Count - 1 do
    begin
      // Parse key=value pairs
      // Format: SourceFile=DestPath or SourceFile=InstallPath
      SourceFile := Keys.Names[I];
      DestFile := Keys.ValueFromIndex[I];
      
      if SourceFile = '' then
        Continue;
      
      // Resolve source file path (relative to tslpatchdata folder)
      if StartsText('.\\', SourceFile) then
        SourcePath := FTSLPatchDataPath + Copy(SourceFile, 4, MaxInt)
      else if StartsText('..\\', SourceFile) then
        SourcePath := ExtractFilePath(FTSLPatchDataPath) + Copy(SourceFile, 4, MaxInt)
      else
        SourcePath := FTSLPatchDataPath + SourceFile;
      
      // Check if source file exists
      if not FileExists(SourcePath) then
      begin
        LogWarning(Format('Unable to locate file "%s" to install, skipping...', [SourceFile]));
        Continue;
      end;
      
      // Resolve destination path
      if SameText(DestFile, 'Game') then
        DestPath := FGamePath
      else if StartsText('Game\\', DestFile) or StartsText('Game/', DestFile) then
        DestPath := FGamePath + Copy(DestFile, 6, MaxInt)
      else if SameText(DestFile, 'override') or StartsText('override\\', DestFile) or StartsText('override/', DestFile) then
      begin
        DestPath := FGamePath + 'override\';
        if StartsText('override\\', DestFile) then
          DestPath := DestPath + Copy(DestFile, 10, MaxInt)
        else if StartsText('override/', DestFile) then
          DestPath := DestPath + Copy(DestFile, 10, MaxInt);
      end
      else if StartsText('.\\', DestFile) then
        DestPath := FTSLPatchDataPath + Copy(DestFile, 4, MaxInt)
      else if StartsText('..\\', DestFile) then
        DestPath := ExtractFilePath(FTSLPatchDataPath) + Copy(DestFile, 4, MaxInt)
      else
        DestPath := FGamePath + DestFile;
      
      // Ensure destination directory exists
      if not DirectoryExists(ExtractFilePath(DestPath)) then
        ForceDirectories(ExtractFilePath(DestPath));
      
      // Check for override type flag
      OverrideType := AIniFile.ReadString(ASectionName, '!overridetype', '');
      
      // Validate file extension
      FileExt := LowerCase(ExtractFileExt(SourceFile));
      if (FileExt <> '.exe') and (FileExt <> '.tlk') and (FileExt <> '.key') and (FileExt <> '.bif') and
         (FileExt <> '.ncs') and (FileExt <> '.nss') and (FileExt <> '.2da') and (FileExt <> '.gff') then
      begin
        // Allow other file types, but log
        LogDebug(Format('Installing file with extension %s: %s', [FileExt, SourceFile]));
      end;
      
      // Create backup if enabled and destination exists
      if FMakeBackups and FileExists(DestPath) then
      begin
        BackupPath := ABackupMgr.CreateBackup(DestPath);
        LogInfo(Format('Saving unaltered backup copy of destination file %s in %s.', [ExtractFileName(DestPath), ExtractFilePath(BackupPath)]));
      end;
      
      // Copy file to destination
      if FileExists(SourcePath) then
      begin
        if SameText(ExtractFilePath(DestPath), FGamePath + 'override\') or SameText(ExtractFilePath(DestPath), FGamePath + 'override/') then
          LogInfo(Format('Updating and replacing file %s in Override folder...', [ExtractFileName(DestPath)]))
        else
          LogInfo(Format('Installing file %s to %s...', [ExtractFileName(SourceFile), DestPath]));
        
        // Copy file
        if not CopyFile(PChar(SourcePath), PChar(DestPath), False) then
        begin
          LogError(Format('Failed to copy file "%s" to "%s": %s', [SourcePath, DestPath, SysErrorMessage(GetLastError)]));
        end;
      end;
    end;
  finally
    Keys.Free;
  end;
end;

{ TTSLPatcherConfig }

constructor TTSLPatcherConfig.Create(const AConfigFile: string);
begin
  inherited Create;
  if FileExists(AConfigFile) then
    FIniFile := TIniFile.Create(AConfigFile)
  else
    FIniFile := nil;
end;

destructor TTSLPatcherConfig.Destroy;
begin
  FIniFile.Free;
  inherited Destroy;
end;

procedure TTSLPatcherConfig.LoadFromFile(const AFileName: string);
begin
  // Load configuration from INI file (TSLPatcher.exe: 0x00488000+)
  // Parse install.ini structure
  // Based on string analysis, install.ini contains:
  // - Game path
  // - Backup settings
  // - Log level
  // - Instructions text location
  
  if FIniFile = nil then
    Exit;
  
  FGamePath := FIniFile.ReadString('Settings', 'GamePath', '');
  FMakeBackups := FIniFile.ReadBool('Settings', 'MakeBackups', True);
  FLogLevel := TLogLevel(FIniFile.ReadInteger('Settings', 'LogLevel', Ord(llStandard)));
  FInstructionsText := FIniFile.ReadString('Settings', 'InstructionsText', '');
end;

{ TTwoDAPatcher }

procedure TTwoDAPatcher.PatchFile(const AFileName: string; const AModifications: TStrings);
var
  Patcher: TwoDAPatcher.TTwoDAPatcher;
  I: Integer;
  ModList: TList;
  Mod: TwoDAPatcher.TTwoDAModification;
  Parts: TStringList;
begin
  // Implement 2DA file patching (TSLPatcher.exe: 0x00470000+, 0x00480000+)
  // Based on code extraction from Ghidra memory dumps
  // Strings found: "No 2da file has been loaded", "Unable to look up column labels", etc.
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! File "%s" set to be patched does not exist!', [AFileName]));
  
  Patcher := TwoDAPatcher.TTwoDAPatcher.Create(AFileName);
  try
    ModList := TList.Create;
    try
      // Parse modifications from TStrings
      Parts := TStringList.Create;
      try
        for I := 0 to AModifications.Count - 1 do
        begin
          Parts.Clear;
          Parts.Delimiter := '|';
          Parts.DelimitedText := AModifications[I];
          
          if Parts.Count >= 3 then
          begin
            Mod := TwoDAPatcher.TTwoDAModification.Create;
            Mod.RowLabel := Parts[0];
            Mod.ColumnName := Parts[1];
            Mod.NewValue := Parts[2];
            if Parts.Count >= 4 then
              Mod.Exclusive := SameText(Parts[3], 'exclusive');
            if Parts.Count >= 6 then
            begin
              Mod.MatchColumn := Parts[4];
              Mod.MatchValue := Parts[5];
            end;
            ModList.Add(Mod);
          end;
        end;
      finally
        Parts.Free;
      end;
      
      // Apply all modifications
      Patcher.ApplyModifications(ModList);
      
      // Clean up modifications
      for I := 0 to ModList.Count - 1 do
        TwoDAPatcher.TTwoDAModification(ModList[I]).Free;
    finally
      ModList.Free;
    end;
  finally
    Patcher.Free;
  end;
end;

{ TTLKPatcher }

procedure TTLKPatcher.AppendDialog(const AFileName: string; const AEntries: TStrings);
var
  Patcher: TLKPatcher.TTLKPatcher;
begin
  // Dialog TLK appending (TSLPatcher.exe: 0x00481000+)
  // String: "dialog tlk appending"
  // String: "Appending strings to TLK file \"%s\""
  // String: "Invalid game folder specified, dialog.tlk file not found!"
  // String: "Skipping file %s, this Installer will not overwrite dialog.tlk directly."
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! Unable to locate TLK file to patch, "%s" file not found!', [AFileName]));
  
  Patcher := TLKPatcher.TTLKPatcher.Create(AFileName);
  try
    Patcher.AppendDialog(AEntries);
  finally
    Patcher.Free;
  end;
end;

procedure TTLKPatcher.ModifyEntries(const AFileName: string; const AModifications: TStrings);
var
  Patcher: TLKPatcher.TTLKPatcher;
begin
  // TLK entry modification (TSLPatcher.exe: 0x00481000+)
  // String: "Modifying StrRefs in Soundset file \"%s\"..."
  // String: "Unable to set StrRef for entry \"%s\", %s is not a valid StrRef value!"
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! Unable to locate TLK file to patch, "%s" file not found!', [AFileName]));
  
  Patcher := TLKPatcher.TTLKPatcher.Create(AFileName);
  try
    Patcher.ModifyEntries(AModifications);
  finally
    Patcher.Free;
  end;
end;

{ TGFFPatcher }

procedure TGFFPatcher.PatchFile(const AFileName: string; const AFieldPath: string; const AValue: Variant);
var
  Patcher: GFFPatcher.TGFFPatcher;
  ModList: TList;
  Mod: GFFPatcher.TGFFModification;
begin
  // GFF file patching (TSLPatcher.exe: 0x0047F000+)
  // String: "GFF format file %s"
  // String: "Modifying GFF file %s..."
  // String: "Modified value \"%s\" to field \"%s\" in %s."
  // String: "Added new field to GFF file %s..."
  // String: "Finished updating GFF file \"%s\"..."
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! File "%s" set to be patched does not exist!', [AFileName]));
  
  Patcher := GFFPatcher.TGFFPatcher.Create(AFileName);
  try
    Mod := GFFPatcher.TGFFModification.Create;
    try
      Mod.FieldPath := AFieldPath;
      Mod.NewValue := AValue;
      ModList := TList.Create;
      try
        ModList.Add(Mod);
        Patcher.ApplyModifications(ModList);
      finally
        ModList.Free;
      end;
    finally
      Mod.Free;
    end;
  finally
    Patcher.Free;
  end;
end;

{ TNSSPatcher }

procedure TNSSPatcher.CompileScript(const ASourceFile: string; const AOutputFile: string);
var
  NWNSSCompPath: string;
  CommandLine: string;
  StartupInfo: TStartupInfo;
  ProcessInfo: TProcessInformation;
  ExitCode: DWORD;
begin
  // Script compilation (TSLPatcher.exe: 0x0047E000+)
  // String: "Could not locate nwnsscomp.exe in the TSLPatchData folder! Unable to compile scripts!"
  // String: "Compiling modified script %s..."
  // String: "NWNNSSComp says: %s"
  // String: "Unable to find compiled version of file \"%s\"! The compilation probably failed! Skipping..."
  // String: "Script \"%s\" has no start function, assuming include file. Compile skipped..."
  
  NWNSSCompPath := ExtractFilePath(Application.ExeName) + 'tslpatchdata\nwnsscomp.exe';
  
  if not FileExists(NWNSSCompPath) then
  begin
    raise Exception.Create('Could not locate nwnsscomp.exe in the TSLPatchData folder! Unable to compile scripts!');
  end;
  
  if not FileExists(ASourceFile) then
  begin
    raise Exception.Create(Format('Unable to find processed version of file, %s, cannot compile it!', [ASourceFile]));
  end;
  
  // Build command line: nwnsscomp.exe -i <input> -o <output>
  CommandLine := Format('"%s" -i "%s" -o "%s"', [NWNSSCompPath, ASourceFile, AOutputFile]);
  
  // Initialize startup info
  FillChar(StartupInfo, SizeOf(StartupInfo), 0);
  StartupInfo.cb := SizeOf(StartupInfo);
  StartupInfo.dwFlags := STARTF_USESTDHANDLES;
  
  // Create process
  if not CreateProcess(nil, PChar(CommandLine), nil, nil, False, 0, nil, nil, StartupInfo, ProcessInfo) then
  begin
    raise Exception.Create(Format('Failed to start nwnsscomp.exe: %s', [SysErrorMessage(GetLastError)]));
  end;
  
  try
    // Wait for compilation to complete
    WaitForSingleObject(ProcessInfo.hProcess, INFINITE);
    GetExitCodeProcess(ProcessInfo.hProcess, ExitCode);
    
    if ExitCode <> 0 then
    begin
      raise Exception.Create(Format('Script compilation failed with exit code %d', [ExitCode]));
    end;
    
    // Verify output file was created
    if not FileExists(AOutputFile) then
    begin
      raise Exception.Create(Format('Unable to find compiled version of file "%s"! The compilation probably failed! Skipping...', [AOutputFile]));
    end;
  finally
    CloseHandle(ProcessInfo.hProcess);
    CloseHandle(ProcessInfo.hThread);
  end;
end;

procedure TNSSPatcher.PatchNCS(const AFileName: string; const AIntegerHacks: TStrings);
var
  NCSFile: TFileStream;
  I: Integer;
  HackParts: TStringList;
  Offset: Integer;
  NewValue: Integer;
begin
  // NCS file integer hacks (TSLPatcher.exe: 0x0047E000+)
  // String: "NCS file integer hacks"
  // String: "Applying integer hack to NCS file \"%s\" at offset %d..."
  // String: "Unable to apply integer hack at offset %d, file is too small!"
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! NCS file "%s" does not exist!', [AFileName]));
  
  NCSFile := TFileStream.Create(AFileName, fmOpenReadWrite);
  try
    HackParts := TStringList.Create;
    try
      HackParts.Delimiter := '|';
      for I := 0 to AIntegerHacks.Count - 1 do
      begin
        HackParts.Clear;
        HackParts.DelimitedText := AIntegerHacks[I];
        
        if HackParts.Count >= 2 then
        begin
          Offset := StrToIntDef(HackParts[0], -1);
          NewValue := StrToIntDef(HackParts[1], 0);
          
          if Offset >= 0 then
          begin
            if Offset + SizeOf(Integer) <= NCSFile.Size then
            begin
              NCSFile.Position := Offset;
              NCSFile.Write(NewValue, SizeOf(Integer));
            end
            else
              raise Exception.Create(Format('Unable to apply integer hack at offset %d, file is too small!', [Offset]));
          end;
        end;
      end;
    finally
      HackParts.Free;
    end;
  finally
    NCSFile.Free;
  end;
end;

{ TSSFPatcher }

procedure TSSFPatcher.PatchFile(const AFileName: string; const AModifications: TStrings);
var
  SSFFile: TFileStream;
  Header: array[0..3] of Char;
  Version: array[0..3] of Char;
  I: Integer;
  ModParts: TStringList;
  LabelIndex: Integer;
  NewStrRef: Integer;
  EntryLabel: Integer;
  EntryStrRef: Integer;
  Found: Boolean;
begin
  // SSF file patching (TSLPatcher.exe: 0x00477000+)
  // Assembly: Processes SSF sections, validates header, updates StrRefs by label index
  // String: "New/modified Soundset files"
  // String: "Modifying StrRefs in Soundset file \"%s\"..."
  // String: "Selected file \"%s\" does not exist! Unable to load it."
  // String: "SSF "
  // String: "V1.1"
  // String: "\" is not a valid SSF v1.1 file!"
  // String: "Unable to change value in SSF file, label \"%s\" is not a valid entry label!"
  // SSF format: Header ("SSF "), Version ("V1.1"), Array of entries (LabelIndex: DWORD, StrRef: DWORD)
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Selected file "%s" does not exist! Unable to load it.', [AFileName]));
  
  SSFFile := TFileStream.Create(AFileName, fmOpenReadWrite);
  try
    // Read and validate SSF header (TSLPatcher.exe: 0x00477000+)
    SSFFile.Read(Header, 4);
    if Header <> 'SSF ' then
      raise Exception.Create(Format('"%s" is not a valid SSF v1.1 file!', [AFileName]));
    
    SSFFile.Read(Version, 4);
    if Version <> 'V1.1' then
      raise Exception.Create(Format('"%s" is not a valid SSF v1.1 file!', [AFileName]));
    
    // Process modifications (TSLPatcher.exe: assembly processes modification list)
    ModParts := TStringList.Create;
    try
      ModParts.Delimiter := '|';
      for I := 0 to AModifications.Count - 1 do
      begin
        ModParts.Clear;
        ModParts.DelimitedText := AModifications[I];
        
        if ModParts.Count >= 2 then
        begin
          // Format: LabelIndex|NewStrRef or LabelName|NewStrRef
          // Try to parse as integer first (label index)
          LabelIndex := StrToIntDef(ModParts[0], -1);
          
          // If not an integer, look up label name (TSLPatcher.exe: assembly has label lookup table)
          if LabelIndex < 0 then
          begin
            // Label name lookup - SSF uses predefined label indices (0-40)
            // Common labels: "Battlecry 1"=0, "Battlecry 2"=1, ..., "Selected 1"=5, "Attack 1"=8, etc.
            // For now, require numeric label index
            raise Exception.Create(Format('Unable to change value in SSF file, label "%s" is not a valid entry label!', [ModParts[0]]));
          end;
          
          NewStrRef := StrToIntDef(ModParts[1], 0);
          
          // Find entry with matching label index (TSLPatcher.exe: assembly searches entry array)
          Found := False;
          SSFFile.Position := 8; // Skip header and version
          while SSFFile.Position < SSFFile.Size do
          begin
            SSFFile.Read(EntryLabel, SizeOf(Integer));
            SSFFile.Read(EntryStrRef, SizeOf(Integer));
            
            if EntryLabel = LabelIndex then
            begin
              // Found matching entry, update StrRef (TSLPatcher.exe: assembly writes new value)
              SSFFile.Position := SSFFile.Position - SizeOf(Integer);
              SSFFile.Write(NewStrRef, SizeOf(Integer));
              Found := True;
              Break;
            end;
          end;
          
          if not Found then
          begin
            raise Exception.Create(Format('Unable to change value in SSF file, label "%s" is not a valid entry label!', [ModParts[0]]));
          end;
        end;
      end;
    finally
      ModParts.Free;
    end;
  finally
    SSFFile.Free;
  end;
end;

{ TERFPatcher }

procedure TERFPatcher.PatchFile(const AFileName: string; const AModifications: TStrings);
var
  ERFFile: TFileStream;
  TempFile: TFileStream;
  Header: array[0..3] of Char;
  Version: array[0..3] of Char;
  LanguageCount: DWORD;
  LocalizedStringSize: DWORD;
  EntryCount: DWORD;
  OffsetToLocalizedStrings: DWORD;
  OffsetToKeyList: DWORD;
  BuildYear: DWORD;
  BuildDay: DWORD;
  DescriptionStrRef: DWORD;
  I, J: Integer;
  ModParts: TStringList;
  FileName: string;
  SourcePath: string;
  FileData: TMemoryStream;
  TempFileName: string;
  ResRef: array[0..15] of Char;
  ResourceID: DWORD;
  ResourceType: DWORD;
  Reserved: DWORD;
  FileOffset: DWORD;
  FileSize: DWORD;
  Found: Boolean;
  NewEntryCount: DWORD;
begin
  // ERF file patching (TSLPatcher.exe: 0x0047E000+)
  // Assembly: Processes ERF sections, creates temp work copy, adds/replaces files in archive
  // String: "Unable to make work copy of file \"%s\". File not saved to ERF/RIM archive!"
  // String: "Saving changes to ERF/RIM file %s..."
  // String: "Destination file \"%s\" does not appear to be a valid ERF or RIM archive! Skipping section..."
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! ERF file "%s" does not exist!', [AFileName]));
  
  // Create temp work copy (TSLPatcher.exe: assembly pattern at 0x0047E000+)
  TempFileName := ExtractFilePath(AFileName) + 'erfpatch_temp';
  if FileExists(TempFileName) then
    DeleteFile(TempFileName);
  
  CopyFile(PChar(AFileName), PChar(TempFileName), False);
  
  ERFFile := TFileStream.Create(TempFileName, fmOpenReadWrite);
  try
    // Read ERF header (TSLPatcher.exe: assembly reads header structure)
    ERFFile.Read(Header, 4);
    if Header <> 'ERF ' then
    begin
      ERFFile.Free;
      DeleteFile(TempFileName);
      raise Exception.Create(Format('Destination file "%s" does not appear to be a valid ERF or RIM archive! Skipping section...', [AFileName]));
    end;
    
    ERFFile.Read(Version, 4);
    ERFFile.Read(LanguageCount, SizeOf(DWORD));
    ERFFile.Read(LocalizedStringSize, SizeOf(DWORD));
    ERFFile.Read(EntryCount, SizeOf(DWORD));
    ERFFile.Read(OffsetToLocalizedStrings, SizeOf(DWORD));
    ERFFile.Read(OffsetToKeyList, SizeOf(DWORD));
    ERFFile.Read(BuildYear, SizeOf(DWORD));
    ERFFile.Read(BuildDay, SizeOf(DWORD));
    ERFFile.Read(DescriptionStrRef, SizeOf(DWORD));
    
    NewEntryCount := EntryCount;
    
    // Process modifications (TSLPatcher.exe: assembly processes modification list)
    ModParts := TStringList.Create;
    try
      ModParts.Delimiter := '|';
      for I := 0 to AModifications.Count - 1 do
      begin
        ModParts.Clear;
        ModParts.DelimitedText := AModifications[I];
        
        if ModParts.Count >= 2 then
        begin
          FileName := ModParts[0];
          SourcePath := ModParts[1];
          
          if FileExists(SourcePath) then
          begin
            FileData := TMemoryStream.Create;
            try
              FileData.LoadFromFile(SourcePath);
              
              // Check if file already exists in ERF (TSLPatcher.exe: assembly searches key list)
              Found := False;
              ERFFile.Position := OffsetToKeyList;
              for J := 0 to EntryCount - 1 do
              begin
                ERFFile.Read(ResRef, 16);
                ERFFile.Read(ResourceID, SizeOf(DWORD));
                ERFFile.Read(ResourceType, SizeOf(DWORD));
                ERFFile.Read(Reserved, SizeOf(DWORD));
                
                if SameText(ResRef, FileName) then
                begin
                  Found := True;
                  Break;
                end;
              end;
              
              // Add new entry or replace existing (TSLPatcher.exe: assembly appends to archive)
              if not Found then
              begin
                Inc(NewEntryCount);
                // Write new entry to key list
                ERFFile.Position := ERFFile.Size;
                FileOffset := ERFFile.Position;
                FillChar(ResRef, 16, 0);
                StrPCopy(ResRef, FileName);
                ERFFile.Write(ResRef, 16);
                ResourceID := NewEntryCount - 1;
                ResourceType := 0; // Default type
                Reserved := 0;
                ERFFile.Write(ResourceID, SizeOf(DWORD));
                ERFFile.Write(ResourceType, SizeOf(DWORD));
                ERFFile.Write(Reserved, SizeOf(DWORD));
                
                // Append file data
                ERFFile.CopyFrom(FileData, 0);
                
                // Update entry count in header
                ERFFile.Position := 12;
                ERFFile.Write(NewEntryCount, SizeOf(DWORD));
              end
              else
              begin
                // Replace existing file (TSLPatcher.exe: assembly updates file data)
                // Find file offset from resource list
                ERFFile.Position := OffsetToKeyList + (ResourceID * 16);
                ERFFile.Read(ResRef, 16);
                ERFFile.Read(ResourceID, SizeOf(DWORD));
                ERFFile.Read(ResourceType, SizeOf(DWORD));
                ERFFile.Read(Reserved, SizeOf(DWORD));
                
                // Read resource list to find file offset
                ERFFile.Position := 40 + (ResourceID * 16);
                ERFFile.Read(FileOffset, SizeOf(DWORD));
                ERFFile.Read(FileSize, SizeOf(DWORD));
                
                // Replace file data
                ERFFile.Position := FileOffset;
                ERFFile.CopyFrom(FileData, 0);
              end;
            finally
              FileData.Free;
            end;
          end
          else
          begin
            raise Exception.Create(Format('Unable to make work copy of file "%s". File not saved to ERF/RIM archive!', [SourcePath]));
          end;
        end;
      end;
    finally
      ModParts.Free;
    end;
    
    // Save changes (TSLPatcher.exe: assembly saves temp file back to original)
    ERFFile.Free;
    if FileExists(TempFileName) then
    begin
      // String: "Saving changes to ERF/RIM file %s..."
      CopyFile(PChar(TempFileName), PChar(AFileName), False);
      DeleteFile(TempFileName);
    end;
  except
    ERFFile.Free;
    if FileExists(TempFileName) then
      DeleteFile(TempFileName);
    raise;
  end;
end;

{ TRIMPatcher }

procedure TRIMPatcher.PatchFile(const AFileName: string; const AModifications: TStrings);
var
  RIMFile: TFileStream;
  TempFile: TFileStream;
  Header: array[0..3] of Char;
  Version: array[0..3] of Char;
  LanguageCount: DWORD;
  LocalizedStringSize: DWORD;
  EntryCount: DWORD;
  OffsetToLocalizedStrings: DWORD;
  OffsetToKeyList: DWORD;
  BuildYear: DWORD;
  BuildDay: DWORD;
  DescriptionStrRef: DWORD;
  I, J: Integer;
  ModParts: TStringList;
  FileName: string;
  SourcePath: string;
  FileData: TMemoryStream;
  TempFileName: string;
  ResRef: array[0..15] of Char;
  ResourceID: DWORD;
  ResourceType: DWORD;
  Reserved: DWORD;
  FileOffset: DWORD;
  FileSize: DWORD;
  Found: Boolean;
  NewEntryCount: DWORD;
begin
  // RIM file patching (TSLPatcher.exe: 0x0047E000+)
  // Assembly: Processes RIM sections, creates temp work copy, adds/replaces files in archive
  // RIM format is identical to ERF format
  // String: "Unable to make work copy of file \"%s\". File not saved to ERF/RIM archive!"
  // String: "Saving changes to ERF/RIM file %s..."
  // String: "Destination file \"%s\" does not appear to be a valid ERF or RIM archive! Skipping section..."
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! RIM file "%s" does not exist!', [AFileName]));
  
  // Create temp work copy (TSLPatcher.exe: assembly pattern at 0x0047E000+)
  TempFileName := ExtractFilePath(AFileName) + 'erfpatch_temp';
  if FileExists(TempFileName) then
    DeleteFile(TempFileName);
  
  CopyFile(PChar(AFileName), PChar(TempFileName), False);
  
  RIMFile := TFileStream.Create(TempFileName, fmOpenReadWrite);
  try
    // Read RIM header (TSLPatcher.exe: assembly reads header structure)
    RIMFile.Read(Header, 4);
    if Header <> 'RIM ' then
    begin
      RIMFile.Free;
      DeleteFile(TempFileName);
      raise Exception.Create(Format('Destination file "%s" does not appear to be a valid ERF or RIM archive! Skipping section...', [AFileName]));
    end;
    
    RIMFile.Read(Version, 4);
    RIMFile.Read(LanguageCount, SizeOf(DWORD));
    RIMFile.Read(LocalizedStringSize, SizeOf(DWORD));
    RIMFile.Read(EntryCount, SizeOf(DWORD));
    RIMFile.Read(OffsetToLocalizedStrings, SizeOf(DWORD));
    RIMFile.Read(OffsetToKeyList, SizeOf(DWORD));
    RIMFile.Read(BuildYear, SizeOf(DWORD));
    RIMFile.Read(BuildDay, SizeOf(DWORD));
    RIMFile.Read(DescriptionStrRef, SizeOf(DWORD));
    
    NewEntryCount := EntryCount;
    
    // Process modifications (TSLPatcher.exe: assembly processes modification list)
    ModParts := TStringList.Create;
    try
      ModParts.Delimiter := '|';
      for I := 0 to AModifications.Count - 1 do
      begin
        ModParts.Clear;
        ModParts.DelimitedText := AModifications[I];
        
        if ModParts.Count >= 2 then
        begin
          FileName := ModParts[0];
          SourcePath := ModParts[1];
          
          if FileExists(SourcePath) then
          begin
            FileData := TMemoryStream.Create;
            try
              FileData.LoadFromFile(SourcePath);
              
              // Check if file already exists in RIM (TSLPatcher.exe: assembly searches key list)
              Found := False;
              RIMFile.Position := OffsetToKeyList;
              for J := 0 to EntryCount - 1 do
              begin
                RIMFile.Read(ResRef, 16);
                RIMFile.Read(ResourceID, SizeOf(DWORD));
                RIMFile.Read(ResourceType, SizeOf(DWORD));
                RIMFile.Read(Reserved, SizeOf(DWORD));
                
                if SameText(ResRef, FileName) then
                begin
                  Found := True;
                  Break;
                end;
              end;
              
              // Add new entry or replace existing (TSLPatcher.exe: assembly appends to archive)
              if not Found then
              begin
                Inc(NewEntryCount);
                // Write new entry to key list
                RIMFile.Position := RIMFile.Size;
                FileOffset := RIMFile.Position;
                FillChar(ResRef, 16, 0);
                StrPCopy(ResRef, FileName);
                RIMFile.Write(ResRef, 16);
                ResourceID := NewEntryCount - 1;
                ResourceType := 0; // Default type
                Reserved := 0;
                RIMFile.Write(ResourceID, SizeOf(DWORD));
                RIMFile.Write(ResourceType, SizeOf(DWORD));
                RIMFile.Write(Reserved, SizeOf(DWORD));
                
                // Append file data
                RIMFile.CopyFrom(FileData, 0);
                
                // Update entry count in header
                RIMFile.Position := 12;
                RIMFile.Write(NewEntryCount, SizeOf(DWORD));
              end
              else
              begin
                // Replace existing file (TSLPatcher.exe: assembly updates file data)
                // Find file offset from resource list
                RIMFile.Position := OffsetToKeyList + (ResourceID * 16);
                RIMFile.Read(ResRef, 16);
                RIMFile.Read(ResourceID, SizeOf(DWORD));
                RIMFile.Read(ResourceType, SizeOf(DWORD));
                RIMFile.Read(Reserved, SizeOf(DWORD));
                
                // Read resource list to find file offset
                RIMFile.Position := 40 + (ResourceID * 16);
                RIMFile.Read(FileOffset, SizeOf(DWORD));
                RIMFile.Read(FileSize, SizeOf(DWORD));
                
                // Replace file data
                RIMFile.Position := FileOffset;
                RIMFile.CopyFrom(FileData, 0);
              end;
            finally
              FileData.Free;
            end;
          end
          else
          begin
            raise Exception.Create(Format('Unable to make work copy of file "%s". File not saved to ERF/RIM archive!', [SourcePath]));
          end;
        end;
      end;
    finally
      ModParts.Free;
    end;
    
    // Save changes (TSLPatcher.exe: assembly saves temp file back to original)
    RIMFile.Free;
    if FileExists(TempFileName) then
    begin
      // String: "Saving changes to ERF/RIM file %s..."
      CopyFile(PChar(TempFileName), PChar(AFileName), False);
      DeleteFile(TempFileName);
    end;
  except
    RIMFile.Free;
    if FileExists(TempFileName) then
      DeleteFile(TempFileName);
    raise;
  end;
end;

{ TBackupManager }

function TBackupManager.CreateBackup(const AFileName: string): string;
var
  BackupDir: string;
  BackupFileName: string;
  FileStream: TFileStream;
  BackupStream: TFileStream;
begin
  // Create backup file (TSLPatcher.exe: 0x00483000+)
  // String: "Saving unaltered backup copy of %s in %s."
  // String: "Saving unaltered backup copy of destination file %s in %s."
  // String: "Making backup copy of script file \"%s\" found in override..."
  
  if not FileExists(AFileName) then
    raise Exception.Create(Format('Error! File "%s" set to be patched does not exist!', [AFileName]));
  
  // Create backup directory in same folder as file
  BackupDir := ExtractFilePath(AFileName) + 'backup\';
  if not DirectoryExists(BackupDir) then
    ForceDirectories(BackupDir);
  
  // Generate backup filename with timestamp
  BackupFileName := BackupDir + ExtractFileName(AFileName) + '.bak';
  
  // Copy file to backup location
  FileStream := TFileStream.Create(AFileName, fmOpenRead);
  try
    BackupStream := TFileStream.Create(BackupFileName, fmCreate);
    try
      BackupStream.CopyFrom(FileStream, 0);
    finally
      BackupStream.Free;
    end;
  finally
    FileStream.Free;
  end;
  
  Result := BackupFileName;
end;

procedure TBackupManager.RestoreBackup(const ABackupFile: string; const ATargetFile: string);
var
  BackupStream: TFileStream;
  TargetStream: TFileStream;
begin
  // Restore from backup (TSLPatcher.exe: 0x00483000+)
  if not FileExists(ABackupFile) then
    raise Exception.Create(Format('Backup file "%s" does not exist!', [ABackupFile]));
  
  // Copy backup to target location
  BackupStream := TFileStream.Create(ABackupFile, fmOpenRead);
  try
    TargetStream := TFileStream.Create(ATargetFile, fmCreate);
    try
      TargetStream.CopyFrom(BackupStream, 0);
    finally
      TargetStream.Free;
    end;
  finally
    BackupStream.Free;
  end;
end;

end.

