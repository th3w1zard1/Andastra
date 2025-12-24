using System.Collections.Generic;
using Andastra.Parsing.Diff;
using Andastra.Parsing.Formats.GFF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Diff
{

    /// <summary>
    /// Tests for GFF diff functionality
    /// Ported from tests/tslpatcher/diff/test_gff.py
    /// </summary>
    public class GffDiffTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FlattenDifferences_ShouldHandleSimpleChanges()
        {
            var compareResult = new GffCompareResult();
            compareResult.AddDifference("Field1", "old_value", "new_value");
            compareResult.AddDifference("Field2", 10, 20);

            Dictionary<string, object> flatChanges = GffDiff.FlattenDifferences(compareResult);

            flatChanges.Should().HaveCount(2);
            flatChanges["Field1"].Should().Be("new_value");
            flatChanges["Field2"].Should().Be(20);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FlattenDifferences_ShouldHandleNestedPaths()
        {
            var compareResult = new GffCompareResult();
            compareResult.AddDifference("Root\\Child\\Field", "old", "new");
            compareResult.AddDifference("Root\\Other", 1, 2);

            Dictionary<string, object> flatChanges = GffDiff.FlattenDifferences(compareResult);

            flatChanges.Should().ContainKey("Root/Child/Field");
            flatChanges["Root/Child/Field"].Should().Be("new");
            flatChanges["Root/Other"].Should().Be(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FlattenDifferences_ShouldHandleRemovals()
        {
            var compareResult = new GffCompareResult();
            compareResult.AddDifference("RemovedField", "old_value", null);

            Dictionary<string, object> flatChanges = GffDiff.FlattenDifferences(compareResult);

            flatChanges.Should().ContainKey("RemovedField");
            flatChanges["RemovedField"].Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FlattenDifferences_ShouldHandleEmptyResult()
        {
            var compareResult = new GffCompareResult();
            Dictionary<string, object> flatChanges = GffDiff.FlattenDifferences(compareResult);

            flatChanges.Should().BeEmpty();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void BuildHierarchy_ShouldBuildSimpleHierarchy()
        {
            var flatChanges = new Dictionary<string, object>
        {
            { "Field1", "value1" },
            { "Field2", "value2" }
        };

            Dictionary<string, object> hierarchy = GffDiff.BuildHierarchy(flatChanges);

            hierarchy.Should().ContainKey("Field1");
            hierarchy["Field1"].Should().Be("value1");
            hierarchy["Field2"].Should().Be("value2");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void BuildHierarchy_ShouldBuildNestedHierarchy()
        {
            var flatChanges = new Dictionary<string, object>
        {
            { "Root/Child/Field", "value" },
            { "Root/Other", "other" }
        };

            Dictionary<string, object> hierarchy = GffDiff.BuildHierarchy(flatChanges);

            hierarchy.Should().ContainKey("Root");
            var root = hierarchy["Root"] as Dictionary<string, object>;
            Assert.NotNull(root);

            root.Should().ContainKey("Child");
            var child = root["Child"] as Dictionary<string, object>;
            Assert.NotNull(child);
            child["Field"].Should().Be("value");

            root.Should().ContainKey("Other");
            root["Other"].Should().Be("other");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void BuildHierarchy_ShouldBuildDeepNesting()
        {
            var flatChanges = new Dictionary<string, object>
        {
            { "Level1/Level2/Level3/Level4", "deep_value" }
        };

            Dictionary<string, object> hierarchy = GffDiff.BuildHierarchy(flatChanges);

            var level1 = hierarchy["Level1"] as Dictionary<string, object>;
            Assert.NotNull(level1);
            var level2 = level1["Level2"] as Dictionary<string, object>;
            Assert.NotNull(level2);
            var level3 = level2["Level3"] as Dictionary<string, object>;
            Assert.NotNull(level3);
            level3["Level4"].Should().Be("deep_value");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void BuildHierarchy_ShouldBuildMultipleBranches()
        {
            var flatChanges = new Dictionary<string, object>
        {
            { "Root/Branch1/Leaf1", "value1" },
            { "Root/Branch1/Leaf2", "value2" },
            { "Root/Branch2/Leaf1", "value3" }
        };

            var hierarchy = GffDiff.BuildHierarchy(flatChanges);

            var root = hierarchy["Root"] as Dictionary<string, object>;
            Assert.NotNull(root);
            var branch1 = root["Branch1"] as Dictionary<string, object>;
            Assert.NotNull(branch1);
            var branch2 = root["Branch2"] as Dictionary<string, object>;
            Assert.NotNull(branch2);

            branch1["Leaf1"].Should().Be("value1");
            branch1["Leaf2"].Should().Be("value2");
            branch2["Leaf1"].Should().Be("value3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SerializeToIni_ShouldSerializeSimpleHierarchy()
        {
            var hierarchy = new Dictionary<string, object>
        {
            { "Section1", new Dictionary<string, object> { { "Field1", "value1" }, { "Field2", "value2" } } }
        };

            string ini = GffDiff.SerializeToIni(hierarchy);

            ini.Should().Contain("[Section1]");
            ini.Should().Contain("Field1=value1");
            ini.Should().Contain("Field2=value2");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SerializeToIni_ShouldQuoteValuesWithSpaces()
        {
            var hierarchy = new Dictionary<string, object>
        {
            { "Section1", new Dictionary<string, object> { { "Field1", "value with spaces" } } }
        };

            string ini = GffDiff.SerializeToIni(hierarchy);

            ini.Should().Contain("Field1=\"value with spaces\"");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SerializeToIni_ShouldHandleNullValues()
        {
            var hierarchy = new Dictionary<string, object>
        {
            { "Section1", new Dictionary<string, object> { { "Field1", null } } }
        };

            string ini = GffDiff.SerializeToIni(hierarchy);

            ini.Should().Contain("Field1=null");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SerializeToIni_ShouldHandleNestedSections()
        {
            var hierarchy = new Dictionary<string, object>
        {
            { "Root", new Dictionary<string, object> { { "Child", new Dictionary<string, object> { { "Field", "value" } } } } }
        };

            string ini = GffDiff.SerializeToIni(hierarchy);

            // Depending on implementation, this might check for [Root.Child] or recursive structure
            // Based on GffDiff implementation:
            ini.Should().Contain("[Root.Child]");
            ini.Should().Contain("Field=value");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void DiffWorkflow_ShouldHandleFullWorkflow()
        {
            // 1. Create comparison result (simulating output from Compare)
            var compareResult = new GffCompareResult();
            compareResult.AddDifference("Section/Field", "old", "new");

            // 2. Flatten differences
            Dictionary<string, object> flatChanges = GffDiff.FlattenDifferences(compareResult);

            // 3. Build hierarchy
            Dictionary<string, object> hierarchy = GffDiff.BuildHierarchy(flatChanges);

            // 4. Serialize to INI
            string ini = GffDiff.SerializeToIni(hierarchy);

            ini.Should().Contain("[Section]");
            ini.Should().Contain("Field=new");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void BuildHierarchy_ShouldResolveConflictWhenLeafValuePrecedesNestedPath()
        {
            // Test conflict resolution: When a field appears as both a leaf value and a parent
            // of nested values, the nested structure should take precedence by overwriting
            // the leaf value with a dictionary.
            //
            // This scenario can occur when processing flat changes where:
            //   - "Field1" = "leafValue" (creates a leaf)
            //   - "Field1/SubField" = "nestedValue" (requires Field1 to be a dict)
            //
            // Expected behavior: The leaf value is overwritten with a dictionary, allowing
            // the nested structure to be built correctly.
            var flatChanges = new Dictionary<string, object>
            {
                // Process "Field1" as leaf first - this creates a leaf value
                { "Field1", "leafValue" },
                // Then process "Field1/SubField" - this conflicts because Field1 is already a leaf
                // The implementation should overwrite the leaf with a dict to allow the nested structure
                { "Field1/SubField", "nestedValue" },
                // Additional nested path under Field1 to verify the dict is working
                { "Field1/SubField2", "nestedValue2" }
            };

            Dictionary<string, object> hierarchy = GffDiff.BuildHierarchy(flatChanges);

            // Verify that Field1 is now a dictionary (not the leaf value)
            hierarchy.Should().ContainKey("Field1");
            var field1 = hierarchy["Field1"] as Dictionary<string, object>;
            Assert.NotNull(field1);

            // Verify the nested values are present
            field1.Should().ContainKey("SubField");
            field1["SubField"].Should().Be("nestedValue");
            field1.Should().ContainKey("SubField2");
            field1["SubField2"].Should().Be("nestedValue2");

            // Verify the original leaf value was overwritten (not preserved)
            // This confirms the conflict resolution overwrote the leaf with a dict
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void BuildHierarchy_ShouldResolveConflictWhenNestedPathPrecedesLeafValue()
        {
            // Test the reverse scenario: When nested paths are processed first, then a leaf value
            // is encountered at the same location.
            //
            // This scenario:
            //   - "Field1/SubField" = "nestedValue" (creates Field1 as a dict)
            //   - "Field1" = "leafValue" (conflicts because Field1 is already a dict)
            //
            // Expected behavior: The final assignment "Field1" = "leafValue" should overwrite
            // the dictionary with the leaf value, since we process in order and later values
            // take precedence.
            var flatChanges = new Dictionary<string, object>
            {
                // Process nested path first - this creates Field1 as a dictionary
                { "Field1/SubField", "nestedValue" },
                // Then process leaf value - this should overwrite the dictionary with the leaf
                { "Field1", "leafValue" }
            };

            Dictionary<string, object> hierarchy = GffDiff.BuildHierarchy(flatChanges);

            // Verify that Field1 is now a leaf value (the dictionary was overwritten)
            hierarchy.Should().ContainKey("Field1");
            hierarchy["Field1"].Should().Be("leafValue");

            // Verify the nested structure is gone (overwritten by the leaf)
            var field1AsDict = hierarchy["Field1"] as Dictionary<string, object>;
            Assert.Null(field1AsDict);
        }

    }
}
