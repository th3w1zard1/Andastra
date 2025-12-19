using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Engines.Eclipse.MassEffect
{
    /// <summary>
    /// Base module/package loader for Mass Effect series (ME1 and ME2).
    /// </summary>
    /// <remarks>
    /// Mass Effect Package Loading (Common):
    /// - Mass Effect uses packages instead of modules
    /// - ME1: intABioSPGameexecPreloadPackage @ 0x117fede8, Engine.StartupPackages @ 0x11849d54, Package @ 0x11849d84
    /// - ME2: Similar package system to ME1
    /// - Loads packages from Packages directory
    /// 
    /// Inheritance Structure:
    /// - BaseEngineModule (Runtime.Games.Common): Common module state management, unloading sequence, resource provider integration
    ///   - EclipseModuleLoader : BaseEngineModule (Runtime.Games.Eclipse)
    ///     - Engine-specific: UnrealScript packages, .rim files, message-based loading
    ///     - MassEffectModuleLoaderBase : EclipseModuleLoader (Runtime.Games.Eclipse.MassEffect)
    ///       - Mass Effect-specific: .upk package files, Packages directory, CookedPC directory
    ///       - MassEffectModuleLoader : MassEffectModuleLoaderBase (ME1-specific)
    ///       - MassEffect2ModuleLoader : MassEffectModuleLoaderBase (ME2-specific)
    /// 
    /// Cross-Engine Analysis:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Uses MODULES directory with IFO/LYT/VIS/GIT/ARE files
    /// - Aurora (nwmain.exe, nwn2main.exe): Uses MODULES directory with Module.ifo files
    /// - Eclipse/DragonAge (daorigins.exe, DragonAge2.exe): Uses MODULES directory with .rim files
    /// - Eclipse/MassEffect (MassEffect.exe, MassEffect2.exe): Uses Packages directory with .upk files (UNIQUE)
    /// 
    /// Ghidra Reverse Engineering Required:
    /// - MassEffect.exe: Verify package existence checking function addresses
    ///   - Package @ 0x11849d84 (needs verification - may be data structure, not function)
    ///   - intABioSPGameexecPreloadPackage @ 0x117fede8 (package preloading function)
    ///   - Engine.StartupPackages @ 0x11849d54 (startup package list - data structure)
    ///   - Search for: "Packages", ".upk", "CookedPC" string references
    ///   - Search for: Package loading functions, package existence checks
    /// - MassEffect2.exe: Verify ME2-specific package checking differences
    ///   - Similar package system but may have ME2-specific optimizations
    ///   - Search for: Package loading functions, package existence checks
    ///   - Compare with ME1 implementation to identify common vs. ME2-specific code
    /// </remarks>
    public abstract class MassEffectModuleLoaderBase : EclipseModuleLoader
    {
        protected MassEffectModuleLoaderBase(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
        }

        /// <summary>
        /// Checks if a Mass Effect package exists and can be loaded.
        /// </summary>
        /// <param name="packageName">The name of the package to check (without extension).</param>
        /// <returns>True if the package exists and can be loaded, false otherwise.</returns>
        /// <remarks>
        /// Mass Effect Package Existence Check:
        /// - Mass Effect uses Unreal Engine packages (.upk files) stored in Packages directory
        /// - Package names are case-insensitive
        /// - Checks for .upk file in Packages directory: Packages\{packageName}.upk
        /// - Also checks CookedPC directory for cooked packages (ME1/ME2 specific)
        /// - Returns false for invalid input or missing packages
        /// 
        /// Based on reverse engineering of Mass Effect executables:
        /// - MassEffect.exe: Package existence checking pattern (needs Ghidra verification)
        ///   - Function address: Package @ 0x11849d84 (needs verification)
        ///   - intABioSPGameexecPreloadPackage @ 0x117fede8 (package preloading)
        ///   - Engine.StartupPackages @ 0x11849d54 (startup package list)
        /// - MassEffect2.exe: Similar package checking pattern (needs Ghidra verification)
        ///   - Package system similar to ME1 but may have ME2-specific differences
        /// 
        /// Package File Structure:
        /// - Unreal Package format (.upk)
        /// - Packages contain game resources: textures, models, scripts, etc.
        /// - Package names typically match Unreal class names (e.g., "BioGame", "BioH_")
        /// 
        /// Search Order (based on Unreal Engine package loading):
        /// 1. Packages\{packageName}.upk (primary location)
        /// 2. CookedPC\{packageName}.upk (cooked packages, ME1/ME2 specific)
        /// 3. CookedPC\Splash\{packageName}.upk (splash packages, if applicable)
        /// </remarks>
        public override bool HasModule(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return false;
            }

            try
            {
                // Mass Effect uses packages stored in Packages directory
                string packagesPath = _installation.PackagePath();
                
                // Remove .upk extension if present (package names may or may not include extension)
                string packageNameWithoutExt = packageName;
                if (packageName.EndsWith(".upk", StringComparison.OrdinalIgnoreCase))
                {
                    packageNameWithoutExt = packageName.Substring(0, packageName.Length - 4);
                }

                // Check if Packages directory exists
                if (Directory.Exists(packagesPath))
                {
                    // Check for package file with .upk extension (primary location)
                    string packageFilePath = Path.Combine(packagesPath, packageNameWithoutExt + ".upk");
                    if (File.Exists(packageFilePath))
                    {
                        return true;
                    }

                    // Case-insensitive match in Packages directory
                    // Search for matching .upk file regardless of case
                    string[] upkFiles = Directory.GetFiles(packagesPath, "*.upk", SearchOption.TopDirectoryOnly);
                    foreach (string upkFile in upkFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(upkFile);
                        if (string.Equals(fileName, packageNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                // Check CookedPC directory for cooked packages (ME1/ME2 specific)
                // Cooked packages are pre-processed packages used at runtime
                string installPath = _installation.Path;
                string cookedPcPath = Path.Combine(installPath, "CookedPC");
                
                if (Directory.Exists(cookedPcPath))
                {
                    // Check CookedPC directory
                    string cookedPackagePath = Path.Combine(cookedPcPath, packageNameWithoutExt + ".upk");
                    if (File.Exists(cookedPackagePath))
                    {
                        return true;
                    }

                    // Case-insensitive check in CookedPC
                    string[] cookedUpkFiles = Directory.GetFiles(cookedPcPath, "*.upk", SearchOption.TopDirectoryOnly);
                    foreach (string cookedUpkFile in cookedUpkFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(cookedUpkFile);
                        if (string.Equals(fileName, packageNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    // Check CookedPC\Splash for splash packages (if applicable)
                    string splashPath = Path.Combine(cookedPcPath, "Splash");
                    if (Directory.Exists(splashPath))
                    {
                        string splashPackagePath = Path.Combine(splashPath, packageNameWithoutExt + ".upk");
                        if (File.Exists(splashPackagePath))
                        {
                            return true;
                        }

                        // Case-insensitive check in Splash
                        string[] splashUpkFiles = Directory.GetFiles(splashPath, "*.upk", SearchOption.TopDirectoryOnly);
                        foreach (string splashUpkFile in splashUpkFiles)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(splashUpkFile);
                            if (string.Equals(fileName, packageNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                // Return false on any error (file system errors, permissions, etc.)
                return false;
            }
        }

        protected override async Task LoadModuleInternalAsync(string packageName, [CanBeNull] Action<float> progressCallback)
        {
            progressCallback?.Invoke(0.1f);

            // Mass Effect uses packages instead of modules
            await LoadMassEffectPackageAsync(packageName, progressCallback);

            progressCallback?.Invoke(0.5f);

            // Set package name
            _currentModuleId = packageName;

            progressCallback?.Invoke(0.9f);
        }

        /// <summary>
        /// Load Mass Effect-specific package resources.
        /// Override in subclasses for game-specific differences.
        /// </summary>
        protected virtual async Task LoadMassEffectPackageAsync(string packageName, [CanBeNull] Action<float> progressCallback)
        {
            // TODO: Load package from Packages directory
            // This requires understanding Mass Effect package format structure
            await Task.CompletedTask;
        }
    }
}

