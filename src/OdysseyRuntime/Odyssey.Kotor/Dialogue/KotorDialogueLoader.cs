using System;
using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Generics.DLG;
using CSharpKOTOR.Resources;
using Odyssey.Core.Dialogue;
using Odyssey.Content.Interfaces;

namespace Odyssey.Kotor.Dialogue
{
    /// <summary>
    /// Dialogue loader implementation using CSharpKOTOR DLG format.
    /// Converts CSharpKOTOR.Resource.Generics.DLG.DLG to Odyssey.Core.Dialogue.RuntimeDialogue.
    /// </summary>
    /// <remarks>
    /// Based on CSharpKOTOR DLG format at CSharpKOTOR/Resource/Generics/DLG/DLG.cs
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py
    /// </remarks>
    public class KotorDialogueLoader : IDialogueLoader
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly Dictionary<string, RuntimeDialogue> _cache;

        public KotorDialogueLoader(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _cache = new Dictionary<string, RuntimeDialogue>(StringComparer.OrdinalIgnoreCase);
        }

        public RuntimeDialogue LoadDialogue(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            // Check cache
            if (_cache.TryGetValue(resRef, out RuntimeDialogue cached))
            {
                return cached;
            }

            // Load DLG from resource provider
            try
            {
                byte[] data = _resourceProvider.GetResourceBytesAsync(
                    new CSharpKOTOR.Resources.ResourceIdentifier(resRef, CSharpKOTOR.Resources.ResourceType.DLG),
                    System.Threading.CancellationToken.None).Result;

                if (data == null || data.Length == 0)
                {
                    return null;
                }

                // Parse DLG using CSharpKOTOR helper
                DLG dlg = CSharpKOTOR.Resource.Generics.DLG.DLGHelper.ReadDlg(data);

                // Convert to RuntimeDialogue
                RuntimeDialogue runtimeDialogue = ConvertToRuntimeDialogue(dlg, resRef);

                // Cache it
                _cache[resRef] = runtimeDialogue;

                return runtimeDialogue;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[KotorDialogueLoader] Failed to load dialogue " + resRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Converts CSharpKOTOR DLG to RuntimeDialogue.
        /// </summary>
        private RuntimeDialogue ConvertToRuntimeDialogue(DLG dlg, string resRef)
        {
            var runtimeDialogue = new RuntimeDialogue
            {
                ResRef = resRef,
                Skippable = dlg.Skippable,
                ConversationType = (int)dlg.ConversationType,
                ComputerType = (int)dlg.ComputerType,
                CameraModel = dlg.CameraModel.ToString(),
                OnEndScript = dlg.OnEnd.ToString(),
                OnAbortScript = dlg.OnAbort.ToString(),
                AmbientTrack = dlg.AmbientTrack.ToString(),
                VoiceOverId = dlg.VoId,
                UnequipItems = dlg.UnequipItems,
                UnequipHands = dlg.UnequipHands
            };

            // Build entry and reply maps
            var entryMap = new Dictionary<DLGEntry, int>();
            var replyMap = new Dictionary<DLGReply, int>();

            // Collect all entries
            List<DLGEntry> allEntries = dlg.AllEntries();
            for (int i = 0; i < allEntries.Count; i++)
            {
                DLGEntry entry = allEntries[i];
                entryMap[entry] = entry.ListIndex >= 0 ? entry.ListIndex : i;
            }

            // Collect all replies
            List<DLGReply> allReplies = dlg.AllReplies();
            for (int i = 0; i < allReplies.Count; i++)
            {
                DLGReply reply = allReplies[i];
                replyMap[reply] = reply.ListIndex >= 0 ? reply.ListIndex : i;
            }

            // Convert entries
            foreach (KeyValuePair<DLGEntry, int> kvp in entryMap)
            {
                DLGEntry dlgEntry = kvp.Key;
                int index = kvp.Value;

                var runtimeEntry = new DialogueEntry
                {
                    Index = index,
                    Text = dlgEntry.Text.ToString(),
                    Script1 = dlgEntry.Script1.ToString(),
                    Script2 = dlgEntry.Script2.ToString(),
                    VoiceResRef = dlgEntry.VoResRef.ToString(),
                    SoundResRef = dlgEntry.Sound.ToString(),
                    Comment = dlgEntry.Comment,
                    CameraAngle = dlgEntry.CameraAngle,
                    CameraAnimation = dlgEntry.CameraAnim ?? -1,
                    Delay = dlgEntry.Delay,
                    EmotionId = dlgEntry.EmotionId,
                    FacialId = dlgEntry.FacialId,
                    Quest = dlgEntry.Quest,
                    QuestEntry = dlgEntry.QuestEntry ?? 0,
                    PlotIndex = dlgEntry.PlotIndex,
                    PlotXpPercentage = dlgEntry.PlotXpPercentage,
                    Speaker = dlgEntry.Speaker,
                    Listener = dlgEntry.Listener
                };

                runtimeDialogue.AddEntry(runtimeEntry);
            }

            // Convert replies
            foreach (KeyValuePair<DLGReply, int> kvp in replyMap)
            {
                DLGReply dlgReply = kvp.Key;
                int index = kvp.Value;

                var runtimeReply = new DialogueReply
                {
                    Index = index,
                    Text = dlgReply.Text.ToString(),
                    Script1 = dlgReply.Script1.ToString(),
                    Script2 = dlgReply.Script2.ToString(),
                    VoiceResRef = dlgReply.VoResRef.ToString(),
                    SoundResRef = dlgReply.Sound.ToString(),
                    Comment = dlgReply.Comment,
                    CameraAngle = dlgReply.CameraAngle,
                    CameraAnimation = dlgReply.CameraAnim ?? -1,
                    Delay = dlgReply.Delay,
                    EmotionId = dlgReply.EmotionId,
                    FacialId = dlgReply.FacialId,
                    Quest = dlgReply.Quest,
                    QuestEntry = dlgReply.QuestEntry ?? 0,
                    PlotIndex = dlgReply.PlotIndex,
                    PlotXpPercentage = dlgReply.PlotXpPercentage
                };

                runtimeDialogue.AddReply(runtimeReply);
            }

            // Now add links and set conditional scripts from links
            foreach (KeyValuePair<DLGEntry, int> kvp in entryMap)
            {
                DLGEntry dlgEntry = kvp.Key;
                int entryIndex = kvp.Value;
                DialogueEntry runtimeEntry = runtimeDialogue.GetEntry(entryIndex);

                // Add reply links (with conditional scripts from link)
                foreach (DLGLink link in dlgEntry.Links)
                {
                    if (link.Node is DLGReply reply && replyMap.TryGetValue(reply, out int replyIndex))
                    {
                        DialogueReply runtimeReply = runtimeDialogue.GetReply(replyIndex);
                        if (runtimeReply != null && !string.IsNullOrEmpty(link.Active1.ToString()))
                        {
                            // Set conditional script from link (Active1 is the primary condition)
                            runtimeReply.ConditionalScript = link.Active1.ToString();
                        }
                        runtimeEntry.AddReplyLink(replyIndex);
                    }
                }
            }

            // Add entry links from replies
            foreach (KeyValuePair<DLGReply, int> kvp in replyMap)
            {
                DLGReply dlgReply = kvp.Key;
                int replyIndex = kvp.Value;
                DialogueReply runtimeReply = runtimeDialogue.GetReply(replyIndex);

                // Add entry links (with conditional scripts from link)
                foreach (DLGLink link in dlgReply.Links)
                {
                    if (link.Node is DLGEntry entry && entryMap.TryGetValue(entry, out int entryIndex))
                    {
                        DialogueEntry runtimeEntry = runtimeDialogue.GetEntry(entryIndex);
                        if (runtimeEntry != null && !string.IsNullOrEmpty(link.Active1.ToString()))
                        {
                            // Set conditional script from link (Active1 is the primary condition)
                            runtimeEntry.ConditionalScript = link.Active1.ToString();
                        }
                        runtimeReply.AddEntryLink(entryIndex);
                    }
                }
            }


            // Set starter indices
            foreach (DLGLink starterLink in dlg.Starters)
            {
                if (starterLink.Node is DLGEntry entry && entryMap.TryGetValue(entry, out int entryIndex))
                {
                    runtimeDialogue.AddStarter(entryIndex);
                }
            }

            return runtimeDialogue;
        }

        /// <summary>
        /// Clears the dialogue cache.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}

