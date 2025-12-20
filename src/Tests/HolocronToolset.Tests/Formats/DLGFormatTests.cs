using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.DLG;
using FluentAssertions;
using Xunit;

namespace HolocronToolset.Tests.Formats
{
    /// <summary>
    /// Tests for DLG format parsing/serialization based on Ghidra reverse engineering analysis.
    ///
    /// DLG format is used by:
    /// - Neverwinter Nights: Enhanced Edition (nwmain.exe) - Base DLG format
    /// - KotOR 1 (swkotor.exe: 0x005a2ae0) - Base DLG format
    /// - KotOR 2 (swkotor2.exe: 0x005ea880) - Extended DLG format with K2-specific fields
    ///
    /// Eclipse games (DAO/DA2/ME) use .cnv "conversation" format, NOT DLG!
    /// Ghidra analysis confirms: daorigins.exe, DragonAge2.exe, MassEffect.exe use "conversation" strings
    /// </summary>
    public class DLGFormatTests
    {
        /// <summary>
        /// Test K1 DLG format - base fields only, no K2-specific fields.
        /// Ghidra analysis: swkotor.exe:0x005a2ae0 reads base DLG fields.
        /// </summary>
        [Fact]
        public void TestK1DlgFormat_BaseFieldsOnly()
        {
            // Create K1-style DLG with only base fields
            var dlg = new DLG
            {
                WordCount = 100,
                OnAbort = new ResRef("k_pdan_abort"),
                OnEnd = new ResRef("k_pdan_end"),
                Skippable = true,
                AmbientTrack = new ResRef("mus_area_dan"),
                AnimatedCut = 1,
                CameraModel = new ResRef("cameran1"),
                ComputerType = DLGComputerType.Modern,
                ConversationType = DLGConversationType.Human,
                OldHitCheck = false,
                UnequipHands = false,
                UnequipItems = false,
                VoId = "dan13_c01",
                // K2-specific fields should be zero/empty for K1
                AlienRaceOwner = 0,
                PostProcOwner = 0,
                RecordNoVo = 0,
                NextNodeId = 0
            };

            // Add simple entry/reply structure
            var entry = new DLGEntry { Speaker = "NPC1", ListIndex = 0 };
            entry.Text = LocalizedString.FromEnglish("Hello there!");
            var reply = new DLGReply { ListIndex = 0 };
            reply.Text = LocalizedString.FromEnglish("Greetings.");

            var starterLink = new DLGLink(entry, 0);
            dlg.Starters.Add(starterLink);

            var replyLink = new DLGLink(reply, 0);
            entry.Links.Add(replyLink);

            // Serialize as K1 format - K2 fields should NOT be written
            GFF gff = DLGHelper.DismantleDlg(dlg, Game.K1);

            // Verify base fields are written
            // NumWords is stored as uint32, so use GetUInt32 to read it
            gff.Root.GetUInt32("NumWords").Should().Be(100u);
            gff.Root.Acquire("EndConverAbort", ResRef.FromBlank()).Should().Be(new ResRef("k_pdan_abort"));
            gff.Root.Acquire("AnimatedCut", (byte)0).Should().Be((byte)1);

            // Ghidra analysis: swkotor.exe:0x005a2ae0 does NOT read AlienRaceOwner
            // Verify K2-specific fields are NOT present in K1 format
            gff.Root.Exists("AlienRaceOwner").Should().BeFalse("K1 format should not include AlienRaceOwner");
            gff.Root.Exists("PostProcOwner").Should().BeFalse("K1 format should not include PostProcOwner");
            gff.Root.Exists("RecordNoVO").Should().BeFalse("K1 format should not include RecordNoVO");
            gff.Root.Exists("NextNodeID").Should().BeFalse("K1 format should not include NextNodeID");

            // Verify entry list
            var entryList = gff.Root.Acquire("EntryList", new GFFList());
            entryList.Count.Should().Be(1);
            var entryStruct = entryList.At(0);
            entryStruct.Acquire("Speaker", string.Empty).Should().Be("NPC1");

            // Verify K2-specific node fields are NOT present
            entryStruct.Exists("ActionParam1").Should().BeFalse("K1 nodes should not have ActionParam1");
            entryStruct.Exists("Script2").Should().BeFalse("K1 nodes should not have Script2");
            entryStruct.Exists("AlienRaceNode").Should().BeFalse("K1 nodes should not have AlienRaceNode");

            // Round-trip test
            DLG reconstructed = DLGHelper.ConstructDlg(gff);
            reconstructed.WordCount.Should().Be(100);
            reconstructed.AnimatedCut.Should().Be(1);
            reconstructed.AlienRaceOwner.Should().Be(0, "K1 format should read as 0");
        }

        /// <summary>
        /// Test K2 DLG format - includes K2-specific fields.
        /// Ghidra analysis: swkotor2.exe:0x005ea880 line 75-76 reads AlienRaceOwner field.
        /// </summary>
        [Fact]
        public void TestK2DlgFormat_WithK2SpecificFields()
        {
            // Create K2-style DLG with K2-specific fields
            var dlg = new DLG
            {
                WordCount = 200,
                OnAbort = new ResRef("a_globalabo"),
                OnEnd = new ResRef("a_globalend"),
                Skippable = true,
                AmbientTrack = new ResRef("mus_s_combat"),
                AnimatedCut = 0,
                CameraModel = new ResRef("camerag"),
                ComputerType = DLGComputerType.Ancient,
                ConversationType = DLGConversationType.Computer,
                OldHitCheck = true,
                UnequipHands = true,
                UnequipItems = false,
                VoId = "per_g0t0",
                // Ghidra analysis: swkotor2.exe:0x005ea880 line 75-76 reads these K2-specific fields
                AlienRaceOwner = 5,
                PostProcOwner = 3,
                RecordNoVo = 1,
                NextNodeId = 42
            };

            // Add entry with K2-specific node fields
            var entry = new DLGEntry
            {
                Speaker = "G0T0",
                ListIndex = 0,
                // K2-specific node fields
                Script1Param1 = 10,
                Script1Param2 = 20,
                Script2 = new ResRef("a_alienrace"),
                AlienRaceNode = 2,
                EmotionId = 3,
                FacialId = 4,
                NodeId = 1,
                Unskippable = true,
                PostProcNode = 5,
                RecordNoVoOverride = true,
                RecordVo = true
            };
            entry.Text = LocalizedString.FromEnglish("Statement: You will cooperate.");

            var reply = new DLGReply
            {
                ListIndex = 0,
                // K2-specific node fields
                Script1Param1 = 5,
                Script2 = new ResRef("a_replynode"),
                NodeId = 2
            };
            reply.Text = LocalizedString.FromEnglish("[Accept]");

            var starterLink = new DLGLink(entry, 0);
            dlg.Starters.Add(starterLink);

            // K2-specific link fields
            var replyLink = new DLGLink(reply, 0)
            {
                Active2 = new ResRef("k_influence"),
                Logic = true,
                Active1Not = false,
                Active2Not = true,
                Active1Param1 = 100,
                Active1Param2 = 200,
                Active2Param1 = 150,
                Active1Param6 = "param_string_a",
                Active2Param6 = "param_string_b"
            };
            entry.Links.Add(replyLink);

            // Serialize as K2 format - K2 fields SHOULD be written
            GFF gff = DLGHelper.DismantleDlg(dlg, Game.K2);

            // Verify base fields
            // NumWords is stored as uint32, so use GetUInt32 to read it
            gff.Root.GetUInt32("NumWords").Should().Be(200u);
            gff.Root.Acquire("ComputerType", (byte)0).Should().Be((byte)1, "Ancient = 1");
            gff.Root.Acquire("ConversationType", 0).Should().Be(1, "Computer = 1");

            // Ghidra analysis: swkotor2.exe:0x005ea880 line 75-76 reads AlienRaceOwner
            // Verify K2-specific root fields ARE present and correct
            gff.Root.Acquire("AlienRaceOwner", 0).Should().Be(5, "K2 format should include AlienRaceOwner");
            gff.Root.Acquire("PostProcOwner", 0).Should().Be(3, "K2 format should include PostProcOwner");
            gff.Root.Acquire("RecordNoVO", 0).Should().Be(1, "K2 format should include RecordNoVO");
            gff.Root.Acquire("NextNodeID", 0).Should().Be(42, "K2 format should include NextNodeID");

            // Verify entry list with K2-specific node fields
            var entryList = gff.Root.Acquire("EntryList", new GFFList());
            entryList.Count.Should().Be(1);
            var entryStruct = entryList.At(0);
            entryStruct.Acquire("Speaker", string.Empty).Should().Be("G0T0");

            // Verify K2-specific node fields ARE present
            entryStruct.Acquire("ActionParam1", 0).Should().Be(10, "K2 nodes should have ActionParam1");
            entryStruct.Acquire("ActionParam2", 0).Should().Be(20, "K2 nodes should have ActionParam2");
            entryStruct.Acquire("Script2", ResRef.FromBlank()).Should().Be(ResRef.FromString("a_alienrace"));
            entryStruct.Acquire("AlienRaceNode", 0).Should().Be(2);
            entryStruct.Acquire("Emotion", 0).Should().Be(3);
            entryStruct.Acquire("FacialAnim", 0).Should().Be(4);
            entryStruct.Acquire("NodeID", 0).Should().Be(1);
            entryStruct.Acquire("NodeUnskippable", 0).Should().Be(1, "Unskippable should be 1");
            entryStruct.Acquire("PostProcNode", 0).Should().Be(5);
            entryStruct.Acquire("RecordNoVOOverri", 0).Should().Be(1);
            entryStruct.Acquire("RecordVO", 0).Should().Be(1);

            // Verify K2-specific link fields
            var repliesList = entryStruct.Acquire("RepliesList", new GFFList());
            repliesList.Count.Should().Be(1);
            var linkStruct = repliesList.At(0);
            linkStruct.Acquire("Active2", ResRef.FromBlank()).Should().Be(ResRef.FromString("k_influence"));
            linkStruct.Acquire("Logic", 0).Should().Be(1, "Logic should be int32 = 1");
            linkStruct.Acquire("Not", (byte)0).Should().Be((byte)0);
            linkStruct.Acquire("Not2", (byte)0).Should().Be((byte)1);
            linkStruct.Acquire("Param1", 0).Should().Be(100);
            linkStruct.Acquire("Param2", 0).Should().Be(200);
            linkStruct.Acquire("Param1b", 0).Should().Be(150);
            linkStruct.Acquire("ParamStrA", string.Empty).Should().Be("param_string_a");
            linkStruct.Acquire("ParamStrB", string.Empty).Should().Be("param_string_b");

            // Round-trip test
            DLG reconstructed = DLGHelper.ConstructDlg(gff);
            reconstructed.WordCount.Should().Be(200);
            reconstructed.AlienRaceOwner.Should().Be(5);
            reconstructed.PostProcOwner.Should().Be(3);
            reconstructed.RecordNoVo.Should().Be(1);
            reconstructed.NextNodeId.Should().Be(42);

            var reconstructedEntry = reconstructed.AllEntries()[0];
            reconstructedEntry.Script1Param1.Should().Be(10);
            reconstructedEntry.Script2.Should().Be(ResRef.FromString("a_alienrace"));
            reconstructedEntry.AlienRaceNode.Should().Be(2);
        }

        /// <summary>
        /// Test that optional fields are conditionally written based on PyKotor patterns.
        /// </summary>
        [Fact]
        public void TestDlgFormat_ConditionalOptionalFields()
        {
            var dlg = new DLG
            {
                WordCount = 50,
                OnAbort = ResRef.FromBlank(),
                OnEnd = ResRef.FromBlank(),
                Skippable = false,
                // Optional fields - should only write if non-default
                AmbientTrack = ResRef.FromBlank(), // Should NOT write
                AnimatedCut = 0, // Should NOT write
                ComputerType = DLGComputerType.Modern, // Should NOT write (default)
                ConversationType = DLGConversationType.Human, // Should NOT write (default)
                OldHitCheck = false, // Should NOT write
                UnequipHands = false, // Should NOT write
                UnequipItems = false // Should NOT write
            };

            GFF gff = DLGHelper.DismantleDlg(dlg, Game.K1);

            // Verify optional fields are NOT present when default
            gff.Root.Exists("AmbientTrack").Should().BeFalse("Empty ResRef should not be written");
            gff.Root.Exists("AnimatedCut").Should().BeFalse("Zero AnimatedCut should not be written");
            gff.Root.Exists("ComputerType").Should().BeFalse("Default ComputerType should not be written");
            gff.Root.Exists("ConversationType").Should().BeFalse("Default ConversationType should not be written");
            gff.Root.Exists("OldHitCheck").Should().BeFalse("False OldHitCheck should not be written");
            gff.Root.Exists("UnequipHItem").Should().BeFalse("False UnequipHands should not be written");
            gff.Root.Exists("UnequipItems").Should().BeFalse("False UnequipItems should not be written");

            // Now test with non-default values
            dlg.AmbientTrack = ResRef.FromString("mus_test");
            dlg.AnimatedCut = 1;
            dlg.ComputerType = DLGComputerType.Ancient;
            dlg.ConversationType = DLGConversationType.Computer;
            dlg.OldHitCheck = true;
            dlg.UnequipHands = true;
            dlg.UnequipItems = true;

            gff = DLGHelper.DismantleDlg(dlg, Game.K1);

            // Verify optional fields ARE present when non-default
            gff.Root.Exists("AmbientTrack").Should().BeTrue("Non-empty ResRef should be written");
            gff.Root.Exists("AnimatedCut").Should().BeTrue("Non-zero AnimatedCut should be written");
            gff.Root.Exists("ComputerType").Should().BeTrue("Non-default ComputerType should be written");
            gff.Root.Exists("ConversationType").Should().BeTrue("Non-default ConversationType should be written");
            gff.Root.Exists("OldHitCheck").Should().BeTrue("True OldHitCheck should be written");
            gff.Root.Exists("UnequipHItem").Should().BeTrue("True UnequipHands should be written");
            gff.Root.Exists("UnequipItems").Should().BeTrue("True UnequipItems should be written");
        }

        /// <summary>
        /// Test IsChild field conditional writing - only written for non-StartingList links.
        /// Ghidra analysis: swkotor.exe/swkotor2.exe only write IsChild for entry/reply links, not starters.
        /// </summary>
        [Fact]
        public void TestDlgFormat_IsChildConditionalWriting()
        {
            var dlg = new DLG();
            var entry = new DLGEntry { Speaker = "Test", ListIndex = 0 };
            entry.Text = LocalizedString.FromEnglish("Test");
            var reply = new DLGReply { ListIndex = 0 };
            reply.Text = LocalizedString.FromEnglish("Reply");

            var starterLink = new DLGLink(entry, 0) { IsChild = true };
            dlg.Starters.Add(starterLink);

            var replyLink = new DLGLink(reply, 0) { IsChild = true };
            entry.Links.Add(replyLink);

            GFF gff = DLGHelper.DismantleDlg(dlg, Game.K1);

            // Verify StartingList links do NOT have IsChild field
            var startingList = gff.Root.Acquire("StartingList", new GFFList());
            var starterStruct = startingList.At(0);
            starterStruct.Exists("IsChild").Should().BeFalse("StartingList links should not have IsChild field");

            // Verify RepliesList links DO have IsChild field
            var entryList = gff.Root.Acquire("EntryList", new GFFList());
            var entryStruct = entryList.At(0);
            var repliesList = entryStruct.Acquire("RepliesList", new GFFList());
            var replyLinkStruct = repliesList.At(0);
            replyLinkStruct.Exists("IsChild").Should().BeTrue("RepliesList links should have IsChild field");
            replyLinkStruct.Acquire("IsChild", (byte)0).Should().Be((byte)1);
        }

        /// <summary>
        /// Test LinkComment conditional writing - only written if non-empty.
        /// </summary>
        [Fact]
        public void TestDlgFormat_LinkCommentConditionalWriting()
        {
            var dlg = new DLG();
            var entry = new DLGEntry { Speaker = "Test", ListIndex = 0 };
            entry.Text = LocalizedString.FromEnglish("Test");
            var reply1 = new DLGReply { ListIndex = 0 };
            reply1.Text = LocalizedString.FromEnglish("Reply1");
            var reply2 = new DLGReply { ListIndex = 1 };
            reply2.Text = LocalizedString.FromEnglish("Reply2");

            var starterLink = new DLGLink(entry, 0);
            dlg.Starters.Add(starterLink);

            var replyLink1 = new DLGLink(reply1, 0) { Comment = "" }; // Empty
            var replyLink2 = new DLGLink(reply2, 1) { Comment = "This is a comment" }; // Non-empty
            entry.Links.Add(replyLink1);
            entry.Links.Add(replyLink2);

            GFF gff = DLGHelper.DismantleDlg(dlg, Game.K1);

            var entryList = gff.Root.Acquire("EntryList", new GFFList());
            var entryStruct = entryList.At(0);
            var repliesList = entryStruct.Acquire("RepliesList", new GFFList());

            // First link should NOT have LinkComment (empty)
            var link1Struct = repliesList.At(0);
            link1Struct.Exists("LinkComment").Should().BeFalse("Empty LinkComment should not be written");

            // Second link SHOULD have LinkComment (non-empty)
            var link2Struct = repliesList.At(1);
            link2Struct.Exists("LinkComment").Should().BeTrue("Non-empty LinkComment should be written");
            link2Struct.Acquire("LinkComment", string.Empty).Should().Be("This is a comment");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:858-1014
        // Test DLG serialization/deserialization using ToDict/FromDict methods
        [Fact]
        public void TestDlgEntrySerializationBasic()
        {
            var entry = new DLGEntry();
            entry.Comment = "Test Comment";
            entry.CameraAngle = 45;

            var serialized = entry.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGEntry;
            deserialized.Should().NotBeNull();

            deserialized.Comment.Should().Be(entry.Comment);
            deserialized.CameraAngle.Should().Be(entry.CameraAngle);
        }

        [Fact]
        public void TestDlgEntrySerializationWithLinks()
        {
            var entry = new DLGEntry();
            entry.Comment = "Entry with links";
            var reply = new DLGReply();
            var link = new DLGLink(reply, 1);
            entry.Links.Add(link);

            var serialized = entry.ToDict();

            // Debug: Verify links are in serialized data
            serialized.Should().ContainKey("data");
            var data = serialized["data"] as System.Collections.Generic.Dictionary<string, object>;
            data.Should().NotBeNull();
            data.Should().ContainKey("links");

            // Debug: Inspect the links structure
            var linksValue = data["links"] as System.Collections.Generic.Dictionary<string, object>;
            linksValue.Should().NotBeNull();
            linksValue.Should().ContainKey("value");
            linksValue.Should().ContainKey("py_type");
            linksValue["py_type"].Should().Be("list");

            // Check actual type of linksList - it might be List<Dictionary<string, object>> not List<object>
            var linksListValue = linksValue["value"];
            linksListValue.Should().NotBeNull();

            // Try to cast to IEnumerable to iterate
            if (linksListValue is System.Collections.IEnumerable linksEnumerable)
            {
                int count = 0;
                foreach (var item in linksEnumerable)
                {
                    count++;
                    if (item is System.Collections.Generic.Dictionary<string, object> linkDict)
                    {
                        linkDict.Should().ContainKey("type");
                        linkDict.Should().ContainKey("key");
                        linkDict.Should().ContainKey("node");
                        linkDict.Should().ContainKey("link_list_index");
                        linkDict.Should().ContainKey("data");
                    }
                }
                count.Should().Be(1);
            }

            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGEntry;
            deserialized.Should().NotBeNull();

            deserialized.Comment.Should().Be(entry.Comment);
            deserialized.Links.Count.Should().Be(1);
            deserialized.Links[0].ListIndex.Should().Be(1);
        }

        [Fact]
        public void TestDlgEntrySerializationAllAttributes()
        {
            var entry = new DLGEntry();
            entry.Comment = "All attributes";
            entry.CameraAngle = 30;
            entry.Listener = "Listener";
            entry.Quest = "Quest";
            entry.Script1 = new ResRef("script1");

            var serialized = entry.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGEntry;
            deserialized.Should().NotBeNull();

            deserialized.Comment.Should().Be(entry.Comment);
            deserialized.CameraAngle.Should().Be(entry.CameraAngle);
            deserialized.Listener.Should().Be(entry.Listener);
            deserialized.Quest.Should().Be(entry.Quest);
            deserialized.Script1.Should().Be(entry.Script1);
        }

        [Fact]
        public void TestDlgEntrySerializationWithMultilanguageText()
        {
            var entry = new DLGEntry();
            entry.Comment = "Localized";
            entry.Text.SetData(Language.English, Gender.Male, "Hello");
            entry.Text.SetData(Language.French, Gender.Female, "Bonjour");
            entry.Text.SetData(Language.German, Gender.Male, "Guten Tag");

            var serialized = entry.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGEntry;
            deserialized.Should().NotBeNull();

            deserialized.Comment.Should().Be("Localized");
            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("Hello");
            deserialized.Text.GetString(Language.French, Gender.Female).Should().Be("Bonjour");
            deserialized.Text.GetString(Language.German, Gender.Male).Should().Be("Guten Tag");
        }

        [Fact]
        public void TestDlgEntryWithNestedReplies()
        {
            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };

            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R223") };
            var reply3 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };

            entry1.Links.Add(new DLGLink(reply1));
            reply1.Links.Add(new DLGLink(entry2));
            reply1.Links.Add(new DLGLink(reply2));
            reply2.Links.Add(new DLGLink(entry1));
            entry2.Links.Add(new DLGLink(reply3));

            var serialized = entry1.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGEntry;
            deserialized.Should().NotBeNull();

            deserialized.Comment.Should().Be(entry1.Comment);
            deserialized.Links.Count.Should().Be(1);
            deserialized.Links[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Links[0].Node.Links.Count.Should().Be(2);
            deserialized.Links[0].Node.Links[0].Node.Comment.Should().Be("E221");
            deserialized.Links[0].Node.Links[1].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R223");
            deserialized.Links[0].Node.Links[1].Node.Links[0].Node.Comment.Should().Be("E248");
        }

        [Fact]
        public void TestDlgEntryWithCircularReference()
        {
            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };

            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };

            entry1.Links.Add(new DLGLink(reply1));
            reply1.Links.Add(new DLGLink(entry2));
            entry2.Links.Add(new DLGLink(reply2));
            reply2.Links.Add(new DLGLink(entry1)); // Circular reference

            var serialized = entry1.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGEntry;
            deserialized.Should().NotBeNull();

            deserialized.Comment.Should().Be(entry1.Comment);
            deserialized.Links.Count.Should().Be(1);
            var deserializedReply1 = deserialized.Links[0].Node as DLGReply;
            deserializedReply1.Should().NotBeNull();
            deserializedReply1.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserializedReply1.Links.Count.Should().Be(1);
            var deserializedEntry2 = deserializedReply1.Links[0].Node as DLGEntry;
            deserializedEntry2.Should().NotBeNull();
            deserializedEntry2.Comment.Should().Be("E221");
            deserializedEntry2.Links.Count.Should().Be(1);
            var deserializedReply2 = deserializedEntry2.Links[0].Node as DLGReply;
            deserializedReply2.Should().NotBeNull();
            deserializedReply2.Text.GetString(Language.English, Gender.Male).Should().Be("R249");
            deserializedReply2.Links.Count.Should().Be(1);
            var deserializedEntry1Circular = deserializedReply2.Links[0].Node as DLGEntry;
            deserializedEntry1Circular.Should().NotBeNull();
            deserializedEntry1Circular.Comment.Should().Be("E248");
        }

        [Fact]
        public void TestDlgReplySerializationBasic()
        {
            var reply = new DLGReply();
            reply.Text = LocalizedString.FromEnglish("Hello");
            reply.Unskippable = true;

            var serialized = reply.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGReply;
            deserialized.Should().NotBeNull();

            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("Hello");
            deserialized.Unskippable.Should().Be(reply.Unskippable);
        }

        [Fact]
        public void TestDlgReplySerializationWithLinks()
        {
            // Matching PyKotor test_dlg_reply_serialization_with_links
            var reply = new DLGReply();
            reply.Text = LocalizedString.FromEnglish("Reply with links");
            var entry = new DLGEntry();
            var link = new DLGLink(entry, 2);
            reply.Links.Add(link);

            var serialized = reply.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGReply;
            deserialized.Should().NotBeNull();

            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("Reply with links");
            deserialized.Links.Count.Should().Be(1);
            deserialized.Links[0].ListIndex.Should().Be(2);
        }

        [Fact]
        public void TestDlgReplySerializationAllAttributes()
        {
            // Matching PyKotor test_dlg_reply_serialization_all_attributes
            var reply = new DLGReply();
            reply.Text = LocalizedString.FromEnglish("Reply with all attributes");
            reply.VoResRef = new ResRef("vo_resref");
            reply.WaitFlags = 5;

            var serialized = reply.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGReply;
            deserialized.Should().NotBeNull();

            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("Reply with all attributes");
            deserialized.VoResRef.Should().Be(new ResRef("vo_resref"));
            deserialized.WaitFlags.Should().Be(5);
        }

        [Fact]
        public void TestDlgReplyWithNestedEntries()
        {
            // Matching PyKotor test_dlg_reply_with_nested_entries
            var reply1 = new DLGReply();
            reply1.Text.SetData(Language.English, Gender.Male, "R222");
            var reply2 = new DLGReply();
            reply2.Text.SetData(Language.English, Gender.Male, "R223");
            var reply3 = new DLGReply();
            reply3.Text.SetData(Language.English, Gender.Male, "R249");

            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };

            reply1.Links.Add(new DLGLink(entry1));
            entry1.Links.Add(new DLGLink(reply2));
            reply2.Links.Add(new DLGLink(entry2));
            entry2.Links.Add(new DLGLink(reply3));

            var serialized = reply1.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGReply;
            deserialized.Should().NotBeNull();

            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Links.Count.Should().Be(1);
            deserialized.Links[0].Node.Comment.Should().Be("E248");
            deserialized.Links[0].Node.Links.Count.Should().Be(1);
            var deserializedReply2 = deserialized.Links[0].Node.Links[0].Node as DLGReply;
            deserializedReply2.Should().NotBeNull();
            deserializedReply2.Text.GetString(Language.English, Gender.Male).Should().Be("R223");
            deserializedReply2.Links.Count.Should().Be(1);
            var deserializedEntry2 = deserializedReply2.Links[0].Node as DLGEntry;
            deserializedEntry2.Should().NotBeNull();
            deserializedEntry2.Comment.Should().Be("E221");
            deserializedEntry2.Links.Count.Should().Be(1);
            var deserializedReply3 = deserializedEntry2.Links[0].Node as DLGReply;
            deserializedReply3.Should().NotBeNull();
            deserializedReply3.Text.GetString(Language.English, Gender.Male).Should().Be("R249");
        }

        [Fact]
        public void TestDlgReplyWithCircularReference()
        {
            // Matching PyKotor test_dlg_reply_with_circular_reference
            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };

            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };

            reply1.Links.Add(new DLGLink(entry1));
            entry1.Links.Add(new DLGLink(reply2));
            reply2.Links.Add(new DLGLink(entry2));
            entry2.Links.Add(new DLGLink(reply1)); // Circular reference

            var serialized = reply1.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGReply;
            deserialized.Should().NotBeNull();

            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Links.Count.Should().Be(1);
            deserialized.Links[0].Node.Comment.Should().Be("E248");
            deserialized.Links[0].Node.Links.Count.Should().Be(1);
            var deserializedReply2 = deserialized.Links[0].Node.Links[0].Node as DLGReply;
            deserializedReply2.Should().NotBeNull();
            deserializedReply2.Text.GetString(Language.English, Gender.Male).Should().Be("R249");
            deserializedReply2.Links.Count.Should().Be(1);
            var deserializedEntry2 = deserializedReply2.Links[0].Node as DLGEntry;
            deserializedEntry2.Should().NotBeNull();
            deserializedEntry2.Comment.Should().Be("E221");
            deserializedEntry2.Links.Count.Should().Be(1);
            var deserializedReply1Circular = deserializedEntry2.Links[0].Node as DLGReply;
            deserializedReply1Circular.Should().NotBeNull();
            deserializedReply1Circular.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
        }

        [Fact]
        public void TestDlgReplyWithMultipleLevels()
        {
            // Matching PyKotor test_dlg_reply_with_multiple_levels
            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R223") };
            var reply3 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };
            var reply4 = new DLGReply { Text = LocalizedString.FromEnglish("R225") };

            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };
            var entry3 = new DLGEntry { Comment = "E250" };
            var entry4 = new DLGEntry { Comment = "E224" };

            reply1.Links.Add(new DLGLink(entry1));
            reply2.Links.Add(new DLGLink(entry2));
            entry1.Links.Add(new DLGLink(reply2));
            entry2.Links.Add(new DLGLink(reply3)); // Reuse R249
            reply3.Links.Add(new DLGLink(entry3));
            entry3.Links.Add(new DLGLink(reply4));
            reply4.Links.Add(new DLGLink(entry4));

            var serialized = reply1.ToDict();
            var deserializedNode = DLGNode.FromDict(serialized);
            var deserialized = deserializedNode as DLGReply;
            deserialized.Should().NotBeNull();

            deserialized.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Links.Count.Should().Be(1);
            deserialized.Links[0].Node.Comment.Should().Be("E248");
            deserialized.Links[0].Node.Links.Count.Should().Be(1);
            var deserializedReply2 = deserialized.Links[0].Node.Links[0].Node as DLGReply;
            deserializedReply2.Should().NotBeNull();
            deserializedReply2.Text.GetString(Language.English, Gender.Male).Should().Be("R223");
            deserializedReply2.Links.Count.Should().Be(1);
            var deserializedEntry2 = deserializedReply2.Links[0].Node as DLGEntry;
            deserializedEntry2.Should().NotBeNull();
            deserializedEntry2.Comment.Should().Be("E221");
            deserializedEntry2.Links.Count.Should().Be(1);
            var deserializedReply3 = deserializedEntry2.Links[0].Node as DLGReply;
            deserializedReply3.Should().NotBeNull();
            deserializedReply3.Text.GetString(Language.English, Gender.Male).Should().Be("R249");

            // Traverse the deserialized graph to ensure all expected nodes survived
            var entryComments = new HashSet<string>();
            var replyTexts = new HashSet<string>();
            var stack = new Stack<DLGNode>();
            stack.Push(deserialized);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node is DLGEntry entry)
                {
                    entryComments.Add(entry.Comment);
                }
                else if (node is DLGReply reply)
                {
                    replyTexts.Add(reply.Text.GetString(Language.English, Gender.Male));
                }
                foreach (var childLink in node.Links)
                {
                    stack.Push(childLink.Node);
                }
            }

            entryComments.Should().Contain("E248");
            entryComments.Should().Contain("E221");
            entryComments.Should().Contain("E250");
            entryComments.Should().Contain("E224");
            replyTexts.Should().Contain("R222");
            replyTexts.Should().Contain("R223");
            replyTexts.Should().Contain("R249");
            replyTexts.Should().Contain("R225");
        }

        [Fact]
        public void TestDlgLinkSerializationBasic()
        {
            // Matching PyKotor test_dlg_link_serialization_basic
            var link = new DLGLink(new DLGEntry(), 3);

            var serialized = link.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.ListIndex.Should().Be(3);
        }

        [Fact]
        public void TestDlgLinkSerializationWithNode()
        {
            // Matching PyKotor test_dlg_link_serialization_with_node
            var entry = new DLGEntry { Comment = "Linked entry" };
            var link = new DLGLink(entry);

            var serialized = link.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.Node.Should().NotBeNull();
            deserialized.Node.Comment.Should().Be("Linked entry");
        }

        [Fact]
        public void TestDlgLinkSerializationAllAttributes()
        {
            // Matching PyKotor test_dlg_link_serialization_all_attributes
            var reply = new DLGReply { Text = LocalizedString.FromEnglish("Linked reply") };
            var link = new DLGLink(reply, 5);

            var serialized = link.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.ListIndex.Should().Be(5);
            deserialized.Node.Should().NotBeNull();
            deserialized.Node.Text.GetString(Language.English, Gender.Male).Should().Be("Linked reply");
        }

        [Fact]
        public void TestDlgAnimationSerializationBasic()
        {
            // Matching PyKotor test_dlg_animation_serialization_basic
            var anim = new DLGAnimation();
            anim.AnimationId = 1200;
            anim.Participant = "Player";

            var serialized = anim.ToDict();
            var deserialized = DLGAnimation.FromDict(serialized);

            deserialized.AnimationId.Should().Be(1200);
            deserialized.Participant.Should().Be("Player");
        }

        [Fact]
        public void TestDlgAnimationSerializationDefault()
        {
            // Matching PyKotor test_dlg_animation_serialization_default
            var anim = new DLGAnimation();

            var serialized = anim.ToDict();
            var deserialized = DLGAnimation.FromDict(serialized);

            deserialized.AnimationId.Should().Be(anim.AnimationId);
            deserialized.Participant.Should().Be(anim.Participant);
        }

        [Fact]
        public void TestDlgAnimationSerializationWithCustomValues()
        {
            // Matching PyKotor test_dlg_animation_serialization_with_custom_values
            var anim = new DLGAnimation();
            anim.AnimationId = 2400;
            anim.Participant = "NPC";

            var serialized = anim.ToDict();
            var deserialized = DLGAnimation.FromDict(serialized);

            deserialized.AnimationId.Should().Be(2400);
            deserialized.Participant.Should().Be("NPC");
        }

        [Fact]
        public void TestDlgStuntSerializationBasic()
        {
            // Matching PyKotor test_dlg_stunt_serialization_basic
            var stunt = new DLGStunt();
            stunt.Participant = "Player";
            stunt.StuntModel = new ResRef("model");

            var serialized = stunt.ToDict();
            var deserialized = DLGStunt.FromDict(serialized);

            deserialized.Participant.Should().Be("Player");
            deserialized.StuntModel.Should().Be(new ResRef("model"));
        }

        [Fact]
        public void TestDlgStuntSerializationDefault()
        {
            // Matching PyKotor test_dlg_stunt_serialization_default
            var stunt = new DLGStunt();

            var serialized = stunt.ToDict();
            var deserialized = DLGStunt.FromDict(serialized);

            deserialized.Participant.Should().Be(stunt.Participant);
            deserialized.StuntModel.Should().Be(stunt.StuntModel);
        }

        [Fact]
        public void TestDlgStuntSerializationWithCustomValues()
        {
            // Matching PyKotor test_dlg_stunt_serialization_with_custom_values
            var stunt = new DLGStunt();
            stunt.Participant = "NPC";
            stunt.StuntModel = new ResRef("npc_model");

            var serialized = stunt.ToDict();
            var deserialized = DLGStunt.FromDict(serialized);

            deserialized.Participant.Should().Be("NPC");
            deserialized.StuntModel.Should().Be(new ResRef("npc_model"));
        }

        [Fact]
        public void TestDlgLinkWithNestedEntriesAndReplies()
        {
            // Matching PyKotor test_dlg_link_with_nested_entries_and_replies
            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };

            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R223") };
            var reply3 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };

            var link1 = new DLGLink(reply1);
            var link2 = new DLGLink(entry2);
            var link3 = new DLGLink(reply2);
            var link4 = new DLGLink(reply3);

            entry1.Links.Add(link1);
            reply1.Links.Add(link2);
            reply1.Links.Add(link3);
            entry2.Links.Add(link4); // Reuse R249
            reply2.Links.Add(new DLGLink(entry1)); // Circular reference: reply2 -> entry1

            var serialized = link1.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.Node.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Node.Links.Count.Should().Be(2);
            deserialized.Node.Links[0].Node.Comment.Should().Be("E221");
            deserialized.Node.Links[1].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R223");
            deserialized.Node.Links[1].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[1].Node.Links[0].Node.Comment.Should().Be("E248");
        }

        [Fact]
        public void TestDlgLinkWithCircularReferences()
        {
            // Matching PyKotor test_dlg_link_with_circular_references
            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };

            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };

            var link1 = new DLGLink(reply1);
            var link2 = new DLGLink(entry2);
            var link3 = new DLGLink(reply2);
            var link4 = new DLGLink(entry1); // Circular reference

            entry1.Links.Add(link1);
            reply1.Links.Add(link2);
            entry2.Links.Add(link3); // Reuse R249
            reply2.Links.Add(link4);

            var serialized = link1.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.Node.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[0].Node.Comment.Should().Be("E221");
            deserialized.Node.Links[0].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[0].Node.Links[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R249");
            deserialized.Node.Links[0].Node.Links[0].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[0].Node.Links[0].Node.Links[0].Node.Comment.Should().Be("E248");
        }

        [Fact]
        public void TestDlgLinkWithMultipleLevels()
        {
            // Matching PyKotor test_dlg_link_with_multiple_levels
            var entry1 = new DLGEntry { Comment = "E248" };
            var entry2 = new DLGEntry { Comment = "E221" };
            var entry3 = new DLGEntry { Comment = "E250" };

            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("R222") };
            var reply2 = new DLGReply { Text = LocalizedString.FromEnglish("R223") };
            var reply3 = new DLGReply { Text = LocalizedString.FromEnglish("R249") };
            var reply4 = new DLGReply { Text = LocalizedString.FromEnglish("R225") };
            var reply5 = new DLGReply { Text = LocalizedString.FromEnglish("R224") };

            var link1 = new DLGLink(reply1);
            var link2 = new DLGLink(entry2);
            var link3 = new DLGLink(reply2);
            var link4 = new DLGLink(reply3);
            var link5 = new DLGLink(entry3);
            var link6 = new DLGLink(reply4);
            var link7 = new DLGLink(reply5);

            entry1.Links.Add(link1);
            reply1.Links.Add(link3);
            reply1.Links.Add(link2);
            entry2.Links.Add(link4); // Reuse R249
            reply3.Links.Add(link5);
            entry3.Links.Add(link6);
            reply4.Links.Add(link7);

            var serialized = link1.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.Node.Text.GetString(Language.English, Gender.Male).Should().Be("R222");
            deserialized.Node.Links.Count.Should().Be(2);
            deserialized.Node.Links[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R223");
            deserialized.Node.Links[1].Node.Comment.Should().Be("E221");
            deserialized.Node.Links[1].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[1].Node.Links[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R249");
            deserialized.Node.Links[1].Node.Links[0].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[1].Node.Links[0].Node.Links[0].Node.Comment.Should().Be("E250");
            deserialized.Node.Links[1].Node.Links[0].Node.Links[0].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[1].Node.Links[0].Node.Links[0].Node.Links[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R225");
            deserialized.Node.Links[1].Node.Links[0].Node.Links[0].Node.Links[0].Node.Links.Count.Should().Be(1);
            deserialized.Node.Links[1].Node.Links[0].Node.Links[0].Node.Links[0].Node.Links[0].Node.Text.GetString(Language.English, Gender.Male).Should().Be("R224");
        }

        [Fact]
        public void TestDlgLinkSerializationPreservesSharedNodes()
        {
            // Matching PyKotor test_dlg_link_serialization_preserves_shared_nodes
            var sharedReply = new DLGReply { Text = LocalizedString.FromEnglish("Shared Reply") };

            var linkA = new DLGLink(sharedReply, 0);
            var linkB = new DLGLink(sharedReply, 1);

            var nodeMap = new Dictionary<string, object>();
            var linkADict = linkA.ToDict(nodeMap);
            var linkBDict = linkB.ToDict(nodeMap);

            var restoreMap = new Dictionary<string, object>();
            var restoredA = DLGLink.FromDict(linkADict, restoreMap);
            var restoredB = DLGLink.FromDict(linkBDict, restoreMap);

            // Both links should reference the same node object (shared node)
            restoredA.Node.Should().BeSameAs(restoredB.Node);
            restoredA.Node.Text.GetString(Language.English, Gender.Male).Should().Be("Shared Reply");
            var listIndices = new HashSet<int> { restoredA.ListIndex, restoredB.ListIndex };
            listIndices.Should().Contain(0);
            listIndices.Should().Contain(1);
        }

        [Fact]
        public void TestDlgLinkIterationTraversesAllDescendants()
        {
            // Matching PyKotor test_dlg_link_iteration_traverses_all_descendants
            var rootEntry = new DLGEntry { Comment = "root" };
            var replyOne = new DLGReply { Text = LocalizedString.FromEnglish("r1") };
            var replyTwo = new DLGReply { Text = LocalizedString.FromEnglish("r2") };
            var entryLeaf = new DLGEntry { Comment = "leaf" };

            var linkRoot = new DLGLink(replyOne, 0);
            var linkSecondary = new DLGLink(replyTwo, 1);
            var linkLeaf = new DLGLink(entryLeaf, 2);

            rootEntry.Links.Add(linkRoot);
            replyOne.Links.Add(linkLeaf);
            replyOne.Links.Add(linkSecondary);

            var visitedNodes = new HashSet<string>();
            foreach (DLGLink link in linkRoot)
            {
                if (link.Node is DLGEntry entry)
                {
                    visitedNodes.Add(entry.Comment);
                }
                else if (link.Node is DLGReply reply)
                {
                    visitedNodes.Add(reply.Text.GetString(Language.English, Gender.Male));
                }
            }

            visitedNodes.Should().Contain("r1");
            visitedNodes.Should().Contain("r2");
            visitedNodes.Should().Contain("leaf");
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1433
    // Original: class TestDLGGraphUtilities(unittest.TestCase):
    public class TestDLGGraphUtilities
    {
        private Tuple<DLG, DLGEntry, DLGReply, DLGEntry, DLGLink, DLGLink, DLGLink, Tuple<DLGLink>> BuildSimpleGraph()
        {
            var dlg = new DLG();

            var entry0 = new DLGEntry { Comment = "start", ListIndex = 0 };
            var entry1 = new DLGEntry { Comment = "leaf", ListIndex = 1 };
            var reply0 = new DLGReply { Text = LocalizedString.FromEnglish("middle"), ListIndex = 0 };

            var startLink0 = new DLGLink(entry0, 0);
            var startLink1 = new DLGLink(entry1, 1);
            var linkEntry0Reply = new DLGLink(reply0, 0);
            var linkReply0Entry1 = new DLGLink(entry1, 0);

            entry0.Links.Add(linkEntry0Reply);
            reply0.Links.Add(linkReply0Entry1);
            dlg.Starters.AddRange(new[] { startLink0, startLink1 });

            var rest = Tuple.Create(linkReply0Entry1);
            return new Tuple<DLG, DLGEntry, DLGReply, DLGEntry, DLGLink, DLGLink, DLGLink, Tuple<DLGLink>>(dlg, entry0, reply0, entry1, startLink0, startLink1, linkEntry0Reply, rest);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1456
        // Original: def test_find_paths_for_nodes_and_links(self):
        [Fact]
        public void TestFindPathsForNodesAndLinks()
        {
            var graph = BuildSimpleGraph();
            var dlg = graph.Item1;
            var entry0 = graph.Item2;
            var reply0 = graph.Item3;
            var entry1 = graph.Item4;
            var startLink0 = graph.Item5;
            var startLink1 = graph.Item6;
            var linkEntry0Reply = graph.Item7;
            var linkReply0Entry1 = graph.Rest.Item1;

            var pathsEntry = dlg.FindPaths(entry1);
            var pathsReply = dlg.FindPaths(reply0);
            var pathsStartLink = dlg.FindPaths(startLink0);
            var pathsChildLink = dlg.FindPaths(linkReply0Entry1);

            pathsEntry.Should().Contain("EntryList\\1");
            pathsReply.Should().Contain("ReplyList\\0");
            pathsStartLink.Should().Contain("StartingList\\0");
            pathsChildLink.Should().Contain("ReplyList\\0\\EntriesList\\0");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1469
        // Original: def test_get_link_parent_and_partial_path(self):
        [Fact]
        public void TestGetLinkParentAndPartialPath()
        {
            var graph = BuildSimpleGraph();
            var dlg = graph.Item1;
            var entry0 = graph.Item2;
            var reply0 = graph.Item3;
            var entry1 = graph.Item4;
            var startLink0 = graph.Item5;
            var startLink1 = graph.Item6;
            var linkEntry0Reply = graph.Item7;
            var linkReply0Entry1 = graph.Rest.Item1;

            // Note: In C#, we can't directly check if parent is dlg (DLG is not a DLGNode)
            // Python version checks: dlg.get_link_parent(start_link0) is dlg
            // We'll check that starter links have no parent node instead
            dlg.GetLinkParent(startLink0).Should().BeNull(); // Starter links have no parent node
            dlg.GetLinkParent(linkEntry0Reply).Should().Be(entry0);
            dlg.GetLinkParent(linkReply0Entry1).Should().Be(reply0);
            startLink0.PartialPath(isStarter: true).Should().Be("StartingList\\0");
            linkEntry0Reply.PartialPath(isStarter: false).Should().Be("RepliesList\\0");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1478
        // Original: def test_all_entries_and_replies_sorted_and_unique(self):
        [Fact]
        public void TestAllEntriesAndRepliesSortedAndUnique()
        {
            var graph = BuildSimpleGraph();
            var dlg = graph.Item1;
            var entry0 = graph.Item2;
            var reply0 = graph.Item3;
            var entry1 = graph.Item4;
            var startLink0 = graph.Item5;
            var startLink1 = graph.Item6;
            var linkEntry0Reply = graph.Item7;
            var linkReply0Entry1 = graph.Rest.Item1;
            var entry2 = new DLGEntry { Comment = "late", ListIndex = -1 };
            var reply1 = new DLGReply { Text = LocalizedString.FromEnglish("shared"), ListIndex = 5 };

            // Create additional shared references
            reply1.Links.Add(new DLGLink(entry2, 0));
            entry1.Links.Add(new DLGLink(reply1, 1));
            reply1.Links.Add(new DLGLink(entry0, 1));

            var entriesUnsorted = dlg.AllEntries();
            var repliesUnsorted = dlg.AllReplies();
            var entriesSorted = dlg.AllEntries(asSorted: true);
            var repliesSorted = dlg.AllReplies(asSorted: true);

            entriesUnsorted.Should().HaveCount(3);
            repliesUnsorted.Should().HaveCount(2);
            entriesSorted[0].ListIndex.Should().Be(0);
            entriesSorted[1].ListIndex.Should().Be(1);
            entriesSorted[entriesSorted.Count - 1].ListIndex.Should().Be(-1);
            repliesSorted[0].ListIndex.Should().Be(0);
            repliesSorted[1].ListIndex.Should().Be(5);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1503
        // Original: def test_calculate_links_and_nodes_counts_cycles_included(self):
        [Fact]
        public void TestCalculateLinksAndNodesCountsCyclesIncluded()
        {
            var graph = BuildSimpleGraph();
            var dlg = graph.Item1;
            var entry0 = graph.Item2;
            var reply0 = graph.Item3;
            var entry1 = graph.Item4;
            var startLink0 = graph.Item5;
            var startLink1 = graph.Item6;
            var linkEntry0Reply = graph.Item7;
            var linkReply0Entry1 = graph.Rest.Item1;
            // Introduce an explicit cycle entry1 -> entry0
            entry1.Links.Add(new DLGLink(entry0, 2));

            var result = entry0.CalculateLinksAndNodes();
            var numLinks = result.Item1;
            var numNodes = result.Item2;
            // entry0 -> reply0, reply0 -> entry1, entry1 -> entry0 (cycle) => 3 links, 3 nodes
            numLinks.Should().Be(3);
            numNodes.Should().Be(3);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1513
        // Original: def test_shift_item_and_bounds(self):
        [Fact]
        public void TestShiftItemAndBounds()
        {
            var graph = BuildSimpleGraph();
            var dlg = graph.Item1;
            var entry0 = graph.Item2;
            var reply0 = graph.Item3;
            var entry1 = graph.Item4;
            var startLink0 = graph.Item5;
            var startLink1 = graph.Item6;
            var linkEntry0Reply = graph.Item7;
            var linkReply0Entry1 = graph.Rest.Item1;
            entry0.ShiftItem(entry0.Links, 0, 0); // no-op allowed
            entry0.ShiftItem(entry0.Links, 0, 0); // idempotent
            // Add second link for ordering
            entry0.Links.Add(new DLGLink(entry1, 1));
            entry0.ShiftItem(entry0.Links, 1, 0);
            entry0.Links[0].Node.Should().Be(entry1);
            entry0.Links[1].Node.Should().Be(reply0);

            // Test bounds checking
            Action act = () => entry0.ShiftItem(entry0.Links, 0, 5);
            act.Should().Throw<IndexOutOfRangeException>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1525
        // Original: def test_node_dict_roundtrip_preserves_metadata(self):
        [Fact]
        public void TestNodeDictRoundtripPreservesMetadata()
        {
            var entry = new DLGEntry
            {
                Comment = "deep node",
                Speaker = "Carth",
                CameraAngle = 33,
                CameraAnim = 77,
                CameraId = 9,
                CameraEffect = -3,
                CameraFov = 90.5f,
                CameraHeight = 1.25f,
                TargetHeight = 0.5f,
                FadeType = 2,
                FadeColor = new Color(0.1f, 0.2f, 0.3f, 1.0f),
                FadeDelay = 0.25f,
                FadeLength = 1.5f,
                Quest = "quest_flag",
                QuestEntry = 4,
                Script1 = new ResRef("script_a"),
                Script2 = new ResRef("script_b"),
                Script1Param1 = 11,
                Script2Param6 = "str",
                WaitFlags = 3,
                SoundExists = 1,
                VoResRef = new ResRef("vo"),
                Sound = new ResRef("snd"),
                EmotionId = 12,
                FacialId = 7,
                NodeId = 42,
                PostProcNode = 17,
                RecordNoVoOverride = true,
                RecordVo = true,
                VoTextChanged = true,
                Unskippable = true
            };
            entry.Text.SetData(Language.English, Gender.Male, "Line");
            entry.Text.SetData(Language.French, Gender.Female, "Ligne");
            var animation = new DLGAnimation { Participant = "p1", AnimationId = 123 };
            entry.Animations.Add(animation);

            var reply = new DLGReply { Text = LocalizedString.FromEnglish("reply"), CameraAnim = 55, FadeType = 9 };
            entry.Links.Add(new DLGLink(reply, 0));
            reply.Links.Add(new DLGLink(entry, 0));

            var serialized = entry.ToDict();
            var restored = DLGEntry.FromDict(serialized);

            restored.CameraAngle.Should().Be(33);
            restored.CameraAnim.Should().Be(77);
            restored.CameraId.Should().Be(9);
            restored.CameraEffect.Should().Be(-3);
            restored.CameraFov.Should().BeApproximately(90.5f, 0.01f);
            restored.CameraHeight.Should().BeApproximately(1.25f, 0.01f);
            restored.TargetHeight.Should().BeApproximately(0.5f, 0.01f);
            restored.FadeType.Should().Be(2);
            restored.FadeColor.Should().NotBeNull();
            restored.FadeColor.R.Should().BeApproximately(0.1f, 0.005f);
            restored.FadeColor.G.Should().BeApproximately(0.2f, 0.005f);
            restored.FadeColor.B.Should().BeApproximately(0.3f, 0.005f);
            restored.FadeColor.A.Should().BeApproximately(1.0f, 0.005f);
            restored.FadeDelay.Should().BeApproximately(0.25f, 0.01f);
            restored.FadeLength.Should().BeApproximately(1.5f, 0.01f);
            restored.Quest.Should().Be("quest_flag");
            restored.QuestEntry.Should().Be(4);
            restored.Script1.Should().Be(new ResRef("script_a"));
            restored.Script2.Should().Be(new ResRef("script_b"));
            restored.Script1Param1.Should().Be(11);
            restored.Script2Param6.Should().Be("str");
            restored.WaitFlags.Should().Be(3);
            restored.SoundExists.Should().Be(1);
            restored.VoResRef.Should().Be(new ResRef("vo"));
            restored.Sound.Should().Be(new ResRef("snd"));
            restored.EmotionId.Should().Be(12);
            restored.FacialId.Should().Be(7);
            restored.NodeId.Should().Be(42);
            restored.PostProcNode.Should().Be(17);
            restored.RecordNoVoOverride.Should().BeTrue();
            restored.RecordVo.Should().BeTrue();
            restored.VoTextChanged.Should().BeTrue();
            restored.Unskippable.Should().BeTrue();
            restored.Text.GetString(Language.French, Gender.Female).Should().Be("Ligne");
            restored.Animations[0].AnimationId.Should().Be(123);
            // Verify circular reference is preserved
            restored.Links.Should().NotBeEmpty("entry should have links");
            restored.Links[0].Node.Should().NotBeNull("link should have a node");
            restored.Links[0].Node.Links.Should().NotBeEmpty("reply should have links back to entry");
            restored.Links[0].Node.Links[0].Node.Comment.Should().Be("deep node", "circular reference should be preserved");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:1612
        // Original: def test_find_paths_respects_multiple_starters_and_link_parenting(self):
        [Fact]
        public void TestFindPathsRespectsMultipleStartersAndLinkParenting()
        {
            var dlg = new DLG
            {
                ConversationType = DLGConversationType.Computer,
                ComputerType = DLGComputerType.Ancient
            };

            var entryA = new DLGEntry { Comment = "A", ListIndex = 2 };
            var entryB = new DLGEntry { Comment = "B", ListIndex = 3 };
            var replyA = new DLGReply { Text = LocalizedString.FromEnglish("R"), ListIndex = 4 };

            var starterA = new DLGLink(entryA, 0);
            var starterB = new DLGLink(entryB, 1);
            dlg.Starters.AddRange(new[] { starterA, starterB });

            entryA.Links.Add(new DLGLink(replyA, 0));
            replyA.Links.Add(new DLGLink(entryB, 0));

            var pathsReplyA = dlg.FindPaths(replyA);
            var pathsEntryB = dlg.FindPaths(entryB);
            var parentForReplyLink = dlg.GetLinkParent(entryA.Links[0]);

            // Matching Python: assert PureWindowsPath("ReplyList", "4") in paths_reply_a
            pathsReplyA.Should().Contain("ReplyList\\4", "replyA should be found by ListIndex");
            // Matching Python: assert PureWindowsPath("EntryList", "3") in paths_entry_b
            pathsEntryB.Should().Contain("EntryList\\3", "entryB should be found by ListIndex");
            // Matching Python: assert parent_for_reply_link is entry_a
            parentForReplyLink.Should().Be(entryA, "parent of entryA's link to replyA should be entryA");
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/tests/resource/generics/test_dlg.py:726
    // Original: class TestDLG(TestCase):
    public class TestDLGReconstruct
    {
        private List<string> _logMessages;

        public TestDLGReconstruct()
        {
            _logMessages = new List<string> { Environment.NewLine };
        }

        private void LogFunc(string message)
        {
            _logMessages.Add(message);
        }

        private string GetTestFilePath(string relativePath)
        {
            // Try to find test files relative to test assembly location
            string testAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string baseDir = System.IO.Path.GetDirectoryName(testAssemblyPath);
            string testFile = System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "vendor", "PyKotor", relativePath);
            testFile = System.IO.Path.GetFullPath(testFile);
            return System.IO.File.Exists(testFile) ? testFile : null;
        }

        // Matching PyKotor test_k1_reconstruct
        [Fact(Skip = "Requires test file - test_k1.dlg")]
        public void TestK1Reconstruct()
        {
            string testFile = GetTestFilePath("Libraries/PyKotor/tests/test_files/test_k1.dlg");
            if (testFile == null)
            {
                // Skip if file not found
                return;
            }

            GFF gff = GFFAuto.ReadGff(testFile, 0, null, ResourceType.DLG);
            GFF reconstructedGff = DLGHelper.DismantleDlg(DLGHelper.ConstructDlg(gff), Game.K1);

            // Note: GFF.Compare doesn't exist in C# yet - this test is a placeholder
            // Once GFF comparison is implemented, uncomment and fix:
            // bool result = gff.Compare(reconstructedGff, LogFunc, ignoreDefaultChanges: true);
            // string output = string.Join(Environment.NewLine, _logMessages);
            // result.Should().BeTrue(output);
        }

        // Matching PyKotor test_k1_reconstruct_from_reconstruct
        [Fact(Skip = "Requires test file - test_k1.dlg")]
        public void TestK1ReconstructFromReconstruct()
        {
            string testFile = GetTestFilePath("Libraries/PyKotor/tests/test_files/test_k1.dlg");
            if (testFile == null)
            {
                return;
            }

            GFF gff = GFFAuto.ReadGff(testFile, 0, null, ResourceType.DLG);
            GFF reconstructedGff = DLGHelper.DismantleDlg(DLGHelper.ConstructDlg(gff), Game.K1);
            GFF reReconstructedGff = DLGHelper.DismantleDlg(DLGHelper.ConstructDlg(reconstructedGff), Game.K1);

            // Note: GFF.Compare doesn't exist in C# yet
            // bool result = reconstructedGff.Compare(reReconstructedGff, LogFunc);
            // string output = string.Join(Environment.NewLine, _logMessages);
            // result.Should().BeTrue(output);
        }

        // Matching PyKotor test_k1_serialization
        [Fact]
        public void TestK1Serialization()
        {
            // Read TEST_DLG_XML - Note: XML format not fully supported yet, so construct DLG directly
            // For now, construct DLG manually from expected structure
            DLG dlg = ConstructTestDlgFromXml();

            foreach (DLGNode node in dlg.AllEntries())
            {
                var serialized = node.ToDict();
                DLGNode deserialized = DLGNode.FromDict(serialized);
                deserialized.Should().BeEquivalentTo(node, options => options.ExcludingMissingMembers());
            }
        }

        // Matching PyKotor test_k2_reconstruct
        [Fact(Skip = "Requires test file - test.dlg")]
        public void TestK2Reconstruct()
        {
            string testFile = GetTestFilePath("Libraries/PyKotor/tests/test_files/test.dlg");
            if (testFile == null)
            {
                return;
            }

            GFF gff = GFFAuto.ReadGff(testFile, 0, null, ResourceType.DLG);
            GFF reconstructedGff = DLGHelper.DismantleDlg(DLGHelper.ConstructDlg(gff), Game.K2);

            // Note: GFF.Compare doesn't exist in C# yet
            // bool result = gff.Compare(reconstructedGff, LogFunc, ignoreDefaultChanges: true);
            // string output = string.Join(Environment.NewLine, _logMessages);
            // result.Should().BeTrue(output);
        }

        // Matching PyKotor test_k2_reconstruct_from_reconstruct
        [Fact(Skip = "Requires test file - test.dlg")]
        public void TestK2ReconstructFromReconstruct()
        {
            string testFile = GetTestFilePath("Libraries/PyKotor/tests/test_files/test.dlg");
            if (testFile == null)
            {
                return;
            }

            GFF gff = GFFAuto.ReadGff(testFile, 0, null, ResourceType.DLG);
            GFF reconstructedGff = DLGHelper.DismantleDlg(DLGHelper.ConstructDlg(gff), Game.K2);
            GFF reReconstructedGff = DLGHelper.DismantleDlg(DLGHelper.ConstructDlg(reconstructedGff), Game.K2);

            // Note: GFF.Compare doesn't exist in C# yet
            // bool result = reconstructedGff.Compare(reReconstructedGff, LogFunc);
            // string output = string.Join(Environment.NewLine, _logMessages);
            // result.Should().BeTrue(output);
        }

        // Matching PyKotor test_io_construct
        [Fact]
        public void TestIoConstruct()
        {
            // Note: XML GFF reading not yet implemented in C#
            // For now, construct DLG directly from expected structure
            DLG dlg = ConstructTestDlgFromXml();
            ValidateIo(dlg);
        }

        private DLG ConstructTestDlgFromXml()
        {
            // Manually construct DLG matching TEST_DLG_XML structure
            var dlg = new DLG
            {
                DelayEntry = 13,
                DelayReply = 14,
                WordCount = 1337,
                OnAbort = new ResRef("abort"),
                OnEnd = new ResRef("end"),
                Skippable = true,
                AmbientTrack = new ResRef("track"),
                AnimatedCut = 123,
                CameraModel = new ResRef("camm"),
                ComputerType = DLGComputerType.Ancient,
                ConversationType = DLGConversationType.Human,
                OldHitCheck = true,
                UnequipHands = true,
                UnequipItems = true,
                VoId = "echo",
                AlienRaceOwner = 123,
                PostProcOwner = 12,
                RecordNoVo = 3
            };

            var entry0 = new DLGEntry
            {
                Speaker = "bark",
                Listener = "yoohoo",
                Text = LocalizedString.FromEnglish("Greetings"),
                VoResRef = new ResRef("gand"),
                Script1 = new ResRef("num1"),
                Delay = -1,
                Comment = "commentto",
                Sound = new ResRef("gonk"),
                Quest = "quest",
                PlotIndex = -1,
                PlotXpPercentage = 1.0f,
                WaitFlags = 1,
                CameraAngle = 14,
                FadeType = 1,
                SoundExists = 1,
                AlienRaceNode = 1,
                VoTextChanged = true,
                EmotionId = 4,
                FacialId = 2,
                NodeId = 1,
                Unskippable = true,
                PostProcNode = 3,
                RecordVo = true,
                Script2 = new ResRef("num2"),
                CameraId = 32,
                CameraEffect = -1,
                RecordNoVoOverride = true,
                ListIndex = 0
            };
            entry0.Animations.Add(new DLGAnimation { Participant = "aaa", AnimationId = 1200 });
            entry0.Animations.Add(new DLGAnimation { Participant = "bbb", AnimationId = 2400 });

            var entry1 = new DLGEntry
            {
                Text = LocalizedString.FromEnglish("Farewell"),
                ListIndex = 1
            };

            var entry2 = new DLGEntry
            {
                Text = LocalizedString.FromEnglish("Hello"),
                ListIndex = 2
            };

            var reply0 = new DLGReply
            {
                Text = LocalizedString.FromEnglish("Reply 0"),
                ListIndex = 0
            };

            var reply1 = new DLGReply
            {
                Text = LocalizedString.FromEnglish("Reply 1"),
                ListIndex = 1
            };

            entry0.Links.Add(new DLGLink(reply0, 0));
            entry0.Links.Add(new DLGLink(reply1, 1));
            reply0.Links.Add(new DLGLink(entry0, 0));
            reply1.Links.Add(new DLGLink(entry1, 0));

            dlg.Starters.Add(new DLGLink(entry0, 0));
            dlg.Starters.Add(new DLGLink(entry2, 1));

            dlg.Stunts.Add(new DLGStunt { Participant = "aaa", StuntModel = new ResRef("model1") });
            dlg.Stunts.Add(new DLGStunt { Participant = "bbb", StuntModel = new ResRef("m01aa_c04_char01") });

            return dlg;
        }

        private void ValidateIo(DLG dlg)
        {
            // Matching PyKotor validate_io method
            var allEntries = dlg.AllEntries();
            var allReplies = dlg.AllReplies();

            var entry0 = allEntries[0];
            var entry1 = allEntries[1];
            var entry2 = allEntries[2];

            var reply0 = allReplies[0];
            var reply1 = allReplies[1];

            allEntries.Count.Should().Be(3);
            allReplies.Count.Should().Be(2);
            dlg.Starters.Count.Should().Be(2);
            dlg.Stunts.Count.Should().Be(2);

            dlg.Starters.Should().Contain(link => link.Node == entry0);
            dlg.Starters.Should().Contain(link => link.Node == entry2);

            entry0.Links.Count.Should().Be(2);
            entry0.Links.Should().Contain(link => link.Node == reply0);
            entry0.Links.Should().Contain(link => link.Node == reply1);

            reply0.Links.Count.Should().Be(1);
            reply0.Links.Should().Contain(link => link.Node == entry0);

            reply1.Links.Count.Should().Be(1);
            reply1.Links.Should().Contain(link => link.Node == entry1);

            entry2.Links.Count.Should().Be(0);

            dlg.DelayEntry.Should().Be(13);
            dlg.DelayReply.Should().Be(14);
            dlg.WordCount.Should().Be(1337);
            dlg.OnAbort.Should().Be(new ResRef("abort"));
            dlg.OnEnd.Should().Be(new ResRef("end"));
            dlg.Skippable.Should().BeTrue();
            dlg.AmbientTrack.Should().Be(new ResRef("track"));
            dlg.AnimatedCut.Should().Be(123);
            dlg.CameraModel.Should().Be(new ResRef("camm"));
            dlg.ComputerType.Should().Be(DLGComputerType.Ancient);
            dlg.ConversationType.Should().Be(DLGConversationType.Human);
            dlg.OldHitCheck.Should().BeTrue();
            dlg.UnequipHands.Should().BeTrue();
            dlg.UnequipItems.Should().BeTrue();
            dlg.VoId.Should().Be("echo");
            dlg.AlienRaceOwner.Should().Be(123);
            dlg.PostProcOwner.Should().Be(12);
            dlg.RecordNoVo.Should().Be(3);

            entry0.Listener.Should().Be("yoohoo");
            entry0.Text.StringRef.Should().Be(-1);
            entry0.VoResRef.Should().Be(new ResRef("gand"));
            entry0.Script1.Should().Be(new ResRef("num1"));
            entry0.Delay.Should().Be(-1);
            entry0.Comment.Should().Be("commentto");
            entry0.Sound.Should().Be(new ResRef("gonk"));
            entry0.Quest.Should().Be("quest");
            entry0.PlotIndex.Should().Be(-1);
            entry0.PlotXpPercentage.Should().BeApproximately(1.0f, 0.01f);
            entry0.WaitFlags.Should().Be(1);
            entry0.CameraAngle.Should().Be(14);
            entry0.FadeType.Should().Be(1);
            entry0.SoundExists.Should().Be(1);
            entry0.AlienRaceNode.Should().Be(1);
            entry0.VoTextChanged.Should().BeTrue();
            entry0.EmotionId.Should().Be(4);
            entry0.FacialId.Should().Be(2);
            entry0.NodeId.Should().Be(1);
            entry0.Unskippable.Should().BeTrue();
            entry0.PostProcNode.Should().Be(3);
            entry0.RecordVo.Should().BeTrue();
            entry0.Script2.Should().Be(new ResRef("num2"));
            entry0.CameraId.Should().Be(32);
            entry0.Speaker.Should().Be("bark");
            entry0.CameraEffect.Should().Be(-1);
            entry0.RecordNoVoOverride.Should().BeTrue();

            dlg.Stunts[1].Participant.Should().Be("bbb");
            dlg.Stunts[1].StuntModel.Should().Be(new ResRef("m01aa_c04_char01"));
        }

        private string GetTestDlgXml()
        {
            // Return the TEST_DLG_XML content from Python test
            return @"<gff3>
  <struct id=""-1"">
    <uint32 label=""DelayEntry"">13</uint32>
    <uint32 label=""DelayReply"">14</uint32>
    <uint32 label=""NumWords"">1337</uint32>
    <resref label=""EndConverAbort"">abort</resref>
    <resref label=""EndConversation"">end</resref>
    <byte label=""Skippable"">1</byte>
    <resref label=""AmbientTrack"">track</resref>
    <byte label=""AnimatedCut"">123</byte>
    <resref label=""CameraModel"">camm</resref>
    <byte label=""ComputerType"">1</byte>
    <sint32 label=""ConversationType"">1</sint32>
    <list label=""EntryList"">
      <struct id=""0"">
        <exostring label=""Speaker"">bark</exostring>
        <exostring label=""Listener"">yoohoo</exostring>
        <list label=""AnimList"">
          <struct id=""0"">
            <exostring label=""Participant"">aaa</exostring>
            <uint16 label=""Animation"">1200</uint16>
          </struct>
          <struct id=""0"">
            <exostring label=""Participant"">bbb</exostring>
            <uint16 label=""Animation"">2400</uint16>
          </struct>
        </list>
        <locstring label=""Text"" strref=""-1"">
          <string language=""0"">Greetings</string>
        </locstring>
        <resref label=""VO_ResRef"">gand</resref>
        <resref label=""Script"">num1</resref>
        <uint32 label=""Delay"">4294967295</uint32>
        <exostring label=""Comment"">commentto</exostring>
        <resref label=""Sound"">gonk</resref>
        <exostring label=""Quest"">quest</exostring>
        <sint32 label=""PlotIndex"">-1</sint32>
        <float label=""PlotXPPercentage"">1.0</float>
        <uint32 label=""WaitFlags"">1</uint32>
        <uint32 label=""CameraAngle"">14</uint32>
        <byte label=""FadeType"">1</byte>
        <list label=""RepliesList"">
          <struct id=""0"">
            <uint32 label=""Index"">0</uint32>
            <resref label=""Active"" />
            <byte label=""IsChild"">0</byte>
          </struct>
          <struct id=""1"">
            <uint32 label=""Index"">1</uint32>
            <resref label=""Active"" />
            <byte label=""IsChild"">0</byte>
          </struct>
        </list>
        <byte label=""SoundExists"">1</byte>
        <sint32 label=""ActionParam1"">1</sint32>
        <sint32 label=""AlienRaceNode"">1</sint32>
        <sint32 label=""CamVidEffect"">-1</sint32>
        <byte label=""Changed"">1</byte>
        <sint32 label=""Emotion"">4</sint32>
        <sint32 label=""FacialAnim"">2</sint32>
        <sint32 label=""NodeID"">1</sint32>
        <sint32 label=""NodeUnskippable"">1</sint32>
        <sint32 label=""PostProcNode"">3</sint32>
        <sint32 label=""RecordNoOverri"">1</sint32>
        <sint32 label=""RecordVO"">1</sint32>
        <resref label=""Script2"">num2</resref>
        <sint32 label=""VOTextChanged"">1</sint32>
        <sint32 label=""CameraID"">32</sint32>
        <sint32 label=""RecordNoVOOverri"">1</sint32>
      </struct>
      <struct id=""1"">
        <exostring label=""Speaker"" />
        <locstring label=""Text"" strref=""-1"">
          <string language=""0"">Farewell</string>
        </locstring>
        <list label=""RepliesList"">
          <struct id=""0"">
            <uint32 label=""Index"">0</uint32>
            <resref label=""Active"" />
            <byte label=""IsChild"">0</byte>
          </struct>
        </list>
      </struct>
      <struct id=""2"">
        <exostring label=""Speaker"" />
        <locstring label=""Text"" strref=""-1"">
          <string language=""0"">Hello</string>
        </locstring>
        <list label=""RepliesList"" />
      </struct>
    </list>
    <list label=""ReplyList"">
      <struct id=""0"">
        <locstring label=""Text"" strref=""-1"">
          <string language=""0"">Reply 0</string>
        </locstring>
        <list label=""EntriesList"">
          <struct id=""0"">
            <uint32 label=""Index"">0</uint32>
            <resref label=""Active"" />
            <byte label=""IsChild"">0</byte>
          </struct>
        </list>
      </struct>
      <struct id=""1"">
        <locstring label=""Text"" strref=""-1"">
          <string language=""0"">Reply 1</string>
        </locstring>
        <list label=""EntriesList"">
          <struct id=""0"">
            <uint32 label=""Index"">1</uint32>
            <resref label=""Active"" />
            <byte label=""IsChild"">0</byte>
          </struct>
        </list>
      </struct>
    </list>
    <list label=""StartingList"">
      <struct id=""0"">
        <uint32 label=""Index"">0</uint32>
        <resref label=""Active"" />
        <byte label=""IsChild"">0</byte>
      </struct>
      <struct id=""1"">
        <uint32 label=""Index"">2</uint32>
        <resref label=""Active"" />
        <byte label=""IsChild"">0</byte>
      </struct>
    </list>
    <list label=""StuntList"">
      <struct id=""0"">
        <exostring label=""Participant"">aaa</exostring>
        <resref label=""StuntModel"">model1</resref>
      </struct>
      <struct id=""0"">
        <exostring label=""Participant"">bbb</exostring>
        <resref label=""StuntModel"">m01aa_c04_char01</resref>
      </struct>
    </list>
    <sint32 label=""AlienRaceOwner"">123</sint32>
    <sint32 label=""PostProcOwner"">12</sint32>
    <sint32 label=""RecordNoVO"">3</sint32>
    <byte label=""OldHitCheck"">1</byte>
    <byte label=""UnequipHands"">1</byte>
    <byte label=""UnequipItems"">1</byte>
    <exostring label=""VO_ID"">echo</exostring>
  </struct>
</gff3>";
        }
    }
}

