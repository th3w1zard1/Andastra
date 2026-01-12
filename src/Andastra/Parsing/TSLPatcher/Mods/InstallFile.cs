using System;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Common.Logger;
using Andastra.Parsing.TSLPatcher.Memory;
using JetBrains.Annotations;

namespace Andastra.Parsing.TSLPatcher.Mods
{

    /// <summary>
    /// Represents a file to be installed/copied during patching.
    /// 1:1 port from Python InstallFile in pykotor/tslpatcher/mods/install.py
    /// </summary>
    public class InstallFile : PatcherModifications
    {
        public InstallFile(
            string filename,
            bool? replaceExisting = null,
            [CanBeNull] string destination = null)
            : base(filename, replaceExisting, destination)
        {
            Action = "Copy ";
            SkipIfNotReplace = true;
        }

        public override object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger, BioWareGame game)
        {
            Apply(source, memory, logger, game);
            // Python: with BinaryReader.from_auto(source) as reader: return reader.read_all()
            return source;
        }

        public override void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger, BioWareGame game)
        {
            // InstallFile doesn't modify the file, just copies it
        }
    }
}
