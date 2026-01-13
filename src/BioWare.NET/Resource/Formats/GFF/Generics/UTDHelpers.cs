using System;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using static BioWare.NET.Common.GameExtensions;
using BioWare.NET.Resource.Formats.GFF;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py
    // Original: construct_utd and dismantle_utd functions
    // Engine references: swkotor2.exe:0x00588010, swkotor.exe:0x00585670
    // These functions load UTD template data from GFF files. Most fields use existing object values as defaults
    // (for new objects, these would be 0, empty string, or blank ResRef).
    public static class UTDHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:498-560
        // Original: def construct_utd(gff: GFF) -> UTD:
        // Engine references: swkotor2.exe:0x00588010, swkotor.exe:0x00585670
        public static UTD ConstructUtd(GFF gff)
        {
            var utd = new UTD();
            var root = gff.Root;

            // Extract basic fields
            // Engine default: existing value (swkotor2.exe:0x00588010 line 56, swkotor.exe:0x00585670 line 56)
            utd.Tag = root.Acquire<string>("Tag", "");
            // Engine default: existing value (swkotor2.exe:0x00588010 line 68, swkotor.exe:0x00585670 line 68)
            utd.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            // Engine default: existing value (swkotor2.exe:0x00588010 line 64, swkotor.exe:0x00585670 line 64)
            utd.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // AutoRemoveKey, Plot, Min1HP, KeyRequired, Lockable, Locked, Static, NotBlastable are stored as UInt8 (boolean flags)
            // Engine default: 0 (swkotor2.exe:0x00588010 line 75, swkotor.exe:0x00585670 line 75)
            byte? autoRemoveKeyNullable = root.GetUInt8("AutoRemoveKey");
            utd.AutoRemoveKey = (autoRemoveKeyNullable ?? 0) != 0;
            // Engine default: existing value (swkotor2.exe:0x00588010 line 180, swkotor.exe:0x00585670 line 167)
            utd.Conversation = root.Acquire<ResRef>("Conversation", ResRef.FromBlank());
            // Faction is stored as UInt32, so we need to read it as uint, not int
            // Engine default: 0 (swkotor2.exe:0x00588010 line 77, swkotor.exe:0x00585670 line 77)
            uint? factionNullable = root.GetUInt32("Faction");
            utd.FactionId = factionNullable.HasValue ? (int)factionNullable.Value : 0;
            // Engine default: fallback from Invulnerable if Plot missing, then existing value (swkotor2.exe:0x00588010 line 79-84, swkotor.exe:0x00585670 line 79-84)
            byte? plotNullable = root.GetUInt8("Plot");
            utd.Plot = (plotNullable ?? 0) != 0;
            // Engine default: existing value (swkotor2.exe:0x00588010 line 89, swkotor.exe:0x00585670 line 86)
            byte? min1HpNullable = root.GetUInt8("Min1HP");
            utd.Min1Hp = (min1HpNullable ?? 0) != 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 165, swkotor.exe:0x00585670 line 152)
            byte? keyRequiredNullable = root.GetUInt8("KeyRequired");
            utd.KeyRequired = (keyRequiredNullable ?? 0) != 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 159, swkotor.exe:0x00585670 line 146)
            byte? lockableNullable = root.GetUInt8("Lockable");
            utd.Lockable = (lockableNullable ?? 0) != 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 161, swkotor.exe:0x00585670 line 148)
            byte? lockedNullable = root.GetUInt8("Locked");
            utd.Locked = (lockedNullable ?? 0) != 0;
            // OpenLockDC is stored as UInt8, so we need to read it as byte, not int
            // Engine default: 0 (swkotor2.exe:0x00588010 line 93, swkotor.exe:0x00585670 line 90)
            byte? unlockDcNullable = root.GetUInt8("OpenLockDC");
            utd.UnlockDc = unlockDcNullable ?? 0;
            // Engine default: existing value (swkotor2.exe:0x00588010 line 101, swkotor.exe:0x00585670 line 94)
            utd.KeyName = root.Acquire<string>("KeyName", "");
            // AnimationState is stored as UInt8, so we need to read it as byte, not int
            // Engine default: 0 (swkotor2.exe:0x00588010 line 338, swkotor.exe:0x00585670 line 315)
            byte? animationStateNullable = root.GetUInt8("AnimationState");
            utd.AnimationState = animationStateNullable ?? 0;
            // HP and CurrentHP are stored as Int16, so we need to read them as short, not int
            // Engine default: 0 (swkotor2.exe:0x00588010 line 147, swkotor.exe:0x00585670 line 134)
            short? maximumHpNullable = root.GetInt16("HP");
            utd.MaximumHp = maximumHpNullable ?? 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 149, swkotor.exe:0x00585670 line 136)
            short? currentHpNullable = root.GetInt16("CurrentHP");
            utd.CurrentHp = currentHpNullable ?? 0;
            // Hardness, Fort, GenericType are stored as UInt8, so we need to read them as byte, not int
            // Engine default: 0 (swkotor2.exe:0x00588010 line 151, swkotor.exe:0x00585670 line 138)
            byte? hardnessNullable = root.GetUInt8("Hardness");
            utd.Hardness = hardnessNullable ?? 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 153, swkotor.exe:0x00585670 line 140)
            byte? fortNullable = root.GetUInt8("Fort");
            utd.Fortitude = fortNullable ?? 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 141, swkotor.exe:0x00585670 line 132)
            // Note: Engine reads "Appearance" field, but we use "GenericType" for compatibility
            byte? genericTypeNullable = root.GetUInt8("GenericType");
            utd.AppearanceId = genericTypeNullable ?? 0;
            // Engine default: 0, but has special logic - if missing and Useable=1, Static=0; if Useable=0, Static=1 (swkotor2.exe:0x00588010 line 127-135, swkotor.exe:0x00585670 line 118-126)
            byte? staticNullable = root.GetUInt8("Static");
            utd.Static = (staticNullable ?? 0) != 0;
            // Engine default: existing value (blank ResRef for new objects)
            utd.OnClick = root.Acquire<ResRef>("OnClick", ResRef.FromBlank());
            // Engine default: existing value (swkotor2.exe:0x00588010 line 319, K2 only)
            utd.OnOpenFailed = root.Acquire<ResRef>("OnFailToOpen", ResRef.FromBlank());
            // Engine default: Comment field is NOT read by the engine (not present in loading functions)
            utd.Comment = root.Acquire<string>("Comment", "");
            // OpenLockDiff and OpenState are stored as UInt8 (K2 only), so we need to read them as byte, not int
            // Engine default: existing value (swkotor2.exe:0x00588010 line 95, K2 only)
            byte? unlockDiffNullable = root.GetUInt8("OpenLockDiff");
            utd.UnlockDiff = unlockDiffNullable ?? 0;
            // OpenLockDiffMod is stored as Int8 (K2 only), so we need to read it as sbyte, not int
            // Engine default: existing value (swkotor2.exe:0x00588010 line 97, K2 only)
            sbyte? unlockDiffModNullable = root.GetInt8("OpenLockDiffMod");
            utd.UnlockDiffMod = unlockDiffModNullable ?? 0;
            // Engine default: existing value (swkotor2.exe:0x00588010 line 197, swkotor.exe:0x00585670 line 184)
            utd.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            // Ref and Will are stored as UInt8 (deprecated), so we need to read them as byte, not int
            // Engine default: 0 (swkotor2.exe:0x00588010 line 157, swkotor.exe:0x00585670 line 144)
            byte? reflexNullable = root.GetUInt8("Ref");
            utd.Reflex = reflexNullable ?? 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 155, swkotor.exe:0x00585670 line 142)
            byte? willpowerNullable = root.GetUInt8("Will");
            utd.Willpower = willpowerNullable ?? 0;
            // Engine default: existing value, but if object is new (0x358==0), defaults to 3 (swkotor2.exe:0x00580ed0 line 52-56, K2 only)
            byte? openStateNullable = root.GetUInt8("OpenState");
            utd.OpenState = openStateNullable ?? 0;
            // Engine default: existing value (swkotor2.exe:0x00588010 line 86, K2 only)
            byte? notBlastableNullable = root.GetUInt8("NotBlastable");
            utd.NotBlastable = (notBlastableNullable ?? 0) != 0;

            // Extract trap properties (deprecated, toolset only)
            // Engine default: 0 (swkotor2.exe:0x00588010 line 110, swkotor.exe:0x00585670 line 103)
            byte? trapDetectableNullable = root.GetUInt8("TrapDetectable");
            utd.TrapDetectable = (trapDetectableNullable ?? 0) != 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 108, swkotor.exe:0x00585670 line 101)
            byte? trapDisarmableNullable = root.GetUInt8("TrapDisarmable");
            utd.TrapDisarmable = (trapDisarmableNullable ?? 0) != 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 112, swkotor.exe:0x00585670 line 105)
            byte? disarmDcNullable = root.GetUInt8("DisarmDC");
            utd.DisarmDc = disarmDcNullable ?? 0;
            // Engine default: 0 (swkotor2.exe:0x00588010 line 120, swkotor.exe:0x00585670 line 111)
            byte? trapOneShotNullable = root.GetUInt8("TrapOneShot");
            utd.TrapOneShot = (trapOneShotNullable ?? 0) != 0;
            // Engine default: existing value (swkotor2.exe:0x00588010 line 123, swkotor.exe:0x00585670 line 114)
            byte? trapTypeNullable = root.GetUInt8("TrapType");
            utd.TrapType = trapTypeNullable ?? 0;
            // Engine default: PaletteID field is NOT read by the engine (not present in loading functions)
            byte? paletteIdNullable = root.GetUInt8("PaletteID");
            utd.PaletteId = paletteIdNullable ?? 0;

            // Extract script hooks
            // Engine default: existing value (blank ResRef for new objects) - all script hooks use same pattern
            // (swkotor2.exe:0x00588010 lines 207-300, swkotor.exe:0x00585670 lines 194-287)
            utd.OnClosed = root.Acquire<ResRef>("OnClosed", ResRef.FromBlank());
            utd.OnDamaged = root.Acquire<ResRef>("OnDamaged", ResRef.FromBlank());
            utd.OnDeath = root.Acquire<ResRef>("OnDeath", ResRef.FromBlank());
            utd.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            utd.OnLock = root.Acquire<ResRef>("OnLock", ResRef.FromBlank());
            utd.OnMelee = root.Acquire<ResRef>("OnMeleeAttacked", ResRef.FromBlank());
            utd.OnOpen = root.Acquire<ResRef>("OnOpen", ResRef.FromBlank());
            utd.OnUnlock = root.Acquire<ResRef>("OnUnlock", ResRef.FromBlank());
            utd.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            utd.OnPower = root.Acquire<ResRef>("OnSpellCastAt", ResRef.FromBlank());

            return utd;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:563-665
        // Original: def dismantle_utd(utd: UTD, game: Game = BioWareGame.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtd(UTD utd, BioWareGame game = BioWareGame.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTD);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", utd.Tag);
            root.SetLocString("LocName", utd.Name);
            root.SetResRef("TemplateResRef", utd.ResRef);
            root.SetUInt8("AutoRemoveKey", utd.AutoRemoveKey ? (byte)1 : (byte)0);
            root.SetResRef("Conversation", utd.Conversation);
            root.SetUInt32("Faction", (uint)utd.FactionId);
            root.SetUInt8("Plot", utd.Plot ? (byte)1 : (byte)0);
            root.SetUInt8("Min1HP", utd.Min1Hp ? (byte)1 : (byte)0);
            root.SetUInt8("KeyRequired", utd.KeyRequired ? (byte)1 : (byte)0);
            root.SetUInt8("Lockable", utd.Lockable ? (byte)1 : (byte)0);
            root.SetUInt8("Locked", utd.Locked ? (byte)1 : (byte)0);
            root.SetUInt8("OpenLockDC", (byte)utd.UnlockDc);
            root.SetString("KeyName", utd.KeyName);
            root.SetUInt8("AnimationState", (byte)utd.AnimationState);
            root.SetInt16("HP", (short)utd.MaximumHp);
            root.SetInt16("CurrentHP", (short)utd.CurrentHp);
            root.SetUInt8("Hardness", (byte)utd.Hardness);
            root.SetUInt8("Fort", (byte)utd.Fortitude);
            root.SetUInt8("GenericType", (byte)utd.AppearanceId);
            root.SetUInt8("Static", utd.Static ? (byte)1 : (byte)0);
            root.SetResRef("OnClick", utd.OnClick);
            root.SetResRef("OnFailToOpen", utd.OnOpenFailed);
            root.SetString("Comment", utd.Comment);

            // KotOR 2 only fields
            // Write OpenLockDiff if it has a non-zero value (for roundtrip compatibility)
            // or if game is K2 (matching Python behavior)
            if (game.IsK2() || utd.UnlockDiff != 0)
            {
                root.SetUInt8("OpenLockDiff", (byte)utd.UnlockDiff);
            }
            if (game.IsK2() || utd.UnlockDiffMod != 0)
            {
                root.SetInt8("OpenLockDiffMod", (sbyte)utd.UnlockDiffMod);
            }
            if (game.IsK2())
            {
                root.SetUInt8("OpenState", (byte)utd.OpenState);
                root.SetUInt8("NotBlastable", utd.NotBlastable ? (byte)1 : (byte)0);
            }

            if (useDeprecated)
            {
                root.SetLocString("Description", utd.Description);
                root.SetUInt8("Ref", (byte)utd.Reflex);
                root.SetUInt8("Will", (byte)utd.Willpower);
                root.SetUInt8("TrapDetectable", utd.TrapDetectable ? (byte)1 : (byte)0);
                root.SetUInt8("TrapDisarmable", utd.TrapDisarmable ? (byte)1 : (byte)0);
                root.SetUInt8("DisarmDC", (byte)utd.DisarmDc);
                root.SetUInt8("TrapOneShot", utd.TrapOneShot ? (byte)1 : (byte)0);
                root.SetUInt8("TrapType", (byte)utd.TrapType);
                root.SetUInt8("PaletteID", (byte)utd.PaletteId);
            }

            // Set script hooks
            root.SetResRef("OnClosed", utd.OnClosed);
            root.SetResRef("OnDamaged", utd.OnDamaged);
            root.SetResRef("OnDeath", utd.OnDeath);
            root.SetResRef("OnHeartbeat", utd.OnHeartbeat);
            root.SetResRef("OnLock", utd.OnLock);
            root.SetResRef("OnMeleeAttacked", utd.OnMelee);
            root.SetResRef("OnOpen", utd.OnOpen);
            root.SetResRef("OnUnlock", utd.OnUnlock);
            root.SetResRef("OnUserDefined", utd.OnUserDefined);
            root.SetResRef("OnSpellCastAt", utd.OnPower);

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:635-641
        // Original: def read_utd(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> UTD:
        public static UTD ReadUtd(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructUtd(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:656-664
        // Original: def bytes_utd(utd: UTD, game: Game = BioWareGame.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesUtd(UTD utd, BioWareGame game = BioWareGame.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.UTD;
            }
            GFF gff = DismantleUtd(utd, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}
