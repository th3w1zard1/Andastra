using System.Collections.Generic;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/editor_wiki_mapping.py
    // Original: EDITOR_WIKI_MAP: dict[str, str | None]
    public static class EditorWikiMapping
    {
        // Editor class name -> wiki markdown filename (null means no help available)
        public static readonly Dictionary<string, string> EditorWikiMap = new Dictionary<string, string>
        {
            { "AREEditor", "GFF-ARE.md", "Bioware-Aurora-AreaFile.md" },
            { "BWMEditor", "BWM-File-Format.md" },
            { "DLGEditor", "GFF-DLG.md", "Bioware-Aurora-Conversation.md" },
            { "ERFEditor", "ERF-File-Format.md", "Bioware-Aurora-ERF.md", "Bioware-Aurora-KeyBIF.md" },
            { "GFFEditor", "GFF-File-Format.md", "Bioware-Aurora-GFF.md", "Bioware-Aurora-CommonGFFStructs.md" }, // Generic GFF editor uses general format doc
            { "GITEditor", "GFF-GIT.md", "Bioware-Aurora-KeyBIF.md" },
            { "IFOEditor", "GFF-IFO.md", "Bioware-Aurora-IFO.md" },
            { "JRLEditor", "GFF-JRL.md", "Bioware-Aurora-Journal.md" },
            { "LTREditor", "LTR-File-Format.md" },
            { "LYTEditor", "LYT-File-Format.md" },
            { "LIPEditor", "LIP-File-Format.md" },
            { "MDLEditor", "MDL-MDX-File-Format.md" },
            { "NSSEditor", "NSS-File-Format.md", "NCS-File-Format.md" },
            { "PTHEditor", "GFF-PTH.md" },
            { "SAVEditor", "GFF-File-Format.md" }, // Save game uses general GFF format doc
            { "SSFEditor", "SSF-File-Format.md", "Bioware-Aurora-SSF.md" },
            { "TLKEditor", "TLK-File-Format.md", "Bioware-Aurora-TalkTable.md" },
            { "TPCEditor", "TPC-File-Format.md" },
            // Note: TXTEditor intentionally not included - plain text, no specific format
            { "TwoDAEditor", "2DA-File-Format.md", "Bioware-Aurora-2DA.md" },
            { "UTCEditor", "GFF-UTC.md", "Bioware-Aurora-Creature.md" },
            { "UTDEditor", "GFF-UTD.md", "Bioware-Aurora-DoorPlaceableGFF.md" },
            { "UTEEditor", "GFF-UTE.md", "Bioware-Aurora-Encounter.md" },
            { "UTIEditor", "GFF-UTI.md", "Bioware-Aurora-Item.md" },
            { "UTMEditor", "GFF-UTM.md", "Bioware-Aurora-Merchant.md" },
            { "UTPEditor", "GFF-UTP.md", "Bioware-Aurora-DoorPlaceableGFF.md" },
            { "UTSEditor", "GFF-UTS.md", "Bioware-Aurora-SoundObject.md" },
            { "UTTEditor", "GFF-UTT.md", "Bioware-Aurora-Trigger.md" },
            { "UTWEditor", "GFF-UTW.md", "Bioware-Aurora-Waypoint.md" },
            { "WAVEditor", "WAV-File-Format.md" }, // WAV/Audio file format
            { "SAVEditor", "GFF-File-Format.md" }, // Save game uses general GFF format doc
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
