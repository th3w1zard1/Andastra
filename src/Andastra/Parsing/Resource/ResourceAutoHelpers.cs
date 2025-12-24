using System;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.ARE;

namespace Andastra.Parsing.Resource
{
    // Helper functions for reading GFF-based resources
    // These will be used by ModuleResource<T>.Resource() to load resources
    public static class ResourceAutoHelpers
    {
        public static ARE ReadAre(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return AREHelpers.ConstructAre(gff);
        }

        public static GIT ReadGit(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return GITHelpers.ConstructGit(gff);
        }

        public static IFO ReadIfo(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return IFOHelpers.ConstructIfo(gff);
        }

        public static Andastra.Parsing.Resource.Generics.UTC.UTC ReadUtc(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return Andastra.Parsing.Resource.Generics.UTC.UTCHelpers.ConstructUtc(gff);
        }

        public static PTH ReadPth(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return PTHHelpers.ConstructPth(gff);
        }

        public static UTD ReadUtd(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return UTDHelpers.ConstructUtd(gff);
        }

        public static UTP ReadUtp(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return UTPHelpers.ConstructUtp(gff);
        }

        public static UTS ReadUts(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return UTSHelpers.ConstructUts(gff);
        }

        public static Andastra.Parsing.Resource.Generics.UTI.UTI ReadUti(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return Andastra.Parsing.Resource.Generics.UTI.UTIHelpers.ConstructUti(gff);
        }
    }
}
