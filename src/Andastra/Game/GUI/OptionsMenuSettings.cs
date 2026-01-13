using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Runtime.Core;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Game options settings that can be configured in the options menu.
    /// </summary>
    /// <remarks>
    /// Options Menu Settings:
    /// - Based on swkotor.exe and swkotor2.exe options menu system
    /// - Settings stored in INI format (swkotor.ini for K1, swkotor2.ini for K2)
    /// - Located via string references: "swkotor2.ini" @ 0x007b5740, ".\swkotor2.ini" @ 0x007b5644
    /// - "DiffSettings" @ 0x007c2cdc (display settings, referenced by 0x005d7ce0 @ 0x005d7ce0)
    /// - INI reading: 0x00631fe0 @ 0x00631fe0 (reads INI values via 0x00635fb0)
    /// - INI writing: 0x00631ff0 @ 0x00631ff0 (writes INI values)
    /// - Original implementation: Options menu saves settings to INI file, game loads settings on startup
    /// - Settings categories: Graphics, Sound, Gameplay, Controls
    /// </remarks>
    public class OptionsMenuSettings
    {
        // Graphics Settings
        public int ResolutionWidth { get; set; } = 1280;
        public int ResolutionHeight { get; set; } = 720;
        public bool Fullscreen { get; set; } = false;
        public bool VSync { get; set; } = true;
        public int TextureQuality { get; set; } = 2; // 0=Low, 1=Medium, 2=High
        public int ShadowQuality { get; set; } = 1; // 0=Off, 1=Low, 2=Medium, 3=High
        public int AnisotropicFiltering { get; set; } = 4; // 0=Off, 2=2x, 4=4x, 8=8x, 16=16x
        public bool AntiAliasing { get; set; } = true;

        // Sound Settings
        public float MasterVolume { get; set; } = 1.0f; // 0.0 to 1.0
        public float MusicVolume { get; set; } = 1.0f; // 0.0 to 1.0
        public float EffectsVolume { get; set; } = 1.0f; // 0.0 to 1.0
        public float VoiceVolume { get; set; } = 1.0f; // 0.0 to 1.0
        public bool SoundEnabled { get; set; } = true;
        public bool MusicEnabled { get; set; } = true;

        // Gameplay Settings
        public float MouseSensitivity { get; set; } = 0.5f; // 0.0 to 1.0
        public bool InvertMouseY { get; set; } = false;
        public bool AutoSave { get; set; } = true;
        public int AutoSaveInterval { get; set; } = 300; // seconds
        public bool Tooltips { get; set; } = true;
        public bool Subtitles { get; set; } = true;
        public float DialogueSpeed { get; set; } = 1.0f; // 0.5 to 2.0
        public bool ClassicControls { get; set; } = false;

        // Controls (key bindings - stored as key names)
        public Dictionary<string, string> KeyBindings { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Loads settings from an INI file.
        /// </summary>
        public static OptionsMenuSettings LoadFromIni(string filePath)
        {
            var settings = new OptionsMenuSettings();

            if (!File.Exists(filePath))
            {
                return settings; // Return defaults if file doesn't exist
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                string currentSection = null;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    {
                        continue; // Skip empty lines and comments
                    }

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).ToUpperInvariant();
                        continue;
                    }

                    int equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex < 0)
                    {
                        continue;
                    }

                    string key = trimmed.Substring(0, equalsIndex).Trim();
                    string value = trimmed.Substring(equalsIndex + 1).Trim();

                    // Parse based on section
                    if (currentSection == "GRAPHICS" || currentSection == "DISPLAY")
                    {
                        switch (key.ToUpperInvariant())
                        {
                            case "WIDTH":
                                if (int.TryParse(value, out int width))
                                {
                                    settings.ResolutionWidth = width;
                                }
                                break;
                            case "HEIGHT":
                                if (int.TryParse(value, out int height))
                                {
                                    settings.ResolutionHeight = height;
                                }
                                break;
                            case "FULLSCREEN":
                                settings.Fullscreen = ParseBool(value);
                                break;
                            case "VSYNC":
                                settings.VSync = ParseBool(value);
                                break;
                            case "TEXTUREQUALITY":
                            case "TEXTURE_QUALITY":
                                if (int.TryParse(value, out int texQual))
                                {
                                    settings.TextureQuality = texQual;
                                }
                                break;
                            case "SHADOWQUALITY":
                            case "SHADOW_QUALITY":
                                if (int.TryParse(value, out int shadowQual))
                                {
                                    settings.ShadowQuality = shadowQual;
                                }
                                break;
                            case "ANISOTROPICFILTERING":
                            case "ANISOTROPIC_FILTERING":
                                if (int.TryParse(value, out int af))
                                {
                                    settings.AnisotropicFiltering = af;
                                }
                                break;
                            case "ANTIALIASING":
                            case "ANTI_ALIASING":
                                settings.AntiAliasing = ParseBool(value);
                                break;
                        }
                    }
                    else if (currentSection == "SOUND" || currentSection == "AUDIO")
                    {
                        switch (key.ToUpperInvariant())
                        {
                            case "MASTERVOLUME":
                            case "MASTER_VOLUME":
                                if (float.TryParse(value, out float masterVol))
                                {
                                    settings.MasterVolume = Math.Max(0.0f, Math.Min(1.0f, masterVol));
                                }
                                break;
                            case "MUSICVOLUME":
                            case "MUSIC_VOLUME":
                                if (float.TryParse(value, out float musicVol))
                                {
                                    settings.MusicVolume = Math.Max(0.0f, Math.Min(1.0f, musicVol));
                                }
                                break;
                            case "EFFECTSVOLUME":
                            case "EFFECTS_VOLUME":
                                if (float.TryParse(value, out float effectsVol))
                                {
                                    settings.EffectsVolume = Math.Max(0.0f, Math.Min(1.0f, effectsVol));
                                }
                                break;
                            case "VOICEVOLUME":
                            case "VOICE_VOLUME":
                                if (float.TryParse(value, out float voiceVol))
                                {
                                    settings.VoiceVolume = Math.Max(0.0f, Math.Min(1.0f, voiceVol));
                                }
                                break;

                            case "SOUNDENABLED":
                            case "SOUND_ENABLED":
                                settings.SoundEnabled = ParseBool(value);
                                break;
                            case "MUSICENABLED":
                            case "MUSIC_ENABLED":
                                settings.MusicEnabled = ParseBool(value);
                                break;
                        }
                    }
                    else if (currentSection == "GAMEPLAY" || currentSection == "GAME")
                    {
                        switch (key.ToUpperInvariant())
                        {
                            case "MOUSESENSITIVITY":
                            case "MOUSE_SENSITIVITY":
                                if (float.TryParse(value, out float mouseSens))
                                {
                                    settings.MouseSensitivity = Math.Max(0.0f, Math.Min(1.0f, mouseSens));
                                }
                                break;
                            case "INVERTMOUSEY":
                            case "INVERT_MOUSE_Y":
                                settings.InvertMouseY = ParseBool(value);
                                break;
                            case "AUTOSAVE":
                            case "AUTO_SAVE":
                                settings.AutoSave = ParseBool(value);
                                break;
                            case "TOOLTIPS":
                                settings.Tooltips = ParseBool(value);
                                break;
                            case "AUTOSAVEINTERVAL":
                            case "AUTO_SAVE_INTERVAL":
                                if (int.TryParse(value, out int interval))
                                {
                                    settings.AutoSaveInterval = interval;
                                }
                                break;
                            case "SUBTITLES":
                                settings.Subtitles = ParseBool(value);
                                break;
                            case "DIALOGUESPEED":
                            case "DIALOGUE_SPEED":
                                if (float.TryParse(value, out float speed))
                                {
                                    settings.DialogueSpeed = Math.Max(0.5f, Math.Min(2.0f, speed));
                                }
                                break;
                            case "CLASSICCONTROLS":
                            case "CLASSIC_CONTROLS":
                                settings.ClassicControls = ParseBool(value);
                                break;
                        }
                    }
                    else if (currentSection == "CONTROLS")
                    {
                        settings.KeyBindings[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OptionsMenuSettings] Error loading INI file {filePath}: {ex.Message}");
            }

            return settings;
        }

        /// <summary>
        /// Saves settings to an INI file.
        /// </summary>
        public void SaveToIni(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("; Game Options - Generated by Andastra");
                    writer.WriteLine("; Based on swkotor.exe/swkotor2.exe options menu system");
                    writer.WriteLine();

                    // Graphics Section
                    writer.WriteLine("[Graphics]");
                    writer.WriteLine($"Width={ResolutionWidth}");
                    writer.WriteLine($"Height={ResolutionHeight}");
                    writer.WriteLine($"Fullscreen={Fullscreen}");
                    writer.WriteLine($"VSync={VSync}");
                    writer.WriteLine($"TextureQuality={TextureQuality}");
                    writer.WriteLine($"ShadowQuality={ShadowQuality}");
                    writer.WriteLine($"AnisotropicFiltering={AnisotropicFiltering}");
                    writer.WriteLine($"AntiAliasing={AntiAliasing}");
                    writer.WriteLine();

                    // Sound Section
                    writer.WriteLine("[Sound]");
                    writer.WriteLine($"MasterVolume={MasterVolume:F2}");
                    writer.WriteLine($"MusicVolume={MusicVolume:F2}");
                    writer.WriteLine($"EffectsVolume={EffectsVolume:F2}");
                    writer.WriteLine($"VoiceVolume={VoiceVolume:F2}");
                    writer.WriteLine($"SoundEnabled={SoundEnabled}");
                    writer.WriteLine($"MusicEnabled={MusicEnabled}");
                    writer.WriteLine();

                    // Gameplay Section
                    writer.WriteLine("[Gameplay]");
                    writer.WriteLine($"MouseSensitivity={MouseSensitivity:F2}");
                    writer.WriteLine($"InvertMouseY={InvertMouseY}");
                    writer.WriteLine($"AutoSave={AutoSave}");
                    writer.WriteLine($"AutoSaveInterval={AutoSaveInterval}");
                    writer.WriteLine($"Tooltips={Tooltips}");
                    writer.WriteLine($"Subtitles={Subtitles}");
                    writer.WriteLine($"DialogueSpeed={DialogueSpeed:F2}");
                    writer.WriteLine($"ClassicControls={ClassicControls}");
                    writer.WriteLine();

                    // Controls Section
                    if (KeyBindings.Count > 0)
                    {
                        writer.WriteLine("[Controls]");
                        foreach (var kvp in KeyBindings)
                        {
                            writer.WriteLine($"{kvp.Key}={kvp.Value}");
                        }
                        writer.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OptionsMenuSettings] Error saving INI file {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a deep copy of the options settings.
        /// </summary>
        public OptionsMenuSettings Clone()
        {
            var clone = (OptionsMenuSettings)MemberwiseClone();
            clone.KeyBindings = new Dictionary<string, string>(KeyBindings);
            return clone;
        }

        /// <summary>
        /// Gets the INI file path for the specified game.
        /// </summary>
        public static string GetIniFilePath(KotorGame game)
        {
            string fileName = game == KotorGame.K1 ? "swkotor.ini" : "swkotor2.ini";
            return Path.Combine(Environment.CurrentDirectory, fileName);
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim().ToUpperInvariant();
            return value == "1" || value == "TRUE" || value == "YES" || value == "ON";
        }
    }
}

