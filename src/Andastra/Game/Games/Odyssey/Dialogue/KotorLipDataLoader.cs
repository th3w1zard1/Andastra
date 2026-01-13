using System;
using System.IO;
using BioWare.NET.Resource.Formats.LIP;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Dialogue;

namespace Andastra.Game.Games.Odyssey.Dialogue
{
    /// <summary>
    /// Loads LIP (lip sync) files using BioWare.NET.
    /// </summary>
    /// <remarks>
    /// LIP Data Loader (Odyssey-specific):
    /// - CLIP::LoadLip @ (K1: swkotor.exe: 0x0070c590, TSL: swkotor2.exe: 0x0077fb30): LIP file loading system
    /// - Located via string references:
    ///   - K1: "LIPS:localization" @ 0x00745898, "LIPS:%s_loc" @ 0x007458ac, "LIP V1.0" @ 0x0075fb14
    ///   - TSL: "LIPS:localization" @ 0x007be654, "LIPS:%s_loc" @ 0x007be668, "LIP V1.0" @ 0x007d98d4
    /// - LIP directories:
    ///   - K1: "d:\lips" @ 0x0074ddd8
    ///   - TSL: "d:\lips" @ 0x007c6840
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): No LIP file support found - uses different lip sync system (if any)
    ///   - Eclipse (daorigins.exe, DragonAge2.exe): No LIP file support found - uses UnrealScript-based lip sync system
    /// - Inheritance: Base class BaseLipDataLoader (Runtime.Games.Common) - abstract lip sync loading, Odyssey override (Runtime.Games.Odyssey) - LIP file format
    /// - Original implementation: Loads LIP files from resource system (LIPS directory or module archives)
    /// - LIP file format: "LIP V1.0" signature (8 bytes), duration (float, 4 bytes), keyframe count (uint32, 4 bytes), keyframes (time + shape pairs)
    /// - LIP files are paired with WAV voice-over files (same ResRef, different extension)
    /// - Original engine behavior (1:1 parity):
    ///   - K1 (swkotor.exe: 0x0070c590): CLIP::LoadLip (thiscall, member function)
    ///     * Creates CResRef from CExoString parameter
    ///     * Sets resource reference via CResHelper&lt;CResLIP,3004&gt;::SetResRef
    ///     * Demands resource via CRes::Demand
    ///     * Validates "LIP V1.0" signature (8 bytes)
    ///     * Parses duration (float at offset 0x8), entry_count (uint32 at offset 0xC), keyframes (array at offset 0x10)
    ///     * Sets field5_0x20 to 1 on success
    ///   - TSL (swkotor2.exe: 0x0077fb30): CLIP::LoadLip (fastcall, member function)
    ///     * Creates CResRef from CExoString parameter (via FUN_00406e70)
    ///     * Sets resource reference via FUN_0077f8f0 (equivalent to CResHelper&lt;CResLIP,3004&gt;::SetResRef)
    ///     * Demands resource via FUN_00409df0 (equivalent to CRes::Demand)
    ///     * Gets resource data pointer via FUN_00404cf0 (equivalent to GetProperty0x30)
    ///     * Validates "LIP V1.0" signature (8 bytes)
    ///     * Parses duration (float at offset 0x8 -> stored at offset 0x28), entry_count (uint32 at offset 0xC -> stored at offset 0x24), keyframes (array at offset 0x10 -> stored at offset 0x2c)
    ///     * Sets field at offset 0x20 to 1 on success
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
        /// <remarks>
        /// Original engine implementation:
        /// - K1: swkotor.exe: 0x0070c590 (CLIP::LoadLip)
        /// - TSL: swkotor2.exe: 0x0077fb30 (CLIP::LoadLip)
        /// </remarks>
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

                // Convert BioWare.NET LIP to Runtime.Core LipSyncData
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

