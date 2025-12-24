using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Serializer for graphics settings import/export functionality.
    /// </summary>
    public static class GraphicsSettingsSerializer
    {
        /// <summary>
        /// Exports graphics settings to an XML file.
        /// </summary>
        public static void ExportToXml(GraphicsSettingsData settings, string filePath)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            try
            {
                var serializer = new XmlSerializer(typeof(GraphicsSettingsData));
                var xmlSettings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    Encoding = Encoding.UTF8
                };

                using (var writer = XmlWriter.Create(filePath, xmlSettings))
                {
                    serializer.Serialize(writer, settings);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export graphics settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Imports graphics settings from an XML file.
        /// </summary>
        public static GraphicsSettingsData ImportFromXml(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Graphics settings file not found.", filePath);

            try
            {
                var serializer = new XmlSerializer(typeof(GraphicsSettingsData));
                using (var reader = XmlReader.Create(filePath))
                {
                    return (GraphicsSettingsData)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to import graphics settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates graphics settings for consistency and correctness.
        /// </summary>
        public static GraphicsSettingsValidationResult Validate(GraphicsSettingsData settings)
        {
            var result = new GraphicsSettingsValidationResult();

            if (settings == null)
            {
                result.AddError("Settings cannot be null.");
                return result;
            }

            // Validate window dimensions
            if (settings.WindowWidth.HasValue)
            {
                if (settings.WindowWidth < 320 || settings.WindowWidth > 7680)
                    result.AddError($"Window width must be between 320 and 7680 pixels. Current: {settings.WindowWidth}");
            }

            if (settings.WindowHeight.HasValue)
            {
                if (settings.WindowHeight < 240 || settings.WindowHeight > 4320)
                    result.AddError($"Window height must be between 240 and 4320 pixels. Current: {settings.WindowHeight}");
            }

            // Validate color values
            ValidateColorComponent(result, "BasicEffectAmbientLightColor",
                settings.BasicEffectAmbientLightColorX,
                settings.BasicEffectAmbientLightColorY,
                settings.BasicEffectAmbientLightColorZ);

            ValidateColorComponent(result, "BasicEffectDiffuseColor",
                settings.BasicEffectDiffuseColorX,
                settings.BasicEffectDiffuseColorY,
                settings.BasicEffectDiffuseColorZ);

            ValidateColorComponent(result, "BasicEffectEmissiveColor",
                settings.BasicEffectEmissiveColorX,
                settings.BasicEffectEmissiveColorY,
                settings.BasicEffectEmissiveColorZ);

            ValidateColorComponent(result, "BasicEffectSpecularColor",
                settings.BasicEffectSpecularColorX,
                settings.BasicEffectSpecularColorY,
                settings.BasicEffectSpecularColorZ);

            // Validate alpha
            if (settings.BasicEffectAlpha.HasValue)
            {
                if (settings.BasicEffectAlpha < 0.0f || settings.BasicEffectAlpha > 1.0f)
                    result.AddError($"BasicEffectAlpha must be between 0.0 and 1.0. Current: {settings.BasicEffectAlpha}");
            }

            // Validate sampler settings
            if (settings.SamplerMaxAnisotropy.HasValue)
            {
                if (settings.SamplerMaxAnisotropy < 0 || settings.SamplerMaxAnisotropy > 16)
                    result.AddError($"SamplerMaxAnisotropy must be between 0 and 16. Current: {settings.SamplerMaxAnisotropy}");
            }

            if (settings.SamplerMaxMipLevel.HasValue)
            {
                if (settings.SamplerMaxMipLevel < 0 || settings.SamplerMaxMipLevel > 15)
                    result.AddError($"SamplerMaxMipLevel must be between 0 and 15. Current: {settings.SamplerMaxMipLevel}");
            }

            // Validate stencil values
            if (settings.DepthStencilReferenceStencil.HasValue)
            {
                if (settings.DepthStencilReferenceStencil < 0 || settings.DepthStencilReferenceStencil > 255)
                    result.AddError($"DepthStencilReferenceStencil must be between 0 and 255. Current: {settings.DepthStencilReferenceStencil}");
            }

            if (settings.DepthStencilStencilMask.HasValue)
            {
                if (settings.DepthStencilStencilMask < 0 || settings.DepthStencilStencilMask > 255)
                    result.AddError($"DepthStencilStencilMask must be between 0 and 255. Current: {settings.DepthStencilStencilMask}");
            }

            if (settings.DepthStencilStencilWriteMask.HasValue)
            {
                if (settings.DepthStencilStencilWriteMask < 0 || settings.DepthStencilStencilWriteMask > 255)
                    result.AddError($"DepthStencilStencilWriteMask must be between 0 and 255. Current: {settings.DepthStencilStencilWriteMask}");
            }

            // Validate spatial audio
            if (settings.SpatialAudioDopplerFactor.HasValue)
            {
                if (settings.SpatialAudioDopplerFactor < 0.0f || settings.SpatialAudioDopplerFactor > 10.0f)
                    result.AddError($"SpatialAudioDopplerFactor must be between 0.0 and 10.0. Current: {settings.SpatialAudioDopplerFactor}");
            }

            if (settings.SpatialAudioSpeedOfSound.HasValue)
            {
                if (settings.SpatialAudioSpeedOfSound < 1.0f || settings.SpatialAudioSpeedOfSound > 10000.0f)
                    result.AddError($"SpatialAudioSpeedOfSound must be between 1.0 and 10000.0 m/s. Current: {settings.SpatialAudioSpeedOfSound}");
            }

            return result;
        }

        private static void ValidateColorComponent(GraphicsSettingsValidationResult result, string name,
            float? x, float? y, float? z)
        {
            if (x.HasValue && (x < 0.0f || x > 1.0f))
                result.AddError($"{name}X must be between 0.0 and 1.0. Current: {x}");
            if (y.HasValue && (y < 0.0f || y > 1.0f))
                result.AddError($"{name}Y must be between 0.0 and 1.0. Current: {y}");
            if (z.HasValue && (z < 0.0f || z > 1.0f))
                result.AddError($"{name}Z must be between 0.0 and 1.0. Current: {z}");
        }
    }

    /// <summary>
    /// Validation result for graphics settings.
    /// </summary>
    public class GraphicsSettingsValidationResult
    {
        private readonly System.Collections.Generic.List<string> _errors = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _warnings = new System.Collections.Generic.List<string>();

        /// <summary>
        /// Gets whether validation passed (no errors).
        /// </summary>
        public bool IsValid => _errors.Count == 0;

        /// <summary>
        /// Gets all validation errors.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Gets all validation warnings.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Adds a validation error.
        /// </summary>
        public void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
                _errors.Add(error);
        }

        /// <summary>
        /// Adds a validation warning.
        /// </summary>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning))
                _warnings.Add(warning);
        }

        /// <summary>
        /// Gets a formatted message with all errors and warnings.
        /// </summary>
        public string GetFormattedMessage()
        {
            var sb = new System.Text.StringBuilder();
            if (_errors.Count > 0)
            {
                sb.AppendLine("Errors:");
                foreach (var error in _errors)
                    sb.AppendLine($"  • {error}");
            }
            if (_warnings.Count > 0)
            {
                sb.AppendLine("Warnings:");
                foreach (var warning in _warnings)
                    sb.AppendLine($"  • {warning}");
            }
            return sb.ToString();
        }
    }
}

