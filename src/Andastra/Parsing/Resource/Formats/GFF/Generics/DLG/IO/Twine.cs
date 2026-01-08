using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource.Generics.DLG;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics.DLG.IO
{
    /// <summary>
    /// Twine format support for dialog system.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py
    /// </summary>
    [PublicAPI]
    public static class Twine
    {
        /// <summary>
        /// Parses a color string from Twine format.
        /// Supports multiple color formats:
        /// - Hex colors: #RRGGBB, #RRGGBBAA, #RGB, #RGBA (for HTML format)
        /// - Space-separated floats: "r g b a" or "r g b" (for JSON format, alpha defaults to 1.0)
        /// Based on PyKotor: HTML uses Color.from_hex_string(), JSON uses space-separated "r g b a" format
        /// </summary>
        /// <param name="colorStr">Color string to parse</param>
        /// <returns>Parsed Color object, or null if parsing fails</returns>
        private static Color ParseTwineColorString(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
            {
                return null;
            }

            colorStr = colorStr.Trim();

            // Try hex format first (for HTML format)
            // Based on PyKotor: HTML format uses Color.from_hex_string(ParsingColor)
            if (colorStr.StartsWith("#") ||
                (colorStr.Length >= 3 && colorStr.Length <= 8 && IsHexString(colorStr)))
            {
                try
                {
                    // ParsingColor.FromHexString handles # prefix and various hex formats
                    ParsingColor parsedColor = ParsingColor.FromHexString(colorStr);
                    return new Color(parsedColor);
                }
                catch (ArgumentException)
                {
                    // Invalid hex format, try space-separated format below
                }
            }

            // Try space-separated float format (for JSON format)
            // Based on PyKotor: JSON format uses "r g b a" or "r g b" (alpha defaults to 1.0)
            // Example: "1 0 0 1" or "0.5 0.3 0.2" or "1.0 0.0 0.0 1.0"
            var components = colorStr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (components.Length == 3 || components.Length == 4)
            {
                try
                {
                    if (float.TryParse(components[0], out float r) &&
                        float.TryParse(components[1], out float g) &&
                        float.TryParse(components[2], out float b))
                    {
                        float a = 1.0f; // Default alpha
                        if (components.Length == 4 && float.TryParse(components[3], out float parsedA))
                        {
                            a = parsedA;
                        }

                        // Clamp values to valid range [0.0, 1.0]
                        r = Math.Max(0.0f, Math.Min(1.0f, r));
                        g = Math.Max(0.0f, Math.Min(1.0f, g));
                        b = Math.Max(0.0f, Math.Min(1.0f, b));
                        a = Math.Max(0.0f, Math.Min(1.0f, a));

                        return new Color(r, g, b, a);
                    }
                }
                catch
                {
                    // Invalid format, return null below
                }
            }

            // Failed to parse
            return null;
        }

        /// <summary>
        /// Checks if a string contains only hexadecimal characters.
        /// </summary>
        private static bool IsHexString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }

            foreach (char c in str)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Reads a Twine file and converts it to a DLG.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:59-72
        /// </summary>
        public static DLG ReadTwine(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            string content = File.ReadAllText(path, Encoding.UTF8);
            TwineStory story;

            if (content.Trim().StartsWith("{"))
            {
                story = ReadJson(content);
            }
            else if (content.Trim().StartsWith("<"))
            {
                story = ReadHtml(content);
            }
            else
            {
                throw new ArgumentException("Invalid Twine format - must be HTML or JSON");
            }

            return StoryToDlg(story);
        }

        /// <summary>
        /// Writes a DLG to Twine format.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:75-100
        /// </summary>
        public static void WriteTwine(DLG dlg, string path, string format = null, Dictionary<string, object> metadata = null)
        {
            if (dlg == null)
            {
                throw new ArgumentNullException(nameof(dlg));
            }
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            // Infer format from extension if not provided
            string chosenFormat = format;
            if (string.IsNullOrEmpty(chosenFormat))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                chosenFormat = ext == ".json" ? "json" : "html";
            }

            TwineStory story = DlgToStory(dlg, metadata);

            if (chosenFormat == "json")
            {
                WriteJson(story, path);
            }
            else if (chosenFormat == "html")
            {
                WriteHtml(story, path);
            }
            else
            {
                throw new ArgumentException($"Invalid format: {chosenFormat}");
            }
        }

        /// <summary>
        /// Reads a Twine story from JSON format.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:103-214
        /// </summary>
        private static TwineStory ReadJson(string content)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON", ex);
            }

            var root = doc.RootElement;

            // Create metadata
            var twineMetadata = new TwineMetadata
            {
                Name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Converted Dialog" : "Converted Dialog",
                Ifid = root.TryGetProperty("ifid", out var ifidProp) ? ifidProp.GetString() ?? "" : "",
                Format = root.TryGetProperty("format", out var formatProp) ? formatProp.GetString() ?? "Harlowe" : "Harlowe",
                FormatVersion = root.TryGetProperty("format-version", out var formatVersionProp) ? formatVersionProp.GetString() ?? "3.3.7" : "3.3.7",
                Zoom = root.TryGetProperty("zoom", out var zoomProp) ? (float)(zoomProp.GetDouble()) : 1.0f,
                Creator = root.TryGetProperty("creator", out var creatorProp) ? creatorProp.GetString() ?? "Andastra" : "Andastra",
                CreatorVersion = root.TryGetProperty("creator-version", out var creatorVersionProp) ? creatorVersionProp.GetString() ?? "1.0.0" : "1.0.0",
                Style = root.TryGetProperty("style", out var styleProp) ? styleProp.GetString() ?? "" : "",
                Script = root.TryGetProperty("script", out var scriptProp) ? scriptProp.GetString() ?? "" : "",
            };

            // Get tag colors
            // Based on PyKotor: JSON format stores colors as strings in "r g b a" or "r g b" format
            // Example: "tag-colors": {"entry": "1 0 0 1", "reply": "0 1 0 1"}
            if (root.TryGetProperty("tag-colors", out var tagColorsProp) && tagColorsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in tagColorsProp.EnumerateObject())
                {
                    string colorStr = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(colorStr))
                    {
                        // Parse color string (supports hex and space-separated float formats)
                        Color parsedColor = ParseTwineColorString(colorStr);
                        if (parsedColor != null)
                        {
                            twineMetadata.TagColors[prop.Name] = parsedColor;
                        }
                        else
                        {
                            // Fallback to default color if parsing fails
                            ParsingColor fallbackColor = ParsingColor.FromBgrInteger(0);
                            twineMetadata.TagColors[prop.Name] = new Color(fallbackColor);
                        }
                    }
                }
            }

            // Create passages
            var passages = new List<TwinePassage>();
            if (root.TryGetProperty("passages", out var passagesProp) && passagesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var pData in passagesProp.EnumerateArray())
                {
                    // Determine passage type
                    var tags = new List<string>();
                    if (pData.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tag in tagsProp.EnumerateArray())
                        {
                            if (tag.ValueKind == JsonValueKind.String)
                            {
                                tags.Add(tag.GetString());
                            }
                        }
                    }
                    PassageType pType = tags.Contains("entry") ? PassageType.Entry : PassageType.Reply;

                    // Parse metadata
                    var passageMetadata = new PassageMetadata();
                    if (pData.TryGetProperty("metadata", out var metaProp) && metaProp.ValueKind == JsonValueKind.Object)
                    {
                        if (metaProp.TryGetProperty("position", out var posProp))
                        {
                            string posStr = posProp.GetString() ?? "0,0";
                            var posParts = posStr.Split(',');
                            if (posParts.Length >= 2 && float.TryParse(posParts[0], out float x) && float.TryParse(posParts[1], out float y))
                            {
                                passageMetadata.Position = new TwineVector2(x, y);
                            }
                        }
                        if (metaProp.TryGetProperty("size", out var sizeProp))
                        {
                            string sizeStr = sizeProp.GetString() ?? "100,100";
                            var sizeParts = sizeStr.Split(',');
                            if (sizeParts.Length >= 2 && float.TryParse(sizeParts[0], out float x) && float.TryParse(sizeParts[1], out float y))
                            {
                                passageMetadata.Size = new TwineVector2(x, y);
                            }
                        }

                        // Restore KotOR-specific metadata from custom dict
                        if (metaProp.TryGetProperty("custom", out var customProp) && customProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var customKvp in customProp.EnumerateObject())
                            {
                                string key = customKvp.Name;
                                string value = customKvp.Value.GetString() ?? "";

                                if (key == "animation_id" && int.TryParse(value, out int animId))
                                {
                                    passageMetadata.AnimationId = animId;
                                }
                                else if (key == "camera_angle" && int.TryParse(value, out int camAngle))
                                {
                                    passageMetadata.CameraAngle = camAngle;
                                }
                                else if (key == "camera_id")
                                {
                                    if (string.IsNullOrEmpty(value))
                                    {
                                        passageMetadata.CameraId = null;
                                    }
                                    else if (int.TryParse(value, out int camId))
                                    {
                                        passageMetadata.CameraId = camId;
                                    }
                                }
                                else if (key == "fade_type" && int.TryParse(value, out int fadeType))
                                {
                                    passageMetadata.FadeType = fadeType;
                                }
                                else if (key == "quest")
                                {
                                    passageMetadata.Quest = value;
                                }
                                else if (key == "sound")
                                {
                                    passageMetadata.Sound = value;
                                }
                                else if (key == "vo_resref")
                                {
                                    passageMetadata.VoResref = value;
                                }
                                else if (key == "speaker")
                                {
                                    passageMetadata.Speaker = value;
                                }
                                else
                                {
                                    // Store remaining custom metadata
                                    passageMetadata.Custom[key] = value;
                                }
                            }
                        }
                    }

                    // Create passage
                    string passageName = "";
                    if (pData.TryGetProperty("name", out var nameProp2))
                    {
                        passageName = nameProp2.GetString() ?? "";
                    }
                    string passageText = "";
                    if (pData.TryGetProperty("text", out var textProp2))
                    {
                        passageText = textProp2.GetString() ?? "";
                    }
                    string passagePid = Guid.NewGuid().ToString();
                    if (pData.TryGetProperty("pid", out var pidProp2))
                    {
                        passagePid = pidProp2.GetString() ?? Guid.NewGuid().ToString();
                    }
                    var passage = new TwinePassage
                    {
                        Name = passageName,
                        Text = passageText,
                        Type = pType,
                        Pid = passagePid,
                        Tags = tags,
                        Metadata = passageMetadata,
                    };

                    // Parse links from text
                    string linkPattern = @"\[\[(.*?)(?:->(.+?))?\]\]";
                    foreach (Match match in Regex.Matches(passage.Text, linkPattern))
                    {
                        string display = match.Groups[1].Value;
                        string target = match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value) ? match.Groups[2].Value : display;
                        passage.Links.Add(new TwineLink { Text = display, Target = target });
                    }

                    passages.Add(passage);
                }
            }

            string startPid = root.TryGetProperty("startnode", out var startNodeProp) ? startNodeProp.GetString() ?? "" : "";
            var story = new TwineStory
            {
                Metadata = twineMetadata,
                Passages = passages,
                StartPid = startPid,
            };

            // Fallback: if start_pid missing, prefer first entry passage
            if (string.IsNullOrEmpty(story.StartPid))
            {
                var entries = story.GetEntries();
                if (entries.Count > 0)
                {
                    story.StartPid = entries[0].Pid;
                }
            }

            return story;
        }

        /// <summary>
        /// Reads a Twine story from HTML format.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:217-350
        /// </summary>
        private static TwineStory ReadHtml(string content)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Parse(content);
            }
            catch (XmlException ex)
            {
                throw new ArgumentException($"Invalid HTML: {ex.Message}", ex);
            }

            var storyData = doc.Descendants("tw-storydata").FirstOrDefault();
            if (storyData == null)
            {
                throw new ArgumentException("No story data found in HTML");
            }

            // Create metadata
            var twineMetadata = new TwineMetadata
            {
                Name = storyData.Attribute("name")?.Value ?? "Converted Dialog",
                Ifid = storyData.Attribute("ifid")?.Value ?? Guid.NewGuid().ToString(),
                Format = storyData.Attribute("format")?.Value ?? "Harlowe",
                FormatVersion = storyData.Attribute("format-version")?.Value ?? "3.3.7",
                Zoom = float.TryParse(storyData.Attribute("zoom")?.Value, out float zoom) ? zoom : 1.0f,
                Creator = storyData.Attribute("creator")?.Value ?? "Andastra",
                CreatorVersion = storyData.Attribute("creator-version")?.Value ?? "1.0.0",
            };

            // Get style/script
            var style = storyData.Descendants("style").FirstOrDefault(e => e.Attribute("type")?.Value == "text/twine-css");
            if (style != null && !string.IsNullOrEmpty(style.Value))
            {
                twineMetadata.Style = style.Value;
            }

            var script = storyData.Descendants("script").FirstOrDefault(e => e.Attribute("type")?.Value == "text/twine-javascript");
            if (script != null && !string.IsNullOrEmpty(script.Value))
            {
                twineMetadata.Script = script.Value;
            }

            // Get tag colors
            // Based on PyKotor: HTML format uses hex colors, parsed via Color.from_hex_string(ParsingColor)
            // Example: <tw-tag name="entry" color="#ff0000" />
            foreach (var tag in storyData.Descendants("tw-tag"))
            {
                string name = tag.Attribute("name")?.Value?.Trim() ?? "";
                string colorStr = tag.Attribute("color")?.Value?.Trim() ?? "";
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(colorStr))
                {
                    // Parse color string (supports hex and space-separated float formats)
                    // HTML format typically uses hex, but we support both for compatibility
                    Color parsedColor = ParseTwineColorString(colorStr);
                    if (parsedColor != null)
                    {
                        twineMetadata.TagColors[name] = parsedColor;
                    }
                    else
                    {
                        // Fallback to default color if parsing fails
                        ParsingColor fallbackColor = ParsingColor.FromBgrInteger(0);
                        twineMetadata.TagColors[name] = new Color(fallbackColor);
                    }
                }
            }

            // Create passages
            var passages = new List<TwinePassage>();
            foreach (var pData in storyData.Descendants("tw-passagedata"))
            {
                // Determine passage type
                string tagsStr = pData.Attribute("tags")?.Value ?? "";
                var tags = tagsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                PassageType pType = tags.Contains("entry") ? PassageType.Entry : PassageType.Reply;

                // Parse position/size
                string positionStr = pData.Attribute("position")?.Value ?? "0,0";
                string sizeStr = pData.Attribute("size")?.Value ?? "100,100";
                var positionParts = positionStr.Split(',');
                var sizeParts = sizeStr.Split(',');

                var passageMetadata = new PassageMetadata
                {
                    Position = positionParts.Length >= 2 && float.TryParse(positionParts[0], out float px) && float.TryParse(positionParts[1], out float py)
                        ? new TwineVector2(px, py) : new TwineVector2(0, 0),
                    Size = sizeParts.Length >= 2 && float.TryParse(sizeParts[0], out float sx) && float.TryParse(sizeParts[1], out float sy)
                        ? new TwineVector2(sx, sy) : new TwineVector2(100, 100),
                };

                // Restore custom metadata from data-custom attribute
                string customData = pData.Attribute("data-custom")?.Value;
                if (!string.IsNullOrEmpty(customData))
                {
                    try
                    {
                        var customDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(customData);
                        if (customDict != null)
                        {
                            foreach (var kvp in customDict)
                            {
                                string key = kvp.Key;
                                string value = kvp.Value.GetString() ?? "";

                                if (key == "animation_id" && int.TryParse(value, out int animId))
                                {
                                    passageMetadata.AnimationId = animId;
                                }
                                else if (key == "camera_angle" && int.TryParse(value, out int camAngle))
                                {
                                    passageMetadata.CameraAngle = camAngle;
                                }
                                else if (key == "camera_id")
                                {
                                    if (string.IsNullOrEmpty(value))
                                    {
                                        passageMetadata.CameraId = null;
                                    }
                                    else if (int.TryParse(value, out int camId))
                                    {
                                        passageMetadata.CameraId = camId;
                                    }
                                }
                                else if (key == "fade_type" && int.TryParse(value, out int fadeType))
                                {
                                    passageMetadata.FadeType = fadeType;
                                }
                                else if (key == "quest")
                                {
                                    passageMetadata.Quest = value;
                                }
                                else if (key == "sound")
                                {
                                    passageMetadata.Sound = value;
                                }
                                else if (key == "vo_resref")
                                {
                                    passageMetadata.VoResref = value;
                                }
                                else if (key == "speaker")
                                {
                                    passageMetadata.Speaker = value;
                                }
                                else
                                {
                                    // Store remaining custom metadata
                                    passageMetadata.Custom[key] = value;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid JSON
                    }
                }

                // Create passage
                var passage = new TwinePassage
                {
                    Name = pData.Attribute("name")?.Value ?? "",
                    Text = pData.Value ?? "",
                    Type = pType,
                    Pid = pData.Attribute("pid")?.Value ?? Guid.NewGuid().ToString(),
                    Tags = tags,
                    Metadata = passageMetadata,
                };

                // Parse links
                string linkPattern = @"\[\[(.*?)(?:->(.+?))?\]\]";
                foreach (Match match in Regex.Matches(passage.Text, linkPattern))
                {
                    string display = match.Groups[1].Value;
                    string target = match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value) ? match.Groups[2].Value : display;
                    passage.Links.Add(new TwineLink { Text = display, Target = target });
                }

                passages.Add(passage);
            }

            string startPid = storyData.Attribute("startnode")?.Value ?? "";
            var story = new TwineStory
            {
                Metadata = twineMetadata,
                Passages = passages,
                StartPid = startPid,
            };

            if (string.IsNullOrEmpty(story.StartPid))
            {
                var entries = story.GetEntries();
                if (entries.Count > 0)
                {
                    story.StartPid = entries[0].Pid;
                }
            }

            return story;
        }

        /// <summary>
        /// Writes a Twine story to JSON format.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:353-435
        /// </summary>
        private static void WriteJson(TwineStory story, string path)
        {
            var data = new Dictionary<string, object>
            {
                { "name", story.Metadata.Name },
                { "ifid", story.Metadata.Ifid },
                { "format", story.Metadata.Format },
                { "format-version", story.Metadata.FormatVersion },
                { "zoom", story.Metadata.Zoom },
                { "creator", story.Metadata.Creator },
                { "creator-version", story.Metadata.CreatorVersion },
                { "style", story.Metadata.Style },
                { "script", story.Metadata.Script },
                { "tag-colors", story.Metadata.TagColors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()) },
                { "startnode", story.StartPid },
                { "passages", new List<object>() },
            };

            var passagesList = (List<object>)data["passages"];
            foreach (var passage in story.Passages)
            {
                // Embed links into text in Twine format: [[text->target]] or [[target]]
                string textWithLinks = passage.Text;
                if (passage.Links.Count > 0)
                {
                    var linkTexts = new List<string>();
                    foreach (var link in passage.Links)
                    {
                        if (!string.IsNullOrEmpty(link.Target))
                        {
                            if (!string.IsNullOrEmpty(link.Text) && link.Text != link.Target)
                            {
                                linkTexts.Add($"[[{link.Text}->{link.Target}]]");
                            }
                            else
                            {
                                linkTexts.Add($"[[{link.Target}]]");
                            }
                        }
                    }
                    if (linkTexts.Count > 0)
                    {
                        textWithLinks = passage.Text + (string.IsNullOrEmpty(passage.Text) ? "" : " ") + string.Join(" ", linkTexts);
                    }
                }

                var metadataDict = new Dictionary<string, object>
                {
                    { "position", $"{passage.Metadata.Position.X},{passage.Metadata.Position.Y}" },
                    { "size", $"{passage.Metadata.Size.X},{passage.Metadata.Size.Y}" },
                };

                // Include KotOR-specific metadata fields in custom dict
                var kotorMetadata = new Dictionary<string, string>();
                if (passage.Metadata.AnimationId != 0)
                {
                    kotorMetadata["animation_id"] = passage.Metadata.AnimationId.ToString();
                }
                if (passage.Metadata.CameraAngle != 0)
                {
                    kotorMetadata["camera_angle"] = passage.Metadata.CameraAngle.ToString();
                }
                if (passage.Metadata.CameraId.HasValue && passage.Metadata.CameraId.Value != 0)
                {
                    kotorMetadata["camera_id"] = passage.Metadata.CameraId.Value.ToString();
                }
                if (passage.Metadata.FadeType != 0)
                {
                    kotorMetadata["fade_type"] = passage.Metadata.FadeType.ToString();
                }
                if (!string.IsNullOrEmpty(passage.Metadata.Quest))
                {
                    kotorMetadata["quest"] = passage.Metadata.Quest;
                }
                if (!string.IsNullOrEmpty(passage.Metadata.Sound))
                {
                    kotorMetadata["sound"] = passage.Metadata.Sound;
                }
                if (!string.IsNullOrEmpty(passage.Metadata.VoResref))
                {
                    kotorMetadata["vo_resref"] = passage.Metadata.VoResref;
                }
                if (!string.IsNullOrEmpty(passage.Metadata.Speaker))
                {
                    kotorMetadata["speaker"] = passage.Metadata.Speaker;
                }

                // Merge with existing custom metadata
                foreach (var kvp in passage.Metadata.Custom)
                {
                    kotorMetadata[kvp.Key] = kvp.Value;
                }

                if (kotorMetadata.Count > 0)
                {
                    metadataDict["custom"] = kotorMetadata;
                }

                var pData = new Dictionary<string, object>
                {
                    { "name", passage.Name },
                    { "text", textWithLinks },
                    { "tags", passage.Tags },
                    { "pid", passage.Pid },
                    { "metadata", metadataDict },
                };
                passagesList.Add(pData);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(data, options), Encoding.UTF8);
        }

        /// <summary>
        /// Writes a Twine story to HTML format.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:438-532
        /// </summary>
        private static void WriteHtml(TwineStory story, string path)
        {
            var root = new XElement("html");
            var storyData = new XElement("tw-storydata");
            root.Add(storyData);

            // Set story metadata
            storyData.SetAttributeValue("name", story.Metadata.Name);
            storyData.SetAttributeValue("ifid", story.Metadata.Ifid);
            storyData.SetAttributeValue("format", story.Metadata.Format);
            storyData.SetAttributeValue("format-version", story.Metadata.FormatVersion);
            storyData.SetAttributeValue("zoom", story.Metadata.Zoom.ToString());
            storyData.SetAttributeValue("creator", story.Metadata.Creator);
            storyData.SetAttributeValue("creator-version", story.Metadata.CreatorVersion);

            // Add style/script
            if (!string.IsNullOrEmpty(story.Metadata.Style))
            {
                var style = new XElement("style");
                style.SetAttributeValue("role", "stylesheet");
                style.SetAttributeValue("id", "twine-user-stylesheet");
                style.SetAttributeValue("type", "text/twine-css");
                style.Value = story.Metadata.Style;
                storyData.Add(style);
            }

            if (!string.IsNullOrEmpty(story.Metadata.Script))
            {
                var script = new XElement("script");
                script.SetAttributeValue("role", "script");
                script.SetAttributeValue("id", "twine-user-script");
                script.SetAttributeValue("type", "text/twine-javascript");
                script.Value = story.Metadata.Script;
                storyData.Add(script);
            }

            // Add tag colors
            foreach (var kvp in story.Metadata.TagColors)
            {
                var tag = new XElement("tw-tag");
                tag.SetAttributeValue("name", kvp.Key);
                tag.SetAttributeValue("color", kvp.Value.ToString());
                storyData.Add(tag);
            }

            // Add passages
            foreach (var passage in story.Passages)
            {
                var pData = new XElement("tw-passagedata");
                pData.SetAttributeValue("name", passage.Name);
                pData.SetAttributeValue("tags", string.Join(" ", passage.Tags));
                pData.SetAttributeValue("pid", passage.Pid);
                pData.SetAttributeValue("position", $"{passage.Metadata.Position.X},{passage.Metadata.Position.Y}");
                pData.SetAttributeValue("size", $"{passage.Metadata.Size.X},{passage.Metadata.Size.Y}");

                // Store custom metadata as JSON in data attribute
                var customPayload = new Dictionary<string, object>();
                foreach (var kvp in passage.Metadata.Custom)
                {
                    customPayload[kvp.Key] = kvp.Value;
                }
                if (passage.Metadata.AnimationId != 0)
                {
                    customPayload["animation_id"] = passage.Metadata.AnimationId.ToString();
                }
                if (passage.Metadata.CameraAngle != 0)
                {
                    customPayload["camera_angle"] = passage.Metadata.CameraAngle.ToString();
                }
                if (passage.Metadata.CameraId.HasValue && passage.Metadata.CameraId.Value != 0)
                {
                    customPayload["camera_id"] = passage.Metadata.CameraId.Value.ToString();
                }
                if (passage.Metadata.FadeType != 0)
                {
                    customPayload["fade_type"] = passage.Metadata.FadeType.ToString();
                }
                if (!string.IsNullOrEmpty(passage.Metadata.Quest))
                {
                    customPayload["quest"] = passage.Metadata.Quest;
                }
                if (!string.IsNullOrEmpty(passage.Metadata.Sound))
                {
                    customPayload["sound"] = passage.Metadata.Sound;
                }
                if (!string.IsNullOrEmpty(passage.Metadata.VoResref))
                {
                    customPayload["vo_resref"] = passage.Metadata.VoResref;
                }
                if (!string.IsNullOrEmpty(passage.Metadata.Speaker))
                {
                    customPayload["speaker"] = passage.Metadata.Speaker;
                }

                if (customPayload.Count > 0)
                {
                    pData.SetAttributeValue("data-custom", JsonSerializer.Serialize(customPayload));
                }

                // Embed links into text
                string textWithLinks = passage.Text;
                if (passage.Links.Count > 0)
                {
                    var linkTexts = new List<string>();
                    foreach (var link in passage.Links)
                    {
                        if (!string.IsNullOrEmpty(link.Target))
                        {
                            if (!string.IsNullOrEmpty(link.Text) && link.Text != link.Target)
                            {
                                linkTexts.Add($"[[{link.Text}->{link.Target}]]");
                            }
                            else
                            {
                                linkTexts.Add($"[[{link.Target}]]");
                            }
                        }
                    }
                    if (linkTexts.Count > 0)
                    {
                        textWithLinks = passage.Text + (string.IsNullOrEmpty(passage.Text) ? "" : " ") + string.Join(" ", linkTexts);
                    }
                }
                pData.Value = textWithLinks;

                storyData.Add(pData);
            }

            // Mark starting passage if known
            if (!string.IsNullOrEmpty(story.StartPid))
            {
                storyData.SetAttributeValue("startnode", story.StartPid);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            doc.Save(path);
        }

        /// <summary>
        /// Converts a Twine story to a DLG.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:535-594
        /// </summary>
        private static DLG StoryToDlg(TwineStory story)
        {
            var dlg = new DLG();
            var converter = new FormatConverter();

            // Track created nodes
            var nodes = new Dictionary<string, DLGNode>();

            // First pass: Create nodes
            foreach (var passage in story.Passages)
            {
                DLGNode node;
                if (passage.Type == PassageType.Entry)
                {
                    var entry = new DLGEntry();
                    entry.Speaker = passage.Name;
                    node = entry;
                }
                else
                {
                    node = new DLGReply();
                }

                // Set text - restore all language/gender combinations from custom metadata
                node.Text = new Andastra.Core.Common.LocalizedString(-1);
                node.Text.SetData(Andastra.Core.Common.Language.English, Andastra.Core.Common.Gender.Male, passage.Text);

                // Restore additional language variants from custom metadata
                foreach (var kvp in passage.Metadata.Custom)
                {
                    string key = kvp.Key;
                    string value = kvp.Value;

                    if (key.StartsWith("text_") && key.Split('_').Length == 3)
                    {
                        var parts = key.Split('_');
                        if (parts[0] == "text")
                        {
                            try
                            {
                                string langName = parts[1].ToUpperInvariant();
                                if (int.TryParse(parts[2], out int genderVal))
                                {
                                    // Try to find matching Language enum
                                    if (Enum.TryParse<Andastra.Core.Common.Language>(langName, true, out Andastra.Core.Common.Language lang))
                                    {
                                        node.Text.SetData(lang, (Andastra.Core.Common.Gender)genderVal, value);
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid language/gender combinations
                            }
                        }
                    }
                }

                // Restore metadata from passage to node
                converter.RestoreKotorMetadata(node, passage);

                nodes[passage.Name] = node;
            }

            // Second pass: Create links
            foreach (var passage in story.Passages)
            {
                if (!nodes.ContainsKey(passage.Name))
                {
                    continue;
                }

                var source = nodes[passage.Name];
                foreach (var link in passage.Links)
                {
                    if (!nodes.ContainsKey(link.Target))
                    {
                        continue;
                    }

                    var target = nodes[link.Target];
                    var dlgLink = new DLGLink(target);
                    dlgLink.IsChild = link.IsChild;
                    if (!string.IsNullOrEmpty(link.ActiveScript))
                    {
                        dlgLink.Active1 = new ResRef(link.ActiveScript);
                    }
                    source.Links.Add(dlgLink);
                }
            }

            // Set starting node
            var startPassage = story.GetStartPassage();
            if (startPassage != null && nodes.ContainsKey(startPassage.Name))
            {
                dlg.Starters.Add(new DLGLink(nodes[startPassage.Name]));
            }

            // Store Twine metadata
            converter.StoreTwineMetadata(story, dlg);

            return dlg;
        }

        /// <summary>
        /// Converts a DLG to a Twine story.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/io/twine.py:597-718
        /// </summary>
        private static TwineStory DlgToStory(DLG dlg, Dictionary<string, object> metadata = null)
        {
            // Create metadata
            var meta = metadata ?? new Dictionary<string, object>();
            float zoomVal = 1.0f;
            if (meta.ContainsKey("zoom"))
            {
                if (meta["zoom"] is float f)
                {
                    zoomVal = f;
                }
                else if (float.TryParse(meta["zoom"].ToString(), out float parsedZoom))
                {
                    zoomVal = parsedZoom;
                }
            }

            var tagColorsVal = new Dictionary<string, Color>();
            if (meta.ContainsKey("tag-colors") && meta["tag-colors"] is Dictionary<string, Color> tagColors)
            {
                tagColorsVal = tagColors;
            }

            var storyMeta = new TwineMetadata
            {
                Name = meta.ContainsKey("name") ? meta["name"].ToString() : "Converted Dialog",
                Ifid = meta.ContainsKey("ifid") ? meta["ifid"].ToString() : Guid.NewGuid().ToString(),
                Format = meta.ContainsKey("format") ? meta["format"].ToString() : "Harlowe",
                FormatVersion = meta.ContainsKey("format-version") ? meta["format-version"].ToString() : "3.3.7",
                Zoom = zoomVal,
                Creator = meta.ContainsKey("creator") ? meta["creator"].ToString() : "Andastra",
                CreatorVersion = meta.ContainsKey("creator-version") ? meta["creator-version"].ToString() : "1.0.0",
                Style = meta.ContainsKey("style") ? meta["style"].ToString() : "",
                Script = meta.ContainsKey("script") ? meta["script"].ToString() : "",
                TagColors = tagColorsVal,
            };

            var story = new TwineStory { Metadata = storyMeta, Passages = new List<TwinePassage>() };
            var converter = new FormatConverter();

            // Track processed nodes to handle cycles without recursion depth issues
            var processed = new HashSet<DLGNode>();
            var nodeToPassage = new Dictionary<DLGNode, TwinePassage>();
            var nameRegistry = new Dictionary<string, int>();
            var nodeNames = new Dictionary<DLGNode, string>();

            string AssignName(DLGNode node)
            {
                if (nodeNames.ContainsKey(node))
                {
                    return nodeNames[node];
                }

                string baseName = (node is DLGEntry entry && !string.IsNullOrEmpty(entry.Speaker))
                    ? entry.Speaker
                    : (node is DLGEntry ? "Entry" : "Reply");

                if (!nameRegistry.ContainsKey(baseName))
                {
                    nameRegistry[baseName] = 0;
                }
                int count = nameRegistry[baseName];
                nameRegistry[baseName] = count + 1;
                string name = count == 0 ? baseName : $"{baseName}_{count}";
                nodeNames[node] = name;
                return name;
            }

            TwinePassage EnsurePassage(DLGNode node, string pid)
            {
                if (nodeToPassage.ContainsKey(node))
                {
                    return nodeToPassage[node];
                }

                // Get primary text (English, Male) for main passage text
                string primaryText = node.Text?.GetString(Andastra.Core.Common.Language.English, Andastra.Core.Common.Gender.Male) ?? "";

                var passage = new TwinePassage
                {
                    Name = AssignName(node),
                    Text = primaryText,
                    Type = node is DLGEntry ? PassageType.Entry : PassageType.Reply,
                    Pid = pid,
                    Tags = new List<string> { node is DLGEntry ? "entry" : "reply" },
                };

                // Store all language/gender combinations in custom metadata
                if (node.Text != null && node.Text.StringRef == -1)
                {
                    foreach ((Language language, Gender gender, string text) tuple in node.Text)
                    {
                        if (!string.IsNullOrEmpty(tuple.text))
                        {
                            string key = $"text_{tuple.language.ToString().ToLowerInvariant()}_{(int)tuple.gender}";
                            passage.Metadata.Custom[key] = tuple.text;
                        }
                    }
                }

                converter.StoreKotorMetadata(passage, node);
                nodeToPassage[node] = passage;
                story.Passages.Add(passage);
                return passage;
            }

            // Process all nodes starting from starters using an explicit stack to avoid recursion limits
            for (int i = 0; i < dlg.Starters.Count; i++)
            {
                var link = dlg.Starters[i];
                if (link?.Node == null)
                {
                    continue;
                }

                var stack = new Stack<Tuple<DLGNode, string>>();
                stack.Push(Tuple.Create(link.Node, (i + 1).ToString()));
                TwinePassage startPassage = null;

                while (stack.Count > 0)
                {
                    var (currentNode, pid) = stack.Pop();
                    var passage = EnsurePassage(currentNode, pid);

                    if (!processed.Contains(currentNode))
                    {
                        processed.Add(currentNode);

                        foreach (var childLink in currentNode.Links)
                        {
                            if (childLink?.Node == null)
                            {
                                continue;
                            }

                            var targetNode = childLink.Node;
                            string targetPid = Guid.NewGuid().ToString();
                            var targetPassage = EnsurePassage(targetNode, targetPid);

                            passage.Links.Add(new TwineLink
                            {
                                Text = "Continue",
                                Target = targetPassage.Name,
                                IsChild = childLink.IsChild,
                                ActiveScript = childLink.Active1?.ToString() ?? "",
                            });

                            if (!processed.Contains(targetNode))
                            {
                                stack.Push(Tuple.Create(targetNode, targetPid));
                            }
                        }
                    }

                    if (startPassage == null)
                    {
                        startPassage = passage;
                    }
                }

                if (i == 0 && startPassage != null)
                {
                    story.StartPid = startPassage.Pid;
                }
            }

            // Restore Twine metadata
            converter.RestoreTwineMetadata(dlg, story);

            return story;
        }
    }
}

