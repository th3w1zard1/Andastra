using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.GFF;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.GFF;
using Xunit;

namespace TSLPatcher.Tests.Mods;

/// <summary>
/// Tests for GFF modifications (ported from test_mods.py - TestManipulateGFF)
/// </summary>
public class GffModificationTests
{
    [Fact]
    public void ModifyField_UInt8()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetUInt8("Field1", 1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2, gff.Root.GetUInt8("Field1"));
    }

    [Fact]
    public void ModifyField_Int8()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetInt8("Field1", 1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2, gff.Root.GetInt8("Field1"));
    }

    [Fact]
    public void ModifyField_UInt16()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetUInt16("Field1", 1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2, gff.Root.GetUInt16("Field1"));
    }

    [Fact]
    public void ModifyField_Int16()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetInt16("Field1", 1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2, gff.Root.GetInt16("Field1"));
    }

    [Fact]
    public void ModifyField_UInt32()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetUInt32("Field1", 1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2u, gff.Root.GetUInt32("Field1"));
    }

    [Fact]
    public void ModifyField_Int32()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetInt32("Field1", 1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2, gff.Root.GetInt32("Field1"));
    }

    [Fact]
    public void ModifyField_String()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetString("Field1", "old".ToString());

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant("new"))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal("new", gff.Root.GetValue("Field1"));
    }

    [Fact]
    public void ModifyField_Float()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetSingle("Field1", 1.5f);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueConstant(2.5f))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.Equal(2.5f, gff.Root.GetSingle("Field1"));
    }

    [Fact]
    public void ModifyField_LocalizedString_StringRef()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetLocString("Field1", new LocalizedString(100));

        var memory = new PatcherMemory();
        memory.MemoryStr[5] = 200;

        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new ModifyFieldGFF("Field1", new FieldValueTLKMemory(5))
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        var locstring = gff.Root.GetLocString("Field1");
        Assert.NotNull(locstring);
        Assert.Equal(200, locstring.StringRef);
    }

    [Fact]
    public void AddField_NewField()
    {
        // Arrange
        var gff = new GFF();
        gff.Root.SetString("ExistingField", "value".ToString());

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new AddFieldGFF("", "NewField", GFFFieldType.UInt8, new FieldValueConstant(42), "")
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        Assert.True(gff.Root.Exists("ExistingField"));
        Assert.True(gff.Root.Exists("NewField"));
        Assert.Equal(42, gff.Root.GetUInt8("NewField"));
    }

    [Fact]
    public void AddField_NestedStruct()
    {
        // Arrange
        var gff = new GFF();
        var struct1 = new GFFStruct();
        gff.Root.SetStruct("Parent", struct1);

        var memory = new PatcherMemory();
        var config = new ModificationsGFF("", replace: false, modifiers: new()
        {
            new AddFieldGFF("", "ChildField", GFFFieldType.Int32, new FieldValueConstant(100), "Parent")
        });

        // Act
        var bytes = config.PatchResource(gff.ToBytes(), memory, new PatchLogger(), Game.K1);
        gff = GFF.FromBytes((byte[])bytes);

        // Assert
        var parentStruct = gff.Root.GetStruct("Parent");
        Assert.NotNull(parentStruct);
        Assert.True(parentStruct.Exists("ChildField"));
        Assert.Equal(100, parentStruct.GetInt32("ChildField"));
    }
}

