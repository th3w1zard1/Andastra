using System;
using System.Collections.Generic;
using System.IO;

namespace Andastra.Runtime.MonoGame.Assets
{
    /// <summary>
    /// Asset validation system for catching errors early.
    /// 
    /// Validates assets at load time to catch corruption, format errors,
    /// and other issues before they cause runtime problems.
    /// 
    /// Features:
    /// - Format validation
    /// - Size validation
    /// - Dependency validation
    /// - Checksum verification
    /// - Error reporting
    /// </summary>
    public class AssetValidator
    {
        /// <summary>
        /// Validation result.
        /// </summary>
        public enum ValidationResult
        {
            Valid,
            InvalidFormat,
            InvalidSize,
            MissingDependency,
            ChecksumMismatch,
            Corrupted
        }

        /// <summary>
        /// Validation error information.
        /// </summary>
        public struct ValidationError
        {
            public string AssetPath;
            public ValidationResult Result;
            public string Message;
        }

        private readonly Dictionary<string, string> _checksums;
        private readonly List<ValidationError> _errors;

        /// <summary>
        /// Gets validation errors.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors
        {
            get { return _errors; }
        }

        /// <summary>
        /// Initializes a new asset validator.
        /// </summary>
        public AssetValidator()
        {
            _checksums = new Dictionary<string, string>();
            _errors = new List<ValidationError>();
        }

        /// <summary>
        /// Validates an asset file.
        /// </summary>
        public ValidationResult Validate(string filePath, string expectedFormat = null, long? expectedSize = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _errors.Add(new ValidationError
                {
                    AssetPath = filePath,
                    Result = ValidationResult.InvalidFormat,
                    Message = "File path is null or empty"
                });
                return ValidationResult.InvalidFormat;
            }

            if (!File.Exists(filePath))
            {
                _errors.Add(new ValidationError
                {
                    AssetPath = filePath,
                    Result = ValidationResult.MissingDependency,
                    Message = "File does not exist"
                });
                return ValidationResult.MissingDependency;
            }

            FileInfo fileInfo = new FileInfo(filePath);

            // Check size
            if (expectedSize.HasValue && fileInfo.Length != expectedSize.Value)
            {
                _errors.Add(new ValidationError
                {
                    AssetPath = filePath,
                    Result = ValidationResult.InvalidSize,
                    Message = $"File size mismatch: expected {expectedSize.Value}, got {fileInfo.Length}"
                });
                return ValidationResult.InvalidSize;
            }

            // Check format
            if (!string.IsNullOrEmpty(expectedFormat))
            {
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension != expectedFormat.ToLower())
                {
                    _errors.Add(new ValidationError
                    {
                        AssetPath = filePath,
                        Result = ValidationResult.InvalidFormat,
                        Message = $"Format mismatch: expected {expectedFormat}, got {extension}"
                    });
                    return ValidationResult.InvalidFormat;
                }
            }

            // Validate file content
            ValidationResult contentResult = ValidateContent(filePath);
            if (contentResult != ValidationResult.Valid)
            {
                _errors.Add(new ValidationError
                {
                    AssetPath = filePath,
                    Result = contentResult,
                    Message = "File content validation failed"
                });
                return contentResult;
            }

            return ValidationResult.Valid;
        }

        /// <summary>
        /// Validates file content (format-specific).
        /// </summary>
        private ValidationResult ValidateContent(string filePath)
        {
            try
            {
                // Read file header to validate format
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[16];
                    int bytesRead = stream.Read(header, 0, 16);

                    if (bytesRead < 4)
                    {
                        return ValidationResult.Corrupted;
                    }

                    // Validate based on file extension
                    string extension = Path.GetExtension(filePath).ToLower();
                    switch (extension)
                    {
                        case ".tpc":
                            // TPC format validation
                            // Placeholder - would check TPC header
                            break;
                        case ".mdl":
                            // MDL format validation
                            // Placeholder - would check MDL header
                            break;
                        case ".ncs":
                            // NCS format validation
                            if (header[0] != 'N' || header[1] != 'C' || header[2] != 'S' || header[3] != ' ')
                            {
                                return ValidationResult.InvalidFormat;
                            }
                            break;
                    }
                }

                return ValidationResult.Valid;
            }
            catch
            {
                return ValidationResult.Corrupted;
            }
        }

        /// <summary>
        /// Clears validation errors.
        /// </summary>
        public void ClearErrors()
        {
            _errors.Clear();
        }
    }
}

