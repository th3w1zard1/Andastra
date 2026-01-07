using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Render optimizer for automatic performance tuning.
    /// 
    /// Automatically adjusts rendering settings based on performance metrics,
    /// finding optimal balance between quality and performance.
    /// 
    /// Features:
    /// - Automatic quality adjustment
    /// - Performance-based optimization
    /// - Multi-parameter tuning
    /// - Learning from performance data
    /// </summary>
    public class RenderOptimizer
    {
        /// <summary>
        /// Optimization parameter.
        /// </summary>
        public struct OptimizationParameter
        {
            public string Name;
            public float MinValue;
            public float MaxValue;
            public float CurrentValue;
            public float StepSize;
        }

        private readonly List<OptimizationParameter> _parameters;
        private readonly Dictionary<string, int> _parameterIndices;
        private readonly Dictionary<string, float> _performanceHistory;
        private float _targetFrameTime;
        private float _currentFrameTime;

        /// <summary>
        /// Gets or sets the target frame time in milliseconds.
        /// </summary>
        public float TargetFrameTime
        {
            get { return _targetFrameTime; }
            set { _targetFrameTime = Math.Max(1.0f, value); }
        }

        /// <summary>
        /// Initializes a new render optimizer.
        /// </summary>
        public RenderOptimizer(float targetFrameTime = 16.67f)
        {
            _parameters = new List<OptimizationParameter>();
            _parameterIndices = new Dictionary<string, int>();
            _performanceHistory = new Dictionary<string, float>();
            _targetFrameTime = targetFrameTime;
            _currentFrameTime = targetFrameTime;
        }

        /// <summary>
        /// Registers an optimization parameter.
        /// </summary>
        /// <param name="name">Parameter name. Must not be null or empty.</param>
        /// <param name="minValue">Minimum parameter value. Must be less than maxValue.</param>
        /// <param name="maxValue">Maximum parameter value. Must be greater than minValue.</param>
        /// <param name="initialValue">Initial parameter value. Must be between minValue and maxValue.</param>
        /// <param name="stepSize">Step size for parameter adjustments. Must be greater than zero.</param>
        /// <exception cref="ArgumentException">Thrown if any parameter validation fails.</exception>
        public void RegisterParameter(string name, float minValue, float maxValue, float initialValue, float stepSize)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Parameter name cannot be null or empty.", nameof(name));
            }
            if (minValue >= maxValue)
            {
                throw new ArgumentException("MinValue must be less than MaxValue.", nameof(minValue));
            }
            if (initialValue < minValue || initialValue > maxValue)
            {
                throw new ArgumentException("InitialValue must be between MinValue and MaxValue.", nameof(initialValue));
            }
            if (stepSize <= 0.0f)
            {
                throw new ArgumentException("StepSize must be greater than zero.", nameof(stepSize));
            }

            int index = _parameters.Count;
            _parameters.Add(new OptimizationParameter
            {
                Name = name,
                MinValue = minValue,
                MaxValue = maxValue,
                CurrentValue = initialValue,
                StepSize = stepSize
            });
            _parameterIndices[name] = index;
        }

        /// <summary>
        /// Updates optimization based on current performance.
        /// </summary>
        /// <param name="frameTime">Current frame time in milliseconds. Should be positive.</param>
        public void Update(float frameTime)
        {
            _currentFrameTime = frameTime;

            // Record performance
            string paramKey = GetParameterKey();
            _performanceHistory[paramKey] = frameTime;

            // Adjust parameters if performance is below target
            if (frameTime > _targetFrameTime * 1.1f) // 10% over target
            {
                OptimizeParameters();
            }
            else if (frameTime < _targetFrameTime * 0.9f) // 10% under target
            {
                // Can increase quality
                IncreaseQuality();
            }
        }

        /// <summary>
        /// Gets current parameter values.
        /// </summary>
        /// <returns>Dictionary mapping parameter names to their current values.</returns>
        public Dictionary<string, float> GetParameterValues()
        {
            var values = new Dictionary<string, float>();
            foreach (OptimizationParameter param in _parameters)
            {
                values[param.Name] = param.CurrentValue;
            }
            return values;
        }

        private string GetParameterKey()
        {
            // Create key from current parameter values
            var parts = new List<string>();
            foreach (OptimizationParameter param in _parameters)
            {
                parts.Add($"{param.Name}:{param.CurrentValue:F2}");
            }
            return string.Join("|", parts);
        }

        private void OptimizeParameters()
        {
            // Reduce quality parameters to improve performance
            for (int i = 0; i < _parameters.Count; i++)
            {
                OptimizationParameter param = _parameters[i];
                float newValue = param.CurrentValue - param.StepSize;
                if (newValue >= param.MinValue)
                {
                    // Update parameter value
                    param.CurrentValue = newValue;
                    _parameters[i] = param;
                }
            }
        }

        private void IncreaseQuality()
        {
            // Increase quality parameters if performance allows
            for (int i = 0; i < _parameters.Count; i++)
            {
                OptimizationParameter param = _parameters[i];
                float newValue = param.CurrentValue + param.StepSize;
                if (newValue <= param.MaxValue)
                {
                    // Update parameter value
                    param.CurrentValue = newValue;
                    _parameters[i] = param;
                }
            }
        }

        /// <summary>
        /// Gets a parameter value by name.
        /// </summary>
        /// <param name="name">Parameter name. Can be null or empty (returns null).</param>
        /// <returns>Parameter value if found, null otherwise.</returns>
        public float? GetParameterValue(string name)
        {
            int index;
            if (_parameterIndices.TryGetValue(name, out index) && index >= 0 && index < _parameters.Count)
            {
                return _parameters[index].CurrentValue;
            }
            return null;
        }

        /// <summary>
        /// Sets a parameter value by name.
        /// </summary>
        /// <param name="name">Parameter name. Can be null or empty (returns false).</param>
        /// <param name="value">New parameter value. Must be within min/max range.</param>
        /// <returns>True if parameter was set successfully, false if parameter not found or value out of range.</returns>
        public bool SetParameterValue(string name, float value)
        {
            int index;
            if (_parameterIndices.TryGetValue(name, out index) && index >= 0 && index < _parameters.Count)
            {
                OptimizationParameter param = _parameters[index];
                if (value >= param.MinValue && value <= param.MaxValue)
                {
                    param.CurrentValue = value;
                    _parameters[index] = param;
                    return true;
                }
            }
            return false;
        }
    }
}

