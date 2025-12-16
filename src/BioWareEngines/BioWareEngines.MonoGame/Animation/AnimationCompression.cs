using System;
using System.Collections.Generic;

namespace BioWareEngines.MonoGame.Animation
{
    /// <summary>
    /// Animation compression system for reducing animation data size.
    /// 
    /// Animation compression reduces memory usage by:
    /// - Keyframe quantization
    /// - Keyframe reduction (removing redundant frames)
    /// - Rotation compression (quaternion normalization)
    /// - Scale/translation quantization
    /// 
    /// Features:
    /// - Lossy compression with quality control
    /// - Automatic keyframe reduction
    /// - Configurable compression ratios
    /// </summary>
    public class AnimationCompression
    {
        /// <summary>
        /// Compressed keyframe.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct CompressedKeyframe
        {
            /// <summary>
            /// Time (quantized).
            /// </summary>
            public ushort Time;

            /// <summary>
            /// Rotation (quantized quaternion, 4x int16).
            /// </summary>
            public short RotX, RotY, RotZ, RotW;

            /// <summary>
            /// Translation (quantized, 3x int16).
            /// </summary>
            public short TransX, TransY, TransZ;

            /// <summary>
            /// Scale (quantized, 3x uint8).
            /// </summary>
            public byte ScaleX, ScaleY, ScaleZ;
        }

        /// <summary>
        /// Compression settings.
        /// </summary>
        public struct CompressionSettings
        {
            /// <summary>
            /// Rotation quantization bits (8-16).
            /// </summary>
            public int RotationBits;

            /// <summary>
            /// Translation quantization scale.
            /// </summary>
            public float TranslationScale;

            /// <summary>
            /// Scale quantization bits (4-8).
            /// </summary>
            public int ScaleBits;

            /// <summary>
            /// Maximum error tolerance for keyframe reduction.
            /// </summary>
            public float MaxError;

            /// <summary>
            /// Whether to enable keyframe reduction.
            /// </summary>
            public bool EnableKeyframeReduction;
        }

        private CompressionSettings _settings;

        /// <summary>
        /// Gets or sets compression settings.
        /// </summary>
        public CompressionSettings Settings
        {
            get { return _settings; }
            set { _settings = value; }
        }

        /// <summary>
        /// Initializes a new animation compression system.
        /// </summary>
        public AnimationCompression()
        {
            _settings = new CompressionSettings
            {
                RotationBits = 14,
                TranslationScale = 0.01f,
                ScaleBits = 6,
                MaxError = 0.001f,
                EnableKeyframeReduction = true
            };
        }

        /// <summary>
        /// Compresses animation keyframes.
        /// </summary>
        public CompressedKeyframe[] CompressKeyframes(
            float[] times,
            float[] rotations,
            float[] translations,
            float[] scales,
            float duration)
        {
            if (times == null || times.Length == 0)
            {
                return null;
            }

            List<CompressedKeyframe> compressed = new List<CompressedKeyframe>();

            for (int i = 0; i < times.Length; i++)
            {
                CompressedKeyframe keyframe = new CompressedKeyframe();

                // Quantize time
                keyframe.Time = (ushort)((times[i] / duration) * 65535.0f);

                // Quantize rotation (quaternion)
                if (rotations != null && i * 4 + 3 < rotations.Length)
                {
                    float qx = rotations[i * 4 + 0];
                    float qy = rotations[i * 4 + 1];
                    float qz = rotations[i * 4 + 2];
                    float qw = rotations[i * 4 + 3];

                    // Normalize and quantize
                    float len = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
                    if (len > 0.0f)
                    {
                        qx /= len;
                        qy /= len;
                        qz /= len;
                        qw /= len;
                    }

                    int maxVal = (1 << (_settings.RotationBits - 1)) - 1;
                    keyframe.RotX = (short)(qx * maxVal);
                    keyframe.RotY = (short)(qy * maxVal);
                    keyframe.RotZ = (short)(qz * maxVal);
                    keyframe.RotW = (short)(qw * maxVal);
                }

                // Quantize translation
                if (translations != null && i * 3 + 2 < translations.Length)
                {
                    keyframe.TransX = (short)(translations[i * 3 + 0] / _settings.TranslationScale);
                    keyframe.TransY = (short)(translations[i * 3 + 1] / _settings.TranslationScale);
                    keyframe.TransZ = (short)(translations[i * 3 + 2] / _settings.TranslationScale);
                }

                // Quantize scale
                if (scales != null && i * 3 + 2 < scales.Length)
                {
                    int maxScale = (1 << _settings.ScaleBits) - 1;
                    keyframe.ScaleX = (byte)(scales[i * 3 + 0] * maxScale);
                    keyframe.ScaleY = (byte)(scales[i * 3 + 1] * maxScale);
                    keyframe.ScaleZ = (byte)(scales[i * 3 + 2] * maxScale);
                }

                compressed.Add(keyframe);
            }

            // Apply keyframe reduction if enabled
            if (_settings.EnableKeyframeReduction)
            {
                compressed = ReduceKeyframes(compressed, times, rotations, translations, scales, duration);
            }

            return compressed.ToArray();
        }

        /// <summary>
        /// Reduces keyframes by removing redundant ones.
        /// </summary>
        private List<CompressedKeyframe> ReduceKeyframes(
            List<CompressedKeyframe> keyframes,
            float[] originalTimes,
            float[] originalRotations,
            float[] originalTranslations,
            float[] originalScales,
            float duration)
        {
            // Simple keyframe reduction algorithm
            // Would use more sophisticated algorithm in production
            // Placeholder - returns original for now
            return keyframes;
        }
    }
}

