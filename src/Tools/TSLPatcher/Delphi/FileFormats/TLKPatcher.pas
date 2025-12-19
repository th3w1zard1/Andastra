unit TLKPatcher;

{*
 * TLK File Patcher - Reverse Engineered from TSLPatcher.exe
 * Handles modification and appending of TLK (Talk) files
 * 
 * Based on string analysis from TSLPatcher.exe:
 * - dialog.tlk file handling
 * - append.tlk file appending
 * - StrRef token management
 * - TLKList processing
 * - String entry modification
 *}

interface

uses
  SysUtils, Classes;

type
  TTLKEntry = class
  public
    StrRef: Integer;
    Text: string;
    SoundResRef: string;
    VolumeVariance: Integer;
    PitchVariance: Integer;
    constructor Create;
  end;

  TTLKPatcher = class
  private
    FFileName: string;
    FEntries: TList;
    FNextStrRef: Integer;
    procedure LoadFile;
    procedure SaveFile;
    function FindEntryByStrRef(AStrRef: Integer): TTLKEntry;
    function FindEntryByText(const AText: string): TTLKEntry;
  public
    constructor Create(const AFileName: string);
    destructor Destroy; override;
    procedure AppendDialog(const AEntries: TStrings);
    procedure ModifyEntries(const AModifications: TStrings);
    function GetStrRefForToken(const AToken: string): Integer;
  end;

implementation

{ TTLKEntry }

constructor TTLKEntry.Create;
begin
  inherited Create;
  StrRef := -1;
  VolumeVariance := 0;
  PitchVariance := 0;
end;

{ TTLKPatcher }

constructor TTLKPatcher.Create(const AFileName: string);
begin
  inherited Create;
  FFileName := AFileName;
  FEntries := TList.Create;
  FNextStrRef := 0;
  if FileExists(AFileName) then
    LoadFile;
end;

destructor TTLKPatcher.Destroy;
var
  I: Integer;
begin
  for I := 0 to FEntries.Count - 1 do
    TTLKEntry(FEntries[I]).Free;
  FEntries.Free;
  inherited Destroy;
end;

procedure TTLKPatcher.LoadFile;
var
  FileStream: TFileStream;
  Header: array[0..3] of Char;
  StringCount: Integer;
  StringDataOffset: Integer;
  I: Integer;
  Entry: TTLKEntry;
  TextLength: Integer;
  TextBuffer: array of Char;
  Flags: Word;
begin
  // Load TLK file format (TSLPatcher.exe: reverse engineering in progress)
  // String: "Error! Unable to locate TLK file to patch, \"%s\" file not found!"
  // String: "Invalid game folder specified, dialog.tlk file not found!"
  // String: "No TLK file loaded. Unable to proceed."
  
  if not FileExists(FFileName) then
    raise Exception.Create(Format('Error! Unable to locate TLK file to patch, "%s" file not found!', [FFileName]));
  
  FileStream := TFileStream.Create(FFileName, fmOpenRead);
  try
    // Read TLK header
    FileStream.Read(Header, 4);
    if Header <> 'TLK ' then
      raise Exception.Create(Format('Invalid TLK file format: %s', [FFileName]));
    
    // Read version (should be V3.0)
    FileStream.Read(Header, 4);
    
    // Read string count
    FileStream.Read(StringCount, 4);
    
    // Read string data offset
    FileStream.Read(StringDataOffset, 4);
    
    // Read entries
    for I := 0 to StringCount - 1 do
    begin
      Entry := TTLKEntry.Create;
      try
        // Read flags
        FileStream.Read(Flags, 2);
        
        // Read sound resref
        FileStream.Read(Entry.SoundResRef[1], 16);
        SetLength(Entry.SoundResRef, 16);
        
        // Read volume variance
        FileStream.Read(Entry.VolumeVariance, 4);
        
        // Read pitch variance
        FileStream.Read(Entry.PitchVariance, 4);
        
        // Read string offset
        FileStream.Read(StringDataOffset, 4);
        
        // Read string length
        FileStream.Read(TextLength, 4);
        
        // Read string text
        if TextLength > 0 then
        begin
          SetLength(TextBuffer, TextLength);
          FileStream.Position := StringDataOffset;
          FileStream.Read(TextBuffer[0], TextLength);
          Entry.Text := string(TextBuffer);
          SetLength(TextBuffer, 0);
        end;
        
        Entry.StrRef := I;
        FEntries.Add(Entry);
      except
        Entry.Free;
        raise;
      end;
    end;
    
    FNextStrRef := StringCount;
  finally
    FileStream.Free;
  end;
end;

procedure TTLKPatcher.SaveFile;
var
  FileStream: TFileStream;
  Header: array[0..3] of Char;
  StringCount: Integer;
  StringDataOffset: Integer;
  I: Integer;
  Entry: TTLKEntry;
  TextLength: Integer;
  Flags: Word;
  CurrentOffset: Integer;
begin
  // Save TLK file format (TSLPatcher.exe: reverse engineering in progress)
  FileStream := TFileStream.Create(FFileName, fmCreate);
  try
    // Write TLK header
    Header := 'TLK ';
    FileStream.Write(Header, 4);
    
    // Write version (V3.0)
    Header := 'V3.0';
    FileStream.Write(Header, 4);
    
    StringCount := FEntries.Count;
    FileStream.Write(StringCount, 4);
    
    // Calculate string data offset (after all entry headers)
    StringDataOffset := 20 + (StringCount * 40); // Header + entry headers
    FileStream.Write(StringDataOffset, 4);
    
    // Write entry headers
    CurrentOffset := StringDataOffset;
    for I := 0 to FEntries.Count - 1 do
    begin
      Entry := TTLKEntry(FEntries[I]);
      
      Flags := 0;
      if Entry.Text <> '' then
        Flags := Flags or 1;
      FileStream.Write(Flags, 2);
      
      // Write sound resref (16 bytes)
      if Length(Entry.SoundResRef) > 0 then
        FileStream.Write(Entry.SoundResRef[1], Min(Length(Entry.SoundResRef), 16));
      if Length(Entry.SoundResRef) < 16 then
      begin
        // Pad with zeros
        TextLength := 0;
        FileStream.Write(TextLength, 16 - Length(Entry.SoundResRef));
      end;
      
      FileStream.Write(Entry.VolumeVariance, 4);
      FileStream.Write(Entry.PitchVariance, 4);
      FileStream.Write(CurrentOffset, 4);
      
      TextLength := Length(Entry.Text);
      FileStream.Write(TextLength, 4);
      
      CurrentOffset := CurrentOffset + TextLength;
    end;
    
    // Write string data
    for I := 0 to FEntries.Count - 1 do
    begin
      Entry := TTLKEntry(FEntries[I]);
      if Entry.Text <> '' then
        FileStream.Write(Entry.Text[1], Length(Entry.Text));
    end;
  finally
    FileStream.Free;
  end;
end;

function TTLKPatcher.FindEntryByStrRef(AStrRef: Integer): TTLKEntry;
var
  I: Integer;
  Entry: TTLKEntry;
begin
  Result := nil;
  for I := 0 to FEntries.Count - 1 do
  begin
    Entry := TTLKEntry(FEntries[I]);
    if Entry.StrRef = AStrRef then
    begin
      Result := Entry;
      Exit;
    end;
  end;
end;

function TTLKPatcher.FindEntryByText(const AText: string): TTLKEntry;
var
  I: Integer;
  Entry: TTLKEntry;
begin
  Result := nil;
  for I := 0 to FEntries.Count - 1 do
  begin
    Entry := TTLKEntry(FEntries[I]);
    if SameText(Entry.Text, AText) then
    begin
      Result := Entry;
      Exit;
    end;
  end;
end;

function TTLKPatcher.GetStrRefForToken(const AToken: string): Integer;
var
  Entry: TTLKEntry;
begin
  // Get StrRef for token (TSLPatcher.exe: reverse engineering in progress)
  // String: "Encountered StrRef token \"%s\" in modifier list that was not present in the TLKList! Value set to StrRef #0."
  // String: "Loading StrRef token table..."
  // String: "%s StrRef tokens found and indexed."
  
  Entry := FindEntryByText(AToken);
  if Entry <> nil then
    Result := Entry.StrRef
  else
    Result := 0; // Default to StrRef #0 if not found
end;

procedure TTLKPatcher.AppendDialog(const AEntries: TStrings);
var
  I: Integer;
  Entry: TTLKEntry;
  Parts: TStringList;
  ExistingEntry: TTLKEntry;
begin
  // Append dialog entries (TSLPatcher.exe: reverse engineering in progress)
  // String: "Appending strings to TLK file \"%s\""
  // String: "Appending new entry to %s, new StrRef is %s"
  // String: "Identical string for append StrRef %s found in %s StrRef %s, reusing it instead."
  // String: "Warning: No new entries appended to %s. Possible missing entries in append.tlk referenced in the TLKList."
  
  if FEntries.Count = 0 then
    raise Exception.Create('No TLK file loaded. Unable to proceed.');
  
  Parts := TStringList.Create;
  try
    Parts.Delimiter := '|';
    
    for I := 0 to AEntries.Count - 1 do
    begin
      Parts.Clear;
      Parts.DelimitedText := AEntries[I];
      
      if Parts.Count >= 2 then
      begin
        // Check if identical string already exists
        ExistingEntry := FindEntryByText(Parts[1]);
        if ExistingEntry <> nil then
        begin
          // Reuse existing StrRef
          // Message: "Identical string for append StrRef %s found in %s StrRef %s, reusing it instead."
          Continue;
        end;
        
        // Create new entry
        Entry := TTLKEntry.Create;
        Entry.StrRef := FNextStrRef;
        Inc(FNextStrRef);
        Entry.Text := Parts[1];
        
        if Parts.Count >= 3 then
          Entry.SoundResRef := Parts[2];
        if Parts.Count >= 4 then
          Entry.VolumeVariance := StrToIntDef(Parts[3], 0);
        if Parts.Count >= 5 then
          Entry.PitchVariance := StrToIntDef(Parts[4], 0);
        
        FEntries.Add(Entry);
        // Message: "Appending new entry to %s, new StrRef is %s"
      end;
    end;
  finally
    Parts.Free;
  end;
  
  SaveFile;
end;

procedure TTLKPatcher.ModifyEntries(const AModifications: TStrings);
var
  I: Integer;
  Entry: TTLKEntry;
  Parts: TStringList;
  StrRef: Integer;
begin
  // Modify TLK entries (TSLPatcher.exe: reverse engineering in progress)
  // String: "Modifying StrRefs in Soundset file \"%s\"..."
  // String: "Unable to set StrRef for entry \"%s\", %s is not a valid StrRef value!"
  
  if FEntries.Count = 0 then
    raise Exception.Create('No TLK file loaded. Unable to proceed.');
  
  Parts := TStringList.Create;
  try
    Parts.Delimiter := '|';
    
    for I := 0 to AModifications.Count - 1 do
    begin
      Parts.Clear;
      Parts.DelimitedText := AModifications[I];
      
      if Parts.Count >= 2 then
      begin
        StrRef := StrToIntDef(Parts[0], -1);
        if StrRef < 0 then
        begin
          // Error: "Unable to set StrRef for entry \"%s\", %s is not a valid StrRef value!"
          Continue;
        end;
        
        Entry := FindEntryByStrRef(StrRef);
        if Entry <> nil then
        begin
          Entry.Text := Parts[1];
          if Parts.Count >= 3 then
            Entry.SoundResRef := Parts[2];
          if Parts.Count >= 4 then
            Entry.VolumeVariance := StrToIntDef(Parts[3], 0);
          if Parts.Count >= 5 then
            Entry.PitchVariance := StrToIntDef(Parts[4], 0);
        end;
      end;
    end;
  finally
    Parts.Free;
  end;
  
  SaveFile;
end;

end.

