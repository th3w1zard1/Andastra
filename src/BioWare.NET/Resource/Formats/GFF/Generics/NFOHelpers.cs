using System;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Engine references: swkotor2.exe:0x00707290, swkotor.exe:0x006c8e50 (NFO loading)
    // Engine references: swkotor2.exe:0x004eb750, swkotor.exe:0x004b3110 (NFO serialization)
    internal static class NFOHelpers
    {
        public static NFOData ConstructNfo(GFF gff)
        {
            if (gff == null) throw new ArgumentNullException(nameof(gff));

            GFFStruct root = gff.Root ?? new GFFStruct();
            var nfo = new NFOData();

            // Engine default: "" (swkotor2.exe:0x00707290 line 153, swkotor.exe:0x006c8e50 line 137)
            nfo.AreaName = root.Acquire("AREANAME", string.Empty);

            // Engine default: "" (swkotor2.exe:0x00707290 line 162, swkotor.exe:0x006c8e50 line 146)
            nfo.LastModule = root.Acquire("LASTMODULE", string.Empty);

            // Engine default: "", but if field not found, defaults to "Old Save Game"
            // (swkotor2.exe:0x00707290 lines 173, 181, swkotor.exe:0x006c8e50 lines 157, 165)
            bool savegameNameExists = root.Exists("SAVEGAMENAME");
            nfo.SavegameName = root.Acquire("SAVEGAMENAME", string.Empty);
            if (!savegameNameExists && string.IsNullOrEmpty(nfo.SavegameName))
            {
                nfo.SavegameName = "Old Save Game";
            }

            // Engine default: 0 (swkotor2.exe:0x00707290 line 169, swkotor.exe:0x006c8e50 line 153)
            nfo.TimePlayedSeconds = root.Acquire("TIMEPLAYED", 0);

            // Engine default: 0 if not present (swkotor2.exe:0x00707290 line 205)
            // TIMESTAMP is commonly FILETIME in a 64-bit integer; tolerate both signed/unsigned.
            if (root.Exists("TIMESTAMP"))
            {
                GFFFieldType? type = root.GetFieldType("TIMESTAMP");
                if (type == GFFFieldType.UInt64)
                {
                    nfo.TimestampFileTime = root.GetUInt64("TIMESTAMP");
                }
                else
                {
                    long v = root.GetInt64("TIMESTAMP");
                    nfo.TimestampFileTime = v < 0 ? (ulong?)null : (ulong)v;
                }
            }

            // Engine default: 0 (swkotor2.exe:0x00707290 line 187, swkotor.exe:0x006c8e50 line 171)
            nfo.CheatUsed = root.Acquire("CHEATUSED", (byte)0) != 0;

            // Engine default: Uses existing value in object if field missing, otherwise 0
            // For new objects, default is 0 (swkotor2.exe:0x00707290 line 219, swkotor.exe:0x006c8e50 line 190)
            nfo.GameplayHint = root.Acquire("GAMEPLAYHINT", (byte)0);

            // STORYHINT variants:
            // - Legacy single byte (K1 only, K2 uses indexed STORYHINT0-9)
            // Engine default: 0 (swkotor.exe:0x006c8e50 line 194)
            nfo.StoryHintLegacy = root.Acquire("STORYHINT", (byte)0);

            // - Per-index flags 0..9 (K2 only)
            // Engine default: Uses existing value in object if field missing, otherwise 0
            // For new objects, default is 0 for each index (swkotor2.exe:0x00707290 lines 223-252)
            bool anyIndexed = false;
            for (int i = 0; i < 10; i++)
            {
                string field = "STORYHINT" + i;
                if (!root.Exists(field))
                {
                    continue;
                }

                anyIndexed = true;
                bool hint = root.Acquire(field, (byte)0) != 0;
                nfo.StoryHints[i] = hint;
            }

            // If only legacy story hint exists, keep StoryHints defaulted.
            if (!anyIndexed)
            {
                // Leave list as-is; consumers can choose legacy or indexed.
            }

            // Engine default: "" (swkotor.exe:0x006c8e50 line 236, swkotor2.exe:0x00707290 line 304)
            // Engine reads PORTRAIT fields in loop using format "PORTRAIT%d" (0, 1, 2)
            // (swkotor.exe:0x006c8e50 lines 234-244, swkotor2.exe:0x00707290 lines 302-312)
            nfo.Portrait0 = root.Acquire("PORTRAIT0", ResRef.FromBlank());
            nfo.Portrait1 = root.Acquire("PORTRAIT1", ResRef.FromBlank());
            nfo.Portrait2 = root.Acquire("PORTRAIT2", ResRef.FromBlank());

            // Engine default: 0 (swkotor2.exe:0x00707290 line 253, swkotor.exe:0x006c8e50 line 196)
            nfo.LiveContentBitmask = root.Acquire("LIVECONTENT", (byte)0);

            // Live entries: tolerate 1..9.
            // Engine default: "" for each LIVE field (swkotor.exe:0x006c8e50 line 207, swkotor2.exe:0x00707290 line 264)
            for (int i = 1; i <= 9; i++)
            {
                string field = "LIVE" + i;
                if (root.Exists(field))
                {
                    nfo.LiveEntries[i - 1] = root.Acquire(field, string.Empty);
                }
            }

            // Engine default: "" (swkotor.exe:0x006c8e50 - not explicitly read in K1, swkotor2.exe:0x00707290 line 209)
            nfo.PcName = root.Acquire("PCNAME", string.Empty);

            return nfo;
        }

        public static GFF DismantleNfo(NFOData nfo)
        {
            if (nfo == null) throw new ArgumentNullException(nameof(nfo));

            var gff = new GFF(GFFContent.NFO);
            GFFStruct root = gff.Root;

            root.SetString("AREANAME", nfo.AreaName ?? string.Empty);
            root.SetString("LASTMODULE", nfo.LastModule ?? string.Empty);
            root.SetString("SAVEGAMENAME", nfo.SavegameName ?? string.Empty);
            root.SetUInt32("TIMEPLAYED", (uint)Math.Max(0, nfo.TimePlayedSeconds));

            if (nfo.TimestampFileTime.HasValue)
            {
                root.SetUInt64("TIMESTAMP", nfo.TimestampFileTime.Value);
            }

            root.SetUInt8("CHEATUSED", (byte)(nfo.CheatUsed ? 1 : 0));
            root.SetUInt8("GAMEPLAYHINT", nfo.GameplayHint);

            // Preserve legacy field for tools expecting it.
            root.SetUInt8("STORYHINT", nfo.StoryHintLegacy);

            // Also write indexed story hints if provided.
            if (nfo.StoryHints != null)
            {
                for (int i = 0; i < 10 && i < nfo.StoryHints.Count; i++)
                {
                    root.SetUInt8("STORYHINT" + i, (byte)(nfo.StoryHints[i] ? 1 : 0));
                }
            }

            root.SetResRef("PORTRAIT0", nfo.Portrait0 ?? ResRef.FromBlank());
            root.SetResRef("PORTRAIT1", nfo.Portrait1 ?? ResRef.FromBlank());
            root.SetResRef("PORTRAIT2", nfo.Portrait2 ?? ResRef.FromBlank());

            root.SetUInt8("LIVECONTENT", nfo.LiveContentBitmask);

            if (nfo.LiveEntries != null)
            {
                for (int i = 1; i <= 9 && i - 1 < nfo.LiveEntries.Count; i++)
                {
                    root.SetString("LIVE" + i, nfo.LiveEntries[i - 1] ?? string.Empty);
                }
            }

            if (!string.IsNullOrEmpty(nfo.PcName))
            {
                root.SetString("PCNAME", nfo.PcName);
            }

            return gff;
        }
    }
}


