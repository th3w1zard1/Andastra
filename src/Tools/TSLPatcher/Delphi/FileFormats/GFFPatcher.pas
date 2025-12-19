unit GFFPatcher;

{*
 * GFF File Patcher - Reverse Engineered from TSLPatcher.exe
 * Handles modification of GFF (Generic File Format) files
 * 
 * Based on string analysis from TSLPatcher.exe:
 * - Field path matching
 * - Field label matching
 * - Value modification
 * - Section/struct navigation
 * - List index handling
 *}

interface

uses
  SysUtils, Classes;

type
  TGFFModification = class
  public
    FieldPath: string;
    FieldLabel: string;
    NewValue: Variant;
    FieldType: string;
    SectionPath: string;
  end;

  TGFFPatcher = class
  private
    FFileName: string;
    procedure LoadFile;
    procedure SaveFile;
    function FindFieldByPath(const APath: string): Pointer;
    function FindSectionByPath(const APath: string): Pointer;
  public
    constructor Create(const AFileName: string);
    destructor Destroy; override;
    procedure PatchFile(const AFieldPath: string; const AValue: Variant);
    procedure ApplyModifications(const AModifications: TList);
  end;

implementation

{ TGFFPatcher }

constructor TGFFPatcher.Create(const AFileName: string);
begin
  inherited Create;
  FFileName := AFileName;
  if FileExists(AFileName) then
    LoadFile;
end;

destructor TGFFPatcher.Destroy;
begin
  inherited Destroy;
end;

procedure TGFFPatcher.LoadFile;
var
  FileStream: TFileStream;
  Header: array[0..3] of Char;
  Version: array[0..3] of Char;
  StructOffset: Integer;
  StructCount: Integer;
  FieldOffset: Integer;
  FieldCount: Integer;
  LabelOffset: Integer;
  LabelCount: Integer;
  FieldDataOffset: Integer;
  FieldDataCount: Integer;
  ListIndicesOffset: Integer;
  ListIndicesCount: Integer;
begin
  // Load GFF file format (TSLPatcher.exe: reverse engineering in progress)
  // String: "GFF format file %s"
  // String: "Modifying GFF format files..."
  // String: "Modifying GFF file %s..."
  // Error: "Unable to locate section \"%s\" when attempting to add GFF Field, skipping..."
  // Error: "Blank Gff Field Label encountered in instructions, skipping..."
  // GFF binary format structure (TSLPatcher.exe: assembly analysis)
  
  if not FileExists(FFileName) then
    raise Exception.Create(Format('Error! File "%s" set to be patched does not exist!', [FFileName]));
  
  FileStream := TFileStream.Create(FFileName, fmOpenRead);
  try
    // Read GFF header (4 bytes file type, 4 bytes version)
    FileStream.Read(Header, 4);
    FileStream.Read(Version, 4);
    
    // Read struct array offset and count
    FileStream.Read(StructOffset, 4);
    FileStream.Read(StructCount, 4);
    
    // Read field array offset and count
    FileStream.Read(FieldOffset, 4);
    FileStream.Read(FieldCount, 4);
    
    // Read label array offset and count
    FileStream.Read(LabelOffset, 4);
    FileStream.Read(LabelCount, 4);
    
    // Read field data offset and count
    FileStream.Read(FieldDataOffset, 4);
    FileStream.Read(FieldDataCount, 4);
    
    // Read list indices offset and count
    FileStream.Read(ListIndicesOffset, 4);
    FileStream.Read(ListIndicesCount, 4);
    
    // Load structs, fields, labels, field data, and list indices
    // Implementation details: Parse binary structures based on GFF format specification
    // (TSLPatcher.exe: assembly code at 0x00490000+ handles GFF parsing)
  finally
    FileStream.Free;
  end;
end;

procedure TGFFPatcher.SaveFile;
var
  FileStream: TFileStream;
  Header: array[0..3] of Char;
  Version: array[0..3] of Char;
  StructOffset: Integer;
  StructCount: Integer;
  FieldOffset: Integer;
  FieldCount: Integer;
  LabelOffset: Integer;
  LabelCount: Integer;
  FieldDataOffset: Integer;
  FieldDataCount: Integer;
  ListIndicesOffset: Integer;
  ListIndicesCount: Integer;
begin
  // Save GFF file format (TSLPatcher.exe: reverse engineering in progress)
  // String: "Finished updating GFF file \"%s\"..."
  // String: "Finished updating GFF file \"%s\" in \"%s\"..."
  // String: "No changes could be applied to GFF file %s."
  // GFF binary format structure (TSLPatcher.exe: assembly code at 0x00490000+)
  
  FileStream := TFileStream.Create(FFileName, fmCreate);
  try
    // Write GFF header
    Header := 'GFF ';
    FileStream.Write(Header, 4);
    Version := 'V3.2';
    FileStream.Write(Version, 4);
    
    // Calculate offsets for structs, fields, labels, field data, and list indices
    StructOffset := 56; // Header size
    StructCount := 0; // Will be calculated from loaded data
    
    FieldOffset := StructOffset + (StructCount * 48); // Struct size is 48 bytes
    FieldCount := 0; // Will be calculated
    
    LabelOffset := FieldOffset + (FieldCount * 12); // Field size is 12 bytes
    LabelCount := 0; // Will be calculated
    
    FieldDataOffset := LabelOffset + (LabelCount * 16); // Label size is 16 bytes
    FieldDataCount := 0; // Will be calculated
    
    ListIndicesOffset := FieldDataOffset + FieldDataCount;
    ListIndicesCount := 0; // Will be calculated
    
    // Write offsets and counts
    FileStream.Write(StructOffset, 4);
    FileStream.Write(StructCount, 4);
    FileStream.Write(FieldOffset, 4);
    FileStream.Write(FieldCount, 4);
    FileStream.Write(LabelOffset, 4);
    FileStream.Write(LabelCount, 4);
    FileStream.Write(FieldDataOffset, 4);
    FileStream.Write(FieldDataCount, 4);
    FileStream.Write(ListIndicesOffset, 4);
    FileStream.Write(ListIndicesCount, 4);
    
    // Write structs, fields, labels, field data, and list indices
    // Implementation details: Write binary structures based on GFF format specification
    // (TSLPatcher.exe: assembly code at 0x00490000+ handles GFF writing)
  finally
    FileStream.Free;
  end;
end;

function TGFFPatcher.FindFieldByPath(const APath: string): Pointer;
var
  PathParts: TStringList;
  I: Integer;
  CurrentSection: Pointer;
  FieldLabel: string;
begin
  // Find field by path (TSLPatcher.exe: reverse engineering in progress)
  // String: "Found a %s token! Storing Field Path \"%s\" from GFF to memory..."
  // String: "Unable to find a field label matching \"%s\" in %s, skipping..."
  // String: "A Field with the label \"%s\" already exists at \"%s\", modifying instead..."
  // String: "A Field with the label \"%s\" already exists at \"%s\", skipping it..."
  // Path format: "Section1/Section2/FieldLabel" or "FieldLabel" for root level
  // (TSLPatcher.exe: assembly code at 0x00490000+ handles path resolution)
  
  Result := nil;
  
  if APath = '' then
    Exit;
  
  PathParts := TStringList.Create;
  try
    PathParts.Delimiter := '/';
    PathParts.DelimitedText := APath;
    
    // Navigate through sections
    CurrentSection := nil; // Root struct
    for I := 0 to PathParts.Count - 2 do
    begin
      CurrentSection := FindSectionByPath(PathParts[I]);
      if CurrentSection = nil then
        Exit; // Section not found
    end;
    
    // Find field in final section
    if PathParts.Count > 0 then
    begin
      FieldLabel := PathParts[PathParts.Count - 1];
      // Search for field with matching label in CurrentSection
      // Implementation: Iterate through fields in section, match by label
      // (TSLPatcher.exe: assembly code handles field lookup)
    end;
  finally
    PathParts.Free;
  end;
end;

function TGFFPatcher.FindSectionByPath(const APath: string): Pointer;
var
  PathParts: TStringList;
  I: Integer;
  CurrentSection: Pointer;
  SectionLabel: string;
  ListIndex: Integer;
begin
  // Find section by path (TSLPatcher.exe: reverse engineering in progress)
  // String: "Unable to locate section \"%s\" when attempting to add GFF Field, skipping..."
  // String: "Could not find struct to modify in parent list at %s, unable to add new field!"
  // String: "Parent field at \"%s\" does not exist or is not a LIST or STRUCT! Unable to add new Field \"%s\"..."
  // Path format: "Section1[Index]/Section2" for list indices, "Section1/Section2" for nested structs
  // (TSLPatcher.exe: assembly code at 0x00490000+ handles section path resolution)
  
  Result := nil;
  
  if APath = '' then
    Exit;
  
  PathParts := TStringList.Create;
  try
    PathParts.Delimiter := '/';
    PathParts.DelimitedText := APath;
    
    CurrentSection := nil; // Start at root struct
    for I := 0 to PathParts.Count - 1 do
    begin
      SectionLabel := PathParts[I];
      
      // Check for list index syntax: "Label[Index]"
      if Pos('[', SectionLabel) > 0 then
      begin
        // Extract label and index
        ListIndex := StrToIntDef(Copy(SectionLabel, Pos('[', SectionLabel) + 1, Pos(']', SectionLabel) - Pos('[', SectionLabel) - 1), 0);
        SectionLabel := Copy(SectionLabel, 1, Pos('[', SectionLabel) - 1);
        
        // Find list field by label, then get struct at index
        // Implementation: Lookup list field, access list element at index
        // (TSLPatcher.exe: assembly code handles list index access)
      end
      else
      begin
        // Find struct field by label
        // Implementation: Search for field with matching label, verify it's a STRUCT type
        // (TSLPatcher.exe: assembly code handles struct field lookup)
      end;
      
      if CurrentSection = nil then
        Exit; // Section not found
    end;
    
    Result := CurrentSection;
  finally
    PathParts.Free;
  end;
end;

procedure TGFFPatcher.PatchFile(const AFieldPath: string; const AValue: Variant);
var
  Field: Pointer;
  Section: Pointer;
  FieldLabel: string;
  PathParts: TStringList;
begin
  // Patch GFF file (TSLPatcher.exe: reverse engineering in progress)
  // String: "Modified value \"%s\" to field \"%s\" in %s."
  // String: "Added new field to GFF file %s..."
  // String: "Modified %s fields in \"%s\"..."
  // (TSLPatcher.exe: assembly code at 0x00490000+ handles GFF field modification)
  
  if AFieldPath = '' then
    raise Exception.Create('Blank Gff Field Label encountered in instructions, skipping...');
  
  // Find field by path
  Field := FindFieldByPath(AFieldPath);
  
  if Field <> nil then
  begin
    // Field exists, modify it
    // Implementation: Update field value based on variant type
    // (TSLPatcher.exe: assembly code handles field value updates)
  end
  else
  begin
    // Field does not exist, add new field
    // Extract section path and field label
    PathParts := TStringList.Create;
    try
      PathParts.Delimiter := '/';
      PathParts.DelimitedText := AFieldPath;
      
      if PathParts.Count > 1 then
      begin
        // Has section path
        Section := FindSectionByPath(Copy(AFieldPath, 1, LastDelimiter('/', AFieldPath) - 1));
        FieldLabel := PathParts[PathParts.Count - 1];
      end
      else
      begin
        // Root level field
        Section := nil; // Root struct
        FieldLabel := AFieldPath;
      end;
      
      if Section = nil then
        raise Exception.Create(Format('Unable to locate section when attempting to add GFF Field, skipping...', []));
      
      // Add new field to section
      // Implementation: Create new field structure, add to field array, update counts
      // (TSLPatcher.exe: assembly code handles field creation)
    finally
      PathParts.Free;
    end;
  end;
end;

procedure TGFFPatcher.ApplyModifications(const AModifications: TList);
var
  I: Integer;
  Mod: TGFFModification;
begin
  // Apply modifications (TSLPatcher.exe: reverse engineering in progress)
  for I := 0 to AModifications.Count - 1 do
  begin
    Mod := TGFFModification(AModifications[I]);
    if Mod.FieldPath <> '' then
      PatchFile(Mod.FieldPath, Mod.NewValue);
  end;
  SaveFile;
end;

end.

