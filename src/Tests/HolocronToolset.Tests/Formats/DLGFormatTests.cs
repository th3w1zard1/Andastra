using System;
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
            gff.Root.Acquire("NumWords", 0).Should().Be(100);
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
            gff.Root.Acquire("NumWords", 0).Should().Be(200);
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
        public void TestDlgLinkSerialization()
        {
            var entry = new DLGEntry { Comment = "Test Entry" };
            var link = new DLGLink(entry, 0);
            link.Active1 = new ResRef("test_script");
            link.Comment = "Link comment";

            var serialized = link.ToDict();
            var deserialized = DLGLink.FromDict(serialized);

            deserialized.ListIndex.Should().Be(link.ListIndex);
            deserialized.Active1.Should().Be(link.Active1);
            deserialized.Comment.Should().Be(link.Comment);
            deserialized.Node.Should().NotBeNull();
            (deserialized.Node as DLGEntry).Comment.Should().Be("Test Entry");
        }

        [Fact]
        public void TestDlgAnimationSerialization()
        {
            var anim = new DLGAnimation();
            anim.AnimationId = 123;
            anim.Participant = "test_participant";

            var serialized = anim.ToDict();
            var deserialized = DLGAnimation.FromDict(serialized);

            deserialized.AnimationId.Should().Be(anim.AnimationId);
            deserialized.Participant.Should().Be(anim.Participant);
        }

        [Fact]
        public void TestDlgStuntSerialization()
        {
            var stunt = new DLGStunt();
            stunt.Participant = "test_participant";
            stunt.StuntModel = new ResRef("test_model");

            var serialized = stunt.ToDict();
            var deserialized = DLGStunt.FromDict(serialized);

            deserialized.Participant.Should().Be(stunt.Participant);
            deserialized.StuntModel.Should().Be(stunt.StuntModel);
        }
    }
}

