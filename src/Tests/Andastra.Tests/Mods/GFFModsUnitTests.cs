using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.GFF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Direct unit tests for GFF modification classes.
    /// </summary>
    public class GFFModsUnitTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt8_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetUInt8("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt8("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int8_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetInt8("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt8("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt16_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetUInt16("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt16("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int16_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetInt16("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt16("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt32_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetUInt32("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt32("Field1").Should().Be(2u);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int32_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetInt32("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt32("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt64_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetUInt64("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2UL)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt64("Field1").Should().Be(2UL);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int64_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetInt64("Field1", 1);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2L)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetInt64("Field1").Should().Be(2L);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Single_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetSingle("Field1", 1.234f);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2.345f)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetSingle("Field1").Should().BeApproximately(2.345f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Double_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetDouble("Field1", 1.234567);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(2.345678)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetDouble("Field1").Should().BeApproximately(2.345678, 0.000001);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_String_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetString("Field1", "abc".ToString());

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant("def")) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetValue("Field1").Should().Be("def");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_LocalizedString_ShouldUpdateStringRef()
        {
            var gff = new GFF();
            gff.Root.SetLocString("Field1", new LocalizedString(0));

            var memory = new PatcherMemory();
            var config = new ModificationsGFF(
                "",
                false,
                new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(new LocalizedStringDelta(new FieldValueConstant(1)))) }
            );

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetLocString("Field1").StringRef.Should().Be(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Vector3_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetVector3("Field1", new Vector3(0, 1, 2));

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(new Vector3(1, 2, 3))) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            Vector3 vector = patchedGff.Root.GetVector3("Field1");
            vector.X.Should().BeApproximately(1f, 0.0001f);
            vector.Y.Should().BeApproximately(2f, 0.0001f);
            vector.Z.Should().BeApproximately(3f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Vector4_ShouldUpdateValue()
        {
            var gff = new GFF();
            gff.Root.SetVector4("Field1", new Vector4(0, 1, 2, 3));

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("Field1", new FieldValueConstant(new Vector4(1, 2, 3, 4))) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            Vector4 vector = patchedGff.Root.GetVector4("Field1");
            vector.X.Should().BeApproximately(1f, 0.0001f);
            vector.Y.Should().BeApproximately(2f, 0.0001f);
            vector.Z.Should().BeApproximately(3f, 0.0001f);
            vector.W.Should().BeApproximately(4f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_NestedPath_ShouldModifyNestedField()
        {
            var gff = new GFF();
            gff.Root.SetList("List", new GFFList());
            GFFList gffList = gff.Root.GetList("List");
            GFFStruct gffStruct = gffList.Add(0);
            gffStruct.SetString("String", "".ToString());

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("List\\0\\String", new FieldValueConstant("abc")) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            GFFList patchedList = patchedGff.Root.GetList("List");
            GFFStruct patchedStruct = patchedList.At(0);
            patchedStruct.Should().NotBeNull();
            patchedStruct.GetValue("String").Should().Be("abc");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_With2DAMemory_ShouldUseMemoryValue()
        {
            var gff = new GFF();
            gff.Root.SetString("String", "".ToString());
            gff.Root.SetUInt8("Integer", 0);

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            var config = new ModificationsGFF("", false, new List<ModifyGFF> {
            new ModifyFieldGFF("String", new FieldValue2DAMemory(5)),
            new ModifyFieldGFF("Integer", new FieldValue2DAMemory(5))
         });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetValue("String").Should().Be("123");
            patchedGff.Root.GetUInt8("Integer").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_WithTLKMemory_ShouldUseMemoryValue()
        {
            var gff = new GFF();
            gff.Root.SetUInt32("StrRef", 0);

            var memory = new PatcherMemory();
            memory.MemoryStr[10] = 999;

            var config = new ModificationsGFF("", false, new List<ModifyGFF> { new ModifyFieldGFF("StrRef", new FieldValueTLKMemory(10)) });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt32("StrRef").Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_MultipleFields_ShouldApplyAll()
        {
            var gff = new GFF();
            gff.Root.SetUInt8("Field1", 1);
            gff.Root.SetString("Field2", "old".ToString());
            gff.Root.SetInt32("Field3", 100);

            var memory = new PatcherMemory();
            var config = new ModificationsGFF("", false, new List<ModifyGFF> {
            new ModifyFieldGFF("Field1", new FieldValueConstant(10)),
            new ModifyFieldGFF("Field2", new FieldValueConstant("new")),
            new ModifyFieldGFF("Field3", new FieldValueConstant(200))
         });

            object bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            patchedGff.Root.GetUInt8("Field1").Should().Be(10);
            patchedGff.Root.GetValue("Field2").Should().Be("new");
            patchedGff.Root.GetInt32("Field3").Should().Be(200);
        }
    }
}
