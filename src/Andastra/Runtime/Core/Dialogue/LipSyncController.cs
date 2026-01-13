using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Dialogue
{
    /// <summary>
    /// Controls lip sync animation during dialogue.
    /// Interpolates between LIP keyframes to drive facial animation.
    /// </summary>
    /// <remarks>
    /// Lip Sync Controller:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) lip sync system
    /// - Located via string references: "LIPS:localization" @ 0x007be654, "LIPS:%s_loc" @ 0x007be668 (LIP file path format)
    /// - LIP directories: ".\lips" @ 0x007c6838, "d:\lips" @ 0x007c6840 (LIP file search directories)
    /// - "=Lip Delay" @ 0x007c7fb7 (lip sync timing offset parameter)
    /// - Original implementation: LIP files contain keyframes with time (float, seconds) and phoneme shape index (byte, 0-9)
    /// - KOTOR uses approximately 10 phoneme shapes for mouth/face animation (PhonemeShapeNames array)
    /// - The controller interpolates between keyframes to produce smooth animation transitions
    /// - LIP files are loaded by ResRef matching dialogue VO files (voice-over WAV files)
    /// - LIP file naming: "{dialogueResRef}_{entryIndex}.lip" or "{VOFileName}.lip" (stored in .\lips or d:\lips directories)
    /// - Phoneme shapes drive blend shapes or bone rotations on character models (facial animation)
    /// - Lip delay: Timing offset for lip sync synchronization with voice-over audio playback
    /// - Duration: LIP files store total duration, controller stops when playback time exceeds duration
    /// - Keyframe interpolation: Linear interpolation between adjacent keyframes based on current playback time
    /// - Based on LIP file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class LipSyncController : ILipSyncController
    {
        private readonly ILipDataLoader _lipLoader;
        private LipSyncData _currentLip;
        private IEntity _speaker;
        private float _time;
        private bool _isActive;

        /// <summary>
        /// KOTOR phoneme shape names (approximately 10 shapes).
        /// These map to blend shapes or bone rotations on the character model.
        /// </summary>
        public static readonly string[] PhonemeShapeNames = new string[]
        {
            "ee",      // 0 - ee/i sound (smile)
            "eh",      // 1 - eh/e sound
            "schwa",   // 2 - schwa/unstressed vowel
            "ah",      // 3 - ah/a sound (open)
            "oh",      // 4 - oh/o sound (round)
            "oo",      // 5 - oo/u sound (pucker)
            "y",       // 6 - y/consonant
            "s",       // 7 - s/z/sh sounds
            "f_v",     // 8 - f/v sounds (lip bite)
            "rest"     // 9 - neutral/closed mouth
        };

        /// <summary>
        /// Gets whether lip sync is currently active.
        /// </summary>
        public bool IsActive { get { return _isActive; } }

        /// <summary>
        /// Gets the current playback time.
        /// </summary>
        public float CurrentTime { get { return _time; } }

        /// <summary>
        /// Gets the current lip sync data.
        /// </summary>
        public LipSyncData CurrentData { get { return _currentLip; } }

        /// <summary>
        /// Event fired when a phoneme shape should be applied.
        /// </summary>
        public event Action<IEntity, int, float> OnPhonemeShape;

        public LipSyncController(ILipDataLoader lipLoader)
        {
            _lipLoader = lipLoader ?? throw new ArgumentNullException("lipLoader");
            _isActive = false;
        }

        /// <summary>
        /// Starts lip sync for a speaker.
        /// </summary>
        public void Start(IEntity speaker, string lipResRef)
        {
            if (speaker == null)
            {
                return;
            }

            Stop();

            _speaker = speaker;

            // Load LIP data
            if (_lipLoader != null && !string.IsNullOrEmpty(lipResRef))
            {
                _currentLip = _lipLoader.LoadLipData(lipResRef);
            }

            if (_currentLip == null || _currentLip.KeyframeCount == 0)
            {
                return;
            }

            _time = 0f;
            _isActive = true;
        }

        /// <summary>
        /// Stops lip sync animation.
        /// </summary>
        public void Stop()
        {
            if (_isActive && _speaker != null)
            {
                // Reset to neutral shape
                ApplyPhoneme(PhonemeShapeNames.Length - 1, 1.0f);
            }

            _isActive = false;
            _currentLip = null;
            _speaker = null;
            _time = 0f;
        }

        /// <summary>
        /// Updates lip sync animation.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isActive || _currentLip == null || _speaker == null)
            {
                return;
            }

            _time += deltaTime;

            // Check if past end of lip sync
            if (_time >= _currentLip.Duration)
            {
                Stop();
                return;
            }

            // Find current keyframe pair
            int keyframeIndex = FindKeyframeIndex(_time);
            if (keyframeIndex < 0)
            {
                return;
            }

            LipKeyframe current = _currentLip.GetKeyframe(keyframeIndex);
            LipKeyframe next = _currentLip.GetKeyframe(keyframeIndex + 1);

            if (next == null)
            {
                // Last keyframe - use current shape
                ApplyPhoneme(current.Shape, 1.0f);
            }
            else
            {
                // Interpolate between keyframes
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Lip sync interpolation implementation
                // Located via string references: "LIPS:localization" @ 0x007be654, ".\lips" @ 0x007c6838
                // Original implementation: Linear interpolation between keyframes for smooth animation
                // Phoneme shapes blended with weights based on time position between keyframes
                float t = (_time - current.Time) / (next.Time - current.Time);
                t = Math.Max(0f, Math.Min(1f, t));

                // Apply both shapes with blend weights
                // Original engine: Blends between current and next phoneme shapes
                ApplyPhoneme(current.Shape, 1f - t);
                ApplyPhoneme(next.Shape, t);
            }
        }

        /// <summary>
        /// Finds the keyframe index for a given time.
        /// </summary>
        private int FindKeyframeIndex(float time)
        {
            if (_currentLip == null)
            {
                return -1;
            }

            for (int i = 0; i < _currentLip.KeyframeCount - 1; i++)
            {
                LipKeyframe current = _currentLip.GetKeyframe(i);
                LipKeyframe next = _currentLip.GetKeyframe(i + 1);

                if (time >= current.Time && time < next.Time)
                {
                    return i;
                }
            }

            // Return last keyframe if past end
            if (_currentLip.KeyframeCount > 0)
            {
                return _currentLip.KeyframeCount - 1;
            }

            return -1;
        }

        /// <summary>
        /// Applies a phoneme shape to the speaker.
        /// </summary>
        private void ApplyPhoneme(int shapeIndex, float weight)
        {
            if (_speaker == null)
            {
                return;
            }

            // Clamp shape index
            shapeIndex = Math.Max(0, Math.Min(PhonemeShapeNames.Length - 1, shapeIndex));

            // Fire event for rendering system to apply the blend shape
            if (OnPhonemeShape != null)
            {
                OnPhonemeShape(_speaker, shapeIndex, weight);
            }
        }
    }

    /// <summary>
    /// Interface for loading LIP data.
    /// </summary>
    public interface ILipDataLoader
    {
        /// <summary>
        /// Loads lip sync data from a resource reference.
        /// </summary>
        LipSyncData LoadLipData(string resRef);
    }

    /// <summary>
    /// Runtime representation of LIP file data.
    /// </summary>
    public class LipSyncData
    {
        private readonly List<LipKeyframe> _keyframes;

        /// <summary>
        /// Total duration of the lip sync animation in seconds.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// Number of keyframes.
        /// </summary>
        public int KeyframeCount { get { return _keyframes.Count; } }

        public LipSyncData()
        {
            _keyframes = new List<LipKeyframe>();
        }

        /// <summary>
        /// Adds a keyframe.
        /// </summary>
        public void AddKeyframe(float time, int shape)
        {
            _keyframes.Add(new LipKeyframe(time, shape));
        }

        /// <summary>
        /// Gets a keyframe by index.
        /// </summary>
        public LipKeyframe GetKeyframe(int index)
        {
            if (index < 0 || index >= _keyframes.Count)
            {
                return null;
            }
            return _keyframes[index];
        }
    }

    /// <summary>
    /// A keyframe in lip sync data.
    /// </summary>
    public class LipKeyframe
    {
        /// <summary>
        /// Time in seconds.
        /// </summary>
        public float Time { get; }

        /// <summary>
        /// Phoneme shape index (0-9).
        /// </summary>
        public int Shape { get; }

        public LipKeyframe(float time, int shape)
        {
            Time = time;
            Shape = shape;
        }
    }
}
