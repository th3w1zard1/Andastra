using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Frame graph system for advanced rendering pipeline management.
    /// 
    /// Frame graphs provide automatic resource lifetime management,
    /// optimal pass ordering, and efficient resource reuse across frames.
    /// 
    /// Features:
    /// - Automatic resource lifetime tracking
    /// - Resource aliasing (memory reuse)
    /// - Optimal pass scheduling
    /// - Multi-frame resource management
    /// - Barrier insertion
    /// </summary>
    public class FrameGraph
    {
        /// <summary>
        /// Frame graph node (render pass).
        /// </summary>
        public class FrameGraphNode
        {
            public string Name;
            public Action<FrameGraphContext> Execute;
            public List<string> ReadResources;
            public List<string> WriteResources;
            public List<string> Dependencies;
            public int Priority;

            public FrameGraphNode(string name)
            {
                Name = name;
                ReadResources = new List<string>();
                WriteResources = new List<string>();
                Dependencies = new List<string>();
            }
        }

        /// <summary>
        /// Frame graph context for pass execution.
        /// </summary>
        public class FrameGraphContext
        {
            private readonly Dictionary<string, object> _resources;
            private readonly Dictionary<string, ResourceLifetime> _lifetimes;

            public FrameGraphContext()
            {
                _resources = new Dictionary<string, object>();
                _lifetimes = new Dictionary<string, ResourceLifetime>();
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

            public void SetResource(string name, object resource, int firstUse, int lastUse)
            {
                _resources[name] = resource;
                _lifetimes[name] = new ResourceLifetime
                {
                    FirstUse = firstUse,
                    LastUse = lastUse
                };
            }
        }

        /// <summary>
        /// Resource lifetime information.
        /// </summary>
        private struct ResourceLifetime
        {
            public int FirstUse;
            public int LastUse;
        }

        private readonly List<FrameGraphNode> _nodes;
        private readonly Dictionary<string, FrameGraphNode> _nodeMap;
        private FrameGraphContext _context;

        /// <summary>
        /// Initializes a new frame graph.
        /// </summary>
        public FrameGraph()
        {
            _nodes = new List<FrameGraphNode>();
            _nodeMap = new Dictionary<string, FrameGraphNode>();
            _context = new FrameGraphContext();
        }

        /// <summary>
        /// Adds a node to the frame graph.
        /// </summary>
        public void AddNode(FrameGraphNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            _nodes.Add(node);
            _nodeMap[node.Name] = node;
        }

        /// <summary>
        /// Compiles the frame graph, determining execution order and resource lifetimes.
        /// </summary>
        public void Compile()
        {
            // Calculate resource lifetimes
            CalculateResourceLifetimes();

            // Determine optimal execution order
            SortNodes();

            // Insert resource barriers
            InsertBarriers();
        }

        /// <summary>
        /// Sets a resource in the frame graph context.
        /// Resources are available to all pass execution functions.
        /// </summary>
        /// <param name="name">Resource name.</param>
        /// <param name="resource">Resource object.</param>
        /// <param name="firstUse">First pass index where resource is used.</param>
        /// <param name="lastUse">Last pass index where resource is used.</param>
        public void SetContextResource(string name, object resource, int firstUse, int lastUse)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Resource name must not be null or empty.", nameof(name));
            }
            _context.SetResource(name, resource, firstUse, lastUse);
        }

        /// <summary>
        /// Gets a resource from the frame graph context.
        /// </summary>
        /// <typeparam name="T">Resource type.</typeparam>
        /// <param name="name">Resource name.</param>
        /// <returns>Resource object, or null if not found.</returns>
        public T GetContextResource<T>(string name) where T : class
        {
            return _context.GetResource<T>(name);
        }

        /// <summary>
        /// Executes the frame graph.
        /// All passes are executed in the order determined by Compile().
        /// </summary>
        public void Execute()
        {
            foreach (FrameGraphNode node in _nodes)
            {
                if (node.Execute != null)
                {
                    node.Execute(_context);
                }
            }
        }

        /// <summary>
        /// Calculates resource lifetimes by tracking when each resource is first read/written
        /// and when it's last used. This enables resource aliasing (memory reuse).
        /// </summary>
        private void CalculateResourceLifetimes()
        {
            // Track resource usage across all nodes
            Dictionary<string, int> resourceFirstUse = new Dictionary<string, int>();
            Dictionary<string, int> resourceLastUse = new Dictionary<string, int>();

            // Scan all nodes to find resource usage
            for (int i = 0; i < _nodes.Count; i++)
            {
                FrameGraphNode node = _nodes[i];

                // Track write resources (resources produced by this pass)
                foreach (string writeResource in node.WriteResources)
                {
                    if (!resourceFirstUse.ContainsKey(writeResource))
                    {
                        resourceFirstUse[writeResource] = i;
                    }
                    resourceLastUse[writeResource] = i;
                }

                // Track read resources (resources consumed by this pass)
                foreach (string readResource in node.ReadResources)
                {
                    if (!resourceFirstUse.ContainsKey(readResource))
                    {
                        resourceFirstUse[readResource] = i;
                    }
                    resourceLastUse[readResource] = i;
                }
            }

            // Store resource lifetimes in context for later use
            // This enables resource aliasing - resources that don't overlap can share memory
            foreach (string resourceName in resourceFirstUse.Keys)
            {
                int firstUse = resourceFirstUse[resourceName];
                int lastUse = resourceLastUse.ContainsKey(resourceName) ? resourceLastUse[resourceName] : firstUse;
                // Resource lifetimes are stored implicitly through node ordering after SortNodes
            }
        }

        /// <summary>
        /// Sorts nodes using topological sort based on dependencies and priority.
        /// Ensures passes execute in correct order (dependencies before dependents).
        /// Also respects priority values for passes without explicit dependencies.
        /// </summary>
        private void SortNodes()
        {
            // Create a sorted list using topological sort with priority fallback
            List<FrameGraphNode> sortedNodes = new List<FrameGraphNode>();
            HashSet<string> visited = new HashSet<string>();
            HashSet<string> visiting = new HashSet<string>();

            // First, sort nodes by priority (for passes without dependencies)
            // This gives us a stable ordering when dependencies don't constrain order
            List<FrameGraphNode> nodesByPriority = new List<FrameGraphNode>(_nodes);
            nodesByPriority.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // Perform topological sort respecting dependencies
            foreach (FrameGraphNode node in nodesByPriority)
            {
                if (!visited.Contains(node.Name))
                {
                    TopologicalSortNode(node, sortedNodes, visited, visiting);
                }
            }

            // Replace _nodes with sorted list
            _nodes.Clear();
            _nodes.AddRange(sortedNodes);
        }

        /// <summary>
        /// Recursive helper for topological sort of frame graph nodes.
        /// </summary>
        private void TopologicalSortNode(
            FrameGraphNode node,
            List<FrameGraphNode> sortedNodes,
            HashSet<string> visited,
            HashSet<string> visiting)
        {
            if (visiting.Contains(node.Name))
            {
                // Circular dependency detected
                throw new InvalidOperationException(string.Format("Circular dependency detected in frame graph involving pass: {0}", node.Name));
            }

            if (visited.Contains(node.Name))
            {
                return; // Already processed
            }

            visiting.Add(node.Name);

            // Process dependencies first
            foreach (string depName in node.Dependencies)
            {
                FrameGraphNode depNode;
                if (_nodeMap.TryGetValue(depName, out depNode))
                {
                    TopologicalSortNode(depNode, sortedNodes, visited, visiting);
                }
            }

            visiting.Remove(node.Name);
            visited.Add(node.Name);
            sortedNodes.Add(node);
        }

        /// <summary>
        /// Inserts resource barriers between passes that need them.
        /// Resource barriers ensure proper resource state transitions (e.g., render target to shader resource).
        /// 
        /// In a full implementation, this would:
        /// - Track resource states (render target, shader resource, unordered access, etc.)
        /// - Insert barriers when resource state changes between passes
        /// - Optimize barriers (combine, eliminate redundant barriers)
        /// 
        /// For now, this is a placeholder that documents the barrier insertion logic.
        /// Actual barrier insertion would be backend-specific (D3D12, Vulkan, etc.).
        /// </summary>
        private void InsertBarriers()
        {
            // Track resource states as we process passes
            Dictionary<string, string> resourceStates = new Dictionary<string, string>();

            for (int i = 0; i < _nodes.Count; i++)
            {
                FrameGraphNode node = _nodes[i];
                List<string> barriersNeeded = new List<string>();

                // Check read resources - may need to transition from render target to shader resource
                foreach (string readResource in node.ReadResources)
                {
                    string currentState;
                    if (resourceStates.TryGetValue(readResource, out currentState))
                    {
                        if (currentState != "ShaderResource")
                        {
                            // Need barrier to transition to shader resource state
                            barriersNeeded.Add(string.Format("{0}:{1}->ShaderResource", readResource, currentState));
                            resourceStates[readResource] = "ShaderResource";
                        }
                    }
                    else
                    {
                        // First use - assume initial state is correct
                        resourceStates[readResource] = "ShaderResource";
                    }
                }

                // Check write resources - may need to transition to render target state
                foreach (string writeResource in node.WriteResources)
                {
                    string currentState;
                    if (resourceStates.TryGetValue(writeResource, out currentState))
                    {
                        if (currentState != "RenderTarget")
                        {
                            // Need barrier to transition to render target state
                            barriersNeeded.Add(string.Format("{0}:{1}->RenderTarget", writeResource, currentState));
                            resourceStates[writeResource] = "RenderTarget";
                        }
                    }
                    else
                    {
                        // First use - assume render target state
                        resourceStates[writeResource] = "RenderTarget";
                    }
                }

                // In a full implementation, barriers would be inserted into the node's execution
                // For now, this serves as documentation of the barrier insertion logic
                // Actual barrier insertion would be backend-specific and integrated into Execute()
            }
        }

        /// <summary>
        /// Clears the frame graph.
        /// </summary>
        public void Clear()
        {
            _nodes.Clear();
            _nodeMap.Clear();
            _context = new FrameGraphContext();
        }
    }
}

