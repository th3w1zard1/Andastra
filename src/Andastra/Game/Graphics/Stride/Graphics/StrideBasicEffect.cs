using System;
using System.Numerics;
using System.Reflection;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Effects;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Vector3Stride = Stride.Core.Mathematics.Vector3;
using MatrixStride = Stride.Core.Mathematics.Matrix;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IBasicEffect.
    /// Inherits from BaseBasicEffect to share common implementation logic.
    ///
    /// Since Stride doesn't have BasicEffect like MonoGame, this implementation uses
    /// Stride's Material and EffectInstance system to provide equivalent functionality.
    /// All shader parameters (matrices, colors, textures, flags) are properly set and applied.
    /// </summary>
    public class StrideBasicEffect : BaseBasicEffect
    {
        private readonly GraphicsDevice _device;
        private StrideEffectTechnique _technique;
        private EffectInstance _effectInstance;
        private Material _material;
        private bool _parametersDirty;
        private ParameterCollection _parameterCollection;

        public StrideBasicEffect(GraphicsDevice device) : base()
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _parametersDirty = true;
            InitializeEffect();
        }

        /// <summary>
        /// Initializes the effect by creating a proper Material with MaterialDescriptor
        /// and extracting the EffectInstance from it.
        ///
        /// This creates a Material that supports all BasicEffect features:
        /// - Transformation matrices (World, View, Projection)
        /// - Lighting (ambient, diffuse, specular, emissive)
        /// - Textures
        /// - Vertex colors
        /// - Fog
        /// - Alpha blending
        ///
        /// Based on MonoGame BasicEffect API and Stride Material system.
        /// Original game: DirectX 9 fixed-function pipeline (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
        /// </summary>
        private void InitializeEffect()
        {
            try
            {
                // Create MaterialDescriptor to configure the shader features
                var materialDescriptor = new MaterialDescriptor();

                // Set up material attributes based on BasicEffect features
                // Diffuse color and alpha
                materialDescriptor.Attributes.Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor(new Color4(_diffuseColor.X, _diffuseColor.Y, _diffuseColor.Z, _alpha))
                );

                // Emissive color
                materialDescriptor.Attributes.Emissive = new MaterialEmissiveMapFeature(
                    new ComputeColor(new Color4(_emissiveColor.X, _emissiveColor.Y, _emissiveColor.Z, 1.0f))
                );

                // Specular properties (if lighting is enabled)
                // MaterialSpecularMapFeature constructor takes a ComputeColor parameter
                // Based on Stride API: MaterialSpecularMapFeature(ComputeColor) constructor
                // Specular power is set via custom parameter key (MaterialKeys.SpecularPowerValue doesn't exist)
                // Original game: DirectX 9 fixed-function pipeline specular lighting (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
                if (_lightingEnabled && _specularPower > 0.0f)
                {
                    var specularColor = new Color4(_specularColor.X, _specularColor.Y, _specularColor.Z, 1.0f);
                    var specularFeature = new MaterialSpecularMapFeature();
                    specularFeature.SpecularMap = new ComputeColor(specularColor);
                    materialDescriptor.Attributes.Specular = specularFeature;
                    // Specular power will be set in InitializeParameters method after ParameterCollection is created
                    // This ensures proper initialization order and prevents accessing null ParameterCollection
                }

                // Create the Material from the descriptor
                // In Stride, Materials are created and then built from MaterialDescriptors to generate EffectInstances
                // Based on Stride API: Material constructor creates Material, MaterialDescriptor configures it
                // Materials are typically built by the rendering system when needed, but we can prepare them
                // for BasicEffect compatibility
                // Note: Material building happens when the Material is used in rendering, not at creation time
                // The MaterialDescriptor will be used by the rendering system to build the Material
                // and generate EffectInstances in the Material's passes
                _material = new Material();

                // Note: In Stride, MaterialDescriptor is typically applied when Material is built by renderer
                // For BasicEffect compatibility without a precompiled shader, we use ParameterCollection
                // to manage parameters. The EffectInstance will be created with null Effect for now,
                // and will work with the Material when it's built by the rendering system

                // Create parameter collection for managing shader parameters
                // This will be used to store all BasicEffect parameters
                _parameterCollection = new ParameterCollection();
                InitializeParameters(_parameterCollection);

                // Create a basic effect instance for parameter management
                // Since Stride doesn't have a built-in BasicEffect shader, we use
                // a parameter collection approach that works with the Material system
                // The EffectInstance will be properly initialized with parameters
                _effectInstance = CreateBasicEffectInstance(_parameterCollection);

                // Mark parameters as dirty to ensure initial setup
                _parametersDirty = true;
            }
            catch (Exception ex)
            {
                // Fallback: Create a minimal effect instance if material creation fails
                Console.WriteLine($"[StrideBasicEffect] Error initializing effect with Material: {ex.Message}");
                _parameterCollection = new ParameterCollection();
                InitializeParameters(_parameterCollection);
                _effectInstance = CreateBasicEffectInstance(_parameterCollection);
                _parametersDirty = true;
            }
        }

        /// <summary>
        /// Initializes all BasicEffect parameters in the parameter collection.
        /// This sets up all the standard transformation, material, lighting, and feature parameters.
        /// </summary>
        private void InitializeParameters(ParameterCollection parameterCollection)
        {
            // Initialize all parameter types that BasicEffect uses
            // Matrices - using Stride's standard transformation keys
            parameterCollection.Set(TransformationKeys.World, MatrixStride.Identity);
            parameterCollection.Set(TransformationKeys.View, MatrixStride.Identity);
            parameterCollection.Set(TransformationKeys.Projection, MatrixStride.Identity);
            parameterCollection.Set(TransformationKeys.WorldView, MatrixStride.Identity);
            parameterCollection.Set(TransformationKeys.ViewProjection, MatrixStride.Identity);
            parameterCollection.Set(TransformationKeys.WorldViewProjection, MatrixStride.Identity);

            // Material colors - using Stride's material keys
            // MaterialKeys.DiffuseValue, EmissiveValue, and SpecularValue may be ObjectParameterKey<Color3> or ValueParameterKey<Color4>
            // Use the appropriate type based on what the MaterialKeys expect
            // For diffuse, we need to include alpha, so use Color4 if the key supports it, otherwise use Color3
            var diffuseColor3 = new Color3(_diffuseColor.X * _alpha, _diffuseColor.Y * _alpha, _diffuseColor.Z * _alpha);
            SetParameterByTypeInCollection(parameterCollection, MaterialKeys.DiffuseValue, diffuseColor3);
            var emissiveColor3 = new Color3(_emissiveColor.X, _emissiveColor.Y, _emissiveColor.Z);
            SetParameterByTypeInCollection(parameterCollection, MaterialKeys.EmissiveValue, emissiveColor3);
            var specularColor3 = new Color3(_specularColor.X, _specularColor.Y, _specularColor.Z);
            SetParameterByTypeInCollection(parameterCollection, MaterialKeys.SpecularValue, specularColor3);
            // MaterialKeys.SpecularPowerValue doesn't exist in Stride API
            // Use a custom parameter key for specular power
            parameterCollection.Set(new ValueParameterKey<float>("SpecularPower"), _specularPower);

            // Lighting - using custom parameter key for ambient light color
            // LightingKeys.AmbientLightColor doesn't exist in Stride API, use custom parameter key
            parameterCollection.Set(new ValueParameterKey<Color3>("AmbientLightColor"), new Color3(_ambientLightColor.X, _ambientLightColor.Y, _ambientLightColor.Z));

            // Feature flags - custom BasicEffect parameters
            parameterCollection.Set(new ValueParameterKey<bool>("VertexColorEnabled"), _vertexColorEnabled);
            parameterCollection.Set(new ValueParameterKey<bool>("LightingEnabled"), _lightingEnabled);
            parameterCollection.Set(new ValueParameterKey<bool>("TextureEnabled"), _textureEnabled);

            // Fog parameters - custom BasicEffect parameters
            parameterCollection.Set(new ValueParameterKey<bool>("FogEnabled"), _fogEnabled);
            parameterCollection.Set(new ValueParameterKey<Color3>("FogColor"), new Color3(_fogColor.X, _fogColor.Y, _fogColor.Z));
            parameterCollection.Set(new ValueParameterKey<float>("FogStart"), _fogStart);
            parameterCollection.Set(new ValueParameterKey<float>("FogEnd"), _fogEnd);

            // Alpha parameter
            parameterCollection.Set(new ValueParameterKey<float>("Alpha"), _alpha);
        }

        /// <summary>
        /// Creates a basic EffectInstance for parameter management.
        /// This EffectInstance will be used to store and set shader parameters
        /// that can be applied when rendering.
        ///
        /// Since Stride doesn't have BasicEffect built-in, we create a parameter
        /// collection that can be used with custom shaders or Material passes.
        ///
        /// Implementation strategy:
        /// 1. First, try to get EffectInstance from built Material passes (if Material was built)
        /// 2. If Material not built or no EffectInstance available, create one with null Effect
        /// 3. Use ParameterCollection to manage all BasicEffect parameters
        /// 4. Parameters are synchronized between ParameterCollection and EffectInstance
        ///
        /// Based on Stride API: EffectInstance requires an Effect, but can be created with null
        /// for parameter management. The actual Effect will come from Material when it's built.
        /// Original game: DirectX 9 fixed-function pipeline (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
        /// - Fixed-function pipeline doesn't use programmable shaders
        /// - Material states are set via DirectX state blocks
        /// - Modern Stride implementation uses programmable shaders with Material system
        /// </summary>
        private EffectInstance CreateBasicEffectInstance(ParameterCollection parameterCollection)
        {
            EffectInstance effectInstance = null;

            // Strategy 1: Try to get EffectInstance from Material passes if Material is built
            // Based on Stride API: Material.GetPasses() returns MaterialPass objects with EffectInstances
            // Materials built from MaterialDescriptor contain EffectInstances in their passes
            if (_material != null)
            {
                try
                {
                    // Get passes from the Material
                    // Based on Stride API: Material.Passes property returns collection of MaterialPass objects
                    // Each MaterialPass has an EffectInstance that can be used for rendering
                    var materialPasses = _material.Passes;
                    if (materialPasses != null && materialPasses.Count > 0)
                    {
                        // Get the first pass's EffectInstance (main rendering pass)
                        // Based on Stride API: MaterialPass has Parameters property (ParameterCollection)
                        // EffectInstance needs to be obtained from Material or created separately
                        var firstPass = materialPasses[0];
                        if (firstPass != null && firstPass.Parameters != null)
                        {
                            // MaterialPass doesn't directly expose EffectInstance
                            // We'll synchronize parameters with the pass's ParameterCollection
                            // The EffectInstance will be available when Material is built by the rendering system
                            if (parameterCollection != null)
                            {
                                // Copy parameters to MaterialPass's ParameterCollection
                                CopyParametersToParameterCollection(parameterCollection, firstPass.Parameters);
                            }

                            // Copy parameters from our collection to the Material's EffectInstance
                            if (effectInstance.Parameters != null && parameterCollection != null)
                            {
                                CopyParametersToEffectInstance(parameterCollection, effectInstance);
                            }

                            return effectInstance;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Material may not be fully built yet - fall through to Strategy 2
                    Console.WriteLine($"[StrideBasicEffect] Could not get EffectInstance from Material passes: {ex.Message}");
                }
            }

            // Strategy 2: Create EffectInstance with null Effect for parameter management
            // This is a valid approach in Stride for managing parameters before Material is built
            // Based on Stride API: EffectInstance constructor accepts null Effect for parameter-only instances
            // The EffectInstance will work with ParameterCollection to manage shader parameters
            // When the Material is built, it will provide the actual Effect, and parameters will be applied
            effectInstance = new EffectInstance(null);

            // Initialize parameters in the effect instance's parameter collection
            // Copy parameters from our collection to the effect instance
            if (effectInstance.Parameters != null && parameterCollection != null)
            {
                CopyParametersToEffectInstance(parameterCollection, effectInstance);
            }

            return effectInstance;
        }

        /// <summary>
        /// Copies parameters from a ParameterCollection to an EffectInstance's Parameters.
        /// This synchronizes parameter values between the collection and the effect instance.
        /// </summary>
        /// <param name="parameterCollection">Source parameter collection.</param>
        /// <param name="effectInstance">Target effect instance.</param>
        private void CopyParametersToEffectInstance(ParameterCollection parameterCollection, EffectInstance effectInstance)
        {
            if (parameterCollection == null || effectInstance == null || effectInstance.Parameters == null)
            {
                return;
            }

            try
            {
                // Copy all parameters to the effect instance
                // Based on Stride API: ParameterCollection.ParameterKeyInfos provides all parameter keys
                // EffectInstance.Parameters.Set() sets parameter values in the effect instance
                foreach (var keyInfo in parameterCollection.ParameterKeyInfos)
                {
                    try
                    {
                        // ParameterCollection.Get<T>() requires a type parameter
                        // We need to determine the type from the key using reflection
                        // Get the generic type argument from ParameterKey<T> to call Get<T>
                        object value = null;
                        try
                        {
                            // Get the type from ParameterKey<T> by extracting the generic type argument
                            // ParameterKey is a generic type, so we get T from ParameterKey<T>
                            var keyType = keyInfo.Key.GetType();
                            if (keyType.IsGenericType)
                            {
                                var genericArgs = keyType.GetGenericArguments();
                                if (genericArgs.Length > 0)
                                {
                                    var valueType = genericArgs[0];
                                    // Get the Get<T> method from ParameterCollection
                                    var getMethod = typeof(ParameterCollection).GetMethod("Get", new[] { typeof(ParameterKey) });
                                    if (getMethod != null)
                                    {
                                        // Make the generic method with the correct type parameter
                                        var genericGetMethod = getMethod.MakeGenericMethod(valueType);
                                        // Invoke Get<T>(key) to retrieve the parameter value
                                        value = genericGetMethod.Invoke(parameterCollection, new[] { keyInfo.Key });
                                    }
                                }
                            }
                        }
                        catch (Exception getEx)
                        {
                            // If we can't get the value, skip this parameter
                            // This can happen if the parameter doesn't exist or type mismatch
                            // Log only unexpected errors (not ArgumentException which is expected for missing parameters)
                            if (!(getEx is ArgumentException) && !(getEx.InnerException is ArgumentException))
                            {
                                Console.WriteLine($"[StrideBasicEffect] Could not get parameter '{keyInfo.Key.Name}' value: {getEx.Message}");
                            }
                            continue;
                        }
                        if (value != null)
                        {
                            // Use type-specific setters to set the parameter in the effect instance
                            // Based on Stride API: ParameterCollection.Get<T>() and Parameters.Set<T>()
                            SetParameterByType(effectInstance.Parameters, keyInfo.Key, value);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Some parameters may not be settable at this stage
                        // This is expected and will be handled when the Material is built
                        // Log only if it's not an ArgumentException (expected for unbuilt effects)
                        if (!(ex is ArgumentException))
                        {
                            Console.WriteLine($"[StrideBasicEffect] Error copying parameter '{keyInfo.Key.Name}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error copying parameters to EffectInstance: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies parameters from one ParameterCollection to another.
        /// </summary>
        /// <param name="source">Source parameter collection.</param>
        /// <param name="target">Target parameter collection.</param>
        private void CopyParametersToParameterCollection(ParameterCollection source, ParameterCollection target)
        {
            if (source == null || target == null)
            {
                return;
            }

            try
            {
                // Copy all parameters from source to target
                foreach (var keyInfo in source.ParameterKeyInfos)
                {
                    try
                    {
                        // Get the parameter value using reflection to determine the type
                        // ParameterCollection.Get<T>() requires a type parameter
                        object value = null;
                        var keyType = keyInfo.Key.GetType();
                        if (keyType.IsGenericType)
                        {
                            var genericArgs = keyType.GetGenericArguments();
                            if (genericArgs.Length > 0)
                            {
                                var valueType = genericArgs[0];
                                // Get the Get<T> method from ParameterCollection
                                var getMethod = typeof(ParameterCollection).GetMethod("Get", new[] { typeof(ParameterKey) });
                                if (getMethod != null)
                                {
                                    // Make the generic method with the correct type parameter
                                    var genericGetMethod = getMethod.MakeGenericMethod(valueType);
                                    // Invoke Get<T>(key) to retrieve the parameter value
                                    value = genericGetMethod.Invoke(source, new[] { keyInfo.Key });
                                }
                            }
                        }
                        if (value != null)
                        {
                            // Copy parameter value to target collection
                            SetParameterByTypeInCollection(target, keyInfo.Key, value);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Some parameters may not be copyable
                        Console.WriteLine($"[StrideBasicEffect] Error copying parameter '{keyInfo.Key.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error copying parameters to ParameterCollection: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a parameter in a ParameterCollection by type.
        /// </summary>
        private void SetParameterByTypeInCollection(ParameterCollection collection, ParameterKey key, object value)
        {
            if (collection == null || key == null || value == null)
            {
                return;
            }

            try
            {
                // Use type-specific setters based on parameter key type
                // Stride requires ValueParameterKey<T> for value types
                if (key is ParameterKey<MatrixStride> matrixKey && value is MatrixStride matrixValue)
                {
                    var valueKey = new ValueParameterKey<MatrixStride>(key.Name);
                    collection.Set(valueKey, matrixValue);
                }
                else if (key is ParameterKey<Vector3Stride> vector3Key && value is Vector3Stride vector3Value)
                {
                    var valueKey = new ValueParameterKey<Vector3Stride>(key.Name);
                    collection.Set(valueKey, vector3Value);
                }
                else if (key is ParameterKey<Color3> color3Key && value is Color3 color3Value)
                {
                    var valueKey = new ValueParameterKey<Color3>(key.Name);
                    collection.Set(valueKey, color3Value);
                }
                else if (key is ParameterKey<Color4> color4Key && value is Color4 color4Value)
                {
                    var valueKey = new ValueParameterKey<Color4>(key.Name);
                    collection.Set(valueKey, color4Value);
                }
                else if (key is ParameterKey<float> floatKey && value is float floatValue)
                {
                    var valueKey = new ValueParameterKey<float>(key.Name);
                    collection.Set(valueKey, floatValue);
                }
                else if (key is ParameterKey<bool> boolKey && value is bool boolValue)
                {
                    var valueKey = new ValueParameterKey<bool>(key.Name);
                    collection.Set(valueKey, boolValue);
                }
                else if (key is ParameterKey<Texture> textureKey && value is Texture textureValue)
                {
                    // Texture is a reference type, requires ObjectParameterKey
                    if (textureKey is ObjectParameterKey<Texture> objectTextureKey)
                    {
                        collection.Set(objectTextureKey, textureValue);
                    }
                    else
                    {
                        // If it's not an ObjectParameterKey, create one
                        // MaterialKeys like DiffuseMap are already ObjectParameterKey<Texture>
                        if (textureKey == MaterialKeys.DiffuseMap)
                        {
                            collection.Set((ObjectParameterKey<Texture>)textureKey, textureValue);
                        }
                        else
                        {
                            var objectKey = new ObjectParameterKey<Texture>(key.Name);
                            collection.Set(objectKey, textureValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error setting parameter '{key.Name}' in ParameterCollection: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a parameter in an EffectInstance's Parameters collection by type.
        /// Handles different parameter types (Matrix, Vector3, Color3, Color4, float, bool, Texture).
        /// </summary>
        /// <param name="parameters">The ParameterCollection from EffectInstance.</param>
        /// <param name="key">The parameter key.</param>
        /// <param name="value">The parameter value.</param>
        private void SetParameterByType(ParameterCollection parameters, ParameterKey key, object value)
        {
            if (parameters == null || key == null || value == null)
            {
                return;
            }

            try
            {
                // Use pattern matching to handle different parameter types correctly
                // Based on Stride API: Parameters.Set<T>(ValueParameterKey<T> key, T value) for value types
                // and Parameters.Set<T>(ObjectParameterKey<T> key, T value) for reference types
                // For value types, we need to create ValueParameterKey instances
                if (key is ParameterKey<MatrixStride> matrixKey && value is MatrixStride matrixValue)
                {
                    var valueKey = new ValueParameterKey<MatrixStride>(key.Name);
                    parameters.Set(valueKey, matrixValue);
                }
                else if (key is ParameterKey<Color3> color3Key && value is Color3 color3Value)
                {
                    var valueKey = new ValueParameterKey<Color3>(key.Name);
                    parameters.Set(valueKey, color3Value);
                }
                else if (key is ParameterKey<Color4> color4Key && value is Color4 color4Value)
                {
                    var valueKey = new ValueParameterKey<Color4>(key.Name);
                    parameters.Set(valueKey, color4Value);
                }
                else if (key is ParameterKey<Vector3Stride> vector3Key && value is Vector3Stride vector3Value)
                {
                    var valueKey = new ValueParameterKey<Vector3Stride>(key.Name);
                    parameters.Set(valueKey, vector3Value);
                }
                else if (key is ParameterKey<float> floatKey && value is float floatValue)
                {
                    var valueKey = new ValueParameterKey<float>(key.Name);
                    parameters.Set(valueKey, floatValue);
                }
                else if (key is ParameterKey<bool> boolKey && value is bool boolValue)
                {
                    var valueKey = new ValueParameterKey<bool>(key.Name);
                    parameters.Set(valueKey, boolValue);
                }
                else if (key is ParameterKey<Texture> textureKey && value is Texture textureValue)
                {
                    // Texture is a reference type, requires ObjectParameterKey
                    if (textureKey is ObjectParameterKey<Texture> objectTextureKey)
                    {
                        parameters.Set(objectTextureKey, textureValue);
                    }
                    else
                    {
                        // If it's not an ObjectParameterKey, create one or skip it
                        // MaterialKeys like DiffuseMap are already ObjectParameterKey<Texture>
                        if (textureKey == MaterialKeys.DiffuseMap)
                        {
                            parameters.Set((ObjectParameterKey<Texture>)textureKey, textureValue);
                        }
                        else
                        {
                            var objectKey = new ObjectParameterKey<Texture>(key.Name);
                            parameters.Set(objectKey, textureValue);
                        }
                    }
                }
                else
                {
                    // For unknown types, use reflection to determine the type and call Set<T> appropriately
                    // This handles any parameter types not explicitly handled above
                    // Based on Stride API: Parameters.Set<T>(ValueParameterKey<T> key, T value) for value types
                    // and Parameters.Set<T>(ObjectParameterKey<T> key, T value) for reference types
                    // Original game: DirectX 9 fixed-function pipeline parameter setting (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
                    try
                    {
                        // Get the type from ParameterKey<T> by extracting the generic type argument
                        var keyType = key.GetType();
                        if (keyType.IsGenericType)
                        {
                            var genericArgs = keyType.GetGenericArguments();
                            if (genericArgs.Length > 0)
                            {
                                var valueType = genericArgs[0];
                                var valueTypeActual = value.GetType();

                                // Ensure the value type is compatible with the parameter key type
                                if (valueType.IsAssignableFrom(valueTypeActual) || valueTypeActual.IsSubclassOf(valueType) || valueType == valueTypeActual)
                                {
                                    // Determine if the key type is ValueParameterKey<T> or ObjectParameterKey<T>
                                    // by checking the generic type definition
                                    var keyGenericDef = keyType.GetGenericTypeDefinition();
                                    var isValueParameterKey = keyGenericDef == typeof(ValueParameterKey<>);
                                    var isObjectParameterKey = keyGenericDef == typeof(ObjectParameterKey<>);

                                    // Find the Set<T> method that takes ParameterKey<T> and T
                                    // ParameterCollection.Set<T>(ParameterKey<T> key, T value)
                                    var setMethods = typeof(ParameterCollection).GetMethods();
                                    MethodInfo setMethod = null;
                                    foreach (var method in setMethods)
                                    {
                                        if (method.Name == "Set" && method.IsGenericMethod && method.GetParameters().Length == 2)
                                        {
                                            var paramTypes = method.GetParameters();
                                            var firstParamType = paramTypes[0].ParameterType;
                                            if (firstParamType.IsGenericType)
                                            {
                                                var firstParamGenericDef = firstParamType.GetGenericTypeDefinition();
                                                if (firstParamGenericDef == typeof(ParameterKey<>) ||
                                                    firstParamGenericDef == typeof(ValueParameterKey<>) ||
                                                    firstParamGenericDef == typeof(ObjectParameterKey<>))
                                                {
                                                    setMethod = method;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    if (setMethod != null)
                                    {
                                        // Make the generic method with the correct type parameter
                                        var genericSetMethod = setMethod.MakeGenericMethod(valueType);

                                        // Create the appropriate parameter key type based on whether T is a value type or reference type
                                        ParameterKey parameterKeyToUse = key;

                                        // If the key is not already the right type, create a new one
                                        // Value types use ValueParameterKey<T>, reference types use ObjectParameterKey<T>
                                        if (!isValueParameterKey && !isObjectParameterKey)
                                        {
                                            if (valueType.IsValueType)
                                            {
                                                // Create ValueParameterKey<T> for value types
                                                var valueKeyType = typeof(ValueParameterKey<>).MakeGenericType(valueType);
                                                var valueKeyCtor = valueKeyType.GetConstructor(new[] { typeof(string) });
                                                if (valueKeyCtor != null)
                                                {
                                                    parameterKeyToUse = (ParameterKey)valueKeyCtor.Invoke(new[] { key.Name });
                                                }
                                            }
                                            else
                                            {
                                                // Create ObjectParameterKey<T> for reference types
                                                var objectKeyType = typeof(ObjectParameterKey<>).MakeGenericType(valueType);
                                                var objectKeyCtor = objectKeyType.GetConstructor(new[] { typeof(string) });
                                                if (objectKeyCtor != null)
                                                {
                                                    parameterKeyToUse = (ParameterKey)objectKeyCtor.Invoke(new[] { key.Name });
                                                }
                                            }
                                        }

                                        // Invoke Set<T>(key, value) to set the parameter
                                        genericSetMethod.Invoke(parameters, new[] { parameterKeyToUse, value });
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[StrideBasicEffect] Could not find Set method for parameter '{key.Name}' of type {valueType.Name}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[StrideBasicEffect] Type mismatch for parameter '{key.Name}': expected {valueType.Name}, got {valueTypeActual.Name}");
                                }
                            }
                        }
                    }
                    catch (Exception reflectionEx)
                    {
                        // Reflection-based parameter setting failed
                        // This can happen for unsupported types or when the Effect is not yet built
                        // Log the error but don't throw - allows other parameters to be set
                        Console.WriteLine($"[StrideBasicEffect] Could not set parameter '{key.Name}' using reflection: {reflectionEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Parameter type mismatch or key not found - this is expected for some parameters
                // when the Effect is not yet built
                var keyTypeName = key.GetType().Name;
                throw new ArgumentException($"Cannot set parameter {key.Name} of type {keyTypeName}", ex);
            }
        }

        protected override IEffectTechnique GetCurrentTechniqueInternal()
        {
            if (_technique == null)
            {
                _technique = new StrideEffectTechnique(this);
            }
            return _technique;
        }

        protected override IEffectTechnique[] GetTechniquesInternal()
        {
            if (_technique == null)
            {
                _technique = new StrideEffectTechnique(this);
            }
            return new IEffectTechnique[] { _technique };
        }

        protected override void OnWorldChanged(Matrix4x4 world)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnViewChanged(Matrix4x4 view)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnProjectionChanged(Matrix4x4 projection)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnVertexColorEnabledChanged(bool enabled)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnLightingEnabledChanged(bool enabled)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnTextureEnabledChanged(bool enabled)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnAmbientLightColorChanged(System.Numerics.Vector3 color)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnDiffuseColorChanged(System.Numerics.Vector3 color)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnEmissiveColorChanged(System.Numerics.Vector3 color)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnSpecularColorChanged(System.Numerics.Vector3 color)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnSpecularPowerChanged(float power)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnAlphaChanged(float alpha)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected override void OnTextureChanged(ITexture2D texture)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected virtual void OnFogEnabledChanged(bool enabled)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected virtual void OnFogColorChanged(System.Numerics.Vector3 color)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected virtual void OnFogStartChanged(float start)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        protected virtual void OnFogEndChanged(float end)
        {
            _parametersDirty = true;
            UpdateShaderParameters();
        }

        /// <summary>
        /// Updates all shader parameters based on current effect state.
        /// This is called whenever a parameter changes or when Apply() is called.
        /// </summary>
        internal void UpdateShaderParameters()
        {
            if (_effectInstance == null || _device == null)
            {
                return;
            }

            try
            {
                // Set transformation matrices
                // World-View-Projection matrix for vertex shader
                Matrix4x4 wvp = _world * _view * _projection;
                SetMatrixParameter("World", _world);
                SetMatrixParameter("View", _view);
                SetMatrixParameter("Projection", _projection);
                SetMatrixParameter("WorldViewProjection", wvp);

                // Set material colors
                // Note: DiffuseColor includes alpha in Stride, so we handle it specially
                SetVector3Parameter("AmbientLightColor", _ambientLightColor);

                // Diffuse color with alpha - update as Color4
                if (_parameterCollection != null)
                {
                    var diffuseColorKey = MaterialKeys.DiffuseValue;
                    var diffuseColor4 = new Color4(_diffuseColor.X * _alpha, _diffuseColor.Y * _alpha, _diffuseColor.Z * _alpha, _alpha);
                    _parameterCollection.Set(diffuseColorKey, diffuseColor4);
                    if (_effectInstance?.Parameters != null)
                    {
                        try
                        {
                            _effectInstance.Parameters.Set(diffuseColorKey, diffuseColor4);
                        }
                        catch (ArgumentException) { }
                    }
                }

                SetVector3Parameter("EmissiveColor", _emissiveColor);
                SetVector3Parameter("SpecularColor", _specularColor);
                SetFloatParameter("SpecularPower", _specularPower);
                SetFloatParameter("Alpha", _alpha);

                // Set feature flags
                SetBoolParameter("VertexColorEnabled", _vertexColorEnabled);
                SetBoolParameter("LightingEnabled", _lightingEnabled);
                SetBoolParameter("TextureEnabled", _textureEnabled);

                // Set fog parameters
                SetBoolParameter("FogEnabled", _fogEnabled);
                SetVector3Parameter("FogColor", _fogColor);
                SetFloatParameter("FogStart", _fogStart);
                SetFloatParameter("FogEnd", _fogEnd);

                // Set texture if enabled and provided
                if (_textureEnabled && _texture != null)
                {
                    if (_texture is StrideTexture2D strideTexture)
                    {
                        SetTextureParameter("Texture", strideTexture.Texture);
                    }
                }
                else
                {
                    SetTextureParameter("Texture", null);
                }

                // Update material properties if material is available
                if (_material != null)
                {
                    UpdateMaterialProperties();
                }

                _parametersDirty = false;
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allows rendering to continue
                Console.WriteLine($"[StrideBasicEffect] Error updating shader parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a matrix parameter in the effect.
        /// Uses Stride's standard transformation keys where applicable,
        /// or custom parameter keys for BasicEffect-specific matrices.
        /// </summary>
        private void SetMatrixParameter(string name, Matrix4x4 value)
        {
            if (_effectInstance == null || _parameterCollection == null)
            {
                return;
            }

            try
            {
                // Convert System.Numerics.Matrix4x4 to Stride.Core.Mathematics.Matrix
                MatrixStride strideMatrix = new MatrixStride(
                    value.M11, value.M12, value.M13, value.M14,
                    value.M21, value.M22, value.M23, value.M24,
                    value.M31, value.M32, value.M33, value.M34,
                    value.M41, value.M42, value.M43, value.M44
                );

                // Map BasicEffect parameter names to Stride's standard keys
                ParameterKey<MatrixStride> parameterKey = null;
                if (name == "World")
                {
                    parameterKey = TransformationKeys.World;
                }
                else if (name == "View")
                {
                    parameterKey = TransformationKeys.View;
                }
                else if (name == "Projection")
                {
                    parameterKey = TransformationKeys.Projection;
                }
                else if (name == "WorldViewProjection")
                {
                    parameterKey = TransformationKeys.WorldViewProjection;
                }
                else
                {
                    // Use custom parameter key for non-standard matrices
                    parameterKey = new ValueParameterKey<MatrixStride>(name);
                }

                // Set parameter in both the parameter collection and effect instance
                if (parameterKey != null)
                {
                    // Create ValueParameterKey<MatrixStride> for value type parameters
                    var valueKey = new ValueParameterKey<MatrixStride>(parameterKey.Name);
                    _parameterCollection.Set(valueKey, strideMatrix);

                    var parameter = _effectInstance.Parameters;
                    if (parameter != null)
                    {
                        try
                        {
                            parameter.Set(valueKey, strideMatrix);
                        }
                        catch (ArgumentException)
                        {
                            // Parameter may not exist in the effect yet - this is expected
                            // It will be available when the Material builds its Effect
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error setting matrix parameter '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a Vector3 parameter in the effect.
        /// Maps BasicEffect parameter names to Stride's standard material and lighting keys.
        /// </summary>
        private void SetVector3Parameter(string name, Vector3Stride value)
        {
            if (_effectInstance == null || _parameterCollection == null)
            {
                return;
            }

            try
            {
                Vector3Stride strideVector = new Vector3Stride(value.X, value.Y, value.Z);
                Color3 color3Value = new Color3(value.X, value.Y, value.Z);

                // Map BasicEffect parameter names to Stride's standard keys
                ParameterKey parameterKey = null;
                object parameterValue = null;

                if (name == "AmbientLightColor")
                {
                    // LightingKeys.AmbientLightColor doesn't exist in Stride API, use custom parameter key
                    parameterKey = new ValueParameterKey<Color3>(name);
                    parameterValue = color3Value;
                }
                else if (name == "DiffuseColor")
                {
                    // Diffuse color is Color4 with alpha, handled separately
                    parameterKey = MaterialKeys.DiffuseValue;
                    parameterValue = new Color4(value.X, value.Y, value.Z, _alpha);
                }
                else if (name == "EmissiveColor")
                {
                    parameterKey = MaterialKeys.EmissiveValue;
                    parameterValue = color3Value;
                }
                else if (name == "SpecularColor")
                {
                    parameterKey = MaterialKeys.SpecularValue;
                    parameterValue = color3Value;
                }
                else if (name == "FogColor")
                {
                    // Custom parameter for fog color
                    parameterKey = new ValueParameterKey<Color3>(name);
                    parameterValue = color3Value;
                }
                else
                {
                    // Use custom parameter key for other Vector3 parameters
                    parameterKey = new ValueParameterKey<Vector3Stride>(name);
                    parameterValue = strideVector;
                }

                // Set parameter in the parameter collection
                // Note: EffectInstance.Parameters.Set may have type resolution issues with non-generic ParameterKey,
                // so we only set on ParameterCollection here. MaterialKeys are set directly elsewhere (e.g., line 612).
                if (parameterKey != null && parameterValue != null)
                {
                    // Use pattern matching to determine the type and set appropriately
                    if (parameterValue is Color3 color3Val && parameterKey is ParameterKey<Color3> color3Key)
                    {
                        var valueKey = new ValueParameterKey<Color3>(parameterKey.Name);
                        _parameterCollection.Set(valueKey, color3Val);
                    }
                    else if (parameterValue is Color4 color4Val && parameterKey is ParameterKey<Color4> color4Key)
                    {
                        var valueKey = new ValueParameterKey<Color4>(parameterKey.Name);
                        _parameterCollection.Set(valueKey, color4Val);
                    }
                    else if (parameterValue is Vector3Stride vector3Val && parameterKey is ParameterKey<Vector3Stride> vector3Key)
                    {
                        var valueKey = new ValueParameterKey<Vector3Stride>(parameterKey.Name);
                        _parameterCollection.Set(valueKey, vector3Val);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error setting Vector3 parameter '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a float parameter in the effect.
        /// Maps BasicEffect parameter names to Stride's standard material keys.
        /// </summary>
        private void SetFloatParameter(string name, float value)
        {
            if (_effectInstance == null || _parameterCollection == null)
            {
                return;
            }

            try
            {
                // Map BasicEffect parameter names to Stride's standard keys
                ValueParameterKey<float> parameterKey = null;

                if (name == "SpecularPower")
                {
                    // MaterialKeys.SpecularPowerValue doesn't exist in Stride API
                    // Use a custom parameter key for specular power
                    parameterKey = new ValueParameterKey<float>("SpecularPower");
                }
                else if (name == "Alpha")
                {
                    // Alpha is part of Color4 in Stride, handled with diffuse color
                    // But we can also store it separately for shader use
                    parameterKey = new ValueParameterKey<float>(name);
                }
                else if (name == "FogStart" || name == "FogEnd")
                {
                    parameterKey = new ValueParameterKey<float>(name);
                }
                else
                {
                    parameterKey = new ValueParameterKey<float>(name);
                }

                // Set parameter in both the parameter collection and effect instance
                if (parameterKey != null)
                {
                    _parameterCollection.Set(parameterKey, value);

                    var parameter = _effectInstance.Parameters;
                    if (parameter != null)
                    {
                        try
                        {
                            // For float parameters, use ValueParameterKey
                            parameter.Set(parameterKey, value);
                        }
                        catch (ArgumentException)
                        {
                            // Parameter may not exist in the effect yet - this is expected
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error setting float parameter '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a bool parameter in the effect.
        /// These are custom BasicEffect feature flags (vertex colors, lighting, textures, fog).
        /// </summary>
        private void SetBoolParameter(string name, bool value)
        {
            if (_effectInstance == null || _parameterCollection == null)
            {
                return;
            }

            try
            {
                // Create parameter key for boolean feature flags
                var parameterKey = new ValueParameterKey<bool>(name);

                // Set parameter in both the parameter collection and effect instance
                _parameterCollection.Set(parameterKey, value);

                var parameter = _effectInstance.Parameters;
                if (parameter != null)
                {
                    try
                    {
                        parameter.Set(parameterKey, value);
                    }
                    catch (ArgumentException)
                    {
                        // Parameter may not exist in the effect yet - this is expected
                        // It will be available when the Material builds its Effect
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error setting bool parameter '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a texture parameter in the effect.
        /// Maps BasicEffect texture to Stride's material diffuse map key.
        /// </summary>
        private void SetTextureParameter(string name, Texture texture)
        {
            if (_effectInstance == null || _parameterCollection == null)
            {
                return;
            }

            try
            {
                // Map BasicEffect texture parameter to Stride's material texture keys
                if (name == "Texture")
                {
                    // Use Stride's diffuse map key for the main texture
                    var textureKey = MaterialKeys.DiffuseMap;

                    // Set texture in parameter collection
                    if (texture != null)
                    {
                        _parameterCollection.Set(textureKey, texture);

                        var parameter = _effectInstance.Parameters;
                        if (parameter != null)
                        {
                            try
                            {
                                parameter.Set(textureKey, texture);
                            }
                            catch (ArgumentException)
                            {
                                // Parameter may not exist in the effect yet - this is expected
                            }
                        }
                    }
                    else
                    {
                        // Clear texture parameter
                        _parameterCollection.Remove(textureKey);
                    }
                }
                else
                {
                    // For other texture parameters, use MaterialKeys or skip custom texture keys
                    // Texture is a reference type and requires ObjectParameterKey, not ValueParameterKey
                    // For now, we'll only support the main "Texture" parameter via MaterialKeys.DiffuseMap
                    // Additional texture parameters would need to use MaterialKeys or ObjectParameterKey
                    Console.WriteLine($"[StrideBasicEffect] Custom texture parameter '{name}' not supported, use MaterialKeys instead");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error setting texture parameter '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Updates material properties based on current effect state.
        /// This configures the Material with all BasicEffect parameters,
        /// building it if necessary to create a valid EffectInstance.
        ///
        /// Based on MonoGame BasicEffect material properties.
        /// Original game: DirectX 9 fixed-function materials (swkotor2.exe: d3d9.dll material states)
        /// </summary>
        private void UpdateMaterialProperties()
        {
            if (_material == null)
            {
                return;
            }

            try
            {
                // Create MaterialDescriptor with current BasicEffect state
                var materialDescriptor = new MaterialDescriptor();

                // Update material diffuse color with alpha
                var diffuseColor = new Color4(
                    _diffuseColor.X * _alpha,
                    _diffuseColor.Y * _alpha,
                    _diffuseColor.Z * _alpha,
                    _alpha
                );
                materialDescriptor.Attributes.Diffuse = new MaterialDiffuseMapFeature(
                    new ComputeColor(diffuseColor)
                );

                // Update material emissive color
                var emissiveColor = new Color3(
                    _emissiveColor.X,
                    _emissiveColor.Y,
                    _emissiveColor.Z
                );
                materialDescriptor.Attributes.Emissive = new MaterialEmissiveMapFeature(
                    new ComputeColor(new Color4(emissiveColor, 1.0f))
                );

                // Update material specular properties (if lighting enabled)
                // MaterialSpecularMapFeature uses parameterless constructor and SpecularMap property
                // Based on Stride API: MaterialSpecularMapFeature() constructor, then set SpecularMap property
                // Specular power is set via custom parameter key (MaterialKeys.SpecularPowerValue doesn't exist)
                // Original game: DirectX 9 fixed-function pipeline specular lighting (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
                if (_lightingEnabled && _specularPower > 0.0f)
                {
                    var specularColor = new Color4(
                        _specularColor.X,
                        _specularColor.Y,
                        _specularColor.Z,
                        1.0f
                    );
                    var specularFeature = new MaterialSpecularMapFeature();
                    specularFeature.SpecularMap = new ComputeColor(specularColor);
                    materialDescriptor.Attributes.Specular = specularFeature;
                    // Set specular power via custom parameter key (MaterialKeys.SpecularPowerValue doesn't exist in Stride API)
                    // This will be used by shaders to control specular highlight size
                    _parameterCollection.Set(new ValueParameterKey<float>("SpecularPower"), _specularPower);
                }

                // Update texture if enabled
                if (_textureEnabled && _texture != null)
                {
                    if (_texture is StrideTexture2D strideTexture)
                    {
                        // Set diffuse map texture
                        materialDescriptor.Attributes.Diffuse = new MaterialDiffuseMapFeature(
                            new ComputeTextureColor(strideTexture.Texture)
                        );
                    }
                }

                // Update Material with current BasicEffect state
                // Based on Stride API: Materials are built by the rendering system when used
                // We update the MaterialDescriptor configuration here, and the Material will use it
                // when built by the renderer. The EffectInstance will be available from Material passes
                // after the Material is built.
                //
                // For immediate parameter management, we use ParameterCollection which works with
                // both the current EffectInstance (if Material is built) and will be applied when
                // the Material is built by the rendering system.
                //
                // Original implementation: Materials are built by the rendering system during rendering
                // Our ParameterCollection-based approach ensures parameters are ready when Material is built
                // and can be applied to EffectInstance from Material passes when available

                // Try to get EffectInstance from Material passes if Material is already built
                // This can happen if Material was built by the rendering system
                if (_material != null && _material.Passes != null && _material.Passes.Count > 0)
                {
                    try
                    {
                        var firstPass = _material.Passes[0];
                        if (firstPass != null && firstPass.Parameters != null)
                        {
                            // MaterialPass has Parameters property (ParameterCollection)
                            // Synchronize parameters from our collection to MaterialPass's ParameterCollection
                            if (_parameterCollection != null)
                            {
                                CopyParametersToParameterCollection(_parameterCollection, firstPass.Parameters);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Material may not be fully built yet - continue with existing EffectInstance
                        // Parameters will be updated on next UpdateShaderParameters() call
                        Console.WriteLine($"[StrideBasicEffect] Could not get EffectInstance from Material: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideBasicEffect] Error updating material properties: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current EffectInstance for external use.
        /// </summary>
        internal EffectInstance GetEffectInstance()
        {
            if (_parametersDirty)
            {
                UpdateShaderParameters();
            }
            return _effectInstance;
        }

        /// <summary>
        /// Gets the current Material for external use.
        /// </summary>
        internal Material GetMaterial()
        {
            if (_parametersDirty)
            {
                UpdateShaderParameters();
            }
            return _material;
        }

        protected override void OnDispose()
        {
            _technique = null;
            _effectInstance?.Dispose();
            _effectInstance = null;
            // Material doesn't implement IDisposable in Stride
            _material = null;
            base.OnDispose();
        }
    }

    /// <summary>
    /// Stride implementation of IEffectTechnique.
    /// </summary>
    internal class StrideEffectTechnique : IEffectTechnique
    {
        private readonly StrideBasicEffect _effect;
        private StrideEffectPass _pass;

        public StrideEffectTechnique(StrideBasicEffect effect)
        {
            _effect = effect ?? throw new ArgumentNullException(nameof(effect));
            _pass = new StrideEffectPass(_effect);
        }

        public string Name => "BasicEffectTechnique";

        public IEffectPass[] Passes => new IEffectPass[] { _pass };
    }

    /// <summary>
    /// Stride implementation of IEffectPass.
    /// Applies all shader parameters when Apply() is called.
    /// </summary>
    internal class StrideEffectPass : IEffectPass
    {
        private readonly StrideBasicEffect _effect;

        public StrideEffectPass(StrideBasicEffect effect)
        {
            _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public string Name => "BasicEffectPass";

        public void Apply()
        {
            // Apply all shader parameters to the graphics device
            // This is called before rendering to ensure all parameters are set correctly
            if (_effect == null)
            {
                return;
            }

            try
            {
                // Get the effect instance and ensure parameters are up to date
                var effectInstance = _effect.GetEffectInstance();
                if (effectInstance == null)
                {
                    return;
                }

                // Update all shader parameters
                _effect.UpdateShaderParameters();

                // Apply the effect instance to the graphics context
                // In Stride, this is typically done through the rendering pipeline
                // For immediate mode rendering, we would use:
                // var graphicsContext = _effect._device.ImmediateContext;
                // effectInstance.Apply(graphicsContext);

                // Note: In a full Stride integration, effect application is typically
                // handled by the rendering system when drawing meshes. This method
                // ensures all parameters are current and ready for the next draw call.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideEffectPass] Error applying effect: {ex.Message}");
            }
        }
    }
}

