using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Editors.DLG
{
    /// <summary>
    /// Categories of dialogue nodes.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/node_types.py:14-20
    /// </summary>
    public enum NodeCategory
    {
        Entry,    // NPC dialogue line
        Reply,    // Player response
        Starter   // Entry point node
    }

    /// <summary>
    /// Visual styles for nodes.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/node_types.py:22-31
    /// </summary>
    public enum NodeStyle
    {
        Default,     // Standard node appearance
        Highlighted, // Currently selected/focused
        Disabled,    // Conditions not met
        Error,       // Invalid state
        Warning,     // Potential issues
        Success      // Conditions met/valid
    }

    /// <summary>
    /// Rule for validating node connections.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/node_types.py:33-40
    /// </summary>
    public class NodeValidationRule
    {
        public NodeCategory SourceCategory { get; set; }
        public NodeCategory TargetCategory { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsRequired { get; set; } = false;
    }

    /// <summary>
    /// Metadata about a node type.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/node_types.py:43-54
    /// </summary>
    public class NodeTypeInfo
    {
        public NodeCategory Category { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool CanHaveChildren { get; set; }
        public bool CanHaveParent { get; set; }
        public int? MaxChildren { get; set; } = null; // null for unlimited
        public int MinChildren { get; set; } = 0;
        public List<NodeValidationRule> ValidationRules { get; set; } = new List<NodeValidationRule>();
    }

    /// <summary>
    /// Registry of available node types and validation rules.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/node_types.py:57-254
    /// </summary>
    public static class NodeTypes
    {
        // Default validation rules
        public static readonly List<NodeValidationRule> DefaultRules = new List<NodeValidationRule>
        {
            // Entry nodes can only connect to Reply nodes
            new NodeValidationRule
            {
                SourceCategory = NodeCategory.Entry,
                TargetCategory = NodeCategory.Reply,
                ErrorMessage = "Entry nodes can only connect to Reply nodes"
            },
            // Reply nodes can only connect to Entry nodes
            new NodeValidationRule
            {
                SourceCategory = NodeCategory.Reply,
                TargetCategory = NodeCategory.Entry,
                ErrorMessage = "Reply nodes can only connect to Entry nodes"
            },
            // Starter nodes must connect to Entry nodes
            new NodeValidationRule
            {
                SourceCategory = NodeCategory.Starter,
                TargetCategory = NodeCategory.Entry,
                ErrorMessage = "Starter nodes must connect to Entry nodes",
                IsRequired = true
            }
        };

        // Node type definitions
        public static readonly Dictionary<NodeCategory, NodeTypeInfo> Types = new Dictionary<NodeCategory, NodeTypeInfo>
        {
            [NodeCategory.Entry] = new NodeTypeInfo
            {
                Category = NodeCategory.Entry,
                DisplayName = "Entry",
                Description = "NPC dialogue line",
                CanHaveChildren = true,
                CanHaveParent = true,
                ValidationRules = new List<NodeValidationRule>
                {
                    new NodeValidationRule
                    {
                        SourceCategory = NodeCategory.Entry,
                        TargetCategory = NodeCategory.Reply,
                        ErrorMessage = "Entry nodes can only connect to Reply nodes"
                    }
                }
            },
            [NodeCategory.Reply] = new NodeTypeInfo
            {
                Category = NodeCategory.Reply,
                DisplayName = "Reply",
                Description = "Player response option",
                CanHaveChildren = true,
                CanHaveParent = true,
                ValidationRules = new List<NodeValidationRule>
                {
                    new NodeValidationRule
                    {
                        SourceCategory = NodeCategory.Reply,
                        TargetCategory = NodeCategory.Entry,
                        ErrorMessage = "Reply nodes can only connect to Entry nodes"
                    }
                }
            },
            [NodeCategory.Starter] = new NodeTypeInfo
            {
                Category = NodeCategory.Starter,
                DisplayName = "Start",
                Description = "Dialogue entry point",
                CanHaveChildren = true,
                CanHaveParent = false,
                MaxChildren = 1,
                ValidationRules = new List<NodeValidationRule>
                {
                    new NodeValidationRule
                    {
                        SourceCategory = NodeCategory.Starter,
                        TargetCategory = NodeCategory.Entry,
                        ErrorMessage = "Starter nodes must connect to Entry nodes",
                        IsRequired = true
                    }
                }
            }
        };

        /// <summary>
        /// Get type information for a node category.
        /// Matching PyKotor: @classmethod def get_type_info(cls, category: NodeCategory) -> NodeTypeInfo
        /// </summary>
        public static NodeTypeInfo GetTypeInfo(NodeCategory category)
        {
            return Types[category];
        }

        /// <summary>
        /// Validate a connection between two nodes.
        /// Matching PyKotor: @classmethod def validate_connection(cls, source_node: DLGNode, target_node: DLGNode) -> tuple[bool, str]
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateConnection(DLGNode sourceNode, DLGNode targetNode)
        {
            try
            {
                NodeCategory sourceCategory = GetNodeCategory(sourceNode);
                NodeCategory targetCategory = GetNodeCategory(targetNode);

                // Check source node's validation rules
                NodeTypeInfo sourceInfo = GetTypeInfo(sourceCategory);
                foreach (var rule in sourceInfo.ValidationRules)
                {
                    if (rule.SourceCategory == sourceCategory && rule.TargetCategory != targetCategory)
                    {
                        return (false, rule.ErrorMessage);
                    }
                }

                // Check target node's validation rules
                NodeTypeInfo targetInfo = GetTypeInfo(targetCategory);
                if (!targetInfo.CanHaveParent)
                {
                    return (false, $"{targetInfo.DisplayName} nodes cannot have parent nodes");
                }

                // Check child count limits
                if (sourceInfo.MaxChildren.HasValue)
                {
                    int childCount = sourceNode.Links.Count;
                    if (childCount >= sourceInfo.MaxChildren.Value)
                    {
                        return (false, $"{sourceInfo.DisplayName} nodes cannot have more than {sourceInfo.MaxChildren.Value} children");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error validating connection between {sourceNode} and {targetNode}: {ex}");
                return (false, "Error validating connection");
            }

            return (true, "");
        }

        /// <summary>
        /// Validate a node's current state.
        /// Matching PyKotor: @classmethod def validate_node(cls, node: DLGNode) -> tuple[bool, list[str]]
        /// </summary>
        public static (bool isValid, List<string> errors) ValidateNode(DLGNode node)
        {
            try
            {
                List<string> errors = new List<string>();
                NodeCategory category = GetNodeCategory(node);
                NodeTypeInfo typeInfo = GetTypeInfo(category);

                // Check minimum children requirement
                if (node.Links.Count < typeInfo.MinChildren)
                {
                    errors.Add($"{typeInfo.DisplayName} nodes must have at least {typeInfo.MinChildren} children");
                }

                // Check maximum children limit
                if (typeInfo.MaxChildren.HasValue && node.Links.Count > typeInfo.MaxChildren.Value)
                {
                    errors.Add($"{typeInfo.DisplayName} nodes cannot have more than {typeInfo.MaxChildren.Value} children");
                }

                // Check required connection rules
                foreach (var rule in typeInfo.ValidationRules)
                {
                    if (rule.IsRequired)
                    {
                        bool hasRequired = node.Links.Any(link => GetNodeCategory(link.Node) == rule.TargetCategory);
                        if (!hasRequired)
                        {
                            errors.Add(rule.ErrorMessage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error validating node {node}: {ex}");
                return (false, new List<string> { "Error validating node" });
            }

            return (true, new List<string>());
        }

        /// <summary>
        /// Determine the category of a node.
        /// Matching PyKotor: @classmethod def get_node_category(cls, node: DLGNode) -> NodeCategory
        /// </summary>
        public static NodeCategory GetNodeCategory(DLGNode node)
        {
            if (node is DLGEntry)
            {
                return NodeCategory.Entry;
            }
            if (node is DLGReply)
            {
                return NodeCategory.Reply;
            }
            throw new ArgumentException($"Unknown node type: {node.GetType()}");
        }

        /// <summary>
        /// Get the visual style for a node.
        /// Matching PyKotor: @classmethod def get_node_style(cls, node: DLGNode, link: DLGLink | None = None) -> NodeStyle
        /// </summary>
        public static NodeStyle GetNodeStyle(DLGNode node, DLGLink link = null)
        {
            try
            {
                // Validate node
                (bool isValid, List<string> errors) = ValidateNode(node);
                if (!isValid)
                {
                    return NodeStyle.Error;
                }

                // Check if node has any links
                NodeCategory category = GetNodeCategory(node);
                NodeTypeInfo typeInfo = GetTypeInfo(category);
                if (node.Links.Count == 0 && typeInfo.MinChildren > 0)
                {
                    return NodeStyle.Warning;
                }

                // If link provided, check its conditions
                if (link != null && (link.Active1 != null || link.Active2 != null))
                {
                    return NodeStyle.Disabled;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error getting style for node {node}: {ex}");
                return NodeStyle.Error;
            }

            return NodeStyle.Default;
        }
    }
}

