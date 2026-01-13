using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using static BioWare.NET.Common.GameExtensions;
using BioWare.NET.Resource.Formats.GFF;

namespace BioWare.NET.Resource.Formats.GFF.Generics.UTM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py
    // Original: construct_utm and dismantle_utm functions
    public static class UTMHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:130-160
        // Original: def construct_utm(gff: GFF) -> UTM:
        // Engine loading functions: [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)
        public static UTM ConstructUtm(GFF gff)
        {
            var utm = new UTM();
            var root = gff.Root;

            // Extract basic fields
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:136-145
            // Note: ResRef and Comment are NOT read by the engine [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)
            utm.ResRef = root.Acquire<ResRef>("ResRef", ResRef.FromBlank());
            // Engine default: "" (invalid LocalizedString for new objects) [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 50
            utm.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            // Engine default: "" [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 41
            utm.Tag = root.Acquire<string>("Tag", "");
            // Engine default: 0 [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 59
            utm.MarkUp = root.Acquire<int>("MarkUp", 0);
            // Engine default: 0 [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 57
            utm.MarkDown = root.Acquire<int>("MarkDown", 0);
            // Engine default: "" (blank ResRef) [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 65
            utm.OnOpenScript = root.Acquire<ResRef>("OnOpenStore", ResRef.FromBlank());
            // Note: Comment is NOT read by the engine
            utm.Comment = root.Acquire<string>("Comment", "");
            // ID is stored as UInt8, so we need to read it as byte, not int
            // Note: ID is NOT read by the engine [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)
            byte? idNullable = root.GetUInt8("ID");
            utm.Id = idNullable ?? 0;  // [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)

            // Extract BuySellFlag
            // Matching PyKotor implementation: utm.can_buy = root.acquire("BuySellFlag", 0) & 1 != 0
            // Matching PyKotor implementation: utm.can_sell = root.acquire("BuySellFlag", 0) & 2 != 0
            // BuySellFlag is stored as UInt8, so we need to read it as byte, not int
            // Engine default: existing value, but 0 for new objects [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 68
            byte? buySellFlagNullable = root.GetUInt8("BuySellFlag");
            byte buySellFlag = buySellFlagNullable ?? (byte)0;
            utm.CanBuy = (buySellFlag & 1) != 0;
            utm.CanSell = (buySellFlag & 2) != 0;

            // Extract store properties (StoreGold, IdentifyPrice, MaxBuyPrice)
            // Based on Bioware Aurora Engine Store Format documentation - these fields are in all Store Structs
            // [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)
            // - Line 63: StoreGold = ReadFieldINT("StoreGold", -1) - default -1 (unlimited)
            utm.StoreGold = root.Acquire<int>("StoreGold", -1);
            // - Line 65: IdentifyPrice = ReadFieldINT("IdentifyPrice", 100) - default 100
            utm.IdentifyPrice = root.Acquire<int>("IdentifyPrice", 100);
            // - Line 67: MaxBuyPrice = ReadFieldINT("MaxBuyPrice", -1) - default -1 (no limit)
            utm.MaxBuyPrice = root.Acquire<int>("MaxBuyPrice", -1);

            // Extract inventory
            // Matching PyKotor implementation: item_list: GFFList = root.acquire("ItemList", GFFList())
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:147-154
            // Engine default: empty list (engine skips if missing) [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 74
            var itemList = root.Acquire<GFFList>("ItemList", new GFFList());
            utm.Items.Clear();
            foreach (var itemStruct in itemList)
            {
                var item = new UTMItem();
                // Matching PyKotor implementation: item.resref = item_struct.acquire("InventoryRes", ResRef.from_blank())
                // Engine default: "" (blank ResRef) [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 106
                item.ResRef = itemStruct.Acquire<ResRef>("InventoryRes", ResRef.FromBlank());
                // Matching PyKotor implementation: item.infinite = bool(item_struct.acquire("Infinite", 0))
                // Engine default: 0 [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0) line 113
                item.Infinite = itemStruct.Acquire<int>("Infinite", 0) != 0 ? 1 : 0;
                // Matching PyKotor implementation: item.droppable = bool(item_struct.acquire("Dropable", 0))
                // Note: Dropable is NOT read by the engine for UTM items [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)
                item.Droppable = itemStruct.Acquire<int>("Dropable", 0) != 0 ? 1 : 0;
                utm.Items.Add(item);
            }

            return utm;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:163-223
        // Original: def dismantle_utm(utm: UTM, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtm(UTM utm, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTM);
            var root = gff.Root;

            // Set basic fields
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:167-174
            root.SetResRef("ResRef", utm.ResRef);
            root.SetLocString("LocName", utm.Name);
            root.SetString("Tag", utm.Tag);
            root.SetInt32("MarkUp", utm.MarkUp);
            root.SetInt32("MarkDown", utm.MarkDown);
            root.SetResRef("OnOpenStore", utm.OnOpenScript);
            root.SetString("Comment", utm.Comment);

            // Set BuySellFlag (can_buy = bit 0, can_sell = bit 1)
            // Matching PyKotor implementation: root.set_uint8("BuySellFlag", utm.can_buy + utm.can_sell * 2)
            int buySellFlag = (utm.CanBuy ? 1 : 0) + (utm.CanSell ? 2 : 0);
            root.SetUInt8("BuySellFlag", (byte)buySellFlag);

            // Set store properties (StoreGold, IdentifyPrice, MaxBuyPrice)
            // Based on Bioware Aurora Engine Store Format documentation - these fields are in all Store Structs
            // [CNWSStore::LoadStore] @ (K1: 0x005c7180, TSL: 0x00571310, NWN:EE: 0x1404fbbf0)
            root.SetInt32("StoreGold", utm.StoreGold);
            root.SetInt32("IdentifyPrice", utm.IdentifyPrice);
            root.SetInt32("MaxBuyPrice", utm.MaxBuyPrice);

            // Set deprecated ID field if useDeprecated is true
            // Matching PyKotor implementation: if use_deprecated: root.set_uint8("ID", utm.id)
            if (useDeprecated)
            {
                root.SetUInt8("ID", (byte)utm.Id);
            }

            // Set inventory
            // Matching PyKotor implementation: item_list: GFFList = root.set_list("ItemList", GFFList())
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:176-185
            var itemList = new GFFList();
            for (int i = 0; i < utm.Items.Count; i++)
            {
                var item = utm.Items[i];
                var itemStruct = itemList.Add(i);
                itemStruct.SetResRef("InventoryRes", item.ResRef);
                // Matching PyKotor implementation: item_struct.set_uint16("Repos_PosX", i)
                itemStruct.SetUInt16("Repos_PosX", (ushort)i);
                itemStruct.SetUInt16("Repos_PosY", 0);
                if (item.Droppable != 0)
                {
                    itemStruct.SetUInt8("Dropable", (byte)(item.Droppable != 0 ? 1 : 0));
                }
                if (item.Infinite != 0)
                {
                    itemStruct.SetUInt8("Infinite", (byte)(item.Infinite != 0 ? 1 : 0));
                }
            }
            root.SetList("ItemList", itemList);

            return gff;
        }

        // Matching pattern from DLGHelper.ReadDlg
        // Original: def read_utm(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> UTM:
        public static UTM ReadUtm(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructUtm(gff);
        }

        // Matching pattern from DLGHelper.BytesDlg
        // Original: def bytes_utm(utm: UTM, game: Game = BioWareGame.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True) -> bytes:
        public static byte[] BytesUtm(UTM utm, BioWareGame game = BioWareGame.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.UTM;
            }
            GFF gff = DismantleUtm(utm, game, useDeprecated);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}

