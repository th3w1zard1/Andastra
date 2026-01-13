using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using static BioWare.NET.Common.GameExtensions;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py
    // Original: construct_utp and dismantle_utp functions
    public static class UTPHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:185-258
        // Original: def construct_utp(gff: GFF) -> UTP:
        // Engine loading functions: swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0
        public static UTP ConstructUtp(GFF gff)
        {
            var utp = new UTP();
            var root = gff.Root;

            // Extract basic fields
            // Engine default: existing value, but "" for new objects (swkotor2.exe:0x00580ed0 line 147, swkotor.exe:0x0058a1f0 line 138)
            utp.Tag = root.Acquire<string>("Tag", "");
            // Engine default: existing value (invalid LocalizedString for new objects) (swkotor2.exe:0x00580ed0 line 188, swkotor.exe:0x0058a1f0 line 179)
            utp.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            // Note: TemplateResRef is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Boolean fields stored as UInt8 - use GetUInt8() != 0 (matching UTW fix)
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 74, swkotor.exe:0x0058a1f0 line 73)
            utp.AutoRemoveKey = root.GetUInt8("AutoRemoveKey") != 0;
            // Engine default: "" (blank ResRef) (swkotor2.exe:0x00580ed0 line 154, swkotor.exe:0x0058a1f0 line 145)
            utp.Conversation = root.Acquire<ResRef>("Conversation", ResRef.FromBlank());
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 79, swkotor.exe:0x0058a1f0 line 79)
            utp.FactionId = root.Acquire<int>("Faction", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 103, swkotor.exe:0x0058a1f0 line 103)
            // Note: Plot is only read if unaff_EBX == 0 in K2, always read in K1
            utp.Plot = root.GetUInt8("Plot") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 114, swkotor.exe:0x0058a1f0 line 114)
            utp.NotBlastable = root.GetUInt8("NotBlastable") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 117, swkotor.exe:0x0058a1f0 line 114)
            utp.Min1Hp = root.GetUInt8("Min1HP") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 126, swkotor.exe:0x0058a1f0 line 123)
            utp.KeyRequired = root.GetUInt8("KeyRequired") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 397, swkotor.exe:0x0058a1f0 line 372)
            utp.Lockable = root.GetUInt8("Lockable") != 0;
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 394, swkotor.exe:0x0058a1f0 line 369)
            utp.Locked = root.GetUInt8("Locked") != 0;
            // Engine default: 0 (swkotor2.exe:0x00580ed0 line 129, swkotor.exe:0x0058a1f0 line 127)
            utp.UnlockDc = root.GetUInt8("OpenLockDC");
            // Engine default: existing value, but "" for new objects (swkotor2.exe:0x00580ed0 line 120, swkotor.exe:0x0058a1f0 line 117)
            utp.KeyName = root.Acquire<string>("KeyName", "");
            // Note: AnimationState is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.AnimationState = root.GetUInt8("AnimationState");
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 46, swkotor.exe:0x0058a1f0 line 45)
            utp.AppearanceId = root.Acquire<int>("Appearance", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 90, swkotor.exe:0x0058a1f0 line 90)
            utp.MaximumHp = root.Acquire<int>("HP", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 93, swkotor.exe:0x0058a1f0 line 93)
            utp.CurrentHp = root.Acquire<int>("CurrentHP", 0);
            // Engine default: 0 (swkotor2.exe:0x00580ed0 line 184, swkotor.exe:0x0058a1f0 line 175)
            utp.Hardness = root.GetUInt8("Hardness");
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 82, swkotor.exe:0x0058a1f0 line 82)
            utp.Fortitude = root.GetUInt8("Fort");
            // Note: HasInventory is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.HasInventory = root.GetUInt8("HasInventory") != 0;
            // Note: PartyInteract is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.PartyInteract = root.GetUInt8("PartyInteract") != 0;
            // Engine default: 0 (swkotor2.exe:0x00580ed0 line 107, swkotor.exe:0x0058a1f0 line 107)
            // Note: Static affects Plot - if Static is 0, Plot is set to 1 (swkotor2.exe:0x00580ed0 line 108-113, swkotor.exe:0x0058a1f0 line 107-113)
            utp.Static = root.GetUInt8("Static") != 0;
            // Note: Useable is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.Useable = root.GetUInt8("Useable") != 0;
            // Note: Comment is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.Comment = root.Acquire<string>("Comment", "");
            // Engine default: existing value, but 0 for new objects (K2 only) (swkotor2.exe:0x00580ed0 line 132, swkotor.exe:0x0058a1f0 - not read)
            utp.UnlockDiff = root.Acquire<int>("OpenLockDiff", 0);
            // Engine default: existing value, but 0 for new objects (K2 only) (swkotor2.exe:0x00580ed0 line 136, swkotor.exe:0x0058a1f0 - not read)
            utp.UnlockDiffMod = root.Acquire<int>("OpenLockDiffMod", 0);
            // Engine default: existing value (invalid LocalizedString for new objects) (swkotor2.exe:0x00580ed0 line 198, swkotor.exe:0x0058a1f0 line 189)
            utp.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 88, swkotor.exe:0x0058a1f0 line 88)
            utp.Reflex = root.Acquire<int>("Ref", 0);
            // Engine default: existing value, but 0 for new objects (swkotor2.exe:0x00580ed0 line 85, swkotor.exe:0x0058a1f0 line 85)
            utp.Will = root.Acquire<int>("Will", 0);

            // Extract script hooks
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 207, swkotor.exe:0x0058a1f0 line 198)
            utp.OnClosed = root.Acquire<ResRef>("OnClosed", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 216, swkotor.exe:0x0058a1f0 line 207)
            utp.OnDamaged = root.Acquire<ResRef>("OnDamaged", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 224, swkotor.exe:0x0058a1f0 line 215)
            utp.OnDeath = root.Acquire<ResRef>("OnDeath", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 242, swkotor.exe:0x0058a1f0 line 233)
            utp.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 251, swkotor.exe:0x0058a1f0 line 242)
            utp.OnLock = root.Acquire<ResRef>("OnLock", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 260, swkotor.exe:0x0058a1f0 line 251)
            utp.OnMelee = root.Acquire<ResRef>("OnMeleeAttacked", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 269, swkotor.exe:0x0058a1f0 line 260)
            utp.OnOpen = root.Acquire<ResRef>("OnOpen", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 278, swkotor.exe:0x0058a1f0 line 269)
            utp.OnPower = root.Acquire<ResRef>("OnSpellCastAt", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 296, swkotor.exe:0x0058a1f0 line 287)
            utp.OnUnlock = root.Acquire<ResRef>("OnUnlock", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 305, swkotor.exe:0x0058a1f0 line 296)
            utp.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (swkotor2.exe:0x00580ed0 line 315, swkotor.exe:0x0058a1f0 line 306)
            utp.OnClick = root.Acquire<ResRef>("OnClick", ResRef.FromBlank());
            // Note: OnEndDialogue is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.OnEndDialog = root.Acquire<ResRef>("OnEndDialogue", ResRef.FromBlank());
            // Note: OnInvDisturbed is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.OnInventory = root.Acquire<ResRef>("OnInvDisturbed", ResRef.FromBlank());
            // Note: OnUsed is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            utp.OnUsed = root.Acquire<ResRef>("OnUsed", ResRef.FromBlank());
            // Engine default: existing value, but "" (blank ResRef) for new objects (K2 only) (swkotor2.exe:0x00580ed0 line 323, swkotor.exe:0x0058a1f0 line 314)
            utp.OnOpenFailed = root.Acquire<ResRef>("OnFailToOpen", ResRef.FromBlank());

            // Extract inventory
            // Note: ItemList is NOT read by the engine (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
            var itemList = root.Acquire<GFFList>("ItemList", new GFFList());
            utp.Inventory.Clear();
            foreach (var itemStruct in itemList)
            {
                // Note: InventoryRes is NOT read by the engine for UTP items (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
                var resref = itemStruct.Acquire<ResRef>("InventoryRes", ResRef.FromBlank());
                // Note: Dropable is NOT read by the engine for UTP items (swkotor2.exe:0x00580ed0, swkotor.exe:0x0058a1f0)
                bool droppable = itemStruct.GetUInt8("Dropable") != 0;
                if (resref != null && !string.IsNullOrEmpty(resref.ToString()))
                {
                    utp.Inventory.Add(new InventoryItem(resref, droppable));
                }
            }

            return utp;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:261-377
        // Original: def dismantle_utp(utp: UTP, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtp(UTP utp, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTP);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", utp.Tag);
            root.SetLocString("LocName", utp.Name);
            root.SetResRef("TemplateResRef", utp.ResRef);
            root.SetUInt8("AutoRemoveKey", utp.AutoRemoveKey ? (byte)1 : (byte)0);
            root.SetResRef("Conversation", utp.Conversation);
            root.SetUInt32("Faction", (uint)utp.FactionId);
            root.SetUInt8("Plot", utp.Plot ? (byte)1 : (byte)0);
            root.SetUInt8("Min1HP", utp.Min1Hp ? (byte)1 : (byte)0);
            root.SetUInt8("KeyRequired", utp.KeyRequired ? (byte)1 : (byte)0);
            root.SetUInt8("Lockable", utp.Lockable ? (byte)1 : (byte)0);
            root.SetUInt8("Locked", utp.Locked ? (byte)1 : (byte)0);
            root.SetUInt8("OpenLockDC", (byte)utp.UnlockDc);
            root.SetString("KeyName", utp.KeyName);
            root.SetUInt8("AnimationState", (byte)utp.AnimationState);
            root.SetUInt32("Appearance", (uint)utp.AppearanceId);
            root.SetInt16("HP", (short)utp.MaximumHp);
            root.SetInt16("CurrentHP", (short)utp.CurrentHp);
            root.SetUInt8("Hardness", (byte)utp.Hardness);
            root.SetUInt8("Fort", (byte)utp.Fortitude);
            root.SetUInt8("HasInventory", utp.HasInventory ? (byte)1 : (byte)0);
            root.SetUInt8("PartyInteract", utp.PartyInteract ? (byte)1 : (byte)0);
            root.SetUInt8("Static", utp.Static ? (byte)1 : (byte)0);
            root.SetUInt8("Useable", utp.Useable ? (byte)1 : (byte)0);
            root.SetString("Comment", utp.Comment);

            // Set script hooks
            root.SetResRef("OnClosed", utp.OnClosed);
            root.SetResRef("OnDamaged", utp.OnDamaged);
            root.SetResRef("OnDeath", utp.OnDeath);
            root.SetResRef("OnHeartbeat", utp.OnHeartbeat);
            root.SetResRef("OnLock", utp.OnLock);
            root.SetResRef("OnMeleeAttacked", utp.OnMelee);
            root.SetResRef("OnOpen", utp.OnOpen);
            root.SetResRef("OnSpellCastAt", utp.OnPower);
            root.SetResRef("OnUnlock", utp.OnUnlock);
            root.SetResRef("OnUserDefined", utp.OnUserDefined);
            root.SetResRef("OnEndDialogue", utp.OnEndDialog);
            root.SetResRef("OnInvDisturbed", utp.OnInventory);
            root.SetResRef("OnUsed", utp.OnUsed);

            // Set inventory
            var itemList = new GFFList();
            root.SetList("ItemList", itemList);
            if (utp.Inventory != null)
            {
                for (int i = 0; i < utp.Inventory.Count; i++)
                {
                    var item = utp.Inventory[i];
                    var itemStruct = itemList.Add(i);
                    itemStruct.SetResRef("InventoryRes", item.ResRef);
                    itemStruct.SetUInt16("Repos_PosX", (ushort)i);
                    itemStruct.SetUInt16("Repos_PosY", 0);
                    if (item.Droppable)
                    {
                        itemStruct.SetUInt8("Dropable", 1);
                    }
                }
            }

            root.SetUInt8("PaletteID", 0);

            // KotOR 2 only fields
            if (game.IsK2())
            {
                root.SetUInt8("NotBlastable", utp.NotBlastable ? (byte)1 : (byte)0);
                root.SetUInt8("OpenLockDiff", (byte)utp.UnlockDiff);
                root.SetInt8("OpenLockDiffMod", (sbyte)utp.UnlockDiffMod);
                root.SetResRef("OnFailToOpen", utp.OnOpenFailed);
            }

            if (useDeprecated)
            {
                root.SetLocString("Description", utp.Description);
                root.SetUInt8("Will", (byte)utp.Will);
                root.SetUInt8("Ref", (byte)utp.Reflex);
            }

            return gff;
        }
    }
}
