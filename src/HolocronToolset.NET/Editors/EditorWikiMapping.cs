using System.Collections.Generic;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/editor_wiki_mapping.py
    // Original: EDITOR_WIKI_MAP: dict[str, str | None]
    public static class EditorWikiMapping
    {
        // Editor class name -> wiki markdown filename (null means no help available)
        public static readonly Dictionary<string, string> EditorWikiMap = new Dictionary<string, string>
        {
            { "AREEditor", "GFF-ARE.md" },
            { "BWMEditor", "BWM-File-Format.md" },
            { "DLGEditor", "GFF-DLG.md" },
            { "ERFEditor", "ERF-File-Format.md" },
            { "GFFEditor", "GFF-File-Format.md" }, // Generic GFF editor uses general format doc
            { "GITEditor", "GFF-GIT.md" },
            { "IFOEditor", "GFF-IFO.md" },
            { "JRLEditor", "GFF-JRL.md" },
            { "LTREditor", "LTR-File-Format.md" },
            { "LYTEditor", "LYT-File-Format.md" },
            { "LIPEditor", "LIP-File-Format.md" },
            { "MDLEditor", "MDL-MDX-File-Format.md" },
            { "NSSEditor", "NSS-File-Format.md" },
            { "NSEditor", "NCS-File-Format.md" }, // NCS compiled from NSS
            { "PTHEditor", "GFF-PTH.md" },
            { "SAVEditor", "GFF-File-Format.md" }, // Save game uses general GFF format doc
            { "SSFEditor", "SSF-File-Format.md" },
            { "TLKEditor", "TLK-File-Format.md" },
            { "TPCEditor", "TPC-File-Format.md" },
            // Note: TXTEditor intentionally not included - plain text, no specific format
            { "TwoDAEditor", "2DA-File-Format.md" },
            { "UTCEditor", "GFF-UTC.md" },
            { "UTDEditor", "GFF-UTD.md" },
            { "UTEEditor", "GFF-UTE.md" },
            { "UTIEditor", "GFF-UTI.md" },
            { "UTMEditor", "GFF-UTM.md" },
            { "UTPEditor", "GFF-UTP.md" },
            { "UTSEditor", "GFF-UTS.md" },
            { "UTTEditor", "GFF-UTT.md" },
            { "UTWEditor", "GFF-UTW.md" },
            { "WAVEditor", "WAV-File-Format.md" }, // WAV/Audio file format
            { "SaveGameEditor", "GFF-File-Format.md" }, // Save game uses general GFF format doc
            { "MetadataEditor", "GFF-File-Format.md" } // Metadata uses general GFF format doc
        };

        // Helper method to get wiki file for an editor class name
        // Returns null if editor has no wiki file (e.g., TXTEditor)
        public static string GetWikiFile(string editorClassName)
        {
            return EditorWikiMap.TryGetValue(editorClassName, out string wikiFile) ? wikiFile : null;
        }
    }
}
