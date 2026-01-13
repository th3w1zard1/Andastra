using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using ResourceType = BioWare.NET.Common.ResourceType;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/validation.py
    // Original: Validation and investigation utilities for KOTOR installations and modules
    public static class Validation
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/validation.py:36-82
        // Original: def check_txi_files(installation: Installation, texture_names: list[str], *, search_locations: list[SearchLocation] | None = None) -> dict[str, list[Path]]:
        public static Dictionary<string, List<string>> CheckTxiFiles(BioWare.NET.Extract.Installation installation, List<string> textureNames, SearchLocation[] searchLocations = null)
        {
            if (searchLocations == null)
            {
                searchLocations = new[]
                {
                    SearchLocation.OVERRIDE,
                    SearchLocation.TEXTURES_GUI,
                    SearchLocation.TEXTURES_TPA,
                    SearchLocation.CHITIN
                };
            }

            var results = new Dictionary<string, List<string>>();

            foreach (string texName in textureNames)
            {
                var resId = new ResourceIdentifier(texName, ResourceType.TXI);
                var locations = installation.Locations(new List<ResourceIdentifier> { resId }, searchLocations);
                var foundPaths = new List<string>();

                foreach (var kvp in locations)
                {
                    foreach (var loc in kvp.Value)
                    {
                        string filePath = loc.FilePath;
                        if (!string.IsNullOrEmpty(filePath) && !foundPaths.Contains(filePath))
                        {
                            foundPaths.Add(filePath);
                        }
                    }
                }

                results[texName] = foundPaths;
            }

            return results;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/validation.py:85-124
        // Original: def check_2da_file(installation: Installation, twoda_name: str, *, search_locations: list[SearchLocation] | None = None) -> tuple[bool, list[Path]]:
        public static (bool found, List<string> paths) Check2daFile(BioWare.NET.Extract.Installation installation, string twodaName, SearchLocation[] searchLocations = null)
        {
            if (searchLocations == null)
            {
                searchLocations = new[] { SearchLocation.CHITIN, SearchLocation.OVERRIDE };
            }

            var resId = new ResourceIdentifier(twodaName, ResourceType.TwoDA);
            var locations = installation.Locations(new List<ResourceIdentifier> { resId }, searchLocations);

            var foundPaths = new List<string>();
            foreach (var kvp in locations)
            {
                foreach (var loc in kvp.Value)
                {
                    string filePath = loc.FilePath;
                    if (!string.IsNullOrEmpty(filePath) && !foundPaths.Contains(filePath))
                    {
                        foundPaths.Add(filePath);
                    }
                }
            }

            return (foundPaths.Count > 0, foundPaths);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/validation.py:335-383
        // Original: def validate_installation(installation: Installation, *, check_essential_files: bool = True) -> ValidationResult:
        public static ValidationResult ValidateInstallation(BioWare.NET.Extract.Installation installation, bool checkEssentialFiles = true)
        {
            var errors = new List<string>();
            var missingFiles = new List<string>();

            // Check BioWare.NET.Extract.Installation path exists
            string installPath = installation.Path;
            if (!Directory.Exists(installPath))
            {
                errors.Add($"BioWare.NET.Extract.Installation path does not exist: {installPath}");
            }

            // Check essential files if requested
            if (checkEssentialFiles)
            {
                string[] essential2das = { "appearance", "baseitems", "classes", "genericdoors" };
                foreach (string twodaName in essential2das)
                {
                    (bool found, _) = Check2daFile(installation, twodaName);
                    if (!found)
                    {
                        missingFiles.Add($"{twodaName}.2da");
                    }
                }
            }

            bool valid = errors.Count == 0 && missingFiles.Count == 0;

            return new ValidationResult
            {
                Valid = valid,
                MissingFiles = missingFiles,
                Errors = errors
            };
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/validation.py:28-33
    // Original: class ValidationResult(TypedDict)
    public class ValidationResult
    {
        public bool Valid { get; set; }
        public List<string> MissingFiles { get; set; }
        public List<string> Errors { get; set; }

        public ValidationResult()
        {
            MissingFiles = new List<string>();
            Errors = new List<string>();
        }
    }
}

