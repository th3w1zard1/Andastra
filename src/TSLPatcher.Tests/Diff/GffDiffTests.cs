using System.Collections.Generic;
using FluentAssertions;
using TSLPatcher.Core.Diff;
using TSLPatcher.Core.Formats.GFF;
using Xunit;

namespace TSLPatcher.Tests.Diff;

/// <summary>
/// Tests for GFF diff functionality
/// Ported from tests/tslpatcher/diff/test_gff.py
/// </summary>
public class GffDiffTests
{
    [Fact]
    public void FlattenDifferences_ShouldHandleSimpleChanges()
    {
        var compareResult = new GffCompareResult();
        compareResult.AddDifference("Field1", "old_value", "new_value");
        compareResult.AddDifference("Field2", 10, 20);

        Dictionary<string, object?> flatChanges = GffDiff.FlattenDifferences(compareResult);

        flatChanges.Should().HaveCount(2);
        flatChanges["Field1"].Should().Be("new_value");
        flatChanges["Field2"].Should().Be(20);
    }

    [Fact]
    public void FlattenDifferences_ShouldHandleNestedPaths()
    {
        var compareResult = new GffCompareResult();
        compareResult.AddDifference("Root\\Child\\Field", "old", "new");
        compareResult.AddDifference("Root\\Other", 1, 2);

        Dictionary<string, object?> flatChanges = GffDiff.FlattenDifferences(compareResult);

        flatChanges.Should().Contain("Root/Child/Field");
        flatChanges["Root/Child/Field"].Should().Be("new");
        flatChanges["Root/Other"].Should().Be(2);
    }

    [Fact]
    public void FlattenDifferences_ShouldHandleRemovals()
    {
        var compareResult = new GffCompareResult();
        compareResult.AddDifference("RemovedField", "old_value", null);

        Dictionary<string, object?> flatChanges = GffDiff.FlattenDifferences(compareResult);

        flatChanges.Should().Contain("RemovedField");
        flatChanges["RemovedField"].Should().BeNull();
    }

    [Fact]
    public void FlattenDifferences_ShouldHandleEmptyResult()
    {
        var compareResult = new GffCompareResult();
        Dictionary<string, object?> flatChanges = GffDiff.FlattenDifferences(compareResult);

        flatChanges.Should().BeEmpty();
    }

    [Fact]
    public void BuildHierarchy_ShouldBuildSimpleHierarchy()
    {
        var flatChanges = new Dictionary<string, object?>
        {
            { "Field1", "value1" },
            { "Field2", "value2" }
        };

        Dictionary<string, object?> hierarchy = GffDiff.BuildHierarchy(flatChanges);

        hierarchy.Should().Contain("Field1");
        hierarchy["Field1"].Should().Be("value1");
        hierarchy["Field2"].Should().Be("value2");
    }

    [Fact]
    public void BuildHierarchy_ShouldBuildNestedHierarchy()
    {
        var flatChanges = new Dictionary<string, object?>
        {
            { "Root/Child/Field", "value" },
            { "Root/Other", "other" }
        };

        Dictionary<string, object?> hierarchy = GffDiff.BuildHierarchy(flatChanges);

        hierarchy.Should().Contain("Root");
        var root = hierarchy["Root"] as Dictionary<string, object?>;
        root.Should().NotBeNull();

        root!.Should().Contain("Child");
        var child = root["Child"] as Dictionary<string, object?>;
        child.Should().NotBeNull();
        child!["Field"].Should().Be("value");

        root.Should().Contain("Other");
        root["Other"].Should().Be("other");
    }

    [Fact]
    public void BuildHierarchy_ShouldBuildDeepNesting()
    {
        var flatChanges = new Dictionary<string, object?>
        {
            { "Level1/Level2/Level3/Level4", "deep_value" }
        };

        Dictionary<string, object?> hierarchy = GffDiff.BuildHierarchy(flatChanges);

        var level1 = hierarchy["Level1"] as Dictionary<string, object?>;
        var level2 = level1!["Level2"] as Dictionary<string, object?>;
        var level3 = level2!["Level3"] as Dictionary<string, object?>;
        level3!["Level4"].Should().Be("deep_value");
    }

    [Fact]
    public void BuildHierarchy_ShouldBuildMultipleBranches()
    {
        var flatChanges = new Dictionary<string, object?>
        {
            { "Root/Branch1/Leaf1", "value1" },
            { "Root/Branch1/Leaf2", "value2" },
            { "Root/Branch2/Leaf1", "value3" }
        };

        var hierarchy = GffDiff.BuildHierarchy(flatChanges);

        var root = hierarchy["Root"] as Dictionary<string, object?>;
        var branch1 = root!["Branch1"] as Dictionary<string, object?>;
        var branch2 = root!["Branch2"] as Dictionary<string, object?>;

        branch1!["Leaf1"].Should().Be("value1");
        branch1!["Leaf2"].Should().Be("value2");
        branch2!["Leaf1"].Should().Be("value3");
    }

    [Fact]
    public void SerializeToIni_ShouldSerializeSimpleHierarchy()
    {
        var hierarchy = new Dictionary<string, object?>
        {
            { "Section1", new Dictionary<string, object?> { { "Field1", "value1" }, { "Field2", "value2" } } }
        };

        string ini = GffDiff.SerializeToIni(hierarchy);

        ini.Should().Contain("[Section1]");
        ini.Should().Contain("Field1=value1");
        ini.Should().Contain("Field2=value2");
    }

    [Fact]
    public void SerializeToIni_ShouldQuoteValuesWithSpaces()
    {
        var hierarchy = new Dictionary<string, object?>
        {
            { "Section1", new Dictionary<string, object?> { { "Field1", "value with spaces" } } }
        };

        string ini = GffDiff.SerializeToIni(hierarchy);

        ini.Should().Contain("Field1=\"value with spaces\"");
    }

    [Fact]
    public void SerializeToIni_ShouldHandleNullValues()
    {
        var hierarchy = new Dictionary<string, object?>
        {
            { "Section1", new Dictionary<string, object?> { { "Field1", null } } }
        };

        string ini = GffDiff.SerializeToIni(hierarchy);

        ini.Should().Contain("Field1=null");
    }

    [Fact]
    public void SerializeToIni_ShouldHandleNestedSections()
    {
        var hierarchy = new Dictionary<string, object?>
        {
            { "Root", new Dictionary<string, object?> { { "Child", new Dictionary<string, object?> { { "Field", "value" } } } } }
        };

        string ini = GffDiff.SerializeToIni(hierarchy);

        // Depending on implementation, this might check for [Root.Child] or recursive structure
        // Based on GffDiff implementation:
        ini.Should().Contain("[Root.Child]");
        ini.Should().Contain("Field=value");
    }

    [Fact]
    public void DiffWorkflow_ShouldHandleFullWorkflow()
    {
        // 1. Create comparison result (simulating output from Compare)
        var compareResult = new GffCompareResult();
        compareResult.AddDifference("Section/Field", "old", "new");

        // 2. Flatten differences
        Dictionary<string, object?> flatChanges = GffDiff.FlattenDifferences(compareResult);

        // 3. Build hierarchy
        Dictionary<string, object?> hierarchy = GffDiff.BuildHierarchy(flatChanges);

        // 4. Serialize to INI
        string ini = GffDiff.SerializeToIni(hierarchy);

        ini.Should().Contain("[Section]");
        ini.Should().Contain("Field=new");
    }
}
