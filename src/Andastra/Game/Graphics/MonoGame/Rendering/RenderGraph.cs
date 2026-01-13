using System;
using System.Collections.Generic;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Render graph for modern rendering architecture.
    /// 
    /// Render graphs define rendering passes and their dependencies,
    /// enabling:
    /// - Automatic resource management
    /// - Optimal pass ordering
    /// - Resource lifetime tracking
    /// - Multi-threaded pass execution
    /// 
    /// Based on modern AAA game render graph architectures (Frostbite, Unreal, etc.).
    /// </summary>
    public class RenderGraph
    {
        /// <summary>
        /// Render pass node.
        /// </summary>
        public class RenderPass
        {
            /// <summary>
            /// Pass name/identifier.
            /// </summary>
            public string Name;

            /// <summary>
            /// Pass execution function.
            /// </summary>
            public Action<RenderContext> Execute;

            /// <summary>
            /// Input resources (read).
            /// </summary>
            public List<string> Inputs;

            /// <summary>
            /// Output resources (written).
            /// </summary>
            public List<string> Outputs;

            /// <summary>
            /// Pass dependencies (must execute after these passes).
            /// </summary>
            public List<string> Dependencies;

            public RenderPass(string name)
            {
                Name = name;
                Inputs = new List<string>();
                Outputs = new List<string>();
                Dependencies = new List<string>();
            }
        }

        /// <summary>
        /// Render context for pass execution.
        /// </summary>
        public class RenderContext
        {
            private readonly Dictionary<string, object> _resources;

            public RenderContext()
            {
                _resources = new Dictionary<string, object>();
            }

            public T GetResource<T>(string name) where T : class
            {
                object resource;
                if (_resources.TryGetValue(name, out resource))
                {
                    return resource as T;
                }
                return null;
            }

            public void SetResource(string name, object resource)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("Resource name must not be null or empty.", nameof(name));
                }
                _resources[name] = resource;
            }
        }

        private readonly Dictionary<string, RenderPass> _passes;
        private readonly List<string> _executionOrder;

        /// <summary>
        /// Initializes a new render graph.
        /// </summary>
        public RenderGraph()
        {
            _passes = new Dictionary<string, RenderPass>();
            _executionOrder = new List<string>();
        }

        /// <summary>
        /// Adds a render pass to the graph.
        /// </summary>
        public void AddPass(RenderPass pass)
        {
            if (pass == null)
            {
                throw new ArgumentNullException(nameof(pass));
            }

            _passes[pass.Name] = pass;
        }

        /// <summary>
        /// Compiles the render graph, determining execution order.
        /// </summary>
        public void Compile()
        {
            _executionOrder.Clear();

            // Topological sort to determine execution order
            HashSet<string> visited = new HashSet<string>();
            HashSet<string> visiting = new HashSet<string>();

            foreach (string passName in _passes.Keys)
            {
                if (!visited.Contains(passName))
                {
                    TopologicalSort(passName, visited, visiting);
                }
            }
        }

        private void TopologicalSort(string passName, HashSet<string> visited, HashSet<string> visiting)
        {
            if (visiting.Contains(passName))
            {
                // Circular dependency detected
                throw new InvalidOperationException(string.Format("Circular dependency detected involving pass: {0}", passName));
            }

            if (visited.Contains(passName))
            {
                return;
            }

            visiting.Add(passName);

            RenderPass pass;
            if (!_passes.TryGetValue(passName, out pass))
            {
                visiting.Remove(passName);
                return; // Skip missing passes
            }

            foreach (string dep in pass.Dependencies)
            {
                TopologicalSort(dep, visited, visiting);
            }

            visiting.Remove(passName);
            visited.Add(passName);
            _executionOrder.Add(passName);
        }

        /// <summary>
        /// Executes the render graph.
        /// </summary>
        /// <param name="context">Render context for pass execution. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
        public void Execute(RenderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            foreach (string passName in _executionOrder)
            {
                RenderPass pass;
                if (!_passes.TryGetValue(passName, out pass))
                {
                    continue; // Skip missing passes
                }
                if (pass.Execute != null)
                {
                    pass.Execute.Invoke(context);
                }
            }
        }

        /// <summary>
        /// Clears all passes.
        /// </summary>
        public void Clear()
        {
            _passes.Clear();
            _executionOrder.Clear();
        }
    }
}

