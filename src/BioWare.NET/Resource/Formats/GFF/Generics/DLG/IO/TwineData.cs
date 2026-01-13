using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.DLG.IO
{
    /// <summary>
    /// Simple 2D vector for Twine position/size metadata.
    /// </summary>
    public struct TwineVector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public TwineVector2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Type of passage in Twine.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:21-26
    /// </summary>
    public enum PassageType
    {
        Entry,  // NPC dialog entry
        Reply   // Player dialog reply
    }

    /// <summary>
    /// Metadata for a Twine story.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:28-42
    /// </summary>
    [PublicAPI]
    public class TwineMetadata
    {
        public string Name { get; set; } = "Converted Dialog";
        public string Ifid { get; set; } = "";  // Unique story identifier
        public string Format { get; set; } = "Harlowe";  // Story format name
        public string FormatVersion { get; set; } = "3.3.7";  // Story format version
        public float Zoom { get; set; } = 1.0f;  // Editor zoom level
        public string Creator { get; set; } = "BioWare.NET";  // Creator tool name
        public string CreatorVersion { get; set; } = "1.0.0";  // Creator tool version
        public string Style { get; set; } = "";  // Custom CSS
        public string Script { get; set; } = "";  // Custom JavaScript
        public Dictionary<string, Color> TagColors { get; set; } = new Dictionary<string, Color>();  // Tag color mapping
    }

    /// <summary>
    /// Metadata for a Twine passage.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:44-61
    /// </summary>
    [PublicAPI]
    public class PassageMetadata
    {
        public TwineVector2 Position { get; set; } = new TwineVector2(0.0f, 0.0f);  // Position in editor
        public TwineVector2 Size { get; set; } = new TwineVector2(100.0f, 100.0f);  // Size in editor
        // KotOR-specific metadata
        public string Speaker { get; set; } = "";  // For entries only
        public int AnimationId { get; set; } = 0;  // Animation to play (0 means not set, use null for DLGNode.camera_anim)
        public int CameraAngle { get; set; } = 0;  // Camera angle
        public int? CameraId { get; set; } = null;  // Camera ID (null means not set, matching DLGNode default)
        public int FadeType { get; set; } = 0;  // Type of fade
        public string Quest { get; set; } = "";  // Associated quest
        public string Sound { get; set; } = "";  // Sound to play
        public string VoResref { get; set; } = "";  // Voice-over resource
        // Additional metadata as needed
        public Dictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// A link between passages in Twine.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:63-73
    /// </summary>
    [PublicAPI]
    public class TwineLink
    {
        public string Text { get; set; } = "";  // Display text
        public string Target { get; set; } = "";  // Target passage name
        // KotOR-specific metadata
        public bool IsChild { get; set; } = false;  // Child link flag
        public string ActiveScript { get; set; } = "";  // Activation script
        public List<string> ActiveParams { get; set; } = new List<string>();  // Script parameters
    }

    /// <summary>
    /// A passage in a Twine story.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:75-86
    /// </summary>
    [PublicAPI]
    public class TwinePassage
    {
        public string Name { get; set; } = "";  // Passage name
        public string Text { get; set; } = "";  // Passage content
        public PassageType Type { get; set; } = PassageType.Entry;  // Entry or Reply
        public string Pid { get; set; } = "";  // Passage ID
        public List<string> Tags { get; set; } = new List<string>();  // Passage tags
        public PassageMetadata Metadata { get; set; } = new PassageMetadata();
        public List<TwineLink> Links { get; set; } = new List<TwineLink>();
    }

    /// <summary>
    /// A complete Twine story.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:88-122
    /// </summary>
    [PublicAPI]
    public class TwineStory
    {
        public TwineMetadata Metadata { get; set; } = new TwineMetadata();
        public List<TwinePassage> Passages { get; set; } = new List<TwinePassage>();
        public string StartPid { get; set; } = "";  // Starting passage ID

        /// <summary>
        /// Get the starting passage.
        /// </summary>
        public TwinePassage GetStartPassage()
        {
            return Passages.FirstOrDefault(p => p.Pid == StartPid);
        }

        /// <summary>
        /// Get a passage by name.
        /// </summary>
        public TwinePassage GetPassage(string name)
        {
            return Passages.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// Get all passages of a specific type.
        /// </summary>
        public List<TwinePassage> GetPassagesByType(PassageType type)
        {
            return Passages.Where(p => p.Type == type).ToList();
        }

        /// <summary>
        /// Get all entry passages.
        /// </summary>
        public List<TwinePassage> GetEntries()
        {
            return GetPassagesByType(PassageType.Entry);
        }

        /// <summary>
        /// Get all reply passages.
        /// </summary>
        public List<TwinePassage> GetReplies()
        {
            return GetPassagesByType(PassageType.Reply);
        }

        /// <summary>
        /// Get all links pointing to a passage.
        /// </summary>
        public List<Tuple<TwinePassage, TwineLink>> GetLinksTo(TwinePassage passage)
        {
            var result = new List<Tuple<TwinePassage, TwineLink>>();
            foreach (var p in Passages)
            {
                foreach (var link in p.Links)
                {
                    if (link.Target == passage.Name)
                    {
                        result.Add(Tuple.Create(p, link));
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Handles conversion between KotOR and Twine formats.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:131-334
    /// </summary>
    [PublicAPI]
    public class FormatConverter
    {
        private readonly HashSet<string> _kotorOnlyFeatures = new HashSet<string>
        {
            "animation_id",
            "camera_angle",
            "camera_id",
            "fade_type",
            "quest",
            "sound",
            "vo_resref",
        };

        private readonly HashSet<string> _twineOnlyFeatures = new HashSet<string>
        {
            "style",
            "script",
            "tag_colors",
        };

        /// <summary>
        /// Store KotOR-specific metadata in a Twine passage.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:169-194
        /// </summary>
        public void StoreKotorMetadata(TwinePassage passage, DLGNode dlgNode)
        {
            if (passage == null || dlgNode == null)
            {
                return;
            }

            var meta = passage.Metadata;
            // camera_anim in DLGNode maps to animation_id in PassageMetadata
            meta.AnimationId = dlgNode.CameraAnim ?? 0;
            meta.CameraAngle = dlgNode.CameraAngle;
            meta.CameraId = dlgNode.CameraId;
            meta.FadeType = dlgNode.FadeType;
            meta.Quest = dlgNode.Quest ?? "";
            // ResRef objects are converted to strings for storage
            meta.Sound = dlgNode.Sound?.ToString() ?? "";
            meta.VoResref = dlgNode.VoResRef?.ToString() ?? "";

            if (dlgNode is DLGEntry entry)
            {
                meta.Speaker = entry.Speaker ?? "";
            }
        }

        /// <summary>
        /// Restore KotOR-specific metadata from a Twine passage.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:196-242
        /// </summary>
        public void RestoreKotorMetadata(DLGNode dlgNode, TwinePassage passage)
        {
            if (dlgNode == null || passage == null)
            {
                return;
            }

            var meta = passage.Metadata;
            // animation_id in PassageMetadata maps to camera_anim in DLGNode
            // 0 means not set (use null), non-zero means explicitly set
            if (meta.AnimationId != 0)
            {
                dlgNode.CameraAnim = meta.AnimationId;
            }

            // camera_angle is always an int (defaults to 0, which is valid)
            dlgNode.CameraAngle = meta.CameraAngle;

            // camera_id can be null or int - null means not set (new file), int means explicitly set
            if (meta.CameraId.HasValue)
            {
                dlgNode.CameraId = meta.CameraId;
            }

            // fade_type is always an int (defaults to 0, which is valid)
            dlgNode.FadeType = meta.FadeType;

            // quest is a string (defaults to "", which is valid)
            dlgNode.Quest = meta.Quest ?? "";

            // sound is a ResRef, stored as string in metadata
            // Only set if non-empty (empty string means not set for new files)
            if (!string.IsNullOrEmpty(meta.Sound))
            {
                dlgNode.Sound = new BioWare.NET.Common.ResRef(meta.Sound);
            }

            // vo_resref is a ResRef, stored as string in metadata
            // Only set if non-empty (empty string means not set for new files)
            if (!string.IsNullOrEmpty(meta.VoResref))
            {
                dlgNode.VoResRef = new BioWare.NET.Common.ResRef(meta.VoResref);
            }

            if (dlgNode is DLGEntry entry && !string.IsNullOrEmpty(meta.Speaker))
            {
                entry.Speaker = meta.Speaker;
            }
        }

        /// <summary>
        /// Store Twine-specific metadata in a dialog.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:244-271
        /// </summary>
        public void StoreTwineMetadata(TwineStory story, DLG dlg)
        {
            if (story == null || dlg == null)
            {
                return;
            }

            // Store Twine metadata in dialog's comment field as JSON
            // Matching PyKotor implementation: Serializes Twine-specific metadata to JSON string
            // Based on Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:244-271
            var twineData = new Dictionary<string, object>
            {
                { "style", story.Metadata.Style ?? "" },
                { "script", story.Metadata.Script ?? "" },
                { "tag_colors", story.Metadata.TagColors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()) },
                { "format", story.Metadata.Format ?? "" },
                { "format_version", story.Metadata.FormatVersion ?? "" },
                { "creator", story.Metadata.Creator ?? "" },
                { "creator_version", story.Metadata.CreatorVersion ?? "" },
                { "zoom", story.Metadata.Zoom },
            };

            // Serialize to JSON string with proper formatting options
            // Matching PyKotor implementation: Uses json.dumps for serialization
            // Note: For dialog comment field, we use compact format (no indentation) to save space
            // Full story JSON files use indented format (see Twine.cs WriteJson method)
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false  // Compact format for comment field storage
                };
                dlg.Comment = JsonSerializer.Serialize(twineData, options);
            }
            catch (Exception)
            {
                // If serialization fails, set empty comment to avoid corrupting dialog data
                // This should not happen in normal operation, but provides safety
                dlg.Comment = "";
            }
        }

        /// <summary>
        /// Restore Twine-specific metadata from a dialog.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine_data.py:273-322
        /// </summary>
        public void RestoreTwineMetadata(DLG dlg, TwineStory story)
        {
            if (dlg == null || story == null || string.IsNullOrEmpty(dlg.Comment))
            {
                return;
            }

            try
            {
                // Deserialize from JSON string
                var twineData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dlg.Comment);
                if (twineData == null)
                {
                    return;
                }

                story.Metadata.Style = twineData.ContainsKey("style") ? twineData["style"].ToString() : "";
                story.Metadata.Script = twineData.ContainsKey("script") ? twineData["script"].ToString() : "";

                // Restore tag_colors, converting string representations back to Color objects
                if (twineData.ContainsKey("tag_colors") && twineData["tag_colors"] is Dictionary<string, object> tagColorsRaw)
                {
                    var tagColorsRestored = new Dictionary<string, Color>();
                    foreach (var kvp in tagColorsRaw)
                    {
                        if (kvp.Value is string colorValue)
                        {
                            // Parse string format "r g b a" back to Color object
                            var components = colorValue.Split(' ');
                            if (components.Length == 4)
                            {
                                if (float.TryParse(components[0], out float r) &&
                                    float.TryParse(components[1], out float g) &&
                                    float.TryParse(components[2], out float b) &&
                                    float.TryParse(components[3], out float a))
                                {
                                    tagColorsRestored[kvp.Key] = new Color(r, g, b, a);
                                }
                            }
                            else if (components.Length == 3)
                            {
                                if (float.TryParse(components[0], out float r) &&
                                    float.TryParse(components[1], out float g) &&
                                    float.TryParse(components[2], out float b))
                                {
                                    tagColorsRestored[kvp.Key] = new Color(r, g, b, 1.0f);
                                }
                            }
                        }
                    }
                    story.Metadata.TagColors = tagColorsRestored;
                }

                story.Metadata.Format = twineData.ContainsKey("format") ? twineData["format"].ToString() : "Harlowe";
                story.Metadata.FormatVersion = twineData.ContainsKey("format_version") ? twineData["format_version"].ToString() : "3.3.7";
                story.Metadata.Creator = twineData.ContainsKey("creator") ? twineData["creator"].ToString() : "BioWare.NET";
                story.Metadata.CreatorVersion = twineData.ContainsKey("creator_version") ? twineData["creator_version"].ToString() : "1.0.0";
                if (twineData.ContainsKey("zoom"))
                {
                    if (float.TryParse(twineData["zoom"].ToString(), out float zoom))
                    {
                        story.Metadata.Zoom = zoom;
                    }
                }
            }
            catch
            {
                // If metadata restoration fails, keep defaults
            }
        }
    }
}

