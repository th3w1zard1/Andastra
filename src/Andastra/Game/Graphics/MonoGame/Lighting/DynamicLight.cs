using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;

namespace Andastra.Game.Graphics.MonoGame.Lighting
{
    /// <summary>
    /// Dynamic light source implementation.
    /// Supports point, spot, directional, and area lights.
    /// </summary>
    public class DynamicLight : IDynamicLight
    {
        private static uint _nextLightId = 1;
        private bool _disposed;
        private bool _dirty = true;

        public uint LightId { get; private set; }
        public LightType Type { get; private set; }

        private bool _enabled = true;
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; _dirty = true; }
        }

        private Vector3 _position;
        public Vector3 Position
        {
            get { return _position; }
            set { _position = value; _dirty = true; }
        }

        private Vector3 _direction = -Vector3.UnitY;
        public Vector3 Direction
        {
            get { return _direction; }
            set { _direction = Vector3.Normalize(value); _dirty = true; }
        }

        private Vector3 _color = Vector3.One;
        public Vector3 Color
        {
            get { return _color; }
            set { _color = value; _dirty = true; }
        }

        private float _intensity = 1.0f;
        public float Intensity
        {
            get { return _intensity; }
            set { _intensity = Math.Max(0, value); _dirty = true; }
        }

        private float _radius = 10.0f;
        public float Radius
        {
            get { return _radius; }
            set { _radius = Math.Max(0.01f, value); _dirty = true; }
        }

        private float _innerConeAngle = 30f;
        public float InnerConeAngle
        {
            get { return _innerConeAngle; }
            set { _innerConeAngle = Math.Max(0, Math.Min(value, _outerConeAngle)); _dirty = true; }
        }

        private float _outerConeAngle = 45f;
        public float OuterConeAngle
        {
            get { return _outerConeAngle; }
            set { _outerConeAngle = Math.Max(_innerConeAngle, Math.Min(value, 180f)); _dirty = true; }
        }

        private float _areaWidth = 1.0f;
        public float AreaWidth
        {
            get { return _areaWidth; }
            set { _areaWidth = Math.Max(0.01f, value); _dirty = true; }
        }

        private float _areaHeight = 1.0f;
        public float AreaHeight
        {
            get { return _areaHeight; }
            set { _areaHeight = Math.Max(0.01f, value); _dirty = true; }
        }

        public bool CastShadows { get; set; } = true;
        public int ShadowResolution { get; set; } = 1024;
        public float ShadowBias { get; set; } = 0.0001f;
        public float ShadowNormalBias { get; set; } = 0.02f;
        public float ShadowNearPlane { get; set; } = 0.1f;
        public float ShadowSoftness { get; set; } = 1.0f;
        public bool RaytracedShadows { get; set; } = false;

        public bool Volumetric { get; set; } = false;
        public float VolumetricIntensity { get; set; } = 1.0f;

        private float _temperature = 6500f;
        public float Temperature
        {
            get { return _temperature; }
            set { _temperature = Math.Max(1000, Math.Min(value, 15000)); _dirty = true; }
        }

        public bool UseTemperature { get; set; } = false;

        public IntPtr IesProfile { get; set; }
        public IntPtr CookieTexture { get; set; }

        public bool AffectsSpecular { get; set; } = true;
        public uint CullingMask { get; set; } = uint.MaxValue;

        // Shadow map matrices (for directional lights)
        // View matrix: transforms world space to light space
        // Projection matrix: orthographic projection for directional lights
        // Combined: lightSpaceMatrix = projection * view (used for shadow mapping)
        public Matrix4x4 ShadowViewMatrix { get; set; } = Matrix4x4.Identity;
        public Matrix4x4 ShadowProjectionMatrix { get; set; } = Matrix4x4.Identity;
        public Matrix4x4 ShadowLightSpaceMatrix { get; set; } = Matrix4x4.Identity;

        public DynamicLight(LightType type)
        {
            LightId = _nextLightId++;
            Type = type;
        }

        public void UpdateTransform(Matrix4x4 worldMatrix)
        {
            // Extract position from matrix
            _position = new Vector3(worldMatrix.M41, worldMatrix.M42, worldMatrix.M43);

            // Extract forward direction (-Z in world space)
            _direction = -Vector3.Normalize(new Vector3(worldMatrix.M31, worldMatrix.M32, worldMatrix.M33));

            _dirty = true;
        }

        public void UpdateGpuData()
        {
            if (!_dirty || _disposed)
            {
                return;
            }

            // Calculate final color
            Vector3 finalColor = _color;
            if (UseTemperature)
            {
                finalColor = TemperatureToRgb(_temperature);
            }

            // Update GPU light data buffer
            // This would write to a structured buffer for clustered/tiled rendering

            _dirty = false;
        }

        /// <summary>
        /// Converts color temperature in Kelvin to RGB color.
        /// Based on Tanner Helland's algorithm.
        /// </summary>
        public static Vector3 TemperatureToRgb(float temperature)
        {
            float temp = temperature / 100f;
            float r, g, b;

            // Red
            if (temp <= 66)
            {
                r = 255;
            }
            else
            {
                r = temp - 60;
                r = 329.698727446f * (float)Math.Pow(r, -0.1332047592);
                r = Math.Max(0, Math.Min(r, 255));
            }

            // Green
            if (temp <= 66)
            {
                g = temp;
                g = 99.4708025861f * (float)Math.Log(g) - 161.1195681661f;
            }
            else
            {
                g = temp - 60;
                g = 288.1221695283f * (float)Math.Pow(g, -0.0755148492);
            }
            g = Math.Max(0, Math.Min(g, 255));

            // Blue
            if (temp >= 66)
            {
                b = 255;
            }
            else if (temp <= 19)
            {
                b = 0;
            }
            else
            {
                b = temp - 10;
                b = 138.5177312231f * (float)Math.Log(b) - 305.0447927307f;
                b = Math.Max(0, Math.Min(b, 255));
            }

            return new Vector3(r / 255f, g / 255f, b / 255f);
        }

        /// <summary>
        /// Calculates light attenuation at a given distance.
        /// Uses physically-based inverse square falloff with radius clamp.
        /// </summary>
        public float CalculateAttenuation(float distance)
        {
            if (Type == LightType.Directional)
            {
                return 1.0f; // No attenuation for directional lights
            }

            if (distance >= _radius)
            {
                return 0f;
            }

            // Physically-based attenuation: 1 / (distance^2 + 1)
            // With smooth falloff near radius
            float d2 = distance * distance;
            float falloff = 1.0f / (d2 + 1.0f);

            // Smooth edge falloff
            float edge = 1.0f - (distance / _radius);
            edge = edge * edge;

            return falloff * edge;
        }

        /// <summary>
        /// Calculates spot light cone attenuation.
        /// </summary>
        public float CalculateSpotAttenuation(Vector3 lightToSurface)
        {
            if (Type != LightType.Spot)
            {
                return 1.0f;
            }

            float cosAngle = Vector3.Dot(Vector3.Normalize(lightToSurface), _direction);
            float cosOuter = (float)Math.Cos(_outerConeAngle * Math.PI / 180f);
            float cosInner = (float)Math.Cos(_innerConeAngle * Math.PI / 180f);

            // Smooth falloff between inner and outer cone
            float t = (cosAngle - cosOuter) / (cosInner - cosOuter);
            t = Math.Max(0, Math.Min(t, 1));

            return t * t;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Release any GPU resources

            _disposed = true;
        }
    }
}

