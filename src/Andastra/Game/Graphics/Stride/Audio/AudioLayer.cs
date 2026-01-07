namespace Andastra.Runtime.Stride.Audio
{
    /// <summary>
    /// Helper class for Stride audio layer operations.
    /// Provides buffer type enumeration for DynamicSoundSource.FillBuffer calls.
    /// </summary>
    public static class AudioLayer
    {
        /// <summary>
        /// Buffer type for audio buffer filling operations.
        /// Maps to Stride's DynamicSoundSource buffer types.
        /// </summary>
        public enum BufferType
        {
            /// <summary>
            /// Normal buffer data (continues playback).
            /// </summary>
            Normal,

            /// <summary>
            /// End of stream marker (stops playback after buffer).
            /// </summary>
            EndOfStream
        }
    }
}


