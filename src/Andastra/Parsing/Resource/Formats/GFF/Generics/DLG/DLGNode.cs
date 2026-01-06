using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics.DLG
{
    /// <summary>
    /// Represents a node in the dialog graph (either DLGEntry or DLGReply).
    /// </summary>
    [PublicAPI]
    public abstract class DLGNode : IEquatable<DLGNode>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:22
        // Original: class DLGNode:
        protected readonly int _hashCache;

        public string Comment { get; set; } = string.Empty;
        public List<DLGLink> Links { get; set; } = new List<DLGLink>();
        public int ListIndex { get; set; } = -1;

        // Camera settings
        public int CameraAngle { get; set; }
        public int? CameraAnim { get; set; }
        public int? CameraId { get; set; }
        public float? CameraFov { get; set; }
        public float? CameraHeight { get; set; }
        public int? CameraEffect { get; set; }

        // Timing
        public int Delay { get; set; } = -1;
        public int FadeType { get; set; }
        public Color FadeColor { get; set; }
        public float? FadeDelay { get; set; }
        public float? FadeLength { get; set; }

        // Content
        public LocalizedString Text { get; set; } = LocalizedString.FromInvalid();
        public ResRef Script1 { get; set; } = ResRef.FromBlank();
        public ResRef Script2 { get; set; } = ResRef.FromBlank();
        public ResRef Sound { get; set; } = ResRef.FromBlank();
        public int SoundExists { get; set; }
        public ResRef VoResRef { get; set; } = ResRef.FromBlank();
        public int WaitFlags { get; set; }

        // Script parameters (KotOR 2)
        public int Script1Param1 { get; set; }
        public int Script1Param2 { get; set; }
        public int Script1Param3 { get; set; }
        public int Script1Param4 { get; set; }
        public int Script1Param5 { get; set; }
        public string Script1Param6 { get; set; } = string.Empty;
        public int Script2Param1 { get; set; }
        public int Script2Param2 { get; set; }
        public int Script2Param3 { get; set; }
        public int Script2Param4 { get; set; }
        public int Script2Param5 { get; set; }
        public string Script2Param6 { get; set; } = string.Empty;

        // Quest/Plot
        public string Quest { get; set; } = string.Empty;
        public int? QuestEntry { get; set; } = 0;
        public int PlotIndex { get; set; }
        public float PlotXpPercentage { get; set; } = 1.0f;

        // Animation
        public List<DLGAnimation> Animations { get; set; } = new List<DLGAnimation>();
        public int EmotionId { get; set; }
        public int FacialId { get; set; }

        // Other
        public string Listener { get; set; } = string.Empty;
        public float? TargetHeight { get; set; }

        // KotOR 2
        public int AlienRaceNode { get; set; }
        public int NodeId { get; set; }
        public int PostProcNode { get; set; }
        public bool Unskippable { get; set; }
        public bool RecordNoVoOverride { get; set; }
        public bool RecordVo { get; set; }
        public bool VoTextChanged { get; set; }

        protected DLGNode()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DLGNode other && Equals(other);
        }

        public bool Equals(DLGNode other)
        {
            if (other == null || GetType() != other.GetType()) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        public string Path()
        {
            string nodeListDisplay = this is DLGEntry ? "EntryList" : "ReplyList";
            return $"{nodeListDisplay}\\{ListIndex}";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:306
        // Original: def add_node(self, target_links: list[DLGLink], source: DLGNode):
        /// <summary>
        /// Adds a node to the target links list.
        /// </summary>
        /// <param name="targetLinks">The list of links to add to</param>
        /// <param name="source">The source node to link to</param>
        public void AddNode(List<DLGLink> targetLinks, DLGNode source)
        {
            if (targetLinks == null)
            {
                throw new ArgumentNullException(nameof(targetLinks));
            }
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            targetLinks.Add(new DLGLink(source, targetLinks.Count));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:314
        // Original: def calculate_links_and_nodes(self) -> tuple[int, int]:
        /// <summary>
        /// Calculates the total number of links and nodes reachable from this node.
        /// </summary>
        /// <returns>A tuple of (number of links, number of nodes)</returns>
        public Tuple<int, int> CalculateLinksAndNodes()
        {
            Queue<DLGNode> queue = new Queue<DLGNode>();
            queue.Enqueue(this);
            HashSet<DLGNode> seenNodes = new HashSet<DLGNode>();
            int numLinks = 0;

            while (queue.Count > 0)
            {
                DLGNode node = queue.Dequeue();
                if (node == null)
                {
                    continue;
                }
                if (seenNodes.Contains(node))
                {
                    continue;
                }
                seenNodes.Add(node);
                numLinks += node.Links.Count;
                foreach (DLGLink link in node.Links)
                {
                    if (link?.Node != null)
                    {
                        queue.Enqueue(link.Node);
                    }
                }
            }

            return Tuple.Create(numLinks, seenNodes.Count);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:332
        // Original: def shift_item(self, links: list[DLGLink], old_index: int, new_index: int):
        /// <summary>
        /// Moves a link from one index to another in the links list.
        /// </summary>
        /// <param name="links">The list of links</param>
        /// <param name="oldIndex">The old index</param>
        /// <param name="newIndex">The new index</param>
        public void ShiftItem(List<DLGLink> links, int oldIndex, int newIndex)
        {
            if (links == null)
            {
                throw new ArgumentNullException(nameof(links));
            }
            if (0 <= newIndex && newIndex < links.Count)
            {
                DLGLink link = links[oldIndex];
                links.RemoveAt(oldIndex);
                links.Insert(newIndex, link);
            }
            else
            {
                throw new IndexOutOfRangeException($"Index {newIndex} is out of range for list of size {links.Count}");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:344
        // Original: def to_dict(self, node_map: dict[str | int, Any] | None = None) -> dict[str | int, Any]:
        /// <summary>
        /// Serializes this node to a dictionary representation.
        /// </summary>
        /// <param name="nodeMap">Optional map to track serialized nodes and prevent duplicates</param>
        /// <returns>A dictionary representation of this node</returns>
        public Dictionary<string, object> ToDict(Dictionary<string, object> nodeMap = null)
        {
            if (nodeMap == null)
            {
                nodeMap = new Dictionary<string, object>();
            }

            // Prefix keys so they never collide with DLGLink keys when stored in shared node_map
            string nodeKey = $"node-{_hashCache}";
            if (nodeMap.ContainsKey(nodeKey))
            {
                return new Dictionary<string, object>
                {
                    { "type", GetType().Name },
                    { "ref", nodeKey }
                };
            }

            Dictionary<string, object> nodeDict = new Dictionary<string, object>
            {
                { "type", GetType().Name },
                { "key", nodeKey },
                { "data", new Dictionary<string, object>() }
            };
            nodeMap[nodeKey] = nodeDict;

            Dictionary<string, object> data = (Dictionary<string, object>)nodeDict["data"];

            // Serialize all properties
            if (this is DLGEntry entry)
            {
                data["speaker"] = new Dictionary<string, object> { { "value", entry.Speaker }, { "py_type", "str" } };
            }

            data["comment"] = new Dictionary<string, object> { { "value", Comment }, { "py_type", "str" } };
            data["list_index"] = new Dictionary<string, object> { { "value", ListIndex }, { "py_type", "int" } };
            data["camera_angle"] = new Dictionary<string, object> { { "value", CameraAngle }, { "py_type", "int" } };
            data["delay"] = new Dictionary<string, object> { { "value", Delay }, { "py_type", "int" } };
            data["fade_type"] = new Dictionary<string, object> { { "value", FadeType }, { "py_type", "int" } };
            data["listener"] = new Dictionary<string, object> { { "value", Listener }, { "py_type", "str" } };
            data["plot_index"] = new Dictionary<string, object> { { "value", PlotIndex }, { "py_type", "int" } };
            data["plot_xp_percentage"] = new Dictionary<string, object> { { "value", PlotXpPercentage }, { "py_type", "float" } };
            data["wait_flags"] = new Dictionary<string, object> { { "value", WaitFlags }, { "py_type", "int" } };
            data["sound_exists"] = new Dictionary<string, object> { { "value", SoundExists }, { "py_type", "int" } };
            data["emotion_id"] = new Dictionary<string, object> { { "value", EmotionId }, { "py_type", "int" } };
            data["facial_id"] = new Dictionary<string, object> { { "value", FacialId }, { "py_type", "int" } };
            data["alien_race_node"] = new Dictionary<string, object> { { "value", AlienRaceNode }, { "py_type", "int" } };
            data["node_id"] = new Dictionary<string, object> { { "value", NodeId }, { "py_type", "int" } };
            data["post_proc_node"] = new Dictionary<string, object> { { "value", PostProcNode }, { "py_type", "int" } };
            data["unskippable"] = new Dictionary<string, object> { { "value", Unskippable ? 1 : 0 }, { "py_type", "bool" } };
            data["record_no_vo_override"] = new Dictionary<string, object> { { "value", RecordNoVoOverride ? 1 : 0 }, { "py_type", "bool" } };
            data["record_vo"] = new Dictionary<string, object> { { "value", RecordVo ? 1 : 0 }, { "py_type", "bool" } };
            data["vo_text_changed"] = new Dictionary<string, object> { { "value", VoTextChanged ? 1 : 0 }, { "py_type", "bool" } };
            data["script1_param1"] = new Dictionary<string, object> { { "value", Script1Param1 }, { "py_type", "int" } };
            data["script1_param2"] = new Dictionary<string, object> { { "value", Script1Param2 }, { "py_type", "int" } };
            data["script1_param3"] = new Dictionary<string, object> { { "value", Script1Param3 }, { "py_type", "int" } };
            data["script1_param4"] = new Dictionary<string, object> { { "value", Script1Param4 }, { "py_type", "int" } };
            data["script1_param5"] = new Dictionary<string, object> { { "value", Script1Param5 }, { "py_type", "int" } };
            data["script1_param6"] = new Dictionary<string, object> { { "value", Script1Param6 }, { "py_type", "str" } };
            data["script2_param1"] = new Dictionary<string, object> { { "value", Script2Param1 }, { "py_type", "int" } };
            data["script2_param2"] = new Dictionary<string, object> { { "value", Script2Param2 }, { "py_type", "int" } };
            data["script2_param3"] = new Dictionary<string, object> { { "value", Script2Param3 }, { "py_type", "int" } };
            data["script2_param4"] = new Dictionary<string, object> { { "value", Script2Param4 }, { "py_type", "int" } };
            data["script2_param5"] = new Dictionary<string, object> { { "value", Script2Param5 }, { "py_type", "int" } };
            data["script2_param6"] = new Dictionary<string, object> { { "value", Script2Param6 }, { "py_type", "str" } };
            data["quest"] = new Dictionary<string, object> { { "value", Quest }, { "py_type", "str" } };
            data["text"] = new Dictionary<string, object> { { "value", Text?.ToDictionary() }, { "py_type", "LocalizedString" } };
            data["script1"] = new Dictionary<string, object> { { "value", Script1?.ToString() ?? "" }, { "py_type", "ResRef" } };
            data["script2"] = new Dictionary<string, object> { { "value", Script2?.ToString() ?? "" }, { "py_type", "ResRef" } };
            data["sound"] = new Dictionary<string, object> { { "value", Sound?.ToString() ?? "" }, { "py_type", "ResRef" } };
            data["vo_resref"] = new Dictionary<string, object> { { "value", VoResRef?.ToString() ?? "" }, { "py_type", "ResRef" } };

            // Serialize links
            // Matching PyKotor implementation: always serialize links, even if empty
            List<Dictionary<string, object>> linksList = new List<Dictionary<string, object>>();
            if (Links != null)
            {
                foreach (DLGLink link in Links)
                {
                    if (link != null)
                    {
                        linksList.Add(link.ToDict(nodeMap));
                    }
                }
            }
            data["links"] = new Dictionary<string, object>
            {
                { "value", linksList },
                { "py_type", "list" }
            };

            // Serialize animations
            List<Dictionary<string, object>> animsList = new List<Dictionary<string, object>>();
            foreach (DLGAnimation anim in Animations)
            {
                animsList.Add(anim.ToDict());
            }
            data["animations"] = new Dictionary<string, object>
            {
                { "value", animsList },
                { "py_type", "list" }
            };

            // Serialize optional fields
            if (QuestEntry.HasValue)
            {
                data["quest_entry"] = new Dictionary<string, object> { { "value", QuestEntry.Value }, { "py_type", "int" } };
            }
            else
            {
                data["quest_entry"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (FadeDelay.HasValue)
            {
                data["fade_delay"] = new Dictionary<string, object> { { "value", FadeDelay.Value }, { "py_type", "float" } };
            }
            else
            {
                data["fade_delay"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (FadeLength.HasValue)
            {
                data["fade_length"] = new Dictionary<string, object> { { "value", FadeLength.Value }, { "py_type", "float" } };
            }
            else
            {
                data["fade_length"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (CameraAnim.HasValue)
            {
                data["camera_anim"] = new Dictionary<string, object> { { "value", CameraAnim.Value }, { "py_type", "int" } };
            }
            else
            {
                data["camera_anim"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (CameraId.HasValue)
            {
                data["camera_id"] = new Dictionary<string, object> { { "value", CameraId.Value }, { "py_type", "int" } };
            }
            else
            {
                data["camera_id"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (CameraFov.HasValue)
            {
                data["camera_fov"] = new Dictionary<string, object> { { "value", CameraFov.Value }, { "py_type", "float" } };
            }
            else
            {
                data["camera_fov"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (CameraHeight.HasValue)
            {
                data["camera_height"] = new Dictionary<string, object> { { "value", CameraHeight.Value }, { "py_type", "float" } };
            }
            else
            {
                data["camera_height"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (CameraEffect.HasValue)
            {
                data["camera_effect"] = new Dictionary<string, object> { { "value", CameraEffect.Value }, { "py_type", "int" } };
            }
            else
            {
                data["camera_effect"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (TargetHeight.HasValue)
            {
                data["target_height"] = new Dictionary<string, object> { { "value", TargetHeight.Value }, { "py_type", "float" } };
            }
            else
            {
                data["target_height"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            if (FadeColor != null)
            {
                data["fade_color"] = new Dictionary<string, object> { { "value", FadeColor.ToBgrInteger() }, { "py_type", "Color" } };
            }
            else
            {
                data["fade_color"] = new Dictionary<string, object> { { "value", null }, { "py_type", "None" } };
            }

            return nodeDict;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:394
        // Original: @staticmethod def from_dict(data: dict[str | int, Any], node_map: dict[str | int, Any] | None = None) -> DLGEntry | DLGReply:
        /// <summary>
        /// Deserializes a node from a dictionary representation.
        /// </summary>
        /// <param name="data">The dictionary data</param>
        /// <param name="nodeMap">Optional map to track deserialized nodes and handle references</param>
        /// <returns>A DLGEntry or DLGReply instance</returns>
        public static DLGNode FromDict(Dictionary<string, object> data, Dictionary<string, object> nodeMap = null)
        {
            if (nodeMap == null)
            {
                nodeMap = new Dictionary<string, object>();
            }

            if (data.ContainsKey("ref"))
            {
                // Return node from node_map - it should already be fully deserialized
                string refKey = data["ref"].ToString();
                if (!refKey.StartsWith("node-"))
                {
                    refKey = $"node-{refKey}";
                }
                if (nodeMap.ContainsKey(refKey) && nodeMap[refKey] is DLGNode existingNodeRef)
                {
                    return existingNodeRef;
                }
                throw new KeyNotFoundException($"Reference key {refKey} not found in node_map");
            }

            string nodeKey = data.ContainsKey("key") ? data["key"].ToString() : null;
            if (nodeKey == null)
            {
                throw new ArgumentException("Node data must contain 'key' or 'ref'");
            }

            // Normalize prefixed keys
            string nodeKeyStr = nodeKey;
            if (!nodeKeyStr.StartsWith("node-"))
            {
                nodeKeyStr = $"node-{nodeKeyStr}";
            }

            string nodeType = data.ContainsKey("type") ? data["type"].ToString() : null;
            Dictionary<string, object> nodeData = data.ContainsKey("data") ? (Dictionary<string, object>)data["data"] : new Dictionary<string, object>();

            DLGNode node;
            if (nodeType == "DLGEntry")
            {
                node = new DLGEntry();
                if (nodeData.ContainsKey("speaker"))
                {
                    Dictionary<string, object> speakerData = (Dictionary<string, object>)nodeData["speaker"];
                    ((DLGEntry)node).Speaker = speakerData.ContainsKey("value") ? speakerData["value"].ToString() : "";
                }
            }
            else if (nodeType == "DLGReply")
            {
                node = new DLGReply();
            }
            else
            {
                throw new ArgumentException($"Unknown node type: {nodeType}");
            }

            // Extract hash cache from key
            string hashStr = nodeKeyStr.Replace("node-", "");
            if (int.TryParse(hashStr, out int hashValue))
            {
                // Use reflection to set private _hashCache field
                var field = typeof(DLGNode).GetField("_hashCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(node, hashValue);
                }
            }

            // Process non-link fields first to ensure all attributes are set before adding to node_map
            foreach (KeyValuePair<string, object> kvp in nodeData)
            {
                string key = kvp.Key;
                object value = kvp.Value;

                if (!(value is Dictionary<string, object> valueDict))
                {
                    continue;
                }

                string pyType = valueDict.ContainsKey("py_type") ? valueDict["py_type"].ToString() : null;
                object actualValue = valueDict.ContainsKey("value") ? valueDict["value"] : null;

                if (pyType == "list" && key == "links")
                {
                    continue; // Process links after all other fields
                }

                // Convert snake_case to PascalCase for property name matching
                string propertyName = ConvertSnakeCaseToPascalCase(key);

                // Set property based on type
                var prop = node.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    if (pyType == "str")
                    {
                        prop.SetValue(node, actualValue?.ToString() ?? "");
                    }
                    else if (pyType == "int")
                    {
                        prop.SetValue(node, Convert.ToInt32(actualValue ?? 0));
                    }
                    else if (pyType == "float")
                    {
                        prop.SetValue(node, Convert.ToSingle(actualValue ?? 0.0f));
                    }
                    else if (pyType == "bool")
                    {
                        prop.SetValue(node, Convert.ToBoolean(actualValue ?? false));
                    }
                    else if (pyType == "ResRef")
                    {
                        prop.SetValue(node, actualValue != null ? new ResRef(actualValue.ToString()) : ResRef.FromBlank());
                    }
                    else if (pyType == "Color")
                    {
                        if (actualValue != null && int.TryParse(actualValue.ToString(), out int bgrInt))
                        {
                            prop.SetValue(node, Color.FromBgrInteger(bgrInt));
                        }
                    }
                    else if (pyType == "LocalizedString")
                    {
                        if (actualValue is Dictionary<string, object> locStringDict)
                        {
                            node.Text = LocalizedString.FromDictionary(locStringDict);
                        }
                    }
                    else if (pyType == "list" && key == "animations")
                    {
                        if (actualValue is List<object> animsList)
                        {
                            List<DLGAnimation> animations = new List<DLGAnimation>();
                            foreach (object animObj in animsList)
                            {
                                if (animObj is Dictionary<string, object> animDict)
                                {
                                    animations.Add(DLGAnimation.FromDict(animDict));
                                }
                            }
                            node.Animations = animations;
                        }
                    }
                    else if (pyType == "None" || actualValue == null)
                    {
                        // Set nullable properties to null
                        if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            prop.SetValue(node, null);
                        }
                    }
                }
            }

            // Add to node_map AFTER all non-link fields are set
            nodeMap[nodeKeyStr] = node;

            // Process links after all other fields are set and node is in node_map
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:467-483
            // Python iterates through node_data.items() again in the second loop
            foreach (KeyValuePair<string, object> kvp in nodeData)
            {
                string key = kvp.Key;
                object value = kvp.Value;

                if (!(value is Dictionary<string, object> valueDict))
                {
                    continue;
                }

                string pyType = valueDict.ContainsKey("py_type") ? valueDict["py_type"].ToString() : null;
                object actualValue = valueDict.ContainsKey("value") ? valueDict["value"] : null;

                if (pyType == "list" && key == "links")
                {
                    // Matching PyKotor implementation: always deserialize links, even if empty
                    List<DLGLink> links = new List<DLGLink>();
                    if (actualValue != null)
                    {
                        // Handle both List<object> and List<Dictionary<string, object>> cases
                        if (actualValue is List<object> linksListObj)
                        {
                            foreach (object linkObj in linksListObj)
                            {
                                if (linkObj is Dictionary<string, object> linkDict)
                                {
                                    DLGLink deserializedLink = DLGLink.FromDict(linkDict, nodeMap);
                                    if (deserializedLink != null)
                                    {
                                        links.Add(deserializedLink);
                                    }
                                }
                            }
                        }
                        else if (actualValue is List<Dictionary<string, object>> linksListDict)
                        {
                            foreach (Dictionary<string, object> linkDict in linksListDict)
                            {
                                DLGLink deserializedLink = DLGLink.FromDict(linkDict, nodeMap);
                                if (deserializedLink != null)
                                {
                                    links.Add(deserializedLink);
                                }
                            }
                        }
                        else if (actualValue is System.Collections.IEnumerable linksEnumerable)
                        {
                            foreach (object linkObj in linksEnumerable)
                            {
                                if (linkObj is Dictionary<string, object> linkDict)
                                {
                                    DLGLink deserializedLink = DLGLink.FromDict(linkDict, nodeMap);
                                    if (deserializedLink != null)
                                    {
                                        links.Add(deserializedLink);
                                    }
                                }
                            }
                        }
                    }
                    // Always set Links, even if empty, to match Python behavior
                    node.Links = links;
                }
            }

            // If Links wasn't set (links key missing), initialize empty list to match Python behavior
            if (node.Links == null)
            {
                node.Links = new List<DLGLink>();
            }

            return node;
        }

        /// <summary>
        /// Converts snake_case string to PascalCase.
        /// Example: "camera_angle" -> "CameraAngle"
        /// </summary>
        private static string ConvertSnakeCaseToPascalCase(string snakeCase)
        {
            if (string.IsNullOrEmpty(snakeCase))
            {
                return snakeCase;
            }

            string[] parts = snakeCase.Split('_');
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            foreach (string part in parts)
            {
                if (part.Length > 0)
                {
                    result.Append(char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                    {
                        result.Append(part.Substring(1));
                    }
                }
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// Replies are nodes that are responses by the player.
    /// </summary>
    [PublicAPI]
    public sealed class DLGReply : DLGNode
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:488
        // Original: class DLGReply(DLGNode):
        public DLGReply() : base()
        {
        }

        // Matching PyKotor API: DLGReply.from_dict() calls DLGNode.from_dict() but returns DLGReply
        // In Python, static methods can be called from subclasses. For C# API compatibility, we add convenience methods.
        /// <summary>
        /// Deserializes a DLGReply from a dictionary representation.
        /// </summary>
        /// <param name="data">The dictionary data</param>
        /// <param name="nodeMap">Optional map to track deserialized nodes and handle references</param>
        /// <returns>A DLGReply instance</returns>
        public static new DLGReply FromDict(Dictionary<string, object> data, Dictionary<string, object> nodeMap = null)
        {
            DLGNode node = DLGNode.FromDict(data, nodeMap);
            if (node is DLGReply reply)
            {
                return reply;
            }
            throw new ArgumentException("Dictionary data does not represent a DLGReply node");
        }
    }

    /// <summary>
    /// Entries are nodes that are responses by NPCs.
    /// </summary>
    [PublicAPI]
    public sealed class DLGEntry : DLGNode
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:502
        // Original: class DLGEntry(DLGNode):
        public string Speaker { get; set; } = string.Empty;

        public DLGEntry() : base()
        {
        }

        public int? AnimationId
        {
            get => CameraAnim;
            set => CameraAnim = value;
        }

        // Matching PyKotor API: DLGEntry.from_dict() calls DLGNode.from_dict() but returns DLGEntry
        // In Python, static methods can be called from subclasses. For C# API compatibility, we add convenience methods.
        /// <summary>
        /// Deserializes a DLGEntry from a dictionary representation.
        /// </summary>
        /// <param name="data">The dictionary data</param>
        /// <param name="nodeMap">Optional map to track deserialized nodes and handle references</param>
        /// <returns>A DLGEntry instance</returns>
        public static new DLGEntry FromDict(Dictionary<string, object> data, Dictionary<string, object> nodeMap = null)
        {
            DLGNode node = DLGNode.FromDict(data, nodeMap);
            if (node is DLGEntry entry)
            {
                return entry;
            }
            throw new ArgumentException("Dictionary data does not represent a DLGEntry node");
        }
    }
}
