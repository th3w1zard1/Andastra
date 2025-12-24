using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using JetBrains.Annotations;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py
    public static class Creature
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:22-151
        // Original: def get_body_model(utc: UTC, installation: Installation, *, appearance: 2DA | None = None, baseitems: 2DA | None = None) -> tuple[str | None, str | None]:
        public static (string bodyModel, string bodyTexture) GetBodyModel(
            Andastra.Parsing.Resource.Generics.UTC.UTC utc,
            Installation.Installation installation,
            TwoDA appearance = null,
            TwoDA baseitems = null)
        {
            var log = new RobustLogger();

            // Load appearance.2da if not provided
            if (appearance == null)
            {
                var appearanceLookup = installation.Resources.LookupResource("appearance", ResourceType.TwoDA);
                if (appearanceLookup == null)
                {
                    throw new ArgumentException("appearance.2da missing from installation.");
                }
                var reader = new TwoDABinaryReader(appearanceLookup.Data);
                appearance = reader.Load();
            }

            // Load baseitems.2da if not provided
            if (baseitems == null)
            {
                var baseitemsLookup = installation.Resources.LookupResource("baseitems", ResourceType.TwoDA);
                if (baseitemsLookup == null)
                {
                    throw new ArgumentException("baseitems.2da missing from installation.");
                }
                var reader = new TwoDABinaryReader(baseitemsLookup.Data);
                baseitems = reader.Load();
            }

            // Prepare context for logging and error messages
            string firstName = utc.FirstName?.ToString() ?? "";
            string contextBase = $" for UTC '{firstName}'";

            log.Debug($"Lookup appearance row {utc.AppearanceId} for get_body_model call.");
            var utcAppearanceRow = appearance.GetRow(utc.AppearanceId);
            string bodyModel = null;
            string overrideTexture = null;
            string texColumn = null;
            string texAppend = null;
            string modelColumn = null;

            // Determine body model and texture based on modeltype
            string modeltype = utcAppearanceRow.GetString("modeltype");
            if (modeltype != "B")
            {
                log.Debug($"appearance.2da: utc 'modeltype' is '{modeltype}', fetching 'race' model{contextBase}");
                bodyModel = utcAppearanceRow.GetString("race");
            }
            else
            {
                log.Debug("appearance.2da: utc 'modeltype' is 'B'");

                // Handle armor or default model/texture
                if (!utc.Equipment.ContainsKey(EquipmentSlot.ARMOR) || utc.Equipment[EquipmentSlot.ARMOR].ResRef == null || string.IsNullOrEmpty(utc.Equipment[EquipmentSlot.ARMOR].ResRef.ToString()))
                {
                    modelColumn = "modela";
                    bodyModel = utcAppearanceRow.GetString(modelColumn);
                    texColumn = utc.Alignment <= 25 ? "texaevil" : "texa";
                    texAppend = "01";
                    overrideTexture = utcAppearanceRow.GetString(texColumn);
                }
                else
                {
                    // Handle armor-specific model and texture
                    var armorResref = utc.Equipment[EquipmentSlot.ARMOR].ResRef;
                    log.Debug($"utc is wearing armor, fetch '{armorResref}.uti'");

                    // Attempt to load armor UTI
                    var armorResLookup = installation.Resources.LookupResource(armorResref.ToString(), ResourceType.UTI);
                    if (armorResLookup == null)
                    {
                        log.Error($"'{armorResref}.uti' missing from installation{contextBase}");
                        // Fallback to default values if armor UTI is missing
                        modelColumn = "modela";
                        bodyModel = utcAppearanceRow.GetString(modelColumn);
                        texColumn = utc.Alignment <= 25 ? "texaevil" : "texa";
                        texAppend = "01";
                        overrideTexture = utcAppearanceRow.GetString(texColumn);
                    }
                    else
                    {
                        // Process armor-specific model and texture
                        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:22-151
                        // Original: armor_uti = read_uti(armor_res_lookup.data)
                        var armorUti = ResourceAutoHelpers.ReadUti(armorResLookup.Data);
                        log.Debug($"baseitems.2da: get body row {armorUti.BaseItem} for their armor");

                        var bodyRow = baseitems.GetRow(armorUti.BaseItem);
                        string bodyCell = bodyRow.GetString("bodyvar");
                        log.Debug($"baseitems.2da: 'bodyvar' cell: {bodyCell}");

                        // Determine model and texture columns
                        string armorVariation = bodyCell.ToLowerInvariant();
                        modelColumn = $"model{armorVariation}";
                        string evilTexColumn = $"tex{armorVariation}evil";
                        texColumn = (utc.Alignment <= 25 && appearance.GetHeaders().Contains(evilTexColumn))
                            ? evilTexColumn
                            : $"tex{armorVariation}";
                        texAppend = armorUti.TextureVariation.ToString().PadLeft(2, '0');

                        bodyModel = utcAppearanceRow.GetString(modelColumn);
                        overrideTexture = utcAppearanceRow.GetString(texColumn);
                    }
                }

                log.Debug($"appearance.2da's texture column: '{texColumn}'");
                log.Debug($"override_texture name: '{overrideTexture}'");

                // Process override texture
                if (!string.IsNullOrWhiteSpace(overrideTexture) && overrideTexture != "****")
                {
                    string fallbackOverrideTexture = overrideTexture + texAppend;
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:22-151
                    // Original: if tex_append != "01" and installation.texture(fallback_override_texture) is None:
                    if (texAppend != "01" && installation.Texture(fallbackOverrideTexture) == null)
                    {
                        fallbackOverrideTexture = $"{overrideTexture}01";
                        log.Debug($"override texture '{fallbackOverrideTexture}' not found, appending '01' to the end like the game itself would do.");
                    }
                    overrideTexture = fallbackOverrideTexture;
                }
                else
                {
                    overrideTexture = null;
                }
                log.Debug($"Final override texture name (from appearance.2da's '{texColumn}' column): '{overrideTexture}'");
                log.Debug($"Final body model name (from appearance.2da's '{modelColumn}' column): '{bodyModel}'");
            }

            // Fallback to 'race' column if body_model is empty or invalid
            if (string.IsNullOrWhiteSpace(bodyModel) || bodyModel == "****")
            {
                bodyModel = utcAppearanceRow.GetString("race");
                log.Debug($"body model name (from appearance.2da's 'race' column): '{bodyModel}'");
            }

            string normalizedModel = !string.IsNullOrWhiteSpace(bodyModel) ? bodyModel.Trim() : null;
            string normalizedTexture = !string.IsNullOrWhiteSpace(overrideTexture) ? overrideTexture.Trim() : null;
            return (normalizedModel, normalizedTexture);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:154-194
        // Original: def get_weapon_models(utc: UTC, installation: Installation, *, appearance: 2DA | None = None, baseitems: 2DA | None = None) -> tuple[str | None, str | None]:
        public static (string rightHandModel, string leftHandModel) GetWeaponModels(
            Andastra.Parsing.Resource.Generics.UTC.UTC utc,
            Installation.Installation installation,
            TwoDA appearance = null,
            TwoDA baseitems = null)
        {
            if (appearance == null)
            {
                var appearanceLookup = installation.Resources.LookupResource("appearance", ResourceType.TwoDA);
                if (appearanceLookup == null)
                {
                    new RobustLogger().Error("appearance.2da missing from installation.");
                    return (null, null);
                }
                var reader = new TwoDABinaryReader(appearanceLookup.Data);
                appearance = reader.Load();
            }
            if (baseitems == null)
            {
                var baseitemsLookup = installation.Resources.LookupResource("baseitems", ResourceType.TwoDA);
                if (baseitemsLookup == null)
                {
                    new RobustLogger().Error("baseitems.2da missing from installation.");
                    return (null, null);
                }
                var reader = new TwoDABinaryReader(baseitemsLookup.Data);
                baseitems = reader.Load();
            }

            string rightHandModel = null;
            if (utc.Equipment.ContainsKey(EquipmentSlot.RIGHT_HAND) && utc.Equipment[EquipmentSlot.RIGHT_HAND].ResRef != null && !string.IsNullOrEmpty(utc.Equipment[EquipmentSlot.RIGHT_HAND].ResRef.ToString()))
            {
                rightHandModel = LoadHandUti(installation, utc.Equipment[EquipmentSlot.RIGHT_HAND].ResRef.ToString(), baseitems);
            }

            string leftHandModel = null;
            if (utc.Equipment.ContainsKey(EquipmentSlot.LEFT_HAND) && utc.Equipment[EquipmentSlot.LEFT_HAND].ResRef != null && !string.IsNullOrEmpty(utc.Equipment[EquipmentSlot.LEFT_HAND].ResRef.ToString()))
            {
                leftHandModel = LoadHandUti(installation, utc.Equipment[EquipmentSlot.LEFT_HAND].ResRef.ToString(), baseitems);
            }

            return (rightHandModel, leftHandModel);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:197-211
        // Original: def _load_hand_uti(installation: Installation, hand_resref: str, baseitems: 2DA) -> str | None:
        private static string LoadHandUti(
            Installation.Installation installation,
            string handResref,
            TwoDA baseitems)
        {
            var handLookup = installation.Resources.LookupResource(handResref, ResourceType.UTI);
            if (handLookup == null)
            {
                new RobustLogger().Error($"{handResref}.uti missing from installation.");
                return null;
            }
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:153-212
            // Original: hand_uti = read_uti(hand_lookup.data)
            var handUti = ResourceAutoHelpers.ReadUti(handLookup.Data);
            string defaultModel = baseitems.GetRow(handUti.BaseItem).GetString("defaultmodel");
            return defaultModel.Replace("001", handUti.ModelVariation.ToString().PadLeft(3, '0')).Trim();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:214-289
        // Original: def get_head_model(utc: UTC, installation: Installation, *, appearance: 2DA | None = None, heads: 2DA | None = None) -> tuple[str | None, str | None]:
        public static (string model, string texture) GetHeadModel(
            Andastra.Parsing.Resource.Generics.UTC.UTC utc,
            Installation.Installation installation,
            TwoDA appearance = null,
            TwoDA heads = null)
        {
            if (appearance == null)
            {
                var appearanceLookup = installation.Resources.LookupResource("appearance", ResourceType.TwoDA);
                if (appearanceLookup == null)
                {
                    new RobustLogger().Error("appearance.2da missing from installation.");
                    return (null, null);
                }
                var reader = new TwoDABinaryReader(appearanceLookup.Data);
                appearance = reader.Load();
            }
            if (heads == null)
            {
                var headsLookup = installation.Resources.LookupResource("heads", ResourceType.TwoDA);
                if (headsLookup == null)
                {
                    new RobustLogger().Error("heads.2da missing from installation.");
                    return (null, null);
                }
                var reader = new TwoDABinaryReader(headsLookup.Data);
                heads = reader.Load();
            }

            string model = null;
            string texture = null;

            int? headId = appearance.GetRow(utc.AppearanceId).GetInteger("normalhead");
            if (headId.HasValue)
            {
                try
                {
                    var headRow = heads.GetRow(headId.Value);
                    model = headRow.GetString("head");
                    string headColumnName = null;
                    if (utc.Alignment < 10)
                    {
                        headColumnName = "headtexvvve";
                    }
                    else if (utc.Alignment < 20)
                    {
                        headColumnName = "headtexvve";
                    }
                    else if (utc.Alignment < 30)
                    {
                        headColumnName = "headtexve";
                    }
                    else if (utc.Alignment < 40)
                    {
                        headColumnName = "headtexe";
                    }
                    else if (heads.GetHeaders().Contains("alttexture"))
                    {
                        if (!installation.Game.IsK2())
                        {
                            new RobustLogger().Error("'alttexture' column in heads.2da should never exist in a K1 installation.");
                        }
                        else
                        {
                            headColumnName = "alttexture";
                        }
                    }
                    if (headColumnName != null)
                    {
                        try
                        {
                            texture = headRow.GetString(headColumnName);
                            texture = !string.IsNullOrWhiteSpace(texture) ? texture.Trim() : null;
                        }
                        catch (KeyNotFoundException)
                        {
                            new RobustLogger().Error($"Cannot find {headColumnName} in heads.2da");
                        }
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    new RobustLogger().Error($"Row {headId} missing from heads.2da, defined in appearance.2da under the column 'normalhead' row {utc.AppearanceId}");
                }
            }

            return (model, texture);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:292-309
        // Original: def get_mask_model(utc: UTC, installation: Installation) -> str | None:
        public static string GetMaskModel(
            Andastra.Parsing.Resource.Generics.UTC.UTC utc,
            Installation.Installation installation)
        {
            string model = null;

            if (utc.Equipment.ContainsKey(EquipmentSlot.HEAD) && utc.Equipment[EquipmentSlot.HEAD] != null)
            {
                var headEquip = utc.Equipment[EquipmentSlot.HEAD];
                var resref = headEquip.ResRef;
                if (resref != null && !string.IsNullOrEmpty(resref.ToString()))
                {
                    var resource = installation.Resources.LookupResource(resref.ToString(), ResourceType.UTI);
                    if (resource != null)
                    {
                        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/creature.py:291-352
                        // Original: uti = read_uti(resource.data)
                        var uti = ResourceAutoHelpers.ReadUti(resource.Data);
                        model = "I_Mask_" + uti.ModelVariation.ToString().PadLeft(3, '0');
                        return model;
                    }
                }
            }

            return !string.IsNullOrWhiteSpace(model) ? model.Trim() : null;
        }
    }
}
