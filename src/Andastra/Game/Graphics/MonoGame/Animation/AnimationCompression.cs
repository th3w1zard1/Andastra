using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Animation
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
        /// Uses error-based reduction: removes keyframes that can be accurately interpolated from neighbors.
        /// Based on industry-standard animation compression techniques (similar to Unity, Unreal Engine).
        /// </summary>
        /// <param name="keyframes">Compressed keyframes to reduce.</param>
        /// <param name="originalTimes">Original time values (for interpolation).</param>
        /// <param name="originalRotations">Original rotation quaternions (for error calculation).</param>
        /// <param name="originalTranslations">Original translation vectors (for error calculation).</param>
        /// <param name="originalScales">Original scale vectors (for error calculation).</param>
        /// <param name="duration">Animation duration.</param>
        /// <returns>Reduced list of keyframes.</returns>
        private List<CompressedKeyframe> ReduceKeyframes(
            List<CompressedKeyframe> keyframes,
            float[] originalTimes,
            float[] originalRotations,
            float[] originalTranslations,
            float[] originalScales,
            float duration)
        {
            if (keyframes == null || keyframes.Count <= 2)
            {
                // Need at least 2 keyframes (first and last must be preserved)
                return keyframes;
            }

            // Build list of keyframes to keep (always keep first and last)
            List<int> keepIndices = new List<int>();
            keepIndices.Add(0); // Always keep first keyframe

            // Iterate through keyframes (excluding first and last)
            for (int i = 1; i < keyframes.Count - 1; i++)
            {
                // Get previous and next kept keyframes
                int prevIndex = keepIndices[keepIndices.Count - 1];
                int nextIndex = i + 1;

                // Calculate time values for interpolation
                float prevTime = originalTimes[prevIndex];
                float currentTime = originalTimes[i];
                float nextTime = originalTimes[nextIndex];

                // Calculate interpolation factor (0 = prev, 1 = next)
                float t = (currentTime - prevTime) / (nextTime - prevTime);
                if (t <= 0.0f || t >= 1.0f)
                {
                    // Time is outside range, keep this keyframe
                    keepIndices.Add(i);
                    continue;
                }

                // Check if this keyframe can be removed by testing interpolation error
                bool canRemove = true;

                // Test rotation error (quaternion SLERP)
                if (originalRotations != null &&
                    prevIndex * 4 + 3 < originalRotations.Length &&
                    i * 4 + 3 < originalRotations.Length &&
                    nextIndex * 4 + 3 < originalRotations.Length)
                {
                    float prevQx = originalRotations[prevIndex * 4 + 0];
                    float prevQy = originalRotations[prevIndex * 4 + 1];
                    float prevQz = originalRotations[prevIndex * 4 + 2];
                    float prevQw = originalRotations[prevIndex * 4 + 3];

                    float nextQx = originalRotations[nextIndex * 4 + 0];
                    float nextQy = originalRotations[nextIndex * 4 + 1];
                    float nextQz = originalRotations[nextIndex * 4 + 2];
                    float nextQw = originalRotations[nextIndex * 4 + 3];

                    float currentQx = originalRotations[i * 4 + 0];
                    float currentQy = originalRotations[i * 4 + 1];
                    float currentQz = originalRotations[i * 4 + 2];
                    float currentQw = originalRotations[i * 4 + 3];

                    // Interpolate quaternion using SLERP
                    float interpQx, interpQy, interpQz, interpQw;
                    SlerpQuaternion(
                        prevQx, prevQy, prevQz, prevQw,
                        nextQx, nextQy, nextQz, nextQw,
                        t,
                        out interpQx, out interpQy, out interpQz, out interpQw);

                    // Calculate quaternion distance (angular error)
                    float quatError = QuaternionDistance(
                        currentQx, currentQy, currentQz, currentQw,
                        interpQx, interpQy, interpQz, interpQw);

                    if (quatError > _settings.MaxError)
                    {
                        canRemove = false;
                    }
                }

                // Test translation error (linear interpolation)
                if (canRemove && originalTranslations != null &&
                    prevIndex * 3 + 2 < originalTranslations.Length &&
                    i * 3 + 2 < originalTranslations.Length &&
                    nextIndex * 3 + 2 < originalTranslations.Length)
                {
                    float prevTx = originalTranslations[prevIndex * 3 + 0];
                    float prevTy = originalTranslations[prevIndex * 3 + 1];
                    float prevTz = originalTranslations[prevIndex * 3 + 2];

                    float nextTx = originalTranslations[nextIndex * 3 + 0];
                    float nextTy = originalTranslations[nextIndex * 3 + 1];
                    float nextTz = originalTranslations[nextIndex * 3 + 2];

                    float currentTx = originalTranslations[i * 3 + 0];
                    float currentTy = originalTranslations[i * 3 + 1];
                    float currentTz = originalTranslations[i * 3 + 2];

                    // Linear interpolation
                    float interpTx = Lerp(prevTx, nextTx, t);
                    float interpTy = Lerp(prevTy, nextTy, t);
                    float interpTz = Lerp(prevTz, nextTz, t);

                    // Calculate Euclidean distance error
                    float transError = (float)Math.Sqrt(
                        (currentTx - interpTx) * (currentTx - interpTx) +
                        (currentTy - interpTy) * (currentTy - interpTy) +
                        (currentTz - interpTz) * (currentTz - interpTz));

                    if (transError > _settings.MaxError)
                    {
                        canRemove = false;
                    }
                }

                // Test scale error (linear interpolation)
                if (canRemove && originalScales != null &&
                    prevIndex * 3 + 2 < originalScales.Length &&
                    i * 3 + 2 < originalScales.Length &&
                    nextIndex * 3 + 2 < originalScales.Length)
                {
                    float prevSx = originalScales[prevIndex * 3 + 0];
                    float prevSy = originalScales[prevIndex * 3 + 1];
                    float prevSz = originalScales[prevIndex * 3 + 2];

                    float nextSx = originalScales[nextIndex * 3 + 0];
                    float nextSy = originalScales[nextIndex * 3 + 1];
                    float nextSz = originalScales[nextIndex * 3 + 2];

                    float currentSx = originalScales[i * 3 + 0];
                    float currentSy = originalScales[i * 3 + 1];
                    float currentSz = originalScales[i * 3 + 2];

                    // Linear interpolation
                    float interpSx = Lerp(prevSx, nextSx, t);
                    float interpSy = Lerp(prevSy, nextSy, t);
                    float interpSz = Lerp(prevSz, nextSz, t);

                    // Calculate Euclidean distance error
                    float scaleError = (float)Math.Sqrt(
                        (currentSx - interpSx) * (currentSx - interpSx) +
                        (currentSy - interpSy) * (currentSy - interpSy) +
                        (currentSz - interpSz) * (currentSz - interpSz));

                    if (scaleError > _settings.MaxError)
                    {
                        canRemove = false;
                    }
                }

                // Keep keyframe if it cannot be removed
                if (!canRemove)
                {
                    keepIndices.Add(i);
                }
            }

            // Always keep last keyframe
            keepIndices.Add(keyframes.Count - 1);

            // Build reduced keyframe list
            List<CompressedKeyframe> reduced = new List<CompressedKeyframe>();
            foreach (int index in keepIndices)
            {
                reduced.Add(keyframes[index]);
            }

            return reduced;
        }

        /// <summary>
        /// Spherical linear interpolation (SLERP) for quaternions.
        /// Provides smooth rotation interpolation between two quaternions.
        /// </summary>
        private void SlerpQuaternion(
            float q1x, float q1y, float q1z, float q1w,
            float q2x, float q2y, float q2z, float q2w,
            float t,
            out float outX, out float outY, out float outZ, out float outW)
        {
            // Normalize input quaternions
            float len1 = (float)Math.Sqrt(q1x * q1x + q1y * q1y + q1z * q1z + q1w * q1w);
            if (len1 > 0.0f)
            {
                q1x /= len1;
                q1y /= len1;
                q1z /= len1;
                q1w /= len1;
            }

            float len2 = (float)Math.Sqrt(q2x * q2x + q2y * q2y + q2z * q2z + q2w * q2w);
            if (len2 > 0.0f)
            {
                q2x /= len2;
                q2y /= len2;
                q2z /= len2;
                q2w /= len2;
            }

            // Calculate dot product
            float dot = q1x * q2x + q1y * q2y + q1z * q2z + q1w * q2w;

            // Clamp dot product to [-1, 1] to avoid numerical issues
            if (dot > 1.0f) dot = 1.0f;
            if (dot < -1.0f) dot = -1.0f;

            // If quaternions are very close, use linear interpolation
            const float epsilon = 0.0001f;
            if (Math.Abs(dot) > 1.0f - epsilon)
            {
                // Linear interpolation for very similar quaternions
                outX = Lerp(q1x, q2x, t);
                outY = Lerp(q1y, q2y, t);
                outZ = Lerp(q1z, q2z, t);
                outW = Lerp(q1w, q2w, t);

                // Normalize result
                float len = (float)Math.Sqrt(outX * outX + outY * outY + outZ * outZ + outW * outW);
                if (len > 0.0f)
                {
                    outX /= len;
                    outY /= len;
                    outZ /= len;
                    outW /= len;
                }
                else
                {
                    outX = q1x;
                    outY = q1y;
                    outZ = q1z;
                    outW = q1w;
                }
            }
            else
            {
                // Spherical linear interpolation
                float theta = (float)Math.Acos(Math.Abs(dot));
                float sinTheta = (float)Math.Sin(theta);

                float w1 = (float)Math.Sin((1.0f - t) * theta) / sinTheta;
                float w2 = (float)Math.Sin(t * theta) / sinTheta;

                // Use shortest path (negate if dot < 0)
                if (dot < 0.0f)
                {
                    w2 = -w2;
                }

                outX = w1 * q1x + w2 * q2x;
                outY = w1 * q1y + w2 * q2y;
                outZ = w1 * q1z + w2 * q2z;
                outW = w1 * q1w + w2 * q2w;

                // Normalize result
                float len = (float)Math.Sqrt(outX * outX + outY * outY + outZ * outZ + outW * outW);
                if (len > 0.0f)
                {
                    outX /= len;
                    outY /= len;
                    outZ /= len;
                    outW /= len;
                }
            }
        }

        /// <summary>
        /// Calculates angular distance between two quaternions.
        /// Returns the angle in radians between the rotations represented by the quaternions.
        /// </summary>
        private float QuaternionDistance(
            float q1x, float q1y, float q1z, float q1w,
            float q2x, float q2y, float q2z, float q2w)
        {
            // Normalize quaternions
            float len1 = (float)Math.Sqrt(q1x * q1x + q1y * q1y + q1z * q1z + q1w * q1w);
            if (len1 > 0.0f)
            {
                q1x /= len1;
                q1y /= len1;
                q1z /= len1;
                q1w /= len1;
            }

            float len2 = (float)Math.Sqrt(q2x * q2x + q2y * q2y + q2z * q2z + q2w * q2w);
            if (len2 > 0.0f)
            {
                q2x /= len2;
                q2y /= len2;
                q2z /= len2;
                q2w /= len2;
            }

            // Calculate dot product
            float dot = q1x * q2x + q1y * q2y + q1z * q2z + q1w * q2w;

            // Clamp to [-1, 1]
            if (dot > 1.0f) dot = 1.0f;
            if (dot < -1.0f) dot = -1.0f;

            // Angular distance is the angle between rotations
            // Use absolute value to get shortest path
            return (float)Math.Acos(Math.Abs(dot));
        }

        /// <summary>
        /// Linear interpolation between two float values.
        /// </summary>
        private float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}

