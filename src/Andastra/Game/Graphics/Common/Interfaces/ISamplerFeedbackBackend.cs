using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support Sampler Feedback.
    /// Sampler Feedback allows shaders to provide feedback about texture sampling operations,
    /// enabling features like texture streaming, mip bias optimization, and adaptive quality.
    ///
    /// Based on DirectX 12 Sampler Feedback: https://devblogs.microsoft.com/directx/sampler-feedback-some-useful-properties/
    /// </summary>
    public interface ISamplerFeedbackBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether Sampler Feedback is available and supported.
        /// </summary>
        bool SamplerFeedbackAvailable { get; }

        /// <summary>
        /// Creates a sampler feedback texture for tracking which mip levels are accessed.
        /// </summary>
        /// <param name="width">Texture width in tiles.</param>
        /// <param name="height">Texture height in tiles.</param>
        /// <param name="format">Sampler feedback format (typically D3D12_FEEDBACK_MAP_FORMAT_UINT8_8x8).</param>
        /// <returns>Handle to the created sampler feedback texture.</returns>
        IntPtr CreateSamplerFeedbackTexture(int width, int height, TextureFormat format);

        /// <summary>
        /// Binds a sampler feedback texture to a shader resource slot.
        /// </summary>
        /// <param name="feedbackTexture">Sampler feedback texture handle.</param>
        /// <param name="slot">Shader resource slot index.</param>
        void SetSamplerFeedbackTexture(IntPtr feedbackTexture, int slot);

        /// <summary>
        /// Reads sampler feedback data from the GPU into CPU-accessible memory.
        /// </summary>
        /// <param name="feedbackTexture">Sampler feedback texture handle.</param>
        /// <param name="data">Output buffer for feedback data.</param>
        /// <param name="sizeInBytes">Size of the output buffer.</param>
        void ReadSamplerFeedback(IntPtr feedbackTexture, byte[] data, int sizeInBytes);
    }
}

