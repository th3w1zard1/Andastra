using System;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Extract
{

    /// <summary>
    /// Represents a resource retrieved from an installation.
    /// Contains the resource data along with metadata about its location.
    /// </summary>
    public class ResourceResult
    {
        public string ResName { get; }
        public ResourceType ResType { get; }
        public string FilePath { get; }
        public byte[] Data { get; }

        [CanBeNull]
        private FileResource _fileResource;

        public ResourceResult(string resName, ResourceType resType, string filePath, byte[] data)
        {
            ResName = resName ?? throw new ArgumentNullException(nameof(resName));
            ResType = resType;
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public void SetFileResource(FileResource fileResource)
        {
            _fileResource = fileResource;
        }

        [CanBeNull]
        public FileResource GetFileResource() => _fileResource;

        public ResourceIdentifier GetIdentifier() => new ResourceIdentifier(ResName, ResType);

        public int Size => Data.Length;

        public override string ToString()
        {
            return $"{ResName}.{ResType.Extension} ({Size} bytes) from {FilePath}";
        }
    }
}
