using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Materials
{
    /// <summary>
    /// Material instancing system for efficient material management.
    /// 
    /// Material instancing allows multiple objects to share material data
    /// while having per-instance parameters, reducing memory usage and
    /// enabling efficient batching.
    /// 
    /// Features:
    /// - Shared material templates
    /// - Per-instance parameters
    /// - Material parameter batching
    /// - Automatic material sorting
    /// </summary>
    public class MaterialInstancing
    {
        /// <summary>
        /// Material template (shared data).
        /// </summary>
        public class MaterialTemplate
        {
            public uint TemplateId;
            public Texture2D AlbedoTexture;
            public Texture2D NormalTexture;
            public Texture2D RoughnessMetallicTexture;
            public Texture2D EmissiveTexture;
            public Vector4 BaseColor;
            public float Roughness;
            public float Metallic;
            public Vector3 EmissiveColor;
        }

        /// <summary>
        /// Material instance (per-object data).
        /// </summary>
        public struct MaterialInstance
        {
            public uint TemplateId;
            public Vector4 ColorTint;
            public float RoughnessOverride;
            public float MetallicOverride;
            public Vector3 EmissiveOverride;
        }

        private readonly Dictionary<uint, MaterialTemplate> _templates;
        private readonly Dictionary<uint, MaterialInstance> _instances;
        private uint _nextTemplateId;
        private uint _nextInstanceId;

        /// <summary>
        /// Gets the number of material templates.
        /// </summary>
        public int TemplateCount
        {
            get { return _templates.Count; }
        }

        /// <summary>
        /// Gets the number of material instances.
        /// </summary>
        public int InstanceCount
        {
            get { return _instances.Count; }
        }

        /// <summary>
        /// Initializes a new material instancing system.
        /// </summary>
        public MaterialInstancing()
        {
            _templates = new Dictionary<uint, MaterialTemplate>();
            _instances = new Dictionary<uint, MaterialInstance>();
            _nextTemplateId = 1;
            _nextInstanceId = 1;
        }

        /// <summary>
        /// Creates a material template.
        /// </summary>
        public uint CreateTemplate(MaterialTemplate template)
        {
            template.TemplateId = _nextTemplateId++;
            _templates[template.TemplateId] = template;
            return template.TemplateId;
        }

        /// <summary>
        /// Creates a material instance from a template.
        /// </summary>
        public uint CreateInstance(uint templateId, MaterialInstance instance)
        {
            if (!_templates.ContainsKey(templateId))
            {
                return 0;
            }

            instance.TemplateId = templateId;
            uint instanceId = _nextInstanceId++;
            _instances[instanceId] = instance;
            return instanceId;
        }

        /// <summary>
        /// Gets a material template.
        /// </summary>
        public MaterialTemplate GetTemplate(uint templateId)
        {
            MaterialTemplate template;
            _templates.TryGetValue(templateId, out template);
            return template;
        }

        /// <summary>
        /// Gets a material instance.
        /// </summary>
        public MaterialInstance GetInstance(uint instanceId)
        {
            MaterialInstance instance;
            _instances.TryGetValue(instanceId, out instance);
            return instance;
        }

        /// <summary>
        /// Batches material instances by template for efficient rendering.
        /// </summary>
        public Dictionary<uint, List<uint>> BatchInstancesByTemplate()
        {
            var batches = new Dictionary<uint, List<uint>>();

            foreach (var kvp in _instances)
            {
                uint instanceId = kvp.Key;
                uint templateId = kvp.Value.TemplateId;

                List<uint> batch;
                if (!batches.TryGetValue(templateId, out batch))
                {
                    batch = new List<uint>();
                    batches[templateId] = batch;
                }

                batch.Add(instanceId);
            }

            return batches;
        }
    }
}

