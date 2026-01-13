using System;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.ARE;
using BioWare.NET.Resource.Formats.GFF.Generics.UTC;
using BioWare.NET.Resource.Formats.GFF.Generics.UTI;

namespace BioWare.NET.Resource
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

        public static UTC ReadUtc(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return UTCHelpers.ConstructUtc(gff);
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

        public static UTI ReadUti(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            GFF gff = reader.Load();
            return UTIHelpers.ConstructUti(gff);
        }
    }
}
