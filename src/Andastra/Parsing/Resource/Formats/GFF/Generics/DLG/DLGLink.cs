using System;
using System.Collections;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics.DLG
{
    /// <summary>
    /// Represents a directed edge from a source node to a target node (DLGNode).
    /// </summary>
    [PublicAPI]
    public sealed class DLGLink : IEquatable<DLGLink>, IEnumerable<DLGLink>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/links.py:21
        // Original: class DLGLink(Generic[T_co]):
        private readonly int _hashCache;

        public DLGNode Node { get; set; }
        public int ListIndex { get; set; } = -1;

        // Conditional scripts
        public ResRef Active1 { get; set; } = ResRef.FromBlank();
        public ResRef Active2 { get; set; } = ResRef.FromBlank();
        public bool Logic { get; set; }
        public bool Active1Not { get; set; }
        public bool Active2Not { get; set; }

        // Script parameters
        public int Active1Param1 { get; set; }
        public int Active1Param2 { get; set; }
        public int Active1Param3 { get; set; }
        public int Active1Param4 { get; set; }
        public int Active1Param5 { get; set; }
        public string Active1Param6 { get; set; } = string.Empty;
        public int Active2Param1 { get; set; }
        public int Active2Param2 { get; set; }
        public int Active2Param3 { get; set; }
        public int Active2Param4 { get; set; }
        public int Active2Param5 { get; set; }
        public string Active2Param6 { get; set; } = string.Empty;

        // Other
        public bool IsChild { get; set; }
        public string Comment { get; set; } = string.Empty;

        public DLGLink(DLGNode node, int listIndex = -1)
        {
            _hashCache = Guid.NewGuid().GetHashCode();
            Node = node;
            ListIndex = listIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is DLGLink other && Equals(other);
        }

        public bool Equals(DLGLink other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        public string PartialPath(bool isStarter)
        {
            string p1 = isStarter ? "StartingList" : (Node is DLGEntry ? "EntriesList" : "RepliesList");
            return $"{p1}\\{ListIndex}";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/links.py:168
        // Original: def to_dict(self, node_map: dict[str | int, Any] | None = None) -> dict[str | int, Any]:
        /// <summary>
        /// Serializes this link to a dictionary representation.
        /// </summary>
        /// <param name="nodeMap">Optional map to track serialized links and prevent duplicates</param>
        /// <returns>A dictionary representation of this link</returns>
        public Dictionary<string, object> ToDict(Dictionary<string, object> nodeMap = null)
        {
            if (nodeMap == null)
            {
                nodeMap = new Dictionary<string, object>();
            }

            // Use the stored hash cache directly
            string linkKey = $"link-{_hashCache}";
            if (nodeMap.ContainsKey(linkKey))
            {
                return new Dictionary<string, object>
                {
                    { "type", GetType().Name },
                    { "ref", linkKey }
                };
            }

            Dictionary<string, object> linkDict = new Dictionary<string, object>
            {
                { "type", GetType().Name },
                { "key", linkKey },
                { "node", Node?.ToDict(nodeMap) },
                { "link_list_index", ListIndex },
                { "data", new Dictionary<string, object>() }
            };

            Dictionary<string, object> data = (Dictionary<string, object>)linkDict["data"];

            // Serialize all properties except node, list_index, and _hash_cache
            data["active1"] = new Dictionary<string, object> { { "value", Active1?.ToString() ?? "" }, { "py_type", "ResRef" } };
            data["active2"] = new Dictionary<string, object> { { "value", Active2?.ToString() ?? "" }, { "py_type", "ResRef" } };
            data["logic"] = new Dictionary<string, object> { { "value", Logic ? 1 : 0 }, { "py_type", "bool" } };
            data["active1_not"] = new Dictionary<string, object> { { "value", Active1Not ? 1 : 0 }, { "py_type", "bool" } };
            data["active2_not"] = new Dictionary<string, object> { { "value", Active2Not ? 1 : 0 }, { "py_type", "bool" } };
            data["active1_param1"] = new Dictionary<string, object> { { "value", Active1Param1 }, { "py_type", "int" } };
            data["active1_param2"] = new Dictionary<string, object> { { "value", Active1Param2 }, { "py_type", "int" } };
            data["active1_param3"] = new Dictionary<string, object> { { "value", Active1Param3 }, { "py_type", "int" } };
            data["active1_param4"] = new Dictionary<string, object> { { "value", Active1Param4 }, { "py_type", "int" } };
            data["active1_param5"] = new Dictionary<string, object> { { "value", Active1Param5 }, { "py_type", "int" } };
            data["active1_param6"] = new Dictionary<string, object> { { "value", Active1Param6 }, { "py_type", "str" } };
            data["active2_param1"] = new Dictionary<string, object> { { "value", Active2Param1 }, { "py_type", "int" } };
            data["active2_param2"] = new Dictionary<string, object> { { "value", Active2Param2 }, { "py_type", "int" } };
            data["active2_param3"] = new Dictionary<string, object> { { "value", Active2Param3 }, { "py_type", "int" } };
            data["active2_param4"] = new Dictionary<string, object> { { "value", Active2Param4 }, { "py_type", "int" } };
            data["active2_param5"] = new Dictionary<string, object> { { "value", Active2Param5 }, { "py_type", "int" } };
            data["active2_param6"] = new Dictionary<string, object> { { "value", Active2Param6 }, { "py_type", "str" } };
            data["is_child"] = new Dictionary<string, object> { { "value", IsChild ? 1 : 0 }, { "py_type", "bool" } };
            data["comment"] = new Dictionary<string, object> { { "value", Comment }, { "py_type", "str" } };

            nodeMap[linkKey] = linkDict;

            return linkDict;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/links.py:216
        // Original: @classmethod def from_dict(cls, link_dict: dict[str | int, Any], node_map: dict[str | int, Any] | None = None) -> DLGLink[T_co]:
        /// <summary>
        /// Deserializes a link from a dictionary representation.
        /// </summary>
        /// <param name="linkDict">The dictionary data</param>
        /// <param name="nodeMap">Optional map to track deserialized links and handle references</param>
        /// <returns>A DLGLink instance</returns>
        public static DLGLink FromDict(Dictionary<string, object> linkDict, Dictionary<string, object> nodeMap = null)
        {
            if (nodeMap == null)
            {
                nodeMap = new Dictionary<string, object>();
            }

            if (linkDict.ContainsKey("ref"))
            {
                // Return link from node_map - it should already be fully deserialized
                string refKey = linkDict["ref"].ToString();
                if (!refKey.StartsWith("link-"))
                {
                    refKey = $"link-{refKey}";
                }
                if (nodeMap.ContainsKey(refKey) && nodeMap[refKey] is DLGLink existingLinkRef)
                {
                    return existingLinkRef;
                }
                throw new KeyNotFoundException($"Reference key {refKey} not found in node_map");
            }

            object linkKeyRaw = linkDict.ContainsKey("key") ? linkDict["key"] : null;
            if (linkKeyRaw == null)
            {
                throw new ArgumentException("Link data must contain 'key' or 'ref'");
            }

            string linkKey = linkKeyRaw.ToString();
            if (!linkKey.StartsWith("link-"))
            {
                linkKey = $"link-{linkKey}";
            }

            if (nodeMap.ContainsKey(linkKey) && nodeMap[linkKey] is DLGLink existingLink)
            {
                return existingLink;
            }

            DLGLink link = new DLGLink(null, -1);
            link.ListIndex = linkDict.ContainsKey("link_list_index") ? Convert.ToInt32(linkDict["link_list_index"]) : -1;

            // Extract hash cache from key
            string hashStr = linkKey.Replace("link-", "");
            if (int.TryParse(hashStr, out int hashValue))
            {
                // Use reflection to set private _hashCache field
                var field = typeof(DLGLink).GetField("_hashCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(link, hashValue);
                }
            }

            // Process data fields
            if (linkDict.ContainsKey("data") && linkDict["data"] is Dictionary<string, object> data)
            {
                foreach (KeyValuePair<string, object> kvp in data)
                {
                    string key = kvp.Key;
                    object value = kvp.Value;

                    if (!(value is Dictionary<string, object> valueDict))
                    {
                        continue;
                    }

                    string pyType = valueDict.ContainsKey("py_type") ? valueDict["py_type"].ToString() : null;
                    object actualValue = valueDict.ContainsKey("value") ? valueDict["value"] : null;

                    // Set property based on type
                    var prop = link.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (prop != null && prop.CanWrite)
                    {
                        if (pyType == "str")
                        {
                            prop.SetValue(link, actualValue?.ToString() ?? "");
                        }
                        else if (pyType == "int")
                        {
                            prop.SetValue(link, Convert.ToInt32(actualValue ?? 0));
                        }
                        else if (pyType == "float")
                        {
                            prop.SetValue(link, Convert.ToSingle(actualValue ?? 0.0f));
                        }
                        else if (pyType == "bool")
                        {
                            prop.SetValue(link, Convert.ToBoolean(actualValue ?? false));
                        }
                        else if (pyType == "ResRef")
                        {
                            prop.SetValue(link, actualValue != null ? new ResRef(actualValue.ToString()) : ResRef.FromBlank());
                        }
                        else if (pyType == "None" || actualValue == null)
                        {
                            // Set nullable properties to null
                            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                prop.SetValue(link, null);
                            }
                        }
                    }
                }
            }

            // Set the node BEFORE adding to node_map to ensure the link is fully constructed
            if (linkDict.ContainsKey("node") && linkDict["node"] != null)
            {
                if (linkDict["node"] is Dictionary<string, object> nodeDict)
                {
                    link.Node = DLGNode.FromDict(nodeDict, nodeMap);
                }
            }
            else
            {
                link.Node = null;
            }

            nodeMap[linkKey] = link;

            return link;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/links.py:128
        // Original: def __iter__(self) -> Generator[DLGLink[T_co], Any, None]:
        /// <summary>
        /// Iterate over nested links without recursion.
        /// </summary>
        public IEnumerator<DLGLink> GetEnumerator()
        {
            Stack<DLGLink> stack = new Stack<DLGLink>();
            stack.Push(this);
            HashSet<DLGLink> seen = new HashSet<DLGLink>();

            while (stack.Count > 0)
            {
                DLGLink current = stack.Pop();
                if (seen.Contains(current))
                {
                    continue; // Avoid infinite loops in circular references
                }
                seen.Add(current);
                yield return current;

                if (current.Node != null && current.Node.Links != null)
                {
                    // Push links in reverse order to maintain order (stack is LIFO)
                    for (int i = current.Node.Links.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.Node.Links[i]);
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
