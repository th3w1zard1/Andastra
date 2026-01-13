using System;
using System.IO;
using BioWare.NET.Resource.Formats.LIP;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Dialogue;

namespace Andastra.Game.Engines.Odyssey.Dialogue
{
    /// <summary>
    /// Loads LIP (lip sync) files using BioWare.NET.
    /// </summary>
    /// <remarks>
    /// LIP Data Loader (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): LIP file loading system
    /// - Located via string references: "LIPS:localization" @ 0x007be654, "LIPS:%s_loc" @ 0x007be668, "LIP V1.0" @ 0x007d98d4
    /// - LIP directories: ".\lips" @ 0x007c6838, "d:\lips" @ 0x007c6840 (LIP file search directories)
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): No LIP file support found - uses different lip sync system (if any)
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): No LIP file support found - uses UnrealScript-based lip sync system
    /// - Inheritance: Base class BaseLipDataLoader (Runtime.Games.Common) - abstract lip sync loading, Odyssey override (Runtime.Games.Odyssey) - LIP file format
    /// - Original implementation: Loads LIP files from resource system (LIPS directory or module archives)
    /// - LIP file format: "LIP V1.0" signature, duration (float), keyframe count (uint32), keyframes (time + shape)
    /// - LIP files are paired with WAV voice-over files (same ResRef, different extension)
    /// - Based on LIP file format documentation in vendor/PyKotor/wiki/LIP-File-Format.md
    /// </remarks>
    public class KotorLipDataLoader : ILipDataLoader
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly Installation _installation;

        public KotorLipDataLoader(IGameResourceProvider resourceProvider, Installation installation = null)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _installation = installation;
        }

        /// <summary>
        /// Loads lip sync data from a resource reference.
        /// </summary>
        /// <param name="resRef">The LIP file resource reference.</param>
        /// <returns>The loaded lip sync data, or null if not found.</returns>
        public LipSyncData LoadLipData(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            try
            {
                // Try IGameResourceProvider first
                byte[] lipBytes = null;
                if (_resourceProvider != null)
                {
                    try
                    {
                        var resourceId = new ResourceIdentifier(resRef, ResourceType.LIP);
                        var task = _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);
                        task.Wait();
                        lipBytes = task.Result;
                    }
                    catch (Exception ex)
                    {
                        // Fall back to Installation if resource provider fails
                        if (_installation != null)
                        {
                            ResourceResult result = _installation.Resource(resRef, ResourceType.LIP, null, null);
                            if (result != null && result.Data != null)
                            {
                                lipBytes = result.Data;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                else if (_installation != null)
                {
                    ResourceResult result = _installation.Resource(resRef, ResourceType.LIP, null, null);
                    if (result != null && result.Data != null)
                    {
                        lipBytes = result.Data;
                    }
                }

                if (lipBytes == null || lipBytes.Length == 0)
                {
                    return null;
                }

                // Parse LIP file using BioWare.NET
                LIP lipFile;
                using (var stream = new MemoryStream(lipBytes))
                using (var reader = new LIPBinaryReader(stream))
                {
                    lipFile = reader.Load();
                }

                if (lipFile == null)
                {
                    return null;
                }

                // Convert BioWare.NET LIP to Andastra.Runtime.Core LipSyncData
                var lipSyncData = new LipSyncData();
                lipSyncData.Duration = lipFile.Length;

                foreach (LIPKeyFrame keyFrame in lipFile.Frames)
                {
                    // Convert LIPShape enum to int (0-15)
                    int shapeIndex = (int)keyFrame.Shape;
                    lipSyncData.AddKeyframe(keyFrame.Time, shapeIndex);
                }

                return lipSyncData;
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"[KotorLipDataLoader] Failed to load LIP file '{resRef}': {ex.Message}");
                return null;
            }
        }
    }
}

