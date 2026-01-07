NCSDecomp - NCS File Decompiler
Version 1.0.0

DESCRIPTION:
NCSDecomp is a tool for decompiling NCS (Neverwinter Nights Compiled Script) files
back into NSS (Neverwinter Nights Script Source) format.

USAGE:
Command Line Mode:
  NCSDecomp.exe <file1.ncs> [file2.ncs ...] [--output-dir <path>]
  
  Options:
    --output-dir <path>  Specify output directory for decompiled files
    --help               Show help message

  Examples:
    NCSDecomp.exe script.ncs
    NCSDecomp.exe script1.ncs script2.ncs --output-dir C:\output
    NCSDecomp.exe --help

GUI Mode:
  Run NCSDecomp.exe without any arguments to launch the graphical interface.

DEPENDENCIES:
- .NET 9.0 Runtime (required)
- All required DLLs are included in this package

NOTES:
- Decompiled files will be saved with .nss extension
- Some files may fail compilation validation if they use game-specific functions
  that aren't available in the standard compiler. This is expected and the
  decompiled code is still valid.

LICENSE:
Part of the Andastra project.
