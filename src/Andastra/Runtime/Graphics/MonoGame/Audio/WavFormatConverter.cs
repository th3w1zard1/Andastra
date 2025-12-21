using System;
using System.IO;
using Andastra.Parsing.Formats.WAV;

namespace Andastra.Runtime.MonoGame.Audio
{
    /// <summary>
    /// Converts WAV files from various formats to MonoGame-compatible PCM format.
    /// 
    /// MonoGame's SoundEffect.FromStream() requires:
    /// - Standard RIFF/WAVE format
    /// - PCM encoding (format 0x0001)
    /// - 8-bit or 16-bit samples (preferably 16-bit)
    /// - Mono or stereo channels
    /// - Common sample rates (8000, 11025, 22050, 44100, 48000 Hz)
    /// 
    /// This converter handles:
    /// - PCM format (pass-through with bit depth conversion if needed)
    /// - IMA ADPCM (format 0x0011) - decodes to 16-bit PCM
    /// - MS ADPCM (format 0x0002) - decodes to 16-bit PCM
    /// - A-Law (format 0x0006) - decodes to 16-bit PCM
    /// - μ-Law (format 0x0007) - decodes to 16-bit PCM
    /// - Different bit depths (8, 16, 24, 32) - converts to 16-bit PCM
    /// - Mono and stereo channels - preserves channel configuration
    /// - All common sample rates - preserves sample rate
    /// </summary>
    /// <remarks>
    /// Based on WAV format specifications and MonoGame requirements.
    /// Reference: vendor/PyKotor/wiki/WAV-File-Format.md
    /// </remarks>
    internal static class WavFormatConverter
    {
        /// <summary>
        /// Converts a WAV object to MonoGame-compatible RIFF/WAVE PCM format.
        /// </summary>
        /// <param name="wav">The WAV object to convert.</param>
        /// <returns>Byte array containing MonoGame-compatible RIFF/WAVE PCM data, or null if conversion fails.</returns>
        public static byte[] ConvertToMonoGameFormat(WAV wav)
        {
            if (wav == null)
            {
                return null;
            }

            // Handle MP3 format - MonoGame doesn't support MP3 directly
            if (wav.AudioFormat == AudioFormat.MP3)
            {
                Console.WriteLine("[WavFormatConverter] MP3 format not supported by MonoGame SoundEffect");
                return null;
            }

            // Convert audio data to 16-bit PCM
            byte[] pcmData = ConvertToPcm(wav);
            if (pcmData == null)
            {
                return null;
            }

            // Determine output format parameters
            int outputChannels = Math.Max(1, Math.Min(2, wav.Channels)); // Mono (1) or stereo (2)
            int outputSampleRate = wav.SampleRate;
            int outputBitsPerSample = 16; // Always 16-bit for MonoGame
            int outputBlockAlign = outputChannels * (outputBitsPerSample / 8); // 2 bytes per sample * channels
            uint outputBytesPerSec = (uint)(outputSampleRate * outputBlockAlign);

            // Create RIFF/WAVE structure
            return CreateRiffWaveFile(pcmData, outputChannels, outputSampleRate, outputBitsPerSample, outputBlockAlign, outputBytesPerSec);
        }

        /// <summary>
        /// Converts WAV audio data to 16-bit PCM format.
        /// </summary>
        private static byte[] ConvertToPcm(WAV wav)
        {
            if (wav.Data == null || wav.Data.Length == 0)
            {
                return null;
            }

            WaveEncoding encoding = GetEncodingEnum(wav.Encoding);

            switch (encoding)
            {
                case WaveEncoding.PCM:
                    return ConvertPcmTo16Bit(wav);

                case WaveEncoding.IMA_ADPCM:
                    return DecodeImaAdpcm(wav);

                case WaveEncoding.MS_ADPCM:
                    return DecodeMsAdpcm(wav);

                case WaveEncoding.ALAW:
                    return DecodeALaw(wav);

                case WaveEncoding.MULAW:
                    return DecodeMuLaw(wav);

                default:
                    Console.WriteLine($"[WavFormatConverter] Unsupported encoding format: 0x{wav.Encoding:X2}");
                    return null;
            }
        }

        /// <summary>
        /// Gets the WaveEncoding enum value from encoding integer.
        /// </summary>
        private static WaveEncoding GetEncodingEnum(int encoding)
        {
            try
            {
                return (WaveEncoding)encoding;
            }
            catch
            {
                return (WaveEncoding)0;
            }
        }

        /// <summary>
        /// Converts PCM data to 16-bit PCM, handling different input bit depths.
        /// </summary>
        private static byte[] ConvertPcmTo16Bit(WAV wav)
        {
            if (wav.BitsPerSample == 16)
            {
                // Already 16-bit, return as-is
                return (byte[])wav.Data.Clone();
            }

            int inputChannels = wav.Channels;
            int inputBitsPerSample = wav.BitsPerSample;
            int inputBytesPerSample = inputBitsPerSample / 8;
            int inputSamples = wav.Data.Length / (inputBytesPerSample * inputChannels);

            // Output: 16-bit (2 bytes per sample)
            byte[] output = new byte[inputSamples * inputChannels * 2];

            using (var inputStream = new MemoryStream(wav.Data))
            using (var outputStream = new MemoryStream(output))
            using (var inputReader = new BinaryReader(inputStream))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                for (int i = 0; i < inputSamples; i++)
                {
                    for (int channel = 0; channel < inputChannels; channel++)
                    {
                        short sample16 = 0;

                        switch (inputBitsPerSample)
                        {
                            case 8:
                                // 8-bit: 0-255 maps to -128 to 127
                                byte sample8 = inputReader.ReadByte();
                                sample16 = (short)((sample8 - 128) << 8);
                                break;

                            case 16:
                                // Already 16-bit
                                sample16 = inputReader.ReadInt16();
                                break;

                            case 24:
                                // 24-bit: read 3 bytes, sign-extend to 16-bit
                                byte b1 = inputReader.ReadByte();
                                byte b2 = inputReader.ReadByte();
                                byte b3 = inputReader.ReadByte();
                                int sample24 = (b3 << 16) | (b2 << 8) | b1;
                                // Sign-extend from 24-bit to 32-bit, then to 16-bit
                                if ((sample24 & 0x800000) != 0)
                                {
                                    sample24 |= unchecked((int)0xFF000000U); // Sign extend
                                }
                                sample16 = (short)((int)(sample24 >> 8)); // Convert to 16-bit
                                break;

                            case 32:
                                // 32-bit: read 4 bytes, convert to 16-bit
                                int sample32 = inputReader.ReadInt32();
                                sample16 = (short)(sample32 >> 16); // Convert to 16-bit
                                break;

                            default:
                                Console.WriteLine($"[WavFormatConverter] Unsupported PCM bit depth: {inputBitsPerSample}");
                                return null;
                        }

                        outputWriter.Write(sample16);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Decodes IMA ADPCM to 16-bit PCM.
        /// </summary>
        /// <remarks>
        /// IMA ADPCM (Interactive Multimedia Association ADPCM) is a 4-bit ADPCM format.
        /// Reference: IMA ADPCM specification and common implementations.
        /// </remarks>
        private static byte[] DecodeImaAdpcm(WAV wav)
        {
            // IMA ADPCM uses 4 bits per sample
            // Block structure: predictor (2 bytes) + compressed samples
            int channels = wav.Channels;
            int blockAlign = wav.BlockAlign;
            if (blockAlign == 0)
            {
                blockAlign = channels * 36; // Default IMA ADPCM block size
            }

            int samplesPerBlock = ((blockAlign - (channels * 4)) * 2) + 1; // 4 bytes header per channel, 2 samples per byte
            int numBlocks = (wav.Data.Length + blockAlign - 1) / blockAlign;

            // Estimate output size (worst case)
            int outputSamples = numBlocks * samplesPerBlock;
            byte[] output = new byte[outputSamples * channels * 2]; // 16-bit = 2 bytes per sample

            using (var inputStream = new MemoryStream(wav.Data))
            using (var outputStream = new MemoryStream(output))
            using (var inputReader = new BinaryReader(inputStream))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                int[] stepTable = new int[]
                {
                    7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
                    34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
                    157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658,
                    724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
                    3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
                    15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
                };

                int[] indexTable = new int[]
                {
                    -1, -1, -1, -1, 2, 4, 6, 8,
                    -1, -1, -1, -1, 2, 4, 6, 8
                };

                int outputPos = 0;
                int maxOutputPos = output.Length;

                for (int block = 0; block < numBlocks && inputStream.Position < wav.Data.Length; block++)
                {
                    int[] predictors = new int[channels];
                    int[] stepIndices = new int[channels];

                    // Read block headers (one per channel)
                    for (int ch = 0; ch < channels; ch++)
                    {
                        if (inputStream.Position + 2 > wav.Data.Length)
                        {
                            break;
                        }
                        predictors[ch] = inputReader.ReadInt16();
                        stepIndices[ch] = inputReader.ReadInt16();
                        if (stepIndices[ch] < 0) stepIndices[ch] = 0;
                        if (stepIndices[ch] > 88) stepIndices[ch] = 88;
                    }

                    // Decode samples in this block
                    int samplesDecoded = 0;
                    int blockDataStart = (int)inputStream.Position;
                    int blockDataEnd = Math.Min(blockDataStart + blockAlign - (channels * 4), wav.Data.Length);

                    while (inputStream.Position < blockDataEnd && samplesDecoded < samplesPerBlock)
                    {
                        for (int ch = 0; ch < channels; ch++)
                        {
                            if (inputStream.Position >= blockDataEnd)
                            {
                                break;
                            }

                            byte compressedByte = inputReader.ReadByte();

                            // Each byte contains 2 samples (4 bits each)
                            for (int sampleInByte = 0; sampleInByte < 2; sampleInByte++)
                            {
                                int code = (compressedByte >> (sampleInByte * 4)) & 0x0F;

                                int step = stepTable[stepIndices[ch]];
                                int difference = step >> 3;

                                if ((code & 1) != 0) difference += step >> 2;
                                if ((code & 2) != 0) difference += step >> 1;
                                if ((code & 4) != 0) difference += step;
                                if ((code & 8) != 0) difference = -difference;

                                predictors[ch] += difference;
                                if (predictors[ch] > 32767) predictors[ch] = 32767;
                                if (predictors[ch] < -32768) predictors[ch] = -32768;

                                stepIndices[ch] += indexTable[code];
                                if (stepIndices[ch] < 0) stepIndices[ch] = 0;
                                if (stepIndices[ch] > 88) stepIndices[ch] = 88;

                                // Write 16-bit sample
                                if (outputPos + 2 <= maxOutputPos)
                                {
                                    outputWriter.Write((short)predictors[ch]);
                                    outputPos += 2;
                                }

                                samplesDecoded++;
                                if (samplesDecoded >= samplesPerBlock)
                                {
                                    break;
                                }
                            }

                            if (samplesDecoded >= samplesPerBlock)
                            {
                                break;
                            }
                        }
                    }
                }

                // Trim output to actual size
                if (outputPos < output.Length)
                {
                    byte[] trimmed = new byte[outputPos];
                    Array.Copy(output, 0, trimmed, 0, outputPos);
                    return trimmed;
                }
            }

            return output;
        }

        /// <summary>
        /// Decodes MS ADPCM to 16-bit PCM.
        /// </summary>
        /// <remarks>
        /// Microsoft ADPCM is a 4-bit ADPCM format with coefficient tables.
        /// Reference: Microsoft ADPCM specification.
        /// </remarks>
        private static byte[] DecodeMsAdpcm(WAV wav)
        {
            // MS ADPCM uses 4 bits per sample
            // Block structure: header (7 bytes per channel) + compressed samples
            int channels = wav.Channels;
            int blockAlign = wav.BlockAlign;
            if (blockAlign == 0)
            {
                blockAlign = channels * 256; // Default MS ADPCM block size
            }

            int samplesPerBlock = ((blockAlign - (channels * 7)) * 2) + 2; // 7 bytes header per channel, 2 samples per byte
            int numBlocks = (wav.Data.Length + blockAlign - 1) / blockAlign;

            // Estimate output size
            int outputSamples = numBlocks * samplesPerBlock;
            byte[] output = new byte[outputSamples * channels * 2]; // 16-bit = 2 bytes per sample

            // MS ADPCM coefficient tables (standard)
            int[][] coefficientTable = new int[][]
            {
                new int[] { 256, 0 },
                new int[] { 512, -256 },
                new int[] { 0, 0 },
                new int[] { 192, 64 },
                new int[] { 240, 0 },
                new int[] { 460, -208 },
                new int[] { 392, -232 }
            };

            using (var inputStream = new MemoryStream(wav.Data))
            using (var outputStream = new MemoryStream(output))
            using (var inputReader = new BinaryReader(inputStream))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                int outputPos = 0;
                int maxOutputPos = output.Length;

                for (int block = 0; block < numBlocks && inputStream.Position < wav.Data.Length; block++)
                {
                    int[] predictors = new int[channels];
                    int[] deltas = new int[channels];
                    int[] sample1s = new int[channels];
                    int[] sample2s = new int[channels];
                    int[] coefficients = new int[channels];

                    // Read block headers (7 bytes per channel)
                    for (int ch = 0; ch < channels; ch++)
                    {
                        if (inputStream.Position + 7 > wav.Data.Length)
                        {
                            break;
                        }
                        byte predictor = inputReader.ReadByte();
                        if (predictor >= coefficientTable.Length)
                        {
                            predictor = 0;
                        }
                        coefficients[ch] = predictor;
                        deltas[ch] = inputReader.ReadInt16();
                        sample1s[ch] = inputReader.ReadInt16();
                        sample2s[ch] = inputReader.ReadInt16();
                        predictors[ch] = sample1s[ch];
                    }

                    // Write first two samples (from header)
                    for (int ch = 0; ch < channels; ch++)
                    {
                        if (outputPos + 2 <= maxOutputPos)
                        {
                            outputWriter.Write((short)sample2s[ch]);
                            outputPos += 2;
                        }
                        if (outputPos + 2 <= maxOutputPos)
                        {
                            outputWriter.Write((short)sample1s[ch]);
                            outputPos += 2;
                        }
                    }

                    // Decode remaining samples
                    int samplesDecoded = 2;
                    int blockDataStart = (int)inputStream.Position;
                    int blockDataEnd = Math.Min(blockDataStart + blockAlign - (channels * 7), wav.Data.Length);

                    while (inputStream.Position < blockDataEnd && samplesDecoded < samplesPerBlock)
                    {
                        for (int ch = 0; ch < channels; ch++)
                        {
                            if (inputStream.Position >= blockDataEnd)
                            {
                                break;
                            }

                            byte compressedByte = inputReader.ReadByte();

                            // Each byte contains 2 samples (4 bits each)
                            for (int sampleInByte = 0; sampleInByte < 2; sampleInByte++)
                            {
                                int code = (compressedByte >> (sampleInByte * 4)) & 0x0F;

                                int[] coeffs = coefficientTable[coefficients[ch]];
                                int predictor = ((sample1s[ch] * coeffs[0]) + (sample2s[ch] * coeffs[1])) >> 8;

                                int difference = code & 0x07;
                                difference = (difference << 1) + 1;
                                difference = (difference * deltas[ch]) >> 3;
                                if ((code & 0x08) != 0)
                                {
                                    difference = -difference;
                                }

                                predictor += difference;
                                if (predictor > 32767) predictor = 32767;
                                if (predictor < -32768) predictor = -32768;

                                sample2s[ch] = sample1s[ch];
                                sample1s[ch] = predictor;

                                deltas[ch] = (AdaptationTable[code] * deltas[ch]) >> 8;
                                if (deltas[ch] < 16) deltas[ch] = 16;

                                // Write 16-bit sample
                                if (outputPos + 2 <= maxOutputPos)
                                {
                                    outputWriter.Write((short)predictor);
                                    outputPos += 2;
                                }

                                samplesDecoded++;
                                if (samplesDecoded >= samplesPerBlock)
                                {
                                    break;
                                }
                            }

                            if (samplesDecoded >= samplesPerBlock)
                            {
                                break;
                            }
                        }
                    }
                }

                // Trim output to actual size
                if (outputPos < output.Length)
                {
                    byte[] trimmed = new byte[outputPos];
                    Array.Copy(output, 0, trimmed, 0, outputPos);
                    return trimmed;
                }
            }

            return output;
        }

        /// <summary>
        /// MS ADPCM adaptation table.
        /// </summary>
        private static readonly int[] AdaptationTable = new int[]
        {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230
        };

        /// <summary>
        /// Decodes A-Law to 16-bit PCM.
        /// </summary>
        private static byte[] DecodeALaw(WAV wav)
        {
            int samples = wav.Data.Length;
            byte[] output = new byte[samples * wav.Channels * 2]; // 16-bit output

            using (var inputStream = new MemoryStream(wav.Data))
            using (var outputStream = new MemoryStream(output))
            using (var inputReader = new BinaryReader(inputStream))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                for (int i = 0; i < samples; i++)
                {
                    for (int ch = 0; ch < wav.Channels; ch++)
                    {
                        byte alawByte = inputReader.ReadByte();
                        short pcm = ALawToPcm(alawByte);
                        outputWriter.Write(pcm);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Converts A-Law byte to 16-bit PCM sample.
        /// </summary>
        private static short ALawToPcm(byte alaw)
        {
            alaw ^= 0x55; // Invert even bits

            int sign = alaw & 0x80;
            int exponent = (alaw >> 4) & 0x07;
            int mantissa = alaw & 0x0F;

            int sample = mantissa << 4;
            if (exponent > 0)
            {
                sample = (sample + 256) << (exponent - 1);
            }
            else
            {
                sample = (sample + 256) >> 1;
            }

            if (sign != 0)
            {
                sample = -sample;
            }

            return (short)Math.Max(-32768, Math.Min(32767, sample));
        }

        /// <summary>
        /// Decodes μ-Law to 16-bit PCM.
        /// </summary>
        private static byte[] DecodeMuLaw(WAV wav)
        {
            int samples = wav.Data.Length;
            byte[] output = new byte[samples * wav.Channels * 2]; // 16-bit output

            using (var inputStream = new MemoryStream(wav.Data))
            using (var outputStream = new MemoryStream(output))
            using (var inputReader = new BinaryReader(inputStream))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                for (int i = 0; i < samples; i++)
                {
                    for (int ch = 0; ch < wav.Channels; ch++)
                    {
                        byte mulawByte = inputReader.ReadByte();
                        short pcm = MuLawToPcm(mulawByte);
                        outputWriter.Write(pcm);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Converts μ-Law byte to 16-bit PCM sample.
        /// </summary>
        private static short MuLawToPcm(byte mulaw)
        {
            mulaw = (byte)~mulaw; // Invert all bits

            int sign = mulaw & 0x80;
            int exponent = (mulaw >> 4) & 0x07;
            int mantissa = mulaw & 0x0F;

            int sample = ((mantissa << 3) + 33) << exponent;
            sample -= 33;

            if (sign != 0)
            {
                sample = -sample;
            }

            return (short)Math.Max(-32768, Math.Min(32767, sample));
        }

        /// <summary>
        /// Creates a standard RIFF/WAVE file structure with PCM data.
        /// </summary>
        private static byte[] CreateRiffWaveFile(byte[] pcmData, int channels, int sampleRate, int bitsPerSample, int blockAlign, uint bytesPerSec)
        {
            if (pcmData == null || pcmData.Length == 0)
            {
                return null;
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // RIFF header
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                uint fileSize = (uint)(4 + 8 + 16 + 8 + pcmData.Length); // WAVE (4) + fmt chunk (8+16) + data chunk (8) + data
                writer.Write(fileSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                // fmt chunk
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write((uint)16); // fmt chunk size (16 bytes for PCM)
                writer.Write((ushort)1); // PCM format (0x0001)
                writer.Write((ushort)channels);
                writer.Write((uint)sampleRate);
                writer.Write(bytesPerSec);
                writer.Write((ushort)blockAlign);
                writer.Write((ushort)bitsPerSample);

                // data chunk
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write((uint)pcmData.Length);
                writer.Write(pcmData);

                return stream.ToArray();
            }
        }
    }
}

