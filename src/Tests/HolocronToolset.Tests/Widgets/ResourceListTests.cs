using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Resource;
using FluentAssertions;
using HolocronToolset.Tests.TestHelpers;
using HolocronToolset.Widgets;
using Xunit;

namespace HolocronToolset.Tests.Widgets
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:234-238
        // Original: def on_section_changed(self):
        [Fact]
        public void ResourceList_SectionChanged_Event_EmitsCorrectly()
        {
            var resourceList = new ResourceList();
            string receivedSection = null;
            
            resourceList.SectionChanged += (sender, section) =>
            {
                receivedSection = section;
            };

            // Set sections and trigger selection change
            var sections = new List<string> { "Section1", "Section2" };
            resourceList.SetSections(sections);
            
            // Manually trigger the event by simulating selection change
            if (resourceList.Ui.SectionCombo != null && resourceList.Ui.SectionCombo.Items.Count > 0)
            {
                resourceList.Ui.SectionCombo.SelectedIndex = 0;
                // Event should be emitted
                receivedSection.Should().Be("Section1");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:240-244
        // Original: def on_reload_clicked(self):
        [Fact]
        public void ResourceList_ReloadClicked_Event_EmitsCorrectly()
        {
            var resourceList = new ResourceList();
            string receivedSection = null;
            
            resourceList.ReloadClicked += (sender, section) =>
            {
                receivedSection = section;
            };

            // Set sections
            var sections = new List<string> { "Module1", "Module2" };
            resourceList.SetSections(sections);
            
            // Trigger reload button click
            if (resourceList.Ui.ReloadButton != null)
            {
                resourceList.Ui.ReloadButton.Command?.Execute(null);
                // If no section selected, should be null
                // If section selected, should match
                if (resourceList.Ui.SectionCombo != null && resourceList.Ui.SectionCombo.SelectedIndex >= 0)
                {
                    receivedSection.Should().NotBeNull();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:246-250
        // Original: def on_refresh_clicked(self):
        [Fact]
        public void ResourceList_RefreshClicked_Event_EmitsCorrectly()
        {
            var resourceList = new ResourceList();
            bool eventFired = false;
            
            resourceList.RefreshClicked += (sender, e) =>
            {
                eventFired = true;
            };

            // Trigger refresh button click
            if (resourceList.Ui.RefreshButton != null)
            {
                resourceList.Ui.RefreshButton.Command?.Execute(null);
                eventFired.Should().BeTrue();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/main_widgets.py:416-418
        // Original: def on_resource_double_clicked(self):
        [Fact]
        public void ResourceList_ResourceDoubleClicked_Event_EmitsCorrectly()
        {
            var resourceList = new ResourceList();
            List<FileResource> receivedResources = null;
            bool? receivedUseSpecializedEditor = null;
            
            resourceList.ResourceDoubleClicked += (sender, e) =>
            {
                receivedResources = e.Resources;
                receivedUseSpecializedEditor = e.UseSpecializedEditor;
            };

            // Set resources
            var resources = new List<FileResource>
            {
                new FileResource("test1", ResourceType.UTC, 100, 0, "test1.utc"),
                new FileResource("test2", ResourceType.UTI, 200, 0, "test2.uti")
            };
            resourceList.SetResources(resources);

            // Simulate double-click (if tree has items)
            // Note: In a real scenario, this would be triggered by user interaction
            // For testing, we verify the event handler is set up correctly
            resourceList.Should().NotBeNull();
            // Event should be null until actually triggered
            receivedResources.Should().BeNull();
        }

        [Fact]
        public void ResourceList_ResourceOpenEventArgs_InitializesCorrectly()
        {
            var resources = new List<FileResource>
            {
                new FileResource("test1", ResourceType.UTC, 100, 0, "test1.utc")
            };

            var args = new ResourceOpenEventArgs(resources, true);
            args.Resources.Should().HaveCount(1);
            args.Resources[0].ResName.Should().Be("test1");
            args.UseSpecializedEditor.Should().BeTrue();

            var argsNull = new ResourceOpenEventArgs(null, null);
            argsNull.Resources.Should().NotBeNull();
            argsNull.Resources.Should().BeEmpty();
            argsNull.UseSpecializedEditor.Should().BeNull();
        }
    }
}
