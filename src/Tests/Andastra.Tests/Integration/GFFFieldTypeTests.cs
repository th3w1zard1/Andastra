using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Integration tests for GFF field type modifications.
    /// </summary>
    public class GFFFieldTypeTests : IntegrationTestBase
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldUInt8_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetUInt8("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt8("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldInt8_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt8("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt8("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldUInt16_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetUInt16("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt16("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldInt16_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt16("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt16("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldUInt32_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetUInt32("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt32("Field1").Should().Be(2u);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldInt32_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt32("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt32("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldUInt64_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetUInt64("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt64("Field1").Should().Be(2ul);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldInt64_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt64("Field1", 1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt64("Field1").Should().Be(2L);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldSingle_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2.345
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetSingle("Field1", 1.234f);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetSingle("Field1").Should().BeApproximately(2.345f, 0.01f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldDouble_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2.345678
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetDouble("Field1", 1.234567);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetDouble("Field1").Should().Be(2.345678);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldString_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=def
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetString("Field1", "abc".ToString());

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetValue("Field1").Should().Be("def");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldLocString_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1(strref)=1
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetLocString("Field1", new LocalizedString(0));

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetLocString("Field1").StringRef.Should().Be(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldVector3_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=1|2|3
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetVector3("Field1", new Vector3(0, 1, 2));

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetVector3("Field1").Should().Be(new Vector3(1, 2, 3));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyFieldVector4_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=1|2|3|4
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetVector4("Field1", new Vector4(0, 1, 2, 3));

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetVector4("Field1").Should().Be(new Vector4(1, 2, 3, 4));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyNested_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Struct1\\Field1=2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            var struct1 = new GFFStruct(0);
            struct1.SetInt32("Field1", 1);
            gff.Root.SetStruct("Struct1", struct1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetStruct("Struct1").GetInt32("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Modify2DAMemory_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=2DAMEMORY5
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt32("Field1", 1);

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "999";

            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt32("Field1").Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyTLKMemory_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Field1=StrRef7
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt32("Field1", 1);

            var memory = new PatcherMemory();
            memory.MemoryStr[7] = 888;

            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt32("Field1").Should().Be(888);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddNewNested_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add1

[add1]
FieldType=Struct
Path=
Label=NewStruct
TypeId=0
AddField0=add_field1

[add_field1]
FieldType=Int
Path=
Label=Field1
Value=123
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetStruct("NewStruct").GetInt32("Field1").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddNested_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add1

[add1]
FieldType=Int
Path=Struct1
Label=Field2
Value=456
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            var struct1 = new GFFStruct(0);
            struct1.SetInt32("Field1", 123);
            gff.Root.SetStruct("Struct1", struct1);

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetStruct("Struct1").GetInt32("Field1").Should().Be(123);
            patchedGff.Root.GetStruct("Struct1").GetInt32("Field2").Should().Be(456);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddUse2DAMemory_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add1

[add1]
FieldType=Int
Path=
Label=Field1
Value=2DAMEMORY5
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "777";

            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt32("Field1").Should().Be(777);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddUseTLKMemory_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add1

[add1]
FieldType=Int
Path=
Label=Field1
Value=StrRef6
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();

            var memory = new PatcherMemory();
            memory.MemoryStr[6] = 666;

            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt32("Field1").Should().Be(666);
        }

    }
}
