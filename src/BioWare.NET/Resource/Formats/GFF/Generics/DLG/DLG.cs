using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.DLG
{
    /// <summary>
    /// Type of computer interface for dialog.
    /// </summary>
    [PublicAPI]
    public enum DLGComputerType
    {
        Modern = 0,
        Ancient = 1
    }

    /// <summary>
    /// Type of conversation for dialog.
    /// </summary>
    [PublicAPI]
    public enum DLGConversationType
    {
        Human = 0,
        Computer = 1,
        Other = 2,
        Unknown = 3
    }

    /// <summary>
    /// Stores dialog data.
    ///
    /// DLG files are GFF-based format files that store dialog trees with entries, replies,
    /// links, and conversation metadata.
    /// </summary>
    [PublicAPI]
    public sealed class DLG
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:36
        // Original: class DLG:
        public static readonly ResourceType BinaryType = ResourceType.DLG;

        public List<DLGLink> Starters { get; set; } = new List<DLGLink>();
        public List<DLGStunt> Stunts { get; set; } = new List<DLGStunt>();

        // Dialog metadata
        public ResRef AmbientTrack { get; set; } = ResRef.FromBlank();
        public int AnimatedCut { get; set; }
        public ResRef CameraModel { get; set; } = ResRef.FromBlank();
        public DLGComputerType ComputerType { get; set; } = DLGComputerType.Modern;
        public DLGConversationType ConversationType { get; set; } = DLGConversationType.Human;
        public ResRef OnAbort { get; set; } = ResRef.FromBlank();
        public ResRef OnEnd { get; set; } = ResRef.FromBlank();
        public int WordCount { get; set; }
        public bool OldHitCheck { get; set; }
        public bool Skippable { get; set; }
        public bool UnequipItems { get; set; }
        public bool UnequipHands { get; set; }
        public string VoId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        // KotOR 2
        public int AlienRaceOwner { get; set; }
        public int NextNodeId { get; set; }
        public int PostProcOwner { get; set; }
        public int RecordNoVo { get; set; }

        // Deprecated
        public int DelayEntry { get; set; }
        public int DelayReply { get; set; }

        public DLG()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:307
        // Original: def all_entries(self, *, as_sorted: bool = False) -> list[DLGEntry]:
        public List<DLGEntry> AllEntries(bool asSorted = false)
        {
            List<DLGEntry> entries = _AllEntries();
            if (!asSorted)
            {
                return entries;
            }
            return entries.OrderBy(e => e.ListIndex == -1).ThenBy(e => e.ListIndex).ToList();
        }

        private List<DLGEntry> _AllEntries(List<DLGLink> links = null, HashSet<DLGEntry> seenEntries = null)
        {
            List<DLGEntry> entries = new List<DLGEntry>();
            links = links ?? Starters;
            seenEntries = seenEntries ?? new HashSet<DLGEntry>();

            foreach (DLGLink link in links)
            {
                DLGNode entry = link.Node;
                if (entry == null || seenEntries.Contains(entry as DLGEntry))
                {
                    continue;
                }
                if (!(entry is DLGEntry dlgEntry))
                {
                    continue;
                }
                entries.Add(dlgEntry);
                seenEntries.Add(dlgEntry);
                foreach (DLGLink replyLink in entry.Links)
                {
                    DLGNode reply = replyLink.Node;
                    if (reply != null)
                    {
                        entries.AddRange(_AllEntries(reply.Links, seenEntries));
                    }
                }
            }

            return entries;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:363
        // Original: def all_replies(self, *, as_sorted: bool = False) -> list[DLGReply]:
        public List<DLGReply> AllReplies(bool asSorted = false)
        {
            List<DLGReply> replies = _AllReplies();
            if (!asSorted)
            {
                return replies;
            }
            return replies.OrderBy(r => r.ListIndex == -1).ThenBy(r => r.ListIndex).ToList();
        }

        private List<DLGReply> _AllReplies(List<DLGLink> links = null, List<DLGReply> seenReplies = null)
        {
            List<DLGReply> replies = new List<DLGReply>();
            links = links ?? Starters.Where(l => l.Node != null).SelectMany(l => l.Node.Links).ToList();
            seenReplies = seenReplies ?? new List<DLGReply>();

            foreach (DLGLink link in links)
            {
                DLGNode reply = link.Node;
                if (seenReplies.Contains(reply as DLGReply))
                {
                    continue;
                }
                if (!(reply is DLGReply dlgReply))
                {
                    continue;
                }
                replies.Add(dlgReply);
                seenReplies.Add(dlgReply);
                foreach (DLGLink entryLink in reply.Links)
                {
                    DLGNode entry = entryLink.Node;
                    if (entry != null)
                    {
                        replies.AddRange(_AllReplies(entry.Links, seenReplies));
                    }
                }
            }

            return replies;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:183
        // Original: def find_paths(self, target: DLGEntry | DLGReply | DLGLink) -> list[PureWindowsPath]:
        /// <summary>
        /// Find all paths to a target node or link.
        /// </summary>
        /// <param name="target">The target node or link to find paths to</param>
        /// <returns>A list of paths to the target</returns>
        public List<string> FindPaths(DLGNode target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            List<string> paths = new List<string>();
            // Build starter paths - iterate through starters and build paths from each
            for (int i = 0; i < Starters.Count; i++)
            {
                string starterPath = $"StartingList\\{i}";
                var starterLinks = new List<DLGLink> { Starters[i] };
                _FindPathsRecursive(starterLinks, target, starterPath, paths, new HashSet<object>());
            }
            return paths;
        }

        /// <summary>
        /// Find all paths to a target link.
        /// </summary>
        /// <param name="target">The target link to find paths to</param>
        /// <returns>A list of paths to the target</returns>
        public List<string> FindPaths(DLGLink target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            List<string> paths = new List<string>();
            DLGNode parentNode = GetLinkParent(target);
            if (parentNode == null)
            {
                if (Starters.Contains(target))
                {
                    paths.Add($"StartingList\\{target.ListIndex}");
                }
                else
                {
                    throw new ArgumentException($"Target {target.GetType().Name} doesn't have a parent, and also not found in starters.");
                }
            }
            else
            {
                _FindPathsForLink(parentNode, target, paths);
            }
            return paths;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:210
        // Original: def _find_paths_for_link(self, parent_node: DLGNode, target: DLGLink, paths: list[PureWindowsPath]):
        private void _FindPathsForLink(DLGNode parentNode, DLGLink target, List<string> paths)
        {
            string nodeListName = parentNode is DLGEntry ? "EntryList" : "ReplyList";
            string parentPath = $"{nodeListName}\\{parentNode.ListIndex}";

            string linkListName = parentNode is DLGEntry ? "RepliesList" : "EntriesList";
            paths.Add($"{parentPath}\\{linkListName}\\{target.ListIndex}");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:236
        // Original: def _find_paths_recursive(self, links: Sequence[DLGLink[T]], target: DLGNode, current_path: PureWindowsPath, paths: list[PureWindowsPath], seen_links_and_nodes: set[DLGNode | DLGLink]):
        private void _FindPathsRecursive(List<DLGLink> links, DLGNode target, string currentPath, List<string> paths, HashSet<object> seenLinksAndNodes)
        {
            foreach (DLGLink link in links)
            {
                if (link == null || seenLinksAndNodes.Contains(link))
                {
                    continue;
                }

                seenLinksAndNodes.Add(link);
                DLGNode node = link.Node;
                if (node == null)
                {
                    continue;
                }

                if (node == target)
                {
                    if (seenLinksAndNodes.Contains(node))
                    {
                        continue;
                    }
                    seenLinksAndNodes.Add(node);
                    string nodeListName = node is DLGEntry ? "EntryList" : "ReplyList";
                    // Add the direct path (matching Python implementation)
                    paths.Add($"{nodeListName}\\{node.ListIndex}");
                    // Add the full path from starter if we have a currentPath
                    // When currentPath is set, we're traversing from a starter, so include full path
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        // currentPath already includes the path up to the link list
                        // Just append the link index to complete the path
                        paths.Add($"{currentPath}\\{link.ListIndex}");
                    }
                    continue;
                }

                if (!seenLinksAndNodes.Contains(node))
                {
                    seenLinksAndNodes.Add(node);
                    string nodeListName = node is DLGEntry ? "EntryList" : "ReplyList";
                    string linkListName = node is DLGEntry ? "RepliesList" : "EntriesList";
                    string nodePath = $"{nodeListName}\\{node.ListIndex}";
                    // Build new path: currentPath / nodePath / linkListName (matching Python)
                    string newPath = string.IsNullOrEmpty(currentPath) ? $"{nodePath}\\{linkListName}" : $"{currentPath}\\{nodePath}\\{linkListName}";
                    _FindPathsRecursive(node.Links, target, newPath, paths, seenLinksAndNodes);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:288
        // Original: def get_link_parent(self, target_link: DLGLink) -> DLGEntry | DLGReply | DLG | None:
        /// <summary>
        /// Find the parent node of a given link.
        /// </summary>
        /// <param name="targetLink">The link to find the parent for</param>
        /// <returns>The parent node or null if not found</returns>
        public DLGNode GetLinkParent(DLGLink targetLink)
        {
            if (targetLink == null)
            {
                return null;
            }

            if (Starters.Contains(targetLink))
            {
                return null; // Return null to indicate DLG is the parent (handled by caller)
            }

            foreach (DLGEntry entry in AllEntries())
            {
                if (entry.Links.Contains(targetLink))
                {
                    return entry;
                }
            }

            foreach (DLGReply reply in AllReplies())
            {
                if (reply.Links.Contains(targetLink))
                {
                    return reply;
                }
            }

            return null;
        }
    }
}
