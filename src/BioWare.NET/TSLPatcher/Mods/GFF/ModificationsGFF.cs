using System;
using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.TSLPatcher.Logger;
using BioWare.NET.TSLPatcher.Memory;
using BioWare.NET.TSLPatcher.Mods;
using JetBrains.Annotations;

namespace BioWare.NET.TSLPatcher.Mods.GFF
{

    /// <summary>
    /// Container for GFF file modifications.
    /// 1:1 port from Python ModificationsGFF in pykotor/tslpatcher/mods/gff.py
    /// </summary>
    public class ModificationsGFF : PatcherModifications
    {
        public new const string DEFAULT_DESTINATION = "Override";
        public static string DefaultDestination => DEFAULT_DESTINATION;

        public List<ModifyGFF> Modifiers { get; set; } = new List<ModifyGFF>();

        public ModificationsGFF(
            string filename,
            bool replace = false,
            [CanBeNull] List<ModifyGFF> modifiers = null)
            : base(filename, replace)
        {
            // Python: self.modifiers: list[ModifyGFF] = [] if modifiers is None else modifiers
            Modifiers = modifiers ?? new List<ModifyGFF>();
        }

        public override object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger, BioWareGame game)
        {
            var reader = new GFFBinaryReader(source);
            Resource.Formats.GFF.GFF gff = reader.Load();
            Apply(gff, memory, logger, game);
            var writer = new GFFBinaryWriter(gff);
            return writer.Write();
        }

        public override void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger, BioWareGame game)
        {
            // Python: for change_field in self.modifiers: change_field.apply(mutable_data.root, memory, logger)
            if (mutableData is Resource.Formats.GFF.GFF gff)
            {
                foreach (ModifyGFF changeField in Modifiers)
                {
                    changeField.Apply(gff.Root, memory, logger);
                }
            }
            else
            {
                logger.AddError($"Expected GFF object for ModificationsGFF, but got {mutableData.GetType().Name}");
            }
        }
    }
}
