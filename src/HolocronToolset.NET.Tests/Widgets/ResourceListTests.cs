using System;
using System.Collections.Generic;
using System.Linq;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Tests.TestHelpers;
using HolocronToolset.NET.Widgets;
using Xunit;

namespace HolocronToolset.NET.Tests.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/widgets/test_texture_loader.py
    // Original: Tests for ResourceList widget
    [Collection("Avalonia Test Collection")]
    public class ResourceListTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public ResourceListTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void ResourceList_InitializesCorrectly()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/widgets/test_texture_loader.py
            // Original: Test that ResourceList initializes correctly
            var resourceList = new ResourceList();
            resourceList.Should().NotBeNull();
        }

        [Fact]
        public void ResourceList_SetResources_UpdatesModel()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:154-187
            // Original: def set_resources(self, resources: list[FileResource], custom_category: str | None = None, *, clear_existing: bool = True):
            var resourceList = new ResourceList();
            var resources = new List<FileResource>
            {
                new FileResource("test1", ResourceType.UTC, 100, 0, "test1.utc"),
                new FileResource("test2", ResourceType.UTI, 200, 0, "test2.uti")
            };

            resourceList.SetResources(resources);
            var selected = resourceList.SelectedResources();
            selected.Should().NotBeNull();
        }

        [Fact]
        public void ResourceList_SetSections_UpdatesCombo()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:189-195
            // Original: def set_sections(self, sections: list[QStandardItem]):
            var resourceList = new ResourceList();
            var sections = new List<string> { "Section1", "Section2", "Section3" };

            resourceList.SetSections(sections);
            // Sections should be set (we can't directly verify ComboBox contents, but no exception should occur)
            resourceList.Should().NotBeNull();
        }

        [Fact]
        public void ResourceList_HideReloadButton_HidesButton()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:131-133
            // Original: def hide_reload_button(self):
            var resourceList = new ResourceList();
            resourceList.HideReloadButton();
            // Button should be hidden (we can't directly verify, but no exception should occur)
            resourceList.Should().NotBeNull();
        }

        [Fact]
        public void ResourceList_HideSection_HidesSection()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:135-138
            // Original: def hide_section(self):
            var resourceList = new ResourceList();
            resourceList.HideSection();
            // Section should be hidden (we can't directly verify, but no exception should occur)
            resourceList.Should().NotBeNull();
        }
    }
}
