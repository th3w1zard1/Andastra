using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resources;
using JetBrains.Annotations;

namespace AuroraEngine.Common.Resource.Generics.DLG
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py
    // Original: construct_dlg, dismantle_dlg, read_dlg, bytes_dlg functions
    public static class DLGHelper
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:30-305
        // Original: def construct_dlg(gff: GFF) -> DLG:
        public static DLG ConstructDlg(GFF gff)
        {
            var dlg = new DLG();

            GFFStruct root = gff.Root;

            GFFList entryList = root.Acquire("EntryList", new GFFList());
            GFFList replyList = root.Acquire("ReplyList", new GFFList());

            var allEntries = new List<DLGEntry>();
            for (int i = 0; i < entryList.Count; i++)
            {
                allEntries.Add(new DLGEntry());
            }

            var allReplies = new List<DLGReply>();
            for (int i = 0; i < replyList.Count; i++)
            {
                allReplies.Add(new DLGReply());
            }

            // Dialog metadata
            dlg.WordCount = root.Acquire("NumWords", 0);
            dlg.OnAbort = root.Acquire("EndConverAbort", ResRef.FromBlank());
            dlg.OnEnd = root.Acquire("EndConversation", ResRef.FromBlank());
            dlg.Skippable = root.Acquire("Skippable", (byte)0) != 0;
            dlg.AmbientTrack = root.Acquire("AmbientTrack", ResRef.FromBlank());
            dlg.AnimatedCut = root.Acquire("AnimatedCut", 0);
            dlg.CameraModel = root.Acquire("CameraModel", ResRef.FromBlank());
            dlg.ComputerType = (DLGComputerType)root.Acquire("ComputerType", (uint)0);
            dlg.ConversationType = (DLGConversationType)root.Acquire("ConversationType", (uint)0);
            dlg.OldHitCheck = root.Acquire("OldHitCheck", (byte)0) != 0;
            dlg.UnequipHands = root.Acquire("UnequipHItem", (byte)0) != 0;
            dlg.UnequipItems = root.Acquire("UnequipItems", (byte)0) != 0;
            dlg.VoId = root.Acquire("VO_ID", string.Empty);
            dlg.AlienRaceOwner = root.Acquire("AlienRaceOwner", 0);
            dlg.PostProcOwner = root.Acquire("PostProcOwner", 0);
            dlg.RecordNoVo = root.Acquire("RecordNoVO", 0);
            dlg.NextNodeId = root.Acquire("NextNodeID", 0);
            dlg.DelayEntry = root.Acquire("DelayEntry", 0);
            dlg.DelayReply = root.Acquire("DelayReply", 0);

            // StuntList
            GFFList stuntList = root.Acquire("StuntList", new GFFList());
            foreach (GFFStruct stuntStruct in stuntList)
            {
                var stunt = new DLGStunt();
                stunt.Participant = stuntStruct.Acquire("Participant", string.Empty);
                stunt.StuntModel = stuntStruct.Acquire("StuntModel", ResRef.FromBlank());
                dlg.Stunts.Add(stunt);
            }

            // StartingList
            GFFList startingList = root.Acquire("StartingList", new GFFList());
            for (int linkListIndex = 0; linkListIndex < startingList.Count; linkListIndex++)
            {
                GFFStruct linkStruct = startingList.At(linkListIndex);
                int nodeStructId = (int)linkStruct.Acquire("Index", (uint)0);
                if (nodeStructId >= 0 && nodeStructId < allEntries.Count)
                {
                    DLGEntry starterNode = allEntries[nodeStructId];
                    var link = new DLGLink(starterNode, linkListIndex);
                    dlg.Starters.Add(link);
                    ConstructLink(linkStruct, link);
                }
            }

            // EntryList
            for (int nodeListIndex = 0; nodeListIndex < entryList.Count; nodeListIndex++)
            {
                GFFStruct entryStruct = entryList.At(nodeListIndex);
                DLGEntry entry = allEntries[nodeListIndex];
                entry.Speaker = entryStruct.Acquire("Speaker", string.Empty);
                entry.ListIndex = nodeListIndex;
                ConstructNode(entryStruct, entry);

                GFFList repliesList = entryStruct.Acquire("RepliesList", new GFFList());
                for (int linkListIndex = 0; linkListIndex < repliesList.Count; linkListIndex++)
                {
                    GFFStruct linkStruct = repliesList.At(linkListIndex);
                    int nodeStructId = (int)linkStruct.Acquire("Index", (uint)0);
                    if (nodeStructId >= 0 && nodeStructId < allReplies.Count)
                    {
                        DLGReply replyNode = allReplies[nodeStructId];
                        var link = new DLGLink(replyNode, linkListIndex);
                        link.IsChild = linkStruct.Acquire("IsChild", (byte)0) != 0;
                        link.Comment = linkStruct.Acquire("LinkComment", string.Empty);
                        entry.Links.Add(link);
                        ConstructLink(linkStruct, link);
                    }
                }
            }

            // ReplyList
            for (int nodeListIndex = 0; nodeListIndex < replyList.Count; nodeListIndex++)
            {
                GFFStruct replyStruct = replyList.At(nodeListIndex);
                DLGReply reply = allReplies[nodeListIndex];
                reply.ListIndex = nodeListIndex;
                ConstructNode(replyStruct, reply);

                GFFList entriesList = replyStruct.Acquire("EntriesList", new GFFList());
                for (int linkListIndex = 0; linkListIndex < entriesList.Count; linkListIndex++)
                {
                    GFFStruct linkStruct = entriesList.At(linkListIndex);
                    int nodeStructId = (int)linkStruct.Acquire("Index", (uint)0);
                    if (nodeStructId >= 0 && nodeStructId < allEntries.Count)
                    {
                        DLGEntry entryNode = allEntries[nodeStructId];
                        var link = new DLGLink(entryNode, linkListIndex);
                        link.IsChild = linkStruct.Acquire("IsChild", (byte)0) != 0;
                        link.Comment = linkStruct.Acquire("LinkComment", string.Empty);
                        reply.Links.Add(link);
                        ConstructLink(linkStruct, link);
                    }
                }
            }

            return dlg;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:55-162
        // Original: def construct_node(gff_struct: GFFStruct, node: DLGNode):
        private static void ConstructNode(GFFStruct gffStruct, DLGNode node)
        {
            node.Text = gffStruct.Acquire("Text", LocalizedString.FromInvalid());
            node.Listener = gffStruct.Acquire("Listener", string.Empty);
            node.VoResRef = gffStruct.Acquire("VO_ResRef", ResRef.FromBlank());
            node.Script1 = gffStruct.Acquire("Script", ResRef.FromBlank());
            uint delay = gffStruct.Acquire("Delay", (uint)0);
            node.Delay = delay == 0xFFFFFFFF ? -1 : (int)delay;
            node.Comment = gffStruct.Acquire("Comment", string.Empty);
            node.Sound = gffStruct.Acquire("Sound", ResRef.FromBlank());
            node.Quest = gffStruct.Acquire("Quest", string.Empty);
            node.PlotIndex = gffStruct.Acquire("PlotIndex", -1);
            node.PlotXpPercentage = gffStruct.Acquire("PlotXPPercentage", 0.0f);
            node.WaitFlags = (int)gffStruct.Acquire("WaitFlags", (uint)0);
            node.CameraAngle = (int)gffStruct.Acquire("CameraAngle", (uint)0);
            node.FadeType = (int)gffStruct.Acquire("FadeType", (byte)0);
            node.SoundExists = (int)gffStruct.Acquire("SoundExists", (byte)0);
            node.VoTextChanged = gffStruct.Acquire("VOTextChanged", (byte)0) != 0;

            // AnimList
            GFFList animList = gffStruct.Acquire("AnimList", new GFFList());
            foreach (GFFStruct animStruct in animList)
            {
                var anim = new DLGAnimation();
                int animationId = (int)animStruct.Acquire("Animation", (ushort)0);
                if (animationId > 10000)
                {
                    animationId -= 10000;
                }
                anim.AnimationId = animationId;
                anim.Participant = animStruct.Acquire("Participant", string.Empty);
                node.Animations.Add(anim);
            }

            node.Script1Param1 = gffStruct.Acquire("ActionParam1", 0);
            node.Script2Param1 = gffStruct.Acquire("ActionParam1b", 0);
            node.Script1Param2 = gffStruct.Acquire("ActionParam2", 0);
            node.Script2Param2 = gffStruct.Acquire("ActionParam2b", 0);
            node.Script1Param3 = gffStruct.Acquire("ActionParam3", 0);
            node.Script2Param3 = gffStruct.Acquire("ActionParam3b", 0);
            node.Script1Param4 = gffStruct.Acquire("ActionParam4", 0);
            node.Script2Param4 = gffStruct.Acquire("ActionParam4b", 0);
            node.Script1Param5 = gffStruct.Acquire("ActionParam5", 0);
            node.Script2Param5 = gffStruct.Acquire("ActionParam5b", 0);
            node.Script1Param6 = gffStruct.Acquire("ActionParamStrA", string.Empty);
            node.Script2Param6 = gffStruct.Acquire("ActionParamStrB", string.Empty);
            node.Script2 = gffStruct.Acquire("Script2", ResRef.FromBlank());
            node.AlienRaceNode = gffStruct.Acquire("AlienRaceNode", 0);
            node.EmotionId = gffStruct.Acquire("Emotion", 0);
            node.FacialId = gffStruct.Acquire("FacialAnim", 0);
            node.NodeId = gffStruct.Acquire("NodeID", 0);
            node.Unskippable = gffStruct.Acquire("NodeUnskippable", (byte)0) != 0;
            node.PostProcNode = gffStruct.Acquire("PostProcNode", 0);
            node.RecordNoVoOverride = gffStruct.Acquire("RecordNoVOOverri", (byte)0) != 0;
            node.RecordVo = gffStruct.Acquire("RecordVO", (byte)0) != 0;

            if (gffStruct.Exists("QuestEntry"))
            {
                node.QuestEntry = gffStruct.Acquire("QuestEntry", 0);
            }
            if (gffStruct.Exists("FadeDelay"))
            {
                node.FadeDelay = gffStruct.Acquire("FadeDelay", 0.0f);
            }
            if (gffStruct.Exists("FadeLength"))
            {
                node.FadeLength = gffStruct.Acquire("FadeLength", 0.0f);
            }
            if (gffStruct.Exists("CameraAnimation"))
            {
                node.CameraAnim = (int)gffStruct.Acquire("CameraAnimation", (ushort)0);
            }
            if (gffStruct.Exists("CameraID"))
            {
                node.CameraId = gffStruct.Acquire("CameraID", 0);
            }
            if (gffStruct.Exists("CamFieldOfView"))
            {
                node.CameraFov = gffStruct.Acquire("CamFieldOfView", 0.0f);
            }
            if (gffStruct.Exists("CamHeightOffset"))
            {
                node.CameraHeight = gffStruct.Acquire("CamHeightOffset", 0.0f);
            }
            if (gffStruct.Exists("CamVidEffect"))
            {
                node.CameraEffect = gffStruct.Acquire("CamVidEffect", -1);
            }
            if (gffStruct.Exists("TarHeightOffset"))
            {
                node.TargetHeight = gffStruct.Acquire("TarHeightOffset", 0.0f);
            }
            if (gffStruct.Exists("FadeColor"))
            {
                var fadeColorVec = gffStruct.Acquire("FadeColor", new Vector3(0, 0, 0));
                node.FadeColor = Color.FromBgrVector3(fadeColorVec);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:164-195
        // Original: def construct_link(gff_struct: GFFStruct, link: DLGLink):
        private static void ConstructLink(GFFStruct gffStruct, DLGLink link)
        {
            link.Active1 = gffStruct.Acquire("Active", ResRef.FromBlank());
            link.Active2 = gffStruct.Acquire("Active2", ResRef.FromBlank());
            link.Logic = gffStruct.Acquire("Logic", (byte)0) != 0;
            link.Active1Not = gffStruct.Acquire("Not", (byte)0) != 0;
            link.Active2Not = gffStruct.Acquire("Not2", (byte)0) != 0;
            link.Active1Param1 = gffStruct.Acquire("Param1", 0);
            link.Active1Param2 = gffStruct.Acquire("Param2", 0);
            link.Active1Param3 = gffStruct.Acquire("Param3", 0);
            link.Active1Param4 = gffStruct.Acquire("Param4", 0);
            link.Active1Param5 = gffStruct.Acquire("Param5", 0);
            link.Active1Param6 = gffStruct.Acquire("ParamStrA", string.Empty);
            link.Active2Param1 = gffStruct.Acquire("Param1b", 0);
            link.Active2Param2 = gffStruct.Acquire("Param2b", 0);
            link.Active2Param3 = gffStruct.Acquire("Param3b", 0);
            link.Active2Param4 = gffStruct.Acquire("Param4b", 0);
            link.Active2Param5 = gffStruct.Acquire("Param5b", 0);
            link.Active2Param6 = gffStruct.Acquire("ParamStrB", string.Empty);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:307-549
        // Original: def dismantle_dlg(dlg: DLG, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleDlg(DLG dlg, Game game = Game.K2)
        {
            var gff = new GFF(GFFContent.DLG);
            GFFStruct root = gff.Root;

            List<DLGEntry> allEntries = dlg.AllEntries(asSorted: true);
            List<DLGReply> allReplies = dlg.AllReplies(asSorted: true);

            root.SetUInt32("NumWords", (uint)dlg.WordCount);
            root.SetResRef("EndConverAbort", dlg.OnAbort);
            root.SetResRef("EndConversation", dlg.OnEnd);
            root.SetUInt8("Skippable", dlg.Skippable ? (byte)1 : (byte)0);
            root.SetResRef("AmbientTrack", dlg.AmbientTrack);
            root.SetUInt32("AnimatedCut", (uint)dlg.AnimatedCut);
            root.SetResRef("CameraModel", dlg.CameraModel);
            root.SetUInt32("ComputerType", (uint)dlg.ComputerType);
            root.SetUInt32("ConversationType", (uint)dlg.ConversationType);
            root.SetUInt8("OldHitCheck", dlg.OldHitCheck ? (byte)1 : (byte)0);
            root.SetUInt8("UnequipHItem", dlg.UnequipHands ? (byte)1 : (byte)0);
            root.SetUInt8("UnequipItems", dlg.UnequipItems ? (byte)1 : (byte)0);
            root.SetString("VO_ID", dlg.VoId);
            root.SetInt32("AlienRaceOwner", dlg.AlienRaceOwner);
            root.SetInt32("PostProcOwner", dlg.PostProcOwner);
            root.SetInt32("RecordNoVO", dlg.RecordNoVo);
            root.SetInt32("NextNodeID", dlg.NextNodeId);
            root.SetInt32("DelayEntry", dlg.DelayEntry);
            root.SetInt32("DelayReply", dlg.DelayReply);

            // StuntList
            var stuntList = new GFFList();
            root.SetList("StuntList", stuntList);
            for (int i = 0; i < dlg.Stunts.Count; i++)
            {
                DLGStunt stunt = dlg.Stunts[i];
                GFFStruct stuntStruct = stuntList.Add(i);
                stuntStruct.SetString("Participant", stunt.Participant);
                stuntStruct.SetResRef("StuntModel", stunt.StuntModel);
            }

            // StartingList
            var startingList = new GFFList();
            root.SetList("StartingList", startingList);
            for (int i = 0; i < dlg.Starters.Count; i++)
            {
                DLGLink link = dlg.Starters[i];
                GFFStruct linkStruct = startingList.Add(i);
                int entryIndex = allEntries.IndexOf(link.Node as DLGEntry);
                linkStruct.SetUInt32("Index", entryIndex >= 0 ? (uint)entryIndex : 0);
                DismantleLink(linkStruct, link);
            }

            // EntryList
            var entryList = new GFFList();
            root.SetList("EntryList", entryList);
            for (int i = 0; i < allEntries.Count; i++)
            {
                DLGEntry entry = allEntries[i];
                GFFStruct entryStruct = entryList.Add(i);
                entryStruct.SetString("Speaker", entry.Speaker);
                DismantleNode(entryStruct, entry, allEntries, allReplies, "RepliesList", game);
            }

            // ReplyList
            var replyList = new GFFList();
            root.SetList("ReplyList", replyList);
            for (int i = 0; i < allReplies.Count; i++)
            {
                DLGReply reply = allReplies[i];
                GFFStruct replyStruct = replyList.Add(i);
                DismantleNode(replyStruct, reply, allEntries, allReplies, "EntriesList", game);
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:356-488
        // Original: def dismantle_node(gff_struct: GFFStruct, node: DLGNode, nodes: list[DLGNode], list_name: str, game: Game):
        private static void DismantleNode(GFFStruct gffStruct, DLGNode node, List<DLGEntry> allEntries, List<DLGReply> allReplies, string listName, Game game)
        {
            gffStruct.SetLocString("Text", node.Text);
            gffStruct.SetString("Listener", node.Listener);
            gffStruct.SetResRef("VO_ResRef", node.VoResRef);
            gffStruct.SetResRef("Script", node.Script1);
            gffStruct.SetUInt32("Delay", node.Delay == -1 ? 0xFFFFFFFF : (uint)node.Delay);
            gffStruct.SetString("Comment", node.Comment);
            gffStruct.SetResRef("Sound", node.Sound);
            gffStruct.SetString("Quest", node.Quest);
            gffStruct.SetInt32("PlotIndex", node.PlotIndex);
            if (node.PlotXpPercentage != 0.0f)
            {
                gffStruct.SetSingle("PlotXPPercentage", node.PlotXpPercentage);
            }
            gffStruct.SetUInt32("WaitFlags", (uint)node.WaitFlags);
            gffStruct.SetUInt32("CameraAngle", (uint)node.CameraAngle);
            gffStruct.SetUInt8("FadeType", (byte)node.FadeType);
            gffStruct.SetUInt8("SoundExists", (byte)node.SoundExists);
            if (node.VoTextChanged)
            {
                gffStruct.SetUInt8("Changed", (byte)(node.VoTextChanged ? 1 : 0));
            }

            // AnimList
            var animList = new GFFList();
            gffStruct.SetList("AnimList", animList);
            for (int i = 0; i < node.Animations.Count; i++)
            {
                DLGAnimation anim = node.Animations[i];
                GFFStruct animStruct = animList.Add(i);
                int animationId = anim.AnimationId;
                if (animationId <= 10000)
                {
                    animStruct.SetUInt16("Animation", (ushort)animationId);
                }
                else
                {
                    animStruct.SetUInt16("Animation", (ushort)(animationId + 10000));
                }
                animStruct.SetString("Participant", anim.Participant);
            }

            if (!string.IsNullOrEmpty(node.Quest) && node.QuestEntry.HasValue)
            {
                gffStruct.SetUInt32("QuestEntry", (uint)node.QuestEntry.Value);
            }
            if (node.FadeDelay.HasValue)
            {
                gffStruct.SetSingle("FadeDelay", node.FadeDelay.Value);
            }
            if (node.FadeLength.HasValue)
            {
                gffStruct.SetSingle("FadeLength", node.FadeLength.Value);
            }
            if (node.CameraAnim.HasValue)
            {
                gffStruct.SetUInt16("CameraAnimation", (ushort)node.CameraAnim.Value);
            }
            if (node.CameraId.HasValue)
            {
                gffStruct.SetInt32("CameraID", node.CameraId.Value);
            }
            if (node.CameraFov.HasValue)
            {
                gffStruct.SetSingle("CamFieldOfView", node.CameraFov.Value);
            }
            if (node.CameraHeight.HasValue)
            {
                gffStruct.SetSingle("CamHeightOffset", node.CameraHeight.Value);
            }
            if (node.CameraEffect.HasValue)
            {
                gffStruct.SetInt32("CamVidEffect", node.CameraEffect.Value);
            }
            if (node.TargetHeight.HasValue)
            {
                gffStruct.SetSingle("TarHeightOffset", node.TargetHeight.Value);
            }
            if (node.FadeColor != null)
            {
                gffStruct.SetVector3("FadeColor", node.FadeColor.ToBgrVector3());
            }

            if (game == Game.K2)
            {
                gffStruct.SetInt32("ActionParam1", node.Script1Param1);
                gffStruct.SetInt32("ActionParam1b", node.Script2Param1);
                gffStruct.SetInt32("ActionParam2", node.Script1Param2);
                gffStruct.SetInt32("ActionParam2b", node.Script2Param2);
                gffStruct.SetInt32("ActionParam3", node.Script1Param3);
                gffStruct.SetInt32("ActionParam3b", node.Script2Param3);
                gffStruct.SetInt32("ActionParam4", node.Script1Param4);
                gffStruct.SetInt32("ActionParam4b", node.Script2Param4);
                gffStruct.SetInt32("ActionParam5", node.Script1Param5);
                gffStruct.SetInt32("ActionParam5b", node.Script2Param5);
                gffStruct.SetString("ActionParamStrA", node.Script1Param6);
                gffStruct.SetString("ActionParamStrB", node.Script2Param6);
                gffStruct.SetResRef("Script2", node.Script2);
                gffStruct.SetInt32("AlienRaceNode", node.AlienRaceNode);
                gffStruct.SetInt32("Emotion", node.EmotionId);
                gffStruct.SetInt32("FacialAnim", node.FacialId);
                gffStruct.SetInt32("NodeID", node.NodeId);
                gffStruct.SetInt32("NodeUnskippable", node.Unskippable ? 1 : 0);
                gffStruct.SetInt32("PostProcNode", node.PostProcNode);
                gffStruct.SetInt32("RecordNoVOOverri", node.RecordNoVoOverride ? 1 : 0);
                gffStruct.SetInt32("RecordVO", node.RecordVo ? 1 : 0);
                gffStruct.SetInt32("VOTextChanged", node.VoTextChanged ? 1 : 0);
            }

            // Links
            var linkList = new GFFList();
            gffStruct.SetList(listName, linkList);
            var sortedLinks = node.Links.OrderBy(l => l.ListIndex == -1).ThenBy(l => l.ListIndex).ToList();
            for (int i = 0; i < sortedLinks.Count; i++)
            {
                DLGLink link = sortedLinks[i];
                GFFStruct linkStruct = linkList.Add(i);
                int nodeIndex = -1;
                if (link.Node is DLGEntry entry)
                {
                    nodeIndex = allEntries.IndexOf(entry);
                }
                else if (link.Node is DLGReply reply)
                {
                    nodeIndex = allReplies.IndexOf(reply);
                }
                linkStruct.SetUInt32("Index", nodeIndex >= 0 ? (uint)nodeIndex : 0);
                linkStruct.SetUInt8("IsChild", link.IsChild ? (byte)1 : (byte)0);
                linkStruct.SetString("LinkComment", link.Comment);
                DismantleLink(linkStruct, link);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:332-353
        // Original: def dismantle_link(gff_struct: GFFStruct, link: DLGLink, nodes: list[DLGNode], list_name: str):
        private static void DismantleLink(GFFStruct gffStruct, DLGLink link)
        {
            gffStruct.SetResRef("Active", link.Active1);
            gffStruct.SetResRef("Active2", link.Active2);
            gffStruct.SetUInt8("Logic", link.Logic ? (byte)1 : (byte)0);
            gffStruct.SetUInt8("Not", link.Active1Not ? (byte)1 : (byte)0);
            gffStruct.SetUInt8("Not2", link.Active2Not ? (byte)1 : (byte)0);
            gffStruct.SetInt32("Param1", link.Active1Param1);
            gffStruct.SetInt32("Param2", link.Active1Param2);
            gffStruct.SetInt32("Param3", link.Active1Param3);
            gffStruct.SetInt32("Param4", link.Active1Param4);
            gffStruct.SetInt32("Param5", link.Active1Param5);
            gffStruct.SetString("ParamStrA", link.Active1Param6);
            gffStruct.SetInt32("Param1b", link.Active2Param1);
            gffStruct.SetInt32("Param2b", link.Active2Param2);
            gffStruct.SetInt32("Param3b", link.Active2Param3);
            gffStruct.SetInt32("Param4b", link.Active2Param4);
            gffStruct.SetInt32("Param5b", link.Active2Param5);
            gffStruct.SetString("ParamStrB", link.Active2Param6);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:551-575
        // Original: def read_dlg(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> DLG:
        public static DLG ReadDlg(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructDlg(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/gff.py:606-633
        // Original: def bytes_dlg(dlg: DLG, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesDlg(DLG dlg, Game game = Game.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.DLG;
            }
            GFF gff = DismantleDlg(dlg, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}
