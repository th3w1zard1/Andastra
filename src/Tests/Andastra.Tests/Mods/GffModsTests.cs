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
    /// Tests for GFF modification functionality
    /// 1:1 port from tests/tslpatcher/test_mods.py (TestManipulateGFF)
    /// </summary>
    public class GffModsTests
    {
        #region Modify Field Tests - Integer Types

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt8()
        {
            // Python test: test_modify_field_uint8
            var gff = new GFF();
            gff.Root.SetUInt8("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetUInt8("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int8()
        {
            // Python test: test_modify_field_int8
            var gff = new GFF();
            gff.Root.SetInt8("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetInt8("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt16()
        {
            // Python test: test_modify_field_uint16
            var gff = new GFF();
            gff.Root.SetUInt16("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetUInt16("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int16()
        {
            // Python test: test_modify_field_int16
            var gff = new GFF();
            gff.Root.SetInt16("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetInt16("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt32()
        {
            // Python test: test_modify_field_uint32
            var gff = new GFF();
            gff.Root.SetUInt32("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetUInt32("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int32()
        {
            // Python test: test_modify_field_int32
            var gff = new GFF();
            gff.Root.SetInt32("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetInt32("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt64()
        {
            // Python test: test_modify_field_uint64
            var gff = new GFF();
            gff.Root.SetUInt64("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant((ulong)2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetUInt64("Field1").Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int64()
        {
            // Python test: test_modify_field_int64
            var gff = new GFF();
            gff.Root.SetInt64("Field1", 1);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant((long)2)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetInt64("Field1").Should().Be(2);
        }

        #endregion

        #region Modify Field Tests - Float Types

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Single()
        {
            // Python test: test_modify_field_single
            var gff = new GFF();
            gff.Root.SetSingle("Field1", 1.234f);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2.345f)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetSingle("Field1").Should().BeApproximately(2.345f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Double()
        {
            // Python test: test_modify_field_double
            var gff = new GFF();
            gff.Root.SetDouble("Field1", 1.234567);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(2.345678)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetDouble("Field1").Should().Be(2.345678);
        }

        #endregion

        #region Modify Field Tests - String and Complex Types

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_String()
        {
            // Python test: test_modify_field_string
            var gff = new GFF();
            gff.Root.SetString("Field1", "abc".ToString());

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant("def")));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetValue("Field1").Should().Be("def");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_LocString()
        {
            // Python test: test_modify_field_locstring
            var gff = new GFF();
            gff.Root.SetLocString("Field1", LocalizedString.FromInvalid());

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            var delta = new LocalizedStringDelta();
            delta.SetData(Language.English, Gender.Male, "test");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(delta)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            LocalizedString locString = patchedGff.Root.GetLocString("Field1");
            locString.Get(Language.English, Gender.Male).Should().Be("test");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Vector3()
        {
            // Python test: test_modify_field_vector3
            var gff = new GFF();
            gff.Root.SetVector3("Field1", new Vector3(0, 1, 2));

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(new Vector3(1, 2, 3))));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetVector3("Field1").Should().Be(new Vector3(1, 2, 3));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Vector4()
        {
            // Python test: test_modify_field_vector4
            var gff = new GFF();
            gff.Root.SetVector4("Field1", new Vector4(0, 1, 2, 3));

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("Field1", new FieldValueConstant(new Vector4(1, 2, 3, 4))));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetVector4("Field1").Should().Be(new Vector4(1, 2, 3, 4));
        }

        #endregion

        #region Modify Field Tests - Nested and Memory

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Nested()
        {
            // Python test: test_modify_nested
            var gff = new GFF();
            gff.Root.SetList("List", new GFFList());
            GFFList gffList = gff.Root.GetList("List");
            GFFStruct gffStruct = gffList.Add();
            gffStruct.SetString("String", "".ToString());

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("List/0/String", new FieldValueConstant("abc")));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            GFFList patchedList = patchedGff.Root.GetList("List");
            GFFStruct patchedStruct = patchedList.At(0);
            patchedStruct.GetValue("String").Should().Be("abc");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_2DAMemory()
        {
            // Python test: test_modify_2damemory
            var gff = new GFF();
            gff.Root.SetString("String", "".ToString());
            gff.Root.SetUInt8("Integer", 0);

            var memory = new PatcherMemory { Memory2DA = { [5] = "123" } };
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("String", new FieldValue2DAMemory(5)));
            config.Modifiers.Add(new ModifyFieldGFF("Integer", new FieldValue2DAMemory(5)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetValue("String").Should().Be("123");
            patchedGff.Root.GetUInt8("Integer").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_TLKMemory()
        {
            // Python test: test_modify_tlkmemory
            var gff = new GFF();
            gff.Root.SetString("String", "".ToString());
            gff.Root.SetUInt8("Integer", 0);

            var memory = new PatcherMemory { MemoryStr = { [5] = 123 } };
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new ModifyFieldGFF("String", new FieldValueTLKMemory(5)));
            config.Modifiers.Add(new ModifyFieldGFF("Integer", new FieldValueTLKMemory(5)));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetValue("String").Should().Be("123");
            patchedGff.Root.GetUInt8("Integer").Should().Be(123);
        }

        #endregion

        #region Add Field Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_NewNested()
        {
            // Python test: test_add_newnested
            var gff = new GFF();
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var addField1 = new AddFieldGFF("", "List", GFFFieldType.List, new FieldValueConstant(new GFFList()), "");

            var addField2 = new AddStructToListGFF("", new FieldValueConstant(new GFFStruct()), "List");
            addField1.Modifiers.Add(addField2);

            var addField3 = new AddFieldGFF("", "SomeInteger", GFFFieldType.UInt8, new FieldValueConstant((byte)123), "List/>>##INDEXINLIST##<<");
            addField2.Modifiers.Add(addField3);

            var config = new ModificationsGFF("", false, new List<ModifyGFF> { addField1 });

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetList("List").Should().NotBeNull();
            patchedGff.Root.GetList("List").At(0).Should().NotBeNull();
            patchedGff.Root.GetList("List").At(0).GetUInt8("SomeInteger").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_Nested()
        {
            // Python test: test_add_nested
            var gff = new GFF();
            var gffList = new GFFList();
            gff.Root.SetList("List", gffList);
            GFFStruct gffStruct = gffList.Add(0);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(
                new AddFieldGFF(
                    "",
                    "String",
                    GFFFieldType.String,
                    new FieldValueConstant("abc"),
                    "List/0"
                )
            );

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            GFFList patchedList = patchedGff.Root.GetList("List");
            GFFStruct patchedStruct = patchedList.At(0);
            patchedStruct.GetValue("String").Should().Be("abc");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_Use2DAMemory()
        {
            // Python test: test_add_use_2damemory
            var gff = new GFF();

            var memory = new PatcherMemory { Memory2DA = { [5] = "123" } };
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new AddFieldGFF("", "String", GFFFieldType.String, new FieldValue2DAMemory(5), ""));
            config.Modifiers.Add(new AddFieldGFF("", "Integer", GFFFieldType.UInt8, new FieldValue2DAMemory(5), ""));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetValue("String").Should().Be("123");
            patchedGff.Root.GetUInt8("Integer").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_UseTLKMemory()
        {
            // Python test: test_add_use_tlkmemory
            var gff = new GFF();

            var memory = new PatcherMemory { MemoryStr = { [5] = 123 } };
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new AddFieldGFF("", "String", GFFFieldType.String, new FieldValueTLKMemory(5), ""));
            config.Modifiers.Add(new AddFieldGFF("", "Integer", GFFFieldType.UInt8, new FieldValueTLKMemory(5), ""));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetValue("String").Should().Be("123");
            patchedGff.Root.GetUInt8("Integer").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_LocString()
        {
            // Python test: test_add_field_locstring
            var gff = new GFF();
            gff.Root.SetLocString("Field1", LocalizedString.FromInvalid());

            var memory = new PatcherMemory { Memory2DA = { [5] = "123" } };
            var logger = new PatchLogger();

            var modifiers = new List<ModifyGFF>
        {
            new AddFieldGFF(
                "",
                "Field1",
                GFFFieldType.LocalizedString,
                new FieldValueConstant(new LocalizedStringDelta(new FieldValue2DAMemory(5))),
                ""
            )
        };

            var config = new ModificationsGFF("", false, modifiers);

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            patchedGff.Root.GetLocString("Field1").StringRef.Should().Be(123);
        }

        #endregion

        #region Add Struct To List Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddList_ListIndex()
        {
            // Python test: test_addlist_listindex
            var gff = new GFF();
            var gffList = new GFFList();
            gff.Root.SetList("List", gffList);

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new AddStructToListGFF("test1", new FieldValueConstant(new GFFStruct(5)), "List", null));
            config.Modifiers.Add(new AddStructToListGFF("test2", new FieldValueConstant(new GFFStruct(3)), "List", null));
            config.Modifiers.Add(new AddStructToListGFF("test3", new FieldValueConstant(new GFFStruct(1)), "List", null));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);
            GFF patchedGff = new GFFBinaryReader(patchedBytes).Load();

            GFFList patchedList = patchedGff.Root.GetList("List");
            patchedList.At(0).StructId.Should().Be(5);
            patchedList.At(1).StructId.Should().Be(3);
            patchedList.At(2).StructId.Should().Be(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddList_Store2DAMemory()
        {
            // Python test: test_addlist_store_2damemory
            var gff = new GFF();
            gff.Root.SetList("List", new GFFList());

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new ModificationsGFF("");
            config.Modifiers.Add(new AddStructToListGFF("test1", new FieldValueConstant(new GFFStruct()), "List"));
            config.Modifiers.Add(new AddStructToListGFF("test2", new FieldValueConstant(new GFFStruct()), "List", indexToToken: 12));

            var writer = new GFFBinaryWriter(gff);
            byte[] gffBytes = writer.Write();
            config.PatchResource(gffBytes, memory, logger, BioWareGame.K2);

            memory.Memory2DA[12].Should().Be("1");
        }

        #endregion
    }
}
