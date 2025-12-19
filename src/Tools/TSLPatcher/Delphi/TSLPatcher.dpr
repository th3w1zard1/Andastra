program TSLPatcher;

{*
 * TSLPatcher - Reverse Engineered from TSLPatcher.exe
 * Deterministic Compilation Test Project
 * 
 * Compiler Settings for Deterministic Build:
 * - Delphi 7 or compatible
 * - Optimization: ON
 * - Range Checking: OFF
 * - Stack Checking: OFF
 * - I/O Checking: OFF
 * - Overflow Checking: OFF
 * - Assertions: OFF
 * - Debug Information: ON (for address mapping)
 * - Local Symbols: ON
 * - Reference Info: ON
 *}

{$APPTYPE CONSOLE}

uses
  SysUtils,
  TSLPatcher in 'TSLPatcher.pas';

var
  MainForm: TMainForm;
begin
  try
    MainForm := TMainForm.Create(nil);
    try
      MainForm.Initialize;
      MainForm.StartPatching;
    finally
      MainForm.Free;
    end;
  except
    on E: Exception do
    begin
      Writeln('Error: ', E.Message);
      ExitCode := 1;
    end;
  end;
end.

