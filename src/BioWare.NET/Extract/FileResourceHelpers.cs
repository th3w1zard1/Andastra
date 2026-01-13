using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource;

namespace BioWare.NET.Extract
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:60-100
    // Original: def _find_real_filesystem_path(filepath: Path) -> tuple[Path | None, list[str]]:
    internal static class FileResourceHelpers
    {
        private static readonly string[] CapsuleExtensions = { ".erf", ".mod", ".rim", ".sav", ".hak" };
        private static readonly string[] AllArchiveExtensions = { ".erf", ".mod", ".rim", ".sav", ".hak", ".bif" };

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:60-100
        // Original: def _find_real_filesystem_path(filepath: Path) -> tuple[Path | None, list[str]]:
        public static (string realPath, List<string> nestedParts) FindRealFilesystemPath(string filepath)
        {
            // Fast path: if the filepath exists directly, return it with no nested parts
            if (File.Exists(filepath))
            {
                return (filepath, new List<string>());
            }

            // Slow path: walk up the path to find where the filesystem ends and virtual path begins
            string[] parts = filepath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = parts.Length; i > 0; i--)
            {
                string candidate = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i));
                if (File.Exists(candidate))
                {
                    // Found a real file - remaining parts are inside this file (nested capsule path)
                    List<string> remaining = parts.Skip(i).ToList();
                    return (candidate, remaining);
                }
            }

            return (null, new List<string>());
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:102-183
        // Original: def _extract_from_nested_capsules(real_path: Path, nested_parts: list[str]) -> bytes:
        public static byte[] ExtractFromNestedCapsules(string realPath, List<string> nestedParts)
        {
            // Start with the outer capsule data
            byte[] currentData = File.ReadAllBytes(realPath);

            // Navigate through each nested level
            for (int i = 0; i < nestedParts.Count; i++)
            {
                string part = nestedParts[i];

                // Parse the current data as a capsule to find the next resource
                using (var reader = BioWare.NET.Common.RawBinaryReader.FromBytes(currentData))
                {
                    string fileType = reader.ReadString(4);
                    reader.Skip(4); // file version

                    // Determine capsule type and read resource list
                    List<(string resname, ResourceType restype, int offset, int size)> resources;

                    if (fileType == "ERF " || fileType == "MOD " || fileType == "SAV " || fileType == "HAK ")
                    {
                        resources = ReadErfResources(reader, currentData);
                    }
                    else if (fileType == "RIM ")
                    {
                        resources = ReadRimResources(reader, currentData);
                    }
                    else
                    {
                        throw new InvalidDataException($"Nested path component at '{part}' is inside an unknown archive type: '{fileType}'");
                    }

                    // Find the requested resource in this capsule
                    ResourceIdentifier resIdent = ResourceIdentifier.FromPath(part);
                    (int offset, int size)? targetResource = null;

                    foreach (var (resname, restype, resOff, resSz) in resources)
                    {
                        if (string.Equals(resname, resIdent.ResName, StringComparison.OrdinalIgnoreCase) && restype == resIdent.ResType)
                        {
                            targetResource = (resOff, resSz);
                            break;
                        }
                    }

                    if (targetResource == null)
                    {
                        throw new FileNotFoundException($"Resource '{part}' not found in nested capsule", realPath);
                    }

                    int targetOffset = targetResource.Value.offset;
                    int targetSize = targetResource.Value.size;

                    // Extract the resource data using offset/size from capsule header
                    // We always extract the full resource at each level
                    byte[] extracted = new byte[targetSize];
                    Array.Copy(currentData, targetOffset, extracted, 0, targetSize);
                    currentData = extracted;
                }
            }

            return currentData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:185-223
        // Original: def _read_erf_resources(reader: BinaryReader, capsule_data: bytes) -> list[tuple[str, ResourceType, int, int]]:
        private static List<(string resname, ResourceType restype, int offset, int size)> ReadErfResources(BioWare.NET.Common.RawBinaryReader reader, byte[] capsuleData)
        {
            var resources = new List<(string, ResourceType, int, int)>();

            reader.Skip(8);
            uint entryCount = reader.ReadUInt32();
            reader.Skip(4);
            uint offsetToKeys = reader.ReadUInt32();
            uint offsetToResources = reader.ReadUInt32();

            var resrefs = new List<string>();
            var restypes = new List<ResourceType>();

            reader.Seek((int)offsetToKeys);
            for (uint i = 0; i < entryCount; i++)
            {
                string resref = reader.ReadString(16);
                resrefs.Add(resref);
                reader.Skip(4); // resid
                ushort restype = reader.ReadUInt16();
                restypes.Add(ResourceType.FromId(restype));
                reader.Skip(2);
            }

            reader.Seek((int)offsetToResources);
            for (int i = 0; i < entryCount; i++)
            {
                uint resOffset = reader.ReadUInt32();
                uint resSize = reader.ReadUInt32();
                resources.Add((resrefs[i], restypes[i], (int)resOffset, (int)resSize));
            }

            return resources;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:226-253
        // Original: def _read_rim_resources(reader: BinaryReader, capsule_data: bytes) -> list[tuple[str, ResourceType, int, int]]:
        private static List<(string resname, ResourceType restype, int offset, int size)> ReadRimResources(BioWare.NET.Common.RawBinaryReader reader, byte[] capsuleData)
        {
            var resources = new List<(string, ResourceType, int, int)>();

            reader.Skip(4);
            uint entryCount = reader.ReadUInt32();
            uint offsetToEntries = reader.ReadUInt32();

            reader.Seek((int)offsetToEntries);
            for (uint i = 0; i < entryCount; i++)
            {
                string resref = reader.ReadString(16);
                ResourceType restype = ResourceType.FromId((int)reader.ReadUInt32());
                reader.Skip(4);
                uint offset = reader.ReadUInt32();
                uint size = reader.ReadUInt32();
                resources.Add((resref, restype, (int)offset, (int)size));
            }

            return resources;
        }
    }
}
